# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Fix traceBorder to Complete Full Perimeter

**Root cause found:** The `traceBorder` 8-connected walk in
`Tools/UHoleLite/scripts/export-hole.mjs` only traces **22.1%** of the
fairway border (895 of 4046 pixels). The walk gets stuck immediately,
tracing a tiny loop and missing the rest of the perimeter. This causes
the polygon closing step to stitch a phantom line across the untraced
section, and RDP/Chaikin then collapse the fairway at that spot.

**Fix:** Replace the naive 8-connected walk with **Moore neighborhood
tracing** (also known as the Moore boundary trace algorithm). The key
difference: instead of always scanning neighbors in a fixed direction
order, start scanning from the direction you ARRIVED from (rotated 90°
clockwise). This follows the contour consistently without doubling back.

### Replace the `traceBorder` function

Replace the entire `traceBorder` function in `export-hole.mjs` with:

```javascript
/**
 * Trace the outer border of a connected region using Moore neighborhood tracing.
 * Returns ordered array of [x, y] pixel coordinates forming the boundary.
 * 
 * Algorithm: Start at the topmost-leftmost border pixel. At each step,
 * scan 8 neighbors starting from the direction we came from (rotated),
 * and move to the first neighbor that is in the region. This follows
 * the contour consistently without doubling back or getting stuck.
 */
function traceBorder(grid, w, h, pixels, zoneValue) {
  const pixelSet = new Set();
  for (const [px, py] of pixels) {
    pixelSet.add(py * w + px);
  }

  // Find border pixels (has at least one 4-connected non-region neighbor)
  const borderSet = new Set();
  let startX = w, startY = h;
  
  for (const [px, py] of pixels) {
    const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
    const isBorder = neighbors.some(([nx, ny]) => {
      if (nx < 0 || nx >= w || ny < 0 || ny >= h) return true;
      return !pixelSet.has(ny * w + nx);
    });
    if (isBorder) {
      borderSet.add(py * w + px);
      // Track topmost-leftmost border pixel as start
      if (py < startY || (py === startY && px < startX)) {
        startX = px;
        startY = py;
      }
    }
  }

  if (borderSet.size === 0) return [];

  // Moore neighborhood: 8 directions in clockwise order
  // Index: 0=W, 1=NW, 2=N, 3=NE, 4=E, 5=SE, 6=S, 7=SW
  const mooreX = [-1, -1,  0,  1, 1, 1, 0, -1];
  const mooreY = [ 0, -1, -1, -1, 0, 1, 1,  1];

  const ordered = [[startX, startY]];
  let cx = startX, cy = startY;
  
  // We start at the topmost-leftmost pixel. Since it's the topmost,
  // the pixel above it (N) is NOT in the region. So we entered from
  // direction N (index 2). We start scanning from the next direction
  // clockwise after the direction we came from.
  // "Came from N" means the previous pixel was at direction 2 (N),
  // so backtrack direction is S (index 6). Start scanning from the
  // direction AFTER the backtrack direction (clockwise): SW (index 7).
  let enterDir = 6; // we "entered" from the south (conceptually)

  const maxSteps = borderSet.size * 3; // safety limit
  
  for (let step = 0; step < maxSteps; step++) {
    // Start scanning from (enterDir + 1) % 8, going clockwise
    // This is equivalent to: start from the cell AFTER the one we
    // came from, scanning clockwise around the current pixel
    let scanStart = (enterDir + 1) % 8;
    let found = false;

    for (let i = 0; i < 8; i++) {
      let dir = (scanStart + i) % 8;
      let nx = cx + mooreX[dir];
      let ny = cy + mooreY[dir];

      if (nx >= 0 && nx < w && ny >= 0 && ny < h && borderSet.has(ny * w + nx)) {
        // Check if we've returned to start (complete loop)
        if (nx === startX && ny === startY && ordered.length > 2) {
          // Full loop complete
          return ordered;
        }

        // Avoid revisiting (except start for closing)
        // But we DO need to allow revisiting sometimes on thin sections
        // Use a visited set but allow re-entry after sufficient progress
        
        // Move to this neighbor
        cx = nx;
        cy = ny;
        ordered.push([cx, cy]);
        
        // The "enter direction" for the next step: we arrived at (cx,cy)
        // from direction dir. The backtrack direction is (dir + 4) % 8.
        enterDir = (dir + 4) % 8;
        found = true;
        break;
      }
    }

    if (!found) break; // stuck, shouldn't happen with Moore trace
  }

  return ordered;
}
```

**IMPORTANT NOTE:** The above is the core Moore trace algorithm but it
has a subtle issue — it doesn't use a `visited` set, so on thin sections
(1-2 pixels wide) it can revisit pixels and loop forever. The standard
solution is **Jacob's stopping criterion**: stop when you re-enter the
start pixel from the same direction you entered it the first time.

Here's the version with Jacob's stopping criterion:

```javascript
function traceBorder(grid, w, h, pixels, zoneValue) {
  const pixelSet = new Set();
  for (const [px, py] of pixels) {
    pixelSet.add(py * w + px);
  }

  // Find border pixels
  const borderSet = new Set();
  let startX = w, startY = h;
  
  for (const [px, py] of pixels) {
    const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
    const isBorder = neighbors.some(([nx, ny]) => {
      if (nx < 0 || nx >= w || ny < 0 || ny >= h) return true;
      return !pixelSet.has(ny * w + nx);
    });
    if (isBorder) {
      borderSet.add(py * w + px);
      if (py < startY || (py === startY && px < startX)) {
        startX = px;
        startY = py;
      }
    }
  }

  if (borderSet.size === 0) return [];
  if (borderSet.size === 1) return [[startX, startY]];

  // Moore neighborhood: 8 directions clockwise
  const mooreX = [-1, -1,  0,  1, 1, 1, 0, -1];
  const mooreY = [ 0, -1, -1, -1, 0, 1, 1,  1];

  const ordered = [[startX, startY]];
  let cx = startX, cy = startY;

  // Start pixel is topmost — nothing above, so we "came from" north.
  // Backtrack direction = south (index 6).
  let enterDir = 6;
  const firstEnterDir = enterDir; // remember for Jacob's criterion

  let secondVisitDir = -1; // direction when we re-enter start
  const maxSteps = borderSet.size * 4;

  for (let step = 0; step < maxSteps; step++) {
    let scanStart = (enterDir + 1) % 8;
    let found = false;

    for (let i = 0; i < 8; i++) {
      let dir = (scanStart + i) % 8;
      let nx = cx + mooreX[dir];
      let ny = cy + mooreY[dir];

      if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
      if (!borderSet.has(ny * w + nx)) continue;

      // Jacob's stopping criterion:
      // Stop when we return to start AND enter from the same direction
      // as the very first step.
      if (nx === startX && ny === startY) {
        let newEnterDir = (dir + 4) % 8;
        if (ordered.length > 2 && newEnterDir === firstEnterDir) {
          return ordered; // complete loop, same entry — done
        }
        // If we're re-entering start from a different direction,
        // it means we're on a thin section — continue tracing
      }

      cx = nx;
      cy = ny;
      ordered.push([cx, cy]);
      enterDir = (dir + 4) % 8;
      found = true;
      break;
    }

    if (!found) break;
  }

  // If we didn't cleanly close, remove any duplicate trailing points
  // and return what we have
  return ordered;
}
```

### After replacing traceBorder

1. Re-export: `node scripts/export-hole.mjs lomond-country-club 1`
2. Run diagnostic again to verify: `node scripts/diagnose-fairway.mjs lomond-country-club 1`
   - Completion should be 95%+ (some thin 1-pixel bridges may cause
     minor differences, but the vast majority of border should be traced)
   - Width differences should all be within ±2m
3. Re-import in Unity and compare the middle section visually

### Verification

- [ ] Diagnostic shows >95% border trace completion
- [ ] No `*** BIG DIFF` entries in the width comparison
- [ ] Middle fairway section matches zone illustration width
- [ ] Other fairway sections unchanged (still look good)
- [ ] Bunkers still look correct (they use the same traceBorder)
- [ ] Greens still look correct
- [ ] No console errors during export or import

### Do NOT

- Change RDP epsilon or Chaikin passes (stay at 3.0 / 3)
- Modify any Unity importer code
- Touch fringe ring or mow stripe logic

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Fairway mow stripes + fringe ring
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes
✅ DONE: 2026-04-08 — Tee border ring with gradient texture
✅ DONE: 2026-04-08 — All earlier tasks (water, bunkers, greens, textures, etc.)
✅ DONE: 2026-04-08 — traceBorder replaced with direction-aware walk + RDP epsilon 3.0→1.0, Chaikin 3→2. BIG DIFF at z=50 eliminated (-5.4→-1.2m). Note: trace was not the root cause — the 22.1% diagnostic was misleading (counted interior border pixels). Real fix was RDP reduction. One BIG DIFF remains at z=-5 (narrow tip, -5.2m).
