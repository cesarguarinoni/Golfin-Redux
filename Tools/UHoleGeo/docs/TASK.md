# TASK.md — Instructions for Claude Code (UHole Geo)

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Full design rationale: `Docs/TERRAIN_SPLINE_PLAN.md`

---

## Current Task — Fix Ravine Detection (High-Pass Against Blurred DEM)

The ravine carving code from 2026-04-17 runs, but the ravine appears
in the WRONG PLACE on Hole 7 — it comes out diagonal and spills into
the fairway. The raw DEM shows the ravine is actually horizontal and
entirely in OB (GSI raw capture confirms this).

### Why It's Wrong

Current detection compares `rawDem` against the synthetic surface
(spline along tee→green + quadratic cross-axis). On Hole 7, the
tee→green axis is strongly diagonal, and the spline paints a huge
diagonal elevation trough into the synthetic. So:

- Where the real ravine is: `rawDem` ≈ low, `synthetic` ≈ also low (diagonal trough overlaps it here) → small residual → NOT detected
- Where the synthetic is artificially high (the "off-diagonal" side): `rawDem` is normal-flat terrain → big negative residual → FALSE-POSITIVE detected

The carve then happens at those false-positive locations — producing
the diagonal artifact we see in-game.

### The Fix — Detect Against a Blurred DEM, Not the Synthetic

Standard GIS technique: to find sharp local features (ravines,
gullies, mounds), compare the raw DEM against a heavily-smoothed
version of itself. This is a "high-pass filter" or "unsharp mask":

```
ravineResidual = rawDem - blur(rawDem, largeRadius)
```

- The blur removes small-scale noise AND local features
- Subtracting recovers ONLY the local features (ravines become very negative, ridges become very positive)
- The overall elevation trend (hills, slopes, tee→green drop) is in BOTH terms and cancels out
- Crucially: detection is now completely independent of the synthetic surface, so the spline's artifacts can't bias it

The carve step then modifies the synthetic heightmap at the detected
locations — which will now correctly correspond to real features in
the DEM.

### Scope

Only `Tools/UHoleGeo/scripts/generate-terrain.mjs`. Two small changes
to the existing ravine-carving block:

1. Replace Step 1 (residual computation) with DEM high-pass
2. Everything else (candidate mask, flood-fill, region filtering,
   Gaussian carve) stays the same

### Implementation

In the existing ravine carving block, find this section:

```javascript
// Step 1: Compute residual (rawDem - synthetic surface)
// Negative values = cell is below the synthetic surface (ravine candidates)
const ravineResidual = new Float64Array(TOTAL_CELLS);
for (let i = 0; i < TOTAL_CELLS; i++) {
  ravineResidual[i] = rawDem[i] - heightmap[i];
}
```

Replace it with:

```javascript
// Step 1: High-pass filter on rawDem to isolate sharp local features.
// ravineResidual = rawDem - blur(rawDem, largeRadius)
// This is independent of the synthetic surface — the spline can't
// bias the detection.
//
// Blur radius tuned to be much larger than ravine width so the
// ravine is preserved in the residual. ~15m sigma works well:
// big enough to smooth over ravines (5-15m typical width) so the
// blur reflects surrounding high-ground elevation, small enough
// to follow overall hole slope so slope cancels in subtraction.
const RAVINE_DETECT_SIGMA_M = 15.0;
const detectMetersPerCell = ((terrainWidthM + terrainLengthM) / 2) / (RES - 1);
const detectSigmaCells = RAVINE_DETECT_SIGMA_M / detectMetersPerCell;
const detectRadius = Math.max(1, Math.ceil(3 * detectSigmaCells));
const detectKernelSize = 2 * detectRadius + 1;
const detectKernel = new Float64Array(detectKernelSize);
{
  const s2 = 2 * detectSigmaCells * detectSigmaCells;
  let kSum = 0;
  for (let i = 0; i < detectKernelSize; i++) {
    const x = i - detectRadius;
    detectKernel[i] = Math.exp(-(x * x) / s2);
    kSum += detectKernel[i];
  }
  for (let i = 0; i < detectKernelSize; i++) detectKernel[i] /= kSum;
}

// Separable Gaussian blur on rawDem → blurredDem
const TOTAL_CELLS = RES * RES;
const blurredDem = new Float64Array(TOTAL_CELLS);
const tempBuf = new Float64Array(TOTAL_CELLS);

// Horizontal pass: rawDem → tempBuf
for (let hy = 0; hy < RES; hy++) {
  const rowBase = hy * RES;
  for (let hx = 0; hx < RES; hx++) {
    let sum = 0;
    for (let k = -detectRadius; k <= detectRadius; k++) {
      let sx = hx + k;
      if (sx < 0) sx = 0;
      else if (sx >= RES) sx = RES - 1;
      sum += rawDem[rowBase + sx] * detectKernel[k + detectRadius];
    }
    tempBuf[rowBase + hx] = sum;
  }
}

// Vertical pass: tempBuf → blurredDem
for (let hx = 0; hx < RES; hx++) {
  for (let hy = 0; hy < RES; hy++) {
    let sum = 0;
    for (let k = -detectRadius; k <= detectRadius; k++) {
      let sy = hy + k;
      if (sy < 0) sy = 0;
      else if (sy >= RES) sy = RES - 1;
      sum += tempBuf[sy * RES + hx] * detectKernel[k + detectRadius];
    }
    blurredDem[hy * RES + hx] = sum;
  }
}

// Residual: how much each cell differs from the smoothed DEM baseline
// Negative = below surroundings (ravines, pits), positive = above (mounds, ridges)
const ravineResidual = new Float64Array(TOTAL_CELLS);
for (let i = 0; i < TOTAL_CELLS; i++) {
  ravineResidual[i] = rawDem[i] - blurredDem[i];
}

console.log(`  Ravine detection: high-pass against ${RAVINE_DETECT_SIGMA_M}m blur ` +
  `(sigma=${detectSigmaCells.toFixed(1)} cells, radius=${detectRadius})`);
```

**IMPORTANT:** The existing code declares `const TOTAL_CELLS = RES * RES;`
immediately after the original Step 1 block. Since the new Step 1 now
declares `TOTAL_CELLS` inside itself, **remove the duplicate line**
below the new Step 1. Find and delete:

```javascript
const TOTAL_CELLS = RES * RES;
```

(the one that appears between the old Step 1 and Step 2 — NOT the one
now inside the new Step 1).

### Everything Else Stays The Same

Do NOT change:
- Step 2 (candidate mask based on `ravineResidual < -RAVINE_MIN_DEPTH_M`)
- Step 3 (playable mask)
- Step 4 (flood-fill connected components)
- Step 5 (per-region stats, filtering, targetDepth calculation)
- Step 6 (Gaussian carve onto the synthetic `heightmap`)
- Any of the tunable constants (RAVINE_MIN_DEPTH_M, RAVINE_MIN_AREA_CELLS, etc.)

The carve still happens on the synthetic `heightmap` (spline+quadratic).
We're only fixing WHERE the carve happens — the carve mechanism itself
is unchanged.

### Why This Should Work

- `rawDem - blur(rawDem, 30m)` gives a clean signal: ravine cells are very negative, ridge cells are very positive, most cells ~0
- The 30m blur is wide enough to "see over" the ravine (ravines are typically 5-15m wide), so the blur at a ravine cell reflects the surrounding high ground — making the residual strongly negative
- The 30m blur is narrow enough to follow overall terrain slope, so the slope cancels out in the subtraction
- The detection no longer looks at the synthetic surface at all, so spline artifacts can't create false positives

### Tuning Notes

Only tune if Hole 7 still looks wrong after this change.

If no ravine detected at all:
- Lower `RAVINE_DETECT_SIGMA_M` to 10.0 — tighter blur, more sensitive to small features
- Lower `RAVINE_MIN_DEPTH_M` to 2.0 — accept shallower cells

If ravine detected but in wrong shape:
- Log the residual min/max and see where cells below threshold actually are
- Could add a debug: export `ravineResidual` as a PNG for visual inspection

If false positives on other holes:
- Raise `RAVINE_DETECT_SIGMA_M` to 25.0 — wider blur, only very sharp features detected
- Raise `RAVINE_MIN_DEPTH_M` to 4.0

### Verification

```bash
cd Tools/UHoleGeo
node scripts/generate-terrain.mjs lomond-country-club 7
```

Expected console output:
```
  Ravine detection: high-pass against 30m blur (sigma=… cells, radius=…)
  Ravine detection: N candidate regions, K qualifying for carve
    Carve Region #R: area=… cells, playable=…%, depth=…m
```

Run `--all` to check no regressions:

```bash
node scripts/generate-terrain.mjs lomond-country-club --all
```

Then in Unity: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] Ravine appears in OB area of Hole 7
- [ ] Ravine shape matches the GSI raw DEM capture (horizontal band, north of fairway in rotated view)
- [ ] No diagonal trough artifact
- [ ] Fairway smooth, no carve bleeding into playable
- [ ] Other holes unchanged vs previous run

---

## Completed Tasks

✅ 2026-04-17 — Ravine carving via connected-component detection + Gaussian blur carve. Hole 7: 4 qualifying regions (depths -12 to -28m). WRONG LOCATIONS — detection was biased by spline-polluted synthetic.
✅ 2026-04-17 — Switched ravine detection to high-pass against 15m blur of rawDem. Hole 7: 5 qualifying regions (depths -3.5 to -12m). All 18 holes pass. Awaiting Unity visual verification.
✅ 2026-04-15 — Fritsch-Carlson monotone spline + 20 samples. Preserves terraces without overshoot.
✅ 2026-04-15 — Cubic spline (natural) along tee→green axis + quadratic cross-axis. Better heights overall but terraces still rounded off by cubic overshoot + sparse sampling.
✅ 2026-04-15 — Per-zone residual blending with Gaussian blur (reverted — zone boundary artifacts)
