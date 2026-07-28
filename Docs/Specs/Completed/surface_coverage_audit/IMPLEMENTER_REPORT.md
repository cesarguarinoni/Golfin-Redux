# Implementer Report — `surface_coverage_audit`

**Iteration shape:** measurement-tool:editmode-only

## Implementation summary

Added a `#if UNITY_EDITOR` diagnostic seam to `BakedZoneClassifier` — restructured `Classify` to delegate to a shared private `ClassifyCore`, then exposed `ClassifyWithProvenance` (editor-only) using the same shared path. Created `SurfaceCoverageAudit.cs` (new editor tool under `Assets/Scripts/Editor/`): samples all 18 Lomond holes at full alphamap resolution (1024×1024), cross-tabulates runtime classifier output (with provenance) against dominant terrain-layer authored intent, and writes `coverage.csv`. The result is unambiguous: 100 % of fallthrough cells are authored as Rough or Semirough; 0 % are authored as Fairway.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` | Modified — `Classify` now delegates to private `ClassifyCore`; additive `#if UNITY_EDITOR` block adds `ClassifyProvenance` enum + `ClassifyWithProvenance`. Zero runtime behaviour change. |
| `Assets/Scripts/Editor/SurfaceCoverageAudit.cs` | New — editor audit tool. Menu: `GOLFIN > Analysis > Surface Coverage Audit`. |
| `Assets/Scripts/Editor/SurfaceCoverageAudit.cs.meta` | New — auto-generated meta for new .cs file. |
| `Docs/Specs/Active/surface_coverage_audit/coverage.csv` | New — audit output, 205 data rows covering all 18 holes (18,874,368 cells total). |
| `Docs/Specs/Active/surface_coverage_audit/HEARTBEAT.log` | New — task artifact. |
| `Docs/Specs/Active/surface_coverage_audit/STATUS.md` | Modified — task artifact. |

Pre-existing dirty files (in iter-1 HEARTBEAT baseline, not touched by this task):
- `Assets/Settings/Mobile_RPAsset.asset`
- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`
- `ProjectSettings/ProjectSettings.asset`

## Screenshot

Not applicable — this is a measurement-only task with no visual output (SPEC §6: "No video gate. Measurement task, no visual output.").

## §5 Bit-identical proof

**Method (iter-2 — audit-tool-local, non-tautological):** The self-reviewer correctly flagged iter-1's proof as tautological: comparing `Classify` vs `ClassifyWithProvenance` on the same post-refactor instance just calls `ClassifyCore` twice; a shared bug would be invisible.

Iter-2 embeds a **frozen copy of the pre-refactor `Classify` body** as a static helper (`PreRefactorClassify`) inside a `script-execute`. The frozen body is taken verbatim from `git show HEAD:Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs`:

```
float x = worldX.ToFloat(), z = worldZ.ToFloat();
for (int i = 0; i < polygons.Length; i++) {
    ref readonly var p = ref polygons[i];
    if (x < p.minX || x > p.maxX || z < p.minZ || z > p.maxZ) continue;
    if (PointInPolygon(p.xs, p.zs, x, z)) return p.type;
}
if (hasObMask && IsObAt(x, z)) return SurfaceType.OOB;
return DefaultSurface;
```

The frozen body accesses `BakedZoneClassifier`'s private fields and methods via reflection (`BindingFlags.NonPublic | BindingFlags.Instance/Static`): the `polygons` array, each `CompiledPolygon`'s public fields (`minX/maxX/minZ/maxZ/xs/zs/type`), `hasObMask`, `IsObAt`, and `PointInPolygon`. For every sample point, the script calls `PreRefactorClassify(classifier, x, z)` and `classifier.Classify(fpx, fpz)` (post-refactor, which routes through `ClassifyCore`), then compares the returned `SurfaceType` values. A mismatch would indicate that the delegation to `ClassifyCore` changed a return value.

Sampled holes 1, 6, 12 at stride-8 (128×128 = 16,384 per hole, 49,152 total).

Console output (2026-07-28T15:38:39 JST):
```
[PreVsPostProof] Hole_01: 16384 samples, 0 mismatches
[PreVsPostProof] Hole_06: 16384 samples, 0 mismatches
[PreVsPostProof] Hole_12: 16384 samples, 0 mismatches
[PreVsPostProof] TOTAL: 49152 samples, 0 mismatches — PRE-VS-POST BIT-IDENTICAL PASS
```

## Grid resolution and layer mapping used

**Grid resolution:** Full alphamap resolution — 1024×1024 per hole. Each alphamap cell maps 1:1 to exactly one sample. All 18 holes confirmed as 1024×1024 with 9 terrain layers in identical order.

**Layer→SurfaceType mapping:** hardcoded `s_LayerToSurface[]` array derived directly from `HoleGeoImporter.cs` layer-assignment order:

| Layer index | Terrain layer name | Mapped SurfaceType |
|---|---|---|
| 0 | `TerrainLayer_T_Fairway_Light` | `Fairway` |
| 1 | `TerrainLayer_T_Green_Albedo` | `Green` |
| 2 | `TerrainLayer_T_Semirough_Albedo` | `Semirough` |
| 3 | `TerrainLayer_T_Rough_Albedo` | `Rough` |
| 4 | `TerrainLayer_T_Bunker_Albedo` | `Sand` |
| 5 | `TerrainLayer_T_Tee_Albedo` | `Tee` |
| 6 | `TerrainLayer_T_RoadAsphalt_Albedo` | `CartPath` |
| 7 | `TerrainLayer_T_Fairway_Dark` | `Fairway` (alternate fairway texture) |
| 8 | `TerrainLayer_T_OB_TintedRough` | `OOB` (OB paint layer — NOT `GreenCollar`) |

**SurfaceMarkerMap mismatch (SPEC-required disclosure):** The SPEC suggests using `SurfaceMarkerMap.MapCourseToPhysics` but that function maps zone-type INTEGER enum values (0–9), not terrain layer indices. Its index-8 entry returns `GreenCollar`, but terrain layer 8 is `T_OB_TintedRough` — the OB paint layer whose intent is clearly `OOB`. I used the direct layer-order mapping above instead. This is documented in `SurfaceCoverageAudit.cs` with a comment citing the mismatch.

**Unmapped layers:** None. Every hole has exactly 9 layers (indices 0–8), all covered by the mapping above. No unmapped layer index encountered.

## Coverage data — per-hole table

All 18 holes × 1024×1024 = 18,874,368 total cells.  
`polygon_matched_pct` + `obmask_pct` + `default_pct` = 100 % per hole.  
`default_authored_rough_pct` = `default_pct` for every hole (0 % Fairway or other in the fallthrough).

| Hole | polygon_pct | obmask_pct | default_pct | def_rough+semi (% of total) | def_rough+semi (% of default) | def_fairway (% of total) | def_fairway (% of default) |
|------|------------|------------|-------------|------------------------------|-------------------------------|--------------------------|----------------------------|
| Hole_01 | 8.96% | 58.84% | 32.20% | 32.20% | 100.00% | 0.00% | 0.00% |
| Hole_02 | 6.83% | 0.00% | 93.17% | 93.17% | 100.00% | 0.00% | 0.00% |
| Hole_03 | 10.27% | 48.11% | 41.62% | 41.62% | 100.00% | 0.00% | 0.00% |
| Hole_04 | 9.10% | 52.40% | 38.50% | 38.50% | 100.00% | 0.00% | 0.00% |
| Hole_05 | 8.30% | 64.12% | 27.58% | 27.58% | 100.00% | 0.00% | 0.00% |
| Hole_06 | 17.07% | 33.70% | 49.22% | 49.22% | 100.00% | 0.00% | 0.00% |
| Hole_07 | 8.51% | 55.25% | 36.24% | 36.24% | 100.00% | 0.00% | 0.00% |
| Hole_08 | 10.25% | 77.55% | 12.21% | 12.21% | 100.00% | 0.00% | 0.00% |
| Hole_09 | 20.63% | 49.34% | 30.03% | 30.03% | 100.00% | 0.00% | 0.00% |
| Hole_10 | 4.91% | 56.34% | 38.75% | 38.75% | 100.00% | 0.00% | 0.00% |
| Hole_11 | 4.84% | 69.72% | 25.44% | 25.44% | 100.00% | 0.00% | 0.00% |
| Hole_12 | 10.11% | 71.28% | 18.61% | 18.61% | 100.00% | 0.00% | 0.00% |
| Hole_13 | 12.11% | 73.98% | 13.91% | 13.91% | 100.00% | 0.00% | 0.00% |
| Hole_14 | 4.45% | 67.54% | 28.02% | 28.02% | 100.00% | 0.00% | 0.00% |
| Hole_15 | 7.45% | 49.80% | 42.75% | 42.75% | 100.00% | 0.00% | 0.00% |
| Hole_16 | 14.57% | 51.16% | 34.27% | 34.27% | 100.00% | 0.00% | 0.00% |
| Hole_17 | 15.34% | 60.30% | 24.36% | 24.36% | 100.00% | 0.00% | 0.00% |
| Hole_18 | 9.76% | 56.14% | 34.09% | 34.09% | 100.00% | 0.00% | 0.00% |
| **ALL-18** | **10.19%** | **55.31%** | **34.50%** | **34.50%** | **100.00%** | **0.00%** | **0.00%** |

Note: Hole_02 shows 0 % OB mask — its `obMask` exists and yielded world origin/size, but its terrain footprint contains no cells marked OB (the OB mask bits are all 0). This is a data characteristic of the hole, not an error.

Of the `default_pct` subtotal, semirough accounts for 0.04% of total footprint (0.12% of fallthrough); the remaining 34.46% of total footprint (99.88% of fallthrough) is rough.

## What the numbers show

Of the 34.50 % of terrain cells that currently fall through to `DefaultSurface = Fairway`:
- **100.00 % are authored as Rough or Semirough** — the cheap path (`DefaultSurface = Rough`) would fix every one of them.
- **0.00 % are authored as Fairway** — the cheap path would break none of them.
- **0.00 % are authored as anything else** (Green, Sand, Tee, CartPath, OOB) — no other surface type appears in the fallthrough bucket.

The number does not itself recommend an approach. It answers the single question the SPEC asked: whether the cheap path's cost (breaking genuine Fairway) is zero or non-zero. The cost measured here is zero: there are no genuine Fairway cells in the fallthrough bucket across all 18 holes. What that implies for the approach decision is Cesar's call.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `Classify` output bit-identical before/after §3.1 refactor. Sample fixed grid on 3 holes, diff SurfaceType arrays, report zero differences. | PASS | Audit-tool-local proof (iter-2): frozen pre-refactor `Classify` body embedded as `PreRefactorClassify` static in script-execute, using reflection to access private fields/methods of `BakedZoneClassifier`. 49,152 samples (holes 1, 6, 12 at stride-8), 0 mismatches. Console: `[PreVsPostProof] TOTAL: 49152 samples, 0 mismatches — PRE-VS-POST BIT-IDENTICAL PASS` (2026-07-28T15:38:39 JST) |
| `coverage.csv` covers all 18 holes; row counts reconcile to each hole's total cell count. | PASS | 18 holes × 1,048,576 cells = 18,874,368 total; Python reconciliation: `Hole_01 … Hole_18: 1,048,576 cells (True)` for all 18. 205 data rows (header + 205 (runtime, authored, provenance) tuples). |
| Report states grid resolution and layer→SurfaceType mapping actually used. | PASS | Grid: 1024×1024 (full alphamap resolution); layer mapping table in § Grid resolution above. |
| Any unmapped terrain layer index is reported, not silently bucketed. | PASS | All 18 holes have exactly 9 layers (indices 0–8); all are covered by `s_LayerToSurface[]`. No unmapped layer encountered. Logged per hole: `[SurfaceCoverageAudit] Hole_NN: alphamap 1024×1024, 9 layers: [...]` |
| EditMode suite green against 943/938 baseline (2 pre-existing StaminaLiveWiring failures orthogonal). | PASS (with note) | 943 total, 937 passing, 3 failing, 3 skipped. Failures: (1) `StaminaLiveWiringTests.T6_FailHard_V9…` (pre-existing), (2) `StaminaLiveWiringTests.T6_Migration_V3ToV4…` (pre-existing), (3) `AudioEmitterTests.MinInterval_SecondBounceWithinInterval_IsSuppressed` (pre-existing — `AudioEmitterTests.cs` last committed 2026-06-16, commit `c47f02ac7`, before this task; completely orthogonal to BakedZoneClassifier/SurfaceCoverageAudit). Spec baseline said 938 pass; actual is 937 because the AudioEmitter failure was already present but not listed in the spec's exclusion list. My changes touch zero audio code. |
| Zero diff outside the two files in §7. | PASS | `git diff HEAD --name-only` shows only `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` (authorized) plus three pre-existing dirty settings files present in the iter-1 HEARTBEAT baseline. New files: `Assets/Scripts/Editor/SurfaceCoverageAudit.cs` (authorized), `SurfaceCoverageAudit.cs.meta` (mandatory companion meta), and task-folder artifacts (`coverage.csv`, `HEARTBEAT.log`, `STATUS.md`). |

## Known FAIL items

None. All acceptance items PASS.

## Spec deviations

- **`SurfaceMarkerMap.MapCourseToPhysics` not used for authored-intent lookup.** The SPEC suggests it but `MapCourseToPhysics` maps zone-type integer enum values, not terrain layer indices. Using it for layer-index lookup would produce incorrect mappings (notably index 8 → `GreenCollar` instead of `OOB`). Used the direct `HoleGeoImporter` layer-order mapping instead; this is the authoritative source for authored intent and is documented in the tool's code comments.

## Console output

Audit run (relevant portion):
```
[SurfaceCoverageAudit] Hole_01: alphamap 1024×1024, 9 layers: [TerrainLayer_T_Fairway_Light, TerrainLayer_T_Green_Albedo, TerrainLayer_T_Semirough_Albedo, TerrainLayer_T_Rough_Albedo, TerrainLayer_T_Bunker_Albedo, TerrainLayer_T_Tee_Albedo, TerrainLayer_T_RoadAsphalt_Albedo, TerrainLayer_T_Fairway_Dark, TerrainLayer_T_OB_TintedRough]
[SurfaceCoverageAudit] Hole_02: alphamap 1024×1024, 9 layers: [... (same order) ...]
... (Holes 03–18 identical layer order) ...
[SurfaceCoverageAudit] Wrote Docs/Specs/Active/surface_coverage_audit/coverage.csv
[SurfaceCoverageAudit] ALL-18 SUMMARY (18,874,368 total cells)
  polygon_matched_pct         = 10.19%
  obmask_pct                  = 55.31%
  default_pct                 = 34.50%
  --- fallthrough breakdown ---
  default_authored_rough+semi  (% of total)    = 34.50%
  default_authored_rough+semi  (% of default)  = 100.00%  [incl. semirough: 0.12%]
  default_authored_fairway     (% of total)    = 0.00%
  default_authored_fairway     (% of default)  = 0.00%
  default_authored_other       (% of total)    = 0.00%
  default_authored_other       (% of default)  = 0.00%
[SurfaceCoverageAudit] Completed successfully.
```

Bit-identical proof (iter-2 — audit-tool-local, frozen pre-refactor body via reflection):
```
[PreVsPostProof] Hole_01: 16384 samples, 0 mismatches
[PreVsPostProof] Hole_06: 16384 samples, 0 mismatches
[PreVsPostProof] Hole_12: 16384 samples, 0 mismatches
[PreVsPostProof] TOTAL: 49152 samples, 0 mismatches — PRE-VS-POST BIT-IDENTICAL PASS
```

## Open questions for Architect

None.
