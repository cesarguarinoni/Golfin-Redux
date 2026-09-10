# SPEC — `golfer_club_grip`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports go in `IMPLEMENTER_REPORT.md`.

## Status
See `STATUS.md`. `SPEC_READY` (2026-09-07); **§3.12 (2026-09-11, evening) — Cesar: this experiment decides whether the pipeline can do the animation with minimum artist intervention, so the close-range grip is the goal again. §3.12 = the hinge-model grip with a staged test protocol; it supersedes §3.11's "hands are the clip's" and the ARCHITECT_CLOSEOUT. The club mount from §3.11 stays.** Earlier: **FINAL SHAPE 2026-09-11 — §3.11: clip hands untouched, club on the two-hand average, no IK, no finger work. §3.5–§3.10 retired (`ARCHITECT_DECISION_FINAL_SHAPE.md`)**; earlier: **amended 2026-09-11 (2nd) — §3.10 handedness + wrap sign, wrap-invariant lead landmark, lead station from the butt cap (`ARCHITECT_REVIEW_ITER7.md`)**; **amended 2026-09-11 — §3.9 supersedes §3.8.3/§3.8.4 and the muscle-space pose: shaft placed by golf-grip landmarks (`reference/GOLF_GRIP_GEOMETRY.html`), fingers closed by the contact wrap through `HumanBodyBones`, harness made deterministic (`ARCHITECT_DECISION_LANDMARKS.md`)**; earlier 2026-09-10 (2nd — §3.8 finger pose brought INTO scope, `ARCHITECT_DECISION_FINGERS.md`; the iter-5 "grip accepted" verdict is withdrawn)**; earlier 2026-09-10 (`ARCHITECT_REVIEW_ITER5.md`: foot-slide baseline rule, impact capture, `addressHeadLocal` dropped) and **2026-09-09 three times** — third: §3.7 hand orientation + palm offset (`ARCHITECT_DECISION_HAND_ORIENT.md`); earlier: — `ARCHITECT_DECISION_3_2.md` (GripTarget under the avatar root) and `ARCHITECT_DECISION_RIG_DEAD.md` (the Rigs too; plus the exception gate in §6 A0). Depends on nothing open; `golfer_3d_test` §9.8 is done (`d3deb518d`).

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
- Promote `AddressHeadLocal` from a `static readonly` constant to `[SerializeField] Vector3 addressHeadLocal = new Vector3(0.735f, 0f, -0.069f)` so each prefab can carry its own. `PlaceAtBall(Vector3, float)` and `AddressClubHeadWorld` read the field. `PfGolfer_Test` keeps the default (byte-identical behaviour). **Amended 2026-09-10:** the MixamoNative value is **not** authored — the §3.4 solve already lands the real `ClubEnd` on the ball (`club.headAtBall` 0.0085 m) and authoring the field would re-open the solve for nothing. `stance.address.clubReachesBall` is reclassified as *informational* (it reports the placement constant, not the club); `club.headAtBall` is the assertion.
- All of this is inside `#if GOLFIN_GOLFER_TEST`, as today. `RigBuilder` / constraint components are prefab data, not code — nothing to gate.

### 3.7 Hand orientation and palm offset — the two rules `HANDOFF_ARCHITECT_GRIP.md` asked for (added 2026-09-09)
Two-bone IK moves the **hand bone origin** — the wrist. With `targetRotationWeight = 0` the hand keeps the clip's rotation (hands flat beside the shaft, `04_current_best…jpg`); with the anchor carrying the *club's* frame at weight 1 both hands take one club rotation and interpenetrate (`05_rejected…jpg`). Neither is a rig fault. The anchor has to carry a **hand** frame, and the IK has to aim the **palm** at the shaft, not the wrist. Both are measured off the clip and the bones — nothing is authored by eye and no Mixamo local axis is assumed.

**Rule 1 — anchor rotation = the clip's own hand-to-club frame at address, baked once.** At the address frame with `Rig_Hands` weight 0 (the club already hangs from `GripTarget`, layer 1 alive):
```
R_anchor_local(hand) = Quaternion.Inverse(anchorParent.rotation) * handBone.rotation
```
written into `GripAnchor_Lead.localRotation` (left hand) and `GripAnchor_Trail.localRotation` (right hand). At address this is a no-op by construction — the hands stay exactly where the mocap actor held them. What it buys is the rest of the swing: with `targetRotationWeight = 1` each hand keeps *its own* address relationship to the club through the whole motion, while the club keeps following the two hands' average (layer 1). Hands cannot rotate into each other because their relative pose is the address pose, frozen. This is deliberately **not** "palm normal points at the shaft": that would be a second, invented target. If the address hands look wrong, the fix is the finger layer (backlog), not a different rotation rule.

**Rule 2 — the IK targets the wrist offset so the palm lands on the shaft.** *(Superseded 2026-09-10 by §3.8.3: `palmLocal` and the shaft direction come from the POSED fingers, not from this formula. Kept for the record.)* Define the palm point from bones (they exist on this rig — `mixamorig:LeftHandIndex1 / Middle1 / Pinky1 …` are in the avatar; the harness `grip.*` SKIP is keyed to *Quaternius* names, which is a different fact):
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

### 3.8 Finger pose — in scope (added 2026-09-10; withdraws the iter-5 acceptance) — **§3.8.1–3.8.5 superseded by §3.9 on 2026-09-11; kept for the record**
Iter-5's full-resolution frames show the shaft passing **between the index and middle fingers** at address and a lead-hand finger **protruding into the trail hand**. The hands are on the shaft and oriented; they are not *holding* it, because the fingers are still the clip's loose empty-hand fist and the shaft axis was put where a formula said a palm is, not where closed fingers leave room. That is the standard held-prop problem and it has a standard answer (sources in `ARCHITECT_DECISION_FINGERS.md`): **an authored hand pose applied over the animation on a hands-only layer, with the prop's grip point defined relative to that pose** — not relative to the wrist and not relative to a formula.

**3.8.1 The pose asset.** One `AnimationClip` `ANIM_HandPose_GolfGrip.anim` (one frame) carrying **Humanoid finger muscle values** for both hands — `LeftHand.Index.1 Stretched` … `RightHand.Thumb.Spread`, all 40 finger muscles. Muscle space, not bone rotations, so the same clip poses every roster model. Authoring path: import **VoxHands** (MIT, `hiroki-o/VoxHands`) or **HumanoidHandPoseHelper** (`umiyuki/HumanoidHandPoseHelper`) into `Assets/Editor/ThirdParty/` (editor-only, gated out of builds), pose with the sliders **in the Inspector on `PfGolfer_MixamoNative` at the address frame with the club visible**, export the clip. Start values so the first pass is not blind: Index/Middle/Ring/Pinky `1/2/3 Stretched = −0.55 / −0.75 / −0.65`, `Spread = 0`; Thumb `1 Stretched −0.4, Spread −0.6, 2 Stretched −0.4, 3 Stretched −0.3`; lead hand thumb points **down the shaft** (adjust Thumb 1 Spread on the left hand until it does). This is the one step in this spec that is done by eye — by Cesar or by Code with a screenshot per adjustment, at most three adjustments, then stop and show. A pose that is 90 % right and stops is better than a fourth round.

**3.8.2 Applying it.** `AnimatorController_Golfer_MixamoNative` gains layer **`Hands`**: weight 1, blending **Override**, avatar mask `Mask_HandsOnly.mask` (Humanoid body parts: *Left Fingers* + *Right Fingers* only — nothing else, or the swing dies), one state playing `ANIM_HandPose_GolfGrip`, looping. The base layer is untouched. Nothing in code; the presenter never sees it.

**3.8.3 Where the shaft goes — measured from the POSED fingers, replacing §3.7 Rule 2's formula.** With the `Hands` layer on and `Rig_Hands` weight 0, at the address frame, per hand: for each of index/middle/ring/pinky take the knuckle `f1` and the tip `f3 + (f3 − f2)` (the tip leaf if the FBX has `*Index4` etc.), form `C_f = 0.5 (knuckle + tip)`, and fit a line through the four `C_f` (principal axis of the four points). That line is the **shaft axis in hand space**; `palmLocal = hand.InverseTransformPoint(mean(C_f))` and `shaftDirLocal = hand.InverseTransformDirection(fit direction)`. The curl radius `r_curl = mean distance of the four tips and four knuckles from that line`. Report `r_curl` against the grip radius `shaftRadius = 0.012`: if `r_curl > 0.018` the pose is too open, tighten `2/3 Stretched` and re-export (that is one of the three adjustments); if `r_curl < 0.009` the fingers pass through the shaft, loosen. `WristTarget.localPosition = −palmLocal` as before; **and** the anchor's baked rotation (Rule 1) is now composed so that the anchor's local +Y (the shaft) coincides with `shaftDirLocal`: `R_anchor_local = R_clip_bake * Quaternion.FromToRotation(shaftDirLocal_in_anchor, Vector3.up)` — i.e. the hand is rolled about its own axis the small amount needed for the *posed fingers'* tunnel to line up with the shaft. Report the angle of that correction per hand; > 35° means the pose or the clip is wrong, stop.

**3.8.4 Hand spacing and the overlap grip.** Stations along the shaft are no longer 0.03/0.11 or the reach-balance numbers: lead-hand station = the §3.4 solve as now; trail-hand station = lead station **+ palm width of the lead hand + 0.004 m**, palm width = `|index1 − pinky1|` of the lead hand at the posed frame. The trail hand's pinky must sit *just past* the lead index knuckle, not on it. The lead thumb lies down the shaft under the trail palm — that is what the pose authoring in 3.8.1 must produce; it is the only intentional contact between the hands.

**3.8.5 What "holding" means, as assertions (§3.6/§6):**
- `grip.fingers.closed_l/_r`: for each of the eight non-thumb fingers, distance from the tip point to the shaft axis in `[shaftRadius − 0.004, shaftRadius + 0.012]` (tip touching or just off the grip, never inside it). Worst finger, at address, t = 0.6, impact.
- `grip.shaft.inTunnel_l/_r`: the shaft axis passes between the palm plane (hand, index1, pinky1) and the tip points: signed distance of the axis from the palm plane in `[0.008, 0.024]`, and the axis's distance from the line through the four tips ≥ 0.006 m. This is the "shaft between the fingers" check.
- `grip.hands.noOverlap`: minimum distance between any lead-hand joint (index/middle/ring/pinky 1–3, excluding thumb) and any trail-hand joint (same set) ≥ 0.010 m at all three samples.
- `grip.thumb.downShaft_l`: angle between (thumb3 − thumb1) of the lead hand and the shaft direction toward the head < 35°.

### 3.9 Landmark placement + contact wrap — supersedes §3.8.3, §3.8.4 and the muscle-space pose (added 2026-09-11)
Iter-6 frames (`evidence/grip38/`): the butt cap emerges between the two wrists, the fingers curl *beside* the shaft. Two causes, both mine: §3.8.3 fitted the shaft through the middle of a closed fist (a **bat** axis, across the palm, ~25–30° off a golf grip and shifted to the wrist), and §3.8.4 stationed the hands a palm width apart (a golf grip **overlaps**). The muscle-space pose also cannot close on this avatar (`r_curl` floors at 0.030; finger axes unconfigured). All three are replaced by rules read off how a club is actually held — `reference/GOLF_GRIP_GEOMETRY.html`, sources inside.

**3.9.1 Shaft axis in each hand — two landmarks, not a fit.** Bones via `Animator.GetBoneTransform(HumanBodyBones.*)` (rig-independent; Mixamo names in brackets). `n` = palm-side unit normal at the hand (from `cross(index1 − hand, pinky1 − hand)`, sign toward the finger curl); `t` = finger half-thickness, `r` = grip radius, both scaled by `s = characterHeight / 1.75` (real: t = 9 mm, r = 11.5 mm; Remy s ≈ 0.76 → 6.8 / 8.7 mm; use the presenter's `shaftRadius` for r if it already carries the scaled value).
- Lead (left): `A = LeftLittleProximal [LeftHandPinky1] + n·(t+r)`, `B = LeftIndexIntermediate [LeftHandIndex2] + n·(t+r)`. Butt→head direction = `A→B`. The butt cap sits `13 mm·s` beyond the pinky-side edge of the heel along `−(A→B)`.
- Trail (right): `A' = RightLittleProximal [RightHandPinky1] + n·(t+r)`, `B' = RightIndexProximal [RightHandIndex1] + n·(t+r)`. Butt→head = `A'→B'`.
These lines are `shaftDirLocal` / `palmLocal` (take `palmLocal` = the point on the line nearest the hand's middle-finger MCP). They **replace** the four-midpoint fit. `WristTarget.localPosition = −palmLocal` as before.

**3.9.2 Anchor rotation — fully determined, no clip bake, no by-eye.** Per hand the anchor's local rotation is the one that (i) maps `shaftDirLocal` onto the club's +Y (2 DOF) and (ii) fixes the roll about the shaft by a palm rule (1 DOF):
- Lead: the **back of the hand faces the golfer's head** — roll so that `dot(−n_world, normalize(Head.position − anchor.position))` is maximal (the "2–2½ knuckles visible" check). Heel pad ends up on top of the grip by construction.
- Trail: the **palm faces the lead thumb** — roll so that `dot(n_world, normalize(LeftThumbIntermediate.position − anchor.position))` is maximal (the lifeline over the thumb).
Compute both at the address frame with `Rig_Hands` weight 0, write them into `GripAnchor_*.localRotation`, `targetRotationWeight = 1`. Rule 1's clip bake is retired; the 35° stop is retired with it — report the angle between the new anchor rotation and the clip's hand as information only.

**3.9.3 Stations — the hands overlap.** Lead station from the §3.4 solve as now. Trail station: the projection of `RightLittleProximal` onto the shaft equals the projection of the midpoint of `LeftIndexProximal` and `LeftMiddleProximal` — the trail pinky rides on the lead index/middle gap. Replaces "lead palm width + 4 mm".

**3.9.4 Fingers — contact wrap, not muscle space.** Remove the `Hands` layer, mask and clip (§3.8.1/3.8.2). Bring back the contact solver that already exists in `GolferPresenter` (`WrapHand` / `WrapJoint` / `AimThumbDownShaft`, the `#if GOLFIN_GOLFER_TEST` block that §3.5 kept) with one change: bones resolved through `HumanBodyBones` (`LeftIndexProximal/Intermediate/Distal` … `RightLittleDistal`, thumbs likewise) instead of Quaternius names, cylinder = the real shaft (`ClubStart→ClubEnd`, radius `r`). It runs in `LateUpdate` after Animator + rig, every frame, on **seven** fingers: the **trail little finger is excluded** (it rides on the lead index; leave it as the clip has it, curled by `WrapJoint` against the lead index knuckle as its "cylinder" if that is a one-line change, else untouched). Thumbs: `AimThumbDownShaft` for the lead thumb toward the head, target 1 o'clock (15–30° toward the trail side of the top); trail thumb rests on the shaft's lead side, inside edge only. `forceGripPose` is the switch; it goes back to `true` on this prefab. The §9.3 "seven rounds" were about wrist roll on a retargeted rig; the wrap itself passed `grip.wrapped_*` then and is the right tool now.

**3.9.5 Harness determinism (from iter-6 §4).** Foot slide varied 0.028 → 0.084 m across identical runs, so the row cannot be trusted at ±0.010. Set `Time.captureDeltaTime = 1/60f` from the harness launch until `Finish()` (and back to 0 after), so animation and physics advance a fixed step per frame regardless of editor frame rate; measure the rig-off baseline and the rig-on run under it. Report three rig-on runs' R values; the spread must be < 0.005 m before the row is compared. If it is still not, the row is downgraded to informational and says so.

**3.9.6 Assertions (§3.6/§6) — replace the §3.8.5 rows with:**
- `grip.axis.landmarks_l/_r`: distance of `ClubStart→ClubEnd` from the landmark line `A→B` (`A'→B'`) < 0.006 m at both landmarks, address / t=0.6 / impact.
- `grip.fingers.closed_l/_r`: for the seven wrapped fingers, tip-to-axis in `[r − 0.003, r + 0.010]`; worst finger, three samples.
- `grip.heelPad.onTop`: lead `dot(−n_world, toHead) > 0.5` at address.
- `grip.trailPalm.onThumb`: trail `dot(n_world, toLeadThumb) > 0.5` at address.
- `grip.hands.overlap`: trail little-finger MCP station within ±0.008 m of the lead index/middle MCP midpoint station.
- `grip.hands.noInterpenetration`: no lead finger joint (index/middle/ring/little 1–3) within 0.008 m of any trail finger joint **other than the trail little finger** — the overlap is the intended contact.
- `grip.thumb.downShaft_l` (keep) with the clock angle reported.
- `grip.buttCap.pastHeel`: butt cap 8–20 mm·s beyond the heel edge along the shaft.
- Retire `grip.shaft.inTunnel_*`, `grip.hands.apart`, `grip.hands.order` (the overlap rule replaces them).

**3.9.7 Out of scope, next:** clubface roll (the blade orientation Cesar flagged in iter-6) — a `ClubHead` face-normal-vs-aim assertion and the roll about the shaft it implies; own row, after the grip.

### 3.10 Handedness, wrap sign, and two landmark fixes (added 2026-09-11, after iter-7)
Iter-7 placed and oriented the hands like a grip (overlap, heel pad, trail palm all PASS) and made the harness deterministic (spread 0.0003 m). The fingers did not close, and `evidence/grip39/address_targetside.png` shows them **straight and hyperextended**, not capped at 80°. Cause, from `GolferPresenter.cs`: `PalmNormal` = `cross(along, across)` with `across = little − index` is mirror-antisymmetric — it points out of the palm on one hand and out of the back on the other (the `WrapJoint` comment already says the palm-normal sign "flips with handedness"). Two consequences: (a) `WrapJoint` picks its sign by "which way reduces distance to the shaft", which returns **extension** when the shaft sits in or behind the finger plane; (b) §3.9.1's `n·(t+r)` offset used the same normal, so the trail shaft axis was placed on the **back** side of the MCP row, where no finger can reach it. Every other iter-7 number is consistent with this.

**3.10.1 One palm normal, handedness-fixed.** `n_out(side)` = unit normal **out of the palm surface**: `cross(along, across)` for the left hand and `−cross(along, across)` for the right, `across = LittleProximal − IndexProximal`, `along = mid(Index/LittleProximal) − Hand`. Verify once per hand and print it: flex the middle MCP by +5° about `cross(bone, n_out)` and confirm the tip moves along `+n_out` (`dot > 0`); if it does not, the sign convention above is wrong for this rig — report, do not flip silently. `PalmNormal`, §3.9.1 (`+ n_out·(t+r)`), §3.9.2 rules (lead: `dot(−n_out, toHead)`; trail: `dot(n_out, toLeadThumb)`) and `AimThumbDownShaft`'s `up12` all use this one function.

**3.10.2 The wrap only flexes.** `WrapJoint` sign is always **+flexion** (toward `+n_out`); the "reduce distance to the shaft" test and the "make a fist" branch are removed. Bisection to contact as now; if contact is unreachable at the cap, take the cap. Caps 90° (proximal) / 90° (intermediate) / 70° (distal). A finger that would have to extend to touch the shaft is a placement error, and `grip.fingers.closed_*` reports it as such. Log per joint at end of frame, at address: applied angle, `d0`, `dMax`, tip-to-axis after — this is the play-mode measurement iter-7 asked for, and it goes in the report for all 21 joints.

**3.10.3 Wrap-invariant lead landmark.** `IndexIntermediate` moves when the index curls, so `grip.axis.landmarks_l` measured 22 mm post-wrap against a line solved pre-wrap. Replace B with a point that does not move with the fingers: `B = IndexProximal + u·(0.6·|IndexIntermediate − IndexProximal|) + n_out·(t+r)`, `u = normalize(MiddleProximal − Hand)` (the hand's length axis — that is where the middle joint of a curled index sits). A and the trail landmarks are MCP joints and already invariant. `grip.axis.landmarks_l/_r` measure against these points.

**3.10.4 Lead station from the butt cap, not the §3.4 solve.** `grip.buttCap.pastHeel = −12 mm` means the lead hand hangs off the end of the grip. Set the lead anchor station so that the projection of the `LeftHand` bone onto the shaft is `10 mm·s` down-shaft of `ClubStart` (the butt cap); then re-run the §3.4 club-position solve and the REACH marks with that station fixed. Trail station stays per §3.9.3 (rig-on two-point fit — accepted as the method).

**3.10.5 Thumb clock sign.** "Toward the trail side" = toward the projection of the **trail palm centre** (`mid(RightIndexProximal, RightLittleProximal)`) onto the plane ⟂ shaft, evaluated after the trail station is final. Report the signed clock angle; PASS = +15…+30°.

**3.10.6 Trail-palm-on-thumb, geometric.** Replace the dot-product rule's *assertion* (keep it as the solve rule) with: along `n_out_trail`, the signed distance of `LeftThumbIntermediate` from the trail palm plane is in `(0, 25 mm·s)` **and** the shaft axis is further out than the thumb along the same normal. That is "the lifeline covers the thumb" as geometry; a palm facing the wrong way cannot pass it.

### 3.11 FINAL SHAPE — clip hands, club on the two-hand average, nothing else (Cesar, 2026-09-11)
Cesar, iter-8: *"Why not simply use the model's initial pose (hands looked alright there) and add a bone to attach the club? Do we need a perfect grip?"* No, we do not. At the gameplay camera the hands are ~40 px tall; `golfer_3d_test`-era `evidence/architect/06_full_frame_address.jpg` already read as a golfer holding a club. Eight iterations of IK, landmarks and a contact wrap produced hands that read as wrong at close range and could not be seen at game range. This section is the whole deliverable; §3.5–§3.10 are retired and stay in the file as history.

**3.11.1 What stays (already built, A0 = 0):** `ClubRoot → GripTarget` under `MixamoChar_TPose`; `Rig_Grip` with the `MultiParentConstraint` — `GripTarget` = 0.5/0.5 average of the two hand bones' position and rotation, Maintain Offset OFF. This is the "bone to attach the club" done in a way that survives the wrist roll: a single hand bone gives the §9.8 F3 shaft-at-the-camera; the average of both does not, and it is what has been holding the club in every frame since iter-4. `ClubSlot` / `PutterSlot` under `GripTarget` with one authored local pose each.

**3.11.2 What goes:** `Rig_Hands` and both `TwoBoneIKConstraint`s (delete the objects; `RigBuilder.layers` = [`Rig_Grip`] only); `GripAnchor_*` / `WristTarget` (delete); `forceGripPose = false` and it stays false — the wrap code remains in `GolferPresenter` under the define for `PfGolfer_Test` only; no `Hands` layer, mask or pose clip (already removed). The hands and fingers are the clip's, untouched, at every frame.

**3.11.3 The one authored thing — `ClubSlot` local pose under `GripTarget`, solved once at address:**
- position: the §3.4 club solve as it stands (head on the ball, `club.headAtBall` 0.0085 m) — keep the current numbers;
- roll about the shaft: **clubface square to the aim at address** — rotate `ClubSlot` about the shaft axis so the driver face normal (the `ClubHead` mesh's face direction; measure it once from the mesh bounds/normal and record which local axis it is) is perpendicular to the aim direction within 5°. This closes the blade-orientation item (§3.9.7) in the same authoring pass, because with no IK the club's roll *is* this offset.
- `PutterSlot` likewise on the putt address.
Report both local poses. No by-eye iteration: two numbers, measured.

**3.11.4 Acceptance for this shape (replaces A3 for the grip rows):**
- A0 = 0; `club.headAtBall` < 0.05 m; `stance.*`, `shot.*`, `tier.*`, `perf.frameDelta` as before; `grip.ikNoLegEffect` against the rig-off baseline under the fixed step;
- `club.faceSquare`: angle between the face normal and the aim, at address, `90° ± 5°`;
- `grip.hand.onShaft_l/_r` (bone origin to shaft axis) **informational** — reported, not gated: it tells us how far the clip's hands drift from the club through the swing (expected 1–4 cm mid-swing, invisible at game range);
- every other `grip.*` row from §3.6–§3.10: **retired** (remove from the harness or leave as SKIP with reason "§3.11 — hands are the clip's");
- frames: the three gameplay-camera frames (address, t = 0.6 s, impact) at full res, plus **one** scene-cam close-up at address for the record. Cesar judges the gameplay frames; nobody judges the close-up.

**3.11.5 Close-out.** When 3.11.3–3.11.4 hold: `STATUS → READY_FOR_SELF_REVIEW`, Architect review, Cesar approves, Code moves the folder to `Docs/Specs/Completed/` and merges `golfer_3d_test` → `main`. Backlog rows: "close-range grip — authored hand pose (animator), only if a camera ever shows hands large" and "left-handed mirror". `reference/GOLF_GRIP_GEOMETRY.html` stays as the reference for that day.

### 3.12 The hinge-model grip, with a staged test protocol (2026-09-11, evening — supersedes §3.11's hand rule and the close-out)
**Purpose restated by Cesar:** *this experiment is specifically to decide if the pipeline can handle the animation with minimum artist intervention.* So the close-range grip is the deliverable, with no artist. What follows is built so that a wrong model fails at the first stage, in the editor, in minutes — not at iteration eight.

**3.12.1 Root cause of nine rounds of claws, so it is not repeated.** Every wrap (§3.5, §3.9.4, §3.10.2, iter-9b) derived each joint's bend axis from the *animated* pose in world space, bent joints independently against caps, and started from the clip's fingers. Three errors compound: a joint rotated about a slightly wrong axis rotates the next joint's axis further; independent joints leave one at 90° beside one at 0°; and the clip's fingers already carry spread and roll. The shipped pattern for held objects (Meta ISDK hand-grab poses, XR Hands) is the opposite — fingers driven from a **rest pose** by **per-joint hinge angles about axes fixed in each joint's own local space**, with the grab pose stored **relative to the object** and applied as data. §3.12 builds exactly that.

**3.12.2 `HandHingeModel` — the mechanism (new class, `Assets/Scripts/Gameplay/Golfer/HandHingeModel.cs`, under the define; replaces `ApplyHeldGripPose` and is not another branch of `ApplyGripPose`).**
- *Capture, once, in the editor, from the prefab's rest pose* (the prefab out of play mode is the FBX bind pose — fingers straight, hand flat). For each of the 30 finger joints (`HumanBodyBones` Index/Middle/Ring/Little × Proximal/Intermediate/Distal, Thumb × 3, both hands) store: `restLocalRotation`; `hingeAxisLocal = joint.InverseTransformDirection(cross(fingerDir_world, palmNormal_world))`, `fingerDir` = child − joint; `abductAxisLocal = joint.InverseTransformDirection(palmNormal_world)`. Palm normal in rest pose from the triangle (Hand, IndexProximal, LittleProximal), **sign fixed by the thumb**: the vector `ThumbProximal − IndexProximal` has a positive component along the palm-side normal (the thumb base is palmar). Store the palm normal and the hand's length axis in `Hand`-local space too. Serialize all of it into a `HandHingeData` asset per prefab (`…/GolferTest/HandHinge_MixamoNative.asset`).
- *Runtime, every `LateUpdate` after Animator + rig:* `joint.localRotation = restLocalRotation * AngleAxis(spread, abductAxisLocal) * AngleAxis(flex, hingeAxisLocal)`. Flexion is positive toward the palm. The clip's finger animation is discarded entirely; the wrist is not touched by this class.
- *Thumb:* `ThumbProximal` gets a 2-DOF aim (rotate `restLocalRotation` by the `FromToRotation` that carries the rest thumb direction onto a target direction given in `Hand`-local space) plus fixed flexion on `ThumbIntermediate`/`ThumbDistal` (15°, 10°). No bisection on the thumb.
- *EditMode tests (`HandHingeModelTests`)*: on the prefab in rest pose, (a) flexing `LeftIndexProximal` by 90° moves `LeftIndexIntermediate` toward the palm by ≥ 0.8 × the proximal length and ≤ 3 mm sideways; (b) flexing all four fingers by 60/80/40 keeps adjacent fingertips ≥ 8 mm apart and every tip on the palm side of the MCP plane; (c) the same on the right hand; (d) capture is idempotent (second capture = first to 1e-5). These run with the define **off** too (the class compiles; the asset is data).

**3.12.3 The grip pose, in numbers (power grip on a Ø 27 mm grip at the club's scale — `Grip` mesh radius 13.575 mm measured in iter-9b, plus finger half-thickness 6.83 mm).** Rest-relative flex (MCP / PIP / DIP) and spread, start values:
| finger | lead (left) | trail (right) |
|---|---|---|
| index | 55 / 75 / 35, spread 0 | 50 / 70 / 30, spread +4 (the "trigger") |
| middle | 65 / 85 / 40 | 65 / 85 / 40 |
| ring | 70 / 90 / 45 | 70 / 90 / 45 |
| little | 75 / 95 / 50 | **40 / 60 / 30** — rides on the lead index, never solved against the club |
| thumb | aim along the shaft toward the head, 1 o'clock; flex 15/10 | aim along the shaft, resting on the lead thumb's trail side; flex 15/10 |
Each wrapped finger then gets one scale factor `k ∈ [0.6, 1.4]` on its triple, bisected so the finger's **closest phalanx segment** sits on the grip surface (segment-to-axis distance = 13.575 + 6.83 mm, ±1.5 mm) with no segment inside it. One parameter per finger, natural ratios, hinge axes fixed — a finger cannot claw.

**3.12.4 Where the shaft is, in hand space — defined, not fitted.** From `reference/GOLF_GRIP_GEOMETRY.html`, in the rest-pose `Hand`-local frame captured in 3.12.2 (so it is the same every frame): lead axis through `LittleProximal + n·(t+r)` and `IndexProximal + u·(0.6·L_prox) + n·(t+r)`; trail axis through `LittleProximal + n·(t+r)` and `IndexProximal + n·(t+r)`; `n` = the stored palm normal, `u` = the stored hand length axis, `t+r = 20.4 mm`. Butt→head direction = little → index. This is the `HandGrabPose`: a wrist anchor **relative to the club**, computed once. Anchor rotation: align the hand-local axis to the club's +Y, then roll about the shaft by the palm rules (lead: back of hand toward `Head`; trail: palm toward `LeftThumbIntermediate`) — evaluated rig-on at address as iter-7 established. Stations: lead so that `LeftHand` projects 10 mm·s down-shaft of `ClubStart`; trail so that `RightLittleProximal` projects at the lead Index/Middle MCP gap. Both anchors are then **data on the prefab** (`GripAnchor_*` return, with `WristTarget` children at the hand-local tunnel point); `Rig_Hands` with the two `TwoBoneIKConstraint`s returns, `targetRotationWeight = 1`. Layer 1 (`GripTarget` = two-hand average) stays as the club carrier.

**3.12.5 Wrists that do not break.** The club has 4 free DOF once the head is on the ball (position along/around the aim, roll). Solve those to **minimise the wrist rotation the IK has to impose** (sum over both hands of `Quaternion.Angle(clipHandRotation, anchorRotation)`) — a coarse grid then a local refine, deterministic, at address. Report the residual per hand; > 40° on either is a stop-and-show (the clip's wrist is being bent, which Cesar allowed "slightly" — not by 90°).

**3.12.6 The staged protocol — every stage ends with frames and a verdict from Cesar before the next starts. A failed stage means the model is wrong at that level; the fix is at that level, never a heuristic above it.**
| Stage | What runs | Frames | Gate |
|---|---|---|---|
| **0 — fist test** | Edit mode, rest pose, no club, no rig. `HandHingeModel` applies **75/95/50** to all four fingers, thumb flexed 30/20, both hands (start value 65/85/40 left the tips 28–41 mm out of the band; **Cesar 2026-09-10: stage 0 PASS, 75/95/50 accepted** — IMPLEMENTER_REPORT § Stage 0). | Both hands, palm side and back side, full res | Looks like a fist. Measured: tips 8–20 mm from the palm plane, adjacent tips ≥ 8 mm apart, no finger crosses another. EditMode tests green. **If this fails, stop — the axes are wrong.** |
| **1 — grip pose in hand space** | Edit mode. Apply 3.12.3; draw a debug cylinder (r = 13.575 mm) on the 3.12.4 axis in hand space. Run the per-finger `k` bisection. | Each hand alone with its cylinder, two angles | Cylinder inside the curled fingers, under the heel pad, thumb along it. Every wrapped finger's closest segment within ±1.5 mm of the surface, nothing inside. |
| **2 — two hands on one club, static** | Play mode, address, `captureDeltaTime = 1/60`, IK on, anchors from 3.12.4, club from 3.12.5. | Down-shaft, target-side, golfer's-eye; full res | §3.9.6-style rows: overlap, heel pad, trail palm on thumb (geometric), no interpenetration, butt cap 8–20 mm·s, wrist residuals reported. **Cesar's eye on the three frames.** |
| **3 — the swing** | One run; contact sheet every 4th frame from address to impact (≈ 18 frames) from two cameras, plus the three samples | Contact sheets + samples | No frame with a finger inside the grip or inside the other hand (measured per frame: min joint-to-axis ≥ r − 1.5 mm; hand-to-hand joints ≥ 8 mm except the trail little finger). Hands on the shaft: tunnel point to axis < 3 mm every frame. Foot slide in band. A0 = 0. |
| **4 — putter** | Stage 2 + 3 on the putt address/stroke | Same | Same |
Stages 0–1 need no play mode and no rig; they are where the model is proven or killed. Stage 2 is the first time the club is involved. Nothing from a later stage is attempted while an earlier one is red.

**3.12.7 Retired by this section:** `ApplyHeldGripPose` and `heldGripPose` (delete the iter-9b path — one solver, not two); `ApplyGripPose`/`WrapJoint` stay for `PfGolfer_Test` only. §3.11.4's "hands are the clip's" acceptance. `ARCHITECT_CLOSEOUT.md` step 1–3 (do not close; do not merge yet). The club mount (§3.11.1) and the two authored slot poses' *face-square roll* are kept; their position is re-solved by 3.12.5.

**3.12.8 The verdict this experiment produces.** At the end of stage 4, a one-page `EXPERIMENT_VERDICT.md`: hours spent per stage, what the pipeline could do alone, what needed Cesar's eye, and the answer to "can this be done with minimum artist intervention" — honest either way.

### 3.6 Harness — `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs`
Add, for the Mixamo-native variant (the Quaternius run keeps its `grip.*` block and its results):
- `grip.hand.onShaft_l` / `grip.hand.onShaft_r` (**redefined 2026-09-09**): distance from the **palm point** `hand.TransformPoint(palmLocal)` (§3.7) — not the bone origin, which scored 0.0000 m with the shaft through the wrist — to the segment `ClubStart → ClubEnd` (world), sampled at address, at t = 0.6 s after commit and at impact (1.167 s, IMPLEMENTER_REPORT §4 of `golfer_3d_test`); the **worst** sample is the value. PASS < 0.035 m.
- `grip.hands.order`: project both hand positions onto the shaft axis; lead (left) must be 0.05–0.12 m nearer the butt cap than trail (right) at address. PASS in range.
- `club.headAtBall`: `|ClubEnd − ball|` in plan at address, on the real transform. PASS < 0.05 m. (This is the assertion `stance.address.clubReachesBall` was assumed to be.)
- `grip.ikNoLegEffect`: foot slide L/R within ±0.010 m of the **rig-off baseline measured under the same harness ordering** (amended 2026-09-10 — the §9.8 numbers 0.0528 / 0.0915 were taken with the tier flip *before* the shot; after the §3.6 tier-restore move they are not comparable). Baseline = one run with `RigBuilder` disabled on the spawned prefab, same hole, same ordering; record it in the JSON header (`baselineSlideL/R`) and in the report. A change in *which* run is the baseline is not a threshold change.
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
~~Authored grip hand pose (fingers)~~ — **in scope since 2026-09-10, §3.8**; left-handed mirror (swap the two anchors + mirror the local offsets); clubface roll assertion; removing the Quaternius finger solver from `GolferPresenter`; Remy's 36,510 tris; anything on `PfGolfer_Test`.

## 6. Acceptance (Implementer fills `IMPLEMENTER_REPORT.md`, PASS/FAIL + one-line evidence)
| # | Check | Pass condition |
|---|---|---|
| A0 | Rig alive (added 2026-09-09) | Zero `InvalidOperationException` from `UnityEngine.Animations.Rigging` in the Console / `Editor.log` for the whole run (Code counts them; the number goes in the report). A run with any is not a run — every other grip number in this table is void without A0. |
| A1 | Package | `com.unity.animation.rigging` in `manifest.json`, project compiles with the define **off** and **on** |
| A2 | Prefab | Hierarchy per §3.2; `RigBuilder.layers` order Grip → Hands; club not under any bone (`object-get-data` or a one-line editor check) |
| A3 | Harness run — ONE, Mixamo-native, Hole 06 (deterministic step) | **§3.11.4**: A0 = 0, `club.headAtBall` < 0.05 m, `club.faceSquare` 90° ± 5°, `grip.ikNoLegEffect` in band, `grip.hand.onShaft_*` informational, all other `grip.*` retired; `grip.hands.order` in band; `club.headAtBall` < 0.05 m; `grip.ikNoLegEffect` in band; every §9.8 PASS still PASS; `budget.tris` still the one FAIL (unchanged, out of scope) |
| A4 | Frames | `evidence/final/` — address, t = 0.6 s, impact **on the gameplay camera at full res** (these are what is judged) plus one scene-cam close-up at address for the record. *(Superseded text follows.)* `evidence/grip/` — address, t = 0.6 s, impact, **on the gameplay camera**, plus close-ups of the hands at **all three** samples labelled as such — **full-resolution PNG crops, ≥ 600 px across the hands, from two angles (down the shaft from the butt, and from the target side)**; the Architect reviews the PNGs, never a compressed copy, and nothing is called done before Cesar has seen them. **Note (2026-09-10):** at impact the gameplay camera has already cut to the ball (§9.2 deferred launch), so the impact close-up must come from a second camera — an editor `Camera.Render` into a RenderTexture at the moment of the impact sample, labelled `scene-cam`; this is the one sanctioned second capture path. Side-by-side with `golfer_3d_test/evidence/9_8/mixamo_*.png` (before) |
| A5 | Define off | EditMode sweep green; `git diff --stat` of shipped (non-`_Test`, non-`#if`) code is empty except `manifest.json` / `packages-lock.json` |
| A6 | Profile | active build profile restored to **`iOS-Full-GPS`** before the final commit, stated in the report |
| A7 | Numbers in report | both authored local poses (§3.4), the three grip samples per hand, **the `n_out` verification dot per hand, the 21-joint wrap log (§3.10.2), `A/B` and `A'/B'` in hand space, the two anchor rotations with the two palm-rule dot products, the trail station, the eight wrap results per hand (angle per joint, tip-to-axis), the thumb clock angle, three rig-on foot-slide R values under the fixed step**, the club scale (0.86880 — a stand-in artefact of the 1.328 m character, NOTE in `CHARACTER_3D_REMAKE_OPTIONS.md` §7: roster models at R2 height carry full-size clubs), the Animation Rigging package version, the A0 count |

Exit: A1–A7 → `STATUS.md` = `READY_FOR_SELF_REVIEW`.

## 7. Files this task touches
`Packages/manifest.json`, `Packages/packages-lock.json`, `…/GolferTest/PfGolfer_MixamoNative.prefab`, `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` (field promotion only), `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` (four assertions + three header fields), `Docs/Specs/Active/golfer_club_grip/*`, `Docs/AI_CONTEXT.md`.

## 8. Backlog rows added this session
See `Docs/GPS/GPS_BACKLOG.md`: authored grip hand pose · left-handed mirror · clubface roll · finger-solver removal · Remy tri budget. The old row "Real club grip — club-in-hand mocap" is **taken up by this spec and deleted** (the §9.8 finding is that the clips were never the problem).
