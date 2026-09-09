# golfer_club_grip — Implementer report, iter-6 (§3.8 finger pose)

**Iteration shape:** `rigging:finger-pose`
**Branch:** `golfer_3d_test` · baseline `365332ab9`
**Decision implemented:** `ARCHITECT_DECISION_FINGERS.md` / SPEC §3.8
**Canonical screenshot:** `screenshots/iter6_address.png`
**Solver version that ran:** `finger-pose-v1` (verified loaded by reflection before every run)

---

## 0. Headline — one thing built, one thing stopped

The pose layer works and moved the grip a long way. **But §3.8.3's roll correction is 36.44° / 53.74°,
over the 35° stop, and `r_curl` cannot reach its 0.018 ceiling on this rig.** Per §3.8.3 and the
kickoff ("> 35 deg is a stop") I **did not author the composed anchor rotation**. The prefab still
carries the iter-5 §3.7 rotations; nothing is built on a number the spec says to stop at.

The stop has a measured mechanism, not just a threshold breach — §2.

| §3.8.5 row | iter-5 (no pose) | now |
|---|---|---|
| `grip.fingers.closed_l` | [0.0293 .. 0.0456] | [0.0213 .. **0.0384**] FAIL |
| `grip.fingers.closed_r` | [0.0288 .. 0.0454] | [**0.0146** .. 0.0329] FAIL |
| `grip.shaft.inTunnel_l` | palm 0.0248 | palm **0.0240** (ceiling is 0.024) FAIL |
| `grip.shaft.inTunnel_r` | palm 0.0139 | **PASS** — palm 0.0141, tip gap 0.0185 |
| `grip.hands.noOverlap` | 0.0094 | **0.0097** FAIL (want ≥ 0.010) |
| `grip.thumb.downShaft_l` | — | **24.40° PASS** |

Every §3.7 row still passes: `onShaft_l/_r` 0.0000 / 0.0000, `orient_l/_r` 0.0000° / 0.0000°,
`hands.apart` 0.0703, `hands.order` 0.0976, `club.headAtBall` 0.0085. **A0 = 0** on every run.
**32 PASS / 6 FAIL / 9 SKIP.**

---

## 1. The pose asset (§3.8.1) — 40 muscle values (A7)

**Deviation, flagged:** §3.8.1 names VoxHands / HumanoidHandPoseHelper as the authoring path. Both
exist only to *emit* a Humanoid muscle clip; I wrote the clip directly with `AnimationClip.SetCurve`
using the binding names derived from `HumanTrait.MuscleName`. Identical artifact, no third-party
package in the repo, smaller diff. If you want the sliders for interactive tweaking that is a
separate ask and I have not made that call for you.

`Assets/Animations/Golfer/ANIM_HandPose_GolfGrip.anim` — one frame, `isHumanMotion = True`,
40 curves, looping:

| muscle | value |
|---|---|
| `{Left,Right}Hand.{Index,Middle,Ring,Little}.{1,2,3} Stretched` | **−1.00** (24 curves) |
| `{Left,Right}Hand.{Index,Middle,Ring,Little}.Spread` | **0.00** (8 curves) |
| `{Left,Right}Hand.Thumb.1 Stretched` | **−0.40** |
| `{Left,Right}Hand.Thumb.Spread` | **−0.60** |
| `{Left,Right}Hand.Thumb.2 Stretched` | **−0.40** |
| `{Left,Right}Hand.Thumb.3 Stretched` | **−0.30** |

**Adjustments used: 2 of the 3 allowed**, both driven by measurement rather than by eye:
0. start values from §3.8.1 (−0.55 / −0.75 / −0.65) → `r_curl` 0.0335 / 0.0342
1. tighten to −0.80 / −0.95 / −0.90 → `r_curl` 0.0313 / 0.0329 (2 mm)
2. end of range −0.95 / −1.00 / −1.00 → knuckle→tip 0.0684 → **0.0674 (1 mm)**

A range diagnostic (not an adjustment) then swept the whole muscle range — §2.

**§3.8.2 applied:** `AnimatorController_Golfer_MixamoNative` layer **`Hands`**, Override, weight 1,
mask `Mask_HandsOnly.mask` (mask bytes 7 and 8 only = LeftFingers + RightFingers), one looping state
`GolfGrip`. Confirmed live at runtime: `layers: 0:Base Layer w=1.00  1:Hands w=1.00`. No code.

## 2. The stop, with its mechanism (§3.8.3)

I did not accept "over threshold" as the finding — the threshold breach has a cause, and it is the
avatar, not the pose.

**The clip works.** Sampling it in edit mode rotates `LeftIndexProximal` from identity to −71° and
shortens knuckle→tip 0.0862 → 0.0684. **The muscle range works too** — sweeping it end to end:

| finger `Stretched` | index1 local euler | knuckle→tip |
|---|---|---|
| +1.00 (open) | (16.3, 3.7, 9.2) | 0.0860 |
| 0.00 | (327.0, 355.3, 10.5) | 0.0823 |
| −0.50 | (302.6, 347.1, 16.5) | 0.0761 |
| −1.00 (max curl) | (281.0, **307.9**, **53.7**) | **0.0674** |

**At full curl the joint carries −52° of yaw and +54° of roll alongside the −79° of flexion.** The
fingers splay sideways as they bend instead of curling into the palm. Consequences, all three
measured:

- knuckle→tip only shortens 22 % across the entire muscle range, so **`r_curl` floors at ≈ 0.030**
  (it is ≈ |knuckle→tip| / 2 by construction) against an 0.018 ceiling — unreachable on this rig.
- the four finger midpoints therefore do not line up across the hand, so the fitted tunnel is
  skewed → **roll correction 36.44° (lead) / 53.74° (trail)**, over the 35° stop.
- the tips end up 21–38 mm from the shaft axis instead of 8–24 mm → `grip.fingers.closed_*` fail.

**Root cause candidate, checkable:** every finger bone in the avatar has `useDefaultValues = True`
with `min = max = (0,0,0)` — the finger axes on this Mixamo auto-generated avatar were never
configured. Muscle space cannot produce a clean curl through an unconfigured axis.

**Two routes out, both the Architect's to pick.** (a) Configure the finger muscle axes in the
Avatar and re-run — muscle space then works and every roster model still inherits the clip.
(b) Take the other industry path `ARCHITECT_DECISION_FINGERS.md` itself lists: author the pose as
**bone rotations** on a posed hand clone rather than in muscle space, which sidesteps the avatar
entirely at the cost of being per-rig.

**Measured §3.8.3 values (A7)** — reported, not authored:

| | lead (L) | trail (R) |
|---|---|---|
| `r_curl` | 0.0302 | 0.0324 |
| `palmLocal` | (0.01014, 0.08846, −0.02191) | (−0.00708, 0.08054, −0.02214) |
| `shaftDirLocal` | (0.93894, −0.33660, 0.07141) | (−0.97756, −0.18038, 0.10880) |
| roll correction | **36.44°** | **53.74°** |
| `WristTarget.localPosition` (= −`palmLocal`) | (−0.01014, −0.08846, 0.02191) | (0.00708, −0.08054, 0.02214) |
| palm width | 0.0663 | 0.0634 |

The compose now self-checks: **`VERIFY tunnel-after-compose vs +Y = 0.0000°`** on both hands. That
line exists because my first version of this roll had a spurious `Inverse(rLocal)` and reported a
false 51.97° / 35.25°; the fix is in the diff and the self-check makes a repeat impossible to miss.

## 3. §3.8.4 — authored (this one the stop does not block)

Lead station unchanged from the §3.4 solve at **−0.0096**. Lead palm width `|index1 − pinky1|` at the
posed frame = **0.0663**. Trail station **0.0599 → 0.0607** (= lead + palm width + 0.004), replacing
the reach-balance station. `grip.hands.noOverlap` moved 0.0094 → **0.0097**; still 0.3 mm under the
0.010 floor, because the splayed fingers of §2 stick out sideways into the other hand.

## 4. `grip.ikNoLegEffect` — the baseline is measured, and the row is not repeatable

**Rig-off baseline run done as §3.6 requires** (RigBuilder disabled on the spawned prefab, same hole,
same ordering): **`baselineSlideL = 0.0518`, `baselineSlideR = 0.0810`**, both now in the JSON header.
Against the old §9.8 figure of 0.0915 this confirms the Architect's call exactly — the harness
ordering moved the number, not the rig.

**But three rig-on runs of an effectively identical prefab gave R = 0.0279, 0.0843, 0.0338.** A
spread of 0.056 m against a ±0.010 m band. The row passed on one of those runs and failed on the
other two; the reported run has **L 0.0479 / R 0.0338 → FAIL**. This is not a leg effect and not a
baseline error: **the foot-slide measurement is not repeatable at its own threshold.** I have not
widened the band. Either the measurement needs to be made deterministic (it samples peak-to-peak
over 2.5 s of a physics-driven swing) or the row needs a tolerance derived from its own variance —
Architect's call, and worth doing before this row is trusted either way.

## 5. Acceptance (SPEC §6)

| # | Check | Verdict | Evidence |
|---|---|---|---|
| **A0** | Rig alive | **PASS** | 0 `UnityEngine.Animations.Rigging` exceptions on every run |
| A1 | Package | **PASS** | Animation Rigging **1.3.1** (Registry); compiles with the define on (runs) and off (2,765 EditMode tests) |
| A2 | Prefab | **PASS** | §3.2 hierarchy intact; `Hands` layer live at weight 1 with the fingers-only mask; anchors/WristTargets as iter-5 plus the §3.8.4 trail station |
| A3 | Harness run | **FAIL** | `fingers.closed_l/_r`, `shaft.inTunnel_l`, `hands.noOverlap`, `ikNoLegEffect` fail; `inTunnel_r`, `thumb.downShaft_l` and every §3.7 row pass. §3.8.3 stop declared |
| A4 | Frames | **PASS** | `evidence/grip38/` — six full-res 1400×1400 PNG hand shots (down-the-shaft + target-side × address / t=0.6 / impact) from the sanctioned scene-cam, plus the three gameplay frames. No compressed copy is the deliverable |
| A5 | Define off | **FAIL** (partial) | Diff outside `Docs/` is three gated files (below). EditMode **2760/2765** — the same path-separator and pendulum tests as iter-5 |
| A6 | Profile | **PASS** | restored to **`iOS-Full-GPS`** before the close-out commit |
| A7 | Numbers | **PASS** | §1 (40 muscles), §2 (`r_curl` / `palmLocal` / `shaftDirLocal` / roll), §3 (palm width, trail station), §4 (baselines) |

## 6. Files modified or created

| path | 1-line summary |
|---|---|
| `Assets/Animations/Golfer/ANIM_HandPose_GolfGrip.anim` | NEW — one-frame Humanoid muscle clip, 40 finger muscles, looping (§3.8.1) |
| `Assets/Animations/Golfer/Mask_HandsOnly.mask` | NEW — avatar mask, LeftFingers + RightFingers only (§3.8.2) |
| `Assets/Animations/Golfer/AnimatorController_Golfer_MixamoNative.controller` | `Hands` layer added: Override, weight 1, mask, one looping `GolfGrip` state |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | §3.8.4 trail station 0.0599 → 0.0607 only; anchor rotations deliberately NOT touched (§3.8.3 stop) |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | §3.8.3 tunnel fit + roll compose with self-check; the four §3.8.5 assertions; rig-off baseline run + `baselineSlideL/R` in the JSON; scene-cam capture; `SolverVersion` → `finger-pose-v1` |
| `Docs/Specs/Active/golfer_club_grip/IMPLEMENTER_REPORT.md` | this report |
| `Docs/Specs/Active/golfer_club_grip/STATUS.md` | `SPEC_READY` → `IMPLEMENTER_WORKING` → `READY_FOR_SELF_REVIEW` |
| `Docs/Specs/Active/golfer_club_grip/HEARTBEAT.log` | iter-6 baseline + progress |
| `Docs/Specs/Active/golfer_club_grip/evidence/grip38/` | six full-res hand shots + three gameplay frames |
| `Docs/Specs/Active/golfer_club_grip/evidence/pose/pose0_hands.png` | pose pass 0, for the adjustment trail |
| `Docs/AI_CONTEXT.md` | session status |

## 7. Traps hit again, and what caught them

- **My own roll maths was wrong** and reported a false stop before the self-check existed. Every
  derived rotation in this file now re-derives its own result and prints the error.
- **`script-execute`'s Roslyn does not inherit project defines**, so an `#if GOLFIN_GOLFER_TEST`
  guard inside it is *always* false. The prefab-write guard reflects for a gated member instead, and
  additionally verifies the gated `GolferPresenter` fields are visible before saving.
- **Unity only auto-imports on Editor focus**, so every `.cs` edit is followed by
  `AssetDatabase.Refresh(ForceUpdate)` and a `SolverVersion` probe. It caught a stale assembly again
  (a `Tf` → `Fb` slip).
- **The first scene-cam framing put the camera inside the hand** at 0.42 m broadside. Caught by
  looking at the PNG before shipping it, which is the whole point of A4's full-res rule.
