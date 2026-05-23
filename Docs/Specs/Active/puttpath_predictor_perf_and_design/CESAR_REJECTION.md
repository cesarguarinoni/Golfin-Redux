# CESAR_REJECTION — `puttpath_predictor_perf_and_design`

**Date:** 2026-05-22 ~18:00 CEST
**Rejected verdict from:** `ARCHITECT_REVIEW_PASS` (commit `a2fd9850`)
**Re-route to:** Implementer iter2 with revised SPEC

## Why rejected

**Visual paradigm mismatch.** The shipped implementation is arrows-on-cells per the literal SPEC text. The intended visual (L1 lock from 2026-05-13: "PGA 2K style Sim positioning") is a **warped wireframe grid** that drapes over the green surface — square cells in world-XZ, Y bending with the topology, lines glowing in a slope-magnitude color ramp.

Reference image: see `reference_pga2k_warped_grid.png` in this folder (PGA Tour 2K green-reading grid). Lines bend because the mesh bends; the squares stay square in plan view.

This is **not the implementer's fault**. The SPEC said "arrow grid"; Code shipped an arrow grid. The SPEC was misaligned with the design lock. Fixing the SPEC, not the iter-2 code.

## What's NOT being thrown out (keep)

The whole data layer survives:

- `BakedZoneClassifier.GetPolygonAABBsForType(SurfaceType)` accessor ✅
- 0.5m-cell bake of slope vectors via `TrySampleMeshY` finite-difference ✅
- Aim-state gating (putter-only, via `ShotController.OnStateChanged`) ✅
- Distance + frustum culling ✅
- 8-site `PhysicsLabController` migration ✅
- Q2 color ramp thresholds (<2% green / 2-5% yellow / >5% red) ✅
- Q5 heatmap dashboard toggle ✅
- `HoleContext.OnChanged` rebake trigger ✅
- 8 EditMode tests for the bake step ✅

## What IS being thrown out

Only the render path:

- `_arrowMesh` SerializeField ❌
- `_arrowMaterial` SerializeField ❌
- `Graphics.RenderMeshInstanced` per-cell TRS loop in `Update()` ❌
- `_matBuf` / `_colorBuf` / `_colorV4Buf` per-frame instance buffers ❌
- `FlushBatch` helper ❌
- `LastVisibleCellCount` test seam (replaced with `MeshVertexCount`) ❌

## What's being added

- Procedural triangulated **heightfield mesh** covering all green cells (vertices on the 0.5m XZ grid, Y from `TrySampleMeshY`, vertex colors from baked slope magnitude)
- URP Shader Graph that draws **world-XZ grid lines** in the fragment shader, multiplied by the interpolated vertex color
- Single MeshFilter + MeshRenderer pair on a child GameObject; one draw call total
- `PhysicsLab_TestGreen.unity` scene with a sculpted heightfield green (sinusoidal undulation, code-generated mesh) — current production greens are all flat, so the visual gate needs a green with elevation to validate
- Lesson U in `tasks/lessons.md`: SPEC §Visual reference is mandatory for visual-fidelity tasks (paste reference image into spec folder, link from SPEC, write implementation language to match the image)

## Effort estimate

Medium delta on top of iter-1 (~6–8 hr of Code time + the pipeline chain). Most of the work is shader + test scene; the bake step doesn't change.

## Definition of redirect

Implementer reads:
1. This file (CESAR_REJECTION.md)
2. Updated SPEC.md (revised §Architecture + new §Visual reference + new §Test green requirement)
3. Existing IMPLEMENTER_REPORT.md (the data-layer summary still applies; render summary obsolete)
4. Lesson U in `tasks/lessons.md`

STATUS goes `ARCHITECT_REVIEW_PASS` → `CESAR_REJECTED` → next subagent run resumes Implementer for the redesign.
