# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task — Phase K-Surface Task 1: Splatmap Importer

**Goal:** Replace the illustration texture on the terrain with 8 tiling golf
textures (fairway, green, semi-rough, rough, bunker, tee, cart path, fringe),
auto-painted from the UHole Lite zone grid via a splatmap.

**Context:** Read `Docs/PHASE_K_SURFACE_SPEC.md` for the full architecture.
The validation tests (`GOLFIN > Debug > Test Terrain Layers` and
`GOLFIN > Debug > Test Zone Alignment`) confirmed:
- Textures tile acceptably at terrain scale ✅
- Zone-to-terrain coordinate mapping is correct ✅

Now build the real thing.

---

### Overview

Modify `HoleLiteImporter.cs` to replace `ApplyTexture()` with `ApplySplatmap()`.
The new method reads `zones.json` from the export folder, generates a smoothed
alphamap, and applies 8 TerrainLayers using the existing textures in
`Assets/Courses/Textures_2025(JPG)/`.

---

### Step 1: Data Classes

Add to `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs` (if not
already added by the validation task):

```csharp
[System.Serializable]
public class ZonesData
{
    public int hole_number;
    public ZoneSourceDimensions source_dimensions;
    public string grid; // base64-encoded uint8 array
}

[System.Serializable]
public class ZoneSourceDimensions
{
    public int width;
    public int height;
}
```

---

### Step 2: Replace ApplyTexture with ApplySplatmap

In `HoleLiteImporter.cs`, replace the call to `ApplyTexture(...)` inside
`ImportLiteHole()` with a call to `ApplySplatmap(...)`. Keep the old
`ApplyTexture` method in the file (commented out or renamed to
`ApplyTextureIllustration`) so we can toggle back for debugging.

Change in `ImportLiteHole()`:
```csharp
// OLD:
// ApplyTexture(terrainData, manifest, exportPath, dataDir, holeId, projectRoot);

// NEW:
ApplySplatmap(terrainData, manifest, exportPath, dataDir, holeId, projectRoot);
```

---

### Step 3: ApplySplatmap Method

Add this new method to `HoleLiteImporter.cs`. It does 6 things:
1. Parse zone grid from zones.json
2. Resample to alphamap resolution
3. Generate synthetic fringe ring around greens
4. Build raw alphamap (hard per-pixel zone assignments)
5. Gaussian blur each channel + re-normalize (soft transitions)
6. Create TerrainLayers and apply

```csharp
private static void ApplySplatmap(TerrainData terrainData, HoleManifest manifest,
    string exportPath, string dataDir, string holeId, string projectRoot)
{
    // --- 1. Parse zone grid ---
    string zonesPath = Path.Combine(exportPath, "zones.json");
    if (!File.Exists(zonesPath))
    {
        Debug.LogWarning("[HoleLiteImporter] zones.json not found, falling back to illustration texture");
        ApplyTextureIllustration(terrainData, manifest, exportPath, dataDir, holeId, projectRoot);
        return;
    }

    string zonesJson = File.ReadAllText(zonesPath);
    var zonesData = JsonUtility.FromJson<ZonesData>(zonesJson);
    byte[] grid = System.Convert.FromBase64String(zonesData.grid);
    int zoneW = zonesData.source_dimensions.width;
    int zoneH = zonesData.source_dimensions.height;

    Debug.Log($"[HoleLiteImporter] Zone grid: {zoneW}x{zoneH}, {grid.Length} bytes");

    // --- 2. Resample to alphamap resolution ---
    int alphaRes = 256;
    terrainData.alphamapResolution = alphaRes;

    byte[] resampledZones = new byte[alphaRes * alphaRes];
    for (int ay = 0; ay < alphaRes; ay++)
    {
        for (int ax = 0; ax < alphaRes; ax++)
        {
            float fx = (float)ax / (alphaRes - 1);
            float fy = (float)ay / (alphaRes - 1);

            // Apply the same 90° CCW rotation as the heightmap/texture.
            // The heightmap uses heights[hx, hy] (swapped indices).
            // Anchors use: worldPos = Vector3(local.z, 0, local.x)
            // where local.x = (normX - 0.5) * terrain_width_m
            //       local.z = (normY - 0.5) * terrain_length_m
            //
            // Alphamap [ay, ax]:
            //   Unity docs: alphamap[z_index, x_index, layer]
            //   ay maps along terrain Z, ax maps along terrain X
            //
            // Terrain X corresponds to zone grid Y (after 90° CCW)
            // Terrain Z corresponds to zone grid X (after 90° CCW)
            //
            // So: zone grid X ← terrain Z fraction (fy... but need to check direction)
            //     zone grid Y ← terrain X fraction (fx... but need to check direction)
            //
            // The validation test confirmed this mapping works:
            //   worldX = (normY - 0.5) * terrain_length_m
            //   worldZ = (normX - 0.5) * terrain_width_m
            //
            // Alphamap ax → terrain X fraction → zone normY
            // Alphamap ay → terrain Z fraction → zone normX

            int gx = Mathf.Clamp(Mathf.RoundToInt(fy * (zoneW - 1)), 0, zoneW - 1);
            int gy = Mathf.Clamp(Mathf.RoundToInt(fx * (zoneH - 1)), 0, zoneH - 1);

            resampledZones[ay * alphaRes + ax] = grid[gy * zoneW + gx];
        }
    }

    // --- 3. Generate fringe ring around greens ---
    int fringeRadius = 3;
    bool[] greenMask = new bool[alphaRes * alphaRes];
    for (int i = 0; i < resampledZones.Length; i++)
        greenMask[i] = (resampledZones[i] == 2);

    bool[] dilatedGreen = DilateMask(greenMask, alphaRes, alphaRes, fringeRadius);

    bool[] fringeMask = new bool[alphaRes * alphaRes];
    for (int i = 0; i < fringeMask.Length; i++)
    {
        if (dilatedGreen[i] && !greenMask[i])
        {
            int zone = resampledZones[i];
            // Only place fringe on adjacent playable surfaces
            if (zone == 1 || zone == 3 || zone == 4)
                fringeMask[i] = true;
        }
    }

    // --- 4. Build raw alphamap ---
    int layerCount = 8;
    float[,,] alphamap = new float[alphaRes, alphaRes, layerCount];

    for (int ay = 0; ay < alphaRes; ay++)
    {
        for (int ax = 0; ax < alphaRes; ax++)
        {
            int idx = ay * alphaRes + ax;
            int layer;

            if (fringeMask[idx])
                layer = 7; // fringe
            else
                layer = ZoneToLayer(resampledZones[idx]);

            alphamap[ay, ax, layer] = 1.0f;
        }
    }

    // --- 5. Gaussian blur + re-normalize ---
    int blurRadius = 3;
    float sigma = blurRadius / 2.0f;

    for (int l = 0; l < layerCount; l++)
    {
        float[,] channel = ExtractChannel(alphamap, alphaRes, layerCount, l);
        float[,] blurred = GaussianBlur2D(channel, alphaRes, blurRadius, sigma);
        SetChannel(alphamap, alphaRes, layerCount, l, blurred);
    }

    // Re-normalize so weights sum to 1.0
    for (int ay = 0; ay < alphaRes; ay++)
    {
        for (int ax = 0; ax < alphaRes; ax++)
        {
            float sum = 0f;
            for (int l = 0; l < layerCount; l++)
                sum += alphamap[ay, ax, l];

            if (sum > 0.001f)
            {
                for (int l = 0; l < layerCount; l++)
                    alphamap[ay, ax, l] /= sum;
            }
            else
            {
                alphamap[ay, ax, 3] = 1.0f; // fallback: rough
            }
        }
    }

    // --- 6. Create TerrainLayers and apply ---
    string texDir = "Assets/Courses/Textures_2025(JPG)";

    string[] albedoNames = {
        "T_Fairway_Light",      // 0 fairway
        "T_Green_Albedo",       // 1 green
        "T_Semirough_Albedo",   // 2 semi-rough
        "T_Rough_Albedo",       // 3 rough (catch-all)
        "T_Bunker_Albedo",      // 4 bunker
        "T_Tee_Albedo",         // 5 tee
        "T_RoadAsphalt_Albedo", // 6 cart path
        "T_Fringe_Albedo",      // 7 fringe
    };
    string[] normalNames = {
        "T_Fairway_Normal",
        "T_Green_Normal",
        "T_Semirough_Normal",
        "T_Rough_Normal",
        "T_Bunker_Normal",
        "T_Tee_Normal",
        "T_RoadAsphalt_Normal",
        "T_Fringe_Normal",
    };
    float[] tileSizes = { 5f, 3f, 6f, 8f, 4f, 3f, 4f, 4f };

    var layers = new TerrainLayer[layerCount];
    string layerDir = $"{dataDir}";
    EnsureDirectory(Path.Combine(projectRoot, layerDir));

    for (int i = 0; i < layerCount; i++)
    {
        layers[i] = new TerrainLayer();
        layers[i].diffuseTexture = FindTextureExact(texDir, albedoNames[i]);
        layers[i].normalMapTexture = FindTextureExact(texDir, normalNames[i]);
        layers[i].tileSize = new Vector2(tileSizes[i], tileSizes[i]);
        layers[i].tileOffset = Vector2.zero;

        if (layers[i].diffuseTexture == null)
            Debug.LogWarning($"[HoleLiteImporter] Missing texture: {albedoNames[i]}");

        string layerPath = $"{layerDir}/TerrainLayer_{albedoNames[i]}.asset";
        var existingLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existingLayer != null)
            AssetDatabase.DeleteAsset(layerPath);
        AssetDatabase.CreateAsset(layers[i], layerPath);
    }

    terrainData.terrainLayers = layers;
    terrainData.SetAlphamaps(0, 0, alphamap);

    // Copy zones.json to Assets for future runtime use
    string destZonesPath = Path.Combine(projectRoot, dataDir, "zones.json");
    File.Copy(zonesPath, destZonesPath, true);
    AssetDatabase.ImportAsset($"{dataDir}/zones.json");

    Debug.Log($"[HoleLiteImporter] Splatmap applied: {layerCount} layers, " +
              $"alphamap {alphaRes}x{alphaRes}, blur radius {blurRadius}");
}
```

---

### Step 4: Helper Methods

Add these helpers to `HoleLiteImporter.cs`:

```csharp
private static int ZoneToLayer(int zoneIndex)
{
    return zoneIndex switch
    {
        1  => 0,  // fairway
        2  => 1,  // green
        3  => 2,  // semi_rough
        4  => 3,  // rough
        5  => 3,  // trees → rough texture
        6  => 4,  // bunker
        7  => 3,  // water → rough for now
        8  => 6,  // cart_path
        9  => 3,  // ob → rough texture
        10 => 5,  // tee_box
        _  => 3,  // background/unknown → rough
    };
}

private static bool[] DilateMask(bool[] mask, int w, int h, int radius)
{
    bool[] result = new bool[w * h];
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
        {
            if (mask[y * w + x])
            {
                // Already set — spread to neighbors
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius) continue;
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                            result[ny * w + nx] = true;
                    }
                }
            }
        }
    }
    return result;
}

private static float[,] ExtractChannel(float[,,] alphamap, int res, int layerCount, int layer)
{
    float[,] channel = new float[res, res];
    for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            channel[y, x] = alphamap[y, x, layer];
    return channel;
}

private static void SetChannel(float[,,] alphamap, int res, int layerCount, int layer, float[,] channel)
{
    for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            alphamap[y, x, layer] = channel[y, x];
}

private static float[,] GaussianBlur2D(float[,] input, int res, int radius, float sigma)
{
    // Build 1D kernel
    int kernelSize = radius * 2 + 1;
    float[] kernel = new float[kernelSize];
    float kernelSum = 0f;
    for (int i = 0; i < kernelSize; i++)
    {
        float d = i - radius;
        kernel[i] = Mathf.Exp(-(d * d) / (2f * sigma * sigma));
        kernelSum += kernel[i];
    }
    for (int i = 0; i < kernelSize; i++)
        kernel[i] /= kernelSum;

    // Horizontal pass
    float[,] temp = new float[res, res];
    for (int y = 0; y < res; y++)
    {
        for (int x = 0; x < res; x++)
        {
            float sum = 0f;
            for (int k = 0; k < kernelSize; k++)
            {
                int sx = Mathf.Clamp(x + k - radius, 0, res - 1);
                sum += input[y, sx] * kernel[k];
            }
            temp[y, x] = sum;
        }
    }

    // Vertical pass
    float[,] output = new float[res, res];
    for (int y = 0; y < res; y++)
    {
        for (int x = 0; x < res; x++)
        {
            float sum = 0f;
            for (int k = 0; k < kernelSize; k++)
            {
                int sy = Mathf.Clamp(y + k - radius, 0, res - 1);
                sum += temp[sy, x] * kernel[k];
            }
            output[y, x] = sum;
        }
    }

    return output;
}

private static Texture2D FindTextureExact(string dir, string exactName)
{
    // Search for the texture by exact filename (without extension)
    string[] guids = AssetDatabase.FindAssets(exactName, new[] { dir });
    foreach (var guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        string fileName = Path.GetFileNameWithoutExtension(path);
        if (fileName == exactName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
    Debug.LogWarning($"[HoleLiteImporter] Texture not found: {exactName} in {dir}");
    return null;
}
```

---

### Step 5: Rename Old Method

Rename the existing `ApplyTexture` method to `ApplyTextureIllustration` so
it's still available for debugging:

```csharp
// Was: private static void ApplyTexture(...)
private static void ApplyTextureIllustration(TerrainData terrainData, HoleManifest manifest,
    string exportPath, string dataDir, string holeId, string projectRoot)
{
    // ... existing code unchanged ...
}
```

---

### Step 6: Clean Up Debug Menu Items

The two validation test menu items from the previous task
(`GOLFIN > Debug > Test Terrain Layers` and `GOLFIN > Debug > Test Zone Alignment`)
can stay — they're useful for future debugging. No need to remove them.

---

### Verification

After implementation, re-import Hole 01: `GOLFIN > Import Hole (Lite) > Hole 01`

- [ ] 8 TerrainLayers created in `Assets/Golf/Courses/lomond-country-club/Data/hole-01/`
- [ ] Each layer has both diffuse and normal map assigned (no null warnings in console)
- [ ] Terrain shows distinct surfaces: green patch, fairway stripe, rough everywhere else
- [ ] Fairway texture is visually different from rough texture
- [ ] Green texture patch visible at the green area
- [ ] Bunker sand patches visible at bunker locations (if any exist in zone grid)
- [ ] Tee texture visible near tee marker anchors
- [ ] Smooth transitions between zone types (no hard pixel edges)
- [ ] Fringe ring visible around the green (slightly different shade)
- [ ] zones.json copied to Assets data folder
- [ ] No console errors
- [ ] Re-running import replaces layers cleanly (no duplicate assets)
- [ ] Walking around in play mode — textures look reasonable from ground level

### If Zone Alignment Is Off

If surfaces appear rotated or mirrored relative to the terrain features:
- The `gx/gy` mapping in the resampling loop is the only thing to adjust
- Try swapping `gx` and `gy`, or inverting one axis: `(zoneW - 1 - gx)` or `(zoneH - 1 - gy)`
- Use the debug spheres from `Test Zone Alignment` as reference points
- The tee anchor markers should sit on tee-textured terrain

### Do NOT

- Remove or modify `CreateTerrain()` or heightmap code
- Change anchor placement or WalkCamera
- Modify UHole Lite scripts
- Change the zone grid data
- Remove the debug menu items from the validation task

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
