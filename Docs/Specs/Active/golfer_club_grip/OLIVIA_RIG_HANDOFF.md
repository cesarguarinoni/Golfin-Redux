# Olivia rig handoff — Architect → Code (2026-09-15)

The first roster-likeness test model is generated, auto-rigged in Mixamo and in the repo. This file tells Code what is
there, what was verified offline, and what to run. Generation details: `MESHY_OLIVIA_RUN_LOG.md`. Requirements it was
built against: `FINDINGS_FOR_NEXT_CHARACTER.md` §2, §3, §7, §8.

## 1. Files (all untracked — Code commits)

`Assets/Art/3D/Characters/_Test/Olivia/`

| Path | What | Verified (assimp/trimesh, cloud) |
|---|---|---|
| `MixamoNative/Olivia_TPose.fbx` | Mixamo auto-rig, **Standard Skeleton (65)**, T-pose, with skin. 3.4 MB | 65 `mixamorig:*` bones incl. `*Hand{Thumb,Index,Middle,Ring,Pinky}1–4` (the `4` = tip child FINDINGS §2 needs); 61,056 tris / 85,032 verts; bounds x −57.6…57.5, **y 0…170.0 cm**, z −17.7…19.3 (Y-up, feet at 0); UV set 0 present |
| `MixamoNative/ANIM_Golf_Drive.fbx` | "Golf Drive — Golf Swing Drive Shot", 30 fps, no keyframe reduction, Without Skin | 176 frames — same length as `_Test/MixamoNative/ANIM_Golf_Drive.fbx` (Remy) |
| `MixamoNative/ANIM_Golf_DriveSetup.fbx` | "Golf Drive Setup — Drive Pre-Shot Setup" (the 355-frame one of the two) | 354 frames — matches Remy's |
| `MixamoNative/ANIM_Golf_Putt.fbx` | "Golf Putt — Medium Putting Shot" (116-frame variant) | 115 frames — matches Remy's |
| `MixamoNative/ANIM_Idle.fbx` | "Idle — Standing Idle" (251-frame variant) | 250 frames — matches Remy's length; file is 27 % larger than Remy's `ANIM_Idle.fbx`, so it may be the other Standing-Idle duplicate; harmless for stages 0–2 |
| `meshy_run2_60k/Meshy_AI_…_texture.fbx` + `_texture.png`, `_normal.png`, `_metallic.png`, `_roughness.png`, `_metallic_roughness.png` | Meshy source (unrigged, embedded textures) + the texture set | the material for the rigged FBX comes from these PNGs — the T-pose FBX went through OBJ so it carries no material |
| `meshy_run2_60k/Olivia_meshy_60k_clean.obj` | the exact mesh Mixamo rigged (debris shells < 50 faces removed: 412 faces) | 4 finger loops per hand at 6 cm from the tips |
| `ref/olivia_apose_*.png` | Nano Banana references | — |

Not committed by Cowork. `Olivia_30k_rigtest.obj/.fbx` in `meshy_run2_60k/` are failed-experiment leftovers — delete them.

## 2. What Mixamo needed (so the next character does not burn an hour)

- **Upload the OBJ, not the Meshy FBX.** The Meshy FBX carries a node hierarchy Mixamo reads as an existing skeleton
  ("Sorry, unable to map your existing skeleton").
- **Groin marker on the inner thigh just below the skirt hem, not on the skirt.** With the marker on the skirt surface
  every rig attempt (60K, 30K, 25-bone) died with `ERROR occured on rig: Unknown error while generating motion`
  (`/api/v1/characters/<uuid>/monitor`). Chin at the chin/neck join, wrists on the wrist crease, elbows mid-joint,
  knees on the kneecap, Use Symmetry on, Standard Skeleton (65).
- Character download: FBX Binary, T-pose (skin always included). Clips: FBX Binary, **Without Skin**, 30 fps,
  Keyframe Reduction none, Overdrive / Arm-Space 50 (defaults).
- Chrome `file_upload` caps at 10 MB; the native Open dialog can be driven with the full path typed into
  "File name" when the file only exists on nihon.

## 3. What Code does

1. Import `Olivia_TPose.fbx` + the four clips exactly like `_Test/MixamoNative/MixamoChar_TPose.fbx` and its clips
   (`golfer_3d_test` SPEC §5.1 root settings; `CHARACTER_3D_REMAKE_OPTIONS.md` §7 / SPEC §9.8 scale rule:
   `useFileScale` ON, no compensating scale — the file is real-size, `lossyScale = 1` on the prefab root is the
   FINDINGS §3 requirement). Humanoid avatar on the T-pose; clips Humanoid, Avatar → Copy From Other Avatar = Olivia's.
2. Material: URP Lit (Simple Lit on Low) from `meshy_run2_60k/*_texture.png` (base), `_normal.png`,
   `_metallic_roughness.png`. One material, one mesh.
3. Prefab `PfGolfer_Olivia` via `AuthorPrefabStructure` (FINDINGS §7 names: `GripTarget`, `ClubSlot` + `ClubStart/
   ClubEnd/ClubHead`, `GripAnchor_Lead/Trail`, `WristTarget`, `GolferRig` + `RigBuilder`, `HandHingeModel` +
   `HandHinge_Olivia.asset`). Nothing hand-authored that `PfGolfer_MixamoNative` gets from the tool.
4. **FINDINGS §8 acceptance, in order, stop at the first red:**
   1. `HandHingeStage0Tool` capture + the 13 `HandHingeModelTests`. Record the measured finger half-thickness and
      `L_prox` (knuckle row) — the OBJ slices suggest thicker fingers than Remy's 7.18 mm; `ContactM` must come from
      the capture, never from Remy's number.
   2. `HandHingeStage1Tool` inscribed wrap: every wrapped tip within 5 mm of the contact circle, bones outside the shaft.
   3. `HandHingeStage2.RunVerify` at address on Hole 06: `stance.*` PASS on the raw clip, `grip.wrist.angle_l` 20–30°,
      both hands on the shaft ≤ 3 mm, IK residual ≤ 10°, `grip.hands.aboveKnees` ≥ 100 mm, `club.faceSquare` ±5°,
      `club.headAtBall` ≤ 50 mm — with the 2026-09-15 stance rig at zero first; only then a stance scan.
   4. Full-res `verify_stance_targetside.png` / `verify_awayside.png` + the stage-0 fist and stage-1 wrap contact
      sheets into `evidence/olivia/`. Not compressed crops.
5. Report in `IMPLEMENTER_REPORT.md` (new section "Olivia") + `STATUS.md`; commit the Olivia files with the report.

Out of scope: hair system, cloth on skirt/ponytail, the game prefab/roster wiring, any texture cleanup, LOD.
