# Course Intake Pipeline for Unity Golf

This repository defines an automation-first pipeline for turning publicly available golf course information into a normalized data package that Unity can import as one scene per hole.

The immediate target is `Lomond Country Club`:

- Official course page: <https://www.lomond-cc.com/course/>
- Address: `2570-3 Ryoocho, Kameyama, Mie 519-0222, Japan`

## Goal

Build a repeatable pipeline that:

1. Collects course data from official websites and licensable geospatial sources.
2. Normalizes that data into a single course package.
3. Produces one Unity scene per hole from that package.
4. Preserves provenance, licensing, and review status at every step.

## Important Licensing Constraint

Do not use Google Earth Pro as the canonical production source for shipped geometry, terrain, or textures.

Use Google Earth Pro only for:

- Internal operator review
- Placemarks and rough annotations
- Visual validation
- Optional KMZ import tagged as `reference_only`

For production data, prefer:

- Official club/course website content
- GSI / Geospatial Information Authority of Japan layers and elevation where allowed
- OpenStreetMap vectors
- Human-reviewed annotations

## Repository Layout

```text
app/
  index.html
  styles.css
  app.js
docs/
  architecture.md
  unity-importer.md
  lomond-notes.md
output/
  lomond-country-club/
scripts/
  dev-server.mjs
  ingest-lomond.mjs
schemas/
  course.schema.json
  hole.schema.json
examples/
  lomond-country-club/
    course.json
    provenance.json
    holes/
      01/
        hole.json
        alignment.json
        source/
          official-map.gif
```

## Pipeline Summary

### 1. Source Intake

Collect:

- Course metadata
- Official hole detail images
- Scorecard and tee yardages
- Aerial imagery and DEM from licensable sources
- Optional Earth Pro KMZ as internal reference only

### 2. Normalize

Write all data into a course package with:

- `course.json`
- One `hole.json` per hole
- Optional `layout.geojson`, `centerline.geojson`, `dem.tif`, and review images
- `provenance.json` and attribution records

### 3. Review

Human-in-the-loop verification corrects:

- Hole boundaries
- Tee/green placement
- Hazard classification
- Georeferencing
- Data conflicts between sources

### 4. Unity Import

Unity editor tooling imports the package and generates:

- One scene per hole
- Terrain or mesh
- Surface masks
- Spawn points
- Bounds
- Hazard colliders
- Navigation splines

## First Build Recommendation

Start by building a `Course Intake` desktop app with four tabs:

1. `Sources`
2. `Map Alignment`
3. `Hole Review`
4. `Export`

Details are in [docs/architecture.md](docs/architecture.md) and [docs/unity-importer.md](docs/unity-importer.md).

## Current Prototype

This repo now includes:

- A dependency-light local app shell in [app/index.html](app/index.html)
- A static dev server in [scripts/dev-server.mjs](scripts/dev-server.mjs)
- A Lomond exporter in [scripts/ingest-lomond.mjs](scripts/ingest-lomond.mjs)
- Shared Lomond metadata in [scripts/lib/lomond-data.mjs](scripts/lib/lomond-data.mjs)
- Per-hole alignment workspaces in `output/<course>/holes/<nn>/alignment.json`

## Run It

### No-Terminal Launch

Double-click `Launch Course Intake.vbs` to:

- Start the local server in the background
- Reuse an existing running server if one is already up
- Open the app in your default browser

Double-click `Stop Course Intake.vbs` to stop the background server.

Inside the app, the main operator actions are:

- `Fetch Lomond Live` to pull the official course page and hole reference images
- `Fetch GSI Base Map` to download licensable GSI imagery and DEM tiles for alignment work
- `Initialize Alignment` to ensure each hole has an `alignment.json` workspace for georeferencing
- `Reload Example Package` to refresh the browser view from disk

## Current Alignment Flow

1. Fetch the official Lomond source images.
2. Fetch the GSI base map.
3. Open the `Alignment` panel.
4. Select a hole and a GSI photo tile.
5. Click a point on the official hole map, then click the matching point on the base map tile.
6. Save control points back into `output/<course>/holes/<nn>/alignment.json`.

Status progression:

- `needs_base_map`
- `ready_for_control_points`
- `control_points_started`
- `ready_for_transform`

## Per-Hole Source Layout

Hole-specific source artifacts now live with the hole they belong to.

Example:

```text
output/
  lomond-country-club/
    source/
      course-page.html
      cache.json
    human/
      README.md
      holes/
        01/
          README.md
          official-map.gif
    holes/
      01/
        hole.json
        alignment.json
        source/
          official-map.gif
```

Refreshing Lomond source data preserves existing `alignment.json` work for each hole, including saved control points and selected tiles.

Each `hole.json` now also stores explicit yardage capture for every tee area in `tee_yardages`, with the source id used for extraction.

The `human/` folder is the companion export for people:

- course-level overview in `human/README.md`
- one readable folder per hole in `human/holes/<nn>/`
- copied reference image plus a markdown summary for quick review without opening JSON

### Manual Run

Generate the Lomond package:

```powershell
node scripts/ingest-lomond.mjs
```

Start the local app:

```powershell
node scripts/dev-server.mjs
```

Then open `http://127.0.0.1:4173`.

Notes:

- In this sandbox, live network fetching is blocked, so the exporter falls back to baked official metadata.
- In a normal networked environment, the exporter will also save the fetched course page and any hole detail links it can resolve.
