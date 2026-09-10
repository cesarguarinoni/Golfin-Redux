# Architect decision — the hinge-model grip, staged (2026-09-11, evening)

**Re:** Cesar: *"this experiment is specifically to decide if you have the capability to handle the animation with minimum artist intervention. So I want you to try as hard as possible to fix this."* The close-out is withdrawn; §3.11's club mount stays; the hand rule is replaced by SPEC §3.12.

## What is different this time — mechanism, not another rule
1. **Hinge axes fixed in joint-local space, captured from the rest pose.** Every previous wrap computed axes from the animated pose in world space and let them drift with each joint it rotated. That is the claw. With `localRotation = rest * AngleAxis(spread) * AngleAxis(flex)` about stored local axes, a finger cannot leave its own plane.
2. **One curl factor per finger on anatomical joint ratios**, not independent bisection to caps. One at 90° next to one at 0° is impossible by construction.
3. **The clip's fingers are discarded**, not used as a starting point. The rest pose is the starting point; the clip drives everything from the wrist up.
4. **The shaft is defined in hand space from the grip geometry** (little-finger base → index middle joint, palm-side offset), not fitted from fingers. The anchor is a `HandGrabPose`: wrist relative to the club, computed once, stored as data — the Meta ISDK shape.
5. **The club's free DOF are spent on keeping the clip's wrists**, so the IK bends them by the least amount, and the residual is a reported number with a stop line.
6. **A staged protocol with a fist test first.** Stage 0 has no club, no rig, no play mode: pose both hands into a fist from the rest pose and look. If that is not a fist, the axes are wrong and nothing above it is attempted. Every stage ends with frames and Cesar's verdict.

## Sources
- Meta Interaction SDK hand-grab: poses stored relative to the object, wrist anchor + per-finger locked/constrained/free, `HandGrabStateVisual` wraps fingers to the object — https://developers.meta.com/horizon/documentation/unity/unity-isdk-hand-grab-interaction/
- Unity XR Hands hand-pose model (per-joint, rest-relative) — https://docs.unity3d.com/Packages/com.unity.xr.hands@1.4/manual/gestures/hand-poses.html

## Process
- Code switches to Fable for this spec (Cesar's offer, accepted): the work is 3D reasoning under a strict protocol.
- One stage per kickoff. Code stops at each gate, writes the frames and numbers, and waits. No stage is started while the previous is red. The Architect reviews full-res PNGs; Cesar gives the verdict.
- Iter-9b (`ApplyHeldGripPose`) is deleted, not kept beside the new class.
