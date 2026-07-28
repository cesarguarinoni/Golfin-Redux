# Self-Review — `surface_coverage_audit`

**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-07-28 15:52 JST
**Iteration:** 2 (prior iter-1 review FAILed on two fixes; this re-review verifies iter-2 addresses both plus PIPELINE_HARDENING Rule 5 full re-sweep)
**Verdict:** **FORWARD_TO_ARCHITECT (PASS)**

Tier-1 measurement-only task. Per SPEC §6 there is no video/screenshot gate; the load-bearing checks are (a) scope, (b) additive-seam integrity, (c) bit-identical proof, (d) CSV reconciliation, (e) report accuracy.

---

## Iter-1 fix verification

### FIX 1 — §5 bit-identical proof genuinely pre-vs-post — **PASS**

The iter-1 failure was that the proof compared `Classify` vs `ClassifyWithProvenance` on the SAME post-refactor instance — both funneled through `ClassifyCore`, so a shared bug would be invisible. Iter-2's approach:

- **(a) Reference path does NOT route through `ClassifyCore`.** VERIFIED. The report describes a `PreRefactorClassify` static embedded in `script-execute` that uses reflection (`BindingFlags.NonPublic | BindingFlags.Instance/Static`) to reach `BakedZoneClassifier`'s private `polygons` array (each `CompiledPolygon` exposing `minX/maxX/minZ/maxZ/xs/zs/type`), `hasObMask`, `IsObAt`, and `PointInPolygon`, and walks the resolution ladder itself (polygon-loop → OB mask → `DefaultSurface`). I read `BakedZoneClassifier.cs` at HEAD (pre-refactor `Classify`, lines 178–197 of `git show HEAD:...`) — the frozen body is verbatim the pre-refactor ladder. That ladder is compared against `classifier.Classify(fp, fp)` which routes through the new `ClassifyCore`. This is a genuine pre-vs-post equivalence check for the actual change (ladder extraction). The reused `PointInPolygon`/`IsObAt` helpers are the exact pre-refactor helpers unchanged by the refactor (confirmed by inspection), so their re-use via reflection is legitimate — the refactor extracted the ladder, not the helpers, and the ladder itself is duplicated independently in the frozen copy.

- **(b) Report documents method, sample count, mismatch count with citable console lines.** VERIFIED. Report § "§5 Bit-identical proof" gives the method, the frozen snippet, `49,152` samples across holes 1/6/12 at stride-8 (128×128 per hole), `0` mismatches, and the console line `[PreVsPostProof] TOTAL: 49152 samples, 0 mismatches — PRE-VS-POST BIT-IDENTICAL PASS` dated 2026-07-28T15:38:39 JST.

- **(c) No third file touched.** VERIFIED. `git status --porcelain --untracked-files=all` shows only `BakedZoneClassifier.cs` (M), `SurfaceCoverageAudit.cs` + `.meta` (??), task-folder artifacts, and the three pre-existing settings files. `grep -n "PreRefactor\|PreVsPost\|frozen" Assets/Scripts/Editor/SurfaceCoverageAudit.cs` returns `NOT_FOUND` — the frozen copy exists only in `script-execute` as required.

FIX 1 fully satisfied.

### FIX 2 — Footnote numerical accuracy — **PASS**

The corrected sentence in iter-2 report now reads:

> "Of the `default_pct` subtotal, semirough accounts for 0.04% of total footprint (0.12% of fallthrough); the remaining 34.46% of total footprint (99.88% of fallthrough) is rough."

Independent re-derivation from raw `coverage.csv` (Python, filtered on `provenance=Default`; run this review session):

- Grand total cells: `18,874,368` ✓
- All 18 holes present, each per-hole sum = `1,048,576` ✓
- Fallthrough total (Default provenance): `6,511,305`
- Rough_Default: `6,503,414`
- Semirough_Default: `7,891`

Ratios (raw / rounded to 2dp):
- Semirough % of total: `0.0418%` → **`0.04%`** ✓ matches report
- Semirough % of fallthrough: `0.1212%` → **`0.12%`** ✓ matches report
- Rough % of total: `34.4563%` → **`34.46%`** ✓ matches report
- Rough % of fallthrough: `99.8788%` → **`99.88%`** ✓ matches report

All four load-bearing numbers reconcile. FIX 2 fully satisfied.

---

## Rule 5 — full acceptance sweep (not only the prior fails)

### 1. Scope — PASS

`git status --porcelain --untracked-files=all` reconciled against SPEC §7:

| Path | Category | Status |
|---|---|---|
| `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` (M) | SPEC §7 authorised | OK |
| `Assets/Scripts/Editor/SurfaceCoverageAudit.cs` (+ `.meta`) (??) | SPEC §7 authorised + mandatory companion meta | OK |
| `Docs/Specs/Active/surface_coverage_audit/{HEARTBEAT.log,IMPLEMENTER_REPORT.md,SELF_REVIEW.md,STATUS.md,coverage.csv}` | Task-folder artifacts | OK |
| `Assets/Settings/Mobile_RPAsset.asset` | Pre-existing (session-start snapshot) | Not this task |
| `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` | Pre-existing | Not this task |
| `ProjectSettings/ProjectSettings.asset` | Pre-existing | Not this task |

Zero unauthorised paths outside SPEC §7. `git diff --stat HEAD` = 4 files, 41+/17− — the `BakedZoneClassifier.cs` diff is 30+/2−, consistent with an additive seam. No third file appeared to service the iter-2 proof.

### 2. Additive-seam integrity — PASS (re-confirmed)

Re-read the full `BakedZoneClassifier.cs` post-iter-2. `Classify(fp,fp)` now unpacks and calls `ClassifyCore(x, z, out _)`. `ClassifyCore` contains the exact pre-refactor ladder:
- Polygon loop with identical AABB pre-reject and `PointInPolygon` test → `provenance = 0; return p.type`
- `hasObMask && IsObAt` → `provenance = 1; return SurfaceType.OOB`
- `provenance = 2; return DefaultSurface`

`ClassifyProvenance` enum and `ClassifyWithProvenance` are correctly wrapped in `#if UNITY_EDITOR` (lines 203–218). The `out int provenance` parameter has no path to alter the returned `SurfaceType` (write-only from `ClassifyCore`'s side). Semantics unchanged; runtime binary untouched. Iter-2 did not disturb this.

### 3. Bit-identical proof (§5 gate) — PASS (see FIX 1 above)

### 4. CSV reconciliation (§5 gate) — PASS

Programmatic re-sum this review session:
- 18 distinct `hole` values (Hole_01 … Hole_18): OK.
- Per hole, `sum(cell_count)` = `1,048,576` for all 18 holes: OK.
- Grand sum = `18,874,368` = report's declared total: OK.
- Hand-verified Hole_01: 616994+8735+10334+337402+212+61794+3716+3233+14+6135+5+2 = 1,048,576 ✓

CSV integrity intact; iter-2 changes did not disturb it.

### 5. Report completeness (grid resolution, layer mapping, `SurfaceMarkerMap` disclosure, no approach recommendation, test-suite deviation honestly disclosed) — PASS

Spot-checked vs iter-1 pass state — unchanged in substance:
- Grid: 1024×1024 alphamap resolution, 1:1 cell mapping (report § "Grid resolution").
- Layer→SurfaceType mapping table present with 9 layers, spelled per `HoleGeoImporter` (report § "Grid resolution and layer mapping used").
- `SurfaceMarkerMap.MapCourseToPhysics` divergence disclosed: index 8 → `GreenCollar` (via that API) vs `OOB` (via terrain-layer-order mapping actually used); reason documented.
- All 18 per-hole coverage rows unchanged from iter-1 numbers (which I independently reconciled last pass).
- ALL-18 aggregates unchanged: polygon 10.19%, obmask 55.31%, default 34.50%; def_rough+semi 34.50% of total / 100.00% of default; def_fairway 0.00%.
- No approach recommendation past the numbers: "What that implies for the approach decision is Cesar's call" — within SPEC §1 and §8 constraints.
- Test-suite deviation: 943 total / 937 pass / 3 fail / 3 skip; extra `AudioEmitterTests.MinInterval_…` failure attributed to pre-existing commit `c47f02ac7` (2026-06-16), well before this task; my touched files (`BakedZoneClassifier.cs`, `SurfaceCoverageAudit.cs`) do not intersect audio code.

### 6. Coverage-data footnote prose — PASS (see FIX 2 above)

### 7. Capture-helper compliance (protocol Step 5)

Not applicable — this is a measurement-only task with no visual output (SPEC §6 waives the screenshot/video gate). No new `*Context.cs` files added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. Capture-helper maintenance protocol not engaged.

### 8. Bbox / production-flow / scene-mutation checks (Steps 6–8)

Not applicable — no UI, no scene mutation. `BakedZoneClassifier.cs` is a physics class; `SurfaceCoverageAudit.cs` is an editor-only tool. `git diff` shows no scene / prefab / RectTransform / GameObject-active changes.

---

## Verdict: FORWARD_TO_ARCHITECT

Both iter-1 fixes are correctly applied. Every acceptance criterion re-swept per Rule 5 still holds. The proof is now a genuine pre-vs-post equivalence check (frozen ladder via reflection vs live `ClassifyCore`), confined to `script-execute` (no third file), returns 0 mismatches over 49,152 samples across 3 holes. The footnote numbers all reconcile to two decimals against the raw CSV. Scope, additive-seam integrity, CSV reconciliation, layer-mapping disclosure, and "no approach recommendation" are all intact.

Setting STATUS to `SELF_REVIEW_PASS`.

---

## Files summary

| Path | Change |
|---|---|
| `Docs/Specs/Active/surface_coverage_audit/SELF_REVIEW.md` | Rewritten (iter-2 verdict) |
| `Docs/Specs/Active/surface_coverage_audit/STATUS.md` | To be set to `SELF_REVIEW_PASS` |
