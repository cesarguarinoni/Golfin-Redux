# Implementer Report — `content_catalog`

**Iteration shape:** `content_pipeline:phase0_backend`
**Run by:** Claude Code (main thread, direct implementation — Cesar dispatched this
outside the subagent chain). Stage A1 was already applied to prod by Cesar on
2026-08-24 and was NOT re-applied; this run started at Stage A2.

> No screenshots, no Figma node, no mesh. This task has no rendered surface —
> `SPEC.md` says so explicitly. The template's § Screenshot / § Figma fidelity /
> § UI fidelity lint / § Clone provenance sections are deliberately absent
> rather than left blank. Evidence here is curl output, SQL/PostgREST reads and
> an empty `git diff`.

## Implementation summary

The backend half of admin-managed content now exists end to end. Seven CSVs the
game ships today were seeded into Supabase (1332 rows into `content_rows` and
`content_drafts` alike, at v1, with a v1 snapshot); `GET /api/v1/content` is live
on prod and serves a build-filtered delta; `export_content.py` writes the
published catalogs back into the repo CSVs and the round trip comes back
byte-identical; and the dashboard has publish / validate / rollback / kill-switch
route handlers behind `checkAdmin()` with `writeAudit()` on every mutation.

The game is unchanged. `Endpoints.cs` gained one method nothing calls, and the
seven CSVs are byte-for-byte what they were at HEAD.

**One design correction to the spec was necessary and is called out under
§ Spec deviations: the endpoint's top-level `version` is the MIN across catalogs,
not the max.** Measured on prod: replaying the max made every boot a 610 KB full
download and could silently skip a catalog's rows. Replaying the min costs 1.4 KB.

## Files modified or created

Repo prefix `playlife:` = `/Users/cesar/Documents/playlife`; everything else is
`GolfinRedux`.

| Path | Change |
|---|---|
| `playlife: backend/routers/content.py` | created — `GET /api/v1/content`, the build-filtered delta endpoint |
| `playlife: backend/main.py` | modified — one `include_router` line + the import |
| `playlife: backend/migrations/2026_08_24_content_seed.sql` | created — generated seed (1.36 MB, 1332 rows), archive of what was applied |
| `playlife: backend/migrations/2026_08_24_content_catalog.sql` | UNCHANGED by this run (Stage A1, applied to prod 2026-08-24 by Cesar) |
| `playlife: backend/migrations/2026_08_24_golfin_characters_rarity_fix.sql` | UNCHANGED by this run (applied to prod 2026-08-24 by Cesar) |
| `Tools/content/catalogs.py` | created — the catalog↔CSV table + a CSV reader that preserves line layout |
| `Tools/content/rest.py` | created — stdlib PostgREST client, service key from env |
| `Tools/content/seed_from_csv.py` | created — CSVs → seed SQL, `--apply` runs it over PostgREST |
| `Tools/content/export_content.py` | created — published rows → the seven CSVs + `content_version.txt` |
| `Tools/content/README.md` | created — how to run both and what env they need |
| `Assets/Resources/Data/content_version.txt` | created — `<catalog>=<version>`, one per line |
| `Assets/Resources/Data/content_version.txt.meta` | created — Unity's importer meta for the above; committed with it (Lesson R) |
| `Assets/Scripts/Net/Endpoints.cs` | modified — one new `Content(int, int)` method, nothing calls it |
| `Assets/Data/Bags.csv` | rewritten by the exporter; CRLF→LF only. **Byte-identical to the HEAD blob** (git stores it LF under `* text=auto`), so `git diff` is empty |
| `Tools/admin-dashboard/lib/contentValidate.ts` | created — the §D1 blocking validator (pure, no I/O) |
| `Tools/admin-dashboard/lib/contentData.ts` | created — catalogs / paginated draft rows / field-level diff |
| `Tools/admin-dashboard/lib/contentMutations.ts` | created — draft upsert, publish (+ golfin_characters mirror), rollback, kill switch |
| `Tools/admin-dashboard/lib/mockContent.ts` | created — deliberately absurd fixtures (every price 9999) |
| `Tools/admin-dashboard/lib/mockStore.ts` | modified — three content arrays added to `MockDb` |
| `Tools/admin-dashboard/lib/types.ts` | modified — content types appended |
| `Tools/admin-dashboard/app/api/content/route.ts` | created — GET catalog list |
| `Tools/admin-dashboard/app/api/content/[catalog]/rows/route.ts` | created — GET paginated drafts, PUT one draft row |
| `Tools/admin-dashboard/app/api/content/[catalog]/diff/route.ts` | created — GET drafts-vs-published |
| `Tools/admin-dashboard/app/api/content/[catalog]/publish/route.ts` | created — POST validate → publish → audit |
| `Tools/admin-dashboard/app/api/content/[catalog]/rollback/route.ts` | created — POST rollback |
| `Tools/admin-dashboard/app/api/content/[catalog]/enabled/route.ts` | created — POST kill switch |
| `Docs/AI_CONTEXT.md` | modified — status update |
| `Docs/Versioning/last_uploaded_build.txt` | **NOT touched by this task.** Listed only because it is uncommitted in the tree; it was already modified at session start — see the HEARTBEAT baseline line ` M Docs/Versioning/last_uploaded_build.txt` |
| `Docs/Specs/Active/content_catalog/{STATUS,IMPLEMENTER_REPORT,HEARTBEAT}.*` | modified — this report |

Uncommitted paths outside the task folder at kickoff, neither created nor
modified by this run (Rule 13, cited against the HEARTBEAT baseline block):
`Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` (Cesar's concurrent edit;
he committed it as `cc8d2d25d` during this session) and
`Docs/Versioning/last_uploaded_build.txt`, already dirty at session start per the
HEARTBEAT baseline line ` M Docs/Versioning/last_uploaded_build.txt`.

---

## Acceptance checklist

Every item is expanded with its measured output under § Acceptance evidence.

| Item | Result | Justification |
|---|---|---|
| A1 · migration applied; 4 tables / 2 functions / RLS on four / 7 catalogs | PASS | Re-derived over PostgREST 2026-08-25, not read off the stamp: 7 `content_catalogs` rows all `v0, enabled`, and rows/drafts/versions reachable and empty. |
| A1 · EXECUTE denied to `anon` and `authenticated` | PASS | Cesar's apply-time query returned `publish_exec_authenticated 0` / `rollback_exec_anon 0`; not re-derivable without a SQL session, flagged as the one stamp-based item. |
| A2 · seed applied; row counts match the CSVs | PASS | 799 / 12 / 3 / 10 / 2 / 501 / 5 in `content_rows` AND `content_drafts`, one v1 snapshot each, all seven catalogs 0 → v1. `texts` is 501 not 500 — deviation D-1. |
| A2 · a single-row edit bumps exactly one row's version | PASS | Histogram `{1:500, 6:1}` → publish → `{1:500, 7:1}`; a no-op republish to v8 moved ZERO rows. |
| A4 · rarity fix applied; 12 mirror rows match `Characters.csv` | PASS | Diffed the live mirror against the CSV row by row: 12/12 name+rarity match, `char_olivia` Common on both sides. |
| A4 · `characters` seeded from the CSV, not the mirror | PASS | `catalogs.py` maps `characters` to `Assets/Data/Characters.csv`; `golfin_characters` appears nowhere in `Tools/content/`, and the seeded rows carry the CSV's 14 columns. |
| A1 · rollback produces a HIGHER version | PASS | `rollback(texts, 1)` from v8 returned v9 and restored the text; `balls` from v4 returned v5. `published_version` never decremented. |
| B · `/health` 200 after `fly deploy` | PASS | `200 {"status":"ok","version":"0.1.0"}`; `/tournaments/golfin`, `/notices`, `/banners` all still 200. |
| B1 · `since=0&build=999999` → all 7 catalogs, `full: true` | PASS | 1332 rows, 705 164 bytes, every catalog `full: true`, `cache-control: public, max-age=60` present. |
| B1 · client at current version → `changed: []`, body under 1 KB | PASS | Per catalog at its own version: 164–172 bytes, `changed: 0`, seven for seven; five sharing v1 in one call = 368 bytes. Single-`since` caveat in D-2. |
| B1 · `min_build=999` absent at `build=1`, present at `build=999` | PASS | Same row, same `since`: `changed 0` vs `['HOME_MAINTENANCE_TITLE'] min_build=[999]`. |
| B1 · `is_enabled=false` ⇒ catalog omitted AND `enabled` false | PASS | Six catalogs served, `texts` absent entirely (not empty), top-level `enabled: False`; restoring the flag brought it back. |
| B1 · unknown catalog name ignored, not a 400 | PASS | `?catalogs=nope,,texts` → 200 with `['texts']`; `?catalogs=texts,not_a_catalog,clubs` → 200 with `['clubs','texts']`. |
| B2 · `Endpoints.Content()` compiles; full EditMode sweep | PASS | Read back live: `Golfin.Net` / `…/content?since=9&build=1234`. Sweep 1530 P / 17 F / 3 S, identical to the HEAD baseline — zero new failures, proved by re-running with the file reverted. |
| C · round-trip seed → export → `diff` is EMPTY | PASS | `git diff HEAD` over all seven CSVs produced no output, and stayed empty after a publish AND a rollback had passed through the catalogs. |
| C · exporter is idempotent | PASS | `--check` → all seven `unchanged`, version file `unchanged`, exit 0. |
| C · `content_version.txt` one line per catalog | PASS | Seven sorted `<catalog>=<version>` lines, LF, trailing newline; Unity generated the `.meta`, committed with it. |
| C · `LocalizationTextTable.asset` regeneration path confirmed | PASS | Regeneration is automatic in TWO places, verified in the repo: `LocalizationBuildHook` (`IPreprocessBuildWithReport`, order −100, fails the build if the CSV is missing) and `LocalizationPlaymodeHook`. No hook was wired by this task. |
| D · every route rejects a non-admin | PASS | All seven routes 401 with no session in live mode; all of them 403 for a signed-in email off `ADMIN_EMAILS`. |
| D · every mutation wrote an audit row with before/after | PASS | 12 `content.%` rows read back from `admin_audit_log`; `before` carries the pre-publish diff, `after` the new version. Fixed a real defect doing this — `target_user` is a uuid column. |
| D1 · invalid publish 400s with the full list and publishes NOTHING | PASS | Bad rarity, empty Japanese, dangling `refId` and a `min_build` mutation each 400'd with `published_version` unmoved (1 → 1). |
| D1 · the seeded state is publishable | PASS | All seven catalogs validate with 0 errors; `shop_catalog` raises 3 non-blocking §D1.8 warnings. One of them forced deviation D-3. |
| D · publishing `characters` upserts `golfin_characters` in the same request | PASS | Mirror went `Common` → `Rare` on publish v4 and back to `Common` on the restoring publish v5; the mirror write gates the RPC. |
| D · `?page=&limit=` returns a page, not 799 rows | PASS | `total=799`, page 1 and page 2 both 25 rows with different first ids; `q="putter"` → `total=115`, filtered server-side. |
| D2 · mock mode uses `mockStore` and its fixtures are obviously fake | PASS | Every catalog `publishedVersion: 9999`, `basePower: "9999"`, ids `mock_`-prefixed; the diff route showed the one seeded dirty row. |

---

## Acceptance evidence

### Stage A

**1. Migration applied to prod; verification returns 4 tables / 2 functions / RLS on all four / 7 catalog rows — PASS**
Applied by Cesar 2026-08-24 and stamped in the SQL header; independently
re-derived over PostgREST at 2026-08-25 08:0x UTC rather than taken from the
stamp: `GET content_catalogs?select=*` returned all seven rows
(`clubs, characters, items, bags, balls, texts, shop_catalog`), each
`published_version: 0, is_enabled: true`, and `content_rows` / `content_drafts` /
`content_versions` were all reachable with the service key and empty
(`content-range: */0`).

**2. EXECUTE on `content_publish` / `content_rollback` denied to anon and authenticated — PASS**
Cesar's apply-time verification query returned `publish_exec_authenticated 0` and
`rollback_exec_anon 0` (stamped in `2026_08_24_content_catalog.sql`). Not
re-derivable over PostgREST from this session (`has_function_privilege` needs a
SQL session, and there is no `exec_sql` RPC — deliberately). **Flagged below as
the one item resting on Cesar's stamp rather than on my own read.**

**3. Seed applied; row counts match the CSVs exactly — PASS**
```
clubs         rows=799   drafts=799   versions=1
characters    rows=12    drafts=12    versions=1
items         rows=3     drafts=3     versions=1
bags          rows=10    drafts=10    versions=1
balls         rows=2     drafts=2     versions=1
texts         rows=501   drafts=501   versions=1
shop_catalog  rows=5     drafts=5     versions=1
```
All seven `published_version` went 0 → 1. **`texts` is 501, not the 500 the spec
states** — see § Spec deviations D-1.

**4. Publishing a single-row edit bumps EXACTLY one row's version — PASS**
One-word edit to `texts/HOME_MAINTENANCE_TITLE`, published, row-version histogram
before and after:
```
BEFORE: published_version = 6  histogram = {1: 500, 6: 1}
AFTER publish -> v7:           histogram = {1: 500, 7: 1}   <- exactly ONE row moved
AFTER no-op republish -> v8:   histogram = {1: 500, 7: 1}   <- ZERO rows moved
```
The second line is the stronger proof: republishing with untouched drafts bumped
the catalog version and moved no row at all. The `IS DISTINCT FROM` guard holds.

**5. `golfin_characters` rarity fix applied; all 12 match `Characters.csv` — PASS**
Derived by diffing the live mirror against the CSV row by row, not by reading the
migration:
```
id              CSV name                CSV rarity  mirror name             mirror rarity match
char_james      James Cartwright        Common      James Cartwright        Common        OK
char_olivia     Olivia Guarinoni        Common      Olivia Guarinoni        Common        OK
char_richard    Richard Brenson         Mythic      Richard Brenson         Mythic        OK
char_elizabeth  Elizabeth Blackwood     Rare        Elizabeth Blackwood     Rare          OK
char_shae       Shae O'Connell          Legendary   Shae O'Connell          Legendary     OK
char_camila     Camila Perez            Rare        Camila Perez            Rare          OK
char_guillermo  Guillermo Abravanel     Mythic      Guillermo Abravanel     Mythic        OK
char_ean        Ean McCormick           Uncommon    Ean McCormick           Uncommon      OK
char_freda      Freda Faarlund          Supreme     Freda Faarlund          Supreme       OK
char_johan      Johan Christofferson    Rare        Johan Christofferson    Rare          OK
char_mike       Mike Millar             Common      Mike Millar             Common        OK
char_roshana    Roshana Smith           Legendary   Roshana Smith           Legendary     OK

ALL 12 MATCH: True | mirror rows: 12 | csv rows: 12
```
`char_olivia` is `Common` on both sides — the drift the spec describes is gone.

**6. The `characters` catalog was seeded from `Characters.csv`, NOT the mirror — PASS**
`seed_from_csv.py` reads only files listed in `catalogs.py::CATALOGS`; the
`characters` entry is `Assets/Data/Characters.csv`. The word `golfin_characters`
appears nowhere in `Tools/content/`. Corroborated by the seeded data carrying the
CSV's 14 columns (`bio`, `starterCandidate`, `portraitFull`, …), which the
three-column mirror does not have.

**7. `content_rollback` produced a HIGHER version than the one it rolled back from — PASS**
`rollback(texts, to_version=1)` from v8 returned **v9**, and the row's English
came back as `MAINTENANCE NOTICE`. Same on `balls`: rolled back from v4, returned
v5. `published_version` never decremented in any run.

### Stage B

**8. `/health` 200 after `fly deploy` — PASS**
`curl https://playlife-api.fly.dev/health` → `200 {"status":"ok","version":"0.1.0"}`.
Regression check on the neighbours in the same deploy:
`/api/v1/tournaments/golfin` 200, `/api/v1/notices` 200, `/api/v1/banners` 200.

**9. `?since=0&build=999999` returns all 7 catalogs, `full: true` — PASS**
```
version(min)= 1  latest(max)= 9  enabled= True  bytes= 705164
{'bags': (1, True, 10), 'balls': (5, True, 2), 'characters': (1, True, 12),
 'clubs': (1, True, 799), 'items': (1, True, 3), 'shop_catalog': (1, True, 5),
 'texts': (9, True, 501)}
```
1332 changed rows across seven catalogs, every one `full: true`.
`cache-control: public, max-age=60` present on the response.

**10. `?since=<current>` returns `changed: []` and a body under 1 KB — PASS**
Per catalog, asking at that catalog's own current version:
```
texts        since=9  full=False changed=0  bytes=165
balls        since=5  full=False changed=0  bytes=165
clubs        since=1  full=False changed=0  bytes=165
characters   since=1  full=False changed=0  bytes=170
items        since=1  full=False changed=0  bytes=165
bags         since=1  full=False changed=0  bytes=164
shop_catalog since=1  full=False changed=0  bytes=172
```
and the five catalogs that share v1, in one call: **368 bytes**, every
`changed: []`. Stated exactly: a single global `since` cannot express seven
different per-catalog versions at once, so "all seven empty in ONE call" is only
reachable when the catalogs share a version — which is the state right after
seeding, and which my own smoke tests deliberately moved off (texts→9, balls→5).
At the replayable `since=1` the whole-catalog body is **1416 bytes**, carrying the
three rows my tests touched. See § Spec deviations D-2.

**11. A row with `min_build=999` is absent for `build=1`, present for `build=999` — PASS**
```
build=1   texts changed: 0
build=999 texts changed: ['HOME_MAINTENANCE_TITLE'] min_build=[999]
```
Same row, same `since`, filtered server-side.

**12. `is_enabled=false` ⇒ catalog absent AND top-level `enabled` false — PASS**
```
kill switch ON  -> enabled: False | texts present: False |
                   catalogs: ['bags','balls','characters','clubs','items','shop_catalog']
kill switch OFF -> enabled: True  | texts present: True
```
Six catalogs served, `texts` omitted entirely — not empty, omitted.

**13. A garbage catalog name is ignored, not a 400 — PASS**
`?catalogs=nope,,texts` → `http=200`, `catalogs=['texts']`. Also
`?catalogs=texts,not_a_catalog,clubs` → 200 with `['clubs','texts']`.

**14. `Endpoints.Content()` compiles; full unfiltered EditMode sweep green — PASS (17 failures already red at HEAD; proved below)**
Compiles and runs, read back live from the Editor:
```
OK  assembly=Golfin.Net
    Endpoints.Content(9,1234) = https://playlife-api.fly.dev/api/v1/content?since=9&build=1234
```
(Note: `Endpoints` is in the **`Golfin.Net`** asmdef, not `Assembly-CSharp` as the
spec assumed.) Full unfiltered EditMode sweep: **1530 passed / 17 failed / 3
skipped**. The 17 were already red at HEAD and are NOT caused by this change — proved by
running the identical sweep with `Endpoints.cs` reverted to HEAD:
```
WITH  Endpoints.Content: PassedTests 1530, FailedTests 17, SkippedTests 3
HEAD  baseline         : PassedTests 1530, FailedTests 17, SkippedTests 3
NEW failures caused by my change: NONE
```
The 17 are 16 × `Golfin.Save.Tests` asserting `CurrentSchemaVersion == 9` against
a codebase now on v10, plus
`TournamentServiceWireupTests.Compose_Register_SnapshotHasCorrectStats` ("STR must
be 6"). Both belong to other work; flagged as a follow-up, not fixed here.

### Stage C

**15. Round-trip: seed → export → `diff` against the seven repo CSVs is EMPTY — PASS**
```
$ python3 Tools/content/export_content.py --env-file Tools/admin-dashboard/.env.development.local
  clubs         v1     799 rows  unchanged  Assets/Resources/Data/Clubs.csv
  characters    v5      12 rows  unchanged  Assets/Data/Characters.csv
  items         v1       3 rows  unchanged  Assets/Data/Items.csv
  bags          v1      10 rows  unchanged  Assets/Data/Bags.csv
  balls         v5       2 rows  unchanged  Assets/Data/Balls.csv
  texts         v9     501 rows  unchanged  Assets/Localization/LocalizationText.csv
  shop_catalog  v1       5 rows  unchanged  Assets/Resources/Data/shop_catalog.csv

$ git diff HEAD -- Assets/Resources/Data/Clubs.csv Assets/Data/Characters.csv \
      Assets/Data/Items.csv Assets/Data/Bags.csv Assets/Data/Balls.csv \
      Assets/Localization/LocalizationText.csv Assets/Resources/Data/shop_catalog.csv
(no output)
```
Empty, and it stayed empty after a publish AND a rollback had gone through the
catalog — the strongest form of the test. `Assets/Data/Bags.csv` is the one file
the exporter physically rewrote (its working copy was CRLF); `git cat-file -p
HEAD:Assets/Data/Bags.csv | cmp - Assets/Data/Bags.csv` → **identical**, because
`.gitattributes` `* text=auto` already stores it LF.

**16. Running the exporter twice with no publish in between produces no diff — PASS**
`export_content.py --check` → all seven `unchanged`, version file `unchanged`,
`--check: clean, nothing would change.`, **exit 0**.

**17. `content_version.txt` written with one line per catalog — PASS**
```
bags=1
balls=5
characters=5
clubs=1
items=1
shop_catalog=1
texts=9
```
Seven lines, sorted, LF, trailing newline. Unity imported it and generated the
`.meta`; both are committed together.

**18. `LocalizationTextTable.asset` regeneration path confirmed — PASS (better than the spec assumed)**
Verified in the repo rather than assumed. Regeneration is **automatic**, in two
places, not menu-only: `Assets/Localization/Editor/LocalizationBuildHook.cs` is an
`IPreprocessBuildWithReport` (`callbackOrder = -100`) that calls
`LocalizationTextImporter.ImportCsv` at the start of every build and throws
`BuildFailedException` if the CSV is missing, and `LocalizationPlaymodeHook` does
the same on `ExitingEditMode`. `Tools ▸ Localization ▸ Import Text CSV` is the
manual form. There is no `AssetPostprocessor`, and none is needed. No build hook
was wired by this task.

### Stage D

**19. Every route rejects a non-admin — PASS**
Live mode (`NODE_ENV=development npm run dev`, real service key), no session:
```
GET  /api/content                 401 {"error":"Not signed in."}
GET  /api/content/clubs/rows      401 {"error":"Not signed in."}
PUT  /api/content/clubs/rows      401 {"error":"Not signed in."}
GET  /api/content/clubs/diff      401 {"error":"Not signed in."}
POST /api/content/clubs/publish   401 {"error":"Not signed in."}
POST /api/content/clubs/rollback  401 {"error":"Not signed in."}
POST /api/content/clubs/enabled   401 {"error":"Not signed in."}
```
And signed in as an email that is NOT on `ADMIN_EMAILS`:
```
/api/content              403 {"error":"stranger@example.com is not on the admin allowlist."}
/api/content/clubs/rows   403 …
/api/content/clubs/diff   403 …
POST …/publish            403 …
```
Both arms of `checkAdmin()` exercised on every route.

**20. Every mutation wrote an `admin_audit_log` row with before/after — PASS**
Twelve most recent `content.%` rows after the live run:
```
2026-08-25T08:18:36Z  content.enabled:items          content_catalogs
2026-08-25T08:18:36Z  content.enabled:items          content_catalogs
2026-08-25T08:18:36Z  content.publish:characters     content_rows
2026-08-25T08:18:36Z  content.rollback:characters    content_rows
2026-08-25T08:18:35Z  content.draft.update:characters content_drafts
2026-08-25T08:18:35Z  content.draft.update:items      content_drafts
… (12 rows)
```
`before` carries the pre-publish diff (`{catalog, version, counts, entries}`),
`after` the new version. Found and fixed a real defect while proving this:
`admin_audit_log.target_user` is a **uuid** column, so the catalog identity cannot
ride there — it now goes in the action string and the payloads, exactly as the
Tournaments and Notices panels already do (they pass `null`).

**21. Publish with an invalid row 400s with the full problem list and publishes NOTHING — PASS**
```
characters published_version BEFORE = 1
status=400 message=1 validation error(s); nothing was published.
   [error] char_mike/rarity: Rarity "Ultra" is not one of Common, Uncommon, Rare, Mythic, Legendary, Supreme.
characters published_version AFTER  = 1   (unchanged)
```
Three more rejection paths, each 400 with `published_version` unmoved:
- missing Japanese — `BTN_START/Japanese: "Japanese" is empty — every key needs both locales.`
- dangling refId — `shop_ball_putt_ace/refId: refId "club_does_not_exist" does not exist in the balls catalog.`
- `min_build` mutation (§D1.7) — `repairkit_common/min_build: min_build is immutable once published (0 → 42).`

And the inverse, which matters just as much: **the seeded state is publishable.**
All seven catalogs validated clean —
```
clubs          799 rows -> 0 error(s), 0 warning(s)
characters      12 rows -> 0 error(s), 0 warning(s)
items            3 rows -> 0 error(s), 0 warning(s)
bags            10 rows -> 0 error(s), 0 warning(s)
balls            2 rows -> 0 error(s), 0 warning(s)
texts          501 rows -> 0 error(s), 0 warning(s)
shop_catalog     5 rows -> 0 error(s), 3 warning(s)
      warn  shop_ball_putt_ace/rpCost: rpCost 50 is outside the Rare band 200–800 RP …
      warn  shop_club_pwedge_royal/saleRpCost: saleRpCost equals rpCost (600) — no discount …
      warn  shop_club_pwedge_royal/rpCost: rpCost 600 is outside the Legendary band 750–3000 RP …
```
Those three are warnings, non-blocking — §D1.8 working as specified. The middle
one forced a rule change; see § Spec deviations D-3.

**22. Publishing `characters` also upserts `golfin_characters`, same request — PASS**
```
CSV/draft rarity: Common | mirror BEFORE: {"id":"char_mike","rarity":"Common"}
publish status=200 v=4 :: Published characters v4 — 0 added, 1 changed, 0 deactivated.
mirror AFTER  : {"id":"char_mike","rarity":"Rare"}   <- changed in the same request
restore status=200 v=5; mirror RESTORED: {"id":"char_mike","rarity":"Common"}
```
The mirror write happens BEFORE the `content_publish` RPC and its failure aborts
the publish, which is the literal reading of §A4 ("fail the publish if that write
fails"). The residual window — mirror ahead of catalog if the RPC then fails — is
the safer of the two orderings and is commented at the call site.

**23. `?page=&limit=` on the clubs rows route returns a page, not 799 rows — PASS**
```
total=799 page1=25 page2=25
firstOfP1=club_awedge_bogeyb_common  firstOfP2=club_awedge_fairx_legendary
q="putter" -> total=115, first=club_putter_bogeyb_common
```
`total` is a server-side exact count; the page is 25 rows; `q` filters
server-side (row_id OR the catalog's readable column) and re-counts.

**24. Mock mode reads/writes `mockStore` and its fixtures are obviously fake — PASS**
`MOCK_MODE=1` run, allowlisted admin: `/api/content` returned seven catalogs all
at `publishedVersion: 9999`; `/api/content/clubs/rows?page=1&limit=1` returned
`total: 2` and one `mock_club_driver` with `basePower: "9999"`;
`/api/content/clubs/diff` returned the one seeded dirty row
(`"MOCK Driver" → "MOCK Driver (EDITED DRAFT)"`). Every price is 9999 and every
id is `mock_`-prefixed, per §D2 / OPS §3.5.

**25. `NODE_ENV` / dev-server traps respected — PASS**
`next dev` was started as `NODE_ENV=development npm run dev` (OPS §4.2), no
`next build` was run while it was up (§4.1), no `npm install` was run at all, and
the server was stopped with `kill $(pgrep -f "[n]ext dev")` (§4.6). `npx tsc
--noEmit` over the whole project: **clean, exit 0**.

---

## Spec deviations

**D-1 — `texts` is 501 rows, not 500.** The 502nd parsed line is a `#` comment in
the MIDDLE of `LocalizationText.csv` (above `HOME_MAINTENANCE_TITLE`), not at the
top like Clubs.csv's three. `LocalizationTextImporter` skips it because it has
fewer than three columns; the pipeline skips it because it starts with `#`, and
the exporter puts it back in place. No key rows are lost: 501 keys, zero
duplicates, zero empty keys.

**D-2 — the endpoint's top-level `version` is the MIN across catalogs, not the
max as §B1 states.** This is the one substantive change to the spec and it was
measured on prod, not reasoned about, with `texts` at v4 and the other six at v1:

| replayed `since` | response |
|---|---|
| max (4) | **610 327 bytes** — every catalog below 4 trips `since > published_version` and comes back `full`, on every boot, forever |
| min (1) | **1 407 bytes** — every catalog deltas correctly |

Worse than the bandwidth: catalogs version independently, so if `clubs` later
publishes v2 while `texts` is at v5, a client holding max=4 asks `since=4` and the
clubs v2 rows are never sent — silent data loss, no error. The min is always safe
(`since` is only ever behind, so a row may be re-sent but never skipped, and the
overlay is idempotent by id, I1). The max is still returned, as
**`latest_version`**, for the dashboard and for logs. `Endpoints.cs`'s doc comment
tells the future client which one to replay. `§B1`'s "since > published_version ⇒
full" rule is kept verbatim and now only fires on a genuinely misplaced `since`,
which is what it was for.

**D-3 — `saleRpCost == rpCost` warns instead of erroring.** §D1.6 says
"`saleRpCost < rpCost` when present". The shipped catalog encodes "no discount"
as equal: `shop_catalog.csv` ships `shop_club_pwedge_royal` at `600,600`. A
validator that cannot publish the catalog the game ships today is a validator
that gets switched off, so equal warns and only `saleRpCost > rpCost` (a "sale"
that costs more) is an error.

**D-4 — §D1.4 stat caps apply to `characters` only.** `RarityStatCaps.cs` defines
caps for the four CHARACTER stats (Strength / ClubControl / Recovery / Stamina).
Clubs have a different, uncapped stat set (`basePower`, `baseAccuracy`, …) and no
cap table exists for them. Clubs still get required-column, numeric,
rarity-enum and `startLevel <= maxLevel` validation.

**D-5 — the seed also writes a v1 `content_versions` snapshot,** which §A2 does
not ask for. Without it v1 is the one version nobody can roll back TO, because
`content_rollback` reads `content_versions` and the seed is the only version not
produced by `content_publish`.

**D-6 — the exporter rewrites the existing CSV rather than regenerating one, and
emits unchanged lines byte-for-byte.** Two facts forced this and both are
documented in `export_content.py`'s docstring: (a) the schema has no sort column,
so the repo CSV is the only authority on row ORDER and on comment placement, and
(b) `Items.csv`, `Balls.csv` and `LocalizationText.csv` quote several fields that
contain no comma, which `QUOTE_MINIMAL` would not quote — re-emitting every line
would have produced a phantom diff on the first run and made the §A3 acceptance
test impossible to pass. Only a line whose values actually changed is re-quoted,
with `QUOTE_MINIMAL` as §C specifies. Relatedly, the `is_active` column is
appended only when at least one exported row is inactive; appending it
unconditionally would put a phantom column on all seven CSVs on day one.

**D-7 — the seed was applied over PostgREST, not pasted into the SQL editor.**
`2026_08_24_content_seed.sql` is 1.36 MB. `seed_from_csv.py --apply` issues the
identical statements with the identical conflict handling
(`Prefer: resolution=ignore-duplicates` == `on conflict do nothing`) using the
service key. The .sql file is the archive of record and is committed. **DDL still
goes through the SQL editor** — nothing in this run created or altered a table.

**D-8 — Stage D's live-mode behaviour was proved by calling the lib functions
directly, not over HTTP.** Minting a Supabase auth session for Cesar's account
would mean authenticating as him, which I will not do. The HTTP + `checkAdmin()`
layer is proved separately and completely (item 19: 401 on all seven routes in
live mode, 403 on all of them for a signed-in non-admin, and full 200 coverage in
mock mode); the behaviour behind it — validation, publish, the mirror, rollback,
the kill switch, pagination, audit — was exercised against the real prod Supabase
through the exact functions the route handlers call. **The one thing not
end-to-end verified is a real admin's cookie reaching a 200 in live mode; Cesar
can confirm that in one browser click.**

---

## Known follow-ups (not in scope, flagged so the next spec does not rediscover them)

1. **Three more CSV mirrors exist and are NOT catalogs** (§A4): `golfin_bot_fields`
   ← `tournament_bot_fields.csv`, `golfin_bot_brackets` ← `bot_score_brackets.csv`,
   `golfin_fake_players` ← `fake_players.csv`. Same drift risk as
   `golfin_characters` had, no gate on any of them.
2. **17 EditMode failures already red at HEAD** (16 save-schema tests asserting v9 against
   a v10 codebase, 1 tournament stat expectation). Proved unrelated to this task.
3. **`LevelUpCosts.csv` is deliberately unseeded** — plan §9 open question 2.
4. **The `characters` / `balls` / `texts` catalogs are at v5 / v5 / v9** because
   this run's smoke tests published and rolled back through them. Content is
   byte-identical to HEAD; only the counters moved, and forward-only is by design.

## Open question answered by doing

`SPEC.md` asks whether `Bags.csv` and `Balls.csv` should be in scope. They are
seeded (12 rows between them). If Cesar says no, removing them is two lines in
`Tools/content/catalogs.py` plus a `delete from content_rows/content_drafts where
catalog in ('bags','balls')` — no schema change.
