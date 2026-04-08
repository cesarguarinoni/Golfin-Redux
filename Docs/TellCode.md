# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Morphological Smooth in classify-zones.mjs

**Goal:** After auto-classification, smooth zone boundaries using
morphological close (dilate → erode) on each zone mask. Done in Node.js
for reliability, runs on all 18 holes automatically.

**File:** `Tools/UHoleLite/scripts/classify-zones.mjs`

---

### Add `morphClose()` function

After the existing `absorbSmallRegions()` function, add:

```javascript
/**
 * Morphological close (dilate → erode) on a per-zone basis.
 * Smooths jagged boundaries without significantly changing zone shapes.
 * Circular kernel for isotropic smoothing.
 */
function morphClose(grid, width, height, radius = 3) {
  const result = new Uint8Array(width * height);
  result.fill(0); // background

  // Process zones in priority order (higher priority overwrites lower)
  // rough < trees < semi_rough < fairway < tee_box < green < bunker < water < cart_path < ob
  const zonePriority = [4, 5, 3, 1, 10, 2, 6, 7, 8, 9];

  for (const zone of zonePriority) {
    // Build binary mask
    const mask = new Uint8Array(width * height);
    let count = 0;
    for (let i = 0; i < width * height; i++) {
      if (grid[i] === zone) { mask[i] = 1; count++; }
    }
    if (count === 0) continue;

    // Dilate
    const dilated = new Uint8Array(width * height);
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        if (!mask[y * width + x]) continue;
        for (let dy = -radius; dy <= radius; dy++) {
          for (let dx = -radius; dx <= radius; dx++) {
            if (dx * dx + dy * dy > radius * radius) continue;
            const nx = x + dx, ny = y + dy;
            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
              dilated[ny * width + nx] = 1;
          }
        }
      }
    }

    // Erode
    const closed = new Uint8Array(width * height);
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        if (!dilated[y * width + x]) continue;
        let allSet = true;
        for (let dy = -radius; dy <= radius && allSet; dy++) {
          for (let dx = -radius; dx <= radius && allSet; dx++) {
            if (dx * dx + dy * dy > radius * radius) continue;
            const nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= width || ny < 0 || ny >= height ||
                !dilated[ny * width + nx])
              allSet = false;
          }
        }
        if (allSet) closed[y * width + x] = 1;
      }
    }

    // Write to result
    for (let i = 0; i < width * height; i++) {
      if (closed[i]) result[i] = zone;
    }
  }

  return result;
}
```

### Hook into `classifyHole()`

After Phase 2b (absorb small regions), before Phase 2c (mark tee boxes),
add:

```javascript
  // Phase 2b2: Morphological close — smooth zone boundaries
  grid = morphClose(grid, width, height, 3);
```

### Also: remove `smoothBoundaries()` if it exists

If the previous `smoothBoundaries()` function and its Phase 2b2 call
are still in the file, delete them. `morphClose()` replaces it.

### Also: remove broken straighten code from app.js

Remove from `Tools/UHoleLite/app/app.js`:
- `straightenBoundaries()` and all its helpers (`traceBorderPixels`,
  `rdpSimplify`, `perpDist`, `chaikinSmooth`, `scanlineFill`,
  `dilateMask`, `erodeMask`)
- The "Straighten Edges" button handler
- The button element from `index.html`

Keep: PNG import, SVG import (if added).

---

### Verification

1. Reclassify: `node scripts/classify-zones.mjs lomond-country-club 1`
   - [ ] No errors
   - [ ] Check `zones.png` — boundaries smoother than before

2. Compare before/after `zones.png` for the same hole

3. Reclassify all: `node scripts/classify-zones.mjs lomond-country-club --all`
   - [ ] All 18 holes process without errors

4. Re-export + re-import a hole — verify splatmap edges are smoother

### Tuning

`radius = 3` should be a good default. If too aggressive (eating small
features), try `radius = 2`. If not smooth enough, try `radius = 4`.

### Do NOT

- Modify export pipeline
- Modify Unity importer
- Modify the majority filter or absorption steps

✅ DONE: 2026-04-08 — morphClose() in classify-zones.mjs replaces smoothBoundaries(), all 18 holes OK
