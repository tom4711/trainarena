#!/usr/bin/env bash
# Sign + notarize a published TrainArena folder for Gatekeeper (Developer ID).
# Required env:
#   APPLE_CERTIFICATE_BASE64, APPLE_CERTIFICATE_PASSWORD, APPLE_SIGNING_IDENTITY
#   APPLE_API_KEY_BASE64, APPLE_API_KEY_ID, APPLE_API_ISSUER_ID
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <publish-dir>" >&2
  exit 2
fi

PUBLISH_DIR=$(cd "$1" && pwd)
BINARY="$PUBLISH_DIR/TrainArena"
ROOT=$(cd "$(dirname "$0")/.." && pwd)
ENTITLEMENTS="${ENTITLEMENTS_PATH:-$ROOT/docs/macos/TrainArena.entitlements}"

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
if [[ ! -f "$ENTITLEMENTS" ]]; then
  echo "Entitlements not found: $ENTITLEMENTS" >&2
  exit 1
fi

KEYCHAIN="trainarena-ci.keychain-db"
KEYCHAIN_PW=$(uuidgen | tr '[:upper:]' '[:lower:]')
CERT_PATH=$(mktemp "${TMPDIR:-/tmp}/trainarena-cert.XXXXXX.p12")
API_KEY_PATH=$(mktemp "${TMPDIR:-/tmp}/AuthKey.XXXXXX.p8")
NOTARY_ZIP=$(mktemp "${TMPDIR:-/tmp}/trainarena-notarize.XXXXXX.zip")

cleanup() {
  security delete-keychain "$KEYCHAIN" 2>/dev/null || true
  rm -f "$CERT_PATH" "$API_KEY_PATH" "$NOTARY_ZIP"
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

# Put CI keychain first so codesign finds the identity.
EXISTING=$(security list-keychains -d user | sed 's/"//g' | tr '\n' ' ')
security list-keychains -d user -s "$KEYCHAIN" $EXISTING
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KEYCHAIN_PW" "$KEYCHAIN"

chmod +x "$BINARY"
codesign --force --options runtime --timestamp \
  --keychain "$KEYCHAIN" \
  --entitlements "$ENTITLEMENTS" \
  --sign "$APPLE_SIGNING_IDENTITY" \
  "$BINARY"

codesign --verify --verbose=2 "$BINARY"
codesign -dv --verbose=2 "$BINARY" 2>&1 | tee /dev/stderr | grep -q "Developer ID Application"

# Notarize the whole publish folder (binary + wwwroot + config).
rm -f "$NOTARY_ZIP"
ditto -c -k --keepParent "$PUBLISH_DIR" "$NOTARY_ZIP"
xcrun notarytool submit "$NOTARY_ZIP" \
  --key "$API_KEY_PATH" \
  --key-id "$APPLE_API_KEY_ID" \
  --issuer "$APPLE_API_ISSUER_ID" \
  --wait

# Stapling applies to .app/.dmg/.pkg; bare binaries rely on notarization ticket lookup.
if xcrun stapler staple "$BINARY" 2>/dev/null; then
  echo "Stapled notarization ticket onto binary."
else
  echo "Staple skipped (expected for non-bundled executables); Gatekeeper uses online ticket check."
fi

echo "Signed and notarized: $BINARY"
