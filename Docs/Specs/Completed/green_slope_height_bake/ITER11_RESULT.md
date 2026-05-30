# ITER-11 RESULT — CORRECTED by video evidence

> **CORRECTION (2026-05-29).** An earlier version of this file concluded "all four
> variants are CLEAN with terrain stubbed → terrain coupling is the cause." **That
> was WRONG.** It was based on the small 4-pane still + per-variant closeups, which
> could not resolve the boundary. Cesar required orbit videos of each variant; the
> videos (and their frame extracts in `screenshots/iter11/frames/`) show the
> opposite. The orchestrator's prior "I verified the stills" claim did not hold —
> the stills were under-resolved. The video-verified verdict below supersedes it.

## Video-verified finding

**ALL FOUR variants scallop on the green→collar boundary, with terrain stubbed to a
constant `seatY`.** The defect is a regular bead/sawtooth along the collar's inner
edge (the bright-green putting surface meeting the darker collar ring), most visible
on the upper arc. Confirmed by direct frame inspection:
- `frames/varA_pct0.jpg`, `varA_pct50.jpg` — Variant A (DilateContour): clear beading.
- `frames/varB_pct0.jpg`, `varB_pct50.jpg` — Variant B (Minkowski): clear beading (arguably the most pronounced/regular).
- `frames/varC_pct50.jpg` — Variant C (≡A): same.
- `frames/varD_pct50.jpg` — Variant D (≡B): same.

## What this eliminates (the opposite of the prior verdict)

| Hypothesis | Verdict from video |
|---|---|
| Terrain coupling (collar ring sampling `terrain.SampleHeight`) | **NOT the cause** — scallop present with terrain stubbed. |
| `DilateContour` offset algorithm | **NOT the cause** — Minkowski (B) scallops too. |
| Green↔collar seam vertex mismatch | **NOT the cause** — shared-boundary (C) scallops. |
| Green/collar being separate meshes | **NOT the cause** — unified CDT (D) scallops. |

Per SPEC_ITER11.md § "What iter-12 does" decision matrix, this is the **"all four
scallop"** branch: *"CDT library or the input data has an asymmetry we haven't found.
Architect re-engages; diagnostic captures become the artifact to study."*

## ROOT CAUSE — found and quantified (orchestrator probes, 2026-05-29)

The cause is the **baked green HEIGHT along the boundary seam**, NOT the XZ outline and
NOT shading. Measured on the isolated variant meshes via `script-execute` (numbers
below are for Var_A; Var_B identical to 2 decimals — same height bake):

| Measurement | Result | Verdict |
|---|---|---|
| Normals (split-position / max angle) | ratio 1.00, 0 split, 0.0° | smooth — **NOT a flat-normal shading bead** |
| XZ contour high-freq (path-order Laplacian, sign-flips) | sub-cm, **13% flips** | outline is **smooth** (Taubin worked) — NOT the contour |
| Collar band width | mean 90.0 cm, stddev **0.07 cm** | uniform — **NOT a scalloped collar offset** |
| **Seam height (Y) high-freq, order-INDEPENDENT** | **mean 12.53 cm, max 47.21 cm, 62% sign-flips** | **THE BUG** |

The green↔collar seam (170 verts) has a smooth XZ outline but its **per-vertex height
zig-zags ~12.5 cm every 0.5 m segment** (62% sign-flips = true segment-frequency
zig-zag). Crucially, **max local dev (47.21 cm) ≈ the full green height range (47.4 cm),
and seam min Y = 0.0** → some boundary vertices collapse to the baseline (~0) while their
neighbors carry full baked height. That is a **height-sampling failure at the contour
edge**: boundary points sample the height field where it is undefined/outside-coverage
and fall back to 0.

The collar bridges this jagged seam to a flat outer ring, so each 0.5 m collar segment
tilts by a different amount → the smooth-interpolated normals swing along the ring →
the visible light/dark scallop. All four variants share the same `green.json` height
data, so they bead identically; terrain is irrelevant (isolated harness, seatY=0).

**This is why 11 iterations missed it:** iter-9 smoothed the XZ contour (confirmed
smooth here), iter-10 + the importer/terrain work never touched the boundary *height*
sampling. The defect was always in the height domain at the seam — iter-10's implementer
even said "the waves are HEIGHT undulation" but mis-located the source as terrain.

## iter-12 direction (high confidence)
Fix the **boundary height** in the bake/sampling, NOT the outline and NOT the collar:
1. **Most likely:** the height grid (`bake-green.mjs`, AABB+0.5 m pad, filled *inside*
   the contour) does not cover the contour perimeter, so seam samples land on
   outside/zero cells. Extend/extrapolate the height field past the contour (dilate the
   filled region by ≥1 collar width) OR clamp-sample to the nearest valid interior cell,
   so boundary vertices get a valid, smooth height.
2. Then low-pass the seam-height loop (preserve the real ~47 cm macro slope, kill the
   ~12 cm per-segment zig-zag) as a belt-and-suspenders step.
**Decisive confirm before coding:** dump seam-vertex Y vs whether each sample fell
inside the height-grid coverage; the zero-Y verts should correlate 1:1 with
outside-coverage samples.

## Carried-over caveats
- The yellow/cyan/magenta overlays and triangle-edge wireframe never rendered
  (`OnDrawGizmos` is Scene-view only; the wireframe capture is a silhouette). So
  triangulation still has NOT been seen directly — do the normals/wireframe probe above.
- C collapsed to ≡A and D to ≡B (CDT already shares boundary verts), so the matrix
  effectively tested DilateContour vs Minkowski — both scallop.

## Artifacts
- Videos: `videos/iter11/iter11_variant_{A,B,C,D}_orbit.mp4` (5s, 1280×720, orbit, captioned).
- Frame extracts: `screenshots/iter11/frames/var{A,B,C,D}_pct{0,25,50,75}.jpg`.
- Stills (under-resolved — see correction): `screenshots/iter11/iter11_all_variants_overhead.png` + closeups.
- Diagnostic code: `Assets/Scripts/Editor/CourseImporter/Debug/GreenVariantDiagnostic.cs`.
