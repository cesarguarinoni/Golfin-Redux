# SPEC — `zone_bake_scope_probe`

**Tier:** 1 — SURGICAL. **DIAGNOSTIC ONLY. READ-ONLY. NO FIX.**
**Blocks:** `zone_bake_completeness` — the repair spec cannot be sized until this returns.
**Context:** `Docs/Specs/Queued/zone_bake_completeness/FINDINGS.md`
**Prior art:** the Hole 14 probe (2026-07-28) that confirmed FINDINGS §5. Same shape, wider scope, plus a control.

---

## 1. Why this exists

The Hole 14 probe confirmed the defect on **one hole**. Holes 02, 12 and 15 are inferred from the same `zones.json` signature and have **never been probed**. Sizing a repair off an inference is the exact pattern that has already cost this task two bad premises today (my invalid SPEC oracle, and the escalation's wrong fix location). Confirm the scope, then build.

This probe also cheaply discriminates the drop mechanism (§4), which the repair spec needs and which is currently three ranked hypotheses with zero observations.

---

## 2. The control is mandatory — read this before writing any code

Every hole in §3 is *expected* to return `Fairway` via `Default`. If the probe is broken — wrong scene, wrong classifier instance, unloaded zones — **it also returns `Fairway` via `Default` for everything**, because that is the fallback. A uniform "all confirmed" result is therefore indistinguishable from total probe failure.

**So the run MUST include a positive control that is expected to SUCCEED:**

> **Hole 01 `Greens/Green_1` centroid → expect `Green` via `Polygon`.**

Hole 01's `zones.json` contains both Fairway and Green groups.

**If the control does not return `Green` via `Polygon`, the entire run is INVALID. Report that and stop — do not report the other holes' results as findings.** A null result without a working positive control is worthless; that lesson is already logged twice in this task's history.

---

## 3. Part A — classification probe

For each row, resolve the named GameObject in that hole's **build** scene (`Assets/Golf/Courses/lomond-country-club/Generated/Hole_NN_Geo.unity` — **not** the `Video/` variant), take its mesh centroid, and call the existing `#if UNITY_EDITOR` `ClassifyWithProvenance` seam on `BakedZoneClassifier`.

| Hole | Mesh | Missing from `zones.json` | Expectation |
|---|---|---|---|
| **01** | `Greens/Green_1` | — (**CONTROL**) | `Green` via `Polygon` |
| **02** | `Greens/Green_1` | Green | `Fairway` via `Default` |
| **12** | `Greens/Green_1` | Green | `Fairway` via `Default` |
| **15** | `Fairways/Fairway_1` | Fairway | `Fairway` via `Default` |
| **14** | `Greens/Green_1` | Green + Fairway | `Fairway` via `Default` (re-confirm) |

Report per row: **hole, mesh, world (x, z), returned `SurfaceType`, `ClassifyProvenance`**.

**Do not massage a surprise into the expected answer.** Any row that deviates is the most valuable output of this task — report it prominently. In particular `OOB` on any row is a *different and worse* defect.

Centroid caveat: a vertex-mean centroid can land outside a concave mesh. If any row returns `Polygon` provenance for an unexpected type, or if a mesh is visibly concave, also sample a point you are confident is interior and report both. (For rows returning `Default`, this is moot — nothing matched, so any interior point gives the same answer.)

---

## 4. Part B — mechanism discriminator (same scene loads, no extra cost)

FINDINGS §4 ranks three hypotheses for why the bake drops these types. This separates them **without running the bake**.

For every `Fairway_*` and `Green_*` GameObject in holes **01, 02, 12, 14, 15**, report:

| Column | Why |
|---|---|
| hole, object path | identity |
| has `MeshFilter`? | `BakeZoneJsonTool:175` requires one — **absent ⇒ H2** |
| `sharedMesh` null? vertex count | a null or degenerate mesh also fails the gate |
| has `Golfin.Physics.Runtime.SurfaceMarker`? | `:182` requires it |
| that marker's `Type` value | **wrong value ⇒ H3** |
| has `Golfin.Course.SurfaceMarker`? | its `surfaceType` value, for the enum-divergence question |

**Reading the result:**
- Components all present and correct on Hole 14's `Fairway_1`, yet Fairway absent from `zones.json` ⇒ **H1** (silent boundary-loop rejection at `BakeZoneJsonTool:278`/`:284`) survives as the only standing explanation.
- `MeshFilter` missing ⇒ **H2**.
- Marker `Type` set to something other than the expected surface ⇒ **H3**.
- Hole 01 (control) should show all components present and correct — that is what a working hole looks like.

State which hypothesis the evidence supports. **If it supports none, say so and stop** — do not invent a fourth and act on it.

---

## 5. Constraints

- **Read-only. No fix. No file writes.** Change nothing in `zones.json`, the bake, the classifier, or any scene.
- **Do not edit `BakedZoneClassifier` or any other source file** — the `ClassifyWithProvenance` seam already exists from `surface_coverage_audit`.
- No asset-refresh that would trigger a domain reload mid-probe.
- Exit any play mode entered; unload every scene opened; reload from disk / discard so **no scene is left dirty**. Editor clean at the end, per the leave-editor-clean rule.
- If a named GameObject does not exist in a hole's scene, **report that as a finding** — do not substitute a similarly-named object.

---

## 6. Report

**In the reply to Cesar. Do NOT write it into any repo file.**

1. Control row first, with an explicit VALID / INVALID verdict on the run.
2. Part A table.
3. Part B table + the hypothesis verdict.
4. Anything that did not go to plan, stated plainly.
5. Explicit confirmation that the probe wrote nothing and the editor was left clean.

**Derive from the primary source; do not confirm an artifact that asserts it.**
