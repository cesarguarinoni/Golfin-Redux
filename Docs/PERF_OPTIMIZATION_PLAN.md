# PERF_OPTIMIZATION_PLAN.md — GOLFIN Redux runtime optimization + quality tiers

> Architect inspection 2026-08-26. Baseline device: **iPhone 15**. Goal: smooth on as many devices as
> possible via (1) fixes that help every device and (2) a Low / Mid / High quality tier system.
> **Fairness rule (Cesar):** every device renders the SAME terrain and the SAME trees. Tiers may change
> how they are drawn (shadows, LOD detail, resolution, wind animation, post-processing) — never WHAT is
> there. Trees may stop animating on Low.
>
> Nothing here is implemented. Decisions of record are in §6 (2026-08-26). Roadmap rows this covers: `8a FPS capture`, `8b Memory profile`,
> `9a Quality settings presets` (Order 900), `9b Texture compression audit`, `9c Tree LOD / culling`,
> `9d Mobile device testing`, and the `940 whole-game perf` watch item.

---

## 0. TL;DR

**Fairness is already structural.** The ball sim is fixed-point (`fp` Q16.16), fixed 240 Hz
(`BallSimulation.Dt = 1/240`, `Assets/Scripts/Physics/Core/BallSimulation.cs:9`), and computes the whole
`Trajectory` up front from the baked `Resources/HoleData/<course>/Hole_NN/heightmap.bytes` +
`tree_obstacles.csv`. Frame rate, resolution, shadows and LOD cannot change a shot's outcome. The only
rule a tier system must obey: **never touch the baked sim inputs or tree placement.**

**The five biggest levers, in order of expected payoff:**

| # | Finding | Evidence | Expected win |
|---|---|---|---|
| 1 | **The hole is very likely rendered twice per frame.** `ShellScene`'s `Main Camera` (Base, depth −1, clear=Skybox, cull=Everything, **post-processing ON** with Bloom+Vignette+Tonemap) is never disabled during a hole; `LabScaffold`'s `Main Camera` (Base, depth 0) renders on top. The shell *light* IS disabled on hole load (`PhysicsLabController.DisableShellDirectionalLight()`), the camera is not. | `Scenes/ShellScene.unity` cam @ line ~18060; `TreeOccludeFadeDriver.cs:157` comment confirms the shell camera stays live | Up to ~2× GPU on every hole frame + a full bloom chain. **Must be confirmed with the Frame Debugger first** (Phase 0). |
| 2 | **Standalone Spruce trees.** Holes 03–18 carry 130–1,958 `Spruce 1/3` trees as *unpacked GameObjects* (12 MeshRenderers each, 4 LODs, LOD0 15–17k tris, no billboard, cull at 1 % screen). Hole 08 = **27,468 GameObjects / 23,538 MeshRenderers / 1,958 LODGroups** in a 68 MB scene; all cast+receive shadows, no static/instanced batching. | `TreePlacer.ForceStandaloneNames`, `Hole_08_Geo.unity` counts | CPU culling + draw submission + shadow passes. The #1 *scaling* problem for Low/Mid. |
| 3 | **Shadow setup is desktop-grade.** Mobile URP asset: main-light shadows **4 cascades**, distance 100 m, 1024 map; every tree renderer casts; terrain casts; no lightmaps baked (light is Mixed but there is no LightingData → effectively realtime). | `Assets/Settings/Mobile_RPAsset.asset` | 4 extra scene passes over 23k renderers. Cascades→1–2 and distance→40–60 is the cheapest big GPU win. |
| 4 | **Terrain renders full 9-layer splat everywhere.** `m_SplatMapDistance: 1000` (basemap never used), `m_DrawInstanced: 0`, pixel error 5 on 2049² heightmaps, URP `TerrainLit` with 9 layers = 3 blend passes. | Terrain block in every `Hole_NN_Geo.unity` | Basemap distance 100–150 m + instanced drawing: big fragment win, zero visual change up close. |
| 5 | **Dead renderer feature + settings that cost for nothing.** `DecalRendererFeature` (DBuffer) active on `Mobile_Renderer` with zero decals in the project (MapView removed its projector in iter-31); HDR on; `m_UseAdaptivePerformance: 1` with no Adaptive Performance package. (~~Render Graph disabled~~ — wrong, see §1.1/§8.) | `Assets/Settings/Mobile_Renderer.asset` | DBuffer forces a DepthNormals prepass on every camera — **confirmed on device, §8**. Free to remove. |

**Also found, not per-frame:** build `Data/` is **1.2 GB** (`resources.assets` 407 MB — `heightmap.bytes` 16.4 MB + `zones.json` 8.1 MB per hole ×18; `TerrainData` ~29.7 MB per hole); all 460 audio clips are DecompressOnLoad (Main Theme ≈ 31 MB PCM resident); no per-platform texture overrides on 3,234 textures; MapView does two GPU `ReadPixels` on every open in player builds.

**Decisions of record:** §6 (Low at 30 fps; pixel error identical on all tiers; Spruce = measure E1 first; `Vegetation.shader` `_WIND` multi_compile approved; bloom High only; build-size track in scope).

---

## 1. What was inspected (facts)

### 1.1 Pipeline & settings
- Unity `6000.3.9f1`, URP `17.3.0`. **Render Graph is ON** (`m_EnableRenderCompatibilityMode: 0`, `UniversalRenderPipelineGlobalSettings.asset:199`; the `m_EnableRenderGraph: 0` at line 32 is an obsolete field — Architect misread it on 2026-08-26, corrected by Phase 0 §9.3). Option I is moot.
- Quality levels: only **`Mobile`** (index 0, iOS+Android default) and `PC`. No runtime quality/tier logic exists anywhere in `Assets/Scripts` (grep: `QualitySettings.`, `SystemInfo.` only in telemetry, `renderScale`, `AdaptivePerformance` — nothing).
- `Mobile_RPAsset.asset`: HDR **on**, MSAA off, **renderScale 0.8**, main light per-pixel + shadows 1024 / **4 cascades** / 100 m, soft shadows off, additional lights **per-pixel** (4/object), light cookies + light layers on, reflection probe blending + box projection on, SRP Batcher on, dynamic batching off, GPU Resident Drawer **off**, `m_UseAdaptivePerformance: 1` (package NOT installed), volume profile = `SampleSceneProfile` (Bloom intensity 1 / threshold 0, Vignette 0.2, Tonemapping Neutral).
- `Mobile_Renderer.asset`: Forward, Native Render Pass on, **DecalRendererFeature active (DBuffer)** — unused.
- Frame rate pinned to 60 by `Assets/Scripts/Core/FramePacingBootstrap.cs`. `androidUseSwappy: 0`, `AndroidEnableSustainedPerformanceMode: 0`.
- Player: iOS min 15.0, Metal; Android min SDK 25, ARM64, **Vulkan first then GLES3**; IL2CPP; incremental GC on; `StripUnusedMeshComponents: 0` (Optimize Mesh Data off).

### 1.2 Cameras / lighting during a hole
- `GameplaySceneLoader` additively loads `LabScaffold.unity` (host, its own `Main Camera`, post OFF, far 3000, occlusion culling flag on but **no occlusion data baked** anywhere) + `Hole_NN_Geo.unity`. `ShellScene` stays loaded.
- `ShellScene` `Main Camera` remains enabled (see TL;DR #1). Global Volume in ShellScene → bloom on every Home-screen frame too.
- Each hole has a directional light (Mixed, soft shadows, strength 0.7) but **no `LightingDataAsset`** → no baked lightmaps/shadowmask; shadows are fully realtime. Ambient = Skybox; `DynamicGI.UpdateEnvironment()` on sky apply is behind the loading screen (fine).
- Skies: 9 HDR cubemaps already downsampled to 1024 (`bea290de8`).

### 1.3 Hole content
- Every hole = one Unity `Terrain` (2049² heightmap, 1024² alphamaps, **9 TerrainLayers**, default URP `TerrainLit`, pixel error 5, basemap distance 1000, instancing off, casts shadows) + overlay surface meshes (`Fairway_n`, `Green_1`, `Tee_n`, cart paths — URP `Lit`) drawn on top of it (overdraw) + `MountainBackdrop` + water (`URPWater_Standard`, `_EDGEFADE_ON` needs a depth texture the Mobile asset does not provide).
- Trees, two systems (`TreePlacer.cs`):
  - **Terrain-tree system**: BSP pack (`Custom/Vegetation`, Amplify, `Cull Off`, alpha test, wind via `shader_feature _WIND`, driven by `TreeWindDriver` → `WindSpeedFloat1`; 0 mph = static). 434–~2,000 per hole (obstacle CSV minus standalone count). `treeDistance 150`, billboard 80, `treeMaximumFullLODCount 50`.
  - **Standalone Spruce** (Realistic Tree pack, Shader Graph `Leaves_URP`: alpha clip + two-sided + Simple-Noise wind; `Bark_URP`): forced standalone by `ForceStandaloneNames`. Per-hole standalone counts from scene LODGroups: 03→766, 04→132, 05→1671, 07→677, 08→1958, 09→371, 10→750, 11→489, 12→1521, 13→1681, 14→1415, 15→292, 16→429, 17→829, 18→721. Holes 01/02/06 → 0.
  - Tree obstacles for the sim: `tree_obstacles.csv` per hole (bake_hash), positions only — rendering never feeds the sim.

### 1.4 Assets / memory / load
- Build (`Builds/iOS-Dev/Data`) 1.2 GB. `resources.assets` 407 MB — `Resources/HoleData` alone is 388 MB (heightmap.bytes 16.4 MB + zones.json 8.1 MB per hole). Each hole's `sharedassetsN` ≈ 29.7 MB (TerrainData).
- Hole load: `Resources.Load<TextAsset>(heightmap)` (16.4 MB) → `HeightmapLoader.LoadFromBytes` → `int[2049²]` (16.8 MB managed, LOH) — ~33 MB transient per hole load; `zones.json` parsed on MapView open (cached per hole).
- Textures: 3,234, none with iOS/Android overrides (9 Android overrides on skies only); 981 without mips (mostly UI — fine).
- Audio: 460 clips, all `loadType 0` (DecompressOnLoad), Vorbis quality 100 %. `Main Theme.mp3` (the clip `ScreenManager` references; 3.5 MB packed) is decoded to PCM (~30 MB, the size of its `.wav` source) and kept resident.
- Fonts: `NotoSansJP` TTF 8.9 MB shipped as source → dynamic SDF atlas on device.
- `MapViewController.DoFrameReadbackAndDump` runs in player builds on every map open (2× `Texture2D.ReadPixels` = GPU sync stalls) — diagnostics that should be editor-only.
- Telemetry already ships `fps_avg` / `fps_low` per hole (`TelemetryHooks.cs:269`, `TelemetryBehaviour.cs:93`) → the beta gives real device numbers for free once `beta_telemetry` is live.

---

## 2. Fairness rules for the tier system

Allowed per tier (presentation only): render scale, target frame rate, shadow cascades/distance/resolution/on-off, `maximumLODLevel` (skip LOD0 on Low — same trees, coarser mesh), tree wind on/off, post-processing (bloom/vignette) on/off, water reflection mode, terrain basemap distance, anisotropic filtering, MSAA.

NOT allowed (identical on every device): tree presence and placement, tree draw/cull distance (a tree that vanishes at 100 m on one phone and 150 m on another = "different trees"), terrain heightmap and pixel error (the terrain *mesh*), overlay surface meshes, sky rotation logic, anything read by `BallSimulation` (heightmap.bytes, tree_obstacles.csv, zones, green.json).

Note on `lodBias`: QualitySettings.lodBias scales the **cull** threshold too, so a lower bias would also remove distant trees. Use `maximumLODLevel` (skips LOD0 only, cull unchanged) or per-tier LODGroup threshold arrays that keep the last (cull) threshold fixed. Do not use lodBias.

---

## 3. Proposed tiers

Detection: iOS by `SystemInfo.deviceModel` → chip generation (table-driven, unknown → Mid); Android by `SystemInfo.graphicsDeviceName` + `systemMemorySize` + `processorCount` heuristics, unknown → Mid; **override in Settings** (Auto / Low / Mid / High) persisted in SaveData; optional runtime demotion if `fps_low` telemetry stays under target for N holes (Phase 3, only if measurement shows it is needed).

| Setting | **Low** (A11–A12: iPhone 8/X/XR; Android SD6xx / Helio G / 3–4 GB) | **Mid** (A13–A14: iPhone 11/12/SE2/SE3; SD7xx / Dimensity 8xx / Exynos 1x80) | **High** (A15+: iPhone 13/14/**15**/16; SD8 Gen1+ / Dimensity 9000+) |
|---|---|---|---|
| Target fps | 30 | 60 | 60 |
| Render scale | 0.6 | 0.7 | 0.8 (current) |
| Main-light shadows | ball/near only: 1 cascade, 512, 15 m (or off) | 1 cascade, 1024, 40 m | 2 cascades, 1024–2048, 60 m |
| Tree shadow casting | off (terrain + trees) | on | on |
| `maximumLODLevel` | 1 (no LOD0) | 0 | 0 |
| Tree wind | **off** (`WindSpeedFloat1 = 0` + Spruce `Wind Speed` 0; ideally the `_WIND`-off variant, see §4 Option F) | on | on |
| Terrain basemap distance | 60 m | 100 m | 150 m |
| Post-processing (Shell) | off | off | on |
| Water | no refl., no edge fade | cubemap refl. | as now |
| Aniso | off | per-texture | per-texture |
| MSAA | off | off | off (2× optional later) |

Everything not listed is identical across tiers. Terrain `DrawInstanced` on for all tiers.

Implementation shape (Order 900 — `9a`): three URP assets (`Mobile_Low/Mid/High_RPAsset`) + three Quality levels replacing the single `Mobile` level; a small `QualityTierService` (Assembly-CSharp, `Golfin.Core`) that resolves the tier at boot (after `FramePacingBootstrap`), applies `QualitySettings.SetQualityLevel`, `Application.targetFrameRate`, `Shader.DisableKeyword("_WIND")`, terrain `basemapDistance`/`drawInstanced`/`treeShadows` on hole load (hook: `PhysicsLabController.OnHoleLoaded`, next to `DisableShellDirectionalLight()`), and exposes `Tier` + `OnTierChanged` for the Settings screen. Tier is logged into the existing `session_start` telemetry event.

---

## 4. Options (pick per line; §5 sequences them)

**A. Kill the double render (all tiers).** Disable `ShellScene` `Main Camera` in `OnHoleLoaded` and re-enable in `OnHoleUnloaded` (same pattern as the shell light), or convert the shell camera to a UI-only camera (cull mask = UI, clear = solid) — UI canvases are Screen-Space Overlay so it renders nothing during a hole anyway. Cost: hours. Risk: `Camera.main` consumers — `TreeOccludeFadeDriver` already resolves by `ChaseCamera`; grep the rest. **Do first, after Phase 0 confirms it.**

**B. Shadow diet (per tier).** Cascades 4→1/2, distance 100→15/40/60, tree shadow casting off on Low. Cheapest large GPU win; purely presentational.

**C. Terrain: basemap distance + instancing (all tiers).** `m_SplatMapDistance` 1000→60/100/150 and `m_DrawInstanced` 1 via `QualityTierService` on hole load (runtime `Terrain.basemapDistance` / `drawInstanced`) — no scene edits, no importer change. Optional later: bake 9 layers down (fairway/rough/semirough share tiling → could be 5–6 layers = 2 passes) — content change, out of this pass.

**D. Remove dead cost (all tiers).** Delete `DecalRendererFeature` from `Mobile_Renderer`; keep HDR only if the Home bloom needs it (High); clear `m_UseAdaptivePerformance` unless Option H is taken; MapView `DoFrameReadbackAndDump` behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.

**E. Spruce rendering — three ways to make 2,000 GameObject trees cheap, same placement:**
- **E1 — GPU Resident Drawer** (Unity 6 BatchRendererGroup, `m_GPUResidentDrawerMode: 1` "Instanced Drawing"). No content change: the 23k renderers become instanced batches with GPU culling. Supported on iOS/Metal and Android/Vulkan; **not GLES3** (falls back to today's path on those devices). Needs Shader Graph materials to be DOTS-instancing compatible (URP 17 Shader Graph is) and `BatchRendererGroup Variants = Keep All` in Player settings. Cheapest to try; measure on device — GRD can *raise* CPU on very low-end Android.
- **E2 — Convert standalone Spruce into terrain trees at identical transforms.** Editor tool: read each `Spruce_n` instance's position/rotation/scale from the scene → `TreeInstance` with a `Spruce 1/3` prototype → delete the GameObjects. Placement bit-identical → `tree_obstacles.csv` bake_hash unaffected. Terrain tree rendering batches, culls in native code, one shadow toggle, and Hole 08 drops from 27k to ~60 GameObjects (load time, memory). Must first verify why `ForceStandaloneNames` exists ("particle systems / complex hierarchies") — the Spruce prefabs are variants of `Realistic Tree/Source/Models/Spruce/Spruce_n.fbx` with a root LODGroup and no particles, so the reason may be historical. Risk: terrain trees ignore per-tree LOD crossfade nuances; the pack's wind Shader Graph needs to survive as a terrain-tree material.
- **E3 — Impostor LOD.** Add a billboard/octahedral impostor as LOD3 for Spruce (the BSP pack already has `Textures_Imposters`). Pairs with E1/E2; content work.

**F. Tree wind off on Low, properly.** `WindSpeedFloat1 = 0` freezes BSP trees visually but the vertex math still runs; the real saving is the `_WIND`-off variant. K5's option (c) — `shader_feature _WIND` → `multi_compile _ _WIND` in `Vegetation.shader` (third-party pack, force-added) — ships both variants so `Shader.DisableKeyword("_WIND")` kills the cost game-wide. Spruce `Leaves_URP.shadergraph` has no keyword; set `Wind Speed`/`Bend Strength` to 0 via global override or add a keyword node. Measure the build-size delta (K5 asked for the number). Also finally closes smoke #6 (`tree_wind_device`) if H1 there was right.

**G. Frame pacing.** Low tier at 30 fps (`Application.targetFrameRate = 30`; the sim is not frame-bound — NOTE: `arrow_speed_retune` F13 was tuned at 30 fps and `ui_frame_pacing` moved the game to 60, so aim-arrow feel must be re-checked at both rates before Low ships at 30); enable `androidUseSwappy` for Android pacing; consider `OnDemandRendering` at 30 fps on static menu screens for battery/thermal on all tiers.

**H. Adaptive Performance package (optional, later).** `com.unity.adaptiveperformance` with the Android (Samsung) and iOS providers: thermal + bottleneck signals → automatic render-scale / LOD scaler. Would make the "Auto" tier self-correcting on thermal throttling — the failure mode a static tier table cannot see. Only worth it after Phase 0 shows throttling on real hardware.

**I. Render Graph on.** ~~Flip `m_EnableRenderGraph`~~ — **MOOT.** Phase 0 §9.3: every Frame Debugger event runs under `ExecuteRenderGraph`; compatibility mode is already off. Dropped.

**J. Memory / load / size (not fps, but "runs on more devices" = 3–4 GB Android phones):**
- Audio: music → `Streaming`, SFX → `CompressedInMemory`, quality 100→~70. (`Main Theme.wav`, 31 MB, is unreferenced — repo hygiene only, it is not in the build.)
- `Resources/HoleData` 388 MB: heightmap.bytes and zones.json are only needed for the current hole — keep in Resources but **compress** (heightmap int32→int16 delta or LZ4; zones.json → binary or gzip) or move to Addressables/StreamingAssets. Also cuts the ~33 MB managed spike per hole load.
- Per-platform texture overrides: max 1024 for UI/character art that is 2048 today (Home characters 904 KB each ×11 in Resources), ASTC 6×6 default is fine.
- `StripUnusedMeshComponents` on; TerrainData holes texture compression on.
- App Store: 1.46 GB iOS demo build is above the 200 MB cellular prompt; on-demand hole delivery is a separate decision.

---

## 5. Phased plan

**Phase 0 — Measure (1 day, no code).** Dev build with Autoconnect Profiler + Frame Debugger over USB on the iPhone (baseline) and, if available, one older iPhone. Capture Holes 06 (no standalone Spruce), 01 (terrain trees only), 08 (worst case): CPU main thread ms, GPU ms, draw calls, shadow-pass count, batches, Frame Debugger event list (count cameras, count DepthNormals/DBuffer passes), memory (Texture/Mesh/Audio/Managed) with the Memory Profiler after 3 holes. Output: `Docs/Reports/perf_baseline_<date>.md`. **Confirms or kills TL;DR #1 and #5 before anything is built.** Kickoff in `Docs/TellCode.md`.

**Phase 1 — Free wins, all tiers (1–2 days).** Options A, C, D, G-swappy, MapView readback guard, audio load types. Each in its own commit; re-capture Hole 08 after each. Expected: the largest single frame-time drop of the whole plan.

**Phase 2 — Tier system (`9a`, Order 900) (2–3 days).** Three URP assets + Quality levels, `QualityTierService`, device table, Settings override, telemetry field, hole-load hooks for terrain/wind/shadows (Options B, F). Ship with the beta so `fps_avg/fps_low` come back per tier.

**Phase 3 — Spruce rendering (`9c`) (3–5 days).** E1 first (a flag flip + shader check, measured on device); if GLES3 Androids matter or E1 disappoints, E2 (convert to terrain trees at identical transforms, with a bake_hash equality check as the acceptance test). E3 if far-tree fill rate still dominates.

**Phase 4 — Memory, load, size (`8b`, `9b`) (2–3 days).** Option J. Acceptance: hole load managed spike < 10 MB, resident audio < 10 MB, `Data/` size reported before/after.

**Phase 5 — Verify (`9d`).** Same three holes on Low/Mid/High devices; compare per-tier `fps_low` from beta telemetry; screenshot A/B of Hole 08 tee on each tier to confirm identical tree placement (diff the tree silhouettes, not the shading).

---

## 6. Decisions (Cesar, 2026-08-26)

1. **Tier table (§3):** cut lines as proposed, provisional until Phase 0 numbers; rationale in §6.1 below. **Low at 30 fps: APPROVED** (Cesar, 2026-08-26) — the aim-arrow feel check at 30 fps in §6.1 is an acceptance test for `9a`, not a reopened decision.
2. **Terrain pixel error: IDENTICAL on all tiers.** The terrain mesh is part of "same terrain". Tiers never touch `heightmapPixelError`, `heightmapMaximumLOD`, or the overlay meshes.
3. **Spruce path: MEASURE.** E1 (GPU Resident Drawer) is tried first and measured on device; E2 only if E1 disappoints or GLES3 Androids matter.
4. **`Vegetation.shader` `_WIND` multi_compile: APPROVED.** Edit the pack file in place (`Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader`, 5× `#pragma shader_feature _WIND` → `#pragma multi_compile _ _WIND`; it is force-added, GUID `e80a1e91…`, so the edit is tracked). Both variants ship; `Shader.DisableKeyword("_WIND")` is the Low-tier hook. Build-size delta is measured and reported as a number (K5 asked for it). Lifts the K5 "do not modify Packs/" constraint for this one file.
5. **Home-screen bloom: HIGH only.** Mid/Low run the shell camera with post-processing off. HDR stays on the High asset only; Mid/Low assets ship HDR off.
6. **Build-size track: IN SCOPE NOW** (Phase 4, Option J).

### 6.1 Why those cut lines and why Low runs at 30 fps

The tiers are drawn by GPU generation, not by phone age, because this game's frame cost is almost entirely GPU (shadow passes over foliage, 9-layer terrain, alpha-tested leaves). Rough single-threaded GPU throughput relative to the iPhone 15 (A16): A15 ≈ 0.85×, A14 ≈ 0.7×, A13 ≈ 0.55×, A12 ≈ 0.4×, A11 ≈ 0.3×, A10 ≈ 0.2×. Memory follows the same line (A11 phones have 2–3 GB, A13+ 4 GB, A15+ 6 GB), and Hole 08 is a 27k-object scene with ~30 MB TerrainData plus a 33 MB load spike.

- **High = A15+.** Within ~15 % of the baseline; the current settings (0.8 scale, 60 fps) are what Cesar already plays on. Nothing to give up.
- **Mid = A13–A14.** Half the GPU budget of the baseline. 60 fps is still reachable if the frame is ~half the cost: one shadow cascade at 40 m, 0.7 scale, no bloom. That is exactly what the Mid column removes and nothing else.
- **Low = A11–A12 (and anything older that iOS 15 still admits — iPhone 6s/7 on A9/A10 are technically installable).** At 0.3–0.4× the GPU there is no honest 60 fps with identical terrain and identical trees; the choice is a locked 30 or a 60 that dips to 35–50 and stutters. A locked 30 with frame pacing is visibly smoother than an unstable 45, and it halves thermal load, which is what makes an old phone *stay* at its frame rate past minute five. Shadows go to a near-only cascade and trees stop animating, both purely presentational.
- **Android** maps the same way: Vulkan-capable SD 8 Gen 1+ / Dimensity 9000+ = High; SD 7xx / Dimensity 8xx / Exynos 1x80 = Mid; SD 6xx / Helio G / any GLES3-only or ≤ 4 GB device = Low. Unknown hardware defaults to Mid, and the Settings override lets a player move either way.

Two things the numbers will settle: whether A12 (iPhone XR/XS, 4 GB) belongs in Mid rather than Low, and whether the 30 fps Low target needs the aim-arrow feel re-tuned (F13 was tuned at 30 fps, `ui_frame_pacing` moved the game to 60 — both rates must be checked before Low ships).

---

## 7. Side findings (not perf — file separately)

- **Hole 02 obstacle/scene mismatch.** `Resources/HoleData/lomond-country-club/Hole_02/tree_obstacles.csv` lists **1,495 Spruce** (892 `Spruce_1` + 603 `Spruce_3`) but `Hole_02_Geo.unity` contains **zero** Spruce objects (0 LODGroups; last scene commit `4b0054069`). Either the scene lost its standalone trees or the bake is stale → the ball would collide with invisible trees on Hole 02. Verify on the tee before the beta.
- Hole 06 uses a different tree set (`Fir_01…06`, Mobile_Tree_Bundle) from every other hole.
- `URPWater` `_EDGEFADE_ON` samples a depth texture the Mobile asset never produces.
- `Assets/Settings/DefaultVolumeProfile.asset` (the URP-generated global default, assigned in `UniversalRenderPipelineGlobalSettings`) carries MotionBlur/FilmGrain entries at intensity 0 plus URP test components — inert, but a cleaner regenerated profile would remove doubt.
- Occlusion culling is ticked on every camera but no occlusion data exists — harmless, just misleading.

---

## 8. Phase 0 results — what changed (2026-08-26, `Docs/Reports/perf_baseline_2026-08-26.md`)

Device: iPhone 15 Pro Max (A17 Pro — *faster* than the iPhone 15 baseline), Dev build 2311.

| Fact | Consequence for this plan |
|---|---|
| **H01 tee cold: 48.8 fps / H08: 31.2 / H06: 20.0** (H08, H06 taken throttled). Main thread 3.8–6.4 ms; render thread 19–38 ms. | The game is **GPU-bound**, and the "High tier holds 60 at current settings" assumption in §3/§6.1 is **false** even above the baseline. Phase 1 is not a low-end courtesy — it is required for every tier. |
| #1 CONFIRMED on device: 622 of 2,011 render events (31 %) belong to the ShellScene camera — 232 shadow + 255 opaque draws + full Bloom chain, never reaches the backbuffer. | Option A stays first. |
| #5 CONFIRMED: `DrawDepthNormalPrepass` on **both** cameras (85 + 237 draws) + 2 CopyDepth; zero DBuffer draws. | Option D moves up next to A. |
| Render Graph already on (§1.1 corrected). | Option I dropped. |
| Unity cannot report GPU ms on Metal (`NotSupportedWithMetal`); the "GPU Frame Time" counter is a CPU stand-in. | Every "GPU ms" line in §5 becomes **Xcode Instruments (Metal System Trace)**; render-thread ms is the in-Unity proxy. |
| Hot vs cold H01: −30 % geometry, +56 % frame time. Thermal throttling is the leading explanation but **unproven** — the pose differed and `ProcessInfo.thermalState` was not captured. | Phase 0b logs thermal state; Option H (Adaptive Performance) promoted to "evaluate after Phase 1". |
| Poses are not reproducible run-to-run (5,483 vs 4,043 batches, same hole, same bot). | A/B protocol for all phases: cooled device, pinned camera yaw, ≥3 runs, median. Single captures are not evidence. |
| **System memory: Home 778 MB → H08 1,370 MB** (+590 MB for one hole). | A 3 GB device will be jetsam-killed on H08. Phase 4 (memory) is no longer "size hygiene" — it is a device-support blocker. Memory Profiler top-10 still missing. |
| **~29 KB GC alloc per frame** in gameplay (≈1.7 MB/s). | New Phase 1 line: find the per-frame allocator(s). |
| H06 draws **6.3 M tris** with the least content; hypothesis = 2049² heightmap on a 229 × 101 m terrain → ~0.11 m samples, `pixelError 5` keeps them all. | If confirmed, fix at **import** (heightmap res scaled to terrain size) — a content change applied to every device, so it stays inside the "identical terrain" rule. Phase 1 candidate. |
| Terrain tree distance is inconsistent: holes 01/02/06 = 5000/50, the other 15 = 150/80. | Normalise to 150/80 in Phase 1 — a fairness-rule item (§2), not just perf. |
| Hole 02: 1,495 Spruce in the collision bake, zero in the scene — structural (`ForceStandaloneNames`). | Own task before the beta; acceptance = `bake_hash` unchanged. |
| `PerfBaselineBot` (`Assets/Scripts/Dev/PerfBaselineBot.cs`, gated `GOLFIN_TESTBUILD`, absent from the iOS-Full IL2CPP output) drives the tee pose hands-off. | Keep; it is the Phase 1/5 A/B harness. Needs a yaw pin + thermal-state log (Phase 0b). |

### 8.1 Phase 0b results (same day, report §10) — the experiments, cooled / pinned yaw / 3 runs / frame-verified

Hole 08 tee, iPhone 15 Pro Max, render-thread ms is the GPU proxy (Unity cannot time the GPU on Metal):

| | baseline | **(a) shell cam off** | **(d) decal feature off (asset)** | **(a+d)** | (c) basemap 100 + instanced | (b) cascades 1 / 40 m |
|---|---|---|---|---|---|---|
| fps | 30.1 | **59.8** | 58.7 | **59.8** | 45.2 | 39.8 |
| render ms | 26.11 | **14.48** | 15.05 | **14.09** | 19.80 | 22.42 |
| batches / tris | 7,375 / 5.04 M | 3,917 / 2.40 M | 4,712 / 3.38 M | **2,430 / 1.41 M** | 4,610 / 3.13 M | 5,358 / 3.11 M |
| thermal held? | — | **60 fps at Serious** | fell to 34 at Serious | held | — | — |

- Cooled H06 = 35.2 fps (§9's 20.0 was throttling). H06 still draws 6.86 M tris: **heightmap density confirmed** — every hole ships 2049² regardless of size, H06 is 182 samples/m² vs H08's 26. Fix at import, all devices, inside the identical-terrain rule. Own task.
- **Trap:** disabling a renderer feature at runtime renders the terrain black and reads as a 2× win (caught by looking at the phone). Asset edit + rebuild only. Removing the feature also churns `Mobile_RPAsset.m_PrefilterDBufferMRT3` — diff all of `Assets/Settings/`.
- **Trap:** `LabHoleBinder` is editor-only → `OnHoleUnloaded()` never fires in a player build; anything disabled at hole load must be restored from `PhysicsLabController.OnDestroy()`. (The shell light has been silently never-restored on device.)
- Consequence for §3: with (a+d+c) the A17 Pro sits at the 60 cap with thermal headroom; the iPhone 15 (A16) is expected to land ~17 ms → High is plausible again, Mid/Low still need (b) + the tier levers. Re-baseline on the A16 after Phase 1.
- **Phase 1 is specced: `Docs/Specs/Active/perf_phase1_free_wins/`** = a + d + c + tree-distance normalisation (01/02/06 → 150/80, runtime) + MapView readback guard + console-spam/GC check. (b) stays a tier lever.
- Still open from Phase 0b: (e) mid-flight, Instruments trace, Memory Profiler top-10, GC call stack (folded into Phase 1 §5 and Phase 4).
