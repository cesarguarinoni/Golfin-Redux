# SPEC — `golfer_club_grip`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports go in `IMPLEMENTER_REPORT.md`.

## Status
See `STATUS.md`. `SPEC_READY` (2026-09-07). Depends on nothing open; `golfer_3d_test` §9.8 is done (`d3deb518d`).

> **EXPERIMENT LANE — still opt-in.** Everything here lives under `GOLFIN_GOLFER_TEST` and the `_Test` asset gate exactly like `golfer_3d_test` §5.6. Nothing reaches a normal build. The one exception is the Animation Rigging package itself (§3.1), which is in `Packages/manifest.json` for every build — accepted, see §3.1.

## Goal
Make the club sit in the golfer's hands on the **Mixamo-native** pipeline that §9.8 selected, without touching the clips and without any per-clip hand tuning: the club is parented to a **grip target** derived from both hands, and **Animation Rigging two-bone IK** pulls each hand onto an anchor on the shaft. The result is the club-mount template every roster model (`Docs/Design/CHARACTER_3D_REMAKE_OPTIONS.md` §7) inherits unchanged.

What it fixes: on `PfGolfer_MixamoNative` the club is parented to the right hand, so its world orientation follows the clip's wrist roll — at address the shaft points at the camera (`golfer_3d_test/evidence/9_8/sbs_address.jpg`, right panel), and through the swing the lead hand is nowhere near the shaft. `golfer_3d_test` §9.3's finger solver is **not** revived (it was written against Quaternius bone names and the retargeting artefact it was chasing no longer exists).

## 1. Why this shape (Architect, 2026-09-07)
- The clips are empty-hand mocap; they carry two hands *near* each other but no shaft. Anything parented to one hand inherits that hand's roll. Deriving the club from **both** hands (position and rotation averaged) removes the single-hand roll and puts the shaft on the line the hands already make.
- IK closes the residual the other way round: instead of moving the club to the hands, the hands move the last few centimetres to the club. Two-bone IK on the arm cannot touch the legs, so the §9.8 foot-slide numbers must not move (§6 checks this).
- Animation Rigging evaluates inside the Animator graph, after the clip and before `LateUpdate`, and rig layers evaluate in order — so layer 1 (grip target from the clip's hands) and layer 2 (hands onto the club) are one pass with no feedback and no script in the hot path.
- Fingers stay as the clip has them (loosely closed). An authored grip hand pose is a separate, later item (§8).

## 2. Assets
None new. The club meshes are the existing `GOLFIN_Driver` / `GOLFIN_Putter` already instanced in the prefab.

## 3. Implementation

### 3.1 Package
Add `com.unity.animation.rigging` to `Packages/manifest.json` (Unity 6000.3.9f1 — take the version the Package Manager marks as released for this editor; **NOTE:** 1.3.x/1.4.x, confirm in the Package Manager rather than guessing). It ships in every build (~100 KB of managed code, no scenes reference it with the define off); accepted by the Architect. Do not add it to any assembly definition outside `Golfin.Gameplay.Golfer`'s asmdef (if that asmdef exists — else the default assembly, as `GolferPresenter` is today).

### 3.2 Prefab — `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab`
Restructure the club so it is **not under a bone**:

```
PfGolfer_MixamoNative            (Animator · GolferPresenter · RigBuilder)
├ mixamorig:Hips …               (skeleton, untouched)
├ ClubRoot                       NEW — top-level, identity transform
│  └ GripTarget                  NEW — the constrained object (§3.3)
│     ├ ClubSlot  ▸ GOLFIN_Driver    MOVED here from the hand; local pose = the authored grip offset (§3.4)
│     ├ PutterSlot ▸ GOLFIN_Putter   MOVED here; its own local pose
│     ├ GripAnchor_Lead          NEW — empty ON the shaft, 0.03 m below the butt cap (shaft axis = ClubSlot local +Y per GolferPresenter.JoinLeadHandToShaft)
│     ├ GripAnchor_Trail         NEW — empty ON the shaft, 0.11 m below the butt cap
│     └ ClubStart / ClubEnd      as before (grip / head empties), now children of the club, not of the hand
└ GolferRig                      NEW — Animation Rigging `Rig`
   ├ Rig_Grip                    `Rig`, weight 1 — layer 1
   │  └ GripTarget_Constraint    `MultiParentConstraint` — constrained = GripTarget; sources = mixamorig:LeftHand (0.5), mixamorig:RightHand (0.5); position+rotation; Maintain Offset OFF
   └ Rig_Hands                   `Rig`, weight 1 — layer 2
      ├ IK_Lead                  `TwoBoneIKConstraint` — root mixamorig:LeftArm, mid mixamorig:LeftForeArm, tip mixamorig:LeftHand; target GripAnchor_Lead; hint none; weight 1
      └ IK_Trail                 `TwoBoneIKConstraint` — root mixamorig:RightArm, mid mixamorig:RightForeArm, tip mixamorig:RightHand; target GripAnchor_Trail; hint none; weight 1
```
`RigBuilder.layers` = [Rig_Grip, Rig_Hands] in that order. Lead = left hand, trail = right (the prefab is right-handed; §8 for the mirror). `GameplayIdleClubSlot` / `GameplayIdlePuttClubSlot`, if present on this prefab, move under `GripTarget` as well and keep their names (R5 socket contract in `CHARACTER_3D_REMAKE_OPTIONS.md` §2).

**`PfGolfer_Test` (Quaternius) is not touched.** It is the dead branch; leave it exactly as §9 left it.

### 3.3 Why MultiParent with Maintain Offset OFF
Maintain Offset captures the constrained→source offset at rig build, i.e. in the prefab's T-pose, which is meaningless for a grip. With it OFF, `GripTarget` *is* the average of the two hand frames every frame, and the grip is authored once as the club's **local** pose under it (§3.4). Nothing is captured at runtime; the prefab is the whole truth.

### 3.4 Authoring the grip offset — once, by measurement, not by eye
1. Enter play mode on Hole 06 with the harness (`GOLFIN > Golfer Test > Verify Mixamo-native on Hole 06 (§9.8)` pauses at address — or use `EditorApplication.isPaused` after `shot.addressAtRest`).
2. With `Rig_Hands` weight temporarily 0, set `ClubSlot`'s local position/rotation under `GripTarget` so that (a) `GripAnchor_Lead` sits inside the left palm and `GripAnchor_Trail` inside the right palm, and (b) `ClubEnd` is on the ground at the ball. Read the world numbers off the Inspector; copy the local pose back into the prefab (play-mode edits are lost).
3. Repeat for `PutterSlot` on the putt address (`ClubSelectionBroadcast.OnPutterModeChanged` swap as today).
4. Restore `Rig_Hands` weight 1. Record both local poses in the report §"Findings".
Two numbers, not seven rounds. If (a) and (b) cannot both hold within the §6 tolerances the clip's hands are somewhere the golfer's real hands could not be — report it with the frame and stop; do not bend the golfer to fit.

### 3.5 `GolferPresenter` (`Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs`)
- `Awake` socket resolution by name (`FindChild("GOLFIN_Driver")` / `"GOLFIN_Putter"`) already survives the move — verify, do not rewrite.
- `HandlePutterMode` (`SetActive` on `driverSocketRoot` / `putterSocketRoot`) is unchanged and still correct because both slots are under `GripTarget`.
- `forceGripPose` stays `false` on this prefab; `ApplyGripPose` and the finger-wrap code stay in the file, untouched (removal is §8).
- Promote `AddressHeadLocal` from a `static readonly` constant to `[SerializeField] Vector3 addressHeadLocal = new Vector3(0.735f, 0f, -0.069f)` so each prefab can carry its own. `PlaceAtBall(Vector3, float)` and `AddressClubHeadWorld` read the field. `PfGolfer_Test` keeps the default (byte-identical behaviour). For `PfGolfer_MixamoNative`, after §3.4, measure `ClubEnd` at address in the golfer's local frame and set the field to that value — then `club.headAtBall` (§6) and `stance.address.clubReachesBall` agree instead of the latter passing by construction.
- All of this is inside `#if GOLFIN_GOLFER_TEST`, as today. `RigBuilder` / constraint components are prefab data, not code — nothing to gate.

### 3.6 Harness — `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs`
Add, for the Mixamo-native variant (the Quaternius run keeps its `grip.*` block and its results):
- `grip.hand.onShaft_l` / `grip.hand.onShaft_r`: distance from `Animator.GetBoneTransform(HumanBodyBones.LeftHand / RightHand).position` to the segment `ClubStart → ClubEnd` (world), sampled at address, at t = 0.6 s after commit and at impact (1.167 s, IMPLEMENTER_REPORT §4 of `golfer_3d_test`); the **worst** sample is the value. PASS < 0.035 m.
- `grip.hands.order`: project both hand positions onto the shaft axis; lead (left) must be 0.05–0.12 m nearer the butt cap than trail (right) at address. PASS in range.
- `club.headAtBall`: `|ClubEnd − ball|` in plan at address, on the real transform. PASS < 0.05 m. (This is the assertion `stance.address.clubReachesBall` was assumed to be.)
- `grip.ikNoLegEffect`: foot slide L/R from the same run within ±0.010 m of the §9.8 baseline 0.0528 / 0.0915 m. PASS in band.
The header of `golfer_invariants_mixamo.json` gains `gripWorstL`, `gripWorstR`, `headAtBallM`. Keep `Skip()` semantics from §9.9 for the Quaternius-only `grip.*` ids on this rig.

## 4. Architecture context
- Events: unchanged — `ShotController.OnShotResolved`, `BallStateMachine.OnShotComplete`, `ClubSelectionBroadcast.OnPutterModeChanged` drive the presenter exactly as in `golfer_3d_test` §4.
- Quality tiers: `ApplyTier` untouched; the rig has no tier behaviour. If `perf.frameDelta` (≤ 1 ms) fails after this change, report the number — do not weaken the assertion.
- Build gate: `GolferTestBuildGate` already excludes `_Test`; the new prefab objects live inside it.

## 5. Out of scope (rows in `Docs/GPS/GPS_BACKLOG.md`)
Authored grip hand pose (fingers); left-handed mirror (swap the two anchors + mirror the local offsets); clubface roll assertion; removing the Quaternius finger solver from `GolferPresenter`; Remy's 36,510 tris; anything on `PfGolfer_Test`.

## 6. Acceptance (Implementer fills `IMPLEMENTER_REPORT.md`, PASS/FAIL + one-line evidence)
| # | Check | Pass condition |
|---|---|---|
| A1 | Package | `com.unity.animation.rigging` in `manifest.json`, project compiles with the define **off** and **on** |
| A2 | Prefab | Hierarchy per §3.2; `RigBuilder.layers` order Grip → Hands; club not under any bone (`object-get-data` or a one-line editor check) |
| A3 | Harness run — ONE, Mixamo-native, Hole 06 | `grip.hand.onShaft_l/_r` < 0.035 m worst-of-three; `grip.hands.order` in band; `club.headAtBall` < 0.05 m; `grip.ikNoLegEffect` in band; every §9.8 PASS still PASS; `budget.tris` still the one FAIL (unchanged, out of scope) |
| A4 | Frames | `evidence/grip/` — address, t = 0.6 s, impact, **on the gameplay camera**, plus one Scene-view close-up of the hands at address labelled as such. Side-by-side with `golfer_3d_test/evidence/9_8/mixamo_*.png` (before) |
| A5 | Define off | EditMode sweep green; `git diff --stat` of shipped (non-`_Test`, non-`#if`) code is empty except `manifest.json` / `packages-lock.json` |
| A6 | Profile | active build profile restored to **`iOS-Full-GPS`** before the final commit, stated in the report |
| A7 | Numbers in report | both authored local poses (§3.4), the measured `addressHeadLocal`, the three grip samples per hand |

Exit: A1–A7 → `STATUS.md` = `READY_FOR_SELF_REVIEW`.

## 7. Files this task touches
`Packages/manifest.json`, `Packages/packages-lock.json`, `…/GolferTest/PfGolfer_MixamoNative.prefab`, `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` (field promotion only), `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` (four assertions + three header fields), `Docs/Specs/Active/golfer_club_grip/*`, `Docs/AI_CONTEXT.md`.

## 8. Backlog rows added this session
See `Docs/GPS/GPS_BACKLOG.md`: authored grip hand pose · left-handed mirror · clubface roll · finger-solver removal · Remy tri budget. The old row "Real club grip — club-in-hand mocap" is **taken up by this spec and deleted** (the §9.8 finding is that the clips were never the problem).
