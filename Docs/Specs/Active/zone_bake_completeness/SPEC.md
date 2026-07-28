# SPEC — `zone_bake_completeness`

**Order:** Notion 1245, **P1 — High**
**Tier:** 3 — FULL PIPELINE (bake pipeline change + data regeneration across 18 holes + gameplay-affecting)
**Findings:** `Docs/Specs/Queued/zone_bake_completeness/FINDINGS.md` — read §4a first, it corrects §4/§5.
**Confirmed by:** Hole 14 probe (2026-07-28) + `zone_bake_scope_probe` (2026-07-28, control passed).

---

## 1. The defect, stated correctly

`BakeZoneJsonTool` silently drops entire surface types from `zones.json`. Confirmed on **4 of 18 holes**:

| Hole | Dropped | Observed classification | Direction of failure |
|---|---|---|---|
| 02 | Green | green → `Fairway` via **Polygon** | green plays fairway |
| 12 | Green | green → `Fairway` via **Polygon** | green plays fairway |
| 14 | Fairway **and** Green | green → `Fairway` via **Default** | both surfaces gone |
| 15 | Fairway | fairway → **`Green`** via **Polygon** | **fairway plays green** |

**The critical framing correction:** this is **not** a clean fallthrough to `DefaultSurface`. A dropped polygon leaves its region to be claimed by whatever *surviving* polygon happens to overlap it. `Default` occurs only when **every** covering polygon was dropped (Hole 14). Everywhere else the result is a **silent, location-dependent wrong-polygon match** — two points on the same green can resolve differently.

Any acceptance test written as "expect `Default`" is wrong. Test for **the correct surface**, not for the fallback.

### 1.1 Blast radius beyond coefficients

- `BallSimulation.cs:758` — `IsPuttSurface(s) => s == Green || s == GreenCollar`. On 02/12/14 the putt integrator **never engages** on the green; on 15 it **wrongly engages on the fairway**.
- `BotDriver.cs:728-732`, `VersusBot.cs:496-501` — bots chip with a wedge unless the surface is Green/GreenCollar. On 02/12/14 a bot on the green **chips instead of putting**; on 15 a bot on the fairway **putts**.
- Coefficients: Green `0.12`/`0.05` vs Fairway `0.18`/`0.10`.
- Existing Notion row `C.4 — Putter blocked when ball is off green` suggests the **player's** putter may be gated the same way. **UNCONFIRMED — verify in Stage 1 and report.** If true, the player cannot putt on 02/12/14's greens.

---

## 2. Mechanism — confirmed by elimination, NOT by instrumentation

`zone_bake_scope_probe` established that every `Fairway_*`/`Green_*` object on the affected holes has a `MeshFilter`, a non-null mesh, and **both** `SurfaceMarker`s stamped correctly — identical to the passing Hole 01 control. So:

- **H2 (missing `MeshFilter`) — ELIMINATED**
- **H3 (mis-stamped marker `Type`) — ELIMINATED**
- **H1 (silent boundary-loop rejection at `BakeZoneJsonTool:278`/`:284`, `if (loopVerts.Count < 3) continue;`) — only standing explanation**

**Nobody has watched that branch fire.** Why loop extraction fails on these meshes is unknown. Stage 1 exists because patching an inferred branch is how today's two bad premises happened.

---

## 3. Stage 1 — instrument, do not fix

Add temporary instrumentation to `BakeZoneJsonTool` and run the bake on **holes 01 (control), 02, 12, 14, 15**. Do not change extraction logic yet.

Capture per candidate mesh:
- hole, object path, marker `Type`
- vertex/triangle count, submesh count, bounds
- **the actual `loopVerts.Count`** at `:278` and `:284`, and which branch was taken
- whether the mesh reached those lines at all, or exited earlier (e.g. the `:175` gate)
- for Hole 01's `Green_1` (control): the same numbers for a mesh that **succeeds**

**Answer explicitly:**
1. Does `loopVerts.Count < 3` actually fire on the dropped meshes? (If not — H1 is dead too, **stop and report**; we have no standing hypothesis and must not guess a fourth.)
2. What is structurally different between Hole 01's `Green_1` (succeeds) and Hole 15's `Fairway_1` (dropped)? Non-manifold edges? Disconnected shells? Zero-area triangles? Duplicate verts defeating edge pairing?
3. Also confirm the player-side putter gate (§1.1) while the scenes are open.

Report before writing any fix. **Stage 2 is gated on Stage 1's answer.**

---

## 4. Stage 2 — fix (shape depends on Stage 1)

Two required components regardless of root cause.

### 4.1 Fix the extraction failure

Repair whatever Stage 1 identifies. Prefer a fix inside the loop-extraction routine over pre-processing meshes at import — the meshes are correct; the extractor is not.

**Do not** simply lower the `< 3` threshold. That branch is a legitimate guard against degenerate output; the bug is that valid meshes are reaching it.

### 4.2 Fail loudly — non-negotiable

The reason this shipped is that a bake which drops a whole surface still writes a **perfectly valid-looking `zones.json`**.

Add a completeness gate: after building the groups and before writing, compare the surface types present in the hole's **source raster** (`Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/zones.json`, field `grid`, legend in `zone_index`) against the types that survived into `zones.json`.

- Any type present in the source with a meaningful cell count but **absent** from the output ⇒ **fail the bake for that hole with an explicit error naming the hole and the missing type**. Do not write the file.
- Small-count types below a stated threshold may warn instead of fail — **state the threshold and justify it in the report** (e.g. Hole 6's `semi_rough` is 575 cells / 0.03%, and treating that as a hard failure would be noise).

> **NOTE:** the source-raster tree lives **outside `Assets/`** and may not be present on every machine. If it is unavailable, the gate must **skip with a clear warning**, never silently pass. Flag this coupling in the report — it is a real dependency and Cesar should know the bake now reads from `Tools/`.

### 4.3 Re-bake and verify

Re-bake **all 18 holes** and diff every `zones.json` against HEAD. Expect changes on 02/12/14/15; **any change on the other 14 holes must be explained**, not waved through.

---

## 5. Acceptance

- [ ] Stage 1 report answers all three questions in §3, with the control comparison.
- [ ] All 18 holes: every surface type present in the source raster (above the §4.2 threshold) is present in the baked `zones.json`.
- [ ] Re-probe the 5 scope-probe holes via `ClassifyWithProvenance`. **Test for the correct surface, not for `Default`:**
  - Hole 01 `Green_1` → `Green`/`Polygon` (control, unchanged)
  - Hole 02 `Green_1` → **`Green`**/`Polygon`
  - Hole 12 `Green_1` → **`Green`**/`Polygon`
  - Hole 14 `Green_1` → **`Green`**/`Polygon`; Hole 14 `Fairway_1` → **`Fairway`**/`Polygon`
  - Hole 15 `Fairway_1` → **`Fairway`**/`Polygon` (was `Green` — the inverted case)
- [ ] Deliberately break one mesh's extraction in a scratch run and confirm the §4.2 gate **fails the bake** rather than writing a file. A gate never observed failing is not a gate.
- [ ] The 14 unaffected holes: `zones.json` byte-identical, or every difference explained.
- [ ] EditMode suite green against the 943/938 baseline (2 pre-existing `StaminaLiveWiring` failures are orthogonal).

---

## 6. Video gate

Real play (`screenshot-game-view` MCP tool / real-user flow — hand-rolled `script-execute` captures are hard-blocked by `.claude/hooks/enforce_capture_tool.py`).

Both failure directions, before and after:
1. **Hole 14** — putt on the green. BEFORE: fairway physics, ball stops short; bot chips from the green. AFTER: correct putt behaviour.
2. **Hole 15** — shot landing on the fairway. BEFORE: rolls too far on green coefficients; bot putts from the fairway. AFTER: correct fairway behaviour.

Clip 2 matters most — it is the inverted case and the one a fairway-only test would miss.

---

## 7. Non-goals

- `surface_classification_ob_rough` in either form. Defect A (out-of-grid → `OOB`) and the Rough/Semirough question stay in that order.
- **Do not implement `DefaultSurface = Rough`.** It is disproven — see FINDINGS §4a and that order's notes.
- The dual `SurfaceMarker` cleanup (FINDINGS §2). Real debt, separate task — the probe confirmed both markers are stamped correctly, so it is not causing *this* bug.
- Option 2's per-cell surface grid. It would supersede the polygon path entirely, but it is larger, needs product sign-off, and this bug is live now. **See §8.**
- Coefficient tuning. No `PHYSICS_TUNING_CHANGELOG.md` entry — this restores intended classification, it does not change any surface's numbers.

---

## 8. Sequencing note for Cesar — flagged, not decided

If `surface_classification_ob_rough` eventually lands Option 2 (per-cell surface grid from the source raster), the polygon path this task repairs would be **bypassed entirely**, making §4.1 throwaway work.

Fixing the bake first is still recommended: the bug is live, Option 2 is unscoped and needs a difficulty-rebalance sign-off, and §4.2's completeness gate stays valuable under either architecture. But the overlap is real and Cesar should be aware of it rather than discover it later.

---

## 9. Report

`IMPLEMENTER_REPORT.md`: Stage 1 instrumentation output incl. the control comparison; the structural difference found; the §4.2 threshold and its justification; the source-raster availability caveat; the all-18 re-bake diff with explanations for any unexpected hole; §5 re-probe table; proof the gate fails when it should; test counts; video links.

**Derive from the primary source; do not confirm an artifact that asserts it.**
