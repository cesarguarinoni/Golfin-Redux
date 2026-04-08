# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Zone Overlay Meshes (Fairway, Tee, Cart Path)

**Goal:** Replace splatmap-painted fairway, tee, and cart path zones with
smooth contour-based mesh overlays — the same approach that already works
great for greens and bunkers. This eliminates the jagged pixel-staircase
edges permanently.

**Why:** The splatmap approach is fundamentally pixel-quantized. No amount
of SDF, blur, or vector contour rasterization can produce genuinely
smooth edges when the final output is a discrete 1024×1024 grid. Greens
and bunkers already look great because they use contour-traced meshes.
We're extending that to all visible zones.

---

### Architecture Overview

The terrain splatmap paints **rough texture everywhere** as the base layer.
On top of that, mesh overlays provide the visible surface for each zone:

| Zone | Current | New |
|------|---------|-----|
| Green (2) | Mesh overlay ✅ | No change |
| Bunker (6) | Mesh overlay ✅ | No change |
| Water (7) | Mesh overlay ✅ | No change |
| Fairway (1) | Splatmap ❌ | **Mesh overlay** |
| Tee box (10) | Splatmap ❌ | **Mesh overlay** |
| Cart path (8) | Splatmap ❌ | **Mesh overlay** |
| Rough (4) | Splatmap ✅ | No change (base layer) |
| Semi-rough (3) | Splatmap ✅ | No change |
| Trees (5) | Splatmap ✅ | No change |

---

### Part 1: Export — `Tools/UHoleLite/scripts/export-hole.mjs`

The contour extraction for fairway, tee, and cart path already exists in
`fairway-contours.json` and `zone-contours.json`. However, ensure these
are present in the `exportHole` function. If they are missing, add them:

```javascript
// --- Build fairway-contours.json ---
const fairways = extractZoneContours(zonesData, terrainMeta, 1, 30, 3.0, 3);
const fairwayOutput = {
  schema_version: '1.0.0',
  hole_number: holeNumber,
  fairway_count: fairways.length,
  fairways: fairways,
};
fs.writeFileSync(
  path.join(exportDir, 'fairway-contours.json'),
  JSON.stringify(fairwayOutput, null, 2), 'utf-8'
);

// --- Build zone-contours.json ---
const tees = extractZoneContours(zonesData, terrainMeta, 10, 15, 1.5, 3);
const semiRough = extractZoneContours(zonesData, terrainMeta, 3, 30, 3.0, 3);
const cartPaths = extractZoneContours(zonesData, terrainMeta, 8, 15, 1.5, 3);

const zoneContoursOutput = {
  schema_version: '1.0.0',
  hole_number: holeNumber,
  zones: {
    tee: tees,
    semi_rough: semiRough,
    cart_path: cartPaths,
  },
};
fs.writeFileSync(
  path.join(exportDir, 'zone-contours.json'),
  JSON.stringify(zoneContoursOutput, null, 2), 'utf-8'
);
```

If these blocks already exist, just verify the parameters and move on.

---

### Part 2: Unity Importer — `HoleLiteImporter.cs`

#### 2a. New method: `CreateFlatZoneMeshes`

Add a new method that creates flat contour meshes for fairway, tee,
and cart path. These sit directly on the terrain surface (no raise).

The pattern is similar to `CreateGreenMeshes` but simpler — no collar,
no raise height. Just a flat polygon mesh at the terrain's Y position,
using a triangulated contour.

```csharp
private static void CreateFlatZoneMeshes(TerrainData terrainData,
    GameObject terrainGO, Transform parentRoot,
    string exportPath, string dataDir, string projectRoot)
{
    string texDir = "Assets/Courses/Textures_2025(JPG)";
    var terrain = terrainGO.GetComponent<Terrain>();
    float terrainBaseY = terrainGO.transform.position.y;

    // ─── Fairway meshes ─────────────────────────────
    string fwPath = Path.Combine(exportPath, "fairway-contours.json");
    if (File.Exists(fwPath))
    {
        string json = File.ReadAllText(fwPath);
        var data = JsonUtility.FromJson<FairwayContoursFile>(json);

        if (data.fairways != null && data.fairways.Length > 0)
        {
            var fwRoot = new GameObject("Fairways");
            fwRoot.transform.SetParent(parentRoot);

            var fwMat = CreateTiledMaterial(texDir, "T_Fairway_Light",
                "T_Fairway_Normal", dataDir, projectRoot, 5f);

            foreach (var fw in data.fairways)
            {
                if (fw.contour == null || fw.contour.Length < 3) continue;

                var meshGO = CreateFlatContourMesh(
                    fw.id, "Fairway", fw.contour,
                    terrain, terrainBaseY, fwMat,
                    Golfin.Course.SurfaceType.Fairway);
                if (meshGO != null)
                    meshGO.transform.SetParent(fwRoot.transform);
            }

            Debug.Log($"[HoleLiteImporter] Created {data.fairways.Length} fairway mesh(es)");
        }
    }

    // ─── Tee meshes ─────────────────────────────
    string zcPath = Path.Combine(exportPath, "zone-contours.json");
    if (File.Exists(zcPath))
    {
        string json = File.ReadAllText(zcPath);
        var data = JsonUtility.FromJson<ZoneContoursFile>(json);

        if (data.zones?.tee != null && data.zones.tee.Length > 0)
        {
            var teeRoot = new GameObject("Tees");
            teeRoot.transform.SetParent(parentRoot);

            var teeMat = CreateTiledMaterial(texDir, "T_Tee_Albedo",
                "T_Tee_Normal", dataDir, projectRoot, 3f);

            foreach (var region in data.zones.tee)
            {
                if (region.contour == null || region.contour.Length < 3) continue;

                var meshGO = CreateFlatContourMesh(
                    region.id, "Tee", region.contour,
                    terrain, terrainBaseY, teeMat,
                    Golfin.Course.SurfaceType.Tee);
                if (meshGO != null)
                    meshGO.transform.SetParent(teeRoot.transform);
            }

            Debug.Log($"[HoleLiteImporter] Created {data.zones.tee.Length} tee mesh(es)");
        }

        // ─── Cart path meshes ─────────────────────────────
        if (data.zones?.cart_path != null && data.zones.cart_path.Length > 0)
        {
            var cpRoot = new GameObject("CartPaths");
            cpRoot.transform.SetParent(parentRoot);

            var cpMat = CreateTiledMaterial(texDir, "T_RoadAsphalt_Albedo",
                "T_RoadAsphalt_Normal", dataDir, projectRoot, 4f);

            foreach (var region in data.zones.cart_path)
            {
                if (region.contour == null || region.contour.Length < 3) continue;

                var meshGO = CreateFlatContourMesh(
                    region.id, "CartPath", region.contour,
                    terrain, terrainBaseY, cpMat,
                    Golfin.Course.SurfaceType.CartPath);
                if (meshGO != null)
                    meshGO.transform.SetParent(cpRoot.transform);
            }

            Debug.Log($"[HoleLiteImporter] Created {data.zones.cart_path.Length} cart path mesh(es)");
        }
    }
}
```

#### 2b. Helper: `CreateFlatContourMesh`

Creates a flat polygon mesh from a contour, sitting just above terrain
surface. Uses ear-clipping triangulation (or a simple fan from centroid).

```csharp
/// <summary>
/// Create a flat mesh from a contour polygon, positioned at terrain height.
/// Uses centroid-fan triangulation (works for convex and mildly concave shapes).
/// </summary>
private static GameObject CreateFlatContourMesh(int id, string zoneName,
    ContourPoint[] contour, Terrain terrain, float terrainBaseY,
    Material mat, Golfin.Course.SurfaceType surfaceType)
{
    int n = contour.Length;
    if (n < 3) return null;

    // Convert contour to world space (90° CCW rotation: worldX = z, worldZ = x)
    Vector3[] worldPts = new Vector3[n];
    float yOffset = 0.02f; // slightly above terrain to prevent z-fighting

    for (int i = 0; i < n; i++)
    {
        float wx = contour[i].z; // 90° CCW rotation
        float wz = contour[i].x;
        float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
        worldPts[i] = new Vector3(wx, terrainBaseY + terrainH + yOffset, wz);
    }

    // Compute centroid
    float cx = 0, cy = 0, cz = 0;
    for (int i = 0; i < n; i++)
    {
        cx += worldPts[i].x;
        cy += worldPts[i].y;
        cz += worldPts[i].z;
    }
    cx /= n; cy /= n; cz /= n;
    Vector3 centroid = new Vector3(cx, cy, cz);

    // Build mesh: vertices = contour points + centroid (all relative to centroid)
    var verts = new Vector3[n + 1];
    var uvs = new Vector2[n + 1];

    // UV bounding box for world-space tiling
    float minX = float.MaxValue, maxX = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;
    for (int i = 0; i < n; i++)
    {
        if (worldPts[i].x < minX) minX = worldPts[i].x;
        if (worldPts[i].x > maxX) maxX = worldPts[i].x;
        if (worldPts[i].z < minZ) minZ = worldPts[i].z;
        if (worldPts[i].z > maxZ) maxZ = worldPts[i].z;
    }
    float extentX = Mathf.Max(maxX - minX, 0.1f);
    float extentZ = Mathf.Max(maxZ - minZ, 0.1f);

    for (int i = 0; i < n; i++)
    {
        verts[i] = worldPts[i] - centroid; // relative to centroid
        uvs[i] = new Vector2(
            (worldPts[i].x - minX) / extentX,
            (worldPts[i].z - minZ) / extentZ);
    }
    // Center vertex
    verts[n] = Vector3.zero; // centroid is at origin
    uvs[n] = new Vector2(
        (cx - minX) / extentX,
        (cz - minZ) / extentZ);

    // Triangles: fan from centroid
    var tris = new int[n * 3];
    for (int i = 0; i < n; i++)
    {
        tris[i * 3 + 0] = i;
        tris[i * 3 + 1] = n; // centroid
        tris[i * 3 + 2] = (i + 1) % n;
    }

    var mesh = new Mesh();
    mesh.name = $"{zoneName}_{id}";
    mesh.vertices = verts;
    mesh.triangles = tris;
    mesh.uv = uvs;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"{zoneName}_{id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    go.AddComponent<MeshCollider>().sharedMesh = mesh;

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = surfaceType;

    return go;
}
```

#### 2c. Helper: `CreateTiledMaterial`

Creates a URP Lit material with a tiling albedo + normal map. Uses the
same matte mask map as the terrain layers.

```csharp
private static Material CreateTiledMaterial(string texDir, string albedoName,
    string normalName, string dataDir, string projectRoot, float tileSize)
{
    var albedo = FindTextureExact(texDir, albedoName);
    var normal = FindTextureExact(texDir, normalName);

    var mat = new Material(GetLitShader());
    mat.name = $"MAT_{albedoName}";
    if (albedo != null) mat.mainTexture = albedo;
    if (normal != null)
    {
        mat.SetTexture("_BumpMap", normal);
        mat.SetFloat("_BumpScale", 0.4f);
        mat.EnableKeyword("_NORMALMAP");
    }

    // Tiling: material covers tileSize x tileSize meters per repeat
    // But the mesh UV is 0-1 over the polygon extent, so we set
    // tiling to extent/tileSize. This needs to be set per-mesh...
    // Actually, simpler: use world-space UVs in the mesh and set
    // material tiling to 1/tileSize. Wait — the UVs are already
    // in 0-1 range over the polygon bbox. Let's tile via material:
    //
    // The mesh UV maps [0,1] across the polygon's bounding box.
    // If bbox is 200m wide and tileSize is 5m, we need 40 repeats.
    // But we don't know bbox size here. Two options:
    //   A) Set UV in world meters in the mesh, tile = 1/tileSize
    //   B) Set per-mesh material tiling
    //
    // For simplicity, we'll use world-space UVs in the mesh
    // (divide by tileSize in CreateFlatContourMesh) and set
    // material tiling to (1,1). See note in CreateFlatContourMesh.

    mat.SetFloat("_Smoothness", 0f);
    mat.SetFloat("_Metallic", 0f);

    // Save as asset
    string matPath = $"{dataDir}/MAT_{albedoName}.mat";
    var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (existing != null) AssetDatabase.DeleteAsset(matPath);
    AssetDatabase.CreateAsset(mat, matPath);

    return mat;
}
```

**IMPORTANT UV note:** The `CreateFlatContourMesh` method above uses
normalized 0-1 UVs over the polygon bbox. For proper texture tiling,
change the UV computation to use **world-space UVs divided by tile size**:

```csharp
// In CreateFlatContourMesh, replace the UV computation:
// Instead of normalizing to 0-1 over bbox, use world coords / tileSize
float tileSize = mat.mainTexture != null ? 5f : 1f; // default 5m tile
// Actually we need to pass tileSize as a parameter...
```

**Simpler approach:** Change `CreateFlatContourMesh` to accept a
`float tileSize` parameter, and compute UVs as:

```csharp
uvs[i] = new Vector2(worldPts[i].x / tileSize, worldPts[i].z / tileSize);
```

This gives world-space tiling — the texture repeats every `tileSize`
meters regardless of polygon size. Set material tiling to (1,1).
Apply the same to the centroid vertex UV.

#### 2d. Add `SurfaceType` entries if missing

Check `Golfin.Course.SurfaceType` enum — ensure it has `Fairway`, `Tee`,
and `CartPath` entries. If not, add them.

#### 2e. Call from ImportLiteHole

In `ImportLiteHole`, add the call after CreateWaterMeshes:

```csharp
EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating zone meshes...", 0.62f);
CreateFlatZoneMeshes(terrainData, terrainGO, holeRoot.transform,
    exportPath, dataDir, projectRoot);
```

#### 2f. Simplify the splatmap

Since fairway, tee, and cart path are now meshes, the splatmap can be
simplified. In `ApplySplatmap` and `ZoneToLayer`:

Change `ZoneToLayer` so fairway (1), tee (10), and cart path (8) all
map to **rough (layer 3)** — same as greens and bunkers already do:

```csharp
private static int ZoneToLayer(int zoneIndex)
{
    return zoneIndex switch
    {
        1  => 3,  // fairway → rough (mesh overlay handles surface)
        2  => 3,  // green → rough (mesh handles surface)
        3  => 2,  // semi_rough
        4  => 3,  // rough
        5  => 3,  // trees → rough texture
        6  => 3,  // bunker → rough (mesh handles sand surface)
        7  => 3,  // water → rough
        8  => 3,  // cart_path → rough (mesh overlay handles surface)
        9  => 3,  // ob → rough texture
        10 => 3,  // tee_box → rough (mesh overlay handles surface)
        _  => 3,  // background/unknown → rough
    };
}
```

Also **remove** the entire vector contour rasterization section (step 2b)
and the SDF/fairway contour code — none of that is needed anymore since
meshes handle everything. Remove:
- The fairway-contours.json loading + RasterizePolygon calls
- The zone-contours.json loading + RasterizeContour calls
- The fairway fringe ring dilation (step 3b)
- The mow stripe logic in the alphamap loop (mow stripes will be
  handled differently on the mesh — see below)
- The `RasterizePolygon` and `RasterizeContour` methods
- The `ComputeSDF` method (if still present)

Keep: the green fringe ring (step 3) — that still makes sense as
splatmap semi-rough around the green mesh.

#### 2g. Fairway mow stripes on mesh (stretch goal)

Mow stripes on the fairway mesh can be handled in a few ways:
- **Option A (simplest):** Skip mow stripes for now. Use a single
  fairway texture. Add mow stripes later.
- **Option B:** Create two materials (light/dark fairway) and split
  the fairway mesh into alternating triangle bands.
- **Option C:** Use a custom shader with world-space stripe calculation.

**For this task, use Option A** — single fairway material, no stripes.
We can add stripes later as a polish pass.

---

### Verification

1. Re-run export: `node scripts/export-hole.mjs lomond-country-club 1`
2. Re-import: GOLFIN > Import Hole (Lite) > Hole 01
3. Check:
   - [ ] Fairway has smooth, organic edges (mesh contour, not splatmap pixels)
   - [ ] Tee boxes have smooth edges
   - [ ] Cart path has smooth curves
   - [ ] Greens and bunkers still look correct (unchanged)
   - [ ] Water still works (unchanged)
   - [ ] Terrain underneath shows rough texture everywhere
   - [ ] Textures tile properly on all mesh surfaces
   - [ ] No z-fighting between mesh and terrain
   - [ ] SurfaceMarker components present on all meshes
   - [ ] Hierarchy: HoleRoot > Fairways/Tees/CartPaths > individual meshes
   - [ ] No console errors

### Do NOT

- Modify green, bunker, or water mesh pipelines
- Apply any splatmap blur or SDF
- Remove the splatmap system entirely (semi-rough and rough still use it)
- Add mow stripes yet (future task)
- Change terrain layers or textures

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Texture cleanup: swap, fringe ring, blur removed, alphamap 1024, zone grid 2048
✅ DONE: 2026-04-08 — PNG + SVG zone import in Hole Viewer
✅ DONE: 2026-04-08 — Morphological close + various smoothing attempts
✅ DONE: 2026-04-08 — Fairway mow stripes: alternating light/dark bands along tee→green axis
✅ DONE: 2026-04-08 — Re-enable normal maps (0.4 intensity) + aniso filtering (level 16) on all terrain textures
✅ DONE: 2026-04-08 — SDF-based smooth fairway border (replaced by mesh approach)
✅ DONE: 2026-04-08 — Vector contour rasterization (replaced by mesh approach)
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway, tee, cart path mesh overlays + splatmap simplification
