# Implementer Report — `content_two_way`

Iteration **1**. Implemented directly in the main Claude Code thread at Cesar's instruction
("read the SPEC and implement it, in the spec's §8 order"), not via the subagent chain.

**Iteration shape:** `content_pipeline:two_way_loop`

## Implementation summary

The client rail from §4 is in: `CharacterDataRuntime` / `ItemDataRuntime` / `BallDataRuntime` now
carry `renderable`, set by their own loader from the sprite resolution it already performs, and
`GetAvailable…()` is `isActive && renderable` while `GetAll…()` is untouched — so a row this build
cannot draw is absent from every visible list and still round-trips through the save and
`InventoryCodec`. Every `GetAll*` call site was enumerated by grep and the visible-list ones
switched; `GeneralShopCatalog.UnrenderableReason` now READS `renderable` instead of re-resolving,
so the shop rail and the game-wide rail cannot disagree. Clubs are untouched: the Placeholder
policy stands (799 rows, 150 on Placeholder, zero nulls — measured in play mode).

§5 adds `Assets/Editor/ContentArtValidator.cs` and a `CIBuild` step beside `ValidateTreeBake()`
that writes `Docs/Reports/content_art_<build>.txt` — **warning only, no failure path, no skip
flag**. §6 adds sprite-field hints (EN + JA) naming the exact `Resources/` folder per column, and
the amber banner on the Characters panel, reusing the Shop panel's component. §2 adds
`Tools/content/tests/` (stdlib `unittest`, a fake PostgREST client shared with the export suite)
including the round-trip property, and §3 adds the value-level half of `--check`: the DRAFT
decides whether the message is *"imported, not yet published"* or *"if you edited the CSV,
import; if not, export"*. Exit code unchanged.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` | modified — `renderable` field + set from `portraitSprite`, withheld-summary `LogWarning`, `GetAvailableCharacters` = `isActive && renderable` |
| `Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs` | modified — same, primary sprite = `thumbnailSprite` |
| `Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs` | modified — same, primary sprite = `thumbnailSprite` |
| `Assets/Scripts/UI/Inventory/ItemDataRuntime.cs` | modified — `public bool renderable = true` + why-doc |
| `Assets/Scripts/UI/Inventory/BallData.cs` | modified — `public bool renderable = true` on `BallDataRuntime` |
| `Assets/Scripts/CharacterManager.cs` | modified — roster seed (`:82`) reads `GetAvailableCharacters()` |
| `Assets/Scripts/ItemManager.cs` | modified — inventory seed (`:56`) reads `GetAvailableItems()` |
| `Assets/Scripts/BallManager.cs` | modified — bag seed (`:58`) reads `GetAvailableBalls()` (found by the §4 grep sweep; the ball counterpart of ItemManager) |
| `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` | modified — fallback opponent pool (`:256`) reads `GetAvailableCharacters()` |
| `Assets/Scripts/UI/Shop/GeneralShopModel.cs` | modified — `UnrenderableReason` reads `renderable` for ball/character/item; clubs keep the Placeholder branch |
| `Assets/Editor/ContentArtValidator.cs` | **created** — §5 report + `GOLFIN/Content/Validate Catalog Art` menu item |
| `Assets/Editor/CIBuild.cs` | modified — `ContentArtValidator.RunAndReport()` beside `ValidateTreeBake()`, wrapped so a report can never fail a build |
| `Assets/Editor/BuildStampGenerator.cs` | modified — `GitRevCount()` private → public, so the report files under the SAME number the binary will carry rather than deriving a second one |
| `Assets/Tests/EditMode/ContentRenderableTests.cs` | **created** — 4 tests driving the SHIPPING loader + SHIPPING roster seed by reflection |
| `Assets/Scripts/InventorySync/Tests/InventoryCodecTests.cs` | modified — `An_owned_but_unrenderable_character_survives_the_round_trip` |
| `Tools/content/export_content.py` | modified — §3 `value_direction_report` + `csv_values` + `fetch_drafts` (lazy), wired into `--check` |
| `Tools/content/tests/fakes.py` | **created** — `FakePostgrestClient`, shared by both suites |
| `Tools/content/tests/test_import_content.py` | **created** — 17 tests incl. the round-trip property |
| `Tools/content/tests/test_export_check.py` | **created** — 9 tests for the §3 half + the unchanged exit code |
| `Tools/content/README.md` | modified — tests row, the three `--check` questions, importer test coverage |
| `Docs/TESTFLIGHT_RUNBOOK.md` | modified — step 3 now names three drift directions and what to run for each |
| `Tools/admin-dashboard/lib/contentView.ts` | modified — `SPRITE_FIELD_FOLDER` + `spriteFolder()`, folder literals cited to the loader constants |
| `Tools/admin-dashboard/app/(panels)/_content/row-editor.tsx` | modified — per-column sprite hint under sprite fields |
| `Tools/admin-dashboard/app/(panels)/characters/characters-panel.tsx` | modified — amber banner (Shop panel's component) |
| `Tools/admin-dashboard/lib/i18n.ts` | modified — `c.edit.spriteHint`, `c.edit.spriteHintClubs`, `ch.notice.headline`, `ch.notice.body` (EN + JA) |
| `Docs/Reports/content_art_2361.txt` | **created** — the §5 report for the current HEAD, regenerated clean after the acceptance run |
| `Docs/Specs/Active/content_two_way/screenshots/*.png` | **created** — 3 play-mode frames, 1170×2532 |
| `Docs/Specs/Active/content_two_way/HEARTBEAT.log` | **created** — baseline + work log |

**Rule 13 sweep.** `git status --porcelain --untracked-files=all` carries exactly one path outside
this task folder that is NOT in the table above: `Docs/Versioning/last_uploaded_build.txt`. It was
`M` **before this task started** — it is in the git status captured at session open, quoted
verbatim in `HEARTBEAT.log`'s baseline block, and nothing in this task reads or writes it.

## Screenshot

- **Canonical screenshot:** `screenshots/roster_withheld_olivia.png` — the whole feature in one
  frame: `char_olivia`'s portrait renamed to a name Resources does not carry, and the Roster
  carousel now runs JAMES → RICHARD → … with **no Olivia card, owned or locked, and no gap**.
- **Supporting:** `screenshots/roster_baseline_12_of_12.png` (before) and
  `screenshots/roster_restored_12_of_12.png` (after restoring the name — Olivia is back).
- **Captured at:** 1170×2532, `mcp__ai-game-developer__screenshot-game-view` (Capture Rule 0).
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes — reached through the REAL player path: boot → Home (authenticated as
  Cratilo) → `NavCharactersButton.onClick.Invoke()`. No screen swap, no synthetic button.
- **Hole loaded:** n/a (menu surface)

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| **Round trip** — CSV edit → import → publish → export byte-identical, `--check` clean | PASS | Pinned as an automated property against the REAL `Assets/Data/Balls.csv`: `RoundTripProperty.test_the_loop_is_byte_identical` and `…test_a_value_edited_in_unity_survives_the_loop_in_canonical_form` (import → publish → export reproduces the edit and nothing else, and a second pass proposes 0 changes). The against-prod half of this bullet is §8 step 3 (throwaway text key) — **left to Cesar**, see § Spec deviations. |
| Import refuses the run on a dirty draft, names the rows, exit 1; `--overwrite-dirty` lets the CSV win with an audit row | PASS | `CliRefusal.test_a_dirty_draft_refuses_the_whole_run_and_writes_nothing` drives `main()` and asserts exit 1, "REFUSED", the row id, "--overwrite-dirty", and `client.writes == []` (the CLEAN row is not written either). `test_overwrite_dirty_applies_and_says_what_it_clobbered` asserts exit 0, "OVERWRITING", and the CSV value landing in the draft; `Applying.test_apply_…` asserts the audit row carries `via: import_content.py`. |
| A row deleted from a CSV is reported, not deactivated | PASS | `PlanVerdicts.test_a_row_deleted_from_the_csv_is_reported_never_deactivated`: `plan.catalog_only == ["ball_retired"]`, `plan.touched == 0`, and every `content_rows` row still `is_active == True`. |
| `--check` on a value-only CSV edit names the loop; after import "imported, not yet published"; clean after publish+export | PASS | Both branches unit-tested (`ValueDirection`, 6 tests) **and** fired live against prod: with `ball_putt_ace.power` edited 10→9 locally, `--check --catalogs balls` printed `balls: values differ from published for 1 row(s) (ball_putt_ace): if you edited the CSV, run import_content.py --apply then publish; if not, run the exporter.` and exited 1. CSV restored (`git diff` empty). The "imported, not yet published" branch is proven by test, not against prod, because proving it live requires writing prod drafts — see § Spec deviations. |
| Editor: rename a `portraitSprite` → no owned and no locked card; save still carries it if owned; shop withholds it; warning names it; restore → back | PASS | Measured in play mode. Before: `all=12 available=12 rosterCards=12 unrenderable=[]`. After renaming `char_olivia` → `Olivia_MISSING_two_way`: `all=12 available=11 rosterCards=11 unrenderable=[char_olivia]` and the carousel shows no Olivia card (canonical screenshot). Shop rail via the shipping `GeneralShopCatalog.UnrenderableReason`: `char_olivia → "no usable character portrait"`, `char_james → ADMITTED`. Warning, verbatim from `Editor.log:134839`: `[CharacterDatabaseCSV] 1 character(s) withheld (unrenderable — sprite missing in this build; ships when the art does): char_olivia`. Restored: `all=12 available=12 rosterCards=12 unrenderable=[]`, Olivia back in the carousel. "Save carries it if owned" is pinned by `An_owned_but_unrenderable_character_survives_the_round_trip` (the live save had no `char_olivia` record because that character is *locked*, not owned). |
| Clubs unchanged — a club with missing art still renders Placeholder in the bag | PASS | Measured in play mode: `clubs=799 placeholderPortraits=150 nullPortraits=0`, sample `club_driver_fairloft_common → Placeholder`. `ClubDatabaseCSV` has **zero** diff; `GeneralShopModel`'s club branch still uses `Usable()`/Placeholder-by-name. |
| `Validate Catalog Art` lists the renamed character; `CIBuild` still succeeds; the report file is written | PASS | With the rename in place the report read `── characters (12 row(s), 1 with missing art) / char_olivia portraitSprite Olivia_MISSING_two_way → withheld`; after restoring, `every sprite column resolves.` `Docs/Reports/content_art_2361.txt` exists (697 lines, build number from `git rev-list --count HEAD`). CIBuild: the step is `try`-wrapped with **no return path** — it cannot produce a failure message, so the build outcome is unchanged by construction. |
| Characters panel banner + sprite-field hints render in EN and JA | PASS | Rendered in a local `MOCK_MODE=1` dev server and read back out of the DOM. EN banner: *"Creating a character here creates its data. / Its art ships with the next build that bundles the sprites; until then it is withheld on every build — it appears in no roster, no shop and no pool, rather than showing as a blank card…"*; JA: *「ここでキャラクターを作成すると、そのデータが作成されます。…」*. Hints render per column with the right folder — items row editor showed `Resources/Items/Thumbnails/` under `thumbnailSprite` and `Resources/Items/Full/` under `fullSprite`, in both languages. |
| Full unfiltered EditMode sweep green; `python3 -m unittest discover Tools/content/tests` green; dashboard `npm run build` green | PASS | EditMode: **1857 tests, 1854 passed, 0 failed, 3 skipped** — the 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 skips, untouched by this task. All five new tests confirmed **by name** in the per-test results (4 × `ContentRenderableTests`, 1 × `InventoryCodecTests.An_owned_but_unrenderable_character_survives_the_round_trip`). Python: `Ran 26 tests … OK`. Dashboard: `npm run build` completed with the full route table. |

## Known FAIL items

None.

## Spec deviations

- **`CharacterManager.cs:99` (the ScriptableObject fallback roster seed) was left on `GetAllCharacters()`.**
  §4 named `:82/99` together, but `:99` reads the legacy `CharacterDatabase` ScriptableObject, whose
  `CharacterData` has no CSV sprite NAME to resolve and therefore no `renderable` to compute — its
  portraits are direct Inspector references. Adding a second, differently-derived renderability rule
  there would create exactly the two-rails-disagree problem §4 exists to close. The branch is
  unreachable whenever `CharacterDatabaseCSV.Instance` exists, which is every production boot.
- **`BallManager.cs:58` was switched although §4 did not name it.** §4 says to grep every call site
  first and that its list "is not guaranteed complete". `BallManager`'s seed is the exact shape of
  `ItemManager.cs:56` (seed from catalog → overlay saved quantities), and it feeds the bag. Saved
  quantities for any id are still restored in step 2, so nothing owned is lost.
- **`LabInventoryStub.cs:180` and `MatchmakingCaptureRunner.cs:127` were left on `GetAll…`.** Both
  are editor/lab diagnostics, not player-facing lists; the lab stub deliberately shows everything.
- **§8 step 3 (round-trip rehearsal against prod on a throwaway text key) was NOT run.** It requires
  writing `content_drafts` on prod and a human pressing publish in the admin. Both are Cesar's to
  authorise; the property is pinned automatically instead. The **read-only** halves were run against
  prod: import dry-run (below) and `export --check`, both clean.
- **The admin evidence is DOM text, not a `screenshots/*.png`.** The dashboard renders in a browser,
  not the Game View; the sanctioned capture path does not cover it. The rendered strings are quoted
  verbatim in the checklist instead.

## Import dry-run against prod (read-only)

```
catalog         add  change   same  conflict  csv
  clubs           0       0    799         0  Assets/Resources/Data/Clubs.csv
  characters      0       0     12         0  Assets/Data/Characters.csv
  items           0       0      3         0  Assets/Data/Items.csv
  bags            0       0     10         0  Assets/Data/Bags.csv
  balls           0       0      2         0  Assets/Data/Balls.csv
  texts           0       0    506         0  Assets/Localization/LocalizationText.csv
  shop_catalog    0       0      7         0  Assets/Resources/Data/shop_catalog.csv

Nothing to import — every CSV row already matches the catalog.
```

`export_content.py --check` against the same prod, immediately after: all seven catalogs
`unchanged`, `--check: clean — no file would change and no catalog has drifted.` (exit 0).

## Console output

The one new runtime line, fired only while the rename was in place:

```
[CharacterDatabaseCSV] Thumbnail sprite 'Olivia_MISSING_two_way' not found in Resources/Portraits/Thumbnails/
[CharacterDatabaseCSV] 1 character(s) withheld (unrenderable — sprite missing in this build; ships when the art does): char_olivia
```

Editor build-time line from the §5 validator (clean tree):

```
[ContentArt] 0 row(s) withheld, 373 club row(s) on Placeholder, 673 missing sprite reference(s) across 4 catalogs. Full list: Docs/Reports/content_art_2361.txt
```

No errors or exceptions attributable to this task.

## Open questions for Architect

- **The §5 report is committed as a build artifact.** `Docs/Reports/content_art_2361.txt` is 697
  lines, 673 of them the club Placeholder list, and CIBuild writes a new file per build number. That
  is what §5 asked for ("so the archive carries the list of what it withholds"), but it will add one
  such file per archive. If that is unwanted, the fix is one line — gitignore `Docs/Reports/content_art_*.txt`
  and let the artifact live only in the build output.
- **`tests-run` intermittently answers "No tests found"** on the first call after a domain reload,
  then succeeds on retry with identical arguments. Not caused by this task (it happened before any
  test file existed), but worth a line in `tasks/lessons.md` if it bites the subagent chain.
