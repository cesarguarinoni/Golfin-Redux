# Architect decision — place the shaft by golf-grip landmarks; close the fingers by contact (2026-09-11)

**Re:** iter-6 (`IMPLEMENTER_REPORT.md`, `evidence/grip38/`). Code did what §3.8 said and stopped where §3.8 said to stop. The frames show the butt cap between the two wrists and the fingers curling beside the shaft. **§3.8 was wrong in three places, all mine.** SPEC §3.9 replaces them; `reference/GOLF_GRIP_GEOMETRY.html` is the source.

## What was wrong
1. **§3.8.3 fitted a bat axis.** A line through the middle of a closed fist runs across the palm, perpendicular to the fingers. A golf club runs *diagonally through the fingers* — base of the little finger to the **middle joint** of the index finger, heel pad on top. That is ~25–30° of rotation in the hand plane plus a shift toward the fingertips. It is exactly why the shaft sat between the index and middle fingers in iter-5 and exits at the heel in iter-6. The 36°/54° "roll correction" iter-6 stopped on was this error being measured.
2. **§3.8.4 separated the hands.** A golf grip overlaps: the trail little finger rides on the gap between the lead index and middle fingers, the trail lifeline covers the lead thumb. "Lead palm width + 4 mm" is a two-fists-on-a-pole grip.
3. **Muscle-space pose can't close on this avatar** (`r_curl` floor 0.030, knuckle→tip only −22 % across the whole range). Code's diagnosis (unconfigured finger axes on the Mixamo auto-avatar) is plausible; I am not sending anyone to configure 30 finger axes by hand. The contact wrap that already exists in `GolferPresenter` closes fingers on the real cylinder by construction, and resolved through `HumanBodyBones` it is as rig-independent as a muscle clip.

## Decisions
- **Shaft axis per hand = two landmarks** (`LittleProximal → IndexIntermediate` lead; `LittleProximal → IndexProximal` trail), offset palm-side by finger half-thickness + grip radius. No fit.
- **Anchor rotation is fully determined**: align that line to the club's +Y, then fix the roll by a palm rule — lead: back of the hand toward the head (the 2½-knuckles check); trail: palm toward the lead thumb (lifeline over thumb). Rule 1's clip bake and the 35° stop are retired.
- **Hands overlap**: trail little-finger MCP at the lead index/middle MCP gap station.
- **Fingers by contact wrap** in `LateUpdate` through `HumanBodyBones`, seven fingers (trail little finger excluded), thumbs by `AimThumbDownShaft` (lead at 1 o'clock). `Hands` layer, mask and clip removed. `forceGripPose = true` on this prefab.
- **Harness fixed step** (`Time.captureDeltaTime = 1/60`) for every measured run; three rig-on foot-slide values reported, spread < 0.005 m or the row is informational. Iter-6 §4 was right to refuse the band.
- **VoxHands deviation**: accepted; moot now.
- **Blade orientation** (Cesar, iter-6): real, separate, next — §3.9.7.

## Accepted from iter-6
Rig-off baselines 0.0518 / 0.0810 (to be re-measured under the fixed step); the scene-cam captures; every §3.7 row; A0 = 0.
