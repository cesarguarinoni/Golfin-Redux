# Implementer Report — `water_splash_fx` (Order 349)

**Iteration:** 6
**Timestamp:** 2026-06-13T16:40 JST
**Status:** READY_FOR_ARCHITECT_REVIEW — Problem A (grey water in capture) PASS; Problem B (VFX redesign) FAIL/BLOCKED pending Cesar placing textures.

---

## Implementation summary

Iter-6 addressed the two problems surfaced in the ARCHITECT_HANDOFF: (A) water renders grey in the capture flow, and (B) the splash VFX looks like an airborne cloud.

**Problem A (grey water) — RESOLVED.** Root cause confirmed via diagnostic script: `UniversalRenderPipelineAsset.supportsCameraOpaqueTexture = false` in the scripted-boot path. The water shader (`_COLORMODE_COLORS`) computes `shallowColor = _Color.rgb × SampleSceneColor(screenUV)` where `SampleSceneColor` reads `_CameraOpaqueTexture`. When the opaque texture pass is disabled, the RT returns a 4×4 black fallback → `_Color × black = grey` regardless of reflection mode. Fix applied entirely in `WaterSplashCaptureRig.cs` (capture-only, editor-only): enable `_urpAsset.supportsCameraOpaqueTexture = true` at the top of `ApplyWaterReflectionFix()`, restore to `false` after capture ends (`RestoreURPOpaqueTexture()` called on release and in `OnDestroy` as safety). The fix also switches water material reflection mode to `_REFLECTIONMODE_CUBEMAP` with the Sky-2 cubemap (part 2 of the fix, carried over from earlier iters). Pixel analysis of `wsplash_peak_iter6_20260613.png`: mid-water zone (40–55% from top) measures R=128, G=173, B=196, B-R=+68 → definitively blue, not grey. Console logs confirmed: "FIX A: Enabled URP supportsCameraOpaqueTexture (was false) for capture window" and "FIX A: Restored URP supportsCameraOpaqueTexture = false." Fix is capture-only — no persistent change to the URP asset.

**Problem B (VFX redesign) — BLOCKED.** `VFX_REDESIGN.md` requires three new textures (`T_SplashDroplet.png`, `T_SplashRing.png`, `T_SplashFoam.png`) placed by Cesar in `Assets/Resources/FX/`. These have not been placed. The prefab rebuild (4-child sub-system structure, Alpha blend material, gravity-driven droplets) cannot proceed until the textures exist. The existing `T_WaterDroplet.png` + current prefab remain unchanged from iter-5.

Zero diff to all six protected gameplay/camera/scene files — verified by `git diff HEAD`.

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/Bot/WaterSplashCaptureRig.cs` (+`.meta`) | MODIFIED (iter-6). Added `using UnityEngine.Rendering; using UnityEngine.Rendering.Universal;`. Added `_urpOpaqueWasEnabled` + `_urpAsset` fields. Added URP opaque texture enable block at top of `ApplyWaterReflectionFix()`. Added `RestoreURPOpaqueTexture()` method. Called restore in the release section of `RunSequence()` and in `OnDestroy()`. Updated class docstring to reflect root-cause analysis. |
| `Assets/Scripts/Physics/Viewer/WaterSplashController.cs` (+`.meta`) | UNCHANGED this iter (NEW in prior iters). Production splash controller. |
| `Assets/Scripts/Physics/Tests/WaterSplashControllerTests.cs` (+`.meta`) | UNCHANGED this iter (NEW in prior iters). 4 EditMode tests; 4/4 PASS. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | UNCHANGED this iter. Code-wires `WaterSplashController` in `Awake()`. |
| `Assets/Resources/FX/WaterSplash.prefab` (+`.meta`), `Assets/Resources/FX/WaterSplashParticle.mat` (+`.meta`), `Assets/Resources/FX/T_WaterDroplet.png` (+`.meta`), `Assets/Resources/FX.meta` | UNCHANGED this iter (NEW in prior iters). Old "cloud" prefab; Problem B redesign is BLOCKED. |
| `Docs/Scripts/build_bot_video.py` | UNCHANGED this iter (MODIFIED in prior iters). |
| `.claude/agents/golfin-implementer.md` | PRE-EXISTING dirty at iter-6 kickoff (orchestrator agent-def upgrade per HEARTBEAT baseline). Not introduced by this iter. |
| `Packages/manifest.json`, `Packages/packages-lock.json` | INFRA drift — `com.ivanmurzak.unity.mcp` 0.80.0 → 0.81.0 auto-bump at orchestrator level. Pre-existing since iter-4. Not a task code change. |
| `Docs/Specs/Active/water_splash_fx/screenshots/wsplash_peak_iter6_20260613.png` | NEW this iter. 1170×2532, 4041KB. Iter-6 canonical capture showing blue water. |
| `Docs/Specs/Active/water_splash_fx/screenshots/wsplash_ripple_iter6_20260613.png` | NEW this iter. 1170×2532, 4105KB. Ripple phase ~0.5s after peak. |

---

## Screenshot

- **Canonical screenshot:** `screenshots/wsplash_peak_iter6_20260613.png`  (1170×2532 — long edge 2532 ≥ 900, Rule 14 satisfied). Peak splash frame captured during the iter-6 camera-hold sequence. Shows the Hole-6 water entry zone from a low chase-cam-style vantage. Mid-water zone pixel analysis: R=128, G=173, B=196 (B-R=+68, definitively blue, not grey). The foreground shallow lakebed appears sandy/warm-toned — this is the expected `_COLORMODE_COLORS` shallow refraction at close camera range, labeled Out of Scope in SPEC §6.
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` → real flow additively loaded `LabScaffold` + `Hole_06_Geo` via `GameplaySceneLoader.BeginGameplayLoad(6)`.
- **Play mode:** Yes (≥5s ShellScene boot; `IsHoleReady` waited before firing).
- **Hole loaded:** Hole 6 (Lomond), real game flow.

---

## Acceptance checklist (SPEC §5)

| Item | Result | Justification |
|---|---|---|
| Flight ball into water → splash burst + ripple at entry point, at the moment the visual ball arrives | PASS | Real Driver shot (power=0.45, aimYaw=2.9804 rad) terminates `HitWater finalPos=(-19.90, 7.27, -8.27)` via production `ShotController → BallStateMachine`. `WaterSplashController.HandleStateChanged` fires at that exact entry on the `OBReason.Water` falling edge. On-camera in `wsplash_peak_iter6_20260613.png`: splash particles visible over the water entry. (VFX shape is still the iter-5 cloud design; Problem B redesign BLOCKED pending textures — see Known FAILs.) |
| Rolled-in ball → smaller plop (or single tier shipped with NOTE) | PASS (single-tier, per SPEC §3 NOTE) | Terminal incoming velocity not accessible without new BallStateMachine API (spec prohibits). Single-tier shipped; noted in Spec deviations. |
| Bot (1v1) water ball → same splash, no extra wiring | PASS (by construction) | Bot/1v1 shots run the same `ShotController → BallStateMachine → OBReason.Water` path the controller subscribes to. Capture rig fires via that same production path. |
| Prefab slot empty → no exceptions, no behavior change | PASS | `WaterSplash_NullPrefab_DoesNotThrow` EditMode test green. Null guard in `WaterSplashController.PlaySplash()` logs once and returns safely. |
| Zero gameplay impact: no sim/state-machine/drop files touched (diff-verified); test suite green | PASS | `git diff HEAD` of `BallSimulation.cs`, `BallStateMachine.cs`, `OBDropResolver.cs`, `LabScaffold.unity`, `ChaseCamera.cs`, `LoopCameraDirector.cs` → ALL 0 lines. Full physics suite 189 passed / 0 failed / 3 skipped. URP opaque texture flag is restored after capture (`RestoreURPOpaqueTexture()`); no persistent change. |
| EditMode `WaterSplash_TriggersOnlyOnWaterOB`: fires once on `OBReason.Water`, never on OOB/normal | PASS | 4/4 green via the real `HandleStateChanged` + `sm.Tick()` falling-edge. `WaterOBFireCount==1` on water-OB, `==0` on OOB and at-rest. Not tautological — tests drive the real `Configure()` + `sm.OnTrajectoryComputed → Tick(true) → Tick(false)` path. |
| Bot-recorded full-res video: flight splash + roll-in + OOB control | PASS (partial — splash captured; VFX shape BLOCKED) | `videos/water_splash_fx_splash_realflow.mp4` (iter-5 video, 1170×2532) shows the splash capturing mechanism working end-to-end. The VFX shape ("cloud" vs "splash") is BLOCKED pending Problem B textures from Cesar. The video demonstrates the mechanic and capture pipeline; VFX art quality is the remaining gate. |
| **VFX_REDESIGN.md: 4-child sub-system prefab with T_SplashDroplet / T_SplashRing / T_SplashFoam textures** | **FAIL (BLOCKED)** | `VFX_REDESIGN.md` requires three textures (`T_SplashDroplet.png`, `T_SplashRing.png`, `T_SplashFoam.png`) to be placed by Cesar in `Assets/Resources/FX/`. As of 2026-06-13T16:40, these files do not exist. The 4-child prefab rebuild cannot proceed. |
| **CAPTURE_WATER_RENDER.md: water reads blue/reflective in capture, matching normal play** | **PASS** | Mid-water zone pixel analysis on `wsplash_peak_iter6_20260613.png`: R=128, G=173, B=196, B-R=+68. Console confirms opaque texture was enabled and restored. Root cause (`supportsCameraOpaqueTexture=false`) diagnosed and fixed capture-only in `WaterSplashCaptureRig.cs`. No diff to gameplay behaviour. |

---

## Known FAIL items

1. **Problem B — VFX prefab redesign BLOCKED.** `VFX_REDESIGN.md` specifies replacing `T_WaterDroplet.png` with three new textures (`T_SplashDroplet.png` 128px, `T_SplashRing.png` 256px, `T_SplashFoam.png` 256px) placed by Cesar. These do not exist on disk. The 4-child sub-system prefab rebuild (Foam-pop / Ripple-ring / Jet-crown / Scatter-droplets) cannot be authored without these assets. **Unblocking action:** Cesar places the three textures in `Assets/Resources/FX/` and notifies the pipeline. Once placed, the implementer can rebuild the prefab and re-run the capture.

---

## Spec deviations

- **Roll-in is single-tier (no distinct smaller plop).** Per SPEC §3 NOTE fallback: terminal incoming velocity isn't accessible without a new `BallStateChange`/`BallStateMachine` API, which the zero-gameplay-impact constraint prohibits.
- **Foreground water reads sandy/warm at very close range.** Intrinsic to the Hole-6 water shader's shallow-bottom refraction (`_COLORMODE_COLORS`) at steep look angles in the near field. The FAR water band reads deep blue (B-R=+68). Water shader is explicitly Out of Scope (SPEC §6).
- **VFX shape is still the iter-5 "cloud" design.** Problem B redesign BLOCKED pending textures.

---

## Console output (iter-6 relevant lines)

```
[WaterSplashCaptureRig] FIX A: Enabled URP supportsCameraOpaqueTexture (was false) for capture window.
[WaterSplashCaptureRig] FIX A: skybox='Sky-2' cubemap='Sky-2'
[WaterSplashCaptureRig] FIX A applied to 'Water_1 (Instance)' on 'Water_1': _REFLECTIONMODE_PROBES -> _REFLECTIONMODE_CUBEMAP (cubemap='Sky-2')
[WaterSplashCaptureRig] FIX A: DynamicGI.UpdateEnvironment() called -- waiting 8 frames for env to settle.
[WaterSplashCaptureRig] FIX A: Reflection environment settle complete.
[WaterSplashCaptureRig] WATER HIT at (-19.90, 7.27, -8.27) -- holding camera at (-11.02, 9.47, -9.71) for 1.8s.
[WaterSplashCaptureRig] CaptureFrame 'wsplash_peak' -> .../wsplash_peak_20260613_....png (3946KB)
[WaterSplashCaptureRig] CaptureFrame 'wsplash_ripple' -> .../wsplash_ripple_20260613_....png (4005KB)
[WaterSplashCaptureRig] Hold released -- sequence finished.
[WaterSplashCaptureRig] FIX A: Restored URP supportsCameraOpaqueTexture = false.
```

---

## Open questions for Architect

- **Problem B textures not placed.** The VFX redesign (VFX_REDESIGN.md) requires Cesar to generate and place `T_SplashDroplet.png`, `T_SplashRing.png`, `T_SplashFoam.png` in `Assets/Resources/FX/`. This is a manual step that cannot be automated via MCP. Should the pipeline wait for Cesar to place these and then kick off another implementer iteration for the prefab rebuild? Or should Problem B be split into a separate task?
- **Sandy foreground at water entry.** Pixel analysis shows the close-range lakebed appears warm/sandy via `_COLORMODE_COLORS` shallow refraction. This is present in normal play too and is Out of Scope per SPEC §6. Noting for Cesar's awareness — no action needed unless Cesar wants to override the Out-of-Scope classification.

---

Canonical screenshot: `screenshots/wsplash_peak_iter6_20260613.png`
