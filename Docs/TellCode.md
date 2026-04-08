# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Straighten Zone Boundaries in Hole Viewer

**Goal:** Add a "Straighten Boundaries" button to the Hole Viewer GUI
that cleans up jagged zone edges by vectorizing boundaries, simplifying
with RDP, smoothing curves with Chaikin, and rasterizing back. One-click
fix for jaggies on any zone grid (auto-classified or hand-painted).

**Files:**
- `Tools/UHoleLite/app/app.js` — add button + straighten logic
- `Tools/UHoleLite/app/index.html` — add button to toolbar

---

### Algorithm

For each zone (1-10, skip background 0):

1. **Find all connected regions** of that zone (flood fill)
2. **Trace the border** of each region (same 8-connected walk as
   `traceBorder()` in `export-hole.mjs`)
3. **Simplify** with RDP (epsilon ~1.5 pixels — straightens lines)
4. **Smooth** with Chaikin (1 pass — rounds corners without making
   things blobby)
5. **Rasterize** the simplified polygon back into the zone grid
   (scanline fill)

After processing all zones, the grid has clean vector-quality edges.

### Implementation

#### 1. Add button to `index.html`

Near the "Import Zones PNG" button:

```html
<button id="straighten-btn" title="Straighten and smooth zone boundaries">Straighten Edges</button>
```

#### 2. Add straighten logic to `app.js`

```javascript
document.getElementById("straighten-btn").addEventListener("click", () => {
  if (!zoneGrid) return;

  // Push undo
  zoneUndoStack.push(new Uint8Array(zoneGrid));
  if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();

  straightenBoundaries();
  zonePaintDirty = true;
  drawHole();
  console.log("Zone boundaries straightened");
});

function straightenBoundaries() {
  const w = zoneGridW, h = zoneGridH;
  const result = new Uint8Array(w * h);

  // Start with background everywhere
  result.fill(0);

  // Process zones in priority order (higher priority zones overwrite lower)
  // Background(0) < rough(4) < trees(5) < semi_rough(3) < fairway(1) <
  // tee_box(10) < green(2) < bunker(6) < water(7) < cart_path(8) < ob(9)
  const zonePriority = [0, 4, 5, 3, 1, 10, 2, 6, 7, 8, 9];

  for (const zone of zonePriority) {
    if (zone === 0) continue; // background is the base

    // Find connected regions of this zone
    const visited = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const idx = y * w + x;
        if (zoneGrid[idx] !== zone || visited[idx]) continue;

        // Flood fill to get region pixels
        const pixels = [];
        const stack = [[x, y]];
        visited[idx] = 1;
        while (stack.length > 0) {
          const [px, py] = stack.pop();
          pixels.push([px, py]);
          for (const [dx, dy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
            const nx = px + dx, ny = py + dy;
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
            const ni = ny * w + nx;
            if (!visited[ni] && zoneGrid[ni] === zone) {
              visited[ni] = 1;
              stack.push([nx, ny]);
            }
          }
        }

        if (pixels.length < 20) {
          // Too small to vectorize — just copy as-is
          for (const [px, py] of pixels) result[py * w + px] = zone;
          continue;
        }

        // Trace border
        const border = traceBorderPixels(pixels, w, h);
        if (border.length < 4) {
          for (const [px, py] of pixels) result[py * w + px] = zone;
          continue;
        }

        // RDP simplify (close polygon first)
        let simplified = rdpSimplify([...border, border[0]], 1.5);
        // Remove duplicate closing point
        if (simplified.length > 1 &&
            simplified[0][0] === simplified[simplified.length-1][0] &&
            simplified[0][1] === simplified[simplified.length-1][1]) {
          simplified = simplified.slice(0, -1);
        }

        // Chaikin smooth (1 pass)
        let smoothed = chaikinSmooth(simplified, 1);

        // Rasterize polygon back to grid
        scanlineFill(result, w, h, smoothed, zone);
      }
    }
  }

  // Copy result back to zoneGrid
  zoneGrid.set(result);
}

// --- Helper: trace border of a pixel region (8-connected walk) ---
function traceBorderPixels(pixels, gridW, gridH) {
  const pixelSet = new Set(pixels.map(([x,y]) => y * gridW + x));
  const border = [];
  for (const [px, py] of pixels) {
    const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
    const isBorder = neighbors.some(([nx,ny]) => {
      if (nx < 0 || nx >= gridW || ny < 0 || ny >= gridH) return true;
      return !pixelSet.has(ny * gridW + nx);
    });
    if (isBorder) border.push([px, py]);
  }
  if (border.length === 0) return [];

  // Order by walking perimeter (8-connected)
  border.sort((a, b) => a[1] - b[1] || a[0] - b[0]);
  const borderSet = new Set(border.map(([x,y]) => y * gridW + x));
  const ordered = [border[0]];
  const visitedSet = new Set([border[0][1] * gridW + border[0][0]]);
  const dirs8 = [[-1,-1],[-1,0],[-1,1],[0,-1],[0,1],[1,-1],[1,0],[1,1]];

  let current = border[0];
  for (let step = 0; step < border.length * 2; step++) {
    let found = false;
    for (const [dx, dy] of dirs8) {
      const nx = current[0] + dx, ny = current[1] + dy;
      const key = ny * gridW + nx;
      if (borderSet.has(key) && !visitedSet.has(key)) {
        visitedSet.add(key);
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

// --- RDP line simplification ---
function rdpSimplify(points, epsilon) {
  if (points.length <= 2) return points;
  let maxDist = 0, maxIdx = 0;
  const start = points[0], end = points[points.length - 1];
  for (let i = 1; i < points.length - 1; i++) {
    const d = perpDist(points[i], start, end);
    if (d > maxDist) { maxDist = d; maxIdx = i; }
  }
  if (maxDist > epsilon) {
    const left = rdpSimplify(points.slice(0, maxIdx + 1), epsilon);
    const right = rdpSimplify(points.slice(maxIdx), epsilon);
    return left.slice(0, -1).concat(right);
  }
  return [start, end];
}

function perpDist(point, lineStart, lineEnd) {
  const dx = lineEnd[0] - lineStart[0];
  const dy = lineEnd[1] - lineStart[1];
  const lenSq = dx * dx + dy * dy;
  if (lenSq === 0) {
    const ex = point[0] - lineStart[0];
    const ey = point[1] - lineStart[1];
    return Math.sqrt(ex * ex + ey * ey);
  }
  return Math.abs(dx * (lineStart[1] - point[1]) -
                  (lineStart[0] - point[0]) * dy) / Math.sqrt(lenSq);
}

// --- Chaikin corner-cutting ---
function chaikinSmooth(polygon, iterations) {
  let pts = polygon;
  for (let iter = 0; iter < iterations; iter++) {
    const smoothed = [];
    const n = pts.length;
    for (let i = 0; i < n; i++) {
      const curr = pts[i], next = pts[(i + 1) % n];
      smoothed.push([0.75*curr[0] + 0.25*next[0], 0.75*curr[1] + 0.25*next[1]]);
      smoothed.push([0.25*curr[0] + 0.75*next[0], 0.25*curr[1] + 0.75*next[1]]);
    }
    pts = smoothed;
  }
  return pts;
}

// --- Scanline polygon fill ---
function scanlineFill(grid, w, h, polygon, zone) {
  if (polygon.length < 3) return;
  // Find Y bounds
  let minY = h, maxY = 0;
  for (const [, y] of polygon) {
    if (y < minY) minY = Math.floor(y);
    if (y > maxY) maxY = Math.ceil(y);
  }
  minY = Math.max(0, minY);
  maxY = Math.min(h - 1, maxY);

  const n = polygon.length;
  for (let y = minY; y <= maxY; y++) {
    // Find X intersections with polygon edges
    const intersections = [];
    for (let i = 0; i < n; i++) {
      const [x1, y1] = polygon[i];
      const [x2, y2] = polygon[(i + 1) % n];
      if ((y1 <= y && y2 > y) || (y2 <= y && y1 > y)) {
        const t = (y - y1) / (y2 - y1);
        intersections.push(x1 + t * (x2 - x1));
      }
    }
    intersections.sort((a, b) => a - b);

    // Fill between pairs
    for (let i = 0; i < intersections.length - 1; i += 2) {
      const xStart = Math.max(0, Math.ceil(intersections[i]));
      const xEnd = Math.min(w - 1, Math.floor(intersections[i + 1]));
      for (let x = xStart; x <= xEnd; x++) {
        grid[y * w + x] = zone;
      }
    }
  }
}
```

---

### Verification

- [ ] "Straighten Edges" button appears in Hole Viewer
- [ ] Clicking it processes the zone grid (may take a second)
- [ ] Zone boundaries become smoother and straighter
- [ ] Small zones (<20px) preserved as-is
- [ ] Undo works (reverts to pre-straighten grid)
- [ ] Can save the straightened grid
- [ ] Works on both auto-classified and imported PNG grids

### Do NOT

- Modify the export pipeline
- Modify the Unity importer
- Modify the classification script

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Texture cleanup: fairway/fringe swap, dark fringe, blur removed, fringe ring
✅ DONE: 2026-04-08 — smoothBoundaries() + upscaled classification
✅ DONE: 2026-04-08 — Import Zones PNG button in UHole Lite GUI
✅ DONE: 2026-04-08 — Straighten Edges button (RDP + Chaikin + scanline fill)
