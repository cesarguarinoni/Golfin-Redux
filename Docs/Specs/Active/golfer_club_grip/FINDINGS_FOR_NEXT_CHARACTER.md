# What the next character model, rig, clip and clubs must bring — findings from `golfer_club_grip` stages 0–2 (2026-09-15)

Written for the Architect at Cesar's request ("since we are going to switch character model, write all your findings
… to get a model as good as possible"). Everything below was measured on `PfGolfer_MixamoNative` (Mixamo humanoid,
1.75 m after the real-size rescale) with the stage-0/1/2 tooling in `HandHingeStage0Tool / Stage1Tool / HandHingeStage2`.
Numbers are in the evidence folders; the rows named in **bold** are the automated checks the new model should be
delivered against (they run on any humanoid prefab with the same object names — see § 7).

## 1. The one-line summary

The grip mechanism works on any humanoid hand (fist, inscribed wrap, two hands on one shaft, IK to anchors), but
**everything that made this character look wrong was upstream of the grip**: the actor's address posture (hands low,
knees over-flexed, wrists 46°/42° with the knuckle rows 40° off the shaft), a stand-in club length, and a stand-in
character scale. The next model should be judged at delivery by the address-pose numbers in § 4, not by eye.

## 2. Skeleton and hand rig

| Requirement | Why (what we hit) |
|---|---|
| Unity **Humanoid** avatar with all four fingers × 3 phalanges + thumb × 3, each distal phalanx with an **end/tip child transform** | `HandHingeModel` addresses bones by `HumanBodyBones` and reads the fingertip as `distal.GetChild(0)`; the inscribed wrap solves MCP→PIP→DIP until the *tip* lands on the contact circle. No tip child = no tip. |
| Fingers in the rest pose **flat and parallel**, palm open, thumb abducted ~45° | The hinge axes are derived per joint as `cross(childDir, palmNormal)` in the rest pose; a curled or splayed rest pose gives skewed hinges (stage 0 test (a): a 90° MCP flex must move the PIP toward the palm, not sideways — 13 tests in `HandHingeModelTests`). |
| Palm normal derivable from Hand, IndexProximal, LittleProximal, sign from the thumb | That is how the model finds "palmar"; a thumb bone that sits behind the palm plane breaks the sign. |
| Consistent bone roll along each finger (no per-bone twist) | The abduction axis is the palm normal expressed in each joint; twisted bones spread the fingers when they should curl. |
| Knuckle row (LittleProximal→IndexProximal) straight, ~80–90 mm long at 1.75 m | The §3.12.4 grip axis runs from the little MCP toward the index MCP; the hinge model measures `L_prox` from it. |
| Elbow and shoulder bones with clean hinge behaviour for `TwoBoneIKConstraint` (Animation Rigging) | Both arms are IK'd to anchors on the club; the trail arm was the reach limit at every solve — an actor with slightly bent elbows at address leaves the IK room. |
| Spine / Hips / legs as standard humanoid; the stance rig (§ 6) overrides Spine rotation and Hips position and IKs both legs to the clip's feet | Only needed if the clip's posture is off-guideline (§ 4); with a good clip it stays at zero. |

## 3. Mesh and scale

| Requirement | Why |
|---|---|
| Model at its **real height** (1.70–1.85 m), `lossyScale = 1` on the prefab root, clubs at real length, no compensating scales anywhere | We ran the whole of stages 0–2 at 0.759 with mm·s everywhere until Cesar asked for real sizes; the rescale changed every contact number and cost a re-capture. `CharScale = 1` now, keep it. |
| Finger half-thickness known and uniform: here **7.18 mm** at the proximal phalanx | `ContactM = shaftRadius + fingerHalfThickness` is where every joint lands; the mesh must match its bones (a thick glove mesh on thin bones puts the skin into the shaft). |
| Skin weights that survive a fist (MCP 75° / PIP 95° / DIP 50° accepted at stage 0) without the palm collapsing or the finger webbing tearing | The grip is a near-fist; Mixamo's weights held. |
| Hand mesh without baked-in curl or a "relaxed hand" sculpt | Same reason as the flat rest pose. |
| Sleeves / cuffs ending above the wrist or skinned to the forearm only | The wrist rotates up to ~30° from the clip under IK; a cuff skinned to the hand clips through the forearm. |

## 4. The address clip — the numbers that decide whether the grip looks real

These are the published guidelines we encoded (sources in `reference/WRIST_ANGLES_AT_ADDRESS.md`) and what the
Mixamo clip measured. **Ask for the clip to be captured at these values, with a real club of the intended length in
the actor's hands.** Rows: `stance.*` and `grip.wrist.*` in `HandHingeStage2`.

| Quantity (row) | Guideline | Mixamo clip | What it cost us |
|---|---|---|---|
| Lead wrist, forearm→hand (**grip.wrist.angle_l**) | 20–30° total: 17–20° ulnar deviation + 0–20° extension | 46.2° (28.3 ext, 33 dev) | Cesar rejected the first bake ("wrists bend too much"); the club pivot had to be scanned with the IK in the loop to reach 27° |
| Trail wrist (**grip.wrist.angle_r**) | slight cup | 41.8° (−8 ext, 40.7 dev) | same |
| Knuckle rows vs the shaft (`CLIP lead/trail little→index vs shaft dot`) | both rows along the shaft (dot ≥ 0.95) | lead 0.97, **trail 0.81 (≈ 36° off)** | the trail axis needed a DOF (index-end offset 0.6·L_prox) and a wrappability filter |
| Hand tunnel points vs a common axis (`tunnel … off the axis`) | ≤ 10 mm | lead 40 mm, trail 13 mm | the hands were not on one shaft; the IK moved them 133 / 110 mm |
| Torso forward tilt, hips→neck from vertical (**stance.torsoTilt**) | 25–45° (25 average … 35–45) | 31.9° ✔ | — |
| Knee flex (**stance.kneeFlex**) | 15–25° | **31–32°** | kneecaps forward, hands "touch the knees" (Cesar); needed a 20 mm hips lift with leg IK |
| Arm hang, shoulder→hand from vertical (**stance.armHang**) | hanging, ≤ 20° (15–20 forward is real) | 17 / 21° ✔ | — |
| Hands off the thighs, surface (**stance.handsFromThighs**) | 150–200 mm with a driver | 146 mm | just short |
| Hands vs chin (**stance.handsUnderChin**) | under to just in front (−50 … +150 mm) | −31 mm ✔ | — |
| Lowest fingertip above the knee joint (**grip.hands.aboveKnees**) | ≥ 100 mm (real ≈ 100–130) | 133 mm (clip); **71 mm** after the wrist-straightening solve | the trade-off is ≈ 16° of wrist per 10 mm of hand height — a clip with real wrists needs no lowering |
| Hand height over the ground (**stance.handsHeight**) | 0.75–0.90 m | 0.78 m ✔ | — |
| Shaft elevation (**club.shaftElevation**) | ≈ 50° with a driver at address | 47° | fine for a 40-inch club |
| Elbow slack | not locked | trail elbow straight at −2° of pitch | the IK cannot reach past the arm; every solve bumped the trail reach |

**The single most valuable thing the new clip can do is to have the actor hold a real club of the intended length,
with the club tracked (a marker on the shaft), so the hands, wrists and posture come from the actor.** Then the
pipeline only closes the fingers (stage 1) and pins the hands to the shaft (stage 2) with near-zero IK residual, and
the two §3.9.6 rows we never solved (heel pad on top: dot 0.14 vs > 0.5; trail palm on the lead thumb: 0.3 mm the
wrong side) come for free from a real overlap/interlock grip instead of from a roll rule.

## 5. Clubs

| Requirement | Why |
|---|---|
| Real lengths per type: driver 1.14–1.17 m, 3-wood 1.09, 5-iron 0.97, wedge 0.90, putter 0.86–0.89 | The "driver" asset is **1.0635 m** (≈ 42 in, a 5-wood); with a real driver the hands sit ~6 cm higher and further from the body at the same wrists |
| Grip radius 13–14 mm (ours 13.58), constant along the grip, taper documented | `ShaftRadiusM` is one number; a tapered grip needs the radius as a function of station |
| `ClubStart` (butt) and `ClubEnd` (sole / leading-edge point) markers, +Y = shaft, and the face normal readable from `ClubHead` | `club.faceSquare`, `club.headAtBall`, `grip.buttCap.pastHeel` read them; the face-roll fix rolls ClubSlot about +Y |
| Lie angle modelled (driver 56–60°, irons 60–64°) with the sole flat when the shaft is at that elevation | `club.shaftElevation` is only meaningful against the club's lie |
| One pivot convention: the club hangs off `ClubSlot` under `GripTarget` (the two-hand average); anchors are children of `ClubSlot` | Every stage-2 tool assumes it |

## 6. The stance rig, as data (keep it, aim for zero)

`AuthorPrefabStructure` now authors `Rig_StanceFeet` (MultiParent copies of both feet into targets, evaluated
first) and `Rig_Stance` (`Stance_Hips` OverrideTransform, Pivot space, position offset; `Stance_SpineBend`
OverrideTransform, Pivot space, rotation about the target line; `Stance_LegL/R_IK` TwoBoneIK to the foot targets),
then `Rig_Grip`, then `Rig_Hands`. The stance sweep (`RunSolve("stancescan")`) reads the posture rows for a lift ×
bend grid with the clip's hands and writes `stage2_stance.json`; `ApplyStanceToPrefab` writes it into the prefab.
Measured on Mixamo: a 20 mm lift takes the knees from 31° to 16–18°, +5° of spine takes the torso from 32° to 28°;
lifts above ~35 mm lock the legs and lift the feet. A clip captured at the § 4 numbers needs none of it — but the
rig is how a near-miss is corrected without touching the clip.

## 7. What the pipeline needs from the prefab (names, for the tools to run unchanged)

`Animator` (humanoid) on a child; root `GolferPresenter` with `addressHeadLocal`; `GripTarget` (MultiParent of both
hands, `Rig_Grip`), `ClubSlot` under it with `ClubStart`, `ClubEnd`, `ClubHead`; `GripAnchor_Lead/Trail` +
`WristTarget` under `ClubSlot`; `GolferRig` with `RigBuilder`; `HandHingeModel` + `HandHinge_<name>.asset`
(re-capture with `HandHingeStage0Tool` after any rig or scale change). Frames: `SkinnedMeshRenderer.
forceMatrixRecalculationPerRender` on for multi-camera captures (Lesson AT); `RigBuilder.Build()` after any
runtime anchor edit (Lesson AU).

## 8. Acceptance on delivery (run these before anyone looks)

1. `HandHingeStage0Tool` capture + the 13 `HandHingeModelTests` (hinge axes, palm normal, fist, idempotent capture).
2. `HandHingeStage1Tool` inscribed wrap on the spec axis: every wrapped finger tip within 5 mm of the contact circle, bones outside the shaft mesh.
3. `HandHingeStage2.RunVerify` at address on Hole 06: all `stance.*` rows PASS on the raw clip (no stance edit), `grip.wrist.angle_l` 20–30°, both hands on the shaft ≤ 3 mm with IK residual ≤ 10°, `grip.hands.aboveKnees` ≥ 100 mm / 160 mm, `club.faceSquare` ±5°, `club.headAtBall` ≤ 50 mm.
4. Look at `verify_stance_targetside.png` and `verify_awayside.png` last.

## 9. Findings that are about the method, not the model (so they are not re-learned)

- A wrist predictor that uses the clip's forearm is blind to the IK; scan the club pivot with the rig rebuilt per candidate and pick by the graded rules (Lesson AX).
- Every DOF that trades against arm reach belongs in the grid, not in a fix-up sweep (the trail gap).
- Rotations about the club head cannot stand the golfer closer to the ball; that is a translation DOF (added 2026-09-15 after the stance edit collapsed feasibility to 29 configurations, all reaching 38° forward).
- The hand height / wrist trade-off through the club pivot alone is ≈ 16° of wrist sum per 10 mm; hand height is set by the shoulders (stance), not by the club.
- A longer club does not raise the hands; the golfer stands further from the ball.
- Bash heredocs un-escape `\\n`; patch scripts go through the Write tool and `py -3` (Lesson AW).
