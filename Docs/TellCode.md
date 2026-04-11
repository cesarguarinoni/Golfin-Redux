# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Fix Overlay Mesh Terrain Draping

**Problem:** Overlay meshes (fairway, tee, cart path, green collar) appear
to sit at a fixed Y-plane instead of following the terrain slope. They
sink into the terrain on one side and float above it on the other.
The previous fix (increasing yOffset to 8cm) made them float everywhere.

**Root cause analysis needed:** The subdivision is in place, yOffset is
back to 0.02f. The issue is likely one of:

A) The `terrain.SampleHeight()` call isn't returning correct values
   because the TerrainData hasn't been fully committed yet (heights
   set but not flushed)
B) The mesh centroid Y is computed as the average of all vertex Y values,
   but on a slope, this average doesn't match any actual terrain point —
   and since vertices are stored relative to centroid, the whole mesh
   shifts
C) The 90° CCW coordinate transform has a subtle error for some holes

**Debugging step — add diagnostic logging:**

In `CreateFairwayMesh`, right after computing worldPts and centroid,
add this log:
```csharp
// DEBUG: Compare sampled height vs terrain height at centroid
float debugTerrainH = terrain.SampleHeight(new Vector3(cx, 0, cz));
float debugExpectedY = terrainBaseY + debugTerrainH + yOffset;
Debug.Log($"[FairwayDebug] Fairway {id}: centroidY={cy:F3}, " +
    $"terrainAtCentroid={debugExpectedY:F3}, diff={cy - debugExpectedY:F3}, " +
    $"terrainBaseY={terrainBaseY:F3}, sampleH={debugTerrainH:F3}");

// DEBUG: Check first 3 vertices
for (int dbg = 0; dbg < Mathf.Min(3, n); dbg++)
{
    float vTerrainH = terrain.SampleHeight(new Vector3(worldPts[dbg].x, 0, worldPts[dbg].z));
    float vExpectedY = terrainBaseY + vTerrainH + yOffset;
    Debug.Log($"[FairwayDebug]   v[{dbg}]: worldY={worldPts[dbg].y:F3}, " +
        $"expectedY={vExpectedY:F3}, diff={worldPts[dbg].y - vExpectedY:F3}");
}
```

Do the same in `CreateRaisedMesh` (green collar) for the outer rim
vertices (ring 0):
```csharp
// DEBUG: After collar vertex computation, log first 3 outer rim verts
if (collarMat != null)
{
    for (int dbg = 0; dbg < Mathf.Min(3, n); dbg++)
    {
        float scale0 = collarScales[0];
        float wx0 = centroidX + (contour[dbg].x - centroidX) * scale0;
        float wz0 = centroidZ + (contour[dbg].y - centroidZ) * scale0;
        float th0 = terrain.SampleHeight(new Vector3(wx0, 0, wz0));
        Debug.Log($"[GreenDebug] Green {id} collar v[{dbg}]: " +
            $"terrainH={th0:F3}, surfaceY={surfaceY:F3}, " +
            $"vertY={collarVerts[dbg].y:F3}, terrainBaseY={terrainBaseY:F3}");
    }
}
```

**Also revert yOffset back to 0.02f** in all methods (undo the 0.08 change):
- `CreateFlatContourMesh`: 0.02f
- `CreateFairwayMesh`: 0.02f  
- `CreateEarClipContourMesh`: 0.02f
- `CreateSpineStripMesh`: 0.02f
- `CreateFringeRing`: 0.03f
- `CreateGradientBorderRing`: 0.01f

**After importing a hilly hole (e.g. Hole 3 or 4)**, paste the debug
output here so we can see what's actually happening with the heights.

### Debug Results (Hole 4)

**Fairway:** Individual vertex heights match terrain perfectly (diff=0.000).
But centroidY=19.718 vs terrainAtCentroid=19.772 → diff=-0.055m.
**Root cause confirmed: hypothesis B.** The centroid Y is computed as the
average of all vertex Y values. On a slope, this average is lower than
the actual terrain height at the centroid's XZ position. Since vertices
are stored relative to centroid (`worldPt - centroid`), the whole mesh
shifts down by ~5.5cm on this slope.

**Fix:** In the centroid computation, don't average Y. Instead sample
terrain height at the averaged XZ: `cy = terrainBaseY + terrain.SampleHeight(cx, cz) + yOffset`.
This anchors the mesh's origin to the actual terrain surface.

**Green collar:** Working correctly. vertY values are relative to surfaceY
and show the expected ramp from green surface down to terrain level.

### Do NOT
- Change the heightmap smoothing code
- Change materials or textures
- Remove the subdivision calls

---

## Previous Task (DONE) — Increase Overlay Mesh Y-Offset (REVERTED)

### Problem 1: Bumpy terrain outside play area
The DEM heightmap (~8m/px at z14) upsampled from 1025→2049 creates
stair-step facets. These are very visible in rough/OB/tree areas.

### Problem 2: Hard step at play/non-play boundary
The transition between play zones and surrounding terrain has a visible
cliff/step where the DEM resolution isn't sufficient.

### Problem 3: Overlay meshes half-sunken into terrain
Fairway and tee meshes only sample terrain height at contour vertices.
Between vertices the mesh interpolates linearly while the terrain curves,
causing the mesh to clip below the terrain surface on hilly holes.

---

### Part 1 — Heightmap Smoothing (in `CreateTerrain`)

After the heightmap is loaded (the `heights[,]` array is populated),
before `terrainData.SetHeights()`, add a smoothing pass:

**Step 1: Build a play-area mask on the heightmap grid**

Load `zones.json` from the export folder (same as splatmap does).
For each heightmap cell `(hx, hz)` in `[0..actualRes)`, map it back to
the zone grid using the same 90° CCW rotation as `ApplySplatmap`:
```
float normX = (float)hx / (actualRes - 1); // terrain X fraction
float normZ = (float)hz / (actualRes - 1); // terrain Z fraction
// Reverse 90° CCW: zone.x = normZ * (zoneW-1), zone.y = normX * (zoneH-1)
int gx = Clamp(Round(normZ * (zoneW - 1)), 0, zoneW - 1);
int gy = Clamp(Round(normX * (zoneH - 1)), 0, zoneH - 1);
int zone = grid[gy * zoneW + gx];
```

Play zones (used for slope calculation — current heights are fine):
**1** (fairway), **2** (green), **6** (bunker),
**7** (water), **8** (cart_path), **10** (tee_box).

Create `bool[] isPlayArea = new bool[actualRes * actualRes]`.
Set `true` for cells matching any play zone.

Note: Play area heights don't need to be exact — they drive slope
calculation, not literal real-world elevation. But we still use them
as the anchor for the transition blend so the boundary is seamless.

**Step 2: Build a blend mask with transition band**

Dilate the play-area mask by `TransitionCells` (const = 40 cells,
~12m at 2049 res over ~600m terrain). Use a distance transform:

```csharp
float[] distToPlay = new float[actualRes * actualRes];
// Initialize: 0 for play cells, float.MaxValue for others
// Forward + backward chamfer pass (same approach as shore slope)
```

Then compute blend factor:
```csharp
float[] blendFactor = new float[actualRes * actualRes];
for each cell:
  if (isPlayArea) blendFactor = 1.0  // keep raw DEM
  else if (dist < TransitionCells)
    blendFactor = dist / TransitionCells  // 0→1 ramp
  else blendFactor = 0.0  // fully smoothed
```

**Step 3: Smooth the heightmap**

Apply a Gaussian blur to the full `heights[,]` array → `smoothedHeights[,]`.
Use the existing `GaussianBlur2D` helper (it operates on `float[,]`).
Parameters: **radius = 8, sigma = 4.0f**.

Note: `GaussianBlur2D` is a private method in `HoleLiteImporter` —
make it `internal` or just call it directly since `CreateTerrain` is
in the same class.

Extract a 2D slice from `heights` → blur it → blend:
```csharp
float[,] smoothed = GaussianBlur2D(heights, actualRes, 8, 4.0f);
for (int z = 0; z < actualRes; z++)
  for (int x = 0; x < actualRes; x++)
  {
      float b = blendFactor[z * actualRes + x];
      heights[z, x] = Mathf.Lerp(smoothed[z, x], heights[z, x], b);
  }
```

This keeps play-area heights as-is (they're already fine for slope
calculation), smoothly transitions at the boundary (no visible step),
and fully smooths outside terrain to remove DEM stair-stepping.

If `zones.json` doesn't exist (no zone data), skip smoothing entirely
(flat terrain fallback already handles this).

**Constants to add at class level:**
```csharp
private const int SmoothRadius = 8;
private const float SmoothSigma = 4.0f;
private const int TransitionCells = 40;
private static readonly HashSet<int> PlayZones =
    new HashSet<int> { 1, 2, 6, 7, 8, 10 };
```

---

### Part 2 — Overlay Mesh Terrain Conformance

**Problem:** `CreateFairwayMesh` and `CreateFlatContourMesh` (tees)
don't subdivide their meshes, so large triangles float above or sink
below the terrain between vertices.

**Fix:** After ear-clip/fan triangulation, call `SubdivideToTerrain`
(already exists and is used by `CreateEarClipContourMesh` for cart paths).

**In `CreateFairwayMesh`**, after ear-clip and before creating the Mesh:
```csharp
var vertList = new System.Collections.Generic.List<Vector3>(verts);
var uvList = new System.Collections.Generic.List<Vector2>(uvs);
var triList = new System.Collections.Generic.List<int>(tris);
SubdivideToTerrain(ref vertList, ref uvList, ref triList,
    centroid, terrain, terrainBaseY, stripeWidth, yOffset, 2.0f);
// Then use vertList/uvList/triList for the Mesh
```

Note: `SubdivideToTerrain` needs a `tileSize` param for UV computation.
For fairway, pass `stripeWidth` (the UV tiling parameter). The subdivision
computes new UVs as `worldPos / tileSize` — but fairway UVs use the
oriented stripe projection (`stripeDir`/`parallelDir`). So we need a
small tweak:

**Add a UV callback overload to `SubdivideToTerrain`**, OR just
recompute UVs for new vertices using the stripe formula:

Simplest approach — after subdivision, recompute ALL UVs using the
stripe orientation:
```csharp
for (int i = 0; i < vertList.Count; i++)
{
    Vector3 wp = vertList[i] + centroid;
    uvList[i] = new Vector2(
        (wp.x * stripeDir.x + wp.z * stripeDir.y) / stripeWidth,
        (wp.x * parallelDir.x + wp.z * parallelDir.y) / stripeWidth);
}
```

**In `CreateFlatContourMesh`** (tees), after fan triangulation:
```csharp
// Convert fan triangulation result to lists
var vertList = new System.Collections.Generic.List<Vector3>(verts);
var uvList = new System.Collections.Generic.List<Vector2>(uvs);
var triList = new System.Collections.Generic.List<int>(tris);
SubdivideToTerrain(ref vertList, ref uvList, ref triList,
    centroid, terrain, terrainBaseY, tileSize, yOffset, 2.0f);
// Then use vertList/uvList/triList for the Mesh
```

### Verification

1. Re-import any hole with elevation changes (try Hole 2, 3, or 4)
2. **Terrain outside play area:** Smooth, no stair-stepping
3. **Play/non-play boundary:** Gradual transition, no cliff/step
4. **Fairway/tee meshes:** Conform to terrain surface, no sinking
5. **Cart paths:** Should already work (already has subdivision)
6. **Greens/bunkers:** Unaffected (they use raised/bowl meshes)

### Do NOT

- Change play-zone heights in a way that breaks slope calculation
- Change zone grid resolution or zone classification
- Modify bunker, green, or water mesh code
- Change the heightmap resolution (2049)

---

## Previous Task (DONE) — Tree Placement System (v2: Mixed Mode)

**Goal:** Place trees on the terrain using zone 5 (trees) mask from
UHole Lite. Support TWO placement modes based on prefab type:

1. **Terrain trees** — prefabs WITH LODGroup → use `terrainData.treePrototypes`
   (automatic billboard LOD, batching)
2. **Standalone trees** — prefabs WITHOUT LODGroup → instantiate as
   GameObjects, sample terrain height, parent under a container

The placer auto-detects which mode to use per prefab.

**Prerequisite:** `tree-zones.json` must exist in the export folder.

### Part 1 — Data Classes

In `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs`, add
(if not already present):

```csharp
[System.Serializable]
public class TreeZonesFile
{
    public string schema_version;
    public int hole_number;
    public int mask_width;
    public int mask_height;
    public string mask_base64;
    public TreeMPP meters_per_pixel;
    public int tree_region_count;
    public TreeRegionData[] tree_regions;
}

[System.Serializable]
public class TreeMPP { public float x; public float z; }

[System.Serializable]
public class TreeRegionData
{
    public int id;
    public int pixel_count;
    public float area_m2;
    public ContourPoint[] contour;
    public AnchorLocal center_local;
    public BunkerSize size_m;
}
```

Also add to manifest: `public string tree_zones_file;`

### Part 2 — TreePlacer.cs

Create `Assets/Scripts/Editor/CourseImporter/TreePlacer.cs`

```csharp
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Golfin.Editor.CourseImporter
{
    public static class TreePlacer
    {
        // All tree prefab paths + weights.
        // Trees marked as standalone are placed as GameObjects
        // (preserves particle systems, complex hierarchies).
        // Others use the terrain tree system (billboard LOD, batching).
        private static readonly string[] TreePrefabPaths = new string[]
        {
            // Trees(2025) — terrain tree system
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_01Cedar.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_JapaneseBlack_01_Var1.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_JapaneseBlack_01.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Mesh_Metasequoia.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_ScottishPine_01.prefab",
            // Realistic Tree (in Objects/) — standalone (has particles, LOD on child)
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Objects/Spruce/Spruce 1.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Objects/Spruce/Spruce 3.prefab",
        };

        private static readonly float[] TreeWeights =
            { 3f, 3f, 0.5f, 2f, 2f, 1.5f, 1f };

        // Indices into TreePrefabPaths that MUST be standalone GameObjects
        // (terrain tree system strips particle systems and complex hierarchies)
        private static readonly HashSet<int> ForceStandaloneIndices =
            new HashSet<int> { 5, 6 };

        private const float MinSpacing = 6f;
        private const float ScaleMin = 0.85f;
        private const float ScaleMax = 1.15f;

        private static readonly HashSet<int> ExcludeZones = new HashSet<int>
            { 1, 2, 6, 7, 8, 10 };

        public static void PlaceTrees(
            Terrain terrain, float terrainBaseY,
            string exportPath, string zonesJsonPath,
            Transform parentRoot)
        {
            string tzPath = Path.Combine(exportPath, "tree-zones.json");
            if (!File.Exists(tzPath))
            {
                Debug.Log("[TreePlacer] No tree-zones.json found, skipping");
                return;
            }

            var tzData = JsonUtility.FromJson<TreeZonesFile>(
                File.ReadAllText(tzPath));
            if (tzData.tree_region_count == 0 ||
                string.IsNullOrEmpty(tzData.mask_base64))
            {
                Debug.Log("[TreePlacer] No tree zones painted, skipping");
                return;
            }

            byte[] mask = System.Convert.FromBase64String(tzData.mask_base64);
            int maskW = tzData.mask_width;
            int maskH = tzData.mask_height;

            byte[] zoneGrid = null;
            int zoneW = 0, zoneH = 0;
            if (File.Exists(zonesJsonPath))
            {
                var zData = JsonUtility.FromJson<ZonesData>(
                    File.ReadAllText(zonesJsonPath));
                zoneGrid = System.Convert.FromBase64String(zData.grid);
                zoneW = zData.source_dimensions.width;
                zoneH = zData.source_dimensions.height;
            }

            var terrainData = terrain.terrainData;
            float tWidth = terrainData.size.x;
            float tLength = terrainData.size.z;

            // ---- Load prefabs, split by LODGroup presence ----
            var terrainProtos = new List<TreePrototype>();
            var terrainProtoIndices = new List<int>(); // index into TreePrefabPaths

            var standaloneGOs = new List<GameObject>();
            var standaloneIndices = new List<int>();

            var allPrefabs = new GameObject[TreePrefabPaths.Length];

            for (int i = 0; i < TreePrefabPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TreePrefabPaths[i]);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[TreePlacer] Not found: {TreePrefabPaths[i]}");
                    continue;
                }
                allPrefabs[i] = prefab;

                bool hasLOD = prefab.GetComponent<LODGroup>() != null ||
                    prefab.GetComponentInChildren<LODGroup>() != null;
                bool forceStandalone = ForceStandaloneIndices.Contains(i);

                if (hasLOD && !forceStandalone)
                {
                    terrainProtoIndices.Add(i);
                    terrainProtos.Add(new TreePrototype { prefab = prefab });
                }
                else
                {
                    standaloneIndices.Add(i);
                    standaloneGOs.Add(prefab);
                }
            }

            // Register terrain tree prototypes
            if (terrainProtos.Count > 0)
                terrainData.treePrototypes = terrainProtos.ToArray();

            // Build combined weight array (all prefabs)
            float totalWeight = 0;
            var cumulativeWeights = new float[TreePrefabPaths.Length];
            for (int i = 0; i < TreePrefabPaths.Length; i++)
            {
                if (allPrefabs[i] == null) continue;
                float w = i < TreeWeights.Length ? TreeWeights[i] : 1f;
                totalWeight += w;
                cumulativeWeights[i] = totalWeight;
            }

            // ---- Poisson disk sampling ----
            var terrainTrees = new List<TreeInstance>();
            var rng = new System.Random(42);
            float cellSize = MinSpacing;
            int cellsX = Mathf.FloorToInt(tWidth / cellSize);
            int cellsZ = Mathf.FloorToInt(tLength / cellSize);

            // Container for standalone trees
            var standaloneRoot = new GameObject("Trees_Standalone");
            standaloneRoot.transform.SetParent(parentRoot);
            int standaloneCount = 0;

            for (int cz = 0; cz < cellsZ; cz++)
            {
                for (int cx = 0; cx < cellsX; cx++)
                {
                    float worldX = (cx + (float)rng.NextDouble()) * cellSize;
                    float worldZ = (cz + (float)rng.NextDouble()) * cellSize;
                    float nx = worldX / tWidth;
                    float nz = worldZ / tLength;
                    if (nx < 0 || nx >= 1 || nz < 0 || nz >= 1) continue;

                    // Check mask
                    int mx = Mathf.Clamp(Mathf.FloorToInt(nx * maskW), 0, maskW - 1);
                    int my = Mathf.Clamp(Mathf.FloorToInt(nz * maskH), 0, maskH - 1);
                    if (mask[my * maskW + mx] == 0) continue;

                    // Check zone exclusion
                    if (zoneGrid != null)
                    {
                        int zx = Mathf.Clamp(Mathf.FloorToInt(nx * zoneW), 0, zoneW - 1);
                        int zy = Mathf.Clamp(Mathf.FloorToInt(nz * zoneH), 0, zoneH - 1);
                        if (ExcludeZones.Contains(zoneGrid[zy * zoneW + zx])) continue;
                    }

                    // Pick prefab (weighted)
                    float roll = (float)rng.NextDouble() * totalWeight;
                    int picked = 0;
                    for (int i = 0; i < cumulativeWeights.Length; i++)
                    {
                        if (allPrefabs[i] != null && roll <= cumulativeWeights[i])
                        { picked = i; break; }
                    }
                    if (allPrefabs[picked] == null) continue;

                    float scale = ScaleMin +
                        (float)rng.NextDouble() * (ScaleMax - ScaleMin);
                    float rotDeg = (float)rng.NextDouble() * 360f;

                    // Check if this is a terrain tree or standalone
                    int terrainIdx = terrainProtoIndices.IndexOf(picked);
                    if (terrainIdx >= 0)
                    {
                        // Terrain tree
                        terrainTrees.Add(new TreeInstance
                        {
                            position = new Vector3(nx, 0f, nz),
                            widthScale = scale,
                            heightScale = scale,
                            rotation = rotDeg * Mathf.Deg2Rad,
                            color = Color.white,
                            lightmapColor = Color.white,
                            prototypeIndex = terrainIdx,
                        });
                    }
                    else
                    {
                        // Standalone GameObject
                        float terrainH = terrain.SampleHeight(
                            new Vector3(worldX, 0, worldZ));
                        var pos = new Vector3(
                            worldX, terrainBaseY + terrainH, worldZ);

                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(
                            allPrefabs[picked]);
                        inst.transform.position = pos;
                        inst.transform.rotation =
                            Quaternion.Euler(0, rotDeg, 0);
                        inst.transform.localScale = Vector3.one * scale;
                        inst.transform.SetParent(standaloneRoot.transform);
                        inst.isStatic = true;
                        standaloneCount++;
                    }
                }
            }

            // Apply terrain trees
            if (terrainTrees.Count > 0)
                terrainData.SetTreeInstances(terrainTrees.ToArray(), true);

            // Unify draw distances
            terrain.treeDistance = 150f;
            terrain.treeBillboardDistance = 80f;
            terrain.treeCrossFadeLength = 20f;
            terrain.treeMaximumFullLODCount = 50;

            // Clean up empty container
            if (standaloneCount == 0)
                Object.DestroyImmediate(standaloneRoot);

            Debug.Log($"[TreePlacer] Placed {terrainTrees.Count} terrain trees + " +
                $"{standaloneCount} standalone trees " +
                $"({MinSpacing}m spacing, seed=42)");
        }

        [MenuItem("GOLFIN/Place Trees (Current Terrain)")]
        private static void PlaceTreesMenuItem()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TreePlacer] No active terrain found");
                return;
            }

            string exportBase = "Tools/UHoleLite/output/lomond-country-club/export";
            string exportPath = null;
            for (int h = 1; h <= 18; h++)
            {
                string candidate = Path.Combine(
                    Application.dataPath, "..", exportBase, $"hole-{h:D2}");
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "tree-zones.json")))
                { exportPath = candidate; break; }
            }

            if (exportPath == null)
            {
                Debug.LogError("[TreePlacer] No tree-zones.json found");
                return;
            }

            // Clear terrain trees
            terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);

            // Clear standalone trees container
            var existing = GameObject.Find("Trees_Standalone");
            if (existing != null) Object.DestroyImmediate(existing);

            // Find parent root (the hole root object)
            Transform parentRoot = terrain.transform.parent ?? terrain.transform;

            float terrainBaseY = terrain.transform.position.y;
            string zonesPath = Path.Combine(exportPath, "zones.json");
            PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath, parentRoot);
        }
    }
}
```

### Part 3 — Wire into HoleLiteImporter

After terrain + zone meshes, add:

```csharp
// ---- Trees ----
string zonesJsonExportPath = Path.Combine(exportPath, "zones.json");
TreePlacer.PlaceTrees(terrain, terrainBaseY, exportPath,
    zonesJsonExportPath, holeRoot.transform);
```

Note: `parentRoot` parameter is new (for standalone tree container).

### Verification

1. Re-import Hole 01
2. Console: `[TreePlacer] Placed NN terrain trees + NN standalone trees`
3. Trees(2025) trees: rendered via terrain system (billboard LOD)
4. Realistic Tree spruces: rendered as GameObjects under `Trees_Standalone`
5. All trees follow terrain height, respect zone exclusions
6. GOLFIN > Place Trees clears both types and re-places

### Do NOT

- Force non-LOD prefabs into the terrain tree system
- Change existing zone mesh or terrain code
- Modify the prefabs themselves

---

## Completed Tasks

✅ 2026-04-08 — Fairway mow stripes + fringe ring
✅ 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes
✅ 2026-04-08 — Tee border ring with gradient texture
✅ 2026-04-08 — All earlier tasks (water, bunkers, greens, textures, etc.)
✅ 2026-04-08 — traceBorder direction-aware walk + RDP/Chaikin tuning
✅ 2026-04-09 — Water contour mesh overlay (ear-clip, opaque material)
✅ 2026-04-09 — Cart path contour mesh + min-width dilation
✅ 2026-04-09 — Water shader (URPWater/Standard, animated normals)
✅ 2026-04-09 — Heightmap .raw loader in CreateTerrain
✅ 2026-04-09 — Overlay mesh Y-offsets for DEM terrain
✅ 2026-04-09 — Cart path spine-based strip mesh
✅ 2026-04-09 — Mountain backdrop (single ring, transparent, URP)
✅ 2026-04-09 — Water mesh DEM positioning fix
✅ 2026-04-10 — Bunker v1-v5 iterations → v5 inscribed rectangle terrain hole
✅ 2026-04-10 — Tree placement v1 (terrain trees only, Trees(2025))
✅ 2026-04-10 — Tree placement v2: mixed mode (terrain + standalone), Spruce prefabs, Objects/ scan
✅ 2026-04-11 — Heightmap smoothing (play-area mask + transition blend) + overlay mesh terrain conformance (SubdivideToTerrain for fairway + tee meshes)
✅ 2026-04-11 — Increased overlay mesh Y-offsets (0.02→0.08, fringe 0.09, border 0.07) + reduced SubdivideToTerrain maxEdge 2.0→1.5
