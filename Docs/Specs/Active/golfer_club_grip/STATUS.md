IMPLEMENTER_BLOCKED

§3.2/§3.3 CANNOT BE BUILT AS WRITTEN. Architect decision needed — see FINDING_3_2_IMPOSSIBLE.md.

Unity Animation Rigging refuses to bind the MultiParentConstraint:
  "Transform 'GripTarget' is not a child of the Animator hierarchy, and cannot be written to."
From the REAL play-mode run (Editor.log 1058330, right after the launch at 1055584; 4 occurrences
across runs), not an edit-mode artifact.

Structural, not a wiring mistake: a CONSTRAINED object needs a ReadWriteTransformHandle and must be
in the animation stream. The two TwoBoneIK constraints bind fine because they WRITE to skeleton
bones and only READ their targets; the MultiParentConstraint must WRITE to GripTarget, which §3.2
requires to be top-level and outside the skeleton. The two requirements are mutually exclusive.

Consequence: every §3.4 measurement taken so far is meaningless — with layer 1 dead GripTarget never
moves, so the club hangs at the golfer's root. headAtBallM 0.7382 m is exactly the golfer->ball
stance distance; gripWorstL/R 1.0855/1.1078 m. Those numbers say "the club is at the origin", NOT
"the clip's hands are in the wrong place". Do not amend the 35 mm threshold on the strength of them.

What DOES hold: grip.ikNoLegEffect — foot slide L 0.0527 / R 0.0929 vs the §9.8 baseline
0.0528 / 0.0915, inside the +/-0.010 band. The rig does not disturb the legs.

Three options for the Architect are enumerated in FINDING_3_2_IMPOSSIBLE.md (constrain a bone /
script-driven grip target / drop the layer). Not chosen here — picking by inference is how the
previous three runs went wrong.

Also done this session: removed GripTargetDriver.cs and GolferClubGripBuilder.cs (spec deviations),
and REVERTED ProjectSettings.asset, which had GOLFIN_GOLFER_TEST written into the global iPhone
defines — that would ship the experiment in every iPhone build regardless of profile.

Active build profile restored to iOS-Full-GPS (A6).
