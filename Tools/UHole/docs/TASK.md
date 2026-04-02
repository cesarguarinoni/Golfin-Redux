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
