# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Previous Task — Port Water Rework to HoleGeoImporter

Hole 7 Geo shows a seesaw waterline and water edges that don't match the
terrain edges. Cause: HoleGeoImporter still has the OLD per-vertex
terrain-following water code. The 2026-04-14 water rework was applied to
HoleLiteImporter.cs only; HoleGeoImporter.cs never got the port.

This task ports the rework to HoleGeoImporter.cs. The Lite version is the
working reference — match its behavior.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**Reference file:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`
(only for cross-checking — do not edit)
**No pipeline changes.** `water.json` is fine.

---

### Part 1 — Shore constants at top of class

Current (HoleGeoImporter.cs, around lines 18–21):

```csharp
public static int ShoreRadius = 2;
public static float ShoreDepthMeters = 0.1f;
```

Change to:

```csharp
public static int ShoreRadius = 10;
public static float ShoreDepthMeters = 0.4f;

// ─── Terrain Y offset — headroom below flat terrain for water bed.
// Must be ≥ ShoreDepthMeters + water surface depth (0.05m) + underwater margin (0.3m)
// so heightmap can represent the full water bed without clamping.
private static float TerrainYOffset => ShoreDepthMeters;
```

---

### Part 2 — Use `TerrainYOffset` for terrain placement

In `ImportHoleInternal`, the terrain object is positioned using
`ShoreDepthMeters`. Change it to use `TerrainYOffset`.

Find:

```csharp
terrainGO.transform.position = new Vector3(-terrainX / 2f, -ShoreDepthMeters, -terrainZ / 2f);
```

Replace with:

```csharp
terrainGO.transform.position = new Vector3(-terrainX / 2f, -TerrainYOffset, -terrainZ / 2f);
```

Do not touch `CreateTerrain`'s use of `ShoreDepthMeters` in
`elevRange`/`normalizedFlat` — those compute headroom, which is correct.

---

### Part 3 — Rewrite `CreateWaterMeshes`

Find the method `CreateWaterMeshes` (signature:
`private static void CreateWaterMeshes(TerrainData terrainData, GameObject terrainGO, Transform parentRoot, string exportPath, string dataDir, string projectRoot, bool[,] holes)`).

The method has TWO sections:
1. The per-water-body mesh-building `foreach (var water in waterFile.water)` loop
2. A trailing "Shore slope pass" section that builds `isWater` mask and depresses terrain

**Delete section 2 entirely.** That work moves to `DepressTerrainUnderOverlays`
(Part 4). Keep only the final `File.Copy` of water.json to Assets and the final
`Debug.Log`.

**Rewrite section 1 (the per-water-body loop):**

Currently each iteration samples terrain height per vertex and sets
`wy = terrainBaseY + terrainH - 0.1f` — this creates uneven water surface that
seesaws along sloped shores.

Replace the loop body with flat-CDT construction. Here is the complete
replacement for the entire `foreach` body:

```csharp
foreach (var water in waterFile.water)
{
    if (water.contour == null || water.contour.Length < 3) continue;

    int n = water.contour.Length;

    // 3A. Flat water Y = min terrain height across contour − 0.05m
    float minTerrainH = float.MaxValue;
    for (int i = 0; i < n; i++)
    {
        float wx = water.contour[i].x;  // Geo: no rotation
        float wz = water.contour[i].z;
        float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
        if (th < minTerrainH) minTerrainH = th;
    }
    float waterY = terrainBaseY + minTerrainH - 0.05f;

    // 3B. CDT triangulation — same pattern as fairway/tee.
    // Water doesn't need fine terrain conformance (flat surface), but CDT
    // needs interior Steiner points for clean triangulation of large
    // concave shapes. 2.0m grid spacing is plenty.
    float tileSize = 10f; // world-UV tiling for URPWater shader
    System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
        new Vector2(wx / tileSize, wz / tileSize);

    var (rawVerts, uvs, tris) = CDTTriangulate(
        water.contour, terrain, terrainBaseY, 0f, 2.0f, uvFunc);

    if (rawVerts == null || tris == null || tris.Length < 3)
    {
        Debug.LogWarning($"[HoleGeoImporter] Water {water.id}: CDT failed, skipping");
        continue;
    }

    // 3C. Flatten all vertex Y to waterY (CDT sampled terrain heights;
    // overwrite them so the surface is perfectly flat).
    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i].y = waterY;

    // 3D. Center mesh at centroid (Y=0 origin pattern, same as fairway).
    float cx = 0f, cz = 0f;
    for (int i = 0; i < rawVerts.Length; i++)
    { cx += rawVerts[i].x; cz += rawVerts[i].z; }
    cx /= rawVerts.Length; cz /= rawVerts.Length;
    Vector3 centroid = new Vector3(cx, 0f, cz);

    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i] -= centroid;

    // 3E. Winding check — ensure top faces up.
    if (tris.Length >= 3)
    {
        Vector3 a = rawVerts[tris[0]];
        Vector3 b = rawVerts[tris[1]];
        Vector3 c = rawVerts[tris[2]];
        float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        if (cross > 0)
        {
            for (int t = 0; t < tris.Length; t += 3)
            { int tmp = tris[t]; tris[t] = tris[t + 2]; tris[t + 2] = tmp; }
        }
    }

    var mesh = new Mesh();
    mesh.name = $"Water_{water.id}";
    mesh.vertices = rawVerts;
    mesh.uv = uvs;
    mesh.triangles = tris;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"Water_{water.id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = waterMat;

    AddCleanMeshCollider(go, mesh);

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = Golfin.Course.SurfaceType.Water;

    go.transform.SetParent(waterRoot.transform);

    Debug.Log($"[HoleGeoImporter] Water {water.id}: {n} contour verts, " +
              $"{rawVerts.Length} CDT verts, {tris.Length / 3} tris, " +
              $"waterY={waterY:F2}");
}
```

**Notes:**
- Keep the two existing `Debug.Log` lines at method top
  (`terrainBaseY={...}` / `ShoreDepthMeters={...}`). They're useful.
- Keep the `waterRoot`/`terrain`/`terrainBaseY`/`waterMat` setup at top
  of the method — unchanged.
- The old section 2 (shore slope, `isWater` mask, chamfer distance,
  `underwaterDrop`, `terrainData.SetHeights`) is DELETED.

---

### Part 4 — Add water handling to `DepressTerrainUnderOverlays`

`DepressTerrainUnderOverlays` currently handles fairway + tee + cart path.
Add water.

**4A. Add water contours to the `depress` bool[,] array.**

In `DepressTerrainUnderOverlays`, find the tee contour section
(immediately after fairway, uses `zone-contours.json` and the
`data.zones.tee` loop with `MarkContourCells(region.contour, depress, ...)`).

Immediately AFTER the closing brace of that tee block, BEFORE the cart path
section (the `cartDepress` block), insert:

```csharp
// Water contours — use same flat depression as fairway/tee
// but with shore slope ramp applied afterward.
string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        foreach (var w in waterData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, depress,
                    hRes, terrainPos, terrainSize, 0f);
                    // inset=0 — depress right up to the contour edge
        }
    }
}
```

This makes water cells receive the standard `OverlayDepressionMeters` (0.40m)
flat drop in the existing apply loop. No separate water-depression needed.

**4B. Add shore slope pass after the existing apply loop.**

The existing apply loop ends with `depressedCount += cartDepressedCount;` and
then `terrainData.SetHeights(0, 0, heights);` followed by a Debug.Log.

**BEFORE** `terrainData.SetHeights(0, 0, heights);`, insert the shore slope
pass:

```csharp
// ─── Shore slope pass: gradual ramp outside water contours ─────────
// Creates a smooth transition from shoreline (full ShoreDepthMeters drop)
// to surrounding terrain (no drop) over ShoreRadius cells.
// Without this, water edges would cliff against un-depressed terrain.
string waterShorePath = Path.Combine(exportPath, "water.json");
int shoreCount = 0;
if (File.Exists(waterShorePath) && ShoreRadius > 0 && ShoreDepthMeters > 0f)
{
    // 4B-1. Build water-only mask from water contours.
    bool[,] waterMask = new bool[hRes, hRes];
    var waterShoreData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterShorePath));
    if (waterShoreData.water != null)
    {
        foreach (var w in waterShoreData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, waterMask,
                    hRes, terrainPos, terrainSize, 0f);
        }
    }

    // 4B-2. Chamfer distance transform from water boundary (cells not in water).
    float[,] distToWater = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;

    // Forward pass
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
        {
            if (x > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x - 1] + 1f);
            if (z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x] + 1f);
            if (x > 0 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x - 1] + 1.414f);
            if (x < hRes - 1 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x + 1] + 1.414f);
        }
    // Backward pass
    for (int z = hRes - 1; z >= 0; z--)
        for (int x = hRes - 1; x >= 0; x--)
        {
            if (x < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x + 1] + 1f);
            if (z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x] + 1f);
            if (x < hRes - 1 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x + 1] + 1.414f);
            if (x > 0 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x - 1] + 1.414f);
        }

    // 4B-3. Apply ramp OUTSIDE water (full drop at boundary,
    //       zero drop at ShoreRadius). Skip water cells (already
    //       depressed in step 4A) and fairway/tee/cart cells
    //       (already fully depressed — another drop would stack).
    float shoreDropNorm = ShoreDepthMeters / elevRange;
    int shoreRadiusCells = ShoreRadius;

    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            if (waterMask[z, x]) continue;           // water cell: skip
            if (depress[z, x]) continue;             // fairway/tee/water: skip
            if (cartDepress[z, x]) continue;         // cart path: skip

            float dist = distToWater[z, x];
            if (dist <= 0f || dist > shoreRadiusCells) continue;

            // smoothstep: 1 at boundary, 0 at shoreRadius
            float t = 1f - (dist / shoreRadiusCells);
            t = t * t * (3f - 2f * t);
            float drop = shoreDropNorm * t;

            heights[z, x] = Mathf.Max(0f, heights[z, x] - drop);
            shoreCount++;
        }
    }
}
```

Then update the final Debug.Log to include shore:

```csharp
Debug.Log($"[HoleGeoImporter] Terrain depression: {depressedCount}" +
          $" cells lowered by {OverlayDepressionMeters:F2}m" +
          $" (cart path: {cartDepressedCount} cells," +
          $" water shore ramp: {shoreCount} cells)");
```

**Important:** The variable `cartDepress` is defined inside
`DepressTerrainUnderOverlays` and is in scope at the insertion point — it's
created above the fairway/tee sections. Verify the variable is accessible
where you insert the shore pass. If for any reason the cart path mask is
scoped differently in Geo, drop the `if (cartDepress[z, x]) continue;` line
(worst case: cart-path grass gets an extra shore drop near water, visually
harmless).

---

### Part 5 — Update water material depth settings

In `CreateWaterMaterial`, find:

```csharp
mat.SetFloat("_DepthStart", 0f);
mat.SetFloat("_DepthEnd", 0.3f);
```

Change to:

```csharp
mat.SetFloat("_DepthStart", 0f);
mat.SetFloat("_DepthEnd", 0.8f);
```

This gives the depth-based color gradient room to work with the new 0.4m
shore depression.

---

### Execution order

1. Part 1 (constants)
2. Part 2 (terrain position)
3. Part 5 (material — trivial, do it while you're near the constants area)
4. Part 3 (CreateWaterMeshes rewrite)
5. Part 4 (DepressTerrainUnderOverlays — 4A then 4B)

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] Water surface is perfectly flat (single Y per body, no seesaw)
- [ ] Water edges line up with terrain edges (no dark cliff strip)
- [ ] Shore slopes gradually into water
- [ ] Depth-based color: shallower teal near edges, darker blue toward center
- [ ] No z-fighting between water mesh and terrain
- [ ] Fairways, tees, bunkers, greens, cart paths unaffected

Then regression check with a hole without water:

- [ ] `Import Hole 01 Geo` completes without errors (Hole 1 has no water —
      make sure the water file handling degrades cleanly)

And a hole with multiple water bodies:

- [ ] `Import Hole 12 Geo` — waterways + pond, check both look flat

---

### Do NOT change

- `CreateWaterMaterial` shader selection (URPWater/Standard)
- Any other CreateWaterMeshes setup code (waterRoot, terrain, terrainBaseY,
  waterMat vars, File.Copy at end)
- Fairway/tee/green/bunker/cart path logic
- UHoleGeo export pipeline — `water.json` is fine as-is
- The disabled `if (false && loadedRaw)` boundary propagation block

---

## Completed Tasks

✅ DONE: 2026-04-17 — Water rework ported to HoleGeoImporter: flat CDT, TerrainYOffset, water depression in DepressTerrainUnderOverlays, shore slope ramp, _DepthEnd 0.8
✅ DONE: 2026-04-16 — Flat inside + 8-cell outward smoothstep ramp implemented
✅ DONE: 2026-04-16 — Green collar CDT complete
✅ DONE: 2026-04-16 — Bunker lip submesh complete
✅ 2026-04-16 — Bunker lip baked as submesh 1
✅ 2026-04-16 — Cart path outward smoothstep ramp (8 cells)
✅ 2026-04-16 — Cart path flat depression
✅ 2026-04-16 — Spline cart path depression footprint
✅ 2026-04-16 — Spline cart path meshes
✅ 2026-04-16 — Fringe/border baked into parent CDT mesh as submesh
✅ 2026-04-14 — Water rework complete (HoleLiteImporter only — Geo ported 2026-04-17)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ All earlier tasks
