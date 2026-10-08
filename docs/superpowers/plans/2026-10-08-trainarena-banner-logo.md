# TrainArena Banner & Logo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a brand sibling set — TA monogram icon (Konzept A) + arena-framed banner with wordmark/tagline (Konzept D) — wired into README, favicon, and Host/Player/Editor headers.

**Architecture:** Hand-authored SVGs under `wwwroot/assets/brand/` (icon) and `docs/brand/` (banner). PNG exports via headless Chrome for GitHub README and favicon. Minimal HTML/CSS: brand row next to existing titles; no host/player theme redesign.

**Tech Stack:** SVG, headless Chrome PNG rasterization, ASP.NET Core static files (`wwwroot`), Razor pages + player HTML, xUnit smoke checks, Markdown README.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-08-trainarena-banner-logo-design.md`
- Hybrid-Set: Icon = Konzept A (TA only); Banner = Konzept D (TA + arena frame + TrainArena + tagline)
- Gradients/colors from thomasmenzl.de TM DNA: `#0032C3`→`#0ACDDE`, `#0026AA`→`#019FDE`, `#0A0A73`→`#0DD8E6`; banner bg `#f7f8fb`, wordmark `#152038`, tagline `#5b6b88`
- Wordmark exactly `TrainArena`; tagline exactly `Live-Quiz für Ausbildung`
- No full UI redesign; no dark banner variant in this plan
- Branch: `cursor/trainarena-banner-logo-062a`; German UI strings; MIT-safe original artwork
- Prefer SVG source of truth; commit PNGs for README/favicon

---

## File map

| File | Role |
|------|------|
| `src/TrainArena/wwwroot/assets/brand/logo-icon.svg` | TA monogram (Konzept A) |
| `src/TrainArena/wwwroot/assets/brand/favicon.png` | 32×32 raster from icon |
| `docs/brand/banner.svg` | Arena + monogramm + wordmark + tagline |
| `docs/brand/banner.png` | ~1280×640 for README |
| `docs/brand/README.md` | One-paragraph brand note + color list |
| `src/TrainArena/Pages/Index.cshtml` | Favicon + brand header |
| `src/TrainArena/Pages/Editor/Index.cshtml` | Favicon + brand header |
| `src/TrainArena/Pages/Editor/Edit.cshtml` | Favicon + brand header |
| `src/TrainArena/wwwroot/player/index.html` | Favicon + brand header |
| `src/TrainArena/wwwroot/css/host.css` | `.brand-row` / `.brand-mark` |
| `src/TrainArena/wwwroot/player/player.css` | Same brand-row rules |
| `README.md` | Banner image at top |
| `tests/TrainArena.Tests/BrandAssetsTests.cs` | Files exist + HTTP 200 for icon/favicon |

---

### Task 1: Logo icon SVG + brand assets test (failing → green)

**Files:**
- Create: `src/TrainArena/wwwroot/assets/brand/logo-icon.svg`
- Create: `tests/TrainArena.Tests/BrandAssetsTests.cs`

**Interfaces:**
- Produces: static path `/assets/brand/logo-icon.svg` served by ASP.NET `wwwroot`
- Consumes: `TrainArenaWebAppFactory` (existing)

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TrainArena.Tests;

public class BrandAssetsTests : IClassFixture<TrainArenaWebAppFactory>
{
    private readonly HttpClient _client;

    public BrandAssetsTests(TrainArenaWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public void LogoIconSvg_ExistsOnDisk()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "TrainArena", "wwwroot", "assets", "brand", "logo-icon.svg"));
        Assert.True(File.Exists(path), $"Missing {path}");
        var svg = File.ReadAllText(path);
        Assert.Contains("TrainArena", svg);
        Assert.Contains("#0032C3", svg);
        Assert.Contains("#0ACDDE", svg);
    }

    [Fact]
    public async Task LogoIconSvg_IsServed()
    {
        var res = await _client.GetAsync("/assets/brand/logo-icon.svg");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("svg", res.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
```

If `TrainArenaWebAppFactory` path resolution differs, open `tests/TrainArena.Tests/TrainArenaWebAppFactory.cs` and keep the same relative pattern used elsewhere for content roots; prefer resolving via:

```csharp
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var path = Path.Combine(repoRoot, "src", "TrainArena", "wwwroot", "assets", "brand", "logo-icon.svg");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~BrandAssetsTests -v n`

Expected: FAIL — missing file and/or 404.

- [ ] **Step 3: Create `logo-icon.svg`**

Write exactly:

```svg
<?xml version="1.0" encoding="UTF-8"?>
<!--
  TrainArena logo-icon (Konzept A) — TA monogram.
  Brand sibling to thomasmenzl.de TM mark: angular facets, blue→cyan gradients.
  Colors: #0032C3 #0ACDDE #0026AA #019FDE #0A0A73 #0DD8E6 #0074CD #09CCE7 #0046B7 #0097D4
-->
<svg viewBox="40 40 720 520" width="720" height="520" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="TrainArena">
  <title>TrainArena</title>
  <defs>
    <linearGradient id="gradBar" x1="80" y1="120" x2="420" y2="120" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0032C3"/>
      <stop offset="1" stop-color="#0ACDDE"/>
    </linearGradient>
    <linearGradient id="gradStem" x1="200" y1="200" x2="200" y2="520" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0026AA"/>
      <stop offset="1" stop-color="#019FDE"/>
    </linearGradient>
    <linearGradient id="gradALeft" x1="380" y1="140" x2="500" y2="520" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0DD8E6"/>
      <stop offset="0.45" stop-color="#0097D4"/>
      <stop offset="1" stop-color="#0A0A73"/>
    </linearGradient>
    <linearGradient id="gradARight" x1="520" y1="140" x2="700" y2="520" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0ACDDE"/>
      <stop offset="0.5" stop-color="#0046B7"/>
      <stop offset="1" stop-color="#0032C3"/>
    </linearGradient>
    <linearGradient id="gradCross" x1="430" y1="340" x2="620" y2="340" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0074CD"/>
      <stop offset="1" stop-color="#09CCE7"/>
    </linearGradient>
  </defs>
  <!-- T stem -->
  <path fill="url(#gradStem)" d="M155 200 L255 200 L255 430 L250 438 L160 520 L155 520 Z"/>
  <!-- T bar -->
  <path fill="url(#gradBar)" d="M80 90 L420 90 L412 105 L368 185 L358 198 L352 200 L80 200 Z"/>
  <!-- A left leg -->
  <path fill="url(#gradALeft)" d="M505 100 L575 100 L500 520 L420 520 L425 505 Z"/>
  <!-- A right leg -->
  <path fill="url(#gradARight)" d="M575 100 L720 100 L655 520 L560 520 L625 180 L590 180 Z"/>
  <!-- A crossbar -->
  <path fill="url(#gradCross)" d="M455 330 L615 330 L608 360 L448 360 Z"/>
</svg>
```

- [ ] **Step 4: Run tests — expect green for icon existence + HTTP**

Run: `dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~BrandAssetsTests -v n`

Expected: PASS for `LogoIconSvg_*`. (Favicon assertions added in Task 3.)

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/wwwroot/assets/brand/logo-icon.svg tests/TrainArena.Tests/BrandAssetsTests.cs
git commit -m "feat(brand): add TA monogram logo-icon.svg and asset tests"
```

---

### Task 2: Banner SVG + PNG + README

**Files:**
- Create: `docs/brand/banner.svg`
- Create: `docs/brand/banner.png`
- Create: `docs/brand/README.md`
- Modify: `README.md` (insert banner at top, after `# TrainArena` or as first content)

**Interfaces:**
- Consumes: same TA path geometry as `logo-icon.svg` (copy paths; keep gradient ids unique within banner file)
- Produces: `docs/brand/banner.png` referenced from root README

- [ ] **Step 1: Write `docs/brand/banner.svg`**

```svg
<?xml version="1.0" encoding="UTF-8"?>
<!--
  TrainArena banner (Konzept D) — TA monogram in subtle arena frame + wordmark + tagline.
  Light surface #f7f8fb; wordmark #152038; tagline #5b6b88.
-->
<svg viewBox="0 0 1280 640" width="1280" height="640" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="TrainArena — Live-Quiz für Ausbildung">
  <title>TrainArena — Live-Quiz für Ausbildung</title>
  <defs>
    <linearGradient id="bGradBar" x1="80" y1="120" x2="420" y2="120" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0032C3"/>
      <stop offset="1" stop-color="#0ACDDE"/>
    </linearGradient>
    <linearGradient id="bGradStem" x1="200" y1="200" x2="200" y2="520" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0026AA"/>
      <stop offset="1" stop-color="#019FDE"/>
    </linearGradient>
    <linearGradient id="bGradALeft" x1="380" y1="140" x2="500" y2="520" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0DD8E6"/>
      <stop offset="0.45" stop-color="#0097D4"/>
      <stop offset="1" stop-color="#0A0A73"/>
    </linearGradient>
    <linearGradient id="bGradARight" x1="520" y1="140" x2="700" y2="520" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0ACDDE"/>
      <stop offset="0.5" stop-color="#0046B7"/>
      <stop offset="1" stop-color="#0032C3"/>
    </linearGradient>
    <linearGradient id="bGradCross" x1="430" y1="340" x2="620" y2="340" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0074CD"/>
      <stop offset="1" stop-color="#09CCE7"/>
    </linearGradient>
    <linearGradient id="arenaStroke" x1="200" y1="80" x2="1080" y2="560" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0a5cd6"/>
      <stop offset="1" stop-color="#00b6c9"/>
    </linearGradient>
  </defs>

  <rect width="1280" height="640" fill="#f7f8fb"/>
  <!-- faint cool wash -->
  <ellipse cx="640" cy="200" rx="520" ry="220" fill="#0a5cd6" opacity="0.06"/>

  <!-- subtle angular arena frame (secondary) -->
  <g fill="none" stroke="url(#arenaStroke)" stroke-width="10" stroke-linejoin="round" opacity="0.55">
    <path d="M180 140 L640 70 L1100 140 L1040 500 L640 570 L240 500 Z"/>
    <path d="M280 200 L640 150 L1000 200 L960 450 L640 500 L320 450 Z" opacity="0.85"/>
  </g>

  <!-- TA mark, scaled/centered left-of-center -->
  <g transform="translate(210,90) scale(0.55)">
    <path fill="url(#bGradStem)" d="M155 200 L255 200 L255 430 L250 438 L160 520 L155 520 Z"/>
    <path fill="url(#bGradBar)" d="M80 90 L420 90 L412 105 L368 185 L358 198 L352 200 L80 200 Z"/>
    <path fill="url(#bGradALeft)" d="M505 100 L575 100 L500 520 L420 520 L425 505 Z"/>
    <path fill="url(#bGradARight)" d="M575 100 L720 100 L655 520 L560 520 L625 180 L590 180 Z"/>
    <path fill="url(#bGradCross)" d="M455 330 L615 330 L608 360 L448 360 Z"/>
  </g>

  <!-- Wordmark + tagline -->
  <text x="720" y="300" font-family="Segoe UI, Helvetica Neue, Arial, sans-serif" font-size="72" font-weight="700" fill="#152038">TrainArena</text>
  <text x="720" y="360" font-family="Segoe UI, Helvetica Neue, Arial, sans-serif" font-size="28" font-weight="500" fill="#5b6b88">Live-Quiz für Ausbildung</text>
</svg>
```

- [ ] **Step 2: Rasterize PNG with headless Chrome**

Create a tiny helper HTML wrapper (do not commit) or point Chrome at the SVG file:

```bash
mkdir -p /tmp/ta-banner
cp docs/brand/banner.svg /tmp/ta-banner/banner.svg
cat > /tmp/ta-banner/render.html <<'HTML'
<!DOCTYPE html>
<html><head><meta charset="utf-8"/>
<style>html,body{margin:0;background:#f7f8fb} img{display:block;width:1280px;height:640px}</style>
</head><body><img src="banner.svg" width="1280" height="640" alt=""/></body></html>
HTML

google-chrome --headless=new --disable-gpu --hide-scrollbars \
  --window-size=1280,640 \
  --screenshot=/workspace/docs/brand/banner.png \
  "file:///tmp/ta-banner/render.html"
```

Verify: `file docs/brand/banner.png` shows PNG ~1280×640. If Chrome crops UI chrome, re-run with `--default-background-color=fff7f8fb` or adjust window size until the banner fills the frame.

- [ ] **Step 3: Write `docs/brand/README.md`**

```markdown
# TrainArena Brand

- `../src` app icon: `src/TrainArena/wwwroot/assets/brand/logo-icon.svg` (Konzept A — TA monogram)
- Banner: `banner.svg` / `banner.png` (Konzept D — arena frame + wordmark)
- Spec: `../superpowers/specs/2026-10-08-trainarena-banner-logo-design.md`

Primary accents: `#0032C3` → `#0ACDDE` (sibling to thomasmenzl.de). Wordmark: **TrainArena**. Tagline: *Live-Quiz für Ausbildung*.
```

- [ ] **Step 4: Wire root README**

At the top of `README.md`, immediately under the `# TrainArena` heading, insert:

```markdown
![TrainArena — Live-Quiz für Ausbildung](docs/brand/banner.png)
```

Keep the existing one-line product description after the image.

- [ ] **Step 5: Commit**

```bash
git add docs/brand/banner.svg docs/brand/banner.png docs/brand/README.md README.md
git commit -m "feat(brand): add TrainArena arena banner and README hero"
```

---

### Task 3: Favicon PNG + extend brand tests

**Files:**
- Create: `src/TrainArena/wwwroot/assets/brand/favicon.png`
- Modify: `tests/TrainArena.Tests/BrandAssetsTests.cs`

**Interfaces:**
- Produces: `/assets/brand/favicon.png` (32×32)

- [ ] **Step 1: Add failing favicon assertions**

Append to `BrandAssetsTests`:

```csharp
[Fact]
public void FaviconPng_ExistsOnDisk()
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var path = Path.Combine(repoRoot, "src", "TrainArena", "wwwroot", "assets", "brand", "favicon.png");
    Assert.True(File.Exists(path), $"Missing {path}");
    Assert.True(new FileInfo(path).Length > 100);
}

[Fact]
public async Task FaviconPng_IsServed()
{
    var res = await _client.GetAsync("/assets/brand/favicon.png");
    Assert.Equal(HttpStatusCode.OK, res.StatusCode);
}
```

- [ ] **Step 2: Run — expect fail**

Run: `dotnet test tests/TrainArena.Tests/TrainArena.Tests.csproj --filter FullyQualifiedName~BrandAssetsTests -v n`

Expected: FAIL on favicon facts.

- [ ] **Step 3: Rasterize 32×32 favicon**

```bash
mkdir -p /tmp/ta-icon
cp src/TrainArena/wwwroot/assets/brand/logo-icon.svg /tmp/ta-icon/logo-icon.svg
cat > /tmp/ta-icon/render.html <<'HTML'
<!DOCTYPE html>
<html><head><meta charset="utf-8"/>
<style>
  html,body{margin:0;width:32px;height:32px;background:transparent;overflow:hidden}
  img{width:32px;height:32px;object-fit:contain;display:block}
</style></head>
<body><img src="logo-icon.svg" alt=""/></body></html>
HTML

google-chrome --headless=new --disable-gpu --hide-scrollbars \
  --window-size=32,32 \
  --screenshot=/workspace/src/TrainArena/wwwroot/assets/brand/favicon.png \
  "file:///tmp/ta-icon/render.html"
```

If transparency is lost, that is acceptable for v1 (solid light bg ok). Prefer `object-fit: contain` with small padding via CSS `padding:2px; box-sizing:border-box` if the mark clips.

- [ ] **Step 4: Run tests — all BrandAssetsTests green**

- [ ] **Step 5: Commit**

```bash
git add src/TrainArena/wwwroot/assets/brand/favicon.png tests/TrainArena.Tests/BrandAssetsTests.cs
git commit -m "feat(brand): add favicon.png and cover with tests"
```

---

### Task 4: Wire favicon + brand header in Host / Editor / Player

**Files:**
- Modify: `src/TrainArena/Pages/Index.cshtml`
- Modify: `src/TrainArena/Pages/Editor/Index.cshtml`
- Modify: `src/TrainArena/Pages/Editor/Edit.cshtml`
- Modify: `src/TrainArena/wwwroot/player/index.html`
- Modify: `src/TrainArena/wwwroot/css/host.css`
- Modify: `src/TrainArena/wwwroot/player/player.css`

**Interfaces:**
- Consumes: `/assets/brand/logo-icon.svg`, `/assets/brand/favicon.png` (player uses absolute `/assets/...` paths so LAN devices resolve against the app origin)

- [ ] **Step 1: Add CSS (host.css)** — append:

```css
.brand-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin: 0 0 0.25rem;
}

.brand-row h1 {
  margin: 0;
}

.brand-mark {
  width: 2.75rem;
  height: auto;
  flex-shrink: 0;
  display: block;
  filter: drop-shadow(0 8px 16px rgba(10, 92, 214, 0.28));
}
```

- [ ] **Step 2: Add the same `.brand-row` / `.brand-mark` rules to `player.css`**

- [ ] **Step 3: Update Host `Index.cshtml` head + h1**

In `<head>`, after charset/viewport:

```html
<link rel="icon" href="/assets/brand/favicon.png" type="image/png" />
<link rel="icon" href="/assets/brand/logo-icon.svg" type="image/svg+xml" />
```

Replace `<h1>TrainArena</h1>` with:

```html
<div class="brand-row">
    <img class="brand-mark" src="/assets/brand/logo-icon.svg" width="64" height="46" alt="" />
    <h1>TrainArena</h1>
</div>
```

- [ ] **Step 4: Same favicon links + brand-row on Editor Index and Edit**

Editor Index: wrap `<h1>Quiz-Editor</h1>` the same way (mark + title).  
Editor Edit: wrap the page’s primary `<h1>` (quiz title heading) or the first visible H1 — if Edit uses a free-form title, still show the mark beside the first `h1`.

- [ ] **Step 5: Player `index.html`**

Head:

```html
<link rel="icon" href="/assets/brand/favicon.png" type="image/png" />
<link rel="icon" href="/assets/brand/logo-icon.svg" type="image/svg+xml" />
```

Body:

```html
<div class="brand-row">
  <img class="brand-mark" src="/assets/brand/logo-icon.svg" width="64" height="46" alt="" />
  <h1>TrainArena</h1>
</div>
```

- [ ] **Step 6: Manual / automated check**

Run: `dotnet test TrainArena.sln -v n`  
Expected: all green.

Optional smoke: `dotnet run --project src/TrainArena` and open Host/Player — favicon + mark visible.

- [ ] **Step 7: Commit**

```bash
git add src/TrainArena/Pages/Index.cshtml \
  src/TrainArena/Pages/Editor/Index.cshtml \
  src/TrainArena/Pages/Editor/Edit.cshtml \
  src/TrainArena/wwwroot/player/index.html \
  src/TrainArena/wwwroot/css/host.css \
  src/TrainArena/wwwroot/player/player.css
git commit -m "feat(brand): wire favicon and TA mark into Host, Editor, Player"
```

---

### Task 5: Spec status + PR polish

**Files:**
- Modify: `docs/superpowers/specs/2026-10-08-trainarena-banner-logo-design.md` — set status to implemented / ready for review
- Update PR description with before/after note and paths

- [ ] **Step 1: Flip spec status line**

Change status to: `Implemented on branch cursor/trainarena-banner-logo-062a (assets + wiring)`

- [ ] **Step 2: Full test suite**

Run: `dotnet test TrainArena.sln -v n`  
Expected: PASS

- [ ] **Step 3: Commit + push**

```bash
git add docs/superpowers/specs/2026-10-08-trainarena-banner-logo-design.md
git commit -m "docs(brand): mark banner/logo spec implemented"
git push -u origin cursor/trainarena-banner-logo-062a
```

---

## Spec coverage checklist (self-review)

| Spec requirement | Task |
|------------------|------|
| Icon Konzept A (TA, no arena) | Task 1 |
| Banner Konzept D (arena + wordmark + tagline) | Task 2 |
| Brand DNA colors | Tasks 1–2 SVG content |
| Paths `wwwroot/assets/brand/` + `docs/brand/` | Tasks 1–3 |
| README banner | Task 2 |
| Favicon + headers Host/Player/Editor | Tasks 3–4 |
| No UI redesign / no dark banner | Constraints + Task 4 CSS only for brand-row |
| MIT original artwork | Hand-authored SVG (not TM path copy) |

## Placeholder scan

None intentional. Chrome raster commands are concrete; if Chrome flags change, keep `--headless=new` and `--screenshot=`.
