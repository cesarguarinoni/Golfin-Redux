# TASK.md — Instructions for Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`

---

## Current Task — Smooth Residual Ramp at Play/Non-Play Boundary

**File:** `Tools/UHoleLite/scripts/generate-terrain.mjs`
**Function:** `generateHeightmapDEM`

### Problem

The DEM residual (75%) is applied as a **hard switch** at the zone
boundary between playable zones and non-playable zones (trees/OB/
background). Playable zones get pure quadratic surface. Non-playable
zones get quadratic + 75% DEM residual. At the exact pixel where the
zone classification changes, the height jumps by the residual amount
— creating a visible cliff in-game.

The single global `blur2D(..., 1)` pass at the end is not enough to
smooth this discontinuity. The Unity-side boundary height propagation
also can't fully fix it because the gap is already baked into the raw
heightmap file.

### Solution — Distance-based residual ramp

Instead of applying a flat 75% residual fraction to all non-playable
cells, **ramp** the fraction from 0% at the playable boundary to 75%
over a configurable transition distance.

### Exact Changes

Find this block (~line 565-585 in `generateHeightmapDEM`):

```javascript
  // Add residual variation ONLY to non-playable zones (trees, OB, background)
  const residualZones = new Set([
    ZONES.trees, ZONES.ob, ZONES.background,
  ]);
  const RESIDUAL_FRACTION = 0.75; // 75% of real DEM hills in tree/OB/background zones

  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const idx = hy * RES + hx;
      if (heightmap[idx] < -9000) continue;

      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);
      const zx = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zy = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      const zone = zoneGrid[zy * zw + zx];

      if (residualZones.has(zone)) {
        const surfH = evalQuadratic(holeSurface, hx, hy);
        const rawH = rawDem[idx];
        const residual = rawH - surfH;
        heightmap[idx] = surfH + residual * RESIDUAL_FRACTION;
      }
    }
  }
```

Replace with:

```javascript
  // Add residual variation to non-playable zones (trees, OB, background)
  // with a distance-based ramp to avoid hard discontinuity at boundary
  const residualZones = new Set([
    ZONES.trees, ZONES.ob, ZONES.background,
  ]);
  const RESIDUAL_FRACTION = 0.75;
  const RESIDUAL_RAMP_CELLS = 60; // cells over which residual ramps from 0→100%

  // Step A: Build playable mask at heightmap resolution
  const isPlayable = new Uint8Array(RES * RES);
  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);
      const zx = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zy = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      const zone = zoneGrid[zy * zw + zx];
      if (!residualZones.has(zone)) {
        isPlayable[hy * RES + hx] = 1;
      }
    }
  }

  // Step B: Distance transform from playable boundary (chamfer)
  const distFromPlay = new Float64Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    distFromPlay[i] = isPlayable[i] ? 0 : 1e9;
  }
  // Forward pass
  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const idx = hy * RES + hx;
      if (hx > 0)
        distFromPlay[idx] = Math.min(distFromPlay[idx], distFromPlay[idx - 1] + 1);
      if (hy > 0)
        distFromPlay[idx] = Math.min(distFromPlay[idx], distFromPlay[(hy - 1) * RES + hx] + 1);
      if (hx > 0 && hy > 0)
        distFromPlay[idx] = Math.min(distFromPlay[idx], distFromPlay[(hy - 1) * RES + (hx - 1)] + 1.414);
      if (hx < RES - 1 && hy > 0)
        distFromPlay[idx] = Math.min(distFromPlay[idx], distFromPlay[(hy - 1) * RES + (hx + 1)] + 1.414);
    }
  }
  // Backward pass
  for (let hy = RES - 1; hy >= 0; hy--) {
    for (let hx = RES - 1; hx >= 0; hx--) {
      const idx = hy * RES + hx;
      if (hx < RES - 1)
        distFromPlay[idx] = Math.min(distFromPlay[idx], distFromPlay[idx + 1] + 1);
      if (hy < RES - 1)
        distFromPlay[idx] = Math.min(distFromPlay[idx], distFromPlay[(hy + 1) * RES + hx] + 1);
      if (hx < RES - 1 && hy < RES - 1)
        distFromPlay[idx] = Math.min(distFromPlay[idx], distFromPlay[(hy + 1) * RES + (hx + 1)] + 1.414);
      if (hx > 0 && hy < RES - 1)
        distFromPlay[idx] = Math.min(distFromPlay[idx], distFromPlay[(hy + 1) * RES + (hx - 1)] + 1.414);
    }
  }

  // Step C: Apply residual with ramped fraction
  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const idx = hy * RES + hx;
      if (isPlayable[idx]) continue; // playable cells stay as quadratic

      const dist = distFromPlay[idx];
      // Smoothstep ramp: 0 at boundary → 1 at RESIDUAL_RAMP_CELLS
      let t = Math.min(dist / RESIDUAL_RAMP_CELLS, 1.0);
      t = t * t * (3 - 2 * t); // smoothstep

      const fraction = RESIDUAL_FRACTION * t;
      const surfH = evalQuadratic(holeSurface, hx, hy);
      const rawH = rawDem[idx];
      const residual = rawH - surfH;
      heightmap[idx] = surfH + residual * fraction;
    }
  }

  console.log(`  Residual ramp: ${RESIDUAL_RAMP_CELLS} cells transition, ` +
    `${(RESIDUAL_FRACTION * 100).toFixed(0)}% max fraction`);
```

### Key Behavior

- **At boundary (dist=0):** Non-playable cell gets 0% residual =
  pure quadratic surface = same height as adjacent playable cell.
  Zero discontinuity.
- **In transition (0 < dist < 60):** Residual fraction ramps via
  smoothstep from 0% to 75%. Gentle slope outward.
- **Beyond transition (dist ≥ 60):** Full 75% residual. Hills and
  terrain features fully present.
- **Playable zones:** Completely untouched.

### After the ramp

The existing `zoneMaskedSmooth` (5 passes per zone) still runs AFTER
this block. That's fine — it smooths the DEM-grid bumps within the
non-playable zones. The ramp handles the boundary; the blur handles
texture.

The single global `blur2D(..., 1)` pass at the end also still runs.

### Do NOT change

- The quadratic surface fitting
- The playable zones set
- The zoneMaskedSmooth passes
- The normalization / encoding steps
- The green slope logic
- Any other file

### Running

```bash
cd Tools/UHoleLite
node scripts/generate-terrain.mjs lomond-country-club 1
```

Then in Unity: GOLFIN > Import Hole (Lite) > Hole 01

### Verification

1. Run pipeline for Hole 01
2. Import in Unity
3. Walk along the fairway/rough boundary — should be flush, no cliff
4. OB/trees areas farther out should still have natural hills
5. Console should log the ramp stats

---

## Completed Tasks
✅ 2026-04-13 — Distance-based residual ramp at play/non-play boundary (60-cell smoothstep transition)
