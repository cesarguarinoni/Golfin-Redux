# IMPLEMENTER_REPORT — `golfer_3d_test`

**Iteration shape:** `golfer_pipeline:humanoid_retarget_and_optin_gate`
**Canonical screenshot:** `screenshots/play_h08_address.png` (1170×2532, real gameplay, Hole 08)
**Canonical video:** `videos/golfer_3d_test_golfer_hole08.mp4`
**Swing animation (camera held on the golfer):** `videos/golfer_3d_test_swing_animation.mp4` — NOT
gameplay footage and not offered as such; the loop camera hides the swing, so this is the animation
itself, rendered offscreen frame-by-frame, so it can be judged at all.
**Canonical video (cont.):** (1170×2532, 35.7 s, 20.5 MB — real play,
real entry path, Unity Recorder via `BotVideoRecorder`; captions burned with `build_bot_video.py`)
**Invariant JSON (the gate):** `golfer_invariants.json` — **27 PASS / 1 FAIL**
**Impact-frame times (SPEC §5.1 / §6): Drive `t = 1.167 s`, Putt `t = 1.333 s`** (both @30 fps; details below)

Driven entirely through Unity MCP (`unity-mcp-cli` — the harness's `mcp__ai-game-developer__*` tools
were dead at session start because the Editor was not running; I launched it and drove the same live
server over the CLI. Same tools, same endpoint).

---

## 1. What was built

| File | 1-line summary |
|---|---|
| `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` | NEW. The golfer's presenter: stance beside the ball, swing on commit, idle at rest, club swap, tier response. Whole body inside `#if GOLFIN_GOLFER_TEST`. |
| `Assets/Scripts/Gameplay/Golfer/GolferTestBootstrap.cs` | NEW. Spawns `PfGolfer_Test` from Resources on `GameSession.OnRoundStarted`; the only thing the scene knows about the experiment. |
| `Assets/Scripts/Gameplay/Golfer/Golfin.Physics.Viewer.asmref` | NEW. Compiles the two files above into `Golfin.Physics.Viewer` — see § Deviation D1. No new asmdef. |
| `Assets/Editor/GolferTestBuildGate.cs` | NEW. Moves `_Test/Resources` out of the tree for every build that did not ask for the golfer; restores it four ways. Writes `Builds/golfer-gate-report.txt`. |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | NEW. The §6 acceptance harness: boots ShellScene → PLAY → `BeginGameplayLoad`, swings through `BotSwing`, writes `golfer_invariants.json`. |
| `Assets/Animations/Golfer/AnimatorController_Golfer.controller` | NEW. 5 params, 5 states, 11 transitions + 1 any-state. Read back with `animator-get-data` (§3). |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_Test.prefab` | NEW. The golfer prefab: model + 6 sockets + both clubs + presenter. |
| `Assets/Art/3D/Characters/_Test/Quaternius/Materials/M_GolferTest*.mat` ×5 | NEW. URP **Simple Lit** materials, remapped onto both FBX (body / eyes / brows, male + female). |
| `Assets/Art/3D/Characters/_Test/**/*.fbx.meta` ×13 | Import settings: Humanoid, avatar from model, per-clip root-transform bake, clip renames, loop flags. |
| `Assets/Settings/Build Profiles/iOS-Full-Golfer.asset` (+`.meta`) | NEW. Byte-identical to `iOS-Full-GPS` apart from the name and the added `GOLFIN_GOLFER_TEST` define (diffed). |
| `Assets/Editor/CIBuild.cs` | +`BuildIOSGolferTest()` mirroring `BuildIOSGps` with `IncludeTestAssets` in try/finally; standalone lane also un-stashes on exit. |
| `Tools/unity-build-ios.sh` | +`golfer)` variant case and the usage comment. |
| `fastlane/Fastfile` | +`variant_table` row `golfer:` and lane `testflight_build_golfer`. |
| `Docs/PUNCH_IT_ROUTINE.md` | +one row: **"punch it golfer"**. |
| `Docs/TESTFLIGHT_RUNBOOK.md` | +the Golfer-variant section. |
| `Assets/Scenes/GameplayScene.unity` | +one empty `GolferTestBootstrap` GameObject. 45-line purely additive diff, **zero asset references**. |
| `Docs/AI_CONTEXT.md`, `STATUS.md`, this report, `HEARTBEAT.log` | Bookkeeping. |

---

## 2. Acceptance checklist (SPEC §6)

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Both FBX import Humanoid, avatar fully green, T-pose | **PASS** | `Superhero_Male_FullBodyAvatar` / `Superhero_Female_FullBodyAvatar` both `isValid=True isHuman=True`, 52 human bones / 70 skeleton bones, `Hips→pelvis`. T-pose confirmed by render (`screenshots/diag_swing_strip.png`, arm span 1.86 m vs height 1.82 m). |
| 2 | Every clip retargets with no twisted joints; club stays in hand through Drive and Putt | **PASS** | Video: the swing at 13 s. | `screenshots/00_contact_sheet.png` — address / top / impact / follow-through for Drive, address + impact for Putt, plus Idle. All 11 clips import `isValid=True isHuman=True`, 30 fps. **This is the one that was broken; see Deviation D2.** |
| 3 | Stands beside the ball, perpendicular to the aim, turns with the heading, feet on a sloped lie | **PASS** | `golfer_invariants.json`: plan distance **0.7500 m**, angle to ball **0.0000°**, angle to aim **90.0000°**, `dot(aim, right) = −1.0000` (target on his left), root-Y minus ground-hit-Y **0.0000 m**. Heading +34.4° ⇒ forward turned **34.3775°**. Measured at address AND after a 247 m drive onto the fairway. |
| 4 | Commit → swing; cancel → idle; ball rest → re-placed | **PASS (2 of 3 measured)** | `shot.swingPlays`: `Idle → Swing_Drive` on `OnShotResolved`. `shot.golferFollowed`: golfer moved **247.2705 m** while the ball moved **247.2110 m**, landing 0.75 m from the new lie. `shot.backToIdle`: `Idle`. **Cancel is not exercised** — see § Not verified. |
| 5 | Putter mode swaps mesh + stroke; driver swaps back | **PASS** | `club.putterSwap`: driver=False putter=True, animator `IsPutt=True`. `club.driverSwapBack`: driver=True putter=False. Visible in the video at 5.8–12.8 s: a flat putter blade at his feet, then the rounded driver head. |
| 6 | **Impact-frame times for Drive and Putt** | **PASS** | **Drive `t = 1.167 s`** (frame 35 of 176, clip 5.867 s, normalized 0.199) — club head 0.103 m above the soles. **Putt `t = 1.333 s`** (frame 40 of 101, clip 3.367 s, normalized 0.396) — club head 0.009 m above the soles. Method below. |
| 7 | Low tier: Bone2, no shadows; frame delta ≤ 1.0 ms | **PASS in Editor / device pending** | `tier.low`: all three SMRs `q=Bone2 shadow=Off`, `animatorCulling=CullCompletely`. `tier.high`: `Bone4 / On`. `perf.frameDelta`: median **16.19 ms with** vs **16.42 ms without** = **−0.23 ms** (i.e. inside noise) on the same hole, same camera, one session. Editor numbers, not device. |
| 8 | Tri count of golfer + club ≤ 15k | **FAIL** | **15,632** rendered tris: `SuperHero_Male=12566 Eyebrows=984 Eyes=768` (golfer 14,318) + `ClubHead=1058 Grip=192 Shaft=64` (driver 1,314). **632 over.** Remedy below. |
| 9 | `unity-build-ios.sh golfer` produces the golfer; no-arg and `gps` do not; lane listed | **PARTIAL** | Wiring is in and syntax-checked (`ruby -c` OK, `bash -n` OK); `fastlane lanes` and the three real builds are **not run** — see § Not verified. |
| 10 | **Gate proof** from a build without the define | **PARTIAL — proven in the Editor, not from a build report** | Three independent halves. **Assets:** with `IncludeTestAssets=false` and `iOS-Full-GPS` active, `MoveTestAssetsOut()` takes the count of asset paths matching `/Resources/` under `_Test` from **2 → 0**, the stash path carries no `/Resources/` segment, and `RestoreNow()` is idempotent (§5). **Code:** after a `CleanBuildCache` recompile without the define, `GolferPresenter` and `GolferTestBootstrap` reflect as **0 fields, 0 declared methods** — empty shells (§6b). **Scene:** the `GameplayScene` diff is 45 additive lines with no asset reference at all. What is missing is the same three facts read off a real `build-report*.txt`; see § Not verified. |
| 11 | EditMode sweep green with and without the define; no Console errors | **PASS** | **655 / 655, 0 failed** in BOTH configurations (2709 discovered, `Golfin.Gameplay.Tests`), 0 console errors, 0 compile errors. The sweep **found a real regression I had introduced** — see § 6a. Without-define run taken after a `CleanBuildCache` recompile, with both golfer types verified as empty shells (§ 6b). |
| 12 | `_Test/` carries both licence files | **PASS** | `LICENSE_CC0.txt` (Quaternius) and `LICENSE_Mixamo.txt` present and untouched. |
| 13 | Deviations flagged | **PASS** | § Deviations. |

**Summary: 9 PASS, 1 FAIL, 3 PARTIAL.**

---

## 3. AnimatorController_Golfer — `animator-get-data` read-back

```
params: IsPutt(Bool) Swing(Trigger) Cancel(Trigger) Reset(Trigger) Address(Trigger)
LAYER Base Layer  weight 1  default Idle
  Idle           motion=ANIM_Idle          speed=1
       -> Address_Drive  exit=False dur=0.15 [Address If & IsPutt IfNot]
       -> Address_Putt   exit=False dur=0.15 [Address If & IsPutt If]
       -> Swing_Drive    exit=False dur=0.10 [Swing If & IsPutt IfNot]
       -> Swing_Putt     exit=False dur=0.10 [Swing If & IsPutt If]
  Address_Drive  motion=ANIM_Golf_Drive    speed=0
       -> Swing_Drive    exit=False dur=0.10 [Swing If]
       -> Idle           exit=False dur=0.20 [Cancel If]
  Address_Putt   motion=ANIM_Golf_Putt     speed=0
       -> Swing_Putt     exit=False dur=0.10 [Swing If]
       -> Idle           exit=False dur=0.20 [Cancel If]
  Swing_Drive    motion=ANIM_Golf_Drive    speed=1
       -> Idle           exit=True@0.35 dur=0.25
  Swing_Putt     motion=ANIM_Golf_Putt     speed=1
       -> Idle           exit=True@0.70 dur=0.25
  ANY -> Idle  dur=0.15  self=False  [Reset If]
```

**The state SPEC §5.2 asked me to name:** the first `ShotState` after `Idle` is **`Aiming`**
(`Assets/Scripts/Gameplay/Input/ShotState.cs:6`). `GolferPresenter.HandleShotState` triggers
`Address` on the `Idle → Aiming` edge only; everything past it (`Pulling / Timing / Flicking`) is
still the address pose.

## 4. Impact-frame method

Measured on the club **as socketed in the shipping prefab**, not on an idealised skeleton. The
Animator is stepped one frame at a time through `Swing_Drive` / `Swing_Putt` (a fresh prefab
instance per frame — reusing one Animator across `Play` calls silently returns the same pose, which
cost me a whole bad take). At each frame the club-head world point is
`ClubSlot.TransformPoint(0, clubLength, 0)` and the reference is the sole plane
(`min(ankle.y) − 0.0865`, the T-pose ankle height). Impact is the minimum of that curve on the
**downswing** — the first descent after the club leaves address — not the global minimum, which is
address itself.

```
Drive  t=1.000 y=2.30   1.033 2.13   1.067 1.83   1.100 1.36   1.133 0.66   1.167 0.10 <= IMPACT   1.200 0.16   1.233 0.65
Putt   t=1.200 y=0.061  1.233 0.041  1.267 0.022  1.300 0.010  1.333 0.009 <= IMPACT  1.367 0.017  1.400 0.037
```

Tables: `Docs/Diagnostics/_capture/` (regenerable); raw at `/tmp/golfimpact2_{Drive,Putt}.tsv`.

## 5. Build-gate transcript (SPEC §6 item 10)

```
active profile = iOS-Full-Golfer
IsGolferBuild() with iOS-Full-Golfer active = True
IsGolferBuild() with iOS-Full-GPS active    = False
BEFORE   paths under a Resources/ folder that live in _Test: 2
    Assets/Art/3D/Characters/_Test/Resources/GolferTest
    Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_Test.prefab
STASHED  _Test exists=True  Resources subfolder exists=False  stash=True
         paths under a Resources/ folder that live in _Test: 0   <-- the gate
         stash contents: 4  (no /Resources/ segment — correct)
RESTORED Resources subfolder exists=True  stashRoot=False  paths restored: 2  prefab loads: True
second RestoreNow (idempotent): _Test exists=True
```

**The first version of the gate was wrong and this check caught it.** SPEC §5.6 says to move
`_Test` aside; `_Test` *contains* `Resources/`, so moving the whole folder to
`Assets/_GolferTestStash/_Test/Resources/…` leaves a `/Resources/` segment in the path and Unity
still ships it — `Resources.Load("GolferTest/PfGolfer_Test")` still resolved after the move. The gate
now moves the inner `Resources` folder to a destination that has no such segment, which is what
actually removes it. (SPEC's `_Test~` rename would also work but needs `Directory.Move` and a full
FBX re-import on every restore instead of a GUID-preserving `MoveAsset`.)

---

## 6a. A regression the EditMode sweep caught (and I fixed)

The first sweep failed **3 tests** — `GameSessionTests.{SeedSession_SetsAllThreeFields,
ResetSession_ClearsAllSeedFields, SetCurrentHole_UpdatesPointerWithoutClearingSeed}` — all with
`Destroy may not be called from edit mode!`.

Cause: `GameSession` is a **static** class, so `GolferTestBootstrap`'s
`OnRoundStarted` subscription outlived the play session that made it, and those three tests raise
`OnRoundStarted` from EDIT mode — straight into `Destroy(_golfer)` / `Instantiate(prefab)`.

Fix: `SpawnGolfer` now returns immediately unless `Application.isPlaying`, and `Boot()` re-arms
`_installed`/`_golfer` and unsubscribes before subscribing (the pattern
`QualityTierService.Boot` already uses, and for the same reason — statics survive a play session
when domain reload is off). After the fix: **655 / 655, 0 failed.**

This is a defect that would have shipped invisibly: nothing in gameplay would have shown it, and
it only surfaced because the sweep ran the whole assembly rather than my own code.

## 6b. Compiled-out proof

Reflected over the loaded assemblies after a `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`
with the active profile back on `iOS-Full-GPS`:

```
Golfin.Gameplay.Golfer.GolferPresenter    : asm=Golfin.Physics.Viewer fields=0 declaredMethods=0 -> EMPTY SHELL
Golfin.Gameplay.Golfer.GolferTestBootstrap: asm=Golfin.Physics.Viewer fields=0 declaredMethods=0 -> EMPTY SHELL
```

With the define on, the same probe reports 21 fields / 17 methods and 2 fields / 4 methods. Note
that a profile switch alone does NOT re-evaluate the define — Unity reuses the cached assemblies,
and `EditorUtility.RequestScriptReload()` is not enough either. Only a clean recompile flips it.

## 7. Deviations from SPEC

| # | Spec said | What I did, and why |
|---|---|---|
| **D1** | §4: new code in **Assembly-CSharp**, "it references the above already" | It does not. `Golfin.Gameplay.Input` is `autoReferenced: false`, so no predefined assembly may even name `ShotController` (`PuttPathPredictor.cs:3` documents the same wall), and `PhysicsLabController.BallSM` / `.ShotController` are `internal` to `Golfin.Physics.Viewer` with no static accessor. I added a 3-line **`.asmref`** — not a new asmdef — that compiles the two golfer files into `Golfin.Physics.Viewer`, which already references Input/Loop/UI and is where every sibling shot-driven presenter lives. SPEC §7's file paths are unchanged and no file under `Assets/Scripts/Physics/` was edited. |
| **D2** | §5.1: model import with **Bake Axis Conversion ✔** | **This corrupts a Humanoid avatar and was the single biggest defect in the task.** With it on, `HumanPoseHandler.GetHumanPose` on the bind pose returns `bodyRotation = (73.8°, 0, 0)` and muscle values up to **6.67** (valid range ±1); every retargeted clip rendered as a contorted figure. With it off: `bodyRotation = (355.6°, 180°, 0)`, max muscle **1.83** (thumbs), and the swing renders correctly. Set to **false** on both models. |
| **D3** | §5.4: `d = (sin θ, 0, cos θ)` | Used **`d = (cos θ, 0, sin θ)`** — the convention `ShotInputState` carries and `PutterAimLine.cs:317-319` states in a comment. The spec form is 90° out and would stand the golfer on the target line. SPEC flagged this as needing confirmation; confirmed. |
| **D4** | §5.1: Root Transform Position (Y) → **Based Upon Original** | Used **Based Upon Feet**. "Original" bakes the Y Bot's hip height onto a differently-proportioned avatar; measured, it floated the golfer ~0.19 m (Idle ankle at 0.192 m instead of 0.086 m). With Feet the ankle sits at 0.084–0.094 m, i.e. soles on the ground — and the in-game invariant now reads **0.0000 m** root-to-ground. |
| **D5** | §5.2: `Address` state plays **TeeUp / PrePutt** | Those clips are not address holds. `ANIM_Golf_TeeUp` is *bend down and place a tee* (the golfer folds double); `ANIM_Golf_DriveSetup` (11.8 s) and `ANIM_Golf_PrePutt` (12.8 s) are pre-shot routines with walking, crouching and pointing — rendered and inspected, see `screenshots/diag_swing_strip.png` history. The address pose that *does* exist is frame 0 of the swing clips themselves, so `Address_Drive` / `Address_Putt` play `ANIM_Golf_Drive` / `ANIM_Golf_Putt` at **speed 0**. A held pose, no idle sway. Mixamo has 5 more Drive variants if a real address-hold is wanted. |
| **D6** | §5.2: 4 animator parameters | Added a 5th, **`Address` (trigger)**. The described machine has an `Address` state and a `Cancel` out of it but nothing to enter it. |
| **D7** | §5.3: Animator on the prefab **root**, model as a child | Kept exactly that — and verified it is safe: with the Animator on a wrapper parent the retarget produces bone positions identical to the FBX-root layout (`hand_r` rel `(0.072, 1.460, −0.282)` both ways). |
| **D8** | §5.3 / §4: copy the two `UnplayableChecker` children, drop the unresolved component | Done — **and it is worse than SPEC thought.** All FOUR script GUIDs in `PfYoungMale.prefab` are unresolved (`41412eff…`, `11412ec1…`, `318caf10…`, `ed287df6…`) **and so is its source model prefab** `9edef746…`, so `LoadAssetAtPath` returns null for the whole prefab. I parsed the YAML directly for the six socket local poses, which are reproduced verbatim in the prefab. In `PfYoungMale` the two `GameplayIdle*ClubSlot`s hang off a *different* (unresolvable) bone than the other four; SPEC §5.3 puts all six under `RightHand`, so that is where they are. |
| **D9** | §5.5: place the golfer through the path that positions `TeePoint.prefab` | That path does not exist. `TeePoint.prefab`'s GUID (`969311bd…`) appears in **no** scene, prefab or script, and no `.cs` file mentions TeePoint at all. The golfer spawns unparented into the active scene and is positioned by `PlaceAtBall` from the live ball — one answer for every hole, no per-hole authoring. |
| **D10** | §5.5: the component lives in `GameplayScene` | It does. But **`GameplayScene` is not in `EditorBuildSettings`** and holds only a camera, a light and an empty root — the gameplay loop actually runs in `Physics/LabScaffold.unity`. A component there would never run, so `GolferTestBootstrap` also installs from `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`; both paths call one idempotent installer. The scene GameObject is kept because SPEC §5.5 and the §6 gate proof both name it, and it costs a 45-line reference-free diff. |
| **D11** | §5.6: rename `_Test` → `_Test~` | See § 5 — moving `_Test` wholesale does not exclude anything. The gate moves `_Test/Resources` instead, via `AssetDatabase.MoveAsset`, and hooks `BuildPlayerProcessor.PrepareForBuild` rather than `OnPreprocessBuild` (which runs after asset collection — `StandaloneBuildPreprocessor`'s own comment says so). |
| **D12** | R4: 1 material | 3 on the golfer (`M_GolferTest` body, `_Eyes`, `_Brows`) — the eyes and eyebrows need their own textures and a stand-in does not justify an atlas pass. Flagged with the tri count below. |

## 8. Not verified — needs a run I did not make

- **`shot.cancel`.** `ShotController.ShotCancelled` → `Cancel` trigger is wired and compiles, but the
  bot harness commits every swing it starts, so no take exercised the cancel edge. **Manual: arm a
  shot and back out; the golfer should return to standing.**
- **The three real iOS builds** (`golfer` / no-arg / `gps`) and `fastlane lanes`. A batchmode build
  needs the Editor CLOSED, which would have ended this session's MCP access mid-task, and each build
  is 20–45 min. The lane wiring is syntax-checked but **unbuilt**; the gate is proven statically (§5)
  rather than from a `build-report*.txt`. Every build now writes `Builds/golfer-gate-report.txt`
  with the decision and the count of `_Test` paths in the report, so the proof falls out of the
  first real build without any extra work.
- **Everything on device.** Low-tier appearance, the ≤1.0 ms budget, and thermal behaviour are
  device numbers; the Editor delta (−0.23 ms) says only that nothing is grossly wrong.

## 8a-----. Round 7 — free mocap sources that were captured WITH a club

The blocker is wrist orientation, so what matters is whether the actor held a real club. Searched
for free sets that do:

| Source | Golf content | Licence | Verdict |
|---|---|---|---|
| **CMU Graphics Lab** (subject 64, plus 63_01) | **30 trials**: 10 Swing, 5 Putt, 5 Placing Tee, 5 Placing Ball, 5 Picking up Ball | "free for use in research and commercial projects worldwide… you may include this data in commercially-sold products, but you may not resell this data directly, even in converted form" | **Best candidate** |
| Bandai Namco Research (3,000 moves) | no golf found | CC BY-NC / BY-NC-ND — **non-commercial** | Ruled out |
| Mixamo (current) | 34 golf clips | commercial OK | Wrists not on a club — the defect |
| Sketchfab CC0 one-offs, Truebones free | single swings | mixed / per-asset | Unverified, no putt/idle set |

**Why CMU is the right shape for this problem, and not just "another set":**

1. **The actor demonstrably had props.** You cannot record *Placing Tee*, *Placing Ball* and
   *Picking up Ball* without a tee, a ball and a club. So the wrists in those swings are rolled onto
   a real club — which is precisely the 58-69 deg that Mixamo is missing.
2. **CMU captures NO finger data.** Its "finger" and "thumb" joints are placeholders and the docs
   say the system does not capture them and the data should be ignored. That sounds like a downside
   and is the opposite here: the authored grip pose I already built has nothing to fight. The exact
   pairing that failed on Mixamo — empty-hand finger animation over un-rolled wrists — cannot occur.
3. **The licence permits shipping it.** We embed it in a product; we do not resell the data.
4. It also supplies the two clips this test is currently faking: a real **Putt** (5 takes) and
   **Placing Tee / Picking up Ball**, which are the natural Address and between-shot idles.

Cost: BVH cleanup and retarget onto the Quaternius avatar — SPEC §1 dismissed CMU as "BVH, needs
cleanup", which was right when Mixamo looked usable and is the wrong trade now that it is not. No
money, and it removes the $35 Motion Cast dependency.

**Recommendation: retarget CMU subject 64 and keep the authored finger pose on top.** Fall back to
Motion Cast #05 only if the CMU swings retarget badly.

Sources: [CMU Graphics Lab](https://mocap.cs.cmu.edu/),
[CMU motion list (cgspeed BVH conversion)](https://sites.google.com/a/cgspeed.com/cgspeed/motion-capture/the-motionbuilder-friendly-bvh-conversion-release-of-cmus-motion-capture-database/bvh-conversion-release-motions-list),
[CMU skeleton — fingers not captured](https://github.com/una-dinosauria/cmu-mocap/blob/master/READMEFIRST.txt),
[Bandai Namco licence](https://github.com/BandaiNamcoResearchInc/Bandai-Namco-Research-Motiondataset).

## 8a----. Round 6 — I tried option 1 and it does not work on this data

Cesar chose "author a grip hand pose". I built it — a solver that lays the shaft across the finger
bases and closes each finger only as far as it needs to meet the shaft — and it failed for a reason
worth writing down, because it rules the whole approach out rather than needing another pass.

**A grip has two halves: the fingers wrap, AND the wrist is rolled so the knuckle line lies along
the shaft.** The second half is the one no finger pose can fake. Measured, at address:

```
lead  wrist: knuckle line is 58.4 deg away from the shaft the club needs
trail wrist: knuckle line is 69.4 deg away
```

With the wrists as the mocap leaves them, the shaft crosses the knuckle line instead of lying along
it, so the fingers at the ends of that line cannot reach the bar at all — index and pinky stayed
0.03-0.06 m short at any curl, while the middle finger sat on it. Rolling the wrists to fix that is
not available either:

- **The trail wrist cannot be rolled at all** — the club is parented to `hand_r`, so rolling the
  wrist rolls the club with it and the shaft direction just follows the hand.
- **Rolling the lead wrist 58 deg** swings the hand off the grip: its fingers went from
  0.017-0.035 m short to 0.05-0.12 m short.
- **Uncapping the curl** so the far fingers keep closing produces hyperextended claws, which looks
  worse than not trying.

So the conflict is in the wrist orientation the clips carry, not in the fingers, and a static hand
pose layered on top cannot resolve it. Reverted to the best achievable state: hands joined on the
grip in the right stations (lead 0.045 m below the butt cap, trail 0.148, separation 0.103 vs one
hand width 0.117), fingers closed, thumbs down the shaft, trail fist exactly on the shaft line.

**What is actually left:**

1. **Golf clips mocapped WITH a club** (SPEC §1's named fallback, Motion Cast #05, $35). The wrists
   arrive already rolled onto a club, so the grip solves itself and the swing improves too. This is
   now my recommendation.
2. **A full arm+hand IK pass** — re-solve both arms per frame so the hands meet an authored grip on
   the club. Real animation-programming work, and it fights the mocap every frame.
3. **Ship the stand-in.** Reads as holding at gameplay distance; wrong close up.

I recommended (1)-as-hand-pose last round and was wrong: I scoped it as a fingers problem when the
measurement I had not taken yet — wrist-to-shaft angle — says it is a wrists problem.

## 8a---. Round 5 — "that is not a real golf club grip". It isn't, and here is why it can't be.

I looked up how a club is actually held instead of continuing to guess. A real grip is:
the club across the **fingers**, not the palm; the **lead thumb down the top of the grip** with the
**trail palm covering it**; the hands **joined** — trail pinky overlapping (Vardon) or interlocking
the lead index; lead hand at the top of the grip, trail immediately below and touching.
Sources: [Golf Distillery](https://www.golfdistillery.com/tweaks/setup/grip/grip-type/),
[Golf Monthly](https://www.golfmonthly.com/tips/8-ways-to-get-the-perfect-golf-grip-179088),
[Foresight Sports](https://www.foresightsports.com/blogs/golf-tips/the-3-common-grips-in-golf-with-pros-and-cons-for-each).

Measured against that, two things were wrong and are now fixed:

| | before | after | target |
|---|---|---|---|
| lead fist below the butt cap | **−0.0057 m** (off the end of the club) | 0.0371 m | ~0.03 |
| trail fist below the butt cap | 0.050 m | 0.1477 m | ~0.15 |
| fist separation along the shaft | 0.058 m | 0.110 m | 0.117 (one hand width) |
| lead thumb | curled into a fist | extended down the shaft | down the shaft |

**It is still not a real grip, and no amount of further socket maths will make it one.** What is
missing is inside the hand: the fingers close uniformly into a fist instead of wrapping diagonally
across the shaft, the trail palm does not cover the lead thumb, and nothing overlaps or interlocks
the pinky and index. Those are properties of a HAND POSE AUTHORED FOR A CLUB. These clips are
Mixamo mocap performed with an empty hand — measured at the top of this section, the source pose is
an open hand in Idle (curl 0.096) — so there is no correct grip anywhere in the data to copy.

**This is the decision SPEC §1 anticipated.** It named the paid *Golf animations (Motion Cast #05,
$35)* set as "the paid fallback **if** Mixamo's golf clips turn out unusable". For everything else
they are fine — the swing, the stance and the ball contact all check out. For the grip they are
not, and the options are:

1. **Author a grip hand pose** (art/rigging task, ~half a day): one posed pair of hands wrapped on
   a club, baked and applied the way the current finger pose already is. Fixes it for every clip
   and every future character. Cheapest correct answer.
2. **Buy clips mocapped WITH a club** — Motion Cast #05 or similar. Fixes the grip and probably
   improves the swing; costs $35 and a re-import.
3. **Ship the stand-in as-is.** The hands read as "holding" at a distance but not close up.

My recommendation is (1): the pose is reusable, it is the thing the real characters will need
anyway, and it does not depend on which animation set wins.

## 8a--. Round 4 — "not gripping the club properly, and even less so while idle"

Measured first this time. Finger curl, tip-to-knuckle (straight ~0.09 m, closed fist ~0.04):

```
address  R middle 0.0412   impact 0.0440      <- the swing frames DO grip
idle     R middle 0.0961   R index 0.0840     <- open hand; the club hung through a flat palm
```

Exactly the complaint, including "even less so while idle". These clips are mocap performed
WITHOUT a club, so the finger animation is whatever the actor's hands were doing.

**Two tidy fixes were tried and BOTH are dead ends — measured, not assumed.** An Animator layer
with an avatar mask needs a clip Unity considers humanoid; a clip built in-editor from muscle
curves comes back `humanMotion == false`, so the layer sat at weight 1 and changed nothing (curl
0.0961 before and after). `HumanPoseHandler.SetHumanPose` with the finger muscles written does
nothing either — set "Right Middle 2 Stretched" to −0.9 and the curl stays 0.0961.

**What works: bake the finger bone rotations from the address frame and re-apply them after the
Animator has evaluated.** Idle now goes 0.0961 → **0.0412**; address and impact hold at 0.0412.

**And a second, separate defect the close-up exposed: the club was not INSIDE the fists.** The
socket aimed the shaft at the wrist/knuckle, so with the fingers now closed the hands were balled
up beside the club. Re-anchored on the FIST CENTRE (the hole the curled fingers make, averaged
over the second knuckles and the thumb tip): right fist to shaft-line **0.0000 m**, and the driver
still reaches the ball at scale 1.000.

Threading BOTH fists onto the shaft is geometrically impossible here — the fist-to-fist axis is
nearly horizontal, so a club on it floats its head **0.41 m** above the turf. The left hand is
brought on with a one-bone aim that swings the left forearm about the elbow: address
**0.057 → 0.017 m**, impact **0.085 → 0.010 m**. In Idle it correctly declines (>45° would mangle
the arm), so he carries the club one-handed with the left arm hanging — which is what a golfer
does.

Guarded by `grip.rightFistOnShaft` (< 0.03 m) and `grip.fingersClosed` (< 0.055 m) in the
invariant JSON. **28 PASS / 1 FAIL.**

## 8a-. Round 3 — "wrong side, swing never shown, club still not held"

**THE root error, under all of it: I measured the swing direction wrong.** I derived it from the
club head's ADDRESS-to-IMPACT difference — a near-zero vector, dominated by noise, pointing local
-Z — and built the placement on it. The truth is the club-head velocity AT impact: **26.75 m/s
along local (0.05, 0, 0.999)**, i.e. local **+Z**, agreeing with the grip (left hand 0.048 m
nearer the butt than the right = right-handed). So the golfer was rotated 180 deg and stood on the
wrong side of the ball. `LookRotation(-d)` became `LookRotation(d)`, and the harness assertion that
had been written against the same wrong basis was corrected with it.

Verified as a picture rather than an argument for the first time: `screenshots/diag_topdown.png`
and `diag_behind_ball.png` place the golfer, the ball and the target line in one frame. Golfer LEFT
of the line, club head **0.0005 m** from the ball, flag straight ahead. In game: club head
**0.0000 m** from the ball and swing **0.0000 deg** off the aim, at the tee and at the new lie.

**The swing is now visible in the clip.** The loop camera still cuts to the ball at commit — that
has not changed and is not mine to change — so the capture harness spawns a SECOND camera at a
higher depth for the swing window and destroys it after. Additive, reverted, gameplay untouched.
It makes the swing filmable; it does not make it visible to a player. **Shipping that is a camera
change and needs Cesar's decision (SPEC §8).**

**Three rounds of review, and each round my own numbers passed.** `facesBall` 0.0000 deg and
`perpendicularToAim` 90.0000 deg were both satisfiable by a stance 90 deg wrong; `swingsDownTheAim`
was satisfiable by one 180 deg wrong. An assertion derived from the same mistaken basis as the code
cannot catch that basis being wrong. The assertion that finally held — club head distance to the
ball — is the one phrased in terms of the THING (does the club reach what he hits), not in terms of
my model of it.

## 8a0. Round 2 — "video still skips shot, hands are not holding the club"

Four more defects, all real, all mine. The first two rounds of fixes were correct and insufficient.

**4 — the golfer was addressing empty grass, a metre from the ball.** In the address pose the club
head sits at local **(0.735, 0, -0.069)** — 0.74 m out to his SIDE — while `PlaceAtBall` rotated him
with `LookRotation(f)` so the ball sat on his local **+Z**. Ball and club head ended up ~1.05 m
apart, 90 deg around. The old invariants (`facesBall` 0.0000 deg, `perpendicularToAim` 90.0000 deg)
both PASSED the whole time, because angles were the wrong question. Placement is now derived FROM
the pose — `pos = ball - rot * AddressHeadLocal` — so the club head lands on the ball by
construction, and the assertion is the one that matters: **club head 0.0000 m from the ball**, at the
tee and again at the new lie after a 247 m drive.

Measured stance basis, which confirms the pose itself is textbook golf: shoulder line vs swing
direction **173.9 deg** (parallel), chest vs swing **96.1 deg** (perpendicular), chest vs ball
**9.3 deg** (he faces it).

**5 — he never adopted the address posture.** `Address` fired only on `ShotState.Aiming`, which a
swing can cross in a frame or two, so what shipped was the IDLE pose with a club stretched out to the
ball — which is what "hands are not holding the club" actually looked like. Address now covers every
non-idle shot state.

**6 — and widening it alone made it worse.** `OnStateChanged` publishes every frame, so firing
`Cancel` unconditionally on idle frames left one permanently pending: the golfer reached Address and
was yanked straight back out by the Cancel queued the frame before, rendering as Idle for the whole
shot. Both triggers are now edge-triggered off a tracked `_lastShotState`. New assertion
`shot.addressBeforeSwing` samples the animator during the setup and requires an `Address` state.

**7 — the swing is invisible in normal play, and that is the product, not the recording.** Sampling
during flight: `vis=False` at the FIRST sample after commit with the swing at normalized **0.07** —
impact is at **0.199**. The loop camera cuts to the ball the instant the shot commits, so the entire
swing, every time, happens after the golfer has left the frame. Suspending culling (defect 1) was
necessary and does not change this. **Making the swing visible is a camera change, which SPEC §8 puts
out of scope — this needs Cesar's call.** Until then the golfer reads only during aim and at rest.

## 8a. Two defects Cesar caught on the first video, and a third they uncovered

**1 — the swing was gutted by the camera cut.** `ApplyTier` set
`AnimatorCullingMode.CullUpdateTransforms` (Mid/High), and the moment the shot commits the game cuts
to its flight framing and the golfer leaves the frustum — under that mode transform writes stop, so
the swing froze at the cut and the club never reached the ball. Low's `CullCompletely` is worse: the
state machine itself halts, so `Swing_Drive` never reaches its exit time. The presenter now suspends
culling for the duration of a swing and restores the tier's setting after. Proof, sampled during
flight with the golfer off-screen:

```
Swing_Drive@0.07 vis=False cull=AlwaysAnimate      <- would have frozen here before
Swing_Drive@0.16 vis=False cull=AlwaysAnimate
Swing_Drive@0.30 vis=False cull=AlwaysAnimate
Swing_Drive@0.35 vis=False cull=AlwaysAnimate
Idle@0.03        vis=False cull=AlwaysAnimate      <- ran to exit time, all off-camera
```

**2 — the model was not holding the club.** The socket solve put the club's origin at the
MIDPOINT OF THE TWO HANDS, which is displaced *across* the grip from `hand_r` — the bone it is
actually parented to. At address that reads fine because both hands are together; in Idle the left
arm is 0.51 m away and the club dangled 0.104 m off an open right hand. Re-solved so the SHAFT LINE
runs through the right fist: distance from the fist to the shaft line is now **0.0000 m**, hand_r
**0.038 m**, and the driver still reaches the ground at a realistic **69.7°** lie with **no** scaling
(it needed 1.109× before the butt offset was trimmed to 0.05 m). Screens: `screenshots/00_grip_zoom.png`.

Honest limit: the left hand sits **0.166 m** off the shaft line. The Y Bot clips are mocap performed
without a prop — the wrist-to-wrist vector is near-horizontal even at impact — so no single rigid
socket can put both hands on the shaft. The right-hand grip is the one that holds in every pose.

**3 — the one they did not ask about, found while fixing 2: the prefab's serialized references were
all NULL.** `GolferPresenter`'s body is inside `#if GOLFIN_GOLFER_TEST`, so a compile WITHOUT the
define makes the class fieldless and Unity drops the serialized data it can no longer map — and
re-enabling the define does not bring it back. I did exactly that recompile to prove the gate
(§6b), which silently un-wired the prefab. `anim` and `skins` recovered through existing Awake
fallbacks; `driverSocketRoot` / `putterSocketRoot` had none, so putter mode stopped swapping the
mesh and nothing said so. Fixed twice over: the prefab is re-wired, AND `Awake` now resolves both
club roots by name (the names SPEC §5.3 already fixes as the contract) and warns if it cannot.
**Anything that must survive a define-off compile cannot live only in serialized data.**

## 8a1. The video

`videos/golfer_3d_test_golfer_hole08.mp4` — one take, real play: address on Hole 08, turn with the
aim heading, the club held through idle and swing, shot commit plays the swing, ball away, and he
re-plants 247 m up the fairway at Turn 2. Recorded through the sanctioned `BotVideoRecorder`
(`CustomOutputPath` + `ArmDeferred`/`BeginDeferred`) at the full iPhone-14 1170×2532 device preset,
deferred so the clip starts after the hole is stable. Orientation checked on **consecutive** decoded
frames (120–123), not keyframe samples: no Y-flip.

**There is no putter beat in the clip, deliberately.** Putter mode 450 yd from the pin is a state
the game actively refuses: §2f's surface auto-switch owns the club at a tee, and
`ClubSelectionBroadcast.SetPutterMode` early-outs when the flag has not changed — so asking for it
is overruled within a frame and the mesh never swaps. Forcing it by also writing
`ShotController.IsPutt` does swap the mesh, and drags the lab camera into its aerial putt framing:
that was take 1, eight seconds of empty fairway under a caption about the driver. Neither version is
worth filming at a tee, so the swap is stated where it can be stated exactly instead of implied —
`golfer_invariants.json` (`club.putterSwap` / `club.driverSwapBack`) and the prefab render sheet.
Five takes; the earlier four are not kept.

## 9. Known defects

1. **Tri budget: 15,632 vs the 15,000 limit (+4.2%).** Golfer 14,318 + driver 1,314. Dropping the
   `Eyebrows` (984) and `Eyes` (768) renderers takes it to **13,880**; that is a look decision, not
   a code one, so I left the mesh alone and am reporting the number. The real character will be
   authored to budget — this is a CC0 stand-in that happens to be 12.5k on the body alone.
2. **The golfer stands upright rather than addressing in the shipped capture.** `Address` fires on
   `ShotState.Aiming`; the bot's flick is over in a few frames so the takes catch `Idle`. A human
   aiming for seconds will see the address pose. Worth an eye on the first device build.
3. **The putter socket carries a 1.171 scale.** The Y Bot's putt address holds the hands 0.878 m
   above the soles and `GOLFIN_Putter.fbx` is 0.75 m grip-to-head, so the head cannot reach the
   ground at 1:1. Scaling the socket was the least-bad of a floating putter head, a longer putter
   mesh, or a different putt clip. Recorded here so the real spec can size the mesh instead.
