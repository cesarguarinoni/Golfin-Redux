# TASK.md — UHole Lite GUI: Heightmap View, Zone Legend & Zone Painting

> Claude Code: Read this file. Execute the current task block.
> Read all files in `app/` AND `scripts/dev-server.mjs` before starting.

---

## Context

UHole Lite has a browser-based GUI at `Tools/UHoleLite/app/` served by
`scripts/dev-server.mjs` on port 4174. Launch with `Launch GUI.bat`.

The GUI currently supports:
- Viewing hole illustrations and zone maps
- Orientation controls (rotate/flip)
- View switching (Map / Zones / Overlay)
- Draggable tee markers with save

**Read these files before starting:**
- `app/app.js` — Main app logic (~350 lines)
- `app/index.html` — HTML structure
- `app/styles.css` — UHole-style dark theme
- `scripts/dev-server.mjs` — API server
- `scripts/classify-zones.mjs` — Zone definitions and colors (ZONE_COLORS, ZONES constants)

---

## Current Task — Three New Features

### Feature 1: Heightmap View

**Goal:** Add a "Height" view mode so the user can see the procedural heightmap
rendered as a grayscale image, and verify it aligns with the illustration.

#### Server side

Add a new API endpoint `GET /api/heightmap?course=X&hole=N` that reads
`output/{courseId}/export/hole-{NN}/heightmap.raw` (129×129 uint16 big-endian),
converts it to a grayscale PNG on the fly, and returns it.

```javascript
// In dev-server.mjs:
if (req.method === "GET" && url.pathname === "/api/heightmap") {
  const courseId = url.searchParams.get("course") || "lomond-country-club";
  const hole = Number(url.searchParams.get("hole"));
  const pad = String(hole).padStart(2, "0");
  const rawPath = path.join(root, "output", courseId, "export", `hole-${pad}`, "heightmap.raw");

  try {
    const rawBytes = await readFile(rawPath);
    const res129 = 129;
    // Convert uint16BE to 8-bit grayscale
    const pixels = Buffer.alloc(res129 * res129);
    for (let i = 0; i < res129 * res129; i++) {
      const val = (rawBytes[i * 2] << 8) | rawBytes[i * 2 + 1]; // big-endian uint16
      pixels[i] = Math.round((val / 65535) * 255);
    }

    // Use sharp to create a PNG from the grayscale buffer
    // Need to import sharp at the top of dev-server.mjs
    const sharp = (await import("sharp")).default;
    const pngBuffer = await sharp(pixels, { raw: { width: res129, height: res129, channels: 1 } })
      .resize(512, 512, { kernel: "nearest" })  // upscale for visibility
      .png()
      .toBuffer();

    res.writeHead(200, { "Content-Type": "image/png", "Cache-Control": "no-cache" });
    res.end(pngBuffer);
  } catch (err) {
    res.writeHead(404);
    res.end("Heightmap not found: " + err.message);
  }
  return;
}
```

NOTE: `sharp` needs to be imported. Add `import sharp from "sharp";` at the
top of dev-server.mjs, or use dynamic import as shown above.

#### Client side

1. Add a "Height" button to the view mode switcher:
   ```html
   <button class="mode-btn view-btn" data-view="heightmap">Height</button>
   ```

2. Load the heightmap image when selecting a hole:
   ```javascript
   heightmapImg = await loadImage("/api/heightmap?course=" + COURSE_ID + "&hole=" + n);
   ```
   Add `let heightmapImg = null;` to the module-level variables.

3. In `drawCanvas()`, add heightmap rendering:
   ```javascript
   if (currentView === "heightmap") {
     if (heightmapImg) {
       ctx.globalAlpha = 1;
       ctx.drawImage(heightmapImg, -hw, -hh, srcW * scale, srcH * scale);
     }
   }
   ```
   
   Also support overlaying heightmap with the illustration: when view is "both"
   and the user switches to heightmap, show the heightmap blended with the map.
   
   Actually, simpler approach: just make "Height" its own standalone view mode.
   If the user wants to compare, they can toggle between Map and Height views.

4. The heightmap image is 512×512 (square) but the illustration is ~530×637
   (portrait). The `drawImage` call stretches the heightmap to match the
   illustration dimensions, which is correct since the heightmap covers the
   same terrain area.

---

### Feature 2: Zone Color Legend

**Goal:** Show a persistent legend/codex that maps zone colors to zone names,
so the user can see what each color in the zone view means.

#### Implementation

Add a legend panel below the toolbar (or in the sidebar) that shows all 11
zone types with their visualization colors. The legend should:
- Display a colored swatch (small square or circle) next to each zone name
- Show the zone name
- Only be visible when the view is "Zones" or "Overlay"
- Highlight the zone under the mouse cursor when hovering the canvas

Zone colors (from `classify-zones.mjs`):

| Index | Name        | Color RGB          | Hex       |
|-------|-------------|-------------------|-----------|
| 0     | background  | (0, 0, 0)         | #000000   |
| 1     | fairway     | (0, 204, 0)       | #00CC00   |
| 2     | green       | (128, 255, 64)    | #80FF40   |
| 3     | semi_rough  | (102, 136, 51)    | #668833   |
| 4     | rough       | (51, 102, 34)     | #336622   |
| 5     | trees       | (26, 51, 16)      | #1A3310   |
| 6     | bunker      | (221, 204, 136)   | #DDCC88   |
| 7     | water       | (51, 102, 204)    | #3366CC   |
| 8     | cart_path   | (153, 153, 153)   | #999999   |
| 9     | ob          | (255, 51, 51)     | #FF3333   |
| 10    | tee_box     | (255, 255, 255)   | #FFFFFF   |

#### HTML

Add a legend bar between the toolbar and the canvas stage:
```html
<div class="zone-legend" id="zone-legend" hidden>
  <!-- Populated by JS -->
</div>
```

#### JS

```javascript
const ZONE_LEGEND = [
  { name: "Background", color: "#000000" },
  { name: "Fairway",    color: "#00CC00" },
  { name: "Green",      color: "#80FF40" },
  { name: "Semi-rough", color: "#668833" },
  { name: "Rough",      color: "#336622" },
  { name: "Trees",      color: "#1A3310" },
  { name: "Bunker",     color: "#DDCC88" },
  { name: "Water",      color: "#3366CC" },
  { name: "Cart path",  color: "#999999" },
  { name: "OB",         color: "#FF3333" },
  { name: "Tee box",    color: "#FFFFFF" },
];

function buildZoneLegend() {
  const el = document.getElementById("zone-legend");
  el.innerHTML = ZONE_LEGEND.map((z, i) =>
    `<div class="legend-item" data-zone="${i}">` +
    `<span class="legend-swatch" style="background:${z.color}"></span>` +
    `<span class="legend-label">${z.name}</span>` +
    `</div>`
  ).join("");
}
```

Show/hide based on view mode — visible when view is "zones", "both", or
when zone painting is active.

#### CSS

```css
.zone-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: 8px 14px;
  margin-bottom: 8px;
  border: 1px solid var(--line);
  border-radius: 10px;
  background: var(--bg-panel);
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border-radius: 6px;
  font-size: 0.78rem;
  color: var(--muted);
  cursor: default;
}

.legend-item.is-active {
  background: var(--accent-soft);
  color: var(--ink);
}

.legend-swatch {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 3px;
  border: 1px solid rgba(255,255,255,0.2);
  flex-shrink: 0;
}

.legend-label {
  white-space: nowrap;
}
```

---

### Feature 3: Zone Painting

**Goal:** Allow the user to paint over zones on the canvas to correct
misclassifications. For example, paint an area that was classified as "rough"
to be "fairway" instead.

#### How it works

1. The user selects a zone type from the legend (clicking a legend item makes
   it the active paint brush)
2. When a legend item is active (highlighted), clicking/dragging on the canvas
   in Zones or Overlay view paints pixels to that zone
3. The paint is applied to the zone grid data and the zones.png is regenerated
4. A "Save Zones" action persists the changes

#### Implementation

**Paint mode activation:**
- Click a legend item → it becomes the active brush (highlighted with
  `is-active` class). Click again to deselect.
- When a brush is active, the cursor over the canvas changes to a crosshair
- Show the active brush zone name somewhere visible (toolbar or below canvas)

**Brush settings:**
- Add a brush size slider (radius 1-20 pixels at raw image resolution)
- Default brush size: 5px

**Painting on canvas:**
- On mousedown + mousemove while a brush is active and NOT over a tee marker,
  paint the zone grid
- The zone grid is stored in `zones.json` as a base64-encoded uint8 array
- Load the zone grid into a client-side array when the hole is selected
- When painting, update the local array and redraw the zones on the canvas

**Client-side zone grid:**
```javascript
let zoneGrid = null;   // Uint8Array, width × height
let zoneGridW = 0;
let zoneGridH = 0;
let zonePaintDirty = false;
let activeBrushZone = -1;  // -1 = no brush
let brushSize = 5;

// Load when selecting a hole:
async function loadZoneGrid(holeNumber) {
  const pad = String(holeNumber).padStart(2, "0");
  const res = await fetch("/api/zones-grid?course=" + COURSE_ID + "&hole=" + holeNumber);
  if (!res.ok) { zoneGrid = null; return; }
  const data = await res.json();
  zoneGridW = data.width;
  zoneGridH = data.height;
  zoneGrid = new Uint8Array(atob(data.grid).split("").map(c => c.charCodeAt(0)));
}
```

**New API endpoint:** `GET /api/zones-grid?course=X&hole=N`

Returns the zone grid data:
```json
{
  "width": 528,
  "height": 637,
  "grid": "<base64 string>"
}
```

Read from `output/{courseId}/holes/{NN}/zones.json` → extract `source_dimensions`
and `grid` fields.

**Painting pixels:**
When the user paints, convert canvas coordinates to zone grid coordinates
(using the same inverse transform as tee dragging), then paint a circle of
`brushSize` radius in the zone grid:

```javascript
function paintZone(normX, normY) {
  if (activeBrushZone < 0 || !zoneGrid) return;
  const gx = Math.round(normX * (zoneGridW - 1));
  const gy = Math.round(normY * (zoneGridH - 1));

  for (let dy = -brushSize; dy <= brushSize; dy++) {
    for (let dx = -brushSize; dx <= brushSize; dx++) {
      if (dx * dx + dy * dy > brushSize * brushSize) continue;
      const px = gx + dx;
      const py = gy + dy;
      if (px < 0 || px >= zoneGridW || py < 0 || py >= zoneGridH) continue;
      zoneGrid[py * zoneGridW + px] = activeBrushZone;
    }
  }

  zonePaintDirty = true;
  regenerateZonesImage();
}
```

**Regenerate zones image from grid:**
After painting, regenerate the `zonesImg` canvas-side from the zone grid:

```javascript
function regenerateZonesImage() {
  const imgData = new ImageData(zoneGridW, zoneGridH);
  for (let i = 0; i < zoneGrid.length; i++) {
    const zone = zoneGrid[i];
    const color = ZONE_COLORS_RGB[zone] || [0, 0, 0];
    imgData.data[i * 4 + 0] = color[0];
    imgData.data[i * 4 + 1] = color[1];
    imgData.data[i * 4 + 2] = color[2];
    imgData.data[i * 4 + 3] = 255;
  }

  // Create an offscreen canvas to generate an ImageBitmap
  const offscreen = new OffscreenCanvas(zoneGridW, zoneGridH);
  const offCtx = offscreen.getContext("2d");
  offCtx.putImageData(imgData, 0, 0);

  // Convert to a regular canvas for drawImage compatibility
  const tempCanvas = document.createElement("canvas");
  tempCanvas.width = zoneGridW;
  tempCanvas.height = zoneGridH;
  const tempCtx = tempCanvas.getContext("2d");
  tempCtx.putImageData(imgData, 0, 0);

  // Use the canvas as the zonesImg source
  zonesImg = tempCanvas;
  drawCanvas();
}
```

Note: `drawImage` can accept a `<canvas>` element, not just `<img>`.

**Define ZONE_COLORS_RGB in the client:**
```javascript
const ZONE_COLORS_RGB = [
  [0,   0,   0  ],  // 0 background
  [0,   204, 0  ],  // 1 fairway
  [128, 255, 64 ],  // 2 green
  [102, 136, 51 ],  // 3 semi_rough
  [51,  102, 34 ],  // 4 rough
  [26,  51,  16 ],  // 5 trees
  [221, 204, 136],  // 6 bunker
  [51,  102, 204],  // 7 water
  [153, 153, 153],  // 8 cart_path
  [255, 51,  51 ],  // 9 ob
  [255, 255, 255],  // 10 tee_box
];
```

**Saving painted zones:**

New API endpoint: `POST /api/zones`

Request body:
```json
{
  "courseId": "lomond-country-club",
  "holeNumber": 1,
  "width": 528,
  "height": 637,
  "grid": "<base64 encoded uint8 array>"
}
```

Server handler:
- Read existing `zones.json`
- Update the `grid` field with the new base64 data
- Recalculate `zone_stats` (count pixels per zone, compute percentages)
- Write updated `zones.json`
- Also regenerate `zones.png` from the new grid using sharp
  (write a new PNG with the zone visualization colors)

Add zones saving to the existing `saveAll()` function — save if `zonePaintDirty`
is true.

**Brush interaction with tee dragging:**
- If `activeBrushZone >= 0` AND the mouse is over a tee marker → tee drag
  takes priority (don't paint)
- If `activeBrushZone >= 0` AND the mouse is NOT over a tee → paint mode
- If `activeBrushZone < 0` → tee drag mode (existing behavior)

**Cursor:**
- Normal mode (no brush): default cursor, `grab` on tee hover
- Brush active: `crosshair` cursor, `grab` on tee hover (tee still draggable)
- Painting: `crosshair` cursor

#### HTML additions

Add between the toolbar and canvas stage:

```html
<!-- Zone legend + paint controls -->
<div class="zone-legend" id="zone-legend" hidden>
  <!-- Populated by buildZoneLegend() -->
</div>

<!-- Brush size (visible when painting) -->
<div class="alignment-toolbar" id="brush-toolbar" hidden>
  <h4 class="toolbar-title">Paint</h4>
  <span id="active-brush-label" class="chip">No brush</span>
  <div class="toolbar-spacer"></div>
  <span style="font-size:0.82rem;color:var(--muted)">Brush size</span>
  <input type="range" id="brush-size" min="1" max="20" value="5"
    style="width:100px;accent-color:var(--accent)">
  <span id="brush-size-label" style="font-size:0.82rem;color:var(--muted)">5px</span>
  <button id="btn-clear-brush">✕ Deselect</button>
</div>
```

#### CSS additions

```css
#hole-canvas.painting { cursor: crosshair; }
```

---

### Integration Notes

- All three features should work together smoothly
- The view mode switcher should now have 4 options: Map | Zones | Overlay | Height
- The zone legend is visible when viewing Zones, Overlay, or when painting
- The heightmap view shows a standalone grayscale terrain preview
- Painting only works in Zones or Overlay view
- Tee dragging works in ALL view modes
- Save button saves orientation + tees + zones (whatever has been modified)

### Verification

- [ ] Heightmap renders as grayscale in the "Height" view mode
- [ ] Heightmap responds to orientation controls (rotate/flip)
- [ ] Zone legend displays all 11 zone types with correct colors
- [ ] Zone legend is visible in Zones and Overlay views
- [ ] Zone legend hides in Map and Height views
- [ ] Clicking a legend item activates it as the paint brush
- [ ] Painting on the zones canvas changes zone colors in real-time
- [ ] Brush size slider adjusts the paint radius
- [ ] Painted changes are reflected in the zone overlay
- [ ] Saving persists painted zones to zones.json and zones.png
- [ ] Zone stats are recalculated after saving
- [ ] Tee dragging still works when a brush is selected (tees take priority)
- [ ] Switching between holes resets the paint state
- [ ] All features work with orientation transforms applied

### Do NOT

- Modify pipeline scripts (Steps 1-6)
- Modify Unity scripts
- Break existing tee drag functionality
- Break existing orientation controls

---

## Previous Completed Tasks

✅ Steps 1-6: Full pipeline complete (scrape, extract, detect-tees, classify-zones,
   generate-terrain, export)
✅ GUI: Browser app with orientation controls, view switching
✅ Draggable tee markers with save

---

## Status Log

(Claude Code: add completion status lines here)
- 2026-04-06: Three features implemented — (1) Heightmap "Height" view mode via GET /api/heightmap (sharp converts uint16be raw to grayscale PNG); (2) Zone legend with 11 color swatches, visible in Zones/Overlay views; (3) Zone painting — click legend item to select brush, paint on canvas, adjustable brush size 1-20px, saves via POST /api/zones (updates zones.json grid+stats and regenerates zones.png). All three integrate with existing tee drag and orientation controls.
