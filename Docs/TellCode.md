# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Water Option 2: Rasterized Quad + Alpha Mask

**Context:** The contour pipeline (traceBorder → RDP → Chaikin) distorts
large water bodies. RDP straightens curves, Chaikin inflates concave
sections. Fine for small bunkers, but visually wrong on 50-100m+ lakes
with complex coastlines. See `Docs/WATER_FINDINGS.md` for full analysis.

**Solution:** Skip contour extraction entirely for water. Export raw pixel
mask data per region, import as a textured quad in Unity. The mask IS the
zone map — pixel-perfect boundaries, zero distortion.

---

### Part A: Export — Rasterized water masks (`export-hole.mjs`)

**Replace** the `extractWaterContours()` function and update water export
in `exportHole()`. Instead of contour points, output per-region pixel masks.

#### A1. New function: `extractWaterMasks()`

Replace `extractWaterContours()` with this new function. It reuses the
existing flood-fill logic from `extractZoneContours()` but outputs mask
data instead of contours.

```javascript
/**
 * Extract water regions as rasterized masks (no contour simplification).
 * Each region gets a bbox-cropped binary mask for pixel-perfect Unity import.
 */
function extractWaterMasks(zonesData, terrainMeta, minPixels = 50) {
  const grid = Buffer.from(zonesData.grid, 'base64');
  const w = zonesData.source_dimensions.width;
  const h = zonesData.source_dimensions.height;
  const visited = new Uint8Array(w * h);

  const tw = terrainMeta.terrain_width_m;
  const tl = terrainMeta.terrain_length_m;
  const targetZone = 7; // water

  function floodFill(startX, startY) {
    const pixels = [];
    const stack = [[startX, startY]];
    while (stack.length > 0) {
      const [x, y] = stack.pop();
      if (x < 0 || x >= w || y < 0 || y >= h) continue;
      const idx = y * w + x;
      if (visited[idx] || grid[idx] !== targetZone) continue;
      visited[idx] = 1;
      pixels.push([x, y]);
      stack.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
    return pixels;
  }

  const regions = [];

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (grid[y * w + x] === targetZone && !visited[y * w + x]) {
        const pixels = floodFill(x, y);
        if (pixels.length < minPixels) continue;

        // Bounding box in pixel coords
        const xs = pixels.map(p => p[0]);
        const ys = pixels.map(p => p[1]);
        const pxMinX = Math.min(...xs);
        const pxMaxX = Math.max(...xs);
        const pxMinY = Math.min(...ys);
        const pxMaxY = Math.max(...ys);

        const maskW = pxMaxX - pxMinX + 1;
        const maskH = pxMaxY - pxMinY + 1;

        // Build binary mask cropped to bbox
        const mask = new Uint8Array(maskW * maskH); // 0 = not water
        for (const [px, py] of pixels) {
          const mx = px - pxMinX;
          const my = py - pxMinY;
          mask[my * maskW + mx] = 1;
        }

        // Convert mask to base64
        const maskBase64 = Buffer.from(mask).toString('base64');

        // Bounding box in local meter coordinates
        // Same coord system as anchors: (normCoord - 0.5) * terrainSize
        const bboxMinX = parseFloat(((pxMinX / (w - 1) - 0.5) * tw).toFixed(2));
        const bboxMaxX = parseFloat(((pxMaxX / (w - 1) - 0.5) * tw).toFixed(2));
        const bboxMinZ = parseFloat(((pxMinY / (h - 1) - 0.5) * tl).toFixed(2));
        const bboxMaxZ = parseFloat(((pxMaxY / (h - 1) - 0.5) * tl).toFixed(2));

        regions.push({
          id: regions.length + 1,
          pixel_count: pixels.length,
          bbox: {
            min_x: bboxMinX,
            max_x: bboxMaxX,
            min_z: bboxMinZ,
            max_z: bboxMaxZ,
          },
          mask: maskBase64,
          mask_width: maskW,
          mask_height: maskH,
        });
      }
    }
  }

  // Sort by size (largest first), re-assign IDs
  regions.sort((a, b) => b.pixel_count - a.pixel_count);
  regions.forEach((r, i) => { r.id = i + 1; });

  return regions;
}
```

#### A2. Update `exportHole()` — water section

Find the water section in `exportHole()` (starts with `// --- Build water.json ---`).
Replace it with:

```javascript
  // --- Build water.json ---
  const water = extractWaterMasks(zonesData, terrainMeta, 50);

  const waterOutput = {
    schema_version: '2.0.0',
    hole_number: holeNumber,
    water_count: water.length,
    water: water,
  };

  fs.writeFileSync(
    path.join(exportDir, 'water.json'),
    JSON.stringify(waterOutput, null, 2),
    'utf-8'
  );

  // Log water mask stats
  if (water.length > 0) {
    const maskStats = water.map(w =>
      `#${w.id}: ${w.mask_width}x${w.mask_height}px (${w.pixel_count}px)`
    ).join(', ');
    console.log(`  Water masks: ${maskStats}`);
  }
```

Also remove `depth_m` from the output — flat planes don't need it.

#### A3. Delete old function

Delete `extractWaterContours()` entirely. It's no longer called.

**Do NOT** modify `extractZoneContours()`, `traceBorder()`,
`simplifyPolygon()`, `smoothPolygon()`, or `ensureCCW()` — bunkers
and greens still use them.

---

### Part B: Import — Rasterized quad meshes (`HoleLiteImporter.cs`)

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

#### B1. Update data classes

Find the existing `WaterFileData` and `WaterRegionData` classes (they're
at the bottom of the file or inline). Replace them to match the new
schema. If they don't exist as named classes, add them:

```csharp
[System.Serializable]
private class WaterFileData
{
    public string schema_version;
    public int hole_number;
    public int water_count;
    public WaterRegionData[] water;
}

[System.Serializable]
private class WaterRegionData
{
    public int id;
    public int pixel_count;
    public WaterBBox bbox;
    public string mask;        // base64-encoded binary mask
    public int mask_width;
    public int mask_height;
}

[System.Serializable]
private class WaterBBox
{
    public float min_x;
    public float max_x;
    public float min_z;
    public float max_z;
}
```

Remove `contour` field references from any old water data class.

#### B2. Replace `CreateWaterMeshes()`

Replace the entire method. The new version reads bbox + mask, creates
quads with alpha-cutout textures.

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

    float waterY = 0.05f; // slightly above flat terrain

    foreach (var water in waterFile.water)
    {
        if (string.IsNullOrEmpty(water.mask) || water.mask_width < 1 || water.mask_height < 1)
            continue;

        // Decode mask
        byte[] maskBytes = System.Convert.FromBase64String(water.mask);
        int mw = water.mask_width;
        int mh = water.mask_height;

        if (maskBytes.Length != mw * mh)
        {
            Debug.LogWarning($"[HoleLiteImporter] Water {water.id}: mask size mismatch " +
                             $"({maskBytes.Length} != {mw}x{mh}={mw * mh}), skipping");
            continue;
        }

        // Apply 90° CCW rotation to bbox (same as anchors/contours)
        // Pre-rotation bbox is in (x, z) = (width_axis, length_axis)
        // 90° CCW: worldX = local.z, worldZ = local.x
        float worldMinX = water.bbox.min_z;
        float worldMaxX = water.bbox.max_z;
        float worldMinZ = water.bbox.min_x;
        float worldMaxZ = water.bbox.max_x;

        float quadW = worldMaxX - worldMinX;
        float quadH = worldMaxZ - worldMinZ;
        float centerX = (worldMinX + worldMaxX) / 2f;
        float centerZ = (worldMinZ + worldMaxZ) / 2f;

        // --- Generate alpha mask texture ---
        // After 90° CCW: mask rows (Y) map to world Z, mask cols (X) map to world X
        // But since we rotated, we need to transpose the mask:
        // mask pixel (mx, my) → rotated texture pixel (my, mw-1-mx)
        int texW = mh;  // rotated dimensions
        int texH = mw;
        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // crisp pixel edges
        tex.wrapMode = TextureWrapMode.Clamp;

        Color waterColor = new Color(0.18f, 0.40f, 0.58f, 1.0f);
        Color clearColor = new Color(0f, 0f, 0f, 0f);

        for (int my = 0; my < mh; my++)
        {
            for (int mx = 0; mx < mw; mx++)
            {
                bool isWater = maskBytes[my * mw + mx] == 1;
                // 90° CCW rotation of mask pixels
                int tx = my;
                int ty = mw - 1 - mx;
                tex.SetPixel(tx, ty, isWater ? waterColor : clearColor);
            }
        }
        tex.Apply();

        // Save texture as asset
        string texPath = $"{dataDir}/WaterMask_{water.id}.png";
        string fullTexPath = Path.Combine(projectRoot, texPath);
        File.WriteAllBytes(fullTexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(texPath);

        // Configure texture importer
        var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // --- Create material (alpha cutout) ---
        string matPath = $"{dataDir}/WaterSurface_{water.id}.mat";
        var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existingMat != null)
            AssetDatabase.DeleteAsset(matPath);

        var mat = new Material(GetLitShader());
        mat.name = $"WaterSurface_{water.id}";
        mat.mainTexture = savedTex;

        // Alpha cutout mode
        mat.SetFloat("_Surface", 0); // 0 = Opaque — we use cutout via AlphaClip
        mat.SetFloat("_AlphaClip", 1);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.SetFloat("_Smoothness", 0.85f);
        mat.SetFloat("_Metallic", 0.05f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = 2450; // AlphaTest queue

        AssetDatabase.CreateAsset(mat, matPath);

        // --- Create quad mesh ---
        var vertices = new Vector3[]
        {
            new Vector3(-quadW / 2f, 0f, -quadH / 2f),
            new Vector3( quadW / 2f, 0f, -quadH / 2f),
            new Vector3( quadW / 2f, 0f,  quadH / 2f),
            new Vector3(-quadW / 2f, 0f,  quadH / 2f),
        };
        var uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
        };
        var triangles = new int[] { 0, 2, 1, 0, 3, 2 };

        var mesh = new Mesh();
        mesh.name = $"WaterQuad_{water.id}";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject($"Water_{water.id}");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;

        // MeshCollider uses the same quad — ball collision covers full bbox,
        // gameplay logic uses SurfaceMarker + zone lookup for precision
        go.AddComponent<MeshCollider>().sharedMesh = mesh;

        go.transform.position = new Vector3(centerX, waterY, centerZ);

        var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
        marker.surfaceType = Golfin.Course.SurfaceType.Water;

        go.transform.SetParent(waterRoot.transform);

        Debug.Log($"[HoleLiteImporter] Water {water.id}: quad {quadW:F1}x{quadH:F1}m, " +
                  $"mask {texW}x{texH}px, pos ({centerX:F1}, {waterY}, {centerZ:F1})");
    }

    // Copy water.json to Assets
    string destPath = Path.Combine(projectRoot, dataDir, "water.json");
    File.Copy(waterPath, destPath, true);
    AssetDatabase.ImportAsset($"{dataDir}/water.json");

    Debug.Log($"[HoleLiteImporter] Created {waterFile.water.Length} water quad(s)");
}
```

#### B3. Delete old methods

**Delete** `CreateFlatWaterMesh()` and `CreateWaterMaterial()` entirely.
They are no longer called.

#### B4. Remove old data classes

If old water data classes reference `contour` fields (like
`WaterContourPoint` or similar), delete them. The new `WaterRegionData`
class has `mask`, `bbox`, `mask_width`, `mask_height` instead.

---

### Important Notes

**Mask rotation:** The zone grid is in the UHole Lite coordinate system.
The importer applies 90° CCW rotation to everything (anchors, bunker
contours, green contours). The mask needs the same rotation. The spec
handles this by transposing the mask when building the Texture2D:
`mask(mx, my) → tex(my, mw-1-mx)`.

**Mask coordinate mapping:** The bbox in water.json uses the SAME local
meter coordinate system as anchors/contours (pre-rotation). The importer
applies the 90° CCW swap: `worldX = local.z, worldZ = local.x`. This is
consistent with how bunker and green contours are rotated.

**FilterMode.Point:** Keeps pixel edges crisp — no blurring between
water and non-water pixels. This is intentional for pixel-perfect match.

**MeshCollider on the quad:** The quad covers the full bounding box, so
the collider is larger than the visible water. For gameplay, SurfaceMarker
detection is the primary mechanism — the collider just prevents the ball
from falling through.

---

### Verification

1. Re-export Hole 12: `node scripts/export-hole.mjs lomond-country-club 12`
   - [ ] Console shows mask stats (e.g. `Water masks: #1: 45x82px (2340px)`)
   - [ ] `water.json` has `schema_version: "2.0.0"`, `bbox`, `mask`, `mask_width`, `mask_height`
   - [ ] No `contour` field in water.json
   - [ ] No `depth_m` in water.json

2. Re-import Hole 12 in Unity: `GOLFIN > Import Hole (Lite) > Hole 12`
   - [ ] Water appears as blue shapes matching the zone map exactly
   - [ ] **No shape distortion** — edges follow zone grid pixels
   - [ ] Edges are crisp (no blur between water/non-water)
   - [ ] Each water region has its own `Water_N` GameObject
   - [ ] Each has `SurfaceMarker` with `SurfaceType.Water`
   - [ ] Each has `MeshCollider`
   - [ ] `WaterMask_N.png` and `WaterSurface_N.mat` in data folder
   - [ ] Bunkers and greens still work (no regression)
   - [ ] No console errors

3. Re-export and re-import Hole 01 (no water):
   - [ ] Export skips water gracefully
   - [ ] Import skips water gracefully (`No water.json found` or `No water`)

### Do NOT

- Modify `extractZoneContours()`, `traceBorder()`, `simplifyPolygon()`,
  `smoothPolygon()`, or `ensureCCW()` — bunkers/greens still use them
- Modify bunker or green mesh generation
- Modify the splatmap pipeline or `ZoneToLayer()`
- Change heightmap resolution or terrain holes logic

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
✅ DONE: 2026-04-07 — Phase K-Surface Task 1: Splatmap importer
✅ DONE: 2026-04-07 — V1 Bunker meshes (bounding-box bowls — dead end)
✅ DONE: 2026-04-07 — Bunker V2: contour export + flat terrain + contour mesh importer
✅ DONE: 2026-04-07 — Bunker V2 polish: Chaikin smoothing, closed-polygon RDP, 1025 heightmap res, sand glow fix
✅ DONE: 2026-04-07 — Green zone meshes: contour export, raised mesh, SurfaceMarker component
✅ DONE: 2026-04-07 — Green collar: semi-rough slope as separate mesh, collar/surface split
✅ DONE: 2026-04-07 — Phase 2 Water Zone Meshes: water contour export + basin mesh importer + transparent material
✅ DONE: 2026-04-07 — Morphological close for water fragments + re-export. Hole 12: 23→16 regions (radius=3 bridges some gaps, wider gaps persist)
✅ DONE: 2026-04-07 — Water tree absorption + dilate-only (replaced morphological close)
✅ DONE: 2026-04-07 — Fix water border gaps: rim expanded to 105%, terrain cut at 100% (was 90%)
✅ DONE: 2026-04-07 — Simplified water to flat plane: no basin, no terrain holes, opaque material
✅ DONE: 2026-04-07 — Water Option 2: Rasterized quad + alpha mask (pixel-perfect water boundaries, no contour distortion)
