READY_FOR_ARCHITECT_REVIEW (iter-4)

Cesar's kickoff asked for READY_FOR_SELF_REVIEW; hard rule 1 routes a report carrying FAIL items
down the architect path, and A3/A5 carry them. Deviation flagged, not laundered.

PART 1 — THE ARCHITECT'S FIX. A0 = 0 on every run since.
ARCHITECT_DECISION_RIG_DEAD was right: GolferRig (both Rigs + all three constraint GameObjects)
moved under MixamoChar_TPose beside ClubRoot; only RigBuilder stays on the prefab root. The
re-parent ran; the prefab-variant fallback was NOT needed. Animation Rigging exceptions 2850 -> 0.

PART 2 — THEN CESAR LOOKED AT THE FRAME AND THE TASK GOT ITS REAL BUG.
"You do see that the club is stabbing the player since it is backwards, right?"
He was right, and my club.headAtBall = 0.0198 m PASS was FALSE. Measured off the driver mesh in
ClubSlot local space:
    Grip     span -0.0411 .. +0.0878  -> butt cap at -0.0411
    ClubHead span +0.9633 .. +1.0815  -> head at +1.0224
so ClubSlot local +Y runs BUTT -> HEAD, exactly as GolferPresenter and SPEC §3.2 always said. But
the ClubEnd marker sat at -0.80 — the OPPOSITE END, 1.82 m from the real head — and the harness
§3.4 solver carried the same inverted assumption ("+Y = toward the butt cap"). The solve dutifully
planted that marker on the ball, rotating the club 180 degrees: head up behind the shoulder, shaft
through the chest, and an assertion reporting 2 cm.

FIXED — every number measured or searched, none tuned:
 1. markers re-derived from the mesh bounds; GripAnchor_Lead/_Trail had also been swapped
 2. solver axis flipped (up = ball - hand); grip.hands.order sign flipped with it; the hard-coded
    0.91 lead-anchor-to-head replaced by a value read from the markers
 3. club scaled 0.86880 to the golfer's reach — he is 1.328 m holding a full-size driver, so lead
    anchor to head was 1.0335 m against a 0.8855 m hand-to-ball and the head buried 0.148 m at any
    rotation. Scale derived from the harness's own §3.4 LENGTH line.
 4. club now hangs from the HAND MIDPOINT (what §1/§3.3 asked for — GripTarget IS the 0.5/0.5 MPC
    midpoint), then slid 0.010 m along the shaft by a BOUNDED search that maximises the worse of
    the two arms' reach slack. New §3.4 REACH marks measure shoulder->target against arm length
    instead of inferring "out of reach" from a shortfall, which I had asserted twice without proof.

RESULT (final run, A0 = 0):
    club.headAtBall      0.7382 -> 0.0085 m   PASS
    grip.hand.onShaft_l  1.0855 -> 0.0000 m   PASS
    grip.hand.onShaft_r  1.1078 -> 0.0000 m   PASS
    grip.hands.order     0.0188 -> 0.0695 m   PASS  (the full anchor spacing; both hands ON their
                                                     anchors, L station -0.0096, R station 0.0599)
    grip.ikNoLegEffect   L 0.0527 / R 0.0933  PASS
    §3.4 REACH           both arms now report WITHIN reach (lead slack 0.0467, trail 0.0006)
    26 PASS / 2 FAIL / 8 SKIP

STILL FAILING:
 - grip.targetTracksHands 0.0175 (want < 0.01). STRUCTURAL, not a rig fault: layer 1 computes
   GripTarget from the PRE-IK hands, layer 2 then moves them, and the assertion compares GripTarget
   against the POST-IK midpoint. It cannot reach < 0.01 by construction. Architect: this assertion
   needs redefining (sample the midpoint before layer 2, or widen the band), not the rig changing.
 - budget.tris 36510. Unchanged, out of scope since §9.1.
 - "one finger crooked" (Cesar): the Mixamo hand mesh rest pose. This rig has no finger bones, so
   no constraint or script can close them. Out of reach of this task by construction.

FALSIFIED, kept because it is worth as much as a fix: forceGripPose was serialized 1 against SPEC
§3.5. Set false and re-ran — every grip number byte-identical. The legacy LateUpdate grip was never
doing the work; IK_Lead was.

NOT A GAME BUG (Cesar asked): the golfer vanishing and the lighting resetting just before the shot
is THIS HARNESS — section 7 calls QualityTierService.SetOverride(Low) then (High) then Auto, and Low
sets animatorCulling = CullCompletely. Nothing in the real shot path does that.

ENVIRONMENT FINDING that cost most of the session: Unity only auto-imports on Editor FOCUS. Driven
over MCP the Editor never gets focus, so .cs edits sat unimported and runs silently executed the
PREVIOUS build while "0 compile errors" and isCompiling=false both read clean. Two solver revisions
were measured as if they were new. Every .cs edit now needs AssetDatabase.Refresh(ForceUpdate) plus
a version probe — GolferTestVerificationRunner.SolverVersion exists for exactly that.

Active profile restored to iOS-Full-GPS. Animation Rigging 1.3.1 (Registry, not preview).

ADDENDUM — targetRotationWeight (Cesar: "seems to be getting worse. Maybe you are moving the hands
the wrong way?"). He was right that it looked worse, and the cause was NOT the direction of travel:
both TwoBoneIKConstraints had targetRotationWeight = 0, so the IK set hand POSITION only and hand
ROTATION still came from the clip — authored for a club that was not there. Pulling two unrotated
hands precisely onto the shaft axis is what made it read as a shaft through a fused hand mass.
Set to 1 on IK_Lead and IK_Trail: the palms now wrap the shaft.

EVERY NUMBER WAS IDENTICAL BEFORE AND AFTER (headAtBall 0.0085, hands.order 0.0695, onShaft
0.0000/0.0000, ikNoLegEffect PASS). NO ASSERTION IN §6 MEASURES HAND ORIENTATION, which is why a
visibly broken grip carried a full set of green grip numbers through every gate. Architect: §6 needs
a hand-orientation row (e.g. angle between the hand's palm normal and the shaft axis), otherwise the
next iteration can regress this without a single number moving.

Second measurement gap, same shape: grip.hand.onShaft measures the hand BONE ORIGIN — the wrist —
against the shaft segment, so 0.0000 m is achieved by putting the shaft THROUGH the wrist. The
assertion should target the palm, i.e. carry a palm-radius offset. Left as-is and reported rather
than changed, because moving it changes what §6 means.

Fingers remain open: this rig has no finger bones, so nothing can close them. Out of reach by
construction, as the eight SKIPs already say.

ADDENDUM 2 — targetRotationWeight = 1 REVERTED. Cesar on the frame: "way worst than second image.
Hands wrongly rotated, going through one another." He is right and my read of that frame was wrong:
forcing targetRotationWeight = 1 makes BOTH hands take the anchors' rotation, and the anchors carry
ClubSlot's frame — a CLUB orientation, not a HAND orientation. Both hands snapped to the same wrong
rotation and interpenetrated. Prefab restored byte-identical to 376e97861.

Making rotation work needs a per-hand orientation authored on GripAnchor_Lead / GripAnchor_Trail
(the two palms oppose each other on the grip). That is an Architect decision, not a sixth inference
from me about Mixamo hand-bone axes. Full writeup with pictures: HANDOFF_ARCHITECT_GRIP.md.

Also fixed in passing: saving this prefab while GOLFIN_GOLFER_TEST is OFF silently strips every
#if-gated GolferPresenter field (anim, sockets, stanceDistance, addressHeadLocal, ...). It happened
during the revert and was restored with git restore --source=376e97861.
