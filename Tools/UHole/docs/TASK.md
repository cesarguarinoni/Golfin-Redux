# TASK.md — UHole Instructions for Claude Code

> Read this file at the start of each task. After completing, add a status line at the bottom.

## Context

- Working directory: `Tools/UHole/`
- Vanilla HTML/CSS/JS app — zero npm dependencies
- ESM scripts (`.mjs`, `"type": "module"`)
- Dev server: `http://127.0.0.1:4173` or `4174`
- App: `app/index.html`, `app/styles.css`, `app/app.js`
- Data: `output/lomond-country-club/`

---

## Current Task — Alignment Tool v2: Stacked Overlay Workflow

Redesign the alignment panel into a single combined view with the official hole map
on top and the GSI basemap underneath.

### Workflow

1. **Stacked view** — official map (adjustable opacity) on top, GSI basemap underneath
2. **Align mode** — operator zooms/rotates/pans the basemap layer to match the official map, then locks it
3. **Fine Tune mode** (optional) — control point pairs for mathematical affine transform
4. **Anchors mode** — click composite view to place tee/green/pin markers. World coords come from basemap tile geo-bounds directly
5. **Export** — produces anchor positions with geo-precise world coords

### UI Layout

Replace the current side-by-side layout. Single combined view, full width, tall:

- **Toolbar:** `[Hole ▼] [Mode: Align | Fine Tune | Anchors] [Opacity slider] [Lock] [Save] [Export]`
- **Combined view:** Three stacked layers (bottom to top):
  - Layer 1: GSI basemap mosaic (z-index 1, own CSS transform, always opaque)
  - Layer 2: Official map image (z-index 2, opacity-controlled, pointer-events: none)
  - Layer 3: Anchor markers (z-index 3)
- **Status bar:** Anchor list chips + anchor type dropdown
- **Hole cards grid:** unchanged

### Modes

**Align mode (default):**
- Scroll wheel = zoom basemap only
- Middle-click drag = pan basemap only
- Rotate +/- buttons = rotate basemap only
- Shift+scroll = zoom both layers (overall navigation)
- Shift+middle-drag = pan both layers
- "Lock Alignment" saves basemap transform, prevents further manipulation

**Fine Tune mode (optional):**
- Same as current CP workflow: click official map, then click basemap
- "Compute Transform" runs affine solver
- Affine transform overrides visual alignment for coordinate conversions

**Anchors mode:**
- Dropdown selects type: Back Tee, Regular Tee, Front Tee, Ladies Tee, Green Center, Pin
- Left-click places anchor, mapped to basemap world coords via inverse CSS transform
- Click existing anchor to select, Delete/× to remove

### Click-to-World Conversion (Anchors mode)

1. Get click `(cx, cy)` relative to container
2. Inverse basemap CSS transform → mosaic pixel `(mx, my)`
3. `tileCol = floor(mx / 256)`, `tileRow = floor(my / 256)` → tile indices
4. `fx = (mx % 256) / 256`, `fy = (my % 256) / 256`
5. `lon = tile.bounds.west + fx * (east - west)`
6. `lat = tile.bounds.north - fy * (north - south)`

No affine transform involved — direct basemap coordinates, no residual error.

### Data Model (`alignment.json`)

Add `visual_alignment` field:
```json
{
  "visual_alignment": {
    "locked": true,
    "basemap_transform": {
      "translate_x": 150.5, "translate_y": -30.2,
      "scale": 2.35, "rotation_deg": -22.5
    }
  },
  "anchors": [
    {
      "type": "tee_back", "label": "Back Tee",
      "view_px": { "x": 350, "y": 280 },
      "basemap_px": { "x": 1450, "y": 820 },
      "basemap_tile": { "local_path": "...", "z": 17, "x": 115211, "y": 51955 },
      "world": { "lat": 34.9138, "lon": 136.4411 }
    }
  ]
}
```

### Verification

- [ ] Combined view shows official map on top of GSI basemap
- [ ] Opacity slider fades between layers
- [ ] Align mode: basemap zooms/pans/rotates independently
- [ ] Shift+interactions move both layers
- [ ] Lock/unlock works
- [ ] Anchors mode: click places anchor at correct basemap position
- [ ] Anchor world coords from basemap tile bounds (not affine)
- [ ] Fine Tune mode: control point pairs still work
- [ ] Save persists visual alignment + anchors
- [ ] Export produces correct world positions

### Do NOT change

- `export-hole.mjs` (reads anchors.json as-is)
- Unity importer
- Other panels
- Dev server API (add new endpoints if needed)

✅ DONE: 2026-04-06 — Implemented Alignment Tool v2: replaced side-by-side with stacked overlay (official map on basemap), added 3 modes (Align/Fine Tune/Anchors), opacity slider, lock alignment, click-to-world via basemap tile bounds, visual_alignment persistence in alignment.json.
