# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Water Shore Adaptive Radius — Phase 1 (Sampling Script)

Hole 12 shows the same serrated-grass artifact on a steep diagonal water
bank that the tee skirt had before its adaptive-radius fix. Cause:
fixed `ShoreRadius = 10 cells ≈ 5m` in `DepressTerrainUnderOverlays`
compresses big drops into a steep ramp face → Unity's terrain shader
stretches grass vertically per-triangle → reads as serrations.

The fix is a direct port of the tee adaptive radius (`dR = clamp(1.5 ×
dropAbs / MaxRampSlope, base, cap)`). But before applying it, we need
data: how big is the worst drop, and what cap (`ShoreMaxRadiusMeters`)
should the spec use? **Phase 1 = sampling script. Phase 2 (apply the
spec) lands in a follow-up TellCode block, after Cesar reviews the
numbers.**

**Target file (new):** `Tools/sample-shore-heights.js`
**No changes** to `HoleGeoImporter.cs`, the UHoleGeo pipeline, or any
exported JSON.

---

### Reference

Fork the existing `Tools/sample-tee-heights.js` — same height-sampling
machinery (uint16be `.raw`, world↔cell math, point-in-polygon). The
shore version differs in three ways:

1. Reads `water.json` instead of `zone-contours.json` → tee bodies.
2. Computes `nearSurfY = minTerrainH_inside_polygon - 0.05f` (the
   formula `DepressTerrainUnderOverlays` uses for the water surface
   level) instead of tee max height.
3. Samples *outside* the polygon at increasing offsets (1m, 2m, 5m,
   10m), to characterise drop-as-function-of-distance — that's what
   determines whether a 5m fixed radius is too short.

---

### Step 1 — Iterate all holes with water

Don't hardcode Hole 12. Walk the export tree and find every hole with
a `water.json`:

```javascript
const exportRoot = 'C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/export';
const holesWithWater = [];
for (let n = 1; n <= 18; n++) {
  const pad = String(n).padStart(2, '0');
  const wpath = `${exportRoot}/hole-${pad}/water.json`;
  if (fs.existsSync(wpath)) holesWithWater.push(n);
}
```

For each hole, load:
- `Tools/UHoleGeo/output/lomond-country-club/holes/NN/heightmap.raw`
  (pad to 2 digits, no leading zero stripped).
- `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/water.json`.

---

### Step 2 — Per-hole terrain dimensions

`sample-tee-heights.js` hardcodes `terrainWidthM = 151.6` /
`terrainLengthM = 127.2` / `elevRangeM = 34.9` for Hole 04. These vary
per hole. Read them from the hole's per-hole metadata.

**NOTE for Code:** I don't know the exact filename / path that holds
per-hole terrain dimensions in this project. Look for:

- `Tools/UHoleGeo/output/lomond-country-club/holes/NN/*.json` (any
  metadata sibling of `heightmap.raw` with `terrain_width_m` /
  `terrain_length_m` / `elev_range_m`-ish fields).
- Failing that, `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/`
  for a `terrain-meta.json`, `hole-meta.json`, or similar.
- Last resort: grep `generate-terrain.mjs` and `export-hole.mjs` for
  where these dimensions are written. The values must already be
  serialised somewhere because `HoleGeoImporter.cs` consumes them.

If absolutely no per-hole metadata exists, fall back to Hole 04's
constants and add a `// TODO: read per-hole terrain dims` comment plus
a script-top warning. Don't silently use the wrong numbers.

---

### Step 3 — Per-water-body drop characterisation

For each water body (`water.json` → `water[i].contour`):

1. Compute `minTerrainH` over all cells **inside** the polygon (point-
   in-polygon over bbox, same pattern as the tee script's interior
   sweep). This mirrors the importer's `nearSurfY` formula:
   `nearSurfY = minTerrainH - 0.05f`.

2. Walk the contour vertices. For each vertex, sample the heightmap
   at four offsets along the **outward normal**:
   `[1m, 2m, 5m, 10m]`. Outward normal at vertex `i` ≈ rotate the
   edge `(p[i+1] - p[i-1])` by 90° CCW, then check sign by testing
   if `vertex + 0.5m × normal` is outside the polygon (flip if not).
   Skip vertex if the sample lands outside the heightmap.

3. For each sampled point, record `drop = h(sample) - nearSurfY`
   (positive = bank rises above water surface, which is the case
   we care about; negative = sample is already below water level,
   discard).

4. Per water body, report:
   - `nearSurfY` (m)
   - Number of contour vertices, number sampled
   - At each offset (1m, 2m, 5m, 10m): min, median, p90, max drop
   - **Adaptive radius needed** at the p90 drop:
     `dR_needed_m = 1.5 × drop_p90 / 0.35` (using the tee fix's 0.35
     `MaxRampSlope`). This is the headline number — it tells us what
     `ShoreMaxRadiusMeters` cap the Phase 2 spec should use.

5. Per hole summary: max drop across all bodies, max `dR_needed`.

6. Course-wide summary at the end: max drop, max `dR_needed`, list of
   `(hole, body_id, drop, dR_needed)` for the top 5 worst spots.

---

### Step 4 — Output format

Console output, plain text, sectioned by hole. Example shape:

```
=== Hole 7 ===
Terrain: 151.6m × 127.2m, elev range 34.9m

  Water body 1 (12,453 px, 78 contour verts):
    nearSurfY = 4.32m
    Outward sampling (78 verts, 76 sampled):
      offset  min     median  p90     max
       1m    -0.10    0.45    1.20    2.10
       2m     0.05    0.92    2.45    3.80
       5m     0.40    1.85    4.10    5.95
      10m    -0.20    2.30    5.20    7.40
    Adaptive radius needed at 5m-offset p90 drop (4.10m):
      dR_needed = 1.5 × 4.10 / 0.35 = 17.6m  ← cap recommendation

=== Hole 12 ===
...

=== COURSE SUMMARY ===
Holes with water: [7, 12, ...]
Max drop course-wide: 7.4m (Hole 7, body 1)
Max dR_needed:        31.7m (Hole 7, body 1)

Top 5 worst spots:
  Hole  7, body 1: drop 7.40m, dR_needed 31.7m
  Hole 12, body 1: drop 5.80m, dR_needed 24.9m
  ...

→ Recommended ShoreMaxRadiusMeters cap for Phase 2 spec: 35m
  (max dR_needed × 1.1 safety margin, rounded up to 5m)
```

The "recommended cap" line at the end is what Phase 2 will read.

---

### Step 5 — Run it

```
node Tools/sample-shore-heights.js
```

Paste the full console output back. Cesar will eyeball it against
the screenshot evidence and the existing `NEXT_SESSION_WATER_SHORE.md`
heuristics:

- **Max drop < 1m course-wide** → skip Phase 2, fixed 5m is fine,
  Hole 12 artifact is something else (re-investigate).
- **Max drop 2–5m** → apply Phase 2 with cap = max `dR_needed` × 1.1.
- **Max drop > 5m** → apply Phase 2, cap as above; this is the case
  Hole 12 likely is.

---

### Verification

- [x] Script runs to completion on all holes with water (no crashes
      on holes without water).
- [x] Hole 12 appears in the report with non-zero drop values
      (matches the screenshot evidence — the steep bank is real).
- [x] Per-hole terrain dimensions are read from real metadata, not
      hardcoded — confirm the dims for at least one non-Hole-04 hole
      look correct (e.g. compare against the Unity terrain in-scene).
- [x] Course summary's recommended cap is a number Cesar can drop
      directly into the Phase 2 spec.

### Do NOT change

- `HoleGeoImporter.cs` — Phase 2 only.
- Any pipeline script (`generate-terrain.mjs`, `export-hole.mjs`,
  `classify-zones.mjs`, `dev-server.mjs`).
- The existing `Tools/sample-tee-heights.js` — fork, don't refactor.

### Out of scope (Phase 2)

- The actual fix in `DepressTerrainUnderOverlays`. Spec is staged in
  `Docs/NEXT_SESSION_WATER_SHORE.md`; Phase 2 TellCode block lands
  after sampling output is reviewed.

---

## Previous Task — Bridge Viewer in UHoleGeo (consume bridges.json)

The Unity side now writes `bridges.json` into each hole's UHoleGeo
export folder (`Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/bridges.json`).
This task adds the UHoleGeo-side viewer so Cesar can paint the cart-path
zone right up to a bridge's anchor endpoints with pixel-accurate visual
feedback — no more screenshot guesswork.

**Target files:**
- `Tools/UHoleGeo/scripts/dev-server.mjs` (add one GET route)
- `Tools/UHoleGeo/app/index.html` (one toggle button in the layer bar)
- `Tools/UHoleGeo/app/app.js` (load + draw + hover + toggle)

**No changes to:** `bridges.json` schema, UHoleGeo export pipeline,
cart-path processing, the Unity `BridgeExporter` or `BridgeAnchor`,
`classify-zones.mjs`, `generate-terrain.mjs`, or `export-hole.mjs`.

This is a **viewer only**. Bridges are authored in Unity and are
read-only in UHoleGeo. Dragging a bridge in UHoleGeo would desync from
Unity — the whole point is that Unity is the source of truth.

---

### Why viewer, not editor

UHoleGeo paints cart paths as a **pixel mask** (`cartPathMask`), not a
spline. "Snap spline endpoint to bridge" is not a thing here. What the
artist actually needs is to **see** the bridge footprint and its two
anchor endpoints on the canvas while painting, so the cart-path mask
can be brushed to meet the anchors cleanly. That's all this task does.

---

### Data flow (already established)

```
Unity scene                                     UHoleGeo canvas
─────────                                       ───────────────
BridgeAnchor component ─► BridgeExporter ─► bridges.json
                                              │
                                              ▼
                              Tools/UHoleGeo/output/{course}/
                                export/hole-XX/bridges.json
                                              │
                                              ▼
                              (this task)  GET /api/bridges
                                              │
                                              ▼
                                       drawCanvas() → visible markers
```

---

### Step 1 — Add `/api/bridges` route in `dev-server.mjs`

Find the `/api/hole-bounds` handler (around line 138). Insert a new
handler immediately after it (before the `/api/fetch-satellite`
handler). The route reads `bridges.json` from the hole's `export/`
folder, not its `holes/` folder — that's where Unity writes it.

```javascript
// --- API: Get bridges (written by Unity BridgeExporter) ---
if (req.method === "GET" && url.pathname === "/api/bridges") {
  const courseId = url.searchParams.get("course") || "lomond-country-club";
  const hole = Number(url.searchParams.get("hole"));
  const pad = String(hole).padStart(2, "0");
  const bridgesPath = path.join(
    root, "output", courseId, "export", `hole-${pad}`, "bridges.json");

  try {
    const data = await readFile(bridgesPath, "utf8");
    sendJson(res, 200, JSON.parse(data));
  } catch {
    // 404 is expected for holes without bridges — not an error
    sendJson(res, 404, { ok: false, message: "bridges.json not found" });
  }
  return;
}
```

That's the entire server-side change. GET only — UHoleGeo never writes
bridges (Unity is authoritative).

Also add bridge loading to `loadCourseData()` so the course payload
carries bridge metadata. Find the per-hole loop inside
`loadCourseData` (the `for (let i = 1; i <= 18; i++)` block, around
line 90). Alongside the existing `try { hole.anchors = ... }` line,
add:

```javascript
try {
  hole.bridges = JSON.parse(
    await readFile(path.join(exportDir, "bridges.json"), "utf8"));
} catch {}
```

Making bridges available in the initial `/api/course` response lets
the hole-nav indicator show which holes have bridges (minor visual
nice-to-have, see Step 4).

---

### Step 2 — Add a "Bridges" toggle button in `index.html`

Find the layer-bar visibility toggles in `app/index.html` (they're
generated in `buildLayerBar()` in app.js; the toolbar itself is in
index.html). Actually the toggle buttons are created dynamically in
`buildLayerBar()` in app.js — no HTML change needed for the button.
**Skip to Step 3; index.html is unchanged.**

---

### Step 3 — Load, draw, hover, and toggle in `app.js`

All of the following changes are in `Tools/UHoleGeo/app/app.js`.

#### 3.1 — New state variables

Add alongside the existing `let showTrees = true;`,
`let showOB = true;`, `let showCartPath = true;` block (around line
30):

```javascript
let bridges = null;         // [{ id, x, y, z, yaw_deg,
                            //    length_forward_m, length_backward_m,
                            //    expected_path_width_m,
                            //    anchor_forward: {x, z},
                            //    anchor_backward: {x, z} }, ...]
let showBridges = true;
let hoveredBridgeIdx = -1;
```

#### 3.2 — Fetch bridges on hole select

In `selectHole(n)`, alongside the existing `await loadZoneGrid(n);`
call, add:

```javascript
await loadBridges(n);
```

New helper next to `loadZoneGrid`:

```javascript
async function loadBridges(holeNumber) {
  try {
    const res = await fetch(
      "/api/bridges?course=" + COURSE_ID + "&hole=" + holeNumber);
    if (res.ok) {
      const data = await res.json();
      bridges = data.bridges || [];
    } else {
      bridges = [];
    }
  } catch {
    bridges = [];
  }
}
```

Bridges that fail to load (404 or missing file) become an empty array,
so the draw code below is safe for holes without bridges.

#### 3.3 — World-meters → canvas coordinates

UHoleGeo stores everything in **normalized [0, 1] canvas coords**. The
bridge file has **Unity world meters**. Convert:

```javascript
// World meters (Unity frame) → normalized canvas coords [0, 1].
// Uses the same pixel-per-meter ratio as placeTees(): the satellite
// image's (0, 0) maps to the terrain's (-width/2, -length/2) corner
// in Unity (terrain is centered on world origin in HoleGeoImporter).
// The mapping is therefore:
//     px = (worldX + terrainWidth/2) / terrainWidth
//     py = (worldZ + terrainLength/2) / terrainLength
// and finally flipped on Y because UHoleGeo canvas Y=0 is north
// (matches the PNG top-down) while Unity +Z is also north, so we
// invert: py = 1 - py.
function worldToNormalized(worldX, worldZ) {
  const tm = currentHole?.terrainMeta;
  if (!tm) return null;
  const tw = tm.terrain_width_m;
  const tl = tm.terrain_length_m;
  const nx = (worldX + tw / 2) / tw;
  const ny = 1 - (worldZ + tl / 2) / tl;
  return { x: nx, y: ny };
}
```

**Verification note for Code:** compare this transform against how
`cart-paths.json` coordinates align with the zone grid inside
`export-hole.mjs`. If `cart-paths.json` contour points look flipped
in the viewer, the Z-inversion in the formula above is the first
place to adjust — drop the `1 -` prefix and retest. Hole 07 Geo is
the best test case because it has both a cart path and a natural
bridge location.

#### 3.4 — Draw bridges in `drawCanvas()`

At the end of `drawCanvas()`, just before `ctx.restore();` (after the
tee-marker drawing block, before the final `ctx.restore()`), add:

```javascript
// Bridge markers — read-only, authored in Unity.
// Footprint rect is drawn rotated by yaw_deg, then anchor endpoints
// as small circles. Hovered bridge gets a thicker outline.
if (showBridges && bridges && bridges.length > 0) {
  const srcW = satelliteImg ? satelliteImg.width : zoneGridW;
  const srcH = satelliteImg ? satelliteImg.height : zoneGridH;
  const tm = currentHole?.terrainMeta;
  if (srcW && srcH && tm) {
    const mppX = tm.terrain_width_m / srcW;
    const mppY = tm.terrain_length_m / srcH;

    for (let bi = 0; bi < bridges.length; bi++) {
      const b = bridges[bi];
      const center = worldToNormalized(b.x, b.z);
      const fA = worldToNormalized(b.anchor_forward.x, b.anchor_forward.z);
      const bA = worldToNormalized(b.anchor_backward.x, b.anchor_backward.z);
      if (!center || !fA || !bA) continue;

      const cx = (center.x - 0.5) * srcW * drawScale;
      const cy = (center.y - 0.5) * srcH * drawScale;
      const fAx = (fA.x - 0.5) * srcW * drawScale;
      const fAy = (fA.y - 0.5) * srcH * drawScale;
      const bAx = (bA.x - 0.5) * srcW * drawScale;
      const bAy = (bA.y - 0.5) * srcH * drawScale;

      // Footprint rect: length along Z axis = length_forward + length_backward,
      // width across X = expected_path_width_m. Convert to canvas pixels
      // via the avg m/px ratio.
      const mpp = (mppX + mppY) / 2;
      const lenPx = (b.length_forward_m + b.length_backward_m) / mpp * drawScale;
      const widPx = (b.expected_path_width_m || 2.5) / mpp * drawScale;

      const isHover = bi === hoveredBridgeIdx;
      const stroke = "#c77dff";  // light purple, high contrast on satellite
      const fill   = isHover ? "rgba(199,125,255,0.32)"
                             : "rgba(199,125,255,0.18)";

      // yaw_deg is +Y CW rotation in Unity (left-handed Y-up). Canvas
      // Y grows downward, so the effective rotation in canvas space is
      // the SAME yaw_deg (both systems treat +CW around the vertical
      // axis identically when viewed top-down). Rotate around center.
      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(b.yaw_deg * Math.PI / 180);
      ctx.fillStyle = fill;
      ctx.strokeStyle = stroke;
      ctx.lineWidth = isHover ? 2.5 : 1.5;
      ctx.beginPath();
      ctx.rect(-widPx / 2, -lenPx / 2, widPx, lenPx);
      ctx.fill();
      ctx.stroke();
      // Forward-direction tick mark (short line from center toward +Z in
      // local frame; helps disambiguate which end is "forward")
      ctx.strokeStyle = "#ffffff";
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(0, 0);
      ctx.lineTo(0, -lenPx / 2 * 0.9);
      ctx.stroke();
      ctx.restore();

      // Anchor endpoints (NOT rotated — already in world space)
      for (const [ax, ay, label] of [[fAx, fAy, "F"], [bAx, bAy, "B"]]) {
        ctx.beginPath();
        ctx.arc(ax, ay, isHover ? 6 : 4, 0, Math.PI * 2);
        ctx.fillStyle = stroke;
        ctx.fill();
        ctx.strokeStyle = "#000";
        ctx.lineWidth = 1.2;
        ctx.stroke();
        if (isHover) {
          ctx.fillStyle = "#000";
          ctx.font = "bold 8px sans-serif";
          ctx.textAlign = "center";
          ctx.textBaseline = "middle";
          ctx.fillText(label, ax, ay);
        }
      }
    }
  }
}
```

#### 3.5 — Hit-test + tooltip

Add a `hitTestBridge` alongside the existing `hitTestTee`:

```javascript
function hitTestBridge(canvasX, canvasY) {
  if (!showBridges || !bridges || bridges.length === 0) return -1;
  const srcW = satelliteImg ? satelliteImg.width : zoneGridW;
  const srcH = satelliteImg ? satelliteImg.height : zoneGridH;
  const tm = currentHole?.terrainMeta;
  if (!srcW || !srcH || !tm) return -1;

  const hitRadius = 14;

  for (let i = 0; i < bridges.length; i++) {
    const b = bridges[i];
    const center = worldToNormalized(b.x, b.z);
    if (!center) continue;

    let imgX = (center.x - 0.5) * srcW * drawScale;
    let imgY = (center.y - 0.5) * srcH * drawScale;

    if (canvasRotation !== 0) {
      const rad = canvasRotation * Math.PI / 180;
      const c = Math.cos(rad), s = Math.sin(rad);
      const rx = imgX * c - imgY * s;
      const ry = imgX * s + imgY * c;
      imgX = rx; imgY = ry;
    }

    const cx = imgX * zoomLevel + canvas.width / 2 + panX;
    const cy = imgY * zoomLevel + canvas.height / 2 + panY;

    const dx = canvasX - cx, dy = canvasY - cy;
    if (dx * dx + dy * dy <= hitRadius * hitRadius) return i;
  }
  return -1;
}
```

In the existing `mousemove` handler (where `hitTestTee` is called for
hover cursor updates), also check bridges so the hovered marker
re-renders with its thicker outline. Add right after the
`const teeIdx = hitTestTee(x, y);` line:

```javascript
const bridgeIdx = hitTestBridge(x, y);
if (bridgeIdx !== hoveredBridgeIdx) {
  hoveredBridgeIdx = bridgeIdx;
  drawCanvas();
}
updateBridgeTooltip(bridgeIdx, x, y);
```

And a tooltip mirroring `updateTeeTooltip`:

```javascript
function updateBridgeTooltip(idx, x, y) {
  let tooltip = document.getElementById("bridge-tooltip");
  if (idx < 0) { hideBridgeTooltip(); return; }
  const b = bridges[idx];
  if (!tooltip) {
    tooltip = document.createElement("div");
    tooltip.id = "bridge-tooltip";
    tooltip.className = "tee-tooltip"; // reuse existing style
    document.getElementById("canvas-stage").appendChild(tooltip);
  }
  tooltip.innerHTML =
    "<strong>Bridge: " + (b.id || "?") + "</strong><br>" +
    "yaw " + b.yaw_deg.toFixed(1) + "°, width " +
      (b.expected_path_width_m || 2.5).toFixed(1) + "m<br>" +
    "F: (" + b.anchor_forward.x.toFixed(1) + ", " +
             b.anchor_forward.z.toFixed(1) + ")<br>" +
    "B: (" + b.anchor_backward.x.toFixed(1) + ", " +
             b.anchor_backward.z.toFixed(1) + ")";
  tooltip.style.left = (x + 15) + "px";
  tooltip.style.top = (y - 10) + "px";
  tooltip.hidden = false;
}

function hideBridgeTooltip() {
  const t = document.getElementById("bridge-tooltip");
  if (t) t.hidden = true;
}
```

Also call `hideBridgeTooltip()` alongside the existing
`hideTeeTooltip()` in the canvas `mouseleave` handler.

#### 3.6 — "Bridges" toggle button in the layer bar

In `buildLayerBar()`, find the `<div class="layer-visibility">` block
that generates the Trees / Cart Path / OB toggle buttons. Add a
fourth:

```javascript
'<button id="btn-toggle-bridges" class="is-active-toggle" ' +
  'title="Toggle Bridges visibility">Bridges</button>' +
```

And below, alongside the existing toggle handlers:

```javascript
document.getElementById("btn-toggle-bridges").addEventListener("click", function () {
  showBridges = !showBridges;
  this.classList.toggle("is-active-toggle", showBridges);
  hoveredBridgeIdx = -1;
  hideBridgeTooltip();
  drawCanvas();
});
```

No changes to `LAYER_ZONES` or `filterBrushesByLayer` — bridges aren't
a paintable zone, they're a top-level overlay like the tee markers.

---

### Step 4 — Optional nice-to-have: bridge indicator in hole nav

In `buildHoleNav()`, after the existing `hasBounds` dot, add a small
indicator for holes that have bridges. Find this line:

```javascript
const hasBounds = hole.hasHoleBounds;
```

Right after it, add:

```javascript
const bridgeCount = hole.bridges?.bridges?.length || 0;
```

Then in the `btn.innerHTML` assignment, append a bridge chip after
the par label:

```javascript
btn.innerHTML =
  '<span class="bounds-dot ' + (hasBounds ? 'has-bounds' : 'no-bounds') + '"></span>' +
  "Hole " + hole.number +
  '<span class="par-label">P' + (ch?.par ?? "?") + "</span>" +
  (bridgeCount > 0
    ? '<span class="par-label" style="background:rgba(199,125,255,0.25);' +
      'color:#c77dff">🌉 ' + bridgeCount + '</span>'
    : '');
```

Pure cosmetic — skip if it conflicts with anything in the CSS.

---

### Verification

1. In Unity, place a `BridgeAnchor` on Hole 07 Geo and export. Confirm
   `Tools/UHoleGeo/output/lomond-country-club/export/hole-07/bridges.json`
   exists.
2. Start the UHoleGeo dev server: `node scripts/dev-server.mjs`.
3. Open the app, click Hole 07.
4. Switch to "Overlay" view (so the zone mask is visible).
5. Expect to see:
   - A light-purple rotated rectangle over the bridge location.
   - A short white tick mark pointing "forward" (toward +Z in Unity).
   - Two purple circles with black outlines at `anchor_forward` and
     `anchor_backward`.
6. Hover the bridge rect — outline thickens, circles show "F" and "B"
   labels, tooltip appears with the bridge id, yaw, and both anchor
   world coords.
7. Paint the cart-path zone (zone 8) right up to one of the anchor
   circles. The circle should stay visible over the painted mask.
8. Click the "Bridges" toggle in the layer bar. Markers disappear /
   reappear.
9. Rotate the canvas (Q/E or the rotation buttons). Bridge markers
   rotate with the satellite image.
10. Open Hole 01 (no bridges). No bridge markers. No console errors.
    Toggle button still works.

Coordinate sanity check:
- In Unity, note the bridge's world `(x, z)` from the exporter window.
- In UHoleGeo, open `bridges.json` directly and confirm those values
  match.
- Check the bridge's rendered position on the canvas vs where the
  water + cart path meet on the satellite image. If the marker is
  offset by a consistent amount in one axis, the `worldToNormalized`
  formula needs its Y-flip adjusted (see the verification note in
  Step 3.3).

Regression:
- [ ] Tee markers still drag / draw / tooltip correctly.
- [ ] Cart path painting still works on a hole with a bridge.
- [ ] `Save` still saves zones (bridges are read-only, not touched by
      Save).
- [ ] `Regen Heightmap` still works — `bridges.json` is not read by
      `generate-terrain.mjs` or `export-hole.mjs`.

---

### Do NOT change

- `bridges.json` schema or the Unity exporter (`BridgeAnchor`,
  `BridgeExporter`). Coordinates flow one way only: Unity → UHoleGeo.
- `cart-paths.json` or the cart-path export/vectorization logic in
  `export-hole.mjs`.
- `classify-zones.mjs`, `generate-terrain.mjs`, or any terrain
  pipeline.
- The `LAYER_ZONES` map — bridges aren't a zone, they're an overlay.
- The zone brush, paint modes, undo stack, or smoothing buttons.
- Leaflet / bounds-setting UI.

### Out of scope (future work)

- Editing bridges in UHoleGeo (explicitly rejected — Unity is the
  single source of truth).
- Auto-snapping the cart-path mask to anchor points (could be a
  future "Smooth Cart Path to Anchors" button; not this task).
- Rendering the Unity bridge prefab's mesh (would require asset
  extraction; the rectangle footprint is enough for alignment work).

---

## Completed Task — Bridge Placement Tool (Unity → UHoleGeo export)

Cesar places bridge prefabs by hand in a hole scene. This tool captures
their positions/rotations and exports them as `bridges.json` into the
hole's UHoleGeo export folder. UHoleGeo will later consume that file
so cart-path splines can snap to bridge anchor points instead of
guessing from screenshots.

**Target file (new):** `Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs`
**Also new:** `Assets/Scripts/Course/BridgeAnchor.cs`
**No `TreePlacer` or `HoleGeoImporter` changes required.**

---

### Design summary

- EditorWindow: **`Window > Trees > Bridge Exporter`** (put it next to
  the Tree Brush so they live in the same menu cluster).
- Artist drops bridge prefabs anywhere under `HoleRoot` — this tool
  doesn't prescribe WHERE in the hierarchy. Detection is by component,
  see Step 1.
- On "Export Bridges for Current Hole", the tool:
    1. Resolves the hole number + Lite/Geo/Flat flavour from the
       active scene name (same logic `TreePlacer.ImportTreesMenuItem`
       uses).
    2. Finds all `BridgeAnchor` components in the scene.
    3. Writes `bridges.json` to
       `Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/`
       (or the corresponding Lite / `-flat` folder), and mirrors to
       the sibling pipeline (Geo↔Lite) if that folder exists.
- No heightmap modifications, no mesh generation, no splatmap touches.
  Pure position export. Bridges render in Unity because the prefab is
  already in the scene; UHoleGeo gets the coordinates separately.

---

### Step 1 — `BridgeAnchor` marker component

Create `Assets/Scripts/Course/BridgeAnchor.cs`:

```csharp
using UnityEngine;

namespace Golfin.Course
{
    /// <summary>
    /// Marks a GameObject as a bridge for the export pipeline.
    /// Attach to the root of a bridge prefab. The exporter captures
    /// world position + yaw rotation + the two anchor endpoints.
    ///
    /// Anchor endpoints are the points where cart paths should meet
    /// the bridge. They're defined as local offsets along the bridge's
    /// local Z axis (forward) from the bridge's pivot.
    /// </summary>
    [DisallowMultipleComponent]
    public class BridgeAnchor : MonoBehaviour
    {
        [Tooltip("Optional bridge id. If empty, exporter auto-assigns 1..N.")]
        public string id = "";

        [Tooltip("Distance from pivot along local +Z to the 'far' anchor (meters).")]
        public float lengthForward = 3f;

        [Tooltip("Distance from pivot along local -Z to the 'near' anchor (meters).")]
        public float lengthBackward = 3f;

        [Tooltip("Path width this bridge expects to meet (meters). " +
                 "Informational — UHoleGeo uses it to sanity-check cart width.")]
        public float expectedPathWidth = 2.5f;

        // Editor gizmo so the artist sees the anchor endpoints in
        // Scene view without needing to open the exporter window.
        private void OnDrawGizmos()
        {
            Vector3 a = transform.position + transform.forward * lengthForward;
            Vector3 b = transform.position - transform.forward * lengthBackward;
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.35f);
            Gizmos.DrawSphere(b, 0.35f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position,
                transform.position + transform.forward * (lengthForward + 1f));
        }
    }
}
```

Lives under `Assets/Scripts/Course/` so it compiles in both editor and
player (same pattern as `SurfaceMarker`).

---

### Step 2 — EditorWindow scaffold

Create `Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs`
wrapped in `#if UNITY_EDITOR ... #endif`, namespace
`Golfin.CourseImport`.

```csharp
public class BridgeExporter : EditorWindow
{
    [MenuItem("Window/Trees/Bridge Exporter")]
    public static void ShowWindow()
    {
        var w = GetWindow<BridgeExporter>("Bridges");
        w.minSize = new Vector2(320, 240);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bridge Exporter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        var anchors = FindAnchorsInActiveScene();
        EditorGUILayout.LabelField(
            $"Found {anchors.Count} BridgeAnchor(s) in scene.");

        if (anchors.Count > 0)
        {
            EditorGUILayout.Space();
            foreach (var a in anchors)
            {
                Vector3 p = a.transform.position;
                EditorGUILayout.LabelField(
                    $"  • {(string.IsNullOrEmpty(a.id) ? a.name : a.id)}" +
                    $"  @ ({p.x:F2}, {p.z:F2})  yaw {a.transform.eulerAngles.y:F1}°");
            }
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Add BridgeAnchor to Selected GameObject"))
            AddAnchorToSelected();

        EditorGUILayout.Space();

        GUI.enabled = anchors.Count > 0;
        if (GUILayout.Button("Export Bridges for Current Hole",
                             GUILayout.Height(30)))
            ExportBridgesForCurrentHole(anchors);
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Writes bridges.json to the current hole's UHoleGeo export " +
            "folder (Lite/Geo/Flat auto-detected from scene name). " +
            "UHoleGeo can read this file so cart-path splines snap to " +
            "bridge anchors.",
            MessageType.Info);
    }

    private double lastRepaint;
    private void OnInspectorUpdate()
    {
        if (EditorApplication.timeSinceStartup - lastRepaint > 0.5)
        {
            Repaint();
            lastRepaint = EditorApplication.timeSinceStartup;
        }
    }
}
```

Helper stubs:
- `List<BridgeAnchor> FindAnchorsInActiveScene()`
- `void AddAnchorToSelected()`
- `void ExportBridgesForCurrentHole(List<BridgeAnchor> anchors)`

---

### Step 3 — `FindAnchorsInActiveScene` + `AddAnchorToSelected`

```csharp
private static List<Golfin.Course.BridgeAnchor> FindAnchorsInActiveScene()
{
    var result = new List<Golfin.Course.BridgeAnchor>();
    var activeScene = UnityEditor.SceneManagement.EditorSceneManager
        .GetActiveScene();
    foreach (var root in activeScene.GetRootGameObjects())
        result.AddRange(
            root.GetComponentsInChildren<Golfin.Course.BridgeAnchor>(true));
    return result;
}

private static void AddAnchorToSelected()
{
    var sel = Selection.activeGameObject;
    if (sel == null)
    {
        EditorUtility.DisplayDialog("Add Bridge Anchor",
            "Select a GameObject in the scene first.", "OK");
        return;
    }
    if (sel.GetComponent<Golfin.Course.BridgeAnchor>() != null)
    {
        EditorUtility.DisplayDialog("Add Bridge Anchor",
            "That GameObject already has a BridgeAnchor.", "OK");
        return;
    }
    Undo.AddComponent<Golfin.Course.BridgeAnchor>(sel);
    EditorUtility.SetDirty(sel);
}
```

---

### Step 4 — `ExportBridgesForCurrentHole`

```csharp
[System.Serializable]
private class BridgeDTO
{
    public string id;
    public float x;     // world X, meters
    public float z;     // world Z, meters
    public float y;     // world Y, meters (for reference; UHoleGeo is 2D)
    public float yaw_deg;
    public float length_forward_m;
    public float length_backward_m;
    public float expected_path_width_m;
    public AnchorDTO anchor_forward;
    public AnchorDTO anchor_backward;
}

[System.Serializable]
private class AnchorDTO
{
    public float x;
    public float z;
}

[System.Serializable]
private class BridgesFile
{
    public string schema_version = "1.0.0";
    public int hole_number;
    public string flavour;  // "geo" | "lite" | "geo-flat" | "lite-flat"
    public int bridge_count;
    public BridgeDTO[] bridges;
}

private static void ExportBridgesForCurrentHole(
    List<Golfin.Course.BridgeAnchor> anchors)
{
    var activeScene = UnityEditor.SceneManagement.EditorSceneManager
        .GetActiveScene();
    string sceneName = activeScene.name;
    string scenePath = activeScene.path ?? "";

    bool isGeo = scenePath.IndexOf("_Geo", System.StringComparison.OrdinalIgnoreCase) >= 0
        || sceneName.IndexOf("_Geo", System.StringComparison.OrdinalIgnoreCase) >= 0;
    bool isFlat = scenePath.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0
        || sceneName.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0;

    string baseName = System.Text.RegularExpressions.Regex
        .Replace(sceneName, "(_Geo)?(_Flat)?$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    int holeNumber = -1;
    if (baseName.StartsWith("Hole_") && baseName.Length >= 7)
        int.TryParse(baseName.Substring(5, 2), out holeNumber);

    if (holeNumber < 1 || holeNumber > 18)
    {
        EditorUtility.DisplayDialog("Export Bridges",
            $"Cannot detect hole number from scene '{sceneName}'.\n" +
            "Expected 'Hole_XX', 'Hole_XX_Geo', 'Hole_XX_Flat', " +
            "or 'Hole_XX_Geo_Flat'.", "OK");
        return;
    }

    string flavour = (isGeo ? "geo" : "lite") + (isFlat ? "-flat" : "");
    string toolFolder = isGeo ? "UHoleGeo" : "UHoleLite";
    string holeFolder = isFlat ? $"hole-{holeNumber:D2}-flat"
                               : $"hole-{holeNumber:D2}";
    string exportPath = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(
            Application.dataPath, "..",
            $"Tools/{toolFolder}/output/lomond-country-club/export",
            holeFolder));

    if (!System.IO.Directory.Exists(exportPath))
    {
        EditorUtility.DisplayDialog("Export Bridges",
            $"Export folder not found:\n{exportPath}\n\n" +
            "Has this hole been exported from UHoleGeo yet?", "OK");
        return;
    }

    var dtos = new BridgeDTO[anchors.Count];
    for (int i = 0; i < anchors.Count; i++)
    {
        var a = anchors[i];
        Vector3 p = a.transform.position;
        Vector3 fwd = a.transform.forward;

        Vector3 anchorF = p + fwd * a.lengthForward;
        Vector3 anchorB = p - fwd * a.lengthBackward;

        dtos[i] = new BridgeDTO
        {
            id = string.IsNullOrEmpty(a.id) ? $"bridge_{i + 1}" : a.id,
            x = p.x, y = p.y, z = p.z,
            yaw_deg = NormalizeYaw(a.transform.eulerAngles.y),
            length_forward_m = a.lengthForward,
            length_backward_m = a.lengthBackward,
            expected_path_width_m = a.expectedPathWidth,
            anchor_forward  = new AnchorDTO { x = anchorF.x, z = anchorF.z },
            anchor_backward = new AnchorDTO { x = anchorB.x, z = anchorB.z },
        };
    }

    var file = new BridgesFile
    {
        hole_number = holeNumber,
        flavour = flavour,
        bridge_count = dtos.Length,
        bridges = dtos,
    };

    string outPath = System.IO.Path.Combine(exportPath, "bridges.json");
    string json = JsonUtility.ToJson(file, true);
    System.IO.File.WriteAllText(outPath, json);

    Debug.Log($"[BridgeExporter] Wrote {dtos.Length} bridge(s) to {outPath}");

    // Mirror to the other pipeline (Geo ↔ Lite) if its folder exists.
    string otherTool = isGeo ? "UHoleLite" : "UHoleGeo";
    string otherExportPath = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(
            Application.dataPath, "..",
            $"Tools/{otherTool}/output/lomond-country-club/export",
            holeFolder));
    if (System.IO.Directory.Exists(otherExportPath))
    {
        string mirrorPath = System.IO.Path.Combine(
            otherExportPath, "bridges.json");
        System.IO.File.WriteAllText(mirrorPath, json);
        Debug.Log($"[BridgeExporter] Mirrored to {mirrorPath}");
    }
}

private static float NormalizeYaw(float yawDeg)
{
    yawDeg = yawDeg % 360f;
    if (yawDeg > 180f) yawDeg -= 360f;
    if (yawDeg < -180f) yawDeg += 360f;
    return yawDeg;
}
```

---

### Step 5 — Example JSON output

```json
{
  "schema_version": "1.0.0",
  "hole_number": 7,
  "flavour": "geo",
  "bridge_count": 1,
  "bridges": [
    {
      "id": "bridge_1",
      "x": -184.30,
      "y": 2.45,
      "z": 72.10,
      "yaw_deg": 38.5,
      "length_forward_m": 3.0,
      "length_backward_m": 3.0,
      "expected_path_width_m": 2.5,
      "anchor_forward":  { "x": -182.43, "z": 74.45 },
      "anchor_backward": { "x": -186.17, "z": 69.75 }
    }
  ]
}
```

**Coordinate convention (important for UHoleGeo consumption):**
`x`/`z` are Unity world meters, matching `cart-paths.json`'s
`contour[i].x`/`.z` exactly. UHoleGeo can treat `anchor_forward` /
`anchor_backward` as snap targets for spline endpoints directly — no
coordinate transformation required. `y` is included for future 3D
routing but can be ignored by the current 2D path logic.

---

### Verification

1. Open `Hole_07_Geo`. Drop a bridge prefab over the stream.
2. `Window > Trees > Bridge Exporter` → window shows "Found 0
   BridgeAnchor(s)".
3. Select the bridge GameObject → click "Add BridgeAnchor to Selected
   GameObject". Window now shows "Found 1" with its position.
4. Yellow gizmo line runs through the bridge with spheres at the two
   anchor endpoints. Rotate/move the bridge — gizmo tracks.
5. Click "Export Bridges for Current Hole". Console logs:
   - `[BridgeExporter] Wrote 1 bridge(s) to .../hole-07/bridges.json`
   - `[BridgeExporter] Mirrored to .../UHoleLite/.../hole-07/bridges.json`
6. Open the written `bridges.json` — coordinates match the bridge's
   Unity world position, yaw matches Y rotation, anchor endpoints are
   offset along the bridge's local forward.

Regression:
- [ ] `Hole_01_Geo` (no bridges): window shows "Found 0", export button
      disabled, no crash.
- [ ] Rename a scene to `Test_Scene`: export shows a clear dialog, no
      crash.
- [ ] `Hole_07_Geo_Flat`: export lands in `hole-07-flat/bridges.json`
      and mirrors to the Lite flat folder if it exists.

---

### Out of scope (future work, not this task)

- UHoleGeo reading `bridges.json` and routing splines to anchors —
  separate JS-side task when Cesar tackles the UHoleGeo tool.
- Bridge prefab authoring (width variants, material sets, LODs).
- Physics colliders / ball bounce behaviour on bridges.
- Runtime bridge loading for gameplay.

---

### Do NOT change

- `TreePlacer.cs`, `HoleGeoImporter.cs`, `HoleLiteImporter.cs`.
- `cart-paths.json` schema — bridges live in a separate file.
- Any scene hierarchy conventions beyond adding `BridgeAnchor`
  components. Bridges can live anywhere under `HoleRoot` (or even at
  scene root — detection is by component, not by name).

---

## Previous Task — Fix Tee Border Ring Texture Twisting (Constant V)

The inset tee border ring is in place and orientation is correct (light
toward tee surface, dark toward terrain). But the texture shows
distortion/twisting at points along the ring's curve.

**Cause:** In `CreateTeeMeshWithInsetBorder`, the border vert
duplication assigns `v = (src.x + src.z) / borderTileSize`. That's a
world-XZ projection, which jumps around as the ring curves. For a
texture with meaningful V-direction content, that would tile badly on
a closed ring.

**But the texture has no meaningful V content.** `T_TeeDark_Albedo` is
a left-to-right color gradient (green → uniform green → rough-darker)
with only mild noise. V variation is purely decorative. Setting V to a
constant eliminates the twisting without visibly losing anything.

### The change

In `CreateTeeMeshWithInsetBorder` (the mesh builder added in the last
task), in the border vert duplication block, find:

```csharp
float u = 1f - Mathf.Clamp01(dist / borderWidth);
float v = (src.x + src.z) / borderTileSize;
```

Replace with:

```csharp
float u = 1f - Mathf.Clamp01(dist / borderWidth);
// T_TeeDark_Albedo has no meaningful V content — it's a pure L→R
// color gradient (tee-green to rough-darker). World-XZ V causes
// visible texture twisting on the ring's curve. Constant V removes
// the twisting; no visual content is lost because V has none to lose.
float v = 0.5f;
```

That's the entire change. `borderTileSize` stays as a function parameter
(still used by other callers / future-me if we ever swap in a texture
with V-direction content).

### Verification

- [ ] Re-import any tee-bearing hole (Hole 4 is fine).
- [ ] Dark border ring still visible, still oriented correctly (light
      toward tee, dark toward terrain).
- [ ] Texture twisting / wavy distortion at the bottom edge is gone.
- [ ] Gradient still clean from the tee-surface edge of the ring to
      the terrain-adjacent edge.

### Do NOT change

- Anything else in `CreateTeeMeshWithInsetBorder`.
- The U calculation.
- The `borderTileSize` parameter or its callsite.
- Any other mesh builder, material, or system.

---

✅ DONE: 2026-04-18 Constant-V UV fix applied. Additionally fixed geometric crease: rebuilt ring as manual quad-strip (outer contour × inset contour vertex pairs by index) instead of CDT-classified triangles — eliminates long diagonal spanning tris. CDT now only triangulates the inset contour for submesh 0; submesh 1 is a clean N-quad strip with winding auto-checked.

✅ DONE: 2026-04-18 Bridge Placement Tool implemented. BridgeAnchor.cs (Golfin.Course) marker component with gizmo. BridgeExporter.cs EditorWindow at Window > Trees > Bridge Exporter — finds anchors, previews positions, exports bridges.json to UHoleGeo/UHoleLite export folder with auto-detection of Geo/Lite/Flat from scene name, mirrors to sibling pipeline folder.

✅ DONE: 2026-04-19 Water Shore Phase 1 sampling script created at Tools/sample-shore-heights.js. Course-wide max drop 14.07m (Hole 12, body 1), max dR_needed 34.7m. Recommended ShoreMaxRadiusMeters cap for Phase 2 spec: 40m. Holes 7 (8.63m) and 13 (6.62m) also need the fix. Per-hole terrain dims read from terrain-meta.json (not hardcoded).

✅ DONE: 2026-04-18 Bridge Viewer in UHoleGeo implemented. dev-server: /api/bridges GET route + bridges loaded into hole nav data. app.js: loadBridges() fetches on hole select; worldToNormalized() converts Unity world meters to canvas coords; drawCanvas() draws purple rotated footprint rect + forward tick + anchor endpoint circles; hitTestBridge() + tooltip on hover; "Bridges" toggle in layer bar; bridge count chip in hole nav.
