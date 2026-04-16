# TASK.md — Instructions for Claude Code (UHole Geo)

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Full design rationale: `Docs/TERRAIN_SPLINE_PLAN.md`

---

## Current Task — Ravine Carving (Detect + Carve Big Negative Features)

The spline+quadratic surface is smooth and playable zones look great.
But **real big topographic features are missing** — most notably the
ravine in Hole 7, which runs through the OB and is currently painted
over by the smooth synthetic surface.

Problem we're solving: make real ravines (and similar big negative
features) appear in the heightmap while preserving the smoothness of
playable zones and avoiding the DEM grid noise that killed every
previous "blend the DEM back in" approach.

Key idea: **detect ravines as coherent regions, carve them as smooth
Gaussian shapes.** We never copy raw DEM pixels into the heightmap —
we only use the DEM to find where ravines are, how deep, and how wide.
The actual carving is done with a separable Gaussian blur of a depth
field, so no grid noise ever enters the output.

### Scope

Only `Tools/UHoleGeo/scripts/generate-terrain.mjs`. One file, one
function (`generateTerrainDEM`). Insert a new block between the
existing "Build heightmap: spline + quadratic cross-axis" loop and
the "Normalize" block. Nothing else changes.

Perlin fallback path is untouched. `playableZones` is already defined
earlier in the function — reuse it, don't redeclare.

### Implementation

Insert the following block in `generateTerrainDEM()` AFTER the line
`` console.log(`  Mode: spline along-axis + quadratic cross-axis`); ``
and BEFORE the line `// Normalize — relative elevation`:

```javascript
// ─── Ravine Carving ──────────────────────────────────────────
//
// Detect big negative features (ravines, gullies) as connected
// regions where rawDem is significantly below the synthetic surface.
// Carve each qualifying region as a smooth Gaussian depression —
// this gives us visible ravines without importing DEM grid noise.
//
// Tunable parameters (safe defaults — see notes at bottom of task)
const RAVINE_MIN_DEPTH_M       = 3.0;   // cell counts as ravine if >= this deep below surface
const RAVINE_MIN_AREA_CELLS    = 2000;  // min region size (rejects noise)
const RAVINE_MAX_AREA_FRAC     = 0.25;  // reject regions bigger than this fraction of hole
const RAVINE_MAX_PLAYABLE_FRAC = 0.05;  // skip region if more than 5% is in playable zones
const RAVINE_KERNEL_SIGMA_M    = 8.0;   // Gaussian falloff (softness of carve edges)
const RAVINE_DEPTH_PERCENTILE  = 0.20;  // use mean of deepest 20% of region cells as target depth

const TOTAL_CELLS = RES * RES;

// Step 1: Compute residual (rawDem - synthetic surface)
// Negative values = cell is below the synthetic surface (ravine candidates)
const ravineResidual = new Float64Array(TOTAL_CELLS);
for (let i = 0; i < TOTAL_CELLS; i++) {
  ravineResidual[i] = rawDem[i] - heightmap[i];
}

// Step 2: Build ravine-candidate mask — cells more than RAVINE_MIN_DEPTH_M below surface
const ravineCandidate = new Uint8Array(TOTAL_CELLS);
for (let i = 0; i < TOTAL_CELLS; i++) {
  if (ravineResidual[i] < -RAVINE_MIN_DEPTH_M) ravineCandidate[i] = 1;
}
```

```javascript
// Step 3: Build playable mask at heightmap resolution (for overlap check)
const isPlayable = new Uint8Array(TOTAL_CELLS);
for (let hy = 0; hy < RES; hy++) {
  for (let hx = 0; hx < RES; hx++) {
    const nx = hx / (RES - 1);
    const ny = hy / (RES - 1);
    const zx = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
    const zy = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
    if (playableZones.has(zoneGrid[zy * zw + zx])) {
      isPlayable[hy * RES + hx] = 1;
    }
  }
}

// Step 4: Flood-fill connected components in the candidate mask (4-connectivity).
// Push neighbours conditionally to avoid bloating the stack.
const regionLabel = new Int32Array(TOTAL_CELLS); // 0 = unlabeled, 1+ = region id
let numRegions = 0;
const floodStack = [];

for (let startIdx = 0; startIdx < TOTAL_CELLS; startIdx++) {
  if (!ravineCandidate[startIdx] || regionLabel[startIdx] !== 0) continue;

  numRegions++;
  const label = numRegions;
  floodStack.length = 0;
  floodStack.push(startIdx);
  regionLabel[startIdx] = label;
```

```javascript
  while (floodStack.length > 0) {
    const idx = floodStack.pop();
    const hx = idx % RES;
    const hy = (idx - hx) / RES;

    // 4-connectivity
    if (hx > 0) {
      const n = idx - 1;
      if (ravineCandidate[n] && regionLabel[n] === 0) {
        regionLabel[n] = label;
        floodStack.push(n);
      }
    }
    if (hx < RES - 1) {
      const n = idx + 1;
      if (ravineCandidate[n] && regionLabel[n] === 0) {
        regionLabel[n] = label;
        floodStack.push(n);
      }
    }
    if (hy > 0) {
      const n = idx - RES;
      if (ravineCandidate[n] && regionLabel[n] === 0) {
        regionLabel[n] = label;
        floodStack.push(n);
      }
    }
    if (hy < RES - 1) {
      const n = idx + RES;
      if (ravineCandidate[n] && regionLabel[n] === 0) {
        regionLabel[n] = label;
        floodStack.push(n);
      }
    }
  }
}
```

```javascript
// Step 5: Gather per-region stats and decide which to carve
const regionStats = new Array(numRegions + 1); // index 0 unused
for (let r = 1; r <= numRegions; r++) {
  regionStats[r] = { cells: [], depths: [], playableCount: 0 };
}

for (let idx = 0; idx < TOTAL_CELLS; idx++) {
  const r = regionLabel[idx];
  if (r === 0) continue;
  regionStats[r].cells.push(idx);
  regionStats[r].depths.push(ravineResidual[idx]);
  if (isPlayable[idx]) regionStats[r].playableCount++;
}

const MAX_AREA_CELLS = Math.floor(TOTAL_CELLS * RAVINE_MAX_AREA_FRAC);
const carvedRegions = [];

for (let r = 1; r <= numRegions; r++) {
  const stats = regionStats[r];
  const area = stats.cells.length;

  if (area < RAVINE_MIN_AREA_CELLS) continue;
  if (area > MAX_AREA_CELLS) {
    console.log(`    Region #${r}: area=${area} cells — REJECTED (too big, > ${(RAVINE_MAX_AREA_FRAC * 100).toFixed(0)}% of hole)`);
    continue;
  }
  const playableFrac = stats.playableCount / area;
  if (playableFrac > RAVINE_MAX_PLAYABLE_FRAC) {
    console.log(`    Region #${r}: area=${area}, playable=${(playableFrac * 100).toFixed(1)}% — REJECTED (overlaps playable)`);
    continue;
  }
```

```javascript
  // Target depth = mean of deepest N% of cells (avoids outliers dominating)
  stats.depths.sort((a, b) => a - b); // ascending (most negative first)
  const deepestCount = Math.max(1, Math.floor(stats.depths.length * RAVINE_DEPTH_PERCENTILE));
  let depthSum = 0;
  for (let i = 0; i < deepestCount; i++) depthSum += stats.depths[i];
  const targetDepth = depthSum / deepestCount; // negative (below surface), meters

  carvedRegions.push({
    id: r,
    area,
    playableFrac,
    targetDepth,
    cells: stats.cells,
  });
}

console.log(`  Ravine detection: ${numRegions} candidate regions, ${carvedRegions.length} qualifying for carve`);
for (const region of carvedRegions) {
  console.log(`    Carve Region #${region.id}: area=${region.area} cells, ` +
    `playable=${(region.playableFrac * 100).toFixed(1)}%, ` +
    `depth=${region.targetDepth.toFixed(1)}m`);
}
```

```javascript
// Step 6: Carve each qualifying region with a separable Gaussian blur.
//
// Approach:
//   - Build a source field: region cells = targetDepth, everywhere else = 0
//   - Apply a separable Gaussian blur (horizontal pass then vertical pass)
//   - Rescale so the deepest point of the blurred field = targetDepth
//     (the blur inevitably shallows the peak)
//   - Add to heightmap
//
// Two buffers are reused across all regions instead of allocating fresh
// buffers per region — saves ~32 MB per extra region per blur buffer.

if (carvedRegions.length > 0) {
  const metersPerCell = ((terrainWidthM + terrainLengthM) / 2) / (RES - 1);
  const sigmaCells = RAVINE_KERNEL_SIGMA_M / metersPerCell;

  // Build 1D Gaussian kernel with radius = ceil(3 * sigma)
  const kernelRadius = Math.max(1, Math.ceil(3 * sigmaCells));
  const kernelSize = 2 * kernelRadius + 1;
  const kernel = new Float64Array(kernelSize);
  {
    const s2 = 2 * sigmaCells * sigmaCells;
    let kSum = 0;
    for (let i = 0; i < kernelSize; i++) {
      const x = i - kernelRadius;
      kernel[i] = Math.exp(-(x * x) / s2);
      kSum += kernel[i];
    }
    for (let i = 0; i < kernelSize; i++) kernel[i] /= kSum;
  }
  console.log(`  Ravine carving: sigma=${RAVINE_KERNEL_SIGMA_M}m (${sigmaCells.toFixed(1)} cells), ` +
    `kernel radius=${kernelRadius} cells, size=${kernelSize}`);
```

```javascript
  // Reusable buffers for blur
  const bufA = new Float64Array(TOTAL_CELLS);
  const bufB = new Float64Array(TOTAL_CELLS);

  for (const region of carvedRegions) {
    // Clear bufA and write source field
    bufA.fill(0);
    for (const idx of region.cells) bufA[idx] = region.targetDepth;

    // Horizontal pass: bufA -> bufB
    for (let hy = 0; hy < RES; hy++) {
      const rowBase = hy * RES;
      for (let hx = 0; hx < RES; hx++) {
        let sum = 0;
        for (let k = -kernelRadius; k <= kernelRadius; k++) {
          let sx = hx + k;
          if (sx < 0) sx = 0;
          else if (sx >= RES) sx = RES - 1;
          sum += bufA[rowBase + sx] * kernel[k + kernelRadius];
        }
        bufB[rowBase + hx] = sum;
      }
    }

    // Vertical pass: bufB -> bufA
    for (let hx = 0; hx < RES; hx++) {
      for (let hy = 0; hy < RES; hy++) {
        let sum = 0;
        for (let k = -kernelRadius; k <= kernelRadius; k++) {
          let sy = hy + k;
          if (sy < 0) sy = 0;
          else if (sy >= RES) sy = RES - 1;
          sum += bufB[sy * RES + hx] * kernel[k + kernelRadius];
        }
        bufA[hy * RES + hx] = sum;
      }
    }
```

```javascript
    // Find deepest value in blurred field (most negative)
    let minAfter = 0;
    for (let i = 0; i < TOTAL_CELLS; i++) {
      if (bufA[i] < minAfter) minAfter = bufA[i];
    }

    // Rescale so deepest = targetDepth (both are negative, so ratio is positive)
    if (minAfter < -1e-6) {
      const rescale = region.targetDepth / minAfter;
      for (let i = 0; i < TOTAL_CELLS; i++) {
        heightmap[i] += bufA[i] * rescale;
      }
    }
  }
}
// ─── End Ravine Carving ──────────────────────────────────────
```

### Why This Avoids the Failures of Previous Attempts

| Previous failure                                          | Why this doesn't hit it                                                                          |
| --------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| DEM residual blending → 5m grid noise on fairway          | We don't blend raw DEM anywhere. The carve uses a smooth Gaussian kernel, not DEM pixels.        |
| Per-zone residual masks → visible boundary artifacts      | No zone masks in the carving. The kernel is applied globally; boundaries emerge from the Gaussian falloff, which is smooth by construction. |
| Playable zones had bumps from residual                    | `RAVINE_MAX_PLAYABLE_FRAC = 0.05` prevents carving anything that overlaps playable more than 5%. |
| Large filters broke narrow zones                          | Carve is a feature-level operation — doesn't care about zone shapes. Just makes a smooth depression wherever the DEM says a ravine is. |

### What NOT to Change

- Axis computation (tee→green centroids, unit vectors)
- DEM sampling loop (splineXs/splineYs construction)
- Monotone spline + quadratic cross-axis surface construction
- `fitQuadratic` / `evalQuadratic`
- Normalization, final blur, uint16 encode, row-flip
- Perlin fallback path
- NaN-fill propagation
- The existing `playableZones` Set definition (re-use it — don't redeclare)

### Parameter Tuning Notes

Defaults are tuned conservatively for Hole 7's ravine. Tune only if
visual results on Hole 7 are wrong.

If the ravine doesn't appear deep enough:
- Lower `RAVINE_MIN_DEPTH_M` (e.g. 2.0) — more cells qualify
- Lower `RAVINE_KERNEL_SIGMA_M` (e.g. 5.0) — sharper edges, less smoothing

If the ravine detection doesn't trigger at all (0 qualifying regions):
- Check console output. If candidate regions exist but all REJECTED for
  "overlaps playable", raise `RAVINE_MAX_PLAYABLE_FRAC` slightly (e.g.
  0.10). This is the knob that protects playable smoothness, be cautious.

If random false positives appear on other holes:
- Raise `RAVINE_MIN_AREA_CELLS` (e.g. 4000)
- Raise `RAVINE_MIN_DEPTH_M` (e.g. 4.0)

### Verification

```bash
cd Tools/UHoleGeo
node scripts/generate-terrain.mjs lomond-country-club 7
```

Expected console output includes lines like:
```
  Ravine detection: N candidate regions, K qualifying for carve
    Carve Region #R: area=… cells, playable=…%, depth=…m
  Ravine carving: sigma=8m (… cells), kernel radius=… cells, size=…
```

Then run `--all` to confirm nothing breaks on other holes:

```bash
node scripts/generate-terrain.mjs lomond-country-club --all
```

Expected: most holes log `0 qualifying for carve`. A few may log
1-2 regions. No errors, no crashes.

Then in Unity: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] Ravine visible in the OB area on Hole 7 (roughly horizontal in the rotated-canvas view)
- [ ] Ravine position matches the GSI raw heightmap capture
- [ ] Fairway stays smooth — no bumps, no diagonal artifact
- [ ] No visible cliff where the ravine meets surrounding terrain
- [ ] Green and tee areas unchanged
- [ ] Cart paths, bunkers, water all unaffected
- [ ] No mesh poke-through on overlays
- [ ] No console errors

Spot-check Hole 1 and Hole 4 to confirm nothing changed:

- [ ] Hole 1 looks the same as before
- [ ] Hole 4 still shows its terraces
- [ ] No new false-positive ravines on either hole

---

## Completed Tasks

✅ 2026-04-15 — Fritsch-Carlson monotone spline + 20 samples. Preserves terraces without overshoot.
✅ 2026-04-15 — Cubic spline (natural) along tee→green axis + quadratic cross-axis. Better heights overall but terraces still rounded off by cubic overshoot + sparse sampling.
✅ 2026-04-15 — Per-zone residual blending with Gaussian blur (reverted — zone boundary artifacts)
✅ 2026-04-17 — Ravine carving via connected-component detection + Gaussian blur carve. Hole 7: 4 qualifying regions (depths -12 to -28m). All 18 holes pass with no errors. Ready for Unity visual verification.
