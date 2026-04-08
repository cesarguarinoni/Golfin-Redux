# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Kill Terrain Plastic Sheen (Take 2)

**Problem:** Terrain still has specular highlights/plastic look despite
`smoothness = 0` on layers and `GetTerrainMaterial()`. The normals we
re-enabled are making it worse by giving the surface micro-detail that
catches specular light.

**Root cause:** URP Terrain/Lit shader controls specular via both the
material AND the per-layer smoothness/normal maps. Setting smoothness=0
on layers isn't enough — the shader still has specular response.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### Fix: Disable normals and force specular off

In the terrain layer creation loop, **null out normal maps** again.
The normals were causing more harm (specular catchlights) than good
(surface detail). We can re-enable them later when we have proper
lighting/shader setup.

Find the layer creation loop and make sure:
```csharp
layers[i].normalMapTexture = null;  // Disable normals — they amplify specular
layers[i].smoothness = 0f;
layers[i].metallic = 0f;
```

Also, in `GetTerrainMaterial()`, make sure the material has these
keywords and properties set:

```csharp
private static Material GetTerrainMaterial()
{
    string matPath = "Assets/Courses/Materials (Shared by courses)/MAT_Terrain.mat";
    var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (existing != null)
    {
        // Always re-apply settings in case they got lost
        existing.SetFloat("_Smoothness", 0f);
        existing.SetFloat("_Metallic", 0f);
        // Try all known URP specular-off approaches
        existing.SetFloat("_SpecularHighlights", 0f);
        existing.SetFloat("_EnvironmentReflections", 0f);
        existing.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        existing.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // Create new
    var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
    if (shader == null) shader = Shader.Find("Terrain/Lit");
    if (shader == null)
    {
        Debug.LogWarning("[HoleLiteImporter] Could not find URP Terrain shader");
        return null;
    }

    var mat = new Material(shader);
    mat.name = "MAT_Terrain";
    mat.SetFloat("_Smoothness", 0f);
    mat.SetFloat("_Metallic", 0f);
    mat.SetFloat("_SpecularHighlights", 0f);
    mat.SetFloat("_EnvironmentReflections", 0f);
    mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
    mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
    AssetDatabase.CreateAsset(mat, matPath);
    return mat;
}
```

**Important:** Delete the existing `MAT_Terrain.mat` file first before
re-importing, so it gets recreated with the correct settings. Or
manually delete it from
`Assets/Courses/Materials (Shared by courses)/MAT_Terrain.mat`.

### Alternative if keywords don't work

If the URP terrain shader ignores `_SPECULARHIGHLIGHTS_OFF`, the nuclear
option is to set the directional light to cast **no specular**:

After creating the light in `ImportLiteHole()`:
```csharp
light.renderMode = LightRenderMode.ForceVertex;
```

This forces vertex lighting which removes per-pixel specular. It's a
blunt instrument but guaranteed to kill the hotspot.

### Fringe texture — check result

The previous task changed fringe tile to `new Vector2(8f, 4f)`. Check
if the grain direction is correct now. If it's still wrong, try
`new Vector2(4f, 8f)` instead.

---

### Verification

- [ ] Delete `MAT_Terrain.mat` from Assets, then re-import a hole
- [ ] No plastic sheen on terrain
- [ ] No sun hotspot when looking toward light
- [ ] Terrain still textured properly (not flat grey)
- [ ] All zone meshes unaffected

### Do NOT

- Modify zone meshes
- Modify splatmap zone mapping
- Modify export pipeline

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope: terrain depression near water edges
✅ DONE: 2026-04-08 — Tee Markers: FBX props replacing debug cylinders, green mat created
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain lighting cleanup attempt 1: normals re-enabled, light reduced, terrain material created (still plastic)
✅ DONE: 2026-04-08 — Terrain plastic sheen Take 2: normals nulled, normalScale=0, env reflections off, specular keywords on material
✅ DONE: 2026-04-08 — Terrain plastic sheen Take 3: ForceVertex light rendering (nuclear option) to kill per-pixel specular
