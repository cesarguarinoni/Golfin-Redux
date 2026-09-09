# golfer_club_grip — handoff to Architect: the rig never evaluated

**From:** PC session (Claude Code), 2026-09-09
**Branch:** `golfer_3d_test` · **commit** `5508062b3` · nothing merged to `main`
**STATUS:** `IMPLEMENTER_BLOCKED`
**Headline:** every grip measurement this task has produced — mine and three implementer runs' —
describes a rig that was never running. The task's entire number history is void.

---

## 1. The fault

```
System.InvalidOperationException: The PropertyStreamHandle cannot be resolved.
  at UnityEngine.Animations.Rigging.FloatProperty.Get
  at UnityEngine.Animations.Rigging.TwoBoneIKConstraintJob.ProcessAnimation
```

**2,850 occurrences in a single run**, starting the frame the golfer spawns. When an Animation
Rigging job throws, the graph stops evaluating. So `GripTarget` is never written, keeps its
inherited parent transform — the golfer's **root**, at ground level — and the club, which hangs
0.80 m below `GripTarget`, renders **underground**. Cesar spotted that on screen; it is what led
here.

It also explains a number I had been reporting as meaningful for days:
`club.headAtBall = 0.7382 m` is **exactly the golfer→ball stance distance**. It was never a club
measurement — it was the distance to a club parked at his feet.

## 2. What is verified, not inferred

Read back off the prefab:

| | |
|---|---|
| `IK_Lead` | root `mixamorig:LeftArm`, mid `LeftForeArm`, tip `LeftHand`, target `GripAnchor_Lead` |
| `IK_Trail` | root `mixamorig:RightArm`, mid `RightForeArm`, tip `RightHand`, target `GripAnchor_Trail` |
| all five transforms | under `Animator.avatarRoot` (`MixamoChar_TPose`) ✓ |
| weights | both constraints 1, both `Rig`s 1, all enabled and active |
| layers | `RigBuilder`: `Rig_Grip → Rig_Hands` ✓ |

**The bindings are correct.** The exception is `FloatProperty.Get` — an animatable **weight**
property, not a transform — which is precisely why correct transform bindings don't fix it.

Also verified: **the §3.4 solve arithmetic was right all along.** Desired `ClubEnd`
`(80.2105, 13.4334, −24.5437)` against the ball at `(80.2103, 13.4343, −24.5443)` — a **1 mm** fit.
It simply never had a working rig to land on.

And `ARCHITECT_DECISION_3_2` was correct and its fix holds: moving `ClubRoot` under `avatarRoot`
removed the `"not a child of the Animator hierarchy"` bind exception at `RigBuilder.Build()`. That
was a real bug, really fixed.

## 3. Tried, did not fix

Flattening both `Rig` components to direct children of the `RigBuilder` GameObject and dropping the
`GolferRig` wrapper (Unity's conventional shape; the spec's §3.2 tree nests them one level deeper).
Layer order preserved. **Exception count unchanged at 2,850/run.** The prefab is left flattened —
it matches convention and is not harmful — but it is an **unverified** change.

## 4. Corrections — findings I previously recorded that are now superseded

I want these struck explicitly, because they are in the commit history and would mislead:

- **"§3.4 single-frame authoring cannot work because of cross-frame feedback"** (`c10b175d4`).
  Overreached. There is no evidence of a feedback loop. There is a rig that never ran.
- **"`GripTarget` lands on `handR` instead of the 0.5/0.5 midpoint despite correct weights."**
  Not a weighting bug — the constraint was not executing.
- **"`grip.hand.onShaft_*` / `club.headAtBall` show the clip's hands are impossible."** They show
  nothing about hands. They measure markers on a club at the golfer's root.
- Earlier still, **"the rig is fixed, only §3.4 authoring remains"** — wrong; the clubs were missing
  from the prefab entirely at that point (Cesar's catch), and the rig was dead besides.

## 5. What I think this needs

The rig was authored **entirely by script** (`AddComponent` + `SaveAsPrefabAsset`), never once
through Animation Rigging's own editor tooling. Unresolvable *weight* handles point at registration
the editor path performs and the scripted path does not.

Cheapest next step: open `PfGolfer_MixamoNative` in the Inspector and build/repair the rig through
the **Animation Rigging UI** once (Rig Setup), then re-run the harness. That is a minute of UI work
against a fourth round of inference from me about package internals — and I have already been wrong
three times on this exact class of question (`MPC can't bind non-bones`, `cross-frame feedback`,
`rigs nested too deep`).

If that does not resolve it, the honest fallback is `golfer_3d_test` F3's option 3: drop layer 1,
parent the club to one hand, accept the wrist roll, and take the grip work off this spec.

## 6. State of the repo

- Branch `golfer_3d_test`, commit `5508062b3`, **not merged**. `main` is untouched by any of this.
- `ClubSlot` at identity (the §3.4 pose is reverted — applied, it made every measure worse **because
  the rig was dead**, not because the pose was wrong).
- Active build profile restored to **`iOS-Full-GPS`**.
- `Library_broken_143700/` still on disk from the crash recovery — regenerable, safe to delete.
- Harness gained this session: `avatarRoot` logged at spawn, `grip.targetTracksHands`,
  the §3.4 solver and the §3.4 VERIFY line (desired-vs-actual). All of that is worth keeping —
  the VERIFY line is what finally made the divergence visible.

## 7. Things that are genuinely good and should not be lost

- `golfer_3d_test` §9.8's conclusion stands and is unaffected: retargeting was the cause of the
  sliding legs (foot slide 0.4770 → 0.0915 m), roster pipeline = rig in Mixamo, clips on the model.
- The Mixamo import scale rule: `useFileScale` ON, `globalScale = 1.328/3.089 = 0.42992`.
- `grip.ikNoLegEffect` held in **every** run — foot slide stayed in the ±0.010 band of the §9.8
  baseline throughout. Whatever the rig was or wasn't doing, it never disturbed the legs.
