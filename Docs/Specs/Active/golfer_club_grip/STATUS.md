IMPLEMENTER_BLOCKED

THE RIG HAS NEVER EVALUATED. Every grip number produced by this task, mine and the agents',
describes a dead rig. Found only because Cesar said "club is underground" and I finally traced the
exception instead of the geometry.

  System.InvalidOperationException: The PropertyStreamHandle cannot be resolved.
    at UnityEngine.Animations.Rigging.FloatProperty.Get
    at UnityEngine.Animations.Rigging.TwoBoneIKConstraintJob.ProcessAnimation

2850 occurrences in ONE run, starting the frame the golfer spawns. When a rig job throws the graph
stops evaluating, so GripTarget is never written and keeps its inherited parent transform — the
golfer's ROOT, on the ground. The club hangs 0.80 m below that, which is why it renders underground
and why club.headAtBall reads 0.7382 m: that is exactly the golfer->ball stance distance, not a
club measurement.

WHAT IS VERIFIED, NOT GUESSED
 - The IK bindings are CORRECT. IK_Lead root/mid/tip = mixamorig:LeftArm/LeftForeArm/LeftHand,
   target GripAnchor_Lead; IK_Trail the same on the right. All five transforms are under
   Animator.avatarRoot (MixamoChar_TPose). Both constraints weight 1, enabled, active. Both Rigs
   weight 1. RigBuilder layers Rig_Grip -> Rig_Hands. Read back off the prefab.
 - The exception is FloatProperty.Get — an animatable WEIGHT property, not a transform. That is why
   correct transform bindings do not fix it.
 - The §3.4 solve arithmetic is CORRECT: desired ClubEnd (80.2105, 13.4334, -24.5437) against ball
   (80.2103, 13.4343, -24.5443) — a 1 mm fit. It was never given a working rig to land on.
 - ARCHITECT_DECISION_3_2 was right and its fix holds: ClubRoot under avatarRoot removed the
   "not a child of the Animator hierarchy" bind exception at RigBuilder.Build().

TRIED AND DID NOT FIX IT
 - Flattening the Rigs to direct children of the RigBuilder GameObject (removing the GolferRig
   wrapper). Layer order preserved. Exception count unchanged at 2850/run. The prefab is LEFT
   flattened — it matches Unity's convention and is not harmful — but it is an unverified change.

CORRECTIONS TO THINGS I PREVIOUSLY WROTE DOWN AS FINDINGS — all superseded by the above:
 - "§3.4 single-frame authoring cannot work because of cross-frame feedback" (commit c10b175d4).
   Overreached. There is no evidence of a feedback loop; there is a rig that never ran.
 - "GripTarget lands on handR instead of the 0.5/0.5 midpoint, despite correct weights."
   Not a weighting bug — the constraint was not executing.
 - "club.headAtBall / grip.hand.onShaft_* show the clip's hands are impossible." They show nothing
   about hands; they measure markers on a club parked at the golfer's root.

WHAT THIS NEEDS — not mine to guess at, three structural guesses is enough
The rig was authored ENTIRELY by script (SaveAsPrefabAsset), never once through Animation Rigging's
own editor tooling. The unresolvable weight handles point at registration that the editor path does
and the scripted path does not. The cheapest next step is for someone to open
PfGolfer_MixamoNative in the Inspector and build/repair the rig through the Animation Rigging UI
once, then re-run — rather than a fourth inference from me about package internals.

Active build profile restored to iOS-Full-GPS. Branch golfer_3d_test. ClubSlot at identity.
