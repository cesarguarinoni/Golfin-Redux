READY_FOR_SELF_REVIEW (iter-7, §3.9 landmark placement + contact wrap)

A0 = 0. 33 PASS / 7 FAIL / 13 SKIP. Solver landmarks-v1, probe-verified before every run.

THE HANDS ARE NOW PLACED AND ORIENTED LIKE A GOLF GRIP. THE FINGERS ARE NOT CLOSED ON IT.
Six of the ten §3.9.6 rows pass, including all three that encode the SHAPE of a grip:
    grip.hands.overlap            0.0064 m   PASS (<= 0.008)  -- the trail pinky rides the lead gap
    grip.heelPad.onTop            0.6654     PASS (> 0.5)
    grip.trailPalm.onThumb        0.6553     PASS (> 0.5)     -- was -0.4189, see the deviation below
    grip.axis.landmarks_r         0.0000 m   PASS
    grip.hands.noInterpenetration 0.0112 m   PASS (>= 0.008)
    grip.thumb.downShaft_l        21.59 deg  PASS (< 35); clock -30.87 deg (magnitude in the 15-30
                                             window, sign on the wrong side of top - reported)
    grip.ikNoLegEffect            L 0.0396 / R 0.0057 vs baseline 0.0396 / 0.0057  PASS, exactly
STILL FAILING:
    grip.axis.landmarks_l  0.0221 m         (little MCP exactly on the shaft, index PIP 22 mm off)
    grip.fingers.closed_l  [0.0311..0.0497] (want [0.0057, 0.0187])
    grip.fingers.closed_r  [0.0425..0.0498]
    grip.buttCap.pastHeel  -0.0124 m        (the lead hand is off the END of the grip)
    budget.tris 36510, out of scope since §9.1.

§3.9.5 CLOSES ITER-6's OPEN QUESTION. Time.captureDeltaTime = 1/60 from launch to Finish (reset
there). Three rig-on runs: R = 0.0057 / 0.0054 / 0.0057 -- SPREAD 0.0003 m against the < 0.005
requirement, where iter-6's identical runs spread 0.056 m. The row is a real measurement again.
Rig-off baseline under the same step: baselineSlideL 0.0396, baselineSlideR 0.0057 (JSON header).

WHY THE FINGERS ARE OPEN, measured not guessed: the wrap RUNS (probed in edit mode -- ApplyGripPose
moves the lead index tip 1.4081 -> 1.2683 m) but does not REACH. Tips sit 31-50 mm from the axis
against a 15.5 mm contact target, which is WrapJoint taking its "as closed as it can be" cap at
maxJointBend = 80 deg per joint. Two candidates and I will not guess between them: (1) the lead
landmark line is not parallel to the shaft (A = 0.0000, B = 0.0221), so those fingers reach for a
cylinder that is not where their tunnel is -- but the trail hand's axis is perfect and its fingers
still fail, so that is not the whole story; (2) the 80 deg per-joint cap is simply short for this
hand at this grip radius. ONE measurement settles it: log grip.hand.orient_l/_r against the authored
anchor alongside the per-joint bend WrapJoint actually applies.

DEVIATION, flagged: §3.9.2 says compute with Rig_Hands at weight 0. Right for the landmark axis
(palmLocal/shaftDirLocal are in HAND space, so the hand's world rotation cannot affect them); WRONG
for the two rule targets, because Head and the lead thumb are WORLD positions and with the rig off
they are the clip's -- which the lead hand then rotates 102 deg away from. That is why the trail rule
solved dot = 1.0000 and evaluated at -0.4189. Rule targets now captured rig-ON before zeroing;
trailPalm.onThumb -0.4189 -> +0.6553. The landmark measurement is still taken at weight 0 as specified.

SECOND DEVIATION: the §3.9.3 rig-off station delta cannot converge -- moving an anchor does not move
a hand the rig is not driving, so it returned the same 0.0082 at every station. Solved from the
RIG-ON overlap error by a two-point fit (S 0.0688 -> err 0.0250; S 0.0938 -> err 0.0449) giving
S* = 0.0374, measured result overlapErr 0.0064.

Removed as superseded: the Hands layer, Mask_HandsOnly and ANIM_HandPose_GolfGrip (git rm).
Ported: WrapChain / PalmNormal / AimThumbDownShaft to HumanBodyBones with the Quaternius name path
kept as a fallback so PfGolfer_Test cannot regress; cylinder is the real ClubStart->ClubEnd;
seven fingers; shaftRadius 0.0120 -> 0.0087 and fingerRadius 0.0090 -> 0.0068 (they were the
UNSCALED real-world numbers, a 37% error on a character three-quarters human height).

Two of my own bugs caught by the self-checks: the §3.9.2 roll search rotated the rule vector AND its
target together (roll-invariant, so it returned an arbitrary angle), and sampling ran during Update
while the wrap runs in LateUpdate. Both fixed; the second turned out not to change the tip numbers,
which is itself useful -- it ruled out a measurement artefact.

Evidence: evidence/grip39/ -- six full-res 1400x1400 scene-cam hand shots (down-shaft + target-side
at address / t=0.6 / impact) plus three gameplay frames. EditMode 2760/2765. Profile restored to
iOS-Full-GPS; Time.captureDeltaTime confirmed back to 0. Blade orientation remains §3.9.7, untouched.
