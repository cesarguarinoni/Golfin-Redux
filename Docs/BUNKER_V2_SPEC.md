# Bunker V2 — Contour-Based Terrain Replacement

> Spec by Claude (Architect) — 2026-04-07
> Implements the "V2 Direction" from BUNKER_RESEARCH.md

## Flat Terrain Strategy

For V2 development, **Hole 1 uses a flat terrain** (no heightmap). The
splatmap still paints correct zone textures from UHole Lite, but elevation
is zero everywhere. This isolates the contour mesh work from heightmap
complexity and makes it trivial to verify bunker shapes visually.

Once contour meshes look right on flat ground, we re-enable heightmaps.

## Problem Summary

V1 bunker meshes are elliptical bowls derived from bounding boxes, overlaid
on terrain that's been cut with `SetHoles()`. This fails because:

- **SetHoles is too coarse** — 128×128 grid for ~630×520m terrain = ~5m/cell.
  Small bunkers (5–15m) get 1–3 cells, producing blocky misaligned cuts.
- **Bounding-box shapes** don't match actual zone contours (irregular shapes
  become uniform ellipses).
- **Bowl-over-terrain z-fights** or leaves visible terrain poking through.

## V2 Approach: Contour → Mesh → Terrain Replacement

Instead of bounding-box bowls, we:

1. **Extract actual contour polygons** from the zone grid (border tracing)
2. **Simplify** the polygon (Ramer-Douglas-Peucker) to manageable vertex count
3. **Cut terrain with SetHoles** traced from the contour (not bounding box)
4. **Generate a replacement mesh** that fills the hole exactly:
   - Rim vertices sit at terrain surface height (seamless edge)
   - Interior vertices depressed for bunkers / smooth for greens
   - Triangulated to fill the shape
5. **Material per zone** — sand for bunkers, green grass for greens, etc.

This approach works for **any zone type**, not just bunkers. Greens, tee boxes,
water hazards, and cart paths can all become separate meshes with their own
materials and colliders.

---

## Part 1: Export Pipeline Changes (`export-hole.mjs`)

### 1A. Contour Extraction

Add a new function `extractContours(zonesData, terrainMeta, targetZone)`
that returns contour polygons for a given zone type.

**Algorithm: Border Tracing (Moore-Neighbor / marching squares)**

For each connected region of `targetZone` pixels in the zone grid:

1. Find the topmost-leftmost pixel of the region (seed)
2. Walk the border using Moore-neighbor tracing:
   - Start on first border pixel, track direction of entry
   - Follow clockwise around boundary, recording each border pixel
   - Stop when returning to start pixel with same entry direction
3. Convert pixel coords to normalized coords (0–1)
4. Convert normalized coords to local meter coords (same system as anchors)
5. Apply Ramer-Douglas-Peucker simplification (epsilon ~1.5m default)
6. Ensure polygon is wound consistently (CCW when viewed from above)

**Output per region:**

```json
{
  "id": 1,
  "zone": 6,
  "zone_name": "bunker",
  "pixel_count": 346,
  "contour": [
    { "x": 12.5, "z": -120.3 },
    { "x": 15.1, "z": -118.0 },
    ...
  ],
  "center_local": { "x": 12.41, "z": -116.61 },
  "size_m": { "x": 14.87, "z": 33.69 }
}
```

The `contour` array is an ordered polygon (first vertex = last vertex implied
closed). `center_local` and `size_m` remain for backward compat / debug.

### 1B. Updated `bunkers.json` Format

```json
{
  "schema_version": "2.0.0",
  "hole_number": 1,
  "bunker_count": 11,
  "depth_m": 2.0,
  "bunkers": [
    {
      "id": 1,
      "zone": 6,
      "zone_name": "bunker",
      "pixel_count": 346,
      "contour": [ { "x": ..., "z": ... }, ... ],
      "center_local": { "x": 12.41, "z": -116.61 },
      "size_m": { "x": 14.87, "z": 33.69 }
    }
  ]
}
```

V1 fields `center_normalized` and `size_normalized` can be dropped (they
were only used for the bounding-box approach).

### 1C. New `greens.json` (Optional — Phase 2)

Same contour format for green zones. Not in initial scope, but the
architecture should make it trivial to add later.

### 1D. Implementation Notes

- **Minimum pixel count**: Keep `MIN_PIXELS = 8` threshold to filter noise
- **Multi-region zones**: A single hole can have multiple disconnected bunker
  regions — each gets its own contour entry (this already works in V1)
- **Coordinate system**: Contour coords use the same local meter system as
  anchors (`(normX - 0.5) * terrain_width_m`, etc.)
- **RDP epsilon**: Start at 1.5m. Too low = too many vertices, too high =
  bunkers become blobs. Make it a parameter in the export.

---

## Part 2: Unity Importer Changes (`HoleLiteImporter.cs`)

### 2A. New `CreateZoneMeshes()` Replaces `CreateBunkers()`

Rename and generalize. The new method:

1. Reads `bunkers.json` (V2 format with contours)
2. For each bunker region:
   a. **Apply 90° CCW rotation** to contour vertices (same transform as
      anchors: `worldX = vertex.z, worldZ = vertex.x`)
   b. **Cut terrain** by tracing the contour in the holes grid
   c. **Generate replacement mesh** with:
      - Rim vertices at terrain surface height
      - Interior vertices depressed by bowl depth
      - Proper triangulation
   d. **Apply sand material** + mesh collider

### 2B. Contour-Based Terrain Cutting

Instead of axis-aligned bounding box in the holes grid, trace the actual
contour polygon:

```csharp
// For each cell in the holes grid, test if it's inside the contour polygon
for (int hz = hMinZ; hz <= hMaxZ; hz++)
{
    for (int hx = hMinX; hx <= hMaxX; hx++)
    {
        // Convert hole grid cell to world position
        float cellWorldX = (hx + 0.5f) / holesRes * terrainSize.x + terrainPos.x;
        float cellWorldZ = (hz + 0.5f) / holesRes * terrainSize.z + terrainPos.z;

        // Point-in-polygon test against contour
        if (IsInsideContour(cellWorldX, cellWorldZ, worldContour))
            holes[hz, hx] = false;
    }
}
```

Use a **margin** of ~1–2 cells inward from the actual contour so the mesh
rim slightly overlaps terrain at the edge (prevents gaps). The rim vertices
match terrain height, so the overlap is invisible.

### 2C. Replacement Mesh Generation

The mesh fills the exact contour shape:

**Vertex layout:**

1. **Rim ring** — contour vertices at terrain surface height + small offset
   (0.02m above surface). These create the seamless edge.
2. **Inner ring** — same contour vertices scaled inward ~80%, at surface
   height. Transition zone from rim to bowl.
3. **Bowl ring** — contour vertices scaled inward ~50%, depressed by
   `depth * 0.5`.
4. **Bowl bottom ring** — contour vertices scaled inward ~20%, depressed
   by `depth * 0.9`.
5. **Center vertex** — centroid of contour, depressed by full `depth`.

**Scaling inward:** For each contour vertex, interpolate toward the centroid:

```csharp
Vector3 ScaleTowardCenter(Vector3 vertex, Vector3 center, float scale)
{
    return center + (vertex - center) * scale;
}
```

**Triangulation between rings:** Connect adjacent rings with triangle strips
(same as V1 but using N contour vertices instead of uniform circle segments).

**Bottom fan:** Triangles from the last ring to the center vertex.

**UV mapping:** Project from above — `u = (x - minX) / (maxX - minX)`,
`v = (z - minZ) / (maxZ - minZ)`.

### 2D. Terrain Height Sampling for Rim

Each rim vertex samples the actual terrain height at its world position:

```csharp
float terrainH = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));
float rimY = (terrainBaseY + terrainH) - meshOriginY + 0.02f;
```

This ensures the mesh edge follows terrain undulation — no gaps, no
floating edges. Same concept as V1's terrain-following lip, but applied
to the actual contour shape.

> **Note:** During flat-terrain development, `SampleHeight()` returns 0
> everywhere, so rim vertices all sit at y=0.02. This is expected and
> correct — the height sampling becomes meaningful when heightmaps are
> re-enabled.

### 2E. Point-in-Polygon Utility

Standard ray-casting algorithm:

```csharp
static bool IsInsideContour(float px, float pz, Vector2[] contour)
{
    bool inside = false;
    for (int i = 0, j = contour.Length - 1; i < contour.Length; j = i++)
    {
        if ((contour[i].y > pz) != (contour[j].y > pz) &&
            px < (contour[j].x - contour[i].x) * (pz - contour[i].y)
                 / (contour[j].y - contour[i].y) + contour[i].x)
        {
            inside = !inside;
        }
    }
    return inside;
}
```

### 2F. Data Classes Update

Add contour support to the JSON deserialization:

```csharp
[System.Serializable]
public class BunkerContourVertex
{
    public float x;
    public float z;
}

[System.Serializable]
public class BunkerData
{
    public int id;
    public int zone;
    public string zone_name;
    public int pixel_count;
    public BunkerContourVertex[] contour;
    public LocalCoord center_local;
    public SizeData size_m;
}

[System.Serializable]
public class BunkersFileData
{
    public string schema_version;
    public int hole_number;
    public int bunker_count;
    public float depth_m;
    public BunkerData[] bunkers;
}
```

### 2G. Fallback for V1 Data

If `bunkers.json` has no `contour` field (or `schema_version` is missing /
"1.0"), fall back to the V1 bounding-box bowl approach. This keeps existing
exported holes working until they're re-exported.

---

## Part 3: Implementation Plan

### Task 0 — Flat Terrain Mode

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

Modify `CreateTerrain()` to skip heightmap loading and produce flat terrain:

```csharp
// Replace heightmap loading with flat terrain:
float[,] heights = new float[res, res];
// All values default to 0.0 — perfectly flat
```

- Keep `terrainData.size` as-is (correct XZ dimensions, elevRange can be
  a small nonzero like 1.0 to avoid division-by-zero edge cases)
- Splatmap pipeline is unchanged — zones still paint correctly on flat ground
- Anchors/camera still work (terrain height = 0 everywhere)

This is a **temporary dev change**, not a permanent feature flag. We'll
revert to heightmap loading once contour meshes are verified.

### Task 1 — Export Pipeline: Contour Extraction (UHole Lite side)

**File:** `Tools/UHoleLite/scripts/export-hole.mjs`

1. Add `traceContour(grid, w, h, startX, startY, visited)` — Moore-neighbor
   border tracing returning ordered pixel coordinates
2. Add `simplifyPolygon(points, epsilon)` — Ramer-Douglas-Peucker
3. Add `ensureCCW(polygon)` — ensure consistent winding
4. Update `extractBunkers()` to call contour tracing instead of just
   computing bounding boxes
5. Add `schema_version: "2.0.0"` to bunkers output
6. Run `node scripts/export-hole.mjs lomond-country-club --all` and verify
   contour data appears in all 18 `bunkers.json` files

**Handoff:** TASK.md for Claude Code (UHole Lite side)

### Task 2 — Unity Importer: Contour Mesh Generator

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

1. Add `IsInsideContour()` utility
2. Replace `CreateBunkers()` with `CreateZoneMeshes()`:
   - Parse V2 contour data
   - Contour-traced `SetHoles` (not bounding box)
   - Contour-shaped mesh with rim/bowl/center vertex rings
   - Terrain height sampling on rim
3. Add V1 fallback check (`schema_version`)
4. Remove V1 `CreateBowlMesh()` (keep for fallback or delete)

**Handoff:** TellCode.md for Claude Code (Unity side)

### Task 3 — Verify & Tune (Flat Terrain)

1. Re-export hole 01: `node scripts/export-hole.mjs lomond-country-club 1`
2. Re-import in Unity: `GOLFIN > Import Hole (Lite) > Hole 01`
3. Walk around each bunker on the flat terrain — check:
   - [ ] Contour shape matches zone painting
   - [ ] No terrain visible inside bunker
   - [ ] Rim edge is flush with flat surface
   - [ ] No z-fighting
   - [ ] Bowl depth looks reasonable (~2m below flat surface)
   - [ ] Sand texture tiles properly
   - [ ] Splatmap zones paint correctly on flat ground
4. Spot-check 2–3 other holes
5. Check small bunkers near greens specifically (the V1 failure case)

### Task 4 — Re-enable Heightmap (after Task 3 passes)

Revert `CreateTerrain()` to load heightmap.raw again. Verify contour
meshes still work with real elevation data. Rim vertices should follow
terrain slope via `SampleHeight()`.

---

## Open Questions

1. **RDP epsilon value** — 1.5m is a guess. Might need to be relative to
   bunker size (e.g., `max(1.0, perimeter * 0.02)`). Will tune after
   seeing first results.

2. **Greens as meshes** — The same pipeline could create green replacement
   meshes with proper putting surface colliders. Defer to Phase 2 but
   architecture supports it.

3. **Water hazards** — Could use the same approach with a flat mesh + water
   shader. Defer.

4. **Bunker lip shape** — Real bunkers have raised lips on some edges
   (especially face/front). V2 starts with uniform rim at terrain height.
   Could add directional lip variation later by raising rim vertices on
   the "face" side.

5. **Collider detail** — MeshCollider on the bowl mesh is fine for now.
   For gameplay we may want to tag bunker colliders with a `SurfaceType`
   component so the ball physics system knows it's in sand.

---

## Files Affected

| File | Change |
|------|--------|
| `Tools/UHoleLite/scripts/export-hole.mjs` | Add contour extraction, RDP, winding |
| `Tools/UHoleLite/output/*/export/*/bunkers.json` | V2 schema with contour arrays |
| `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` | Replace CreateBunkers → CreateZoneMeshes |
| `Docs/BUNKER_RESEARCH.md` | Update V2 status section |
| `Docs/TellCode.md` | Task 2 handoff |
| `Tools/UHoleLite/docs/TASK.md` | Task 1 handoff |

---

## Success Criteria

- Bunker meshes follow actual zone contours (not bounding boxes)
- Terrain is cleanly cut along contour boundary
- Rim edges are seamless — no gaps, no floating edges
- Small green-adjacent bunkers render correctly (the V1 failure case)
- All 18 holes import without errors
- No z-fighting or terrain bleed-through
- Sand texture tiles properly on irregularly shaped meshes
