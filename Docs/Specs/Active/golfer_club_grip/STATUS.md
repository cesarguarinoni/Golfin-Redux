READY_FOR_SELF_REVIEW (iter-5, §3.7 hand orientation + palm offset)

A0 = 0. 30 PASS / 2 FAIL / 9 SKIP. Solver hand-orient-v1, verified loaded before the run.

BOTH §3.7 RULES LANDED, and the two assertions the Architect added pass at their floor:
    grip.hand.orient_l / _r   0.0000 deg / 0.0000 deg   PASS (< 5)
    grip.hands.apart          0.0695 m                  PASS (>= 0.045)
    grip.hand.onShaft_l / _r  0.0000 m / 0.0000 m       PASS (< 0.035) -- now the PALM point
    grip.hands.order          0.0968 m                  PASS (0.05-0.12)
    club.headAtBall           0.0085 m                  PASS (< 0.05)
Orientation and separation hold at those values at ALL THREE samples (address / t=0.6 / impact),
which is Rule 1 behaving exactly as predicted: each hand keeps its own address relationship to the
club, so they cannot rotate into each other mid-swing. That was the "going through one another".

Rule 1 baked (identical across 3 independent bake runs):
    GripAnchor_Lead  R_anchor_local = (-0.05943, 0.46660, -0.29832, 0.83052)  euler (10.351, 55.477, 325.938)
    GripAnchor_Trail R_anchor_local = ( 0.41102, 0.73048,  0.05360, -0.54275) euler (328.367, 241.590, 39.564)
Rule 2:
    palmLocal L (-0.00016, 0.05926, -0.02200)  ->  WristTarget.localPosition ( 0.00016, -0.05926, 0.02200)
    palmLocal R ( 0.00012, 0.05556, -0.02200)  ->  WristTarget.localPosition (-0.00012, -0.05556, 0.02200)
    palmHalfThickness = 0.010 -- the SPEC'S DECLARED FALLBACK, not a measurement, and saying so:
      (a) the bone-derived version was degenerate (index1/pinky1 DEFINE the normal, so their
          component along it is ~0) and returned 0.0010 m -- a 1 mm palm;
      (b) the mesh route is blocked by isReadable: 0 on MixamoChar_TPose.fbx, and the BakeMesh
          workaround did not land inside the sanity band. I did not flip Read/Write on the asset.
    shaftRadius 0.012 read from GolferPresenter, not re-declared.

ONE NEW FAILURE, reported not explained away:
  grip.ikNoLegEffect  L 0.0530 (baseline 0.0528 +/-0.010) OK   R 0.0710 (baseline 0.0915) FAIL by 0.0105
  The right foot slides LESS than the no-rig baseline, failing a two-sided band. Two changes this
  iteration could cause it and ONE run cannot separate them:
    1. targetRotationWeight = 1 + the WristTargets -- the arm chain now resolves differently;
    2. the §3.6 tier-restore move (Architect-requested) -- the swing is now measured entirely at the
       High tier instead of across a flip to Auto, and tier.low sets animatorCulling=CullCompletely,
       so the measured window differs from the §9.8 run the 0.0915 baseline came from.
  Cause 2 makes the stored baseline arguably stale by construction. I have NOT re-baselined or
  widened the band -- quietly moving a threshold to match a result is the exact failure mode this
  task has spent the week correcting. The A/B is one run with the tier restore put back.

budget.tris 36510 unchanged, out of scope since §9.1. Fingers still open -- backlog, and the
Architect is right that the bones exist (the harness SKIP is keyed to Quaternius names).

EditMode 2760/2765; the 2 failures are a path-separator test and a pendulum test, untouched by a
diff that is one gated prefab + one Editor-only file. Active profile restored to iOS-Full-GPS.
Animation Rigging 1.3.1. Frames: evidence/grip37/ (three gameplay + three hand close-ups).
