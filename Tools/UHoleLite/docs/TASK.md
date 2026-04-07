# TASK.md — UHole Lite: Bunker V2 Contour Export

> Claude Code: Read this file. Execute the current task block.
> Read `scripts/export-hole.mjs` before starting.
> Reference: `Docs/BUNKER_V2_SPEC.md` for full architecture.

---

## Context

V1 bunker export uses bounding boxes (center + size). V2 adds **contour
polygons** — the actual border of each bunker region traced from zone pixels.
Unity will use these contours to generate properly-shaped replacement meshes
instead of uniform ellipses.

The bounding-box fields (`center_local`, `size_m`, etc.) are kept for
backward compat. The new `contour` field is additive.

---

## Current Task — Add Contour Tracing to `extractBunkers()`

**File:** `Tools/UHoleLite/scripts/export-hole.mjs`

### Step 1: Add Border Tracing Function

Add a new function that traces the border of a connected region of pixels.
Uses Moore-neighbor tracing (8-connected boundary following).

```javascript
/**
 * Trace the outer border of a connected region of pixels.
 * Returns ordered array of {x, y} pixel coordinates forming the boundary.
 *
 * @param {Buffer} grid - zone grid (flat, row-major)
 * @param {number} w - grid width
 * @param {number} h - grid height
 * @param {number[][]} pixels - array of [x,y] coords belonging to this region
 * @param {number} zoneValue - the zone index to trace
 */
function traceBorder(grid, w, h, pixels, zoneValue) {
  // Build a quick lookup set for this region's pixels
  const pixelSet = new Set();
  for (const [px, py] of pixels) {
    pixelSet.add(py * w + px);
  }

  // A pixel is a border pixel if it's in the set AND has at least one
  // neighbor (4-connected) that is NOT in the set
  const border = [];
  for (const [px, py] of pixels) {
    const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
    const isBorder = neighbors.some(([nx, ny]) => {
      if (nx < 0 || nx >= w || ny < 0 || ny >= h) return true;
      return !pixelSet.has(ny * w + nx);
    });
    if (isBorder) border.push([px, py]);
  }

  if (border.length === 0) return [];

  // Order the border pixels by walking the perimeter.
  // Start from topmost-leftmost border pixel.
  border.sort((a, b) => a[1] - b[1] || a[0] - b[0]);

  const borderSet = new Set(border.map(([x, y]) => y * w + x));
  const ordered = [border[0]];
  const visited = new Set();
  visited.add(border[0][1] * w + border[0][0]);

  // Walk neighbors (8-connected) to order the border
  const dirs8 = [[-1,-1],[-1,0],[-1,1],[0,-1],[0,1],[1,-1],[1,0],[1,1]];

  let current = border[0];
  for (let step = 0; step < border.length * 2; step++) {
    let found = false;
    for (const [dx, dy] of dirs8) {
      const nx = current[0] + dx;
      const ny = current[1] + dy;
      const key = ny * w + nx;
      if (borderSet.has(key) && !visited.has(key)) {
        visited.add(key);
        ordered.push([nx, ny]);
        current = [nx, ny];
        found = true;
        break;
      }
    }
    if (!found) break;
  }

  return ordered;
}
```

### Step 2: Add Ramer-Douglas-Peucker Simplification

```javascript
/**
 * Ramer-Douglas-Peucker line simplification.
 * @param {Array<{x: number, z: number}>} points - ordered polygon vertices
 * @param {number} epsilon - distance threshold in meters
 * @returns {Array<{x: number, z: number}>} simplified polygon
 */
function simplifyPolygon(points, epsilon) {
  if (points.length <= 2) return points;

  // Find the point with the greatest distance from the line
  // between first and last point
  let maxDist = 0;
  let maxIdx = 0;
  const start = points[0];
  const end = points[points.length - 1];

  for (let i = 1; i < points.length - 1; i++) {
    const d = perpendicularDistance(points[i], start, end);
    if (d > maxDist) {
      maxDist = d;
      maxIdx = i;
    }
  }

  if (maxDist > epsilon) {
    const left = simplifyPolygon(points.slice(0, maxIdx + 1), epsilon);
    const right = simplifyPolygon(points.slice(maxIdx), epsilon);
    return left.slice(0, -1).concat(right);
  } else {
    return [start, end];
  }
}

function perpendicularDistance(point, lineStart, lineEnd) {
  const dx = lineEnd.x - lineStart.x;
  const dz = lineEnd.z - lineStart.z;
  const lenSq = dx * dx + dz * dz;

  if (lenSq === 0) {
    const ex = point.x - lineStart.x;
    const ez = point.z - lineStart.z;
    return Math.sqrt(ex * ex + ez * ez);
  }

  const num = Math.abs(dx * (lineStart.z - point.z) - (lineStart.x - point.x) * dz);
  return num / Math.sqrt(lenSq);
}
```

### Step 3: Add Winding Order Check

```javascript
/**
 * Ensure polygon has counter-clockwise winding (when viewed from above,
 * +Y up). Uses the shoelace formula to check signed area.
 */
function ensureCCW(polygon) {
  let area = 0;
  for (let i = 0; i < polygon.length; i++) {
    const j = (i + 1) % polygon.length;
    area += polygon[i].x * polygon[j].z;
    area -= polygon[j].x * polygon[i].z;
  }
  if (area > 0) polygon.reverse(); // was CW, flip to CCW
  return polygon;
}
```

### Step 4: Update `extractBunkers()` to Include Contours

After the existing flood-fill finds each region's pixels, add contour
extraction. Modify the section inside the `if (pixels.length < MIN_PIXELS)`
check, after computing bounding box / center / size:

```javascript
        // --- Trace contour ---
        const borderPixels = traceBorder(grid, w, h, pixels, BUNKER_ZONE);

        // Convert border pixels to local meter coordinates
        let contourMeters = borderPixels.map(([bx, by]) => ({
          x: parseFloat(((bx / (w - 1) - 0.5) * tw).toFixed(2)),
          z: parseFloat(((by / (h - 1) - 0.5) * tl).toFixed(2)),
        }));

        // Simplify (epsilon in meters — start conservative)
        const RDP_EPSILON = 1.5;
        contourMeters = simplifyPolygon(contourMeters, RDP_EPSILON);
        contourMeters = ensureCCW(contourMeters);

        bunkers.push({
          id: bunkers.length + 1,
          pixel_count: pixels.length,
          contour: contourMeters,        // ← NEW: ordered polygon vertices
          center_local: { x: localX, z: localZ },
          size_m: { x: sizeX, z: sizeZ },
          // Keep V1 fields for backward compat
          center_normalized: {
            x: parseFloat(normCX.toFixed(4)),
            y: parseFloat(normCY.toFixed(4)),
          },
          size_normalized: {
            w: parseFloat(normW.toFixed(4)),
            h: parseFloat(normH.toFixed(4)),
          },
        });
```

### Step 5: Add `schema_version` to Output

Update the bunkers output object:

```javascript
  const bunkersOutput = {
    schema_version: '2.0.0',       // ← NEW
    hole_number: holeNumber,
    bunker_count: bunkers.length,
    depth_m: 2.0,
    bunkers: bunkers,
  };
```

### Step 6: Log Contour Stats

After writing bunkers.json, add a log showing vertex counts:

```javascript
  if (bunkers.length > 0) {
    const contourStats = bunkers.map(b =>
      `#${b.id}: ${b.contour.length}pts`
    ).join(', ');
    console.log(`  Contours: ${contourStats}`);
  }
```

---

## Verification

```bash
cd Tools/UHoleLite
node scripts/export-hole.mjs lomond-country-club 1
```

- [ ] No errors
- [ ] `bunkers.json` has `schema_version: "2.0.0"`
- [ ] Each bunker has a `contour` array of `{x, z}` objects
- [ ] Contour vertex count is reasonable (10–40 per bunker, NOT hundreds)
- [ ] Contour coordinates are in local meters (same range as `center_local`)
- [ ] `center_local` and `size_m` still present (backward compat)
- [ ] Console shows contour point counts

Then run all 18:

```bash
node scripts/export-hole.mjs lomond-country-club --all
```

- [ ] All 18 holes export without errors
- [ ] No bunker has 0-length contour
- [ ] Small bunkers (pixel_count < 50) still get contours

### Quick sanity check on contour data:

Pick Bunker #1 from hole 01 (largest, 346 pixels). Its center_local is
approximately (12.4, -116.6) and size ~(14.9, 33.7). The contour vertices
should roughly outline an area of those dimensions centered around that point.

---

## Do NOT

- Modify `generate-terrain.mjs` (bunker depression already removed)
- Modify GUI code (`app/`)
- Modify Unity scripts
- Change zone grid data
- Remove existing V1 fields from bunkers.json (center_local, size_m, etc.)

---

## Status Log

✅ DONE: 2026-04-07 — Added contour tracing (traceBorder + RDP simplification + CCW winding), schema_version 2.0.0, all 18 holes exported with contour data (3-10 pts per bunker)
