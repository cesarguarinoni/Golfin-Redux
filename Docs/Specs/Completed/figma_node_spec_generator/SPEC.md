# SPEC — figma_node_spec_generator

> **Order:** TBD (Cesar to assign in Notion). **Follow-up to `figma_unity_reuse_pipeline`** — the last
> ⏳ of the four durable fixes (node-spec auto-parse). **Tier 2** — one Python script + unit tests,
> no Unity/scene/prefab changes.

## Status

`SPEC_READY`.

## Goal

Generate the per-element `spec.json` that `UIFidelityLinter` consumes **automatically from a Figma
node**, removing the manual hand-authoring step. Today the implementer writes each element's
`{name,w,h,radius,requireSprite,color,fontSize,fontWeight}` by hand off the `get_design_context`
numbers (Rule 21 / step 6e) — slow and error-prone. This tool turns that into a lookup: node context in,
lint spec out.

## ⚠️ RESCOPE 2026-07-03 (Option B — Cesar) — OVERRIDES §Locked D1 + §Implementation step 1

Red-team escalation (`REDTEAM_REVIEW.md`) proved the original premise wrong: `get_design_context`
returns **React/Tailwind JSX**, NOT a `{elements:[...]}` array. The iter-1 tool required a bespoke
hand-authored intermediate (authoring relocated, not removed) and its anchor input was hand-edited to
force `requireSprite=true`. **Cesar chose Option B: parse the REAL Figma output** so it is truly
node-in / spec-out. This section is the corrected contract; the OUTPUT schema (§Reference below) is
unchanged. **Keep the iter-1 back-end** (8-key emission, sentinel logic, requireSprite heuristic core,
color/weight normalization, `--verbose`, and every test that still applies) — REPLACE ONLY the input
front-end. This is a front-end swap, not a from-scratch rebuild.

**New input contract — TWO saved dumps per node, joined on `data-node-id`:**
1. **`get_metadata` XML** → authoritative geometry: node `id`, `type` (frame/text/rounded-rectangle/vector),
   `name`, exact px `width`/`height`, hierarchy. (JSX sizes are often `size-full`/relative — trust metadata for w/h.)
2. **`get_design_context` JSX** → visual attributes via Tailwind classes + inline styles: fills, border, radius,
   font size/weight, colors, `<img>` presence.

Ground-truth fixtures (REAL pulls, committed) live in `reference/nodes/`:
`menu_row_13330-1178_metadata.xml` + `menu_row_13330-1178_context.jsx`. Build + test against these.

**CLI:** `python3 Docs/Scripts/figma_node_to_spec.py <metadata.xml> <context.jsx> [--name-map map.json] -o <out_spec.json>`

**Field mapping — where each of the 8 keys comes from:**
| key | source | transform |
|---|---|---|
| `name` | metadata/JSX `data-name` | **remapped Figma-name → Unity-GO-name via `--name-map`** (see below). Without a map: emit Figma name + WARN. |
| `w`,`h` | **get_metadata** exact px | authoritative; `-1` for wrappers not size-checked |
| `radius` | JSX `rounded-[Npx]` | uniform value; if per-corner (`rounded-tl/br-[..]`) differ → `-1` + WARN |
| `requireSprite` | heuristic on JSX signals (KEEP iter-1 core) | `<img>` child → true; `bg-gradient`/inline `linear-gradient` → true; any `border-[..]` stroke → true; `rounded-[N>0]` on non-text → true; plain `bg-[#hex]` solid + no border + radius 0 → false; text → false; ambiguous → **true** (fail-safe) |
| `color` | JSX `bg-[#hex]`/`border-[#hex]`/`text-[#hex\|white]` | `rgba()`→hex; `white`→`#FFFFFF`; gradient/multi-color → `""` (skip) |
| `fontSize` | JSX `text-[Npx]` | **÷ 1.2 AUTOMATED** (shell TMP convention); `-1` non-text |
| `fontWeight` | JSX `font-['Family:Weight']` / `font-{bold\|semibold\|medium\|normal}` | → `Bold\|SemiBold\|Medium\|Regular`; `""` non-text |

**Element selection (D4 unchanged):** emit rows for named frames carrying size/visual + text nodes; skip
anonymous wrappers (`Frame`, `Frame 9/10`, the outer container). Keep `--include`/`--min-size`.

**The one irreducible manual input — `--name-map`:** the linter matches spec `name` against the built
Unity GO name, which the tool cannot infer from a Figma name (`menu_whisky-flight`→`Thumbnail`,
`RP Container`→`RpPill`, etc.). Accept `--name-map <json>` = `{"<figma-name-or-node-id>":"<UnityGOName>"}`.
This is the sole remaining manual step (a tiny name lookup), not per-element geometry/style transcription —
that IS the automation Option B delivers. Document it clearly; without a map, emit Figma names + WARN.

**Regression anchor — re-derive FAITHFULLY (fixes the red-team blocker):** run the tool on the two
committed fixtures with a `--name-map` to the `StaminaMenuRow` GO names. The emitted spec must (a) match
the `requireSprite` decisions of the hand-authored `Docs/Specs/Completed/stamina_boost_shop/reference/nodes/row_spec.json`,
and (b) yield `fail:0` through `UIFidelityLinter.LintPrefab` against the SHIPPED `StaminaMenuRow.prefab`.
**No hand-edited fills:** RP Container stays SOLID `#001e39` radius-43 (→ `requireSprite=true` via the
radius branch — proving the heuristic is correct on faithful input, NOT a fake gradient); BuyButton's
border is on `Main Buttons` (13330:1194) + gradient on inner `Button Container` (13330:1195).

**Robustness:** build + test against **≥2 node samples** so the parser isn't overfit to one JSX shape —
the menu row (above) plus one more: capture the selection card `13156:1232` via `get_metadata` +
`get_design_context` into `reference/nodes/` and add it as a second regression fixture.

### iter-3 refinements 2026-07-04 (Cesar) — encode the human skip-judgment

iter-2 parses Figma faithfully but over-fails real prefabs (15 fails on the approved `StaminaMenuRow`)
because it checks things a hand-authored spec deliberately skips. A generated spec is only useful if it
encodes that judgment. Add these rules; they collapse all 15 iter-2 fails:

1. **Text nodes** (metadata `type=text` / JSX `<p>`): emit `w = h = -1` (skip — Unity TMP rects are
   container-sized, not glyph-box-sized) AND `requireSprite = false` **always**, even when the text has a
   `bg-clip-text` gradient fill (a text gradient is a TMP vertex-gradient, not a sprite; text has no Image
   for the linter's requireSprite check to pass). For text, only `fontSize` (÷1.2), `fontWeight`, and
   `color` are meaningful. iter-2's `TierLabel requireSprite=true` HARD FAIL is this bug.
2. **Sprite-filled elements** (`requireSprite = true`): emit `color = ""` (skip). The color is carried by
   the sprite; the Unity `Image.color` is white/tint, so a Figma-fill-color check false-fails every reused
   sprite pill/button (iter-2's `RpPill #001E39`, `BuyButton #422100` fails). Only emit a `color` check for
   FLAT-FILL elements (`requireSprite = false` solid backgrounds) where the Figma `bg-[#hex]` really maps to
   `Image.color`.
3. **Root / layout-wrapper containers:** emit `w = h = -1`. The outermost node and pure-layout frames are
   sized by their parent/layout, not intrinsically (iter-2's `MenuRow` root 994×160 vs the prefab's
   layout-driven size). The hand-authored `row_spec.json` checked geometry only on intrinsic-size atoms
   (image 124×124, pill/button 215×56, badge) — mirror that.
4. **`--name-map` must map to GO names that EXIST in the target prefab.** Don't emit spec rows for Figma
   nodes with no Unity counterpart (iter-2's `BuyButtonContainer`/`MenuRow` "missing"). Map or omit.

**Reframed acceptance (supersedes the iter-2 anchor):** after these rules, run the emitted spec (with the
`StaminaMenuRow` name-map) through `UIFidelityLinter.LintPrefab` against the SHIPPED `StaminaMenuRow.prefab`
and it must reach **`fail:0`** — proving the generator now produces a spec USABLE as a Rule-21 gate on an
approved prefab, not just faithful raw values. If any residual fail remains, itemize it and prove it is a
genuine prefab-vs-Figma delta, NOT a generator-logic error — no blanket "expected/correct." Add unit tests
for each new rule (text→w/h=-1 & requireSprite=false-even-with-gradient; sprite-element→color=""; root→w/h=-1).

## Reference — the exact schema to emit (from `Assets/Editor/UIFidelity/UIFidelityLinter.cs`)

The linter deserializes via `JsonUtility.FromJson<UISpec>`:

```json
{ "elements": [
  { "name": "MenuRow", "w": 994, "h": 160, "radius": 32, "requireSprite": true,
    "color": "#133453", "fontSize": -1, "fontWeight": "" },
  { "name": "ItemName", "w": -1, "h": -1, "radius": -1, "requireSprite": false,
    "color": "#FFFFFF", "fontSize": 28, "fontWeight": "Bold" }
]}
```

Field contract (MUST match the linter exactly):
- `name` (string) — element identifier the linter matches against the built GO name.
- `w,h,radius,fontSize` (float) — **sentinel `-1` = "do not check"**. Emit `-1` explicitly for any value
  the node doesn't determine. Do NOT omit fields — `JsonUtility`'s omitted-field behavior is fragile;
  every element MUST carry all 8 keys.
- `requireSprite` (bool) — `true` ⇒ a flat/null-sprite `Image` here is a HARD FAIL. This is the
  load-bearing field (it catches the fabricated flat-fill boxes the whole pipeline exists to stop).
- `color` (string) — expected `#RRGGBB` (rim / fill / text), or `""` to skip the color check.
- `fontWeight` (string) — one of `Bold | SemiBold | Medium | Regular`, or `""` to skip.

Input: a saved `get_design_context` JSON for the node (the implementer already pulls this at Rule 9 —
have them dump it to `reference/nodes/<Node>_context.json`). The generator reads that file.

## Locked decisions (my calls — override if you disagree)

- **D1 input:** consume the saved `get_design_context` JSON dump (path as CLI arg). NOT the Figma REST
  API directly — reuse what Rule 9 already fetches; keeps the tool offline + deterministic.
- **D2 language/location:** Python, `Docs/Scripts/figma_node_to_spec.py`, mirroring `figma_diff.py`. CLI:
  `python3 Docs/Scripts/figma_node_to_spec.py <context.json> -o reference/nodes/<Node>_spec.json`.
- **D3 `requireSprite` heuristic (TEST-CRITICAL):** `true` when the node element has an image/gradient
  fill, OR a visible stroke/border, OR a non-uniform/rounded corner treatment carried by a sprite;
  `false` for pure text nodes and plain uniform-solid backgrounds. This heuristic is the entire point —
  it needs the most test coverage. When genuinely ambiguous, emit `true` (fail-safe: better a false
  requireSprite the implementer justifies than a fabricated flat-fill that ships).
- **D4 which elements:** emit rows for named frames that carry size/visual, plus text nodes; skip
  anonymous wrapper/layout groups. Provide a `--include`/`--min-size` filter.
- **D5 color:** emit the element's dominant rim/fill/text hex; for multi-color/gradient elements where a
  single expected hex would be wrong, emit `color: ""` (skip) rather than a misleading value.

## Implementation

1. Parse the `get_design_context` JSON → a flat list of element descriptors (name, size, corner radius,
   fills[], strokes[], text{size,weight,color}).
2. Map each descriptor → a `UISpecElement`, applying D3/D4/D5. Emit all 8 keys with sentinels for unknowns.
3. Write `{ "elements": [...] }` to `-o` path (create parent dirs). Pretty-print, UTF-8.
4. `--verbose` prints a per-element decision trace (why requireSprite true/false) for auditability.

## Acceptance (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **Regression anchor:** run the generator on the shipped `stamina_boost_shop` menu-row node context;
      feed the emitted `spec.json` to `UIFidelityLinter` against the SHIPPED `StaminaMenuRow.prefab` and
      confirm it yields the **same pass/fail** as the hand-authored spec did (attach both JSONs + lint output).
- [ ] Unit tests: `requireSprite` heuristic (image-fill→true, uniform-solid→false, text→false, border→true,
      ambiguous→true); all 8 keys always present; sentinel `-1`/`""`/`false` emitted for unknowns;
      `fontWeight` maps to the 4 allowed values or `""`; color emits `#RRGGBB` or `""`.
- [ ] Valid JSON that `JsonUtility.FromJson<UISpec>` round-trips without error (no omitted fields).
- [ ] Runs offline from a saved context file; no network, no Unity dependency.
- [ ] `--verbose` decision trace present.

## Files

- `Docs/Scripts/figma_node_to_spec.py` — NEW.
- `Docs/Scripts/tests/test_figma_node_to_spec.py` — NEW (or the repo's existing python-test location).
- No `Assets/` changes. No Unity/scene/prefab edits.

## Out of scope

- Running the linter, or any pixel-diff (that's `UIFidelityLinter` / `figma_diff.py`).
- Live Figma REST calls (consume the saved `get_design_context` dump only).
- Auto-authoring the Element Reuse Map (Rule 22) — that stays a human judgment against the palette.

## Kickoff

```
Use the implementer subagent on "figma_node_spec_generator"
```
