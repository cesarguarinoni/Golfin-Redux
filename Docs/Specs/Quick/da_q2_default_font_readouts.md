# Quick · `da_q2_default_font_readouts` — 41 stat readouts still on Unity's default font

**From:** `design_consistency_audit` § 3.2 (fix group Q2), approved by Cesar 2026-09-06. **Est:** S.

## What is wrong

41 `TextMeshProUGUI` labels are bound to `LiberationSans SDF` (`8f586378b4e144a9851e7b34d9b748ee`),
Unity's default, which is never a design token. All are stat readouts ("50/100", "9/25", "228 yd",
"STRENGTH") at 33 px rendered (16–18 px inside the card prefabs):

| Where | labels |
|---|---|
| ShellScene `InventoryScreen` | 27 |
| ShellScene `RosterScreen` (incl. the inactive Compare panel) | 8 |
| ShellScene `SettingsScreen` (inactive UserProfile submenu) | 1 |
| `Assets/Prefabs/UI/Roster/CharacterThumbnailCard.prefab` | 3 |
| `Assets/Prefabs/UI/Roster/StatBar.prefab` | 2 |

Exact paths: `Docs/Diagnostics/_capture/design_audit/{InventoryScreen,RosterScreen,SettingsOverlay}__en.json`
(filter `font` contains `Liberation`) and the two `PREFAB_*` dumps. One label is EMPTY today and
will render in Liberation the moment it gets text — fix it too.

## Fix

- Font asset → `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` (`0e84913c86a5b7f4881cb73d5e80728f`)
  on every one of the 41 (the body/label face per `Docs/Design/DESIGN_TOKENS.md`; these readouts
  sit beside Rubik-SemiBold headings — Medium/Regular is right for a value, SemiBold for a heading;
  if a readout's sibling label on the same row is SemiBold, match the row and say which).
- Do it with an Editor script (`Assets/Editor/UIFidelity/` or beside `GamePolishBuilder`) using
  `SerializedObject` + `RecordPrefabInstancePropertyModifications` for the scene objects, prefab
  edits for the two prefabs — **not by hand**, so the report can quote the site list the script
  changed. `m_sharedMaterial` must follow the font asset (TMP does this when `font` is set through
  the property; verify one label's material GUID in the YAML afterwards).
- Rendered size stays what it is: a font swap changes glyph metrics, so re-check that no readout
  now clips or wraps (the audit's tripwire saw an auto-sized label move 49.05 → 51 from a font
  swap alone) — `autoSize` labels keep their min/max, fixed ones keep `fontSize`.

## Done when

- `Docs/Scripts/audit_numbers.py` reports `LiberationSans (in-screen) 0 (+0 …)` after re-running
  the dumper on Inventory (all four tabs), Roster (incl. Compare), Settings (UserProfile open) and
  the two prefabs — the re-dump command quoted.
- `grep -c "guid: 8f586378b4e144a9851e7b34d9b748ee" Assets/Scenes/ShellScene.unity Assets/Prefabs/UI/Roster/*.prefab` → 0 0 0.
- One still per surface (Inventory Clubs tab, Roster detail, Roster Compare, Settings UserProfile)
  beside the pre-change capture from the audit's `screenshots/`; no clipped or wrapped readout.
- Lint delta zero on `StatBar.prefab` and `CharacterThumbnailCard.prefab`. EditMode sweep green.
