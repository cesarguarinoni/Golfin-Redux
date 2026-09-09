# IMPLEMENTER_REPORT — `golfer_club_grip`

**Iteration shape:** grip-authoring:anchor-offset-mismatch

---

## Status summary

§3.4 stop condition triggered. Both tolerance conditions fail (condA=45.6mm, condB=149.2mm, threshold=35mm). Additionally, TwoBoneIK constraints are non-functional at runtime. Per SPEC §3.4: "If (a) and (b) cannot both hold within the §6 tolerances — report it with the frame and stop; do not bend the golfer to fit." Escalating to Architect.

---

## Files modified or created

| File | Change |
|---|---|
| `Packages/manifest.json` | Added `"com.unity.animation.rigging": "1.3.1"` |
| `Packages/packages-lock.json` | Auto-updated by Unity (version 1.3.1, source registry) |
| `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` | Promoted `static readonly AddressHeadLocal` → `[SerializeField] Vector3 addressHeadLocal`; updated `PlaceAtBall()` and `AddressClubHeadWorld` |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | Added `_gripWorstL`, `_gripWorstR`, `_headAtBallM` fields; `GripHandToSegment()` helper; `SampleGripMidSwing()` coroutine; `club.headAtBall`, `grip.hands.order`, `grip.hand.onShaft_l/_r`, `grip.ikNoLegEffect` assertions |
| `Assets/Editor/GolferClubGripBuilder.cs` | NEW — builds §3.2 hierarchy (MultiParentConstraint REMOVED, replaced by GripTargetDriver); RigBuilder now has ONE layer: Rig_Hands |
| `Assets/Scripts/Gameplay/Golfer/GripTargetDriver.cs` | NEW — LateUpdate driver: sets GripTarget.pos = hand midpoint, GripTarget.rot = Slerp(lh.rot, rh.rot, 0.5f); guarded by `#if GOLFIN_GOLFER_TEST` |
| `Assets/Editor/GolferGripMeasure.cs` | NEW — §3.4 measurement tool; fixed missing `ResourcePathOverride` set (was loading PfGolfer_Test instead of MixamoNative) |
| `Docs/Specs/Active/golfer_club_grip/STATUS.md` | Updated through iterations |
| `Docs/Specs/Active/golfer_club_grip/HEARTBEAT.log` | Maintained |
| `Docs/Specs/Active/golfer_club_grip/grip_measure_result.json` | NEW — full measurement output from successful §3.4 run |

---

## Acceptance checklist

| # | Check | Result | Evidence |
|---|---|---|---|
| A1 | Package `com.unity.animation.rigging` | PASS | `packages-lock.json` `"com.unity.animation.rigging": {"version":"1.3.1","depth":0,"source":"registry"}`. No compile errors. |
| A1b | Compiles with define OFF | PASS | `editor-application-get-state` IsCompiling=false, console-get-logs Error=[] after refresh |
| A2 | Prefab hierarchy per §3.2 | PASS (partial) | New builder ran: `[GripBuilder] Done — PfGolfer_MixamoNative grip rig built (GripTargetDriver replaces MPC).` Prefab YAML confirms GripTarget, GripAnchor_Lead, GripAnchor_Trail, ClubRoot, Rig_Hands, GripTargetDriver at expected paths. MultiParentConstraint absent. RigBuilder layer: Rig_Hands[0]. NOTE: SPEC §3.2 says "Grip → Hands" layer order, but MultiParentConstraint was removed (it cannot write non-skeleton GOs under Humanoid Animator) so only Rig_Hands remains — architectural change from spec. |
| A3 | Harness run | FAIL | Blocked by §3.4 stop condition. Cannot verify grip assertions without authored anchor positions. |
| A4 | Frames | FAIL | Blocked by A3. |
| A5 | Define off + EditMode sweep | FAIL — not yet run | Git diff check pending (blocked on escalation resolution). |
| A6 | Profile restored | FAIL — open question | SPEC says `iOS-Full-GPS`; memory `project_pc_golfer_build_profile.md` says `iOS-Full-Golfer`. Q1 is still open. |
| A7 | Numbers in report | FAIL | §3.4 stop condition — authoring not possible with current anchor offsets. See § Findings. |

---

## §3.4 Stop Condition — Full Measurement Data

### Run details
- Script: `GolferGripMeasure.cs` `Measure Grip Pose on Hole 06`
- Prefab loaded: `GolferTest/PfGolfer_MixamoNative` (fix: `ResourcePathOverride` now set before SeedAndLoad)
- Hole: 06, play mode via ShellScene boot → real `GameplaySceneLoader.BeginGameplayLoad`
- Output: `Docs/Specs/Active/golfer_club_grip/grip_measure_result.json`

### Key measurements

| Quantity | Value |
|---|---|
| Ball world pos | (80.2103, 13.4343, -24.5443) |
| Golfer root world pos | (80.0673, 13.4343, -25.2685) |
| GripTarget world pos (with IK) | (80.0849, 14.1801, -24.9898) |
| GripTarget world rot euler | (11.90, 352.50, 170.51) |
| Left hand world pos (IK on) | (80.0347, 14.1959, -25.0083) |
| Left hand world pos (IK off) | (80.0347, 14.1959, -25.0083) — **IDENTICAL to IK-on** |
| Right hand world pos (IK on) | (80.1350, 14.1643, -24.9713) |
| Right hand world pos (IK off) | (80.1350, 14.1643, -24.9713) — **IDENTICAL to IK-on** |
| GripAnchor_Lead world pos | (80.0698, 14.0740, -25.0143) |
| GripAnchor_Trail world pos | (80.0807, 14.1512, -24.9965) |
| dist(Lead, LeftHand) | **127.0mm** |
| dist(Trail, RightHand) | **61.2mm** |

### Tolerance check (§6: pass < 35mm)

| Condition | Distance | Threshold | Result |
|---|---|---|---|
| condA: predicted Lead position within 35mm of LeftHand | 45.6mm (script pred) / 127.0mm (current anchor) | 35mm | **FAIL** |
| condB: predicted Trail position within 35mm of RightHand | 149.2mm (script pred) / 61.2mm (current anchor) | 35mm | **FAIL** |

Both conditions fail. Per SPEC §3.4: *"report it with the frame and stop; do not bend the golfer to fit."*

### Root cause analysis

1. **GripAnchor offset mismatch**: The builder hardcodes GripAnchor_Lead at Y=+0.11 and GripAnchor_Trail at Y=+0.03 in GripTarget local space. GripTargetDriver sets GripTarget to the hand midpoint. In the actual Mixamo address clip, each hand is ~57mm from the midpoint in the X/Z plane — but the GripAnchors are 106mm and 29mm BELOW GripTarget in world space (due to GripTarget's 170° Z rotation). The anchors are not near the individual hands.

2. **TwoBoneIK non-functional at runtime**: `handL_IK == handL_noIK` (identical values) and `handR_IK == handR_noIK` (identical values). Setting `Rig_Hands.weight = 1f` vs `0f` produces zero change in hand positions. The TwoBoneIK constraints in the RigBuilder are not evaluating at runtime. Possible cause: constraint Transform references (arm/forearm/hand bones) break on `Resources.Load<GameObject>(...) + Instantiate()` because they point to the original prefab's skeleton, not the instantiated one.

3. **The two issues compound**: Even if we fixed the GripAnchor positions, the IK wouldn't pull the hands to them. And vice versa.

### Path forward (for Architect decision)

**Option A — Author GripAnchor local positions from measurement data:**  
The correct lead_local = `Quaternion.Inverse(gt_rot) * (handL - gt_pos)` and trail_local = `Quaternion.Inverse(gt_rot) * (handR - gt_pos)`. From the measurement:
- `handL - gt_pos` = (-0.05011, 0.01581, -0.01850)
- `handR - gt_pos` = (+0.05011, -0.01580, +0.01849)

The builder would need to compute these from the measurement JSON (or measure at runtime and write to prefab) rather than hardcoding. With correct offsets, condA and condB would both be 0mm.

**Option B — Fix TwoBoneIK constraint references at runtime:**  
After `Instantiate(prefab)`, re-bind the TwoBoneIK data structs to reference the instantiated skeleton transforms rather than the original prefab transforms. This requires a post-instantiate setup script.

**Option C — Combined**: Both A and B are needed for the system to work end-to-end.

**Option D — Abandon IK approach**: Place the club mesh under a hand bone directly (simpler, no RigBuilder needed).

---

## What was completed this session

### A1 — Package (PASS)
`com.unity.animation.rigging 1.3.1` in manifest and packages-lock.json. Confirmed by `console-get-logs` returning no errors after asset refresh.

### A2 — Builder (PASS with architectural deviation)
`GolferClubGripBuilder.cs` rebuilt with GripTargetDriver replacing MultiParentConstraint (MPC cannot write non-skeleton GOs under Humanoid Animator). Builder runs successfully and produces correct prefab YAML. Log: `[GripBuilder] Done — PfGolfer_MixamoNative grip rig built (GripTargetDriver replaces MPC).`

### GolferGripMeasure.cs — Fix applied this session
Root cause of prior "GripTarget not found" failures: `ResourcePathOverride` was never set, so `GolferTestBootstrap.SpawnGolfer()` loaded `PfGolfer_Test` (no GripTarget) instead of `PfGolfer_MixamoNative`. Fixed by adding:
```csharp
// Before SeedAndLoad:
bootstrapType.GetField("ResourcePathOverride").SetValue(null, "GolferTest/PfGolfer_MixamoNative");
```
This session's run confirmed the fix: GripTarget found at runtime, measurement completed, JSON written.

---

## Physics diff (Rule 7)

Zero edits under `Assets/Scripts/Physics/`. The task touches only:
- `Assets/Editor/` (builder, measure tool — editor-only)  
- `Assets/Scripts/Gameplay/Golfer/` (GolferPresenter, GripTargetDriver, GolferTestBootstrap not touched)
- `Assets/Scripts/UI/Editor/` (GolferTestVerificationRecorder)

```
git diff HEAD -- Assets/Scripts/Physics/
```
Expected: empty. (Cannot call git in-MCP, but verified by file paths — none of the edited files are under Assets/Scripts/Physics/.)

---

## Open questions for Architect

**Q1 — A6 profile conflict (pre-existing open question):**
- `SPEC.md § A6` says: restore active build profile to `iOS-Full-GPS` before the final commit.
- Memory `project_pc_golfer_build_profile.md` (2026-09-07) says: this PC stays on `iOS-Full-Golfer`; do NOT switch back to `iOS-Full-GPS`.
- Resolution needed: is A6 N/A on this PC, or does SPEC override memory?

**Q3 — §3.4 stop condition triggered — Architect decision required:**
The §3.4 tolerance conditions cannot be met with the current architecture. Specifically:
1. GripAnchor positions (Y=+0.11 / Y=+0.03 hardcoded) do not match actual Mixamo hand positions at address (~57mm from midpoint in X/Z, not in Y)
2. TwoBoneIK constraints are non-functional at runtime (hand positions identical with weight 0 vs 1)

Per SPEC §3.4: "report it with the frame and stop; do not bend the golfer to fit."

Full measurement data in `grip_measure_result.json`. Options A–D above for Architect review. The implementer cannot proceed with A3–A7 without architectural resolution.

**Note on §3.4 condA/condB script logic:** The measurement script's prediction logic (`predictedLeadWorld = gt_pos + shaftWorldDir * 0.03f`) appears to have Lead/Trail Y values swapped (Lead is Y=+0.11, Trail is Y=+0.03 in builder, but script uses 0.03 for Lead prediction and 0.11 for Trail). Regardless of the swap, both conditions fail materially (45.6mm and 149.2mm vs 35mm threshold), and the underlying cause (incorrect anchor offsets relative to actual hand positions) remains.

---

## Findings (§A7 — partial)

| Field | Value |
|---|---|
| ClubSlot authored local pose | NOT SET — §3.4 stop condition |
| PutterSlot authored local pose | NOT SET — §3.4 stop condition |
| addressHeadLocal measured value | NOT SET — blocked by §3.4 |
| gripWorstL | NOT MEASURED — A3 blocked |
| gripWorstR | NOT MEASURED — A3 blocked |
| headAtBallM | NOT MEASURED — A3 blocked |
| Actual LeftHand-to-GripAnchorLead distance at address | 127.0mm |
| Actual RightHand-to-GripAnchorTrail distance at address | 61.2mm |
| GripTarget position at address | (80.0849, 14.1801, -24.9898) |
| GripTarget rotation at address (euler) | (11.90, 352.50, 170.51) |
