# Architect decision — hand orientation & palm offset (2026-09-09, 3rd)

**Re:** `HANDOFF_ARCHITECT_GRIP.md` (branch `golfer_3d_test`, prefab = `376e97861`). **Verdict: the rig is right, the spec was under-specified. Two rules added as SPEC §3.7; two assertions added; one retired.**

## Answers to the three questions
1. **What orientation does each anchor carry?** The clip's own hand-to-club frame at address, baked once per hand: `R_anchor_local = inverse(anchorParent.rotation) * handBone.rotation`, sampled at the address frame with `Rig_Hands` at weight 0. Then `targetRotationWeight = 1`. At address this changes nothing; through the swing each hand keeps its own address relationship to the club and the two hands keep theirs to each other — which is the "going through one another" fix. I am **not** choosing "palm normal at the shaft": that is a second invented target on top of a mocap pose that already holds a club-shaped nothing. If address looks flat, that is fingers (below), not rotation.
2. **Flat, unwrapped hands acceptable?** Yes for this spec. But the reason given — "this rig has no finger bones" — is wrong: `mixamorig:LeftHandThumb1…Pinky3` are in the avatar (`MixamoChar_TPose.fbx.meta`, human map), and image 05 shows fingers splaying, i.e. the mesh is skinned to them. The harness `grip.*` SKIP is keyed to *Quaternius* bone names. A closed hand is a one-frame hand-pose clip on a hands-only avatar-mask layer — its own item, backlog row corrected.
3. **§6 hand-orientation row:** added — `grip.hand.orient_l/_r` (< 5° post-IK, worst of three samples) and `grip.hands.apart` (palm points ≥ 0.045 m). `grip.targetTracksHands` retired: by construction it compares pre-IK with post-IK.

## The second gap (§3 of the handoff) — decided
`grip.hand.onShaft_*` is redefined to the **palm point**, and the IK no longer aims the wrist at the shaft: each anchor gets a `WristTarget` child at `−palmLocal` (hand frame), with `palmLocal` measured from `hand / middle1 / index1 / pinky1` + `shaftRadius` (existing 0.012) + palm half-thickness off the mesh. Threshold stays 0.035 m; it now means what it says.

## Things from the handoff I am accepting as-is
- Club scale 0.86880: correct for a 1.328 m stand-in; **not** a pipeline rule. Noted for `CHARACTER_3D_REMAKE_OPTIONS.md` §7 (roster models at 1.75/1.65 m carry full-size clubs; scale the character, never the club).
- Marker re-derivation, axis flip, hand-midpoint hang + bounded reach balance, `§3.4 REACH/LENGTH/BALANCE`, `SolverVersion`: keep all.
- Harness tier flip before the shot: fix (restore after the shot block).
- `AssetDatabase.Refresh(ForceUpdate)` + version probe after every `.cs` edit: keep doing it.

## Not accepted
- "targetRotationWeight = 1 fixed the wrap" (addendum 1) — already reverted by Cesar's eye; the correct version is Rule 1 above, which only works *with* the baked anchor rotation. Do not set the weight to 1 without the bake.
