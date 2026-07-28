# SPEC — `surface_fallthrough_coverage_probe`

**Tier:** 1 — SURGICAL. **DIAGNOSTIC ONLY. READ-ONLY. NO FIX. NO RE-BAKE.**
**Blocks:** `surface_classification_ob_rough` (Order 1260) — the approach decision there cannot be made without this number.
**Supersedes:** `surface_coverage_audit` (Deferred — its authored-intent oracle was invalid; see §2).
**Depends on:** `zone_bake_completeness` (DONE `b7ebbf000`) — this probe is only meaningful against the re-baked `zones.json`.

---

## 1. Why this exists

On 2026-07-28 I declared the cheap path for `surface_classification_ob_rough` — `DefaultSurface = Fairway` → `Rough`, one line — **dead**. The reason: a read-only probe showed Hole 14's *visible* fairway resolving by `Default` fallthrough rather than by polygon, so flipping the default would have turned real, visible fairway into rough.

**`zone_bake_completeness` removed that reason.** The dropped polygons were restored (Hole 14's runtime `zones.json` now carries 6 zone groups where it carried 4). The fairway that was resolving by fallthrough should now resolve by polygon.

So the question that killed the cheap path is open again, and it is a measurement:

> Of the in-footprint ground that still resolves via `DefaultSurface` fallthrough, how much is authored **Fairway** (which the cheap path would BREAK) versus authored **Rough/Semirough** (which the cheap path would FIX)?

This probe answers exactly that and nothing else.

---

## 2. Two poisoned oracles — do not use either

This task has already been attempted once and failed on oracle choice. **Both of the following will produce a confident, wrong, foregone-conclusion answer. Neither is acceptable as the authored-intent source.**

### 2.1 ❌ The terrain alphamap — sank `surface_coverage_audit`
`HoleGeoImporter.ZoneToLayer` (`:1614-1630`) collapses fairway → rough, green → rough, bunker → rough, cart_path → rough, tee_box → rough. Only semi_rough survives as its own layer. Sourcing "authored fairway" from the alphamap therefore guarantees `0.00%` **as an artifact of the collapse, not as a measurement**. That was my error in the previous spec's §3.2 and the red-team FAIL against it was correct.

### 2.2 ❌ `terrain_grid` in the source raster — **NEW, verified 2026-07-29, and the more dangerous of the two**
The source raster file carries two per-cell arrays. They are **not** interchangeable. Measured on Hole 14:

| class | `grid` | `terrain_grid` |
|---|---:|---:|
| fairway | 257,120 | 257,120 |
| green | 20,208 | 20,208 |
| semi_rough | 831 | 831 |
| rough | 580,741 | **3,472,630** |
| trees | 201,917 | — |
| cart_path | 44,854 | — |
| **ob** | **2,670,512** | **— (absent)** |
| water | 48,786 | 74,180 |

**`terrain_grid` has no `ob` class at all — OB, trees and cart_path are all absorbed into `rough`.** Using it would classify all 2.67M out-of-course OB cells as "authored rough," inflating the *cheap-path-fixes-it* number toward a foregone YES. It is the same failure shape as §2.1, pointing the opposite direction.

**The absorption reconciles exactly** (Hole 14, independently re-derived 2026-07-29 — the claim above is arithmetic, not impression):

```
terrain_grid.rough - grid.rough  = 3,472,630 - 580,741 = 2,891,889
ob + trees + cart_path           = 2,670,512 + 201,917 + 44,854 = 2,917,283
difference                       =    25,394
terrain_grid.water - grid.water  = 74,180 - 48,786 =  25,394   ✓ exact
```

So `terrain_grid` = `grid` with `{ob, trees, cart_path}` folded into `rough` and 25,394 cells reassigned to `water`. Zero residual. There is no ambiguity about which array is the pre-collapse one.

**Use `grid`. Not `terrain_grid`. Not the alphamap.**

---

## 3. The correct oracle

`Tools/UHoleGeo/output/{courseSlug}/export/hole-NN/zones.json` — the pre-collapse source raster, the same file the new §4.2 completeness gate in `BakeZoneJsonTool` already reads.

Relevant fields (verified on Hole 14):
- `source_dimensions` → `{width: 1885, height: 2048}` (per-hole; **do not hardcode**)
- `zone_index` → `{0:background, 1:fairway, 2:green, 3:semi_rough, 4:rough, 5:trees, 6:bunker, 7:water, 8:cart_path, 9:ob, 10:tee_box}`
- `grid_encoding` → `base64_uint8`
- `grid` → base64 uint8, `width × height` bytes, row-major. **This is the oracle.**

`rough` and `semi_rough` survive here as distinct indices. That is the whole reason this file rescues a measurement the alphamap could not support.

### 3.1 `semi_rough` is authored noise — verified across all 18 holes (2026-07-29)

A read-only sweep of `zone_stats.pixel_count` across every hole:

| class | min | max | holes below 1,000 px |
|---|---:|---:|---:|
| fairway | 38,265 | 257,120 | 0 |
| green | 6,038 | 76,445 | 0 |
| **semi_rough** | **314** | **1,564** | **15 of 18** |
| rough | 201,777 | 1,243,210 | 0 |
| trees | 84,765 | 1,859,150 | 0 |
| ob | **0** | 3,223,842 | 1 |
| tee_box | 4,467 | 75,945 | 0 |
| bunker | 5,326 | 30,018 | 0 |
| water | 0 | 129,876 | 10 |
| cart_path | 9,740 | 58,344 | 0 |

Two consequences, both of which the implementer should carry rather than rediscover:

1. **`semi_rough` is effectively not authored.** It sits below the §4.2 completeness gate's own 1,000-cell noise threshold on 15 of 18 holes, and peaks at 1,564. `surface_classification_ob_rough`'s FINDINGS frames Defect B as "Rough **and** Semirough are never classified," which reads as two comparable problems. It is one problem: **Rough.** Still report the `semi_rough` bucket separately per §5.4 — but if it lands near zero that is the authored data being faithful, not a probe defect.
2. **Rough is the dominant authored surface on every hole** (min 201,777 px). Whatever the fallthrough set turns out to contain, rough is not a marginal class.

This sweep is aggregate `zone_stats` only — it is **not** a substitute for the §5 per-cell measurement, which is the thing that actually answers the decision question. It is context, and it is falsifiable against the per-cell run: if §5 reports a fallthrough `rough` share wildly inconsistent with these totals, distrust the mapping (§4) before distrusting these numbers.

**Note the schema difference from the runtime file:** source raster is snake_case (`ob_mask`), runtime is camelCase (`obMask`). They are different trees with different schemas. Do not cross-assume field names.

---

## 4. MAPPING GATE — blocking, must pass before any number is produced

The source raster carries **no world-space bounds**. `hole-manifest.json` has `bounds: {}` — empty. The mapping must be derived, and **a derived mapping that is silently wrong produces a plausible, fully-populated, completely meaningless CSV.** That is the single largest risk in this task.

### 4.1 Derivation
The runtime `zones.json` `obMask` block gives the world rect. Verified Hole 14:

```
obMask: worldOriginX -155.3, worldOriginZ -168.8, worldSizeX 310.6, worldSizeZ 337.6
```

This matches `hole-manifest.json` → `terrain.terrain_width_m 310.6` / `terrain_length_m 337.6` exactly, and the aspect ratios agree: raster `1885/2048 = 0.9204`, world `310.6/337.6 = 0.9200` → uniform ≈ **0.1648 m/px on both axes**. The raster therefore covers the same world rect as the terrain footprint.

Candidate mapping for raster cell `(px, py)`:
```
worldX = worldOriginX + (px + 0.5) / width  * worldSizeX
worldZ = worldOriginZ + (py + 0.5) / height * worldSizeZ        // row orientation UNRESOLVED
```

### 4.2 The unresolved unknown: row orientation
Whether raster row 0 is at `worldOriginZ` (+Z-up) or at `worldOriginZ + worldSizeZ` (flipped, image convention) is **not established**. Resolve it empirically — do not assume, and do not pick the one that "looks better."

### 4.3 Positive control (mandatory)
Test **both** orientations against known landmarks and accept only the one that matches. Use at minimum these four, on Hole 14:

| landmark | world (X, Z) | expected `grid` class |
|---|---|---|
| `Greens/Green_1` centroid | (−111.506, 127.607) | `green` |
| `Fairways/Fairway_1` centroid | (−42.815, 62.402) | `fairway` |
| a Tee mesh centroid (read from the scene) | — | `tee_box` |
| a Water mesh centroid (read from the scene) | — | `water` |

**Pass condition:** one orientation matches **all four**; the other matches **at most one** (a mapping that is genuinely correct should win decisively — if both score similarly, the mapping is not validated, it is coincidental).

**On failure: ABORT.** Write the finding, produce no coverage numbers, and escalate. Do not weaken the control to get a result. Producing numbers off an unvalidated mapping is the exact failure this project has now paid for twice.

Re-run the same control on **one additional hole** (pick any with all six zone groups) to confirm the orientation is a pipeline constant and not a per-hole accident.

---

## 5. Method

1. **Reuse the existing seam — do not re-implement point-in-polygon.**
   `BakedZoneClassifier.ClassifyWithProvenance(fp, fp, out ClassifyProvenance)` already exists (`#if UNITY_EDITOR`, delegating to the shared `ClassifyCore` that `Classify` also calls, so it is bit-identical by construction). Committed as part of this task's housekeeping. Re-implementing the ladder inside the audit tool risks divergence that would silently invalidate the whole measurement.

2. **Sample at source-raster resolution, 1:1 with `grid` cells** — no resampling, no interpolation. One sample per raster cell, at the cell centre, via the §4 mapping.

3. **For each cell record:** `holeId, px, py, worldX, worldZ, authoredClass (from grid), runtimeSurface, provenance (Polygon|ObMask|Default)`.

4. **Tabulate.** The decision numbers are computed over **fallthrough cells only** (`provenance == Default`), and reported *also* as a share of total footprint so the absolute scale is visible:
   - `% of fallthrough authored fairway` → **the cheap path breaks this much**
   - `% of fallthrough authored rough + semi_rough` → **the cheap path fixes this much**
   - `% of fallthrough authored ob` → should be ~0 if the obMask is doing its job; a non-trivial number here is a **separate finding** and must be reported, not smoothed over
   - remainder (green / tee_box / bunker / water / cart_path / trees / background) broken out, not lumped

5. **Run all 18 holes.** Per-hole rows plus an aggregate. A per-hole outlier is a finding, not noise — Hole 15 in particular inverted the failure last time (its fairway classified as `Green`), so report it individually.

   **Known anomaly — Hole 02 has `ob` = 0 px in the source raster.** It is the only such hole. Expect its fallthrough-authored-`ob` figure to be trivially zero, and **check whether its runtime `zones.json` carries an `obMask` at all** — if it does not, `hasObMask` is false and step 2 of the classifier ladder is skipped entirely on that hole, so *everything* not covered by a polygon falls through. Report that as a **finding in its own right**; do not treat it as a probe malfunction and do not exclude Hole 02 from the aggregate without saying so.

6. **Trees (index 5) are not a ground surface.** Report the trees bucket separately and do **not** fold it into either decision number. If it is large, say so and let it be a decision input rather than resolving it silently.

---

## 6. My prediction, stated up front so it is falsifiable

I expect fallthrough cells to be overwhelmingly authored `rough` (+ a sliver of `semi_rough`), because rough is painted into the splatmap and never polygonized, while OB should now be caught by the obMask and fairway/green by the restored polygons. That would indicate the cheap path is back on the table.

**This prediction is not the answer and must not shape the method.** It is recorded here so that if the numbers come out differently I cannot retrofit a story. If authored fairway in the fallthrough set is non-trivial, report it plainly — that kills the cheap path a second time, and that is a perfectly good outcome for this task.

**If the two numbers come out close, say they are close.** Do not stretch an ambiguous measurement into a recommendation.

---

## 7. Deliverable

- `coverage.csv` — per-cell or per-hole-aggregated rows (per-hole aggregate is sufficient if per-cell is prohibitively large; state which was produced)
- `FINDINGS.md` — the mapping-gate result incl. both orientations' control scores; the per-hole table; the aggregate; the two decision percentages; the OB-in-fallthrough number; the trees bucket; and an explicit approach recommendation for `surface_classification_ob_rough` **or** an explicit statement that the data does not support one.

---

## 8. Out of scope — do not do these

- **No fix.** Do not change `DefaultSurface`. Do not change `IsObAt`. Do not touch out-of-grid handling.
- **No re-bake.** Do not run `BakeZoneJsonTool`. The re-baked `zones.json` from `b7ebbf000` is the subject under measurement; re-baking mid-probe destroys the thing being measured.
- **No Defect A measurement.** "Beyond the terrain footprint classifies Fairway" is already unambiguous and needs no number — and the source raster covers only the footprint, so it cannot speak to outside it anyway.
- **No coefficient changes**, no `PHYSICS_TUNING_CHANGELOG.md` entry. Nothing here alters ball behaviour.

---

## 9. Report

`IMPLEMENTER_REPORT.md`: the §4.3 control table for both orientations with pass/fail; which `grid` was used and confirmation it was **not** `terrain_grid`; per-hole and aggregate tables; the two decision percentages; every anomaly encountered, including any hole where the raster file was missing (the §4.2 gate skips-with-warning when `Tools/UHoleGeo/` is absent — the same absence must be reported here, not silently skipped).

**Derive from the primary source; do not confirm an artifact that asserts it.**
