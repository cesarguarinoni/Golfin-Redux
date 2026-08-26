# SPEC — `perf_phase1_free_wins`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
> Parent plan: `Docs/PERF_OPTIMIZATION_PLAN.md` (Phase 1). Evidence: `Docs/Reports/perf_baseline_2026-08-26.md` §9–§10.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

Ship the four measured, frame-verified, device-independent wins from Phase 0b so every tier starts from a sane frame: Hole 08 tee on the iPhone 15 Pro Max goes from **30.1 fps / 26.11 ms render thread** to **~60 fps / ≤ 14 ms** (Phase 0b §10.3: (a) shell camera off −11.63 ms, (d) decal feature off −11.06 ms, (a+d) −12.02 ms, (c) terrain basemap+instancing −6.31 ms). None of these changes what is on screen: same terrain, same trees, same placement. Also normalises the terrain-tree draw distance on holes 01/02/06 (fairness rule, plan §2) and removes two diagnostics that run on retail devices.

Not a tier system (that is `9a`, next spec). Not the shadow diet (option (b) — it is a per-tier lever, plan §3).

## Reference

- Numbers + frames: `Docs/Reports/perf_baseline_2026-08-26.md` §10.3, `Docs/Reports/perf_baseline_2026-08-26_frames/exp_ad_CORRECT.png` (the target look — it is indistinguishable from baseline).
- Figma: N/A (no UI change).

## Figma Fidelity

N/A — the acceptance criterion is *no visible change* except the tree draw distance on holes 01/02/06 (§4 below).

## Architecture context

- **Asmdef boundaries:** `Golfin.Physics.Viewer` (`PhysicsLabController`), `Golfin.Gameplay.UI` (`MapViewController`), assets under `Assets/Settings/`. No new assemblies.
- **Existing code referenced:**
  - `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `OnHoleLoaded(string)` (`:2032`), `DisableShellDirectionalLight()` (`:2475`, called at `:2196`), `_shellDirLightDisabled` (`:2465`), `OnHoleUnloaded()` (`:2492`), `OnDestroy()` (`:343`).
  - `Assets/Scripts/Physics/Viewer/LabHoleBinder.cs` — **editor-only** bridge; in a player build `OnHoleUnloaded()` is never called (see §1 NOTE).
  - `Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs` — `UnloadGameplayScenes()` (`:194`) unloads Hole + LabScaffold on every exit path; `LoadCoroutine` step 2b does the same before Next Hole.
  - `Assets/Scripts/Gameplay/UI/ShotUI/HUD/TreeWindDriver.cs` — precedent for touching `Terrain.activeTerrain` at hole load.
  - `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` — `DoFrameReadbackAndDump` calls at `:525` and `:2318`.
  - `Assets/Scripts/Dev/PerfBaselineBot.cs` — `ExpShellCameraOff()` (`:491`) is the proven runtime shape for §1.
- **Existing assets:** `Assets/Settings/Mobile_Renderer.asset` (DecalRendererFeature sub-asset `&-7092247394123479118`), `Assets/Settings/Mobile_RPAsset.asset` (`m_PrefilterDBufferMRT3` will churn — expected, §2).
- **Scenes:** NO scene edits. Everything terrain-related is set at runtime (no merge driver on `.unity`; the three old-batch holes are fixed without touching them).

## Implementation

### 1. Shell camera off during a hole (Option A)

In `PhysicsLabController`, mirror the light pattern exactly:

```csharp
Camera _shellCameraDisabled;   // next to _shellDirLightDisabled (:2465)

void DisableShellCamera()
{
    if (_shellCameraDisabled != null) return;
    foreach (var cam in Camera.allCameras)            // enabled cameras only — that is what we want
    {
        if (cam == null || cam.gameObject.scene.name != "ShellScene") continue;
        cam.enabled = false;                          // Camera component ONLY. GameObject stays active:
        _shellCameraDisabled = cam;                   // the AudioListener on it must keep running.
        Debug.Log($"[PhysicsLab] Disabled ShellScene camera '{cam.gameObject.name}' while hole is loaded (perf_phase1_free_wins §1).");
        return;
    }
}

void RestoreShellCamera()
{
    if (_shellCameraDisabled == null) return;
    _shellCameraDisabled.enabled = true;
    _shellCameraDisabled = null;
}
```

- Call `DisableShellCamera()` at `:2196` immediately after `DisableShellDirectionalLight()`.
- Call `RestoreShellCamera()` in `OnHoleUnloaded()` next to the light restore (`:2498`) **and in `OnDestroy()` (`:343`)**.
- **NOTE (found while speccing — fix it for the light too):** `LabHoleBinder` only fires `OnHoleUnloaded()` under `#if UNITY_EDITOR`. In a player build nothing calls it, so today the shell light is never re-enabled after a hole. Nobody noticed because Home is a full-screen overlay canvas. A camera left disabled would be noticed: **restore both the camera and the light from `OnDestroy()`** — `PhysicsLabController` lives in `LabScaffold`, which `UnloadGameplayScenes()` unloads on every exit path (menu quit, curtain, Next Hole step 2b), so `OnDestroy` is the reliable hook. Keep the `OnHoleUnloaded()` restore for the editor picker path. Null-check: on domain teardown the camera may already be destroyed.
- `Camera.main`: both cameras are tagged `MainCamera` (`ShellScene.unity:18037`, `LabScaffold.unity:10007`); with the shell one disabled, `Camera.main` resolves to the LabScaffold camera. The three runtime consumers (`PhysicsLabController.cs:1717`, `WaterSplashCaptureRig.cs:110`, `BotDriver.cs:991`) all prefer `ChaseCamera` first — no change needed. `TreeOccludeFadeDriver` already resolves by `ChaseCamera` component.
- Do NOT touch `SkyRandomizer` — it sets `RenderSettings.skybox`/`sun`, not cameras.

### 2. Remove `DecalRendererFeature` (Option D)

- `Assets/Settings/Mobile_Renderer.asset`: remove the entry from `m_RendererFeatures` (and `m_RendererFeatureMap`) and delete the sub-asset `&-7092247394123479118` — remove it, do not just set `m_Active: 0`. Do it in the Inspector (renderer data → feature → ⋮ → Remove) so Unity rewrites both lists.
- **Expected churn:** Unity rewrites `Mobile_RPAsset.asset` `m_PrefilterDBufferMRT3: 0 → 1` (Phase 0b §10.4b). Commit both files. Diff the whole `Assets/Settings/` folder before committing; nothing else may change.
- `PC_Renderer.asset` is out of scope (its feature is SSAO, not decal).
- ⚠️ **Do NOT disable the feature at runtime for testing** — Phase 0b §10.3 proved `SetActive(false)` on a built renderer renders the terrain black. Asset edit + rebuild only.
- **NOTE — water edge fade.** `URPWater_Standard` (`_EDGEFADE_ON`) samples `_CameraDepthTexture`. The decal feature's CopyDepth was the only thing producing it (`Mobile_RPAsset.m_RequireDepthTexture: 0`). After removal, water shorelines on holes with water (05–09, 12–14, 16–18; Hole 13 has 9 water objects) may lose their soft edge. Check Hole 08 and Hole 13 water on device. If the edge is visibly hard: set `m_RequireDepthTexture: 1` on `Mobile_RPAsset` (a depth copy only — far cheaper than the DepthNormals prepass), re-measure, and report the delta. If it still looks fine, leave depth off. Report which.

### 3. Terrain render defaults at hole load (Option C + tree-distance normalisation)

In `PhysicsLabController.OnHoleLoaded`, after the scene is bound (same place as §1), apply to the hole's terrain:

```csharp
static void ApplyTerrainRenderDefaults()
{
    var t = Terrain.activeTerrain;
    if (t == null) return;
    t.basemapDistance        = 100f;   // was 1000 (m_SplatMapDistance) on all 18 holes — 9-layer splat everywhere
    t.drawInstanced          = true;   // was off on all 18 holes
    t.treeDistance           = 150f;   // holes 01/02/06 shipped 5000 — normalise to the other 15 (fairness, plan §2)
    t.treeBillboardDistance  = 80f;    // holes 01/02/06 shipped 50
    t.treeCrossFadeLength    = 20f;    // holes 01/02/06 shipped 5
    // treeMaximumFullLODCount stays 50 (identical on all holes already).
}
```

- Runtime only. **No `.unity` or TerrainData edit** — the scene values stay as authored; this is the same "no scene edits" constraint K5 carried, and it fixes 01/02/06 without a merge.
- These are the values Phase 0b experiment (c) measured (−6.31 ms). `basemapDistance` becomes the per-tier hook in `9a`; put the number in one `const` so `9a` can move it.
- Add the `[PhysicsLab]` log line stating the values applied so the device log shows it.

### 4. MapView readback guard

`MapViewController.cs:525` and `:2318`: wrap both `StartCoroutine(DoFrameReadbackAndDump(...))` calls in `#if UNITY_EDITOR`. Check that `_lzFrameCenter` / `_lzFrameEdge` (`:339`) have no runtime consumer outside the invariant dump (`ForceInvariantDump`, `:2770`); if the dump method itself is player-compiled, guard it the same way rather than leaving dead coroutine code. Two GPU `ReadPixels` stalls per map open on retail devices is the thing being removed.

### 5. Development Console spam

`Docs/Reports/perf_baseline_2026-08-26_frames/exp_ad_CORRECT.png` shows the Unity Development Console (bottom-left) full of red lines during the tee pose. Identify the repeating error/exception from the device log. If it is a `PerfBaselineBot` artefact, note it and move on. If it is a game error, report the message + stack; fix it only if the fix is ≤ 5 lines and obviously correct, otherwise file it. Per-frame `Debug.LogError` with stack traces is a plausible source of the ~29 KB/frame GC — say whether it is.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Device protocol = Phase 0b §10: iPhone cooled (thermalState Nominal), pinned yaw, 3 runs, median + raws, frame PNG saved beside every number. Bot job names as in `PerfBaselineBot`.

- [ ] **Hole 08 tee, after:** fps ≥ 58 median; render-thread ms ≤ 15.0 median (Phase 0b a+d = 14.09; with §3 expect lower). Table with before (26.11 ms / 30.1 fps from §10.2) and after.
- [ ] **Hole 01 tee and Hole 06 tee, after:** same table. H06 render-thread must be ≤ its cooled baseline 26.59 ms.
- [ ] **Hole 08 mid-flight (driver):** captured once under protocol (Phase 0b A3 was never run) — number recorded, no target.
- [ ] **Frame Debugger (one capture, cheapest pose, limit ≤ 3000 events — §9.5 crash note):** exactly ONE camera renders during a hole; zero `DrawDepthNormalPrepass`; zero `CopyDepth` unless §2's water decision turned depth on (then exactly one CopyDepth, no DepthNormals).
- [ ] **Frame PNGs:** Hole 08 tee after vs `exp_ad_CORRECT.png` — trees, terrain, shadows, HUD identical to the eye; **tree silhouettes in the same places** (fairness).
- [ ] **Water:** Hole 08 and Hole 13 shoreline screenshot; §2 water decision stated with the before/after ms if depth was turned on.
- [ ] **Hole 01 tee, tree distance:** before/after screenshots; distant trees now cut at 150 m like the other holes. State it plainly in the report — this is the one deliberate visible change.
- [ ] **Teardown paths, on device:** (i) gear → quit mid-hole → Home renders normally (not black, not stale); (ii) hole-complete → Home; (iii) Next Hole → second hole renders with the shell camera still off (log line present, no one-frame flash of the Home skybox); (iv) return to Home → shell camera AND shell light re-enabled (log lines). This is the §1 `OnDestroy` NOTE being verified.
- [ ] **`Assets/Settings/` diff:** exactly `Mobile_Renderer.asset` (feature removed) + `Mobile_RPAsset.asset` (`m_PrefilterDBufferMRT3` line, and `m_RequireDepthTexture` only if §2 chose it). Nothing else.
- [ ] **No scene or TerrainData file modified** (`git status` shows no `.unity`/`.asset` under `Assets/Golf/` or `Assets/Scenes/`).
- [ ] **MapView:** open the map on device, no `ReadPixels` in the frame (Frame Debugger or absence of the readback log), map still aims/pans correctly.
- [ ] **§5 console spam:** message identified, source named, GC-per-frame after stated (Profiler or the bot's counter).
- [ ] EditMode tests: previous pass count preserved (1172+ / 0 failed baseline from `reward_points_backend` close-out; report the actual numbers).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — §1 camera disable/restore (+ light restore in `OnDestroy`), §3 terrain defaults.
- `Assets/Settings/Mobile_Renderer.asset` — §2 feature removed.
- `Assets/Settings/Mobile_RPAsset.asset` — §2 prefilter churn (+ depth texture only if chosen).
- `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` — §4 editor guards.
- `Assets/Scripts/Dev/PerfBaselineBot.cs` — only if a job needs adding for the acceptance captures.
- `Docs/Reports/perf_baseline_2026-08-26.md` — append §11 "Phase 1 after" with the acceptance tables (the report is the running perf ledger).

## Smoke evidence

Device captures per the checklist, saved to `Docs/Reports/perf_baseline_2026-08-26_frames/phase1_*.png`; raw STATS lines appended to `raw_device_stats.txt`. Visual-fidelity verification (Lesson O): Cesar plays one full hole (tee → cup → Next Hole → quit) on the device build and confirms nothing looks different except the far trees on Hole 01.

## Out of scope (do NOT do these)

- The tier system, quality levels, URP asset variants, `QualityTierService` — that is `9a`.
- Shadow cascades/distance (experiment (b)) — per-tier lever, stays at 4/100 here.
- `maximumLODLevel`, `Vegetation.shader` `_WIND`, Spruce conversion / GPU Resident Drawer — Phase 2/3.
- Audio load types, texture overrides, `Resources/HoleData` compression, Optimize Mesh Data — Phase 4.
- Hole 02's 1,495 invisible tree collisions (report §5.1) — own task.
- Hole 06 heightmap density (report §10.5) — own task at the importer.
- Any `.unity` / TerrainData / `HoleGeoImporter` edit.
- Adaptive Performance package.
