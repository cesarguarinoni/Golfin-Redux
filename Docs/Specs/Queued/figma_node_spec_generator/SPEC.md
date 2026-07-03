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
