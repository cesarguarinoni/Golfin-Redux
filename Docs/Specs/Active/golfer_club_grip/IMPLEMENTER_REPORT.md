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
