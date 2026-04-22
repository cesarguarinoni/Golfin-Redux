# Baked Shadow Lighting Pipeline (Shadowmask + Automated Bake)

**Status:** Queued, not urgent. Implement after current terrain/mesh work stabilizes. No existing system depends on this; it's pure perf win.

**Goal:** Replace real-time directional shadows on static scenery (terrain, trees, buildings, bunker lip meshes, cart paths, tee borders, green collars) with baked lightmaps + shadowmasks. Keep real-time shadows only for dynamic objects (ball, character, flag, cart). Automate across all 18 Lomond hole scenes so one menu item bakes the entire course.

**Expected perf:** mid-tier Android frame cost of directional-light shadows drops from ~2–4ms to ~0.3ms. Shadow distance can be reduced from 150m default to ~25m (dynamic-only).

---

## Part A — Lightmap UV2 generation in `HoleGeoImporter.cs`

Unity's Progressive lightmapper needs a second UV set (UV2) on every mesh that contributes GI or receives baked lighting. Meshes generated at import time (greens, bunkers, fairways, tees with border ring, cart paths, water, tee skirt, collars) currently have UV0 only. Without UV2, the lightmapper auto-generates on first bake but re-import invalidates the bake. We generate UV2 in-importer so re-imports are bake-safe.

**Target:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`.

**What to change:**

1. Add a private static helper near the other mesh utilities:

```csharp
/// <summary>
/// Generate UV2 (lightmap UVs) on a mesh using Unity's built-in
/// unwrapper. Call this AFTER all vertices/triangles/UV0/normals are
/// finalized on the mesh — UV2 generation reads the mesh topology.
/// Safe to call on multi-submesh meshes; unwrapper packs all submeshes
/// into a single UV2 chart set.
/// </summary>
private static void GenerateLightmapUVs(Mesh mesh)
{
    if (mesh == null || mesh.vertexCount == 0) return;
    var settings = new UnityEditor.UnwrapParam
    {
        hardAngle    = 88f,   // degrees; edges sharper than this get a chart seam
        packMargin   = 0.005f,// fraction of UV space between charts
        angleError   = 0.08f, // 0..1; lower = more faithful unwrap, slower
        areaError    = 0.15f, // 0..1; area distortion tolerance
    };
    UnityEditor.Unwrapping.GenerateSecondaryUVSet(mesh, settings);
}
```

2. Call `GenerateLightmapUVs(mesh)` at the end of every mesh-construction method in the importer, right before the mesh is assigned to a `MeshFilter`. Sites to hit (verify against current file, line numbers drift):

   - `CreateZoneMeshes` — bunker meshes (including lip submesh)
   - `CreateGreenMeshes` — green + collar submesh
   - `CreateFlatZoneMeshes` — fairway, tee (with border ring submesh), cart path overlay
   - `CreateTeeMeshWithInsetBorder` — 2-submesh tee mesh
   - `CreateSplineCartPaths` — ribbon strip meshes
   - `CreateWaterMeshes` — flat water surface mesh

   Do NOT call on the Unity Terrain itself — Terrain uses its own lightmap UV path managed by the terrain system.

3. Set each generated `GameObject` (except the flag, ball, character spawn, and any dynamic prefab) as lightmap-static. Helper:

```csharp
private static void SetMeshStaticForBaking(GameObject go)
{
    var flags = StaticEditorFlags.ContributeGI
              | StaticEditorFlags.BatchingStatic
              | StaticEditorFlags.OccluderStatic
              | StaticEditorFlags.OccludeeStatic;
    GameObjectUtility.SetStaticEditorFlags(go, flags);
    var mr = go.GetComponent<MeshRenderer>();
    if (mr != null)
    {
        mr.receiveGI          = ReceiveGI.Lightmaps;
        mr.scaleInLightmap    = 1.0f;
        mr.stitchLightmapSeams = true;
    }
}
```

   Call `SetMeshStaticForBaking(go)` on every generated mesh GameObject alongside `GenerateLightmapUVs`.

4. Terrain: set the terrain GameObject's static flags to include `ContributeGI`; Terrain bakes via the terrain-specific lightmap path, already correct as long as the GO is ContributeGI-flagged.

**Do NOT:**
- Run `GenerateSecondaryUVSet` on meshes every frame or on runtime-generated meshes (editor-only path).
- Set static flags on `WalkCamera`, `FlyoverCamera`, ball spawn, character spawn, flag prefab root, or any MonoBehaviour-driven animated object.
- Change UV0 — gameplay shaders read UV0 for splats, gradients, base tiling.

---

## Part B — Lighting settings preset

**Target (new):** `Assets/Settings/Lighting/Lomond_BakedLighting.lighting` (LightingSettings asset).

Create via editor menu (Assets → Create → Rendering → Lighting Settings Asset). Settings:

```
Lightmapper:             Progressive GPU
Lighting Mode:           Shadowmask
Direct Samples:          32
Indirect Samples:        256
Environment Samples:     256
Min Bounces:             1
Max Bounces:             2
Filter:                  Gaussian (direct 1px, indirect 2px, ao 1px)
Lightmap Resolution:     12 texels/unit
Lightmap Size:           1024
Lightmap Padding:        2
Compress Lightmaps:      on
Ambient Occlusion:       on (Max Distance 1.0, Indirect 1.0, Direct 0.0)
Directional Mode:        Non-Directional
Albedo Boost:            1.0
Indirect Intensity:      1.0
```

**Per-scene lighting configuration** (applied once, then saved with the scene):
- Directional light (sun): Mode = **Mixed**, Shadow Type = Soft Shadows, Strength = 1.0, Bias/Normal Bias defaults.
- Any point/spot lights (flag lamps, etc., if added later): Mode = **Baked**.
- Environment: whatever skybox is in use; intensity multiplier 1.0.

Part C handles applying this asset to all 18 scenes programmatically.

---

## Part C — Automated bake menu item

**Target (new file):** `Assets/Scripts/Editor/Lighting/BakeAllHoles.cs`.
Namespace: `Golfin.CourseImport.Lighting`.

**Menu items:**

1. `Golfin → Lighting → Apply Baked Lighting Settings to All Holes` — opens each hole scene, assigns the `Lomond_BakedLighting.lighting` asset via `Lightmapping.lightingSettings`, configures the directional light to Mixed + Soft Shadows, saves the scene. Does NOT bake — prep only, fast (<30s for 18 scenes).

2. `Golfin → Lighting → Bake All Holes` — prep pass (same as above) followed by `Lightmapping.BakeMultipleScenes(scenePaths)`. Unity handles scene switching, saving, bake, moving to the next. Show a progress bar with current hole number and phase. Estimated time: 5–30 minutes total depending on GPU (Progressive GPU on a 3060 bakes one hole in ~30s–2min).

3. `Golfin → Lighting → Bake Current Hole Only` — bakes just the active scene. Useful for iterating on one hole without re-baking the course.

4. `Golfin → Lighting → Clear All Lightmaps` — calls `Lightmapping.Clear()` and `Lightmapping.ClearDiskCache()` after a "Are you sure?" dialog. Useful before a fresh course-wide bake.

**Skeleton:**

```csharp
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.CourseImport.Lighting
{
    public static class BakeAllHoles
    {
        private const string HoleScenesDir = "Assets/Courses/LomondCC/Holes";
        private const string LightingAsset =
            "Assets/Settings/Lighting/Lomond_BakedLighting.lighting";

        [MenuItem("Golfin/Lighting/Apply Baked Lighting Settings to All Holes")]
        public static void ApplyToAll()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingAsset);
            if (settings == null)
            { Debug.LogError($"[BakeAllHoles] Missing {LightingAsset}"); return; }

            var scenes = HoleScenePaths();
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Applying Lighting Settings",
                    scenes[i], (float)i / scenes.Length);
                EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
                Lightmapping.lightingSettings = settings;
                ConfigureSceneLights();
                EditorSceneManager.SaveOpenScenes();
            }
            EditorUtility.ClearProgressBar();
            Debug.Log($"[BakeAllHoles] Applied to {scenes.Length} scenes.");
        }

        [MenuItem("Golfin/Lighting/Bake All Holes")]
        public static void BakeAll()
        {
            ApplyToAll();
            var scenes = HoleScenePaths();
            Debug.Log($"[BakeAllHoles] Starting bake of {scenes.Length} scenes...");
            Lightmapping.BakeMultipleScenes(scenes);
            Debug.Log("[BakeAllHoles] Bake complete.");
        }

        [MenuItem("Golfin/Lighting/Bake Current Hole Only")]
        public static void BakeCurrent()
        {
            ConfigureSceneLights();
            Lightmapping.Bake();
        }

        [MenuItem("Golfin/Lighting/Clear All Lightmaps")]
        public static void ClearAll()
        {
            if (!EditorUtility.DisplayDialog("Clear Lightmaps",
                "Clear all baked lightmap data and disk cache?", "Clear", "Cancel"))
                return;
            Lightmapping.Clear();
            Lightmapping.ClearDiskCache();
        }

        private static string[] HoleScenePaths()
        {
            if (!Directory.Exists(HoleScenesDir)) return new string[0];
            return Directory.GetFiles(HoleScenesDir, "hole-*.unity")
                .OrderBy(p => p).ToArray();
        }

        private static void ConfigureSceneLights()
        {
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.lightmapBakeType = LightmapBakeType.Mixed;
                    light.shadows = LightShadows.Soft;
                }
                else
                {
                    light.lightmapBakeType = LightmapBakeType.Baked;
                }
            }
        }
    }
}
```

**NOTE for Code:** `HoleScenesDir` — verify the actual hole-scene directory. Current memory says `Assets/Courses/LomondCC/Holes/hole-XX.unity` but confirm before shipping. If scenes live elsewhere, fix the constant.

---

## Part D — Quality/Graphics settings tuning

**Target:** `Project Settings → Quality` (Mobile tier) and `Project Settings → Graphics` (URP asset).

Manual one-time changes, document here so Claude Code knows what to set:

- **URP Asset (Mobile):** Shadows → Max Distance = **25m** (down from default). Cascade Count = **1** (down from 4). Soft Shadows = on, Low quality. Main Light shadows resolution = 1024.
- **Quality tier (Mobile):** Shadowmask Mode = **Distance Shadowmask** (baked shadows far, real-time near — cheaper) or **Shadowmask** (baked at all ranges — cheapest, less accurate). Pick Distance Shadowmask as default; swap to Shadowmask if perf is still short.
- **Lightmap Streaming:** on.

Leave PC/Editor quality tier alone so bakes look correct during authoring.

---

## Part E — Gitignore + asset hygiene

Baked lightmap data lives in `Assets/Courses/LomondCC/Holes/hole-XX/` as `Lightmap-*.exr`, `ReflectionProbe-*.exr`, `LightingData.asset`. These are large (1–5 MB each × 18 holes = up to 90 MB). **Commit them** — bakes are deterministic-ish but slow; CI shouldn't rebake. Add an LFS pattern if the repo uses LFS:

```
*.exr filter=lfs diff=lfs merge=lfs -text
LightingData.asset filter=lfs diff=lfs merge=lfs -text
```

If not on LFS, just commit and move on.

---

## Verification

1. Open Hole 1 Geo. Run `Golfin → Lighting → Bake Current Hole Only`. Bake completes in under 5 min. Scene renders with visible baked shadows under trees/bunker lips/buildings.
2. Move the ball in Play mode — ball casts a real-time shadow, scenery shadows are unchanged (they're baked). Shadow distance = 25m, so far scenery shadows are baked-only, near is mixed.
3. Player frame profiler on mid-tier Android (Pixel 6a / equivalent): directional light shadow cost < 0.5ms. Lightmap sampling adds ~0.1ms to opaque pass. Net win ≥ 2ms vs real-time.
4. Re-import Hole 1 Geo via HoleGeoImporter. Re-open scene — meshes still have valid UV2 (check a mesh in Inspector, "UV Channels" shows UV2 present). Bake does NOT need to be redone immediately; however, after re-import the bake is stale (mesh topology may have shifted). Re-bake Hole 1 only — confirms the re-bake flow works.
5. `Golfin → Lighting → Bake All Holes` on a weekend. All 18 scenes bake unattended. No crashes, no scene-corruption, progress bar advances through each.
6. Regression: Hole 12 water, Hole 7 shore — no visual regression from baked AO/indirect (water is unlit in URP default, should bake as a flat surface; verify shore serrations from Phase 2c stay fixed).
7. Build size: measure APK/IPA delta. Lightmaps add roughly 30–60 MB. Acceptable.

## Watch for

- **UV2 seams on large meshes:** if greens or fairways show visible seam lines in baked AO, increase `packMargin` to `0.01f` or bump lightmap resolution on that mesh via `MeshRenderer.scaleInLightmap = 2.0f` on the affected GO.
- **Bunker lip / green collar submeshes:** UV2 unwrapping packs both submeshes into one chart set — verify no light leak between sand and lip. If leak, the fix is to separate the submeshes into two GameObjects before baking (not recommended — breaks the submesh breadcrumb system).
- **Trees as terrain detail vs prefab:** if trees are painted via `TreeInstance` on the terrain, Unity bakes them via billboard shadowmask (separate path). If trees are `GameObject` prefabs placed by `TreePlacer`, they need `ContributeGI` + lightmap-static flags — check `TreePlacer.cs` and apply `SetMeshStaticForBaking` equivalent when instantiating.
- **Dynamic range on bright days:** if the scene skybox is very bright, indirect bounce can blow out green surfaces. Lower `IndirectIntensity` to 0.8 if it happens.
- **Cart paths:** they're thin ribbons with tight curves — UV2 auto-unwrap sometimes produces slivers. If cart paths show blotchy AO, set `scaleInLightmap = 0.25f` on the cart path GO (they don't need high-res baked shadows — the mesh is the visual, shadows on it are subtle).

## Out of scope (future)

- Reflection probes (skybox reflection is enough for a golf course; no water reflections planned until the long-term water shader).
- Real-time GI or adaptive probe volumes.
- Per-hole time-of-day variation (baked = one lighting condition per hole).
- Dynamic weather affecting baked lighting.

## Do NOT change

- Any gameplay scripts, `WalkCamera`, `FlyoverCamera`, ball/character prefabs.
- Existing materials (they already have `enableLightmapping = true` on URP/Lit by default).
- The terrain splatmap, zone meshes, or any import-time geometry logic beyond adding the UV2 + static-flag calls.
- Shadow biases on the directional light unless visible shadow acne appears — then lower Normal Bias to 0.02.
