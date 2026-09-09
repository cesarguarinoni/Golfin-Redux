# SPEC — `golfer_club_grip`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports go in `IMPLEMENTER_REPORT.md`.

## Status
See `STATUS.md`. `SPEC_READY` (2026-09-07); **amended 2026-09-09 three times** — third: §3.7 hand orientation + palm offset (`ARCHITECT_DECISION_HAND_ORIENT.md`); earlier: — `ARCHITECT_DECISION_3_2.md` (GripTarget under the avatar root) and `ARCHITECT_DECISION_RIG_DEAD.md` (the Rigs too; plus the exception gate in §6 A0). Depends on nothing open; `golfer_3d_test` §9.8 is done (`d3deb518d`).

> **EXPERIMENT LANE — still opt-in.** Everything here lives under `GOLFIN_GOLFER_TEST` and the `_Test` asset gate exactly like `golfer_3d_test` §5.6. Nothing reaches a normal build. The one exception is the Animation Rigging package itself (§3.1), which is in `Packages/manifest.json` for every build — accepted, see §3.1.

## Goal
Make the club sit in the golfer's hands on the **Mixamo-native** pipeline that §9.8 selected, without touching the clips and without any per-clip hand tuning: the club is parented to a **grip target** derived from both hands, and **Animation Rigging two-bone IK** pulls each hand onto an anchor on the shaft. The result is the club-mount template every roster model (`Docs/Design/CHARACTER_3D_REMAKE_OPTIONS.md` §7) inherits unchanged.

What it fixes: on `PfGolfer_MixamoNative` the club is parented to the right hand, so its world orientation follows the clip's wrist roll — at address the shaft points at the camera (`golfer_3d_test/evidence/9_8/sbs_address.jpg`, right panel), and through the swing the lead hand is nowhere near the shaft. `golfer_3d_test` §9.3's finger solver is **not** revived (it was written against Quaternius bone names and the retargeting artefact it was chasing no longer exists).

## 1. Why this shape (Architect, 2026-09-07)
- The clips are empty-hand mocap; they carry two hands *near* each other but no shaft. Anything parented to one hand inherits that hand's roll. Deriving the club from **both** hands (position and rotation averaged) removes the single-hand roll and puts the shaft on the line the hands already make.
- IK closes the residual the other way round: instead of moving the club to the hands, the hands move the last few centimetres to the club. Two-bone IK on the arm cannot touch the legs, so the §9.8 foot-slide numbers must not move (§6 checks this).
- Animation Rigging evaluates inside the Animator graph, after the clip and before `LateUpdate`, and rig layers evaluate in order — so layer 1 (grip target from the clip's hands) and layer 2 (hands onto the club) are one pass with no feedback and no script in the hot path. **Constraint (learned 2026-09-09, `FINDING_3_2_IMPOSSIBLE.md`):** a *constrained* (written) transform must be a descendant of **`Animator.avatarRoot`** — on this prefab that is the nested FBX instance `MixamoChar_TPose`, not the prefab root. Bones are not required; the subtree is. See §3.2.
- Fingers stay as the clip has them (loosely closed). An authored grip hand pose is a separate, later item (§8).

## 2. Assets
None new. The club meshes are the existing `GOLFIN_Driver` / `GOLFIN_Putter` already instanced in the prefab.

## 3. Implementation

### 3.1 Package
Add `com.unity.animation.rigging` to `Packages/manifest.json` (Unity 6000.3.9f1 — take the version the Package Manager marks as released for this editor; **NOTE:** 1.3.x/1.4.x, confirm in the Package Manager rather than guessing). It ships in every build (~100 KB of managed code, no scenes reference it with the define off); accepted by the Architect. Do not add it to any assembly definition outside `Golfin.Gameplay.Golfer`'s asmdef (if that asmdef exists — else the default assembly, as `GolferPresenter` is today).

### 3.2 Prefab — `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab`
Restructure the club so it is **not under a bone** (but *is* under the avatar root — amended 2026-09-09):

```
PfGolfer_MixamoNative            (Animator · GolferPresenter · RigBuilder)
├ MixamoChar_TPose               (nested FBX instance = Animator.avatarRoot — AMENDED 2026-09-09, see ARCHITECT_DECISION_3_2.md)
│  ├ mixamorig:Hips …            (skeleton, untouched)
│  └ ClubRoot                    NEW — direct child of MixamoChar_TPose, NOT of a bone; identity transform
│     └ GripTarget               NEW — the constrained object (§3.3); bindable because it is under avatarRoot
│        ├ ClubSlot  ▸ GOLFIN_Driver    MOVED here from the hand; local pose = the authored grip offset (§3.4)
│        ├ PutterSlot ▸ GOLFIN_Putter   MOVED here; its own local pose
│        ├ GripAnchor_Lead       NEW — empty ON the shaft axis (station measured, §3.4); its ROTATION = the lead hand's frame relative to the club, baked at address (§3.7)
│        │  └ WristTarget         NEW (§3.7) — child, localRotation identity, localPosition = −palmLocal_L: the point the IK tip (hand bone) must reach so the PALM, not the wrist, sits on the shaft
│        ├ GripAnchor_Trail      NEW — same for the trail hand, further down the shaft
│        │  └ WristTarget         NEW (§3.7) — localPosition = −palmLocal_R
│        └ ClubStart / ClubEnd   as before (grip / head empties), now children of the club, not of the hand
│  └ GolferRig                   NEW — plain empty; under MixamoChar_TPose (AMENDED 2026-09-09, 2nd — the Rigs were outside the stream root)
│     ├ Rig_Grip                 `Rig`, weight 1 — layer 1
│     │  └ GripTarget_Constraint `MultiParentConstraint` — constrained = GripTarget; sources = mixamorig:LeftHand (0.5), mixamorig:RightHand (0.5); position+rotation; Maintain Offset OFF
│     └ Rig_Hands                `Rig`, weight 1 — layer 2
│        ├ IK_Lead               `TwoBoneIKConstraint` — root mixamorig:LeftArm, mid mixamorig:LeftForeArm, tip mixamorig:LeftHand; target **GripAnchor_Lead/WristTarget**; hint none; weight 1; **targetPositionWeight 1, targetRotationWeight 1** (§3.7)
│        └ IK_Trail              `TwoBoneIKConstraint` — root mixamorig:RightArm, mid mixamorig:RightForeArm, tip mixamorig:RightHand; target **GripAnchor_Trail/WristTarget**; hint none; weight 1; **targetPositionWeight 1, targetRotationWeight 1** (§3.7)
└ UnplayableChecker ×2           (unchanged; nothing else on the root)
```
`RigBuilder.layers` = [Rig_Grip, Rig_Hands] in that order. **`GolferRig` (both `Rig`s and every constraint GameObject) also lives under `MixamoChar_TPose` — amended 2026-09-09 (2nd), see `ARCHITECT_DECISION_RIG_DEAD.md`.** A constraint's weight and per-constraint floats are read through `PropertyStreamHandle`s bound to the constraint's *own* transform; outside the stream root they never resolve and the job throws every frame (`FloatProperty.Get` in `TwoBoneIKConstraintJob.ProcessAnimation`). `RigBuilder` alone stays on the prefab root, on the Animator's GameObject.

**Stream-root rule (the §3.2 lesson, corrected twice):** with the Animator one level above the FBX instance, the animation stream is rooted at `Animator.avatarRoot` = `MixamoChar_TPose`. **Every** object Animation Rigging touches through a stream handle lives under it: transforms it writes (`GripTarget`, the arm bones), transforms it reads (`GripAnchor_*`, hand sources — put them there too, no exceptions), and the `Rig` / constraint components themselves (their weights are stream properties). The only thing on the prefab root is `RigBuilder`, beside the Animator. Before the first run, `GolferTestBootstrap` (or the harness `spawn.animator` detail) logs `anim.avatarRoot.name` once — expected `MixamoChar_TPose`. If it prints anything else, **stop and report the name**; do not re-parent by trial. Lead = left hand, trail = right (the prefab is right-handed; §8 for the mirror). `GameplayIdleClubSlot` / `GameplayIdlePuttClubSlot`, if present on this prefab, move under `GripTarget` as well and keep their names (R5 socket contract in `CHARACTER_3D_REMAKE_OPTIONS.md` §2).

**`PfGolfer_Test` (Quaternius) is not touched.** It is the dead branch; leave it exactly as §9 left it.

### 3.3 Why MultiParent with Maintain Offset OFF
**Amended 2026-09-09:** the constrained object is still `GripTarget`, unchanged; only its parent moved (§3.2). No script replaces this constraint — `GripTargetDriver.cs` and any magic-constant driver stay deleted.
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

### 3.7 Hand orientation and palm offset — the two rules `HANDOFF_ARCHITECT_GRIP.md` asked for (added 2026-09-09)
Two-bone IK moves the **hand bone origin** — the wrist. With `targetRotationWeight = 0` the hand keeps the clip's rotation (hands flat beside the shaft, `04_current_best…jpg`); with the anchor carrying the *club's* frame at weight 1 both hands take one club rotation and interpenetrate (`05_rejected…jpg`). Neither is a rig fault. The anchor has to carry a **hand** frame, and the IK has to aim the **palm** at the shaft, not the wrist. Both are measured off the clip and the bones — nothing is authored by eye and no Mixamo local axis is assumed.

**Rule 1 — anchor rotation = the clip's own hand-to-club frame at address, baked once.** At the address frame with `Rig_Hands` weight 0 (the club already hangs from `GripTarget`, layer 1 alive):
```
R_anchor_local(hand) = Quaternion.Inverse(anchorParent.rotation) * handBone.rotation
```
written into `GripAnchor_Lead.localRotation` (left hand) and `GripAnchor_Trail.localRotation` (right hand). At address this is a no-op by construction — the hands stay exactly where the mocap actor held them. What it buys is the rest of the swing: with `targetRotationWeight = 1` each hand keeps *its own* address relationship to the club through the whole motion, while the club keeps following the two hands' average (layer 1). Hands cannot rotate into each other because their relative pose is the address pose, frozen. This is deliberately **not** "palm normal points at the shaft": that would be a second, invented target. If the address hands look wrong, the fix is the finger layer (backlog), not a different rotation rule.

**Rule 2 — the IK targets the wrist offset so the palm lands on the shaft.** Define the palm point from bones (they exist on this rig — `mixamorig:LeftHandIndex1 / Middle1 / Pinky1 …` are in the avatar; the harness `grip.*` SKIP is keyed to *Quaternius* names, which is a different fact):
```
P      = hand.position + 0.5 * (middle1.position − hand.position)          // palm centre, along the hand
n      = normalize(cross(index1.position − hand.position, pinky1.position − hand.position))
         with the sign flipped if dot(n, middle2.position − middle1.position) < 0   // n points out of the palm, the way the fingers curl
palmW  = P + n * (palmHalfThickness + shaftRadius)                         // where the shaft AXIS passes through a closed hand
palmLocal = hand.InverseTransformPoint(palmW)                              // constant in hand space; bake it
```
`shaftRadius = 0.012` is the existing `GolferPresenter.shaftRadius`; `palmHalfThickness` is read once off the hand mesh bounds at the scale the prefab runs at (report the number; if it cannot be measured use 0.010 and say so). `WristTarget.localPosition = −palmLocal` under an anchor whose rotation is the hand frame (Rule 1), so aiming the tip at `WristTarget` puts `palmW` on the anchor, i.e. on the shaft axis. Left and right get their own `palmLocal`.

**Anchor stations** stay as measured this iteration (lead −0.0096 / trail +0.0599 along the shaft from the §3.4 solve; keep the bounded reach-balance search). `hint` stays none.

**Why not fingers now:** the finger bones are there, so a closed-hand pose is possible — a one-frame hand-pose clip on an Animator layer with a hands-only avatar mask — but it is its own item (`GPS_BACKLOG.md`, row updated: "no finger bones" was wrong). This spec closes when the hands are on the shaft and oriented; open fingers are accepted.

### 3.6 Harness — `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs`
Add, for the Mixamo-native variant (the Quaternius run keeps its `grip.*` block and its results):
- `grip.hand.onShaft_l` / `grip.hand.onShaft_r` (**redefined 2026-09-09**): distance from the **palm point** `hand.TransformPoint(palmLocal)` (§3.7) — not the bone origin, which scored 0.0000 m with the shaft through the wrist — to the segment `ClubStart → ClubEnd` (world), sampled at address, at t = 0.6 s after commit and at impact (1.167 s, IMPLEMENTER_REPORT §4 of `golfer_3d_test`); the **worst** sample is the value. PASS < 0.035 m.
- `grip.hands.order`: project both hand positions onto the shaft axis; lead (left) must be 0.05–0.12 m nearer the butt cap than trail (right) at address. PASS in range.
- `club.headAtBall`: `|ClubEnd − ball|` in plan at address, on the real transform. PASS < 0.05 m. (This is the assertion `stance.address.clubReachesBall` was assumed to be.)
- `grip.ikNoLegEffect`: foot slide L/R from the same run within ±0.010 m of the §9.8 baseline 0.0528 / 0.0915 m. PASS in band.
- `grip.hand.orient_l` / `grip.hand.orient_r` (**new 2026-09-09**): `Quaternion.Angle(handBone.rotation, anchor.rotation)` post-IK, sampled at address, t = 0.6 s and impact; worst sample. PASS < 5°. This is the row that was missing — a visibly wrong grip must move a number.
- `grip.hands.apart` (**new**): distance between the two palm points at the same three samples; worst (smallest). PASS ≥ 0.045 m (the anchors are ~0.07 m apart along the shaft; two palms cannot occupy the same 4.5 cm).
- `grip.targetTracksHands`: **retired** — it compared layer 1's pre-IK midpoint with the post-IK hands and could not pass by construction; `grip.hand.onShaft_*` + `grip.hand.orient_*` cover what it meant to check.
- Harness section 7 (quality tiers): restore `Auto` **after** the shot block, not immediately before the shot — the Low override sets `animatorCulling = CullCompletely`, which is the "golfer vanishes before the shot" Cesar saw. Harness-only.
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
| A0 | Rig alive (added 2026-09-09) | Zero `InvalidOperationException` from `UnityEngine.Animations.Rigging` in the Console / `Editor.log` for the whole run (Code counts them; the number goes in the report). A run with any is not a run — every other grip number in this table is void without A0. |
| A1 | Package | `com.unity.animation.rigging` in `manifest.json`, project compiles with the define **off** and **on** |
| A2 | Prefab | Hierarchy per §3.2; `RigBuilder.layers` order Grip → Hands; club not under any bone (`object-get-data` or a one-line editor check) |
| A3 | Harness run — ONE, Mixamo-native, Hole 06 | `grip.hand.onShaft_l/_r` (palm point) < 0.035 m worst-of-three; **`grip.hand.orient_l/_r` < 5°; `grip.hands.apart` ≥ 0.045 m**; `grip.hands.order` in band; `club.headAtBall` < 0.05 m; `grip.ikNoLegEffect` in band; every §9.8 PASS still PASS; `budget.tris` still the one FAIL (unchanged, out of scope) |
| A4 | Frames | `evidence/grip/` — address, t = 0.6 s, impact, **on the gameplay camera**, plus Scene-view close-ups of the hands at **all three** samples labelled as such (mid-swing is where hands "go through one another"; address alone proved nothing). Side-by-side with `golfer_3d_test/evidence/9_8/mixamo_*.png` (before) |
| A5 | Define off | EditMode sweep green; `git diff --stat` of shipped (non-`_Test`, non-`#if`) code is empty except `manifest.json` / `packages-lock.json` |
| A6 | Profile | active build profile restored to **`iOS-Full-GPS`** before the final commit, stated in the report |
| A7 | Numbers in report | both authored local poses (§3.4), the measured `addressHeadLocal`, the three grip samples per hand, **the two baked `R_anchor_local` and `palmLocal` values with `palmHalfThickness`**, the club scale (0.86880 — a stand-in artefact of the 1.328 m character, NOTE in `CHARACTER_3D_REMAKE_OPTIONS.md` §7: roster models at R2 height carry full-size clubs), the Animation Rigging package version, the A0 count |

Exit: A1–A7 → `STATUS.md` = `READY_FOR_SELF_REVIEW`.

## 7. Files this task touches
`Packages/manifest.json`, `Packages/packages-lock.json`, `…/GolferTest/PfGolfer_MixamoNative.prefab`, `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` (field promotion only), `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` (four assertions + three header fields), `Docs/Specs/Active/golfer_club_grip/*`, `Docs/AI_CONTEXT.md`.

## 8. Backlog rows added this session
See `Docs/GPS/GPS_BACKLOG.md`: authored grip hand pose · left-handed mirror · clubface roll · finger-solver removal · Remy tri budget. The old row "Real club grip — club-in-hand mocap" is **taken up by this spec and deleted** (the §9.8 finding is that the clips were never the problem).
