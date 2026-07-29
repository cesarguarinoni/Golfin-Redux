ARCHITECT_REVIEW_PASS

golfin-redteam-reviewer PASS at 2026-07-29 (JST).

Adversarial gate: I re-derived every headline from primary source, not from the reviewer's report.
- Oracle = `grid` confirmed independently (Hole 14 rough 580,741 / ob 2,670,512 / water 48,786; sum = 1885×2048). Not `terrain_grid`, not alphamap.
- Mapping gate re-derived from the RAW raster myself: Orientation A 0/4 (all `ob`), Orientation B 4/4 (green/fairway/tee_box/water exact). Decisive.
- Decision numbers from coverage.csv: FIX 8,286,618 (68.33%) / BREAK 32,411 (0.27%) / OB 8,525 (0.07%) / trees 3,399,017 (28.03%). All 18 per-hole Default counts reconcile.
- Seam cross-check re-derived from raw seam_results.csv: 8,400/8,400 provenance agreement, strata sum to 8,400, 2,858 real Default cells all → C# Default+Fairway, boundary split 465/458/277 non-uniform & C#-matched, zero mismatches. Hole 02 included.
- SPEC §0 freshness gate run by me for all 18 holes: 17/18 within ≤0.06pp; only Hole 02 stale (0.0% vs 72.5%) — the documented case. No silently-stale hole poisoning the aggregate.
- Hole 02 quarantine holds (mislabeled-OB cells carry ObMask provenance, Default uncontaminated); recommendation robust excluding Hole 02 (251:1).
- Scope clean: git diff Assets/Scripts/Physics/ = 0, no zones.json mutated, no BakeZoneJsonTool, ClassifyWithProvenance committed.

Cosmetic slip (non-blocking): FINDINGS/REPORT say fix:break "253:1"; true value is 255.67:1 (~256:1). Both component counts reconcile from the CSV — pure mis-division, not fabrication. Recommendation unchanged. Correct to ~256:1 in FINDINGS §4 + report checklist at Cesar's close-out (docs-only).

Deliverable is fit for purpose: gives `surface_classification_ob_rough` a clear, correctly-caveated go (cheap path viable; trees-as-Rough a separate 3.4M-cell decision; 0.27% fairway residual = polygon-gap defect).

Route to Cesar for final approval.

---

## Architect close-out — 2026-07-29

**ACCEPTED.** Slip corrected: `253:1` → `255.67:1 (~256:1)` in FINDINGS §4 (×2) and IMPLEMENTER_REPORT (×2). Docs-only, recommendation unchanged.

**I re-derived the Hole 02 quarantine rather than accepting it.** The red-team's argument is that mislabeled-OB cells carry `ObMask` provenance and so never reach `Default`. That is correct, and here is the check it rests on — the stale-vs-fresh raster deltas:

| class | stale total | ObMask-caught | residue | **fresh total** | drift |
|---|---:|---:|---:|---:|---:|
| rough | 1,243,210 | 753,717 | 489,493 | **489,348** | 145 |
| trees | 1,859,150 | 1,687,927 | 171,223 | **170,554** | 669 |
| fairway | 192,197 | 0 | 192,197 | **192,197** | **0** |

The re-export moved cells **only into `ob`** — no cell moved between two non-OB classes (losses summed exactly to the OB gain: 1,688,596 + 753,862 + 15,013 = 2,457,471). So every cell whose label changed is an OB cell, the fresh runtime mask catches it at ladder step 2, and it cannot enter `Default`. **Both decision numbers are uncontaminated.** Fairway is byte-identical stale-vs-fresh, so BREAK is exact.

**One residue, non-blocking, recorded so it is not rediscovered:** Hole 02 `cart_path` shows 53,093 non-ObMask cells against a fresh total of 42,915 — ~10,178 cells the stale raster calls `cart_path` that the fresh raster calls `ob`. These sit in the **remainder** bucket, not in FIX or BREAK, so no decision number moves. They do mean the aggregate `OB-in-Default = 8,525 (0.07%)` is mildly **understated**. Still negligible, and it argues the same direction as the recommendation. A Hole 02 re-export on the Mac would clear it; not worth a re-run on its own.

**Deliverable accepted as the gate input for `surface_classification_ob_rough`.**
