# Meshy → clean atlas → welded Mixamo rig (the v2 fix), reproducible steps

Cloud-side (Blender 4.2 as `pip install bpy`, `trimesh`, `scipy`, `Pillow`). Scripts in this folder, in order:

1. `boxuv.py` — welded 60K mesh → face normals smoothed over the adjacency graph (12 iterations) → each face assigned to one of 6 axis
   projections → connected components → tiny charts (< 40 faces) absorbed into the neighbour with most shared edges. Result: 266 charts.
2. `peel.py` — depth-peels every chart (BVH ray cast per face along its projection axis, counting same-chart hits) so no chart overlaps
   itself in projection (the skirt-over-thigh bug), smooths the layer labels, absorbs slivers again. Result: 269 charts.
3. `step_box_bake.py` — builds the Blender mesh from the chart UVs, `pack_islands(ACTIVE_UDIM, margin 0.008)`, Cycles selected-to-active
   bake from the Meshy FBX (cage 1.5 mm, max ray 6 mm, margin 16 px): BaseColor 2K, Normal 2K (tangent), Roughness 1K, Metallic 1K.
4. `weld_rig.py` — imports the Mixamo v1 rig FBX, averages skin weights across coincident vertices (they were already identical),
   `remove_doubles` (78,082 → 30,214 verts), transfers the packed UVs by face-centroid match (0 misses).
5. `fix_export.py` — `recalc_face_normals` (2,086 flipped faces fixed — the vanishing finger patches), smooth shading, baked material,
   FBX export with the armature (65 bones; world rest matrices identical to Mixamo's to 4e-6).

Why not re-rig in Mixamo: the welded OBJ/FBX fails Mixamo's auto-rigger ("Unknown error while generating motion") where the
split-vertex OBJ passed; and the v1 rig is fine — keeping it means `HandHinge_Olivia.asset` stays valid.

Known gap (v2.1): where two surfaces sit within `max_ray_distance` of each other (chin over collar, armpits, cap brim over
forehead) the bake can hit the wrong surface — a green speck / bad normal on the chin was patched by hand. Next run: bake
skin and clothing as separate source objects, or drop `max_ray_distance` to 3 mm for the head.

Unity-side findings (Code, 2026-09-16, `IMPLEMENTER_REPORT.md` § Olivia v2):

- **Export without the `Armature` wrapper node.** `fix_export.py` writes `Olivia_TPose_v2/Armature/mixamorig:Hips/…`; the Mixamo
  clip FBXs are `<root>/mixamorig:Hips/…`. Unity's "Copy From Other Avatar" refuses the v2 avatar for the clips (`Rig Error: Copied
  Avatar Rig Configuration mis-match … Parent for 'mixamorig:Hips' differs … 'ANIM_Golf_Drive' was found instead of 'Armature'`),
  the clips import with zero takes and the controller's states go null. The clips therefore stay on the v1 avatar (rest poses equal
  within 0.003 mm / 0°, retarget measured as the identity); a v3 export should parent the bones directly under the root.
- **The finger tears are the skin weights, not the winding.** Same 38 skinning-inverted finger triangles at the 75/95/50 fist on v1
  and v2 (0 at rest; 37–39 at the stage-2 grip poses): each finger joint's influence spans the whole neighbouring phalanx (index:
  child-bone weight 0.08 → 0.39 → 0.87 over 10 % → 80 % → 120 % of the proximal phalanx) on a tube of ~5 rings, so the palmar rings
  cross at 75–95° and the triangles invert (back-face culled → holes). Fix before `fix_export.py`: tighten the falloff at every
  finger joint (blend over ±3–4 mm) and/or add 2–3 rings per phalanx; the winding fix stands (the back of the fist is now a closed
  shell) but does not touch this.
