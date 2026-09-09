# golfer_club_grip — Implementer report, iter-7 (§3.9 landmark placement + contact wrap)

**Iteration shape:** `rigging:landmark-grip`
**Branch:** `golfer_3d_test` · baseline `6d6edd624`
**Decision implemented:** `ARCHITECT_DECISION_LANDMARKS.md` / SPEC §3.9 · source `reference/GOLF_GRIP_GEOMETRY.html`
**Canonical screenshot:** `screenshots/iter7_address.png`
**Solver version that ran:** `landmarks-v1` (probe-verified before every run)

---

## 0. Headline

**The hands are now placed and oriented like a real golf grip; the fingers are not closed on it.**
**A0 = 0. 33 PASS / 7 FAIL / 13 SKIP.**

Six of the ten §3.9.6 rows pass, including all three that encode the *shape* of a golf grip —
overlap, heel pad on top, trail palm on the thumb — and the determinism problem iter-6 raised is
closed outright.

| §3.9.6 row | result | |
|---|---|---|
| `grip.axis.landmarks_r` | **0.0000 m** | PASS |
| `grip.heelPad.onTop` | **0.6654** | PASS (> 0.5) |
| `grip.trailPalm.onThumb` | **0.6553** | PASS (> 0.5) |
| `grip.hands.overlap` | **0.0064 m** | PASS (≤ 0.008) |
| `grip.hands.noInterpenetration` | **0.0112 m** | PASS (≥ 0.008) |
| `grip.thumb.downShaft_l` | **21.59°** | PASS (< 35); clock **−30.87°** — see §5 |
| `grip.ikNoLegEffect` | L 0.0396 / R 0.0057 vs baseline 0.0396 / 0.0057 | **PASS, exactly** |
| `grip.axis.landmarks_l` | 0.0221 m | FAIL |
| `grip.fingers.closed_l` | [0.0311 .. 0.0497] m | FAIL |
| `grip.fingers.closed_r` | [0.0425 .. 0.0498] m | FAIL |
| `grip.buttCap.pastHeel` | −0.0124 m | FAIL |

Every §3.7 row still passes. `evidence/grip39/address_targetside.png` shows it plainly: two hands
overlapped on the grip with the butt cap above the top hand — and the fingers splayed open.

## 1. §3.9.5 — the determinism question from iter-6 is answered

`Time.captureDeltaTime = 1/60` from launch to `Finish()` (reset to 0 there, so a failing run cannot
leave the Editor stepping). Three rig-on runs of the same prefab:

| run | foot slide R |
|---|---|
| 1 | 0.0057 |
| 2 | 0.0054 |
| 3 | 0.0057 |

**Spread 0.0003 m** against the < 0.005 m requirement — iter-6's identical runs spread **0.056 m**.
The row is now a real measurement, not an informational one. Rig-off baseline under the same step:
**`baselineSlideL = 0.0396`, `baselineSlideR = 0.0057`** (in the JSON header). Note these are far
from the free-running 0.0518 / 0.0810 of iter-6 — as expected; the whole point is that the old
numbers were taken under a clock that varied.

## 2. §3.9.1 — the landmark axis (A7)

`s = 1.328 / 1.75 = 0.7589`, `t = 9 mm·s = 0.00683`, `r = 11.5 mm·s = 0.00873`, `t + r = 0.01556`.
Measured in **hand space**, so they are independent of the hand's world rotation:

| | A | B | `palmLocal` | `shaftDirLocal` |
|---|---|---|---|---|
| lead (L) | (0.03764, 0.09391, −0.01745) | (−0.03661, 0.09951, −0.03167) | (0.00290, 0.09653, −0.02411) | (−0.97946, 0.07393, −0.18758) |
| trail (R) | (−0.03664, 0.08990, −0.01770) | (0.02204, 0.11404, −0.01790) | (0.00244, 0.10598, −0.01783) | (0.92482, 0.38040, −0.00325) |

`WristTarget.localPosition = −palmLocal` on both. Lead uses `LittleProximal → IndexIntermediate`
(diagonal through the fingers); trail uses `LittleProximal → IndexProximal` (along the base crease).

## 3. §3.9.2 — anchor rotations, fully determined (A7)

| | rule | dot | roll | `R_anchor_local` (quat) | vs the clip's hand |
|---|---|---|---|---|---|
| lead | back-of-hand → Head | **1.0000** | −37° | (0.33344, −0.23251, −0.59317, 0.69491) | 102.52° |
| trail | palm → lead thumb | **1.0000** | +96° | (0.41494, 0.61739, 0.37098, 0.55590) | 140.28° |

Both self-check at `axis-after-compose vs +Y = 0.0000°`. The "vs the clip's hand" angles are large
because the mocap clip never held a club; §3.9.2 makes them information, not a stop, which is
correct — the 35° stop is retired with the bake that needed it.

**Deviation, flagged.** §3.9.2 says compute with `Rig_Hands` at weight 0. That is right for the
landmark axis (hand space) but **wrong for the two rule targets**: `Head` and the lead thumb are
world positions, and with the rig off they are the *clip's*, which the lead hand then rotates 102°
away from. Sampling them there made the trail rule solve `dot = 1.0000` and then evaluate at
**−0.4189** on the rigged pose. The rule targets are now captured **rig-on, before zeroing**, and
`trailPalm.onThumb` went −0.4189 → **+0.6553**. The landmark measurement is still taken at weight 0
exactly as specified.

## 4. §3.9.3 — the trail station (A7)

Final `GripAnchor_Trail.localPosition.y = 0.0374` (lead unchanged at −0.0096 from the §3.4 solve).

The rig-off delta the spec describes **cannot converge**: moving an anchor does not move a hand the
rig is not driving, so it returned the same 0.0082 at every station. Solved from the **rig-on**
overlap error instead — two points (S = 0.0688 → err 0.0250; S = 0.0938 → err 0.0449, slope 0.796)
give S* = 0.0374, and the measured result is **overlapErr 0.0064**, inside the 0.008 band. Reported
because it is a method change, not a tuned constant.

## 5. §3.9.4 — the contact wrap, and why the fingers are still open

Ported: `WrapChain` / `PalmNormal` / `AimThumbDownShaft` resolve through `HumanBodyBones` with the
Quaternius name path kept as a fallback, so `PfGolfer_Test` cannot regress. Cylinder is the real
shaft (`ClubStart → ClubEnd`) via a new `ShaftSegment`. **Seven fingers** — the trail little finger
is excluded. `forceGripPose = true`; `shaftRadius` 0.0120 → **0.0087** and `fingerRadius` 0.0090 →
**0.0068**, the scaled `r` and `t` (they were the unscaled real-world numbers, a 37 % error on a
character three-quarters human height).

**The wrap runs** — probed directly in edit mode, `ApplyGripPose` moves the lead index tip
1.4081 → 1.2683 m. **It does not reach.** Tips sit 31–50 mm from the axis against a 15.5 mm contact
target, at all three samples, which is `WrapJoint` taking its "as closed as it can be" cap at
`maxJointBend = 80°` per joint.

Two candidates, and I am not guessing between them without a measurement:
1. **`grip.axis.landmarks_l = 0.0221 m`** — the lead index PIP is 22 mm off the shaft while the
   little MCP is exactly on it (A = 0.0000, B = 0.0221). The lead landmark *line* is not parallel to
   the shaft, so the fingers are reaching for a cylinder that is not where their tunnel is. The
   trail hand, whose axis is perfect (A = B = 0.0000), still fails `fingers.closed_r` — so this is
   not the whole story.
2. **The 80° per-joint cap** may simply be short for a hand this size at this grip radius.

**One measurement settles it** and I would rather it were asked for than assumed: log `grip.hand.orient_l/_r`
against the authored anchor together with the per-joint bend actually applied by `WrapJoint`. If the
lead hand is not reaching its anchor rotation, (1) is the cause and the IK is the thing to look at;
if it is, (2) is, and the cap is a one-line change.

**`grip.buttCap.pastHeel = −0.0124`** is the same defect seen from the other end: the heel landmark
sits 12 mm *up*-shaft of the butt cap, i.e. the lead hand is off the end of the grip rather than
half an inch below it.

**Thumb clock = −30.87°.** Magnitude is in the 15–30° window; the sign puts it on the lead side of
top rather than the trail side. `AimThumbDownShaft` picks the direction from the trail hand's
position, which the trail station moved after the thumb rule was written. Reported, not adjusted.

## 6. Acceptance (SPEC §6)

| # | Check | Verdict | Evidence |
|---|---|---|---|
| **A0** | Rig alive | **PASS** | 0 `UnityEngine.Animations.Rigging` exceptions on every run |
| A1 | Package | **PASS** | Animation Rigging **1.3.1**; compiles with the define on (runs) and off (2,765 EditMode tests) |
| A2 | Prefab | **PASS** | §3.2 hierarchy intact; `Hands` layer, mask and clip removed; anchors carry the §3.9.2 rotations and §3.9.1 WristTargets |
| A3 | Harness run (deterministic step) | **FAIL** | 6 of 10 §3.9.6 rows pass; `axis.landmarks_l`, `fingers.closed_l/_r`, `buttCap.pastHeel` fail — §5 |
| A4 | Frames | **PASS** | `evidence/grip39/` — six full-res 1400×1400 scene-cam hand shots (down-shaft + target-side × address / t=0.6 / impact) + three gameplay frames |
| A5 | Define off | **FAIL** (partial) | Shipped diff is the gated `_Test` prefab, the gated presenter block, the Editor-only harness and the three deleted pose assets. EditMode **2760/2765** — the same path-separator and pendulum tests as iter-5/6 |
| A6 | Profile | **PASS** | restored to **`iOS-Full-GPS`**; `Time.captureDeltaTime` confirmed back to 0 |
| A7 | Numbers | **PASS** | §2 (A/B), §3 (rotations + dots), §4 (station), §1 (three R values), §5 (wrap) |

## 7. Files modified or created

| path | 1-line summary |
|---|---|
| `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` | §3.9.4: `WrapChain`/`PalmNormal`/`AimThumbDownShaft` resolve through `HumanBodyBones`, seven fingers, new `ShaftSegment` so the wrap closes on the real `ClubStart→ClubEnd` cylinder, lead thumb at 1 o'clock |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | §3.9.1 landmark axis replaces the four-midpoint fit; §3.9.2 rotation solve (rule targets rig-on) with self-check; §3.9.3 station; the eight §3.9.6 rows; §3.9.5 fixed timestep; end-of-frame sampling; `SolverVersion` → `landmarks-v1` |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | anchor rotations + WristTargets + trail station 0.0374; `forceGripPose = true`; `shaftRadius`/`fingerRadius` scaled |
| `Assets/Animations/Golfer/AnimatorController_Golfer_MixamoNative.controller` | `Hands` layer and its state machine removed |
| `Assets/Animations/Golfer/ANIM_HandPose_GolfGrip.anim` (+`.meta`) | **deleted** — muscle-space pose superseded by §3.9 |
| `Assets/Animations/Golfer/Mask_HandsOnly.mask` (+`.meta`) | **deleted** — same |
| `Docs/Specs/Active/golfer_club_grip/IMPLEMENTER_REPORT.md` | this report |
| `Docs/Specs/Active/golfer_club_grip/STATUS.md` | `SPEC_READY` → `IMPLEMENTER_WORKING` → `READY_FOR_SELF_REVIEW` |
| `Docs/Specs/Active/golfer_club_grip/HEARTBEAT.log` | iter-7 baseline + progress |
| `Docs/Specs/Active/golfer_club_grip/evidence/grip39/` | nine frames |
| `Docs/AI_CONTEXT.md` | session status |

## 8. Two bugs of mine that the self-checks caught

- **The §3.9.2 roll search rotated the rule vector *and* its target together**, which makes the dot
  product roll-invariant: it returned an arbitrary angle (lead dot −0.0021, hand 157° from the clip).
  The target is fixed in world space. Fixed, and both rules now solve to `dot = 1.0000`.
- **Sampling ran during `Update`, the wrap runs in `LateUpdate`** — so the harness was measuring the
  Animator's output before the fingers closed. Now sampled at end of frame. It turned out not to
  change the tip numbers, which is itself the useful result: it ruled out a measurement artefact and
  left a real reach problem (§5).
