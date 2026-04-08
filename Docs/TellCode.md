# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Tee Border Ring with Gradient Texture

**Goal:** Add a border outline ring around each tee area, using the
`T_TeeDark_Albedo` texture. This texture has a lighter side and a darker
side — the lighter side must face inward (toward tee center) and the
darker side must face outward.

This is similar to the fairway fringe ring but uses a **directional UV
mapping** across the ring width instead of world-space tiled UVs.

---

### Implementation

Use the existing `CreateFringeRing` and `OffsetContourOutward` as
reference, but create a dedicated method or adjust the fringe ring
approach for the tee border. The critical difference is the UV mapping.

#### UV mapping for gradient texture

The fringe ring has two vertex rings:
- **Inner ring** (original tee contour edge) — UV.v = 0 (lighter side)
- **Outer ring** (offset outward) — UV.v = 1 (darker side)

The UV.u coordinate should be based on the vertex's position along the
contour perimeter (normalized arc length), so the texture wraps around
the ring without stretching.

```csharp
// Compute cumulative arc length along the inner ring for UV.u
float[] arcLengths = new float[n];
arcLengths[0] = 0f;
for (int i = 1; i < n; i++)
{
    float dx = innerRing[i].x - innerRing[i - 1].x;
    float dz = innerRing[i].z - innerRing[i - 1].z;
    arcLengths[i] = arcLengths[i - 1] + Mathf.Sqrt(dx * dx + dz * dz);
}
// Close the loop
float totalArc = arcLengths[n - 1] +
    Mathf.Sqrt(Mathf.Pow(innerRing[0].x - innerRing[n - 1].x, 2) +
               Mathf.Pow(innerRing[0].z - innerRing[n - 1].z, 2));

// Tile the U axis — repeat texture every ~3m along the perimeter
float uTileSize = 3f;

for (int i = 0; i < n; i++)
{
    float u = arcLengths[i] / uTileSize; // tiling along perimeter
    fringeUVs[i]     = new Vector2(u, 0f); // inner = light (v=0)
    fringeUVs[n + i] = new Vector2(u, 1f); // outer = dark  (v=1)
}
```

**NOTE:** Check which axis of `T_TeeDark_Albedo` has the gradient. If
the gradient runs along U (left=light, right=dark), swap the UV
assignment — use U for the inner/outer mapping and V for the perimeter.
You may need to visually test and swap U/V if the gradient appears
rotated 90°.

**NOTE:** The texture import settings should have `wrapMode = Repeat`
(for the perimeter axis) and `wrapMode = Clamp` on the gradient axis.
Since Unity textures have a single wrap mode, set it to Repeat and
ensure the gradient fills the full 0→1 range so clamping isn't needed.

#### Where to add

In `CreateFlatZoneMeshes`, in the tee section, after creating each
tee mesh, add the border ring:

```csharp
// After creating tee mesh...
// Create tee border ring
var teeBorderMat = CreateTiledMaterial(texDir, "T_TeeDark_Albedo",
    "T_TeeDark_Normal", dataDir, 1f); // tileSize=1 since UVs are manual

// The ring goes OUTSIDE the tee contour
float teeFringeWidth = 1.0f; // 1 meter wide border
```

Then build the ring mesh using `OffsetContourOutward(worldPts, teeFringeWidth)`
and the gradient UV mapping described above.

**IMPORTANT:** Don't reuse `CreateFringeRing` directly because it uses
world-space tiling UVs. Either:
- (A) Add a parameter to `CreateFringeRing` for UV mode (tiled vs gradient)
- (B) Create `CreateGradientBorderRing` as a new method
- (C) Modify `CreateFringeRing` to accept a UV callback/mode

Option B (new method) is cleanest. Copy the structure of `CreateFringeRing`
but replace the UV section with the arc-length + inner/outer gradient
mapping shown above.

#### Material setup

```csharp
var teeBorderMat = new Material(GetLitShader());
teeBorderMat.name = "MAT_TeeBorder";
teeBorderMat.mainTexture = FindTextureExact(texDir, "T_TeeDark_Albedo");
var teeNormal = FindTextureExact(texDir, "T_TeeDark_Normal");
if (teeNormal != null)
{
    teeBorderMat.SetTexture("_BumpMap", teeNormal);
    teeBorderMat.SetFloat("_BumpScale", 0.4f);
    teeBorderMat.EnableKeyword("_NORMALMAP");
}
teeBorderMat.SetFloat("_Smoothness", 0f);
teeBorderMat.SetFloat("_Metallic", 0f);

string matPath = $"{dataDir}/MAT_TeeBorder.mat";
var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
if (existing != null) AssetDatabase.DeleteAsset(matPath);
AssetDatabase.CreateAsset(teeBorderMat, matPath);
```

#### SurfaceMarker

Use `SurfaceType.Fringe` (or `SurfaceType.Tee` — either works, your call).

---

### Verification

- [ ] Each tee area has a visible border ring around it
- [ ] The lighter side of the texture faces inward (toward tee center)
- [ ] The darker side of the texture faces outward (toward rough)
- [ ] The texture wraps smoothly around the perimeter without stretching
- [ ] No z-fighting with the tee mesh underneath
- [ ] Border width looks reasonable (~1m)
- [ ] No console errors
- [ ] Fairway fringe, greens, bunkers unaffected

### If the gradient is flipped (dark inside, light outside)

Swap the V values:
```csharp
fringeUVs[i]     = new Vector2(u, 1f); // inner = dark  (v=1)
fringeUVs[n + i] = new Vector2(u, 0f); // outer = light (v=0)
```

Or if the gradient runs along U instead of V, swap the axes:
```csharp
fringeUVs[i]     = new Vector2(0f, u); // inner: u=0 (light)
fringeUVs[n + i] = new Vector2(1f, u); // outer: u=1 (dark)
```

### Do NOT

- Modify fairway mesh or fairway fringe
- Touch green, bunker, or water meshes
- Change the export pipeline
- Apply blur or SDF

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Fairway mow stripes (T_Fairway_Mix, ear-clip triangulation) + fringe ring (semirough, 0.5m inward)
✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Texture cleanup: swap, fringe ring, blur removed, alphamap 1024, zone grid 2048
✅ DONE: 2026-04-08 — PNG + SVG zone import in Hole Viewer
✅ DONE: 2026-04-08 — Morphological close + various smoothing attempts
✅ DONE: 2026-04-08 — Re-enable normal maps (0.4 intensity) + aniso filtering (level 16) on all terrain textures
✅ DONE: 2026-04-08 — SDF-based smooth fairway border (replaced by mesh approach)
✅ DONE: 2026-04-08 — Vector contour rasterization (replaced by mesh approach)
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes with smooth edges
✅ DONE: 2026-04-08 — Tee border ring with gradient texture (T_TeeDark_Albedo, CreateGradientBorderRing method, 1m width, arc-length UVs)
