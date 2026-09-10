READY_FOR_SELF_REVIEW (iter-8, §3.10 handedness + wrap sign + landmark fixes)

A0 = 0. 31 PASS / 9 FAIL / 13 SKIP -- AGAINST ITER-7's 33 / 7. The row count went the wrong way and
that is the headline, not the wins.

THE §3.10.1 SIGN DIAGNOSIS IS CONFIRMED BY MEASUREMENT. n_out verification passes on both hands:
lead dot 0.9661, trail dot 0.2722 (a +5 deg flex moves the tip along +n_out), stable across runs.
Three signs that were wrong are now right:
    lead fingertips     [0.0311..0.0497] -> [0.0156..0.0416]  (min EXACTLY at the 0.0156 contact)
    thumb clock         -30.87 deg -> +11.75 deg              (right side of top; want +15..+30)
    buttCap.pastHeel    -0.0124 -> +0.0355                    (right sign; overshoots the band)
    §3.4 REACH slack    lead 0.0318 / trail -0.0159 -> lead 0.0119 / trail 0.0199, BOTH positive
    §3.10.4 lead station converged EXACTLY: delta 0.0000
The lead hand visibly wraps the shaft now (evidence/grip310/address_targetside.png). The trail hand
does not, and the hands are further apart than in iter-7.

WHAT REGRESSED AND WHY, in one sentence: §3.10.4 moved the lead station 48 mm up the grip
(-0.0096 -> +0.0288) and the trail station is defined RELATIVE to the lead hand (§3.9.3), so the
overlap fit died with it -- hands.overlap 0.0064 -> 0.0231, hand.onShaft_r 0.0000 -> 0.0457,
fingers.closed_r [0.0425..0.0498] -> [0.0734..0.0795]. I chased the trail station to compensate and
it did not hold. FIVE authoring passes went into discovering that the two stations cannot be chased
one at a time.

    lead station  |  hands.overlap  |  buttCap.pastHeel
    iter-7 -0.0096|  0.0064 PASS    |  -0.0124 FAIL
    iter-8 +0.0288|  0.0231 FAIL    |  +0.0355 FAIL (right sign)

I did NOT silently revert to iter-7's stations to buy back the row count: §3.10.4 is the spec and
its rule converged exactly. This needs a JOINT SOLVE of the two stations against both constraints at
once, not alternating one-variable corrections. That is a method decision and it is the one thing
between the verified §3.10 fixes and a passing grip.

THE 21-JOINT WRAP LOG (§3.10.2) is the most useful thing in the report; full table there. Read it by
comparing d0 with dMax: on 13 of 21 joints dMax > d0, i.e. flexing to the cap moves the tip FURTHER
from the shaft, so the joint takes the cap and ends worse than it started (R.Index 0.0182 -> 0.0222;
L.Ring 0.0157 -> 0.0323). On 5 joints flexion reaches contact in a few tens of degrees (16.3, 29.2,
87.0, 33.8 -- solved angles, not caps). On 4 the tip was already touching. That is neither a cap
problem nor a sign problem: it says the shaft is INSIDE the arc those fingers sweep, so flexing
carries the tip around it. A placement question, which is what §3.10.2 predicted the row would report.

§3.10.6 IS DOING ITS JOB: the geometric trail-palm test FAILS where the dot product passed. The lead
thumb sits 0.0740 m outside the trail palm plane (want 0..0.0190) while the shaft is 0.0143 m out --
the thumb is further out than the shaft. The old dot read 0.6459 and called that fine.

§3.10.3 lead B is wrap-invariant now; axis.landmarks_l 0.0221 -> 0.0204 and A and B report the SAME
error, so the line is consistently offset rather than sheared by the wrap. axis.landmarks_r 0.0014 PASS.

grip.ikNoLegEffect PASS: L 0.0441 / R 0.0064 against a rig-off baseline of 0.0396 / 0.0057,
re-measured under the fixed step because the prefab's non-finger transforms changed.

NOT AUTHORED, Architect's call: the §3.4 re-solve with the station fixed gives ClubSlot local
(-0.03787, -0.04266, -0.09164) euler (347.542, 237.803, 313.704). club.headAtBall already passes at
0.0085 m and authoring it would move ClubStart and re-open every station again.

Evidence: evidence/grip310/ -- six full-res 1400x1400 scene-cam hand shots + three gameplay frames.
EditMode 2760/2765. Profile restored to iOS-Full-GPS; captureDeltaTime confirmed back to 0.
Blade orientation remains §3.9.7, untouched.
