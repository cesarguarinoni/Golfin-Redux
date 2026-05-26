# `green_authoring_polygon_cell_render_offset`

Tool-UX bug found during `green_authoring_editor_tool` close-out (2026-05-26). Editor-only — does NOT affect runtime gameplay (`green.json` data on disk is internally consistent; runtime reads it directly via `GreenTopologyCache.TrySampleSlope`).

## Symptom

When the Green Topology Editor (`GOLFIN/Green Authoring/Open Editor`) is open on Hole 1, the bright green polygon outline does NOT visually overlap the dark-green rasterized cell region in the way you'd expect. The pin marker (when present, at polygon centroid) ALSO appears co-located with the polygon outline, not with the rasterized region — so the offset is between (polygon outline + pin marker) on one side and (cell grid render) on the other.

Reference frame: [`Docs/Specs/Completed/green_authoring_editor_tool/screenshots/step9_post_reopen_2026-05-26_16-37-10.png`](../Completed/green_authoring_editor_tool/screenshots/step9_post_reopen_2026-05-26_16-37-10.png) — Step 9 of the visual gate, after close+reopen, at zoom 28.6 px/m.

## Confirmed not the cause

- **Data is consistent.** `zones.json` green polygon AABB is (-241.62, -84.78) → (-219.02, -61.77) with centroid (-230.50, -72.48); pin label in screenshot matches that centroid exactly. `green.json` (when saved by the gate) stores bounds = polygon AABB. Grid 46×47 × 0.5m matches 23m × 23.5m polygon AABB.
- **Fill math correctly clips to polygon interior.** [`GreenAuthoringMath.cs:104`](../../../Assets/Scripts/Editor/GreenAuthoring/GreenAuthoringMath.cs#L104) explicitly skips cells outside polygon; interior cells get baseline 1.5% slope (above the 0.01 magPct render threshold), so the bright-green rasterization should match the polygon interior cell-by-cell.
- **Phase 1 read tests + iter-4 T1 round-trip test both pass.** Save/load round-trip is byte-clean.

## Likely cause (hypothesis — verify by experiment, don't pre-bake)

The rendering function [`DrawGreenView`](../../../Assets/Scripts/Editor/GreenAuthoring/GreenTopologyEditor.cs#L859) opens `GUI.BeginClip(_greenViewRect)` at line 862, then:

- **Cells** are drawn with `EditorGUI.DrawRect(cellRect, cellColor)` in **clip-local** coordinates (no `_greenViewRect.x/y` offset added).
- **Polygon outline** at line 950 calls `Handles.BeginGUI()` and adds `_greenViewRect.x` / `_greenViewRect.y` to each point.
- **Pin marker** at line 1009 same pattern (`Handles.BeginGUI` + offset).
- **AABB ruler lines** at line 1033 same pattern.

`GUI.BeginClip` + `Handles.BeginGUI` interact in non-obvious ways. If `Handles.BeginGUI` resets the GUI matrix to absolute (un-clipped) coordinates, then adding `_greenViewRect.x/y` is correct. If it inherits the clip's matrix, the `+_greenViewRect.x/y` is double-offsetting. Visual evidence (pin marker co-located with polygon outline but offset from cells) is consistent with the latter, but a 32-vertex polygon with subtle concavities could also produce a perceived offset that's actually just rasterization quantization.

**First diagnostic step:** open the editor on Hole 1, add temporary debug visualizations — e.g., draw a small yellow dot at each polygon vertex using BOTH paths (cell-style local coords AND Handles-style with `+ _greenViewRect.x/y`) so you can see which one lines up with the actual polygon-vertex world position. Compare to a known cell-centre dot. Whichever overlaps the cell-rendered position is correct; the other is the bug.

## Definition of Done

1. Open the editor on Hole 1 (`GOLFIN/Green Authoring/Open Editor`, hole picker = 1).
2. The bright green polygon outline visibly inscribes (within 1 cell ≈ 0.5m of) the bright-green rasterized cell region. Pin marker (if added at centroid) lands inside the rasterized region, not offset from it.
3. Capture a single frame at the same zoom (~28 px/m) as the reference step9 image and compare side-by-side. Outline + cells + pin should all visually align.
4. No regression in the iter-4 visual gate output (`Assets/Scripts/Editor/GreenAuthoring/GreenAuthoringVisualGate.cs`). Re-run the gate from menu `GOLFIN > Smoke > Green Authoring Visual Gate` after the fix to confirm the new step9 frame shows outline + fill + pin aligned.
5. Test gate ≥ 362 / 0 / 3 (no regressions from current baseline).

## Out of scope

- Runtime green-reading aim assist visualization (`PutterGreenReader` — different code path, not affected).
- Polygon authoring UI itself (the polygon comes from `zones.json` and is read-only in this editor).
- Any refactor of `_greenViewRect` layout.

## Files likely touched

- `Assets/Scripts/Editor/GreenAuthoring/GreenTopologyEditor.cs` (the rendering function `DrawGreenView` and possibly the `DrawArrow`/AABB-ruler paths that share the same coordinate convention)

## Verification artifact

Drop the post-fix frame extract at `Docs/Specs/Quick/_attachments/green_authoring_polygon_cell_render_offset_fixed.png` (create folder if needed) and reference it in the close-out chat message. No video required.
