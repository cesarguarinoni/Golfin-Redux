# REDTEAM_REVIEW — figma_node_spec_generator (iter-7)

**Reviewer:** golfin-redteam-reviewer
**Timestamp:** 2026-07-04 08:45 CEST
**Verdict:** `ARCHITECT_REVIEW_PASS`

Tier-2 backend task (Python + unittest). Rule 5 screenshot / Rule 18 Figma-fidelity gates
correctly exempt (no rendered UI, no consumed Figma node). I attacked the substance, re-ran
everything myself in a fresh interpreter + a live Unity MCP linter run, and could not break it.
This is the terminal closure of the reachable `requireSprite=False` silent false-negative class
**I personally raised and FAILED at iter-4.**

---

## Attack 1 — my iter-4 blocker: is it DEAD? — GONE (verified by my own hostile sweep)

I re-implemented the sweep from scratch (my own hand-built XML+JSX fixtures, NOT the tests'
helper), driving each type through the REAL `parse_figma_dumps` → `element_to_spec` output path
(not just the internal flag). Target = a mapped child node whose visual is rendered by an `<img>`
on the parent (the RP-Icon pattern) and which is ABSENT from the JSX (join miss → empty vis).

Result — 23 types incl. instance/component/component-set, every shape primitive, image/
boolean-op/slice, and arbitrary unknown/future strings (`widget`, `embed`, `sticker`,
`mystery-glyph`, `canvas`, `document`): **every one emits `_metadata_only_shape=True`,
`requireSprite=True`, and a metadata-only WARN.** SILENT FALSE-NEGATIVES: **NONE.**

I then hunted the residual join-shape edge the guard's `not vis` condition could hide:
- **Case A (shape node join-HIT but styleless `<div>`):** emits `requireSprite=False` silently.
  I checked whether this is reachable on real Figma output. In the committed fixture the RP-Icon
  (`13330:1192` rounded-rectangle) is **absent from the JSX** (`grep` → 0 matches) → metadata-only
  → D6-covered. The button's real visual shapes (`13330:1196` vector, `13330:1198` rounded-rect)
  are **present with `data-node-id`** and their fills/border/radius ARE detected. So real Figma
  shape nodes are EITHER join-hit-with-visual (correct True) OR metadata-only-miss (D6 True+WARN).
  A styleless matched div means Figma itself rendered the shape as visually empty — where `False`
  is correct. Case A is not a reachable false-negative for a genuinely-visible sprite.

The only `requireSprite=False` exits are: the 4 excluded layout types, and a text node (Rule 1).
Both correct. **My iter-4 blocker is structurally closed.**

## Attack 2 — fail:0 real + earned — PASS (I re-ran the linter live)

- Regenerated the emitted spec from the committed fixtures + committed name-map:
  `diff menu_row_emitted_spec.json <my regen>` → **BYTE-IDENTICAL**. Not a stale artifact.
- Second fixture (`selection_card_13156-1232`) regenerates cleanly (18 elements, no crash) →
  parser is not overfit to one JSX shape.
- `git diff HEAD -- Assets/` → **empty**. `git status` on `StaminaMenuRow.prefab` and
  `UIFidelityLinter.cs` → clean. `fail:0` was not forced by editing the prefab or the linter.
- **I re-ran `UIFidelityLinter.LintPrefab` MYSELF via Unity MCP this pass** (as at iter-4):
  freshly written `Docs/Diagnostics/_capture/StaminaMenuRow_lint.json` (timestamped 08:40 today) =
  `{"prefab":".../StaminaMenuRow.prefab","fail":0,"warn":0,"findings":[]}`, Editor.log
  `— 0 FAIL, 0 WARN, 0 INFO — RESULT: PASS (health)`. `fail:0` genuine on the SHIPPED prefab.
- Emitted-spec decisions are the correct/stricter ones: sprite atoms (Thumbnail/TierBadge/
  RpPill/BuyButton) → `requireSprite=True, color=""`; text → `requireSprite=False` w/ real
  color+font; TierLabel bg-clip-text gradient → `requireSprite=False` (TMP vertex gradient).

## Attack 3 — no over-reach — PASS

The fail-safe-by-default does NOT flip a genuine layout spacer to `requireSprite=true`. My sweep:
`frame`, `group`, `section`, `text`, when mapped with empty vis, all emit
`_metadata_only_shape=False, requireSprite=False, warn=False`. Layout containers stay negative;
no spurious FAIL of a real prefab.

## Attack 4 — non-hollow + no regression — PASS

- **Non-hollow (my own patch):** I text-substituted `_NON_GRAPHIC_CONTAINER_TYPES` back to the
  iter-6 wide set and re-ran the 3 new tests → all 3 **FAILED**
  (`AssertionError ... _metadata_only_shape must be True`). Restored the tool **byte-identical**
  (`diff` clean). The tests assert `_metadata_only_shape` AND `_decide_require_sprite(...)==True`
  via the real function + the WARN — not gamed booleans.
- **183 tests pass** (my run). Anchor spec byte-identical. Scope: only 2 Python files + task
  folder + the SPEC Queued→Active rename. Rule 7 standing bans trivially satisfied (no Assets/).

## Attack 5 — whole-tool final sweep — PASS (one non-blocking follow-up noted)

- Border/stroke: the exact `1v1_ingame_ui` 3px `#818EA1` banner-border scar → stroke weight 3 →
  `requireSprite=True`. `border` shorthand + color, `border-2` → detected. Flat solid no
  border/radius → `False` (correct).
- Radius: `rounded-full` → `-1` + WARN; `rounded-[43.077px]` → exact. Gradient → `requireSprite=True`.
- Font: `text-[28px] font-['Rubik:SemiBold'] text-white` → 28 / SemiBold / #FFFFFF. bg-clip-text
  gradient text → `requireSprite=False` (correct).
- **Minor latent fragility (NOT a blocker):** text `color` extraction is class-ORDER-dependent —
  if Figma ever emitted `text-[Npx]` BEFORE `text-[#hex]` in one className, the greedy
  `text-\[([^\]]+)\]` matches the px token first and the color drops to `""`. I verified real
  Figma emits **color-before-size** (`grep` for the bad order in BOTH fixtures → 0 hits;
  `ItemDescLabel` = `text-[#c7d6eb] text-[18px]`), so it is unreproduced on real input. It never
  touches the load-bearing `requireSprite` field (text is always `False`), and its failure mode is
  the SAFE direction (a SKIPPED color check, never a greenlit fabricated sprite). Follow-up polish,
  not a ship-blocker for an iter scoped to the exclude-list.
- Report integrity (Rule 6): exclude-list literal at line 914, the 3 test names, and the 183 count
  all match the shipping code. No fabricated tool output in any report.

---

## Prior-rejection replay

The only prior "rejection" is my own iter-4 `ARCHITECT_REVIEW_FAIL` (§4 metadata-only shape node,
silent `requireSprite:false`). Re-shot at the exact angle (mapped shape child, parent-`<img>`,
JSX join miss) on both the synthetic sweep and the real committed fixture: **GONE** —
`requireSprite=True` + WARN, on every node type, including the `instance`/`component`/`component-set`
path the iter-6 self-reviewer caught before it reached me.

## Three break-attempts (why each failed)

1. **Construct a mapped sprite-like node that silently emits `requireSprite=False`.** Tried 23
   types incl. unknown/future strings, plus the join-hit-but-styleless edge. All non-container
   types → True+WARN; the styleless edge is unreachable for a visible sprite on real Figma output.
2. **Force fail:0 by editing the prefab/linter, or ship a stale JSON.** `git diff HEAD -- Assets/`
   empty; I re-ran the linter live via Unity MCP → fresh `fail:0`. Not gameable.
3. **Find a different dropped-signal class (border/radius/font/color).** Border/radius/gradient/
   font all faithful; the one color-order fragility is safe-direction, non-load-bearing, and
   unreproduced on real Figma input.

## Verdict

The iter-7 3-LOC narrow of `_NON_GRAPHIC_CONTAINER_TYPES` to `{frame, group, section, text}` is
the terminal, structural fix for the reachable `requireSprite=False` silent false-negative class I
FAILED at iter-4. Default is now fail-safe True; the only negatives are pure layout containers +
text. I actively tried to break it three ways and came up empty. The anchor spec regen is
byte-identical, `fail:0` is genuine on the SHIPPED prefab (re-run live by me), 183 tests pass, the
new tests are non-hollow, and scope is clean.

`ARCHITECT_REVIEW_PASS` — advances to Cesar's approval gate.

## Files touched by this review
| File | Change |
|---|---|
| `Docs/Specs/Active/figma_node_spec_generator/REDTEAM_REVIEW.md` | Rewritten for iter-7: PASS — iter-4 false-negative class structurally closed, verified by own sweep + live linter |
| `Docs/Specs/Active/figma_node_spec_generator/STATUS.md` | → `ARCHITECT_REVIEW_PASS` |
| `Docs/Diagnostics/_capture/StaminaMenuRow_lint.json` | Regenerated by my own live `LintPrefab` run (fail:0 confirmed) — diagnostic artifact, not a task deliverable |
