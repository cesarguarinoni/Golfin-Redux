# golfer_club_grip — Implementer report, iter-4

**Iteration shape:** `rigging:stream-root-binding`
**Branch:** `golfer_3d_test` · baseline `c2336867c` (work continues from `5508062b3`)
**Decision implemented:** `ARCHITECT_DECISION_RIG_DEAD.md` (2026-09-09, 2nd)
**Canonical screenshot:** `screenshots/iter4_grip_address.png`
**Layout that ran:** the **re-parent** (decision step 1). The fallback (prefab variant of
`MixamoChar_TPose.fbx`) was **not** needed and was not built.

---

## 0. Headline

**Gate A0 = 0.** Zero `InvalidOperationException` from `UnityEngine.Animations.Rigging` across a
complete run — was **2,850**. Moving `GolferRig` (both `Rig`s and all three constraint GameObjects)
under `MixamoChar_TPose` fixed it exactly as the Architect predicted. The rig has evaluated for the
first time in this task's life.

What that bought, measured on the same run:

| | before (dead rig, `5508062b3`) | now |
|---|---|---|
| `club.headAtBall` | 0.7382 m (= the stance distance; not a club measurement) | **0.0198 m PASS** |
| `grip.hand.onShaft_l` | 1.0855 m | **0.0197 m PASS** |
| `grip.hand.onShaft_r` | 1.1078 m | 0.0476 m FAIL (want < 0.035) |
| §3.4 VERIFY, desired-vs-actual ClubSlot up | 130.0170° | **0.0000°** |
| club on screen | hanging underground | in his hands, head on the ball |

Three grip assertions still FAIL. They are all one geometric fact, stated in §4.

---

## 1. What I changed

| # | Change | Why |
|---|---|---|
| 1 | `GolferRig` created under `MixamoChar_TPose` (sibling of `ClubRoot`, identity local); `Rig_Grip` and `Rig_Hands` moved from the prefab root into it | Decision step 1. `RigBuilder` stays on the prefab root beside the Animator; its `layers` references survived the move (read back below) |
| 2 | `ClubSlot` **and** `PutterSlot` set to the §3.4 pose the harness solved **against the live rig** | The pose in `0daf243cf` was solved against a `GripTarget` parked at the golfer's root and is void with the rest |
| 3 | `forceGripPose` set `false` on this prefab | SPEC §3.5 requires it; it was serialized `1`. See §5 — it changed **nothing**, which is itself the useful result |

Nothing else. No code changed at all — the entire diff outside `Docs/` is one prefab.

### Read-back after the move (from the prefab asset, not from memory)

```
RigBuilder on PfGolfer_MixamoNative  layers=2
  layer rig=Rig_Grip   active=True weight=1.00  MixamoChar_TPose/GolferRig/Rig_Grip
  layer rig=Rig_Hands  active=True weight=1.00  MixamoChar_TPose/GolferRig/Rig_Hands
MPC  MixamoChar_TPose/GolferRig/Rig_Grip/GripTarget_Constraint  weight=1.00
     constrained=GripTarget  sources: mixamorig:LeftHand 0.50 + mixamorig:RightHand 0.50
     maintainPositionOffset=False maintainRotationOffset=False  pos XYZ=TTT  rot XYZ=TTT
IK_Lead   MixamoChar_TPose/GolferRig/Rig_Hands/IK_Lead   w=1.00
          root=mixamorig:LeftArm  mid=mixamorig:LeftForeArm  tip=mixamorig:LeftHand   target=GripAnchor_Lead
IK_Trail  MixamoChar_TPose/GolferRig/Rig_Hands/IK_Trail  w=1.00
          root=mixamorig:RightArm mid=mixamorig:RightForeArm tip=mixamorig:RightHand  target=GripAnchor_Trail
```

---

## 2. Acceptance (SPEC §6)

| # | Check | Verdict | Evidence |
|---|---|---|---|
| **A0** | **Rig alive** | **PASS** | **0** `InvalidOperationException` / **0** `PropertyStreamHandle` / **0** `UnityEngine.Animations.Rigging` / **0** `Exception:` lines over the whole run, counted from a marked `Editor.log` offset. Counted three times: A0 gate run, run 1, run 2 — all 0. Was 2,850 in one run at `5508062b3` |
| A1 | Package | **PASS** | `com.unity.animation.rigging` **1.3.1**, `source=Registry` — a released version, not a preview. Project compiles with the define **on** (`ShotController.OnShotResolvedImmediate` present under `iOS-Full-Golfer`) and **off** (2,765 EditMode tests discovered and executed under `iOS-Full-GPS`) |
| A2 | Prefab | **PASS** | Hierarchy per §3.2 as amended — read-back in §1 above. `RigBuilder.layers` order Grip → Hands. Club is under `ClubRoot`, not under any bone; `ClubRoot` is a child of `MixamoChar_TPose` |
| A3 | Harness run | **FAIL** | `grip.hand.onShaft_l` 0.0197 PASS · `grip.hand.onShaft_r` **0.0476 FAIL** · `grip.hands.order` **0.0188 FAIL** · `club.headAtBall` 0.0198 PASS · `grip.ikNoLegEffect` PASS (L 0.0529 / R 0.0826 vs baseline 0.0528 / 0.0915) · `grip.targetTracksHands` **0.0349 FAIL** · every §9.8 assertion still PASS · `budget.tris` still the one out-of-scope FAIL at 36,510 (unchanged). Full list in §3 |
| A4 | Frames | **PASS** | `evidence/grip/grip_address.png`, `grip_t0_6.png`, `grip_impact.png` — gameplay camera, iPhone 14 Game View, from the measurement run. Hands close-up `evidence/grip/grip_hands_closeup.jpg` (a full-res crop of the address frame, **not** a Scene-view shot — a Scene-view close-up would have cost a third play-mode run and A0 is already proven) |
| A5 | Define off | **FAIL** (partial) | Shipped-code diff **is empty**: `git diff --stat 5508062b3 -- . ':(exclude)Docs'` = **one file**, `PfGolfer_MixamoNative.prefab`, which lives under `_Test/Resources/` behind the asset gate. No `.cs` changed, no `manifest.json` change (the package was added in an earlier iteration). But the EditMode sweep is **not** green — 3 of 2,765 fail. See §6 |
| A6 | Profile | **PASS** | Active build profile restored to **`iOS-Full-GPS`** before the close-out commit, verified by the 5/5 gate tests running with `GOLFIN_GOLFER_TEST` off |
| A7 | Numbers in report | **PASS** | §5 below: both authored local poses, the measured `addressHeadLocal`, the three grip samples per hand, package version, A0 count |

**Exit state: `READY_FOR_ARCHITECT_REVIEW`, not `READY_FOR_SELF_REVIEW`.** Cesar's kickoff said
`READY_FOR_SELF_REVIEW`; hard rule 1 routes any report carrying FAIL items down the architect path
instead, and A3/A5 do carry them. Flagging the deviation rather than laundering the FAILs.

---

## 3. The run, in full

`GOLFIN > Golfer Test > Verify Mixamo-native on Hole 06 (§9.8)` — 26 PASS, 4 FAIL, 8 SKIP.

**PASS:** `spawn.exists`, `spawn.animator` (`avatarRoot=MixamoChar_TPose`, as §3.2 requires),
`spawn.presenter`, `bind.shotController`, `stance.address.distance`,
`stance.address.clubReachesBall`, `stance.address.swingsDownTheAim`, `stance.address.onGround`,
`shot.addressAtRest`, **`club.headAtBall` 0.0198 m**, `perf.frameDelta` (Δ +0.055 ms, Editor),
`tier.low`, `tier.high`, `shot.driver`, `shot.launchDeferredToImpact` (ball moved 0.0000 m at
t = 0.788 s, impact at 1.167 s), **`grip.hand.onShaft_l` 0.0197 m**, `grip.ikNoLegEffect`,
`shot.addressBeforeSwing`, `shot.swingPlays`, `shot.ballMoved` (45.14 m), `shot.golferFollowed`,
all four `stance.atRest.*`, `shot.addressAfterShot`.

**FAIL:**
- `grip.hand.onShaft_r` — 0.0476 m worst of three samples (want < 0.035)
- `grip.hands.order` — lead 0.0188 m above trail (want 0.05–0.12); L station 0.1100, R station 0.0912
- `grip.targetTracksHands` — `GripTarget` 0.0349 m from the hand midpoint (want < 0.01)
- `budget.tris` — 36,510 vs 15,000. Unchanged, out of scope, flagged as such since §9.1

**SKIP (8):** every finger assertion — the Mixamo rig has no finger bones. Expected.

---

## 4. Why the three grip FAILs are one fact

The §3.4 GEOMETRY line at address:

```
handL=(80.0482, 14.1507, -25.0206)   anchorLead =(80.0482, 14.1507, -25.0206)   handL->anchorLead = 0.0000
handR=(80.0900, 14.1565, -24.9916)   anchorTrail=(80.0630, 14.0853, -24.9771)   handR->anchorTrail = 0.0775
```

**`IK_Lead` lands its hand exactly on its anchor. `IK_Trail` misses by 7.75 cm.** Everything else
follows: the trail hand sits at shaft station 0.0912 instead of its anchor's 0.0300, so
`grip.hands.order` reads 0.0188 instead of 0.08; the right hand is therefore 0.0476 m off the shaft
segment; and `grip.targetTracksHands` reads 0.0349 because layer 1 computes `GripTarget` from the
**pre-IK** hand midpoint while layer 2 then moves only the lead hand — the post-IK midpoint the
assertion measures is a different point by construction.

The two constraints are configured **identically** — I read the solver data off the prefab rather
than assuming it:

```
IK_Lead   componentWeight=1.000  targetPositionWeight=1.000  targetRotationWeight=0.000  hint=<null>
          chain LeftArm->LeftForeArm->LeftHand    upper=0.2370 fore=0.2269  maxReach=0.4639 m
IK_Trail  componentWeight=1.000  targetPositionWeight=1.000  targetRotationWeight=0.000  hint=<null>
          chain RightArm->RightForeArm->RightHand upper=0.2401 fore=0.2163  maxReach=0.4564 m
```

Same weights, same absent hint, near-identical arm lengths. So the asymmetry is **geometric, not a
wiring defect**: with the club posed so that `ClubEnd` sits on the ball *and* `GripAnchor_Lead` sits
in the left palm, `GripAnchor_Trail` — 8 cm further down the shaft — is somewhere the right arm does
not put its hand.

That is precisely the stop condition SPEC §3.4 writes: *"If (a) and (b) cannot both hold within the
§6 tolerances the clip's hands are somewhere the golfer's real hands could not be — report it with
the frame and stop; do not bend the golfer to fit."* So I stopped.

**What I did NOT do, deliberately:** move `GripAnchor_Trail` up the shaft until the number passes.
That is tuning a constant until an assertion goes green, which §3.4 forbids and which is how three
earlier runs of this task went wrong.

**Honest limit on this conclusion.** I have not *proven* reach exhaustion — that needs the right
shoulder's world position at address, which nothing logs. One harness line (`RightArm` world
position and `|RightArm → anchorTrail|` beside `maxReach = 0.4564`) would settle it in the next run
and turn this from a strong inference into a measurement. I am not adding it unasked.

---

## 5. Numbers (A7)

**Both authored local poses under `GripTarget`** — identical, and both **solved by the harness
against the live rig**, not eyeballed:

| slot | localPosition | localEuler |
|---|---|---|
| `ClubSlot` (driver) | `(-0.00605, 0.11099, 0.05487)` | `(333.580, 216.836, 135.890)` |
| `PutterSlot` | `(-0.00605, 0.11099, 0.05487)` | `(333.580, 216.836, 135.890)` |

The putter pose is **copied, not solved**: the harness only solves the driver, on Hole 06. Both
clubs hang off `GripTarget` with the shaft on local +Y, so the same pose gives the same grip
orientation — but the putter is a shorter club, so its head-at-ball geometry is **unverified**.
Calling that out rather than implying two solves.

**§3.4 VERIFY, the run after authoring** — the pose landed dead on:

```
desired ClubSlot up=(-0.1851, 0.8184, -0.5441)   actual=(-0.1851, 0.8184, -0.5441)   angle=0.0000 deg
desired ClubEnd =(80.2166, 13.4060, -24.5255)    actual =(80.2166, 13.4060, -24.5255)
ball=(80.2103, 13.4343, -24.5443)   ClubEnd is 0.0282 m below the ball in Y, 0.0198 m off in plan
```

**Measured `addressHeadLocal`** (SPEC §3.5). Computed from the logged address geometry —
golfer at `(80.0673, 13.4343, -25.2685)`, forward `(-0.9949, 0, 0.1011)`, `ClubEnd` as above:

> **`(0.7543, -0.0283, -0.0734)`**  — currently serialized `(0.735, 0, -0.069)`

**I did not author it.** Changing it moves the golfer's address placement, which changes the §3.4
solve, which changes this number — a loop that needs one deliberate pass, not a same-session
edit. `club.headAtBall` already PASSes at 0.0198 m without it. Architect's call.

**The three grip samples per hand** (address / t = 0.6 s / impact, distance to the `ClubStart →
ClubEnd` segment; the harness reports the worst of the three):

| hand | worst of three | verdict |
|---|---|---|
| left (lead) | 0.0197 m | PASS (< 0.035) |
| right (trail) | 0.0476 m | FAIL |

**`grip.ikNoLegEffect`** — L **0.0529 m** (baseline 0.0528 ± 0.010), R **0.0826 m** (baseline
0.0915 ± 0.010). Held in every run this task has ever produced, live rig or dead. §1's worry that
the IK would disturb the legs has never once materialised.

**Animation Rigging package:** `com.unity.animation.rigging` **1.3.1**, `source=Registry`. Not a
preview build.

**A0 count:** **0**, three times (gate run, run 1, run 2). Previous value 2,850 per run.

---

## 6. Two results that are worth as much as the fix

**`forceGripPose` was serialized `1`, and SPEC §3.5 says it stays `false`.** The legacy
`LateUpdate` grip (`ApplyGripPose` → `JoinLeadHandToShaft`, which rotates the lead elbow to clamp
the lead fist to `LeadHandFromButt = 0.03`) was running on top of the rig. I set it false and re-ran
— and **every grip number came back byte-identical**: `handL` and `handR` to 4 decimal places,
L station 0.1100, R station 0.0912, `onShaft_l` 0.0197, `onShaft_r` 0.0476, `targetTracksHands`
0.0349. Only foot slide R moved (0.0866 → 0.0826, inside the noise band).

So the script grip was **not** what put the lead hand on the shaft — `IK_Lead` was, and the
coincidence that L station 0.1100 equals `GripAnchor_Lead`'s local Y of 0.1100 is the IK reaching
its target, not the script's clamp. The prefab is left with `forceGripPose = false` because §3.5
mandates it, but the flag is not load-bearing. That is a falsification, not a fix, and I would
rather record it than quietly keep it as a claimed improvement.

**EditMode sweep is not green — 3 of 2,765 fail.** Reporting them rather than rounding to "green":

| test | message |
|---|---|
| `Golfin.Content.Tests.RemoteContentSourceTests.CachePath_IsUnderPersistentData_AndPerCatalog` | expected `C:/Users/...` got `C:\Users\...` — a path-separator comparison |
| `Golfin.Content.Tests.RemoteContentSourceTests.WriteCache_OverExistingFile_ReplacesIt` | expected `{"v":2}` got `{"v":1}` |
| `Golfin.Gameplay.Tests.PendulumSchemeDriverTests.MarkerFreezes_AtTheUpswingReversal_NotAtRelease` | expected 0.309 ± 0.02, got 0.0 |

None of them touch the golfer, the rig, or anything this iteration changed — the complete diff
outside `Docs/` is a single prefab under `Assets/Art/3D/Characters/_Test/Resources/GolferTest/`,
which cannot reach `RemoteContentSource` or `PendulumSchemeDriver`. I have **not** re-run them on a
clean tree to confirm that, so I am not asserting provenance, only the diff.

The **§9.6 build-gate suite is 5/5 green** with the define off:
`IncludeTestAssets_ForcesAGolferBuild`, `MoveOut_StashesTheResourcesFolder_AndRestorePutsItBack`,
`RestoreIsIdempotent_AndSafeWhenNothingMoved`, `TheGolferPrefabIsUnderAResourcesFolder`,
`WithoutTheOverride_TheDecisionComesFromTheActiveProfile`.

---

## 7. Files modified or created

| path | 1-line summary |
|---|---|
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | `GolferRig` (both `Rig`s + all constraints) moved under `MixamoChar_TPose`; `ClubSlot`/`PutterSlot` set to the §3.4 pose solved against the live rig; `forceGripPose` → `false` per §3.5 |
| `Docs/Specs/Active/golfer_club_grip/IMPLEMENTER_REPORT.md` | this report |
| `Docs/Specs/Active/golfer_club_grip/STATUS.md` | `SPEC_READY` → `IMPLEMENTER_WORKING` → `READY_FOR_ARCHITECT_REVIEW` |
| `Docs/Specs/Active/golfer_club_grip/HEARTBEAT.log` | iter-4 kickoff baseline + progress entries |
| `Docs/Specs/Active/golfer_club_grip/SPEC.md` | Architect's amendment (not mine — arrived with the decision file) |
| `Docs/Specs/Active/golfer_club_grip/ARCHITECT_DECISION_RIG_DEAD.md` | the decision (not mine) |
| `Docs/Specs/Active/golfer_club_grip/screenshots/iter4_grip_*.png` | canonical address + t0.6 + impact, full res |
| `Docs/Specs/Active/golfer_club_grip/evidence/grip/*` | the same three frames + the hands close-up crop + 800px versions |
| `Docs/Diag/baked-pivot/M0-regression-DriverFromGreen.md` | regenerated as a side effect of the EditMode sweep — not authored |
| `Docs/Diag/baked-pivot/M0-regression-PutterFromGreen.md` | same |
| `ProjectSettings/EditorBuildSettings.asset` | build-profile switch residue (Golfer → GPS); active profile ends on `iOS-Full-GPS` per A6 |
| `Docs/AI_CONTEXT.md` | session status |

## 8. Corrections carried forward

The four findings struck in `HANDOFF_ARCHITECT_RIG_DEAD.md` §4 stay struck. This iteration adds a
fifth of my own, from earlier today: **"the rig was authored by script and needs the Animation
Rigging editor UI to register properly"** — wrong. The Architect's §"Why" is right; there is no
editor-side registration, the layout was simply outside the stream root. Moving three GameObjects
fixed it, no UI involved.
