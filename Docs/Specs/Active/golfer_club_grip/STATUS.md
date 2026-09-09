READY_FOR_ARCHITECT_REVIEW (iter-4)

Cesar's kickoff asked for READY_FOR_SELF_REVIEW; hard rule 1 routes a report carrying FAIL items
down the architect path, and A3/A5 carry them. Deviation flagged, not laundered.

A0 = 0. Zero Animation Rigging exceptions over a complete run — was 2850. ARCHITECT_DECISION_RIG_DEAD
was right: moving GolferRig (both Rigs + all three constraints) under MixamoChar_TPose fixed it.
The re-parent ran; the prefab-variant fallback was NOT needed.

The rig has evaluated for the first time in this task:
  club.headAtBall     0.7382 -> 0.0198 m  PASS
  grip.hand.onShaft_l 1.0855 -> 0.0197 m  PASS
  §3.4 desired-vs-actual  130.0170 deg -> 0.0000 deg
  the club is in his hands with the head on the ball (screenshots/iter4_grip_address.png)

STILL FAILING — all three are one geometric fact, not a wiring defect:
  IK_Lead lands its hand exactly on GripAnchor_Lead (0.0000 m). IK_Trail misses GripAnchor_Trail by
  0.0775 m. Both constraints are configured identically (weight 1, targetPositionWeight 1, no hint,
  arm reach 0.4639 vs 0.4564 m) — read off the prefab, not assumed. With ClubEnd on the ball AND
  GripAnchor_Lead in the left palm, GripAnchor_Trail is where the right arm does not put its hand.
  That is SPEC §3.4's written stop condition, so I stopped instead of sliding the anchor until the
  number went green.
    grip.hand.onShaft_r    0.0476 (want < 0.035)
    grip.hands.order       0.0188 (want 0.05-0.12; L station 0.1100 R 0.0912)
    grip.targetTracksHands 0.0349 (want < 0.01) - downstream: layer 1 reads the PRE-IK hand midpoint
  budget.tris 36510 unchanged, out of scope since §9.1.

FALSIFIED, worth as much as the fix: forceGripPose was serialized 1 against SPEC §3.5. Set false and
re-ran — every grip number byte-identical. The legacy LateUpdate grip was not doing the work; IK_Lead
was. Left false because §3.5 mandates it, but it is not load-bearing.

NOT PROVEN: reach exhaustion is a strong inference, not a measurement. One harness line (RightArm
world position and |RightArm -> anchorTrail| beside maxReach 0.4564) settles it next run.

NOT AUTHORED, Architect's call: measured addressHeadLocal = (0.7543, -0.0283, -0.0734) vs the
serialized (0.735, 0, -0.069). Authoring it moves the address placement, which re-opens the §3.4
solve. club.headAtBall already passes without it.

EditMode 2762/2765; the 3 failures are content-cache and pendulum tests, untouched by a diff that is
one prefab. §9.6 build gate 5/5 green with the define off. Active profile restored to iOS-Full-GPS.
Animation Rigging 1.3.1 (Registry, not preview).
