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

The hardcoded fallback that mirrors the CSV got the same swap — and then got replaced
outright; see below.

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

## Follow-up in the same task — the fallback, and guards against it drifting again

Cesar: *"fix the fallback drift too"*, then *"and put guards so it does not keep drifting"*.

`AddFallbackModes()` — the path that runs when `Resources.Load` cannot produce modes.csv — was a
hand-built list of five `ModeData` objects: a second, independent model of the same rows, kept in
sync by whoever remembered. Nobody did. It had drifted three ways:

- no `tournaments` row at all (declined on purpose in `tournaments_mode_card` SPEC §87 — "if the
  CSV is missing we have bigger problems");
- `missions` still `locked = true, target = "none"`, six weeks after `missions_v1` unlocked it;
- `missions.rewards = 20` against the CSV's 35.

Had it ever fired, the player would have been shown a game that does not exist: a Coming Soon
Missions card and no Tournaments card.

**Fixed by removing the duplication, not by re-typing it.** The fallback is now
`ModesDatabaseCSV.FallbackCsv` — a verbatim copy of modes.csv as a string — parsed by
`LoadFromCSV` itself. There are no longer any fields to keep in sync, only one string that either
equals the file or does not. The copy was generated from the file, never transcribed. A side
benefit: the fallback now honours the content overlay and the withhold rule, which the hardcoded
list bypassed entirely.

**Guards (both new):**

| Guard | Where | Fires |
|---|---|---|
| `ModesFallbackCsvTests` | `Assets/Tests/EditMode/` | fast loop — line-by-line compare that names the drifted row, plus a well-formedness check (column count, unique ids, unique `order`) |
| `ModesFallbackBuildHook` | `Assets/Scripts/UI/ModeSelect/Editor/` | `IPreprocessBuildWithReport` — **fails the build** on divergence, modelled on `LocalizationBuildHook` |

And because a guard that only accuses gets skipped when the repair is hand-editing a string
literal, `Tools ▸ Golfin ▸ Modes ▸ Sync Fallback CSV` regenerates it in one click
(`Tools ▸ Golfin ▸ Modes ▸ Validate Fallback CSV` reports without writing).

`VersusResultHandler`'s `_fallbackReward` tooltip pointed at `AddFallbackModes()`; it now points at
the CSV row and says plainly that this Inspector value is *not* covered by the new guards.

## Verification of the follow-up

- Embedded copy proved **byte-identical** to modes.csv (1259 bytes both sides), using the same
  markers and normalisation the C# guard uses.
- Drift/repair round trip simulated against the real file: a CSV-only edit reads **DRIFTED** (build
  would fail), `Sync()` repairs it, and the repair touches **exactly one line**. This proves the
  Editor tool's anchors match the file as written.
- Compile-checked with Unity's own Roslyn, in dependency order, each stage pointed at the freshly
  built dll rather than the stale `Library/ScriptAssemblies` copy, and with the two NEW files
  appended (the `.csproj` is a snapshot): **Assembly-CSharp 0 errors, Assembly-CSharp-Editor 0
  errors, GolfinRedux.Tests.EditMode 0 errors.**

**Not verified:** neither guard has been *executed by Unity* — the Editor is owned by another
session, so the EditMode test has not been run and the build hook has not fired. Their logic was
verified out-of-process as described above, but a real `tests-run` is still owed.

## Also worth knowing

`.cs.meta` files for the two new scripts were hand-written with fresh GUIDs (Lesson R: always
commit the meta alongside the .cs). Unity will accept them on import.
