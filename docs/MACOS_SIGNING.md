# macOS Signing & Notarization (GitHub Releases)

TrainArena-Downloads von GitHub brauchen **Developer ID Application** + Notarisierung (nicht App-Store-`Apple Distribution`).

CI: `.github/workflows/release.yml` baut auf `macos-14` ein **Universal Binary** (`osx-arm64` + `osx-x64` via `lipo`), packt **`TrainArena.app`**, signiert mit Developer ID, notarized und **stapelt** das Ticket (`scripts/macos-sign-notarize.sh`). So akzeptiert Gatekeeper Downloads ohne den „Papierkorb“-Dialog (nackte Binaries lassen sich nicht stapeln).

Schreibbare Daten (SQLite, Uploads, `startup.log`) liegen unter `~/Library/Application Support/TrainArena/` — nicht im App-Bundle (App Translocation / sealed Resources).

## Secrets (Repo → Settings → Secrets and variables → Actions)

Am Mac (einmalig):

```bash
# .p12 → Base64 in die Zwischenablage
base64 -i DeveloperID.p12 | pbcopy

# App Store Connect API Key .p8 (wie Finanzübersicht)
base64 -i ~/.appstoreconnect/private_keys/AuthKey_XXXXXXXXXX.p8 | pbcopy

# Identity-String
security find-identity -v -p codesigning
```

| Secret | Inhalt |
|--------|--------|
| `APPLE_CERTIFICATE_BASE64` | Base64 der `.p12` (Developer ID Application) |
| `APPLE_CERTIFICATE_PASSWORD` | Passwort der `.p12` |
| `APPLE_SIGNING_IDENTITY` | z. B. `Developer ID Application: Thomas Menzl (XY663DU933)` |
| `APPLE_API_KEY_BASE64` | Base64 der `.p8` |
| `APPLE_API_KEY_ID` | Key-ID (z. B. `XXXXXXXXXX`) |
| `APPLE_API_ISSUER_ID` | Issuer-UUID aus App Store Connect |

API-Key-Rechte: mindestens **Developer** / Zugang für Notary (`notarytool`).

## Test ohne neuen SemVer-Tag

1. Secrets setzen  
2. Actions → **Release** → **Run workflow** (`workflow_dispatch`)  
3. Artifact `osx-universal` laden und `TrainArena.app` auf dem Mac starten (ohne `xattr`-Workaround)

Oder nach Merge: Tag `v1.0.1` pushen → Release-Assets sind signiert.

## Lokal

Lokal signieren ist optional; die Pipeline nutzt einen temporären Keychain (vermeidet typische `errSecInternalComponent`-Probleme auf dem Desktop).
