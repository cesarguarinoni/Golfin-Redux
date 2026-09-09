# golfer_club_grip — Implementer report, iter-5 (§3.7 hand orientation + palm offset)

**Iteration shape:** `rigging:hand-orientation`
**Branch:** `golfer_3d_test` · baseline `67ce40c1b` · prefab continued from `376e97861`
**Decision implemented:** `ARCHITECT_DECISION_HAND_ORIENT.md` / SPEC §3.7 + §3.6/§6 rows
**Canonical screenshot:** `screenshots/iter5_address.png`
**Solver version that ran:** `hand-orient-v1` (verified loaded by reflection before the run)

---

## 0. Headline

Both §3.7 rules landed and the two new assertions the Architect added pass at their floor:

| | before (iter-4) | now |
|---|---|---|
| `grip.hand.orient_l` / `_r` | *did not exist* — a visibly wrong grip moved no number | **0.0000° / 0.0000° PASS** (< 5) |
| `grip.hands.apart` | *did not exist* | **0.0695 m PASS** (≥ 0.045) |
| `grip.hand.onShaft_l` / `_r` | 0.0000 m measuring the **wrist** | **0.0000 m / 0.0000 m PASS** measuring the **palm** |
| `grip.hands.order` | 0.0695 m | **0.0968 m PASS** |
| `club.headAtBall` | 0.0085 m | **0.0085 m PASS** |
| A0 rigging exceptions | 0 | **0** |

**30 PASS / 2 FAIL / 9 SKIP.** The orientation numbers hold at **0.0000° at all three samples** —
address, t = 0.6 s and impact — which is Rule 1 doing exactly what the Architect predicted: each hand
keeps its own address relationship to the club through the whole swing, so they cannot rotate into
each other. `grip.hands.apart` is 0.0695 m at all three samples too, i.e. dead constant.

**The two FAILs are `budget.tris` (unchanged, out of scope) and `grip.ikNoLegEffect` — a new
regression, reported in §4 rather than explained away.**

---

## 1. §3.7 Rule 1 — baked anchor rotations (A7)

Measured by the harness at the address frame with `Rig_Hands` weight 0, exactly as specified.
**Identical across three independent bake runs**, so these are the clip's own numbers, not a fit:

| anchor | `R_anchor_local` (quaternion) | euler |
|---|---|---|
| `GripAnchor_Lead` (left) | `(-0.05943, 0.46660, -0.29832, 0.83052)` | `(10.351, 55.477, 325.938)` |
| `GripAnchor_Trail` (right) | `(0.41102, 0.73048, 0.05360, -0.54275)` | `(328.367, 241.590, 39.564)` |

Then `targetRotationWeight = 1` on both `TwoBoneIKConstraint`s — **only** after the bake, per the
Architect's "Not accepted" note. Weight 1 without the bake is image 05.

## 2. §3.7 Rule 2 — palm offsets and WristTargets (A7)

| hand | `palmLocal` | `WristTarget.localPosition` = −`palmLocal` | `palmHalfThickness` | `shaftRadius` |
|---|---|---|---|---|
| lead (L) | `(-0.00016, 0.05926, -0.02200)` | `(0.00016, -0.05926, 0.02200)` | 0.010 † | 0.012 |
| trail (R) | `(0.00012, 0.05556, -0.02200)` | `(-0.00012, -0.05556, 0.02200)` | 0.010 † | 0.012 |

The shape is right by construction: `palmLocal.z = −0.0220 = −(palmHalfThickness + shaftRadius)`,
and the y term is the palm centre along the hand.

**† `palmHalfThickness = 0.010` is the spec's DECLARED FALLBACK, not a measurement — saying so as
§3.7 requires.** Two attempts, both reported rather than buried:

1. **From the finger bones — degenerate, and I caught it only because the number was absurd.**
   `index1` and `pinky1` *define* the palm normal `n` via the cross product, so their component
   along `n` is ~0 by construction. It returned **0.0010 m — a 1 mm palm**. A degenerate measurement
   that looks like a measurement is worse than a declared constant.
2. **From the hand mesh —** blocked, then inconclusive. `MixamoChar_TPose.fbx.meta` has
   `isReadable: 0`, so `sharedMesh.vertices` / `.boneWeights` cannot be read at all. I did **not**
   flip Read/Write on the asset to take one measurement. `SkinnedMeshRenderer.BakeMesh` into a mesh
   we own sidesteps the flag, with vertices selected geometrically (within one hand-length of the
   palm centre) since bone weights need the same unreadable asset — that ran, but did not land a
   value inside the sanity band, so the fallback stands.

0.010 m is sound for a 1.328 m character (an adult palm half-thickness ~0.015 m scales to ~0.011),
and the resulting grip measures 0.0000 m palm-to-shaft. If the Architect wants it measured for real,
the cheap route is `isReadable: 1` on the `_Test` FBX.

## 3. Acceptance (SPEC §6)

| # | Check | Verdict | Evidence |
|---|---|---|---|
| **A0** | Rig alive | **PASS** | **0** `UnityEngine.Animations.Rigging` exceptions across the whole run |
| A1 | Package | **PASS** | `com.unity.animation.rigging` **1.3.1** (Registry, not preview); compiles with the define **on** (run) and **off** (2,765 EditMode tests executed) |
| A2 | Prefab | **PASS** | §3.2 hierarchy + §3.7: both anchors carry the baked rotation, each has a `WristTarget` child, both IKs target the WristTargets with `targetPositionWeight = 1`, `targetRotationWeight = 1`. Read back off the saved prefab |
| A3 | Harness run — ONE, Mixamo-native, Hole 06 | **FAIL** | Every grip row PASSES (§0 table). `grip.ikNoLegEffect` **FAILS** — see §4. `budget.tris` 36,510 still the known out-of-scope FAIL |
| A4 | Frames | **PASS** | `evidence/grip37/` — `01_address`, `02_t0_6`, `03_impact` on the gameplay camera at 1170×2532, plus hand close-ups at **all three** samples in `_compressed/*_hands.jpg` (full-res crops of those same frames — CAPTURE RULE 0: no second capture path) |
| A5 | Define off | **FAIL** (partial) | Shipped diff is **two files, both gated**: the `_Test` prefab and the Editor-only harness. No runtime `.cs`, no `manifest.json`. But the EditMode sweep is **not** green — 2 of 2,765 fail (§5) |
| A6 | Profile | **PASS** | Restored to **`iOS-Full-GPS`** before the close-out commit |
| A7 | Numbers | **PASS** | §1, §2 above; §3.4 poses and club scale unchanged from iter-4 and restated in §6 |

## 4. The one new failure — `grip.ikNoLegEffect`

```
foot slide L=0.0530 m (baseline 0.0528 ±0.0100)   R=0.0710 m (baseline 0.0915 ±0.0100)
```

Left is dead on. **Right is 0.0710 m — 0.0105 m BELOW the band**, so it fails a two-sided check by
sliding *less* than the no-rig baseline. Every iter-4 run sat in band (R = 0.0873 / 0.0889 / 0.0917 /
0.0933).

**I changed two things this iteration and cannot separate them from one run**, which the spec caps
at one:

1. **`targetRotationWeight = 1` + the WristTargets.** The arms are now constrained in rotation as
   well as position, so the arm chain resolves differently through the swing.
2. **The §3.6 tier-restore move.** The Architect asked for `Auto` to be restored *after* the shot
   block; the swing is therefore now measured entirely at the **High** tier instead of across a
   flip. `tier.low` sets `animatorCulling = CullCompletely`, so the culling/quality state inside the
   measured window is genuinely different from every run that produced the 0.0915 baseline —
   **including the §9.8 run the baseline came from.**

Cause 2 makes the stored baseline arguably stale by construction: it was measured under an ordering
that no longer exists. I have **not** re-baselined or widened the band — that is the Architect's
call, and quietly moving a threshold to match a result is the failure mode this task has been
correcting all week. The A/B that settles it is one run with the tier restore put back before the
shot; say the word and it is one run.

## 5. EditMode sweep

**2760 / 2765**, 2 failed, 3 skipped:

| test | message |
|---|---|
| `RemoteContentSourceTests.CachePath_IsUnderPersistentData_AndPerCatalog` | expected `C:/Users/...`, got `C:\Users\...` — a path-separator comparison |
| `PendulumSchemeDriverTests.MarkerFreezes_AtTheUpswingReversal_NotAtRelease` | expected 0.309 ± 0.02, got 0.0 |

Neither touches the golfer, the rig or the harness; the complete diff outside `Docs/` is one gated
prefab and one Editor-only file. `WriteCache_OverExistingFile_ReplacesIt` failed last iteration and
passes now with no related change, so at least one of these is flaky. I have not re-run them on a
clean tree, so I am asserting the diff, not provenance.

## 6. Carried forward unchanged from iter-4

`ClubSlot` / `PutterSlot` local pose `(-0.00338, -0.03012, -0.01120)` / euler
`(28.744, 31.564, 37.031)`; club scale **0.86880** (a stand-in artefact of the 1.328 m character —
the Architect has noted it for `CHARACTER_3D_REMAKE_OPTIONS.md` §7, roster models carry full-size
clubs); markers `ClubStart −0.0357`, `ClubEnd 0.8883`, anchors `−0.0096` / `0.0599`; the §3.4
hand-midpoint hang with the bounded reach-balance search; `addressHeadLocal` still the default
`(0.735, 0, −0.069)`, measured value `(0.7543, −0.0283, −0.0734)` still not authored (it re-opens
the §3.4 solve — Architect's call, unchanged from iter-4).

## 7. Files modified or created

| path | 1-line summary |
|---|---|
| `Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_MixamoNative.prefab` | §3.7: baked rotations on both `GripAnchor_*`, new `WristTarget` children at −`palmLocal`, IKs retargeted with `targetPositionWeight`/`targetRotationWeight` = 1 |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | §3.7 bake mark + palm maths (`TryPalmLocal`, `MeasureHandHalfThickness`, `PresenterShaftRadius`); `onShaft_*` redefined to the palm point; new `grip.hand.orient_l/_r` and `grip.hands.apart`; `grip.targetTracksHands` retired to a Skip; tier restore moved after the shot block; `SolverVersion` → `hand-orient-v1` |
| `Docs/Specs/Active/golfer_club_grip/IMPLEMENTER_REPORT.md` | this report |
| `Docs/Specs/Active/golfer_club_grip/STATUS.md` | `SPEC_READY` → `IMPLEMENTER_WORKING` → `READY_FOR_SELF_REVIEW` |
| `Docs/Specs/Active/golfer_club_grip/HEARTBEAT.log` | iter-5 baseline + progress |
| `Docs/Specs/Active/golfer_club_grip/SPEC.md` | the Architect's §3.7 amendment (not mine) |
| `Docs/Specs/Active/golfer_club_grip/ARCHITECT_DECISION_HAND_ORIENT.md` | the decision (not mine) |
| `Docs/Specs/Active/golfer_club_grip/evidence/grip37/` | three gameplay frames + three hand close-ups |
| `Docs/Specs/Active/golfer_club_grip/screenshots/iter5_*.png` | canonical frames (full res) |
| `Docs/AI_CONTEXT.md` | session status |

## 8. Two traps this iteration hit, both now guarded

- **`script-execute` does not inherit the project's scripting defines.** My first authoring script
  guarded itself with `#if GOLFIN_GOLFER_TEST` — which is *always* false in that context, so it
  refused to run. Harmless here (it failed safe), but a guard that always fires is not a guard. The
  working version reflects for `ShotController.OnShotResolvedImmediate` instead, and additionally
  verifies the gated `addressHeadLocal` field is visible on the loaded prefab **before** saving.
  That guard exists because saving this prefab with the define off silently strips every gated
  `GolferPresenter` field — it happened in iter-4.
- **Unity only auto-imports on Editor focus**, so every `.cs` edit here is followed by
  `AssetDatabase.Refresh(ForceUpdate)` and a `SolverVersion` probe. It caught a stale assembly again
  this iteration: a `Tf` → `Fb` compile error left the previous build loaded, and "0 compile errors"
  read clean because the errors were in the *previous* log window.
