# Self-Review — `surface_fallthrough_coverage_probe`

**Date:** 2026-07-29 12:58 JST
**Reviewer:** golfin-self-reviewer
**Iteration:** 2 (re-review of §5.1 seam-divergence closure)
**Verdict:** `FORWARD_TO_ARCHITECT`

---

## Verdict summary

The one FAIL from iter-1 (SPEC §5.1 seam-divergence risk) is closed. The added `FINDINGS.md §10` is REAL, not asserted: I located the underlying raw artifacts in the implementer's scratchpad (`seam_sample.csv` — 8,400 rows input; `seam_results.csv` — 8,400 rows with C# seam output columns) and re-derived every §10 headline number directly from `seam_results.csv`. Every claim reconciles cell-for-cell. The gap I named — Default-tier and boundary-cell coverage — is exactly what §10 provides (2,858 Default cells + 1,200 boundary cells, both at 100% Python↔C# agreement).

The iter-1 acceptance items I already passed still hold (re-confirmed): oracle is `grid` (Hole 14 CSV totals still bit-exact vs SPEC §2.2 across all 8 classes), mapping gate PASS/FAIL split intact, Hole 02 stale-raster reasoning intact, trees-separately intact, read-only intact (`git diff HEAD -- Assets/Scripts/Physics/` empty, 0 lines).

---

## How I verified §10 (re-derived vs consistency-only)

I did NOT have Unity MCP access to independently spot-check the C# seam on fresh cells. My verification is **primary-source re-derivation of the implementer's own recorded C# output**, plus internal-consistency reconciliation. That is a strictly weaker check than an independent C# call, but stronger than a consistency-only pass, because the primary artifact carries per-cell inputs (worldX, worldZ) AND per-cell C# outputs (cs_surface, cs_prov) — so I can distinguish "the numbers arithmetic-reconcile" from "the artifact is empty and the tables are made up."

### 1. Raw artifact exists at expected size

`/private/tmp/claude-501/-Users-cesar-Documents-GolfinRedux/1e92e4e1-a3f4-494f-8ae1-464b89a5faf4/scratchpad/seam_results.csv`
- 8,401 lines (1 header + 8,400 data rows).
- Schema: `hole, py, px, worldX, worldZ, py_prov, py_prov_name, py_zone, py_zone_name, sample_class, cs_surface, cs_prov, agree_surface, agree_prov`.
- Every row has non-empty `cs_surface` and `cs_prov` values (populated `Fairway`/`Polygon`/`ObMask`/`Default`, not `?`).

Sibling `seam_sample.csv` (8,401 lines, no cs_* columns) is the input file — same cells, no C# results — evidencing the two-step design (build sample → run through C# seam → append cs_* columns).

### 2. Every §10 headline number re-derived from the raw file

Ran `python3 csv.DictReader` over `seam_results.csv`. Results:

**Overall (§10 "Overall 8,400 / 8,400 (100.00%)"):**
- Total rows: 8,400 ✓
- `agree_prov == PASS`: 8,400 ✓
- Mismatches: 0 ✓

**Per-hole (§10 "1,400 (100.00%) × 6 holes"):**
- Hole 2: 1,400 | Hole 6: 1,400 | Hole 8: 1,400 | Hole 12: 1,400 | Hole 14: 1,400 | Hole 15: 1,400 ✓

**Sample-design strata (SPEC-required Default + boundary presence):**
- `sample_class` counts: interior_polygon=2400, interior_ob=2400, interior_default=2400, boundary=1200. Sum = 8,400 ✓ (2400+2400+2400+1200).
- Per-hole: 400 + 400 + 400 + 200 = 1,400. Reconciles with the sample-design table.

**Provenance-agreement table (§10):**
- The results table's interior_polygon=2,865 / interior_ob=2,677 / interior_default=2,858 sums to 8,400 exactly. My initial confusion — thinking these were additive with boundary=1,200 (which would give 9,600) — resolved by cross-tabbing `sample_class × py_prov_name`:

  | sample_class | Polygon | ObMask | Default |
  |---|---:|---:|---:|
  | interior_polygon | 2,400 | 0 | 0 |
  | interior_ob | 0 | 2,400 | 0 |
  | interior_default | 0 | 0 | 2,400 |
  | boundary | 465 | 277 | 458 |

  Column sums: Polygon 2,865, ObMask 2,677, Default 2,858 → **exactly the §10 results-table numbers**. So the §10 rollup groups by resolved provenance (with boundary cells folded into their provenance bucket), and the separate "boundary 1,200" row is a subset labeling, not additive. Everything reconciles.

**Default cells all return SurfaceType.Fairway (§10 key claim):**
- `cs_prov == Default` count: 2,858 ✓ (matches `py_prov == Default`)
- `cs_surface` for those 2,858: all `Fairway` (2,858 / 2,858) ✓ — consistent with `BakedZoneClassifier.DefaultSurface = SurfaceType.Fairway` (line 73).

**Source-raster oracle breakdown of the 2,858 Default cells (§10 table):**

| §10 table | My re-derivation |
|---|---|
| rough 1,813 (63.4%) | 1,813 ✓ |
| trees 853 (29.8%) | 853 ✓ |
| cart_path 94 (3.3%) | 94 ✓ |
| tee_box 29 (1.0%) | 29 ✓ |
| water 28 (1.0%) | 28 ✓ |
| bunker 24 (0.8%) | 24 ✓ |
| other 17 (0.6%) | fairway 9 + background 3 + ob 3 + semi_rough 2 = 17 ✓ |

Every cell reconciles.

### 3. Sanity-checks that argue against fabrication

Beyond bit-exact reconciliation, three shape-observations point at real data, not a made-up table:
- **Boundary provenance distribution is asymmetric** (465 Polygon / 277 ObMask / 458 Default). A fabricator would round to something even; this is the messy distribution real terrain produces near polygon edges.
- **The 2,858 Default cells contain 9 authored-`fairway` cells** (0.3%). That is consistent with (though slightly higher than) the full-scan aggregate 0.27% authored-fairway-in-Default from §4 — the kind of within-tolerance drift stratified sampling produces, not something invented to match a table.
- **The 100% Python↔C# provenance agreement across 1,200 boundary cells is itself an independent re-confirmation of the mapping gate** (Orientation B). If the raster→world mapping were off, boundary cells (which sit within ±2 px of a provenance transition) would be the FIRST place to see disagreement. Zero disagreement across 1,200 boundary cells means the C# and Python both landed in the same cells and computed the same PIP/obMask decisions — i.e. mapping is bit-consistent between the two implementations.

### 4. The specific gap I named in iter-1 is addressed

My iter-1 SELF_REVIEW called out: *"None of the 5 landmark probes tests a `Default` cell — and `Default` is the entire population whose composition drives the recommendation."*

§10 addresses this with:
- **2,858 Default-provenance cells** in the cross-check (~34% of the sample, deliberately over-sampled given the SPEC's decision hinges on Default composition).
- **1,200 boundary cells** where sub-pixel PIP/obMask disagreement would first appear.
- **100% agreement** on both.

That is the exact evidence I said was missing. Gate closed.

### Limitation on my verification (stated honestly)

I did NOT run `ClassifyWithProvenance` myself on a handful of these cells to independently confirm the recorded `cs_surface` / `cs_prov` values are what the live C# actually returns. Unity MCP was not in my tool inventory for this review, so I could not do the cell-level independent re-check the routing prompt suggested as ideal. My verification is therefore:
- Strong on: internal consistency, arithmetic reconciliation of §10 vs raw file, sample-design reconciliation, and shape/asymmetry sanity checks that argue against fabrication.
- Weak on: absolute-truth independent re-derivation via a fresh Unity MCP call (not possible for me here).

If the architect has Unity MCP available, a 10-cell spot-check picking, say, 3 rows each of Polygon/ObMask/Default plus 1 boundary row from `seam_results.csv` and running them through `ClassifyWithProvenance` would upgrade this from "very likely real" to "empirically confirmed." I do not consider that a blocking gap — the pattern of the raw data (asymmetric boundary distribution, 9 real fairway cells inside Default, exact §10 arithmetic reconciliation) is quite hostile to a fabrication hypothesis. Forwarding.

---

## Iter-1 items re-confirmed (Rule 5 re-walk)

Quick re-check because PIPELINE_HARDENING rule 5 requires a full acceptance re-walk on every pass. These were verified in iter-1 SELF_REVIEW and still hold:

1. **Oracle is `grid`, not `terrain_grid`.** Re-verified: Hole 14 CSV totals still bit-exact against SPEC §2.2's `grid` column for all 8 classes (fairway 257,120; green 20,208; semi_rough 831; rough 580,741; trees 201,917; cart_path 44,854; ob 2,670,512; water 48,786). No change from iter-1.
2. **Mapping gate PASS.** FINDINGS §1 unchanged (Orientation A 0/4, Orientation B 4/4, Hole 06 2/2). Additionally *reinforced* by 100% Python↔C# agreement on 1,200 boundary cells across 6 holes — an independent re-confirmation.
3. **Hole 02 stale-raster reasoning intact.** FINDINGS §6 unchanged; CSV row `2,4,rough` shows 753,717 ObMask cells and `2,5,trees` shows 1,687,927 ObMask cells (~2.44M), the cells whose authored-class is misrecorded but whose provenance is correctly ObMask, so Hole 02 Default count remains uncontaminated. The seam cross-check *included* Hole 02 and got 1,400/1,400 agreement — the stale raster does not affect the classifier ladder, only the oracle labeling, exactly as reasoned.
4. **Trees reported separately, excluded from decision numbers.** §5 unchanged.
5. **Read-only compliance.** `git diff HEAD -- Assets/Scripts/Physics/` → 0 lines. `git diff HEAD -- Assets/` shows nothing touched. Iter-2 only appended §10 to FINDINGS and updated report fields — no code paths mutated.
6. **All §5.4 required percentages present.** 68.33% FIX / 0.27% BREAK / 0.07% OB / 253:1 all in FINDINGS §4 and §6, unchanged.
7. **Aggregate arithmetic still self-consistent.** Polygon 5,344,894 + ObMask 28,697,144 + Default 12,128,074 = 46,170,112 ✓.

Nothing regressed in iter-2. Only §10 was added and IMPLEMENTER_REPORT §5.1 was updated FAIL→PASS coherently with the new evidence.

---

## Files touched (this review)

| Path | Change |
|---|---|
| `Docs/Specs/Active/surface_fallthrough_coverage_probe/SELF_REVIEW.md` | Overwritten — iter-2 verdict FORWARD_TO_ARCHITECT with §10 re-derivation |
| `Docs/Specs/Active/surface_fallthrough_coverage_probe/STATUS.md` | `READY_FOR_SELF_REVIEW` → `READY_FOR_ARCHITECT_REVIEW` |

---

## STATUS transition

Setting `STATUS.md` → `READY_FOR_ARCHITECT_REVIEW`. Route to `golfin-reviewer` (which will hand to `golfin-redteam-reviewer` on PASS per the two-gate review).
