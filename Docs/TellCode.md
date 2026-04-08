# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Fix Straighten Edges (Morphological Close Approach)

**Problem:** The vectorize→rasterize approach completely broke zone
shapes. Revert it and replace with morphological close (dilate → erode).

**Approach:** For each zone (as a binary mask):
1. **Dilate** by N pixels (fills jagged indentations)
2. **Erode** by N pixels (restores original size, but edges are now smooth)
3. Write the smoothed mask back to the grid

This is a standard morphological close operation. Process zones in
priority order so higher-priority zones overwrite lower ones.

**File:** `Tools/UHoleLite/app/app.js`

---

### Replace the `straightenBoundaries()` function entirely

Delete all the helper functions added in the previous attempt
(`traceBorderPixels`, `rdpSimplify`, `perpDist`, `chaikinSmooth`,
`scanlineFill`). Replace `straightenBoundaries()` with:

```javascript
function straightenBoundaries() {
  const w = zoneGridW, h = zoneGridH;
  const result = new Uint8Array(w * h);
  result.fill(0); // start with background

  // Process zones in priority order (higher overwrites lower)
  const zonePriority = [4, 5, 3, 1, 10, 2, 6, 7, 8, 9];
  const radius = 3; // dilate/erode radius

  for (const zone of zonePriority) {
    // Build binary mask for this zone
    const mask = new Uint8Array(w * h);
    for (let i = 0; i < w * h; i++) {
      if (zoneGrid[i] === zone) mask[i] = 1;
    }

    // Dilate (expand)
    const dilated = dilateMask(mask, w, h, radius);
    // Erode (shrink back)
    const closed = erodeMask(dilated, w, h, radius);

    // Write to result where mask is set
    for (let i = 0; i < w * h; i++) {
      if (closed[i]) result[i] = zone;
    }
  }

  zoneGrid.set(result);
}

function dilateMask(mask, w, h, radius) {
  const result = new Uint8Array(w * h);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (!mask[y * w + x]) continue;
      for (let dy = -radius; dy <= radius; dy++) {
        for (let dx = -radius; dx <= radius; dx++) {
          if (dx * dx + dy * dy > radius * radius) continue;
          const nx = x + dx, ny = y + dy;
          if (nx >= 0 && nx < w && ny >= 0 && ny < h)
            result[ny * w + nx] = 1;
        }
      }
    }
  }
  return result;
}

function erodeMask(mask, w, h, radius) {
  const result = new Uint8Array(w * h);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (!mask[y * w + x]) continue;
      // Check if ALL pixels in the circular kernel are set
      let allSet = true;
      for (let dy = -radius; dy <= radius && allSet; dy++) {
        for (let dx = -radius; dx <= radius && allSet; dx++) {
          if (dx * dx + dy * dy > radius * radius) continue;
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || nx >= w || ny < 0 || ny >= h || !mask[ny * w + nx])
            allSet = false;
        }
      }
      if (allSet) result[y * w + x] = 1;
    }
  }
  return result;
}
```

Delete the old helper functions: `traceBorderPixels`, `rdpSimplify`,
`perpDist`, `chaikinSmooth`, `scanlineFill`.

---

### Verification

- [ ] Click "Straighten Edges" in Hole Viewer
- [ ] Zone shapes are preserved (no distortion like before)
- [ ] Jagged edges are smoothed/rounded
- [ ] Small features preserved (bunkers, tee boxes)
- [ ] Undo works
- [ ] Can save the result

### Do NOT

- Modify export pipeline
- Modify Unity importer

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Texture cleanup: fairway/fringe swap, dark fringe, blur removed, fringe ring
✅ DONE: 2026-04-08 — Straighten Edges v1 (vectorize — broke shapes, needs replacement)
✅ DONE: 2026-04-08 — Straighten Edges v2 (morphological close: dilate→erode, radius 3)
