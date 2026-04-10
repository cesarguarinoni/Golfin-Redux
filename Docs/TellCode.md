# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Directional Light & Shadows Setup

**Goal:** Replace the placeholder directional light with a
properly configured sun light that has realistic shadows,
minimal shadow pop-in, and is ready for future light baking.

### What to change

In `HoleLiteImporter.cs`, find the existing light creation block:

```csharp
var lightGO = new GameObject("Directional Light");
var light = lightGO.AddComponent<Light>();
light.type = LightType.Directional;
light.color = new Color(1f, 0.96f, 0.84f);
light.intensity = 1.0f;
lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
```

Replace with:

```csharp
// ---- Directional Light (Sun) ----
var lightGO = new GameObject("Directional Light");
var light = lightGO.AddComponent<Light>();
light.type = LightType.Directional;

// Warm sunlight color — slightly less saturated than before
light.color = new Color(1f, 0.96f, 0.88f);
light.intensity = 1.2f;

// Sun position: 45° altitude, 135° azimuth (SE → NW shadows)
// Simulates mid-morning sun at Lomond CC (~34.9°N latitude)
lightGO.transform.rotation = Quaternion.Euler(45f, 135f, 0f);

// Shadows
light.shadows = LightShadows.Soft;
light.shadowStrength = 0.7f;
light.shadowBias = 0.05f;
light.shadowNormalBias = 0.4f;
light.shadowNearPlane = 0.2f;

// Light mode: Mixed — allows baking later while keeping
// real-time shadows for dynamic objects (ball, character)
light.lightmapBakeType = LightmapBakeType.Mixed;

// Shadow distance — controls how far from camera shadows render.
// 100m covers the playable area without wasting budget on distant terrain.
// This reduces shadow pop-in when walking.
QualitySettings.shadowDistance = 100f;

// Terrain shadow settings
var terrainComp = terrainGO.GetComponent<Terrain>();
terrainComp.shadowCastingMode =
    UnityEngine.Rendering.ShadowCastingMode.On;
```

**NOTE:** The `terrainComp` variable already exists a few lines
above (used for `reflectionProbeUsage`). Move the
`shadowCastingMode` line right after that existing block, or
just add it below the light setup — either way is fine as long
as `terrainComp` is in scope.

### Why these values

| Setting | Value | Reason |
|---------|-------|--------|
| Euler(45, 135, 0) | Mid-morning sun from SE | Natural shadows falling NW, good depth on terrain features |
| intensity 1.2 | Slightly brighter | Compensates for shadow darkening; sky is HDR so won't blow out |
| shadowStrength 0.7 | Soft shadows | Not pitch-black; simulates ambient light filling shadows |
| shadowBias 0.05 | Low bias | Reduces shadow acne on flat terrain |
| shadowNormalBias 0.4 | Moderate | Prevents peter-panning (shadow separation from objects) |
| shadowDistance 100 | 100 meters | Covers fairway + green from any tee; minimizes pop-in |
| Mixed bake mode | Future-proof | Can bake lightmaps later; dynamic objects still get real-time shadows |

### URP Pipeline Asset check (manual, not code)

After import, verify in Edit > Project Settings > Graphics >
URP Asset > Shadows:
- **Shadow Distance** ≥ 100 (pipeline asset caps the
  QualitySettings value)
- **Cascade Count** = 4 (default is fine; 4 cascades minimize
  pop-in at distance transitions)
- **Soft Shadows** = ON

If shadow distance in the URP asset is lower than 100, the
QualitySettings line won't have full effect. Log a warning
if you can detect this at import time:

```csharp
var pipelineAsset = UnityEngine.Rendering.GraphicsSettings
    .currentRenderPipeline
    as UnityEngine.Rendering.Universal
       .UniversalRenderPipelineAsset;
if (pipelineAsset != null)
{
    // Check shadow distance
    var sdField = pipelineAsset.GetType().GetProperty(
        "shadowDistance");
    if (sdField != null)
    {
        float pipelineShadowDist = (float)sdField.GetValue(
            pipelineAsset);
        if (pipelineShadowDist < 100f)
            Debug.LogWarning(
                "[HoleLiteImporter] URP shadow distance is " +
                $"{pipelineShadowDist}m — shadows will clip " +
                "before 100m. Increase in URP Asset > Shadows.");
    }
}
```

Place this check right after the light setup block.

### Verification

1. Re-import: GOLFIN > Import Hole (Lite) > Hole 01
2. In Scene view, shadows should be visible on terrain, trees,
   bunker bowls, tee markers, and flag
3. Walk around with WalkCamera — shadows should not visibly
   pop in/out within ~80m of camera
4. Console: no shadow-related warnings (unless URP asset needs
   manual adjustment)
5. Shadow direction should feel like morning sun — cast from SE
   toward NW

### Do NOT

- Add any new scripts or files — this is purely edits to
  `HoleLiteImporter.cs`
- Change the skybox assignment
- Change any existing terrain, zone mesh, or tree code
- Modify the URP pipeline asset programmatically (manual only)

---

## Completed Tasks

✅ 2026-04-08 — Fairway mow stripes + fringe ring
✅ 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes
✅ 2026-04-08 — Tee border ring with gradient texture
✅ 2026-04-08 — All earlier tasks (water, bunkers, greens, textures, etc.)
✅ 2026-04-08 — traceBorder direction-aware walk + RDP/Chaikin tuning
✅ 2026-04-09 — Water contour mesh overlay (ear-clip, opaque material)
✅ 2026-04-09 — Cart path contour mesh + min-width dilation
✅ 2026-04-09 — Water shader (URPWater/Standard, animated normals)
✅ 2026-04-09 — Heightmap .raw loader in CreateTerrain
✅ 2026-04-09 — Overlay mesh Y-offsets for DEM terrain
✅ 2026-04-09 — Cart path spine-based strip mesh
✅ 2026-04-09 — Mountain backdrop (single ring, transparent, URP)
✅ 2026-04-09 — Water mesh DEM positioning fix
✅ 2026-04-10 — Bunker v1-v5 iterations → v5 inscribed rectangle terrain hole
✅ 2026-04-10 — Tree Placement System: data classes, TreePlacer.cs, wired into HoleLiteImporter
✅ 2026-04-10 — Directional Light & Shadows: soft shadows, Mixed bake, 100m distance, URP check
