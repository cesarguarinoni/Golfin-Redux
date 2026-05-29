# SPEC ITER-11 — Isolated green mesh diagnostic (multi-variant)

**Authored:** 2026-05-29 16:34 CEST / 23:34 JST (Architect)
**Status:** SPEC_READY
**Kickoff:** `Use the golfin-implementer subagent on "green_slope_height_bake" (iter-11)`
**Scope:** ONE goal — render four candidate solutions side-by-side **with the green mesh isolated from terrain**, captured top-down. Cesar visually picks the one that looks right; iter-12 promotes it to production. **No production code changes in iter-11.** All variant code lives in a dedicated debug file and a debug-only editor menu.

---

## Why this iter, and why four variants

The defect is asymmetric scallop on the green↔collar boundary (overhead `h07_iter10_overhead.png` — left/downhill side smooth, top + right/uphill side dramatically scalloped). Verified on-disk: input `contourResampled` is 170 uniform points, 0.5 m spacing, closure gap 0.5 m (correctly closed loop), no duplicates, no sharp turns. So **the contour is not the problem.**

The tee mound work (Apr 15 → Apr 20, multiple chats) hit the same fingerprint and burned several "smooth the boundary" iterations before the fix landed. Cesar's own lessons from that arc:
- *"Two failed attempts at the same fix shape should trigger adversarial review rather than another variation."* We just had iter-9 (Taubin contour smooth) and iter-10 (skirt depth) — both "fix-shape variations" on smoothing the boundary. Adversarial review is iter-11.
- *"Chamfer vs. exact polygon-edge distance — blur fixes 1-cell Voronoi noise but not N-cell plateau spokes."* The asymmetric scallop is N-cell plateau-spoke shaped, not 1-cell noise. Boundary-smoothing was always the wrong category of fix.

The tee fix was never in the polygon — it was in the **outward mechanism** (the skirt), made per-cell adaptive. We don't yet know which surface produces the green's scallop. Four candidate causes, four rendered candidate solutions, all in one isolated debug scene with no terrain coupling so the test is decisive.

---

## What "isolated" means in this iter

- **No terrain integration.** No `holes[hz,hx] = false` carve. No fairway mesh. No splatmap. The debug scene contains: a large grey backdrop quad at `seatY − 1.0`, the four mesh variants, and a top-down camera.
- **No `terrain.SampleHeight` for the collar outer ring.** All variants use a constant `seatY = 0.0f` baseline. Outer-ring Y is computed without sampling terrain. This is the critical control: it removes the terrain-coupled axis of variation, so any remaining asymmetric scallop in any variant is a code/mesh bug, not a terrain artifact.
- **Same baked `green.json`** (H07, iter-9 Taubin'd contour, min-shifted height field) feeds all four variants. Same `contourResampled`, same height grid. Differences between panes are *purely* in how the green+collar geometry is built from that data.
- **One hole only: H07.** The 17 others aren't touched. iter-11 is diagnostic; iter-12 ports the winning variant to production and rebakes all 18.

## The four variants

Each variant builds a green+collar mesh from the same `green.json` data, at a different XZ position in the debug scene (`(0,0)`, `(40,0)`, `(0,-40)`, `(40,-40)`), parented under `DebugGreenVariants`. Mesh material identical to production so colors read the same.

### Variant A — ISOLATED-BASELINE (control)

Calls the existing `CreateGreenMeshCDT` + `DilateContour` path verbatim, with one substitution: every `terrain.SampleHeight(...)` call returns `seatY` (constant). Outer-ring vertices Y = `seatY`. Inner-ring Y = `seatY + heightField(boundaryXZ)`. Skirt Y = `seatY` (flat ring, no terrain drop).

This is the production code with all terrain dependencies neutralized. If it scallops, the bug is in the mesh-build (DilateContour or CDT) and is independent of terrain. If it's clean, the bug is somewhere in the terrain coupling and iter-12 needs an adaptive outward mechanism (the tee parallel).

### Variant B — MINKOWSKI-OFFSET

Same as A in every respect except `DilateContour(contour, 0.65f)` is replaced with a clean **Minkowski-sum offset**: each contour vertex generates an outward arc fillet of radius 0.65 m around it on convex corners, and a single mitered point on concave corners (with miter clamp at 2× radius to prevent spikes). Resulting offset polygon is then re-sampled to ~0.5 m segment length for CDT input. Reference algorithm: standard 2D polygon offsetting (Clipper / Skia path stroke / etc. — pick any clean implementation; this is well-studied, no novel math).

If A scallops and B is clean → `DilateContour` is the bug. The current per-vertex-normal offset likely produces self-intersections or spikes on sections with tight curvature, and that asymmetry tracks where the input has more curvature variation.

### Variant C — SHARED-BOUNDARY

Same as A except: build the green submesh first, capture its outer-ring vertex array verbatim, and pass it **by reference** as the collar submesh's inner ring (no re-resampling, no re-dilation of the inner edge, no separate vertex computation). The collar's outer ring is still the dilated polygon as in A.

If A scallops and C is clean → the green↔collar seam in production has subtly mismatched vertex positions (resampling / floating-point drift / index ordering), and the visible boundary is a mesh-seam artifact, not a polygon artifact.

### Variant D — UNIFIED-CDT

Build a **single** CDT mesh covering both green and collar regions, no separate submeshes. The outer constraint is the dilated polygon (Minkowski-offset, same as B). An interior constraint marks the green↔collar seam (the resampled contour). Submesh assignment is by which side of the seam constraint each triangle's centroid falls on. Zero `DilateContour` involvement *inside* the mesh — the only place the dilated polygon is used is as the outer CDT boundary, after which the topology is one connected manifold.

If A, B, C all scallop and D is clean → the bug is the fact that green and collar are separate CDT meshes at all. Both need to be one mesh with the seam as an internal constraint.

## Diagnostic overlays (drawn in each pane)

For each variant pane, draw on top of the mesh at `seatY + 1.0`:
- **Yellow line loop** — the input `contourResampled` (170 pts). Same in all four panes. Lets us verify the input contour is clean (it is, per the data check, but worth visual confirmation per Cesar's "verify don't trust" rule).
- **Cyan line loop** — the offset polygon used by that variant (`DilateContour` in A/C, Minkowski in B/D). The two should look identical-ish in shape but the cyan in A should look wavy if `DilateContour` is the bug.
- **Magenta wireframe** — the mesh triangulation. Renders into a separate `Gizmos.DrawLine` pass or a wireframe-shader material; whichever the implementer finds cleanest. Shows whether scallop lives in the mesh's actual triangle edges vs. the polygon-overlay illusion of edges.

## Captures (saved to `Docs/Specs/Active/green_slope_height_bake/screenshots/iter11/`)

Top-down orthographic camera framing all four panes in one shot:
- `iter11_all_variants_overhead.png` — 4-pane overhead, full meshes + overlays visible. Primary deliverable.
- `iter11_variant_A_closeup.png` … `_D_closeup.png` — per-variant zoomed top-down, mesh only (overlays off).
- `iter11_variant_A_wireframe.png` … `_D_wireframe.png` — per-variant zoomed top-down, **wireframe only** (no material fill), for direct comparison of triangulation patterns.

## Files touched

- **NEW:** `Assets/Scripts/Editor/CourseImporter/Debug/GreenVariantDiagnostic.cs` — all four variant builders, the menu item, the capture harness.
- **NEW:** `Assets/Scripts/Editor/CourseImporter/Debug/GreenVariantDiagnostic.asmdef` if needed for editor-only scope.
- **NEW:** `Assets/Scenes/Debug/Hole_07_Geo_Diagnostic.unity` — empty scene with backdrop quad + camera, opened by the menu item.
- **NO CHANGES** to `HoleGeoImporter.cs`, `bake-green.mjs`, `GreenTopology.cs`, any `green.json`, or any production scene. Diagnostic file is editor-only and shippable code is unaffected.

Variants A, B, C, D may share helper code (CDT call wrapper, material lookup, vertex-array utilities) — keep it in `GreenVariantDiagnostic.cs`, not extracted to a shared library yet. iter-12 will lift the winning variant's logic into `HoleGeoImporter` as a single targeted change.

## Hard rules

1. Editor-only. No runtime, no build pipeline. Diagnostic code never ships.
2. Zero production-code changes in iter-11. iter-12 is where a fix lands, informed by what we see here.
3. H07 only. `green.json` and `contourResampled` consumed verbatim — no re-baking in this iter.
4. No terrain integration in any variant. `terrain.SampleHeight` returns constant `seatY` for the diagnostic harness; if `CreateGreenMeshCDT` calls it directly, wrap or stub it. Whatever is cleanest — this is editor code, not architecture.
5. All four variants must use the *same* CDT library call and the *same* base material so the comparison is fair. Only the inputs to CDT (boundary polygons, seam constraints, shared-vertex passing) differ.

## Definition of done

- Editor menu item `Debug → Build Green Variants (H07)` opens the diagnostic scene and instantiates all four variants under `DebugGreenVariants`.
- All four panes render top-down at the same orientation, same scale, with mesh + yellow + cyan + magenta overlays.
- `iter11_all_variants_overhead.png` captured at ≥1600 px wide, all four variants distinguishable, scallop pattern (if present) clearly visible per pane.
- Per-variant closeup + wireframe captures saved.
- The implementer report includes: (a) which variants scallop and which don't, on their honest first-pass read; (b) which polygon (input vs DilateContour vs Minkowski) looks visually wavy if any; (c) any setup choices made for stubbing `terrain.SampleHeight`.

## Open items the implementer should report back on

1. If `CreateGreenMeshCDT` is structured such that stubbing `terrain.SampleHeight` cleanly is awkward (e.g. it's called deep inside the function), flag the alternative — a small refactor extracting an `IHeightProvider` interface, or duplicating the CDT body into the diagnostic with `seatY` hardcoded. Cleaner duplication is fine for editor-only code; we don't ship it.
2. If Variant D's "single mesh with internal seam constraint" can't be done with the existing CDT library (does it accept internal constraints?), flag the workaround — likely splitting into two meshes that share an inner-vertex array, which is then Variant C, and Variant D becomes "C + Minkowski outer" rather than truly unified.
3. If the Minkowski offset library brought in for B is non-trivial (a Clipper dependency), flag the size/license. A 100-line custom convex-arc offset is acceptable for diagnostic; we're not shipping it yet.

## What iter-12 does, conditional on iter-11 results

- **A clean, B/C/D scallop similarly to production:** terrain coupling is the bug. iter-12 ports the tee's per-cell adaptive outward mechanism to the green's collar. Same algorithmic family that resolved the tee asymmetry.
- **A scallops, B clean:** `DilateContour` is the bug. iter-12 replaces it with the Minkowski offset, in production.
- **A scallops, C clean:** Seam-vertex mismatch. iter-12 forces shared-by-reference inner-ring vertices in production.
- **A/B/C all scallop, D clean:** Single-mesh refactor. iter-12 unifies green+collar into one CDT mesh in production. (Bigger change, but the data would have argued for it decisively.)
- **All four scallop:** CDT library or the input data has an asymmetry we haven't found. Architect re-engages; diagnostic captures become the artifact to study.
