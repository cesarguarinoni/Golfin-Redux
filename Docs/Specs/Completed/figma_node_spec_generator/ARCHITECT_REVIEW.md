# ARCHITECT_REVIEW — figma_node_spec_generator (iter-7)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-04 JST
**Verdict:** PASS → `READY_FOR_REDTEAM`

Tier-2 backend / Python task (one script + unit tests; no Unity/scene/prefab changes).
Rule 5 screenshot / Rule 9 Figma re-pull / Rule 10 reference-image diff / Rule 11 clone
provenance read-back / Rules 14–19/21 all correctly exempt (backend, no rendered UI, no
Figma node consumed, no prefab clones). No Step 0 pixel scan, no Step 2 mesh metrics, no
Step 2b Figma fidelity table, no Step 2c clone provenance, no Step 2d UI fidelity lint —
none apply.

Every claim in `IMPLEMENTER_REPORT.md` and `SELF_REVIEW.md` was re-verified against the
shipping code, not accepted on prose. I did NOT trust the self-reviewer's structural-
closure sweep — I ran my own 19-type sweep and my own iter-6 non-hollow patch, both
against the real `parse_figma_dumps + _decide_require_sprite` pipeline in a fresh
interpreter.

---

## 1. Exclude-list is exactly 4 types

```
$ grep -n "_NON_GRAPHIC_CONTAINER_TYPES" Docs/Scripts/figma_node_to_spec.py
914:        _NON_GRAPHIC_CONTAINER_TYPES = {"frame", "group", "section", "text"}
918:            and meta.get("type", "") not in _NON_GRAPHIC_CONTAINER_TYPES  # NOT a layout container
```

Confirmed — narrow set with exactly 4 elements at line 914.

## 2. Full suite: 183 pass

```
$ python3 -m pytest Docs/Scripts/tests/test_figma_node_to_spec.py -q
183 passed in 0.07s
```

Matches the +3 growth (180 → 183) claimed by the report.

## 3. Structural closure — 19-type independent sweep

Re-implemented the sweep from scratch (fresh temp XML+JSX fixtures matching the tests'
own `_make_xml_and_jsx` shape, real `parse_figma_dumps` + real `_decide_require_sprite`,
name-mapped `ShapeChild → SyntheticGO`, empty vis via JSX join miss).

```
type                      class              meta_only  require    warned   result
------------------------------------------------------------------------------------------
frame                     excluded           False      False      False    OK
group                     excluded           False      False      False    OK
section                   excluded           False      False      False    OK
text                      excluded           False      False      False    OK
instance                  sprite-like        True       True       True     OK
component                 sprite-like        True       True       True     OK
component-set             sprite-like        True       True       True     OK
rectangle                 sprite-like        True       True       True     OK
rounded-rectangle         sprite-like        True       True       True     OK
ellipse                   sprite-like        True       True       True     OK
vector                    sprite-like        True       True       True     OK
line                      sprite-like        True       True       True     OK
polygon                   sprite-like        True       True       True     OK
star                      sprite-like        True       True       True     OK
image                     sprite-like        True       True       True     OK
boolean-operation         sprite-like        True       True       True     OK
slice                     sprite-like        True       True       True     OK
sticker-unknown-2027      unknown/future     True       True       True     OK
mystery-shape             unknown/future     True       True       True     OK

STRUCTURAL CLOSURE: CLOSED
```

**Every non-excluded type — including instance/component/component-set that the iter-6
self-reviewer identified as the class hole, plus two arbitrary unknown/future type
strings the pipeline has never seen — emits `_metadata_only_shape=True`,
`requireSprite=True`, and a metadata-only WARN.** The design is now fail-safe by default:
`unknown type + no vis + mapped → True + WARN`, exactly the D3 contract. The only silent-
negative paths are the 4 excluded layout container types, all of which are genuinely pure
containers (frame/group/section) or handled by their own Rule-1 path (text).

Explicit reasoning for closure: the D6 guard fires iff (a) `not vis`, (b) `meta.type` is
truthy, (c) `meta.type not in {frame,group,section,text}`, (d) `_in_map and unity_name is not None`.
For a mapped element with empty vis, the only paths to a silent `requireSprite=False` are
type-in-exclude-list (4 types by explicit construction) or type falsy — and
`_parse_metadata_xml` sets `type = tag.lower()` from a non-empty XML element tag, so
falsy-type is unreachable in practice. No third path exists. Confirmed empirically by
the sweep above and structurally by the code path.

## 4. Non-hollow — 3 new tests fail under iter-6 wide exclude-list

I text-substituted the exclude-list back to the iter-6 wide set
`{frame, group, section, text, instance, component, component-set}` in a re-exec'd copy
of the module, then re-ran the 3 new tests' assertions against the real pipeline:

```
Under WIDE iter-6 exclude-list:
type                 meta_only  require    warned   test would
----------------------------------------------------------------------
instance             False      False      False    FAIL
component            False      False      False    FAIL
component-set        False      False      False    FAIL

Under WIDE iter-6 (excluded-4 sanity):
  frame                meta_only=False require=False warned=False
  group                meta_only=False require=False warned=False
  section              meta_only=False require=False warned=False
  text                 meta_only=False require=False warned=False
```

All 3 new tests would FAIL (`_metadata_only_shape=True` and `requireSprite=True` and
warn-emitted are all violated) under the iter-6 wide set. They are load-bearing, not
tautological. Sanity: the excluded-4 still emit `(False, False, False)` under both narrow
and wide sets — the fix does not flip layout containers.

## 5. Anchor spec regen byte-identical

```
$ python3 Docs/Scripts/figma_node_to_spec.py \
    <task>/reference/nodes/menu_row_13330-1178_metadata.xml \
    <task>/reference/nodes/menu_row_13330-1178_context.jsx \
    --name-map <task>/reference/nodes/stamina_menu_row_name_map.json \
    -o /tmp/menu_row_iter7_indep_regen.json
Wrote 10 elements -> /tmp/menu_row_iter7_indep_regen.json

$ diff <task>/reference/nodes/menu_row_emitted_spec.json /tmp/menu_row_iter7_indep_regen.json
(exit 0) BYTE_IDENTICAL
```

Confirmed. Committed fixtures contain no `instance`/`component`/`component-set` nodes, so
narrowing the exclude-list has zero emission effect on either fixture.

## 6. `fail:0` invariant on SHIPPED `StaminaMenuRow.prefab`

```
$ git diff HEAD -- Assets/     -> empty
$ cat StaminaMenuRow_emitted_spec_lint.json
{"prefab":"Assets/Prefabs/UI/Shop/StaminaMenuRow.prefab","fail":0,"warn":0,"findings":[]}
```

Emitted spec byte-identical + zero prefab/linter edits → `UIFidelityLinter.LintPrefab`
result is structurally invariant. `fail:0` on the SHIPPED prefab holds. The red-team ran
this via Unity MCP at iter-4 and confirmed the value is not a stale on-disk artifact;
nothing has moved on the linter or prefab side since.

## 7. Scope clean

```
$ git status --porcelain --untracked-files=all
RM Docs/Specs/Queued/figma_node_spec_generator/SPEC.md -> Docs/Specs/Active/figma_node_spec_generator/SPEC.md
?? Docs/Scripts/figma_node_to_spec.py
?? Docs/Scripts/tests/test_figma_node_to_spec.py
?? Docs/Specs/Active/figma_node_spec_generator/...
```

Only the 2 Python files + the task folder + the SPEC rename. No stray edits outside
scope. Rule 13 satisfied. Rule 7 standing bans (Physics/, Scenarios.cs, LabScaffold.unity,
`M_Splash*.mat`) trivially satisfied — no Assets/ edits at all.

## 8. Rule sweep

| Rule | Status |
|---|---|
| Rule 5 (re-walk entire acceptance list) | Each of the 14 checklist items independently re-verified above (grep, pytest, diff, git status/diff, in-process 19-type sweep, in-process wide-set patch). Nothing "carried forward." |
| Rule 6 (report integrity) | Every PASS in the report is backed by a re-runnable tool result — no assertion-only claims. No fabricated tool output. |
| Rule 9 (Figma node re-pull) | N/A — this task is the tool that produces node specs, not a task that consumes a node reference. |
| Rule 10 (reference-image diff) | N/A — no rendered UI. |
| Rule 11 (clone-provenance read-back) | N/A — no Unity prefab clones. |
| Rule 13 (scope) | PASS — no drift outside task folder + 2 Python files. |
| Rule 14 (canonical screenshot >= 900px) | N/A — Tier-2 backend, no screenshots at all. |
| Rules 15-17 (rejection, mesh metrics, mesh video) | N/A. |
| Rule 18 (Figma fidelity table) | N/A — SPEC references no Figma node. |
| Rule 19 (clone provenance) | N/A. |
| Rule 21 (UI fidelity lint) | The invariant is that `fail:0` holds on the shipped `StaminaMenuRow.prefab` and no Assets/ edits happened — both confirmed. |

## Structural-closure argument (why the class is provably closed)

The iter-4 red-team FAIL condition was: **a mapped element with empty JSX visual emits
`requireSprite=False` with no warning, silently.** The fix invariant, in plain-English
form, must be:

> For every mapped element with empty vis and any XML `type`, the element emits
> `requireSprite=True + WARN` UNLESS the type is genuinely a pure layout container.

The iter-7 D6 guard implements exactly that as `type not in {frame,group,section,text}`
with a DEFAULT-TRUE fallthrough. The 4 excluded types are the complete set of Figma node
types that carry no visual contribution of their own — frame/group/section are pure
layout, text is handled by its own Rule 1 path. Every OTHER type — whether a known shape
primitive, a component reference (instance/component/component-set — the exact hole the
iter-6 self-reviewer identified), an image, a boolean op, a slice, OR an unknown/future
Figma type name the pipeline has never seen — hits `True + WARN`. This is verified end-
to-end above, not just argued on paper.

The class is closed. The red-team's iter-4 blocker is resolved.

---

## Verdict

**PASS → `READY_FOR_REDTEAM`.**

The iter-7 3-LOC narrow of `_NON_GRAPHIC_CONTAINER_TYPES` to `{frame, group, section,
text}` is the terminal, structural fix for the reachable `requireSprite=False` silent
false-negative class that the iter-4 red-team raised. Every reachable Figma node type
that is not a pure layout container — including component instances (the dominant icon
authoring path) and any unknown/future type — now emits `requireSprite=True + WARN` on
metadata-only join miss. The 4 excluded types remain correctly negative. Anchor spec
regen byte-identical, `fail:0` invariant intact, 183 tests green, scope clean.

Handing to the adversarial red-team gate that raised this class — it should confirm
closure, but I write the verdict to survive a skeptic actively trying to break it.

## Files touched by this review

| File | Change |
|---|---|
| `Docs/Specs/Active/figma_node_spec_generator/ARCHITECT_REVIEW.md` | Overwritten — PASS verdict (iter-7) with independent structural-closure sweep and non-hollow patch check |
| `Docs/Specs/Active/figma_node_spec_generator/STATUS.md` | -> `READY_FOR_REDTEAM` |
