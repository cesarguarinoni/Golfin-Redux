# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task — Phase K-Surface: Validation Pass

**Goal:** Two quick tests to validate the splatmap approach BEFORE building
the full pipeline. Total time budget: ~20 minutes.

**Context:** Read `Docs/PHASE_K_SURFACE_SPEC.md` for the full plan. This task
validates the two riskiest assumptions before we invest in the full implementation.

---

### Test 1: Manual TerrainLayer Check

Add 3 TerrainLayers to the existing Hole 01 terrain **programmatically** (not
the full splatmap pipeline — just enough to see if the textures look right at
this terrain scale).

**File to modify:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

Add a new menu item:

```csharp
[MenuItem("GOLFIN/Debug/Test Terrain Layers")]
public static void TestTerrainLayers()
```

This method:

1. Gets the active scene's Terrain component
2. Loads 3 textures from `Assets/Courses/Textures_2025(JPG)/`:
   - `T_Fairway_Light.jpg` + `T_Fairway_Normal.jpg`
   - `T_Rough_Albedo.jpg` + `T_Rough_Normal.jpg`
   - `T_Green_Albedo.jpg` + `T_Green_Normal.jpg`
3. Creates 3 `TerrainLayer` assets (saved to the terrain's data folder)
4. Sets tileSize for each: fairway=5m, rough=8m, green=3m
5. Assigns all 3 as the terrain's layers
6. Paints a simple test pattern into the alphamap:
   - Default everything to layer 1 (rough)
   - Paint a horizontal stripe in the middle third as layer 0 (fairway)
   - Paint a small circle at terrain center as layer 2 (green)

```csharp
public static void TestTerrainLayers()
{
    var terrain = Object.FindObjectOfType<Terrain>();
    if (terrain == null)
    {
        EditorUtility.DisplayDialog("Error", "No terrain in scene. Import a hole first.", "OK");
        return;
    }

    var terrainData = terrain.terrainData;
    string texDir = "Assets/Courses/Textures_2025(JPG)";
    
    // Find textures by searching in the directory
    // Use AssetDatabase.FindAssets to locate them
    var fairwayLayer = CreateTestLayer(texDir, "T_Fairway_Light", "T_Fairway_Normal", 5f);
    var roughLayer = CreateTestLayer(texDir, "T_Rough_Albedo", "T_Rough_Normal", 8f);
    var greenLayer = CreateTestLayer(texDir, "T_Green_Albedo", "T_Green_Normal", 3f);

    if (fairwayLayer == null || roughLayer == null || greenLayer == null)
    {
        Debug.LogError("[TestTerrainLayers] Could not find all textures. Check Assets/Courses/Textures_2025(JPG)/");
        return;
    }

    // Save layer assets
    string dataDir = "Assets/Golf/Courses/lomond-country-club/Data/hole-01";
    EnsureDirectory(Path.Combine(Path.GetDirectoryName(Application.dataPath), dataDir));
    
    SaveLayerAsset(fairwayLayer, $"{dataDir}/TestLayer_Fairway.asset");
    SaveLayerAsset(roughLayer, $"{dataDir}/TestLayer_Rough.asset");
    SaveLayerAsset(greenLayer, $"{dataDir}/TestLayer_Green.asset");

    terrainData.terrainLayers = new TerrainLayer[] { fairwayLayer, roughLayer, greenLayer };

    // Paint test pattern
    int alphaRes = terrainData.alphamapResolution;
    float[,,] alphamap = new float[alphaRes, alphaRes, 3];

    for (int y = 0; y < alphaRes; y++)
    {
        for (int x = 0; x < alphaRes; x++)
        {
            float fx = (float)x / alphaRes;
            float fy = (float)y / alphaRes;

            // Default: rough
            int layer = 1;

            // Middle third horizontal stripe: fairway
            if (fy > 0.33f && fy < 0.66f)
                layer = 0;

            // Small circle at center: green
            float dx = fx - 0.5f;
            float dy = fy - 0.5f;
            if (dx * dx + dy * dy < 0.02f)
                layer = 2;

            alphamap[y, x, layer] = 1.0f;
        }
    }

    terrainData.SetAlphamaps(0, 0, alphamap);
    Debug.Log($"[TestTerrainLayers] Applied 3 test layers to terrain (alphamap {alphaRes}x{alphaRes})");
    Debug.Log("[TestTerrainLayers] Pattern: rough (everywhere) + fairway (middle stripe) + green (center circle)");
    Debug.Log("[TestTerrainLayers] CHECK: Do textures tile well? Are sizes reasonable? Walk around in play mode.");
}

private static TerrainLayer CreateTestLayer(string texDir, string albedoName, string normalName, float tileSize)
{
    var albedo = FindTextureInDir(texDir, albedoName);
    var normal = FindTextureInDir(texDir, normalName);
    
    if (albedo == null)
    {
        Debug.LogWarning($"Could not find texture: {albedoName} in {texDir}");
        return null;
    }

    var layer = new TerrainLayer();
    layer.diffuseTexture = albedo;
    if (normal != null)
        layer.normalMapTexture = normal;
    layer.tileSize = new Vector2(tileSize, tileSize);
    layer.tileOffset = Vector2.zero;
    return layer;
}

private static Texture2D FindTextureInDir(string dir, string namePrefix)
{
    string[] guids = AssetDatabase.FindAssets(namePrefix, new[] { dir });
    foreach (var guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        // Make sure the filename actually starts with the prefix
        // (FindAssets can match partial/fuzzy)
        string fileName = Path.GetFileNameWithoutExtension(path);
        if (fileName == namePrefix || fileName.StartsWith(namePrefix + "."))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
        }
    }
    return null;
}

private static void SaveLayerAsset(TerrainLayer layer, string path)
{
    var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
    if (existing != null)
        AssetDatabase.DeleteAsset(path);
    AssetDatabase.CreateAsset(layer, path);
}
```

**NOTE:** `FindAssets` can be fuzzy. The `FindTextureInDir` helper checks
that the filename actually matches the prefix exactly. Be careful that
`T_Fairway_Light` doesn't also match `T_Fairway_Light_Something` — in this
texture set it won't, but the check is there for safety.

After running: Kai will open Scene view, walk around in Play mode, and
evaluate whether the tiling textures look acceptable at this terrain scale.

---

### Test 2: Zone Grid Alignment Debug

Add a second debug menu item that reads `zones.json`, computes the centroid
of the green zone, applies the 90° CCW coordinate transform, and places
a debug sphere at that position on the terrain.

```csharp
[MenuItem("GOLFIN/Debug/Test Zone Alignment")]
public static void TestZoneAlignment()
```

This method:

1. Finds the terrain in the active scene
2. Reads `zones.json` from the export folder:
   ```
   Tools/UHoleLite/output/lomond-country-club/export/hole-01/zones.json
   ```
3. Parses the base64 grid
4. Finds the centroid of zone index 2 (green):
   ```csharp
   // Parse zones.json
   string projectRoot = Path.GetDirectoryName(Application.dataPath);
   string zonesPath = Path.Combine(projectRoot, "Tools", "UHoleLite", "output",
       "lomond-country-club", "export", "hole-01", "zones.json");
   string zonesJson = File.ReadAllText(zonesPath);
   var zonesData = JsonUtility.FromJson<ZonesData>(zonesJson);
   
   byte[] grid = System.Convert.FromBase64String(zonesData.grid);
   int w = zonesData.source_dimensions.width;
   int h = zonesData.source_dimensions.height;
   
   // Find green centroid (zone 2)
   float sumX = 0, sumY = 0;
   int greenCount = 0;
   for (int gy = 0; gy < h; gy++)
   {
       for (int gx = 0; gx < w; gx++)
       {
           if (grid[gy * w + gx] == 2) // green
           {
               sumX += gx;
               sumY += gy;
               greenCount++;
           }
       }
   }
   
   if (greenCount == 0)
   {
       Debug.LogError("[TestZoneAlignment] No green zone pixels found!");
       return;
   }
   
   float centroidGX = sumX / greenCount;  // zone grid X
   float centroidGY = sumY / greenCount;  // zone grid Y
   float normX = centroidGX / (w - 1);    // 0..1
   float normY = centroidGY / (h - 1);    // 0..1
   
   Debug.Log($"[TestZoneAlignment] Green centroid: grid({centroidGX:F1}, {centroidGY:F1}), " +
             $"norm({normX:F3}, {normY:F3}), {greenCount} pixels");
   ```

5. Applies the 90° CCW transform to get terrain-space position.

   The current `HoleLiteImporter` uses this rotation:
   - Terrain X dimension = zone grid's height (terrain_length_m)
   - Terrain Z dimension = zone grid's width (terrain_width_m)
   - Heightmap: `heights[hx, hy]` (swapped from raw `[hy, hx]`)
   - Anchors: `worldPos = new Vector3(anchor.local.z, 0, anchor.local.x)`
   - Anchors in export: `local.x = (normX - 0.5) * terrain_width_m`, `local.z = (normY - 0.5) * terrain_length_m`
   
   So the zone grid normalized coords `(normX, normY)` map to terrain world coords:
   ```csharp
   // Terrain dimensions after 90° CCW swap
   float terrainX = manifest.terrain.terrain_length_m;  // = 631.2
   float terrainZ = manifest.terrain.terrain_width_m;   // = 523.4
   
   // The anchor export does: local.x = (normX - 0.5) * terrain_width_m
   //                         local.z = (normY - 0.5) * terrain_length_m
   // Then the importer places at: Vector3(local.z, 0, local.x)
   // So: worldX = (normY - 0.5) * terrain_length_m
   //     worldZ = (normX - 0.5) * terrain_width_m
   
   float worldX = (normY - 0.5f) * manifest.terrain.terrain_length_m;
   float worldZ = (normX - 0.5f) * manifest.terrain.terrain_width_m;
   ```

6. Creates a debug sphere at that position:
   ```csharp
   // Clean up any previous debug spheres
   var old = GameObject.Find("DEBUG_GreenCentroid");
   if (old != null) Object.DestroyImmediate(old);
   
   var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
   sphere.name = "DEBUG_GreenCentroid";
   sphere.transform.localScale = new Vector3(10f, 10f, 10f);  // big enough to see
   
   float terrainHeight = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));
   float terrainBase = terrain.transform.position.y;
   sphere.transform.position = new Vector3(worldX, terrainBase + terrainHeight + 10f, worldZ);
   
   var renderer = sphere.GetComponent<Renderer>();
   var mat = new Material(Shader.Find("Standard"));
   mat.color = Color.magenta;
   renderer.sharedMaterial = mat;
   
   Debug.Log($"[TestZoneAlignment] Placed DEBUG_GreenCentroid at world ({worldX:F1}, {terrainBase + terrainHeight + 10f:F1}, {worldZ:F1})");
   Debug.Log("[TestZoneAlignment] CHECK: Is the magenta sphere on or near the green area?");
   ```

7. Also place debug spheres for each **zone type centroid** to verify more broadly:
   
   Do the same centroid calculation for zones 1 (fairway), 6 (bunker), and
   10 (tee_box). Place smaller colored spheres:
   - Fairway centroid: green sphere
   - Bunker centroid: yellow sphere
   - Tee centroid: white sphere
   
   ```csharp
   // Repeat centroid calc for zones 1, 6, 10
   int[] debugZones = { 1, 6, 10 };
   string[] debugNames = { "Fairway", "Bunker", "TeeBox" };
   Color[] debugColors = { Color.green, Color.yellow, Color.white };
   float[] debugSizes = { 8f, 6f, 6f };
   
   for (int i = 0; i < debugZones.Length; i++)
   {
       float sx = 0, sy = 0;
       int count = 0;
       for (int gy2 = 0; gy2 < h; gy2++)
       {
           for (int gx2 = 0; gx2 < w; gx2++)
           {
               if (grid[gy2 * w + gx2] == debugZones[i])
               {
                   sx += gx2;
                   sy += gy2;
                   count++;
               }
           }
       }
       if (count == 0) continue;
       
       float cnx = (sx / count) / (w - 1);
       float cny = (sy / count) / (h - 1);
       float wx = (cny - 0.5f) * manifest.terrain.terrain_length_m;
       float wz = (cnx - 0.5f) * manifest.terrain.terrain_width_m;
       
       var oldSph = GameObject.Find($"DEBUG_{debugNames[i]}Centroid");
       if (oldSph != null) Object.DestroyImmediate(oldSph);
       
       var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
       sph.name = $"DEBUG_{debugNames[i]}Centroid";
       sph.transform.localScale = Vector3.one * debugSizes[i];
       float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
       sph.transform.position = new Vector3(wx, terrainBase + th + debugSizes[i], wz);
       var r = sph.GetComponent<Renderer>();
       var m = new Material(Shader.Find("Standard"));
       m.color = debugColors[i];
       r.sharedMaterial = m;
       
       Debug.Log($"[TestZoneAlignment] {debugNames[i]} centroid: norm({cnx:F3}, {cny:F3}) → world({wx:F1}, {wz:F1}), {count}px");
   }
   ```

**Data classes needed:** Add `ZonesData` and `ZoneSourceDimensions` to
`HoleManifestData.cs` if they don't already exist:

```csharp
[System.Serializable]
public class ZonesData
{
    public int hole_number;
    public ZoneSourceDimensions source_dimensions;
    public string grid;
}

[System.Serializable]
public class ZoneSourceDimensions
{
    public int width;
    public int height;
}
```

Also need to read `hole-manifest.json` for the terrain dimensions. The test
can load it from the same export folder:
```csharp
string manifestPath = Path.Combine(projectRoot, "Tools", "UHoleLite", "output",
    "lomond-country-club", "export", "hole-01", "hole-manifest.json");
string manifestJson = File.ReadAllText(manifestPath);
var manifest = JsonUtility.FromJson<HoleManifest>(manifestJson);
```

---

### What To Check After Running

**Test 1 results (Kai evaluates visually):**
- Do fairway/rough/green textures tile naturally, or are repeating patterns obvious?
- Are the tile sizes (5m/8m/3m) reasonable? Too big → blurry. Too small → obvious tiling.
- Do the normal maps add visible detail, or are they washed out?
- Is there a visible seam at the transition between layers?
- How does it look from WalkCamera height vs. top-down Scene view?

**Test 2 results (Kai evaluates alignment):**
- Is the magenta sphere (green centroid) on or near the green area of the terrain?
- Is the green sphere (fairway centroid) along the fairway path?
- Is the yellow sphere (bunker centroid) near a bunker area?
- Is the white sphere (tee centroid) near the tee markers?
- If ALL spheres are offset in the same direction → the transform formula is wrong
  but consistent (easy fix: adjust the mapping)
- If spheres are scattered randomly → the transform is fundamentally wrong
  (need to re-derive the rotation mapping)

**Pass criteria:**
- Test 1: textures look acceptable (we can tune tile sizes later)
- Test 2: debug spheres land on or very near their expected zones

**If either test fails**, we stop and fix before building the full splatmap pipeline.

---

### Files to Create/Modify

| File | Action |
|---|---|
| `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` | Add `TestTerrainLayers()` and `TestZoneAlignment()` menu items |
| `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs` | Add `ZonesData` and `ZoneSourceDimensions` classes |

### Do NOT

- Build the full splatmap pipeline yet
- Modify the existing `ImportLiteHole()` method
- Change UHole Lite scripts
- Touch anything outside the two debug menu items

---

### Verification

- [ ] Menu item `GOLFIN > Debug > Test Terrain Layers` exists and runs without errors
- [ ] 3 terrain layers visible on terrain (rough everywhere, fairway stripe, green circle)
- [ ] Textures have normal maps applied (visible surface detail)
- [ ] Menu item `GOLFIN > Debug > Test Zone Alignment` exists and runs without errors
- [ ] Magenta sphere placed on terrain (visible in Scene view)
- [ ] Debug log prints green centroid coordinates and pixel count
- [ ] All 4 debug spheres placed (green, fairway, bunker, tee centroids)
- [ ] Re-running either test replaces previous results cleanly

---

## Previous Completed Tasks

✅ DONE: 2026-04-01 — Phase J: Bags Inventory Screen (BagCarouselController, BagDetailPanel, BagClubModalController, BagClubCard, BagThumbnailCard, localization keys, CSV updates)

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
✅ DONE: 2026-04-06 — Phase K Step 1: HoleImporter.cs, HoleManifestData.cs, HoleMetadata.cs, WalkCamera.cs — terrain from heightmap, stitched satellite texture, tee anchor markers, WASD walk camera (New Input System)
✅ DONE: 2026-04-06 — Phase K Step 2: Aerial texture alignment fix — crop stitched tile grid to hole_bounds before applying to terrain
✅ DONE: 2026-04-06 — Phase K Step 3: North/south axis fix — flip heightmap rows and aerial texture vertically so north=-Z matches UHole anchor convention
✅ DONE: 2026-04-06 — Phase K Step 4: Horizontal flip (negate X for anchors/camera, flip heightmap+texture on X axis) + alignment debug logging
✅ DONE: 2026-04-06 — Phase K Step 4b: Separated texture flips into two explicit passes (vertical then horizontal) for clarity; heightmap keeps heights[res-1-y, res-1-x]
✅ DONE: 2026-04-06 — Phase K Step 5: Reverted all X-axis flips; back to vertical-only flip for heightmap and texture
✅ DONE: 2026-04-06 — Phase K Step 6: Replaced crop-based alignment with UV offset mapping — full stitched texture + tileSize/tileOffset computed from grid geo bounds
✅ DONE: 2026-04-06 — Phase K Step 7: Definitive texture alignment — pixel-by-pixel geo sampling from tiles using same bounds as heightmap, tileSize=terrain, tileOffset=zero
✅ DONE: 2026-04-06 — Phase K Step 8: Fixed texture horizontal flip — reversed U sampling direction (1-u) in ApplyAerialTexture() so satellite features align with anchor positions
✅ DONE: 2026-04-07 — Phase K-Surface Validation: Added GOLFIN > Debug > Test Terrain Layers (3 tiling textures with alphamap pattern) and Test Zone Alignment (debug spheres at zone centroids with 90° CCW transform)
