# `Tools/content` — the CSV ↔ Supabase content pipeline

Two scripts and the shared mapping between them. Together they are invariant
**I3** from `Docs/CONTENT_PIPELINE_PLAN.md` §2: *the admin is upstream of the
CSV, not downstream.* Without the round trip the delta grows forever and the
bundled floor rots.

```
Assets/**/*.csv  ──seed_from_csv.py──►  content_rows / content_drafts (Supabase)
                                                    │
                                              admin publishes
                                                    │
Assets/**/*.csv  ◄──export_content.py──────────────┘   + content_version.txt
```

| File | What it is |
|---|---|
| `catalogs.py` | The catalog ↔ CSV table, and the CSV reader that keeps line layout. Run it directly to print the current row counts. |
| `rest.py` | Stdlib PostgREST client. Service key from the environment. |
| `seed_from_csv.py` | Repo CSVs → `2026_08_24_content_seed.sql` (and `--apply` runs it). |
| `export_content.py` | Published Supabase rows → the seven repo CSVs + `content_version.txt`. |

## Credentials

Both scripts read the environment and **never** hold a key in the repo:

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
