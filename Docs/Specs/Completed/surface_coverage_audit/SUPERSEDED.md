# SUPERSEDED — `surface_coverage_audit`

**Closed 2026-07-29. Not completed. Never shipped a valid measurement.**
**Superseded by:** `surface_fallthrough_coverage_probe` (`Docs/Specs/Active/surface_fallthrough_coverage_probe/`).

---

## Why it died

SPEC §3.2 sourced authored intent from the **terrain alphamap**. `HoleGeoImporter.ZoneToLayer` (`:1614-1630`) collapses fairway → rough, green → rough, bunker → rough, cart_path → rough, tee_box → rough; only semi_rough survives as its own layer. So `default_authored_fairway = 0.00%` was **a foregone conclusion produced by the collapse, not a measurement**.

The red-team FAIL at iteration 2 was **correct**. The architect error was mine: the §3.2 NOTE told the implementer to verify the *mapping* (which was fine) while the *source* was lossy — I guarded the wrong step.

The escalation's proposed fix was also wrong on two counts, both verified: (1) the pre-collapse raster is not in the runtime `zones.json`; it lives in a different tree with a different schema (`Tools/UHoleGeo/output/.../export/hole-NN/zones.json`, snake_case `ob_mask` vs runtime camelCase `obMask`). (2) The claim that `terrain_grid` under-reports fairway is false — fairway and green counts are identical between `grid` and `terrain_grid`.

**Correction to that second point, found 2026-07-29:** `terrain_grid` is nonetheless unusable as an oracle for a different and worse reason — **it has no `ob` class at all**, absorbing OB, trees and cart_path into `rough` (Hole 14: rough 580,741 in `grid` vs 3,472,630 in `terrain_grid`). The successor spec uses `grid` and prohibits `terrain_grid` explicitly.

---

## What survives and is reused

The **`ClassifyWithProvenance` seam** on `BakedZoneClassifier` is sound and is now committed and reused:
- additive, `#if UNITY_EDITOR`
- `Classify` and `ClassifyWithProvenance` both delegate to a shared private `ClassifyCore`, so provenance reporting is bit-identical to production classification **by construction**, not by test
- already reused read-only by the Hole-14 probe that confirmed `zone_bake_completeness`, and by the scope probe

`Assets/Scripts/Editor/SurfaceCoverageAudit.cs` is retained as a starting point for the successor's sampling loop. **Its authored-intent axis is poisoned — do not reuse that part.** The runtime and provenance axes are valid.

---

## Notion

Row `surface_coverage_audit` stays **Deferred**. Do not re-queue it; the successor supersedes it.
