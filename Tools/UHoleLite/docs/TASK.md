# TASK.md — Instructions for Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Full design rationale: `Docs/TERRAIN_RELIEF_PLAN.md`

---

## Current Task — Per-Zone DEM Residual Blending

The terrain is too flat. `generateHeightmapDEM` fits a single quadratic
surface to all playable zones and discards 100% of DEM detail. Holes
with real elevation changes (uphill/downhill) play as gentle bowls.

Fix: blend DEM residual back per-zone with heavy blur to keep large-
scale elevation trends while suppressing 5m-grid noise.

**File:** `Tools/UHoleLite/scripts/generate-terrain.mjs`
**Function:** `generateHeightmapDEM()`

### What to do

Replace the entire "Add residual variation to non-playable zones"
section — everything from:

```javascript
// Add residual variation to non-playable zones (trees, OB, background)
```

Through and including:

```javascript
console.log(`  Residual ramp: ${RESIDUAL_RAMP_CELLS} cells transition, ` +
    `${(RESIDUAL_FRACTION * 100).toFixed(0)}% max fraction`);
```

And ALSO remove the per-zone `zoneMaskedSmooth` calls that come right
after (the block starting with `// Smooth residual zones`):

```javascript
for (const z of [ZONES.trees, ZONES.ob, ZONES.background]) {
    zoneMaskedSmooth(heightmap, zoneGrid, zw, zh, z, 5, RES);
}
```

Replace ALL of the above with:

```javascript
  // --- Per-zone residual blending ---
  // Blend DEM residual back with zone-specific fractions.
  // Heavy blur removes 5m DEM grid noise, keeps large-scale slopes.

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

    // Copy residual for this zone
    const zoneRes = new Float64Array(RES * RES);
    for (let i = 0; i < RES * RES; i++) {
      zoneRes[i] = mask[i] ? residual[i] : 0;
    }

    // Zone-masked blur (same kernel as zoneMaskedSmooth)
    let src = zoneRes;
    for (let p = 0; p < cfg.blur; p++) {
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

    // Blend blurred residual back into heightmap
    for (let i = 0; i < RES * RES; i++) {
      if (mask[i]) {
        heightmap[i] += src[i] * cfg.fraction;
      }
    }

    const zoneName = Object.keys(ZONES).find(k => ZONES[k] === zone);
    console.log(`    ${zoneName}: residual ${(cfg.fraction * 100).toFixed(0)}%, blur ${cfg.blur} passes`);
  }
```

### Also remove these constants (no longer used)

Delete these lines near the removed section:

```javascript
const RESIDUAL_FRACTION = 0.75;
const RESIDUAL_RAMP_CELLS = 60;
```

### What NOT to change

- Quadratic surface fitting (fitQuadratic, evalQuadratic)
- Green slope (applyGreenSlope)
- Normalization (25m max range, scaleFactor)
- Final blur2D pass
- Water handling
- Heightmap rotation or uint16 encoding
- Perlin fallback path (generateHeightmap)
- Any existing helper functions (buildZoneMask, etc.)

### Verification

```bash
cd Tools/UHoleLite
node scripts/generate-terrain.mjs lomond-country-club 1
node scripts/generate-terrain.mjs lomond-country-club 4
```

Expected console output should now show per-zone residual lines like:
```
    fairway: residual 30%, blur 15 passes
    semi_rough: residual 40%, blur 10 passes
    rough: residual 50%, blur 8 passes
    trees: residual 75%, blur 5 passes
    ...
```

Then in Unity: `Import > Lite > Normal > Import Hole 01 Lite` and
`Import Hole 04 Lite`

- [ ] Hole 4: visible elevation change tee→green
- [ ] Fairway follows overall slope, no 5m staircase bumps
- [ ] Rough has gentle mounds (not pancake flat)
- [ ] Trees/OB mountainous (similar to before)
- [ ] No cliff at fairway↔rough boundary
- [ ] Green still flat
- [ ] Water/bunker overlays unaffected
- [ ] No console errors

---

## Completed Tasks
✅ 2026-04-13 — Taper strip at T-junction endpoints
✅ 2026-04-13 — Distance-based residual ramp at play/non-play boundary
✅ DONE: 2026-04-14 Per-zone DEM residual blending — replaced ramped-distance residual with per-zone fraction+blur config in generateHeightmapDEM; verified holes 1 & 4 generate with expected per-zone console output.
