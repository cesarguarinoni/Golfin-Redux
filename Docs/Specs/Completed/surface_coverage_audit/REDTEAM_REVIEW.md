# Red-Team Review — `surface_coverage_audit`

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-07-28 15:58 JST
**Iteration under review:** 2
**Verdict:** `ARCHITECT_REVIEW_FAIL`

The prior reviewer's PASS is not carried forward. Every acceptance item was re-derived
from primary source. Three of the four attack axes came up clean; the fourth — the
validity of the authored-intent axis — is a hard, primary-source-proven blocker that
invalidates the entire deliverable.

---

## BLOCKER — the authored-intent axis cannot detect Fairway, so the headline number is a foregone conclusion, not a measurement

### What the deliverable claims
> "0.00% of fallthrough cells are authored as Fairway → the cheap path (`DefaultSurface = Rough`) breaks none of them."

The SPEC exists (SPEC §1) to answer exactly one question: of the ground that falls
through to `DefaultSurface`, how much is *genuine fairway* (cheap path BREAKS) vs
rough (cheap path FIXES). The leaky-coverage failure mode is a cell that should read
as fairway but sits outside any Fairway polygon → falls through → cheap path flips it
to rough. To detect that, "authored intent" must capture the fairway surface extent.

### Why the number is an artifact (three independent primary-source proofs)

**1. Raw alphamap dominant-layer histogram (I ran it myself, read-only, via Unity
`script-execute` on `TerrainData_Hole01Geo`):**

```
RAW ALPHAMAP DOMINANT HIST Hole_01 1024x1024
  L0 T_Fairway_Light     = 0
  L1 T_Green_Albedo      = 0
  L2 T_Semirough_Albedo  = 233
  L3 T_Rough_Albedo      = 422614
  L4 T_Bunker_Albedo     = 0
  L5 T_Tee_Albedo        = 0
  L6 T_RoadAsphalt_Albedo= 0
  L7 T_Fairway_Dark      = 0
  L8 T_OB_TintedRough    = 625729
```
Only Semirough, Rough, and OB are ever the dominant terrain layer. Fairway, Green,
Sand, Tee, CartPath dominate **0 cells** — so the audit's authored axis can *never*
output Fairway. "0.00% authored fairway" is guaranteed regardless of coverage quality.

**2. Cross-tab of the delivered `coverage.csv` (re-summed from raw):** of the
**1,923,673** cells the runtime classifier resolves via a *feature polygon*
(Fairway/Green/Tee/Sand/CartPath/Water), **0 (0.000%)** have a matching authored
surface. Every one of the 854,131 cells inside a **Fairway polygon** is labelled
authored=Rough. If the authored axis were valid, the vast majority of fairway-polygon
cells would be authored Fairway. They are 100% Rough. Across all 18 holes the only
authored surfaces that ever appear are {OOB, Rough, Semirough}.

**3. The shipping importer's own code says so.** `HoleGeoImporter.ZoneToLayer`
(lines 1614-1630) deliberately collapses feature zones to the rough splat because
overlay meshes render the surface:
```
1  => 3,  // fairway → rough (mesh overlay handles surface)
2  => 3,  // green → rough (mesh handles surface)
6  => 3,  // bunker → rough (mesh handles sand surface)
8  => 3,  // cart_path → rough (mesh overlay handles surface)
10 => 3,  // tee_box → rough (mesh overlay handles surface)
```
For Geo holes the terrain alphamap is an intentionally rough/semirough/OB base under
overlay geometry. It does not, by design, encode authored fairway/green/etc.

### Consequence
The audit measured the deliberately-rough splat base, not authored surface intent.
The `default_authored_rough = 34.50% / 100%` result is tautological (cells outside
feature polygons sit over the rough base — of course), and `default_authored_fairway
= 0.00%` would be reported **even if fairway polygon coverage were catastrophically
leaky**. The number has zero diagnostic power for the SPEC's question, and the report
(IMPLEMENTER_REPORT "What the numbers show") presents it as a definitive finding
("breaks none of them") without surfacing the anomaly. SPEC §8 and the "derive from
primary source; do not confirm an artifact that asserts it" doctrine required the
implementer to catch that a 0%-fairway-everywhere authored axis (including under
fairway polygons) is invalid, and to surface it — not report it as the answer.

This is precisely the highest-stakes failure mode the red-team brief named: *"if the
audit mapped a layer wrong such that authored-Fairway cells were mislabeled as Rough
… that is the highest-stakes failure mode."* Here the collapse is worse than a
mis-map — the source data physically cannot represent fairway.

### Fix instruction
1. **Source authored intent from data that actually encodes feature surfaces for Geo
   holes.** Candidates: the upstream **satellite/zone raster grid** (`grid` /
   `terrain_grid` in the export `zones.json` the importer consumed — where 1=fairway,
   2=green, 6=bunker, 10=tee, *before* `ZoneToLayer` collapses them), applying the Geo
   Y-flip (`gy = (1-fy)*(zoneH-1)`), mapped via the zone-index→SurfaceType table; **or**
   rasterize the feature overlay-mesh footprints (GreenSurface / bunker / fairway /
   cartpath / tee meshes). The terrain alphamap is the wrong source — do not use it.
2. **Add a mandatory self-consistency gate the current run fails 100%:** for cells the
   runtime resolves via a feature polygon, authored must equal runtime for the large
   majority. A ~0% self-match (current state) must **hard-fail the audit** and block
   any fallthrough number from being reported.
3. Re-derive the two bolded decision numbers from the corrected authored axis.
4. If the export zone raster genuinely wasn't retained for Geo holes and no valid
   authored source can be reconstructed, **do not accept the alphamap number** —
   set `IMPLEMENTER_BLOCKED` and surface to Cesar (SPEC §3.3-NOTE + "surface, don't
   rebuild"), because the SPEC's prescribed method (§3.3 "dominant alphamap layer")
   is invalid for Geo data and the measurement approach needs a decision.

---

## Attack axes that came up CLEAN (genuine break-attempts that failed)

### 1. Scope (SPEC §7) — clean
`git status --porcelain` + `git diff --stat HEAD`: outside the task folder only
`BakedZoneClassifier.cs` (M, +30/-2, §7-authorised), `SurfaceCoverageAudit.cs` (+`.meta`,
§7-authorised). The three settings assets (`Mobile_RPAsset.asset`,
`UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings.asset`) are present in
**both** the iter-1 and iter-2 `=== kickoff baseline ===` blocks in HEARTBEAT.log →
genuine pre-existing drift, not introduced here. (Note: the prior reviewer
mischaracterised the `ProjectSettings.asset` diff as "prefilter keywords" — it is
actually an added iPhone static-batching build target + a preloaded asset — but since
it predates the task, scope is not violated.)

### 2. Additive `#if UNITY_EDITOR` seam — clean and bit-identical
`git diff HEAD -- BakedZoneClassifier.cs`: `Classify` now calls `ClassifyCore(...,
out _)`; `ClassifyCore` carries the identical ladder (same AABB pre-reject, same
`PointInPolygon`, same `hasObMask && IsObAt` branch, same `return DefaultSurface`),
adding only three `provenance = 0/1/2` write-only assignments. The enum +
`ClassifyWithProvenance` are inside `#if UNITY_EDITOR`; `ClassifyCore` is plain C# with
no editor dependency, so the runtime/player path is unaffected (no editor-only-seam
player-build risk). Verified verbatim against `git show HEAD:...` lines 178-197.

### 3. §5 bit-identical proof — genuine, not tautological
The frozen `PreRefactorClassify` body (embedded in `script-execute`, reached by
reflection into private `polygons`/`hasObMask`/`IsObAt`/`PointInPolygon`) is a verbatim
copy of the pre-refactor `Classify` and does NOT route through `ClassifyCore`; it is
diffed against live `Classify` (which does). 49,152 samples, 0 mismatches. `grep`
confirms no committed source carries the frozen copy. This proof is sound — but note it
only proves the *refactor is behaviour-neutral*, which is true; it says nothing about
the validity of the audit's authored axis (the actual defect).

### 4. CSV reconciliation & arithmetic — clean (but on an invalid axis)
Independent re-sum of `coverage.csv` (utf-8-sig, 205 rows): all 18 holes reconcile to
exactly 1,048,576; grand total 18,874,368 = 18×1024². polygon=10.1920%, obmask=55.3099%,
default=34.4981%; def_fairway 0.0000%/0.0000%; def_rough+semi 34.4981%/100.0000%;
semirough 0.0418%/0.1212%; rough-only 34.4563%/99.8788%. Every number matches the report
to ≥4 dp, and every per-hole `def_fair`/`def_other` is 0 (no total-masking surprise).
The arithmetic is correct; the axis it operates on is not.

### 5. Layer→SurfaceType mapping honesty — the map itself is honest
Verified against `HoleGeoImporter` lines 1476-1484: the audit's `s_LayerToSurface[]`
matches the importer layer order, and both fairway textures (0, 7) map to Fairway — so
the map does **not** mislabel fairway texture as rough. The disclosed
`SurfaceMarkerMap.MapCourseToPhysics` divergence (zone-int vs layer-index; index-8
GreenCollar vs OB) is accurate. The problem is not the map — it is that the *input*
(dominant alphamap layer) never contains a feature layer to map.

---

## Summary
Scope, additive seam, bit-identical proof, and CSV arithmetic all pass. The task fails
on validity: the authored-intent measurement is derived from the terrain alphamap, which
the shipping Geo importer intentionally paints rough under overlay meshes, so it cannot
detect authored Fairway/Green/Sand/Tee/CartPath at all. The deliverable's decision number
("0% authored fairway in fallthrough") is a structural artifact with no power to detect
the leaky-coverage case the SPEC was built to measure. Route back to the implementer to
re-source authored intent from the zone raster / overlay-mesh footprints, add a
polygon self-match consistency gate, and re-derive — or surface `IMPLEMENTER_BLOCKED`
if no valid authored source exists for Geo holes.

## Files summary
| Path | Change |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/surface_coverage_audit/REDTEAM_REVIEW.md` | Written (FAIL verdict, iter-2) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/surface_coverage_audit/STATUS.md` | Set to `ARCHITECT_REVIEW_FAIL` |
