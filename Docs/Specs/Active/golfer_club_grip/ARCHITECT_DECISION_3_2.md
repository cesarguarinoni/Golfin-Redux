# Architect decision — §3.2 rig shape (2026-09-09)

**Re:** `FINDING_3_2_IMPOSSIBLE.md` (PC session, 2026-09-09). **Decision: option 1, narrowed — nothing becomes a bone; `ClubRoot` moves under the avatar root.**

## Diagnosis (from the prefab on disk, `PfGolfer_MixamoNative.prefab` as the finding left it)
- The `Animator` is on the prefab root `PfGolfer_MixamoNative`. `ClubRoot → GripTarget` is a child of that root. So `GripTarget` **is** a child of the Animator's GameObject, and the bind still throws.
- Therefore the check is not `IsChildOf(animator.transform)`. Animation Rigging binds read-write handles against **`Animator.avatarRoot`**, which for a Humanoid Animator sitting above a nested model is the model's root — here the FBX instance **`MixamoChar_TPose`** (nested prefab, `m_SourcePrefab d7cc1f41…`). The two `TwoBoneIKConstraint`s write to `mixamorig:*` bones under it and bind; `GripTarget` is a sibling of it and does not.
- So "not under a bone" and "constrained object" were never mutually exclusive. The spec's error was "**top-level**": my placement, not the implementer's wiring. Sorry — that cost three runs.

## Decision
1. Re-parent `ClubRoot` (with `GripTarget` and everything under it) so its parent is the **`MixamoChar_TPose`** transform — a direct child of the FBX instance root, **not** under `mixamorig:Hips` or any bone. Identity local transform.
2. `GripTarget_Constraint` stays exactly as built: `MultiParentConstraint`, constrained = `GripTarget`, sources LeftHand 0.5 / RightHand 0.5, Maintain Offset OFF, all axes. `Rig_Grip → Rig_Hands` order unchanged. `GolferRig` stays where it is.
3. Diagnostic before the run, one line: log `anim.avatarRoot.name` at spawn (harness `spawn.animator` detail is the natural place). Expected `MixamoChar_TPose`. Any other name → stop, report the name, no re-parenting by trial.
4. Then §3.4 measurement and the ONE run as specced. The 0.7382 m / 1.08 m numbers from the dead-layer runs are void, as the finding says; the 35 mm threshold stands.
5. Option 2 (script-driven target) is **rejected**: a `LateUpdate` writer runs after the rig, so the IK anchors would trail by a frame — ~8 cm at impact speed at 60 fps — which the `grip.hand.onShaft_*` samples would then measure as a grip error. Option 3 is rejected because it gives up the thing the spec exists for.

## Also
- Removing `GripTargetDriver.cs` / `GolferClubGripBuilder.cs` and reverting the global iPhone defines in `ProjectSettings.asset`: correct, keep those reverts. The define belongs to the build profile only.
- `grip.ikNoLegEffect` L 0.0527 / R 0.0929 m: accepted as the first valid A3 number.
- Work is on branch `golfer_3d_test` per the finding; finish there and say in the report which branch/commit carries the result so it can be merged.

SPEC.md §1, §3.2, §3.3 and the Status line are amended in place to match. STATUS → `SPEC_READY` (amended).
