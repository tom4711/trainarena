# TrainArena Brand

Same final approach as Finanzübersicht `docs/finanzuebersicht-brand-logo` (`c36785c`):
**PNG masters are the source of truth.** SVG redraws lost the approved mark, so they are not shipped.

| File | Role |
|------|------|
| `logo-icon.png` | Icon master, 4096×4096 (Konzept A) |
| `banner.png` | Banner master, 3840×2160 (Konzept A mark + arena + wordmark) |
| `logo-icon-source.png` / `banner-source.png` | Approved draft references |
| `../src/TrainArena/wwwroot/assets/brand/logo-icon.png` | App header raster |
| `../src/TrainArena/wwwroot/assets/brand/favicon.png` | Favicon |

Colors: `#0032C3` → `#0ACDDE`, wordmark `#152038`, tagline *Live-Quiz für Ausbildung*.

Check: `python3 scripts/check-brand-logo.py`
