# Red-Team Review — `surface_fallthrough_coverage_probe`

**Date:** 2026-07-29 (JST)
**Reviewer:** golfin-redteam-reviewer
**Verdict:** `ARCHITECT_REVIEW_PASS`

Scope: Tier-1 DIAGNOSTIC-ONLY, READ-ONLY probe (SPEC §8). Rules 16/17/18/19/21 are N/A (no scene/prefab/Figma/mesh/video). The only attack surface is measurement validity. I re-derived every number from primary source (`coverage.csv`, raw `Tools/UHoleGeo/.../hole-NN/zones.json` rasters, runtime `zones.json` obMasks, and `scratchpad/seam_results.csv`) — I did not confirm any artifact that asserts a number.

## What I independently re-derived

**Oracle (must be `grid`, not `terrain_grid`).** Decoded Hole 14 CSV totals: rough=580,741 (grid) NOT 3,472,630 (terrain_grid); ob=2,670,512 present (terrain_grid has no ob); water=48,786 (grid) NOT 74,180. Sum of all classes = 3,860,480 = 1885×2048 = source_dimensions, so no cell missed/double-counted. Oracle is definitively `grid`. Not poisoned.

**Mapping gate (SPEC's #1 stated risk).** I decoded the raw Hole-14 raster myself and applied BOTH orientations to all four SPEC §4.3 landmarks using the runtime obMask world rect (origin −155.3/−168.8, size 310.6/337.6):
- Orientation A: 0/4 — every landmark resolved to `ob` (wrong).
- Orientation B (flipped): 4/4 — Green_1→green, Fairway_1→fairway, Tee→tee_box, Water→water. Exact.
Decisive separation independently reproduced. Green_1 (−111.506,127.607)→`green` confirmed by my own decode, not inherited.

**Decision numbers.** From `coverage.csv`: Polygon 5,344,894 / ObMask 28,697,144 / Default 12,128,074 / grand 46,170,112 (poly+obm+dfl reconciles exactly). FIX (rough+semi in Default)=8,286,618=68.326% of Default; BREAK (fairway in Default)=32,411=0.267%; OB-in-Default=8,525=0.070%; trees-in-Default=3,399,017=28.026%. All 18 per-hole Default counts match FINDINGS §7 exactly.

**Seam cross-check (SPEC §5.1, central risk) — re-derived from raw `seam_results.csv`, not FINDINGS' table.** 8,400 data rows; holes {2,6,8,12,14,15}×1,400 (Hole 02 IS included); strata {interior_polygon 2400, interior_ob 2400, interior_default 2400, boundary 1200} sum to 8,400; provenance-resolved buckets Polygon 2865 / ObMask 2677 / Default 2858 sum to 8,400; `agree_prov` = PASS on all 8,400 (zero mismatches). The 2,858 Python-Default cells all return C# `Default` provenance AND `SurfaceType.Fairway`. Boundary split is non-uniform and plausible (Polygon 465 / Default 458 / ObMask 277) and matches C# cell-for-cell. Anti-fabrication shape holds: real Default cells exist, strata sum, boundary split is not a tautology. (Note: `agree_surface` column is literally "?" for all rows — Python did not emit a surface to diff — but `cs_surface` IS recorded and is Fairway for every Default cell, so the surface claim is backed and the provenance gate that actually drives bucketing is 100%.)

**SPEC §0 per-hole freshness gate — the implementer did NOT document this per-hole, so I ran it myself for all 18 holes** (source-raster ob-cell share vs runtime obMask set-bit share, 2pp tolerance):
- 17/18 PASS within ≤0.06pp.
- Only Hole 02 FAILs (src 0.00% vs runtime 72.53%) — the exact, known, spec-§5-documented stale-export case.
- **No silently-stale hole is poisoning the aggregate.** This was the single biggest way this measurement could have been invalid, and it is clean.

**Hole 02 quarantine.** CSV confirms no `ob` row for Hole 02 (stale raster) and its rough/trees carry 753,717+1,687,927 = 2.44M ObMask-provenance cells — the mislabeled-OB cells are caught by obMask (provenance ObMask), so Default composition is uncontaminated. Recommendation holds even excluding Hole 02 entirely (251.1:1). Seam check hit Hole 02 at 1,400/1,400.

**Scope.** `git diff HEAD -- Assets/Scripts/Physics/` = 0 lines. No `zones.json` in working tree. No `BakeZoneJsonTool` invocation. `ClassifyWithProvenance` is committed (diff=0), not an uncommitted edit. The only dirty files (Mobile_RPAsset, URPGlobalSettings, dailyreport.plist, ProjectSettings) are the pre-existing session-baseline environment noise — none code, none task-related.

## Prior-rejection / prior-gate findings

| Finding | Verdict |
|---|---|
| Oracle must be `grid` not `terrain_grid`/alphamap | GONE — independently confirmed `grid` (rough 580,741, ob present, water 48,786) |
| Mapping gate decisive (B 4/4, A 0/4), Hole 06 confirm | GONE — I re-derived 4/4 vs 0/4 from the raw raster myself |
| Seam divergence (Python vs C# ladder) | GONE — 8,400/8,400 re-derived from raw seam CSV; strata sum; boundary split real |
| Arithmetic slip 253:1 vs 255.67:1 | PRESENT but cosmetic — see disposition below |
| Hole 02 stale-oracle contamination | GONE — quarantined to ObMask; Default uncontaminated; recommendation robust w/o it |
| Scope violation (hidden fix/re-bake) | GONE — Physics diff 0, no zones.json, no bake |

## Three break attempts (all failed)

1. **Oracle poisoning:** tried to show `terrain_grid` was read. Hole 14 rough=580,741 / ob=2,670,512 / water=48,786 are the pre-collapse `grid` values; terrain_grid would show 3.47M/absent/74,180. Failed.
2. **Silent mapping error (the SPEC's named largest risk):** decoded the raster and ran both orientations on 4 landmarks myself. A 0/4, B 4/4, decisive. A wrong mapping would not score 4/4 on green+fairway+tee+water simultaneously. Failed.
3. **Undetected stale export poisoning the aggregate:** ran the §0 freshness gate on all 18 holes. Only Hole 02 (the documented case) is stale; the other 17 match to ≤0.06pp. The aggregate is not silently poisoned. Failed.

## 253→256 slip disposition (explicit, per routing prompt)

The true ratio is 8,286,618 ÷ 32,411 = **255.673:1** (≈256:1). FINDINGS §4 / IMPLEMENTER_REPORT say **253:1**. Both component counts (8,286,618 FIX and 32,411 BREAK) reconcile exactly from the per-hole CSV rows — so this is a **cosmetic mis-division, not hand-transcription and not fabrication**: the inputs are right, only the quotient is wrong, and both values are ~250:1 so decisiveness and the recommendation are unchanged. **Not a blocker.** It should be corrected to ~256:1 (255.67:1) in FINDINGS §4 and the report checklist row at Cesar's close-out (docs-only).

## Deliverable fitness (blocks `surface_classification_ob_rough`)

FINDINGS §9 gives a clear, correctly-caveated go: cheap path viable (256× decisive on the posed question); **trees-as-Rough carved out as a separate deliberate 3.4M-cell decision** (not silently resolved, per SPEC §5.6); 0.27% fairway residual correctly framed as a polygon-gap defect small enough to accept; Hole 02 refresh and OB fringe noted as no-runtime-impact. Not overstated, not under-caveated.

**Conclusion:** I actively tried to break the oracle, the mapping, and the freshness of the aggregate, and re-derived the seam agreement, the decision numbers, and the per-hole counts from primary source. Everything reconciles. Advancing to `ARCHITECT_REVIEW_PASS` with the 253→256 cosmetic-slip correction noted for close-out.
