# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — High-Resolution Zone Grid (2048×2048)

**Problem:** The zone grid resolution matches the source illustration
(~528×637 or ~1024 upscaled). When mapped onto terrain that's 500+m
wide, each zone pixel = ~0.5m. This causes visible jaggies on all zone
boundaries — no amount of smoothing can fix it because the underlying
data is too coarse.

**Fix:** Decouple zone grid resolution from source image resolution.
The zone grid should always be a fixed high resolution (2048×2048)
regardless of the source illustration size. This means:

- Classification upsamples to 2048×2048 before classifying
- Hole Viewer canvas operates on a 2048×2048 zone grid
- Zone painting, flood fill, and all tools work at this resolution
- Export reads the 2048×2048 grid
- Source illustration is displayed as a background but doesn't
  constrain the zone grid size

At 2048×2048 on a ~500m terrain, each pixel ≈ 0.25m — sub-foot
resolution, effectively invisible jaggies.

---

### Part A: Classification (`Tools/UHoleLite/scripts/classify-zones.mjs`)

#### A1. Upscale source image to 2048 before classification

In `classifyHole()`, after loading the image with sharp, resize to
2048 on the longest side (maintaining aspect ratio) before reading
pixels:

```javascript
const ZONE_RES = 2048;

// Load and upscale to ZONE_RES on longest side
const metadata = await sharp(rawPath).metadata();
const scale = ZONE_RES / Math.max(metadata.width, metadata.height);
const targetW = Math.round(metadata.width * scale);
const targetH = Math.round(metadata.height * scale);

const { data, info } = await sharp(rawPath)
  .resize(targetW, targetH, { kernel: 'lanczos3' })
  .raw()
  .toBuffer({ resolveWithObject: true });
const { width, height, channels } = info;
```

Replace the existing `sharp(rawPath).raw().toBuffer(...)` call with this.
The rest of the classification pipeline (per-pixel classify, majority
filter, absorption, morph close) operates on the larger grid
automatically.

`lanczos3` kernel gives sharp upscaling — better than bilinear for
preserving color boundaries.

#### A2. Make ZONE_RES configurable

Add at the top of the file:
```javascript
const ZONE_RES = 2048; // Zone grid resolution (longest side)
```

---

### Part B: Hole Viewer (`Tools/UHoleLite/app/`)

#### B1. Load zone grid at native resolution

The Hole Viewer already loads the zone grid from `GET /api/zones-grid`
which returns `width`, `height`, and `grid`. If the classification
produces 2048×2048, the viewer automatically gets that size. No change
needed for loading.

#### B2. Painting at higher resolution

The zone painting tools (brush, flood fill) already work in zone grid
coordinates, not canvas coordinates. The `drawHole()` function maps
between canvas display and zone grid. This should work at any grid
resolution — verify by testing.

If brush strokes feel too fine or too coarse at the higher resolution,
the `brushSize` value may need scaling. The brush size is in zone grid
pixels, so a brush that was 5px on a 1024 grid covers the same visual
area as 10px on a 2048 grid. Consider doubling the default brush size:

```javascript
let brushSize = 10; // was 5
```

#### B3. PNG import at zone grid resolution

The PNG import currently reads the PNG at its native resolution. If the
user provides a PNG at the old resolution, it won't match the new 2048
grid. Two options:

a) Require PNG to be 2048×2048 (simplest)
b) Upscale imported PNG to match zone grid resolution

Go with (a) — just document that imported PNGs should be 2048×2048.

#### B4. SVG import

If SVG import exists, it should rasterize at the zone grid resolution
(use `zoneGridW` and `zoneGridH` which will now be ~2048×2048).

---

### Part C: Export pipeline

No changes needed. `export-hole.mjs` reads `zones.json` which has
`source_dimensions` and `grid`. The contour tracing, water mask
extraction, etc. all use the grid dimensions from the JSON. A larger
grid means finer contours (more border pixels) but RDP+Chaikin still
simplify to reasonable vertex counts.

### Part D: Unity importer

No changes needed. The splatmap pipeline resamples from zone grid to
256×256 alphamap — it reads `source_dimensions` from zones.json.

---

### File size concern

2048×2048 = 4M pixels = ~5.3MB base64 in zones.json. This is fine.
(4096×4096 would be ~21MB — too large. 2048 is the sweet spot.)

---

### Verification

1. Reclassify Hole 1:
   `node scripts/classify-zones.mjs lomond-country-club 1`
   - [ ] zones.json has `source_dimensions` ~2048×something
   - [ ] zones.png is larger, boundaries visibly smoother

2. Reclassify all 18:
   `node scripts/classify-zones.mjs lomond-country-club --all`
   - [ ] All 18 holes succeed

3. Open Hole Viewer — zone painting works at higher resolution

4. Re-export + re-import a hole — smoother splatmap and contours

5. Check zones.json file size — should be ~5MB (acceptable)

### Do NOT

- Modify export pipeline
- Modify Unity importer
- Change the zone color palette

---

## Also: Clean up broken code

Remove from `app.js` any remaining broken straighten code:
`straightenBoundaries()`, `traceBorderPixels()`, `rdpSimplify()`,
`perpDist()`, `chaikinSmooth()`, `scanlineFill()`, `dilateMask()`,
`erodeMask()`, and the Straighten button from `index.html`.

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Texture cleanup: fairway/fringe swap, dark fringe, blur removed, fringe ring
✅ DONE: 2026-04-08 — Various smoothing attempts (smoothBoundaries, straighten v1/v2, morph close)

✅ DONE: 2026-04-08 — High-res zone grid (2048 longest side), brush size doubled, SVG uses grid res
