# §3.2 cannot be built as written — Animation Rigging will not constrain a non-bone

**From:** PC session (Claude Code), 2026-09-09, working the task directly after three implementer runs
**Branch:** `golfer_3d_test` · nothing merged
**STATUS:** `IMPLEMENTER_BLOCKED` — Architect decision needed on the rig shape

## The finding

§3.2 requires `ClubRoot` / `GripTarget` to be **top-level, outside the skeleton** ("Restructure the
club so it is **not under a bone**"), and §3.3 requires a `MultiParentConstraint` whose
**constrained object is `GripTarget`**.

Those two requirements are mutually exclusive. Unity's Animation Rigging throws:

```
InvalidOperationException: Transform 'GripTarget' is not a child of the Animator hierarchy,
and cannot be written to.
  at ReadWriteTransformHandle.Bind (Animator, Transform)
  at MultiParentConstraintJobBinder`1[T].Create (...)
  at RigLayer.Initialize (Animator)
  at RigBuilder.Build ()
```

**This is from the real play-mode run, not an edit-mode artifact** — `Editor.log` line 1058330,
immediately after the `[RunGrip2]` launch at line 1055584, and again at 1129552; four occurrences
across runs.

### Why the IK layer works and the grip layer cannot

The asymmetry is the whole explanation, and it is structural:

| constraint | writes to | reads | binds? |
|---|---|---|---|
| `TwoBoneIKConstraint` (IK_Lead / IK_Trail) | `mixamorig:*Arm/*ForeArm/*Hand` — **skeleton bones** | its target (read-only handle) | **yes** |
| `MultiParentConstraint` (GripTarget_Constraint) | **`GripTarget`** — a plain GameObject | the two hands | **no** |

A constrained object needs a `ReadWriteTransformHandle`, which requires the transform to be in the
**animation stream**. Source/target objects only need a read-only handle and may sit anywhere — which
is why §3.2's IK half bound correctly with `Root/Mid/Tip/Target` all resolved, while layer 1 died.

`GripTarget` is a GameObject added to the prefab long after the Humanoid avatar was created, so it is
not part of the avatar's skeleton and not in the stream. Parenting it under `mixamorig:Hips` does not
obviously fix that (it would still not be an avatar bone); I did not chase it further, because which
way to go is an Architect decision, not mine to guess.

### What this explains

- **Why the previous implementer wrote `GripTargetDriver.cs`.** It hit this same wall and worked
  around it with a `LateUpdate` script. The *instinct* was right — a script can write to any
  transform. The mistakes were deviating silently from §3.3 and then iterating magic constants
  (`0.07`, `0.04`) until an assertion passed, which §3.4 forbids.
- **Why every §3.4 measurement so far is meaningless.** With layer 1 dead, `GripTarget` never moves,
  so the club hangs at the golfer's root. Measured on the full-rig run: `headAtBallM = 0.7382 m` —
  which is exactly the golfer→ball stance distance, i.e. `ClubEnd` sitting at the root — and
  `gripWorstL/R` of 1.0855 / 1.1078 m. Those are not "the clip's hands are in the wrong place"; they
  are "the club is still at the origin".

## What I built, and what state the prefab is in

I built §3.2/§3.3 exactly as specified, so the failure is the spec's shape and not a wiring mistake:

- `GolferRig/Rig_Grip` — `Rig`, weight 1
- `Rig_Grip/GripTarget_Constraint` — `MultiParentConstraint`, weight 1, constrained = `GripTarget`,
  sources = `mixamorig:LeftHand` @0.5 + `mixamorig:RightHand` @0.5, all position and rotation axes
  constrained, **Maintain Offset OFF** per §3.3
- `RigBuilder.layers` = `Rig_Grip -> Rig_Hands`, in that order

Verified by read-back before running. It is left in the prefab **deliberately**: it is the evidence.
It throws at `RigBuilder.Build()`, which is the finding.

Also removed, as deviations from the spec:
- `Assets/Scripts/Gameplay/Golfer/GripTargetDriver.cs` — the hand-written replacement for §3.3
- `Assets/Editor/GolferClubGripBuilder.cs` — the builder that installs it and sets a single-layer
  `RigBuilder`; running it would undo the spec-faithful rig

And reverted, because it broke the opt-in gate:
- `ProjectSettings.asset` had `GOLFIN_GPS;GOLFIN_GOLFER_TEST` written into the **global iPhone**
  scripting defines. That turns the experiment on for every iPhone build regardless of build profile
  and would ship it — the exact thing §5.6 and `GolferTestBuildGate` exist to prevent.

## The one number that did survive

`grip.ikNoLegEffect` holds: foot slide **L 0.0527 / R 0.0929 m** against the §9.8 baseline of
**0.0528 / 0.0915** — inside the ±0.010 m band. Adding the rig does not disturb the legs, which was
§1's stated worry. That part of the design is sound.

## Architect decision needed

1. **Constrain a bone instead.** Keep `GripTarget` conceptually but make the *constrained object* a
   real skeleton transform, and hang the club off it. Needs a nominated bone and a statement of what
   the club's local offset is relative to it.
2. **Accept a script-driven grip target.** What the agent reached for, done properly: derive position
   and rotation from *both* hands per §1, no tuned constants, in `LateUpdate` after the Animator.
   This contradicts §1's "no script in the hot path" — that line needs rewriting if this is chosen.
3. **Drop the grip-target layer.** Keep only the two-bone IK and parent the club to one hand,
   accepting the wrist roll §9.8 F3 already reported.

I have not chosen. Each changes what §3.2/§3.3/§6 assert, and picking one by inference is how the
last three runs went wrong.

## Repo state

Branch `golfer_3d_test`, **nothing committed** beyond this document and STATUS. Active build profile
restored to **`iOS-Full-GPS`** (A6). Unity idle, not in play mode.
`Docs/Diag/baked-pivot/M0-regression-*.md` are regenerated by the EditMode suite — left uncommitted.
