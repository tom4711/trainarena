# TrainArena Brand

Same final approach as Finanzübersicht `docs/finanzuebersicht-brand-logo` (`c36785c`):
**PNG masters are the source of truth.** SVG redraws are not shipped.

All masters are **RGBA with transparent background** so marks work on light and dark surfaces.

| File | Role |
|------|------|
| `logo-icon.png` | Icon master, 4096×4096 RGBA — pure TA ohne Kranz (Konzept A) |
| `logo-icon-arena.png` | Icon master, 4096×4096 RGBA — TA + Hex-Kranz, ohne Wordmark |
| `banner.png` | Banner master, 3840×2160 RGBA — Kranz-Mark + Wordmark (Logo-A) + Tagline, ohne Strahlen/Dekor |
| `logo-icon-source.png` / `logo-icon-arena-source.png` / `banner-source.png` | Approved draft references |
| `../src/.../logo-icon.png` | App header (downscale, transparent) |
| `../src/.../logo-icon-arena.png` | Optional arena mark |
| `../src/.../favicon.png` | Favicon |

Colors: `#0032C3` → `#0ACDDE`, wordmark `#152038`, tagline *Live-Quiz für Ausbildung*.

Check: `python3 scripts/check-brand-logo.py`
