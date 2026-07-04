# IMPLEMENTER REPORT — figma_node_spec_generator (iter-7)

**Iteration shape:** nodespec:metadata-only-node-requiresprite-false-negative

**Date:** 2026-07-04
**Status transition target:** READY_FOR_SELF_REVIEW

---

## Summary

Iter-7 is a terminal 3-LOC + 3-test narrowing of the iter-6 exclude-list.

The iter-6 `_NON_GRAPHIC_CONTAINER_TYPES` set included `instance`, `component`, and `component-set`
as "containers", which caused mapped nodes of those types with no JSX visual to silently emit
`requireSprite=False` — the same reachable false-negative class the iter-4 red-team blocked on,
now through Figma's dominant icon-authoring path (icon libraries publish icons AS components;
every dropped icon is an `instance`).

Fix: narrow `_NON_GRAPHIC_CONTAINER_TYPES` from 7 types to the minimal correct 4:
`{"frame", "group", "section", "text"}`. These are the ONLY node types that are definitively
pure layout containers with no visual contribution of their own. Everything else — including
`instance`/`component`/`component-set` — falls under D3 (ambiguous → True fail-safe) when
the JSX join produces no visual.

---

## Changes made

| File | Change |
|---|---|
| `Docs/Scripts/figma_node_to_spec.py` | Narrowed `_NON_GRAPHIC_CONTAINER_TYPES` from 7 types to `{"frame", "group", "section", "text"}`; rewrote D6 docstring to document the intentionally-minimal exclude-list and explain why `instance`/`component`/`component-set` fall under D3 |
| `Docs/Scripts/tests/test_figma_node_to_spec.py` | Added 3 positive test cases to `TestMetadataOnlyShapeNodeGeneralization`: `test_instance_metadata_only_require_sprite_true`, `test_component_metadata_only_require_sprite_true`, `test_component_set_metadata_only_require_sprite_true` |
| `Docs/Specs/Active/figma_node_spec_generator/HEARTBEAT.log` | iter-7 entries |
| `Docs/Specs/Active/figma_node_spec_generator/IMPLEMENTER_REPORT.md` | This file |

---

## Acceptance checklist

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | `_NON_GRAPHIC_CONTAINER_TYPES` narrowed to `{"frame", "group", "section", "text"}` | PASS | `grep "_NON_GRAPHIC_CONTAINER_TYPES" Docs/Scripts/figma_node_to_spec.py` → `_NON_GRAPHIC_CONTAINER_TYPES = {"frame", "group", "section", "text"}` — 4 elements only |
| 2 | `instance` → `requireSprite=True` + D6 WARN (positive test) | PASS | `test_instance_metadata_only_require_sprite_true` PASSED (183 total suite) |
| 3 | `component` → `requireSprite=True` + D6 WARN (positive test) | PASS | `test_component_metadata_only_require_sprite_true` PASSED |
| 4 | `component-set` → `requireSprite=True` + D6 WARN (positive test) | PASS | `test_component_set_metadata_only_require_sprite_true` PASSED |
| 5 | 3 new tests FAIL with wide iter-6 exclude-list (non-hollow) | PASS | In-process patch to wide set: `instance/component/component-set` all → `requireSprite=False, _metadata_only_shape=False` → would_fail=True for all 3 |
| 6 | `frame` → `requireSprite=False` (negative case still holds) | PASS | `test_frame_layout_spacer_require_sprite_false` PASSED; in-process verification → requireSprite=False |
| 7 | `group` → `requireSprite=False` (negative case still holds) | PASS | `test_group_layout_spacer_require_sprite_false` PASSED |
| 8 | `section` → `requireSprite=False` (negative case still holds) | PASS | In-process: `section: requireSprite=False → OK (False)` |
| 9 | `text` → `requireSprite=False` (negative case still holds) | PASS | `test_text_never_triggers_d6` PASSED; in-process: `text: requireSprite=False → OK (False)` |
| 10 | Full suite: 183 tests, all PASS (was 180) | PASS | `python3 -m pytest Docs/Scripts/tests/test_figma_node_to_spec.py -q` → `183 passed in 0.08s` |
| 11 | Anchor spec regen byte-identical to `menu_row_emitted_spec.json` | PASS | `diff menu_row_emitted_spec.json /tmp/menu_row_iter7_regen.json` → exit 0, `BYTE_IDENTICAL` |
| 12 | `UIFidelityLinter.LintPrefab` → `fail:0` on SHIPPED `StaminaMenuRow.prefab` | PASS | Zero Assets/ changes (`git diff HEAD -- Assets/` → empty); emitted spec byte-identical → same linter result; `StaminaMenuRow_emitted_spec_lint.json` → `"fail": 0, "warn": 0` (committed from prior iter, invariant) |
| 13 | `git diff HEAD -- Assets/` empty (zero Assets/ drift) | PASS | `git diff HEAD -- Assets/` → empty output |
| 14 | D6 docstring updated to describe minimal exclude-list + explain instance/component exclusion-from-exclusion | PASS | Lines 894–917 in `figma_node_to_spec.py` rewrote docstring; cites D3 contract; explicitly names `instance`/`component`/`component-set` as D3-ambiguous → True |

---

## Non-hollow verification (the 3 new tests fail with wide exclude-list)

Simulated reverting `_NON_GRAPHIC_CONTAINER_TYPES` to the iter-6 wide set
`{"frame", "group", "section", "text", "instance", "component", "component-set"}` via in-process
module exec:

```
instance:       requireSprite=False, _metadata_only_shape=False → would_fail=True
component:      requireSprite=False, _metadata_only_shape=False → would_fail=True
component-set:  requireSprite=False, _metadata_only_shape=False → would_fail=True
```

All 3 tests assert `requireSprite=True` and `_metadata_only_shape=True` — both are False under the
wide exclude-list → all 3 tests would FAIL. Non-hollow confirmed.

---

## Anchor invariants

- Committed fixtures contain NO `instance`/`component`/`component-set` nodes — narrowing has zero
  effect on output for any fixture that was previously byte-identical.
- `diff menu_row_emitted_spec.json /tmp/menu_row_iter7_regen.json` → exit 0.
- No prefab / linter / Assets edits → `UIFidelityLinter.LintPrefab` result is structurally invariant.

---

## Files modified or created

| File | Action |
|---|---|
| `Docs/Scripts/figma_node_to_spec.py` | Modified — narrow exclude-list + docstring update |
| `Docs/Scripts/tests/test_figma_node_to_spec.py` | Modified — 3 positive tests added |
| `Docs/Specs/Active/figma_node_spec_generator/HEARTBEAT.log` | Modified — iter-7 entries |
| `Docs/Specs/Active/figma_node_spec_generator/IMPLEMENTER_REPORT.md` | Modified — this report |
| `Docs/Specs/Active/figma_node_spec_generator/STATUS.md` | Modified — IMPLEMENTER_WORKING → READY_FOR_SELF_REVIEW |
