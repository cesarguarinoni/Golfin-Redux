# SPEC — `surface_coverage_audit`

**Order:** (Notion, P2, Gameplay Polish)
**Tier:** 1 — SURGICAL. Measurement only.
**Blocks:** `surface_classification_ob_rough` — that task cannot pick an approach until this produces a number.
**Scope:** One editor-only audit tool + one editor-only diagnostic seam. **No runtime behaviour change. No bake change. No re-bake. No CSV/asmdef/scene touch.**

---

## 1. The one question this answers

`surface_classification_ob_rough` has two candidate fixes and no basis to choose between them:

- **Cheap path** — `DefaultSurface = Rough` (1 line) + out-of-grid→`OOB` (~5 lines). No bake change, no re-bake.
- **Option 2** — replace the OB-only `obMask` with a full per-cell surface grid baked from the alphamap. Bake change + re-bake all 18 holes + full physics suite re-run.

Both hinge on a single unknown:

> **Of the ground that currently falls through to `DefaultSurface`, how much is authored as Rough/Semirough (which the cheap path FIXES) versus authored as Fairway (which the cheap path BREAKS)?**

Tight Fairway polygon coverage ⇒ the cheap path gets most of Option 2's benefit for a fraction of the work. Leaky coverage ⇒ flipping the default would make genuine fairway play as rough, and Option 2 earns its cost.

**Produce the number. Do not recommend an approach beyond what the number supports, and do not implement either fix in this task.**

---

## 2. Background — verified, do not re-derive

Confirmed against primary sources during the `ob_boundary_presentation` close-out. Treat as given:

- `BakedZoneClassifier.Classify(x,z)` resolves: **polygon zones** (priority-sorted, first match wins) → **OB mask** (`if (hasObMask && IsObAt(x,z)) return OOB;`) → **`return DefaultSurface;`**
- `public const SurfaceType DefaultSurface = SurfaceType.Fairway;`
- `IsObAt` returns `false` for any point outside the mask grid, and the mask grid == the terrain footprint.
- All 18 `zones.json` contain **only** `Fairway / Green / Tee / Sand / CartPath / Water` polygon groups. **Zero** `Rough`, `Semirough`, or `OOB` polygons.
- Nothing samples the terrain alphamap at play time. The single `GetAlphamaps` call in the codebase is `BakeZoneJsonTool.cs:353-359` (Editor, OB-layer-scoped).
- The runtime `obMask` is baked by **`BakeZoneJsonTool`**, not `HoleGeoImporter`.
- `BakedZoneClassifier`'s own class XML doc says the chain ends `"... > CartPath > Fairway > Rough (default)"` — the **documented** default is `Rough`, the code says `Fairway`. `Priority()` already ranks `Semirough: 20`, `Rough: 10`, `OOB: 5`.

---

## 3. What to build

### 3.1 Diagnostic seam (additive, editor-only)

`Classify` cannot distinguish "returned Fairway because a Fairway polygon matched" from "returned Fairway because nothing matched." The audit needs that distinction, and it **must** come from the real classifier — a re-implemented point-in-polygon test in the audit tool could diverge and silently invalidate the whole measurement.

Add to `BakedZoneClassifier`, wrapped in `#if UNITY_EDITOR`:

```csharp
public enum ClassifyProvenance { Polygon, ObMask, Default }
public SurfaceType ClassifyWithProvenance(fp worldX, fp worldZ, out ClassifyProvenance how)
```

Implement by **restructuring `Classify` to delegate**, so both share one code path — do not copy the resolution ladder. `Classify`'s behaviour must be bit-identical after the change; that is a §5 gate.

### 3.2 Audit tool (new, editor-only)

New file under `Assets/Scripts/Editor/` (namespace and folder to match the existing editor-tool convention there — check neighbours before choosing). Menu item under the existing `GOLFIN` menu.

For **each of the 18 Lomond holes**:

1. Load the hole's `zones.json` + terrain (same path `PhysicsLabController.TryLoadBakedProviders` uses — reuse it, don't hand-roll loading).
2. Sample a uniform grid over the terrain footprint. **Match the alphamap resolution** so each sample maps 1:1 to an alphamap cell — no resampling error. Report the grid dimensions used.
3. Per cell, record two values:
   - **Runtime answer:** `ClassifyWithProvenance(x, z, out how)`
   - **Authored intent:** dominant alphamap layer at that cell → `SurfaceType`, via `SurfaceMarkerMap.MapCourseToPhysics`.
     > **NOTE:** verify the layer→type mapping directly in `SurfaceMarkerMap` before relying on it. Report the mapping you used. If any terrain layer index has no mapping, report it rather than guessing.
4. Cross-tabulate.

### 3.3 Output

Two artifacts in this spec folder:

**`coverage.csv`** — one row per hole per (runtime, authored, provenance) combination, with cell counts and the hole's total cell count. Raw, so the numbers can be re-cut without re-running.

**In `IMPLEMENTER_REPORT.md`**, a per-hole table plus an all-18 total:

| Metric | Meaning |
|---|---|
| `polygon_matched_pct` | resolved by a polygon — unaffected by either fix |
| `obmask_pct` | resolved by the OB mask — unaffected |
| **`default_authored_rough_pct`** | fell through to default, alphamap says Rough/Semirough → **the cheap path FIXES these** |
| **`default_authored_fairway_pct`** | fell through to default, alphamap says Fairway → **the cheap path BREAKS these** |
| `default_authored_other_pct` | fell through to default, alphamap says something else — break out by type |

The two bolded rows are the decision. Report both as a percentage of total footprint cells **and** as a percentage of fallthrough cells only, since those answer different questions.

---

## 4. Non-goals

- Implementing either fix. **This task changes no runtime behaviour.**
- Touching the bake pipeline, `zones.json`, or any hole data.
- Re-baking anything.
- Tuning coefficients or writing a `PHYSICS_TUNING_CHANGELOG.md` entry — nothing changes yet.
- Off-course / beyond-footprint analysis. Defect A's fix (out-of-grid→`OOB`) is not in question; only the in-bounds default is.
- Semirough-vs-Rough discrimination as a *recommendation*. Count them separately, but the cheap path collapses both to `Rough` and that trade-off is Cesar's call, not this task's.

---

## 5. Acceptance

- [ ] `Classify` output is **bit-identical** before and after the §3.1 refactor. Prove it: sample a fixed grid on 3 holes pre- and post-change, diff the resulting `SurfaceType` arrays, report zero differences.
- [ ] `coverage.csv` covers all 18 holes; row counts reconcile to each hole's total cell count.
- [ ] Report states the grid resolution and the layer→`SurfaceType` mapping actually used.
- [ ] Any unmapped terrain layer index is reported, not silently bucketed.
- [ ] EditMode suite green against the 943/938 baseline (2 pre-existing `StaminaLiveWiring` failures are orthogonal — leave them).
- [ ] Zero diff outside the two files in §7.

---

## 6. No video gate

Measurement task, no visual output. Numbers and the CSV are the deliverable.

---

## 7. Files touched (expected)

| File | Change |
|---|---|
| `Assets/Scripts/Editor/…/SurfaceCoverageAudit.cs` | **new** — editor-only audit tool + `GOLFIN` menu item |
| `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` | additive `#if UNITY_EDITOR` provenance seam; `Classify` delegates to shared path |

Anything beyond these two — **stop and report before proceeding.**

---

## 8. Report

`IMPLEMENTER_REPORT.md` must contain the §3.3 tables, the §5 bit-identical proof, the grid resolution, the layer mapping used, and anything that did not go to plan.

State what the numbers show. **Do not stretch them into a recommendation they don't support** — if the result is ambiguous (e.g. the two bolded percentages are close), say so plainly and let the ambiguity stand.

**Derive from the primary source; do not confirm an artifact that asserts it.**
