# SPEC ITER-13 — Ridge-slope staircase fix (issue 4 of 4)

**Authored:** 2026-05-30 07:10 CEST / 14:10 JST (Architect)
**Status:** SPEC_READY
**Kickoff:** `Use the golfin-implementer subagent on "green_ship_polish" (iter-13)`
**Parent task:** `green_ship_polish` — four ship-blocker issues from `ARCHITECT_ESCALATION.md` + iter-12 review. Order: ridge bumps (this iter) → fairway break → raised ring → off-center.
**Scope:** ONE issue. Visible bumps/staircase on the **ridge slope** (the tier transition itself), confirmed in image 2 of Cesar's iter-12 review and quantified via architect probe of post-iter-12 `green.json`.

---

## Root cause (verified, not inferred)

In `Tools/GreenSlope/scripts/bake-green.mjs:402` the Poisson relaxation treats the ridge as a **hard barrier**: `ridgeSeparated()` checks if two adjacent cells have different region IDs in `regionGrid`, and if so, the cells cannot average with each other during Gauss-Seidel iterations (L423). Each region's height field integrates independently from its own gradient field.

This is correct for the *interior* of each tier — but at the ridge itself, it means the upper-tier and lower-tier cells have no continuity constraint at their shared border. Whatever values they happen to converge to on either side of the ridge become the ridge step. Verified on disk for H07: a perpendicular slice across the ridge midpoint shows the entire 14 cm tier drop happening in **80 cm of horizontal distance**, with the steepest section at ~32% slope (3–5 cells wide). Because the ridge runs **diagonally** across the axis-aligned 0.5 m grid, this near-cliff is rasterized as a staircase. The mesh CDT then samples that staircase at vertex resolution and renders the bumps Cesar sees.

The tier flats themselves are smooth — Laplacian sub-millimeter across an 11×17 m patch on the upper tier. The bug is **localized to the ridge slope**, exactly matching the image.

## What we don't want to change

- Tier flats stay smooth (the Poisson-with-barrier behavior is correct for them).
- The macro tier height difference stays (~14 cm at H07's ridge midpoint, ~9 cm where the ridge thins).
- No schema bump; no importer change; no mesh-build change. Bake-only.
- No new arrow authoring (the user-traced ridge polyline is the authority).

## The fix — controlled ridge ramp width

The bake currently treats the ridge as a **zero-width** barrier. Real-world tier transitions are not zero-width; they are a steep ramp 1–3 m wide. We change the barrier from "hard zero-width discontinuity" to "soft band of controllable width" in **one tunable parameter**, applied post-Poisson.

### Algorithm — `smoothRidgeBand` in `bake-green.mjs`

After Poisson relaxation completes and **before** min-shift, do an additional pass:

1. **Identify ridge-band cells.** For each grid cell, compute `distRidge = minimum distance from cell center to the ridge polyline`. Cells with `distRidge ≤ rampWidth/2` are in the ridge band.
2. **Blend across the band.** For each ridge-band cell:
   - Let `t = distRidge / (rampWidth/2)` ∈ [0, 1] — distance from ridge centerline, normalized.
   - Get the cell's current height `h_self` (from Poisson, one region's value).
   - Find the **mirror cell** across the ridge — same perpendicular distance from ridge centerline, opposite side. Sample its height `h_mirror` via bilinear (mirror point may not align with a cell center).
   - Blend with **smoothstep weight on `t`**: `h_new = lerp(midpoint(h_self, h_mirror), h_self, smoothstep(t))` where `midpoint = (h_self + h_mirror) / 2`.
   - At `t=0` (on ridge centerline): `h_new = midpoint` — clean averaging, both sides agree exactly.
   - At `t=1` (band edge): `h_new = h_self` — Poisson value preserved.
   - Smoothstep ensures C¹ continuity at both endpoints; no new kinks introduced.
3. **Write back** to the height grid in-place.

### Parameters

- `RidgeRampWidth = 1.5f` — meters of horizontal ramp width. Real golf tier ramps are typically 1–3 m. Start at 1.5 m, tune by eye if needed; sane bounds 1.0–2.5 m.
- No other tunables. The smoothstep weighting is fixed.

This preserves:
- Macro tier height difference (centerline of band sits at the average of the two regions' values, which equals what the discontinuity was approximately at).
- Tier flat smoothness (cells outside the band are untouched).
- All slope-grid values (slope is not regenerated from height; the slope grid is built independently from arrows).

### Why this rather than other options

Considered and discarded:
- **Make ridge a soft barrier in the Poisson loop itself** (allow some weighted averaging across regions during relaxation). Would intermix the two regions' fields globally, not just at the boundary. Risks contaminating the tier flats with cross-tier slope information.
- **Replace ridge-as-barrier with no barrier and rely on authored arrows alone.** Would lose the tier height difference entirely — the Poisson loop would relax it away.
- **Tessellate the mesh at the ridge with finer CDT density.** Doesn't fix the data; just renders the existing staircase at higher resolution. Worse, not better.

Smoothstep band post-pass is the minimum-surface-area change: one function, one parameter, runs after Poisson is done, touches only ridge-adjacent cells.

## Files touched

- `Tools/GreenSlope/scripts/bake-green.mjs` — add `smoothRidgeBand()` function, call it after `buildHeightGrid()` returns, before min-shift.
- All 18 `Assets/Resources/HoleData/Hole_NN/green.json` — regenerated by `--all`.

Nothing else. Schema unchanged, byte layout unchanged, importer unchanged.

## Verification — architect-replicable, before in-engine

Implementer extends `verify-boundary-coverage.mjs` (or adds `verify-ridge.mjs`) to report, per hole with a ridge:

```
H07: ridge length 11.2m, 23 ridge cells
  pre-iter13: ridge perpendicular slope max:  31.8%  (over 0.5m cell)
              ridge perpendicular slope mean: 24.3%
              cells in ramp band (1.5m):     94
  post-iter13: ridge perpendicular slope max:  9.4%
               ridge perpendicular slope mean: 6.8%
               band continuity check:          ✓  (no Δh > 5cm between adjacent band cells)
```

Acceptance: post-iter-13 ridge perpendicular slope max ≤ 12% (real-world tier ramps cap there for a 1.5 m band carrying ~14 cm of drop); band continuity check passes (no jumps between adjacent ridge-band cells).

## In-engine verification

Reimport H07 only. Cesar checks the **ridge slope specifically** from the gameplay-camera angle of image 2:
- Ridge slope reads as a clean ramp, not a stair.
- Tier flats unchanged (still smooth, no new bumps introduced anywhere on the surface).
- Macro tier height difference visible — upper tier still sits visibly higher than lower tier.
- Boundary bead from iter-12 still gone (no regression).

If signed off → `--all`, reimport all 18, spot-check the other 2-tier holes (3, 11, 18).

## Hard rules

1. Single file touched in code: `Tools/GreenSlope/scripts/bake-green.mjs`. Plus regenerated `green.json`s.
2. Single new function: `smoothRidgeBand()`. Called once, after `buildHeightGrid`, before min-shift.
3. Single new parameter: `RidgeRampWidth = 1.5`.
4. **Do not modify** the Poisson loop, `ridgeSeparated`, `buildSlopeGrid`, `classifyRegions`, or any importer code.
5. **Do not** add a runtime smoothing pass. The fix is bake-time only; mesh build sees the corrected height field and renders it.
6. No schema changes. v2 byte layout intact.

## Definition of done

- `bake-green.mjs --hole 7` produces a `green.json` where the verify script reports ridge perp slope max ≤ 12%.
- Reimport H07: ridge slope is a clean ramp, no staircase or bumps. Tier flats unchanged. Boundary bead from iter-12 still absent.
- Cesar in-engine sign-off on H07 from the image-2 camera angle.
- `--all` writes 18 fresh `green.json`s; 2-tier holes (3, 11, 18) get same ridge-band smoothing visible in-engine; flat / single-region holes untouched (no ridge to smooth).

## Open items the implementer should report back on

1. Final `RidgeRampWidth` setting after H07 sign-off. If 1.5 m visually reads as too gentle (tiers look like soft mounds), drop to 1.0 m. If too sharp, raise to 2.0–2.5 m. Document the chosen value.
2. Whether the mirror-cell sampling (point on the opposite side of ridge centerline) requires a different lookup strategy on holes where the ridge polyline has tight curvature. If the perpendicular projection doesn't land cleanly inside the green for some band cells, flag and fall back to nearest-cell-in-opposite-region.
3. Whether the band continuity check ever fails on the other 2-tier holes (3, 11, 18). If it does, the ridge polyline on that hole may have a sharp kink or near-self-intersection that breaks the perpendicular-distance assumption — architect look needed.

---

## Queue (4 issues, locked order)

- [ ] **iter-13** — Ridge-slope staircase bumps **(this spec)**
- [ ] **iter-14** — Fairway breaking around the green
- [ ] **iter-15** — Raised green ring (donut/pillow rim)
- [ ] **iter-16** — Off-center raise
