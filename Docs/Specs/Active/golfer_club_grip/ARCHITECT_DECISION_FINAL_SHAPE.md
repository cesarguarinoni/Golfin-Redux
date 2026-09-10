# Architect decision — final shape: clip hands, club on the two-hand average (2026-09-11)

**Re:** iter-8 (`IMPLEMENTER_REPORT.md`, `evidence/grip310/`), and Cesar: *"Why not simply use the model's initial pose (hands looked alright there) and add a bone to attach the club? Do we need a perfect grip?"*

## Answer
No. At the gameplay camera the hands are ~40 px tall; the iter-4 full frame already read as a golfer holding a club. I spent eight iterations on a close-range grip nobody sees, and the procedural wrap produces claws by construction — one joint bisected to a 90° cap while its neighbours sit at 0° is not a hand shape. Sorry. The question should have been asked on 2026-09-09, and it was mine to ask.

## Decision (SPEC §3.11)
- **Hands and fingers: the clip's, untouched.** No IK, no wrap, no pose layer. `Rig_Hands`, anchors, WristTargets deleted; `forceGripPose = false`.
- **Club: on the two-hand average**, which is what "a bone to attach the club" has to mean here — a single hand bone gives the §9.8 F3 shaft-at-the-camera (one wrist's roll drives it); `GripTarget` = the MultiParent 0.5/0.5 average of both hands is already built, has been alive since the RIG_DEAD fix, and holds the club correctly in every frame since iter-4.
- **One authored offset**, solved once: `ClubSlot` under `GripTarget` — position from the existing §3.4 solve (head on the ball), roll so the clubface is square to the aim at address. That also closes the blade-orientation item in the same pass.
- **Acceptance**: A0, head on the ball, face square, foot slide, the three gameplay frames. Hand-to-shaft distance becomes informational. Every other grip row retired.

## What the eight iterations leave behind that is worth keeping
The stream-root rule (everything Animation Rigging touches lives under `avatarRoot`); the A0 gate; the deterministic harness step; the Mixamo scale rule; the retarget-vs-clips conclusion; `reference/GOLF_GRIP_GEOMETRY.html` for the day a camera shows hands large. The rest is history in this folder.
