# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Water: Replace Rasterized Quad with Contour Mesh Overlay

**Goal:** Replace the water rasterized quad + SDF alpha mask with the
same contour mesh system used by fairways, greens, and bunkers. This
gives water the same smooth edges as all other zones, and simplifies the
pipeline (one system for everything).

### Part 1 — Export Side (`Tools/UHoleLite/scripts/export-hole.mjs`)

Replace the `extractWaterMasks()` call with `extractZoneContours()`.

In the `exportHole()` function, find the water section and replace:

```javascript
// OLD:
const water = extractWaterMasks(zonesData, terrainMeta, 50);

const waterOutput = {
  schema_version: '2.0.0',
  hole_number: holeNumber,
  water_count: water.length,
  water: water,
};
```

With:

```javascript
// NEW:
const water = extractZoneContours(zonesData, terrainMeta, 7, 50, 2.0, 2);
// zone 7 = water, min 50px, RDP epsilon 2.0, 2 Chaikin passes
// (water shapes are large — epsilon 2.0 is fine; Chaikin 2 softens edges
// without the over-inflation that was a problem with the old dedicated pipeline)

const waterOutput = {
  schema_version: '3.0.0',
  hole_number: holeNumber,
  water_count: water.length,
  water: water,
};
```

Also update the log line:

```javascript
// OLD:
if (water.length > 0) {
  const maskStats = water.map(w =>
    `#${w.id}: ${w.mask_width}x${w.mask_height}px (${w.pixel_count}px)`
  ).join(', ');
  console.log(`  Water masks: ${maskStats}`);
}

// NEW:
if (water.length > 0) {
  const contourStats = water.map(w =>
    `#${w.id}: ${w.contour.length}pts (${w.pixel_count}px)`
  ).join(', ');
  console.log(`  Water contours: ${contourStats}`);
}
```

Also update the manifest: change `water_file` from `'water.json'` to
`'water.json'` (same name, just noting the schema changed).

The `extractWaterMasks()` function can stay in the file (dead code) or
be removed — your choice. It's no longer called.

### Part 2 — Unity Side (`Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`)

Replace `CreateWaterMeshes()` entirely. The new version:

1. Reads `water.json` as contour data (same schema as greens/bunkers)
2. For each water region:
   a. Apply 90° CCW rotation to contour vertices (worldX = local.z,
      worldZ = local.x) — same as all other zones
   b. Compute centroid
   c. Create a flat mesh at `waterY = 0.05f` using **ear-clip
      triangulation** (the `EarClipTriangulate()` method already exists)
   d. Apply a water material (solid color, high smoothness)
   e. Add `MeshCollider` + `SurfaceMarker(Water)`
3. Keep the **shore slope depression** pass (the distance-transform code
   that dips terrain near water edges) — it's independent of mesh shape
4. Do NOT cut terrain holes for water (water sits on top, opaque)

Here's the replacement `CreateWaterMeshes()` method:

```csharp
private static void CreateWaterMeshes(TerrainData terrainData, GameObject terrainGO,
    Transform parentRoot, string exportPath, string dataDir, string projectRoot,
    bool[,] holes)
{
    string waterPath = Path.Combine(exportPath, "water.json");
    if (!File.Exists(waterPath))
    {
        Debug.Log("[HoleLiteImporter] No water.json found, skipping");
        return;
    }

    string json = File.ReadAllText(waterPath);
    var waterFile = JsonUtility.FromJson<WaterFileData>(json);

    if (waterFile.water == null || waterFile.water.Length == 0)
    {
        Debug.Log("[HoleLiteImporter] No water in water.json");
        return;
    }

    var waterRoot = new GameObject("Water");
    waterRoot.transform.SetParent(parentRoot);

    float waterY = 0.05f;

    // Create water material (solid blue-ish, high smoothness)
    var waterMat = CreateWaterMaterial(dataDir);

    var terrain = terrainGO.GetComponent<Terrain>();
    float terrainBaseY = terrainGO.transform.position.y;

    foreach (var water in waterFile.water)
    {
        if (water.contour == null || water.contour.Length < 3) continue;

        int n = water.contour.Length;

        // Apply 90° CCW rotation (same as all other zones)
        Vector3[] worldPts = new Vector3[n];
        float sumX = 0, sumZ = 0;
        for (int i = 0; i < n; i++)
        {
            float wx = water.contour[i].z;  // 90° CCW
            float wz = water.contour[i].x;
            worldPts[i] = new Vector3(wx, waterY, wz);
            sumX += wx;
            sumZ += wz;
        }
        float centroidX = sumX / n;
        float centroidZ = sumZ / n;
        Vector3 centroid = new Vector3(centroidX, waterY, centroidZ);

        // Build mesh with ear-clip triangulation
        var verts = new Vector3[n];
        var uvs = new Vector2[n];
        float tileSize = 10f; // water texture tiling
        for (int i = 0; i < n; i++)
        {
            verts[i] = worldPts[i] - centroid;
            uvs[i] = new Vector2(worldPts[i].x / tileSize, worldPts[i].z / tileSize);
        }

        var tris = EarClipTriangulate(worldPts);
        if (tris == null || tris.Length < 3)
        {
            Debug.LogWarning($"[HoleLiteImporter] Water {water.id}: ear-clip failed, skipping");
            continue;
        }

        var mesh = new Mesh();
        mesh.name = $"Water_{water.id}";
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject($"Water_{water.id}");
        go.transform.position = centroid;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = waterMat;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;

        var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
        marker.surfaceType = Golfin.Course.SurfaceType.Water;

        go.transform.SetParent(waterRoot.transform);

        Debug.Log($"[HoleLiteImporter] Water {water.id}: {n} contour verts, " +
                  $"{tris.Length / 3} tris, pos ({centroidX:F1}, {waterY}, {centroidZ:F1})");
    }

    // ─── Shore slope pass (KEEP — independent of mesh type) ──────────
    if (ShoreRadius > 0 && ShoreDepthMeters > 0f)
    {
        // ... (keep the entire existing shore slope code block unchanged)
        // This code uses the zone grid directly, not the mesh contour,
        // so it works the same regardless of water mesh type.
    }

    // Copy water.json to Assets
    string destPath = Path.Combine(projectRoot, dataDir, "water.json");
    File.Copy(waterPath, destPath, true);
    AssetDatabase.ImportAsset($"{dataDir}/water.json");

    Debug.Log($"[HoleLiteImporter] Created {waterFile.water.Length} water contour mesh(es)");
}

private static Material CreateWaterMaterial(string dataDir)
{
    string matPath = $"{dataDir}/WaterSurface.mat";
    var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (existingMat != null)
        AssetDatabase.DeleteAsset(matPath);

    var mat = new Material(GetLitShader());
    mat.name = "WaterSurface";
    mat.color = new Color(0.18f, 0.40f, 0.58f);  // dark water blue
    mat.SetFloat("_Smoothness", 0.85f);
    mat.SetFloat("_Metallic", 0.05f);

    // Opaque — no alpha clip needed anymore
    mat.SetFloat("_Surface", 0);
    mat.SetFloat("_AlphaClip", 0);

    AssetDatabase.CreateAsset(mat, matPath);
    return mat;
}
```

**Key difference from old code:** The shore slope depression block must
be preserved in full. Copy it verbatim from the existing
`CreateWaterMeshes()`. It reads the zone grid directly (not the mesh
contour), so it doesn't need any changes.

### Part 3 — Update `WaterFileData` / `WaterRegionData` deserialization classes

The `WaterRegionData` class (in `HoleManifestData.cs` probably) currently
has `mask`, `mask_width`, `mask_height`, `bbox` fields for the rasterized
approach. Update it to match the contour schema:

```csharp
[System.Serializable]
public class WaterRegionData
{
    public int id;
    public int pixel_count;
    public ContourPoint[] contour;     // NEW — same as bunkers/greens
    public AnchorLocal center_local;   // NEW
    public SizeData size_m;            // NEW
    // Remove: mask, mask_width, mask_height, bbox
}
```

Make sure `WaterFileData` still has `public WaterRegionData[] water;`.

The `ContourPoint` and `AnchorLocal` classes should already exist from
bunkers/greens. Check `HoleManifestData.cs` for them.

### Verification

1. Re-export: `node scripts/export-hole.mjs lomond-country-club 1`
   - Should log `Water contours: #1: NNpts (NNNpx)` instead of mask stats
2. Re-import in Unity: GOLFIN > Import Hole (Lite) > Hole 01
   - Water should appear with smooth contour edges (same style as fairway)
   - Shore slope should still work (terrain dips toward water)
   - No SDF/mask texture files generated
3. Walk around in play mode — water edges should look smooth, not pixelated

### Do NOT

- Change `traceBorder`, `simplifyPolygon`, `smoothPolygon`, or `ensureCCW`
- Modify bunker, green, or fairway pipeline code
- Change shore slope depression logic
- Change `EarClipTriangulate`

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Fairway mow stripes + fringe ring
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes
✅ DONE: 2026-04-08 — Tee border ring with gradient texture
✅ DONE: 2026-04-08 — All earlier tasks (water, bunkers, greens, textures, etc.)
✅ DONE: 2026-04-08 — traceBorder replaced with direction-aware walk + RDP epsilon 3.0→1.0, Chaikin 3→2. BIG DIFF at z=50 eliminated (-5.4→-1.2m). Note: trace was not the root cause — the 22.1% diagnostic was misleading (counted interior border pixels). Real fix was RDP reduction. One BIG DIFF remains at z=-5 (narrow tip, -5.2m).
✅ DONE: 2026-04-09 — Water: replaced rasterized quad + SDF alpha mask with contour mesh overlay. Export uses extractZoneContours (zone 7, epsilon 2.0, 2 Chaikin passes). Unity importer uses ear-clip triangulation + opaque water material. Shore slope depression preserved unchanged.
