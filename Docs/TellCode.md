# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Kill Terrain Plastic Sheen (Take 3)

**Root cause found:** The URP terrain shader reads smoothness from the
**alpha channel of the albedo texture**. Our terrain textures are JPGs
which have no alpha channel. When Unity imports a JPG, it fills alpha
with **white (1.0) = full smoothness** → plastic sheen.

This is why the zone meshes look fine — they use `URP/Lit` which respects
the material's `_Smoothness` property. The terrain shader ignores the
layer smoothness and reads from the texture alpha instead.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### The Fix

In `ApplySplatmap()`, in the terrain layer creation loop, after
`FindTextureExact()` loads each albedo texture, configure the texture
importer to disable alpha:

```csharp
for (int i = 0; i < layerCount; i++)
{
    layers[i] = new TerrainLayer();
    var albedoTex = FindTextureExact(texDir, albedoNames[i]);

    // Fix terrain plastic sheen: URP terrain shader reads smoothness
    // from albedo alpha. JPGs have no alpha → Unity fills white (1.0)
    // = full smoothness. Disable alpha source to force 0 smoothness.
    if (albedoTex != null)
    {
        string texPath = AssetDatabase.GetAssetPath(albedoTex);
        var texImporter = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (texImporter != null && texImporter.alphaSource != TextureImporterAlphaSource.None)
        {
            texImporter.alphaSource = TextureImporterAlphaSource.None;
            texImporter.SaveAndReimport();
        }
    }

    layers[i].diffuseTexture = albedoTex;
    // ... rest of layer setup
}
```

This sets `Alpha Source = None` on each terrain albedo texture, so Unity
won't generate a white alpha channel. The terrain shader will then read
alpha = 0 = no smoothness = no plastic sheen.

### Also: null out normal maps again

The normals were making the specular worse. Set them to null until we
have proper lighting:

```csharp
layers[i].normalMapTexture = null;
```

### Also: remove GetTerrainMaterial()

The custom terrain material approach didn't help. Remove the
`GetTerrainMaterial()` method and the lines that set
`terrain.materialTemplate`. Let the terrain use Unity's default
Terrain/Lit shader — with the alpha fix, it won't be shiny.

Remove these lines from `ImportLiteHole()`:
```csharp
terrainComp.materialTemplate = GetTerrainMaterial();
```

And delete the `GetTerrainMaterial()` method.

Keep `terrain.reflectionProbeUsage = Off` — doesn't hurt.

---

### Verification

- [ ] Re-import any hole
- [ ] Terrain no longer has plastic/shiny sheen
- [ ] No sun hotspot blob
- [ ] Terrain textures still display correctly (colors unchanged)
- [ ] Zone meshes (bunkers, greens, water) unaffected
- [ ] No console errors

### Do NOT

- Modify zone meshes or their materials
- Modify splatmap zone mapping
- Modify export pipeline

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope: terrain depression near water edges
✅ DONE: 2026-04-08 — Tee Markers: FBX props replacing debug cylinders, green mat created
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain lighting cleanup attempts 1-2 (material keywords, normals — didn't fix root cause)
✅ DONE: 2026-04-08 — Terrain plastic sheen Take 3: albedo alpha fix (root cause — JPG alpha=1.0 → full smoothness)
