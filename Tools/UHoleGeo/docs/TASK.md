# TASK.md — Instructions for Claude Code (UHole Geo)

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Full design rationale: `Docs/TERRAIN_RELIEF_PLAN.md`, `Docs/TERRAIN_CONFORMING_PLAN.md`

---

## Current Task — Per-Zone Residual with Proper Gaussian Blur

UHoleGeo's `generate-terrain.mjs` has the same terrain problem as
UHole Lite: playable zones use a pure quadratic surface (flat), and
the 3×3 `zoneMaskedSmooth` blur is too weak to smooth 5m DEM grid
noise. Overlay meshes sample bumpy terrain → meshes are bumpy →
terrain pokes through overlay edges.

### Changes Required

**File 1: `Tools/UHoleGeo/scripts/lib/terrain.mjs`**

Add this exported function after `blur2D`:

```javascript
/**
 * Separable Gaussian blur on a 2D float array with zone mask.
 * Only blurs cells where mask[i] is truthy. Cells outside the
 * mask are treated as zero during convolution (boundary handling).
 * Uses two-pass separable approach: horizontal then vertical.
 *
 * @param {Float64Array} data - width×height array
 * @param {Uint8Array} mask - width×height mask (1 = blur, 0 = skip)
 * @param {number} width
 * @param {number} height
 * @param {number} sigma - Gaussian standard deviation in cells
 * @returns {Float64Array} blurred copy
 */
export function gaussianBlurMasked(data, mask, width, height, sigma) {
  const radius = Math.ceil(sigma * 3);
  const kernelSize = radius * 2 + 1;
  const kernel = new Float64Array(kernelSize);
  let kernelSum = 0;
  for (let i = 0; i < kernelSize; i++) {
    const d = i - radius;
    kernel[i] = Math.exp(-(d * d) / (2 * sigma * sigma));
    kernelSum += kernel[i];
  }
  for (let i = 0; i < kernelSize; i++) kernel[i] /= kernelSum;

  // Horizontal pass
  const temp = new Float64Array(width * height);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = y * width + x;
      if (!mask[idx]) { temp[idx] = data[idx]; continue; }
      let sum = 0, wSum = 0;
      for (let k = 0; k < kernelSize; k++) {
        const sx = x + k - radius;
        if (sx < 0 || sx >= width) continue;
        const sIdx = y * width + sx;
        if (!mask[sIdx]) continue;
        sum += data[sIdx] * kernel[k];
        wSum += kernel[k];
      }
      temp[idx] = wSum > 0 ? sum / wSum : data[idx];
    }
  }

  // Vertical pass
  const result = new Float64Array(width * height);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = y * width + x;
      if (!mask[idx]) { result[idx] = temp[idx]; continue; }
      let sum = 0, wSum = 0;
      for (let k = 0; k < kernelSize; k++) {
        const sy = y + k - radius;
        if (sy < 0 || sy >= height) continue;
        const sIdx = sy * width + x;
        if (!mask[sIdx]) continue;
        sum += temp[sIdx] * kernel[k];
        wSum += kernel[k];
      }
      result[idx] = wSum > 0 ? sum / wSum : temp[idx];
    }
  }

  return result;
}
```

**File 2: `Tools/UHoleGeo/scripts/generate-terrain.mjs`**

**Step 1:** Update the import at the top.

Change:
```javascript
import { perlin2D, blur2D } from './lib/terrain.mjs';
```
To:
```javascript
import { perlin2D, blur2D, gaussianBlurMasked } from './lib/terrain.mjs';
```

**Step 2:** In `generateTerrainDEM()`, replace the entire residual
section. Delete everything from:

```javascript
  // Add residual variation to non-playable zones with distance-based ramp
  const residualZones = new Set([ZONES.trees, ZONES.ob, ZONES.background]);
```

Through and including:

```javascript
  console.log(`  Residual ramp: ${RESIDUAL_RAMP_CELLS} cells transition, ` +
    `${(RESIDUAL_FRACTION * 100).toFixed(0)}% max fraction`);
```

And ALSO delete the per-zone `zoneMaskedSmooth` calls right after:

```javascript
  zoneMaskedSmooth(heightmap, zoneGrid, zw, zh, ZONES.green, 8, RES);
  zoneMaskedSmooth(heightmap, zoneGrid, zw, zh, ZONES.fairway, 4, RES);
  zoneMaskedSmooth(heightmap, zoneGrid, zw, zh, ZONES.semi_rough, 3, RES);
  zoneMaskedSmooth(heightmap, zoneGrid, zw, zh, ZONES.bunker, 2, RES);
  for (const z of [ZONES.trees, ZONES.ob, ZONES.background]) {
    zoneMaskedSmooth(heightmap, zoneGrid, zw, zh, z, 2, RES);
  }
```

Replace ALL of the above with:

```javascript
  // --- Per-zone DEM residual blending with proper Gaussian blur ---
  //
  // DEM5A is 5m resolution. On a 2049-cell heightmap the number of
  // heightmap cells per DEM cell varies with terrain width:
  //   demCellsInHM = RES / (terrainWidthM / 5)
  // sigma = demCellsInHM × multiplier removes grid artifacts at
  // DEM scale while preserving larger elevation trends.

  const demCellsInHM = RES / (terrainWidthM / 5);
  const sigmaBase = demCellsInHM; // one DEM cell width in heightmap cells

  const ZONE_RESIDUAL = {
    [ZONES.green]:      { fraction: 0.0,  sigma: 0 },
    [ZONES.tee_box]:    { fraction: 0.0,  sigma: 0 },
    [ZONES.fairway]:    { fraction: 0.30, sigma: sigmaBase * 2.0 },
    [ZONES.bunker]:     { fraction: 0.0,  sigma: 0 },
    [ZONES.cart_path]:  { fraction: 0.0,  sigma: 0 },
    [ZONES.semi_rough]: { fraction: 0.40, sigma: sigmaBase * 1.5 },
    [ZONES.rough]:      { fraction: 0.50, sigma: sigmaBase * 1.0 },
    [ZONES.water]:      { fraction: 0.0,  sigma: 0 },
    [ZONES.trees]:      { fraction: 0.75, sigma: sigmaBase * 0.5 },
    [ZONES.ob]:         { fraction: 0.75, sigma: sigmaBase * 0.5 },
    [ZONES.background]: { fraction: 0.75, sigma: sigmaBase * 0.5 },
  };

  console.log(`  DEM cell = ${demCellsInHM.toFixed(1)} heightmap cells, sigmaBase = ${sigmaBase.toFixed(1)}`);

  // Per-cell residual: rawDem - quadratic
  const residual = new Float64Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    residual[i] = rawDem[i] - heightmap[i];
  }

  for (const [zoneStr, cfg] of Object.entries(ZONE_RESIDUAL)) {
    const zone = parseInt(zoneStr);
    if (cfg.fraction <= 0) continue;

    const mask = buildZoneMask(zoneGrid, zw, zh, zone, RES);

    // Skip if zone has no cells
    let hasCells = false;
    for (let i = 0; i < RES * RES; i++) {
      if (mask[i]) { hasCells = true; break; }
    }
    if (!hasCells) continue;

    // Copy residual for this zone (zero outside mask)
    const zoneRes = new Float64Array(RES * RES);
    for (let i = 0; i < RES * RES; i++) {
      zoneRes[i] = mask[i] ? residual[i] : 0;
    }

    // Gaussian blur with proper sigma (zone-masked)
    const blurred = cfg.sigma > 0
      ? gaussianBlurMasked(zoneRes, mask, RES, RES, cfg.sigma)
      : zoneRes;

    // Blend blurred residual back into heightmap
    for (let i = 0; i < RES * RES; i++) {
      if (mask[i]) {
        heightmap[i] += blurred[i] * cfg.fraction;
      }
    }

    const zoneName = Object.keys(ZONES).find(k => ZONES[k] === zone);
    console.log(`    ${zoneName}: residual ${(cfg.fraction * 100).toFixed(0)}%, sigma ${cfg.sigma.toFixed(1)} cells`);
  }
```

### What NOT to Change

- Quadratic surface fitting (fitQuadratic, evalQuadratic)
- NaN fill via neighbour propagation (better than avg fill)
- Normalization (25m max range, scaleFactor)
- Final blur2D pass (1 pass global softening — keep it)
- Water handling
- Heightmap row-flip in writeHeightmapRaw
- uint16 encoding
- Perlin fallback path (generateTerrainPerlin)

### Also OK to Remove (unused after this change)

- `RESIDUAL_FRACTION` and `RESIDUAL_RAMP_CELLS` constants
- `isPlayable` mask and `distFromPlay` distance field
- `chamferDist` function (optional — keep if you prefer)
- The old `zoneMaskedSmooth` calls after the residual section

### Verification

```bash
cd Tools/UHoleGeo
node scripts/generate-terrain.mjs lomond-country-club 4
node scripts/generate-terrain.mjs lomond-country-club 1
```

Expected output:
```
  DEM cell = 68.3 heightmap cells, sigmaBase = 68.3
    fairway: residual 30%, sigma 136.5 cells
    semi_rough: residual 40%, sigma 102.4 cells
    rough: residual 50%, sigma 68.3 cells
    trees: residual 75%, sigma 34.1 cells
    ...
```

Then in Unity: `Import > Geo > Normal > Import Hole 04 Geo`

- [ ] Fairway smooth (no 5m DEM staircase)
- [ ] Fairway follows general slope tee→green
- [ ] Rough has gentle undulation
- [ ] Trees/OB mountainous backdrop
- [ ] No cliff at zone boundaries
- [ ] Overlay meshes not eaten by terrain
- [ ] Green/bunker/water unaffected
- [ ] No console errors

---

## Completed Tasks

✅ DONE: 2026-04-15 — Per-zone residual blending with proper Gaussian blur. Added `gaussianBlurMasked` to `lib/terrain.mjs`. Replaced old `RESIDUAL_FRACTION`/`chamferDist`/`zoneMaskedSmooth` block in `generateTerrainDEM` with `ZONE_RESIDUAL` table using `sigmaBase` derived from `terrainWidthM`. Verified Holes 1 and 4 generate cleanly with correct per-zone sigma output.
