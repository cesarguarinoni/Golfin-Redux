# SPEC — `golfer_3d_test`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status
See `STATUS.md`. `SPEC_READY` — **step 0 is DONE (2026-09-05)**; every asset in §3 is in `Assets/Art/3D/Characters/_Test/`. Code can start.

> **EXPERIMENT — OPT-IN ONLY (Cesar, 2026-09-05).** Nothing from this task may reach a normal Dev/TestFlight build unless explicitly requested. Everything is gated on the scripting define `GOLFIN_GOLFER_TEST` (§5.6): without it the code is compiled out, the scene carries no reference, and the `_Test` assets are excluded from the build.

## Goal
Prove the golfer pipeline end-to-end with **free, commercially usable** assets before any money or artist time is spent: a rigged Humanoid character standing at the ball in `GameplayScene`, playing a golf swing on shot commit, a putt stroke in putter mode, returning to idle when the ball stops, on device at the Low quality tier. Output is a working `PfGolfer_Test` prefab + a shared `AnimatorController_Golfer` that the real characters (see `Docs/Design/CHARACTER_3D_REMAKE_OPTIONS.md`) drop into unchanged — **only the FBX changes later.** No likeness to the roster is expected from this test.

## 1. Asset choice (researched 2026-09-05)

| Role | Pick | License | Why |
|---|---|---|---|
| **Character** | **Quaternius — Universal Base Characters** (`Superhero_Male_FullBody` + `Superhero_Female_FullBody` — the free Standard zip ships only the Superhero proportion set; Regular/Teen are Patreon-only — plus `Hair_SimpleParted` / `Hair_Long` rigged to the head bone) | **CC0** | ~13k tris, Humanoid, FBX + Unity-URP source, tested in Unity, 3 proportion sets. Realistic-ish proportions (arm span ≈ height) = the exact thing the old models failed. |
| **Swing / putt / idle clips** | **Mixamo** (Adobe, free with an Adobe ID) | Royalty-free, commercial games explicitly allowed, no attribution; may not redistribute the raw files | Verified 2026-09-05: 34 golf clips exist. Downloaded 11 on the stock **Y Bot** (Without Skin): Drive, Putt, Chip, Drive Setup, Pre-Putt, Tee Up, Post-Swing, Bad Shot, Putt Victory, Putt Failure, Idle. Unity Humanoid retargets them onto the Quaternius avatar — no Mixamo upload of our model needed (the auto-rigger rejects the already-rigged Quaternius FBX anyway). |
| **Fallback idle / walk / celebrate** | **Quaternius — Universal Animation Library 1 & 2** | CC0 | 250+ Humanoid clips, Unity-tested exports; no golf swing, so only fills the non-golf states. |
| **Club** | existing `Assets/Art/3D/Clubs/Drivers/GOLFIN_Driver/GOLFIN_Driver.fbx` and `Putters/GOLFIN_Putter/GOLFIN_Putter.fbx` | ours | Already in the repo. |

Rejected for the test: Mixamo *characters* (fine license, but Adobe has called the service unsupported since the June-2025 outage — don't build a dependency on their models, take only the clips); CMU mocap (BVH, needs cleanup); paid `Golf animations (Motion Cast#05)` $35 on the Asset Store — the paid fallback **if** Mixamo's golf clips turn out unusable, not before.

## 2. Hard requirements (from `CHARACTER_3D_REMAKE_OPTIONS.md` §2, unchanged)
R1 Humanoid rig, T-pose · R2 bottom-centre pivot, 1 u = 1 m · R4 ≤ 15k tris, 1 material, URP Simple Lit · R5 sockets `ClubSlot`, `PutterSlot`, `GameplayIdleClubSlot`, `GameplayIdlePuttClubSlot`, `ClubStart`, `ClubEnd` · R6 naming `MESH_/T_/M_/ANIM_`.

## 3. Step 0 — assets (DONE 2026-09-05, Architect via Chrome)

`Assets/Art/3D/Characters/_Test/`
- `Quaternius/Superhero_Male_FullBody.fbx`, `Superhero_Female_FullBody.fbx` (rigged, ~13k tris), `Textures/` (BaseColor Light/Dark, Roughness, Unity normals, eyes, hair), `Hair/` (`Hair_SimpleParted`, `Hair_Long`, eyebrows — *Rigged to Head Bone* variants), `LICENSE_CC0.txt`.
- `Mixamo/ANIM_Golf_{Drive,Putt,Chip,DriveSetup,PrePutt,TeeUp,PostSwing,BadShot,PuttVictory,PuttFailure}.fbx`, `ANIM_Idle.fbx` — Y Bot skeleton, no skin, 30 fps, no keyframe reduction, "FBX for Unity". `YBot_TPose.fbx` = the source skeleton (reference only; never in a prefab). `LICENSE_Mixamo.txt`.

Nothing else to download. If a clip retargets badly, the Mixamo search `golf` has 5 more Drive variants and 3 more Putt lengths.

## 4. Architecture context
- **Asmdefs:** `Golfin.Gameplay.Loop` (`BallStateMachine`), `Golfin.Gameplay.Input` (`ShotController`), `Golfin.Gameplay.UI` (`ClubSelectionBroadcast`, `QualityTierService`). New code goes in `Assets/Scripts/Gameplay/Golfer/` in **`Assembly-CSharp`** (no new asmdef for a test; it references the above already).
- **Events to consume** (all exist):
  - `ShotController.OnShotResolved : Action<ShotInput, BallPhysicsModifiers>` (`Assets/Scripts/Gameplay/Input/ShotController.cs:183`) — the shot is committed → play the swing.
  - `ShotController.ShotCancelled : static Action` (`:488`) → back to idle.
  - `ShotController.IsPutt`, `ShotController.CameraHeadingRadians` (`:23`) — stance side & facing.
  - `BallStateMachine.OnShotComplete : Action<ShotResult>` (`Assets/Scripts/Gameplay/Loop/BallStateMachine.cs:30`) → ball at rest → re-place golfer at the ball, idle.
  - `ClubSelectionBroadcast.OnPutterModeChanged : static Action<bool>` (`Assets/Scripts/Gameplay/UI/ShotUI/ClubSelectionBroadcast.cs:33`) → swap club mesh + `IsPutt` animator bool.
  - `QualityTierService.OnTierChanged : static Action<QualityTier>` (`…/Quality/QualityTierService.cs:43`) → Low tier: `SkinnedMeshRenderer.quality = SkinQuality.Bone2`, shadows off.
  - NOTE: how the ball's resting `Transform` is exposed to non-Loop code — check `BallStateMachine` / `ShotController` for the ball reference the HUD uses (the trajectory system reads it). Use that; do **not** `FindObjectOfType` every shot.
- **Existing prefab contract:** `Assets/Prefabs/Original/Characters/PfYoungMale.prefab` — copy the six socket transforms and the two `UnplayableChecker` children (their script guid `41412eff…` is unresolved in the repo → **drop the component, keep the empty transforms**, flag in report).
- **Naming:** `Docs/Game Design/ASSET_NAMING_CONVENTION.md`.

## 5. Implementation

### 5.1 Import settings (Editor, then commit the `.meta`)
- `Superhero_Male_FullBody.fbx` (and Female): Rig → **Humanoid**, Avatar **Create From This Model**, Configure → all green, Pose = T-pose (use *Enforce T-Pose* if needed). Model → Scale 1, Bake Axis Conversion ✔, Import Blendshapes ✖, Normals Import, Mesh Compression Medium, Read/Write ✖. Materials → Extract to `_Test/Quaternius/Materials/`, convert to **URP Simple Lit**.
- Every `ANIM_*.fbx`: Rig → **Humanoid**, Avatar **Create From This Model** (they carry the Y Bot skeleton; Humanoid retargets onto the Quaternius avatar — do NOT copy the Quaternius avatar onto them). Mixamo Y Bot imports at 0.01 scale with `Bake Axis Conversion` — check the clip's avatar is green before anything else. Animation tab per clip: **Root Transform Rotation: Bake Into Pose, Based Upon Original**; **Root Transform Position (Y): Bake Into Pose, Based Upon Original**; **Root Transform Position (XZ): Bake Into Pose, Based Upon Original** (golfer must not drift off the ball); `Loop Time` ✔ only on `Idle`, `PrePutt`, `TeeUp` if it is an address pose; Anim. Compression **Optimal**. Rename the clip inside the FBX to `ANIM_Golf_Drive` etc.
- On import of each clip, scrub in Preview with the Quaternius avatar assigned: **write down the impact frame time** (club at ball) for Drive and Putt → these two numbers go in the report; the real spec will delay ball launch by them.

### 5.2 `AnimatorController_Golfer` — `Assets/Animations/Golfer/AnimatorController_Golfer.controller`
Parameters: `IsPutt` (bool), `Swing` (trigger), `Cancel` (trigger), `Reset` (trigger).
States (one layer, no blend trees): `Idle` (default, loop) → `Address` (TeeUp / PrePutt via `IsPutt` sub-choice, loop) → `Swing_Drive` / `Swing_Putt` (`Swing` trigger, `IsPutt` decides; **Has Exit Time ✖**, transition 0.1 s) → `Idle` on exit time. `Cancel` from `Address` → `Idle`. `Reset` any-state → `Idle`. Entering `Address` happens when `ShotController.OnStateChanged` reports the aiming/armed state — NOTE: read `ShotInputState` (`Assets/Scripts/Gameplay/Input/`) and pick the first state after idle; name it in the report.

### 5.3 `PfGolfer_Test.prefab` — `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_Test.prefab`
```
PfGolfer_Test            (GolferPresenter, Animator: AnimatorController_Golfer, Apply Root Motion ✖, Culling: Cull Update Transforms)
 └ Superhero_Male_FullBody (SkinnedMeshRenderer, M_GolferTest, cast shadows ON only ≥ Mid tier)
    └ …Humanoid bones…
       └ RightHand
          ├ ClubSlot / PutterSlot / GameplayIdleClubSlot / GameplayIdlePuttClubSlot (empty transforms, copied from PfYoungMale)
          │   └ GOLFIN_Driver (under ClubSlot) · GOLFIN_Putter (under PutterSlot) — one active at a time
          └ ClubStart / ClubEnd (empties on the grip / head for the future trail)
 ├ UnplayableChecker ×2   (empties, no component — see §4)
```
Hand-place the club in the sockets in the Editor with `Swing_Drive` scrubbed to the address frame; store the local pose on the socket, not the mesh.

### 5.4 `GolferPresenter.cs` — `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` (namespace `Golfin.Gameplay.Golfer`)
- `[SerializeField] Animator anim; Transform driverSocketRoot, putterSocketRoot; float stanceDistance = 0.75f; float stanceForwardOffset = 0f; bool rightHanded = true; bool enabledInBuild = true;`
- `OnEnable`: subscribe to the five events in §4; `OnDisable`: unsubscribe (C# Action pattern as everywhere else).
- **Placement** — `PlaceAtBall(Vector3 ball, float headingRad)`:
  `d = new Vector3(sin(heading), 0, cos(heading))` (NOTE: confirm the heading convention against how `ShotController` turns `CameraHeadingRadians` into a world direction — reuse its helper if public). Golfer forward `f = Vector3.Cross(Vector3.up, d)` (right-hander faces perpendicular to the target line with the target on his left; negate for `rightHanded = false`). `position = ball − f * stanceDistance + d * stanceForwardOffset`, `y` = ground under that point (`Physics.Raycast` down 2 m, layer = course; fall back to `ball.y`). `rotation = LookRotation(f)`. Call it on `OnShotComplete`, on hole start, and when the aim heading changes while in `Idle/Address` (poll `CameraHeadingRadians` in `LateUpdate` only while not swinging — cheap).
- `OnShotResolved` → `anim.SetBool("IsPutt", shotController.IsPutt); anim.SetTrigger("Swing")`.
- `ShotCancelled` → `SetTrigger("Cancel")`; `OnShotComplete` → `SetTrigger("Reset")` then `PlaceAtBall`.
- `OnPutterModeChanged(bool putt)` → toggle the two club GameObjects, set `IsPutt`.
- `OnTierChanged` → Low: `smr.quality = SkinQuality.Bone2`, `smr.shadowCastingMode = Off`, `anim.cullingMode = CullCompletely`; Mid/High: Bone4 / On.
- No per-frame allocations; no `Find*` calls after `Awake`.

### 5.5 Scene wiring — NO scene reference
`GameplayScene` gets **one empty GameObject `GolferTestBootstrap`** with the `GolferTestBootstrap` component and nothing else. The component's entire body is inside `#if GOLFIN_GOLFER_TEST … #endif`: on `GameSession.OnRoundStarted` (`Assets/Scripts/Gameplay/Loop/Session/GameSession.cs:222`) it `Resources.Load<GameObject>("GolferTest/PfGolfer_Test")` and instantiates it under the same parent as `TeePoint` (read how `TeePoint.prefab` is positioned per hole and place the golfer through the same path). Without the define the class is an empty MonoBehaviour — the scene holds **no reference to any `_Test` asset**, so the build pulls none of it. Menus and the physics-lab scenes are untouched either way.

### 5.6 Opt-in gate — `GOLFIN_GOLFER_TEST` (default OFF)
- Prefab lives at `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_Test.prefab` (Resources sub-folder so §5.5 can load it by name; `AnimatorController_Golfer` and `GolferPresenter` live outside `_Test` — they are the reusable part).
- `GolferPresenter.cs` and `GolferTestBootstrap.cs` bodies wrapped in `#if GOLFIN_GOLFER_TEST`. Define is **not** in `ProjectSettings` for any platform; it is added locally (Player Settings → Scripting Define Symbols) only for the test build, or via the `CIBuild` entry point when explicitly passed.
- **Build exclusion:** `Assets/Editor/GolferTestBuildGate.cs` — `IPreprocessBuildWithReport` (order after `ValidateTreeBake`) that, when `GOLFIN_GOLFER_TEST` is **absent**, renames `Assets/Art/3D/Characters/_Test` → `_Test~` (Unity ignores `~` folders) for the duration of the build and `IPostprocessBuildWithReport` restores it, `try/finally` so a failed build cannot leave it renamed. Log one line either way. NOTE: renaming under `AssetDatabase` needs `AssetDatabase.MoveAsset` + `Refresh`; if that proves flaky on the CI Mac, the fallback is a `BuildPlayerOptions.extraScriptingDefines`-driven check that **fails the build** if any `_Test/` path appears in the build report — report which one was used.
- No settings UI, no localisation (nothing player-facing is added → no `LocalizationText.csv` change).

### 5.7 Shipping it on purpose — "punch it golfer" (4th lane, same pattern as GPS / standalone)
The existing lanes differ by exactly one row of `variant_table` + one profile + one `CIBuild` entry point (`Docs/PUNCH_IT_ROUTINE.md`, `fastlane/Fastfile:44-69`, `Tools/unity-build-ios.sh:43-48`, `Assets/Editor/CIBuild.cs`). Add the fourth the same way — nothing else in the pipeline changes:

| Piece | Add |
|---|---|
| Build profile | `Assets/Settings/Build Profiles/iOS-Full-Golfer.asset` — duplicate of `iOS-Full-GPS.asset`, `m_ScriptingDefines: [GOLFIN_GPS, GOLFIN_GOLFER_TEST]`. Same bundle id / ASC record (`game`) as punch it. |
| `CIBuild.BuildIOSGolferTest()` | Mirror `BuildIOSGps()` (`CIBuild.cs:159`): `AssertProfileDefine(GolferProfilePath, GpsDefine) ?? AssertProfileDefine(GolferProfilePath, GolferDefine)` then `BuildIOSCore(GolferProfilePath, OutputPath, BuildOptions.None)`. Wrap in `try/finally` that sets `GolferTestBuildGate.IncludeTestAssets = true/false` — **profile defines never reach editor assemblies** (the exact reason `StandaloneBuildPreprocessor.ForceStandaloneIdentity` exists, `CIBuild.cs:214-247`), so the gate cannot read `#if GOLFIN_GOLFER_TEST`. Call `GolferTestBuildGate.RestoreNow()` before `Fail()` like `StandaloneBuildPreprocessor.RestoreNow()`. |
| `GolferTestBuildGate` (§5.6) | Default = **exclude**: unless `IncludeTestAssets` is true it stashes `_Test` for the build, using the same move-out/restore mechanism as `StandaloneBuildPreprocessor.MoveGolfResourcesOut()` / `RestoreGolfResources()` (reuse, don't re-implement). So a menu-bar *Build and Run*, `BuildIOS`, `BuildIOSGps`, `BuildIOSStandalone`, Android — all exclude the golfer without knowing it exists. |
| `Tools/unity-build-ios.sh` | `golfer) METHOD="Golfin.EditorTools.CIBuild.BuildIOSGolferTest" ;;` + the usage comment. |
| `fastlane/Fastfile` | `variant_table` row `golfer: { unity_arg: "golfer", record: "game", label: "Golfer test (iOS-Full-Golfer, GOLFIN_GPS;GOLFIN_GOLFER_TEST)" }` + `lane :testflight_build_golfer do testflight_build_shared(variant: :golfer) end`. |
| `Docs/PUNCH_IT_ROUTINE.md` + `Docs/TESTFLIGHT_RUNBOOK.md` | One table row: **"punch it golfer"** → `./Tools/testflight.sh testflight_build_golfer` → `iOS-Full-Golfer` → the GPS game + the stand-in golfer. Tell on device: a blue Quaternius figure beside the ball. Same ASC record as punch it / punch it GPS → **sequential with a commit between** (upload guard). |

**Editor testing:** activate `iOS-Full-Golfer` in *Build Profiles* while working — the active profile's defines apply to the Editor in Unity 6, so `#if GOLFIN_GOLFER_TEST` code compiles and Play Mode shows the golfer. Switch back to `iOS-Full` before committing; the active-profile change must not enter the diff (`ProjectSettings/EditorBuildSettings.asset`).

### 5.8 Unity MCP tool map (com.ivanmurzak.unity.mcp 0.90 + .animation) — use these, not the Inspector
| Step | Tools |
|---|---|
| §5.1 import settings on all 13 FBX | `script-execute` — one editor snippet over `ModelImporter` (`animationType = Humanoid`, per-clip `lockRootRotation` / `lockRootHeightY` / `lockRootPositionXZ`, `loopTime`, clip names) then `AssetDatabase.ImportAsset`. Verify with `object-get-data` on the avatar + `console-get-logs` for importer warnings. |
| §5.1 impact-frame times | `script-execute` sampling `AnimationMode.SampleAnimationClip` on the golfer prefab at 0.05 s steps + `screenshot-scene-view` per sample → contact sheet in `screenshots/`; the two times go in the report. |
| §5.2 controller | `animator-create` → `animator-modify` (`AddParameter`, `AddState`, `SetStateMotion`, `AddTransition`, `AddAnyStateTransition`, `SetDefaultState`) → **`animator-get-data` after every batch** (paste the output in the report). |
| §5.3 prefab | `assets-material-create` (URP Simple Lit), `assets-prefab-create` / `-open` / `-save`, `gameobject-create`, `gameobject-set-parent`, `gameobject-component-add` / `-modify`. Verify with `object-get-data`. |
| §5.5 scene | `scene-open` → `gameobject-create` + `gameobject-component-add` → `scene-save`. |
| §5.7 profile | `assets-copy` `iOS-Full-GPS.asset` → `iOS-Full-Golfer.asset`, `assets-modify` `m_ScriptingDefines`. |
| §6 acceptance | `editor-application-set-state` (Play), `screenshot-game-view`, `console-get-logs`, `profiler-start` / `profiler-capture-frame` / `profiler-get-rendering-stats` (tri count and frame time, with vs without the golfer), `tests-run` (EditMode, with and without the define). |
| NOT via MCP | `unity-build-ios.sh` runs and the §6 gate proof — batch builds cannot run inside the MCP-held Editor; use the shell with the Editor closed, as the punch-it routine does. |

## 6. Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`, PASS/FAIL + one-line evidence)
- [ ] Both FBX import as Humanoid with a fully green avatar; T-pose confirmed (screenshot of Configure).
- [ ] Every clip retargets onto the Quaternius avatar with no twisted wrists / knees; club stays in hand through Drive and Putt (video, 3 swings each).
- [ ] Golfer stands beside the ball, facing perpendicular to the aim, and turns with the camera heading; feet on the ground on a sloped lie (Lomond hole with a slope — screenshot).
- [ ] Shot commit → swing plays; cancel → idle; ball rest → golfer re-placed at the new ball position within one frame of `OnShotComplete`.
- [ ] Putter mode swaps mesh + stroke; driver mode swaps back.
- [ ] **Impact-frame times** for Drive and Putt written in the report (seconds from clip start).
- [ ] Low tier on device: golfer visible, Bone2 skinning, no shadows; frame-time delta vs. no golfer ≤ 1.0 ms (Profiler screenshot, same hole, same camera).
- [ ] Tri count of the rendered golfer + club ≤ 15k (Frame Debugger or stats window).
- [ ] `./Tools/unity-build-ios.sh golfer` produces the Xcode project with the golfer; `./Tools/unity-build-ios.sh` (no arg) and `gps` produce it without (§5.7). Fastlane lane `testflight_build_golfer` listed by `fastlane lanes`.
- [ ] **Gate proof:** a build WITHOUT `GOLFIN_GOLFER_TEST` — `Builds/*/build-report*.txt` contains zero `_Test/` paths and no `GolferPresenter` type; the scene's `GolferTestBootstrap` is an empty component. A build WITH the define shows the golfer. Both reports quoted.
- [ ] EditMode sweep green with and without the define; no Console errors.
- [ ] `_Test/` contains `LICENSE_CC0.txt` and `LICENSE_Mixamo.txt`.
- [ ] Spec deviations flagged at the bottom of the report.

## 7. Files / hierarchy this task touches
- NEW `Assets/Art/3D/Characters/_Test/**` (Cesar), `.meta` import settings (Code)
- NEW `Assets/Animations/Golfer/AnimatorController_Golfer.controller`
- NEW `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_Test.prefab`
- NEW `Assets/Scripts/Gameplay/Golfer/GolferTestBootstrap.cs`, `Assets/Editor/GolferTestBuildGate.cs`
- NEW `Assets/Settings/Build Profiles/iOS-Full-Golfer.asset`; `Assets/Editor/CIBuild.cs` (+`BuildIOSGolferTest`), `Tools/unity-build-ios.sh`, `fastlane/Fastfile`, `Docs/PUNCH_IT_ROUTINE.md`, `Docs/TESTFLIGHT_RUNBOOK.md` (one row each)
- NEW `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs`
- `Assets/Scenes/GameplayScene.unity` — one empty `GolferTestBootstrap` GameObject (no asset references)
- `Docs/AI_CONTEXT.md`, this folder's `STATUS.md` + `IMPLEMENTER_REPORT.md`

## 8. Out of scope (rows added to `Docs/GPS/GPS_BACKLOG.md`)
- Delaying ball launch to the swing's impact frame (needs the §6 numbers first).
- Per-character model selection from `Characters.csv` (`modelPrefab` column) — the real-roster spec.
- Camera changes, club trail on `ClubStart/ClubEnd`, reactions/celebrations, cloth/hair physics, facial animation, bot golfers.
- Any likeness work; this character is a stand-in.
