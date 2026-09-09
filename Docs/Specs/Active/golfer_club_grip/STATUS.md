READY_FOR_SELF_REVIEW (iter-6, §3.8 finger pose)

A0 = 0 on every run. 32 PASS / 6 FAIL / 9 SKIP. Solver finger-pose-v1, verified loaded before each run.

BUILT: the §3.8.1 pose clip (one frame, 40 Humanoid finger muscles, isHumanMotion), the §3.8.2 Hands
layer (Override, weight 1, Mask_HandsOnly = LeftFingers + RightFingers only, confirmed live at
runtime as "1:Hands w=1.00"), the §3.8.4 trail station, the four §3.8.5 assertions, the rig-off
foot-slide baseline, and the sanctioned scene-cam so the impact close-up exists at all.

STOPPED at §3.8.3, as the spec and the kickoff both require: roll correction 36.44 deg (lead) /
53.74 deg (trail), over the 35 deg stop. THE COMPOSED ANCHOR ROTATION WAS NOT AUTHORED. The prefab
still carries the iter-5 §3.7 rotations; nothing is built on a number the spec says to stop at.

The stop has a mechanism, not just a threshold breach. The clip works (sampling it bends index1 to
-71 deg) and the muscle range works (+1.00 -> knuckle-tip 0.0860; -1.00 -> 0.0674). But at full curl
index1's local euler is (281.0, 307.9, 53.7): -52 deg of YAW and +54 deg of ROLL alongside the
flexion. The fingers splay sideways as they bend instead of curling. So knuckle-tip only shortens
22% across the whole range, r_curl floors at ~0.030 against an 0.018 ceiling, the fitted tunnel comes
out skewed, and the tips sit 21-38 mm from the shaft instead of 8-24 mm.
Root cause candidate, checkable: every finger bone in the avatar has useDefaultValues=True with
min=max=(0,0,0) -- the finger axes on this Mixamo auto-avatar were never configured.
Two routes, Architect's call: (a) configure the finger muscle axes in the Avatar and re-run, keeping
muscle space so every roster model inherits the clip; (b) the other path the decision file lists --
author the pose as BONE ROTATIONS on a posed hand clone, sidestepping the avatar, at the cost of
being per-rig.

MOVED BY THE POSE (iter-5 -> now): fingers.closed_l [0.0293..0.0456] -> [0.0213..0.0384];
fingers.closed_r [0.0288..0.0454] -> [0.0146..0.0329]; shaft.inTunnel_r now PASSES; thumb.downShaft_l
24.40 deg PASS; hands.noOverlap 0.0094 -> 0.0097 (still 0.3 mm under the floor).
UNCHANGED AND STILL GREEN: onShaft_l/_r 0.0000, orient_l/_r 0.0000 deg, hands.apart 0.0703,
hands.order 0.0976, club.headAtBall 0.0085.

grip.ikNoLegEffect: rig-off baseline measured as §3.6 requires -- baselineSlideL 0.0518,
baselineSlideR 0.0810, both in the JSON header. Against the old §9.8 0.0915 that confirms the
Architect exactly: the harness ordering moved the number, not the rig. BUT three rig-on runs of an
effectively identical prefab gave R = 0.0279 / 0.0843 / 0.0338 -- a 0.056 m spread against a
+/-0.010 m band. The row passed on one and failed on two. That is not a leg effect and not a bad
baseline: the measurement is not repeatable at its own threshold. Band NOT widened.

DEVIATION, flagged: §3.8.1 names VoxHands / HumanoidHandPoseHelper. Both exist only to emit a
Humanoid muscle clip, so the clip was written directly with AnimationClip.SetCurve -- identical
artifact, no third-party package in the repo, smaller diff. Say the word if you want the sliders.

Adjustments used: 2 of 3, both measurement-driven. Evidence: evidence/grip38/ -- six full-res
1400x1400 PNG hand shots (down-the-shaft + target-side at address / t=0.6 / impact) plus three
gameplay frames. EditMode 2760/2765. Active profile restored to iOS-Full-GPS. Animation Rigging 1.3.1.
