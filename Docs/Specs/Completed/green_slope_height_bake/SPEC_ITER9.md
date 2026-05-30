# SPEC ITER-9 — Contour smoothing (issue 1 only)

**Authored:** 2026-05-29 12:14 CEST / 19:14 JST (Architect)
**Status:** SPEC_READY
**Kickoff:** `Use the golfin-implementer subagent on "green_slope_height_bake" (iter-9)`
**Scope:** ONE issue: wavy green↔collar border. Issues #2 (raised green / off-center raise) and #3 (fairway breaking around green) stay parked until Cesar signs off on this in-engine.

---

## Diagnosis (verified against on-disk data, see ITER8 follow-up below)

The wavy border is **the green contour polygon itself being irregular**, not the green surface being bumpy. The baked height field is smooth (verified earlier: H07 height field PNG is a clean 2-tier with smooth ridge, no per-cell lumps, range [0, +0.472] after iter-8 D3 min-shift). The border you see is the XZ boundary between putting surface and collar; it's defined by the 2D polygon, not by Y values. A perfectly flat green and a wildly bumpy green would render that border identically.

**Why iter-8 D1 didn't fix it:** `resampleContour` is linear arc-length (correct — preserves shape). `smoothContour` is Laplacian, 2 passes at α=0.3 — far too weak to remove the mid-frequency waves (3–5 m wavelength) inherited from the source 32-point contour (spacing varies 1.4–3.7 m, CV=0.25). And plain Laplacian, even with more iterations, would shrink the polygon toward the centroid — which is why simply cranking the passes was the wrong fix.

## Fix

Replace `smoothContour` in `Tools/GreenSlope/scripts/bake-green.mjs` with **Taubin λ-μ smoothing** (Taubin 1995 — the canonical non-shrinking smoothing filter, used by MeshLab/PyTorch3D/Cartagen/MIRTK with the same defaults).

### Algorithm

Each iteration applies two Laplacian passes with alternating signs:

```
for iter in 0..N:
    # Shrink pass (λ > 0):
    for each vertex i:
        avg_i = (prev_i + next_i) / 2
        new_i = curr_i + λ · (avg_i − curr_i)
    pts ← new

    # Inflate pass (μ < 0, |μ| > λ):
    for each vertex i:
        avg_i = (prev_i + next_i) / 2
        new_i = curr_i + μ · (avg_i − curr_i)
    pts ← new
```

The inflate step moves vertices *away* from the neighbor average, undoing the shrinkage from the shrink step while preserving the smoothing of high-frequency waves. Net effect: same shape envelope, mid+high frequency wobbles removed.

### Parameters (canonical, from cross-referenced implementations)

- **`λ = 0.5`** (shrink pass coefficient)
- **`μ = −0.53`** (inflate pass coefficient; |μ| > λ is required for convergence per Taubin)
- **`iterations = 12`** (start; 10–15 is the canonical range, MeshLab/PyTorch3D defaults are 10)

Signature change:

```js
function smoothContour(contour, iterations = 12, lambda = 0.5, mu = -0.53)
```

Call site stays a one-liner. Existing `smoothContour(resampleContour(rawContour, 0.5), 2, 0.3)` becomes `smoothContour(resampleContour(rawContour, 0.5))` (defaults give Taubin), or explicit `smoothContour(resampleContour(rawContour, 0.5), 12, 0.5, -0.53)`.

## Safeguard — perimeter sanity check

Before vs after, in `bake-green.mjs`:

1. Compute `perimeterOriginal` (sum of segment lengths in the resampled-but-unsmoothed contour).
2. Compute `perimeterSmoothed` (after Taubin).
3. Print to `bake_report.txt`:
   ```
   contour smoothing: perimeter 89.42m → 88.95m (Δ -0.5%, 12 Taubin iters λ=0.5 μ=-0.53)
   ```
4. **FAIL LOUD** if `|Δ| > 2%`. Taubin shouldn't shrink at all in theory; any sizable change means the math is mis-implemented (likely a sign error on μ). Sanity check, not an expected failure mode.

## Out of scope

- Issue #2 (raised green ring / off-center raise) — separate iter.
- Issue #3 (fairway breaking around green) — separate iter.
- Surface smoothness (already verified clean in iter-8; the height field is good).
- Importer changes — `HoleGeoImporter` already reads `contourResampled` from `green.json` as single source of truth (iter-8 D1 implementation, line 2495). It will pick up the smoother contour automatically. Zero importer change in this iter.

## Hard rules

1. Only file touched: `Tools/GreenSlope/scripts/bake-green.mjs` (function body of `smoothContour`, call site if signature changes, perimeter check in bake report).
2. No schema change. `green.json` v2 layout unchanged; the same `contourResampled` field just carries a smoother polygon.
3. No importer change. HoleGeoImporter remains as iter-8 left it.
4. No bake parameter changes outside `smoothContour` (don't touch resample spacing, IDW, Poisson, min-shift, etc.).
5. Bake all 18 holes after H07 sign-off (`bake-green.mjs --all`), commit the regenerated `green.json` files in the same commit as the script change.

## Definition of done

- `smoothContour` reimplemented as Taubin λ-μ, defaults λ=0.5 μ=-0.53 iterations=12.
- `bake_report.txt` shows perimeter delta line for every hole; all 18 within ±2%.
- Reimport H07: the green↔collar border reads as a clean smooth oval. No visible mid-frequency wobbles when viewed from any angle.
- Cesar in-engine sign-off on H07 border. If signed off, `--all` and reimport all 18.
- Issues #2 and #3 untouched (regression-check against iter-8 baseline screenshots).

## Open items the implementer should report back on

1. Final perimeter Δ% on H07 and the average + max across all 18. If any hole exceeds 1.5%, flag it for an architect look — likely means that hole's source contour has tight concavities the smoothing is rounding off (not a math bug, but worth knowing about).
2. If the H07 border is *still* visibly wavy after 12 iterations, was the issue actually at the bake or in the importer's CDT triangulation? (The importer uses `contourResampled` as a CDT constraint — if the CDT inserts Steiner points and reshuffles, the visible boundary edges might not be the smoothed polygon edges. Flag this and we'll look at the CDT pass next.)
