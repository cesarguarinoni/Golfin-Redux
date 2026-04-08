# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Terrain Texture & Lighting Cleanup

**Goal:** Fix plastic sheen on splatmap terrain, kill sun hotspot,
fix fringe texture orientation.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### Fix 1: Kill plastic sheen on terrain

The terrain's URP Lit shader has default specular highlights that cause
a plastic look even with `smoothness = 0` on layers.

After creating the terrain GO in `ImportLiteHole()`, add these settings
to the `Terrain` component. Find the line:

```csharp
terrainGO.name = "TerrainRoot";
```

After the terrain position line, add:

```csharp
var terrain = terrainGO.GetComponent<Terrain>();
terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
terrain.materialTemplate = GetTerrainMaterial();
```

Add a helper method:

```csharp
private static Material GetTerrainMaterial()
{
    // Try to load existing terrain material first
    string matPath = "Assets/Courses/Materials (Shared by courses)/MAT_Terrain.mat";
    var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (existing != null) return existing;

    // Create a URP terrain lit material with specular killed
    var mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
    mat.SetFloat("_Smoothness", 0f);
    mat.SetFloat("_Metallic", 0f);
    mat.SetFloat("_SpecularHighlights", 0f);
    mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
    mat.name = "MAT_Terrain";
    AssetDatabase.CreateAsset(mat, matPath);
    return mat;
}
```

NOTE: If `Shader.Find("Universal Render Pipeline/Terrain/Lit")` returns
null, try `"Terrain/Lit"` or check what shader the current terrain uses.
The key settings are `_Smoothness = 0`, `_SpecularHighlights = 0`, and
the keyword `_SPECULARHIGHLIGHTS_OFF`. If URP terrain shader doesn't
support these properties, the alternative is to set
`terrain.reflectionProbeUsage = Off` and reduce the directional light
intensity or change its color.

### Fix 2: Fix the normalMapTexture null bug

In the terrain layer creation loop, there's a line that nulls the normal
map after setting it:

```csharp
layers[i].normalMapTexture = FindTextureExact(texDir, normalNames[i]);
// ...
layers[i].normalMapTexture = null;  // BUG: overwrites the line above
```

**Remove** the `layers[i].normalMapTexture = null;` line. The normal
maps should be applied — they'll add surface detail and reduce the
flat/plastic look.

### Fix 3: Fringe texture orientation

The fringe texture has a vertical grass grain but the terrain rotation
causes it to appear horizontal. Fix by swapping the tile dimensions
for the fringe layer.

In the `tileSizes` array, the fringe is index 7 with value `4f`.
Change it to use non-square tiling that accounts for the 90° rotation:

```csharp
// After creating the layer, swap U/V tile size for fringe
if (i == 7) // fringe layer
{
    layers[i].tileSize = new Vector2(tileSizes[i], tileSizes[i]);
    // Rotate tile by swapping U/V offset or using tileOffset
    // Actually: the terrain itself is rotated 90° CCW, so the texture
    // grain needs to be rotated too. Try swapping tile dimensions:
    layers[i].tileSize = new Vector2(4f, 4f);
}
```

Actually, since the terrain is rotated 90° CCW and the texture is
square-tiled, the grain direction depends on the texture itself. The
simplest fix: **check if the fringe texture needs rotation.** If it has
a visible directional grain (mow lines running one way), the fix is to
either:

a) Swap `tileSize` to `new Vector2(tileY, tileX)` — but both are 4f so
   that won't help.
b) **Pre-rotate the fringe texture 90° CCW** at import time (same as
   we do for the illustration texture). Add the fringe albedo to the
   rotation step.

The cleanest approach: in the layer creation loop, for ALL grass-type
layers (fairway, green, semi-rough, rough, tee, fringe — indices
0,1,2,3,5,7), check if the texture has a directional grain and needs
rotation. For now, just **rotate the fringe texture 90°** by creating
a rotated copy at import time:

```csharp
// Before creating layers, rotate fringe texture if needed
string fringeOrigPath = $"{texDir}/T_Fringe_Albedo";
var fringeTex = FindTextureExact(texDir, "T_Fringe_Albedo");
if (fringeTex != null)
{
    // Make readable, rotate, save as rotated copy
    // ... (use RotateTexture90CCW helper)
}
```

**Simpler approach:** Just check if rotating the fringe tile 90° fixes
it by setting a non-square tile size: `new Vector2(4f, 8f)` or similar.
Experiment with the tile aspect ratio until the grain runs the right way.

**Recommendation:** Try `layers[7].tileSize = new Vector2(8f, 4f)` first.
If the grain is still wrong, swap to `new Vector2(4f, 8f)`. If neither
works, pre-rotate the texture file.

### Fix 4: Reduce light intensity

The directional light is at 1.2 intensity which causes the harsh hotspot.
Reduce to 1.0 and soften the color:

Find:
```csharp
light.intensity = 1.2f;
```
Replace with:
```csharp
light.intensity = 1.0f;
```

---

### Verification

- [ ] Re-import any hole
- [ ] Terrain no longer has plastic/shiny sheen
- [ ] No sun hotspot blob when looking toward the light
- [ ] Fringe texture grain runs vertically (same direction as mow lines)
- [ ] Normal maps visible on terrain (subtle surface detail)
- [ ] Bunker, green, water meshes unaffected
- [ ] No console errors

### Do NOT

- Modify zone meshes (bunkers, greens, water)
- Modify the splatmap zone mapping or blur
- Modify export pipeline

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
✅ DONE: 2026-04-07 — Morphological close for water fragments + re-export
✅ DONE: 2026-04-07 — Water tree absorption + dilate-only (replaced morphological close)
✅ DONE: 2026-04-07 — Fix water border gaps: rim expanded to 105%, terrain cut at 100%
✅ DONE: 2026-04-07 — Simplified water to flat plane: no basin, no terrain holes, opaque material
✅ DONE: 2026-04-07 — Water Option 2: Rasterized quad + alpha mask (pixel-perfect water boundaries)
✅ DONE: 2026-04-07 — Water SDF texture: signed distance field for smooth edges
✅ DONE: 2026-04-08 — Water Shore Slope: terrain depression near water edges
✅ DONE: 2026-04-08 — Tee Area raised mesh + collar (reverted — wrong approach)
✅ DONE: 2026-04-08 — Tee Markers: FBX props replacing debug cylinders, green mat created
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain texture & lighting cleanup (plastic sheen fix, normal map bug, fringe orientation, light intensity)
