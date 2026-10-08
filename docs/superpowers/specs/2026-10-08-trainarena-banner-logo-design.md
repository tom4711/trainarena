# TrainArena Banner & Logo — Design Spec

**Datum:** 2026-10-08  
**Status:** Implemented on branch cursor/trainarena-banner-logo-062a (assets + wiring)  
**Produkt:** TrainArena (`trainarena`) — MIT OSS, self-hosted Live-Quiz  
**Bezugsmarke:** Thomas Menzl Softwareentwicklung ([thomasmenzl.de](https://thomasmenzl.de))  
**Branch:** `cursor/trainarena-banner-logo-062a`

---

## Ziel

Ein Branding-Set für TrainArena, das **klar zur Unternehmensmarke passt** (Geometrie + Blau→Cyan), aber als **eigenes OSS-Produktzeichen** erkennbar bleibt — kein TM-Klon, keine Firmen-Übernahme-Optik.

## Entscheidungen (gelockt)

| Feld | Entscheidung |
|------|----------------|
| Scope | **Hybrid-Set (Option 3):** Icon = Konzept A, Banner = Konzept D |
| Verwandtschaft | **A** — deutlich verwandt: gleiche Verläufe + eckige Geometrie wie TM |
| Icon-Motiv | **TA-Monogramm** (Konzept A), ohne Arena-Rahmen |
| Banner-Motiv | **TA-Monogramm in dezentem angularen Arena-Rahmen** + Wordmark (Konzept D) |
| Tagline | **ja** — *Live-Quiz für Ausbildung* (nur auf dem Banner) |
| Wordmark | **TrainArena** (Produktname gelockt) |
| UI-Redesign | **nicht** im Scope — nur Assets + README-/Favicon-Einbindung |
| Dunkle Banner-Variante | **nicht** im Primärset (Backlog) |

## Marken-DNA (Referenz thomasmenzl.de)

- Monogramm aus **eckigen polygonalen Flächen** (kein weiches Rounded-Consumer-Icon)
- Verläufe Blau → Cyan, u. a.:
  - `#0032C3` → `#0ACDDE`
  - `#0026AA` → `#019FDE`
  - tiefes Navy `#0A0A73` → Cyan `#0DD8E6`
- UI-Accents (Site): `#0a5cd6` / `#00b6c9` (light), `#4da8ff` / `#35e0e6` (dark)
- Flächen: hell `#f7f8fb`, Text Navy `#152038`, Dark-BG `#0b1020`
- Kein Lila, kein Neon-Glow-Spam, keine Fotocollagen

## Asset-Set

| Asset | Inhalt | Primärer Einsatz |
|-------|--------|------------------|
| `logo-icon.svg` (+ `favicon.png` 32×32) | TA-Monogramm (Konzept A) | Favicon, Host-/Player-/Editor-Header |
| `banner.svg` + `banner.png` (~1280×640) | Arena-Rahmen + Monogramm + Wordmark + Tagline | GitHub README (`banner.png`), Archiv/Edit (`banner.svg`) |
| Farbleiste | Kommentarblock in den SVGs + Kurznotiz in dieser Spec (Marken-DNA) | spätere UI-Anbindung |

### Icon (Konzept A)

- Nur **T + A**, interlocking/adjacent, gleiche visuelle Sprache wie TM
- Transparenter Hintergrund; mittig mit bescheidenem Padding
- Muss bei **16×16 / 32×32** noch als Marke lesbar sein
- Kein Wordmark im Icon-File

### Banner (Konzept D)

- Dasselbe Monogramm **innerhalb** eines **dezenten**, angularen Arena-/Stadion-Rahmens (Outline/Ring, sekundär zum Monogramm)
- Wordmark **TrainArena** in klarer Sans-Serif, Farbe `#152038` auf hell `#f7f8fb`
- Eine Zeile Tagline in Muted (`#5b6b88`): *Live-Quiz für Ausbildung*
- Generöse Ränder; ruhige Komposition; Rahmen nicht dominant

### Varianten (Backlog)

- Dunkle Banner-Variante auf `#0b1020` mit hellerem Wordmark — nach Primärset
- Wordmark-lowercase `trainarena` — **abgelehnt** (bleibt **TrainArena**)

## Dateiorte (Umsetzung)

- `src/TrainArena/wwwroot/assets/brand/logo-icon.svg` — App-Icon / Header
- `src/TrainArena/wwwroot/assets/brand/favicon.png` — Favicon (aus Icon gerastert)
- `docs/brand/banner.svg` — editierbares Banner
- `docs/brand/banner.png` — README-Einbindung (`![TrainArena](docs/brand/banner.png)`)
- Favicon- und Header-Links in Host-Layout, Player-`index.html`, Editor-Seiten

## Nicht-Ziele

- Vollständiges Redesign von Host/Player/Editor-CSS
- Ersetzen oder Ableiten des TM-Unternehmenslogos
- App-Store-Assets / Marketing-Website
- Animationen als Pflichtteil des Logos

## Abnahmekriterien

1. Icon und Banner teilen **dieselbe** TA-Form und Farbverläufe.
2. Icon ohne Arena-Rahmen; Banner mit dezentem Arena-Rahmen.
3. Optisch als Geschwister zu thomasmenzl.de erkennbar, aber als „TrainArena“ lesbar.
4. README zeigt das Banner; App-Header/Favicon nutzen das Icon.
5. Assets sind SVG (primär) und für GitHub ein PNG-Banner; MIT-kompatibel (eigenes Werk).

## Konzept-Referenzen (Braindump)

Während der Abstimmung erzeugte Entwürfe (Artefakte, nicht final):

- Konzept A Icon / Banner
- Konzept D Icon / Banner

Finale Vektoren werden im Implementation-Plan als saubere SVGs (handgeführt oder nachgezogen) geliefert — nicht ungeprüft als reine Raster-AI-Exports.

## Offene Punkte

Keine. Dunkle Banner-Variante = Backlog.
