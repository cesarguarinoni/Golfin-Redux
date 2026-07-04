DONE

# STATUS — figma_node_spec_generator

**State:** `DONE` — Cesar approved 2026-07-04 (7-iter chain; red-team ARCHITECT_REVIEW_PASS, class structurally closed). Moved Active → Completed.
**Prior:** `ARCHITECT_REVIEW_PASS` (iter-7 golfin-redteam-reviewer 2026-07-04 08:45 CEST).
The adversarial gate that RAISED and FAILED this class at iter-4 confirms it is structurally
DEAD. Re-ran everything independently: my own hostile 23-type sweep through the real
`parse_figma_dumps → element_to_spec` output path (instance/component/component-set + every
shape primitive + image/boolean-op/slice + arbitrary unknown/future strings) → all emit
`requireSprite=True + WARN`, ZERO silent false-negatives; the styleless-join-hit edge proven
unreachable for a visible sprite on real Figma output (RP-Icon is metadata-only in the real
fixture → D6-covered). No over-reach (frame/group/section/text stay False). Non-hollow proven
by my OWN widen-back patch (3 new tests FAIL under iter-6 wide set; tool restored byte-identical).
Anchor spec regen BYTE-IDENTICAL; `fail:0` re-run LIVE via Unity MCP on the SHIPPED
`StaminaMenuRow.prefab` (fresh _capture JSON + Editor.log PASS); 183 tests pass; scope clean.
Whole-tool sweep clean (border/radius/gradient/font all faithful; one class-order color
fragility noted as non-blocking follow-up — safe-direction, non-load-bearing, unreproduced on
real input). Advances to Cesar's final approval gate.

**State (was):** `READY_FOR_ARCHITECT_REVIEW` (iter-7 golfin-self-reviewer PASS 2026-07-04 JST).
The terminal 3-LOC narrow of `_NON_GRAPHIC_CONTAINER_TYPES` from 7 → 4 types
(`{frame, group, section, text}`) structurally closes the reachable
`requireSprite=False` silent false-negative class. Verified end-to-end against the
shipping script: every plausible node type (instance/component/component-set + all
shape primitives + image/boolean-op/slice + any unknown/future type) fires
`_metadata_only_shape=True` + `requireSprite=True` + D6 WARN when mapped with
empty JSX vis; the 4 excluded layout containers remain negative (no over-trigger).
183 tests PASS. Anchor spec regen BYTE_IDENTICAL. `fail:0` on SHIPPED
`StaminaMenuRow.prefab` (Assets/ empty). 3 new tests confirmed non-hollow via
in-process patch to the iter-6 wide set. No fourth slip-through remains. Ready for
golfin-reviewer.

**State (was):** `SELF_REVIEW_FAIL` (iter-6 golfin-self-reviewer 2026-07-04 JST). Attacks 1, 2, 4
all hold (180 tests pass; byte-identical anchor spec regen; `fail:0` on SHIPPED prefab
independently confirmed; scope clean; non-hollow tests with overfit-guard). But Attack 3
reopens the same reachable `requireSprite:false` silent false-negative class the red-team
FAILED at iter-4 — through a NEW type: `instance` / `component` / `component-set`. The
iter-6 exclude-list `{frame, group, section, text, instance, component, component-set}`
treats Figma component INSTANCES as definitionally container-only, but instances are the
dominant Figma icon authoring path (Material/Phosphor/Iconify/in-house icon libraries all
publish icons AS components), and I reproduced end-to-end that a mapped `instance` child
with the RP-Icon parent-`<img>` JSX shape emits `requireSprite:false` with ZERO warning.
Same for `component` and `component-set`. This violates the D3 ambiguous→True contract
in the exact way the red-team blocks on. Per the review directive: uncertain / plausibly
graphic → do NOT exclude → narrow the exclude-list to `{frame, group, section, text}` and
add 3 positive tests (instance/component/component-set → True). Fix is 3 LOC + 3 tests;
scoped iter-7 implementer round. See `SELF_REVIEW.md` for the reproduction and fix instruction.

**State (was):** `READY_FOR_SELF_REVIEW` (iter-6 golfin-implementer 2026-07-04 CEST). D6
guard generalized from 3-type include-list to exclude-list; 173 → 180 tests; anchor
byte-identical; `fail:0` on shipped prefab; zero `Assets/` drift.

**State (was):** `ARCHITECT_REVIEW_FAIL` (iter-6 architect intercept: generalize the D6 guard).
The iter-5 reviewer PASSed but flagged that the D6 guard ENUMERATES shape types
`{rounded-rectangle, vector, ellipse}` — so `line`/`polygon`/`star` (and any future shape
primitive) with empty JSX visual fall through the SAME silent `requireSprite:false` hole. That is
the identical load-bearing false-negative class the red-team FAILED at iter-4 (reachable, must be
STRUCTURAL, not an enumerated bandaid). Rather than ship an incomplete enumeration, iter-6 generalizes
the D6 fail-safe to cover ALL graphic/shape primitives (exclude only text + pure layout frames/groups).
Then → red-team.

**State (was):** `READY_FOR_REDTEAM` (iter-5 golfin-reviewer PASS 2026-07-04 09:45 CEST). D6 structural
fix closes Attack 3: `_metadata_only_shape` guard (XML type + empty vis + explicit map, zero
name-string comparisons) fires on both fixtures → `requireSprite=True` + D6 WARN, restoring the
D3 ambiguous→True fail-safe contract. Non-hollow (3 D6 tests fail on guard revert). No
over-reach: byte-identical anchor spec regen, only RP-Icon-class element flagged when mapped,
text/flat/normal-sprite elements untouched. `fail:0` mechanically holds on the SHIPPED
`StaminaMenuRow.prefab` (zero prefab / linter / Assets edits — verified via `git diff`). 173/173
tests green. Scope pristine (2 `Docs/Scripts/` files + task folder). Final sibling-gap sweep:
no reachable class remaining (`<line>`/`polygon`/`star`-with-empty-vis noted as an unreachable
follow-up, not a blocker). Handing to the adversarial red-team gate that raised the blocker.

**State (was):** `ARCHITECT_REVIEW_FAIL` (iter-4 red-team 2026-07-04 07:45 CEST). The div-wrapped-text fix,
byte-identical spec regen, and `fail:0` on the SHIPPED `StaminaMenuRow.prefab` are all confirmed
(red-team re-ran `UIFidelityLinter.LintPrefab` itself via Unity MCP — the golfin-reviewer could not).
Attacks 1, 2, 4 survived. **Blocker (Attack 3):** the §4 "metadata-only shape node" gap the golfin-reviewer
documented-not-failed is, on independent judgment, a ship-blocker: mapping a `rounded-rectangle`/`vector`
Figma node whose visual is rendered by an `<img>` on the parent (the `RP Icon` pattern, present in BOTH
committed fixtures) emits `requireSprite:false` **silently, with no warning** — a reachable false-negative
on the one load-bearing field, violating the tool's own D3 ambiguous→True fail-safe contract. See
`REDTEAM_REVIEW.md` Attack 3 for the reproduction and the scoped fix instruction.

## iter-7 fix list (from SELF_REVIEW.md, iter-6 self-reviewer)
1. In `Docs/Scripts/figma_node_to_spec.py` line 903 narrow `_NON_GRAPHIC_CONTAINER_TYPES`
   from `{frame, group, section, text, instance, component, component-set}` to
   `{frame, group, section, text}`. Rationale: `instance` / `component` / `component-set`
   are *references* to component definitions that may themselves be graphic (icon libraries
   publish icons AS components); the RP-Icon-parent-`<img>` JSX shape reproduces the exact
   iter-4 silent false-negative through mapped `instance` children.
2. Add 3 positive tests to `TestMetadataOnlyShapeNodeGeneralization`:
   `test_instance_metadata_only_require_sprite_true`,
   `test_component_metadata_only_require_sprite_true`,
   `test_component_set_metadata_only_require_sprite_true`. Each: mapped shape child with no
   JSX `data-node-id` → assert `_metadata_only_shape=True`, `requireSprite=True`, D6 WARN
   emitted. These MUST FAIL with the current iter-6 exclude-list.
3. Update the D6 docstring at lines 894–902 to describe the intentionally-minimal
   exclude-list (pure layout containers only) and explicitly note that instance/component/
   component-set fall under D3 ambiguous→True.
4. Re-confirm the anchor spec regen still emits byte-identical `menu_row_emitted_spec.json`
   and `UIFidelityLinter.LintPrefab` still returns `fail:0` on the SHIPPED prefab. The two
   committed fixtures do not map any `instance`/`component`/`component-set` node so both
   invariants should hold — but re-run to confirm.

**State (was):** `READY_FOR_REDTEAM` (iter-4 golfin-reviewer PASS 2026-07-04 07:55 CEST)
**Tier:** 2 (one Python script + unit tests; no Unity/scene/prefab changes)
**Follow-up to:** figma_unity_reuse_pipeline (the 4th durable fix — node-spec auto-parse)
**Created:** 2026-07-03 (Queued → Active kickoff by Claude Code)
**Escalated:** 2026-07-03 22:47 CEST by golfin-redteam-reviewer
**Escalation resolved:** 2026-07-03 by Cesar → **Option B (parse the real Figma output)**.

## Pipeline
- [x] SPEC_READY
- [x] IMPLEMENTER_WORKING (iter-1)
- [x] READY_FOR_SELF_REVIEW (iter-1)
- [x] SELF_REVIEW_PASS (iter-1)
- [x] READY_FOR_ARCHITECT_REVIEW (iter-1)
- [x] READY_FOR_REDTEAM (iter-1)
- [x] ARCHITECT_REVIEW_ESCALATE (iter-1) → Cesar chose Option B
- [x] IMPLEMENTER_WORKING (iter-2 — Option B rebuild)
- [x] READY_FOR_SELF_REVIEW (iter-2)
- [x] ARCHITECT_REVIEW_FAIL (iter-2 directed by architect: skip-judgment rules)
- [x] IMPLEMENTER_WORKING (iter-3 — skip-judgment rules)
- [x] READY_FOR_SELF_REVIEW (iter-3)
- [x] SELF_REVIEW_PASS (iter-3)
- [x] READY_FOR_ARCHITECT_REVIEW (iter-3)
- [x] ARCHITECT_REVIEW_FAIL (iter-3 — Rule 1 supplemental incomplete for div-wrapped text)
- [x] IMPLEMENTER_WORKING (iter-4 — extract text-props off div wrappers)
- [x] READY_FOR_SELF_REVIEW (iter-4)
- [x] SELF_REVIEW_PASS (iter-4)
- [x] READY_FOR_ARCHITECT_REVIEW (iter-4)
- [x] READY_FOR_REDTEAM (iter-4 golfin-reviewer PASS)
- [x] ARCHITECT_REVIEW_FAIL (iter-4 red-team — reachable requireSprite false-negative, §4 escalated to blocker)
- [x] IMPLEMENTER_WORKING (iter-5 — metadata-only shape-node fail-safe)
- [x] READY_FOR_SELF_REVIEW (iter-5)
- [x] SELF_REVIEW_PASS (iter-5)
- [x] READY_FOR_ARCHITECT_REVIEW (iter-5)
- [x] READY_FOR_REDTEAM (iter-5 golfin-reviewer PASS)
- [x] ARCHITECT_REVIEW_FAIL (iter-5 architect intercept — enumeration bandaid; generalize D6)
- [x] IMPLEMENTER_WORKING (iter-6 — D6 generalized to exclude-list)
- [x] READY_FOR_SELF_REVIEW (iter-6)
- [x] SELF_REVIEW_FAIL (iter-6 — instance/component/component-set over-excluded, silent requireSprite:false reachable)
- [x] IMPLEMENTER_WORKING (iter-7 — narrow exclude-list to {frame, group, section, text})
- [x] READY_FOR_SELF_REVIEW (iter-7)
- [x] SELF_REVIEW_PASS (iter-7)
- [x] READY_FOR_ARCHITECT_REVIEW (iter-7)
- [x] READY_FOR_REDTEAM (iter-7 golfin-reviewer PASS)
- [x] ARCHITECT_REVIEW_PASS (iter-7 red-team — class structurally closed, verified by own sweep + live linter)
- [ ] DONE
