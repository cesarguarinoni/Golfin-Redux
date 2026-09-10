IMPLEMENTER_WORKING (iter-9b — authored hand pose in progress, Cesar reviewing live)

§3.11's own deliverables are done and green: 32 PASS / 1 FAIL / 25 SKIP / 2 INFO, A0 = 0, the single
FAIL being budget.tris (36510 vs 15000), unchanged and out of scope per §5. Rig_Hands + both
TwoBoneIKConstraints, GripAnchor_Lead/Trail and both WristTargets deleted; RigBuilder.layers =
[Rig_Grip]; ClubRoot/GripTarget/Rig_Grip MultiParent kept as the club mount. club.headAtBall
0.0085 m, club.faceSquare edge-vs-aim 89.9804 deg / azimuth 0.0000, club.faceSquare.putt likewise,
foot slide L 0.0396 / R 0.0057 against a rig-off baseline of L 0.0422 / R 0.0063.

NOT done: the grip itself. §3.11's "hands are the clip's" could not survive contact — three club
placements were rejected on sight, and the measurement says why: the two fists sit 46.4 mm apart
PERPENDICULAR to the shaft and only 37.7 mm along it, side by side across the club rather than
threaded on it, so no placement is both inside them and clear of the fingers. Cesar chose to author
a hand pose ("you can also bend the wrists slightly if needed").

ApplyHeldGripPose (NEW path; ApplyGripPose/WrapJoint/JoinLeadHandToShaft untouched, PfGolfer_Test
still uses them): capped wrist seat + knuckle-row twist, then one curl parameter per finger across
its three joints. The club now runs THROUGH BOTH HANDS with the trail hand curling round the grip.
Remaining defect: the lead fingers splay rather than wrap, worst joint clearance 0.0073 m against a
0.013575 m grip surface, so a few finger bones still touch the club.

Build profile stays on iOS-Full-Golfer (Cesar: "stop restoring profile in this machine").
