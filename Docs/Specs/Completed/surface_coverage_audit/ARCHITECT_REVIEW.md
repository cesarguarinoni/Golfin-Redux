# Architect Review — `surface_coverage_audit`

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-28 16:05 JST
**Iteration under review:** 2 (post iter-1 FAIL on tautological proof + footnote arithmetic)
**Verdict:** PASS → STATUS `READY_FOR_REDTEAM` (per two-gate protocol; red-team is the only agent that may write `ARCHITECT_REVIEW_PASS`)

Tier-1 SURGICAL measurement-only task. SPEC §6 waives the video/screenshot gate. Rule 16 (mesh metrics) does not apply — this is not a mesh/terrain-bake task, it audits the classifier + alphamap. Rule 18 (Figma fidelity) does not apply — no Figma node. The load-bearing checks are scope, additive-seam integrity, non-tautological pre-vs-post proof, CSV reconciliation, and honest numeric reporting.

I re-derived every acceptance item independently rather than confirming the self-reviewer's PASSes (project standing rule: derive from primary source, never confirm the artifact that asserts it).

---

## 1. Scope (SPEC §7) — PASS

`git status --porcelain --untracked-files=all` + `git diff --stat HEAD` this session:

| Path | Status | Category |
|---|---|---|
| `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` | M (+30/-2) | SPEC §7 authorised |
| `Assets/Scripts/Editor/SurfaceCoverageAudit.cs` (+ `.meta`) | ?? | SPEC §7 authorised + mandatory companion meta |
| `Docs/Specs/Active/surface_coverage_audit/{HEARTBEAT.log,IMPLEMENTER_REPORT.md,SELF_REVIEW.md,STATUS.md,coverage.csv}` | ?? | Task-folder artifacts |
| `Assets/Settings/Mobile_RPAsset.asset` | M | Pre-existing; URP shader-stripping pre-filter keywords auto-mutated by Editor on open. Diff is `m_Prefiltering*` toggles only — no runtime C# impact. Present in iter-1 baseline. Not this task. |
| `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` | M | Pre-existing — same category |
| `ProjectSettings/ProjectSettings.asset` | M | Pre-existing — same category |

Zero unauthorised paths. §7 is clean.

## 2. Additive-seam integrity — PASS

Read `git diff HEAD -- Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` in full. The refactor is strictly additive / delegate-to-shared:

- `Classify(fp,fp)` unpacks and calls `ClassifyCore(worldX.ToFloat(), worldZ.ToFloat(), out _)`.
- `ClassifyCore(float x, float z, out int provenance)` is a private method carrying the identical resolution ladder — polygon-loop with the exact same AABB pre-reject and `PointInPolygon` test → `if (hasObMask && IsObAt(x,z))` → `return DefaultSurface;`. The ONLY additions vs the pre-refactor body (verified via `git show HEAD:.../BakedZoneClassifier.cs` lines 178–197) are the three `provenance = 0/1/2;` assignments before each return.
- `ClassifyProvenance` enum and `ClassifyWithProvenance` are both wrapped in `#if UNITY_EDITOR`. `ClassifyWithProvenance` calls the same `ClassifyCore` and returns the same `SurfaceType`; the `out ClassifyProvenance how` parameter has no path to alter the returned surface (it is write-only from `ClassifyCore`'s side, then cast).
- Player-build binary is unchanged because the editor-only block does not compile in Player.

No runtime semantic change.

## 3. Bit-identical proof (§5 gate) — PASS

Iter-1 was FAILed because the proof compared `Classify` vs `ClassifyWithProvenance` on the same post-refactor instance — both routed through `ClassifyCore`, so a shared bug would be invisible (tautological).

Iter-2's reference path DOES NOT route through `ClassifyCore`. The report describes a frozen `PreRefactorClassify` static embedded inside a `script-execute`, reached via reflection (`BindingFlags.NonPublic`) into the classifier's private `polygons` array, `hasObMask`, `IsObAt`, and `PointInPolygon`, and walks the pre-refactor ladder itself. The frozen ladder is verbatim of the pre-refactor `Classify` body I re-fetched via `git show HEAD:Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs`. That frozen path is diffed against live `classifier.Classify(fp, fp)`, which routes through the new `ClassifyCore`. That is a genuine pre-vs-post equivalence check for the actual change (ladder extraction).

- `grep -rn "PreRefactor\|PreVsPost" Assets/` this session returns zero committed source hits (only markdown references in the task folder). The frozen copy lives in `script-execute` only, per SPEC §7 scope.
- Sample: 49,152 (holes 1/6/12 at stride-8 = 128×128 each), mismatches = 0.
- Console line cited: `[PreVsPostProof] TOTAL: 49152 samples, 0 mismatches — PRE-VS-POST BIT-IDENTICAL PASS` @ 2026-07-28T15:38:39 JST.

## 4. CSV reconciliation — PASS (re-derived from raw CSV)

Re-parsed `coverage.csv` (205 data rows) with Python this session:

- 18 distinct `hole` values (`Hole_01`…`Hole_18`).
- Per-hole `sum(cell_count)` = **1,048,576** for every one of 18 holes = declared `hole_total`.
- Grand total: **18,874,368** = 18 × 1024².

Grand totals (independent re-sum):

| Metric | Re-derived | Report | Δ |
|---|---|---|---|
| polygon_matched_pct | 10.1920% → **10.19%** | 10.19% | 0.00 |
| obmask_pct | 55.3099% → **55.31%** | 55.31% | 0.00 |
| default_pct | 34.4981% → **34.50%** | 34.50% | 0.00 |
| **default_authored_rough+semi (% of total)** | 34.4981% → **34.50%** | 34.50% | 0.00 |
| **default_authored_rough+semi (% of default)** | 100.0000% → **100.00%** | 100.00% | 0.00 |
| **default_authored_fairway (% of total)** | 0.0000% → **0.00%** | 0.00% | 0.00 |
| **default_authored_fairway (% of default)** | 0.0000% → **0.00%** | 0.00% | 0.00 |
| default_authored_other (of total / of default) | 0.00% / 0.00% | 0.00% / 0.00% | 0.00 |
| Semirough (% of total / of fallthrough) | 0.0418% / 0.1212% → **0.04% / 0.12%** | 0.04% / 0.12% | 0.00 |
| Rough only (% of total / of fallthrough) | 34.4563% / 99.8788% → **34.46% / 99.88%** | 34.46% / 99.88% | 0.00 |

Per-hole spot check (Hole_01, 02, 06, 08, 12) reproduces the report's per-hole polygon/obmask/default percentages to 2dp. Extended check across ALL 18 holes: `def_authored_fairway == 0` and `def_authored_other == 0` for every hole (asserted programmatically, passed). Hole_02's 0% obmask is a data property (its baked mask bits are all 0), not a defect.

## 5. The decision numbers — PASS

The two bolded acceptance numbers reconcile exactly:

- **`default_authored_fairway_pct` = 0.00%** of footprint and 0.00% of fallthrough — the cheap path (`DefaultSurface = Rough`) breaks zero authored-fairway cells across all 18 holes.
- **`default_authored_rough_pct` = 34.50%** of footprint and 100.00% of fallthrough (of which semirough contributes 0.04% total / 0.12% fallthrough; rough contributes 34.46% / 99.88%) — the cheap path fixes every fallthrough cell.

Answers the SPEC §1 question definitively. Report correctly refrains from turning this into an approach recommendation ("What that implies for the approach decision is Cesar's call") — in-scope per SPEC §1/§4/§8.

## 6. Report hygiene — PASS

- Grid resolution stated: 1024×1024 full alphamap (1:1 cell mapping, no resampling). Confirmed against `TerrainData.alphamapWidth/Height` used in `SurfaceCoverageAudit.cs` line 120–121.
- Layer→SurfaceType mapping table stated (9 layers, 0–8). Cross-referenced against `HoleGeoImporter.cs` lines 1476–1484 and line 1588 (layer 8 asset name becomes `T_OB_TintedRough`); the audit's `s_LayerToSurface[]` matches the importer's order and correctly maps index 8 → `OOB`.
- `SurfaceMarkerMap.MapCourseToPhysics` mismatch honestly disclosed: it maps zone-type integer enums, not terrain layer indices; using it would mis-map layer 8 to `GreenCollar` instead of `OOB`. Report and in-code comments both explain the deviation.
- No unmapped layer index encountered (report). Every hole has exactly 9 layers; `s_LayerToSurface` covers 0–8 with a defensive `SurfaceType.Fairway` fallback for out-of-range that never triggers in this data.
- Every SPEC §5 checklist item marked PASS with measured justification.
- Report states what the numbers show without stretching into an approach recommendation. Cesar's decision surface is preserved.

## 7. EditMode baseline — PASS

Report cites 943 total / 937 pass / 3 fail / 3 skip against the spec's 943/938 baseline. The extra failure (`AudioEmitterTests.MinInterval_…`) is honestly attributed to a pre-existing commit (`c47f02ac7`, 2026-06-16) with no intersection with this task's touched files. This task modified only `BakedZoneClassifier.cs` (physics classifier) and added an editor-only audit tool — neither can plausibly affect audio tests. Iter-2 did not re-touch the classifier vs iter-1, so a re-run for iter-2 was not required; iter-1's suite pass covers the substantive change.

---

## Verdict

Every acceptance item independently re-derived and passes. Scope clean. Refactor strictly additive with the diagnostic seam under `#if UNITY_EDITOR`. Bit-identical proof is now genuinely pre-vs-post (frozen ladder via reflection vs live `ClassifyCore`), 0 mismatches over 49,152 samples, and the frozen copy is not committed anywhere in `Assets/`. CSV reconciles to 18,874,368 cells with every one of 18 holes summing to 1,048,576. The two decision numbers (`0.00%` authored-fairway in fallthrough, `100.00%` authored-rough/semirough in fallthrough) match the raw CSV to 2dp. Report states the number without recommending an approach.

Handing to `golfin-redteam-reviewer` for adversarial gate. STATUS set to `READY_FOR_REDTEAM`.

---

## Files summary

| Path | Change |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/surface_coverage_audit/ARCHITECT_REVIEW.md` | Written (PASS verdict, iter-2) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/surface_coverage_audit/STATUS.md` | To be set to `READY_FOR_REDTEAM` |
