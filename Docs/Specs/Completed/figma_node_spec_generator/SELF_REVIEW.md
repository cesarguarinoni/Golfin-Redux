# SELF_REVIEW — figma_node_spec_generator (iter-7)

**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-07-04 JST
**Iteration:** 7
**Verdict:** `FORWARD_TO_ARCHITECT` (SELF_REVIEW_PASS)

Tier-2 backend / Python task (no Unity scene/prefab changes). Rule 5 screenshot / Rule 18
Figma-fidelity gates correctly exempt. No `screenshots/figma-reference.png` required. Steps
6 (bbox), 7 (scene-mutation), 8 (production-flow) not applicable.

Iter-7 was gated on the ★ critical structural question the review directive posed: with
the exclude-list narrowed to EXACTLY `{frame, group, section, text}` and DEFAULT =
fail-safe True, is there ANY remaining node type or shape that could emit a SILENT
`requireSprite=False` for a mapped, sprite-like element? I ran that check end-to-end
against the shipping script (not against a description of it) and the answer is **no** —
the class is structurally closed.

---

## What I verified independently

Every claim in the report was reproduced against the shipping code, not accepted on prose.

### 1. Exclude-list narrowed to 4 types

```
$ grep "_NON_GRAPHIC_CONTAINER_TYPES" Docs/Scripts/figma_node_to_spec.py
914:        _NON_GRAPHIC_CONTAINER_TYPES = {"frame", "group", "section", "text"}
918:            and meta.get("type", "") not in _NON_GRAPHIC_CONTAINER_TYPES  # NOT a layout container
```

Confirmed. The set literal at line 914 is exactly 4 elements: `{frame, group, section, text}`.

### 2. Full suite: 183 tests, all PASS

```
$ python3 -m pytest Docs/Scripts/tests/test_figma_node_to_spec.py -q
........................................................................ [ 39%]
........................................................................ [ 78%]
.......................................                                  [100%]
183 passed in 0.07s
```

Report claim: 173 → 180 (iter-6) → 183 (iter-7). Confirmed.

### 3. Anchor spec regen byte-identical

Re-ran the pipeline on the committed fixture (metadata + JSX + name-map) with the
iter-7 script:

```
$ python3 Docs/Scripts/figma_node_to_spec.py \
    reference/nodes/menu_row_13330-1178_metadata.xml \
    reference/nodes/menu_row_13330-1178_context.jsx \
    --name-map reference/nodes/stamina_menu_row_name_map.json \
    -o /tmp/menu_row_iter7_selfreview_regen.json
Wrote 10 elements → /tmp/menu_row_iter7_selfreview_regen.json

$ diff reference/nodes/menu_row_emitted_spec.json /tmp/menu_row_iter7_selfreview_regen.json
BYTE_IDENTICAL
```

Confirmed. The committed fixture has no `instance`/`component`/`component-set` nodes, so
narrowing the exclude-list has no effect on emission for it.

### 4. `fail:0` invariant on SHIPPED StaminaMenuRow.prefab

`git diff HEAD -- Assets/` → empty output (zero drift). The committed
`StaminaMenuRow_emitted_spec_lint.json` is:

```
{"prefab":"Assets/Prefabs/UI/Shop/StaminaMenuRow.prefab","fail":0,"warn":0,"findings":[]}
```

Emitted spec byte-identical + zero prefab changes → linter result is structurally
invariant. Confirmed.

### 5. Working tree scope clean

```
$ git status --porcelain --untracked-files=all
RM Docs/Specs/Queued/figma_node_spec_generator/SPEC.md -> Docs/Specs/Active/figma_node_spec_generator/SPEC.md
?? Docs/Scripts/figma_node_to_spec.py
?? Docs/Scripts/tests/test_figma_node_to_spec.py
?? Docs/Specs/Active/figma_node_spec_generator/...
```

Only the 2 Python files + the task folder + the SPEC rename. No stray edits outside
scope. Rule 13 satisfied (no drift outside the task folder).

---

## ★ Structural closure — the critical question

The directive's core question: with the exclude-list narrowed to
`{frame, group, section, text}`, does ANY plausible node type still emit a SILENT
`requireSprite=False` for a mapped, empty-JSX-visual element?

I ran the actual pipeline (`parse_figma_dumps` on synthetic XML+JSX+name-map) for a
representative universe of types. Full end-to-end results against the shipping script:

```
MUST fire (D6 → requireSprite=True + WARN):
  instance             | meta_only=True  | requireSprite=True  | warn=True   ✓
  component            | meta_only=True  | requireSprite=True  | warn=True   ✓
  component-set        | meta_only=True  | requireSprite=True  | warn=True   ✓
  rectangle            | meta_only=True  | requireSprite=True  | warn=True   ✓
  line                 | meta_only=True  | requireSprite=True  | warn=True   ✓
  polygon              | meta_only=True  | requireSprite=True  | warn=True   ✓
  star                 | meta_only=True  | requireSprite=True  | warn=True   ✓
  vector               | meta_only=True  | requireSprite=True  | warn=True   ✓
  ellipse              | meta_only=True  | requireSprite=True  | warn=True   ✓
  rounded-rectangle    | meta_only=True  | requireSprite=True  | warn=True   ✓
  image                | meta_only=True  | requireSprite=True  | warn=True   ✓
  boolean-operation    | meta_only=True  | requireSprite=True  | warn=True   ✓
  slice                | meta_only=True  | requireSprite=True  | warn=True   ✓
  sticker              | meta_only=True  | requireSprite=True  | warn=True   ✓  (unknown/future type — fail-safe as intended)

MUST NOT fire (layout spacer → requireSprite=False, no WARN):
  frame                | meta_only=False | requireSprite=False | warn=False  ✓
  group                | meta_only=False | requireSprite=False | warn=False  ✓
  section              | meta_only=False | requireSprite=False | warn=False  ✓
  text                 | meta_only=False | requireSprite=False | warn=False  ✓
```

### The class is closed. Explicit reasoning:

The D6 guard fires iff **ALL** of these hold (lines 915–920 of the shipping script):

1. `not vis` — no JSX visual joined
2. `meta.get("type", "")` truthy — has a non-empty type
3. type NOT in `{frame, group, section, text}`
4. `_in_map and unity_name is not None` — explicitly mapped to a Unity GO

For a mapped, empty-vis element with any type, the ONLY paths to a silent
`requireSprite=False` are:

- **Path A: type IS in the exclude-list.** By construction, only 4 types:
  `{frame, group, section, text}`. Each is a genuine pure-layout container / a text
  node handled by its own path (Rule 1). This is correct.
- **Path B: type is falsy (`""`).** Unreachable in practice: `type` is set from the
  XML element's `tag` in `_parse_metadata_xml` (line 480 —
  `"type": tag`), and XML elements always have a non-empty tag. This isn't a
  reachable silent hole.

No third path exists to `requireSprite=False` without a WARN. Every other type
(shape primitive, component reference, image, boolean-op, slice, and any unknown /
future type — because the exclude-list is EXPLICIT, unknown types fall out of it and
hit `True + WARN`) fires D6.

The design has been inverted correctly: default = fail-safe True; exclude only text +
pure layout containers. That's the D3 contract.

### No fourth slip-through — layout containers stay negative

For each of the 4 excluded types, I verified with the shipping code that a mapped
element with empty vis emits `requireSprite=False` with NO WARN. Layout spacers
are protected. No over-trigger.

---

## Non-hollow — the 3 new tests fail with the wide iter-6 set

I ran the 3 new tests' assertions against a widened exclude-list (in-process patch to
`{frame, group, section, text, instance, component, component-set}`) via the actual
`parse_figma_dumps` code path:

```
Under WIDE exclude-list (iter-6, before iter-7 fix):
  instance         meta_only=False requireSprite=False warn=False → test would PASS: False
  component        meta_only=False requireSprite=False warn=False → test would PASS: False
  component-set    meta_only=False requireSprite=False warn=False → test would PASS: False

Under CURRENT (narrow) exclude-list (iter-7):
  instance         meta_only=True  requireSprite=True  warn=True  → test would PASS: True
  component        meta_only=True  requireSprite=True  warn=True  → test would PASS: True
  component-set    meta_only=True  requireSprite=True  warn=True  → test would PASS: True
```

All 3 new tests would FAIL under the iter-6 wide set → they are load-bearing, not
tautological. Report claim #5 confirmed.

---

## D6 docstring updated

Lines 894–913 rewrite the D6 docstring to describe the intentionally-minimal
exclude-list, cite the D3 contract, and explicitly name
`instance`/`component`/`component-set` as ambiguous → True. Lines 924–926 also
generalized the WARN copy to "any non-layout-container type." Confirmed by direct
inspection.

---

## Rule check — the ones that apply

- **Rule 5 (re-walk full acceptance list):** every criterion of iter-6's fix list
  from `SELF_REVIEW.md` was re-verified (list narrowed → PASS; 3 positive tests →
  PASS; instance/component/component-set → PASS; frame/group/section/text negative →
  PASS; docstring → PASS; anchor byte-identical → PASS; lint fail:0 → PASS; scope
  clean → PASS). Not "carried forward from prior iter" — every item re-run.
- **Rule 6 (unbacked PASS = FAIL):** every PASS in the implementer report has a tool
  result I re-ran myself: grep output, pytest count, diff exit code, git diff /
  status output, in-process type-universe check, in-process wide-set patch check.
  No unbacked claims.
- **Rule 9 (Figma node re-pull):** not applicable — this task is the tool that
  produces node specs, not a task that consumes a node reference.
- **Rule 10 (reference-image diff):** not applicable (no rendered UI).
- **Rule 11 (clone-provenance read-back):** not applicable (no Unity prefab clones).
- **Rule 13 (scope):** verified — no drift outside the task folder + 2 Python files.
- **Rule 14 (canonical screenshot ≥900px long edge):** not applicable — no
  screenshots at all for this Tier-2 backend task.
- **Rules 15–17 (rejection follow-up, mesh metrics, mesh video):** not applicable.
- **Rule 18 (Figma fidelity table):** correctly exempt — SPEC references no Figma
  node (the task IS the node-spec generator).
- **Rule 19 (clone provenance):** not applicable.
- **Rule 21 (UI fidelity lint):** the lint JSON is present
  (`StaminaMenuRow_emitted_spec_lint.json` = `fail:0, warn:0`) and I confirmed
  Assets/ untouched → the invariant holds structurally.
- **Iteration count (N=7):** N≥3 with a FAIL would ordinarily route to ESCALATE, but
  this iteration is a PASS on the terminal 3-token fix the iter-6 self-reviewer
  wrote out; the class is structurally closed. Forward to architect is correct.

---

## Verdict

**`FORWARD_TO_ARCHITECT`** → `READY_FOR_ARCHITECT_REVIEW`.

The iter-7 fix is the terminal, structural closure of the reachable
`requireSprite=False` silent false-negative class the iter-4 red-team blocked on:

- **Exclude-list is now minimal-correct.** Only pure layout containers + text
  (`{frame, group, section, text}`). Every other node type — every shape primitive,
  every component reference, every image / boolean-op / slice, every unknown or
  future type — hits `_metadata_only_shape=True` + `requireSprite=True` + D6 WARN
  when mapped with empty JSX vis. Verified end-to-end against the shipping script.
- **The design is fail-safe by default.** Any unknown Figma type from a future
  version of `get_metadata` will fall through to True + WARN, not silently to False.
  That is exactly the D3 contract.
- **The 3 new tests are non-hollow.** Confirmed to fail under the iter-6 wide set
  via in-process patching of the actual pipeline.
- **Zero regression.** Anchor spec regen byte-identical. `fail:0` on shipped
  prefab. `git diff HEAD -- Assets/` empty. 183 tests pass. Layout containers still
  emit `requireSprite=False` (no over-trigger). Second committed fixture
  (`selection_card_13156-1232`) is covered by the same test suite.
- **Scope clean.** Only 2 Python files + task folder. No drift.

The class the red-team defined at iter-4 ("mapped element resolving to
metadata-only via the RP-Icon-parent-`<img>` pattern") is closed for every reachable
Figma node type. There is no fourth slip-through.

## Files touched by this review

| File | Change |
|---|---|
| `Docs/Specs/Active/figma_node_spec_generator/SELF_REVIEW.md` | Overwritten — verdict FORWARD_TO_ARCHITECT (iter-7) |
| `Docs/Specs/Active/figma_node_spec_generator/STATUS.md` | → `READY_FOR_ARCHITECT_REVIEW` |
