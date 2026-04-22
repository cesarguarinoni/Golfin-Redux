# Terrain Relief Plan

## Problem

`generateHeightmapDEM` fits a single quadratic surface to all playable
zones and throws away 100% of DEM detail. The terrain looks flat — no
uphill/downhill between tee and green, no ridges or valleys in the
rough. Hole 4 has 38m of real elevation change (109–147m ASL) but
plays as a gentle bowl.

The quadratic captures the *average* slope but not the shape. A hole
that goes uphill, then flat, then steeply down gets averaged into one
smooth curve.

## Goal

- Terrain should reflect real elevation changes between tee and green
- Rough/semi-rough/OB areas should have natural ridges and mounds
- Fairway can stay smooth (no bumps) but must follow the overall slope
- Green stays near-flat with its existing directional slope
- No hard cliffs between zones

## Current Architecture

```
rawDem[]  →  fitQuadratic(playable zones)  →  holeSurface
                                                 ↓
                              heightmap = quadratic everywhere
                                                 ↓
                   non-playable: blend back 75% DEM residual
                   playable: 0% residual (pure quadratic)
                                                 ↓
                              green slope  →  normalize  →  blur
```

## Proposed Change

Replace the binary playable/non-playable residual split with
**per-zone residual fractions** and **heavy Gaussian blur on the
playable residual** to preserve elevation trends while removing
5m-grid bumps.

### Per-Zone Residual Fractions

| Zone | Residual | Blur passes | Rationale |
|------|----------|-------------|-----------|
| green | 0% | — | Flat putting surface |
| tee_box | 0% | — | Level tee |
| fairway | 30% | 15 | Follows slope, no bumps |
| bunker | 0% | — | Bowl mesh handles shape |
| cart_path | 0% | — | Flat strip |
| semi_rough | 40% | 10 | Gentle mounding |
| rough | 50% | 8 | Natural terrain feel |
| water | 0% | — | Flat mesh, Unity handles |
| trees | 75% | 5 | Mountainous backdrop (unchanged) |
| ob | 75% | 5 | Mountainous backdrop (unchanged) |
| background | 75% | 5 | Mountainous backdrop (unchanged) |

### How It Works

1. **Raw DEM** → same as now
2. **Fit quadratic** to playable zones → same as now
3. **Compute residual** per cell: `residual = rawDem - quadratic`
4. **Per-zone blur**: blur the residual array within each zone mask.
   High blur (15 passes) for fairway removes 5m-grid noise but
   preserves the 50m–100m scale elevation trends (uphill stretches,
   valley crossings). Low blur (5 passes) for trees keeps terrain detail.
5. **Blend**: `height = quadratic + blurredResidual * zoneFraction`
6. **Boundary ramp**: at zone boundaries, interpolate between the
   two neighboring fractions over ~20 cells to avoid cliff edges
7. **Green slope** → same as now
8. **Normalize** → same as now

### Key Insight: Blur Scale vs DEM Resolution

DEM5A is 5m resolution. The 2049-cell heightmap covers ~500m terrain,
so each cell ≈ 0.25m. A 3×3 blur pass has a radius of ~0.75m. After
15 passes the effective radius is ~3–4m — enough to smooth out the
5m grid artifacts but preserves features larger than ~10m.

For fairway, we want to keep features at the 50m+ scale (a hill that
rises over 50 yards of play). 15 blur passes on the residual keeps
that while removing the staircase pattern from 5m DEM cells.

## Changes Required

### File: `Tools/UHoleLite/scripts/generate-terrain.mjs`

**In `generateHeightmapDEM()`**, after the quadratic surface is computed
and applied to the entire heightmap:

Replace the current "Add residual variation to non-playable zones"
section (Steps A/B/C) with:

```javascript
// --- Per-zone residual blending ---

const ZONE_RESIDUAL = {
  [ZONES.green]:      { fraction: 0.0,  blur: 0  },
  [ZONES.tee_box]:    { fraction: 0.0,  blur: 0  },
  [ZONES.fairway]:    { fraction: 0.30, blur: 15 },
  [ZONES.bunker]:     { fraction: 0.0,  blur: 0  },
  [ZONES.cart_path]:  { fraction: 0.0,  blur: 0  },
  [ZONES.semi_rough]: { fraction: 0.40, blur: 10 },
  [ZONES.rough]:      { fraction: 0.50, blur: 8  },
  [ZONES.water]:      { fraction: 0.0,  blur: 0  },
  [ZONES.trees]:      { fraction: 0.75, blur: 5  },
  [ZONES.ob]:         { fraction: 0.75, blur: 5  },
  [ZONES.background]: { fraction: 0.75, blur: 5  },
};

// Build per-cell residual
const residual = new Float64Array(RES * RES);
for (let i = 0; i < RES * RES; i++) {
  residual[i] = rawDem[i] - heightmap[i]; // rawDem - quadratic
}

// For each zone that has residual > 0, blur residual within zone
// mask and blend it back
for (const [zoneStr, config] of Object.entries(ZONE_RESIDUAL)) {
  const zone = parseInt(zoneStr);
  if (config.fraction <= 0) continue;

  const mask = buildZoneMask(zoneGrid, zw, zh, zone, RES);

  // Check if zone has any cells
  let hasCells = false;
  for (let i = 0; i < RES * RES; i++) {
    if (mask[i]) { hasCells = true; break; }
  }
  if (!hasCells) continue;

  // Copy residual for this zone, blur it
  const zoneResidual = new Float64Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    zoneResidual[i] = mask[i] ? residual[i] : 0;
  }

  // Zone-masked blur
  let src = zoneResidual;
  for (let p = 0; p < config.blur; p++) {
    const dst = new Float64Array(src);
    for (let hy = 0; hy < RES; hy++) {
      for (let hx = 0; hx < RES; hx++) {
        const idx = hy * RES + hx;
        if (!mask[idx]) continue;
        let sum = 0, weight = 0;
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            const nx = hx + dx, ny = hy + dy;
            if (nx < 0 || nx >= RES || ny < 0 || ny >= RES) continue;
            const w = (dx === 0 && dy === 0) ? 4 :
                      (dx === 0 || dy === 0) ? 2 : 1;
            sum += src[ny * RES + nx] * w;
            weight += w;
          }
        }
        dst[idx] = sum / weight;
      }
    }
    src = dst;
  }

  // Blend back
  for (let i = 0; i < RES * RES; i++) {
    if (mask[i]) {
      heightmap[i] += src[i] * config.fraction;
    }
  }

  const zoneName = Object.keys(ZONES).find(k => ZONES[k] === zone);
  console.log(`    ${zoneName}: residual ${(config.fraction*100).toFixed(0)}%, blur ${config.blur} passes`);
}

// Boundary smoothing: global blur pass handles zone transitions
// (existing blur2D at the end already does this)
```

**Remove** the old block:
- The `RESIDUAL_FRACTION`, `RESIDUAL_RAMP_CELLS` constants
- Steps A/B/C (playable mask, distance transform, ramped residual)
- The `zoneMaskedSmooth` calls for trees/ob/background after Step C

**Remove** the old per-zone `zoneMaskedSmooth` calls (the blurring is
now integrated into the residual step).

### What NOT to change

- Quadratic surface fitting logic
- Green slope (`applyGreenSlope`)
- Normalization (25m max range)
- Final blur pass
- Water handling
- Heightmap rotation or uint16 encoding

## Geo-Align Quality Warning

Hole 4 has mean residual 13.96m (max 27.3m) — the affine transform
is very inaccurate. DEM samples are being pulled from ~14m-offset
locations. This means the elevation contours for Hole 4 won't match
reality well. But even with misaligned DEM, the *general slope
direction and magnitude* will be more correct than a flat quadratic.

Holes with >5m mean residual should probably be re-aligned in
GeoAlign tool before we do a final all-18 terrain pass.

## Verification

Re-generate Hole 1: `node scripts/generate-terrain.mjs lomond-country-club 1`
Re-generate Hole 4: `node scripts/generate-terrain.mjs lomond-country-club 4`
Then re-import in Unity: `Import > Lite > Normal > Import Hole 01/04 Lite`

- [ ] Hole 4: visible elevation change from tee to green
- [ ] Fairway follows overall slope (no flat plateau)
- [ ] No 5m staircase artifacts on fairway
- [ ] Rough/semi-rough has gentle mounds
- [ ] Trees/OB areas have mountainous backdrop (unchanged feel)
- [ ] No cliff at fairway↔rough boundary
- [ ] Green still flat (putting surface)
- [ ] Water, bunkers, overlays unaffected

## Handoff

This task is for **Claude Code via `Tools/UHoleLite/docs/TASK.md`**.
Only `generate-terrain.mjs` changes. No Unity-side changes needed.
