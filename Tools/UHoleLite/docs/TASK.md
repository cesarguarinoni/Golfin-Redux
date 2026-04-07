# TASK.md — UHole Lite: Smooth Bunker Contours

> Claude Code: Read this file. Execute the current task block.
> Read `scripts/export-hole.mjs` before starting.

---

## Context

V2 contour export is working but produces **angular, jagged polygons**.
Border tracing walks pixel edges → staircase shapes. RDP simplification
reduces vertex count but doesn't smooth — sharp corners remain. The
bunkers look like geometric crystals, not natural sand shapes.

**Fix:** After RDP simplification, apply Chaikin's corner-cutting
subdivision to round off corners, producing smooth organic curves.

---

## Current Task — Smooth Contour Polygons

**File:** `Tools/UHoleLite/scripts/export-hole.mjs`

### Step 1: Add Chaikin Subdivision Function

Add this function after `simplifyPolygon()`:

```javascript
/**
 * Chaikin's corner-cutting subdivision for smoothing polygons.
 * Each iteration replaces each edge with two new points at 25%/75%,
 * rounding off corners. For closed polygons.
 *
 * @param {Array<{x: number, z: number}>} polygon - ordered vertices
 * @param {number} iterations - number of subdivision passes (2-3 is good)
 * @returns {Array<{x: number, z: number}>} smoothed polygon
 */
function smoothPolygon(polygon, iterations = 2) {
  let pts = polygon;
  for (let iter = 0; iter < iterations; iter++) {
    const smoothed = [];
    const n = pts.length;
    for (let i = 0; i < n; i++) {
      const curr = pts[i];
      const next = pts[(i + 1) % n];
      // Q = 75% curr + 25% next
      smoothed.push({
        x: parseFloat((0.75 * curr.x + 0.25 * next.x).toFixed(2)),
        z: parseFloat((0.75 * curr.z + 0.25 * next.z).toFixed(2)),
      });
      // R = 25% curr + 75% next
      smoothed.push({
        x: parseFloat((0.25 * curr.x + 0.75 * next.x).toFixed(2)),
        z: parseFloat((0.25 * curr.z + 0.25 * next.z).toFixed(2)),
      });
    }
    pts = smoothed;
  }
  return pts;
}
```

**WAIT — there's a bug in the template above.** The R point formula should be:
```javascript
      // R = 25% curr + 75% next
      smoothed.push({
        x: parseFloat((0.25 * curr.x + 0.75 * next.x).toFixed(2)),
        z: parseFloat((0.25 * curr.z + 0.75 * next.z).toFixed(2)),  // 0.75 not 0.25!
      });
```

Make sure both x and z use the correct weights (0.25/0.75).

### Step 2: Update the Contour Pipeline in `extractBunkers()`

Find the section where contour is simplified and change the pipeline to:

1. Trace border (existing `traceBorder()`)
2. Convert to meters (existing)
3. RDP simplify with slightly higher epsilon (2.0m instead of 1.5m —
   we're going to add vertices back via subdivision, so we can be more
   aggressive removing them first)
4. **NEW: Chaikin smooth (2 iterations)**
5. Ensure CCW winding (existing `ensureCCW()`)

```javascript
        // --- Trace contour ---
        const borderPixels = traceBorder(grid, w, h, pixels, BUNKER_ZONE);

        // Convert border pixels to local meter coordinates
        let contourMeters = borderPixels.map(([bx, by]) => ({
          x: parseFloat(((bx / (w - 1) - 0.5) * tw).toFixed(2)),
          z: parseFloat(((by / (h - 1) - 0.5) * tl).toFixed(2)),
        }));

        // Simplify then smooth
        const RDP_EPSILON = 2.0;  // slightly more aggressive (was 1.5)
        contourMeters = simplifyPolygon(contourMeters, RDP_EPSILON);
        contourMeters = smoothPolygon(contourMeters, 2);  // ← NEW
        contourMeters = ensureCCW(contourMeters);
```

### Step 3: Update Contour Log

The log already shows vertex counts. After smoothing, vertex counts will
be higher (each Chaikin iteration roughly doubles). Typical results:

- Border trace: ~80-200 pixels
- After RDP: ~8-15 vertices
- After 2x Chaikin: ~32-60 vertices

This is a good range — enough for smooth curves, not so many it bogs
down mesh generation.

---

## Verification

```bash
cd Tools/UHoleLite
node scripts/export-hole.mjs lomond-country-club 1
```

- [ ] No errors
- [ ] Bunker contours have ~30-60 vertices (not 8-15 like before)
- [ ] Contour coordinates are still in the right local-meter range
- [ ] Vertex positions form smooth curves (no sharp 90° corners)

Quick numerical check on Bunker #1 (hole 01):
- Before smoothing: 9 vertices, very angular
- After smoothing: should be ~36 vertices, with rounded corners

Then run all 18:
```bash
node scripts/export-hole.mjs lomond-country-club --all
```

- [ ] All holes export cleanly
- [ ] No bunker has 0-length contour

---

## Do NOT

- Modify `generate-terrain.mjs`
- Modify `traceBorder()` or `simplifyPolygon()` or `ensureCCW()` (those are fine)
- Modify GUI code
- Modify Unity scripts
- Change zone grid data

---

## Status Log

✅ DONE: 2026-04-07 — Added Chaikin smoothing (2 iterations), RDP epsilon bumped to 2.0. Contours now 8-32 vertices (smooth curves). All 18 holes exported.
