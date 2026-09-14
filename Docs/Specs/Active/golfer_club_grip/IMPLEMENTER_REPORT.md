# IMPLEMENTER REPORT — `golfer_club_grip` iter-9 / 9b (SPEC §3.11 + the authored hand pose)

**Iteration shape:** `club-mount:authored-offset`
**Branch:** `golfer_3d_test` · **Baseline HEAD:** `d188ab58f` (see `HEARTBEAT.log` → `=== iter-9 kickoff baseline 2026-09-10T01:20:58Z ===`)
**Canonical screenshot:** `screenshots/iter9_address.png` (1170 × 2532, gameplay camera, address)
**Invariants:** `golfer_invariants_mixamo.json` — **32 PASS / 1 FAIL / 25 SKIP / 2 INFO**, A0 = 0

---

## 1. What was built

§3.11 in one line: **the hands and fingers are the clip's, untouched; the club hangs from the
two-hand average and carries one authored offset per slot.**

- **Deleted** from `PfGolfer_MixamoNative`: `Rig_Hands` (with `IK_Lead` + `IK_Trail`,
  both `TwoBoneIKConstraint`), `GripAnchor_Lead`, `GripAnchor_Trail` and both `WristTarget`s.
- `RigBuilder.layers` = `[Rig_Grip]` only. `forceGripPose` 1 → **0**.
- **Kept exactly as they were**: `ClubRoot → GripTarget`, `Rig_Grip` with the
  `MultiParentConstraint` (constrained `GripTarget`; sources `mixamorig:LeftHand` @ 0.5 and
  `mixamorig:RightHand` @ 0.5; maintain position **and** rotation offset **OFF**). Read back off
  the saved asset, not asserted from memory.
- The wrap code in `GolferPresenter` is untouched and still compiles — `PfGolfer_Test` uses it.

## 2. The two authored poses (§3.11.3)

Both are **local, under `GripTarget`**, and both were solved by measurement — no by-eye passes.

| Slot | `localPosition` | `localRotation` (x, y, z, w) | `localEuler` |
|---|---|---|---|
| `ClubSlot` (driver) | `(0.03009, 0.07460, -0.09264)` | `(-0.144565, 0.882671, 0.433683, 0.109142)` | `(307.1408, 173.6019, 344.5799)` |
| `PutterSlot` | `(0.03073, 0.08187, -0.09457)` | `(0.450949, -0.651749, -0.192278, 0.578703)` | `(15.7415, 254.5588, 302.6554)` |

### 2.1 Which axis is the face normal — measured once off the meshes

Required by §3.11.3 ("measure it once from the mesh bounds/normal and record which local axis it
is"). Area-weighted planar clustering of each head mesh, expressed in slot space:

| Mesh | Face normal (slot-local) | Why that cluster is the face |
|---|---|---|
| `GOLFIN_Driver / ClubHead` | `(-0.9120, -0.3607, -0.1951)` | The **only genuinely flat surface** on the head: 13.3 % of mesh area over a 51.7 mm radius, **flat to 0.6 mm**, 103 triangles. Every other cluster is curved — crown, sole and skirt deviate 4–26 mm across the cluster. Its 21.1° tilt out of the shaft-perpendicular plane is the **loft**. |
| `GOLFIN_Putter / Clubhead` | `(0, 0, -1)` | A flat 120 mm disc: 193 verts inside a 3 mm slab, against 31 on the `+Z` side, which is the flanged back. |

The meshes report `isReadable = false`, but in the Editor the CPU copy is still present, so
`mesh.vertices` was read directly — no importer setting was changed and no `.meta` was touched.

### 2.2 The roll was solved, not inferred — this mattered

The plan-azimuth error at the start was **174.34°**, and the naive fix is to roll by that.
**The correct roll was 147.55°.** The shaft carries the lie angle, so rolling about it by θ does
*not* move the plan azimuth by θ; taking the error as the roll would have left the face ~27° open.
The harness therefore scans the one-parameter family (rotate the world face normal about the world
shaft axis) and reports the angle that actually zeroes the azimuth — residual **0.0000°**.

### 2.3 Why `club.faceSquare` gates on two numbers

§3.11.4 asks for "the angle between the face normal and the aim, 90 ± 5". Read literally of the
**normal**, that describes an *open/shut* face — a normal perpendicular to the aim points at the
golfer's feet. The quantity that is exactly 90° on a square club is the **leading edge** against
the aim, which is also what §3.9.7 called "the blade orientation". Both are reported and **both are
required**, so the row cannot be passed by a club that is square in name only.

That is not a theoretical distinction: the starting state read **edge 84.34°** — a club 174° backwards
would have come within 0.7° of passing an edge-only test, because a 180° flip leaves the edge angle
unchanged. The loft (the normal's tilt out of the shaft-perpendicular plane) is reported and
deliberately **not** penalised; a lofted face cannot be parallel to a horizontal aim vector.

## 3. Cesar's two mid-run corrections, and what the geometry actually allows

**(1) "The club is a bit too high for the hand pose … should look inside the cupped hands."**
Measured rather than nudged: each fist's centre is the centroid of its eight non-thumb MCP + PIP
joints (8/8 bones resolved on both hands); the seat is the midpoint. The shaft was missing that
hollow by **0.0969 m**, while the seat's *station* (y = 0.1032) was already on the grip — a pure
sideways miss. Translating would have cost `club.headAtBall` **0.0085 → 0.0922 m**, over the 0.05
gate, so the club was **pivoted about its head** instead: the head stays exactly on the ball and the
lie changes by 7.03°.

**(2) "A bit too low now, it goes through the left hand's pinky … should not go through any
fingers/hand."** Correct — seating on the midpoint put the shaft in the gap *between* the fists
(each centre ~23 mm off-axis on **opposite** sides), clipping `R.Ring3` at 0.0041 m, `R.Mid2` at
0.0064 m and `L.Ring2`/`L.Mid2` at 0.0129 m, all inside the grip surface (0.0136 m from the axis).

All 32 finger/hand joints beside the grip were then measured as radii from the shaft axis, and the
axis solved for. **The finding is that the clip's two fists do not share a tunnel** — they are closed
on nothing, hollows too small and misaligned for a 27 mm grip. There are exactly **two**
non-intersecting placements:

| Placement | Distance from the fist hollow | Worst joint clearance | Verdict |
|---|---|---|---|
| **down 31 mm (authored)** | 0.0311 m | **0.0205 m** — clear | the closest position that touches nothing |
| up ≥ 85 mm | ≥ 0.085 m | 0.0236 m at 85 mm | ≈ the original "too high" (0.0969 m) |

Going *up* does not help: the shaft has to pass through fingertips (0–40 mm), then the thumbs
(45–65 mm), then the wrist, intersecting continuously until it finally clears at 85 mm. The
authored position is **3× closer to the hands** than the state Cesar first rejected, and the
optimum is pinned symmetrically between the two hands' nearest fingers (`R.Ring2` and `L.Pinky2`
both at exactly 0.0205 m), which is what a real optimum looks like. Solved offline from the
measured joint set, then **verified live** in the acceptance run: worst clearance 0.0205 m,
matching the prediction to four decimals.

## 4. A latent harness defect, found and fixed

Sections 3 and 4 of the harness — `stance.followsHeading`, `club.bothPresent`,
`club.driverDefault`, `club.putterSwap`, `club.driverSwapBack` and the **entire
`PuttGripOnGreen` putt-address measurement** — were nested inside
`if (slot != null && hasQuaterniusFingers)`, which is **false on the Mixamo rig**. They had never
run there, and did not even appear as SKIP, so no reviewer saw them missing. Two braces had been
lost in an earlier edit; the counts still balanced, so it compiled.

**Its age is provable rather than asserted, and the proof is a commit, not the working tree.**
The committed iter-8 artifact `Docs/Specs/Active/golfer_club_grip/golfer_invariants_mixamo.json`,
tracked at `d727b8ac4` — several commits before this iteration's baseline `d188ab58f` — already
contains none of those five ids, so the rows were already missing before any edit here. (The
kickoff DIRTY block cannot source this one: the working tree was clean at kickoff apart from
`Library_broken_143700/`, which is the usual way that attribution gets sourced and does not apply
to a defect that lives in committed code.) The indentation at the seam showed the same thing:
section 3 was indented as if outside the block, over a whitespace-only line where the closing
brace belonged.

Fixed by restoring both braces. It cost nothing and bought six rows, **all PASS** — `pass` went
25 → 31 before the club work even landed. It was also required: without it the `PutterSlot` pose,
which §3.11.3 explicitly asks for, could not be measured at the putt address at all.

## 5. Numbers (§6 A7)

| Quantity | Value |
|---|---|
| A0 — Animation Rigging exceptions | **0** over the whole run (console cleared at launch; 0 errors, and 0 `InvalidOperationException` from `UnityEngine.Animations.Rigging` in `Editor.log`) |
| `club.headAtBall` | **0.0085 m** (gate < 0.05) — unchanged by every pivot, because `ClubStart`/`ClubEnd` lie **on** the roll/pivot axis |
| `club.faceSquare` (driver) | leading edge vs aim **89.9850°** (gate 90 ± 5); face-normal azimuth error **0.0000°** (gate 0 ± 5); loft −21.1442° (reported, not gated) |
| `club.faceSquare.putt` | leading edge vs aim **89.9826°**; azimuth error **0.0000°** |
| Foot slide, rig ON | L **0.0387 m**, R **0.0056 m** |
| Foot slide, rig-off baseline | L **0.0422 m**, R **0.0063 m** (own run, same code, same ordering, `captureDeltaTime` 1/60) |
| `grip.ikNoLegEffect` | Δ L **0.0035 m**, Δ R **0.0007 m** — both inside ±0.010 → **PASS** |
| `grip.hand.onShaft_l / _r` (INFO) | **0.1248 m / 0.1377 m**, worst of address / t=0.6 s / impact, from the hand **bone origin** (§3.11.4's definition for this row) |
| Worst finger-joint clearance | **0.0205 m** (`R.Ring2`, `L.Pinky2`) vs grip surface 0.0136 m + `fingerRadius` 0.00683 = 0.0204 m target |
| Club scale | 0.86880 (unchanged stand-in artefact of the 1.328 m character) |
| Animation Rigging package | `com.unity.animation.rigging` **1.3.1** (`Packages/manifest.json`) |

**On the `onShaft` numbers going up** (0.0626/0.0361 → 0.1248/0.1377): expected, and exactly why
§3.11.4 made this row informational. It measures the **wrist**, and the club moved ~10 cm to sit at
the fingers rather than across the back of the hand — so the wrist is now further from the shaft.
A lower number here would mean the shaft was back through the wrist, which is the defect §3.7 chased
for four iterations.

Retired by §3.11 and therefore **not** reported: the `n_out` verification dots, the 21-joint wrap
log, `A/B` and `A'/B'` in hand space, the two anchor rotations and their palm-rule dot products,
the trail station, the eight per-hand wrap results, and the thumb clock angle. All belonged to the
IK/wrap machinery this section deletes; their harness rows are `SKIP` with the reason
"RETIRED by 3.11 - hands are the clip's", carrying their last measured values for the record.

## 5b. iter-9b — the authored hand pose (Cesar's call, and why §3.11 could not hold)

§3.11 says the hands are the clip's. Three placements of the club were rejected on sight — above
the hands (0.0969 m), through the fingers (0 m), and below both hands (0.0311 m) — and the
measurement above explains why: **no placement can work.** The two fists sit **46.4 mm apart
perpendicular** to the shaft and only **37.7 mm along** it. They are side by side *across* the
club, not threaded on it, so the club can be beside either fist or between them, never inside both.
Put to Cesar with those numbers, he chose to author a hand pose, adding: *"remember you can also
bend the wrists slightly if needed."*

**New code path, not a change to the old one.** `ApplyGripPose` / `WrapJoint` /
`JoinLeadHandToShaft` are `PfGolfer_Test`'s wrap and are untouched; the new `ApplyHeldGripPose`
sits behind its own `heldGripPose` flag, and a prefab sets one or the other. Both run from
`LateUpdate`, after the Animator, and neither reads the club transform back — Animation Rigging has
already evaluated `GripTarget` inside the animator graph, so posing the hands cannot move the club.

Two stages per hand, each one parameter:

1. **A capped wrist rotation** (25°) that swings the hand's grip seat onto the shaft axis, plus a
   **twist** about the arm axis that lays the knuckle row along the shaft. The twist is the DOF
   `Quaternion.FromToRotation` leaves free, and it mattered: without it the mean knuckle sat at its
   contact distance while individual knuckles read 11.0 and 12.5 mm — a knuckle row lying *across*
   the club, so the fingers met it side-on and no curl could wrap them.
2. **One curl parameter per finger**, split across its three joints in natural proportions
   (0.40 / 0.35 / 0.25) and solved so the closest joint the curl can move lands on the grip surface.
   One parameter per finger is what stops a single joint taking a cap alone, which is the mechanism
   that made the old wrap's claws.

**Three corrections worth recording, because each was a real defect:**

- **The seat target was wrong first time.** Aiming the *fist centre* at the axis buries the shaft in
  the finger mass — measured result, MCP joints 9–15 mm from the axis against a 13.6 mm grip
  surface. A curl cannot fix that: rotating a joint moves its children, not itself, so a knuckle
  inside the grip stays there. The wrist is the only thing that places the knuckle row, so the
  target became the knuckle row offset by one contact distance along the palm normal.
- **`shaftRadius` was 0.008727 m** while the `Grip` mesh is 0.02715 m across — radius **0.013575 m**.
  The solve would have seated every fingertip ~5 mm *inside* the mesh. Now measured off the mesh
  bounds, so contact = 0.013575 + 0.006830 = **0.020405 m**.
- **A serialized value shadowed a C# default.** Raising `heldOpenMaxDeg` in the field initialiser
  changed nothing and two runs came back byte-identical, because the prefab carries its own value.
  Set on the prefab thereafter.

**Where it stands.** The club now runs **through both hands**, with the trail hand curling around
the grip — a real grip in silhouette, and the first state in this task that reads as *held*.
It is **not finished**: the lead fingers still splay rather than wrap, and worst joint clearance is
**0.0073 m** against the 13.6 mm grip surface, so a few finger bones still touch the club. Progress
across the four pose runs, all measured at the drive address:

| Run | Change | Fist seat miss | Worst joint clearance |
|---|---|---|---|
| 1 | fist-centre seat, fingertip objective | 0.0072 m | 0.0036 m |
| 2 | knuckle-row seat, closest-movable-joint objective | 0.0026 m | 0.0062 m |
| 3 | (no change — the shadowed serialized value) | 0.0026 m | 0.0062 m |
| 4 | knuckle-row twist + open cap 45° | 0.0032 m | **0.0073 m** |

**Nothing regressed while this landed:** 32 PASS / 1 FAIL, the FAIL still only `budget.tris`;
`club.headAtBall` 0.0085 m, `club.faceSquare` edge 89.9804°, foot slide L 0.0396 / R 0.0057 against
the rig-off baseline L 0.0422 / R 0.0063.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| A0 — rig alive, zero Animation Rigging exceptions | PASS | 0 console errors over the acceptance run (console cleared immediately before launch) and 0 `InvalidOperationException` from `UnityEngine.Animations.Rigging` anywhere in `Editor.log`. Every number below is therefore valid. |
| A1 — package present, compiles with the define on and off | PASS | `com.unity.animation.rigging: "1.3.1"` at `Packages/manifest.json:12`, resolved version confirmed via `PackageInfo.FindForAssembly`. Compiled clean with `GOLFIN_GOLFER_TEST` **on** — 0 console errors before each of the four play-mode runs. **Define-off is argued, not observed, and the distinction is stated deliberately:** after restoring `iOS-Full-GPS` the Editor kept its define-on assemblies (the golfer types stayed loaded through `RequestScriptCompilation`, a clean-cache rebuild and `RequestScriptReload`), so no define-off recompile could be witnessed in this session. What *is* verified: `GOLFIN_GOLFER_TEST` is declared in exactly one place in the whole project, `Assets/Settings/Build Profiles/iOS-Full-Golfer.asset` (no `.rsp`, no asmdef `defineConstraints`/`versionDefines`), and that profile is no longer active; and the one code file changed here, `GolferTestVerificationRecorder.cs`, names `GolferPresenter`/`GolferTestBootstrap` **only inside comments** (lines 102, 401, 1777) — every access is reflection by string — so it cannot compile differently with the define off. A reviewer with a focused Editor should confirm the recompile. |
| A2 — prefab per §3.11.2 | PASS | Read back off the saved asset: `Rig_Hands` 0, `GripAnchor_Lead` 0, `GripAnchor_Trail` 0, `WristTarget` 0, `TwoBoneIKConstraint` count **0**; `RigBuilder.layers` = `[Rig_Grip/active=True]`; `MultiParentConstraint` constrained `GripTarget`, sources `mixamorig:LeftHand@0.5` + `mixamorig:RightHand@0.5`, `maintainPositionOffset=False`, `maintainRotationOffset=False`; `forceGripPose = False`. Club is under `GripTarget`, not under a bone. |
| A3 — one acceptance run, Mixamo-native, Hole 06, deterministic step | PASS | `golfer_invariants_mixamo.json`: 32 PASS / 1 FAIL / 25 SKIP / 2 INFO under `Time.captureDeltaTime = 1/60`. `club.headAtBall` 0.0085 m, `club.faceSquare` edge 89.9850° / azimuth 0.0000°, `club.faceSquare.putt` edge 89.9826°, `grip.ikNoLegEffect` in band, `onShaft_*` informational, all other `grip.*` retired to SKIP. The single FAIL is `budget.tris` (36 510 vs 15 000) — unchanged and out of scope per §5. |
| A4 — frames in `evidence/final/` | PASS | Four full-res frames from the acceptance run: `gameplay_address.png`, `gameplay_t0_6.png`, `gameplay_impact.png` (all 1170 × 2532, gameplay camera) and `scenecam_address_closeup.png` (1400 × 1400, for the record), plus `scenecam_address_targetside.png` as the second record angle. Greyscale variance 1092–2297 (fabrication floor is 5.0). |
| A5 — nothing shipped changes | PASS | Only two non-doc files changed. `GolferTestVerificationRecorder.cs` lives under `Assets/Scripts/UI/Editor/`, so it compiles into `Assembly-CSharp-Editor` and is never in a player build. `PfGolfer_MixamoNative.prefab` lives under `Assets/Art/3D/Characters/_Test/Resources/`, which `GolferTestBuildGate` moves out of every build that did not opt in. No `#if`-free runtime code and no `manifest.json` / `packages-lock.json` change this iteration. |
| A6 — build profile restored | PASS | Switched to `iOS-Full-Golfer` to run the harness (the golfer types compile out otherwise) and restored to **`iOS-Full-GPS`** before the commit, verified by `BuildProfile.GetActiveBuildProfile()` and by the golfer types being absent from the loaded assemblies afterwards. |
| A7 — numbers in the report | PASS | §5 above carries both authored local poses, both face-normal measurements with the evidence for each, the solved rolls and their residuals, A0, `club.headAtBall`, both `faceSquare` rows, the three-sample informational hand-to-shaft numbers, the rig-on and rig-off foot slides with their deltas, the finger-clearance solve, the club scale and the package version. The §3.9/§3.10 quantities A7 also lists are retired by §3.11 and are named as such rather than silently dropped. |

## Files modified or created

Every uncommitted path outside `Docs/Specs/Active/golfer_club_grip/` is listed.

| File | One-line summary |
|---|---|
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | §3.11.2 surgery — `Rig_Hands` + both IKs, both `GripAnchor_*` and both `WristTarget`s deleted, `RigBuilder.layers` = `[Rig_Grip]`, `forceGripPose` → false, and the two authored `ClubSlot` / `PutterSlot` local poses. |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | Added `club.faceSquare` / `club.faceSquare.putt` (gating leading-edge **and** azimuth) with the roll solver, the grip-seat and finger-clearance measurements, an `Info()` verdict for ungated rows; made `grip.hand.onShaft_*` informational, retired 12 `grip.*` rows to SKIP, and restored the two lost braces that had been hiding harness sections 3–4 from the Mixamo rig. |
| `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` | iter-9b — NEW `ApplyHeldGripPose` path (capped wrist seat + knuckle-row twist, then a one-parameter-per-finger curl onto the grip) behind a new `heldGripPose` flag, plus the `TrailLittle` chain. `ApplyGripPose` / `WrapJoint` / `JoinLeadHandToShaft` are untouched — `PfGolfer_Test` still uses them. |
| `Docs/AI_CONTEXT.md` | Session entry for iter-9. |
| `Library_broken_143700/` | **Not this task's.** A stale Unity Library backup directory, untracked, ~5.9 MB of paths. Pre-existing — it is in this iteration's kickoff baseline block in `HEARTBEAT.log` as `?? Library_broken_143700/`, and nothing here wrote to it. Listed only so Rule 13 can see it was seen. |
| `Docs/Specs/Active/golfer_club_grip/*` | `STATUS.md`, `HEARTBEAT.log`, this report, `golfer_invariants_mixamo.json`, `evidence/final/`, `screenshots/iter9_address.png`. |

`Library_broken_143700/` is untracked in the working tree and is **pre-existing** — it is a stale
Unity Library backup directory, recorded as such in this iteration's kickoff baseline block in
`HEARTBEAT.log` (`?? Library_broken_143700/`), and nothing in this iteration wrote to it.

## Screenshot

Canonical screenshot: `screenshots/iter9_address.png` — gameplay camera, address, 1170 × 2532,
from the acceptance run (11:05:17). Full evidence set in `evidence/final/`.

## What a reviewer should be sceptical about

- **The club touches nothing, but it is not *inside* the fists.** It cannot be: §3 shows the clip's
  fists have no tunnel. The authored position is the closest non-intersecting one, and that is a
  compromise, not a grip. If it is judged wrong, the fix is an authored hand pose (already the
  §3.11.5 backlog row), not another offset — every offset has now been enumerated.
- **`club.faceSquare.putt` is measured with the golfer 152.70 m from the ball.** `PuttGripOnGreen`
  moves the *ball* to the green without re-placing the *golfer*, so every distance in that section
  is meaningless. The roll solve is direction-only and invariant to the golfer's heading, so the
  number itself is sound — but it is worth knowing, and it is a limitation the harness has carried
  since §9.4 was written, not something this iteration introduced into it.
- **The two address clips share their hand keys.** At the putt sample the animator is genuinely
  `Address_Putt` (normalizedTime 0, not in transition), yet `GripTarget` and **both hand world
  rotations are bit-identical** to `Address_Drive`. That was checked rather than assumed, because it
  decides whether `PutterSlot`'s roll is the right number.
- **Three play-mode runs preceded the acceptance run** — two §3.4-style measurement passes and the
  §3.6 rig-off baseline, all structurally required. The acceptance run itself was run once.

---
STATUS → `READY_FOR_SELF_REVIEW`

---

# Stage 0 — the fist test (SPEC §3.12.6, iter-10, 2026-09-10)

**Iteration shape:** `hinge-model:stage0-fist`. Scope: §3.12.2 mechanism + §3.12.6 stage 0 only. No club, no rig, no
play mode, no anchors; the prefab's only change is the removal of four stale iter-9b fields. **STOPPED at the gate —
stage 1 is not started.**

## 0.1 What was built

- **`ApplyHeldGripPose` is gone** (SPEC §3.12.7): the iter-9b path (`heldGripPose`, `wristSeatMaxDeg`, `heldCurlMaxDeg`,
  `heldOpenMaxDeg`, `HeldCurlRatio`, `ApplyHeldGripPose`, `GripSeatInHand`, `SeatHandOnShaft`, `CurlFingerOntoGrip`) and
  the three chain tables it alone used (`TrailLittle`, `LeadFingers`, `TrailFingers`) are deleted from
  `GolferPresenter.cs` (1222 → 972 lines; 250 deletions, 4 insertions = the `LateUpdate` comment). `ApplyGripPose` /
  `WrapJoint` / `JoinLeadHandToShaft` stay, untouched, for `PfGolfer_Test`. The four serialized fields were stripped from
  `PfGolfer_MixamoNative.prefab` as a text edit (the define was ON; no Unity save of the prefab happened this stage).
- **`HandHingeModel.cs`** (`Assets/Scripts/Gameplay/Golfer/`, `Golfin.Physics.Viewer` via the asmref, gated by
  `GOLFIN_GOLFER_TEST` with an inert `#else` shell): `Capture(anim, right)` from the rest pose → per joint
  `restLocalRotation`, `hingeAxisLocal = joint.InverseTransformDirection(cross(childDir, palmNormal))`,
  `abductAxisLocal = joint.InverseTransformDirection(palmNormal)`, segment length; palm normal from
  (Hand, IndexProximal, LittleProximal) with the sign fixed by `ThumbProximal − IndexProximal`; hand length axis and
  across axis stored Hand-local. `Apply` = `rest * AngleAxis(spread, abduct) * AngleAxis(flex, hinge)` (spread at the MCP
  only), thumb = `FromToRotation` aim on `ThumbProximal` (zero = rest) + fixed hinges on the other two. `Measure` = the
  stage-gate numbers. A `LateUpdate` re-applies a serialized pose only when `applyEveryFrame` is set — it is **not** on
  the prefab yet (stage 2's job).
- **`HandHingeData.cs`** — the ScriptableObject, its own file. First saved from inside `HandHingeModel.cs` it wrote
  `m_Script: {fileID: 0}` and could not be loaded after the next domain reload (Unity binds a ScriptableObject to its
  MonoScript by file name). Caught when the sweep threw "Capture the asset first" after the test run; split, re-captured,
  verified on disk `m_Script guid 2e50a92dfe102154a91946cf27451c14` = `HandHingeData.cs.meta`, reloads 15/15 joints.
- **`HandHinge_MixamoNative.asset`** at `Assets/Art/3D/Characters/_Test/Resources/GolferTest/` (same Resources folder the
  build gate moves out). Capture sanity, read off the asset: left `palmNormalHandLocal (−0.0072, −0.0173, −0.9998)`,
  right `(0.0053, −0.0216, −0.9998)` (hand-local −Z, world −Y in the T-pose, i.e. the thumb side — mirror-consistent);
  every finger hinge ≈ joint-local `(−1, 0, ±0.006)`, the thumb's `(−0.972, 0, ∓0.234)`; all 30 finger rest rotations
  identity except the two `Thumb1`s. Capture is deterministic: run 1 and run 2 of the fist produced bit-identical
  numbers.
- **`HandHingeModelTests.cs`** + `Golfin.Gameplay.Golfer.Tests.asmdef` (Editor-only, references `Golfin.Physics.Viewer`).
  Prefab opened with `PrefabUtility.LoadPrefabContents` — isolated, never an open scene, never saved.
- **`HandHingeStage0Tool.cs`** (`Assets/Scripts/UI/Editor/`, whole file `#if`-gated): menu `GOLFIN ▸ Golfer Test ▸ Hinge ▸`
  *Capture HandHinge_MixamoNative.asset* and *Stage 0 fist test*. The fist test instantiates the prefab into a
  **temporary additive scene** at (0, 500, 0) with `RigBuilder` disabled, poses from the asset, measures, renders the four
  frames with a throw-away `Camera.Render → RenderTexture` (the A4-sanctioned second-camera path; no `CaptureCore`, no
  Game View), writes `<label>_numbers.json`, closes the temp scene. The open scene was never dirtied — `tests-run`'s
  "all scenes saved" precondition passed three times afterwards.

## 0.2 EditMode tests — define ON and OFF, both observed

| Config | How it was observed | Result |
|---|---|---|
| `GOLFIN_GOLFER_TEST` **on** (`iOS-Full-Golfer`) | `Version` const read back = `stage0-b`; `HandHingeModel` 13 public statics | **8/8 PASS**: `A_IndexProximal90_MovesIntermediateTowardPalm_NotSideways` ×2 hands, `BC_Fist_60_80_40_TipsApart_AndPalmSide` ×2, `D_CaptureIsIdempotent` ×2 (same instance twice, after apply→rest, and a fresh instance — all equal to 1e-5), `PalmNormal_IsPalmar_ByTheThumb` ×2 |
| `GOLFIN_GOLFER_TEST` **off** (`iOS-Full-GPS`) | `BuildProfile.SetActiveBuildProfile` + `RequestScriptCompilation`; domain reload observed in `Editor.log` (test asm 144 → **143** defines); `activeScriptCompilationDefines` lacks the define; `HandHingeModel`/`HandHingeData`/`GolferPresenter` all **0 declared members**; `HandHingeStage0Tool` type absent; project test count 2773 → **2766** | **1/1 PASS**: `DefineOff_ModelCompilesAsAnInertShell`; 0 `error CS` |
| back **on** | profile restored to `iOS-Full-Golfer`, reload observed (143 → 144 defines), `Version = stage0-b`, asset reloads | **8/8 PASS** again |

Unlike iter-9's A1 ("argued, not observed"), the define-off recompile was witnessed this time: the reason it did not
show in iter-9 was that the profile switch alone does not recompile — `CompilationPipeline.RequestScriptCompilation()`
after `SetActiveBuildProfile` does.

## 0.3 The fist — 65 / 85 / 40, thumb 30 / 20, spread 0 (the mandated pose)

Palm plane = through the four finger MCP joint centres, normal = the captured palm normal carried by the Hand bone;
distances signed along it, positive on the palm side. (The palm *skin* is ~7–9 mm palm-side of the joint plane — the
iter-9b finger half-thickness 6.83 mm is the nearest measured proxy — so a tip at *d* mm from the joint plane hovers
roughly *d − 8* mm off the skin.)

| Hand | Finger | tip → MCP plane | (PIP, DIP) | Gate 8–20 mm |
|---|---|---|---|---|
| left | index | **38.33** mm | 28.37, 42.76 | OUT |
| left | middle | **35.93** | 25.83, 40.91 | OUT |
| left | ring | **32.45** | 23.11, 37.19 | OUT |
| left | little | **31.80** | 26.34, 35.54 | OUT |
| left | thumb tip | 91.73 | — (rest aim; the T-pose thumb hangs 65 mm palm-side before any flex) | n/a |
| right | index | **41.45** | 28.54, 45.07 | OUT |
| right | middle | **33.87** | 22.53, 38.77 | OUT |
| right | ring | **36.60** | 25.31, 40.54 | OUT |
| right | little | **28.31** | 24.10, 32.49 | OUT |
| right | thumb tip | 92.60 | — | n/a |

| Hand | Pair | adjacent tip spacing (≥ 8) | min phalanx-to-phalanx | crossing |
|---|---|---|---|---|
| left | index–middle | **23.69** mm | 22.16 | none |
| left | middle–ring | **20.08** | 18.37 | none |
| left | ring–little | **19.55** | 19.54 | none |
| right | index–middle | **26.07** | 21.97 | none |
| right | middle–ring | **17.35** | 17.13 | none |
| right | ring–little | **21.92** | 20.95 | none |

Crossing = the MCP order along the across-axis flipping at PIP, DIP or tip; none anywhere. Source:
`evidence/stage0/fist_65_85_40_numbers.json`.

**Stage-0 gate, by the spec's letter:** spacing PASS, crossing PASS, EditMode tests PASS, **tip-to-palm-plane OUT OF BAND
on all eight fingers** (28–41 mm against 8–20). The mechanism does what §3.12.1 says it must — every finger stays in its
own plane (test (a) sideways = 0.000 mm on both hands), the fingers never fan or cross, and the curl is a monotone
function of one triple — but 65/85/40 (190° total) is a loose curl, not a closed fist: the tips hang ~35 mm in front of
the knuckle plane. Analytically the same: with these phalanx lengths (30 / 28 / 28 mm, index) the tip lands ~37 mm
palm-side and ~39 mm back toward the wrist, which is exactly what was measured. **The band is reached at 75 / 95 / 50**
(next section). Which triple defines "a fist" for the gate is the Architect's / Cesar's call; nothing above the hinge
level was touched to chase the number.

## 0.4 Supplementary — curl sweep, same model, numbers only (`evidence/stage0/sweep/`)

tip → MCP plane, mm; L = index / middle / ring / little, R likewise. Spacing stayed 17.1–26.6 mm and crossing stayed
`none` in every row.

| MCP / PIP / DIP | left | right | in band |
|---|---|---|---|
| 65 / 85 / 40 (mandated) | 38.3 / 35.9 / 32.5 / 31.8 | 41.5 / 33.9 / 36.6 / 28.3 | 0 / 8 |
| 70 / 90 / 45 | 27.9 / 24.4 / 21.5 / 24.0 | 31.6 / 21.9 / 26.6 / 19.8 | 1 / 8 |
| **75 / 95 / 50** | 17.8 / 13.3 / 11.0 / 16.5 | 21.8 / 10.2 / 16.7 / 11.7 | **7 / 8** (right index 21.8) |
| 80 / 95 / 50 | 14.1 / 9.3 / 7.2 / 13.9 | 18.1 / 6.0 / 13.1 / 9.0 | 5 / 8 |
| 80 / 100 / 50 | 10.0 / 4.8 / 3.0 / 10.9 | 14.0 / 1.3 / 9.0 / 6.0 | 3 / 8 |
| 85 / 100 / 55 | 5.0 / −0.6 / −2.2 / 7.3 | 9.1 / −4.4 / 4.1 / 2.1 | 1 / 8 |
| 90 / 100 / 60 | 0.6 / −5.4 / −6.7 / 4.1 | 4.7 / −9.4 / −0.2 / −1.3 | 0 / 8 (tips through the joint plane) |

Per-finger spread inside a row comes from the phalanx lengths differing per finger (right middle: 26.5 / 31.3 / 32.1 mm
vs right little: 25.5 / 16.2 / 27.4 mm) — the §3.12.3 per-finger triples already anticipate that. Four supplementary
frames at 75 / 95 / 50 are in `evidence/stage0/supplementary_75_95_50/` for the eye; they are **not** the mandated
deliverable.

## 0.5 Frames (full res, 1600 × 1600, greyscale variance 4500–5400; fabrication floor 5.0)

Canonical screenshot: `evidence/stage0/fist_65_85_40_left_palm.png`

| Frame | What it shows |
|---|---|
| `evidence/stage0/fist_65_85_40_left_palm.png` | left hand, camera on the palm side looking along −n, fingers up: four fingers curled toward the camera in parallel planes, even gaps, thumb at its rest pose |
| `evidence/stage0/fist_65_85_40_left_back.png` | left hand from the back: knuckle row at the top, fingers curled away |
| `evidence/stage0/fist_65_85_40_right_palm.png` | right hand, palm side — the mirror of the left |
| `evidence/stage0/fist_65_85_40_right_back.png` | right hand, back |

Every PNG was opened and looked at before being cited (fingers up, wrist down, not Y-flipped; ReadPixels from the RT
came out upright on this D3D11 editor).

## 0.6 Acceptance for this stage

| Item | Result | Evidence |
|---|---|---|
| iter-9b deleted, one solver | PASS | `grep ApplyHeldGripPose\|heldGripPose` → 0 hits in `Assets/`; reflection: `GolferPresenter.ApplyHeldGripPose` null, `ApplyGripPose` present |
| `HandHingeModel` per §3.12.2 | PASS | §0.1; axes read off the asset; test (a) sideways 0.000 mm |
| EditMode tests (a)–(d), both hands, define ON | PASS | 8/8, §0.2 |
| … define OFF | PASS | 1/1 with the recompile actually observed, §0.2 |
| Fist frames, 4, full res | PASS | §0.5 |
| Tips 8–20 mm from the palm plane | **FAIL at 65/85/40** (28–41 mm); in band at 75/95/50 | §0.3, §0.4 |
| Adjacent tips ≥ 8 mm | PASS | 17.4–26.1 mm |
| No finger crosses another | PASS | order preserved at PIP/DIP/tip, min phalanx clearance 17.1 mm |
| Club / rig / anchors untouched | PASS | prefab diff = 4 deleted stale lines only |
| Shipped code diff empty (A5) | PASS | new runtime files are `#if`-gated with inert shells; define-off build has 0 declared members on all three types; `git diff --stat HEAD -- Packages ProjectSettings` empty |
| Profile (A6) | `iOS-Full-Golfer` left active on purpose — Cesar 2026-09-10: *"stop restoring profile in this machine"* (memory `project_pc_golfer_build_profile`); it was switched to `iOS-Full-GPS` only for the define-off pass and switched back |

## Files modified or created (stage 0)

Every uncommitted path outside `Docs/Specs/Active/golfer_club_grip/` is listed.

| File | One-line summary |
|---|---|
| `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` | iter-9b held-grip path deleted (§0.1); `LateUpdate` comment updated |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | 4 stale serialized fields removed (text edit) |
| `Assets/Scripts/Gameplay/Golfer/HandHingeModel.cs` (+ `.meta`) | NEW — the mechanism |
| `Assets/Scripts/Gameplay/Golfer/HandHingeData.cs` (+ `.meta`) | NEW — the ScriptableObject, its own file |
| `Assets/Scripts/Gameplay/Golfer/Tests/Golfin.Gameplay.Golfer.Tests.asmdef` (+ `.meta`), `Tests.meta` | NEW — Editor test assembly |
| `Assets/Scripts/Gameplay/Golfer/Tests/HandHingeModelTests.cs` (+ `.meta`) | NEW — (a)–(d) + palm-sign test, define-off shell test |
| `Assets/Scripts/UI/Editor/HandHingeStage0Tool.cs` (+ `.meta`) | NEW — capture + fist test + frames (menu items) |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/HandHinge_MixamoNative.asset` (+ `.meta`) | NEW — the capture (data) |
| `Docs/AI_CONTEXT.md` | stage-0 session entry |
| `tasks/lessons.md` | ScriptableObject-in-the-wrong-file lesson |
| `Docs/Specs/Active/golfer_club_grip/SPEC.md`, `ARCHITECT_DECISION_HINGE_MODEL.md`, `ARCHITECT_CLOSEOUT.md`, `Docs/GPS/GPS_BACKLOG.md`, `Docs/Design/CHARACTER_3D_REMAKE_OPTIONS.md` | **Pre-existing** — the Architect's §3.12 edits, in the tree at kickoff (HEARTBEAT.log iter-10 baseline `DIRTY:` block lists ` M Docs/GPS/GPS_BACKLOG.md`, ` M Docs/Design/CHARACTER_3D_REMAKE_OPTIONS.md`, ` M Docs/Specs/Active/golfer_club_grip/SPEC.md`, `?? …/ARCHITECT_CLOSEOUT.md`, `?? …/ARCHITECT_DECISION_HINGE_MODEL.md`); committed separately, first |
| `Library_broken_143700/` | **Pre-existing** stale Library backup, untracked; in the baseline block as ` ?? Library_broken_143700/`; nothing here wrote to it |

## What a reviewer should be sceptical about

- **The palm plane is the joint plane, not the skin.** 8–20 mm from the MCP joint centres is ~0–12 mm off the palm
  skin; if the gate meant skin contact the band itself, not the pose, is what to re-read.
- **The thumb was not aimed** (rest aim, 30/20 on the two hinges), as the stage-0 row specifies. Its 92 mm number is
  the T-pose thumb hanging down, not a defect of the hinge model; the aim is exercised from stage 1.
- **Spread was 0**, so the abduct-axis sign convention (mirror-antisymmetric, documented in the file header) has been
  captured but not visually exercised yet.

## Stage 0 verdict (Cesar, 2026-09-10)

**PASS.** Fist triple for the gate accepted as **75 / 95 / 50** (SPEC §3.12.6 row 0 updated). Stage 1 (grip pose in hand space, cylinder r = 13.575 mm on the §3.12.4 axis, per-finger k bisection) is the next kickoff.

---

# Stage 1 — the grip pose in hand space (SPEC §3.12.3 / §3.12.4 / §3.12.6 row 1, iter-11, 2026-09-15)

**Iteration shape:** `hinge-model:stage1-grip-in-hand-space`. Edit mode only, each hand alone with its debug cylinder,
no club, no rig, no play mode; the prefab is untouched. **STOPPED at the gate — stage 2 is not started.**

**Verdict in one line:** the mechanism holds (axes fixed, joints land where they are sent to 0.01 mm), but the
§3.12.3 one-k solve against the §3.12.4 axis does not produce a grip on this hand — the lead hand cannot get outside
the surface at any k, and the trail hand "reaches the surface" by grazing it with the middle knuckle, tips 41–51 mm
away. The reasons are geometric and measured below; a per-joint *inscribed* wrap on the same axis does produce a
grip (supplementary, §1.4). Which of the two defines stage 1 is the Architect's / Cesar's call.

## 1.1 What was built

- **`HandHingeModel` stage-1 members** (`Version = stage1-b`): `GripAxisHandLocal` (§3.12.4 — through
  `LittleProximal + n·c` and `IndexProximal [+ u·0.6·L_prox] + n·c`, butt → head = little → index, Hand-local, from
  the MCPs which are rigid to the Hand bone); `SegmentToLineDistance` (exact, convex-in-t); `MeasureFingerAxis`
  (joint and segment distances to the axis); `SolveFingerK` (§3.12.3 — one k ∈ [0.6, 1.4] on the triple, 24-step
  bisection so the closest wrapped segment = contact 20.405 mm); `ThumbAimAlongShaft` (1 o'clock = 30° from top
  toward +n, station 60 mm down-shaft; conventions in the file header); `LeadGripStart` / `TrailGripStart` = the
  §3.12.3 tables; and, supplementary, `SolveJointToContact` / `SolveFingerInscribed`.
- **Cross-section convention written down** (file header): +n = trail side (the shaft sits between the palm and the
  trail hand), −n = target side, −u (wrist) = "top" where the heel pad closes over, +u = under the grip. "1 o'clock
  viewed from the butt" = −u rotated 30° toward +n. The trail hand is the mirror and uses the same formula.
- **`HandHingeStage1Tool.cs`** (Editor, `#if`-gated; menu `GOLFIN ▸ Golfer Test ▸ Hinge ▸ Stage 1 …` and a batch entry
  `RunStage1Batch`): temp scene, prefab instance, `RigBuilder` off, per hand → axis, a cylinder of r = 13.575 mm as a
  child of the Hand bone on that axis, start pose, solve, thumb, measure, four 1600 px frames (down-shaft from the
  butt, palm side, back, top), JSON. Then the supplementary inscribed pass into `supplementary_inscribed/`.
- **Three stage-1 EditMode tests** added to `HandHingeModelTests`: axis one contact off the little MCP and running
  little → index on both hands; segment-to-line exactness; and the gate itself (`S1_SolveK_ClosestWrappedSegmentOnContact`,
  both hands) — **12 / 13 green; the lead-hand gate test is red on purpose** (it is the stage-1 gate in NUnit form
  and it fails for the reason in §1.3; it is not `[Ignore]`d because hiding the red is the rubber-stamp failure mode).
  Run twice: in Unity batch mode (`-runTests -testPlatform EditMode`, results XML in the scratchpad) and again through
  the Editor once it came back — identical 12 / 13.
- **Two tooling defects found and fixed on the way**, both now in `tasks/lessons.md` (Lesson AT):
  1. A `SkinnedMeshRenderer` is skinned **once per editor frame**; every `Camera.Render` in the same frame reuses it.
     The first trail-hand frames showed the REST pose with correctly posed bones (the mesh was skinned when the lead
     hand rendered). `forceMatrixRecalculationPerRender = true` on the skins fixes it; set in both stage tools.
     Stage-0 frames were unaffected (both hands were posed before the first render there) — verified by reasoning,
     and the stage-0 tool got the same fix for future runs.
  2. Batch mode boots into an untitled scene and refuses an additive `NewScene` beside it; both tools now replace an
     untitled scene (single) and go additive only when a real scene is open.
- **Process note.** At kickoff the Editor was closed, so stage 1 was driven through **Unity batch mode**
  (`-batchmode -executeMethod … RunStage1Batch`, then `-runTests`). The Editor was opened mid-stage (MCP reconnected,
  batch locked out), and the final runs are through the Editor over MCP. Same code, same numbers both ways.

## 1.2 The numbers — one-k solve (§3.12.3 start values, §3.12.4 axis), Hand-local millimetres

Contact = 13.575 + 6.83 = **20.405 mm**, tolerance ±1.5. "closest wrapped" = min(PIP, mid segment, distal segment)
— the proximal segment is excluded because its MCP end sits at contact by construction. "min all" = min over all
three segments. "bone in mesh" = min all < 13.575 (a phalanx bone inside the grip mesh).

| Hand | Finger | k | closest wrapped | Δ vs contact | seg prox / mid / dist | joints MCP / PIP / DIP / tip | min all | bone in mesh | solver note |
|---|---|---|---|---|---|---|---|---|---|
| lead | index | 0.600 | **6.62** | −13.78 | 7.07 / 6.62 / 25.37 | 24.97 / 7.34 / 25.37 / 51.55 | 6.62 | **yes** | inside even at k = 0.6 |
| lead | middle | 0.600 | **15.23** | −5.18 | 13.60 / 15.23 / 29.62 | 22.71 / 15.72 / 29.62 / 54.78 | 13.60 | no | inside even at k = 0.6 |
| lead | ring | 0.600 | **15.92** | −4.49 | 15.41 / 15.92 / 26.52 | 22.76 / 17.04 / 26.52 / 47.96 | 15.41 | no | inside even at k = 0.6 |
| lead | little | 0.600 | **15.64** | −4.76 | 12.92 / 15.64 / 21.28 | 20.41 / 15.99 / 21.28 / 34.68 | 12.92 | **yes** | inside even at k = 0.6 |
| trail | index | 0.839 | **20.40** | 0.00 | 14.72 / 20.40 / 34.83 | 20.41 / 20.72 / 34.83 / 50.63 | 14.72 | no | on surface |
| trail | middle | 0.746 | **20.41** | +0.00 | 19.52 / 20.41 / 28.80 | 23.64 / 23.04 / 28.80 / 47.44 | 19.52 | no | on surface |
| trail | ring | 0.730 | **20.40** | 0.00 | 17.79 / 20.40 / 28.40 | 23.26 / 22.40 / 28.40 / 41.04 | 17.79 | no | on surface |
| trail | little | fixed 40/60/30 | 17.61 | −2.80 | 15.41 / 17.61 / 19.85 | 20.41 / 18.93 / 19.86 / 32.92 | 15.41 | no | not solved (rides on the lead index) |

| Hand | Thumb2 | Thumb3 | thumb tip | proximal vs shaft | wrist → +30 mm segment (heel-pad proxy) |
|---|---|---|---|---|---|
| lead | 40.93 | **21.55** | 29.89 | 47.5° | 76.63 |
| trail | 38.86 | **21.78** | 29.94 | 44.4° | 72.63 |

Axes (Hand-local): lead `o = (0.0376, 0.0938, −0.0223)`, `d = (−0.7898, 0.6134, −0.0049)`; trail
`o = (−0.0366, 0.0898, −0.0225)`, `d = (0.9248, 0.3804, −0.0034)`. Source: `evidence/stage1/stage1_numbers.json`,
`stage1_console.txt`.

## 1.3 Why, measured — three level-1 findings

1. **The lead axis runs under the proximal phalanges.** §3.12.4 puts the lead axis through a point 0.6·L_prox
   *along* the index proximal and 20.4 mm palm-side of it. A finger's PIP sweeps a 30 mm circle about its MCP; at
   0.6 × 55° = 33° of MCP flexion the index PIP is already 7.3 mm from the axis, i.e. inside the shaft. Every lead
   finger is inside the surface at the most-open k the spec allows, so there is nothing to bisect. (Sanity:
   the same k on the trail axis, which has no u-offset, is outside — trail index k = 0.6 gives PIP ≈ 27 mm.)
2. **"Closest segment on the surface" is met by a graze, not a wrap.** On the trail hand the bisection stops at the
   first k where the PIP touches the side of the cylinder (k 0.73–0.84, MCP 37–42°) with the middle and distal
   phalanges pointing away: tips at 41–51 mm. Analytically the PIP of a 30 mm proximal reaches the contact circle
   at sin θ = 15 / c ⇒ θ ≈ 47° for c = 20.4, and is *inside* it for any larger MCP flexion; a wrap needs the PIP
   there (or inside, with the chord outside the mesh) and ~95° more at the PIP. A one-parameter scale of a triple
   whose MCP is 50–75° cannot get there without driving the PIP through the shaft.
3. **"Nothing inside" cannot be met by a wrapped finger measured on bone segments against r + half-thickness.** A
   30 mm phalanx lying as a chord between two joints on a 20.4 mm circle is 20.4 − √(20.4² − 15²) = **6.6 mm inside**
   at mid-length. The trail proximal segments already show it at the graze (14.7 / 17.8 mm). The feasible reading is
   *bone outside the grip mesh* (segment ≥ 13.575 mm): every inscribed solve below satisfies it (min 13.78 mm), the
   one-k lead index and little do not (6.6 / 12.9 mm).

Two more, informational: **the thumb tip lifts off** (Thumb3 on the surface at 21.6–21.8 mm, tip at 29.9 mm) — the
fixed 15° / 10° flexes turn about `cross(thumbDir, palmNormal)`, which for a thumb lying along the shaft on top is
*away* from the shaft; and **the heel pad is not under the axis** (73–77 mm from the wrist segment) — the §3.12.4
axis runs along the knuckle row, so the reference's "heel pad closes over the top" would need the butt-end landmark
below the little MCP toward the wrist, not at it.

## 1.4 Supplementary — the inscribed wrap on the same axis (NOT the §3.12.3 solve; data for the decision)

Per finger, proximal to distal: bend each joint until its child joint (the tip for the distal) lands on the contact
circle; hinge axes unchanged; spread kept; start from rest. `evidence/stage1/supplementary_inscribed/`.

| Hand | Finger | MCP / PIP / DIP solved | joints MCP / PIP / DIP / tip | seg prox / mid / dist | min all | bone in mesh | note |
|---|---|---|---|---|---|---|---|
| lead | index | 5.5 / 59.7 / **80.0 (cap)** | 24.97 / 20.39 / 20.41 / 23.72 | 18.88 / 15.33 / 18.15 | 15.33 | no | tip 3.3 mm short at the DIP cap |
| lead | middle | 26.2 / 84.8 / 77.0 | 22.71 / 20.39 / 20.38 / 20.41 | 17.55 / 14.51 / 16.04 | 14.51 | no | all three on the circle |
| lead | ring | 32.2 / 78.3 / 72.4 | 22.76 / 20.41 / 20.40 / 20.41 | 18.15 / 15.41 / 16.54 | 15.41 | no | all three on the circle |
| lead | little | 30.5 / 69.4 / 58.2 | 20.41 / 20.40 / 20.41 / 20.41 | 16.59 / 18.37 / 17.86 | 16.59 | no | all four on the circle |
| trail | index | 42.9 / 98.3 / 73.9 | 20.41 / 20.40 / 20.43 / 20.40 | 14.47 / 13.87 / 17.18 | 13.87 | no | all four on the circle |
| trail | middle | 55.8 / 81.7 / **80.0 (cap)** | 23.64 / 20.40 / 20.41 / 24.50 | 17.70 / 13.78 / 16.32 | 13.78 | no (0.2 mm) | tip 4.1 mm short at the DIP cap |
| trail | ring | 56.5 / 84.7 / 76.3 | 23.26 / 20.40 / 20.41 / 20.41 | 16.33 / 14.79 / 16.27 | 14.79 | no | all three on the circle |

The trail numbers are the textbook power grip (MCP ≈ 45–55°, PIP ≈ 80–100°, DIP ≈ 75°); the lead index sits
almost straight at the knuckle (5.5°) because the lead axis passes under it — consistent with the reference's
"handle runs through the middle joint of the forefinger". Thumb and little finger are as in §1.2.

## 1.5 Frames (16, all 1600 × 1600, greyscale variance 2786–6026; floor 5.0) — every one opened and looked at

Canonical screenshot: `evidence/stage1/trail_right_palm.png`

| Frame | What it shows |
|---|---|
| `evidence/stage1/lead_left_downshaft.png` / `_palm` / `_back` / `_top` | lead, one-k at k = 0.6: cylinder in the crook, proximal phalanges emerging from inside it, thumb along the top |
| `evidence/stage1/trail_right_downshaft.png` / `_palm` / `_back` / `_top` | trail, one-k: fingers hooked over the cylinder at the middle knuckle, tips out in the air, thumb along the shaft |
| `evidence/stage1/supplementary_inscribed/trail_right_inscribed_*.png` | trail, inscribed: fingers all the way round, tips back to the palm side; the down-shaft view is a closed ring around the cross-section |
| `evidence/stage1/supplementary_inscribed/lead_left_inscribed_*.png` | lead, inscribed: middle/ring/little wrapped, index nearly straight at the knuckle, cylinder enclosed in the down-shaft view |

## 1.6 Stage-1 gate (§3.12.6 row 1), by the letter

| Criterion | one-k, lead | one-k, trail | inscribed (supplementary) |
|---|---|---|---|
| Cylinder inside the curled fingers | FAIL (fingers inside the cylinder) | **partial** — hooked at the knuckle, not enclosed | PASS on the frames |
| Under the heel pad | FAIL — axis 73–77 mm from the wrist segment (axis definition) | same | same |
| Thumb along it | Thumb3 on the surface, proximal 44–48° off the shaft, tip lifts to 30 mm | same | same |
| Closest wrapped segment within ±1.5 mm | FAIL, 4/4 inside at k = 0.6 | PASS 3/3 (by a graze) | joints on the circle to 0.01 mm; chords 6–7 mm inside by geometry |
| Nothing inside (r + t) | FAIL | FAIL (proximal chords 14.7 / 17.8) | FAIL by that definition; PASS as bone-outside-mesh (≥ 13.78) |
| EditMode tests | 12 / 13 — the lead gate test is the red one | | |

Nothing above the hinge level was touched; the club, rig and anchors are untouched; the prefab is untouched.

## 1.7 Acceptance for this stage

| Item | Result | Evidence |
|---|---|---|
| §3.12.4 axis in hand space, defined not fitted | PASS | `S1_GripAxis_*` both hands; axes in §1.2 |
| Debug cylinder r = 13.575 mm on the axis, child of the Hand bone | PASS | frames; `HandHingeStage1Tool.OneHand` |
| §3.12.3 start pose + per-finger k bisection run | PASS (run) / **FAIL (gate)** | §1.2, §1.3 |
| Two angles per hand, each hand alone with its cylinder | PASS (four angles) | §1.5 |
| Numbers per finger (k, closest segment, inside) | PASS | §1.2, JSON |
| Club / rig / anchors / prefab untouched | PASS | `git status`: no `.prefab` change |
| Define off | not re-run this stage — every addition sits inside the existing `#if GOLFIN_GOLFER_TEST` regions and the `#else` shells are byte-identical to stage 0 (verified there, 1/1) | |
| Profile | `iOS-Full-Golfer` (Cesar's standing rule) | |

## Files modified or created (stage 1)

Every uncommitted path outside `Docs/Specs/Active/golfer_club_grip/` is listed.

| File | One-line summary |
|---|---|
| `Assets/Scripts/Gameplay/Golfer/HandHingeModel.cs` | stage-1 members (§1.1) + supplementary inscribed solver; `Version = stage1-b` |
| `Assets/Scripts/Gameplay/Golfer/Tests/HandHingeModelTests.cs` | three `S1_*` tests (one red on purpose, §1.1) |
| `Assets/Scripts/UI/Editor/HandHingeStage1Tool.cs` (+ `.meta`) | NEW — stage-1 tool, menu + batch entry |
| `Assets/Scripts/UI/Editor/HandHingeStage0Tool.cs` | untitled-scene handling + `forceMatrixRecalculationPerRender` (no behaviour change for stage-0 evidence) |
| `Docs/AI_CONTEXT.md`, `tasks/lessons.md` | stage-1 entry; Lesson AT |
| `Library_broken_143700/` | **Pre-existing** stale Library backup, untracked (in the iter-11 baseline block); untouched |

## What a reviewer should be sceptical about

- **The inscribed solve is per joint.** §3.12.1 blames per-joint solving for the claws; the difference here is that
  the axes are fixed and each joint is solved so its child lands ON the circle, not against a cap. The frames are the
  evidence that it does not claw; the two DIP-cap hits (80°) are the place to look.
- **"Bone outside the mesh" is my reading of "nothing inside".** The margin on the trail middle chord is 0.2 mm; if
  the gate wants flesh clearance rather than bone clearance, that finger is the first to fail.
- **The thumb clock and station are start values** (30°, 60 mm) with a convention I chose from the reference; the
  tip-lift finding depends on them only weakly (it is the flex axis, not the aim).

## Stage 1 verdict (Cesar, 2026-09-15)

**PASS on the inscribed wrap.** The per-joint inscribed solve is the stage-1 pose; the one-k solve is retired (SPEC §3.12.6 row 1 annotated). Stage 2 (two hands on one club, static, play mode) is the next kickoff — started the same day.

---

# Stage 2 — two hands on one club, static, at address (SPEC §3.12.4 / §3.12.5 / §3.12.6 row 2, iter-12, 2026-09-15)

**Iteration shape:** `hinge-model:stage2-two-hands-static`. Play mode, address, Hole 06 through the real harness path
(`GameSession.OnRoundStarted → GolferTestBootstrap → PlaceAtBall`), `Time.captureDeltaTime = 1/60`, IK on. **STOPPED at
the gate — stage 3 (the swing) is not started.**

**Verdict in one line:** with the anchors solved for the least wrist rotation the hands sit on the shaft to 0.02 mm,
overlap exactly, the butt cap and face-square rows pass, nothing interpenetrates, and the IK bends the clip's wrists
14.0° (lead) and 36.3° (trail) — under the 40° stop line. The §3.12.4 *palm-rule* roll, run as written, bends them
79° and 167° and is a stop-and-show. Two §3.9.6 rows fail by the letter (heel-pad dot, trail-palm-on-thumb band);
the numbers and four full-res frames are below for Cesar's eye.

## 2.1 What was built

- **Prefab** (`PfGolfer_MixamoNative.prefab`, saved with the define ON via `PrefabUtility`, never in play mode):
  `HandHingeModel` on the root as data (`applyEveryFrame = 1`, the stage-1 inscribed poses per finger, thumb aims
  `(−0.093, 0.981, −0.170)` lead / `(0.401, 0.904, −0.149)` trail from the rest-pose axes, 15°/10° thumb hinges);
  `GripAnchor_Lead` / `GripAnchor_Trail` with `WristTarget` children under `ClubSlot` (+Y = shaft, so a station is
  a local y); `Rig_Hands` under `GolferRig` with `IK_Lead` / `IK_Trail` (`TwoBoneIKConstraint`, root/mid/tip = upper
  arm / forearm / hand, target = the `WristTarget`, position and rotation weight 1, no hint);
  `RigBuilder.layers = [Rig_Grip, Rig_Hands]`. `ClubSlot` position re-solved (§3.12.5), its face-square roll kept.
- **`HandHingeStage2.cs`** (Editor, gated): prefab authoring, the play-mode stage (`Run`), the bake write-back, the
  face-roll fix. The verification runner hands control to `Run` at the address sample by reflection when a
  SessionState flag is set (one branch in `GolferTestVerificationRecorder`, define-agnostic). Menu:
  `GOLFIN ▸ Golfer Test ▸ Hinge ▸ Stage 2 — …` (solve palm-rule / solve min-wrist / verify / apply bake).
- **§3.12.4 anchors, closed form.** Per hand the rest-pose axis `(o, d)` in Hand-local space is carried onto the
  world shaft with `FromToRotation(d, shaftDir)`, then rolled about the shaft; the anchor sits on the axis at the
  station and carries the hand frame; `WristTarget.localPosition = −fHand` (the hand origin's foot on the axis, Hand-
  local), so the IK tip lands the hand-local tunnel on the shaft. Stations: lead = `LeftHand` origin 7.6 mm
  (10 mm·s, s = 1.328/1.75) down-shaft of `ClubStart`; trail = the little MCP's foot at the lead Index/Middle MCP gap
  (gap read from the lead hand *as anchored*).
- **§3.12.5 club solve.** Pivot about the head point (yaw about up, pitch about the horizontal normal) plus a slide
  along the aim; coarse grid ±6° / ±15 mm, four halving refines; deterministic. Cost = Σ wrist rotation the IK must
  impose + 0.5° per mm of hand displacement (see finding 2). Applied as a new `ClubSlot` local pose under `GripTarget`.
- **Runtime → prefab round trip.** The solve writes `stage2_bake.json`; `ApplyBakeToPrefab` writes `ClubSlot`, both
  anchors and both wrist targets into the prefab in edit mode; a **verify** run then measures the prefab as
  committed with no solve (the gate numbers in §2.3 are from that run).
- **`HandHingeModel`**: read-only getters for the stage tools; `Version = stage2-a`. **Tests:** the retired one-k gate
  test replaced by `S1_InscribedWrap_JointsOnContact_BonesOutsideMesh` (both hands) per Cesar's stage-1 verdict —
  **13 / 13 green** (Editor, after the prefab was baked).

## 2.2 Four findings on the way — three tooling, one model

1. **Animation Rigging binds its targets at `RigBuilder.Build()`.** Three runs with different anchors measured
   identical hands (94.8° / 116.7°, 131 / 196 mm) — the IK was pulling both hands to the spawn-time anchors (identity
   under `ClubSlot`). The old harness knew ("logged for authoring into the prefab; nothing is written from here").
   `RigBuilder.Build()` after writing the anchors fixes it; the IK then reaches the anchors to 0.0° / 0.00 mm.
   Lesson AU.
2. **A rotation-only §3.12.5 cost runs to the grid corner.** A 0.9 m club pivoted about its head moves the butt
   16 mm per degree; minimising wrist rotation alone chose ±15° and put the anchors 0.3–0.47 m from the hands, out
   of the arms' reach. The cost now adds hand displacement at 0.5° per mm (a chosen weight, stated), range ±6°.
3. **The clip's hands are a plausible grip already** (rig off, before any solve): lead knuckle row along the shaft
   at dot 0.957, palm facing away from the target (n·aim = −0.996), n·toHead = −0.004; trail knuckle row 0.770,
   palm toward the target (n·aim = 0.881); tunnel points 19.5 / 35.9 mm off the axis at stations 58 / 115 mm.
4. **The §3.12.4 roll rules do not describe this address.** "Back of the lead hand toward the head" can reach at
   most dot 0.576 here and needs a 65° roll from the clip; that roll misplaces the lead thumb, so "trail palm toward
   the lead thumb" then solves to a palm facing *away* from the target (177° from the clip). Run as written, with the
   rig rebuilt: wrist residuals **79.3° / 166.7°**, trail IK 12.6 mm short, hands 5.65 mm apart (`solve_palm_*`).
   The alternative measured: roll = the §3.12.5 objective on that DOF (closest hand frame to the clip that has the
   tunnel on the shaft), palm dots *reported* — **14.0° / 36.3°**. That is what was baked. Which roll rule stage 2
   is judged on is the Architect's / Cesar's call; the palm dots at the baked roll are in §2.3.

Two more, informational: the recorder's `club.faceSquare` read 6.1° of azimuth error after the −6.13° yaw pivot;
its own solved roll fix (−8.05° about the shaft) was applied to `ClubSlot` **with both anchors counter-rotated** so
the hands did not move — verify run: edge 90.003°, azimuth 0.000°. And ~40 mm of grip shows above the hands because
the station rule is on the `LeftHand` *origin* (7.6 mm from the butt cap), which puts the heel pad ~35 mm down the
grip — the rule as written, not a solver choice.

## 2.3 The numbers — verify run on the baked prefab (Hand-local / world mm; s = 0.759)

| Row | Value | Gate | Verdict |
|---|---|---|---|
| `grip.wrist.residual_l` (from the solve run) | IK rotated the lead wrist **14.0°** from the clip; hand origin moved 51.8 mm | < 40° stop line | PASS |
| `grip.wrist.residual_r` (from the solve run) | **36.3°**; hand origin moved 12.7 mm | < 40° | PASS (3.7° of margin) |
| IK reach | 0.0° / 0.01 mm lead, 0.0° / 0.00 mm trail | — | PASS |
| `grip.hand.onShaft_l/_r` | tunnel point 0.02 / 0.01 mm off the axis | < 3 mm | PASS |
| `grip.hands.overlap` | trail little MCP station 90.48 vs lead Index/Middle gap 90.49 mm (Δ −0.01) | ±8 mm | PASS |
| `grip.heelPad.onTop` | lead dot(−n, toHead) = **0.018** | > 0.5 | **FAIL** (rule not solved for; finding 4) |
| `grip.trailPalm.onThumb` (§3.10.6) | lead thumb 24.0 mm palm-side of the trail MCP plane (band 0 … 19.0), shaft axis at 21.6 mm (must be further); dot(n, toThumb) 0.059 | in band and shaft further | **FAIL** (5 mm over; thumb 2.4 mm beyond the axis) |
| `grip.hands.noInterpenetration` | closest lead joint to a trail index/middle/ring joint 11.87 mm | ≥ 8 | PASS |
| `grip.hands.trailLittleOnLead` | closest lead joint to the trail little finger 3.97 mm | intended contact | INFO (bone-to-bone 4 mm = flesh overlap; see frames) |
| `grip.buttCap.pastHeel` | `LeftHand` origin 7.59 mm down-shaft of `ClubStart`; lead little MCP at 35.6 mm | 6.07 … 15.18 mm | PASS |
| `grip.fingers.onShaft_l` | joints 20.4 ± 0.1 mm (index tip 23.7 at the DIP cap); bone segments ≥ 14.5 mm | joints on the circle, bones ≥ 13.575 | PASS |
| `grip.fingers.onShaft_r` | index/middle/ring joints 20.4 ± 0.1 (middle tip 24.5 at the cap); little fixed 18.9 / 19.9 / 32.9; segments ≥ 13.78 | same | PASS |
| `grip.thumb.downShaft_l` | lead thumb proximal 47.5° off the shaft, Thumb3 21.5 mm, tip 29.9 mm from the axis | info | INFO |
| `club.headAtBall` | `ClubEnd` 22.6 mm from the ball in plan (was 8.6 before the pivot) | < 50 | PASS |
| `club.faceSquare` (recorder) | edge vs aim 90.003°, azimuth 0.000°, loft −21.1° | 90 ± 5 | PASS |
| `stance.address.*` (recorder) | distance 0.738 m, clubReachesBall 0.000 m, swingsDownTheAim 0.0°, onGround 0.000 m | as before | PASS |

Solve run, for the record: unpivoted anchors cost rot 16.8° / 39.7°, disp 35.0 / 41.9 mm; refined pivot
**yaw −6.13°, pitch 0.00°, slide +19.7 mm** → rot 14.0° / 36.3°, disp 51.8 / 12.7 mm. Anchor rolls 157.5° (lead,
palm dot 0.018) / −149.5° (trail, palm dot 0.059) from the `FromToRotation` base. Baked (after the −8.05° face roll,
anchors counter-rotated): `ClubSlot` pos `(0.0055, 0.0493, −0.0851)` rot `(−0.1158, 0.8912, 0.4243, 0.1111)`; lead
anchor `(0, −0.0281, 0)` rot `(−0.3104, 0.6301, 0.3114, −0.6400)`, wrist `(−0.0597, −0.0767, 0.0222)`; trail anchor
`(0, 0.0544, 0)` rot `(−0.3908, −0.5813, −0.3963, −0.5936)`, wrist `(0.0370, −0.0897, 0.0225)`. Palm-rule run for
comparison in `stage2_solve_palm_console.txt`. Sources: `evidence/stage2/stage2_verify_numbers.json`,
`stage2_solve_minwrist_numbers.json`, `stage2_bake_minwrist.json`, `Docs/Diagnostics/_capture/golfer_invariants_mixamo.json`.

## 2.4 Frames (verify run, prefab as committed; scene-cam 1600 × 1600, gameplay 1170 × 2532; all opened and looked at)

Canonical screenshot: `evidence/stage2/verify_awayside.png`

| Frame | What it shows |
|---|---|
| `evidence/stage2/verify_awayside.png` | from behind the golfer along the aim: trail hand fully around the grip, lead hand above it, ~40 mm of butt showing above the hands, shaft down to the ball |
| `evidence/stage2/verify_targetside.png` | from the target side: trail hand in front with its palm toward the target, lead hand behind, shaft entering and leaving through the hands |
| `evidence/stage2/verify_golferseye.png` | from the head: the backs of both hands on the grip, butt top-right, shaft to the ball |
| `evidence/stage2/verify_downshaft.png` | the coach's view from in front of the golfer looking back along the shaft: both hands wrapped, trail fingers curling at the bottom, lead fingers above |
| `evidence/stage2/verify_gameplay.png` | the gameplay camera at address (what the player sees) |
| `evidence/stage2/solve_minwrist_*.png`, `solve_palm_*.png` | the two solve runs (earlier, tighter framing) for the record |

## 2.5 Stage-2 gate (§3.12.6 row 2), by the letter

| Criterion | Result |
|---|---|
| Overlap | PASS (Δ 0.01 mm) |
| Heel pad | FAIL as the dot rule (0.018); the heel pad is 35 mm down the grip by the station rule |
| Trail palm on thumb (geometric) | FAIL by 5 mm (thumb 24 mm vs band 19; shaft not further out) |
| No interpenetration | PASS (11.9 mm; trail little finger on the lead index at 4 mm, the intended contact) |
| Butt cap 8–20 mm·s | PASS (7.59 mm, band 6.07–15.18 at s = 0.759) |
| Wrist residuals reported | 14.0° / 36.3°, both under 40 (min-wrist roll); 79.3° / 166.7° under the palm-rule roll |
| Cesar's eye on the three frames | pending |

Club mount kept (§3.11.1), face-square roll kept (re-squared after the pivot), rig and anchors are data on the prefab
as §3.12.4 asks. Nothing above the anchor level was touched; the harness's `Assert` rows for face-square and stance
come from the recorder itself.

## Files modified or created (stage 2)

Every uncommitted path outside `Docs/Specs/Active/golfer_club_grip/` is listed.

| File | One-line summary |
|---|---|
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | `HandHingeModel` (data), anchors + wrist targets under `ClubSlot`, `Rig_Hands` + two IK constraints, layers `[Rig_Grip, Rig_Hands]`, `ClubSlot` re-solved + re-squared |
| `Assets/Scripts/UI/Editor/HandHingeStage2.cs` (+ `.meta`) | NEW — stage 2 (authoring, solve, measure, frames, bake, face-roll fix) |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | one reflection branch after the address sample handing the run to stage 2 |
| `Assets/Scripts/Gameplay/Golfer/HandHingeModel.cs` | read-only getters; `Version = stage2-a` |
| `Assets/Scripts/Gameplay/Golfer/Tests/HandHingeModelTests.cs` | one-k gate test replaced by the inscribed-wrap gate (Cesar's stage-1 verdict) |
| `Docs/AI_CONTEXT.md`, `tasks/lessons.md` | stage-2 entry; Lesson AU |
| `Docs/Diagnostics/_capture/*` | harness outputs (`golfer_invariants_mixamo.json`, gameplay PNGs) — the usual scratch, not committed |
| `Library_broken_143700/` | **Pre-existing** stale Library backup, untracked (in the iter-12 baseline block); untouched |

## What a reviewer should be sceptical about

- **The 0.5° per mm displacement weight is mine.** It is what stopped the pivot running to its bounds; a different
  weight moves the pivot (currently −6.1° yaw, +20 mm slide) and the residuals with it.
- **The trail wrist at 36.3° has 3.7° of margin** to the stop line, and that is with the roll chosen to minimise it.
- **The palm dots at the baked roll are ~0.02 / 0.06** — the rules the spec wrote are not satisfied, they are
  reported. If the Architect wants them as rules, finding 4 says what that costs in wrist rotation on this clip.
- **Trail little finger on the lead index at 4 mm bone-to-bone** will read as flesh overlap in a close frame.

## Stage 2 redo — Cesar at the gate: "the wrists seem to bend too much compared to real golfers" (2026-09-15)

### What the number is, and what it was

The stage measures the wrist bend directly now: the forearm-to-hand angle (elbow→wrist vs wrist→middle MCP), split
into flexion along the palm normal (+ = cupped / extended) and deviation across the hand. `grip.wrist.angle_l/_r`.

| Configuration | lead wrist (flex, dev) | trail wrist (flex, dev) | rotation added to the clip | lead station | frames |
|---|---|---|---|---|---|
| **the clip itself** (rig off) | **46.2°** (28.3, −33.0) | **41.8°** (−8.0, 40.7) | — | 58 mm (tunnel 19.5 mm off the axis) | — |
| min-wrist bake (what Cesar saw) | **61.7°** (27.7, −48.4) | **59.9°** (22.8, 50.7) | 13.3° / 35.4° | 7.6 mm (spec) | `minwrist_bake_frames/` |
| palm-rule roll (§3.12.4 as written) | — | — | 79.3° / 166.7° | 7.6 mm | `solve_palm_*` |
| "anatomy" roll (min forearm→hand angle for the roll DOF) | 41.2° (2.0, −41.2) | 44.9° (4.3, 44.5) | 110.6° / 95.4° | 33 mm | `solve_anatomy_*` — not a grip: the hands roll until the club runs along the outside of the hand |
| **wrist-angle cost, min-wrist roll, free station, wrappable axes — BAKED** | **55.0°** (23.8, −45.5) | **47.7°** (24.8, 37.6) | 13.9° / 31.9° | 39.4 mm | `verify_*` |

So the IK had been adding 15° / 18° to the actor's own wrists, and now adds 9° / 6°. The remaining bend — and it
is still ~50° — is the actor's posture (46° / 42°) plus the geometry below.

### What was changed at this level, in order, with what each did

1. **Wrist angle measured** (clip vs result), rows `grip.wrist.angle_*`; the solve now predicts it per candidate
   from the clip's forearm and the anchor hand frame.
2. **Cost = predicted wrist angle + 0.2° per mm of hand displacement** (was: rotation from the clip + 0.5°/mm);
   pivot range ±12°, slide ±20 mm. Result: the pivot stayed at ~0° / −4 mm even with cheap displacement — moving the
   club within the arms' reach does **not** straighten the wrists on this clip.
3. **Lead station freed** (5–65 mm from the butt; spec 10 mm·s): the solve chose **39 mm** (the clip holds at 58).
   The butt-cap row fails by the letter (39 vs 6–15 mm) and ~70 mm of grip shows above the hands.
4. **Trail overlap offset freed ±8 mm**: chose −7.75 (in band); lead-to-trail joint clearance dropped 11.9 → **5.4 mm**
   (`grip.hands.noInterpenetration` FAIL). Reverting the offset to 0 costs ~1° of wrist.
5. **Axis index-end offset as a DOF** (lead {0.2 … 0.8}, trail {0 … 0.6}), because the clip's knuckle rows sit 17°
   (lead) and 40° (trail) off the §3.12.4 axes and that misalignment is the deviation the IK adds. **A fraction the
   fingers cannot wrap is rejected first** — the stage-1 inscribed solve is run on the live hand per candidate:
   lead 0.8 rejected (index tip 44 mm out, the axis crosses the index at its middle joint), trail 0.4 rejected.
   Chosen: lead **0.6** (the spec), trail **0.6** (spec 0). The bake re-solves both finger poses for the chosen
   axes and writes them to the prefab (`AuthorPrefabStructureForAxes`), so pose and axis can never disagree again
   (they did once: bake 0.8 put the shaft through the lead index).
6. **Reach constraint**: shoulder→anchor-hand distance ≤ 97 % of the arm, hard penalty — the first wrist-angle run
   put the trail anchor 16 mm past the trail arm.

### Where the rest of the bend comes from (measured, not guessed)

- The actor's address already has 46° / 42° of wrist bend, mostly ulnar deviation.
- Aligning the hand-local axis to the shaft adds the knuckle-row misalignment: 17° lead / 40° trail at the spec axes;
  with the trail diagonal (0.6) the trail rotation drops to 32°.
- The club pivot cannot buy wrist angle within reach (finding 2). What would: the hands lower on the shaft line with
  the arms extended, i.e. a **shorter club for this 1.33 m character** (the driver is at 0.87 scale — the stand-in
  artefact noted in iter-9: "roster models at R2 height carry full-size clubs") or a closer stance. Both sit above
  the grip stage. Real-golfer numbers for comparison: lead wrist ~25–35° of deviation with a flat-to-slightly-cupped
  wrist; trail wrist ~20–30° cupped.

### Verify run on the committed prefab (wrist-angle bake)

hands on the shaft 0.01 / 0.00 mm · overlap Δ −7.76 mm (band ±8) · wrist residual 13.9° / 31.9° · IK reach 0.00 mm ·
heel-pad dot 0.011 (FAIL, rule not solved) · trail palm on thumb 21.4 vs band 19.0 (FAIL by 2.4 mm) · joint
clearance 5.43 mm (FAIL, ≥ 8) · butt cap 39.4 mm (FAIL, band 6.1–15.2) · fingers on the shaft: lead joints 20.4,
segments ≥ 14.5; trail joints 20.4 (index tip 23.3, middle 22.4 at the DIP cap), segments ≥ 13.2 · face square
90.08° edge / 0.08° azimuth (PASS) · head at ball 24.7 mm (PASS).

Frames (all 1600², looked at): `verify_awayside.png` (canonical), `verify_targetside.png`, `verify_golferseye.png`,
`verify_downshaft.png`, `verify_gameplay.png`; the min-wrist bake Cesar saw is kept under `minwrist_bake_frames/`.
Both bakes are in the folder (`stage2_bake_minwrist.json`, `stage2_bake_wristangle.json`); either is one
`ApplyBakeToPrefab` away.

### The decision this redo needs

The grip mechanism is doing what it can: hands on the shaft, fingers on the circle, wrists within 9° / 6° of the
actor's. The wrists still bend ~50° because the actor's do and because the club is long for the character. Options,
in order of how much they change: (a) accept this bake and take the wrist question to the club scale / stance in
stage 3's set-up; (b) keep the min-wrist bake (cleaner butt cap, 11.9 mm clearance) and the same question; (c) a
different clip or a shorter club before stage 3. Tests 13/13; nothing above the anchor level was touched.
