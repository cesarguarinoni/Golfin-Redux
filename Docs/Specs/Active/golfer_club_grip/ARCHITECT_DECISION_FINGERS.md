# Architect decision — the fingers are the grip (2026-09-10, withdraws the iter-5 acceptance)

**Re:** iter-5 frames, full resolution. Cesar: a lead-hand finger protrudes into the trail hand at address; at the hands close-up the shaft passes between the index and middle fingers. **I called this grip done off compressed crops. That was wrong and I'm sorry.** `ARCHITECT_REVIEW_ITER5.md` §"Verdict" is withdrawn; the foot-slide baseline run in it still stands and is folded into the kickoff below.

## What the searches say (and what we were doing instead)
The way held two-handed props are done in shipped Unity games is consistent across every source I read: the prop's grip points live **on the prop**; the dominant hand carries the prop or both hands IK to it; and the **fingers come from an authored hand pose applied over the animation on a hands-only layer / avatar mask** — either a posed clone of the hand bones on the prop, or a Humanoid muscle-space clip layered with a mask. Nobody derives the finger shape from the mocap clip and nobody derives the palm point from a formula; the pose is authored, and the prop is placed where the posed fingers leave a tunnel.
- Unity discussions, "Using IK for two-handed items": prop parented to the main hand, off-hand IK'd to a grip empty **on the prop**, fingers posed against a cloned hand parented to the prop with avatar masks disabling hand animation. https://discussions.unity.com/t/using-ik-for-two-handed-items-and-animations/693838
- "Animating Hand Poses in Unity": hand poses as **Humanoid muscle clips** on override tracks with an avatar mask so only the hands are overridden — rig-independent. https://ordinaryanimator.com/blog/animating-hand-poses-in-unity
- Tools that export such clips from sliders: VoxHands (MIT) https://github.com/hiroki-o/VoxHands · HumanoidHandPoseHelper https://github.com/umiyuki/HumanoidHandPoseHelper
- Unity Animation Rigging advanced setups (Ciro Continisio): multi-parent on the sword + IK on hands, layered — the constraint half of what we have. https://github.com/ciro-unity/AnimationRigging-AdvancedSetups

We had the constraint half right (club from both hands, IK on both wrists, orientation baked). We skipped the half that makes it a grip: the pose. §3.7 Rule 2 put the shaft where a formula said a palm is; a loose mocap fist has no tunnel there, so the shaft went between fingers, and the trail hand was stationed by reach balance rather than by the lead hand's width, so a finger poked through.

## Decision
1. **Finger pose is in scope now** (SPEC §3.8). A one-frame Humanoid muscle clip on an Override `Hands` layer with a fingers-only avatar mask. Muscle space, so every roster model inherits it.
2. **The shaft goes where the posed fingers leave a tunnel** — measured from the posed finger bones (§3.8.3), replacing the Rule 2 formula. The anchor's baked rotation gains the small roll that lines the tunnel up with the shaft; the angle is reported, and > 35° is a stop.
3. **Trail-hand station = lead station + lead palm width + 4 mm**, measured, so the hands stack instead of intersecting. The only intended contact is the lead thumb under the trail palm.
4. **Four new assertions** that would have failed iter-5: fingers closed on the grip, shaft inside the finger tunnel, no hand-to-hand overlap, lead thumb down the shaft.
5. **Evidence rule change:** full-res PNG hand crops, two angles, three samples. I review the PNGs. Nothing is called done by me; Cesar calls it.
6. The one by-eye step is the pose sliders — Cesar or Code, at most three adjustments with a screenshot each, then show. That is how it is done in the industry and it is not a failure of rigour.

## Kept from iter-5 / ITER5 review
Rule 1 bake, GripTarget from both hands, IK on both wrists, A0 gate, rig-off foot-slide baseline run, `addressHeadLocal` not authored, scene-cam impact frame. `palmHalfThickness` is no longer used by anything.
