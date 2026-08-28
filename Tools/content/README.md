# `Tools/content` — the CSV ↔ Supabase content pipeline

Three scripts, the shared mapping between them, and a test suite. Together they
are invariant
**I3** from `Docs/CONTENT_PIPELINE_PLAN.md` §2: *the admin is upstream of the
CSV, not downstream.* Without the round trip the delta grows forever and the
bundled floor rots.

```
Assets/**/*.csv  ──seed_from_csv.py────►  content_rows / content_drafts   (once, day one)

Assets/**/*.csv  ──import_content.py──►  content_drafts  ─┐   a CSV edit is a PROPOSAL
                                                          │
                                                   admin publishes
                                                          │
Assets/**/*.csv  ◄──export_content.py────────────────────┘   + content_version.txt
```

The round trip closes in BOTH directions. Published Supabase is the single truth;
the importer only ever writes drafts, so an edit made in Unity is a proposal
until somebody publishes it.

| File | What it is |
|---|---|
| `catalogs.py` | The catalog ↔ CSV table, and the CSV reader that keeps line layout. Run it directly to print the current row counts. |
| `rest.py` | Stdlib PostgREST client. Service key from the environment. |
| `seed_from_csv.py` | Repo CSVs → `2026_08_24_content_seed.sql` (and `--apply` runs it). |
| `export_content.py` | Published Supabase rows → the nine repo CSVs + `content_version.txt`. |
| `import_content.py` | Repo CSVs → `content_drafts`, as a proposal. The fix for the one drift direction the exporter cannot repair. |
| `tests/` | `python3 -m unittest discover Tools/content/tests` — stdlib only, no network. A fake PostgREST client (`tests/fakes.py`) stands in for Supabase, shared by the import and export suites. |

## The catalogs

Nine, and `catalogs.py` is the one place they are listed. Two of them are read by
the SERVER as well as by the game, which is what makes an edit there a price
change rather than a display change:

| Catalog | CSV | Also read by |
|---|---|---|
| `clubs` | `Assets/Resources/Data/Clubs.csv` | |
| `characters` | `Assets/Data/Characters.csv` | tournament rarity gates, via the `golfin_characters` mirror |
| `items` | `Assets/Data/Items.csv` | |
| `bags` | `Assets/Data/Bags.csv` | |
| `balls` | `Assets/Data/Balls.csv` | |
| `texts` | `Assets/Localization/LocalizationText.csv` | |
| `shop_catalog` | `Assets/Resources/Data/shop_catalog.csv` | `POST /shop/purchase` prices from the published rows |
| `level_up_costs` | `Assets/Data/LevelUpCosts.csv` | `golfin_level_up()` sums `cost_r` over the published rows |
| `modes` | `Assets/Resources/Data/modes.csv` | `POST /points/spend` prices a mode entry, via the `golfin_mode_fees` mirror |

Adding one is a row in `CATALOGS` plus a scoped seed:

```
python3 Tools/content/seed_from_csv.py --catalogs <name> --out ../playlife/backend/migrations/<date>_content_<name>_seed.sql
```

Day-one parity is exact by construction: the first `export_content.py --catalogs <name>`
after seeding must leave the CSV **byte-identical**. If it does not, the mapping
is lossy and every later phase inherits it.

## ⚠️ After ANY live publish, rollback or E2E — re-export

A publish or a rollback bumps `content_catalogs.published_version`. The repo's
`Assets/Resources/Data/content_version.txt` is the BUNDLED CURSOR — what a fresh
install starts from — and it does not move on its own. So verifying something
against prod silently desyncs it, and the symptom is invisible until somebody
runs `--check`.

This has bitten twice on one task (`game_modes_admin`: caught at iter-1, missed
at iter-2 and failed by the reviewer). Both times the CSV was fine and only the
cursor was stale, which is exactly what makes it easy to miss — nothing looks
wrong.

**The rule: any run that publishes or rolls back, including a throwaway
verification you intend to undo, ends with**

```
python3 Tools/content/export_content.py --catalogs <name> --env-file <dotenv>
git add Assets/Resources/Data/content_version.txt
```

**and then `--check` must exit 0 before you call the work done.** A rollback does
NOT restore the cursor — it publishes forward, so the version is higher than
before you started even though the content is identical. That is the counter
working; the cursor still has to follow it.

`--check` is the mechanical guard and it is cheap. Run it at close-out, not only
when you suspect something.

## Credentials

All three scripts read the environment and **never** hold a key in the repo:

```
SUPABASE_URL                https://<ref>.supabase.co
SUPABASE_SERVICE_ROLE_KEY   the service_role key   (SUPABASE_SERVICE_KEY also accepted)
```

`--env-file <path>` sources a dotenv file instead. The dashboard's gitignored
env file already carries both, which is the shortest path on Cesar's Mac:

```bash
python3 Tools/content/export_content.py --env-file Tools/admin-dashboard/.env.development.local
```

The service key bypasses RLS. `content_*` has RLS on with **zero policies**, so
service_role is the only way in — that is the intended posture
(`2026_08_24_content_catalog.sql`), not a workaround.

## Seeding (once, already done for prod on 2026-08-25)

```bash
python3 Tools/content/seed_from_csv.py                       # write the .sql only
python3 Tools/content/seed_from_csv.py --apply --env-file …  # write it AND apply it
```

Writes `playlife/backend/migrations/2026_08_24_content_seed.sql`: `INSERT …
ON CONFLICT DO NOTHING` into `content_rows` **and** `content_drafts`, all at
`version = 1`, a v1 snapshot into `content_versions`, and
`content_catalogs.published_version = 1`. Deterministic — re-running after a CSV
edit produces a reviewable diff, not a reshuffle.

`--apply` performs the identical statements over PostgREST so the seed does not
need 1.3 MB pasted into the Supabase SQL editor. **DDL still goes through the
SQL editor** (`WORKFLOW_NOTES.md`); this touches data only.

## Exporting (before every release build)

```bash
python3 Tools/content/export_content.py --env-file …            # rewrite in place
python3 Tools/content/export_content.py --env-file … --check    # exit 1 if stale
python3 Tools/content/export_content.py --env-file … --catalogs texts
```

`--check` writes nothing and exits 1 when the repo is behind the published
catalogs. That is the CI-shaped form: a release build whose CSVs are stale ships
a bundled floor that disagrees with the server, and the client then downloads a
delta it should never have needed.

**It also says which loop to run.** A failing `--check` used to mean "something
differs", which is the same message whether somebody published without exporting
or edited a CSV without importing — and the fixes are opposite. So `--check`
reports three things:

* **by file** — this export would rewrite N files (the repo is behind a publish).
* **by id** — an id one side has and the other does not, naming the direction
  (`drift_report`).
* **by value** (content_two_way §3) — for an id BOTH sides carry, under
  `CSV-vs-published VALUE differences — which loop to run:`. The DRAFT decides:
  a draft that already equals the CSV means *"imported, not yet published —
  publish `<catalog>` in the admin"*, and anything else prints both branches
  (*"if you edited the CSV, run `import_content.py --apply` then publish; if not,
  run the exporter."*). Exporting in the first case would silently overwrite the
  imported edit with the still-published value, which is why the line exists.

The exit code is unchanged — 1 on any difference. The extra `content_drafts`
query is made only for a catalog that actually has a value difference.

## Two things the exporter does that are worth knowing

**It rewrites the existing file; it does not regenerate one.** The catalog has no
sort column (deliberate — plan §3), so the repo CSV stays the authority on row
ORDER and on line LAYOUT, and Supabase is the authority on VALUES. Comment lines
pass through in place — Clubs.csv's three leading `#` lines *and*
LocalizationText.csv's mid-file one. New rows are appended, sorted, at the end.

**An unchanged line is emitted byte-for-byte.** `Items.csv`, `Balls.csv` and
`LocalizationText.csv` quote several fields that contain no comma, which
`QUOTE_MINIMAL` would not quote. Re-emitting every line would produce a phantom
diff on the first run. Only a line whose values actually changed is re-quoted.

Together those two are why the acceptance test — seed, export, `git diff` — comes
back empty.

## Importing (when a CSV edit needs to reach the admin)

```bash
python3 Tools/content/import_content.py --env-file …                    # PLAN only (default)
python3 Tools/content/import_content.py --env-file … --apply            # write the drafts
python3 Tools/content/import_content.py --env-file … --catalogs bags --apply
```

**Drift has two directions and only one is self-healing.** *Repo behind catalog*
(a row was published and never exported) is what the exporter fixes. *CSV ahead
of catalog* — a row added in Unity and never created in the admin — was
unfixable: the exporter never deletes (I6), so it keeps the extra line verbatim,
reports "unchanged", and the drift persists. That is what this script is for. It
was written after five `SETTINGS_QUALITY_*` keys sat outside the `texts` catalog
for weeks and were finally caught by the release lane, at archive time.

It **plans by default** and writes nothing without `--apply`, and it writes
`content_drafts` only — never `content_rows`. Publish is still the gate, and the
publish drawer still shows the diff.

Three things it refuses to do:

* **Delete.** A row in the catalog but not in the CSV is reported and left alone
  (I6). Deactivating in the admin is the delete.
* **Clobber an in-flight admin edit.** If a draft already differs from published,
  somebody is mid-edit; those rows are CONFLICTS, and `--apply` refuses the whole
  run — not just the row — unless `--overwrite-dirty` says otherwise, in which
  case every clobbered row is named in the output.
* **Guess a `min_build` low.** A row being ADDED gets
  `git rev-list --count HEAD` + 1, the first build number that can contain the
  commit its CSV line is in. Too high is benign (the build renders it from its own
  bundled floor); too low ships a row to clients that cannot render it. Override
  with `--min-build`. CHANGED rows never have theirs touched — it is immutable
  once published (§D1.7).

Covered by `tests/test_import_content.py` (content_two_way §2): the three
verdicts, both `min_build` rules, the refusal and `--overwrite-dirty`, the
`is_active` column, "never `content_rows`", and the **round-trip property** —
import → publish → export leaves the CSV byte-identical, including after a value
edited in Unity.

## Texts

`Assets/Localization/LocalizationTextTable.asset` is regenerated from the CSV
**automatically** — verified in the repo, not assumed:

* `Assets/Localization/Editor/LocalizationBuildHook.cs` —
  `IPreprocessBuildWithReport`, `callbackOrder = -100`, calls
  `LocalizationTextImporter.ImportCsv` at the start of **every build** and fails
  the build outright if the CSV is missing.
* `LocalizationPlaymodeHook` — same import on `ExitingEditMode`.
* `Tools ▸ Localization ▸ Import Text CSV` — the manual form, if you want to see
  the table update without building or entering Play.

So an export that touches texts needs no extra step before a release build. The
one caveat is Unity's importer: Unity reads the *imported* asset, not your disk
write, so if the Editor is open when the exporter runs, let it refocus (or run
`AssetDatabase.Refresh`) before building.

## Not seeded

`Assets/Data/LevelUpCosts.csv` — `CONTENT_PIPELINE_PLAN.md` §9 open question 2,
unanswered. The schema is generic: adding it later is one `content_catalogs` row
and a seed run.
