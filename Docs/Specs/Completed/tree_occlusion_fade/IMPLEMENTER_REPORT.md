# IMPLEMENTER_REPORT — tree_occlusion_fade

**Iteration:** iter-1 · **Date:** 2026-08-07 · **Iteration shape:** `shader/occlude-fade:visual-window-unproven`

**Bottom line:** the implementation is complete and the *driver* is verified end-to-end on a real Hole 1
through the player's own entry path. The *shader window has NOT been visually proven to render.*
I am setting `IMPLEMENTER_BLOCKED` rather than claiming a PASS I cannot evidence — see §D.

---

## A. STEP 0 — inventory + premise verification (SPEC §3)

Method: opened `Hole_01_Geo.unity` and `Hole_06_Geo.unity` additively via Unity MCP and enumerated
`terrainData.treePrototypes[*]` renderers + any `StandaloneTrees` container.
Raw dumps: `Docs/Diagnostics/_capture/tree_occ_inventory.txt`, `tree_occ_inventory2.txt`.

### Hole 1 — 4 prototypes, 1362 tree instances, **no** `StandaloneTrees` container

| Prototype | Leaf | Bark | Impostor |
|---|---|---|---|
| `MESH_JapaneseBlack_01` | `MAT_JapaneseBlackLeaf` → **Custom/Vegetation** | `MAT_JapaneseBlackBark` → URP/Lit | `MAT_01JapaneseBlackImposter` → URP/Lit |
| `MESH_JapaneseBlack_01_Var1` | `MAT_JapaneseBlackLeaf_Var1` → **Custom/Vegetation** | `MAT_JapaneseBlackBark_Var1` → URP/Lit | (shares `MAT_01JapaneseBlackImposter`) |
| `MESH_ScottishPine_01` | `MAT_ScottishPineLeaf` → **Custom/Vegetation** | `MAT_ScottishPineBark` → URP/Lit | `MAT_ScottishPineImposter01` → URP/Lit |
| `Mesh_Metasequoia` | `MAT_MetasequoiaLeaf` → **Custom/Vegetation** | `MAT_MetasequoiaBark` → URP/Lit | `MAT_MetasequoiaImposter` → URP/Lit |

### Premise verdicts

| # | SPEC premise | Verdict |
|---|---|---|
| 1 | Leaves are `Custom/Vegetation` | **CONFIRMED** (all 4 Hole-1 prototypes) |
| 2 | Bark + impostors are stock URP/Lit | **CONFIRMED** (all 4 Hole-1 prototypes) |
| 3 | Hole 6 same shape as Hole 1 | **FALSE — better than assumed.** Hole 6 is 6 × `Fir 01-06` (434 instances) where **both** `fir_bark` **and** `fir_leaves_1/2` are *already* `Custom/Vegetation`. Hole 6 needs **zero** material retargeting and fades trunk-and-canopy from the shader patch alone. |
| 4 | Spruce 1/3 are on `Mobile_Tree_Bundle` NoWind built-in-RP Standard shaders | **FALSE.** Spruce is on `Shader Graphs/Bark_URP` + `Shader Graphs/Leaves_URP` from `Assets/Realistic Tree/`. Also **no Spruce and no `StandaloneTrees` container exists on Hole 1 or Hole 6**, so that path is unexercised on both test holes. Report-only per SPEC §6; **not fixed here.** |
| 5 | `SV_POSITION` available in each patched pass | **CONFIRMED** — every target pass declares `float4 clipPos : SV_POSITION` in `VertexOutput` and every target `frag` takes `VertexOutput IN`. Forward/GBuffer already use `IN.clipPos.xyz` for `LODDitheringTransition`. |

### Additional STEP-0 findings the SPEC did not anticipate

- **The injection guard is `_ALPHATEST_ON`, not `ASE_DEPTH_WRITE_ON`.** Every pass wraps its clip in
  `#ifdef _ALPHATEST_ON`. The SPEC's instruction ("place it after the `#endif`, unconditionally") is
  still exactly right — I just note the conditional's real name. `_ALPHATEST_ON` is in fact
  `#define`d to 1 unconditionally at every pass scope (lines 216/939/1351/…), so alpha clip is always live.
- **`WorldPosition` is in scope in all 5 target passes** — each declares `#define ASE_NEEDS_FRAG_WORLD_POSITION`
  at pass scope (Forward + GBuffer build it from `tSpace0..2.w`; the rest from `IN.worldPos`).
- **`_NORMALMAP` is `#define`d 1 unconditionally**, so retargeted bark keeps its normal map.
- **`Custom/Vegetation` is `Cull Off` and `Queue=Geometry(2000)`.** Retargeted bark moves Back→Off culling
  (no visual change on closed trunk meshes; small extra fragment cost) and impostors move queue 2450→2000.
  Both are depth-tested opaque so no sorting change is expected. Flagged, not measured.
- **`Occlusion = 1 - _WindStrength1`** in this shader, default `0.5`. Left alone it would have halved
  ambient on every retargeted material vs Lit's occlusion 1.0. I set `_WindStrength1 = 0` on all 7.
- **Collider-proxy renderers** (`LeafCollider`, `BarkCollider*`) use the read-only URP package `Lit.mat`
  but are `enabled=false` (never render). **Not** retargeted — correctly excluded.

---

## B. What was built

### B1. `Assets/Scripts/Physics/Viewer/TreeOccludeFadeDriver.cs` (new)
Static class, `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, zero scene wiring, globals only,
hooked on `RenderPipelineManager.beginCameraRendering`. Publishes `_GolfinOccFadeBall`,
`_GolfinOccFadeCam`, `_GolfinOccFadeStrength`, `_GolfinOccFadeParams`, `_GolfinOccFadeBias`.
All SPEC §4.1 tunables present incl. the `Disabled` kill switch. Statics reset in `Init`, strength
published as 0 on init so a stale global cannot leak.

**Two SPEC §4.1 premises were falsified in play mode and required design changes:**

1. **`Camera.main` is NOT the gameplay camera.** During a hole the `MainCamera` tag stays on the
   ShellScene camera at `(0,1,-10)`. Gating on `cam == Camera.main` (as §4.1 implies) silently pinned the
   cone origin to `(0,1,-10)` and held strength at 0 forever. **Fix:** resolve the gameplay camera by its
   `ChaseCamera` component instead, and integrate once per frame rather than per-camera.
2. **`ChaseCamera.CurrentFocus` is `(0,0,0)` during aiming.** `LoopCameraDirector` only calls
   `SetTarget`/`ResetToOrigin` from `ArmChaseForShot`, and deliberately leaves the chase camera dormant
   before that ("the dormant camera writes nothing", `LoopCameraDirector.cs:231`). So at the tee
   `_target == null` and `_shotOrigin == (0,0,0)` — the SPEC's "resting ball at `_shotOrigin`" is not true
   in the production loop, and the cone would have pointed at the world origin for the whole aiming phase.
   **Fix:** `TryResolveFocus()` uses the chase focus when live (flight, armed shots, terminal modes) and
   falls back to the live ball transform when it is not. This required one new read-only accessor,
   `LoopCameraDirector.CurrentBall`, mirroring the `ChaseCamera.CurrentFocus` accessor the SPEC sanctioned.

   *Both are verified corrections, not speculation — see the §C log excerpts.*

### B2. `ChaseCamera.cs` — `public Vector3 CurrentFocus` accessor (exactly as specced, 2 lines + doc).

### B3. `Golfin.Physics.Viewer.asmdef` — added `Unity.RenderPipelines.Core.Runtime`
(GUID `df380645f10b7bc4b97d4f5eb6303d95`), required to reference `RenderPipelineManager`.

### B4. `Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader`
One helper block in the shared `HLSLINCLUDE` + one **identical** injection in each of 5 passes.
Pass membership verified programmatically after insertion:

```
line  750 -> pass Forward
line 1747 -> pass DepthOnly
line 2556 -> pass Universal2D
line 2959 -> pass DepthNormals
line 3503 -> pass GBuffer
patched passes: [DepthNormals, DepthOnly, Forward, GBuffer, Universal2D]
```
**ShadowCaster and Meta are untouched**, as specced. All edits are inside
`// ── GOLFIN OCCLUDE FADE ──` … `// ── END GOLFIN OCCLUDE FADE ──` markers, and a warning banner
was added to the file header that an Amplify regen will wipe them.

**One deviation from the SPEC snippet:** the SPEC's helper reads `_WorldSpaceCameraPos`. That symbol is
not declared inside `HLSLINCLUDE` (it arrives with URP's per-pass `UnityInput.hlsl`), and it produced a
hard compile error: `undeclared identifier '_WorldSpaceCameraPos' … Pass: ShadowCaster, Vertex program`.
The driver now publishes the camera position itself as `_GolfinOccFadeCam`, which makes the shared block
dependency-free and identical for every pass. Everything else matches the SPEC snippet verbatim.

**Compile status:** `ShaderUtil.GetShaderMessages` → **0 errors**. The 2 remaining warnings
(`_FORWARD_PLUS` deprecated, `UnityGBuffer.hlsl` deprecated in 6.1) are pre-existing URP-6.1 deprecations
in untouched ASE boilerplate.

### B5. Bark + impostor retarget (SPEC §4.3) — 7 materials
`MAT_JapaneseBlackBark`, `MAT_JapaneseBlackBark_Var1`, `MAT_01JapaneseBlackImposter`,
`MAT_ScottishPineBark`, `MAT_ScottishPineImposter01`, `MAT_MetasequoiaBark`, `MAT_MetasequoiaImposter`.
Slot mapping carried explicitly: `_BaseMap`→`_Albedo` (+ST), `_BumpMap`→`_NormalMap` (+ST,
`_NormalMapStrength=0` when the source had no map), `_BaseColor`→`_Color`, metallic/smoothness across,
`_Cutoff`→`_AlphaCutoff` (0 for opaque bark, source cutoff for impostors), `_Wind=0` + `_WIND` disabled,
plus `_WindStrength1=0` / `_Shadowcolor=(0,0,0,0)` / `_ShadowStrength=0` to hold Vegetation's own
defaults at parity. Shader GUID `e80a1e91e51638b47b825fa1c86cbb65` (Custom/Vegetation) confirmed on disk.

### B6. `Assets/Scripts/Physics/Tests/TreeOccludeFadeDriverTests.cs` (new) — SPEC §4.4, 16 tests.
### B7. `Assets/Scripts/UI/Editor/TreeOccludeFadeCaptureBot.cs` (new) — real-entry-path acceptance harness.

---

## C. Acceptance checklist (SPEC §5)

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Window works — ball visible through a faint dithered ghost | **FAIL (unproven)** | See §D. The occluded configuration was reached and `strength=1.000`, but no frame demonstrably shows the dither. |
| 2 | No pop — gradient edge, 0.25 s ramp | **PARTIAL** | Temporal ramp PASS objectively: `strength 1.000 → 0.000` within the 0.25 s ramp and back to `1.000`. Spatial gradient **unproven** (same gap as #1). |
| 3 | Zero-diff when corridor clear | **PASS (by construction + data)** | At the tee `treesInCone=0`; shader returns early at `s <= 0.001` and clips nothing when `golfinFade <= 0.001`, so a clear corridor is a literal no-op. |
| 4 | Flight — window tracks the live ball | **PASS (data)** | Published focus tracked the ball through flight: `218.28 → 177.41 → 170.22 → 163.98 → 160.84 → 158.32 → 156.18 → 155.60`, strength held `1.000` throughout, no flicker at the terminal transition. |
| 5 | Map view — no window | **PASS** | `MAP opened via Open() — IsOpen=True` then `GLOBALS[map_open] strength=0.000` while `disabled=False`. |
| 6 | Shadows unchanged | **PASS (by construction)** | ShadowCaster pass verified untouched (§B4). Not visually re-shot. |
| 7 | No depth artifacts / SSAO halo | **PARTIAL** | DepthOnly + DepthNormals patched **identically** to Forward (verified programmatically, §B4), which is the stated mechanism. Not visually confirmed. |
| 8 | Kill switch restores pre-change rendering | **PASS (data)** | `Disabled=true` → `strength=0.000`; `Disabled=false` → `strength=1.000`. At strength 0 the shader path is a literal no-op. |
| 9 | EditMode suite green, zero regressions | **PASS** | **1023 tests, 1020 passed, 0 failed**, 3 skipped (pre-existing, each with a documented Stage-C1 reason). |
| 10 | Device: dither grain, perf, cone tuning | **DEFERRED — Cesar, on device** | As specced. |

### Real-entry-path proof (Rule 2)
The bot drives `StartButton.onClick` → `PlayButton.onClick` → hole-selection `PLAY.onClick` →
Lomond Hole 1, and fires through the production `ShotController` drag path
(`BeginExternalDrag` → ramped `SetExternalPower` → `EndExternalDrag(bypassFlickGate:true)`).
No synthetic entry point. Log: `capture_log.txt`.

```
[12.4s] reached: hole tee (ChaseCamera)
[16.4s] TEE focus=(219.43, 11.46, 34.73)  cam=(227.21, 14.46, 36.59)  treesInCone=0
[16.4s] GLOBALS[tee_on]  strength=1.000 ball=(219.43,11.46,34.73) cam=(227.21,14.46,36.59)
                         params=(cosOuter=0.9613,cosInner=0.9848,cut=0.85,feather=1.50) bias=0.50
[17.0s] GLOBALS[tee_off] strength=0.000 … disabled=True
[17.6s] GLOBALS[tee_back_on] strength=1.000 … disabled=False
[20.3s] FIRED via production ShotController drag path, power=0.62
[23.8s] *** FROZE at flight[32] treesInCone=2 …  cam=(163.09,13.12,22.23) focus=(202.47,11.12,30.93) dist=40.4m
[82.5s] REST focus=(205.72, 11.19, 31.66) cam=(163.09, 13.12, 22.23) treesInCone=2
[85.3s] MAP opened via Open() — IsOpen=True → strength=0.000
```

---

## D-VIDEO (added after Cesar asked for a video — supersedes the block reasoning below)

`videos/tree_occlude_fade_ab.mp4` — 1170×2532, 16.5 s, real Hole 1, real entry path, recorded through
Unity Recorder with a `GameViewInputSettings` source (no RT reads during the record; all `CaptureCore`
calls are skipped in record mode). Four phases, captioned: shipped 10°/16° → kill switch → 45°/60° → back.

**Two conclusions, and the video is what produced both:**

1. **The shader injection DOES render.** At the 45°/60° cone the Bayer screen-door is unmistakable, and it
   appears on **bark as well as leaves** — which independently confirms the §4.3 retarget took effect.
   Side-by-side 2× crop: `screenshots/ab_killed_vs_widecone_2x.png` (top = kill switch, bottom = 60° cone);
   full frames `occfade_killswitch_full.png` / `occfade_widecone_full.png`.
2. **A real focus bug, found only because of the video.** The first recording showed no fade even at 60°.
   The log said why: published `ball=(219.43,11.46,34.73)` — the **tee** — while the camera was downrange at
   `(163.09,…)`, i.e. the cone was pointing behind the viewer. Cause: after the terminal `SetTarget(null)`,
   `ChaseCamera.CurrentFocus` degrades to `_shotOrigin`, which is the origin of the shot that just
   *finished*. SPEC §5.4 anticipated that fallback and judged it harmless; it is not — it aims the cone
   backwards at exactly the moment the ball is sitting in trees. **Fixed:** `TryResolveFocus` now prefers
   the live ball transform and treats `CurrentFocus` as the fallback. Re-recorded: `focus=(155.31,10.12,20.36)`
   = the resting ball, 8.5 m from the camera. This is the bug the acceptance video existed to catch.

**Cone tuning — RESOLVED. Cesar's call, 2026-08-07: ship 45°/60°.**
`InnerHalfAngleDeg`/`OuterHalfAngleDeg` defaults changed 10/16 → 45/60 in the driver, superseding the
SPEC §4.1 values. Rationale kept in the field's doc comment: the gate is *angular*, so a near occluder
subtends a huge screen area while sitting mostly outside a narrow cone — at 10/16 a trunk a metre or two
from the camera filled two-thirds of the frame and barely faded.

Re-recorded at the new defaults (same lie, same real entry path). The clip is now
**shipped 45/60 → kill switch → old narrow 10/16 → back to shipped**, so the comparison runs the right way
round. Live params confirm it: phases 1 and 4 publish `cosOuter=0.5000 cosInner=0.7071` (= 60°/45°),
phase 3 `0.9613/0.9848` (= 16°/10°). The dither is plainly visible at the shipped default on bark, branch
and foliage — `screenshots/occfade_shipped_45_60_crop2x.png` (2× crop),
`occfade_shipped_45_60_full.png` (full frame). EditMode re-run at the new defaults: **1023 / 1020 passed /
0 failed.**

**Caveat on the A/B rigour:** the phase-2 and phase-3 frames are not the same camera pose (the scene is
live, not frozen, during the record), so the comparison is qualitative. The dither pattern itself is
self-evident — a regular 4×4 pixel grid over geometry is not a lighting or camera artifact — but a
frozen-pose diff would be stronger and is cheap to add.

---

## D. Why iter-1 was BLOCKED (superseded in part by D-VIDEO above)

**I cannot evidence that the dithered window actually renders**, and I will not mark §5.1 PASS without it.

- The occluded scenario **was** reached through real play: the ball came to rest in the ROUGH directly
  behind a trunk that fills two-thirds of the screen — Cesar's exact described defect — with
  `strength=1.000` and 2 trees confirmed inside the cone, 40.4 m from the camera.
  That frame was captured at 1170×2532 with the sanctioned `screenshot-game-view` MCP tool and surfaced
  to Cesar inline in chat, but **it was not persisted to disk** — the tool returns the image, not a file,
  and `CaptureCore.SnapPlayModeSafe` (the in-coroutine path that would have written it) was producing
  phantom paths, see the capture-path defects below. **There is therefore no canonical screenshot file
  for this iteration**, which is a second reason this cannot go to review as-is; the next run must persist
  the three A/B frames properly.
- **In that frame the trunk does not visibly fade.** That may be correct-but-narrow behaviour rather than a
  bug: the gate is *angular*, so a trunk ~1 m from the camera sits mostly **outside** a 16° half-angle cone
  even while filling the screen — exactly the tuning SPEC §5.10 defers to Cesar on device. But I could not
  distinguish "working and too narrow" from "not executing at all".
- The decisive test — re-capture with the cone temporarily widened to 45°/60° — **did not complete**: the
  gameplay stack unloaded while the sim was frozen, so `ChaseCamera`/`LoopCameraDirector` went null and
  strength dropped to 0 before the second capture.

**Two capture-path defects found along the way (both real, both worth knowing):**
1. `CaptureCore.SnapPlayModeSafe` **returns a path for a file it never wrote** when
   `ScreenCapture.CaptureScreenshotAsTexture()` returns null (editor unfocused). It logs a warning but
   still hands back `Path.GetFullPath(path)`. Every `occfade_*` path in the first two runs is a phantom —
   0 files on disk. Worth a `CaptureCore` fix (return `null`/empty on a null texture).
2. `ShotController.EndExternalDrag(bool bypassFlickGate = false)` — reflection does **not** apply optional
   defaults, so `Invoke(shot, null)` throws `TargetParameterCountException` and silently kills the calling
   coroutine. Fixed in my bot; a trap for any future reflection-driven harness.

**What the next iteration needs (~15 min):** re-run `GOLFIN > Physics > Capture Tree Occlude Fade`, and at
the `HOLDING frozen` point capture three frames via `screenshot-game-view` — (a) shipped 10°/16°,
(b) `MaxOpacityCut = 0` (an off-state that needs no ramp and so survives `timeScale = 0`), and
(c) cone widened to 45°/60°. A pixel diff of (a) vs (b) settles §5.1 and §5.2; (c) proves the injection
executes independent of tuning. `TreeOccludeFadeCaptureBot.ReleaseHold` now holds the freeze indefinitely
under orchestrator control, so the stack-unload race that beat me is already fixed.

---

## E. ⚠️ `Vegetation.shader` is in a **gitignored** path

`.gitignore:104` ignores `Assets/Packs/`, and `Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader`
is **untracked** — the entire shader half of this feature will not reach the repo on a normal commit.
(Other files under `Assets/Packs/` are tracked only because they predate that rule.)

Close-out must run:
```
git add -f "Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader"
```
Once tracked, gitignore no longer applies and future edits commit normally. I did **not** commit — that is
Cesar's call. All 7 `.mat` files, `ChaseCamera.cs`, `LoopCameraDirector.cs` and the asmdef **are** tracked.

---

## F. Files modified or created

| File | Status | Summary |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/TreeOccludeFadeDriver.cs` | **new** | Globals-only occlude-fade driver: cone/strength state machine, focus smoothing, tunables, kill switch |
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | modified | +`CurrentFocus` accessor (2 lines + doc), as specced |
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | modified | +`CurrentBall` read-only accessor so the driver has a focus during aiming |
| `Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef` | modified | +`Unity.RenderPipelines.Core.Runtime` ref for `RenderPipelineManager` |
| `Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader` | modified (**untracked — see §E**) | Helper block + identical dither-clip injection in 5 passes; header warning |
| `Assets/Art/…/MAT_JapaneseBlackBark.mat` | modified | Retargeted URP/Lit → Custom/Vegetation, wind off |
| `Assets/Art/…/MAT_JapaneseBlackBark_Var1.mat` | modified | ” |
| `Assets/Art/…/MAT_01JapaneseBlackImposter.mat` | modified | ” (cutoff 0.538 preserved) |
| `Assets/Art/…/MAT_ScottishPineBark.mat` | modified | ” |
| `Assets/Art/…/MAT_ScottishPineImposter01.mat` | modified | ” (cutoff 0.311 preserved) |
| `Assets/Art/…/MAT_MetasequoiaBark.mat` | modified | ” |
| `Assets/Art/…/MAT_MetasequoiaImposter.mat` | modified | ” (cutoff 0.5 preserved) |
| `Assets/Scripts/Physics/Tests/TreeOccludeFadeDriverTests.cs` | **new** | 16 EditMode tests (ramp, focus smoothing, param packing, global round-trip) |
| `Assets/Scripts/UI/Editor/TreeOccludeFadeCaptureBot.cs` | **new** | Real-entry-path acceptance harness (boot → tee → fire → freeze on occlusion) |
| `Docs/Specs/Active/tree_occlusion_fade/{STATUS,IMPLEMENTER_REPORT,HEARTBEAT.log,capture_log.txt}` | new | Task docs |
| `Docs/Specs/Active/tree_occlusion_fade/screenshots/*.png` | new | Capture + §4.3 A/B rig frames |

### Pre-existing drift NOT introduced by this task (Rule 13)
These were dirty in the working tree at the iter-1 kickoff baseline (`HEARTBEAT.log`, HEAD
`0f39ec0a37beb3a05b887cc8ddb4ee00aaaeda77`) and are untouched by me:

```
 M Assets/Scripts/UI/Gacha/GachaCarouselController.cs
 M Assets/Scripts/UI/ModeSelect/ModeCardController.cs
 M Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs
 M Docs/Specs/Completed/shot_ui_translucency_glow/ARCHITECT_REVIEW.md
 M Docs/TellCode.md
```

### On the §4.3 before/after gate
`screenshots/bark_retarget_{before,control_unpatched_shader,after}.png` were rendered from an offscreen
EditMode `Camera.Render()` rig. **They cannot grade the retarget**: that rig fails to sample `_Albedo` on
`Custom/Vegetation` at all — the leaf materials, which were already on that shader and have a verified
`_Albedo` bound (`T_JapaneseBlack_Leaf_albedo`), render as flat untextured quads in the **before** frame
too. The `control_unpatched_shader` frame (rendered with my GOLFIN blocks stripped out) is **pixel-wise
the same failure**, which is what exonerates my shader edit as the cause. Consequence: **SPEC §4.3's
before/after gate is NOT satisfied** and bark parity must be graded on a real hole in the next iteration.

### Editor state
Play mode exited, runtime tunables restored to file defaults, no scene saved, no scene mutated,
`Time.timeScale=1`. Both hole scenes were opened additively for the inventory and closed again.
