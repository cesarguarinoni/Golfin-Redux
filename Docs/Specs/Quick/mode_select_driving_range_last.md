# Quick task — `mode_select_driving_range_last`

**Asked by Cesar, 2026-09-09:** *"In mode selection screen, move Driving Range to the bottom
since it is the only one locked still."*

## What changed

The full-screen Mode Select list (`ModeSelectScreenController`, a vertical list) renders
`ModesDatabaseCSV.GetAllModes()`, which is sorted by the `order` column of
`Assets/Resources/Data/modes.csv`. Driving Range sat at `order=4` with Missions below it at
`order=5`, so the one Coming Soon card was sandwiched between two playable ones.

Swapped the two:

| id | order before | order after | locked |
|---|---|---|---|
| `versus_1v1` | 1 | 1 | |
| `practice` | 2 | 2 | |
| `tournaments` | 3 | 3 | |
| `missions` | 5 | **4** | |
| `driving_range` | 4 | **5** | **true — the only locked mode** |

Nothing else moved: no titles, fees, rewards, targets or `locked` flags were touched.

`ModesDatabaseCSV.AddFallbackModes()` (the hardcoded mirror used only when the CSV fails to
load) got the same swap so it does not disagree with the CSV.

## Published, not just edited

`modes` is a content catalog, so the published rows OVERLAY the bundled CSV at runtime — a
CSV-only edit would have left the old order winning on device. Full loop run:

- `import_content.py --catalogs modes` → PLAN `0 add / 2 change / 3 same / 0 conflict`
- `--apply` → 2 drafts written
- `golfin_mode_fees` re-mirrored from the drafts *before* publishing (the ordering
  `contentMutations.mirrorModeFees` uses). `entryFee` / `locked` are unchanged by this edit,
  so it was an idempotent re-upsert of the same five rows.
- `content_publish` → **modes v10 → v11**
- `export_content.py` → `content_version.txt` `modes=10` → `11`; `--check` **clean**

Published card order now reads, top → bottom: 1v1, Practice, Tournaments, Missions, Driving
Range (LOCKED). Bundled CSV agrees.

## Verification

- Published `content_rows` re-read after the publish and sorted by `order` — Driving Range last.
- `export_content.py --check` exits 0 ("clean — no file would change, no catalog has drifted").
- `Assembly-CSharp` compile-checked with Unity's own Roslyn (312 sources, 387 refs, 33 project
  refs): **0 errors**. The Editor was NOT touched — another session is driving it, so the
  Editor-free path (`reference_compile_check_without_unity`) was used instead of Unity MCP.

## Known pre-existing drift, NOT fixed here (out of scope)

`AddFallbackModes()` disagrees with the CSV in three ways that predate this task and that this
change deliberately left alone:

- it omits `tournaments` entirely;
- `missions` is `locked = true, target = "none"` there but `locked=false, target=mission_select`
  in the CSV (stale since `missions_v1` unlocked the mode);
- `missions.rewards` is 20 there vs 35 in the CSV.

The fallback only runs when `Resources.Load` of the CSV fails, so none of this is reachable in a
shipped build — but it is worth a follow-up.
