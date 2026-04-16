# TASK.md — Instructions for Claude Code (UHole Geo)

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Full design rationale: `Docs/TERRAIN_SPLINE_PLAN.md`

---

## Current Task — Monotone Spline + Denser Sampling for Terraces

The natural cubic spline is working (better overall heights) but
**terraces are still missing**. Two reasons:

1. **Natural cubic splines overshoot.** When DEM data shows
   flat→drop→flat (a terrace), the cubic creates a smooth S-curve
   that rounds off the shelf edges. The flat sections get pulled
   up/down by the spline's tendency to minimize curvature globally.

2. **10 sample points is too sparse.** On a 200m hole, that's one
   sample per 20m. A 5m-wide terrace edge falls between samples
   and gets smoothed away.

### Fix: Replace `naturalCubicSpline` with Fritsch-Carlson Monotone Interpolation + Bump to 20 Samples

Fritsch-Carlson preserves the monotonicity of the data between each
pair of points. If two consecutive samples have the same elevation,
the interpolant stays flat between them. If there's a drop, it drops
monotonically without overshooting. This is exactly what terraces
need: flat→steep→flat, not flat→overshoot→undershoot→flat.

### Step 1: Replace `naturalCubicSpline` in `lib/terrain.mjs`

Replace the entire `naturalCubicSpline` function with:

```javascript
/**
 * Fritsch-Carlson monotone cubic interpolation.
 *
 * Unlike natural cubic splines, this preserves the monotonicity of
 * each interval: flat data stays flat, drops stay monotone. This
 * prevents overshoot and preserves terrace-like elevation profiles.
 *
 * @param {number[]} xs - sorted x coordinates (strictly increasing)
 * @param {number[]} ys - y values at each x
 * @returns {function(number): number} interpolation function
 */
export function monotoneCubicSpline(xs, ys) {
  const n = xs.length;
  if (n < 2) return () => ys[0] || 0;
  if (n === 2) {
    const slope = (ys[1] - ys[0]) / (xs[1] - xs[0]);
    return (x) => {
      if (x <= xs[0]) return ys[0];
      if (x >= xs[1]) return ys[1];
      return ys[0] + slope * (x - xs[0]);
    };
  }

  // Step 1: Compute secants (slopes between consecutive points)
  const delta = new Float64Array(n - 1);
  for (let i = 0; i < n - 1; i++) {
    delta[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
  }

  // Step 2: Initialize tangents using average of neighbouring secants
  const m = new Float64Array(n);
  m[0] = delta[0];
  m[n - 1] = delta[n - 2];
  for (let i = 1; i < n - 1; i++) {
    if (delta[i - 1] * delta[i] <= 0) {
      // Sign change or zero — set tangent to zero (local extremum)
      m[i] = 0;
    } else {
      m[i] = (delta[i - 1] + delta[i]) / 2;
    }
  }

  // Step 3: Fritsch-Carlson monotonicity correction
  for (let i = 0; i < n - 1; i++) {
    if (Math.abs(delta[i]) < 1e-30) {
      // Flat segment — force both tangents to zero
      m[i] = 0;
      m[i + 1] = 0;
    } else {
      const alpha = m[i] / delta[i];
      const beta = m[i + 1] / delta[i];

      // Check if (alpha, beta) falls outside the monotonicity region
      // The region is: alpha² + beta² <= 9
      const h = Math.sqrt(alpha * alpha + beta * beta);
      if (h > 3) {
        const tau = 3 / h;
        m[i] = tau * alpha * delta[i];
        m[i + 1] = tau * beta * delta[i];
      }
    }
  }

  // Step 4: Build Hermite basis evaluation function
  return function (x) {
    if (x <= xs[0]) return ys[0];
    if (x >= xs[n - 1]) return ys[n - 1];

    // Binary search for interval
    let lo = 0, hi = n - 2;
    while (lo < hi) {
      const mid = (lo + hi) >> 1;
      if (xs[mid + 1] < x) lo = mid + 1;
      else hi = mid;
    }
    const i = lo;

    // Hermite interpolation on [xs[i], xs[i+1]]
    const h = xs[i + 1] - xs[i];
    const t = (x - xs[i]) / h;
    const t2 = t * t;
    const t3 = t2 * t;

    const h00 = 2 * t3 - 3 * t2 + 1;
    const h10 = t3 - 2 * t2 + t;
    const h01 = -2 * t3 + 3 * t2;
    const h11 = t3 - t2;

    return h00 * ys[i] + h10 * h * m[i] +
           h01 * ys[i + 1] + h11 * h * m[i + 1];
  };
}
```

Also update the export at the bottom of `lib/terrain.mjs`. If exports
are at the top via `export function`, this is already handled. If
there's an explicit export block, make sure `monotoneCubicSpline` is
exported and `naturalCubicSpline` is removed (or kept but unused).

### Step 2: Update Import in `generate-terrain.mjs`

Change:
```javascript
import { perlin2D, blur2D, naturalCubicSpline } from './lib/terrain.mjs';
```

To:
```javascript
import { perlin2D, blur2D, monotoneCubicSpline } from './lib/terrain.mjs';
```

### Step 3: Update Spline Usage in `generateTerrainDEM()`

Find:
```javascript
  const N_SPLINE_POINTS = 10;
```

Change to:
```javascript
  const N_SPLINE_POINTS = 20;
```

Find (the spline construction call):
```javascript
  const spline = naturalCubicSpline(splineXs, splineYs);
```

Change to:
```javascript
  const spline = monotoneCubicSpline(splineXs, splineYs);
```

That's it — the rest of the spline pipeline (sampling, axis projection,
cross-axis residual) stays exactly the same.

### What NOT to Change

- Axis computation (tee→green centroids, unit vectors)
- DEM sampling loop (splineXs/splineYs construction)
- Heightmap construction loop (spline + quadratic cross-axis)
- Everything downstream (normalization, blur, uint16, row-flip)
- Perlin fallback path
- `fitQuadratic` / `evalQuadratic`

### Verification

```bash
cd Tools/UHoleGeo
node scripts/generate-terrain.mjs lomond-country-club 4
node scripts/generate-terrain.mjs lomond-country-club 1
node scripts/generate-terrain.mjs lomond-country-club --all
```

Expected: Hole 4's elevation log should show **20 samples** with
clearer flat→drop→flat patterns. The monotone spline should NOT
overshoot between consecutive samples of equal elevation.

Compare Hole 4 elevation log before/after:
- **Before (10 pts, natural):** smooth curve, terraces rounded off
- **After (20 pts, monotone):** flat sections stay flat, drops are
  steeper and more defined

Then in Unity: `Import > Geo > Normal > Import Hole 04 Geo`

- [ ] Visible terrace/shelf on Hole 4 fairway
- [ ] Flat sections of fairway actually look flat
- [ ] Drops between terraces look like real terrain steps
- [ ] No overshoot (terrain shouldn't dip below the lower terrace)
- [ ] All overlay meshes intact
- [ ] No console errors
- [ ] All 18 holes generate without errors (`--all`)

---

## Completed Tasks

✅ 2026-04-15 — Cubic spline (natural) along tee→green axis + quadratic cross-axis. Better heights overall but terraces still rounded off by cubic overshoot + sparse sampling.
✅ 2026-04-15 — Per-zone residual blending with Gaussian blur (reverted — zone boundary artifacts)
