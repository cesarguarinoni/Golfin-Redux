# ARCHITECT ESCALATION — green_slope_height_bake

**Raised:** 2026-05-29 by Cesar (rejecting iter-10).
**Why escalated:** 10 iterations, the green boundary is *still* visibly broken. This is no longer an implement-and-retry problem — it needs a hard architectural look at *which surface* produces the wavy boundary and at what resolution it is cut. The implementer chain has been alternating fixes (contour-polygon smoothing vs skirt vertical placement) without isolating the actual cause.

---

## Current defect (iter-10)

The iter-10 fix (`GreenSkirtDepth 0.10f → -0.02f` in `HoleGeoImporter.cs`) **did** clean up the *outer* boundary — the fringe→terrain edge on the lower/left of the green is now a smooth curve, and the dark triangular skirt facets are gone.

**But the wrong boundary was fixed.** The *inner* boundary — where the bright-green **putting surface meets the fringe/collar**, along the **top and right** of the green — is still completely wavy / stair-stepped. It looks like a regular short-step sawtooth, not a smooth curve.

**Asymmetry is the key clue:** the **left/downhill** side of the inner boundary reads smooth; the **top + right/uphill** side is jagged. Whatever produces the waviness correlates with the uphill terrain, not with the contour polygon (which iter-9 already smoothed and which looks fine in plan).

### Where to find the pictures
All under `Docs/Specs/Active/green_slope_height_bake/`:

| File | What it shows |
|---|---|
| `screenshots/h07_iter10_overhead.png` (1280×960) | **PRIMARY.** Top-down. Left edge smooth; **top + right inner green→fringe edge clearly stair-stepped/wavy.** This is the rejected defect. |
| `screenshots/h07_iter10_uphill.png` | Grazing uphill view — boundary waviness on the far/top edge. |
| `screenshots/h07_iter10_grazing.png` | NW grazing — shows the *lower* boundary that iter-10 DID smooth (for contrast). |
| `screenshots/h07_iter10_front_low.png`, `_left.png`, `_right.png` | Other angles. |
| `videos/h07_iter10_orbit.mp4` | 360° orbit; the top-edge waviness is visible as the camera passes the uphill side. |
| `videos/h07_iter8_orbit.mp4` | Prior iteration for before/after comparison. |

A fresh live 1100px overhead captured this session (`screenshot-isolated`, isolated=false, scene `Hole_07_Geo.unity` open) confirms the same defect — the architect can re-capture identically via MCP on `HoleRoot/Greens`, `cameraView=Top`, `padding≈1.05`, `resolution≥1100`.

---

## Iteration history (what each tried, what it left broken)

| Iter | Change | Result |
|---|---|---|
| 1–3 | Arrows→continuous gradient + height bake, schema v2, mesh deform, grid-force break | iter-3 **rejected**: uphill terrain poked through the flat green (terrain under green never graded). |
| 4 | Grade a level terrain **pad** under the green | **Superseded/reverted** — wrong approach. |
| 5 | Retarget: cut green+collar footprint from fairway + terrain mesh (shared `cutContour`), revert iter-4 pad | Carve sizing fixed; boundary still rough. |
| 6 | — | **rejected**: entire D3/D4 implementation + all screenshots were on the **deprecated `HoleLiteImporter` / `Hole_07.unity`**. Shipping path is **`HoleGeoImporter.cs` / `Hole_07_Geo.unity`**. |
| 7 | Port D3+D4 to the Geo importer | On the right importer now. |
| 8 | Consolidated fidelity pass: skirt + min-shift + resample + terrain-correlation gate | Boundary improved but wavy. |
| 9 | **Taubin λ-μ smoothing** of the 2D contour polygon in `bake-green.mjs` | Smoothed the **plan-view outline** (XZ). PASSed by reviewer on a **256px** canonical; Cesar rejected at full res — boundary still waved + dark skirt facets. |
| 10 | `GreenSkirtDepth 0.10→-0.02` (collar ring vertical placement) | Fixed dark facets + **outer** fringe→terrain edge. **Inner** green→fringe edge (top/right) **still wavy** → this rejection. |

**Pattern:** iter-9 attacked the contour polygon (XZ). iter-10 attacked the skirt height (Y). Neither isolated the surface that renders the visible *inner top-edge* waviness.

---

## Root-cause hypotheses for the architect to adjudicate

The unanswered question: **what surface is the visible inner (green→fringe) boundary, and at what resolution is it cut?** Candidates:

1. **CDT triangulation density along the green mesh edge.** The bright-green putting-surface mesh edge is built from the contour via `CDTTriangulate`. If the boundary triangle edges are coarse on the uphill side (where interior height relief is largest), the rim renders as a sawtooth. The smooth left/downhill vs jagged top/right asymmetry fits a height-driven triangulation artifact.
2. **Terrain hole-carve grid.** The green footprint is cut from terrain via `holes[hz,hx]=false` at `holesRes ≈ 0.3 m/cell`. If the *visible* inner edge is actually where the carved terrain hole meets the green/collar, the waviness is grid-rasterization at 0.3 m steps — and would track the uphill terrain. This matches the stair-step character.
3. **Splatmap/texture seam.** If the green/fringe colour transition is painted into the TerrainData alphamap rather than being a mesh edge, the waviness is alphamap-texel aliasing — independent of any mesh smoothing, which is why 9 iterations of geometry edits never killed it.
4. **Collar mesh inner edge vs green mesh outer edge mismatch.** The collar is `DilateContour(contour, collarWidth)`; the green is the raw contour. If those two boundaries are computed/resampled differently, the seam between them waves even when each individually looks fine.

**Recommended first diagnostic** (cheap, decisive): in `Hole_07_Geo.unity`, isolate and view *just the green mesh* (wireframe) vs *just the collar mesh* vs the *terrain splatmap* separately. Whichever one shows the top/right sawtooth in isolation is the surface to fix; the other edits have been treating symptoms on the wrong surface.

---

## Code / pipeline pointers

- **Bake:** `Tools/GreenSlope/scripts/bake-green.mjs` (Taubin `smoothContour`, perimeter gate). Output: `Assets/Resources/HoleData/Hole_NN/green.json`.
- **Importer (SHIPPING):** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — green mesh build, collar (`DilateContour`, `GreenSkirtDepth`), terrain hole-carve. (SPEC.md still references the deprecated `HoleLiteImporter` line numbers — see § Amendment iter-5/iter-6; treat Geo as canonical.)
- **Schema:** `Assets/Scripts/Course/Runtime/GreenTopology.cs` (v2 fields).
- **SPEC:** `SPEC.md` (issue definitions, hard rules, iter-5/6 amendments).
- **Reviewer history:** `ARCHITECT_REVIEW.md`, `CESAR_REJECTION.md` (iter-3/6/9/10), `IMPLEMENTER_REPORT.md` (per-iter diagnoses).

## Question to Cesar / architect

Before any iter-11, the architect should decide: **(a)** which of the four surfaces above is the visible top-edge boundary, **(b)** whether the correct fix is mesh-resolution, carve-resolution, or splat-resolution, and **(c)** whether the green-mesh edge and collar-inner edge are guaranteed to share one resampled boundary loop. Without that decision the implementer will keep smoothing the wrong surface.
