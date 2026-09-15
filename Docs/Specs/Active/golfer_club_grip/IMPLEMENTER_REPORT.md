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

### Real-golfer reference (2026-09-15, Cesar asked)

See `reference/WRIST_ANGLES_AT_ADDRESS.md`. Published address values: lead wrist ulnar deviation ~17–20°, extension 0–20° (15–20 typical), total ≈ 20–30°; trail wrist a slight cup. Ours: clip 46.2 / 41.8, committed bake 55.0 / 47.7 — the excess is deviation (45° vs 17–20°), set by hand height over the ball and club length, not by the anchors.

## Stage 2, real size — Cesar: "scale things to real world sizes" and "a target for whoever changes the club or stance: that is you" (2026-09-15)

**Iteration shape:** `hinge-model:stage2-real-size-wrists`. Same harness path (Hole 06 through
`GolferTestVerificationRecorder.VerifyMixamoNative`, reflection hook at the address sample), same gate: §3.12.6 row 2.

### Rejection follow-up

| Defect (CESAR_REJECTION.md) | Verdict | Same-angle evidence |
|---|---|---|
| "The wrists seem to bend too much compared to real golfers" | **RESOLVED** — lead 27.5° (extension 26.4, deviation 6.9), trail 16.2° (extension 10.2, deviation 12.4). Published address values (`reference/WRIST_ANGLES_AT_ADDRESS.md`): lead total ≈ 20–30°, trail a slight cup. The committed bake was 55.0° / 47.7°, the clip itself is 46.2° / 41.8°. | `evidence/stage2/verify_awayside.png` (the angle Cesar judged), `verify_targetside.png`, `verify_stance_targetside.png` (full body) |

Canonical screenshot: `evidence/stage2/verify_stance_targetside.png`

### 3.1 Real-world size (done first, Cesar's note)

- Prefab root scale 1.05156 → the body is **1.75 m** (was 1.664 m at the 0.759 stand-in scale, 1.33 m as Cesar saw it).
  Clubs at **native** scale (lossy 1.0; driver 1.0635 m butt to head, grip radius 13.58 mm) — driver/putter
  `localScale` 0.95097 under the scaled root, `ClubStart` y −0.03908, `ClubEnd` y 0.97227.
- `GolferPresenter.PlaceAtBall` now scales `addressHeadLocal` by the root scale (`Vector3.Scale(addressHeadLocal, transform.localScale)`);
  the verify run lands the head **0.00 mm** from the ball.
- `HandHingeModel`: `CharScale = 1.0` (mm·s = mm), `FingerHalfThicknessM = 0.00718`, `ContactM = ShaftRadiusM (0.013575) + FingerHalfThicknessM`;
  `HandHinge_MixamoNative.asset` re-captured at the new scale; finger poses re-solved at the real-size contact circle.
  Tests **13/13** (`Golfin.Gameplay.Golfer.Tests`, EditMode, 0.65 s).

### 3.2 What moved the wrists — the club pivot with the IK in the loop

The §3.12.5 predictor uses the clip's forearm, so it cannot see where the IK puts the elbow once the anchors move; the
committed solve had run to the edge of what the predictor could see (55° / 48°). Replaced the predictor step with a scan
that applies each candidate, rebuilds the rig (Lesson AU), and reads the post-IK wrists:

1. **Coarse grid**: lead station {44 (the solved), 30, 20, 12 mm} × yaw about the head {0, −4, −8, −12, +4, +8°} ×
   pitch about the head {0 … −7°} × trail-gap offset down the shaft {0, 4, 8, 12 mm} — 672 configurations, ~6 frames each.
2. **Local refinement** around the coarse best: ±4 mm station, ±2° yaw, ±1° pitch, ±2 mm gap — 225 more.
3. **Pick rule = the rows this report grades**, as hard constraints, with the wrist sum as the only objective: both
   tunnel points on the shaft ≤ 3 mm (the post-IK truth of reach), lead-to-trail joint clearance ≥ 8 mm, every finger
   bone segment outside the shaft mesh, trail little MCP within ±8 mm of the lead gap, hands ≥ 140 mm above the knees
   (§3.3 below — unmet by every configuration, so the scan reports it and keeps the least wrist bend among the
   grip-feasible ones).

Why the rule set grew one row at a time (each caught by the next run, all in `evidence/stage2/stage2_solve_pitchscan_console_*_prev.txt`):
a coordinate-descent gap sweep after the best pitch cleared the joints at 12 mm but pushed the trail hand 5.9 mm off the
shaft — the trail arm is the reach limit, so the gap had to be a scan dimension, not a fix-up; the coarse best then left
the trail hand 1.9 mm short and its middle-finger chord 1 mm inside the mesh (→ refinement + finger constraint); the
refined best sat 0.03 mm outside the overlap band (→ overlap constraint). Lesson: grade by the same rules you pick by.

**Result**: station **16 mm**, yaw **−6°**, pitch **−5°**, trail gap **10 mm**, axes 0.6 / 0.6 (unchanged, both wrappable).
Face roll: the recorder solved −9.15° after the bake (azimuth 6.47°, over ±5); `ApplyFaceRollFix(−9.15)` → **0.000°**.

### 3.3 The numbers — verify run on the prefab as committed (world mm, s = 1.0)

| Row | Value | Verdict |
|---|---|---|
| grip.wrist.angle_l — lead forearm→hand | **27.5°** (ext 26.4, dev −6.9) — clip 46.2, committed bake 55.0; real 20–30 | INFO (in the envelope; extension at the top of the 0–20 band, deviation under the 17–20 reference) |
| grip.wrist.angle_r — trail | **16.2°** (ext 10.2, dev 12.4) — clip 41.8, committed 47.7; real "slight cup" | INFO |
| grip.wrist.residual_l / _r | 7.9° / 28.8° from the clip; IK reached both anchors to 0.0° / 0.01 mm | PASS / PASS |
| grip.hand.onShaft_l / _r | 0.00 / 0.01 mm off the axis (< 3) | PASS / PASS |
| grip.hands.overlap | Δ 6.93 mm (±8) | PASS |
| grip.hands.noInterpenetration | 8.50 mm (≥ 8) — committed bake had 5.4 | PASS |
| grip.buttCap.pastHeel | 16.0 mm (8 … 20) — committed bake had 39 mm | PASS |
| grip.fingers.onShaft_l / _r | segments ≥ 15.65 / 14.43 mm vs mesh 13.58; joints on the 21.8 mm contact circle | PASS / PASS |
| club.faceSquare | 0.000° azimuth after the roll fix (was 6.47°) | PASS |
| club.headAtBall | 0.00 mm | PASS |
| grip.heelPad.onTop | dot 0.138 (> 0.5) — the §3.9.6 rule this solver never targets; failed in every stage-2 bake | FAIL |
| grip.trailPalm.onThumb | thumb 23.31 vs shaft 23.05 mm — 0.26 mm the wrong side; failed in every stage-2 bake (2.4 mm at the committed one) | FAIL |
| grip.hands.aboveKnees (new) | lowest hand joint **70.7 mm** above the knee joint (floor 140; the clip's own value is 80.9) | FAIL — see 3.4 |
| stance.handsHeight (new) | hand origins 755 mm over the ground; real golfers ≈ 0.75–0.90 m with a driver | INFO |
| club.shaftElevation (new) | 47.1° above horizontal; a driver at address ≈ 50°, static lie 55–60° | INFO |
| grip.arms.clearLegs (new) | nearest arm bone to a leg bone 166.5 mm centreline (L forearm ↔ R thigh) | INFO — Cesar withdrew the elbow note |

### 3.4 Cesar's note during the run: "as you straighten the grip, move the arms higher so they don't collide with the knees when swinging"

Measured before deciding. The straightening lowered the hands **10 mm** relative to the clip (71 vs 81 mm above the knee
joint); the low hands are the actor's stance, not the solve. The club pivot cannot buy height without giving the wrists
back — from the 897 scanned configurations, best wrist sum at each hands-above-knees floor:

| floor (mm) | best wrist sum | lead (ext, dev) | trail | configuration |
|---|---|---|---|---|
| ≤ 70 | **43.6°** | 27.4 (26.4, −6.9) | 16.2 | st 16, yaw −6, pitch −5, gap 10 (baked) |
| 80 | 59.8° | 30.7 (29.6, −7.5) | 29.1 | st 16, yaw −8, pitch −4, gap 10 |
| 90 | 72.2° | 35.3 (33.8, −9.1) | 36.9 | st 16, yaw −10, pitch −3, gap 8 |
| 100 | 83.2° | 39.4 (37.5, −10.4) | 43.8 | st 12, yaw −12, pitch −2, gap 8 |
| ≥ 110 | none | | | (the clip's own wrists sum to 88°) |

≈ 16° of wrist per 10 mm of hand height. What the geometry says: hand height is set by the arm hang from the shoulders
(the actor bends over a lot — `verify_stance_targetside.png`), and with the hands at 0.755 m and a 1.01 m shaft the
elevation is forced to 47° by Pythagoras. A longer club does not raise the hands (the golfer just stands further from the
ball); a steeper club does, at the wrist cost above. Raising the hands **without** the wrist cost needs the shoulders
higher — less torso bend or less knee flex in the address pose — which is a stance edit on the clip, one level up from
the club pivot. The floor (140 mm, derived from a real 1.75 m golfer: wrists 0.75–0.90 m, trail fingertips ≈ 0.16 m
down the shaft line, knee 0.50 m) stays in the scan as a stance-level target so the next person sees it in the log.

### 3.5 Frames (verify run, prefab as committed; scene-cam 1600 × 1600, gameplay 1170 × 2532; greyscale variance 771–4198, floor 5.0; all opened and looked at)

`verify_stance_targetside.png` (canonical — full body down the target line: arms hanging straight, hands at knee height,
shaft 47°, head on the ball), `verify_stance_faceon.png`, `verify_awayside.png` (the grip from the trail side — the angle
of the rejection), `verify_targetside.png`, `verify_golferseye.png`, `verify_downshaft.png`, `verify_gameplay.png`
(the real Hole 06 camera; the roster stand-in's head is the same asset as before). The scan's own frames are
`solve_pitchscan_*.png`; the pre-roll-fix verify is `stage2_verify_console_prefaceroll.txt`; the committed
wrist-angle bake's verify is kept as `stage2_verify_console_wristangle_prev.txt`.

### 3.6 Stage-2 gate (§3.12.6 row 2), by the letter — unchanged in structure from 2.5; what changed

Hands on the shaft, no interpenetration, butt cap in band, fingers outside the mesh, face square, head on the ball: all
PASS at real size (the committed bake failed clearance and butt cap). Wrists in the real-golfer envelope. Open by the
letter: heel-pad dot and trail-palm-on-thumb (both pre-existing, both untargeted by this solver), and the new
hands-above-knees floor, which is Cesar's call: accept the hands where the actor holds them (10 mm under the clip), or
authorise a stance edit before stage 3.

### Files modified or created (stage 2, real size)

| File | Change |
|---|---|
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | root scale 1.05156, clubs native, ClubSlot/anchors/wrist targets from the pitch-scan bake, face roll −9.15°, `addressHeadLocal`, finger poses re-solved (saved define ON) |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/HandHinge_MixamoNative.asset` | re-captured at real size |
| `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` | `PlaceAtBall` scales `addressHeadLocal` by the root scale |
| `Assets/Scripts/Gameplay/Golfer/HandHingeModel.cs` | `CharScale` 1.0, `FingerHalfThicknessM`, `ContactM` from the shaft radius |
| `Assets/Scripts/UI/Editor/HandHingeStage2.cs` | pitch scan with the IK in the loop (coarse + refinement, rule-set pick), `FingerSegMin`, `KneeClear`, `ArmLegClear`, stance rows, full-body frames, bake fields `trailGapOffsetM` / `scanYawDeg` |
| `Docs/Specs/Active/golfer_club_grip/evidence/stage2/*` | pitch-scan consoles (final + the four `_prev` steps), `stage2_bake.json` / `stage2_bake_pitchscan.json` (+ `_noknee` = identical, kept for the record), `solve_pitchscan_*.png`, `verify_*.png` (7), `stage2_verify_*` |
| `Docs/Specs/Active/golfer_club_grip/{IMPLEMENTER_REPORT,STATUS}.md`, `HEARTBEAT.log` | this section; STATUS → `STAGE_2_REVIEW` (real size) |
| `Docs/AI_CONTEXT.md`, `tasks/lessons.md` | session entry; Lessons AW, AX |

Not mine, left alone: `Assets/Art/3D/Characters/_Test/Olivia/*` (+ `.meta`), `Library_broken_143700/`.

## Stage 2, stance edit — Cesar: "Stance edit. Hands clearly touch the knees in that pose." / "follow all the guidelines to keep a real golfer pose" (2026-09-15)

**Iteration shape:** `hinge-model:stage2-stance-edit`. Outcome up front: the stance rig is built and measured, the
posture guidelines are encoded as rows, and on this actor **straight wrists and a guideline hand position cannot both
be had** — the shipped prefab keeps the verified straight-wrist bake (27.5° / 16.2°) with the stance data at zero, and
`FINDINGS_FOR_NEXT_CHARACTER.md` carries what the next model must bring. Cesar's model switch is the fix.

### Rejection follow-up

| Defect | Verdict | Evidence |
|---|---|---|
| "Hands clearly touch the knees in that pose" | **STILL-PRESENT on the shipped bake, measured** (lowest fingertip 71 mm above the knee joint, 137 mm from it, hands 67 mm off the thigh surface). The stance rig removes it (knees 31° → 16/18°, fingertips 107–139 mm, 172–225 mm from the knee) but only at the wrist cost in the table below. Decision handed to the model switch. | `verify_stance_targetside.png`, `verify_awayside.png`, scan tables in `evidence/stage2/stage2_solve_pitchscan_console_stance*.txt` |

Canonical screenshot: `evidence/stage2/verify_stance_targetside.png`

### 4.1 What was built (data-driven, stays on the prefab, zero = no-op)

- **Posture guidelines** looked up and filed (`reference/WRIST_ANGLES_AT_ADDRESS.md` § posture): torso forward tilt
  25–45° from vertical, knee flex 15–25°, arms hanging (≤ 20° from vertical), hands 6–8 in off the thighs with a
  driver, hands under to just in front of the chin. Encoded as `stance.torsoTilt / kneeFlex / armHang /
  handsFromThighs / handsUnderChin` rows, graded PASS/FAIL, plus `stance.handsHeight`, `club.shaftElevation`,
  `stance.hipsLift`, `stance.spineBend`.
- **Stance rig** (`AuthorPrefabStructure`): `Rig_StanceFeet` (MultiParent copies of both feet into targets, first
  layer) and `Rig_Stance` (`Stance_Hips` OverrideTransform position offset in Pivot space; `Stance_SpineBend`
  OverrideTransform rotation about the target line in Pivot space; `Stance_LegL/R_IK` two-bone IK to the foot
  targets), evaluated before `Rig_Grip` and `Rig_Hands`, so the clip's hands, GripTarget and the arm IK all see the
  adjusted torso. Pivot space post-multiplies in the bone's own frame, so the edit is body-relative through the swing.
- **Stance sweep** (`RunSolve("stancescan")`): hips lift {0…100 mm} × spine bend {−5…15°} with the clip's hands,
  rig hands off; prints the posture rows per cell; picks the smallest edit with knee flex and torso tilt in band and
  room for the scan; writes `stage2_stance.json`; `ApplyStanceToPrefab` writes it into the prefab.
- **Scan DOFs**: per-row penalties (fewest violated rows win the tie), a **stand-closer** translation of the club
  toward the golfer (rotations about the head cannot express it; without it the stance left 29 feasible
  configurations, all reaching 38° forward), the 3D hand-joint-to-knee-joint clearance (≥ 160 mm), and the posture
  rows as constraints. Knee floor re-derived from the guideline geometry: **100 mm** (the 140 came from a standing
  wrist height), real ≈ 100–130.

### 4.2 The sweep (clip hands; `stage2_solve_stancescan_console.txt`)

| lift / bend | torso tilt | knee flex | arm hang | hands off thighs (surface) | hands vs chin | fingertips over knee |
|---|---|---|---|---|---|---|
| 0 / 0 (the clip) | 31.9° ✔ | **31 / 32°** | 17 / 21° | 146 mm | −31 mm ✔ | 133 mm |
| 20 mm / 0 | 31.9° ✔ | **16 / 18°** ✔ | 17 / 21° | 161 mm ✔ | −31 ✔ | 149 |
| 20 mm / +5° | 27.8° ✔ | 16 / 18° ✔ | 20 / 24° | 181 ✔ | +16 ✔ | 175 |
| 20 mm / −5° | 36.0° ✔ | 16 / 18° ✔ | 15 / 18° ✔ | 139 | −78 | 125 |
| ≥ 40 mm | — | 0.3° (legs locked, feet leave the ground) | | | | |

A 20 mm hips lift with the feet pinned is the whole knee fix: knees from 31° to 16–18°, kneecaps back, and it costs
nothing else. The spine bend is a free choice inside the band.

### 4.3 The scans on the lifted stance — the conflict, in numbers (`stage2_solve_pitchscan_console_stance{5,0,-5}_*.txt`)

Each scan: station × yaw × pitch × trail gap × stand-closer (600–1000 configurations + refinement), IK rebuilt per
candidate. "Least wrist" = the best wrists among configurations meeting every grip row and both knee rows;
"posture-first" = the best wrists among those also meeting arm hang ≤ 20°, hands ≥ 150 mm off the thighs, hands
≥ −50 mm from the chin (there were none — these are the fewest-violation picks).

| stance | least wrist: wrists | its hands off thighs / vs chin / arm hang | posture-first: wrists | its arm hang |
|---|---|---|---|---|
| lift 20 / +5° | **25.8° / 9.0°** | **39 mm** / −134 mm / 26° | 46.3° / 54.2° | 40° |
| lift 20 / 0° | 31.7° / 28.3° | 70 mm / −119 mm / 30° | 45.7° / 53.7° | 38° |
| lift 20 / −5° | 44.9° / 51.0° | 106 mm / −115 mm / 39° | 46.6° / 55.5° | 36° |
| shipped (no lift, pre-stance bake) | **27.5° / 16.2°** | 67 mm / −116 mm / 26° | — | — |

Reading: with both hands on the shaft (≤ 3 mm) the **trail arm is fully extended in every feasible configuration**;
for this actor's shoulders and arm length with a 1.06 m club, the hands can be either out in front where the
guidelines put them (arms reaching, wrists at the clip's 46° / 54°) or pulled in under the chest where the wrists
straighten (26° / 9°) with the hands 40–70 mm off the thighs. No stance inside the torso band moves that line;
the trail reach is the property that would have to change — the actor's arm length, shoulder position, or a shorter
club. That is § 4 of `FINDINGS_FOR_NEXT_CHARACTER.md`.

### 4.4 What ships (verify run, prefab as committed; `stage2_verify_console.txt`)

The pre-stance bake restored with the stance data at zero (`ApplyStanceToPrefab` zeros → `ApplyBakeToPrefab` from
`stage2_bake_pitchscan_prestance.json` → `ApplyFaceRollFix(−9.15)`), verified identical to the earlier real-size
run: wrists 27.5° / 16.2°, hands on the shaft 0.00 / 0.02 mm, clearance 8.49 mm, butt cap 16 mm, fingers outside
the mesh, face 0.000°, head on the ball 0.03 mm. The zero-data stance rig (feet copies + leg IK + zero offsets)
changed no row — it is safe to leave on. Posture rows on this pose, by the letter: torso 31.9° PASS; knee flex
31 / 32° FAIL; arm hang 26 / 11° FAIL; hands 67 mm off the thighs FAIL; hands 116 mm behind the chin FAIL;
fingertips 71 mm over the knee / 137 mm from it FAIL. Those five rows are the measured form of "hands clearly touch
the knees". Frames re-rendered: `verify_stance_targetside.png` (canonical), `verify_stance_faceon.png`,
`verify_awayside.png`, `verify_targetside.png`, `verify_golferseye.png`, `verify_downshaft.png`, `verify_gameplay.png`.

### 4.5 Corrections to the previous section

- "The straightening lowered the hands 10 mm relative to the clip" compared a rest-pose-finger clip (81 mm) with a
  posed bake; with posed fingers the clip's fingertips are 133 mm over the knee and the solve lowered them **62 mm**.
- The knee floor is 100 mm, not 140 (derivation in the code comment on `KneeClearM`).

### Files modified or created (stance edit)

| File | Change |
|---|---|
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | `Rig_StanceFeet` + `Rig_Stance` (zero data), layers `[Rig_StanceFeet, Rig_Stance, Rig_Grip, Rig_Hands]`; pre-stance bake + face roll restored (saved define ON) |
| `Assets/Scripts/UI/Editor/HandHingeStage2.cs` | stance rig authoring (`CopyBone`), `stancescan` mode, `ApplyStanceToPrefab`, `Posture` + guideline constants, posture rows, stand-closer DOF, per-row penalties, `HandKneeMinM`, knee floor 100 mm, grid re-centred |
| `Docs/Specs/Active/golfer_club_grip/FINDINGS_FOR_NEXT_CHARACTER.md` | for the Architect (committed e602105f8) |
| `Docs/Specs/Active/golfer_club_grip/reference/WRIST_ANGLES_AT_ADDRESS.md` | § posture guidelines with sources |
| `Docs/Specs/Active/golfer_club_grip/evidence/stage2/` | `stage2_stance*.json`, `stage2_solve_stancescan_console*.txt`, `stage2_solve_pitchscan_console_stance*.txt` + `_prestance`, `stage2_bake_pitchscan_stance0_fullyaw.json`, `stage2_verify_console_prestance_bake.txt`, `solve_pitchscan_*.png` (last scan), `verify_*.png` (restored bake) |
| `Docs/Specs/Active/golfer_club_grip/{IMPLEMENTER_REPORT,STATUS}.md`, `HEARTBEAT.log`, `Docs/AI_CONTEXT.md`, `tasks/lessons.md` | this section; STATUS; session entry; Lesson AY |

Not mine, left alone: `Assets/Art/3D/Characters/_Test/Olivia/*` (+ `.meta`), `Library_broken_143700/`.

## Olivia — first roster-likeness model through the same pipeline (2026-09-15, OLIVIA_RIG_HANDOFF.md §3)

**Iteration shape:** `character:olivia-first-pass`. Handoff §3 steps 1–5 done; FINDINGS §8 run in order.
**Stopped at step 3 (stage 2), red on four rows** — listed with their causes below; steps 1–2 are green.

Canonical screenshot: `evidence/olivia/stage2/verify_stance_targetside.png`

### 5.1 What was built (nothing hand-authored; one switch selects the character)

- `GolferTestCharacter` (Editor, ungated): `Name` in EditorPrefs (`MixamoNative` default / `Olivia`), every path
  derived (`PfGolfer_<n>.prefab`, `HandHinge_<n>.asset`, `GolferTest/PfGolfer_<n>` for the harness, `evidence/<n>/stageN`).
  Menu `GOLFIN/Golfer Test/Character/…`. The stage-0/1/2 tools, the verification recorder and the tests read it;
  `HandHingeModelTests` runs one fixture per character (**26/26**, 13 each).
- `GolferTestCharacterBuilder.BuildOlivia()` (Editor, gated): imports (`Olivia_TPose.fbx` Humanoid / Create From This
  Model, `useFileScale` on, global scale 1, axis conversion baked, no materials; the four clips Humanoid / Copy From
  Other Avatar = Olivia's, root orientation/height/XZ kept, no loop — the §5.1 settings read off Remy's `.meta`),
  `M_Olivia.mat` (URP Lit, base + normal; the Meshy `_metallic_roughness.png` is glTF-packed — G roughness / B
  metallic — and URP Lit wants metallic in R / smoothness in A, so it is left out: metallic 0, smoothness 0.35;
  texture cleanup is out of scope), `AnimatorController_Golfer_Olivia.controller` (Remy's copied, every state's
  motion swapped for her clip of the same name — clip avatar = character avatar, §9.8), `PfGolfer_Olivia.prefab`
  (root scale **1**, `lossyScale = 1`; Animator with her avatar and controller; `GolferPresenter` with Remy's values;
  `RigBuilder`; the FBX as a nested instance; Remy's `ClubRoot` subtree copied with the slot chain ×1.05156 and the
  clubs at scale 1 so the driver stays 1.0635 m; `GolferRig/Rig_Grip/GripTarget_Constraint` on her hands).
- Then the tool chain as for Remy: `HandHingeStage0Tool.CaptureAsset` → `HandHinge_Olivia.asset`;
  `HandHingeStage2.AuthorPrefabStructureForAxes(0.6, 0)` → `HandHingeModel`, anchors, `Rig_Hands`, `Rig_StanceFeet`,
  `Rig_Stance`, `Rig_HandTwist`; `RunSolve("pitchscan")` → bake → `ApplyFaceRollFix` → `RunVerify`.

### 5.2 Step 1 — capture + tests: GREEN

| Measured on Olivia (stage-0 capture) | value |
|---|---|
| finger half-thickness, palmar side of the proximal phalanx, mean of index/middle/ring both hands | **8.14 mm** (L 8.3 / 8.7 / 8.4, R 7.7 / 8.4 / 7.5; little 8.0 / 7.8) |
| `ContactM` = 13.58 + 8.14 | **21.71 mm** (Remy 20.41) |
| `L_prox`, little MCP → index MCP | **52.9 mm L / 52.7 mm R** (Remy 69.7 / 66.7 — her hands are ¾ of his) |
| fist 75/95/50 (contact sheets `evidence/olivia/stage0/fist_75_95_50_*.png`) | tips 17–23 mm from the palm plane (band 8–20: index/middle in, ring/little 1.7–3.3 mm over — Remy's accepted run had one finger over too), spacing 7–17 mm, no crossing, thumb 73–79 mm |

The contact radius is now the character's: `HandHingeData.fingerHalfThicknessM` is written at capture and
`HandHingeModel.UseData` sets the static `FingerHalfThicknessM` (a `const` before) in every tool and test; an asset
without a measurement (Remy's) restores the 7.18 mm his accepted stages were solved with. Method check on Remy:
the same palmar measure reads 9.39 mm and an all-round radial mean 10.35 mm, so the number is method-dependent by
±2 mm on a low-poly hand; Olivia's per-finger spread is 7.5–8.7.

### 5.3 Step 2 — inscribed wrap: GREEN

`evidence/olivia/stage1/supplementary_inscribed/` (the accepted stage-1 model): every wrapped tip on the 21.71 mm
circle — lead index/middle/little 21.71, ring 22.61; trail index/ring 21.71, middle 22.83 (tolerance 1.5, gate ≤ 5) —
bones ≥ 16.4 mm from the axis (mesh 13.58). Two joints hit the 80° DIP cap (lead ring, trail middle) at +0.9 / +1.1 mm.

### 5.4 Step 3 — stage 2 at address on Hole 06, stance rig at zero: RED (four rows)

Five solve passes were needed before the prefab was hers rather than Remy's (each one a Remy value that had gone
unnoticed; Lesson AZ): the club hung mirrored under her hand frames (head 1.6 m from the ball) → a re-aim-to-ball
pre-step; the re-aim put the head 215 mm in the air → elevation from hand height and shaft length; she stood at
Remy's `addressHeadLocal` (0.5 m too far, 0 grip-feasible configurations) → pass-1 bake + re-scan; her clip plays the
**hands twisted about the forearm** (palm·aim +0.998 / −0.871 where Remy reads −0.9997 / +0.999, length axes equal)
→ `Rig_HandTwist` (a Pivot-space OverrideTransform per hand bone, first layer, 176.6° / 157.6° measured and stored
in the bake); her 53 mm knuckle row → trail gap grid to 8–20 mm. Consoles: `stage2_solve_pitchscan_console_{mirrored_prev,twist_prev,pass1_farball,pass2_headinair,pass3,pass4,pass5}.txt`.

Verify run, prefab as committed (`evidence/olivia/stage2/stage2_verify_console.txt`), bake `stage2_bake.json`
(station 16 mm, yaw −2°, pitch 1°, trail gap 16 mm, axes 0.2 / 0, face roll −176.4°):

| Handoff §3.4.3 row | value | verdict |
|---|---|---|
| `stance.*` on the raw clip | torso 30.3° ✔ · arm hang 18.2 / 19.4° ✔ · hands 193 mm off the thighs ✔ · hands 24 mm behind the chin ✔ · **knee flex L 24.1 / R 27.6°** (band 15–25) | **RED (knee R, the clip's)** |
| `grip.wrist.angle_l` 20–30° | **31.2°** (flex −26.6, dev 15.0); trail 12.7° | **RED by 1.2°** |
| both hands on the shaft ≤ 3 mm | 0.01 / 0.01 mm | PASS |
| IK residual ≤ 10° | lead 27.1°, **trail 47.6°** (over the 40° stop line) | **RED** — the twist correction leaves the trail hand where a 47° rotation is needed to reach a grip frame |
| `grip.hands.aboveKnees` ≥ 100 mm | 241 mm, 337 mm from the knee joint | PASS |
| `club.faceSquare` ±5° | 0.000° after `ApplyFaceRollFix(−176.4)` (the recorder solved it; 150° open before) | PASS |
| `club.headAtBall` ≤ 50 mm | 0.02 mm | PASS |
| other grip rows | overlap **Δ 16 mm** (±8) vs clearance 8.99 (≥ 8) — her 53 mm knuckle row cannot give both; butt cap 16 mm ✔; fingers outside the mesh ✔; trail palm on thumb ✔ (17.7 vs 23.3 — Remy never passed this); heel pad dot 0.04 ✗ (never solved) | **RED (overlap)** |
| shaft elevation | 51.3° (driver at address ≈ 50) | — |

What the reds are: (1) the **hand twist is Mixamo's**, not ours — the auto-rig built from a palms-forward A-pose
plays every clip with the hands rotated about the forearm; the correction is data on the prefab and a re-rig from a
palms-down T-pose reference (FINDINGS §2) removes it; the 47.6° trail residual and part of the 31° lead wrist follow
from it. (2) The **knee flex** is the clip's own 27.6° (the stance rig can lift 5 mm; not run — "stance rig at zero
first"). (3) **Overlap vs clearance** is the hand size against two Remy-tuned constants.

### 5.5 Step 4 — frames (all opened; full-res, none compressed)

`evidence/olivia/stage2/verify_stance_targetside.png` (canonical: address down the target line — head on the ball,
shaft 51°, arms hanging, torso 30°), `verify_stance_faceon.png`, `verify_awayside.png` (the grip from the trail
side — **Cesar's eye at full res**: the trail hand's palm and the lead thumb), `verify_targetside.png`,
`verify_golferseye.png`, `verify_downshaft.png`, `verify_gameplay.png` (Hole 06 camera). Stage-0 fist
sheets `evidence/olivia/stage0/fist_75_95_50_{left,right}_{palm,back}.png` and stage-1 wrap sheets
`evidence/olivia/stage1/supplementary_inscribed/*.png` (variance 3781–6957; a first-render cull on her single
body-sized SkinnedMeshRenderer returned one blank frame until `updateWhenOffscreen` + a warm-up render).
`mirrored_prev/` keeps the two frames of the club hanging behind her.

**For Cesar's eye at full res:** `verify_awayside.png` (is the trail hand's roll acceptable given the twist
correction?), `verify_stance_targetside.png` (the model at address), `fist_75_95_50_right_palm.png` (the Meshy
finger mesh under a fist), `lead_left_inscribed_palm.png` (the wrap on the 21.7 mm circle).

### 5.6 Housekeeping

- `Olivia_30k_rigtest.obj/.fbx` and `Olivia_meshy_60k_clean.obj` are **not in the folder** (nothing to delete; the
  handoff's file table lists the OBJ — it never reached the repo).
- `ShellScene` had an in-memory dirty flag (disk = HEAD) that blocked the test runner; reopened from disk, nothing saved.
- Remy's prefab was re-authored so both prefabs carry the same structure (`Rig_HandTwist` at zero); his verify run
  after it is in `evidence/stage2/stage2_verify_console.txt` (identical rows expected — see the console).
- The character switch is left on `MixamoNative`; `GOLFIN/Golfer Test/Character/Use Olivia` selects her.

### Files modified or created (Olivia)

| File | Change |
|---|---|
| `Assets/Art/3D/Characters/_Test/Olivia/**` (+ `.meta`) | the Architect's rig, clips, Meshy source and textures, refs — committed as delivered; `.meta` files carry the Humanoid import settings; `Materials/M_Olivia.mat` new |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_Olivia.prefab`, `HandHinge_Olivia.asset` | tool-built (§5.1), pass-5 bake + face roll, hand twist 176.6° / 157.6° |
| `Assets/Animations/Golfer/AnimatorController_Golfer_Olivia.controller` | Remy's controller with her clips |
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | structure re-authored (`Rig_HandTwist` at zero) |
| `Assets/Scripts/UI/Editor/GolferTestCharacter.cs`, `GolferTestCharacterBuilder.cs` | new |
| `Assets/Scripts/UI/Editor/HandHingeStage0Tool.cs`, `HandHingeStage1Tool.cs`, `HandHingeStage2.cs`, `GolferTestVerificationRecorder.cs` | character-derived paths; finger half-thickness measure at capture; `updateWhenOffscreen` + warm-up render; re-aim-to-ball (head on the ground); `Rig_HandTwist` authoring + measurement + bake/apply; butt cap in the pick rule; trail gap grid 8–20 |
| `Assets/Scripts/Gameplay/Golfer/HandHingeModel.cs`, `HandHingeData.cs`, `Tests/HandHingeModelTests.cs` | `FingerHalfThicknessM` per character via `UseData` (default restores Remy's); measured fields on the asset; one fixture per character |
| `Docs/Specs/Active/golfer_club_grip/{OLIVIA_RIG_HANDOFF,MESHY_OLIVIA_RUN_LOG}.md` | the Architect's, committed with this report |
| `Docs/Specs/Active/golfer_club_grip/evidence/olivia/**` | stage 0 / 1 / 2 consoles, numbers, frames, five bake passes |
| `Docs/Specs/Active/golfer_club_grip/{IMPLEMENTER_REPORT,STATUS}.md`, `HEARTBEAT.log`, `Docs/AI_CONTEXT.md`, `tasks/lessons.md` (AZ), `Docs/TellCode.md` (the Architect's kickoff entry, uncommitted in the tree) | this section |

## Olivia — Cesar's rejection at sight: "her hands are backwards, so is the club" / "she is backwards" (2026-09-15, same day)

### Rejection follow-up

| Defect | Verdict | Same-angle evidence |
|---|---|---|
| She is backwards (back to the ball) | **GONE.** Cause: my builder placed her FBX instance at identity; Remy's sits at a 180° yaw inside his prefab (the presenter aims the root, the Mixamo model faces −Z). Fixed on the prefab and in `GolferTestCharacterBuilder` (copies Remy's instance pose, never assumes it). | `evidence/olivia/stage2/verify_stance_faceon.png` (she faces the camera like Remy's `evidence/stage2/verify_stance_faceon.png`), `verify_stance_targetside.png` |
| Hands backwards | **GONE.** They followed the body. New rows grade it: `grip.palmSide_l` 0.998 / `_r` 0.996 (Remy 0.9997 / 0.9993); the trail-side frame shows the back of the trail hand, knuckles to the camera, as Remy's does. | `verify_awayside.png` vs `evidence/stage2/verify_awayside.png` |
| Club backwards | **GONE.** Face 0.000° after `ApplyFaceRollFix(138.8)` and the new `club.crownUp` row 0.998 (the face-square row alone cannot tell a club rolled 180° about the shaft from a square one; the crown-up direction in ClubSlot-local is taken from Remy's accepted bake). | `verify_stance_targetside.png` (head soled at the ball) |

Canonical screenshot: `evidence/olivia/stage2/verify_stance_faceon.png`

### What I got wrong, in order

The previous section's §5.4 diagnosis — "her clips play the hands twisted 180° about the forearm, a Mixamo auto-rig
defect" — was **false**. Every symptom (mirrored club, "twisted" palms, 47° trail residual, five scan passes) was one
missing transform in my own builder. I explained the symptom with a rig theory before diffing the two prefabs; the
diff that found it took one call. The `Rig_HandTwist` layer built on that theory is **removed** from the code and
from both prefabs (Remy's re-authored and re-verified). Then, chasing it, I edited a `.cs` file while the harness
was in play mode and wedged the session (Cesar: "you launched the game wrong, text labels are not resolving") — the
standing lesson, broken. Lesson AZ is rewritten accordingly.

### Verify run, prefab as committed (`evidence/olivia/stage2/stage2_verify_console.txt`; bake `stage2_bake.json`: station 20 mm, yaw −2°, pitch −3°, trail gap 6 mm, stand closer 45 mm, axes 0.2 / 0, face roll +138.8°)

| Handoff §3.4.3 row | value | verdict |
|---|---|---|
| `stance.*` on the raw clip | torso 30.3° ✔ · arm hang 15.4 / 7.2° ✔ · hands 156 mm off the thighs ✔ · 12 mm behind the chin ✔ · **knee flex R 27.6°** (band 15–25) | **RED (knee R, the clip's)** |
| `grip.wrist.angle_l` 20–30° | **17.3°** (flex 14.7, dev −8.8); trail 13.1° | RED by 2.7° under the band (straighter than the reference) |
| both hands on the shaft ≤ 3 mm | 0.02 / 2.01 mm | PASS |
| IK residual ≤ 10° | lead 7.4°, **trail 32.5°** (Remy's accepted bake: 7.9 / 28.8) | RED on the trail, as Remy |
| `grip.hands.aboveKnees` ≥ 100 mm | 211 mm, 298 mm from the knee joint | PASS |
| `club.faceSquare` ±5° | 0.000°; `club.crownUp` 0.998 | PASS |
| `club.headAtBall` ≤ 50 mm | 0.01 mm | PASS |
| other grip rows | overlap Δ 3.9 ✔ · clearance 8.8 ✔ · butt cap 20.0 ✔ · fingers outside the mesh both hands ✔ · palm side ✔✔ · heel pad dot −0.002 ✗ (never solved) · trail palm on thumb 30.7 vs 25 ✗ (Remy fails it too) | two pre-existing letters |
| shaft elevation | 46.0° | — |

Step 3 is still red by the letter on the knee, the lead wrist (under) and the trail residual — all properties of
her clip, none of the rejected defects. The earlier "overlap vs clearance" red is gone with the correct facing.

### Frames (all opened at full resolution this time, crops at 1:1 on the hands and the club head)

`verify_stance_faceon.png` (canonical), `verify_stance_targetside.png`, `verify_awayside.png`, `verify_targetside.png`,
`verify_downshaft.png`, `verify_gameplay.png`; `verify_golferseye.png` is filled by her shirt (the camera sits
inside her torso for this head/torso geometry — a tooling gap, not a pose fault). The rejected frames are kept in
`rejected_hands_backwards/`; the consoles of the wrong-diagnosis passes stay as history (`*_twist_prev`, `*_pass1…5`,
`*_palmside_backwards`).

### Still for Cesar's eye at full res

`verify_awayside.png` (the trail hand's roll), `verify_stance_targetside.png` (the model at address, knee flex),
`verify_stance_faceon.png`.

### Files (this fix)

| File | Change |
|---|---|
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_Olivia.prefab` | FBX instance at Remy's pose (yaw 180°); `Rig_HandTwist` removed; bake + face roll re-applied |
| `PfGolfer_MixamoNative.prefab` | `Rig_HandTwist` removed; re-verified unchanged |
| `Assets/Scripts/UI/Editor/GolferTestCharacterBuilder.cs` | copies Remy's model-instance pose |
| `Assets/Scripts/UI/Editor/HandHingeStage2.cs` | hand-twist authoring/measure/bake removed (a stale rig is cleaned at author time); palm-side roll rule (opt-in, off) + `grip.palmSide_l/_r` rows; `club.crownUp` row against Remy's up-local |
| `Docs/Specs/Active/golfer_club_grip/{IMPLEMENTER_REPORT,STATUS}.md`, `Docs/AI_CONTEXT.md`, `tasks/lessons.md` | this section; STATUS; context; Lesson AZ rewritten |

## Stage 2 verdict (Cesar, 2026-09-15) and the follow-ups

1. **Approved** — Olivia's address grip on Hole 06 as committed (91dffd6d1) is the stage-2 result. STATUS → `STAGE_2_PASS`.
2. **"I need to see it in movement."** `GolferTestVerificationRecorder.RecordHole06` now records the selected character (it spawned the default prefab before) into this task's `videos/`: `videos/olivia_swing_h06_2026-09-15_17-00-45.mp4` — the real Hole 06 gameplay camera, 1170 × 2532, 4.7 s: address, the power commit, backswing, top, downswing, follow-through, then the cut to the ball flight. Stills every 0.3 s of the swing phase in `evidence/olivia/swing/swing_t*.png` and the sheet `swing_contact_sheet.png`. The club stays in both hands through the swing (the grip anchors are prefab data and the arm IK runs live).
3. **Remy retired.** `GolferTestCharacter.Default = "Olivia"`; the verify and video menus say "current character"; Remy's prefab, hinge asset and evidence stay in the repo and he remains selectable from the Character menu for comparison. Nothing of his was deleted.
4. The model findings (`FINDINGS_FOR_NEXT_CHARACTER.md`) go to the Architect with Cesar.
5. The six Meshy screenshots in `Claude outputs/` are tracked; `Library_broken_143700/` is deleted.

## Video frame rate and the putter — Cesar, 2026-09-15 evening

### Frame rate: measured, and the fix is outside my remit

The clip is recorded by `BotVideoRecorder` over the Unity Recorder (the sanctioned path), not stitched from PNGs.
Frame accounting added to the harness around the video window (`GolferTestVerificationRecorder.VideoBeginDeferred/End`):

| run | rendered frames in the window (each a fixed 1/60 s simulation step) | frames the Recorder wrote | video |
|---|---|---|---|
| 17:24 (`videos/olivia_swing_h06_2026-09-15_17-24-09.mp4`) | **334** over 28.1 s wall / 9.7 s sim | **95** over 5.27 s, gaps 13–333 ms | three frames in four discarded, the discards cluster in the swing |

The harness fixes `Time.captureDeltaTime = 1/60` for determinism (SPEC §3.9.5); the recorder runs
`FrameRatePlayback.Variable` (real-time stamping, chosen so the bot videos' captions sync). Under a fixed step with
the editor at 12–20 fps, variable playback samples the wall clock and drops the simulation frames in between. A
container retime (`videos/olivia_swing_h06_realtime_60fps.mp4`, `_halfspeed_30fps.mp4`) fixes the pacing of the
frames that exist and cannot invent the missing ones — Cesar: "still drops frames at the crucial swing time".
**Fix:** `FrameRatePlayback.Constant` (the Recorder then drives `Time.captureFramerate` and writes every frame) as an
opt-in flag for fixed-step harness runs, in `Assets/Scripts/Physics/Viewer/Bot/Editor/BotVideoRecorder.cs` — the
`Assets/Scripts/Physics/` tree is under the standing zero-edit ban, so this needs Cesar's explicit exception (or the
Architect to move the recorder out of that tree). The bots keep Variable. Also found: the recorder's
one-clip-per-Unity-session guard silently blocked the second recording (reset via `GOLFIN > Capture > Reset Video
Session Guard`, its own menu).

### Putter

The drive video never shows the putter (inactive). At the putt address the recorder's own row passes:
`club.faceSquare.putt` — leading edge 92.1° vs aim, azimuth error 2.1°, solved roll −2.5°; the head's putt-address
orientation reads head.forward·aim 0.90. I added putt-address scene frames (`evidence/olivia/putt/`), but the first two
sets framed the grip end (the `Clubhead` transform's pivot sits at the club origin; the mesh is 0.78 m down the shaft)
and the third run went down the stage-2 verify path, which finishes at the drive address before the putt block runs (the putt block only executes in the full Hole 06 sequence, i.e. the video run); no head close-up exists yet. Cesar's own view stands: the putter, irons, wedges
and woods do not share the driver's orientation.

### Club orientation, scoped (Cesar: "they should all copy the driver's")

All 31 club prefabs under `Assets/Art/3D/Clubs/` share the pivot: butt at y −0.11, +Y down the shaft, `Grip` /
`Shaft_Head` → `ClubHead|Clubhead` / `Shaft` / `ClubTipPosition`; lengths driver 1.19, wood 1.18, iron 1.06–1.07,
wedge 0.96–0.97, putter 0.89–0.90. What differs is the HEAD: the face direction is a per-model mesh fact (the
recorder hard-codes `DriverFaceLocal` for `ClubHead` and `PutterFaceLocal` for `Clubhead`; nothing for irons /
wedges / woods) and the lie, and the golfer's `ClubStart/ClubEnd/addressHeadLocal` are the driver's, so a shorter club
under the same slot floats or digs. The task: one head convention (face normal along one local axis, sole at the
shaft end, lie authored) applied to every club prefab, plus per-club-type markers on the golfer. Not started.

### Video, constant playback — shipped (Cesar 2026-09-15: "reliable video at 60 (or 30) fps, not cutting — do what you have to do")

The one authorized edit under `Assets/Scripts/Physics/` since the zero-edit ban:
`Assets/Scripts/Physics/Viewer/Bot/Editor/BotVideoRecorder.cs` gains an opt-in `ConstantPlayback` (SessionState
`LoopV2SmokeBot.ConstantPlayback`, read and cleared by `Begin()` so it never leaks into a bot clip) with
`ConstantFps` (60 default, 30 allowed). When set, the Recorder runs `FrameRatePlayback.Constant` + `CapFrameRate` at
that rate, so it drives `Time.captureFramerate` and writes EVERY rendered frame; the bots keep Variable (their
realtime-stamped captions). `GolferTestVerificationRecorder.VideoArm` opts in at 60 fps with the 90 s watchdog and
re-pins `Time.captureDeltaTime = 1/60` after `End`. Guard reset (`GOLFIN > Capture > Reset Video Session Guard`)
before each run.

Result, `videos/olivia_swing_h06_2026-09-15_17-40-42.mp4` (local — `Docs/Specs/**/videos/` is gitignored; sent to
Cesar in chat): 60/1, 146 frames, every pts gap 16.7 ms (min = median = max), zero gaps > 20 ms, written frames ==
rendered frames (log: "video END: rendered 147 frames … 2.43 s sim"). The frame drops are gone. Stills at 0 / 0.5 /
1.0 / 1.5 / 2.0 / 2.4 s in `evidence/olivia/swing60/`.

**Open, deferred by Cesar ("good enough, not perfect, we fix it another day"):** the clip is 2.43 s — the harness's
own END fired after 147 rendered frames in this run, where the previous (variable) run's window spanned 546 frames /
9.22 s. The recorder wrote everything it was given; the cut is in the harness window under constant playback (the
Recorder's constant mode owns `Time.captureFramerate`, so the harness's sim-time budget and its wall-clock watchdog
no longer mean what they did). Also open: the putt head close-up (`evidence/olivia/putt/putt_head.png`) still frames
grass beside the head; the putter orientation verdict stays on the recorder's `club.faceSquare.putt` row (leading edge
92.1° vs aim) and Cesar's eye ("blade points the wrong way"), scoped with the club-orientation task above.

Cesar's verdict on this clip: "I saw the capture, good enough (but not perfect). We fix it another day. Give me this one."

### Video, takes 2 and 3 — the window was the cut (Cesar: "You cut the swinging part, that video is unusable")

Take 1 (17:40) was cut at the top of the backswing, and the recorder was not the reason: the harness's video window
was wall-clock (`Hold` = `WaitForSecondsRealtime`, 2 s at address + 4 s after the swing snap) while the simulation
steps 1/60 per rendered frame — under constant playback the editor renders 1.5–2.4 frames per wall second (the
Recorder encoding 1170×2532 at 60 fps), so 4 s of wall was 0.67 s of swing. Fix in `GolferTestVerificationRecorder`:
`HoldSim` (golfer time: `Time.time`), 1.5 s at address, 4.0 s after the swing snap; the recorder's wall-clock watchdog
raised through its existing `MaxRecordSecondsSessionOverride` (90 → 240 → 420 s, a runaway backstop only; the
window ends by `VideoEnd`). No further edit under `Assets/Scripts/Physics/`.

- Take 2, `videos/olivia_swing_h06_2026-09-15_17-48-45.mp4`: 224 frames, 3.73 s, uniform 16.7 ms — address, full
  swing, follow-through, cut to the ball; the 90 s watchdog force-stopped it 0.2 s after the cut.
- **Take 3, `videos/olivia_swing_h06_2026-09-15_17-53-56.mp4` — the deliverable:** 369 frames, 6.15 s, every gap
  16.7 ms (min = median = max), 0 gaps > 20 ms, written == rendered (log "rendered 369 frames over 243.92 s wall /
  6.13 s sim"). Address → backswing → impact → follow-through → cut → ball at rest in the rough with her re-placed.
  The 240 s watchdog closed it 0.2 s of golfer time before `VideoEnd` would have; nothing of the swing or the
  flight is missing. Stills every ~0.9 s in `evidence/olivia/swing60/`.

Known: the recorder's encode throughput drops clip over clip inside one editor session (10 → 2.4 → 1.5 rendered
fps); its own guard text says to relaunch Unity between clips. The run is force-exited from play mode when the
watchdog fires, so the putt block after the swing did not run on takes 2–3 (putt frames unchanged). The harness rows
`shot.swingPlays` / `§9.2 t=0.6 s` sample in wall time and read one frame after commit at this frame rate — probe
artefacts of the recording run, not the video; the verify run (no video) is the gate for those.

### Club orientation — diagnosis (Cesar: "fix the other clubs"; earlier: "only the driver is correct, the iron goes into the ground, the rest point the blade wrong")

**The club Cesar sees is the on-course handle SPRITE at the ball, not a 3D club.** Facts, each checked:

- The golfer carries exactly two 3D clubs, `GOLFIN_Driver` and `GOLFIN_Putter` (`GolferPresenter` has only those two
  sockets); no runtime code instantiates any iron, wood or wedge prefab, and nothing outside `Assets/Art/3D/Clubs/`
  references them. The 3D putter's face normal at the putt address points along the aim (`faceNormalWorld·aim`
  +0.97, `club.faceSquare.putt` 2.1°) — it is not backwards.
- What is drawn at the ball is `ClubHandleSpriteBinder` painting `Resources/Clubs/Controls/S_Controls_<Type>_<BRAND>`
  (1156×649, RectTransform 178×100 at y −70 under the ball). In take 3 the driver at the tee and the iron in the
  rough are those sprites: `evidence/clubs/handle_at_ball_driver_vs_iron.png`.
- The five GOLFIN templates (`evidence/clubs/controls_templates_golfin_4types.png`): **Driver** — golfer's-eye view,
  crown up, hosel top-left, shaft up-left, face edge toward the ball (the accepted one). **Iron** — the back cavity
  with the wordmark rotated 180° (the "upside-down wordmark" the art-batch spec itself lists as its item 0), which
  reads as the blade pointing into the ground. **Wood** — the SOLE, seen from below. **Putter** — top-down with the
  shaft going straight up and the face toward the bottom, i.e. away from the ball. **Wedge** — as the iron.
- Every other brand's sprite of a type was generated by Gemini from that type's GOLFIN template "keeping the first
  image's exact camera angle" (`Docs/Specs/Active/club_art_batches/SPEC.md` W2), so all 18 other brands per type
  inherit the template's wrong view: 4 templates + 72 brand sprites = 76 of the 95 (`evidence/clubs/controls_all_95.png`).
- No 2D flip/rotation fixes any of them: the iron/wedge need the hosel top-left AND the wordmark upright, which no
  in-plane transform of the current image gives; the wood shows the wrong face of the head; the putter's face must
  turn toward the ball. And the sprites are NOT renders of the 3D club prefabs — different head designs
  (`evidence/clubs/3d_models_are_not_the_sprites.png`), so they cannot be re-shot from the models either.

**The fix is the art pipeline of record:** regenerate the four GOLFIN Wood/Iron/Wedge/Putter templates with the
DRIVER template as the camera reference (W2 with `S_Controls_Driver_GOLFIN` as the first image and the type's
portrait as the second), QA the wordmark, then re-run W2 for the 18 other brands per type from the corrected
template (72 images, 60–120 s each plus the wordmark check — roughly two hours of Gemini in Cesar's Chrome).
That runs in Cesar's Gemini account; not started without his go.

### Club head under the terrain at the rough lie — FIXED (Cesar: "fix that the head of the club is under the terrain in the image with the iron head")

Cause, measured on the at-rest row of the 17:24 run: `stance.atRest.clubReachesBall` head=(17.00, **8.5405**, −17.60)
vs ball=(17.00, **8.6516**, −17.60) — the head 111 mm below the ball centre. `PlaceAtBall` grounded the golfer's
ROOT under his own feet (`stance.atRest.onGround` "ray hit TerrainRoot", 0.0000 m) while the ball rests on the top
surface at ITS xz, 0.92 m uphill; `addressHeadLocal.y` is 0, so the head sits at root height, under the lie.
**Fix** (`GolferPresenter.PlaceAtBall`): the root's Y is the top physical surface at the BALL's xz (`GroundYAt`,
RaycastAll, the ball's own colliders excluded, fallback ball.y − radius). New harness row
`stance.<tag>.headOnLie` (club head Y minus the top surface under it, want −0.02..0.12): **0.0000 m at the tee
(Tee_1) and 0.0000 m at the rough (TerrainRoot)**; the ball rests 21 mm above the same surface. The feet now follow
the slope instead (`stance.atRest.onGround` root minus ground under the root = 0.188 m on that 12° rough — foot
ground-IK is not in this task). Verified twice on the full Hole 06 sequence (18:32, 18:38).

### The ball inside the club head at address — FIXED at the tee, verification of the bake pending (Cesar: "at rest in tee off it seems the ball comes before the club")

`PlaceAtBall` put the shaft TIP (`addressHeadLocal` = ClubEnd, the heel) on the ball in plan; the driver's face
plane lies past the tip on the face side. New row `club.faceBehindBall.<tag>` off the active head mesh (readable
now: Read/Write enabled on GOLFIN_Driver.fbx and GOLFIN_Putter.fbx, the two heads the golfer carries): at the tee
address **the face plane was +20.9 mm PAST the ball centre along the aim and the face centre +34.4 mm beyond the
ball across the line** — the ball inside the head, toward the heel. Fix: `addressFaceOffsetLocal` /
`addressFaceOffsetLocalPutt` on `GolferPresenter` (golfer-local offsets from ClubEnd to the point that must land on
the ball: the face centre pushed back a ball radius + 4 mm; zero = the old tip behaviour, PfGolfer_Test unchanged;
the stage-2 bake keeps writing `addressHeadLocal` as the tip), baked (0.0344, 0, 0.0464) on Olivia. The row's
reference is the presenter's address point + ball radius (identical to the ball when placed; meaningful at the
tee-putt block where the ball is on the green). `club.headAtBall` widened 0.05 → 0.12 m (the tip is now ~58 mm from
the ball by design; the face row is the gate). Same row runs at rest (the rough) and at the putt address.

### Putter head (Cesar: "Now you just need to fix the Putter head"; then: "there is no character whatsoever during the putting camera")

Two different putters, and only one of them is ever on screen:

1. **The 3D putter in her hands** exists only at the harness's tee-side putt block and at a putt address the
   player never sees (the putting camera is top-down with no character —
   `evidence/clubs/putting_camera_putter_sprite.png`). Its face was RIGHT all along: with the original PutterSlot
   roll, live markers on the head (`evidence/clubs/putter_3d_face_markers_original_roll.png`: red = head +Z,
   blue = −Z, yellow = toward the aim) show the milled insert and the red marker toward the target-side camera and
   the GOLFIN wordmark toward the camera behind. What was wrong was the harness: `PutterFaceLocal` said the face was
   −Z (the wordmark side; prefab renders with markers: `evidence/clubs/putter_driver_face_side_markers_prefab.png`),
   and `club.faceSquare.putt` still passed because it read the Clubhead transform in Update, BEFORE this frame's
   animation/rig evaluation — the same transform read `head.forward` (0.974, 0.217, −0.063) in the row and (−0.859,
   0.261, 0.439) a few calls later in the same frame after a scene camera had rendered. Fixed: `PutterFaceLocal =
   +Z`, the putt-block rows measured after `WaitForEndOfFrame`. I had rolled the PutterSlot 180° on the wrong
   constant at 18:20 and reverted it at 19:00 once the live markers showed the face turned away; the prefab's
   PutterSlot is back at its original rotation (0.43829, −0.66156, −0.16776, 0.58489). Also measured there and NOT
   fixed (never on screen): the putter hangs from the driver-address hands 28 cm short of the address point and
   19 cm above it (`club.faceBehindBall.putt`) — the putt pose was never built for a 0.89 m club.

2. **The putter the player sees is the on-course handle SPRITE in the putting camera** (top-down, hole at the top,
   `S_Controls_Putter_<BRAND>` drawn under the ball). Every putter sprite is a top-down product view with the shaft
   pointing UP — into the ball and toward the hole; the face edge (hosel side) is toward the ball. A correct top-down
   putter at address has the face toward the ball and the shaft leaving the heel toward the player (down / down-left).
   No rotation or flip of the current sprite gives that (180° puts the face away from the ball; a vertical flip mirrors
   the wordmark), so this is the same art question as the other types: the 19 putter sprites need re-authoring
   (Gemini W2 from a corrected GOLFIN putter template, about 30 minutes) — the pipeline Cesar said to leave alone.
   Open, awaiting his call.

**Confirmation run 19:12 (full Hole 06 sequence):** `club.faceBehindBall.address` −25.5 mm / +2.7 mm PASS,
`club.faceBehindBall.atRest` −25.5 mm / +2.7 mm PASS, `stance.address.headOnLie` 0.0000 m (Tee_1) PASS,
`stance.atRest.headOnLie` 0.0000 m (TerrainRoot) PASS, `club.faceSquare` (drive) 0.000° PASS. The two putt rows
(`club.faceSquare.putt` 177.9°, `club.faceBehindBall.putt`) still read the transform the other way from what the
scene camera renders even after `WaitForEndOfFrame`; they are now INFO rows with the reason in code (never
player-visible; unresolved). Red rows in the run: `budget.tris` only (Olivia 61k tris vs the 15k limit — the
pre-existing red since her kickoff, report line 206). Not mine, left untouched in the working tree:
`Assets/Art/3D/Characters/_Test/Olivia/MixamoNative/v2/`, `Tools/character_pipeline/`.

### Putter moment, the sequence with Cesar (19:20–20:30) — Game view only

Cesar's rule from 19:30 on: the scene-camera frames are unusable (they render before the rig evaluates: broken
hands, club wrong); the Game view is the only evidence, the driver is the example, the sprites are untouched.

1. **Blade direction.** PutterSlot rolled 180° about the shaft → Cesar: "Now it's pointing the right way."
   (`golfer_h06_putter_2026-09-15_19-24-22.png`)
2. **Hands on the shaft.** The putter hung on its own slot axis while the hands wrap ClubSlot's. PutterSlot now sits
   at ClubSlot's local pose with a −90° roll that maps the putter's (toe −X, face +Z) onto the driver's (toe −Z,
   face −X): toe·toe 1.000, face·face 0.978, 19° from the roll he had approved → Cesar: "Finally. Now it's ok."
   (`golfer_h06_putter_2026-09-15_19-27-03.png`)
3. **Posture.** Finding first: the pose Cesar approved at that moment is the DRIVE address frame — the presenter's
   `CullUpdateTransforms` off the swing had frozen the transforms across the club switch, so `Address_Putt` (which
   does play `ANIM_Golf_Putt` at 9 %) was never applied to the render; under `AlwaysAnimate` the putt clip's real
   pose reads 439 mm up / 1.29 m short (the pose Cesar called unusable). So `Address_Putt` now plays the drive
   address frame deterministically (same motion and cycle offset as `Address_Drive`, Olivia's controller only).
   Then the putt stance scan (`GOLFIN/Golfer Test/Putt stance scan on Hole 06`, `PuttStanceScan`): Stance_Hips
   drop × Stance_SpineBend about the target line on the rendered pose, objective sole on the ground:

   | drop mm | bend ° | sole above ground mm | face lateral mm | torso ° | hands mm |
   |---|---|---|---|---|---|
   | 0 | 0 | 175 | −238 | 30.3 | 779 |
   | 0 | −15 | 28 | −446 | 42.5 | 722 |
   | **30** | **−15** | **−2** | **−446** | **42.5** | **692** |
   | 60 | −10 | 11 | −372 | 38.4 | 679 |
   | 120 | −5 | 0 | −303 | 34.3 | 638 |

   Pick 30 mm / −15° (torso 42.5°, inside the 25–45° band). Baked on Olivia's presenter: `puttHipsOffset`
   (−0.0007, −0.0293, −0.0065), `puttSpineBendEuler` (345.23, 359.52, 2.63), applied by `ApplyPuttStance` on the
   club switch (reflection on the constraints' `m_Data`, fenced); `addressFaceOffsetLocalPutt` (−0.446, 0, 0.0495)
   so she stands 45 cm closer for the putt; the club switch now re-places her (`HandlePutterMode → PlaceAtBall`).
   Confirmation Game view `golfer_h06_putter_2026-09-15_20-29-32.png`: head on the ground behind the ball. Face
   row at that pose: face normal·aim +0.99, azimuth 7.3° open (informational; a −7° roll would square it).
   Awaiting Cesar's read.

### Safeguards (Cesar: "put safeguards so you check instead of me having to do it for you")

A scan died on a `NotSupportedException` from `ApplyPuttStance` (the rigging `data` property is by-ref) and the
editor sat in play mode for 40 minutes behind a success-only watcher. Now: (1) `Docs/Scripts/watch_golfer_run.sh`
exits on success, any exception line, 150 s of harness silence, or timeout, printing the last harness step — used
for every run since; (2) the harness stall watchdog: every `Mark` stamps the clock, and an editor update callback
logs `[GolferVerify] STALLED after '<last step>'` and exits play mode after 180 s of silence; (3) the presenter's
stance application is fenced so an event handler can never kill the harness again. Second finding on the way: 36
identical scan samples — `CullUpdateTransforms` again; the scan and the putt rows now measure under `AlwaysAnimate`.

### Putter posture, clearance pass (20:40–21:00) and the putt motion — PARKED

Cesar on the first posture bake: "Fingers go through the leg and shaft seems to go into the skirt." The scan gained
two constraints (hands ≥ 100 mm and shaft ≥ 50 mm off the thigh surface, the drive rows' 80 mm thigh + 20 mm hand
allowances) and a third lever, the putter's lie (PutterSlot rolled about the aim line — a putter stands more upright
than a driver, and it brings the head down without moving the hands). Pick: hips −59 mm, bend −5°, lie −8°, stand
380 mm closer → sole +6 mm, hands 108 mm, shaft 109 mm, torso 34.3°. Game view
`golfer_h06_putter_2026-09-15_20-46-59.png` sent; awaiting Cesar's read.

**Putt motion (Cesar: "Make a video of the putt motion as well").** A putt-video window was added
(`GOLFIN/Golfer Test/Record PUTT motion video on Hole 06`: a beat at the putt address, the Swing trigger, 4.5 s of
golfer time at constant 60 fps). Cesar's screenshot of it: "Hands and club flip as soon as the shot starts." Cause:
the club hangs from a grip point whose offset was solved (stage 2) for the DRIVE clip's hands; `ANIM_Golf_Putt`
holds the club differently, so the same offset puts the shaft sideways the moment the putt clip plays — and it is
also why the putt clip's own address read 44 cm up / 1.3 m short. Cesar: "Stop the video ... useless with the broken
hand." Stopped. The proper fix is the stage-2 solve run on the putt clip's hands with the putter (address → swing),
then the posture scan on top; scheduled by Cesar, not started. Also on the way the stall watchdog (180 s) fired
inside the legitimately silent video window; the window must mark progress or the watchdog must yield while a
clip is armed — follow-up.

### The putt-clip grip solve (Cesar 21:05: "First order of business, do the putt-clip solve") — DONE, awaiting his read

The club hangs from `GripTarget` (MultiParent of the two hand bones, 0.5/0.5) with a slot pose solved for the drive
clip's hands; `Rig_Hands` then IKs the wrists onto `WristTarget_Lead/Trail` under `ClubSlot`. `ANIM_Golf_Putt` holds
the wrists differently, so the drive pose flipped the shaft the moment the putt clip played.
`PuttGripSolve` (`GOLFIN/Golfer Test/Putt grip solve + stance scan on Hole 06`): at the putt address, hand IK off,
animator always animating, fit the club pose so the two WristTargets land on the clip's wrists (rotation = slerp of
the two per-hand rotations, translation = mean residual; per-hand split 28.7°, fit residual 10 mm each); the pose
under GripTarget is applied to BOTH slots in putt mode (`puttSlotSolved/puttSlotLocalPosition/Rotation` on the
presenter, drive pose restored otherwise). The face is squared by rolling the PUTTER CHILD about the shaft
(−80.5°, `GOLFIN_Putter.localRotation`), never the slot the anchors hang from: face·aim 0.22 → 1.000. The posture
scan re-ran on it (lie now moves both slots): hips −88 mm, bend −5°, sole −1 mm, hands 110 / shaft 78 mm off the
thighs, torso 34.3°; placement offset (−0.377, 0, 0.163). `Address_Putt` is back on `ANIM_Golf_Putt` at 8.9 %, and
Olivia's animator gained `Address_Drive ⇄ Address_Putt` transitions on `IsPutt`, so a club switch at address
re-addresses with the putt clip (before, she held the putter in the drive pose — the backward-pointing club of the
21:07 frame). Rows at the green putt address: face·aim 0.997, face plane −25 mm, centre +1 mm, 7.5 mm above the ball.
Game view at the tee-side moment: `golfer_h06_putter_2026-09-15_21-14-15.png` (the 21:10 take was covered by a
daily-mission popup — the harness should dismiss modals before its snaps; follow-up).

### The video stutter (second order of business) — what is known

The 20:49 putt clip and the approved 17:53 drive clip are encoded identically (H.264 Constrained Baseline, level
5.2, ~12 Mbps, 60 fps, yuv420p), both have every frame 16.7 ms apart, and the putt clip has no repeated frames and
no motion jumps (frame-to-frame change never below 0.4× or above 2.5× its local median). The stutter Cesar saw is
the broken motion, not the recorder. Re-judged on a re-take of the putt motion with the solved club. Also fixed on the
way: the video windows now mark progress each second of golfer time so the stall watchdog stays quiet during a clip.
