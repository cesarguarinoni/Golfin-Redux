# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Kill Terrain Plastic Sheen (Take 4 — Mask Map)

**Root cause confirmed from Unity docs:** The URP TerrainLit shader reads
smoothness from the **albedo texture's alpha channel**. JPG textures have
no alpha, so Unity fills it with white = full smoothness = plastic.

Setting `layer.smoothness = 0` and `alphaSource = None` did NOT work
because the URP terrain shader ignores the layer smoothness property and
reads directly from the texture alpha.

**The proper fix:** Assign a **Mask Map** texture to each terrain layer.
Per Unity docs, when a mask map is present, the shader reads smoothness
from the **mask map's alpha** instead of the albedo alpha. The mask map
channels are: `R=Metallic, G=AO, B=Detail, A=Smoothness`.

A mask map with `A=0` → smoothness = 0 → no plastic.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### Step 1: Generate a "matte" mask map texture at import time

In `ApplySplatmap()`, before the layer creation loop, create a small
(4×4) mask map texture with the right channel values:

```csharp
// Create a shared "matte" mask map: R=0 (no metallic), G=255 (full AO),
// B=0 (no detail mask), A=0 (zero smoothness)
string matteMaskPath = $"{dataDir}/MatteMaskMap.png";
string fullMattePath = Path.Combine(projectRoot, matteMaskPath);
if (!File.Exists(fullMattePath))
{
    var matteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    Color matteColor = new Color(0f, 1f, 0f, 0f); // R=0,G=1,B=0,A=0
    for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
            matteTex.SetPixel(x, y, matteColor);
    matteTex.Apply();
    File.WriteAllBytes(fullMattePath, matteTex.EncodeToPNG());
    Object.DestroyImmediate(matteTex);
}
AssetDatabase.ImportAsset(matteMaskPath);

// Configure as linear (non-sRGB) — mask maps must NOT be color-corrected
var maskImporter = AssetImporter.GetAtPath(matteMaskPath) as TextureImporter;
if (maskImporter != null)
{
    maskImporter.sRGBTexture = false;        // CRITICAL: must be linear
    maskImporter.textureType = TextureImporterType.Default;
    maskImporter.textureCompression = TextureImporterCompression.Uncompressed;
    maskImporter.npotScale = TextureImporterNPOTScale.None;
    maskImporter.SaveAndReimport();
}

var matteMask = AssetDatabase.LoadAssetAtPath<Texture2D>(matteMaskPath);
```

### Step 2: Assign mask map to each terrain layer

In the layer creation loop, after setting diffuseTexture, add:

```csharp
layers[i].maskMapTexture = matteMask;
```

### Step 3: Clean up previous failed fixes

- **Remove** `GetTerrainMaterial()` method entirely
- **Remove** `terrainComp.materialTemplate = GetTerrainMaterial();` line
- **Remove** any `alphaSource` modifications on terrain textures
- **Keep** `terrain.reflectionProbeUsage = Off` (doesn't hurt)
- **Keep** normal maps nulled for now (`layers[i].normalMapTexture = null`)

---

### Verification

- [ ] Re-import any hole
- [ ] No plastic sheen on terrain
- [ ] No sun hotspot blob
- [ ] Terrain colors unchanged (mask map doesn't affect albedo color)
- [ ] Zone meshes unaffected
- [ ] No console errors
- [ ] Check `MatteMaskMap.png` in data folder — should be 4×4, RGBA

### Do NOT

- Modify zone meshes or materials
- Modify splatmap zone mapping
- Modify export pipeline

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope: terrain depression near water edges
✅ DONE: 2026-04-08 — Tee Markers: FBX props replacing debug cylinders, green mat created
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain lighting cleanup attempts 1-3 (keywords, normals, alpha source — none fixed root cause)
✅ DONE: 2026-04-08 — Terrain plastic sheen Take 4: matte mask map (A=0 smoothness) assigned to all terrain layers
