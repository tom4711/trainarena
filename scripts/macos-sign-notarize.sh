#!/usr/bin/env bash
# Wrap publish output in TrainArena.app, Developer-ID-sign, notarize, and staple.
# Layout: MacOS/ = executable only; Resources/ = wwwroot + config (ContentRoot).
# Required env:
#   APPLE_CERTIFICATE_BASE64, APPLE_CERTIFICATE_PASSWORD, APPLE_SIGNING_IDENTITY
#   APPLE_API_KEY_BASE64, APPLE_API_KEY_ID, APPLE_API_ISSUER_ID
# Optional: APP_VERSION (defaults to 0.0.0)
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <publish-dir>" >&2
  exit 2
fi

PUBLISH_DIR=$(cd "$1" && pwd)
BINARY="$PUBLISH_DIR/TrainArena"
ROOT=$(cd "$(dirname "$0")/.." && pwd)
ENTITLEMENTS="${ENTITLEMENTS_PATH:-$ROOT/docs/macos/TrainArena.entitlements}"
PLIST_TEMPLATE="$ROOT/docs/macos/Info.plist.template"
APP_VERSION="${APP_VERSION:-0.0.0}"
APP_BUNDLE="$PUBLISH_DIR/TrainArena.app"

for var in APPLE_CERTIFICATE_BASE64 APPLE_CERTIFICATE_PASSWORD APPLE_SIGNING_IDENTITY \
           APPLE_API_KEY_BASE64 APPLE_API_KEY_ID APPLE_API_ISSUER_ID; do
  if [[ -z "${!var:-}" ]]; then
    echo "Missing env: $var" >&2
    exit 1
  fi
done

if [[ ! -f "$BINARY" ]]; then
  echo "Binary not found: $BINARY" >&2
  exit 1
fi
if [[ ! -f "$ENTITLEMENTS" || ! -f "$PLIST_TEMPLATE" ]]; then
  echo "Missing entitlements or Info.plist template under docs/macos/" >&2
  exit 1
fi

KEYCHAIN="trainarena-ci.keychain-db"
KEYCHAIN_PW=$(uuidgen | tr '[:upper:]' '[:lower:]')
CERT_PATH=$(mktemp "${TMPDIR:-/tmp}/trainarena-cert.XXXXXX.p12")
API_KEY_PATH=$(mktemp "${TMPDIR:-/tmp}/AuthKey.XXXXXX.p8")
NOTARY_ZIP=$(mktemp "${TMPDIR:-/tmp}/trainarena-notarize.XXXXXX.zip")
STAGING=$(mktemp -d "${TMPDIR:-/tmp}/trainarena-app.XXXXXX")

cleanup() {
  security delete-keychain "$KEYCHAIN" 2>/dev/null || true
  rm -f "$CERT_PATH" "$API_KEY_PATH" "$NOTARY_ZIP"
  rm -rf "$STAGING"
}
trap cleanup EXIT

echo "$APPLE_CERTIFICATE_BASE64" | base64 --decode > "$CERT_PATH"
echo "$APPLE_API_KEY_BASE64" | base64 --decode > "$API_KEY_PATH"

security delete-keychain "$KEYCHAIN" 2>/dev/null || true
security create-keychain -p "$KEYCHAIN_PW" "$KEYCHAIN"
security set-keychain-settings -lut 21600 "$KEYCHAIN"
security unlock-keychain -p "$KEYCHAIN_PW" "$KEYCHAIN"
security import "$CERT_PATH" -k "$KEYCHAIN" -P "$APPLE_CERTIFICATE_PASSWORD" \
  -A -T /usr/bin/codesign -T /usr/bin/security -T /usr/bin/productsign

EXISTING=$(security list-keychains -d user | sed 's/"//g' | tr '\n' ' ')
security list-keychains -d user -s "$KEYCHAIN" $EXISTING
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KEYCHAIN_PW" "$KEYCHAIN"

rm -rf "$APP_BUNDLE"
MACOS_DIR="$STAGING/TrainArena.app/Contents/MacOS"
RES_DIR="$STAGING/TrainArena.app/Contents/Resources"
mkdir -p "$MACOS_DIR" "$RES_DIR"

# Executable only in MacOS/; everything else in Resources/ (required for codesign).
mv "$BINARY" "$MACOS_DIR/TrainArena"
chmod +x "$MACOS_DIR/TrainArena"
shopt -s dotglob nullglob
for item in "$PUBLISH_DIR"/*; do
  base=$(basename "$item")
  [[ "$base" == "TrainArena.app" ]] && continue
  mv "$item" "$RES_DIR/"
done
shopt -u dotglob nullglob

sed "s/__VERSION__/${APP_VERSION//\//\\/}/g" "$PLIST_TEMPLATE" \
  > "$STAGING/TrainArena.app/Contents/Info.plist"

# Sign Mach-O, then the .app bundle.
codesign --force --options runtime --timestamp \
  --keychain "$KEYCHAIN" \
  --entitlements "$ENTITLEMENTS" \
  --sign "$APPLE_SIGNING_IDENTITY" \
  "$MACOS_DIR/TrainArena"

codesign --force --options runtime --timestamp \
  --keychain "$KEYCHAIN" \
  --entitlements "$ENTITLEMENTS" \
  --sign "$APPLE_SIGNING_IDENTITY" \
  "$STAGING/TrainArena.app"

codesign --verify --deep --strict --verbose=2 "$STAGING/TrainArena.app"
codesign -dv --verbose=2 "$STAGING/TrainArena.app" 2>&1 || true

rm -f "$NOTARY_ZIP"
ditto -c -k --keepParent "$STAGING/TrainArena.app" "$NOTARY_ZIP"
xcrun notarytool submit "$NOTARY_ZIP" \
  --key "$API_KEY_PATH" \
  --key-id "$APPLE_API_KEY_ID" \
  --issuer "$APPLE_API_ISSUER_ID" \
  --wait

xcrun stapler staple "$STAGING/TrainArena.app"
xcrun stapler validate "$STAGING/TrainArena.app"

mv "$STAGING/TrainArena.app" "$APP_BUNDLE"
echo "Signed, notarized, and stapled: $APP_BUNDLE"
