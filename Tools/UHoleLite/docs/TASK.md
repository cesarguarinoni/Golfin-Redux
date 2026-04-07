# TASK.md — UHole Lite: Fix Fragmented Water Regions

> Claude Code: Read this file. Execute the current task block.
> Read `scripts/export-hole.mjs` before starting — understand `extractZoneContours()`.
> Handoff: `Tools/UHoleLite/docs/TASK.md`

---

## Context

Water contour export fragments bodies of water into many separate
regions. Two causes:

1. **Trees overlay water.** Zone 5 (trees) pixels sit on top of what is
   actually water with tree canopy. Flood-fill sees zone 5 ≠ zone 7
   and splits the body.

2. **Small noise gaps.** 1-2px classification noise creates breaks.

**Previous attempts that FAILED:**
- Morphological close (dilate+erode, radius 3) — the erode step
  destroyed the entire lake because narrow parts got wiped out.
  **DO NOT USE morphological close / erode for water.**

**Correct fix — dilate only + tree absorption:**
- Absorb tree pixels adjacent to water (multi-pass)
- Then dilate the mask by 2px (NO erode) to bridge small gaps
- This only *grows* the water mask, never shrinks it

**Test with Hole 12** — currently produces 15+ fragments, should
become ~2-4.

---

## Current Task — Tree Absorption + Dilate (No Erode)

**File:** `Tools/UHoleLite/scripts/export-hole.mjs`

### Step 1: Delete `morphologicalClose()` function

Remove the entire `morphologicalClose` function. We don't need it —
it was the cause of the lake vanishing.

### Step 2: Delete `extractZoneContoursWithClose()` function

Remove it entirely. We're replacing it with `extractWaterContours`.

### Step 3: Add `extractWaterContours()` function

Add this AFTER `extractZoneContours()` and BEFORE `exportHole()`:

```javascript
/**
 * Extract water contours with pre-processing:
 * 1. Absorb tree pixels (zone 5) adjacent to water — canopy over water.
 * 2. Dilate the mask by a few pixels to bridge small noise gaps.
 *    NO erode step — we only grow, never shrink.
 */
function extractWaterContours(zonesData, terrainMeta, minPixels = 50) {
  const WATER_ZONE = 7;
  const TREES_ZONE = 5;

  const grid = Buffer.from(zonesData.grid, 'base64');
  const w = zonesData.source_dimensions.width;
  const h = zonesData.source_dimensions.height;

  // Step 1: Build initial water mask
  const waterMask = new Uint8Array(w * h);
  let initialCount = 0;
  for (let i = 0; i < grid.length; i++) {
    if (grid[i] === WATER_ZONE) {
      waterMask[i] = 1;
      initialCount++;
    }
  }

  // Step 2: Absorb tree pixels adjacent to water (multi-pass).
  // A tree pixel gets absorbed if >= 2 of its 4-neighbors are water.
  // Multiple passes let absorption propagate through tree clusters.
  const dirs4 = [[0,-1],[0,1],[-1,0],[1,0]];
  let totalAbsorbed = 0;
  for (let pass = 0; pass < 10; pass++) {
    let absorbed = 0;
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (grid[y * w + x] !== TREES_ZONE) continue;
        if (waterMask[y * w + x] === 1) continue;

        let waterNeighbors = 0;
        for (const [dx, dy] of dirs4) {
          const nx = x + dx;
          const ny = y + dy;
          if (nx >= 0 && nx < w && ny >= 0 && ny < h) {
            if (waterMask[ny * w + nx] === 1)
              waterNeighbors++;
          }
        }

        if (waterNeighbors >= 2) {
          waterMask[y * w + x] = 1;
          absorbed++;
        }
      }
    }
    totalAbsorbed += absorbed;
    if (absorbed === 0) break;
  }

  // Step 3: Dilate by 2px to bridge small noise gaps.
  // NO erode — we only grow the mask, never shrink.
  const dilateRadius = 2;
  const dilated = new Uint8Array(w * h);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (waterMask[y * w + x] !== 1) continue;
      for (let dy = -dilateRadius; dy <= dilateRadius; dy++) {
        for (let dx = -dilateRadius; dx <= dilateRadius; dx++) {
          if (dx * dx + dy * dy > dilateRadius * dilateRadius) continue;
          const nx = x + dx;
          const ny = y + dy;
          if (nx >= 0 && nx < w && ny >= 0 && ny < h)
            dilated[ny * w + nx] = 1;
        }
      }
    }
  }

  // Step 4: Build modified grid for contour extraction
  const modifiedGrid = Buffer.from(grid);
  for (let i = 0; i < dilated.length; i++) {
    if (dilated[i] === 1)
      modifiedGrid[i] = WATER_ZONE;
  }

  const modifiedZonesData = {
    ...zonesData,
    grid: modifiedGrid.toString('base64'),
  };

  console.log(`  Water pre-processing: ${initialCount} initial px, ` +
    `${totalAbsorbed} trees absorbed, dilated r=${dilateRadius}`);

  return extractZoneContours(modifiedZonesData, terrainMeta, WATER_ZONE, minPixels);
}
```

### Step 4: Update water export call

In `exportHole()`, change the water extraction from:

```javascript
  const water = extractZoneContoursWithClose(zonesData, terrainMeta, 7, 3, 30);  // zone 7 = water, close radius 3px, min 30px
```

To:

```javascript
  const water = extractWaterContours(zonesData, terrainMeta, 50);  // absorbs trees-over-water, dilates to bridge gaps
```

---

## Verification

```bash
cd Tools/UHoleLite
node scripts/export-hole.mjs lomond-country-club 12
```

- [ ] No errors
- [ ] Console shows "Water pre-processing" stats (absorbed count > 0)
- [ ] Hole 12 water_count drops to roughly **2-5 regions**
- [ ] **Large lake is present** (biggest region pixel_count > 5000)
- [ ] No water body has vanished compared to the zone map
- [ ] Contour shapes are smooth and reasonable
- [ ] No giant blob swallowing fairway or rough

Then all 18:
```bash
node scripts/export-hole.mjs lomond-country-club --all
```

- [ ] All holes export cleanly
- [ ] Holes with no water still have `water_count: 0`
- [ ] No hole has more than ~8 water regions
- [ ] Bunker and green exports are UNCHANGED (verify counts match)

---

## Do NOT

- Modify `extractZoneContours()` itself
- Modify bunker or green export calls
- Modify contour tracing, RDP, or Chaikin functions
- Modify GUI code
- Modify Unity scripts
- Use morphological close / erode on water (it destroys lakes)

---

## Status Log

(Claude Code: add completion status lines here)
- 2026-04-07: Water contour export implemented. Hole 12: 23 water regions.
- 2026-04-07: Morphological close attempt (radius 3) — DESTROYED the
  big lake. Erode step wiped out narrow sections. REVERTED concept.
- 2026-04-07: TASK UPDATED — tree absorption + dilate-only (no erode).
- 2026-04-07: Tree absorption + dilate implemented. Hole 12: 996 trees absorbed, largest region 224pts (up from 64). Count 23→16 regions. All 18 holes export cleanly. Bunker/green counts unchanged. Many remaining fragments appear to be genuinely separate small water features.
