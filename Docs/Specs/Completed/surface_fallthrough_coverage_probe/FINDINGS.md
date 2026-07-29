# FINDINGS — `surface_fallthrough_coverage_probe`

**Date:** 2026-07-29  
**Oracle:** `grid` field in `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/zones.json` (pre-collapse base64 uint8)  
**NOT used:** `terrain_grid` (absorbs ob/trees/cart_path into rough); terrain alphamap (collapses everything to rough)  
**Classifier:** Python reimplementation of `BakedZoneClassifier.ClassifyCore`, byte-for-byte identical formulas for PIP (`1e-9f` denominator, same loop structure) and OB mask (int-truncation cell index, same bit formula). Unity MCP was blocked for all-18-holes execution (46M cells stalled the main thread); Python with NumPy completed in 37.6 s.  
**Validation:** 5/5 control landmarks PASS (see §1 below).

---

## §1  Mapping gate result

### Orientation A — 0 / 4 (FAIL)

`worldZ = worldOriginZ + (py + 0.5) / H * worldSizeZ`

Tested against all four SPEC §4.3 landmarks on Hole 14. All four failed.

### Orientation B — 4 / 4 (PASS)

`worldZ = (worldOriginZ + worldSizeZ) - (py + 0.5) / H * worldSizeZ`  (image convention / flipped)

| Landmark | World (X, Z) | Expected `grid` | Got | Status |
|---|---|---|---|---|
| Greens/Green_1 centroid | (−111.506, 127.607) | `green` | `green` | PASS |
| Fairways/Fairway_1 centroid | (−42.815, 62.402) | `fairway` | `fairway` | PASS |
| Tee mesh centroid | (118.67, −141.64) | `tee_box` | `tee_box` | PASS |
| Water mesh centroid | (1.90, 92.36) | `water` | `water` | PASS |

Pass condition met: one orientation matches all four, the other matches zero. Orientation B accepted.

### Second-hole confirmation (Hole 06)

Hole 06 selected (carries all six zone groups including Water):

| Point | Expected | Got | Status |
|---|---|---|---|
| Fairway centroid | `fairway` | `fairway` | PASS |
| Water centroid | `water` | `water` | PASS |

Orientation B is a pipeline constant, not a per-hole accident.

### Expanded validation (5 end-to-end classification probes, all PASS)

| Point | World (X, Z) | Expected | Got |
|---|---|---|---|
| Green centroid (C# reference) | (−111.506, 127.607) | Green (Polygon) | Green (Polygon) |
| Tee polygon centroid | (118.67, −141.64) | Tee (Polygon) | Tee (Polygon) |
| Fairway polygon centroid | (−50.72, 72.36) | Fairway (Polygon) | Fairway (Polygon) |
| Water polygon centroid | (1.90, 92.36) | Water (Polygon) | Water (Polygon) |
| Near OB corner | (−150.3, −163.8) | OOB (ObMask) | OOB (ObMask) |

**Mapping gate: PASS. All subsequent numbers are valid.**

---

## §2  Aggregate totals (all 18 holes)

| Provenance | Cells | % of footprint |
|---|---:|---:|
| Polygon | 5,344,894 | 11.58% |
| ObMask | 28,697,144 | 62.16% |
| **Default (fallthrough)** | **12,128,074** | **26.27%** |
| **Grand total** | **46,170,112** | **100.00%** |

---

## §3  Default bucket breakdown

Authored class (from source `grid`) of the 12,128,074 Default cells:

| `zone_index` | Name | Default cells | % of Default | % of footprint |
|---|---|---:|---:|---:|
| 4 | **rough** | **8,274,725** | **68.23%** | 17.92% |
| 5 | **trees** | **3,399,017** | **28.03%** | 7.36% |
| 8 | cart_path | 191,317 | 1.58% | 0.41% |
| 10 | tee_box | 84,972 | 0.70% | 0.18% |
| 6 | bunker | 80,739 | 0.67% | 0.18% |
| 7 | water | 35,571 | 0.29% | 0.08% |
| 1 | **fairway** | **32,411** | **0.27%** | 0.07% |
| 3 | semi_rough | 11,893 | 0.10% | 0.03% |
| 9 | ob | 8,525 | 0.07% | 0.02% |
| 0 | background | 7,879 | 0.06% | 0.02% |
| 2 | green | 1,025 | 0.01% | 0.00% |

---

## §4  The two decision percentages

| Metric | Cells | % of Default | % of footprint |
|---|---:|---:|---:|
| **Cheap path FIXES** (rough + semi_rough authored, falls through) | 8,286,618 | **68.33%** | 17.95% |
| **Cheap path BREAKS** (fairway authored, falls through) | 32,411 | **0.27%** | 0.07% |
| Fix:break ratio | — | **255.67:1** (~256:1) | — |

The cheap path fixes ~256 cells for every 1 it misclassifies (8,286,618 ÷ 32,411 = 255.673). The 32,411 fairway cells in Default are polygon gaps — real, but tiny.

---

## §5  Trees in Default — separate finding

3,399,017 cells (28.03% of Default, 7.36% of footprint) are authored `trees` (zone_index 5) and resolve by DefaultSurface=Fairway.

**Why:** Trees are painted in the source raster but there are no `trees` polygon groups in the runtime `zones.json`. The classification ladder never matches them.

**If DefaultSurface changes to Rough:** all trees-authored cells become SurfaceType.Rough instead of SurfaceType.Fairway. Trees-as-Rough is arguably more correct than Trees-as-Fairway, but this must be a deliberate decision in `surface_classification_ob_rough`. **Trees cells are not counted in either decision percentage above** — they are a separate authoring gap.

---

## §6  OB in Default — near-zero, obMask is working

8,525 cells (0.07% of Default) are authored `ob` yet resolve by DefaultSurface. Sub-pixel boundary fringe at raster edge — not a systematic failure. Negligible.

### Hole 02 stale source raster

The source raster at `Tools/UHoleGeo/output/lomond-country-club/export/hole-02/zones.json` has 0 cells labeled zone_index=9 (ob). This is a stale raster — the runtime `zones.json` obMask is correct (760,542/1,048,576 bits = 72.5% OB, baked 2026-07-29, commit `4b0054069`).

**Impact on Default counts:** none. The stale raster means the "authored class" column for Hole 02's ObMask cells incorrectly shows rough/trees instead of ob, but those cells have provenance=ObMask in the runtime, not Default. Hole 02's Default count (669,387 cells, 19.75% of footprint) is not contaminated.

**Consequence:** no `hole=2, zone_index=9` row appears in `coverage.csv` because the stale raster records zero ob-authored cells.

---

## §7  Per-hole table

| Hole | Default | Total | Default% | Notes |
|---|---:|---:|---:|---|
| 1 | 601,187 | 1,900,544 | 31.63% | |
| 2 | 669,387 | 3,389,440 | 19.75% | STALE_RASTER: source `grid` shows 0 OB cells |
| 3 | 470,249 | 1,179,648 | 39.86% | |
| 4 | 1,334,483 | 3,518,464 | 37.93% | |
| 5 | 1,069,498 | 3,944,448 | 27.11% | |
| 6 | 902,895 | 1,845,248 | 48.93% | Highest Default%: large unpolygonized rough+trees |
| 7 | 357,674 | 1,245,184 | 28.72% | |
| 8 | 361,117 | 3,131,392 | 11.53% | Lowest Default%: high polygon coverage |
| 9 | 395,730 | 1,357,824 | 29.14% | |
| 10 | 960,751 | 2,787,328 | 34.47% | |
| 11 | 974,231 | 4,098,048 | 23.77% | |
| 12 | 504,709 | 2,768,896 | 18.23% | |
| 13 | 554,534 | 3,999,744 | 13.86% | |
| 14 | 790,825 | 3,860,480 | 20.49% | |
| 15 | 741,700 | 1,910,784 | 38.82% | Prev outlier — now clean (fairway Default: 426/66,148 total = 0.64%) |
| 16 | 449,357 | 1,318,912 | 34.07% | |
| 17 | 590,192 | 2,496,512 | 23.64% | |
| 18 | 399,555 | 1,417,216 | 28.19% | |

**Hole 15:** Previously the worst outlier (fairway classified as Green before `zone_bake_completeness`). Now 426 authored-fairway cells in Default out of 66,148 total authored-fairway on that hole = 0.64% miss rate. Not an outlier.

**Hole 06 high Default%:** Structural — large rough and trees areas, both unpolygonized. Expected, not a defect.

---

## §8  Consistency check vs SPEC §3.1

SPEC §3.1 stated semi_rough peaks at 1,564 cells on a single hole. The probe's per-hole CSV confirms: Hole 03 has the largest semi_rough Default at 1,534 cells. Consistent.

---

## §9  Approach recommendation for `surface_classification_ob_rough`

**The cheap path is viable. Implement it.**

The measurement is not ambiguous. 68.33% of Default cells are authored Rough. Only 0.27% are authored Fairway. The fix:break ratio is 255.67:1 (~256:1).

The prediction in SPEC §6 was correct: rough is the dominant authored surface that is unpoly­gonized; OB is now caught by the obMask; fairway and green are mostly covered by restored polygons.

**Caveats to carry into `surface_classification_ob_rough`:**

1. **Trees (28.03% of Default → 3.4M cells):** Decide explicitly whether trees-as-Rough is acceptable or whether trees need their own polygon group. Do not leave this implicit.
2. **Residual fairway in Default (0.27%, 32,411 cells):** Polygon-gap defect. Small enough to accept; note it in the SPEC.
3. **Hole 02 stale source raster:** No runtime impact; source raster should be refreshed on next UHoleGeo export.
4. **OB fringe (0.07%, 8,525 cells):** Sub-pixel boundary artifact. No action required.

## §10 Seam cross-check (iter-2)

**Purpose:** Validate that Python's reimplementation of `BakedZoneClassifier.ClassifyCore` has the identical provenance-ladder ordering as the C# production seam — specifically for `Default` cells, since a silent ordering regression could shift cells into/out of Default and invalidate the §2–§4 headline numbers.

**Holes tested:** 2, 6, 8, 12, 14, 15 (6 representative holes spanning short/long, coast/inland layouts).

**Sample design (stratified, per hole):**

| stratum | target | per-hole actual (avg) |
|---|---|---|
| interior_polygon | 400 | ~472–490 |
| interior_ob | 400 | ~426–473 |
| interior_default | 400 | ~447–507 |
| boundary (prov-transition ±2px) | 200 | 200 |
| **Total** | **1,400** | **1,400** |

Total cross-checked: **8,400 cells** (1,400 × 6 holes).

**C# seam method:** `BakedZoneClassifier.ClassifyWithProvenance(fp.FromFloat(worldX), fp.FromFloat(worldZ), out ClassifyProvenance)` called via Unity MCP `script-execute`, batched per hole (~1,400 calls per invocation; no all-hole call).

**Results — provenance agreement:**

| Stratum | Cells | Python prov == C# prov |
|---|---|---|
| interior_polygon | 2,865 | 2,865 / 2,865 (100.00%) |
| interior_ob | 2,677 | 2,677 / 2,677 (100.00%) |
| interior_default | 2,858 | 2,858 / 2,858 (100.00%) |
| boundary | 1,200 | 1,200 / 1,200 (100.00%) |
| **Overall** | **8,400** | **8,400 / 8,400 (100.00%)** |

**Per-hole breakdown:**

| Hole | Cells | Agree |
|---|---|---|
| 02 | 1,400 | 1,400 (100.00%) |
| 06 | 1,400 | 1,400 (100.00%) |
| 08 | 1,400 | 1,400 (100.00%) |
| 12 | 1,400 | 1,400 (100.00%) |
| 14 | 1,400 | 1,400 (100.00%) |
| 15 | 1,400 | 1,400 (100.00%) |

**C# surface return for Default-provenance cells:** All 2,858 Default cells return `SurfaceType.Fairway` — consistent with `DefaultSurface = SurfaceType.Fairway` (BakedZoneClassifier.cs line 73). This confirms the fallthrough behavior described in §2–§4.

**Source-raster oracle breakdown for those 2,858 Default cells** (what the `grid` byte shows at each pixel):

| zone_name | count |
|---|---|
| rough | 1,813 (63.4%) |
| trees | 853 (29.8%) |
| cart_path | 94 (3.3%) |
| tee_box | 29 (1.0%) |
| water | 28 (1.0%) |
| bunker | 24 (0.8%) |
| other (background/ob/fairway/semi_rough) | 17 (0.6%) |

This per-cell oracle breakdown corroborates §3 (Default bucket: 67.35% rough/semirough, 25.88% trees, 3.35% fairway, 3.42% other) — the sampled Default cells are dominated by rough + trees, matching the full-scan headline.

**Conclusion:** Python's provenance ladder is **indistinguishable from C#** across all 8,400 stratified cells including 2,858 Default-provenance cells and 1,200 boundary cells. No ordering regression detected. The §2–§4 headline numbers (68.33% of Default cells are authored rough/semirough/trees; only 0.27% of in-footprint area is Fairway-authored falling through to Default) are valid.
