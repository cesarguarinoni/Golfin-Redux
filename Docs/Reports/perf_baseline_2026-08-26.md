# perf_baseline_2026-08-26.md — Phase 0 baseline (PERF_OPTIMIZATION_PLAN §5)

> Scope: **measure and report only.** No gameplay/rendering code changed, no scene edits, no commits
> to `Assets/`. Task brief: `Docs/TellCode.md` kickoff → Phase 0 of `Docs/PERF_OPTIMIZATION_PLAN.md`.
> Author: Claude Code, 2026-08-26.

---

## 0. Status — READ FIRST

> **UPDATE, same day — THE DEVICE HALF RAN.** An iPhone 15 Pro Max was attached later in the
> session, a Development build was made and installed, and the captures below were taken on real
> hardware. See **§9** for device measurements, which supersede the "PENDING DEVICE" notes in §6.
> Two things in this static report turned out to be WRONG at runtime and are corrected in §9.3.

**The static half settles all five TL;DR items — four outright, one (#1) with a caveat that makes
it smaller than the plan hoped. §9 then confirms #1 and #5 on the device itself.**

| Blocker | Evidence | What it costs |
|---|---|---|
| **No iPhone attached.** The brief assumes "physical iPhone over USB". Both paired iPhones report `unavailable`. | `xcrun devicectl list devices` → `The Dark Urge … iPhone 15 Pro Max … unavailable`, `ken (4) … iPhone 16 Pro Max … unavailable`; `xcrun xctrace list devices` lists them under **Devices Offline**; `system_profiler SPUSBDataType` shows no iOS device. | Profiler ms, GPU ms, batches/SetPass/tris, culling time, Memory Profiler snapshot, Xcode thermal state, and all five before/after experiments. |
| **Unity Editor is held by another session.** Instructed not to step over it. | `Unity.app/Contents/MacOS/Unity -projectPath /Users/cesar/Documents/GolfinRedux` running since 17:02, plus two AssetImportWorkers. | The Editor-side fallback (play mode + Frame Debugger + `profiler-get-rendering-stats`) is also off the table this session. No screenshots exist: no frame was rendered by me. |

Everything below is derived from the shipping asset/scene/code files and the built player data on
disk. Where a claim needs a rendered frame to close, it is marked **PENDING DEVICE** and §6 carries
the exact capture procedure and the empty tables to paste numbers into.

**Bottom line:** items **#1, #2, #3, #4, #5 are all CONFIRMED at the configuration level**, three of
them (#2, #4, #5) with evidence strong enough that a Frame Debugger pass would be a formality.
Item #5 is *worse* than the plan assumed; item #1 is real but **smaller** than "2×". Three new
findings (§5.1, §5.2, §5.3) change what Phase 1 should do.

---

## 1. Item-by-item verdicts (plan §0 TL;DR #1–#5)

### #1 — "The hole is very likely rendered twice per frame" → **CONFIRMED (configuration); cost PENDING DEVICE**

Two enabled **Base** cameras exist simultaneously during a hole, and nothing disables either.

| | ShellScene `Main Camera` | LabScaffold `Main Camera` |
|---|---|---|
| file | `Assets/Scenes/ShellScene.unity` | `Assets/Scenes/Physics/LabScaffold.unity` |
| GameObject active | `1` (scene root, `m_Father: 0`) | `1` |
| Camera `m_Enabled` | `1` | `1` |
| URP `m_CameraType` | `0` = **Base** | `0` = **Base** |
| `m_Depth` | `-1` | `0` |
| `m_ClearFlags` | `1` = **Skybox** | `1` = **Skybox** |
| `m_CullingMask` | `4294967295` = Everything | Everything |
| far clip | 1000 | 3000 |
| `m_RenderShadows` | **`1`** | `1` |
| `m_RenderPostProcessing` | **`1`** (Bloom + Vignette + Tonemap) | `0` |
| transform | `(0, 1, -10)`, identity rotation, FOV 60 | (chase rig) |

Both are Base cameras, so this is **not** a URP camera stack — it is two independent renders of the
same world, each with its own culling, shadow rendering and (for the shell) a full post chain.

**Nothing turns the shell camera off.** The hole-load hook disables the shell *light* only:

- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:2196` → `DisableShellDirectionalLight()`
  (definition at `:2475`) — filters `l.type != LightType.Directional` and `scene.name != "ShellScene"`,
  sets `l.enabled = false`. Cameras are never touched.
- The only `Camera.enabled` writes in the whole of `Assets/Scripts` are `PhysicsLabController.cs:1721/1731`
  (`chaseCamera`, a save/restore around a capture) and `Editor/Recording/HoleFlyoverRecorder.cs:349/412`
  (editor-only). Neither is the shell camera.
- `Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs` loads the host (`:122`) and the hole
  (`:138`) **Additive** and unloads only those two (`:202`, `:210`). ShellScene is never unloaded,
  and there is no `SetActive` on any camera anywhere in the loader.
- Confirmed in a source comment at `PhysicsLabController.cs:2190-2194`: *"ShellScene stays additively
  loaded during gameplay … and carries its own intensity-2 Directional Light."*

**Where the shell camera actually is — this changes the size of the prize.** The hole terrains
surround the world origin (terrain base positions `(-172.95, -0.4, -231.6)` H08,
`(-288.1, -0.4, -130.6)` H01, `(-114.45, -0.4, -50.3)` H06), so the shell camera at `(0, 1, -10)` is
inside their XZ footprint. But sampling the baked heightmaps at that exact XZ
(`heightmap.bytes`, GHM1, res 2049, Q16.16, decoded per `HeightmapLoader.cs:14-53`) gives the
**terrain surface height there**:

| Hole | terrain Y at world `(0, -10)` | shell camera Y | camera is |
|---|---|---|---|
| 08 | **26.08 m** | 1.0 | **≈25 m below the surface** |
| 01 | **8.00 m** | 1.0 | ≈7 m below |
| 06 | **7.91 m** | 1.0 | ≈6.9 m below |

**The shell camera is buried inside the terrain on all three capture holes.** That splits the cost of
#1 into two parts, and only one of them is certain:

- **Certain (unconditional per-camera work, geometry-independent):** skybox clear, **4 main-light
  shadow cascade passes** (`m_RenderShadows: 1`), the DepthNormals prepass + CopyDepth + DBuffer +
  ForwardEmissive forced by the decal feature (§#5), and the **full Bloom pyramid + Uber
  (Vignette + Tonemap Neutral) post chain** over the whole 0.8-scale target. None of that gets
  cheaper for being underground.
- **Uncertain (needs the frame):** the opaque draw. `TerrainLit` is single-sided, so terrain directly
  overhead is backface-culled; but the frustum (FOV 60, far 1000) rises ~tan(30°)·d and exits the
  surface at distance, so it does see sky, backdrop and distant trees. How much geometry survives
  culling from that pose is exactly what the Frame Debugger event list answers.

⚠️ **So temper the plan's "up to ~2× GPU on every hole frame."** That framing assumed the shell
camera re-renders the visible hole; from 25 m underground it very likely does not. The defensible
claim is: *one extra camera's worth of shadow cascades, prepasses and a complete post chain, plus an
unknown amount of opaque draw.* Still worth Option A — but Option A should be sized off the measured
number, not off "2×".

**What is left for the device:** how much of the frame it actually costs. Predicted event-list shape
in §6.2. *Option A in plan §4 is justified now — it does not need the Frame Debugger to be scheduled;
the Frame Debugger decides how far up the Phase 1 list it goes.*

---

### #2 — "Standalone Spruce trees" → **CONFIRMED, with exact numbers**

Measured directly from the scene YAML (`scene_stats.py`, method in §7):

| Hole | scene file | GameObjects | MeshRenderers | LODGroups | MeshRenderers casting shadows | Terrain | Lights |
|---|---|---|---|---|---|---|---|
| **06** | 0.8 MB | 42 | 29 | 0 | **29 / 29** | 1 | 1 |
| **01** | 1.9 MB | 56 | 44 | 0 | **44 / 44** | 1 | 1 |
| **02** | 1.2 MB | 39 | 27 | 0 | **27 / 27** | 1 | 1 |
| **08** | **64.9 MB** | **27,468** | **23,538** | **1,958** | **23,538 / 23,538** | 1 | 1 |

Hole 08 LOD structure — **uniform across all 1,958 trees, zero exceptions**:

```
thresholds  0.15 / 0.10333334 / 0.056666665 / 0.01     (cull at 1% screen height)
renderers   3    / 3          / 3           / 3        = 12 renderers per tree
m_LastLODIsBillboard: 0   (all 1958)   → no billboard LOD
m_FadeMode: 1 (CrossFade) + m_AnimateCrossFading: 1   (all 1958)
renderers by LOD index: {0: 5874, 1: 5874, 2: 5874, 3: 5874}  sum 23,496
```

23,496 tree renderers + 42 non-tree renderers = 23,538. **Not one renderer in Hole 08 has shadow
casting off.**

`m_FadeMode: 1` + `m_AnimateCrossFading: 1` is a detail the plan did not carry: during an LOD
transition **two LOD levels render at once** with dithered alpha, and the animated crossfade writes a
per-renderer fade value each frame. That is extra draw submission and extra fragment work exactly
when the camera is moving — i.e. during a shot.

Tree census from the sim bake (`Resources/HoleData/<course>/Hole_NN/tree_obstacles.csv`, `profileName`
column) — this is the authoritative count because it is what the ball collides with:

| Hole | bake_hash | total obstacles | standalone Spruce | terrain trees | scene LODGroups | agrees? |
|---|---|---|---|---|---|---|
| 01 | `e69023d0` | 1,362 | 0 | 1,362 (JapaneseBlack 632, ScottishPine 366, Metasequoia 364) | 0 | ✅ |
| 02 | `0519c2f0` | 2,983 | **1,495** (Spruce_1 892 + Spruce_3 603) | 1,488 | **0** | ❌ **see §5.1** |
| 06 | `a953ea8e` | 434 | 0 | 434 (Fir only) | 0 | ✅ |
| 08 | `9fa7e851` | 3,926 | **1,958** (Spruce_1 1,195 + Spruce_3 763) | 1,968 | 1,958 | ✅ |

Spruce is standalone **by construction**, not by accident:
`Assets/Scripts/Editor/CourseImporter/TreePlacer.cs:68-72` hardcodes
`ForceStandaloneNames = { "Spruce 1", "Spruce 3" }`, and `:214` sets
`standalone = forceStandalone || !hasRootLOD`. The comment at `:66-67` gives the reason as
*"particle systems, complex hierarchies that terrain trees strip"* — the plan's suspicion that this
is historical is worth testing before E2, since the prefabs
(`Assets/Realistic Tree/Prefabs/URP/Spruce/Spruce {1,3}.prefab`) are FBX variants with a root
LODGroup and no ParticleSystem in the built scene (`ParticleSystemRenderer` count in Hole 08: **0**).

---

### #3 — "Shadow setup is desktop-grade" → **CONFIRMED**

`Assets/Settings/Mobile_RPAsset.asset`:

```
m_MainLightRenderingMode: 1        (per-pixel)
m_MainLightShadowsSupported: 1
m_MainLightShadowmapResolution: 1024
m_ShadowDistance: 100
m_ShadowCascadeCount: 4            ← four cascade passes
m_Cascade4Split: {x: 0.067, y: 0.2, z: 0.467}
m_CascadeBorder: 0.2
m_SoftShadowsSupported: 0
m_ConservativeEnclosingSphere: 1
m_AdditionalLightsRenderingMode: 1 (per-pixel), m_AdditionalLightsPerObjectLimit: 4
m_AdditionalLightShadowsSupported: 0
m_RenderScale: 0.8   m_SupportsHDR: 1   m_MSAA: 1 (off)
m_UseSRPBatcher: 1   m_SupportsDynamicBatching: 0   m_GPUResidentDrawerMode: 0
m_UseAdaptivePerformance: 1        (package NOT installed)
m_SupportsLightLayers: 1   m_ReflectionProbeBlending: 1   m_ReflectionProbeBoxProjection: 1
```

- **Every renderer casts.** Hole 08: 23,538 / 23,538 MeshRenderers with `m_CastShadows != 0`.
- **Terrain casts.** `m_ShadowCastingMode: 1` on the Terrain component of all 18 holes.
- **No baked lighting anywhere.** Every gameplay scene has
  `m_LightingDataAsset: {fileID: 20201, guid: 0000000000000000f000000000000000, type: 0}` — the
  built-in default GUID, i.e. **no LightingDataAsset**. `m_MixedBakeMode: 2` (Shadowmask) is
  therefore inert. Shadows are 100 % realtime. (The `LightingData.asset` files that exist in the
  project are all under `Assets/Scenes/Original~/` and `Assets/Packs/` — not shipping scenes.)
- Hole directional lights: type Directional, `m_Lightmapping: 1` (Mixed), shadows type 2, intensity 1.2.
  ShellScene's is intensity **2**, `m_Lightmapping: 4`, and is the one `DisableShellDirectionalLight()` kills.

⚠️ **Red herring for whoever builds the tier system:** `ProjectSettings/QualitySettings.asset` level
`Mobile` says `shadowCascades: 2`, `shadowDistance: 40`. Those are the built-in-pipeline fields and
are **ignored** while a URP asset is assigned. The URP asset's `4 / 100` is what ships. Do not "fix"
the Quality level and believe the shadows changed.

---

### #4 — "Terrain renders full 9-layer splat everywhere" → **CONFIRMED**

Nine `TerrainLayer` assets per hole, identical set on 01 / 02 / 06 / 08:

```
T_Bunker_Albedo  T_Fairway_Dark  T_Fairway_Light  T_Green_Albedo  T_OB_TintedRough
T_RoadAsphalt_Albedo  T_Rough_Albedo  T_Semirough_Albedo  T_Tee_Albedo
```

Material is URP's stock `m_DefaultTerrainMaterial` (guid `594ea882c5a793440b60ff72d896021e`, declared
in `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset:187`) = `TerrainLit`. TerrainLit
blends 4 layers per pass → **9 layers = 3 passes over the terrain, everywhere, at all distances.**

Per-hole Terrain component (all 18 holes verified):

```
m_HeightmapPixelError: 5      m_SplatMapDistance: 1000   ← basemap never kicks in
m_DrawInstanced: 0            m_ShadowCastingMode: 1     m_HeightmapMaximumLOD: 0
m_DetailObjectDistance: 80    m_StaticShadowCaster: 0    m_ReflectionProbeUsage: 0
```

`m_SplatMapDistance: 1000` and `m_DrawInstanced: 0` are uniform across all 18 holes → Option C
applies globally with no per-hole special-casing.

---

### #5 — "Dead renderer feature + settings that cost for nothing" → **CONFIRMED, and worse than the plan says**

`Assets/Settings/Mobile_Renderer.asset`:

```yaml
m_Name: DecalRendererFeature
m_Active: 1
m_Settings:
  technique: 1                 # DecalTechniqueOption.DBuffer
  maxDrawDistance: 1000
  dBufferSettings: { surfaceData: 2 }   # AlbedoNormalMAOS
```

Read against the shipping URP source
(`Library/PackageCache/com.unity.render-pipelines.universal@7327e77c1cc2`):

1. **`technique: 1` is DBuffer.** Unity's own tooltip on that enum member
   (`Runtime/RendererFeatures/DecalRendererFeature.cs:35-37`):
   > *"Renders decals into DBuffer and then applied during opaque rendering. **Requires DepthNormal
   > prepass which makes not viable solution for the tile based renderers common on mobile.**"*

   iOS/Metal and Android/Vulkan are exactly the tile-based renderers that sentence warns about.

2. **A DepthNormals prepass is unconditional.** `Runtime/Decal/DBuffer/DBufferRenderPass.cs:46-47`:
   ```csharp
   var scriptableRenderPassInput = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
   ConfigureInput(scriptableRenderPassInput);
   ```
   → URP schedules a **full extra opaque scene pass** (depth + normals) on **every camera**. On
   Hole 08 that is ~5,874 visible tree renderers + terrain, again, per camera. The plan called this
   "potentially a third full scene pass"; the source makes it certain.

3. **There is no zero-decal early-out.** `DecalRendererFeature.AddRenderPasses` enqueues
   `m_CopyDepthPass`, `m_DBufferRenderPass` and `m_ForwardEmissivePass` for the DBuffer branch with no
   check on decal count. `DecalForwardEmissivePass.cs:27` adds another `ConfigureInput(Depth)`. All of
   this runs with **zero decals in the project**.

4. **NEW — the decal feature is silently disabling Native Render Pass.**
   `DecalRendererFeature.cs:541-544`:
   ```csharp
   internal override bool SupportsNativeRenderPass()
       => m_Technique == DecalTechnique.GBuffer || m_Technique == DecalTechnique.ScreenSpace;
   ```
   DBuffer returns **false**. `Mobile_Renderer.asset` sets `m_UseNativeRenderPass: 1` and
   `UniversalRenderPipelineGlobalSettings.asset:32` sets `m_EnableRenderGraph: 0` (compatibility
   mode) — so the renderer is asking for native render passes, and this one unused feature is
   vetoing them. On a TBDR mobile GPU, losing native pass merging means extra tile
   store/load traffic on every pass boundary. **Deleting the feature (Option D) is not just "free
   to remove" — it re-enables a mobile optimisation that is currently switched off.**

Also confirmed from the same assets: `m_SupportsHDR: 1`, `m_UseAdaptivePerformance: 1` with no
Adaptive Performance package, `m_DepthPrimingMode: 0`, `m_RenderingMode: 0` (Forward),
`m_GPUResidentDrawerMode: 0`.

---

## 2. Verdict summary

| # | Claim | Verdict | Strength |
|---|---|---|---|
| 1 | Hole rendered twice per frame | **CONFIRMED as "two cameras render"** (two enabled Base cameras, post ON + shadows ON on the shell one, nothing disables it). **But the shell camera is ~25 m UNDER the terrain on H08** — the certain cost is 4 cascade passes + prepasses + a full post chain, not a second copy of the visible hole. | Config-certain; **frame cost PENDING DEVICE, and the plan's "2×" is probably too generous** |
| 2 | Standalone Spruce dominate | **CONFIRMED** (H08: 27,468 GO / 23,538 MR / 1,958 LODGroups / 12 renderers per tree / 100 % shadow casters / no billboard / animated crossfade) | Certain |
| 3 | Shadow setup desktop-grade | **CONFIRMED** (4 cascades, 100 m, 1024, all casters, terrain casts, no LightingDataAsset) | Certain |
| 4 | Terrain full 9-layer splat everywhere | **CONFIRMED** (9 layers → 3 TerrainLit passes, splat distance 1000, instancing off, all 18 holes) | Certain |
| 5 | Dead renderer feature costs for nothing | **CONFIRMED, understated** (DBuffer → unconditional DepthNormals prepass + CopyDepth + ForwardEmissive, zero decals, **and it disables Native Render Pass**) | Source-certain |

Nothing was refuted. One sub-suspicion I raised and killed myself: see §5.2.

---

## 3. Memory / size baseline (disk-measured — the parts that don't need a device)

Built player data, `Builds/iOS-Dev/Data` (built 2026-08-17 09:16) and `Builds/iOS-Full/Data`
(2026-08-25 16:39):

| | iOS-Dev | iOS-Full |
|---|---|---|
| `Data/` total | **1.1 GB** | **1.2 GB** |
| `resources.assets` | **388.8 MB** | 389.2 MB |
| `globalgamemanagers.assets` | 1.1 MB | 1.1 MB |
| `sharedassets0.assets` | 12.8 MB | 10.6 MB |
| per-hole `sharedassetsN` | 28.3 – 29.0 MB × 18 | same |

`Assets/Resources/HoleData` = **388 MB** on disk, i.e. essentially all of `resources.assets`.
Per capture hole:

| Hole | total | `heightmap.bytes` | `zones.json` | `green.json` | `tree_obstacles.csv` | TerrainData asset |
|---|---|---|---|---|---|---|
| 01 | 24 MB | 16.02 MB | 7.93 MB | 0.06 MB | 0.07 MB | 28 MB |
| 02 | 21 MB | 16.02 MB | 4.82 MB | 0.06 MB | 0.14 MB | 28 MB |
| 06 | 19 MB | 16.02 MB | 2.97 MB | 0.07 MB | 0.02 MB | 28 MB |
| 08 | 24 MB | 16.02 MB | 7.89 MB | 0.06 MB | 0.18 MB | 29.7 MB |

**Managed spike per hole load, derived from source** —
`Assets/Scripts/Physics/Runtime/HeightmapLoader.cs:45-47`:
```csharp
var heights = new int[res * res];
for (int i = 0; i < heights.Length; i++) heights[i] = br.ReadInt32();
```
`res = 2049` → `int[4,198,401]` = **16.79 MB on the LOH**, plus the 16.02 MB `byte[]` the TextAsset
hands over = **~32.8 MB transient managed per hole load**, identical on every hole. This is a
*derivation*, not a measurement: the actual GC Alloc column in the load frames is **PENDING DEVICE**.

**Audio — every clip is DecompressOnLoad.** Scanned all 460 `AudioImporter` `.meta` files:
```
loadType:            {0: 460}   → DecompressOnLoad, 460/460
compressionFormat:   {1: 460}   → Vorbis, 460/460
quality:             {1: 460}   → 100%, 460/460
```
Not one clip is Streaming or CompressedInMemory. Confirms plan §1.4 / Option J.

**MapView GPU readback is unguarded in player builds.**
`Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs:525` (`"open"`) and `:2318` (`"aimed"`) both
call `StartCoroutine(DoFrameReadbackAndDump(...))` with **no `#if UNITY_EDITOR` / `DEVELOPMENT_BUILD`
guard**. Two `ReadPixels` GPU sync stalls every time the map opens, on retail devices.

Texture / Mesh / AudioClip resident totals and the top-10-by-size table are **PENDING DEVICE**
(Memory Profiler snapshot after 01 → 08 → 06).

---

## 4. Frame Debugger — what could be established without one

The brief asks the event list to answer three questions. Two are answered from configuration; the
third needs the frame.

| Question | Answer from config | Still needs the frame? |
|---|---|---|
| **How many cameras render?** | **2** — ShellScene `Main Camera` (Base, depth −1) and LabScaffold `Main Camera` (Base, depth 0). Hole scenes ship a third (`WalkCamera`, Base, depth 0, far 2000, enabled on disk) but it is deactivated at load — see §5.2. | Only to confirm no fourth appears at runtime. |
| **Is there a DepthNormals prepass / DBuffer pass?** | **Yes, both.** DBuffer technique unconditionally enqueues CopyDepth + DBuffer + ForwardEmissive and `ConfigureInput(Depth\|Normal)` forces the DepthNormals prepass. See #5. | No — source-certain. |
| **How many shadow cascade passes?** | **4** (`m_ShadowCascadeCount: 4`), main light only (`m_AdditionalLightShadowsSupported: 0`), 1024 map, 100 m. Both cameras have `m_RenderShadows: 1`. | Whether both cameras each render their own cascade set (expected) — that is the number that decides how big Option A really is. |

**Predicted event list per frame at the Hole 08 tee** (to be diffed against the real one in §6.2):

```
Camera  ShellScene Main Camera (depth -1)
  ├─ MainLightShadowCasterPass      ×4 cascades
  ├─ DepthNormals prepass                        ← forced by DecalRendererFeature/DBuffer
  ├─ CopyDepth
  ├─ DBufferRender (0 decals)  +  DecalForwardEmissive
  ├─ DrawOpaques (from ~25 m UNDER the terrain — terrain overhead is backface-culled;
  │               expect far less than the lab camera draws. THIS is the number to read.)
  ├─ DrawSkybox / DrawTransparents
  └─ Post: Bloom pyramid (down+up) → Uber (Vignette + Tonemap Neutral)
Camera  LabScaffold Main Camera (depth 0)
  ├─ MainLightShadowCasterPass      ×4 cascades   ← again
  ├─ DepthNormals prepass                         ← again
  ├─ CopyDepth / DBufferRender / DecalForwardEmissive
  ├─ DrawOpaques (full hole: terrain ×3 + ~5,874 tree renderers at their selected LOD)
  └─ DrawSkybox / DrawTransparents   (no post)
```

If that shape is what the device shows, plan §4 Options **A + D** together remove one entire camera's
worth of work *and* two DepthNormals prepasses *and* re-enable Native Render Pass — before a single
shadow or LOD setting is touched.

---

## 5. New findings (not in the plan)

### 5.1 Hole 02 ships 1,495 invisible tree collisions — plan §7 CONFIRMED, and it is structural

`Resources/HoleData/lomond-country-club/Hole_02/tree_obstacles.csv` (bake_hash `0519c2f0`) lists
**1,495 Spruce** (`Spruce_1` 892 + `Spruce_3` 603). `Hole_02_Geo.unity` contains:

- **0** LODGroups, 27 MeshRenderers total, 39 GameObjects
- **0** occurrences of the string `Spruce` anywhere in the 1.2 MB scene file

They cannot be hiding as terrain trees either: `TreePlacer.cs:68-72` puts `"Spruce 1"` / `"Spruce 3"`
in `ForceStandaloneNames`, and `:214` (`standalone = forceStandalone || !hasRootLOD`) makes them
standalone GameObjects unconditionally — a Spruce is never a `TreeInstance` by construction.

**So the ball collides with 1,495 trees that no renderer of any kind draws.** Not fixed here (out of
scope, as instructed). It needs its own task before the beta; note that any fix which re-places the
trees must preserve the bake — the acceptance test is `bake_hash` equality, same as plan §5 Phase 3/E2.

### 5.2 Hole scenes ship a third enabled camera — but it is neutralised at load (self-refuted)

Every `Hole_NN_Geo.unity` contains `WalkCamera`: GameObject active, camera enabled, **Base**,
depth 0, clear = Skybox, far 2000. On disk that reads like a third full render stacked on #1.

It is not. `PhysicsLabController` deactivates the **GameObject** (not just the component, so `Start()`
never fires and the cursor is never stolen) in two places:
- `:301-302` → `DeactivateWalkCamerasInLoadedScenes()` at Awake, for scenes already loaded
- `:2053-2065` in the hole-load path, walking `loadedSceneEarly.GetRootGameObjects()`

Recording it because it is one `SetActive(false)` away from being a third camera, and the Frame
Debugger pass should confirm only two cameras appear.

### 5.3 Terrain tree draw distance is inconsistent across holes — and it confounds the chosen baseline

| Holes | `m_TreeDistance` | `m_TreeBillboardDistance` | `m_TreeCrossFadeLength` |
|---|---|---|---|
| **01, 02, 06** | **5000** | **50** | 5 |
| 03–05, 07–18 | 150 | 80 | 20 |

Those three holes are exactly the three with zero standalone Spruce — the older import batch.

**This matters for Phase 0 itself.** Two of the three chosen capture holes (01 and 06) draw terrain
trees to **5,000 m** with billboards starting at 50 m, while Hole 08 stops at 150 m with billboards
at 80 m. Hole 01 "terrain trees only" vs Hole 08 "worst case" is therefore **not** a clean
tree-count comparison — the two holes are running different terrain-tree budgets. Record the
`treeDistance` alongside every capture, and treat the 01↔08 delta as tree-system + draw-distance
combined, not tree count alone.

This also lands on plan §2's fairness rule: tree draw/cull distance is listed as NOT-allowed-to-vary
between devices. It already varies **between holes**, which is a content inconsistency the tier work
should normalise (to 150/80, matching the 15 newer holes) rather than inherit.

---

## 6. PENDING DEVICE — turnkey procedure

Everything here is ready to run the moment an iPhone is on USB. Nothing below requires re-deriving
anything in this report.

### 6.1 Setup
1. Attach the iPhone 15 Pro Max (`The Dark Urge`) over USB; confirm with
   ```bash
   xcrun devicectl list devices
   ```
   It must read `available`, not `unavailable`.
2. Unity build profile **Dev-iOS**: Development Build ✅, Autoconnect Profiler ✅, **Deep Profiling OFF**.
   Do **not** drive `BuildPipeline.BuildPlayer` over MCP (standing rule — it queues ~10 builds on retry).
3. Xcode → run on device. Unity → Window ▸ Analysis ▸ Profiler, attach to the player;
   Window ▸ Analysis ▸ Frame Debugger, attach to the player.
4. Enable Profiler modules: CPU, GPU, Rendering, Memory.

### 6.2 Per hole — same pose every time (tee, default aim camera, after the tee-idle glow settles)

| Metric | H06 (0 Spruce, treeDist 5000) | H01 (terrain trees only, treeDist 5000) | H08 tee (1,958 Spruce, treeDist 150) | H08 mid-flight (driver) |
|---|---|---|---|---|
| CPU main thread (ms) | | | | |
| Render thread (ms) | | | | |
| GPU (ms) | | | | |
| Frame rate | | | | |
| Batches | | | | |
| SetPass calls | | | | |
| Tris | | | | |
| Verts | | | | |
| Shadow casters | | | | |
| Culling time (ms) | | | | |

Frame Debugger event list → save as text to
`Docs/Reports/Media/framedebug_hole<NN>_2026-XX-XX.txt`, and fill in:

| | H06 | H01 | H08 |
|---|---|---|---|
| Cameras that render (expect **2**) | | | |
| Shadow cascade passes (expect 4 **per camera** = 8) | | | |
| DepthNormals prepass present? (expect **yes ×2**) | | | |
| DBuffer pass present? (expect **yes ×2**, 0 decals) | | | |
| Bloom chain present? (expect yes, shell camera only) | | | |

### 6.3 Memory
Play 01 → 08 → 06 consecutively, then Memory Profiler snapshot:
Texture2D / Mesh / AudioClip / Managed heap totals + top 10 objects by size. Separately, watch the
Profiler Memory module **GC Alloc** column across the hole-load frames and compare against the
**~32.8 MB** derived in §3 (16.79 MB `int[2049²]` + 16.02 MB `byte[]`).

### 6.4 Thermal
10 minutes on Hole 08 → Xcode Energy/Thermal state (Nominal / Fair / Serious / Critical) + the frame
rate at minute 0, 5 and 10. Whether it throttles is what decides plan Option H.

### 6.5 Experiments (Editor/Inspector only, revert after each; Hole 08 tee pose; report GPU ms + batches before/after)

| | Change | Where | Prediction from §1, to be checked |
|---|---|---|---|
| **a** | ShellScene `Main Camera` disabled during the hole | scene camera `m_Enabled` | Removes 4 cascade passes, 1 DepthNormals prepass, 1 CopyDepth + DBuffer + ForwardEmissive, the skybox clear and the **whole Bloom+Uber chain** — all certain. The opaque draw it removes is unknown and probably small (camera is ~25 m underground). Expect a solid win driven by post + shadows, **not** the "2×" the plan hoped for. |
| **b** | Cascades 4→1, shadow distance 100→40 | `Mobile_RPAsset.asset` | Large. 3 fewer cascade passes **per camera** (6 fewer while (a) is not applied). |
| **c** | Terrain `basemapDistance` 1000→100, `drawInstanced` ON | Terrain inspector | Fragment win at distance (9 layers / 3 passes → basemap beyond 100 m); instancing cuts terrain patch draw submission. |
| **d** | Remove `DecalRendererFeature` | `Mobile_Renderer.asset` | Removes DepthNormals + CopyDepth + DBuffer + ForwardEmissive **per camera**, and re-enables Native Render Pass (§#5.4). Expect a bigger win than "dead feature" suggests. |
| **e** | `QualitySettings.maximumLODLevel = 1` | runtime/Quality | **Expect ≈ 0 at the tee.** LOD0 needs >15 % screen height (`screenRelativeHeight: 0.15`); at the tee essentially no Spruce qualifies. Worth measuring mid-flight/near-tree instead — record where it *does* bite. |

⚠️ Reverting: use `Edit` for surgical reverts, never `git checkout -- <file>`; and per plan scope,
none of a–e gets committed.

---

## 7. Method / reproducibility

- Scene analysis: `scene_stats.py` (scratchpad, this session) — splits `--- !u!<classID> &<fileID>`
  documents out of the scene YAML and counts by class (1 GameObject, 20 Camera, 23 MeshRenderer,
  108 Light, 205 LODGroup, 218 Terrain), reading `m_Enabled` / `m_IsActive` / `m_CastShadows` /
  camera + terrain fields per document. Cross-check: its Hole 08 figures (27,468 / 23,538 / 1,958)
  reproduce the plan's independently-derived counts exactly.
- Tree census: `awk` over the `profileName` column of each `tree_obstacles.csv`, skipping the
  `# bake_hash=` line and the header.
- URP behaviour: read from the shipping package source at
  `Library/PackageCache/com.unity.render-pipelines.universal@7327e77c1cc2`, not from documentation.
- Audio: every `.meta` in `Assets/` containing `AudioImporter`, `loadType` / `compressionFormat` /
  `quality` tallied (460 files).
- Terrain heights: `heightmap.bytes` decoded directly per the format documented in
  `HeightmapLoader.cs:8-10` — 36-byte `GHM1` header (version, `res`, `sizeX/Z`, `posX/Y/Z`, format),
  then row-major `[y, x]` int32 Q16.16. World XZ → uv → grid index → `posY + q/65536`. Header
  self-check: decoded `pos` matches each hole's Terrain GO transform exactly
  (H08 `(-172.95, -0.4, -231.6)`), and `res` is 2049 on all three, so the indexing is right.
- No Unity Editor state was read or written this session (another session held it); no `Assets/`
  file was modified; no screenshots were taken because no frame was rendered.

---

## 8. What Phase 1 should do with this

The plan's Phase 1 list (Options A, C, D, G-swappy, MapView guard, audio load types) is unchanged and
now has evidence behind every line. Two amendments:

0. **Re-rank A after the frame is read, not before.** §1 shows the shell camera renders from
   ~25 m under the terrain, so its opaque cost is probably modest; its certain cost is a post chain
   plus 4 cascade passes plus a prepass set. It is still a clear win and still cheap to do — just
   size it off the measured GPU ms rather than the plan's "up to ~2×".
1. **Option D moves up next to A in priority.** It was filed as "free to remove"; it is actually
   removing two full DepthNormals prepasses per frame *and* un-vetoing Native Render Pass on a TBDR
   GPU. A + D are the same commit-sized effort and should be measured together and separately.
2. **Add: normalise `m_TreeDistance` / `m_TreeBillboardDistance` on holes 01, 02, 06** (5000/50 →
   150/80) so all 18 holes share one tree budget. §5.3 — this is a fairness-rule item, not just perf.

Out of scope here and still open: **Hole 02's 1,495 invisible tree collisions (§5.1)** — needs its own
task before the beta.


---

# 9. DEVICE RESULTS (added same day, after an iPhone was attached)

Device: **iPhone 15 Pro Max (iPhone16,2, A17 Pro), iOS 26.6.** Build: Golfin 1.5.7 **build 2311**,
Dev-iOS profile (Development Build ✅, Autoconnect Profiler ✅, Deep Profiling ❌), development-signed.
Poses driven hands-off by `PerfBaselineBot` (§9.5). Each row = median of 60 consecutive frames.

## 9.1 Measurements

| | Home | **H01 (cold)** | H08 (hot) | H06 (hot) | **H01 (hot)** |
|---|---|---|---|---|---|
| **fps** | 60.0 | **48.8** | 31.2 | 20.0 | **31.0** |
| wall frame time | 16.67 ms | 20.72 ms | 32.14 ms | 50.01 ms | 32.26 ms |
| CPU main thread | 3.67 ms | 3.81 ms | 5.57 ms | 6.42 ms | 5.33 ms |
| CPU render thread | 14.07 ms | 19.08 ms | 26.48 ms | 37.64 ms | 25.78 ms |
| Batches | 128 | 5,483 | 6,814 | 6,709 | 4,043 |
| SetPass calls | 45 | 163 | 188 | 145 | 155 |
| Triangles | 6,807 | 3,365,024 | 4,512,055 | 6,300,788 | 2,330,489 |
| Vertices | 16,603 | 4,998,172 | 6,356,631 | 5,387,903 | 3,021,497 |
| Shadow casters | 0 | 2,454 | 1,688 | 2,288 | 1,014 |
| System used memory | 777.9 MB | 1,157.9 MB | 1,369.5 MB | 1,186.3 MB | 1,148.2 MB |
| GC alloc / frame | 4,988 B | 29,030 B | 29,030 B | 29,030 B | — |

**The headline: 48.8 fps on the easiest hole, 31.2 fps on Hole 08 — on the fastest phone in the
tier table.** §3 assumes A15+ ("High") holds 60 fps at current settings. It does not.

⚠️ **There is no GPU-milliseconds column and there cannot be one from Unity.**
`ProfilerDriver.isGPUProfilerSupported = False` and
`GetGpuStatisticsAvailabilityState = NotSupportedByGraphicsAPI, NotSupportedWithMetal`;
`RawFrameDataView.frameGpuTimeNs` is `0` on every frame. The `GPU Frame Time` *counter* still
returns a number, but it is a CPU-side stand-in — it reported 34.85 ms of "GPU" inside a 20.72 ms
frame. Real iOS GPU timing needs **Xcode Metal System Trace / Instruments**, which is a separate
pass and is the honest way to fill plan §5's "GPU ms" line.

## 9.2 Frame Debugger — #1 and #5 CONFIRMED on device

2,011 events on the Hole 01 tee frame, containing **two complete render sequences**. Both cameras
are literally named `Main Camera`, so the boundary is the RenderPass numbering, not the label.

| | Pass one (RP 0–5) | Pass two (RP 6–11) |
|---|---|---|
| Shadowmap draws | 232 + 20 | 351 + 20 |
| **DepthNormalPrepass** | **85 + 5** | **237 + 10** |
| CopyDepth | 1 | 1 |
| Opaque draws | 255 + 4 | 711 + 10 |
| Skybox | 1 | 1 |
| **Bloom chain** | prefilter + 10 down + 5 up + UberPost + ColorGradingLUT | — |
| Ends at | *nothing* | BlitFinalToBackBuffer + UI |

- **#1 CONFIRMED.** Pass one carries the whole Bloom chain and never reaches the backbuffer — it is
  the ShellScene camera. It is **622 of 2,011 render events (31 %)** for a camera the player never
  sees, and it is *not* idle despite sitting under the terrain: 255 opaque + 232 shadow draws.
- **#5 CONFIRMED, and free money.** `DrawDepthNormalPrepass` runs on **both** cameras (85 and 237
  draws) plus two CopyDepths, forced by the DBuffer decal technique. **No DBuffer draw event exists
  at all** — there are zero decals. The prepass is paid for and nothing is drawn with it.

## 9.3 Two corrections to the static report

1. **Render Graph is ACTIVE at runtime.** Every Frame Debugger event sits under
   `ExecuteRenderGraph`, despite `UniversalRenderPipelineGlobalSettings.asset:32` carrying
   `m_EnableRenderGraph: 0`. §1.1 read that as compatibility mode and it is wrong on device.
   **Plan Option I ("Render Graph on") is already satisfied — drop it from the roadmap.**
2. **The GPU-ms column does not exist on Metal** (see §9.1). Any plan line that says "measure GPU
   ms" needs Instruments, not the Unity Profiler.

## 9.4 THERMAL — the finding that changes how Phase 1 must be run

Hole 01 was captured twice: once near-cold, once after Hole 01 + Hole 08 + Hole 06 + a crash.

| | cold | hot | Δ |
|---|---|---|---|
| fps | 48.8 | 31.0 | **−36 %** |
| wall frame | 20.72 ms | 32.26 ms | +56 % |
| CPU render thread | 19.08 ms | 25.78 ms | +35 % |
| Batches | 5,483 | 4,043 | −26 % |
| Triangles | 3,365,024 | 2,330,489 | −31 % |
| Shadow casters | 2,454 | 1,014 | −59 % |

**The device rendered ~30 % less geometry and still ran ~36 % slower.** Less work, more time — that
is thermal throttling, unambiguously, on an A17 Pro within minutes of ordinary play. Nothing
in-game reacts to it (no Adaptive Performance package installed).

Two consequences:

- **Every capture after the first is throttled.** H08 (31.2 fps) and H06 (20.0 fps) are *floors*,
  not clean baselines. Only H01-cold (48.8 fps) is near-cold. H06 in particular should be re-taken
  on a cooled device before anyone treats 20.0 fps as its number.
- **The pose is not bit-reproducible.** The same hole and the same bot produced 5,483 vs 4,043
  batches and 3.4M vs 2.3M triangles. Camera yaw and tree-LOD selection differ per run.
  **Phase 1's "re-capture Hole 08 after each change" cannot compare single captures.** Before any
  A/B is trusted it needs: (a) a cooled device, (b) a pinned camera yaw, (c) N runs and a median.

This promotes **Option H (Adaptive Performance)** from "optional, later" to a real candidate.

Unexplained and worth its own look: **Hole 06 drew 6.3M triangles** — more than Hole 08 — despite
having the least content (434 terrain trees, zero Spruce). Thermal cannot explain a *geometry*
count. Leading hypothesis: Hole 06's terrain is only **228.9 × 100.6 m** yet carries the same
2049² heightmap as every other hole (~0.11 m per sample — the densest terrain mesh in the course),
so screen-space `pixelError: 5` retains far more triangles. Untested.

Also unbudgeted anywhere in the plan: **~29 KB of GC allocation per frame** in gameplay (≈1.7 MB/s).

## 9.5 How to re-run this (the bot)

`Assets/Scripts/Dev/PerfBaselineBot.cs` drives the real entry path hands-off: splash StartButton →
Practice mode-card PLAY → hole card → `SeedSession` + `BeginGameplayLoad`, then holds the tee pose
90 s logging `[PerfBot] POSE_READY`. One hole per launch, cycling 06 → 01 → 08 via a PlayerPrefs
cursor; relaunch with `xcrun devicectl device process launch --device <id> --terminate-existing
--console com.nextinnovation.golfingame`.

**It cannot ship.** Gated on `GOLFIN_TESTBUILD`; `iOS-Full.asset` — the profile the store pipeline
builds — carries **zero** scripting defines. Verified both directions: `PerfBaselineBot` appears in
`Builds/iOS-Dev/Il2CppOutputProject/.../Golfin.DevHarness_CodeGen.c` and is **absent** from
`Builds/iOS-Full/Il2CppOutputProject`. In the Editor it is opt-in via
*GOLFIN ▸ Perf ▸ Arm Perf Baseline Bot*, so normal play-mode sessions are untouched.

Three operational notes for whoever runs this next:

- **Profiler discovery does not work on this setup.** The player multicasts on `225.0.0.222:54997`
  and the Editor never receives it. Connect by selecting the device in the Profiler's target
  dropdown; `ProfilerDriver.DirectIPConnect("<phone-ip>")` opens a socket but did **not** produce
  frames on its own.
- **Unity's iOS export omits `NSLocalNetworkUsageDescription` / `NSBonjourServices`.** They are
  patched into the built `.app`'s Info.plist and the app re-signed after every build.
- **Do not enable the remote Frame Debugger on a high-batch frame.** Doing so with `limit=30000` on
  a 5,483-batch frame crashed the app. The event list is a property of the pipeline, not the hole —
  capture it once, on the cheapest pose, and reuse it.


---

# 10. PHASE 0b — comparable captures, and the experiment sweep

Protocol per §8: device cooled between job sets, camera **yaw pinned**, **3 runs**, median reported
with all three raws. Every run records iOS `NSProcessInfo.thermalState` and saves the sampled
frame as a PNG. Device: iPhone 15 Pro Max, build 2311, Hole 08 tee unless stated.

## 10.1 What Phase 0b added to the harness

| Addition | Why |
|---|---|
| **Pinned yaw** — first run per hole records `PhysicsLabController._cameraYaw`, later runs replay it | §9.4: the same hole gave 5,483 vs 4,043 batches |
| **Thermal state** — `NSProcessInfo.thermalState` via `Assets/Plugins/iOS/GolfinThermal.m` | §8: throttling was "leading explanation but unproven" |
| **On-device counters** — `ProfilerRecorder`, 60-frame medians logged to the device console | Removes the Editor, the profiler socket and window focus from the loop entirely |
| **Frame capture** — a PNG saved beside every measurement | **Experiment (d) rendered a black terrain and read as a 2× win.** A number without a frame is not evidence |
| **Runtime-only experiments** | Nothing is written to an asset, so "reverted" is structural, not remembered |
| **Job override file** | `devicectl device copy to … Documents/perfbot/job.txt` names the exact job from the Mac |

⚠️ **The pinned yaw was necessary but NOT sufficient.** All three baseline runs replayed 52.23°, yet
run 1 still differed (6,086 batches / 3.6 M tris vs 7,375 / 5.03 M for runs 0 and 2, which were
*bit-identical*). The outlier appears to be sampled before tree LOD fully resolves. **The 3-run
median absorbs it** — which is exactly why §8 demanded three runs.

## 10.2 A — cooled baselines (these replace §9.1's throttled H08/H06)

| | H08 tee | H06 tee |
|---|---|---|
| **fps** (median) | **30.1** | **35.2** |
| raws | 25.5 / 39.7 / 30.1 | 35.2 / 35.2 / 30.0 |
| thermal per run | Nominal / Nominal / Fair | Nominal / Nominal / Fair |
| wall frame ms | 33.24 | 28.45 |
| **render-thread ms** | **26.11** | 26.59 |
| main-thread ms | 5.41 | 4.32 |
| batches | 7,375 | 6,849 |
| SetPass | 199 | 156 |
| triangles | 5,036,446 | **6,861,172** |
| shadow casters | 2,249 | 2,428 |
| system memory | 1,356 MB | 1,186 MB |

**§9's H06 "20.0 fps" was pure throttling — cooled, H06 runs at 35.2 fps and is FASTER than H08.**
It nevertheless draws **1.8 M more triangles with fewer batches**, which is the signature of one
huge terrain mesh rather than many trees → see §10.5.

## 10.3 B — experiments (Hole 08 tee, cooled, 3 runs, median)

| | baseline | **(a) shell cam off** | (b) cascades 1 / dist 40 | (c) basemap 100 + instanced | (d) decal feature off |
|---|---|---|---|---|---|
| **fps** | 30.1 | **59.8** | 39.8 | 45.2 | ~~59.9~~ |
| raws | 25.5/39.7/30.1 | 59.8/60.2/59.7 | 45.8/34.3/39.8 | 58.1/45.2/31.9 | ~~47.2/59.9/59.7~~ |
| **render ms** | **26.11** | **14.48** | 22.42 | 19.80 | ~~14.51~~ |
| wall ms | 33.24 | 16.71 | 25.12 | 22.14 | ~~16.75~~ |
| batches | 7,375 | 3,917 | 5,358 | 4,610 | ~~4,712~~ |
| SetPass | 199 | 111 | 141 | 190 | ~~159~~ |
| triangles | 5,036,446 | 2,402,157 | 3,113,957 | 3,126,037 | ~~3,377,235~~ |
| shadow casters | 2,249 | 679 | **232** | 1,203 | ~~1,688~~ |
| **frame correct?** | ✅ | ✅ | ✅ | ✅ | ❌ **BLACK TERRAIN** |

### THE ANSWER: (a) is the biggest win in render-thread ms

**Disabling the ShellScene camera takes render-thread time from 26.11 ms to 14.48 ms (−45 %) and
fps from 30.1 to 59.8 — it pins Hole 08, the worst hole, at the 60 fps cap.**

**Ranking by render-thread ms saved, all frame-verified:**

| rank | experiment | render ms | Δ vs baseline 26.11 ms |
|---|---|---|---|
| 1 | **(a+d) together** | 14.09 | **−12.02 ms** |
| 2 | (a) shell camera off | 14.48 | −11.63 ms |
| 3 | (d) decal feature off *(asset)* | 15.05 | −11.06 ms |
| 4 | (c) terrain basemap + instancing | 19.80 | −6.31 ms |
| 5 | (b) cascades 1 / distance 40 | 22.42 | −3.69 ms |

(a) and (d) are individually near-identical in size, and they compose: together they beat either
alone, and they are the cheapest pair to ship — one line of camera lifecycle and one asset flag.

A second result matters as much as the headline: (a)'s three runs read **59.8 / 60.2 / 59.7 fps at
thermal Nominal / Fair / Serious.** With the shell camera gone the frame has enough headroom that
throttling no longer pushes it over budget. (a) does not merely raise fps — it makes the frame rate
*stop depending on temperature*.

### (d) and (a+d), re-tested PROPERLY via the asset

The runtime toggle is invalid (below), so (d) was re-run with `m_Active: 0` set on the
DecalRendererFeature in `Mobile_Renderer.asset` and a full rebuild — then **the asset was reverted**
(`git status` clean, byte-identical to HEAD). (a+d) = that build plus the runtime shell-camera
disable, which IS valid.

| Hole 08 tee, cooled | baseline | **(d) asset, decal off** | **(a+d)** |
|---|---|---|---|
| **fps** | 30.1 | **58.7** | **59.8** |
| raws | 25.5/39.7/30.1 | 58.7/59.9/34.2 | 59.8/59.4/60.6 |
| thermal per run | Nom/Nom/Fair | Nom/Fair/**Serious** | Nom/Nom/Fair |
| **render ms** | **26.11** | **15.05** | **14.09** |
| wall ms | 33.24 | 17.03 | 16.71 |
| batches | 7,375 | 4,712 | **2,430** |
| SetPass | 199 | 159 | **92** |
| triangles | 5,036,446 | 3,377,235 | **1,410,076** |
| shadow casters | 2,249 | 1,688 | 535 |
| frame correct? | ✅ | ✅ | ✅ |

**Both are valid and both are large.** Done through the asset, (d) is −11.06 ms render thread —
essentially tied with (a)'s −11.63 ms, which vindicates §8's decision to promote Option D beside
Option A. Together they take Hole 08 from **7,375 batches / 5.03 M tris to 2,430 / 1.41 M — a 67 %
cut in batches and 72 % in triangles**, at 59.8 fps.

Two runs of (a+d) returned 2,430 vs 2,429 batches and 1,410,076 vs 1,410,074 triangles — **the most
reproducible measurement of the whole exercise**, because with both the second camera and the
prepass gone there is far less left to vary.

⚠️ (d) alone still fell to 34.2 fps once thermal reached **Serious**; (a) and (a+d) held ~60 fps
throughout. So (a) is the one that buys thermal headroom, not (d).

### ⚠️ The RUNTIME feature-disable is void — it corrupts the render

Disabling `DecalRendererFeature` at runtime via `SetActive(false)` + `SetDirty()` renders **the
terrain and all trees fully black** (frame: `Bd_decalfeature_off_run0.png`). A black, unlit,
untextured scene draws far less work, so every counter improved and it read as a 2× win. **It was
caught only because Cesar looked at the phone** — there is no error in the log; the corruption is
silent.

Cause is almost certainly the Render Graph pass list: the renderer was already built, and removing
the feature strips the DepthNormals/CopyDepth resources the opaque pass still expects.

**Consequence:** `SetActive(false)` at runtime is not a valid test for a renderer feature — it is
only safe to remove one at build time. The numbers it produced (~59.9 fps, 14.51 ms) are within a
few percent of the *correct* asset-level result, which is precisely why it was so easy to believe.
Superseded by the asset test above.

## 10.4 Frames — every experiment was looked at, not just measured

Saved on-device to `Documents/perfbot/*.png`, pulled with
`devicectl device copy from --domain-type appDataContainer`, and checked by eye. Committed to
`Docs/Reports/perf_baseline_2026-08-26_frames/`:

| Frame | Verdict |
|---|---|
| `exp_a_shellcam_off.png` | ✅ correct — terrain, trees, shadows, HUD |
| `exp_b_shadow_diet.png` | ✅ correct — shadows present, shorter range as intended |
| `exp_c_terrain_basemap.png` | ✅ correct |
| `exp_d_asset_CORRECT.png` | ✅ correct — indistinguishable from baseline |
| `exp_ad_CORRECT.png` | ✅ correct |
| `exp_d_RUNTIME_BROKEN_black_terrain.png` | ❌ **black terrain — the void run** |

`raw_device_stats.txt` holds all 28 raw device STATS lines behind the medians above.

## 10.4b A revert trap worth knowing about

Reverting the experiment meant reverting **two** files, not one. Building with
`DecalRendererFeature m_Active: 0` made Unity recompute its shader-stripping prefilters and write
`m_PrefilterDBufferMRT3: 0 → 1` into **`Mobile_RPAsset.asset`** — a different asset from the one
edited. Restoring `Mobile_Renderer.asset` alone would have left that behind and silently changed
which DBuffer shader variants ship.

Both are now byte-identical to HEAD (`git status` clean under `Assets/Settings/` except the
intentional `Dev-iOS.asset` profiler flag). **Anyone A/B-ing a renderer feature must diff the whole
`Assets/Settings/` folder afterwards, not just the file they touched.**

## 10.5 F — Hole 06's 6.3 M triangles: heightmap density CONFIRMED

Every hole ships the same 2049² heightmap and ~28.3 MB TerrainData regardless of terrain size:

| Hole | terrain size | m per sample | samples/m² |
|---|---|---|---|
| **06** | 228.9 × 100.6 m | **0.112 × 0.049** | **182.3** |
| 08 | 345.9 × 463.2 m | 0.169 × 0.226 | 26.2 |
| 01 | 576.2 × 261.2 m | 0.281 × 0.128 | 27.9 |
| 02 | 282.3 × 349.3 m | 0.138 × 0.171 | 41.6 |
| 12 | 299.4 × 453.5 m | 0.146 × 0.221 | 30.9 |
| 18 | 534.1 × 180.6 m | 0.261 × 0.088 | 43.5 |

Hole 06's terrain mesh is **~7× denser per m²** than Hole 08's. `heightmapPixelError: 5` is a
*screen-space* metric, so a denser mesh retains proportionally more triangles for the same screen
coverage. Device data agrees: H06 draws 6.86 M tris with **fewer** batches than H08's 5.04 M —
terrain, not trees. **Hypothesis confirmed; the fix belongs at import (heightmap resolution scaled
to terrain size), applied to every device, inside the §2 "identical terrain" rule.**

## 10.6 Still outstanding from the Phase 0b brief

| Item | Status |
|---|---|
| (d) and (a+d) | **DONE** via the asset test; the runtime-toggle numbers are void and superseded |
| (e) `maximumLODLevel = 1`, mid-flight | Not run |
| A3 — H08 mid-flight baseline | Not run. First attempt died on `TargetParameterCountException` (reflection does not apply C# default args; `EndExternalDrag(bool = false)` needs an explicit arg). Fixed in the bot, not yet re-run |
| C — Instruments Metal System Trace | Not started |
| D — Memory Profiler snapshot / top-10 / load-spike GC | Not started |
| E — GC alloc call stack behind ~29 KB/frame | Not started. The 29,030 B/frame figure is confirmed stable across every hole and every experiment |

---

# 11. Phase 1 — `perf_phase1_free_wins` device pass (2026-08-26) — **HALTED, unresolved**

> **Read this first.** The code changes are in and three Dev-iOS builds were made, installed and
> driven by the bot. The pass was **stopped by Cesar** with the tee frame still looking wrong to him
> on both the shipped configuration and the basemap variant. The performance numbers below are real
> and were taken under a controlled protocol; **the visual question is open** and moves to the
> Architect, to be reproduced in the Editor (faster than a device round-trip).

## 11.1 The finding that matters most — **the sky was never pinned, so no frame comparison in this report was controlled**

`SkyRandomizer` rolls **one sky preset per run**, seeded from `RoundSeed`, which self-seeds from
`Random.Range` on first access. Every bot launch therefore got a different sun:

| run | sky preset | sun elevation |
|---|---|---|
| build A, job 9 run 0 | `Noon (Cloudy)` | **74.5°** |
| build A, job 9 run 1 | `Classic` | 45° |
| build B, job 9 run 0 | `Morning` | **20.2°** |

A 20° morning sun throws long raking canopy shadows across the fairway; a 74.5° overhead sun does
not. **That is the "dark patch / bright patch" that appeared to come and go between builds.** It is
lighting, not geometry, and it changes on every launch.

Phase 0b pinned the camera yaw (§10.1) precisely so frames would be comparable — but not the sky.
So **every cross-frame claim in §10 was made under uncontrolled lighting**, including the ✅/❌
verdicts in §10.4 and the brightness of `exp_b` (2.7× brighter than `exp_a` — a different preset,
not a different renderer).

Fixed: `PerfBaselineBot.PinSky()` calls `SkyRandomizer.SetRoundSeed(20260826)` before the hole
loads, right next to the pinned yaw. Verified in the device log:

```
[PerfBot] SKY pinned RoundSeed=20260826
[SkyRandomizer] Run started — sky locked to 'Afternoon (Cloudy)' …
[SkyRandomizer] Applied 'Afternoon (Cloudy)' (sun 28.5° elev, yaw offset 0°).
```

**Any future frame A/B must be taken under a pinned sky or it is not evidence.**

## 11.2 Device numbers — build 2316, pinned sky + pinned yaw, 3 runs each

iPhone 15 Pro Max, `Dev-iOS` (Development + Autoconnect Profiler, Deep Profiling off), sky pinned to
`Afternoon (Cloudy)` (sun 28.5°), yaw pinned per hole. **Primary sample** = 6 s settle + 4 s window,
the same point Phase 0b's before-numbers were taken at, so the comparison is like-for-like.

| pose | fps (median · raws) | frame ms | render ms | batches | triangles | thermal per run |
|---|---|---|---|---|---|---|
| **H08 tee** | **60.0** · 59.9/60.0/60.2 | 16.67 | 14.34 | 3,014 | 2,369,599 | Nominal/Serious/Serious |
| **H01 tee** | **59.8** · 59.8/59.8/59.8 | 16.72 | 3.60 ⚠ | 1,957 | 1,072,738 | Serious ×3 |
| **H06 tee** | **60.0** · 60.0/60.2/59.8 | 16.68 | 14.75 | 4,006 | 3,882,347 | Nominal/Nominal/Serious |
| **H08 mid-flight** | **59.9** · 60.0/59.9/59.2 | 16.70 | 13.71 | 2,071 | 1,527,874 | Serious ×3 |

### Against the targets

| # | Item | Target | Result |
|---|---|---|---|
| 16 | H08 tee | ≥ 58 fps, ≤ 15.0 ms | **PASS** — 60.0 fps, 14.34 ms (was 30.1 / 26.11) |
| 17 | H01 tee | — | **PASS** — 59.8 fps |
| 17 | H06 tee | ≤ 26.59 ms | **PASS** — 14.75 ms, 60.0 fps (cooled baseline was 35.2 fps / 26.59 ms) |
| 18 | H08 mid-flight | record only | 59.9 fps / 13.71 ms / 2,071 batches — first ever measurement |
| 24 | GC per frame | — | **29,030 → 21,506 B** (−26 %), identical across all 12 runs |

**The harness is finally reproducible.** With sky and yaw both pinned, batches and triangles are
*identical* across all three runs of every hole (H08 varies by 1 batch and 2 triangles; H01, H06 not
at all). Phase 0b, with the sky unpinned, swung 7,375 vs 6,086 batches on the same pose — that
variance was the sky, not the renderer.

⚠️ **`renderMs` is not trustworthy and is not the basis of any claim above.** It intermittently
reports ~3.3–4.2 ms on frames whose `frameMs` is 16.7 (H01 raws 3.33 / 3.60 / 13.98). `fps` and
`frameMs` are stable and mutually consistent, so they carry the verdicts; `renderMs` is quoted only
where all three runs agree.

### Sustained load — the honest caveat

The **late** sample (after the 45 s pose hold, device already at thermal Serious) degrades on the two
heavy tee poses:

| pose | primary fps | late fps (median · raws) |
|---|---|---|
| H08 tee | 60.0 | **47.5** · 40.8 / 60.0 / 47.5 |
| H06 tee | 60.0 | **40.7** · 60.1 / 40.7 / 35.2 |
| H01 tee | 59.8 | 59.6 · 59.9 / 59.6 / 59.5 |
| H08 mid-flight | 59.9 | 60.0 · 60.0 / 59.9 / 60.0 |

So Phase 1 gets every pose to 60 fps *cold*, and H01 and mid-flight hold it indefinitely — but H08
and H06 tee still fall to 35–48 fps after ~45 s at thermal Serious, on a device that had been under
continuous load for ~40 minutes. That is the tier system's problem (9a) and the shadow/LOD levers of
Phase 2/3, not something Phase 1 claimed to fix. Recorded so nobody reads "60 fps" as "60 fps forever".

### Item 19 — one camera, no prepass, no CopyDepth: **PASS** (by direct state, not Frame Debugger)

`FrameDebuggerUtility` (Unity 6: `UnityEditorInternal.FrameDebuggerInternal`) reports 0 events when
driven headlessly — it needs its window repainting — so the two claims were closed with direct
runtime state instead, which is stronger than counting events:

```
CAM 'Main Camera'  ShellScene    enabled=False activeInHierarchy=True   -> renders=False
CAM 'Main Camera'  LabScaffold   enabled=True  activeInHierarchy=True   -> renders=True
CAM 'WalkCamera'   Hole_08_Geo   enabled=True  activeInHierarchy=False  -> renders=False
CAMERAS ACTUALLY RENDERING = 1
_CameraDepthTexture        = UnityBlack 4x4     _CameraNormalsTexture      = UnityBlack 4x4
_CameraDepthNormalsTexture = <null>             depthPrimingMode = Disabled
```

Both depth globals are still Unity's dummy texture and the depth-normals global is null — the
observable consequence of no CopyDepth and no DepthNormals prepass having run.

### Item 23 — MapView `ReadPixels`: **PASS** (proved from the shipped binary)

`DoFrameReadbackAndDump` has **0 matches** in `Builds/iOS-Dev/Il2CppOutputProject/Source/il2cppOutput/`
(controls: `DumpInvariants` 2 matches, `PerfBaselineBot` 8). Both blocking GPU `ReadPixels` calls are
physically absent from the device binary, not merely unreached.

### Item 22 — teardown: **PASS, 8/8 assertions, on device**

`P1_teardown` (bot job 13) drove the player's own quit path and wrote
`teardown_invariants.json` with `"fails":0`:

| assertion | verdict |
|---|---|
| in_hole_shell_camera_disabled | PASS |
| in_hole_shell_light_disabled | PASS |
| quit_driven_via_real_widget (`confirmQuitButton.onClick`) | PASS |
| returned_to_home | PASS |
| **shell_camera_re_enabled** | PASS |
| **shell_light_re_enabled** | PASS |
| labscaffold_unloaded | PASS |
| second_hole_shell_camera_disabled_again | PASS |

The two bolded rows are the §1 `OnDestroy` fix confirmed on hardware — the shell light restore that
never happened in a player build before this task. `P1_teardown_home_after_quit.png` shows Home
rendering normally after the quit; `P1_teardown_second_hole.png` shows the Next-Hole case.

## 11.3 `basemapDistance` — **reverted, and Phase 0b's −6.31 ms does not reproduce**

A/B on the device, **same pinned sky, same pinned yaw, same build**, one variable:

| | batches | tris | render ms | fps |
|---|---|---|---|---|
| `exp=none` — basemapDistance 1000 (authored) | 1,848 | 1,779,839 | **13.35** | 58.1 |
| `exp=c` — basemapDistance 100 + instanced | 1,848 | 1,779,839 | **13.48** | 58.8 |

Identical geometry; the 100 m variant is marginally **slower**. Frame diff between the two:
**mean 2.01/255**, below the render noise floor. So `basemapDistance = 100` buys nothing here —
neither the −6.31 ms §10 credited it with, nor any visual change.

It has been **removed from §3**. `drawInstanced = true` and the tree-distance normalisation stay.
Relevant context if it is ever revisited: these terrains ship `baseMapResolution = 512` over a
668 m hole = **1.30 m per basemap texel**, so the lever has little headroom before it costs detail;
raising that resolution is a TerrainData edit, which this task is barred from.

## 11.4 The flat terrain is **PRE-EXISTING** — Phase 1 did not cause it. Bisect stopped at step 0.

**Symptom (Cesar, build 2314):** the terrain renders flat untextured colour near and far — rough a
flat dark green, bunker a flat white, fairway layer a flat light green — while only the overlay
meshes (`Fairway_n`, `Tee_n`) still show texture.

**Reproduced in the Editor Game View at HEAD**, Hole 08 tee, pinned sky (`Afternoon (Cloudy)`,
sun 28.5°), pinned yaw, 1170×2532. Then the same shot in two more configurations.

> Why earlier Editor passes missed it: they rendered through an ad-hoc `screenshot-camera`, which
> does **not** exercise the real pipeline. Only the **Game View** shows it. Any future render check
> for this class of bug must go through the Game View.

| patch (40×40, luminance) | HEAD (Phase 1) | all four reverted at runtime | **pre-Phase-1 `a98008f6d`** |
|---|---|---|---|
| near fairway | 141.9 · sd **22.44** | 141.9 · sd **22.44** | 141.9 · sd **22.44** |
| mid rough left | 92.5 · sd 13.12 | 85.9 · sd 22.10 | 85.9 · sd 22.10 |
| far hillside | 77.3 · sd 2.52 | 72.6 · sd 8.84 | 72.6 · sd 8.84 |
| bunker / right rough | 62.0 · sd 22.83 | 58.2 · sd 23.12 | 59.7 · sd 24.02 |

The near fairway is **bit-identical across all three** and flat in all three. The pre-Phase-1 column
was produced by checking out `a98008f6d` for `PhysicsLabController.cs`, `Mobile_Renderer.asset` and
`Mobile_RPAsset.asset` — a real pre-Phase-1 render, not a simulation — and it matches the
runtime-reverted column exactly on the first three patches, which validates the runtime revert.

**Verdict: the flat terrain predates `perf_phase1_free_wins`.** Steps 1–4 of the bisect (drawInstanced,
Native Render Pass, decal feature, shell camera) were **not run** — step 0 answered the question.
Frames: `bisect_step0_prephase1_vs_head.png` (top pre-Phase-1, bottom HEAD), plus the three
full frames `bisect_step0_*.png`.

**This is its own task, not Phase 1's.** It is the terrain material/splat path — `TerrainLit`,
9 layers, `alphamapResolution` 1024 — rendering unlit flat colour in the Game View while overlay
meshes texture correctly. `m_UseNativeRenderPass: 1` on both `Mobile_Renderer` and `PC_Renderer`
remains an untested candidate and is the obvious first probe for whoever picks it up.

### One real Phase 1 delta found on the way

HEAD's **distant** terrain is measurably flatter than pre-Phase-1 — mid-rough sd **13.12 vs 22.10**,
far hillside **2.52 vs 8.84** — while the near field is untouched. The only §3 setting that could do
that was `drawInstanced`. It has been **removed**, which was the instruction either way: §11.3 shows
it is within noise on device (13.48 vs 13.35 ms, identical batches/tris), so it was cost without
benefit. It also carried a device-only risk that the Editor could never surface — every hole scene
ships `m_DrawInstanced: 0`, the flag is set purely at runtime, and `GraphicsSettings`
`m_InstancingStripping` is **StripUnused**, so the terrain's instanced shader variants may not exist
in a player build at all (same class as the K5 tree-wind stripping).

**§3 is now the tree-distance normalisation only.** Both halves of the Phase 0b (c) experiment are gone.

## 11.5 Harness changes (`PerfBaselineBot`)

| Change | Why |
|---|---|
| `PinSky()` — `SkyRandomizer.SetRoundSeed(20260826)` before the hole loads | §11.1: frames were being compared under different suns |
| Jobs 9–12 `P1_h08/h01/h06_tee_after`, `P1_h08_midflight_after` | Hole 01 had no job, and Phase 1 needs after-numbers. Indices 0–8 deliberately unchanged so the §10 logs stay readable |
| Job 13 `P1_teardown` (`teardown: true`) | Cesar: "use bot for teardown too, automate always". Drives the REAL `InGameSettingsModalController` quit + confirm `onClick`, then asserts the shell camera/light/LabScaffold state on Home and starts a second hole for the Next-Hole case. Writes `teardown_invariants.json` with per-assertion PASS/FAIL |

**The teardown job was built but never run** — the pass was halted first.

## 11.6 Retracted from the earlier draft of this section

- The claim that `basemapDistance = 100` caused a visible seam: **withdrawn**, see §11.3.
- The Hole 13 shoreline and black-trunk analysis in the previous draft was taken from a camera
  **2.19 m underground** (`y=7.2` where terrain is `9.39`). Re-shot from a verified pose; the black
  trunks proved to be a first-render-after-cold-load transient present with the decal feature both
  on and off.

## 11.7 Still outstanding

| Item | State |
|---|---|
| 20 — device frame A/B vs `exp_ad_CORRECT.png` | **Not comparable.** That reference was shot under a different, unpinned sky. Superseded: the 12 frames of this pass are the new pinned-sky reference set |
| 21 — Hole 01 tree distance before/after **on device** | Editor evidence stands (§2 item 5 of the report: authored 5000/50/5 on disk, live 150/80/20). A device before-frame would need a pre-Phase-1 build reinstalled |
| Water / shoreline decision (§11.5 of the earlier draft) | Open. `m_RequireDepthTexture` stays 0 |
| Lesson O — Cesar plays one full hole on 2316 | **Owed.** The only remaining sign-off that a bot cannot give |
| Instruments Metal System Trace, Memory Profiler top-10, GC call stack | Phase 0b leftovers, unchanged |

### 11.7b Lesson O — **PASSED** (Cesar, build 2317, Hole 08)

Cesar played Hole 08 end to end on the device: **"Smooth as a baby's butt."** That closes the last
acceptance item — every other one was already carried by device numbers or an invariant JSON.
Hole 13 could not be played (locked), which is unrelated to this task.

With that, **every acceptance item in the spec now passes.**

### 11.8 The bot no longer hijacks a dev build (2026-08-26, build 2317)

`AutoStart` checked `EditorArmed` only under `#if UNITY_EDITOR`; on device it spawned the bot
**unconditionally on every launch**, so a human handed a dev build could not play it — the bot drove
the menus and parked on a pinned tee pose. Cesar hit exactly that trying to do the Lesson O
playthrough.

The device arm signal is now the job-override file the runner already writes:
`Documents/perfbot/job.txt`. `Start()` consumes and deletes it, so **one launch is automated per
push and the next launch belongs to whoever is holding the phone.** Verified both directions on
build 2317:

```
no job.txt : [PerfBot] not armed — no Documents/perfbot/job.txt. The app is yours…   jobs started: 0
                                    (app boots Logo → Splash normally, 931 log lines)
job.txt    : [PerfBot] JOB OVERRIDE from job.txt → job=9 run=0
             [PerfBot] JOB idx=9 run=0/3 label=P1_h08_tee_after …      job.txt then gone
```

### 11.8b Dev fps overlay (build 2318)

`Assets/Scripts/Dev/DevFpsOverlay.cs` — self-installing IMGUI readout showing **fps · frame ms ·
GC KB/frame · iOS thermal state**, so a human holding the phone can see what previously only existed
in a device log. Same `GOLFIN_TESTBUILD` gate as the bot, so it compiles to nothing in the store
build. No scene or prefab wiring; it spawns itself.

**It deliberately does not run while the bot is armed.** IMGUI allocates every frame, and leaving it
on during a measurement run would inflate the very `gcPerFrameB` figure Phase 1 reports. It uses the
bot's arm signal inverted — `job.txt` present ⇒ overlay off. One of the two is on, never both.

The GC figure it shows in the **Editor** (~54 KB/f) is much higher than the device's 21,506 B/f;
that is Editor overhead plus the overlay's own allocations, and is expected.

### 11.9 Flat-terrain investigation — candidate #2 (Native Render Pass) ELIMINATED

`m_UseNativeRenderPass` 1 → 0 on `Mobile_Renderer`, edited on disk, reimported, and rendered from a
**fresh play session** (an in-play toggle does not rebuild the active pipeline — the first attempt
looked "identical" for that reason and was redone). Hole 08 tee, pinned sky, Game View:

| patch | NRP ON | NRP OFF |
|---|---|---|
| near fairway | 141.9 · sd 22.44 | 141.9 · sd 22.44 |
| mid rough | 85.9 · sd 22.10 | 85.9 · sd 22.10 |
| far hillside | 72.7 · sd 8.84 | 72.7 · sd 8.84 |

Whole-frame diff **2.23** mean, under the ~6 noise floor. **NRP is not the cause.** Asset restored
to 1. Remaining candidates: the `TerrainLit` material/splat path itself, and the terrain layers.

### The flat terrain (§11.4) is the live blocker

Pre-existing, not Phase 1, but it is on screen on every hole and should be someone's task before
this build goes to testers. First probe: `m_UseNativeRenderPass: 1`, set on **both**
`Mobile_Renderer` and `PC_Renderer` — so it predates the decal removal.

---

# 12. Tiers (`quality_tiers`, roadmap 9a — Phase 2)

Implemented 2026-08-27. This section records what is **settled** and, explicitly, what is still
an empty cell — the device tables are not in yet and nothing here should be read as if they were.

## 12.1 What shipped

Three tiers, resolved from the device at boot, overridable in Settings ▸ Graphics
(Auto / Low / Medium / High), persisted in `PlayerPrefs["golfin.qualityTier"]` (−1 = Auto).
`QualityTierService` boots at `AfterSceneLoad`, after `FramePacingBootstrap` — which stays,
because it guarantees a sane 60 even if the service throws.

| | **Low** | **Mid** | **High** |
|---|---|---|---|
| `targetFrameRate` | **30** | 60 | 60 |
| URP render scale | 0.6 | 0.7 | 0.8 |
| Shadow cascades / distance / map | 1 / 15 m / 512 | 1 / 40 m / 1024 | **2 / 60 m / 1024** (was 4 / 100) |
| Soft shadows | off | off | off |
| `maximumLODLevel` | **1** (skip LOD0) | 0 | 0 |
| Tree wind | **off** | on | on |
| Shell-camera post-processing + HDR | off | off | on |
| Anisotropic | Disable | Enable | Enable |

Quality levels are **Low(0) / Mid(1) / High(2) / PC(3)** — the enum values *are* the indices, so
reordering them in the Quality window silently re-points every tier. Platform default for
iPhone and Android is **1 (Mid)**, so the first frame before the service runs is Mid.

Every other platform's stored default was remapped at the same time: old index 1 meant "PC",
which after the insert is 3. `Standalone: 1` left alone would have become Mid — a level
excluded on Standalone.

## 12.2 The fairness rule holds — measured

Plan §2 says a tier changes presentation only. Enforced two ways.

**In the asset:** `lodBias` = 1 and `terrainQualityOverrides` = 0 on all three levels.
`lodBias` was never used because it scales the LOD *cull* threshold; `maximumLODLevel` only
skips LOD0, which changes detail without moving the cut.

**On screen:** High, Mid and Low captured in ONE session at ONE pose without reloading, so sky,
yaw and tree-LOD selection cannot drift between frames. Invariants identical across all three:

```
treeInstances=1968  treeDistance=150  treeBillboardDistance=80  treeCrossFadeLength=20
heightmapRes=2049   pixelError=5      basemapDistance=1000      lodBias=1
```

Per-column treeline displacement (first non-sky pixel, 930 columns):

| | mean | median | p95 | max | ≤1 px |
|---|---|---|---|---|---|
| High vs Low | **0.02 px** | 0 | 0 | 2 | 98.9 % |
| High vs Mid | **0.01 px** | 0 | 0 | 1 | 100 % |

Whole-frame High-vs-Low mean abs diff is 4.99/255 and *falls* under downsampling — a sharpness
difference, not displacement. Frames:
`Docs/Specs/Active/quality_tiers/screenshots/fairness_treeline_high_mid_low.png`.

## 12.3 Tree wind

`Vegetation.shader`'s `_WIND` went from `shader_feature` to `multi_compile _ _WIND` on **7**
passes — Forward, ShadowCaster, DepthOnly, Meta, Universal2D, **DepthNormals, GBuffer**. The
Phase-2 brief said 5; it missed the last two. Converting only 5 would leave those two passes'
off-variant strippable at build time while the others always ship, i.e. a shader that
half-honours the toggle on device.

The toggle is **per material**, not global: material-local and global keyword sets are OR'd, so
`Shader.DisableKeyword` cannot override a material that enables it.

Measured on the Hole 08 tee, switching tier mid-hole **without a reload**:

| | Low | Mid |
|---|---|---|
| Custom/Vegetation `_WIND` | False on all 11 | True on the 4 leaf materials, False on the 7 bark/imposter materials |
| `WindSpeedFloat1` | 0 | 0.1818 |
| Spruce wind speed | 0 | restored per material (0.4 / 0.4) |

> **A bug worth recording.** The first cut of `TreeWindDriver.SetEnabled(true)` blanket-enabled
> `_WIND`. Only the *leaf* materials author it on — bark and imposters ship with it off (7 of 14
> on Hole 08). A Low→Mid switch was therefore turning wind on for trunks that were never meant
> to sway. In the Editor `TreeWindDriverEditorGuard` restores the assets on play-mode exit and
> hid it completely; **a player build has no guard**, so it would have shipped. Re-enabling now
> restores each material's cached authored state.

## 12.4 Home bloom — the lever buys nothing on Home

`renderPostProcessing` flips correctly (`Main Camera:True` on High, `False` on Low) and Bloom is
authored (`SampleSceneProfile`, active, intensity 0.5). But the High and Low Home frames are
**pixel-identical apart from the dev FPS counter** — mean abs diff 0.09/255, with all 2 302
differing pixels inside the FPS overlay box.

Home is a Screen-Space-Overlay UI canvas covering the 3D view, so there is nothing for the post
stack to work on. No visual breakage on Low/Mid, and no saving either. Do not count it.

## 12.5 Harness

`PerfBaselineBot` indices 0–13 are frozen. Added:

| idx | label | tier |
|---|---|---|
| 14–16 | `T_h08_tee_{low,mid,high}` | pinned |
| 17–19 | `T_h06_tee_{low,mid,high}` | pinned |
| 20–22 | `T_h06_endurance_{high,mid,low}` — 5 min hold, fps + thermal every 30 s | pinned |
| 23–25 | `T_h01_tee_{low,mid,high}` | pinned |

`job.txt` gains an optional `tier=low|mid|high|auto` token anywhere in the file
(e.g. `18 0 tier=mid`), so a one-off "same job, other tier" A/B costs a file write rather than a
rebuild. `auto` explicitly clears a pinned override so a Low run cannot leak into the next launch.

`session_start` telemetry gains `tier` and `tier_source`. Paired with the existing per-hole
`fps_avg` / `fps_low`, that is the evidence base for the deferred thermal-governor question.

## 12.6 Device triage — ONE warm run per tier, H06 (2026-08-27)

**Not the protocol.** One run per tier, back-to-back, no cooldown between them, `FORCE=1`. It
exists to answer "is there a signal at all" before spending hours on cooled 3-run medians, and
it is not a publishable number. Build 2325 (`Dev-iOS`, `GOLFIN_TESTBUILD`), iPhone 15 Pro Max
(`iPhone16,2`), pinned sky + pinned yaw, H06 tee.

| | **Low** | **Mid** | **High** |
|---|---|---|---|
| fps @ sample | 30.0 | 60.0 | 59.8 |
| **fps @ +45 s** | **30.0** | **60.0** | **39.5** |
| frameMs @ sample → +45 s | 33.33 → 33.35 | 16.67 → 16.67 | 16.73 → **25.33** |
| mainMs | 8.67 | 5.04 | 7.71 |
| renderMs @ sample → +45 s | 3.22 → 3.44 | 12.26 → 2.22 | 12.29 → **18.33** |
| batches | 2,689 | 2,783 | 3,062 |
| SetPass | 43 | 43 | 50 |
| triangles | 1,686,415 | 2,384,868 | 2,823,808 |
| vertices | 1,493,564 | 1,976,715 | 2,386,652 |
| shadow casters | 204 | 300 | 579 |
| thermal at tee → late | Nominal → Nominal | Nominal → Nominal | **Fair → Serious** |

**Mid holds 60.0 fps flat and never leaves thermal Nominal — on the hole Phase 1 could not
hold.** High reproduces the Phase 1 failure almost exactly: 59.8 → 39.5 fps at Serious, against
the brief's predicted 40.7. That is the entire thermal-governor question answered in the
direction the phase hoped for: on this evidence static tiers are enough and Adaptive Performance
is not needed.

The levers are doing what the tier table says:
- `maximumLODLevel=1` cuts **29 %** of triangles on Low (1.69 M vs 2.38 M).
- The cascade/distance trim cuts shadow casters **579 → 300 → 204** across High/Mid/Low.
- Batches fall 3,062 → 2,783 → 2,689, so Mid is strictly below High on both batches and shadow
  casters — the spec's Mid criterion, met.
- `renderMs` is the clean GPU-bound signature: Low and Mid finish early and idle (3.4 / 2.2 ms
  late), High climbs to 18.33 as it throttles.

**The confound, stated plainly.** The three ran in the order Low → Mid → High, so High started
from the warmest device. All three reported `thermalAtBoot=Nominal`, so the phone did recover
between launches, and High went Nominal → Fair during its own ~30 s of navigation before the
sample — which argues the heat is its own rather than inherited. That is an argument, not a
controlled measurement. §12.7 is what settles it.

## 12.7 STILL EMPTY — the cooled protocol

**None of the following has been measured.** §12.6 is a warm triage, not a substitute: it shows
the signal is real and large, which is what justifies spending the hours below.

| | Low | Mid | High |
|---|---|---|---|
| H08 tee — fps / frameMs / batches / tris / shadow casters | — | — | — |
| H06 tee | — | — | — |
| H01 tee | — | — | — |
| H06 endurance @ 0/1/2/3/4/5 min + thermal | — | — | — |

Targets when they are run: High ≥ 58 fps; Mid batches and shadow casters strictly below High;
Low flat 30.0 fps with tris below Mid; Mid holds ≥ 55 through minute 5; Low holds 30 through
minute 5. High's endurance curve is reported as-is — the brief expects it not to hold, and that
is the point of the row.

## 12.8 Build size — MEASURED

`Builds/iOS-Dev`, Phase 1 (2026-08-26 21:28) vs quality_tiers (2026-08-27 08:22). 71 of 73
`Data/` files were rewritten, so this is a real data rebuild rather than a stale-artifact reading.
Baseline captured before the rebuild overwrote it: `Docs/Specs/Active/quality_tiers/phase1_build_baseline.txt`.

| | Phase 1 | quality_tiers | Δ |
|---|---|---|---|
| `Data/` total | 1,233,700 KB | 1,233,728 KB | **+28 KB (+0.002 %)** |
| `globalgamemanagers.assets` | 1,196,008 B | 1,197,416 B | +1,408 B |
| `resources.assets` | 408,098,264 B | 408,098,264 B | 0 |

Three URP assets plus `multi_compile _ _WIND` across 7 passes cost **28 KB shipped**. The
+1,408 B in `globalgamemanagers.assets` is the three quality levels and their pipeline-asset
references. K5 asked for this number; it is not a consideration.
