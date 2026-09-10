# golfer_club_grip — Implementer report, iter-8 (§3.10 handedness, wrap sign, landmark fixes)

**Iteration shape:** `rigging:handedness-sign`
**Branch:** `golfer_3d_test` · baseline `7e5f546ce`
**Decision implemented:** `ARCHITECT_REVIEW_ITER7.md` / SPEC §3.10
**Canonical screenshot:** `screenshots/iter8_address.png`
**Solver version that ran:** `handedness-v1` (probe-verified before every run)

---

## 0. Headline — the diagnosis was right, and the row count still went backwards

**A0 = 0. 31 PASS / 9 FAIL / 13 SKIP — against iter-7's 33 / 7.** I am leading with that rather
than with the wins, because it is the number that matters and it moved the wrong way.

**The §3.10.1 sign diagnosis is confirmed by measurement.** The `n_out` verification passes on both
hands (lead **0.9661**, trail **0.2722** — a +5° flex moves the tip along +`n_out`), and the lead
hand now visibly wraps the shaft (`evidence/grip310/address_targetside.png`). Three signs that were
wrong are now right:

| | iter-7 | iter-8 |
|---|---|---|
| lead fingertips | [0.0311 .. 0.0497] | **[0.0156 .. 0.0416]** — min exactly at the 0.0156 contact target |
| thumb clock | −30.87° (wrong side of top) | **+11.75°** (right side; want +15…+30) |
| `buttCap.pastHeel` | −0.0124 (hand off the END of the grip) | **+0.0355** (right sign, overshoots) |
| §3.4 REACH slack | lead 0.0318 / trail −0.0159 | **lead 0.0119 / trail 0.0199 — both positive** |
| §3.10.4 lead station | — | **converged exactly: delta 0.0000** |

**What regressed, and why.** `hands.overlap` 0.0064 → 0.0231, `hand.onShaft_r` 0.0000 → 0.0457,
`fingers.closed_r` [0.0425..0.0498] → [0.0734..0.0795]. All of it follows from one thing: **§3.10.4
moved the lead station 48 mm up the grip, and the two stations are coupled through the shaft.** I
then chased the trail station to compensate, which is the wrong method — see §4.

## 1. §3.10.1 — the palm normal, verified not assumed (A7)

`n_out` = `cross(along, across)` for the left hand, **negated for the right**;
`across = LittleProximal − IndexProximal`, `along = mid(Index/Little Proximal) − Hand`.
One function, used by `PalmNormal`, the §3.9.1 offset, both §3.9.2 rules and `AimThumbDownShaft`.

**Verification (SPEC §3.10.1): lead dot = 0.9661, trail dot = 0.2722.** Both > 0, so the convention
is right for this rig. Stable across runs (0.9658 / 0.2737 on the second pass). The trail value is
weaker because the right middle MCP's bend axis is less aligned with the palm normal on this rig —
the *sign* is what the test is for, and it is unambiguous.

This is the fix the Architect identified: the old normal was mirror-antisymmetric, so on the trail
hand it pointed out of the **back**, which (a) put the §3.9.1 shaft offset on the back of the MCP
row and (b) let `WrapJoint`'s distance heuristic choose **extension**. Both are gone.

## 2. §3.10.2 — the wrap only flexes, and the 21-joint log (A7)

`WrapJoint` no longer infers a direction: `+bend` is flexion, always. The "reduce distance to the
shaft" test and the "make a fist" branch are deleted. Caps 90 / 90 / 70.

**The full log at address — this is the measurement iter-7 asked for, and it is the most useful
thing in this report:**

```
R.IndexProximal      applied=90.0 cap=90 d0=0.0182 dMax=0.0222 after=0.0222
R.IndexIntermediate  applied=90.0 cap=90 d0=0.0463 dMax=0.0498 after=0.0498
R.IndexDistal        applied=70.0 cap=70 d0=0.0678 dMax=0.0710 after=0.0710
R.MiddleProximal     applied=16.3 cap=90 d0=0.0203 dMax=0.0141 after=0.0156   <- reaches contact
R.MiddleIntermediate applied=90.0 cap=90 d0=0.0221 dMax=0.0435 after=0.0435
R.MiddleDistal       applied=70.0 cap=70 d0=0.0469 dMax=0.0723 after=0.0723
R.RingProximal       applied=29.2 cap=90 d0=0.0231 dMax=0.0143 after=0.0156   <- reaches contact
R.RingIntermediate   applied=90.0 cap=90 d0=0.0291 dMax=0.0441 after=0.0441
R.RingDistal         applied=70.0 cap=70 d0=0.0635 dMax=0.0683 after=0.0683
L.IndexProximal      applied=90.0 cap=90 d0=0.0322 dMax=0.0203 after=0.0203
L.IndexIntermediate  applied=87.0 cap=90 d0=0.0415 dMax=0.0150 after=0.0156   <- reaches contact
L.IndexDistal        applied= 0.0 cap=70 d0=0.0078 dMax=0.0294 after=0.0078   <- already touching
L.MiddleProximal     applied=90.0 cap=90 d0=0.0241 dMax=0.0284 after=0.0284
L.MiddleIntermediate applied=33.8 cap=90 d0=0.0304 dMax=0.0110 after=0.0155   <- reaches contact
L.MiddleDistal       applied= 0.0 cap=70 d0=0.0117 dMax=0.0193 after=0.0117   <- already touching
L.RingProximal       applied= 0.0 cap=90 d0=0.0124 dMax=0.0386 after=0.0124   <- already touching
L.RingIntermediate   applied=90.0 cap=90 d0=0.0157 dMax=0.0323 after=0.0323
L.RingDistal         applied=70.0 cap=70 d0=0.0281 dMax=0.0545 after=0.0545
L.LittleProximal     applied= 0.0 cap=90 d0=0.0128 dMax=0.0201 after=0.0128   <- already touching
L.LittleIntermediate applied=90.0 cap=90 d0=0.0287 dMax=0.0275 after=0.0275
L.LittleDistal       applied=70.0 cap=70 d0=0.0450 dMax=0.0510 after=0.0510
```

**Read it by comparing `d0` with `dMax`.** On 13 of 21 joints `dMax > d0` — flexing to the cap moves
the tip **further from the shaft**, so the joint takes the cap and ends up worse than it started
(R.Index 0.0182 → 0.0222; L.Ring 0.0157 → 0.0323). On 5 joints flexion reaches contact cleanly
(16°, 29°, 87°, 34° — real solved angles, not caps). On 4 the tip was already touching.

**That pattern is not a cap problem and not a sign problem** — the sign is verified, and the joints
that can reach do reach in a few tens of degrees. It says the shaft is **inside the arc those
fingers sweep**: flexing carries the tip around and away rather than onto it. That is a placement
question, which is exactly what §3.10.2 predicted the row would report.

## 3. §3.10.3 / §3.10.5 / §3.10.6

- **§3.10.3 wrap-invariant lead B** implemented (`IndexProximal + u·0.6·L_prox + n_out·(t+r)`).
  `grip.axis.landmarks_l` = **0.0204** (was 0.0221 against the moving PIP). A and B now report the
  *same* error, which is the point — the line is consistently offset rather than sheared by the wrap.
  Still over the 0.006 gate. `grip.axis.landmarks_r` = **0.0014 PASS**.
- **§3.10.5 clock sign** from the trail palm centre: **−30.87° → +11.75°.** Sign fixed; magnitude is
  3° under the +15…+30 window.
- **§3.10.6 geometric trail-palm** implemented and it **fails where the dot product passed** —
  thumb sits 0.0740 m outside the trail palm plane (want 0 … 0.0190) while the shaft is 0.0143 m
  out, i.e. the thumb is further out than the shaft. The dot product read 0.6459 and called that
  fine. The stricter test is doing its job; this is a row that was passing on a weak criterion.

## 4. The coupling I did not solve, stated plainly

§3.10.4 converged **exactly** — `LeftHand` projects 0.0076 m down-shaft of `ClubStart`, want 0.0076,
delta 0.0000 — and it fixed `buttCap.pastHeel`'s sign. But it moved the lead anchor from −0.0096 to
**+0.0288**, 48 mm up the grip, and the trail station is defined *relative to the lead hand*
(§3.9.3: the trail little MCP rides the lead index/middle gap). I moved the trail to compensate by
preserving the 0.0470 offset that had measured well. It did not hold: `overlapErr` 0.0231.

**Both stations cannot be chased one at a time.** Every lead move invalidates the trail fit and vice
versa, and I made five authoring passes this round discovering that. The two rows now trade directly
against each other:

| | lead station | `hands.overlap` | `buttCap.pastHeel` |
|---|---|---|---|
| iter-7 | −0.0096 | **0.0064 PASS** | −0.0124 FAIL |
| iter-8 (§3.10.4) | +0.0288 | 0.0231 FAIL | +0.0355 FAIL (right sign) |

I did **not** silently revert to iter-7's stations to buy back the row count — §3.10.4 is the spec
and its rule converged. What this needs is a **joint solve** for the two stations against both
constraints at once, not alternating one-variable corrections. That is an Architect decision about
method, and it is the single thing standing between the verified §3.10 fixes and a passing grip.

## 5. Acceptance (SPEC §6)

| # | Check | Verdict | Evidence |
|---|---|---|---|
| **A0** | Rig alive | **PASS** | 0 `UnityEngine.Animations.Rigging` exceptions, every run |
| A1 | Package | **PASS** | Animation Rigging 1.3.1; compiles define-on (runs) and define-off (2,765 tests) |
| A2 | Prefab | **PASS** | anchors carry the re-solved §3.9.2 rotations, §3.10.4 lead station, §3.9.3 trail station |
| A3 | Harness run (fixed step) | **FAIL** | 31 PASS / 9 FAIL. Passing: `axis.landmarks_r`, `heelPad.onTop`, `thumb.downShaft_l`, `hand.orient_l/_r`, `hand.onShaft_l`, `ikNoLegEffect`. Failing: `axis.landmarks_l`, `fingers.closed_l/_r`, `hands.overlap`, `hands.noInterpenetration`, `trailPalm.onThumb`, `buttCap.pastHeel`, `hand.onShaft_r`, `budget.tris` |
| A4 | Frames | **PASS** | `evidence/grip310/` — six full-res 1400×1400 scene-cam hand shots + three gameplay frames |
| A5 | Define off | **FAIL** (partial) | Diff is the gated `_Test` prefab, the gated presenter block and the Editor-only harness. EditMode **2760/2765** — the same two long-standing failures |
| A6 | Profile | **PASS** | `iOS-Full-GPS`; `Time.captureDeltaTime` confirmed back to 0 |
| A7 | Numbers | **PASS** | §1 (n_out dots), §2 (21-joint log), §3, §4 (stations), §6 |

## 6. Other numbers (A7)

- **Anchor rotations re-solved with `n_out`**, both rules `dot = 1.0000`:
  lead `(0.17575, −0.35814, −0.40300, 0.82367)` roll −47°; trail `(0.37027, 0.55049, 0.41557, 0.62222)` roll +83°.
- **Stations:** lead **+0.0288** (§3.10.4), trail **+0.0758**.
- **`grip.ikNoLegEffect` PASS** — L 0.0441 / R 0.0064 against a rig-off baseline of 0.0396 / 0.0057,
  re-measured under the fixed step because the prefab's non-finger transforms changed.
- `club.headAtBall` **0.0085 PASS**; `hand.orient_l/_r` **0.0000° / 0.0000°**.
- §3.4 re-solved with the station fixed: `ClubSlot` local `(−0.03787, −0.04266, −0.09164)`,
  euler `(347.542, 237.803, 313.704)`. **Not authored** — `club.headAtBall` already passes at
  0.0085 m and authoring it would move `ClubStart` and re-open every station again. Architect's call.

## 7. Files modified

| path | 1-line summary |
|---|---|
| `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` | §3.10.1 handedness-fixed `n_out` in `PalmNormal`; §3.10.2 `WrapJoint` flexes only, caps 90/90/70, 21-joint log hooks; `VerifyPalmNormal`; thumb `up12` uses `n_out` |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | `n_out` in `LandmarkAxis`; §3.10.3 wrap-invariant lead B; §3.10.4 lead-station mark; §3.10.5 clock sign from the trail palm; §3.10.6 geometric trail-palm assertion; `SolverVersion` → `handedness-v1` |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | anchor rotations re-solved with `n_out`; lead station +0.0288; trail station +0.0758 |
| `Docs/Specs/Active/golfer_club_grip/IMPLEMENTER_REPORT.md` | this report |
| `Docs/Specs/Active/golfer_club_grip/STATUS.md` | `SPEC_READY` → `IMPLEMENTER_WORKING` → `READY_FOR_SELF_REVIEW` |
| `Docs/Specs/Active/golfer_club_grip/HEARTBEAT.log` | iter-8 baseline |
| `Docs/Specs/Active/golfer_club_grip/evidence/grip310/` | nine frames |
| `Docs/AI_CONTEXT.md` | session status |
