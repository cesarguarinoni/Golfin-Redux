# SPEC — mode_card_inspector_colors

**Type:** Quick / small (could run as a Quick task or a light pipeline pass)
**Effort:** ~1–2 hrs
**Restore point:** `restore/mode_select_working_2026-06-05` (tag) + `Docs/Backups/mode_select_working_2026-06-05/`
**Surfaces:** `ModeCard.prefab` (full-screen) + `ModeHomeCard.prefab` (home carousel)

## Problem

The mode card's state-driven colours are **hardcoded `static readonly Color`
constants** in `ModeCardController.cs`, so Cesar can't retune them in Unity — any
colour change needs a code edit + recompile. The card's *sprites* are already
`[SerializeField]` fields (editable), but the colours aren't:

- `BorderActive`   = white `#FFFFFF`            (active/selected border tint)
- `BorderInactive` = `#3E7CA8`                  (inactive/collapsed border tint)
- `TitleColorActive`    = gold `#EEDC9A`        (selected card title)
- `TitleColorCollapsed` = silver `#D1D5DB`      (inactive/locked title)
- `InsufficientRpColor` = `#C04000`             (entry-fee text when unaffordable)

These are consumed in `RefreshCenterVisuals()` (border + title) and
`RefreshFeeColor()` (fee). See the position-driven colour logic added 2026-06-05.

## Goal

Make every card colour editable **per-prefab in the Inspector**, with the current
values as defaults — zero behaviour change unless Cesar edits a value.

## Scope / changes

1. **`ModeCardController.cs`** — convert the five colour constants from
   `static readonly Color` to instance `[SerializeField] private Color` fields under
   a `[Header("Colours (§6.2)")]`, keeping the **exact current default values**.
   Update `RefreshCenterVisuals()` / `RefreshFeeColor()` references (drop `static`).
   - Keep `NormalWhite` as-is (it's plain white; optional to expose).
   - Do NOT change any logic — only the source of the colour values.

2. **Both prefabs** — because Unity serializes `[SerializeField]` defaults into the
   prefab at the moment the field is added, confirm each prefab's new colour fields
   hold the intended defaults (gold/blue/silver/white/#C04000). If Unity leaves them
   at `(0,0,0,0)` (can happen when a field is added to an already-serialized
   component), set them via **sanctioned MCP only**
   (`SerializedObject.FindProperty(...).colorValue` → `ApplyModifiedProperties` on
   `LoadPrefabContents`/`SaveAsPrefabAsset`). **Never raw-edit the `.prefab` YAML.**

## Acceptance criteria

- [ ] The five colours appear as editable Color swatches in the Inspector on both
      `ModeCard` and `ModeHomeCard`, grouped under a "Colours" header.
- [ ] Defaults exactly match today: border active `#FFFFFF`, border inactive
      `#3E7CA8`, title active `#EEDC9A`, title collapsed `#D1D5DB`, insufficient-RP
      `#C04000`. (Verify with a sampled play-mode capture: selected card title still
      gold, side cards silver, inactive border blue, white border on selected.)
- [ ] With defaults unchanged, the home + full-screen cards render **pixel-identical**
      to the restore point (compare against the approved screenshots in
      `Docs/Specs/Completed/mode_select_system/screenshots/`).
- [ ] Sanity edit: set `TitleColorActive` to red on `ModeHomeCard`, enter play mode →
      the selected card's title is red; revert.
- [ ] `script-execute` compile check passes; no new console errors.

## Out of scope

- The dim-overlay colour/alpha (already a normal Image on the prefab → editable).
- The panel border sprites (already `[SerializeField]`).
- Any layout/animation change.

## Verification

iPhone 14 (1170×2532) over the loaded Home + ModeSelection screens, defaults-kept
capture compared to the approved set; plus the red-title sanity edit.
