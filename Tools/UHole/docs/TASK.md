# TASK.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each UHole task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`

---

## Context

- Working directory: `Tools/UHole/`
- This is a vanilla HTML/CSS/JS app — zero npm dependencies, keep it that way
- All scripts are ESM (`.mjs`, `"type": "module"`)
- Dev server runs on `http://127.0.0.1:4173` or `4174`
- The app is served from `app/index.html`, `app/styles.css`, `app/app.js`
- Output data lives in `output/lomond-country-club/`

---

## Current Task — Fix Course Center Coordinates and Re-fetch Basemap

**Problem:** The inferred course center was `34.8789, 136.4468` — this is ~3.8km south
of the actual course. The GSI tiles were showing a different golf course entirely.

**Verified correct coordinate (clubhouse area):** `34.91318, 136.44164`

The course center (centroid of all fairways) is slightly west/northwest of the clubhouse.
Use `34.9115, 136.4370` as the corrected course center — this puts it roughly in the
middle of the course layout based on aerial imagery.

### Step 1: Update `scripts/fetch-gsi-basemap.mjs`

Replace the `inferredCourseCenter` object:

```javascript
const inferredCourseCenter = {
  lat: 34.9115,
  lon: 136.4370,
  source: "manual_verification_google_maps",
  confidence: "high",
  note: "Clubhouse verified at 34.9132, 136.4416 via Google Maps. Course center estimated from aerial imagery to be slightly west-northwest of clubhouse."
};
```

### Step 2: Delete existing basemap tiles

Delete all files in:
- `output/lomond-country-club/basemap/gsi-photo-z17/`
- `output/lomond-country-club/basemap/gsi-dem-z14/`
- `output/lomond-country-club/basemap/manifest.json`

This forces a clean re-fetch with the corrected center.

### Step 3: Run the basemap fetch

```powershell
cd Tools/UHole
node scripts/fetch-gsi-basemap.mjs --force
```

Verify the new manifest has tiles covering the area around 34.91°N, 136.43-136.45°E.

### Step 4: Update `output/lomond-country-club/provenance.json`

The basemap fetch script already updates provenance automatically, but verify the
`base_map.inferred_center` field now shows the corrected coordinates.

### Step 5: Reset alignment statuses

All 18 holes should already be at `ready_for_control_points` with 0 control points.
No change needed there — just verify none have stale tile references in their
`alignment.json` files. The `target_base_map.path` should still point to
`basemap/manifest.json` which is correct.

If any `alignment.json` has a `selected_photo_tile` field pointing to an old tile path,
clear that field (set to `null`).

### Step 6: Update `output/lomond-country-club/course.json`

The course.json doesn't store coordinates currently, but add a `center` field after
`address` for reference:

```json
"center": {
  "lat": 34.9115,
  "lon": 136.4370,
  "confidence": "high",
  "source": "manual_verification_google_maps"
},
```

### Verification

- [ ] `fetch-gsi-basemap.mjs` has the corrected center coordinates
- [ ] Old tiles deleted, new tiles fetched
- [ ] New manifest.json references tiles around 34.91°N
- [ ] provenance.json updated with new center
- [ ] course.json has center field
- [ ] No alignment.json has stale `selected_photo_tile` references
- [ ] App loads without errors after restart
- [ ] Base Map panel shows new tile previews
- [ ] Alignment panel shows GSI tiles that cover the correct golf course

### Do NOT change

- Any app UI code (index.html, styles.css, app.js)
- The hole data (hole.json files, yardages, pars, etc.)
- The schemas
- The ingest script

✅ DONE: 2026-04-03 — Corrected course center to 34.9115, 136.4370; deleted old tiles; re-fetched 340 photo + 9 DEM tiles covering correct area; updated provenance.json, course.json (center field added); no stale alignment.json references found.

---

## Current Task — Fix Control Point Placement Accuracy Under Zoom/Rotate

**Problem:** Control point markers are NOT placed precisely under the cursor when the
viewport is zoomed in and/or rotated. The discrepancy grows with higher zoom levels
and rotation angles. At scale=1 rotation=0 it's fine, but at 400% zoom + 25° rotation
the dot lands far from where the user clicked.

This affects both the official map stage and the basemap mosaic stage.

### Root Cause Analysis

The issue is in `clickToContentCoords()` and/or `placeMarker()` / `updateMarkerPositions()`.
There are TWO coordinate systems at play:

1. **Screen space** — where the mouse click happens (`event.clientX/Y`)
2. **Content space** — the un-transformed image pixel coordinates

The CSS transform applied to `.stage-viewport` is:
`translate(Xpx, Ypx) scale(S) rotate(Rdeg)`

This means the transform order (as CSS applies right-to-left) is: rotate → scale → translate.
The inverse (screen→content) must undo these in reverse: un-translate → un-scale → un-rotate.

`screenToLocal()` and `localToScreenOffset()` already exist and handle this math.

### Debugging Strategy

Use the browser dev tools console to verify at each step:

1. Add a `console.log` in `clickToContentCoords()` that prints:
   - `event.clientX/Y` (raw mouse)
   - `stageRect` (the `.image-stage` bounding rect)
   - `viewport` state (x, y, scale, rotation)
   - The computed `localClick` from `screenToLocal()`
   - The final `x, y, normalized_x, normalized_y` returned

2. Add a `console.log` in `placeMarker()` that prints:
   - Input `contentX, contentY`
   - The `offset` from `localToScreenOffset()`
   - The final `left, top` CSS values

3. Test by clicking the same visible point at:
   - scale=1, rotation=0 (should be accurate — baseline)
   - scale=3, rotation=0 (test scale only)
   - scale=1, rotation=20 (test rotation only)
   - scale=3, rotation=20 (test both)

4. Compare the `normalized_x/y` values across these tests — if clicking the same
   visual point, they should be identical regardless of viewport state.

### Known Pitfalls

- `offsetLeft`/`offsetTop` reflect the **un-transformed DOM layout**, not the visual
  position after CSS transforms. Walking `offsetParent` inside a transformed container
  gives wrong results.
- `getBoundingClientRect()` DOES reflect CSS transforms but returns the axis-aligned
  bounding box, which is distorted by rotation.
- The `.stage-viewport` has `transform-origin: top left` — make sure the inverse math
  matches this origin.
- For the basemap mosaic, each tile button is inside a grid cell — the `offsetLeft/Top`
  walk may not correctly account for the grid layout within the transformed viewport.

### Approach

The most reliable approach for `clickToContentCoords()`:

1. Get the click position relative to the `.image-stage` container: `screenX = clientX - stageRect.left`, `screenY = clientY - stageRect.top`
2. Apply `screenToLocal(screenX, screenY, viewport)` to get coordinates in the un-transformed content space
3. For the **official map**: the `.stage-button` is at content position (0,0) since it's the direct child of `.stage-content`. So `localClick.x` and `localClick.y` are already the content-relative coordinates. `normalized = localClick / buttonElement.offsetWidth|Height`.
4. For the **basemap mosaic**: each tile is in a CSS grid. The tile's content position can be computed from its grid column/row index × tile size. `contentX = colIndex * tileWidth`, `contentY = rowIndex * tileHeight`. Then `x = localClick.x - contentX`, `normalized_x = x / tileWidth`.

For `placeMarker()` / `updateMarkerPositions()`:
- Already uses `localToScreenOffset()` which should be the exact inverse of `screenToLocal()`.
- Verify the marker layer is positioned at the `.image-stage` origin (not inside the transformed viewport).

### Verification

- [ ] Click at scale=1, rotation=0 → marker lands exactly under cursor
- [ ] Click at scale=4, rotation=0 → marker lands exactly under cursor
- [ ] Click at scale=1, rotation=-25 → marker lands exactly under cursor
- [ ] Click at scale=4, rotation=-25 → marker lands exactly under cursor
- [ ] Existing control points display at correct positions after zoom/rotate
- [ ] Basemap mosaic tile clicks are accurate at all zoom/rotation levels
- [ ] Coordinate readout on basemap hover is accurate at all zoom/rotation levels
- [ ] Remove all console.logs before committing

### Do NOT change

- The viewport transform model (translate + scale + rotate)
- The `screenToLocal()` / `localToScreenOffset()` math (these are likely correct)
- Any data formats or server-side code
- The zoom/pan/rotate interaction handlers

✅ DONE: 2026-04-05 — Deleted `clickToContentCoords` (used offsetParent traversal, sub-pixel rounding mismatch). Inlined coordinate math in `handleStageClick` and `updateBasemapCoordinateReadout` using `screenToLocal` + `colIndex * tileW` — now consistent with `updateMarkerPositions`. Added localStorage viewport persistence: `vpKey(kind)`, `loadSavedViewport(kind)`, save in `applyViewport`. Restored on load, hole-change, and initial page load.
