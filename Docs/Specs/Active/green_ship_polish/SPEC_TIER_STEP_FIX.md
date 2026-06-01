# SPEC — Tier-step fix: restore 2-tier shelves flattened by iter-13 ramp band

**Authored:** 2026-06-01 10:30 CEST / 17:30 JST (Architect)
**Status:** SPEC_READY
**Track:** `green_ship_polish` — **PREREQUISITE to the green-seat re-architecture.** Must land + verify BEFORE the seat/seam (B1) spec, because this changes `relH` (real shelves return) and the seat model must build on corrected height data.
**Kickoff:** `Use the golfin-implementer subagent on "green_ship_polish" (tier-step-fix)`
**Scope:** ONE defect, ONE function. `bake-green.mjs` `smoothRidgeBand()` only. Re-bakes the 4 two-tier greens. No importer change, no schema change, no seat/seam work.

---

## Defect (Cesar-flagged, root cause VERIFIED in code + bake data)

Cesar: H7 should be a **two-tier (two-shelf) green** but now reads as a single slope. Confirmed: the tier was flattened by **iter-13's ridge-band smoothing**, which we shipped as DONE. This is a regression we introduced, caught now because Cesar scrutinized H7.

**Evidence chain:**
1. **Authoring is intact.** `Tools/GreenSlope/output/hole_07_slope_authoring.json`: `regionCount: 2`, 10-pt ridge, `ridgePresent`. H7 is correctly in `TWO_TIER_HOLES = {3,7,11,18}` (`bake-green.mjs` L109, source: A4 攻略冊子 PDF 「２段グリーン」). The tier intent is NOT lost in authoring.
2. **Baked `relH` is a ramp, not shelves.** Decoded H7 `green.json` heightGrid (54×61): plane-fit shows **0.443 m of the 0.474 m spread is smooth planar tilt; only 0.180 m is residual undulation**. Histogram is unimodal (no shelf gap) — statistically indistinguishable from flat hole H5. The two shelves are gone.
3. **The flattener is `smoothRidgeBand()` (`bake-green.mjs` L432+), via a mis-defined `tierDrop`:**
```
// L446-453 (CURRENT — WRONG):
tierDrop = hMax − hMin over ALL active cells   // = TOTAL green relief, not the tier STEP
rampWidth = 1.5 × tierDrop / RidgeTargetSlope(0.08)   // L478
```
   For H7: `tierDrop` picks up the **whole-green relief** (~0.474 m, which includes general slope + undulation, NOT just the step across the ridge). `rampWidth = 1.5 × 0.474 / 0.08 ≈ 8.9 m`, clamped to `maxBand = 0.40 × greenPerpWidth ≈ 10–11 m` → **not clamped**. An 8.9 m smoothing band on a green whose shelves are ~12 m apart **spans nearly the whole green** → both shelf flats are blended away → the hard tier step is smeared into the single 0.017 m/m ramp the histogram shows.

**Why iter-13 looked correct at the time:** it fixed the real staircase artifact (1–2 cell cliff rasterizing as bumps) by widening the barrier into a ramp. But sizing the ramp to *total relief* instead of the *tier step* makes the band far too wide on any green whose total relief exceeds its tier step (i.e. greens with slope/undulation on top of the step). All four `TWO_TIER_HOLES` are suspect (H18 relief 0.512 m → even wider band); H7 is just the one Cesar caught.

## The fix — measure the actual tier STEP, not total relief

`smoothRidgeBand` already receives `regionGrid` (L432) — region 0 / region 1 are the two shelves, split by the ridge. The correct `tierDrop` is the **height difference between the two shelves**, measured AWAY from the ridge (where the Poisson values are the true plateau heights, not the cliff transition).

Replace the `tierDrop` computation (L446-453) with a region-mean step:
```
// Tier STEP = |mean(region 0 plateau) − mean(region 1 plateau)|, measured on
// post-Poisson h[], using only cells FAR from the ridge (outside the eventual band)
// so plateau means aren't contaminated by the cliff transition zone.
//
// Two-pass: (1) provisional step from ALL active cells per region to seed an
// initial band guess; (2) recompute using only cells beyond ~RidgeMinBand from the
// ridge for a clean plateau mean. (Single clean pass acceptable if step is stable.)
//
//   sum0,n0 = Σ h[i], count  for active cells in region 0
//   sum1,n1 = Σ h[i], count  for active cells in region 1
//   tierStep = |sum0/n0 − sum1/n1|
//   tierDrop = tierStep        // feeds the EXISTING rampWidth formula unchanged
```
Everything downstream (the `rampWidth = 1.5 × tierDrop / RidgeTargetSlope` formula L478, the smoothstep blend, the C¹ guarantee) stays **exactly as-is**. We only correct the *input* magnitude. The band is now sized to the real step (H7 step is the ~region-mean delta, far smaller than 0.474 m total relief → a band of ~1–3 m, not 8.9 m) → the shelves survive, only the cliff between them is ramped.

> NOTE (implementer): use region-mean, not max−min within a region (a single outlier undulation cell shouldn't size the band). Measure plateau means from cells at distance > RidgeMinBand from the ridge if cheaply available; if that excludes too many cells on a small green, fall back to all-active-per-region means and report which path was used. The QA gate (below) catches a wrong magnitude either way.

## Why this is correct, not just smaller
- iter-13's **intent** was right: ramp the hard cliff to kill the staircase, hold peak slope ≤ RidgeTargetSlope for C¹ continuity. That intent is preserved — same formula, same smoothstep, same continuity gate.
- The **bug** was the magnitude fed in. Tier step is the physically meaningful quantity ("how tall is the step between shelves"); total relief is not. Correcting it sizes the ramp to the feature it's smoothing.
- A green with a big step still gets a proportionally wider ramp (correct — a tall cliff needs a longer ramp for C¹); a green with general slope + a small step no longer has its shelves eaten.

## What must NOT change
- The Poisson loop, `classifyRegions`, `buildSlopeGrid`, `ridgeSeparated`, the smoothstep blend, the mirror-sampling, `RidgeTargetSlope`/`RidgeMinBand`/`maxBand`, the `rampWidth` formula itself — all untouched. ONLY the `tierDrop` magnitude computation changes.
- `TWO_TIER_HOLES` set unchanged ({3,7,11,18}).
- Schema v2 byte layout, `green.json` structure — untouched.
- The importer (`HoleGeoImporter.cs`) — NOT touched in this spec. Seat/seam is the next pass.
- Non-tier holes (`applyRidgeBarrier=false`) — already early-return no-op (L436); unaffected, must stay byte-identical.

## Files touched
- `Tools/GreenSlope/scripts/bake-green.mjs` — `smoothRidgeBand()` `tierDrop` computation only (~L446-453).
- Re-baked `green.json` for the 4 two-tier holes (3, 7, 11, 18) under `Assets/Resources/HoleData/Hole_NN/`. (Re-run the bake for those 4; the other 14 are no-op for this function but re-bake all 18 if the pipeline is all-or-nothing — confirm the 14 non-tier come out byte-identical.)
- NO importer, NO schema, NO authoring JSON.

## Hard rules
1. `bake-green.mjs` `smoothRidgeBand()` ONLY. No other function, no importer.
2. Change the `tierDrop` *magnitude* only — `rampWidth` formula + smoothstep + continuity logic stay byte-for-byte.
3. Non-tier holes must re-bake byte-identical (early-return path untouched). Prove it.
4. Do NOT touch the iter-13 staircase fix's actual ramp mechanism — this corrects its input, it does not revert it. The staircase must NOT come back.
5. Re-bake is expected to CHANGE `relH` for the 4 tier holes (that's the point) → downstream importer meshes + physics will change when the seat/seam pass re-imports. This spec stops at the bake; do not re-import/re-bake the physics gate here (that happens in the seat/seam pass against final `relH`).

## Verification — the histogram is the objective gate
Re-bake H7 FIRST. Implementer reports, per tier hole:
```
Hole N: tierStep(new)=__ vs totalRelief(old tierDrop)=__  rampWidth(new)=__ vs (old)=__  bandCellCount=__
relH histogram (12 bins): __|__|...   bimodal? Y/N   shelf gap depth=__%
plane-fit: planeTilt=__m residualUndulation=__m  (ratio should DROP vs the 0.443/0.180 pre-fix split)
```
**Objective pass criteria:**
- **H7 relH histogram becomes BIMODAL** — two clusters with a low-count valley between them (the two shelves + the ramp between). Contrast pre-fix unimodal `4|6|9|14|14|10|8|9|8|9|6|4`. This is the machine-checkable proof the tier returned.
- `rampWidth(new)` ≪ `rampWidth(old ≈8.9m)` for H7 (expect ~1–3 m).
- Staircase does NOT return: max cell-to-cell Δh in the band still ≤ the iter-13 continuity gate (~5 cm/cell ≡ 10% slope). Both must hold — bimodal AND continuous.
- interiorY total spread may change slightly (the ramp no longer spreads the step across the green) — that's expected; the SHELVES are what matter, not the spread number.

**Then Cesar visual gate on H7:** orbit/overhead must show **two distinct shelves with a defined ramp between them**, not a single slope. (Cesar's flag is the real acceptance test; the histogram is the objective pre-check.) Frame-extract the orbit video and LOOK before captioning (v1 false-PASS discipline).

Spot-check: re-bake H3, H11, H18 — all must go bimodal (they were all suspect). H18 (largest relief) is the strongest test the fix scales. Confirm the 14 non-tier holes byte-identical.

## Definition of done
- `tierDrop` redefined as region-mean tier step in `smoothRidgeBand()`; everything else in the function byte-identical.
- H7 + H3/H11/H18 re-bake to **bimodal** relH histograms (shelves restored), AND band stays C¹-continuous (no staircase return) — both gates pass, reported per hole.
- 14 non-tier holes re-bake byte-identical (proof in report).
- Cesar visual sign-off: H7 reads as a genuine two-tier green.
- `relH` change is documented as the handoff input to the seat/seam (B1) pass.
- IMPLEMENTER_REPORT content-sanity per Lesson O.

## Handoff to next pass
Once Cesar signs off the tier: the seat/seam re-architecture (B1 — terrain-following seat plane, flag/cup on surface, welded seam) is specced against the CORRECTED `relH`. Do NOT start it until this lands — it depends on the final shelf heights.

## Open items to report back
1. Per-hole tierStep vs old-totalRelief, and rampWidth before/after (all 4 tier holes).
2. Did measuring plateau means "far from ridge" exclude too many cells on any small green? Which path used per hole.
3. Confirm H7 bimodal histogram + the shelf-to-shelf step matches the authoring intent magnitude (sanity vs the PDF if a target step is stated).
4. Any tier hole where the corrected (narrower) band re-introduces a staircase (bimodal but NOT continuous)? If so, that hole needs the band floored higher — flag, don't silently widen back toward total-relief.
