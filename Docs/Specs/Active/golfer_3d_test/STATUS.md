IMPLEMENTER_BLOCKED

Blocked on a defect that invalidates the current line of work: in real gameplay the golfer never
enters Address — he stands upright with the club dangling, club head nowhere near the ball. Reproduce: run GOLFIN > Golfer Test > Verify on Hole 06 and open the newest
Docs/Diagnostics/_capture/golfer_h06_address_*.png (screenshots/ is gitignored).

`shot.addressBeforeSwing` PASSES in golfer_invariants.json while the render shows Idle, so that
assertion is unreliable and so is anything else in the JSON never checked against a render.

Handing to a PC session. Full brief: KICKOFF_PC.md
