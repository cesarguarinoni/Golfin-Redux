# ARCHITECT ESCALATION — `surface_coverage_audit`

**Raised:** 2026-07-28 (orchestrator, after red-team FAIL iter-2)
**STATUS:** `ARCHITECT_REVIEW_ESCALATE`
**Cesar's call:** "Rethink the question" — reconsider the measurement framing before more implementer effort.
**Trigger:** red-team `REDTEAM_REVIEW.md` — the audit's authored-intent axis is structurally invalid on Geo holes.

---

## 1. The defect (verified from primary source, not relayed)

SPEC §3.2 step 3 sourced "authored intent" from the **dominant terrain alphamap layer**. On a Geo hole that splat **cannot encode Fairway**. Confirmed by reading the shipping importer:

`HoleGeoImporter.ZoneToLayer` (`Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs:1614-1630`) collapses the splat to a deliberately-rough base under overlay meshes:

```
1  fairway   → 3 rough   ("mesh overlay handles surface")
2  green     → 3 rough
3  semi_rough→ 2 semirough
4  rough     → 3 rough
6  bunker    → 3 rough   ("mesh handles sand surface")
8  cart_path → 3 rough   ("mesh overlay handles surface")
10 tee_box   → 3 rough   ("mesh overlay handles surface")
```

Only `semi_rough`(2) and `rough`(3)/OB(8) ever appear in the alphamap. Therefore:

- `default_authored_fairway = 0.00%` was a **foregone conclusion**, not a measurement. It would read 0.00% even under catastrophically leaky fairway coverage — zero diagnostic power for the exact failure mode the task exists to detect.
- Corroboration in the delivered `coverage.csv`: **all 854,131 cells inside Fairway polygons are labelled authored=Rough**. Authored-fairway-under-a-fairway-polygon reading as rough is a five-alarm invalidity signal the audit should have halted on.

The rest of the work is sound (scope clean, additive seam behaviour-neutral, iter-2 bit-identical proof genuine, CSV arithmetic exact). It is **correct arithmetic on an invalid axis.**

---

## 2. The reframe — is the question even answerable from baked artifacts?

**Yes, at zero re-bake cost.** The correct oracle is on disk, inside the very file the audit already opens.

Each hole's `zones.json` carries the **pre-collapse zone raster** as a base64 field, with `source_dimensions` for W/H — the same source `HoleGeoImporter` decodes at import:

- `HoleGeoImporter.cs:1301-1305` — `zonesData.terrain_grid` (fallback `zonesData.grid`), `Convert.FromBase64String`, dims from `zonesData.source_dimensions`.
- Zone indices are the true authored surfaces **before** `ZoneToLayer` flattens them: `1=fairway, 2=green, 3=semi_rough, 4=rough, 6=bunker, 8=cart_path, 10=tee_box`.
- Geo Y-flip when sampling raster→world: `gy = round((1-fy)*(zoneH-1))` (`:1327`, `:1359`).

The audit read the wrong field — it went downstream to the alphamap (post-collapse) instead of decoding the raster in the JSON it already loaded. **No re-bake, no bake-pipeline change** — this stays inside the SPEC's "measurement only, no re-bake" envelope.

---

## 3. The judgment call for the Architect (why this is an escalation, not an auto-loop)

There are **two** rasters in `zones.json`, and which one is "authored fairway intent" is a real decision, not a mechanical one:

| Field | Meaning | Effect on the measurement |
|---|---|---|
| `grid` (merged) | feature zones painted in — what the fairway/green/etc. **polygons were derived from** | fallthrough-cell zone==1 ⇒ a fairway cell the polygonization **missed** → **this is the SPEC's actual "leaky coverage" question** |
| `terrain_grid` | "preserves real terrain under overlays" (rough base beneath overlay meshes) | would again under-report fairway — same trap as the alphamap |

So the audit almost certainly wants the **merged `grid`**, not `terrain_grid`. But note the circularity to reason about: if the merged `grid` and the Fairway polygons derive from the *same* source with the *same* footprint, then fallthrough-vs-grid measures only polygonization/rasterization slop, which may be near-zero by construction — in which case the "leaky vs tight" question is really "how lossy is the polygonizer," and the honest answer might be "coverage is tight because both come from one source," which *supports* the cheap path but for a different reason than the SPEC assumed.

**Decisions needed from the Architect before re-kick:**
1. Confirm the authored-intent oracle: merged `grid` (recommended) vs `terrain_grid` vs overlay-mesh footprints.
2. Add a **mandatory self-consistency gate** to the SPEC: authored==runtime for the majority of feature-polygon cells must PASS before any fallthrough number is trusted (the gate that would have caught this: 0% agreement under fairway polygons = audit invalid).
3. Decide whether the reframed number (polygonization leak, not splat authoring) actually answers `surface_classification_ob_rough`'s cheap-vs-Option-2 decision, or whether that downstream task needs its decision criteria rewritten.

---

## 4. Recommended next step

Architect revises SPEC §3.2 (authored-intent source + self-consistency gate) and §5 (add the gate as an acceptance item), then a clean implementer iteration runs against the corrected SPEC. The audit tool + additive seam from iter-1/2 are reusable as-is; only the authored-intent sampling changes.

**What stays true:** measurement-only, no re-bake, two-file scope (the raster decode lives in the existing `SurfaceCoverageAudit.cs`).
