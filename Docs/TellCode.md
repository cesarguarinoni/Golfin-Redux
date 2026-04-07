# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task — Re-import After Contour Smoothing

The UHole Lite export now produces smoothed contours (Chaikin subdivision).
The Unity importer code (`CreateZoneMeshes` / `CreateContourMesh`) is
already correct — it uses whatever contour vertices it receives.

> **IMPORTANT:** Run `Tools/UHoleLite/docs/TASK.md` first (contour
> smoothing), re-export hole 01, THEN re-import in Unity.

### Steps

1. In UHole Lite: `node scripts/export-hole.mjs lomond-country-club 1`
2. In Unity: `GOLFIN > Import Hole (Lite) > Hole 01`
3. Verify bunker shapes are now smooth (rounded curves, not angular)
4. Verify terrain hole cuts follow the contour (not rectangular)
5. Check rim edges are flush, no z-fighting, no terrain bleed-through

If the Unity importer is NOT already updated with `CreateZoneMeshes` /
`CreateContourMesh`, apply the changes below first.

---

### Change 1: Flat Terrain

In `CreateTerrain()`, replace the heightmap loading with a flat surface.

**Find this block** (the heightmap loading + rotation loop):

```csharp
string heightmapPath = Path.Combine(exportPath, manifest.terrain.heightmap_file);
byte[] rawBytes = File.ReadAllBytes(heightmapPath);

// Rotate heightmap 90° CCW: heights[hx, hy] instead of heights[res-1-hy, hx]
float[,] heights = new float[res, res];
for (int hy = 0; hy < res; hy++)
{
    for (int hx = 0; hx < res; hx++)
    {
        int idx = (hy * res + hx) * 2;
        ushort val = (ushort)((rawBytes[idx] << 8) | rawBytes[idx + 1]);
        heights[hx, hy] = val / 65535f;
    }
}
```

**Replace with:**

```csharp
// V2 DEV: Flat terrain — skip heightmap, all heights = 0.
// Splatmap still paints correct zones. Re-enable heightmap in Task 4.
float[,] heights = new float[res, res];
// All values default to 0.0 — perfectly flat
```

Also change `elevRange` to a safe nonzero value:

```csharp
// OLD:
float elevRange = manifest.terrain.max_elevation_m - manifest.terrain.min_elevation_m;

// NEW:
float elevRange = 1.0f;  // V2 DEV: flat terrain, nonzero to avoid edge cases
```

---

### Change 2: Replace CreateBunkers → CreateZoneMeshes

Delete the existing `CreateBunkers()` and `CreateBowlMesh()` methods entirely.
Replace with the new contour-based system below.

#### 2A. New Data Classes

Add/update these serializable classes (some may already exist — replace as
needed):

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
    public int pixel_count;
    public BunkerContourVertex[] contour;  // V2: ordered polygon vertices
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

(If `LocalCoord` and `SizeData` already exist and are used elsewhere, keep
them. Just make sure `BunkerData` has `contour` and `BunkersFileData` has
`schema_version`.)

#### 2B. Point-in-Polygon Utility

Add this static method:

```csharp
private static bool IsInsideContour(float px, float pz, Vector2[] contour)
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

#### 2C. New `CreateZoneMeshes()` Method

```csharp
private static void CreateZoneMeshes(TerrainData terrainData, GameObject terrainGO,
    Transform parentRoot, string exportPath, string dataDir, string projectRoot)
{
    string bunkersPath = Path.Combine(exportPath, "bunkers.json");
    if (!File.Exists(bunkersPath))
    {
        Debug.Log("[HoleLiteImporter] No bunkers.json found, skipping");
        return;
    }

    string json = File.ReadAllText(bunkersPath);
    var bunkersFile = JsonUtility.FromJson<BunkersFileData>(json);

    if (bunkersFile.bunkers == null || bunkersFile.bunkers.Length == 0)
    {
        Debug.Log("[HoleLiteImporter] No bunkers in bunkers.json");
        return;
    }

    // Check for V2 contour data
    bool hasContours = !string.IsNullOrEmpty(bunkersFile.schema_version) &&
                       bunkersFile.bunkers[0].contour != null &&
                       bunkersFile.bunkers[0].contour.Length > 0;

    if (!hasContours)
    {
        Debug.LogWarning("[HoleLiteImporter] bunkers.json has no contour data " +
                         "(V1 format). Re-export with updated export-hole.mjs. Skipping bunkers.");
        return;
    }

    float defaultDepth = bunkersFile.depth_m > 0 ? bunkersFile.depth_m : 2.0f;

    var sandMat = CreateBunkerMaterial(dataDir, projectRoot);

    var bunkersRoot = new GameObject("Bunkers");
    bunkersRoot.transform.SetParent(parentRoot);

    var terrain = terrainGO.GetComponent<Terrain>();
    float terrainBaseY = terrainGO.transform.position.y;
    Vector3 terrainPos = terrainGO.transform.position;
    Vector3 terrainSize = terrainData.size;

    // --- Terrain holes ---
    int holesRes = terrainData.holesResolution;
    bool[,] holes = terrainData.GetHoles(0, 0, holesRes, holesRes);

    foreach (var bunker in bunkersFile.bunkers)
    {
        // Apply 90° CCW rotation to contour vertices (same as anchors)
        var worldContour = new Vector2[bunker.contour.Length];
        float sumX = 0, sumZ = 0;
        for (int i = 0; i < bunker.contour.Length; i++)
        {
            float wx = bunker.contour[i].z;  // 90° CCW: worldX = local.z
            float wz = bunker.contour[i].x;  // 90° CCW: worldZ = local.x
            worldContour[i] = new Vector2(wx, wz);
            sumX += wx;
            sumZ += wz;
        }
        float centroidX = sumX / worldContour.Length;
        float centroidZ = sumZ / worldContour.Length;

        // Bounding box of contour (for limiting hole-grid search)
        float cMinX = float.MaxValue, cMaxX = float.MinValue;
        float cMinZ = float.MaxValue, cMaxZ = float.MinValue;
        foreach (var v in worldContour)
        {
            if (v.x < cMinX) cMinX = v.x;
            if (v.x > cMaxX) cMaxX = v.x;
            if (v.y < cMinZ) cMinZ = v.y;
            if (v.y > cMaxZ) cMaxZ = v.y;
        }

        // Cut terrain holes by tracing contour (with small inward margin)
        float marginX = (cMaxX - cMinX) * 0.05f;
        float marginZ = (cMaxZ - cMinZ) * 0.05f;

        int hMinX = Mathf.Clamp(Mathf.FloorToInt(((cMinX + marginX) - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
        int hMaxX = Mathf.Clamp(Mathf.CeilToInt(((cMaxX - marginX) - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
        int hMinZ = Mathf.Clamp(Mathf.FloorToInt(((cMinZ + marginZ) - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);
        int hMaxZ = Mathf.Clamp(Mathf.CeilToInt(((cMaxZ - marginZ) - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);

        for (int hz = hMinZ; hz <= hMaxZ; hz++)
        {
            for (int hx = hMinX; hx <= hMaxX; hx++)
            {
                float cellWorldX = ((hx + 0.5f) / holesRes) * terrainSize.x + terrainPos.x;
                float cellWorldZ = ((hz + 0.5f) / holesRes) * terrainSize.z + terrainPos.z;

                if (IsInsideContour(cellWorldX, cellWorldZ, worldContour))
                    holes[hz, hx] = false;
            }
        }

        // --- Generate contour-shaped mesh ---
        float surfaceY = terrainBaseY + terrain.SampleHeight(
            new Vector3(centroidX, 0, centroidZ));

        float bowlDepth = Mathf.Max(Mathf.Min(defaultDepth, 3f), 0.5f);

        var meshGO = CreateContourMesh(bunker.id, worldContour, centroidX, centroidZ,
            surfaceY, bowlDepth, sandMat, terrain, terrainBaseY);
        meshGO.transform.SetParent(bunkersRoot.transform);
    }

    terrainData.SetHoles(0, 0, holes);

    // Copy bunkers.json to Assets
    string destPath = Path.Combine(projectRoot, dataDir, "bunkers.json");
    File.Copy(bunkersPath, destPath, true);
    AssetDatabase.ImportAsset($"{dataDir}/bunkers.json");

    Debug.Log($"[HoleLiteImporter] Created {bunkersFile.bunkers.Length} contour-based bunker(s)");
}
```

#### 2D. New `CreateContourMesh()` Method

This generates a mesh from the contour polygon with concentric rings
that descend into a bowl shape.

```csharp
private static GameObject CreateContourMesh(int id, Vector2[] contour,
    float centroidX, float centroidZ, float surfaceY, float depth,
    Material sandMat, Terrain terrain, float terrainBaseY)
{
    int n = contour.Length; // number of contour vertices
    if (n < 3)
    {
        Debug.LogWarning($"[HoleLiteImporter] Bunker {id}: contour has < 3 vertices, skipping");
        return new GameObject($"Bunker_{id}_SKIP");
    }

    // Ring layout: rim (100%) → inner (80%) → mid (50%) → deep (20%) → center
    float[] ringScales = { 1.0f, 0.80f, 0.50f, 0.20f };
    float[] ringDepths = { 0.0f, 0.0f, depth * 0.5f, depth * 0.9f };
    // Ring 0 (rim): at terrain height + tiny offset
    // Ring 1 (inner): at terrain height (transition)
    // Ring 2 (mid): half depth
    // Ring 3 (deep): near full depth

    int ringCount = ringScales.Length;
    int vertCount = n * ringCount + 1; // +1 for center
    var vertices = new Vector3[vertCount];
    var uvs = new Vector2[vertCount];

    // Compute bounding box for UV mapping
    float minX = float.MaxValue, maxX = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;
    foreach (var v in contour)
    {
        if (v.x < minX) minX = v.x;
        if (v.x > maxX) maxX = v.x;
        if (v.y < minZ) minZ = v.y;
        if (v.y > maxZ) maxZ = v.y;
    }
    float extentX = Mathf.Max(maxX - minX, 0.1f);
    float extentZ = Mathf.Max(maxZ - minZ, 0.1f);

    for (int r = 0; r < ringCount; r++)
    {
        float scale = ringScales[r];
        float ringY = -ringDepths[r];

        for (int i = 0; i < n; i++)
        {
            // Scale toward centroid
            float wx = centroidX + (contour[i].x - centroidX) * scale;
            float wz = centroidZ + (contour[i].y - centroidZ) * scale;

            float y = ringY;

            // Rim ring (r==0): sample terrain height for seamless edge
            if (r == 0)
            {
                float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                y = (terrainBaseY + terrainH) - surfaceY + 0.02f;
            }
            // Inner ring (r==1): also at terrain height, no offset
            else if (r == 1)
            {
                float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                y = (terrainBaseY + terrainH) - surfaceY;
            }

            // Local space relative to mesh origin (centroid at surface)
            float localX = wx - centroidX;
            float localZ = wz - centroidZ;

            int vi = r * n + i;
            vertices[vi] = new Vector3(localX, y, localZ);
            uvs[vi] = new Vector2(
                (wx - minX) / extentX,
                (wz - minZ) / extentZ);
        }
    }

    // Center vertex — bottom of bowl
    int centerIdx = vertCount - 1;
    vertices[centerIdx] = new Vector3(0, -depth, 0);
    uvs[centerIdx] = new Vector2(0.5f, 0.5f);

    // --- Triangles ---
    // Quads between adjacent rings + fan from last ring to center
    int triCount = n * (ringCount - 1) * 6 + n * 3;
    var triangles = new int[triCount];
    int ti = 0;

    for (int r = 0; r < ringCount - 1; r++)
    {
        for (int i = 0; i < n; i++)
        {
            int curr = r * n + i;
            int next = r * n + (i + 1) % n;
            int currInner = (r + 1) * n + i;
            int nextInner = (r + 1) * n + (i + 1) % n;

            triangles[ti++] = curr;
            triangles[ti++] = currInner;
            triangles[ti++] = next;

            triangles[ti++] = next;
            triangles[ti++] = currInner;
            triangles[ti++] = nextInner;
        }
    }

    // Fan from last ring to center
    int lastRingStart = (ringCount - 1) * n;
    for (int i = 0; i < n; i++)
    {
        int curr = lastRingStart + i;
        int next = lastRingStart + (i + 1) % n;

        triangles[ti++] = curr;
        triangles[ti++] = centerIdx;
        triangles[ti++] = next;
    }

    // --- Build mesh ---
    var mesh = new Mesh();
    mesh.name = $"BunkerContour_{id}";
    mesh.vertices = vertices;
    mesh.triangles = triangles;
    mesh.uv = uvs;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"Bunker_{id}");
    var mf = go.AddComponent<MeshFilter>();
    mf.sharedMesh = mesh;
    var mr = go.AddComponent<MeshRenderer>();
    mr.sharedMaterial = sandMat;

    var mc = go.AddComponent<MeshCollider>();
    mc.sharedMesh = mesh;

    // Position: mesh origin at centroid, at terrain surface height
    go.transform.position = new Vector3(centroidX, surfaceY, centroidZ);

    Debug.Log($"[HoleLiteImporter] Bunker {id}: {n} contour verts, " +
              $"{ringCount} rings, {mesh.vertexCount} total verts");

    return go;
}
```

#### 2E. Update the Call Site

In `ImportLiteHole()`, replace the call:

```csharp
// OLD:
CreateBunkers(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot);

// NEW:
CreateZoneMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot);
```

---

### Verification

**Prerequisites:** Run the UHole Lite TASK.md first to generate V2 contour
data, then re-export hole 01.

Re-import Hole 01: `GOLFIN > Import Hole (Lite) > Hole 01`

- [ ] Terrain is flat (no elevation anywhere)
- [ ] Splatmap paints correct zone textures on flat ground
- [ ] Bunker meshes appear as contour-shaped bowls sunk below surface
- [ ] Bunker shapes match the splatmap zone boundaries (not bounding boxes)
- [ ] No terrain visible inside bunker bowls
- [ ] Rim edges are flush with flat surface (no gaps)
- [ ] No z-fighting
- [ ] Sand texture tiles on bowl surface
- [ ] No console errors
- [ ] Walk camera works on flat terrain
- [ ] Anchor markers visible (at ground level on flat terrain)
- [ ] Try Hole 02 and 03 as well

---

### Do NOT

- Modify UHole Lite scripts (those are handled by separate TASK.md)
- Modify `ApplySplatmap()` (it works fine as-is)
- Modify the debug tools (`TestTerrainLayers`, `TestZoneAlignment`)
- Delete `CreateBunkerMaterial()` — it's still used
- Change bunker positions or zone data

---

## Previous Completed Tasks

✅ DONE: 2026-04-01 — Phase J: Bags Inventory Screen
✅ DONE: 2026-03-31 — Phase I2 Item Use → Club Selection Modal
✅ DONE: 2026-03-31 — Phase I1 Items Inventory
✅ DONE: 2026-03-27 — Phase H Balls Inventory
✅ DONE: 2026-03-26 — Phase G Character Compare stat diff labels
✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients, visual fixes, filter dividers, arrows, viewport, fade, level text
✅ DONE: 2026-03-25 — Club Compare Phase D: ClubCompareController, builder, auto-wire, stat differences
✅ DONE: 2026-03-24 — Project cleanup: GOLFIN menu reorganized, Art/References folders renamed PascalCase, 5 editor scripts archived
✅ DONE: 2026-03-25 — Phase E1 Club Level Up Modal
✅ DONE: 2026-03-26 — Phase E2 Club Repair One-Tap
✅ DONE: 2026-03-26 — Phase E3 Bag Selection Modal
✅ DONE: 2026-03-26 — Phase E3b Bags CSV + Data-Driven Bag Slots
✅ DONE: 2026-03-26 — Phase E4 Bag ↔ Club management
✅ DONE: 2026-03-26 — Phase F Level Up Modal polish (SP allocation UI)
✅ DONE: 2026-03-30 — Fix Club Filter Bar: 8→6 tabs + unified WEDGES
✅ DONE: 2026-03-30 — Fix filter button raycast targets
✅ DONE: 2026-04-06 — Phase K Steps 1-8: HoleImporter + HoleLiteImporter terrain pipeline
✅ DONE: 2026-04-07 — Phase K-Surface Validation: Test Terrain Layers + Test Zone Alignment debug tools
✅ DONE: 2026-04-07 — Phase K-Surface Task 1: Splatmap importer — ApplySplatmap() with 8 terrain layers, zone grid resampling, fringe ring, Gaussian blur, fallback to illustration texture
✅ DONE: 2026-04-07 — V1 Bunker meshes (bounding-box bowls, SetHoles, terrain-following lip, multiple iterations)
✅ DONE: 2026-04-07 — Bunker V2: contour export (traceBorder+RDP+CCW), flat terrain, contour mesh importer (CreateZoneMeshes+CreateContourMesh)
✅ DONE: 2026-04-07 — Contour smoothing: Chaikin subdivision (2 iterations), RDP epsilon 2.0, all 18 holes re-exported with smooth contours
