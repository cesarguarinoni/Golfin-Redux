# Meshy — Olivia test model run log (2026-09-15)

Architect-driven. Applies `FINDINGS_FOR_NEXT_CHARACTER.md` §2 (hand rig), §3 (mesh and scale) to the Meshy generation.
Account: Cesar's Meshy subscription, workspace on nihon Chrome. Credits 4,100 → 4,000.

## Inputs

- `Assets/Art/3D/Characters/_Test/Olivia/ref/olivia_apose_front.png` (Nano Banana iteration 3: face locked to the roster
  portrait, A-pose ~35°, palms to camera, five separated straight fingers, low ponytail, no props, plain grey background)
- `.../ref/olivia_apose_back.png` (same iteration, back view)

## Settings that worked (run 2)

| Setting | Value |
|---|---|
| Mode | Image to 3D, **Meshy 7 – Flagship**, High Detail |
| Images | Main = front, Multi-view (Beta) ON with Back slot = back view; Left/Right empty |
| Resolution | Ultra 2K |
| Texture | ON |
| **Pose control** | **OFF** (see lesson 1) |
| Image Enhancement | ON |
| Split | OFF |
| License | Private |
| Cost | 35 credits, ~2 min geometry + ~3 min texture |
| Remesh | Fixed, Custom **60,000**, Triangle → 61,468 faces / 78,982 verts (Meshy showed 0 credits, balance dropped 30) |
| Download | Resize ON, **Height 170 cm**, Origin Bottom, Format **fbx** (zip with base/normal/metallic/roughness PNGs) |

Output: `Assets/Art/3D/Characters/_Test/Olivia/meshy_run2_60k/Meshy_AI_Green_Fairway_Mascot_0915034355_texture.fbx`
(+ 5 PNGs). Not committed by Cowork.

## Verification (offline, assimp + trimesh on the exported FBX)

- Bounds: x −57.6…57.5, y 0…**170.0**, z −17.7…19.3 (cm, Y-up, feet on the floor) ✔ §3 real height
- 61,468 triangles, 85,064 verts (UV-split), 0 bones, 3 embedded textures
- Finger separation, both hands: x-slices at 2/4/6 cm from the fingertip give 2/3/**4 separate loops**, merging into one
  palm loop at 8 cm → four fingers + thumb are distinct tubes ✔ §2 "flat and parallel"
- Finger loop perimeters at 6 cm: 5.4–7.5 cm (oblique cut) → finger half-thickness roughly 8–12 mm; Mixamo stand-in was 7.18 mm.
  `ContactM = shaftRadius + fingerHalfThickness` must be re-measured on the rig (HandHingeStage0Tool capture).
- Face matches the roster portrait (violet eyes, brown ponytail, white/green cap); outfit matches the 2D art.
- Ponytail is a solid mesh fused to the head/back (acceptable for the test; hair system for final).
- Skin texture clean (no speckles) on run 2.

## Lessons

1. **Meshy Pose control (A-Pose) re-poses the hands** — run 1 with it ON produced curled mitten hands (fingers fused,
   pointing down) and white speckle artifacts in the skin. With it OFF the mesh follows the reference image's open,
   spread hands. Put the rig-ready pose in the reference image; never ask Meshy to pose it.
2. Multi-view slot order on the page is Left / Back / Right; the `find` labels can mislabel the file inputs — verify the
   thumbnail landed in the Back slot before generating.
3. Remesh at 60K kept all five fingers separate (verified by slicing); do not go lower for a hand-critical model without re-checking.
4. Meshy's viewer ignores extension scroll/drag; camera control works by dispatching `wheel` / `pointer*` events on the
   canvas via JavaScript (right-drag pans, left-drag orbits, wheel zooms to the view centre).
5. Chrome `file_upload` is capped at 10 MB — the 16 MB textured FBX cannot be uploaded to Mixamo through the extension;
   the assimp OBJ export (9.2 MB, UVs kept) or a texture-free FBX under 10 MB is the upload candidate.

## Mixamo (same day)

Rigged as `OLIVIA_MESHY_60K_CLEAN`, Standard Skeleton (65), from `Olivia_meshy_60k_clean.obj`. Four failed attempts
first — all `Unknown error while generating motion`: 60K clean OBJ, 30K decimated OBJ, 30K with the 25-bone skeleton,
and the raw Meshy FBX ("unable to map your existing skeleton"). The fix was the **groin marker on the inner thigh just
below the skirt hem** instead of on the skirt front. Downloads (T-pose + Golf Drive 176 f, Golf Drive Setup 354 f,
Golf Putt 115 f, Idle 250 f — same lengths as Remy's set) are in `_Test/Olivia/MixamoNative/`. Offline check of the
T-pose FBX: 65 `mixamorig` bones with finger tips, 170.0 cm, UVs kept. Full handoff: `OLIVIA_RIG_HANDOFF.md`.

## Next

Code: import, `PfGolfer_Olivia` via `AuthorPrefabStructure`, FINDINGS §8 acceptance in order (see the handoff).
