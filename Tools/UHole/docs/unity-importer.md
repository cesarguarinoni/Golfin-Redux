# Unity Importer Contract

## Goal

The Unity side should be a deterministic importer. It should not scrape websites or fetch geodata directly. It should only consume a reviewed course package exported by `Course Intake`.

## Import Boundary

### Input

A folder with:

- `course.json`
- `provenance.json`
- `holes/<nn>/hole.json`
- Optional reviewed assets such as:
  - `layout.geojson`
  - `centerline.geojson`
  - `hazards.geojson`
  - `dem.tif`
  - `heightmap.png`
  - reference images

### Output

For each hole:

- One Unity scene named `Hole_<nn>`
- Terrain or mesh
- Tagged surfaces
- Tee markers and pin placements
- Hazard colliders
- Camera anchors
- Gameplay bounds

## Recommended Unity Structure

```text
Assets/
  Golf/
    Courses/
      lomond-country-club/
        Data/
        Generated/
          Hole_01.unity
          Hole_02.unity
```

## Import Steps

### 1. Read Course Manifest

Load `course.json` and validate against the schema.

Abort import if:

- The package is not marked `reviewed` or `production_candidate`
- Required attribution is missing
- Hole records are absent

### 2. Read Hole Payload

For each `hole.json`:

- Parse yardages and par
- Read reviewed geometry references
- Read hole-local origin and orientation

### 3. Generate Terrain

If `dem.tif` or `heightmap.png` is present:

- Convert to Unity terrain or terrain mesh
- Scale in meters
- Apply vertical exaggeration factor from metadata if any

If no terrain raster is present:

- Build a flat placeholder terrain
- Mark hole status as incomplete in importer logs

### 4. Generate Gameplay Surfaces

From reviewed polygons:

- `tee`
- `fairway`
- `green`
- `bunker`
- `water`
- `rough`
- `trees`
- `out_of_bounds`

Map each class to:

- Physics material
- Shot behavior surface id
- Rendering layer or terrain texture

### 5. Place Anchors

Create transforms for:

- Tee spawn points
- Green center
- Pin candidates
- Flyover spline nodes
- Camera targets

### 6. Create Scene

Scene naming:

- `Hole_01`
- `Hole_02`
- `...`
- `Hole_18`

Each scene should contain:

- `HoleMetadata` component
- `TerrainRoot`
- `GameplaySurfaces`
- `Hazards`
- `Anchors`
- `DebugReferences`

## Unity Metadata Component

Suggested fields:

```csharp
public class HoleMetadata : MonoBehaviour
{
    public string courseId;
    public int holeNumber;
    public int par;
    public int strokeIndex;
    public int championshipYards;
    public float centerlineLengthMeters;
    public string packageVersion;
    public string reviewStatus;
}
```

## Scene Generation Rules

- One hole equals one scene.
- Every generated object should be under a stable root for re-import safety.
- Generated content must be replaceable without touching hand-authored dressing objects.
- Keep geometry generation and art dressing separate.

## Recommended Split Between Data and Art

Generated from intake data:

- Terrain base
- Surface polygons
- Hazard extents
- Hole anchors
- Bounds and splines

Authored in Unity:

- Trees and vegetation dressing
- Props
- Clubhouse and course furniture
- FX
- Lighting polish

## Reimport Safety

Use a stable import key based on:

- `course_id`
- `hole_number`
- `package_version`

The importer should update generated children only, leaving artist-authored nodes untouched.
