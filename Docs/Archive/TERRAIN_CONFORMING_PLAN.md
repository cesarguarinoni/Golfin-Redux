# Terrain-Conforming Overlays Plan (v2 — Post-Code-Review)

## Findings

### Execution Order: Already Correct ✅

```
1. CreateTerrain()              — heightmap imported (with DEM relief)
2. ApplySplatmap()              — terrain textures
3. CreateZoneMeshes()           — bunker bowls + terrain holes
4. CreateGreenMeshes()          — raised green meshes
5. CreateWaterMeshes()          — flat water CDT
6. CreateFlatZoneMeshes()       — fairway CDT, tee CDT, fringe ring,
                                   tee border, cart path spine strip
7. DepressTerrainUnderOverlays() — drops terrain under overlays
8. SetHoles()                   — applies bunker terrain holes
```

Overlay meshes (step 6) are built AFTER heightmap import (step 1)
and BEFORE depression (step 7). This means:

- **CDTTriangulate** already calls `terrain.SampleHeight()` per vertex
  including Steiner interior points. With DEM relief in the heightmap,
  CDT vertices will follow the varied terrain automatically. ✅
- **CreateSpineStripMesh** (cart paths) already samples terrain height
  per vertex (left and right of spine). ✅
- **CreateRaisedMesh** (greens) samples terrain per vertex and adds
  green height. Not affected by terrain relief — intentionally raised. ✅
- **CreateContourMesh** (bunkers) rim vertices sample terrain height.
  Bowl interior is relative to surface. ✅
- **CreateWaterMeshes** — flat at min terrain height. Unaffected. ✅

### Depression Ordering: Correct ✅

Depression runs AFTER overlays, pushing terrain 0.4m below the overlay
surface. Overlays are positioned at pre-depression terrain + tiny
offset (0.01–0.02m). After depression, the terrain sits 0.4m below,
hidden under the overlay. This is the correct architecture.

## Fringe Ring Issue: Triangle Overlap on Slopes

### Root Cause

`CreateFringeRing` builds the fringe's edge ring (inner edge) from
the same contour points as the fairway CDT's boundary vertices. Both
sample `terrain.SampleHeight()` at the same XZ → same terrain Y.

The 2mm Y offset difference (fairway=0.01m, fringe=0.012m) should
keep fringe above fairway. But the problem is **triangle geometry**:

The fairway CDT has triangles at the contour boundary that connect
contour vertices to interior Steiner points. On a slope, a CDT
triangle face may extend above 0.012m at the contour edge because
its interior Steiner point is at a higher terrain elevation, and
the triangle face interpolates linearly across the slope. The fringe
ring is a thin strip with both edges at 0.012m (terrain-following),
but the fairway CDT triangle face cuts through it where the slope
lifts the interior vertex.

```
   Fairway CDT triangle face (sloped)
     /|          ← interior Steiner vertex at higher terrain
    / |          ← triangle face rises above fringe edge
   /  |
  ·---·─────── fringe ring edge at contour (0.012m)
  ← fairway contour vertex (0.01m)
```

### Fix: Increase Fringe Y Offset on Slopes

**Option A (simple):** Bump fringe yOffset from 0.012m to 0.015m.
This gives 5mm clearance above the fairway instead of 2mm. On steep
slopes, the CDT triangle face still rises, but 5mm buys more room.

**Option B (robust):** After building both the fairway CDT and fringe
ring, for each fringe inner-edge vertex, find the fairway CDT triangle
that contains its XZ position, compute the barycentric Y on that
triangle, and set the fringe vertex Y = max(fringe_Y, fairway_tri_Y
+ 0.002m). This guarantees the fringe never sits below the fairway
triangle face.

**Option C (simplest, recommended):** Increase the fairway CDT's
contour-edge offset so it slopes DOWN at the boundary. Currently all
CDT vertices use yOffset=0.01m uniformly. If we detect boundary vs
interior vertices and set boundary vertices to yOffset=0.005m while
interior stays at 0.01m, the CDT surface dips at the contour edge,
creating headroom for the fringe ring at 0.012m even on slopes.

### Recommendation

Start with **Option A** (bump fringe to 0.015m). If it's still
visible on steep slopes, implement Option C. Option B is correct but
complex.

### Same Issue for Tee Border Ring

`CreateGradientBorderRing` uses yOffset=0.008m (below tee CDT at
0.01m on purpose — border sits under the tee). The inner edge is
pulled inward by 0.15m to overlap under the tee mesh. This works
when flat, but on slopes the tee CDT face may rise above the border
inner edge. Same fix: ensure the overlap is wide enough to cover the
worst-case slope.

The tee is small (typically <20m across) so slopes within a single
tee are minimal. Lower risk than fairway.

## Action Plan

### Step 1: Run TERRAIN_RELIEF and Test (no Unity changes)

Generate terrain for Holes 1 and 4 with the new per-zone residual
blending. Import in Unity. Walk around and observe:

- Does the fairway visibly follow the slope? (Hole 4 especially)
- Are there terrain poke-throughs on any overlay?
- Does the fringe ring have visible issues?

If overlays look fine with no visible artifacts, we're done — the
existing code already handles terrain relief correctly.

### Step 2: Fix Fringe Ring If Needed

If fringe ring artifacts are visible:

In `CreateFringeRing`, change:
```csharp
float yOffset = 0.012f; // slightly above fairway (0.01)
```
To:
```csharp
float yOffset = 0.015f; // 5mm above fairway for slope clearance
```

### Step 3: Increase Cart Path Depression Margin If Needed

Cart paths are narrow → most sensitive to terrain variation. If
terrain pokes through cart path mesh edges:

In `CreateFlatZoneMeshes` where `BuildSpinePolygon` is called for
cart path depression, increase the margin from 0.30m to 0.50m:
```csharp
float halfWidth = (cp.width_m > 0 ? cp.width_m : 2.5f) / 2f + 0.50f;
```

### Do NOT Change

- Execution order (already correct)
- Green/bunker/water mesh creation
- Depression logic (DepressTerrainUnderOverlays)
- CDTTriangulate (already samples terrain correctly)
- CreateSpineStripMesh (already samples terrain correctly)
