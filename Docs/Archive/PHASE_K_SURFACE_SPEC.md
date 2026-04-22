# Phase K-Surface: Zone-Based Terrain Surfacing

> **Purpose:** Replace the illustration texture with proper tiling golf textures,
> driven by UHole Lite's zone grid. Automated, no manual terrain painting.
>
> **Handoff:** Claude (Architect) → Claude Code (Implementer) via `Docs/TellCode.md`

---

## What We Have

### From UHole Lite Export (`Tools/UHoleLite/output/.../export/hole-NN/`)

| File | Contents |
|---|---|
| `heightmap.raw` | 129×129 uint16be terrain elevation |
| `texture.png` | Illustration image (current terrain texture) |
| `zones.json` | Pixel grid (528×637 for Hole 1) with 11 zone types, base64-encoded `grid` field |
| `anchors.json` | Tee positions in local meters |
| `hole-manifest.json` | Terrain dimensions, par, yardage, etc. |

### From Assets/Courses/ (existing textures + materials)

**Textures** (`Assets/Courses/Textures_2025(JPG)/`):

| Surface | Albedo | Normal |
|---|---|---|
| Fairway | `T_Fairway_Light.jpg` | `T_Fairway_Normal.jpg` |
| Semi-rough | `T_Semirough_Albedo.jpg` | `T_Semirough_Normal.jpg` |
| Rough | `T_Rough_Albedo.jpg` | `T_Rough_Normal.jpg` |
| Green | `T_Green_Albedo.jpg` | `T_Green_Normal.jpg` |
| Bunker | `T_Bunker_Albedo.jpg` | `T_Bunker_Normal.jpg` |
| Tee | `T_Tee_Albedo.jpg` | `T_Tee_Normal.jpg` |
| OOB | `T_OOB_Albedo.jpg` | `T_OOB_Normal.jpg` |
| Cart Path | `T_RoadAsphalt_Albedo.jpg` | `T_RoadAsphalt_Normal.jpg` |
| Fringe | `T_Fringe_Albedo.jpg` | `T_Fringe_Normal.jpg` |

**Also available:** Dark variants (Fairway_Dark, Bunker_Dark, Tee_Dark), existing
Materials in `Assets/Courses/Materials (Shared by courses)/`, and tree billboards.

### Zone Index (from classify-zones.mjs)

```
0 = background  →  OOB texture
1 = fairway      →  Fairway texture
2 = green        →  Green texture
3 = semi_rough   →  Semirough texture
4 = rough        →  Rough texture
5 = trees        →  OOB texture (trees are separate objects, not terrain paint)
6 = bunker       →  Bunker texture
7 = water        →  (flat blue plane, not terrain layer — future)
8 = cart_path    →  RoadAsphalt texture
9 = ob           →  OOB texture
10 = tee_box     →  Tee texture
```

---

## Architecture

### Core Idea: Splatmap from Zone Grid

Unity terrain uses **alphamaps** (splatmaps) to blend multiple TerrainLayers.
Each terrain layer = a tiling texture. The alphamap stores per-pixel weights
that control which texture is visible where.

We generate the alphamap programmatically from the zone grid → no manual painting.

### TerrainLayer Allocation

We need **8 unique terrain layers** (Unity supports up to 16):

| Layer Index | Surface | Texture Source | Tiling (meters) |
|---|---|---|---|
| 0 | Fairway | T_Fairway_Light | ~5m |
| 1 | Green | T_Green_Albedo | ~3m |
| 2 | Semi-rough | T_Semirough_Albedo | ~6m |
| 3 | Rough / OOB / Background / Trees | T_Rough_Albedo | ~8m |
| 4 | Bunker | T_Bunker_Albedo | ~4m |
| 5 | Tee Box | T_Tee_Albedo | ~3m |
| 6 | Cart Path | T_RoadAsphalt_Albedo | ~4m |
| 7 | Fringe | T_Fringe_Albedo | ~4m |

> **Layer 3 (Rough) is the "catch-all"** — background, OOB, trees, and rough
> all use the rough texture. This simplifies the splatmap while keeping all
> gameplay-distinct zones queryable via the zone grid data.

Unity allocates splatmap textures in groups of 4 layers (RGBA). 8 layers = 2 splatmap textures. This is fine for mobile.

### Zone → Layer Mapping

```csharp
static int ZoneToLayer(int zoneIndex)
{
    return zoneIndex switch
    {
        1  => 0,  // fairway
        2  => 1,  // green
        3  => 2,  // semi_rough
        4  => 3,  // rough
        5  => 3,  // trees → rough texture
        6  => 4,  // bunker
        7  => 3,  // water → rough for now (water is a separate plane)
        8  => 6,  // cart_path
        9  => 3,  // ob → rough texture
        10 => 5,  // tee_box
        _  => 3,  // background/unknown → rough
    };
}
```

### Fringe (Layer 7) — Synthetic Zone

UHole Lite doesn't classify "fringe" (the collar around the green). We generate
it by **dilating the green zone** outward by N pixels and marking the ring as
fringe. This is done in the splatmap generation, not in the zone grid.

```
Fringe ring = (dilated green mask) AND NOT (original green mask)
Width: ~2-3 meters of fringe around the green
```

---

## Splatmap Generation Pipeline

### Step 1: Parse Zone Grid

Read `zones.json` from the export package. The `grid` field is a base64-encoded
`Uint8Array` of size `width × height`. Each byte is a zone index (0-10).

```csharp
byte[] gridBytes = System.Convert.FromBase64String(zonesData.grid);
// gridBytes[y * width + x] = zone index at pixel (x, y)
```

### Step 2: Resample to Alphamap Resolution

Unity's alphamap resolution is set via `terrainData.alphamapResolution`.
Use **256** for mobile (good balance of quality vs memory).

Resample the zone grid (528×637) to the alphamap size (256×256) using
**nearest-neighbor** sampling. This preserves zone boundaries.

```csharp
int alphaRes = 256;
terrainData.alphamapResolution = alphaRes;

byte[] resampledZones = new byte[alphaRes * alphaRes];
for (int ay = 0; ay < alphaRes; ay++)
{
    for (int ax = 0; ax < alphaRes; ax++)
    {
        // The zone grid and terrain have a 90° CCW rotation relationship
        // (same as the existing heightmap/texture rotation in HoleLiteImporter)
        // Zone grid: x = left→right, y = top→bottom
        // Terrain: after 90° CCW, we swap and flip axes
        //
        // Map alphamap pixel to zone grid pixel using the same
        // rotation transform as the heightmap.
        //
        // Alphamap coords: [ay, ax] where ay=0 is terrain north edge
        // Unity terrain alphamap convention:
        //   alphamap[0, 0] = terrain corner at (posX, posZ) = NW corner
        //   alphamap[0, alphaRes-1] = NE corner
        //   alphamap[alphaRes-1, 0] = SW corner
        //
        // After the 90° CCW rotation used in HoleLiteImporter:
        //   heightmap uses heights[hx, hy] (swapped indices)
        //   The zone grid needs the same transform.
        //
        // For now, use a simple fractional mapping and verify alignment
        // with anchor markers. The exact transform may need tweaking.

        float fx = (float)ax / (alphaRes - 1);  // 0..1 across terrain X
        float fy = (float)ay / (alphaRes - 1);  // 0..1 across terrain Z

        // Convert terrain fraction to zone grid pixel
        // This mapping depends on the 90° CCW rotation.
        // In the current importer: terrain X = zone grid Y, terrain Z = zone grid X
        // (rotated 90° CCW means (x,y) → (-y, x) → zone row = terrainX, zone col = terrainZ)
        //
        // Need to verify this empirically — see Verification section.
        int gx = Mathf.Clamp(Mathf.RoundToInt(fy * (zoneW - 1)), 0, zoneW - 1);
        int gy = Mathf.Clamp(Mathf.RoundToInt(fx * (zoneH - 1)), 0, zoneH - 1);

        resampledZones[ay * alphaRes + ax] = gridBytes[gy * zoneW + gx];
    }
}
```

**NOTE:** The exact `fx→gx, fy→gy` mapping needs to match the 90° CCW rotation
used for the heightmap and texture in `HoleLiteImporter`. This is the most
critical alignment step — the spec provides the pattern, but the implementer
should verify by checking that the green zone splatmap aligns with the green
area visible in the texture, and tee zone aligns with tee markers.

### Step 3: Generate Fringe Ring

Dilate the green mask and subtract the original to get a fringe ring.

```csharp
// Create green mask
bool[] greenMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < resampledZones.Length; i++)
    greenMask[i] = (resampledZones[i] == 2); // zone 2 = green

// Dilate green mask by fringeRadius pixels
int fringeRadius = 3; // ~3 alphamap pixels ≈ 3-4 meters
bool[] dilatedGreen = DilateMask(greenMask, alphaRes, alphaRes, fringeRadius);

// Fringe = dilated AND NOT original green, AND on a playable surface
bool[] fringeMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < fringeMask.Length; i++)
{
    if (dilatedGreen[i] && !greenMask[i])
    {
        int zone = resampledZones[i];
        // Only place fringe on surfaces adjacent to green (fairway, semi_rough, rough)
        if (zone == 1 || zone == 3 || zone == 4)
            fringeMask[i] = true;
    }
}
```

### Step 4: Build Raw Alphamap (Hard Boundaries)

```csharp
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
```

### Step 5: Gaussian Smooth + Re-normalize

Apply a Gaussian blur to each layer channel independently, then re-normalize
so all channels sum to 1.0 at every pixel. This creates soft, natural-looking
transitions between surfaces.

```csharp
int blurRadius = 3; // adjust for softness (2=sharp, 5=very soft)
float sigma = blurRadius / 2.0f;

// Blur each layer channel
for (int layer = 0; layer < layerCount; layer++)
{
    float[,] channel = ExtractChannel(alphamap, alphaRes, layer);
    float[,] blurred = GaussianBlur2D(channel, alphaRes, blurRadius, sigma);
    SetChannel(alphamap, alphaRes, layer, blurred);
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
            // Fallback: rough
            alphamap[ay, ax, 3] = 1.0f;
        }
    }
}
```

### Step 6: Apply to Terrain

```csharp
// Create TerrainLayers
var layers = new TerrainLayer[layerCount];
string[] textureNames = {
    "T_Fairway_Light",     // 0
    "T_Green_Albedo",      // 1
    "T_Semirough_Albedo",  // 2
    "T_Rough_Albedo",      // 3
    "T_Bunker_Albedo",     // 4
    "T_Tee_Albedo",        // 5
    "T_RoadAsphalt_Albedo",// 6
    "T_Fringe_Albedo",     // 7
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

string texDir = "Assets/Courses/Textures_2025(JPG)";

for (int i = 0; i < layerCount; i++)
{
    layers[i] = new TerrainLayer();
    layers[i].diffuseTexture = FindTexture(texDir, textureNames[i]);
    layers[i].normalMapTexture = FindTexture(texDir, normalNames[i]);
    layers[i].tileSize = new Vector2(tileSizes[i], tileSizes[i]);
    layers[i].tileOffset = Vector2.zero;

    string layerPath = $"{dataDir}/TerrainLayer_{textureNames[i]}.asset";
    AssetDatabase.CreateAsset(layers[i], layerPath);
}

terrainData.terrainLayers = layers;
terrainData.SetAlphamaps(0, 0, alphamap);
```

**`FindTexture` helper** — search by name prefix in the texture directory:
```csharp
static Texture2D FindTexture(string dir, string namePrefix)
{
    string[] guids = AssetDatabase.FindAssets(namePrefix, new[] { dir });
    if (guids.Length > 0)
        return AssetDatabase.LoadAssetAtPath<Texture2D>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    Debug.LogWarning($"Texture not found: {namePrefix} in {dir}");
    return null;
}
```

---

## Runtime Surface Query

### SurfaceQuery.cs (new runtime script)

**File:** `Assets/Scripts/Gameplay/SurfaceQuery.cs`
**Namespace:** `Golfin.Gameplay`

```csharp
public enum SurfaceType
{
    Background = 0,
    Fairway = 1,
    Green = 2,
    SemiRough = 3,
    Rough = 4,
    Trees = 5,
    Bunker = 6,
    Water = 7,
    CartPath = 8,
    OB = 9,
    TeeBox = 10,
}

public static class SurfaceQuery
{
    // Zone grid loaded at scene start from zones.json
    private static byte[] _zoneGrid;
    private static int _gridW, _gridH;
    private static float _terrainW, _terrainH; // terrain size in meters

    public static void Initialize(byte[] zoneGrid, int gridW, int gridH,
        float terrainWidth, float terrainHeight)
    {
        _zoneGrid = zoneGrid;
        _gridW = gridW;
        _gridH = gridH;
        _terrainW = terrainWidth;
        _terrainH = terrainHeight;
    }

    /// <summary>
    /// Returns the surface type at a world position.
    /// Uses the zone grid data (not alphamap) for authoritative gameplay classification.
    /// </summary>
    public static SurfaceType GetSurfaceAt(Vector3 worldPos)
    {
        if (_zoneGrid == null) return SurfaceType.Rough;

        // Convert world pos to 0..1 terrain fraction
        // Terrain is centered at origin: -W/2 to +W/2, -H/2 to +H/2
        float fx = (worldPos.x + _terrainW / 2f) / _terrainW;
        float fz = (worldPos.z + _terrainH / 2f) / _terrainH;

        // Apply inverse of 90° CCW rotation to get zone grid coords
        // (same transform as splatmap generation)
        int gx = Mathf.Clamp(Mathf.RoundToInt(fz * (_gridW - 1)), 0, _gridW - 1);
        int gy = Mathf.Clamp(Mathf.RoundToInt(fx * (_gridH - 1)), 0, _gridH - 1);

        byte zone = _zoneGrid[gy * _gridW + gx];
        return (SurfaceType)zone;
    }
}
```

**Key design choice:** The runtime surface query reads from the **zone grid**
(authoritative pixel data), NOT from the terrain alphamap (which has been
smoothed/blurred for visual softness). This means gameplay boundaries are
precise even though visual transitions are soft.

### HoleZoneData.cs (MonoBehaviour — loads zone data at runtime)

**File:** `Assets/Scripts/Gameplay/HoleZoneData.cs`

Attached to HoleRoot. Loads `zones.json` from a TextAsset or streaming path
at scene start, initializes `SurfaceQuery`.

---

## Changes to HoleLiteImporter.cs

### Replace `ApplyTexture()` with `ApplySplatmap()`

The existing `ApplyTexture()` method loads `texture.png` and applies it as a
single terrain layer. Replace it with `ApplySplatmap()` that:

1. Reads `zones.json` from the export folder
2. Parses the base64 grid
3. Generates fringe ring
4. Builds alphamap with smoothing
5. Creates 8 TerrainLayers from existing textures
6. Applies via `SetAlphamaps()`

### Keep the illustration as a debug toggle (optional)

Optionally add a second menu item `GOLFIN > Import Hole (Lite) > Hole 01 (Illustration)`
that uses the old `ApplyTexture()` for comparison. Or just keep `texture.png`
in the data folder for reference — it's not applied to the terrain anymore.

### Copy zones.json into Assets for runtime access

The importer should copy `zones.json` from the export folder into
`Assets/Golf/Courses/{courseId}/Data/hole-{NN}/zones.json` so it's available
at runtime (as a TextAsset) for `SurfaceQuery`.

---

## Implementation Order (for Claude Code)

### Task 1: Splatmap Importer

1. Add `ZonesData` classes to `HoleManifestData.cs`:
   ```csharp
   [System.Serializable]
   public class ZonesData {
       public int hole_number;
       public ZoneSourceDimensions source_dimensions;
       public string grid; // base64
   }
   [System.Serializable]
   public class ZoneSourceDimensions {
       public int width, height;
   }
   ```

2. Add `ApplySplatmap()` method to `HoleLiteImporter.cs`

3. Add helper methods: `ZoneToLayer()`, `DilateMask()`, `GaussianBlur2D()`,
   `ExtractChannel()`, `SetChannel()`, `FindTexture()`

4. Replace the `ApplyTexture()` call with `ApplySplatmap()` in `ImportLiteHole()`

5. Verify: re-import Hole 01, confirm textures appear on terrain

### Task 2: Alignment Verification

The zone→alphamap coordinate mapping must match the 90° CCW rotation used for
the heightmap. Steps:

1. Import Hole 01 with splatmap
2. Open Scene view, top-down
3. Check: green texture patch aligns with the green zone position
4. Check: tee texture patch aligns with tee anchor markers
5. Check: bunker texture patches appear where bunkers should be
6. If misaligned: adjust the `fx→gx, fy→gy` mapping in the resampling step

### Task 3: Runtime Surface Query (can be deferred)

1. Create `SurfaceQuery.cs` and `HoleZoneData.cs`
2. Copy zones.json to Assets data folder during import
3. Wire `HoleZoneData` to HoleRoot
4. Test: walk around, query surface type, display in debug UI

---

## Tuning Parameters

| Parameter | Default | Effect |
|---|---|---|
| `alphamapResolution` | 256 | Higher = sharper zone edges, more memory |
| `blurRadius` | 3 | Higher = softer transitions (2=gamey, 5=realistic) |
| `fringeRadius` | 3 | Width of fringe ring around green in alphamap pixels |
| `tileSizes[]` | 3-8m | How often each texture repeats; smaller = more detail |

These can be adjusted after the first import to taste.

---

## What This Does NOT Cover (Future Work)

- **Bunker meshes** — per `BUNKER_RESEARCH.md`, bunkers should be separate
  3D bowls placed on the terrain. The splatmap marks the sand texture, but
  the 3D depression is a future task.
- **Water planes** — water zones get rough texture for now. A flat blue
  plane at water level is a future step.
- **Tree placement** — the tree billboard assets exist but aren't placed yet.
  The zone grid tells us where trees go; a scatter script is a future task.
- **Green contours / putting surface** — the green is flat in the heightmap.
  A detailed putting surface with slopes is a future task.
- **Lighting / baking** — the current directional light is placeholder.

---

## Verification Checklist

- [ ] 8 TerrainLayers created from existing textures (no missing textures)
- [ ] Each texture has a normal map applied
- [ ] Fairway texture visible on the fairway path
- [ ] Green texture visible as a distinct patch at the green area
- [ ] Rough texture covers the majority of the terrain (as expected)
- [ ] Bunker sand patches visible at bunker locations
- [ ] Tee texture patches visible at tee areas
- [ ] Smooth transitions between zone types (no pixel-art jaggies)
- [ ] Fringe ring visible around the green
- [ ] Anchor markers sit on the correct texture type
- [ ] No console errors
- [ ] Re-import works cleanly (replaces existing terrain layers)
- [ ] Zone grid coordinate mapping matches terrain rotation
