IMPLEMENTER_BLOCKED

§3.4 single-frame authoring DOES NOT WORK on this rig. Measured, not argued — applying the solved
pose made every number worse, and the frame shows the club lying near his feet:

                     ClubSlot identity      §3.4 pose applied
  gripWorstL              0.3210 m               0.7051 m
  gripWorstR              0.3686 m               0.6976 m
  club.headAtBall         0.3161 m               1.2593 m
  (gate 0.035 m)
  foot slide L/R     0.0527 / 0.0872        0.0517 / 0.0945   (baseline 0.0528 / 0.0915 — in band)

Evidence: evidence/address_pose_applied_WORSE.jpg. The pose has been REVERTED to identity, which is
the better of the two measured states.

WHY IT CANNOT WORK AS SPECCED — the rig has cross-frame feedback, which §1 assumes it does not:
  layer 1 (MultiParentConstraint) sets GripTarget from the CLIP's hands
    -> the club, and therefore the anchors, move with GripTarget
      -> layer 2 (TwoBoneIK) pulls the HANDS onto those anchors
        -> next frame layer 1 reads the hands layer 2 just moved
§1 says "one pass with no feedback", which is true WITHIN a frame but not ACROSS frames. §3.4 asks
for a single fixed local pose authored from one frame's GripTarget; that pose stops being correct as
soon as the loop runs, and the system settles somewhere else. That is what the numbers above show.

The same loop explains the other open anomaly: grip.targetTracksHands measures GripTarget landing
EXACTLY on handR (not the 0.5/0.5 midpoint) even though the prefab YAML has m_Length 2 with both
source weights 0.5 — a fixed point of the loop, not a weighting bug.

ARCHITECT DECISION NEEDED. §3.4 as written (author once, by measurement, at address) is not
achievable while both layers are live. Options, none chosen here:
  a) Author with Rig_Hands weight 0 permanently — breaks the loop, but then the IK never closes the
     residual and §1's second half is abandoned.
  b) Damp or one-way the loop (e.g. layer 1 reads a cached pre-IK hand pose).
  c) Drop layer 1; parent the club to one hand and accept the wrist roll, as golfer_3d_test F3
     already reported.

WHAT IS GOOD AND SHOULD BE KEPT
 - ARCHITECT_DECISION_3_2 verified: ClubRoot under avatarRoot (MixamoChar_TPose); the
   "not a child of the Animator hierarchy" bind exception is gone; RigBuilder.Build() succeeds.
 - Clubs restored to the prefab after they were found missing entirely (Cesar's catch).
 - Anchors + ClubStart/ClubEnd ride with ClubSlot.
 - grip.ikNoLegEffect holds in every run — the rig does not disturb the legs.

Active build profile restored to iOS-Full-GPS. Branch golfer_3d_test.
