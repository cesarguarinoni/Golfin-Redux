# SPEC — `content_two_way`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-27 (Architect via Cowork). Cesar's requirement (2026-08-27): *"create
> characters, clubs, etc. in the admin and have them inform the next build; and if I add them
> directly in Unity via CSV, have that inform the admin"* — with the standing invariant that a
> client missing any information never shows a broken item and never wrongly spends RP.
> Decisions of record 2026-08-27: **clubs keep the Placeholder-art policy; characters, items
> and balls with unresolvable art are withheld everywhere**; **the CSV import refuses a row that
> clashes with an unpublished admin draft** (`--force` overrides, per catalog).
>
> Follows `shop_server_purchase` + `shop_stocking` (both DONE). Plan: `CONTENT_PIPELINE_PLAN.md`
> I3 (admin upstream of the CSV) and §10.2 (art by URL — deliberately NOT here, see §7).

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Close the loop in both directions with **one truth**: the published catalogs in Supabase.

- **Admin → build** already works for the data: `+ New row` (shop_stocking §2) creates the row,
  publish makes it live, `export_content.py` rewrites the repo CSV, the fastlane gate refuses a
  stale repo. What is missing is the *art half* and the runtime rail: a character created in the
  admin has no sprites until someone drops them into `Resources/` and builds, and today a
  **bundled** character/item/ball whose sprite is missing renders with a null image
  (`CharacterDatabaseCSV.FindSpriteByName` / `ItemDatabaseCSV.LoadSprite` /
  `BallDatabaseCSV` — warn and continue). Overlay rows are already guarded
  (`ContentSpriteGuard`: appended row without art → dropped; patched row → bundled fallback);
  bundled rows are not.
- **CSV → admin** landed mid-`shop_stocking` as `import_content.py` (§2) — untested, and
  `--check` still cannot say who is newer when an existing row's *values* differ (§3).

Rule that resolves every conflict in this spec: **a CSV edit is a proposal. It becomes a draft;
a human publishes it; the export then rewrites the CSV in canonical form.** Published Supabase
never loses to a file.

---

## 1. What is true today (verified 2026-08-27)

| Piece | State |
|---|---|
| `+ New row`, all catalogs | shipped (`shop_stocking` §2) |
| `Tools/content/{catalogs,rest,seed_from_csv,export_content,import_content}.py` | export rewrites in place, preserves order/comments, appends `is_active` column only when a row is inactive; `min_build` is **not** in any CSV (server-only); `--check` = "would change OR id-set drift" → exit 1, id-level direction named; **import exists** (`0e4fedcaa`), no tests |
| fastlane `testflight_build` | runs `export_content.py --check`, aborts on non-zero (`shop_stocking` §5) |
| `golfin_characters` mirror on `characters` publish | shipped (`lib/contentMutations.ts` ~L179–253, fails the publish if the mirror write fails) |
| `ContentSpriteGuard` (`Assets/Scripts/ContentRuntime/`) | vetoes **overlay** rows whose sprite names do not resolve, in all five DB loaders |
| Bundled rows, missing sprite | clubs → `Placeholder` (deliberate, `ClubDatabaseCSV.LoadSprite`); characters / items / balls → `null` sprite, row still served |
| Roster | `RosterScreenController` ← `CharacterManager.GetAllOwnedCharacters()` ← `CharacterDatabaseCSV.GetAllCharacters()` at `CharacterManager.cs:82` — no renderability filter; `isActive` is checked only for starter candidates (`:379`) |
| Unity build gates | `CIBuild.ValidateTreeBake()` is the pattern (fails the build with a named report) |

---

## 2. `Tools/content/import_content.py` — EXISTS (`0e4fedcaa`, landed during `shop_stocking`); accept it, add the tests

Verified 2026-08-27: Code built the importer when the first release-lane gate found five
`SETTINGS_*` text keys sitting in the CSV with no catalog row. It already is the §2 this spec
was going to ask for — plan-only by default, `--apply`, `--catalogs`, `--by`, an audit row,
ADD / CHANGE / same, never `content_rows`, never deletes, and it **refuses the whole run** when
any draft is mid-edit (`--overwrite-dirty` overrides). Two things to record, one to change:

- **Accepted as built — refuse the run, not the row.** Cesar's decision was "refuse and name
  the row"; Code refused the whole run instead, on the argument that a half-applied import is a
  state nobody can reason about. That is the stricter reading of the same decision and it stands.
  The flag name `--overwrite-dirty` stays.
- **Accepted — `--min-build` defaults to `git rev-list --count HEAD + 1` for ADDED rows.** This
  is a *lower bound* on the next archive's build number (the count only grows), so it is safe in
  the direction that matters — unlike the shop banner's "last upload + 1", which needed the
  exact value. Rows added by import are therefore invisible to every installed build until the
  next archive, which is exactly right for a row whose art is in the same commit.
- **Change: tests.** `Tools/content/tests/` does not exist. Add
  `test_import_content.py` (stdlib `unittest`, a fake `PostgrestClient` small enough for
  `export_content` to share): ADD / CHANGE / same / absent-from-CSV-reported / dirty-draft
  refuses the run / `--overwrite-dirty` / `is_active` column round-trip, and the **round-trip
  property**: import → simulated publish → export leaves a canonical CSV byte-identical.

`Tools/content/README.md` already documents it? — check; if the loop diagram lacks the import
arrow, add it.

## 3. `--check` — id-level direction exists; add the value-level half

`export_content.drift_report` already distinguishes *"in the CSV, not in the catalog — re-seed /
import"* from *"in the catalog, not in the CSV — export"* **by id**. Value differences on an
existing id fall through to `write_if_changed` and read as "would change", which cannot say who
is newer. Add, for each id whose CSV values differ from published:

- a draft exists and **equals the CSV** → *"imported, not yet published — publish `<catalog>`
  in the admin."*
- otherwise → *"values differ from published for `<n>` row(s) (`<sample>`): if you edited the
  CSV, run `import_content.py --apply` then publish; if not, run the exporter."*

Exit code unchanged (1 on any difference). `Docs/TESTFLIGHT_RUNBOOK.md` and the README gain the
sentence. The fastlane gate then tells Cesar which loop to run instead of only that one is needed.

## 4. Client — bundled rows the build cannot render are withheld

The invariant, applied to the whole game rather than the shop. **Clubs are excluded by decision
(Placeholder policy stands).**

- `CharacterDataRuntime`, `ItemDataRuntime`, `BallDataRuntime` gain `public bool renderable`
  (default true), set at load: **false when the primary sprite is null** —
  `portraitSprite` for characters (full-body missing is a warning, not a veto, since the Roster
  card needs the thumbnail first), `thumbnailSprite` for items and balls. Use the same
  resolution the loader already performs; do not add a second `Resources.Load`.
- `GetAvailableCharacters()` / `GetAvailableItems()` / `GetAvailableBalls()` become
  `isActive && renderable`. `GetAll…()` is untouched (owned-but-unrenderable rows must still
  round-trip through the save and the inventory blob — a player must never *lose* a granted
  character because its art is late; they just cannot see it yet).
- Consumers that build a *visible* list from `GetAll…` switch to `GetAvailable…`:
  `CharacterManager.cs:82/99` (the roster seed — locked cards included, so an unrenderable
  character shows neither as owned nor as locked), `MatchmakingModalController.cs:256`,
  `ItemManager.cs:56`. `CharacterManager.cs:566` and `:379` — read them; if they are
  ownership/starter bookkeeping keep `GetAll`. NOTE: enumerate every `GetAllCharacters()` /
  `GetAllItems()` / `GetAllBalls()` call site with grep before deciding; the list above is what
  was found on 2026-08-27 and is not guaranteed complete.
- Summary log per loader, same shape as the club loader's missing-art line: count + first
  12 ids, `LogWarning`, once per load. Text: *"withheld (unrenderable — sprite missing in this
  build; ships when the art does)"*.
- Shop: `GeneralShopCatalog.Admit` (shop_stocking §6) already resolves sprites itself; make it
  read `renderable` instead of re-resolving, so the two rails cannot disagree.

Tests (EditMode): a character row whose `portraitSprite` name resolves to nothing → `renderable
= false`, excluded from `GetAvailableCharacters`, present in `GetAllCharacters`; an owned
unrenderable character survives `InventoryCodec` encode/decode; the roster seed skips it.

## 5. Unity build gate — `Validate Catalog Art` (report, never fail)

`Assets/Editor/ContentArtValidator.cs` + a `CIBuild` step beside `ValidateTreeBake()` and a
menu item `GOLFIN/Content/Validate Catalog Art`. For each bundled CSV row of characters, items,
balls and clubs: does every sprite column resolve under its `Resources` folder? Output one
report: per catalog, rows with missing art, which column, and — for clubs — "Placeholder" vs
"withheld" for the rest. **Warning only.** A character whose data is published but whose art
lands next week is a legitimate state that §4 makes safe; failing the build for it would
recreate the "validator gets switched off" problem. Write the report to
`Docs/Reports/content_art_<build>.txt` so the archive carries the list of what it withholds.

## 6. Admin — two hints, no new control

- `RowEditor`: for `characters` / `items` / `balls` / `clubs`, the sprite-name fields get a hint
  (EN + JA): *"Must match a file under `Resources/<folder>/` in the build. Rows whose art is
  missing are withheld on that build (clubs show Placeholder)."* Folder per catalog from the
  loader constants (`ThumbnailResourcesPath` etc. — copy the literal strings, note their source).
- Characters panel banner (amber, same component as the shop's): *"Creating a character here
  creates its data. Its art ships with the next build that bundles the sprites; until then it is
  withheld on every build."*

## 7. Deliberately not here → next spec `content_art_urls`

Art by URL (plan §10.2: `portraitUrl` / `fullUrl` / `controlUrl` on rows, served through a
second `TournamentArtService` instance, uploaded from the admin like banners) is what makes an
admin-created character **render on an installed build with no store release**. It is a
separate spec because it is the larger change and this one is correct without it: with §4, the
admin can create the row today, the next build bundles the art, and nothing in between is
broken. `LevelUpCosts` as a catalog (plan §9.2) and the gacha prize pool are also out.

## 8. Sequencing

1. §4 + §5 + §6 (Unity + admin) — one commit set; EditMode sweep.
2. §2 tests + §3 (tooling) — second commit; run the import dry-run against prod and paste
   the plan (expected: nothing to do after the last export).
3. Round-trip rehearsal (§9, item 1) on a throwaway text key before any real use.
4. Next TestFlight carries §4; `content_art_urls` is filed when Cesar says.

## 9. Acceptance

- [ ] **Round trip.** Edit one value in `LocalizationText.csv` in Unity → `import_content.py
      --apply` → the Texts panel shows exactly one changed row in the publish drawer → publish →
      `export_content.py` → the CSV is byte-identical to the edit (canonical form) and `--check`
      is clean.
- [ ] Import refuses the run when any draft is mid-edit, names the rows, exit 1;
      `--overwrite-dirty --catalogs texts` lets the CSV win, audit row attributes it.
- [ ] A row deleted from a CSV is reported, not deactivated.
- [ ] `--check` on a value-only CSV edit prints the "if you edited the CSV, import" line;
      after the import, "imported, not yet published"; after publish + export, clean.
- [ ] Editor: rename a character's `portraitSprite` to a name that does not exist → the roster
      shows neither an owned nor a locked card for it; the save still carries it if owned; the
      shop withholds it; the warning names it. Restore the name → it is back. *(Editor only —
      Cesar's standing rule.)*
- [ ] Clubs unchanged: a club with missing art still renders Placeholder in the bag.
- [ ] `Validate Catalog Art` lists the renamed character; `CIBuild` still succeeds; the report
      file is written.
- [ ] Characters panel banner + sprite-field hints render in EN and JA.
- [ ] Full unfiltered EditMode sweep green; `python3 -m unittest discover Tools/content/tests`
      green; dashboard `npm run build` green.

## Out of scope

- Art by URL / admin art upload for catalog rows (`content_art_urls`).
- `LevelUpCosts`, gacha pool, bot CSVs as catalogs; the remaining server mirrors
  (`golfin_bot_*`, `golfin_fake_players`).
- Any change to the content endpoint, the shop endpoint, or the inventory blob.
