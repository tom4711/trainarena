# TrainArena Brand

Same final approach as Finanzübersicht `docs/finanzuebersicht-brand-logo` (`c36785c`):
**PNG masters are the source of truth.** SVG redraws are not shipped.

| File | Role |
|------|------|
| `logo-icon.png` | Icon master, 4096×4096 — pure TA (Konzept A) |
| `logo-icon-arena.png` | Icon master with arena wreath, 4096×4096 — no wordmark |
| `banner.png` | Banner master, 3840×2160 — arena mark + wordmark (logo-A in *TrainArena*) + tagline |
| `logo-icon-source.png` / `logo-icon-arena-source.png` / `banner-source.png` | Approved draft references |
| `../src/.../logo-icon.png` | App header |
| `../src/.../logo-icon-arena.png` | Optional arena mark |
| `../src/.../favicon.png` | Favicon |

Colors: `#0032C3` → `#0ACDDE`, wordmark `#152038`, tagline *Live-Quiz für Ausbildung*.

Check: `python3 scripts/check-brand-logo.py`
