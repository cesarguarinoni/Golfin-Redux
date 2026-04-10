# TASK.md — GeoAlign: Geo-Alignment Tool for UHole Lite

> Claude Code: Read this file at the start of each task.
> After completing, add a status line at the bottom.
> Handoff: `Tools/GeoAlign/docs/TASK.md`

---

## Overview

GeoAlign is a **standalone lightweight web app** for aligning UHole Lite
hole illustrations to real-world GSI satellite imagery. The operator
superimposes the two images, places control points to establish a
precise pixel→lat/lon affine transform, and saves the result. UHole
Lite later reads the output to sample real DEM elevation data.

**Style:** Must visually match UHole Lite — same dark theme, same CSS
variables, same UI patterns (sidebar, toolbars, cards, mode switches).
Copy the CSS variables and component patterns from
`Tools/UHoleLite/app/styles.css` wholesale. The user should feel like
GeoAlign is a sibling tool in the same product family.

---

## Directory Structure

```
Tools/GeoAlign/
  app/
    index.html          # Main app page
    app.js              # All client-side logic
    styles.css          # Copy UHole Lite CSS vars + extend
  scripts/
    dev-server.mjs      # Express server (same pattern as UHole Lite)
    fetch-tiles.mjs     # Download GSI tiles for a hole's area
    lib/
      tiles.mjs         # Geo-math (copy from Tools/UHole/scripts/lib/tiles.mjs)
  cache/
    gsi-photo-z18/      # Cached satellite tile downloads
  docs/
    TASK.md             # This file
  package.json
```

---

## Part 1 — Dev Server (`scripts/dev-server.mjs`)

Express server on port 3200 (UHole Lite uses 3100). Serves:

### API Endpoints

**`GET /api/course?id=lomond-country-club`**
Returns course data from `Tools/UHoleLite/output/{id}/course.json`.
Same format as UHole Lite — the app needs hole list, names, par, etc.

**`GET /api/hole/:courseId/:holeNumber`**
Returns hole metadata: extract-meta.json, terrain-meta.json, and
existing geo-align.json if present.

**`GET /api/illustration/:courseId/:holeNumber`**
Serves the hole illustration PNG from UHole Lite output:
`Tools/UHoleLite/output/{courseId}/holes/{NN}/illustration.png`

**`GET /api/gsi-tile/:z/:x/:y`**
Proxy endpoint for GSI satellite tiles. Fetches from:
`https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg`
Caches to `Tools/GeoAlign/cache/gsi-photo-z18/{z}-{x}-{y}.jpg`.
Returns the cached file on subsequent requests.

**`GET /api/gsi-composite/:courseId/:holeNumber`**
Composites multiple GSI tiles into a single image covering the
estimated area around the hole. Uses the course center
`(34.91318, 136.44164)` as a starting point, fetches a generous grid
of z18 tiles, stitches them into one large JPEG, and returns it with
metadata (pixel bounds, lat/lon bounds of the composite).

Response JSON:
```json
{
  "image_url": "/api/gsi-composite-image/{courseId}/{holeNumber}",
  "width": 2048,
  "height": 2048,
  "bounds": {
    "north": 34.xxx, "south": 34.xxx,
    "east": 136.xxx, "west": 136.xxx
  },
  "zoom": 18,
  "meters_per_pixel": 0.597
}
```

The composite image endpoint returns the actual JPEG.

**How to size the composite:** Each hole terrain is ~500-650m long.
At z18, each 256px tile covers ~153m at Lomond's latitude. So we need
roughly a 6×6 grid of tiles (1536×1536px) to cover one hole, but
fetch 10×10 (2560×2560px) to give the operator room to pan/rotate
the GSI layer and find the right alignment. Center the grid on the
course center. The composite may be larger than the illustration —
that's fine, the GSI layer has independent zoom.

**`POST /api/save-alignment/:courseId/:holeNumber`**
Saves the geo-align.json to:
`Tools/UHoleLite/output/{courseId}/holes/{NN}/geo-align.json`

Body: the full geo-align.json content (see Output Format below).

**`GET /api/load-alignment/:courseId/:holeNumber`**
Loads geo-align.json if it exists. Returns 404 if not.

### Static files
Serve `Tools/GeoAlign/app/` as static root.

---

## Part 2 — Client App (`app/index.html` + `app/app.js`)

### Layout (matches UHole Lite)

```
┌─────────────────────────────────────────────────┐
│ Sidebar (220px)  │  Main content area            │
│                  │                               │
│ GeoAlign         │  [Hole 1 - Par 5]             │
│ Geo-Alignment    │                               │
│                  │  ┌─ Mode toolbar ──────────┐  │
│ ● Hole 1  Par 5  │  │ Navigate | Point  │ CP:3 │  │
│   Hole 2  Par 4  │  └───────────────────────────┘  │
│   Hole 3  Par 4  │  ┌─ View toolbar ────────────┐  │
│   ...            │  │ Opacity ████░░ │ Rot ░░░  │  │
│                  │  └───────────────────────────┘  │
│ ┌─ Info card ──┐ │  ┌─────────────────────────┐  │
│ │ Status: ✓    │ │  │                         │  │
│ │ Points: 4    │ │  │    Canvas (stacked      │  │
│ │ Error: 1.2m  │ │  │    illustration + GSI)  │  │
│ └──────────────┘ │  │                         │  │
│                  │  └─────────────────────────┘  │
│ [Save] [Load]    │  ┌─ Control points table ──┐  │
│                  │  │ CP1: (450,320)→(34.9,..) │  │
│                  │  │ CP2: ...        [Delete] │  │
│                  │  └───────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

### Canvas System

Single `<canvas>` element with two logical layers composited
in the draw loop:

**Layer 1 (bottom): Hole illustration**
- Loaded from `/api/illustration/{courseId}/{holeNumber}`
- Fixed position and scale (fills the canvas stage, centered)
- Always drawn first

**Layer 2 (top): GSI satellite composite**
- Loaded from `/api/gsi-composite/{courseId}/{holeNumber}`
- Has its own independent transform state:
  - `gsiOffsetX`, `gsiOffsetY` — pan (pixels)
  - `gsiRotation` — rotation (radians, continuous)
  - `gsiScale` — zoom factor
- Drawn on top of the illustration with `globalAlpha` = opacity slider value
- The GSI composite is drawn centered at
  `(canvas center + gsiOffset)`, rotated by `gsiRotation`,
  scaled by `gsiScale`

**Layer 3 (overlay): Control points + UI elements**
- Orange circles on illustration positions
- Blue circles on GSI positions (transformed to canvas space)
- Dashed lines connecting paired points
- Point labels (CP1, CP2, etc.)
- Drag handles (larger hit area for touch/mouse)

### Draw loop pseudocode:
```javascript
function draw() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  // Layer 1: Illustration (fixed)
  ctx.drawImage(illustrationImg, illX, illY, illW, illH);

  // Layer 2: GSI satellite (transformed, with opacity)
  ctx.save();
  ctx.globalAlpha = gsiOpacity;
  ctx.translate(gsiCenterX + gsiOffsetX, gsiCenterY + gsiOffsetY);
  ctx.rotate(gsiRotation);
  ctx.scale(gsiScale, gsiScale);
  ctx.drawImage(gsiImg, -gsiImg.width/2, -gsiImg.height/2);
  ctx.restore();

  // Layer 3: Control points
  drawControlPoints();
}
```

### Mode Toolbar

Two modes, toggled with buttons (same `mode-switcher` pattern as
UHole Lite):

**Navigate mode** (default):
- Mouse drag on canvas → pans the GSI layer (`gsiOffsetX/Y`)
- Mouse wheel → zooms the GSI layer (`gsiScale`)
- The illustration does NOT move — only the GSI layer transforms

**Point mode:**
- First click → places an orange dot on the illustration
  (records the illustration-space pixel coordinate)
- Second click → places a blue dot on the GSI layer
  (records the GSI-world coordinate by reverse-transforming the
  canvas click through the current GSI transform to get lat/lon)
- The pair is linked and numbered
- Subsequent clicks alternate: illustration, GSI, illustration, GSI...
- ESC cancels a half-placed point

### View Toolbar

A single toolbar row with:

**Opacity slider** (0–100%): Controls `gsiOpacity` (0 = illustration
only, 1 = GSI only). Same `opacity-control` class as UHole Lite.

**Rotation slider** (-180° to +180°): Controls `gsiRotation`.
Show the current angle as a label. Fine-grained — each pixel of
slider travel = small rotation. This is for the initial rough
alignment before control points snap it precisely.

**Scale slider** (0.5× to 3×): Controls `gsiScale`. For the initial
rough fit.

**Reset button**: Resets GSI transform to defaults (centered, no
rotation, scale 1.0).

### Control Point Interactions

- **Draggable:** Both orange (illustration) and blue (GSI) dots can
  be dragged to reposition. When dragged, the transform recomputes
  in real-time if 3+ points exist.

- **Delete:** Each point pair has a delete button in the control
  points list below the canvas (or right-click on canvas).

- **Visual feedback:** When hovering over a control point, show a
  larger highlight ring. When dragging, cursor changes to `grabbing`.

- **Hit testing:** Use a 12px radius for click detection on points.
  Check illustration points first (they're on top visually).

### Affine Transform Computation

When 3+ control point pairs exist, compute a least-squares affine
transform mapping illustration pixels → lat/lon.

Each control point provides:
- `(px, py)` = illustration pixel coordinates
- `(lat, lon)` = GSI world coordinates

The affine transform is:
```
lon = a * px + b * py + tx
lat = c * px + d * py + ty
```

With N points, solve the 6 unknowns (a, b, c, d, tx, ty) using
least-squares (normal equations). This is identical to what UHole
used — see `Tools/UHole/scripts/compute-transform.mjs` for reference.

**Display stats:** After computing, show:
- Number of control points
- RMS error in meters (convert lat/lon residuals to meters)
- Per-point residual (highlight any point with error > 3m in red)
- Status chip: "Good" (< 2m), "Fair" (2–5m), "Poor" (> 5m)

**"Apply Transform" button:** When clicked, instead of using the
manual pan/rotate/zoom to position the GSI layer, compute where the
GSI layer SHOULD be based on the affine transform and snap it there.
This visually confirms the alignment — the operator can then slide
the opacity back and forth to verify everything lines up.

After applying, the manual GSI controls (pan/rotate/zoom sliders)
update to reflect the computed position but remain adjustable.

### GSI Click → Lat/Lon Conversion

When the user clicks on the GSI layer (in Point mode, second click),
we need to convert the canvas pixel coordinate to a lat/lon. This
requires reverse-transforming through the GSI display transform:

```javascript
function canvasToGsiWorld(canvasX, canvasY) {
  // Reverse the canvas → GSI pixel transform
  const dx = canvasX - (gsiCenterX + gsiOffsetX);
  const dy = canvasY - (gsiCenterY + gsiOffsetY);

  // Reverse rotation
  const cos = Math.cos(-gsiRotation);
  const sin = Math.sin(-gsiRotation);
  const rx = (dx * cos - dy * sin) / gsiScale;
  const ry = (dx * sin + dy * cos) / gsiScale;

  // Now (rx, ry) is in GSI composite pixel space (centered at 0,0)
  const gsiPx = rx + gsiComposite.width / 2;
  const gsiPy = ry + gsiComposite.height / 2;

  // Convert GSI pixel to lat/lon using composite bounds
  const lon = gsiMeta.bounds.west +
    (gsiPx / gsiComposite.width) *
    (gsiMeta.bounds.east - gsiMeta.bounds.west);
  const lat = gsiMeta.bounds.north -
    (gsiPy / gsiComposite.height) *
    (gsiMeta.bounds.north - gsiMeta.bounds.south);

  return { lat, lon };
}
```

### Sidebar

**Brand section:**
```
GeoAlign          (eyebrow: "UHole Lite")
Geo-Alignment Tool
Lomond Country Club (ロモンドカントリー倶楽部) · 18 holes
```

**Hole navigation:** Same nav-link list as UHole Lite. Shows hole
number and par. Active hole highlighted with accent color. A green
checkmark icon appears next to holes that have a saved geo-align.json.

**Info card:** Shows current alignment status:
- Status: Not aligned / Aligning (N points) / Aligned ✓
- Points: N control points
- RMS Error: X.Xm (color-coded)
- Last saved: timestamp or "unsaved"

**Action buttons at bottom of sidebar:**
- **Save** (accent-colored, like UHole Lite's save button) — saves
  geo-align.json via POST to server
- **Load** — loads existing geo-align.json if present
- **Clear** — removes all control points and resets GSI transform

### Save Banner

Same `status-banner` pattern as UHole Lite. Shows "Saved ✓" with
timestamp after successful save, fades after 3 seconds.

---

## Part 3 — GSI Tile Fetcher (`scripts/fetch-tiles.mjs`)

Standalone script that pre-downloads GSI z18 tiles for the course area.

```
node scripts/fetch-tiles.mjs lomond-country-club
```

Uses `lib/tiles.mjs` to compute the tile range covering the course
at z18. Downloads to `cache/gsi-photo-z18/`. Skips existing tiles.

Parameters:
- Course center: `34.91318, 136.44164`
- Coverage radius: 2000m in each direction (covers entire course)
- Zoom: 18
- Tile URL: `https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg`
- Rate limit: 100ms between requests (be polite to GSI servers)

The dev-server's `/api/gsi-tile` endpoint also downloads on-demand
if a tile isn't cached yet.

---

## Part 4 — Tile Compositing (server-side)

The `/api/gsi-composite/:courseId/:holeNumber` endpoint builds a
single large image from multiple z18 tiles. Use the `sharp` npm
package (or canvas) to composite them.

Steps:
1. Determine the tile range covering the hole area (use course center
   + generous padding)
2. Load all tiles from cache (or fetch missing ones)
3. Stitch into a single image (each tile is 256×256, arranged in grid)
4. Return metadata (bounds, dimensions, meters_per_pixel)
5. Cache the composite to avoid rebuilding on each request

The composite metadata is critical — the client needs `bounds`
(north/south/east/west in lat/lon) to convert GSI pixel positions
to world coordinates.

---

## Part 5 — Output Format (`geo-align.json`)

Saved to: `Tools/UHoleLite/output/{courseId}/holes/{NN}/geo-align.json`

```json
{
  "schema_version": "1.0.0",
  "course_id": "lomond-country-club",
  "hole_number": 1,
  "gsi_zoom": 18,
  "illustration_dimensions": {
    "width": 1024,
    "height": 1235
  },
  "control_points": [
    {
      "id": 1,
      "illustration_px": { "x": 450, "y": 320 },
      "world": { "lat": 34.91345, "lon": 136.43721 }
    },
    {
      "id": 2,
      "illustration_px": { "x": 780, "y": 110 },
      "world": { "lat": 34.91298, "lon": 136.43570 }
    }
  ],
  "transform": {
    "method": "least_squares_affine",
    "coefficients": {
      "a": -0.000005861,
      "b": 0.000005436,
      "tx": 136.4401752,
      "c": 0.0000037858,
      "d": 0.0000027552,
      "ty": 34.90933857
    },
    "residuals": [
      { "point_id": 1, "error_m": 0.56 },
      { "point_id": 2, "error_m": 1.28 }
    ],
    "mean_residual_m": 0.92,
    "max_residual_m": 1.28,
    "point_count": 4
  },
  "terrain_bounds_latlon": {
    "north": 34.914,
    "south": 34.912,
    "east": 136.441,
    "west": 136.435
  },
  "saved_at": "2026-04-09T12:30:00.000Z"
}
```

The `terrain_bounds_latlon` is computed by applying the affine
transform to the four corners of the illustration (0,0), (W,0),
(W,H), (0,H) and taking the bounding box of the resulting lat/lon
coordinates.

---

## Part 6 — CSS / Visual Style

**Copy the UHole Lite CSS variables and component styles verbatim.**
The `:root` variables, `.sidebar`, `.nav-link`, `.alignment-toolbar`,
`.mode-switcher`, `.stacked-stage`, `.stat-card`, `.status-banner`,
`.eyebrow`, `.chip`, button styles — all should be identical.

Key UHole Lite CSS variables to copy:
```css
:root {
  --bg: #091018;
  --bg-panel: rgba(18, 26, 36, 0.9);
  --bg-panel-alt: rgba(8, 14, 22, 0.86);
  --ink: #edf4fb;
  --muted: #96aabc;
  --line: rgba(171, 197, 224, 0.14);
  --accent: #45bcff;
  --accent-deep: #9edbff;
  --accent-soft: rgba(69, 188, 255, 0.16);
  --shadow: 0 24px 60px rgba(0, 0, 0, 0.34);
}
```

The body background gradient, sidebar border-radius, toolbar layout,
font stack ("Segoe UI", "Helvetica Neue", sans-serif) — all must
match. The user should see GeoAlign as a natural extension of UHole
Lite, not a different app.

Additional styles needed for GeoAlign-specific elements:

```css
/* Control point markers */
.cp-marker-ill {
  /* orange circle for illustration points */
}
.cp-marker-gsi {
  /* blue circle for GSI points */
}

/* Control points table */
.cp-table { ... }
.cp-row { ... }
.cp-error-good { color: #8fd3a0; }
.cp-error-fair { color: #ffd666; }
.cp-error-poor { color: #ff6666; }
```

These are canvas-drawn, not DOM elements — the CSS classes above are
for the control points list below the canvas (a DOM table/grid).

---

## Implementation Notes

### Dependencies (package.json)
```json
{
  "name": "geoalign",
  "type": "module",
  "scripts": {
    "start": "node scripts/dev-server.mjs",
    "fetch": "node scripts/fetch-tiles.mjs lomond-country-club"
  },
  "dependencies": {
    "express": "^4.18.0",
    "sharp": "^0.33.0"
  }
}
```

`sharp` is for server-side tile compositing. If sharp causes install
issues on Windows, fall back to a canvas-based approach (e.g.,
`@napi-rs/canvas` or just send individual tiles to the client and
composite in the browser — simpler but more network requests).

### Keyboard shortcuts
- `Escape` — cancel half-placed control point
- `N` — switch to Navigate mode
- `P` — switch to Point mode
- `S` — save (Ctrl+S also)
- `Delete` — delete selected control point
- Mouse wheel — zoom GSI layer (Navigate mode)

### Performance
- GSI composite is pre-built server-side and cached
- Canvas redraws only on user interaction (not continuous RAF loop)
- Keep all images in memory once loaded (illustration + GSI composite
  are each ~2-5MB)

### Error handling
- If GSI tiles fail to download (404, network error), show which
  tiles are missing in the console and render the available ones
- If illustration PNG not found, show clear error in sidebar
- If geo-align.json has corrupt data, log warning and start fresh

---

## Verification

1. `npm install` in `Tools/GeoAlign/`
2. `npm run fetch` to pre-download z18 tiles
3. `npm start` → open `http://localhost:3200`
4. Select Hole 1 — illustration should load
5. GSI satellite composite should load on top with default 50% opacity
6. Drag/rotate/zoom the GSI layer to roughly align
7. Switch to Point mode, click 4 matching features
8. Transform should compute, showing RMS error
9. Click "Apply Transform" — GSI should snap to alignment
10. Slide opacity back and forth to verify alignment
11. Save → verify `geo-align.json` written to hole folder
12. Reload page → Load → verify state restored

### Do NOT
- Modify any UHole Lite files
- Modify any UHole files
- Use any external mapping libraries (Leaflet, Mapbox, etc.)
  Keep it simple: raw canvas + tile images
- Require an internet connection after initial tile fetch
  (everything should work from cache)
