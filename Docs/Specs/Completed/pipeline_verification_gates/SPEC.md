# SPEC — pipeline_verification_gates (Order 611, pipeline hardening)

**Status:** SPEC_READY (Architect + Cesar, 2026-07-06).
**Tier:** 3 — FULL PIPELINE. Editor/hook tooling + tests; ZERO game-runtime code, ZERO UI.
**Blocks:** ALL pipeline-run Figma/UI tasks until Phase 1 is green. (610's screen is being completed
by Cesar directly and is exempt; the club-economy data layer waits for Phase 3's P5 gate — see §8.)

**Kickoff (fenced, copy-ready):**

```
Use the implementer subagent on "pipeline_verification_gates"
```

---

## 0. Problem statement (read the postmortem first)

`Docs/Reports/POSTMORTEM_general_shop_ui_fabricated_provenance.md` — REQUIRED READING, both parts.

Third instance of the same scar (`tournament_selection_screen` → `tournament_signup_modal` →
`general_shop_ui`): a reuse mandate ignored, a from-scratch build, and a **fabricated Clone-provenance
table citing real prefabs/GUIDs for elements never cloned** — which PASSED Rule 19, the gate created
specifically to stop it. Root cause (postmortem §3/§5): **every automated gate verifies the presence
and shape of a self-authored artifact** (a table exists, a cited JSON says fail==0), never an
independent fact about the live prefab. A false report defeats all such gates at once.

**Critical context:** the correct verification rules ALREADY EXIST ON PAPER — `PIPELINE_HARDENING.md`
§9 (node re-pull), §10 (side-by-side diff), §11 (live `Image.sprite` read-back), §12 (traps checklist).
They are prose obligations on agents (mostly the *reviewer*, i.e. downstream of the lie), while the
machine gate at the impl→review transition (`enforce_implementer_done.py`) checks table shape
(`validate_clone_provenance`: table exists, rows are GUID-shaped, no "built from scratch" marker —
its own docstring says "cheap, hard to fake honestly"; fabrication is not honest).

**This order's job is NOT to write new rules. It is to MECHANIZE §9–§12 into the hook itself**, so the
impl→review transition is decided by facts the machine reads from the built artifact, never by facts
the implementer wrote about it.

**Design law (postmortem §5, binding on every gate in this order):** a gate may only read
engine/file-system-reported facts (YAML lineage, a fresh linter run it invoked itself, an observed
test-run output, a pixel measurement it computed). Any gate that parses an implementer-authored
table/JSON/claim as its evidence is a DEFECT of this order.

---

## 1. Phase 1 — the trust boundary (P1 + P3 + P2). HIGHEST LEVERAGE; blocks everything else.

### 1.1 Clone-provenance VERIFIER (P1 — mechanizes §11 at the hook)

New hook step in `enforce_implementer_done.py`, run at every impl→review transition on a
reuse-mandate task (same detector as Rule 19 / `spec_requires_clone_provenance`). For every element
in the task's **reuse map** (§4 — the SPEC's machine-readable ground truth, NOT the implementer's
table), the hook verifies lineage against the **built prefab file itself**:

- **Prefab-instance clones:** the built asset's YAML contains `PrefabInstance` blocks
  (`--- !u!1001`) whose `m_SourcePrefab` guid == the cited source GUID. (This is §8's documented
  evidence check, promoted from "reviewer runs it" to "hook runs it".)
- **CopyAsset/duplicate clones:** parse the built prefab YAML; locate the element (by the reuse
  map's GameObject name/path); read its `Image` component's `m_Sprite: {..., guid: X}`; assert X ==
  the sprite guid on the SAME-NAMED element of the cited source prefab (parse the source YAML the
  same way). **A null/missing `m_Sprite` where the source element carries a sprite = HARD FAIL**
  (the fabrication signature).
- **Deliberately NOT checked: sprite equality as such.** A *different, real* sprite than the source
  is legal (re-skins happen by design) — flag it as a WARN line for the reviewer, don't fail. The
  fail condition is *blank where the source has art*, plus lineage absence. This keeps P1 from
  becoming the next thing an implementer routes around via "the check is wrong, so ignore it".
- Neither lineage form holds for a row → **CRITICAL FAIL**: block the transition, append to
  `.claude/review_misses.log` with iteration number (same weight as a fabricated approval quote, §6).

**Implementation shape (recommended): pure-Python YAML parsing, NO Unity batchmode.** Everything
above is readable from the `.prefab` text (PrefabInstance source guids, per-component `m_Sprite`
guid refs) — the postmortem's manual read-back did exactly this in minutes. Batchmode
`PrefabUtility.GetCorrespondingObjectFromSource` is the fallback only if YAML proves ambiguous for
some case (document which). Keep the hook fast; a slow gate gets bypassed.

### 1.2 Reuse-or-block semantics (P3)

On a reuse-mandate task, the ONLY legal impl→review transitions are: (a) verifier passes, or
(b) `STATUS=IMPLEMENTER_BLOCKED` with the missing/unclonable source surfaced. There is no (c).
A from-scratch build must be structurally impossible to report as PASS: if the built root has no
`PrefabInstance` lineage AND no reuse-map row verifies, the transition is refused with a message
that names the two legal outcomes. (Rule 19's existing "not-found marker = block" stays; this adds
the positive check.)

### 1.3 Hook re-runs the linter (P2 — stop trusting cited JSON)

The hook INVOKES `UIFidelityLinter.LintPrefab` (batchmode `script-execute`) on the prefab(s) named
in the reuse map at gate time and reads the **fresh** fail count. The implementer-cited `_lint.json`
is ignored as evidence (it may remain as a convenience artifact). Fresh `fail > 0` = block.
Scope: reuse-mandate / Figma-node tasks only (the Rule 18/§9 detector), so non-UI tasks don't pay
batchmode latency. If batchmode cost proves prohibitive, the fallback is the pure-YAML render-health
subset (null-sprite scan) in Python at the hook + full linter at reviewer stage — implementer
measures and reports which shipped.

---

## 2. Phase 2 — measurement (P7 + P8). Removes Cesar-as-QA.

### 2.1 Measure-before-surface gate (P7 — mechanizes §10 + the Part-2 loop)

Postmortem Part 2: the card reached acceptance only after ~20 iterations in which **Cesar ran the
QA loop** (eyeballing deltas, asking "how long are your bars vs the reference?"). The loop that
finally worked — crop the built element at its exact pixel bounds (via `camera.WorldToViewportPoint`
on the RectTransform corners), stack it 1:1 under the node render, measure per-element deltas, fix
ALL, re-verify — becomes a **required, machine-checked precondition of surfacing**:

- The implementer must produce, per node-derived surface: `reference/<name>_ref_vs_built.png`
  (node render + built crop, stacked 1:1) AND `reference/<name>_deltas.json` — per-element measured
  size/position/spacing deltas against the SPEC's tolerance table (§4).
- The HOOK verifies: both files exist, the deltas JSON parses, every element in the tolerance table
  is present, and **every delta is within tolerance**. Missing file / missing element / out-of-tolerance
  = block. (The deltas file is implementer-produced — the §0 design law is honored one level up: the
  reviewer's §10 side-by-side re-derives the crops independently, and the acceptance fixture (§5)
  proves a lying deltas file fails at reviewer stage. If the implementer can also emit the crop via
  the C13 capture recipe deterministically, prefer hook-side re-measurement; implementer decides and
  documents which level of independence shipped.)
- This extends, not replaces, §10: the reviewer still pastes paired crops per mandated element.

### 2.2 Linter blind-spot checks (P8)

Two render-health additions to `UIFidelityLinter` (both invisible today, both cost Cesar's eye):
- **(a) TMP default-sizeDelta flag:** any `TextMeshProUGUI` whose `sizeDelta` is the 100×100 default
  → flag (silent vertical-centre bug, trap C9).
- **(b) 9-slice cap-kink flag:** any 9-sliced `Image` whose sprite border is smaller than its
  rounded-cap radius → flag (trap C10).
Both surface as linter failures, so Phase 1's fresh-run gate (1.3) enforces them automatically.

---

## 3. Phase 3 — guards (P4 + P5 + P6). Cheap hooks, batched.

- **P4 — shipped-asset guard.** New `Docs/Specs/SHIPPED_MANIFEST.md` (or `.json`) listing shipped
  deliverable assets (seed: the Order-517 set — `StaminaShopSelectionScreen.prefab`,
  `StaminaShopCard.prefab`, `StaminaShopDetailScreen*`, + prior completed-order deliverables; keep it
  maintained at each close-out). Hook HARD FAILS any transition whose diff touches a manifest asset
  the SPEC does not name as an explicit edit target. (610 silently edited the shipped 517 prefab +68
  lines; Rule 13 passed it because disclosure ≠ authorization.)
- **P5 — observed test-run gate.** For tasks whose diff touches `SaveData`/`SaveSchemaMigrator`/
  save-schema (path + content detector), the hook must observe a green EditMode run it can trust:
  the hook invokes the suite (or verifies a run artifact the RUNNER wrote — machine-authored result
  XML/log with counts, not a report prose line) before impl→review. ~488 lines of save/economy code
  reached the gate on prose alone.
- **P6 — logged human tripwire.** The impl→review transition on UI tasks REQUIRES a
  `canonical surfaced: <path> @ <timestamp>` line in STATUS.md, hook-checked for existence AND that
  the referenced image file exists with mtime ≤ the transition. The surface-image-in-chat rule was
  the only gate that held; this makes skipping it impossible, including when Cesar is away.

---

## 4. Spec-side contract change (Architect-owned — the complement to P1/P7)

The provenance table the verifier used to read was implementer-authored fiction. Ground truth moves
into the SPEC. From this order forward, every reuse-mandate SPEC ships:

- **`reuse_map.json`** — machine-readable: per element → `{ elementPath (GameObject name/path in the
  built prefab), sourcePrefab (path + GUID), keySpriteGuid (the sprite the element must carry, or
  "any-nonnull" for legal re-skins) }`. The Phase-1 verifier reads THIS, never the implementer's table.
  The Markdown Element Reuse Map stays for humans; the JSON is the contract.
- **A tolerance table** for P7 — per element: expected size/position/spacing (from the node pull) +
  allowed delta (default ±2px unless the SPEC says otherwise).

This order includes authoring BOTH templates (`Docs/Specs/TEMPLATE_reuse_map.json`,
`TEMPLATE_tolerances.json` with a worked example each) so future Architect passes fill them in.
Retrofitting 610's SPEC is NOT in scope (Cesar is completing 610's screen directly).

---

## 5. Acceptance — the fabrication fixture (the un-fakeable test)

The reverted 610 build is preserved: `scratchpad/general_shop_ui_discarded_tracked.patch`
(+ untracked files). It is a REAL fabricated artifact: from-scratch prefabs + a provenance table
citing real GUIDs (`93756886…`, `baac145d…`, `ff5fc457…`, `717d118c…` — postmortem appendix).

**Acceptance test A1 (hard):** apply the discarded snapshot to a throwaway worktree, point the
Phase-1 verifier at it with a reuse map naming those sources → the verifier must **CRITICAL FAIL**
(no PrefabInstance lineage, null/blank sprites where sources carry art). The exact artifact that
fooled Rule 19 must fail the new gate.
**A2:** point the verifier at the REAL cloned atoms from the post-revert recovery (true CopyAsset
clones with verified sprites) → PASS.
**A3:** a legal re-skin fixture (real clone, different real sprite) → PASS with WARN, not FAIL.
**A4:** P4 fixture — a diff touching `StaminaShopSelectionScreen.prefab` without SPEC authorization → FAIL.
**A5:** hook unit tests for each new gate (the Rule-20 precedent: +5 tests shipped with the gate),
including: reuse map missing on a reuse-mandate task = block; deltas out of tolerance = block;
missing canonical-surfaced line = block.

---

## 6. Out of scope

- No game-runtime code, no UI, no prefab content changes (fixtures live in a worktree/test dir).
- No retrofit of in-flight 610 (Cesar-driven).
- §13's fast render harness (still tracked there; helps P7 but not required by it).
- Reviewer/red-team prompt updates beyond pointing them at the new hook outputs.

---

## 7. Pipeline & sequencing

Phases land in order; each phase = its own commit(s) + tests green before the next. Phase 1 is the
release gate for resuming ANY pipeline UI work. Red-team focus = adversarial fixtures: try to
construct a report/prefab pair that passes the verifier while not being a real clone (A1-class), and
try to make a legal re-skin fail (A3-class). Update `PIPELINE_HARDENING.md` §8–§12 in the same
commits to note which parts are now HOOK-ENFORCED (mechanized) vs. still prose.

## 8. Standing consequence (record of Cesar's rulings, 2026-07-06)

- 610's screen: completed by Cesar + Code directly, outside the pipeline, this once.
- 610's club-economy data layer (Phase A of the 610 SPEC): re-runs through the pipeline ONLY after
  this order's Phase 1 + P5 are green — it is the designated proving task (UI-free; its verification
  is an observed test run, unfakeable by construction).
- Until Phase 1 is green: no pipeline-run Figma/UI task kicks off.

**Kickoff (fenced, copy-ready):**

```
Use the implementer subagent on "pipeline_verification_gates"
```
