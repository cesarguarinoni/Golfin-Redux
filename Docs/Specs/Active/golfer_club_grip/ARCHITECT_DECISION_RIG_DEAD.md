# Architect decision — the rig never evaluated (2026-09-09, 2nd decision of the day)

**Re:** `HANDOFF_ARCHITECT_RIG_DEAD.md` (branch `golfer_3d_test`, `5508062b3`). **Decision: same root cause as this morning, one rule I stated wrongly. Fix = move the Rigs under the FBX instance. Fallback specified below so there is no fourth round trip.**

## What Code is doing wrong
Nothing, this time. The diagnosis in the handoff is right: the job throws every frame, so nothing downstream was ever real, and `club.headAtBall = 0.7382 m` was the stance distance. The thing that is wrong is a sentence in my §3.2 decision: *"`GolferRig` stays at the prefab root — constraint components need not be under `avatarRoot`."* That was an inference, not a fact, and it is false. Sorry.

## Why (from the prefab on disk, `5508062b3`)
- Inside the FBX instance `MixamoChar_TPose` (= `Animator.avatarRoot`): `ClubRoot → GripTarget → …`, the arm bones. Everything here binds.
- On the prefab root, outside the instance: `Rig_Grip`, `Rig_Hands`, `IK_Lead`, `IK_Trail`, `GripTarget_Constraint`. Everything here fails.
- Both exceptions are the same fact seen from two sides. With the Animator one level **above** the model, the animation stream is rooted at the model, not at the Animator's GameObject. `ReadWriteTransformHandle` for `GripTarget` failed at bind (this morning). `PropertyStreamHandle` for the constraints' **weight floats** — bound to the constraint's own transform — fails at resolve (now). Correct transform bindings cannot fix a property handle on a transform the stream cannot see, which is exactly what §2 of the handoff observed.
- Flattening the Rigs to the prefab root (§3 of the handoff) could not change the count: it moved them from outside the stream to outside the stream.
- "Editor path vs scripted path" (§5 of the handoff) is not it. Animation Rigging has no editor-side registration; `RigBuilder.Build()` binds everything at runtime from the components. Building it through the Inspector would have produced the same tree in the same place and the same 2,850.

## Decision
1. Re-parent `GolferRig` (with `Rig_Grip`, `Rig_Hands` and all constraint GameObjects under it) so it is a child of **`MixamoChar_TPose`**, sibling of `ClubRoot`. Identity local transform. Keep `RigBuilder` on the prefab root (it needs the Animator's GameObject); its `layers` references survive the move.
2. Rule, now complete (SPEC §3.2): **every** object Animation Rigging touches through a stream handle — written transforms, read transforms, and the `Rig`/constraint components — lives under `MixamoChar_TPose`. Only `RigBuilder` stays beside the Animator.
3. **Gate A0 before any measurement:** count `InvalidOperationException` from `UnityEngine.Animations.Rigging` for the run. It must be **0**. No grip number is reported from a run with a non-zero count — that is the mistake that produced a week of void numbers, and it is now an acceptance row.
4. Then `ClubSlot` back to the §3.4 solved pose (the 1 mm solve is fine, keep it), the ONE harness run, report.

## Fallback — specified now so it is not a guess later
If step 1 gives a non-zero A0 count, the wrapper-root layout is the problem and we remove it instead of chasing it: rebuild `PfGolfer_MixamoNative` as a **prefab variant of `MixamoChar_TPose.fbx`** so the prefab root *is* the model root (`avatarRoot == transform`, the layout Animation Rigging is written for). The FBX import already puts the Animator on that root; add `GolferPresenter`, `RigBuilder`, the two `UnplayableChecker`s, `ClubRoot`, `GolferRig` under it, same names, same constraints. No code change: the harness spawns by Resources path, `GolferPresenter.Awake` resolves by `GetComponent`/name. Report which layout ran. Do not try anything else.

## Also
- Package version goes in the report (§6 A7). If it is a preview, say so.
- `Library_broken_143700/` — Cesar deletes.
- Keep: `avatarRoot` log, `grip.targetTracksHands`, the §3.4 solver and VERIFY line. Strike the four superseded findings exactly as the handoff §4 does — they stay in history with the correction beside them.
