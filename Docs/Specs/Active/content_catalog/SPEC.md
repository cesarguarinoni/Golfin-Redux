# SPEC — `content_catalog`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Architecture rationale, phasing and the invariants this spec implements:
> `Docs/CONTENT_PIPELINE_PLAN.md` (§2 invariants, §3 Phase 0, §7 rails, §11 shop).
> Read §2 before starting — every design choice below follows from those six invariants.

## Status

See `STATUS.md`. Values as per `_TEMPLATE/SPEC.md`.

## Goal

Stand up the backend half of admin-managed game content: Supabase tables, a seed generated from
the CSVs the game ships **today**, an atomic draft → validate → publish → rollback flow, the
game-facing `/api/v1/content` delta endpoint, and the build-time export script that rewrites the
repo CSVs from the published catalogs.

**No Unity change and no admin UI in this task.** When it lands, the game is byte-for-byte
unaffected and the dashboard looks identical; what exists is a validated, rollback-able content
store with a read endpoint and a round-trip to the repo. The panels (`content_admin_panels`) and
the Unity overlay (`content_overlay_texts`) are separate specs that build on this one.

## Reference

No Figma. This task has no UI surface — the **Figma Fidelity** table from the template is
deliberately omitted rather than left empty.

Ground truth read from the live repos on 2026-08-24 (do not re-derive, but DO re-verify before
depending on any of it):

- CSVs in scope, with row counts: `Assets/Resources/Data/Clubs.csv` (799 + 3 comment lines),
  `Assets/Data/Characters.csv` (12), `Assets/Data/Items.csv` (3), `Assets/Data/Bags.csv` (10),
  `Assets/Data/Balls.csv` (2), `Assets/Localization/LocalizationText.csv` (500),
  `Assets/Resources/Data/shop_catalog.csv` (5).
- Existing remote-content precedent: `backend/routers/notices.py` and `routers/banners.py`
  (game-facing read), `Tools/admin-dashboard/lib/audit.ts` + `lib/supabaseAdmin.ts` +
  `lib/mode.ts` (dashboard writes).
- Router mounting: `playlife/backend/main.py` lines 22–46, all under `/api/v1/<name>`.

## Architecture context

- **Repos touched:** `playlife` (FastAPI + migrations) and `GolfinRedux`
  (`Tools/content/`, `Tools/admin-dashboard/`). **No `Assets/` edits.**
- **Existing code referenced:**
  - `playlife/backend/main.py` — router mount list
  - `playlife/backend/config.py` — `settings.supabase_url` / `settings.supabase_service_key`
  - `playlife/backend/routers/notices.py` — the envelope, `_parse`, and fail-closed window
    handling to copy
  - `Tools/admin-dashboard/lib/supabaseAdmin.ts` — `getSupabaseAdmin()`
  - `Tools/admin-dashboard/lib/audit.ts` — `writeAudit(adminEmail, action, targetUser, tableName, before, after)`
  - `Tools/admin-dashboard/lib/mode.ts` — `isMockMode()`, fails closed with no service key
  - `Tools/admin-dashboard/lib/auth.ts` — `checkAdmin()`, first line of every route handler
- **Not touched:** every `*DatabaseCSV.cs`, `LocalizationManager`, `SaveData`, `ShopModel`,
  `Golfin.Net`. Those are the next specs.

---

## Stage A — schema + seed

### A1. Migration

Write `playlife/backend/migrations/2026_08_24_content_catalog.sql`. **The full SQL is delivered
to Cesar in chat** (per `WORKFLOW_NOTES.md` — SQL always goes in the chat message); the file in
the repo is the archive. Do not invent a different shape; if you believe the SQL is wrong, say so
before writing anything else.

Four tables + two functions:

| Object | Purpose |
|---|---|
| `content_catalogs(name pk, published_version, is_enabled)` | one row per catalog; `is_enabled=false` is the §7.4 kill switch |
| `content_rows(catalog, row_id, data jsonb, min_build, is_active, version, updated_at)` | **published** state. `version` = the catalog version at which this row last CHANGED |
| `content_drafts(...same shape + updated_by)` | staging. Never served |
| `content_versions(catalog, version, snapshot jsonb, published_by, published_at, note)` | one snapshot per publish — this is what makes rollback one call |
| `content_publish(p_catalog, p_by, p_note) → int` | atomic: bump version → upsert drafts into rows → snapshot. Returns the new version |
| `content_rollback(p_catalog, p_to_version, p_by) → int` | restore a snapshot into drafts, then publish it |

**Three things about this schema that are load-bearing — do not "simplify" them:**

1. **`content_rows` upsert has a `WHERE ... IS DISTINCT FROM` guard.** A row whose `data`,
   `min_build` and `is_active` are all unchanged keeps its OLD `version`. That is the entire
   reason the delta stays small: publishing a one-word text fix must bump exactly one row, not
   500. A publish that stamps every row with the new version turns every client's next fetch into
   a full-catalog download.
2. **Rollback moves FORWARD.** `content_rollback` restores an old snapshot as a **new, higher**
   version. It must never decrement `published_version`. Clients cache by version and ask
   `since=N`; rewinding the counter means a client that already has version 12 is never told
   about the rollback and keeps serving the bad content forever.
3. **RLS on, zero policies** — service_role only, the same posture as `golfin_fake_players`
   (`2026_08_18_golfin_leaderboards.sql`). `EXECUTE` on both functions is revoked from
   `public`, `anon` and `authenticated`.

**Migration before deploy, always** (`ADMIN_DASHBOARD_OPS.md` §3.2). Cesar pastes the SQL into
the Supabase SQL editor; verify it landed with the verification query at the foot of the file
BEFORE any code that references the tables is deployed.

### A2. Seed generator

`GolfinRedux/Tools/content/seed_from_csv.py` — reads the seven CSVs above and emits
`playlife/backend/migrations/2026_08_24_content_seed.sql`: `INSERT ... ON CONFLICT DO NOTHING`
into **`content_rows` and `content_drafts` alike**, all stamped `version = 1`, plus
`update content_catalogs set published_version = 1`.

Rules:

- `row_id` is the CSV's own id column: `id` for clubs/characters/items/bags/balls,
  `entryId` for `shop_catalog`, `key` for `texts`.
- `data` is `{column_name: value}` with values kept as **strings, verbatim**. Do not coerce
  numbers or booleans — the Unity parsers already parse strings, and coercion is how `"7"` and
  `7` start disagreeing across the CSV/JSON boundary.
- `Clubs.csv` opens with three `#` comment lines (see `ClubCsvParser`). Skip them; the header is
  the first non-comment line.
- `min_build = 0`, `is_active = true` for every seeded row.
- Emit deterministically (rows sorted by `row_id`) so re-running produces an identical file and
  a reviewable diff.

### A4. Existing server mirrors — resolve, do NOT duplicate

**Found 2026-08-24 while checking this migration for collisions. Read before writing the seed.**

Four tables in prod are already hand-maintained mirrors of client CSVs, all following the same
"the client CSV stays authoritative; this mirror is what the SERVER reads" comment:

| Mirror | Source CSV | Read by |
|---|---|---|
| `golfin_characters(id, display_name, rarity)` | `Assets/Data/Characters.csv` | `routers/tournaments_golfin.py:373` — `char_rarity_min/max` at entry |
| `golfin_bot_fields` | `Assets/Resources/Data/tournament_bot_fields.csv` | tournament bot generation |
| `golfin_bot_brackets` | `Assets/Resources/Data/bot_score_brackets.csv` | tournament bot generation |
| `golfin_fake_players` | `Assets/Resources/Data/fake_players.csv` | `routers/leaderboards.py` |

**`golfin_characters` is ALREADY STALE, and it is a live behaviour bug.** It was seeded
2026-08-18 and says `char_olivia` = `Uncommon`; `Characters.csv` says `Common` (the starter
rarity asymmetry was resolved 2026-08-21 — `ECONOMY_MASTER.md` §3). All 11 other rows agree.
Effect: on a **rarity-restricted** tournament only, Olivia is wrongly rejected from a
Common-only event and wrongly accepted into an Uncommon-minimum one. Unrestricted tournaments
never reach that branch.

Fix shipped alongside this spec:
`playlife/backend/migrations/2026_08_24_golfin_characters_rarity_fix.sql` (NOT YET APPLIED,
idempotent). **Apply it before or with the content-catalog migration** — a stale mirror seeded
into a new catalog is a stale catalog.

**What this task must do about the mirrors:** nothing structural, but do not make it worse.

- Seed the `characters` catalog from **`Characters.csv`**, never from `golfin_characters`.
- **Add a check to the Stage D publish path**: publishing the `characters` catalog must also
  `upsert` `id`/`display_name`/`rarity` into `golfin_characters`, inside the same request, and
  fail the publish if that write fails. One line of divergence is a wrongly-rejected tournament
  entry, and this task is the moment the divergence stops being a one-off and becomes routine —
  an admin editing rarity in a panel has no idea the mirror exists.
- The other three mirrors are NOT in scope (their CSVs are not seeded catalogs here). Note them
  in `IMPLEMENTER_REPORT.md` as a known follow-up so the next spec does not rediscover this.

### A3. Round-trip test — this is Stage A's real acceptance

Run A2, apply the seed, run the Stage C exporter, and **diff the exported CSVs against the repo
CSVs.** They must be byte-identical apart from line-ending normalisation. If they are not, the
mapping is lossy and every later phase inherits the loss. Record the diff command and its output
in `IMPLEMENTER_REPORT.md`.

---

## Stage B — game-facing read endpoint

New `playlife/backend/routers/content.py`, mounted in `main.py` as
`app.include_router(content.router, prefix="/api/v1/content", tags=["Content"])`.

### B1. `GET /api/v1/content`

Query params: `since` (int, default 0), `build` (int, default 0), `catalogs`
(comma-separated, default all).

**No auth**, deliberately — the same posture and the same reason as `/banners` and `/notices`:
it warms at boot before any token work has happened. **No trailing slash** — the bare form is
the 200, and the client must not depend on redirect following.

Response, in the hand-written `{"data": …}` envelope the other routers use:

```json
{"data": {
  "fetched_at": "2026-08-24T12:00:00Z",
  "enabled": true,
  "version": 42,
  "catalogs": {
    "texts": {"version": 42, "full": false, "changed": [
      {"id": "BTN_START", "is_active": true, "min_build": 0,
       "data": {"key": "BTN_START", "English": "PLAY", "Japanese": "プレイ"}}
    ]}
  }
}}
```

- `version` at the top level is the max `published_version` across the returned catalogs.
- Filter `version > since` AND `min_build <= build`. Both server-side — an old build must never
  receive a row it cannot render (§2 I4).
- `full: true` when `since = 0`, when `since` is greater than the catalog's `published_version`
  (a client from a future/staging catalog — send everything rather than nothing), or when the
  delta exceeds **30 %** of the catalog's row count (§5 sizing rule). In the `full` case
  `changed` carries every active row.
- **`is_enabled = false` ⇒ that catalog is omitted entirely and top-level `enabled` is false.**
  The kill switch must produce "no remote content", never a partial or empty-looking catalog
  that a client could mistake for "everything was deleted".
- An unknown catalog name in `catalogs` is ignored, not a 400. A client from a future build
  asking for a catalog this server does not have yet should degrade, not fail.

Add `Cache-Control: public, max-age=60` — this endpoint is hit by every client at boot and the
content changes on a human's publish, not per second.

### B2. `Endpoints.cs`

Add to `Assets/Scripts/Net/Endpoints.cs` **only**:

```csharp
/// <summary>GET → <c>{data:{fetched_at, enabled, version, catalogs:{…}}}</c> — the admin-managed
/// content delta. No auth, same posture as <see cref="Banners"/>. No trailing slash.</summary>
public static string Content(int since, int build) =>
    BaseUrl + "/content?since=" + since + "&build=" + build;
```

This is the one file under `Assets/` this task touches, and it adds a property nothing calls yet.
Do not write `Golfin.Content`, do not touch any `*DatabaseCSV.cs`, do not add an asmdef.

---

## Stage C — export script

`GolfinRedux/Tools/content/export_content.py` — pulls the **published** rows (PostgREST, service
key from the environment, never committed) and rewrites, in place:

- `Assets/Resources/Data/Clubs.csv` — **preserving the three leading `#` comment lines verbatim**
- `Assets/Data/Characters.csv`, `Items.csv`, `Bags.csv`, `Balls.csv`
- `Assets/Resources/Data/shop_catalog.csv`
- `Assets/Localization/LocalizationText.csv`
- `Assets/Resources/Data/content_version.txt` — **new**, one integer per line as
  `<catalog>=<version>`. This is what the client will send as `since`.

Rules:

- Column order comes from the **existing repo CSV header**, not from JSON key order. A reordered
  header is a diff nobody can review and a parser change waiting to happen.
- Quote exactly as Python's `csv` module does with `QUOTE_MINIMAL`; the current files quote only
  fields containing commas (verified against `Characters.csv` and `Clubs.csv`).
- Write with `\n` line endings and a trailing newline.
- `is_active = false` rows are still EXPORTED (§2 I6 — deactivated is not deleted), carrying
  their flag in a new `is_active` column appended at the END of the header. Appending is the only
  safe position under §2 I4.
- The script must be **idempotent**: running it twice with no publish in between produces no diff.

`LocalizationTextTable.asset` needs no step here — `Assets/Localization/Editor/LocalizationTextImporter.cs`
already regenerates it from the CSV. **Verify that** rather than assuming it; if the importer is
manual-trigger-only, say so in the report and do not wire a build hook in this task.

---

## Stage D — publish / validate / rollback API (dashboard, no UI)

Route handlers only. Every one starts with `checkAdmin()` and ends with `writeAudit()`.

| Route | Method | Behaviour |
|---|---|---|
| `app/api/content/route.ts` | GET | list catalogs: name, published_version, is_enabled, draft-vs-published dirty count |
| `app/api/content/[catalog]/rows/route.ts` | GET | paginated draft rows (`?page=&q=&limit=`). **Server-side pagination is not optional — clubs is 799 rows** |
| `app/api/content/[catalog]/rows/route.ts` | PUT | upsert one draft row |
| `app/api/content/[catalog]/diff/route.ts` | GET | drafts vs published: added / changed (field-level) / deactivated |
| `app/api/content/[catalog]/publish/route.ts` | POST | run §D1 validation → `rpc('content_publish')` → audit with the diff as `before`/`after` |
| `app/api/content/[catalog]/rollback/route.ts` | POST | `rpc('content_rollback')` → audit |
| `app/api/content/[catalog]/enabled/route.ts` | POST | flip `is_enabled` → audit. The kill switch |

### D1. Validation — blocking, on publish

Implemented once in `lib/contentValidate.ts` and called by the publish route. **A failure returns
400 with the full list of problems and publishes nothing** — never a partial publish.

1. Required columns present per catalog; `row_id` non-empty and unique.
2. `rarity` ∈ {Common, Uncommon, Rare, Mythic, Legendary, Supreme} wherever the column exists.
3. Numeric columns parse as numbers; `startLevel <= maxLevel`.
4. Stats within `RarityStatCaps` for the row's rarity. **Read the caps from
   `Assets/Scripts/.../RarityStatCaps.cs` and mirror them into a constant in
   `lib/contentValidate.ts` with a comment naming the source file** — do not re-derive them from
   the spreadsheet, and do not guess.
5. `texts`: every key has BOTH `English` and `Japanese` non-empty.
6. `shop_catalog`: `refId` resolves in the catalog named by `category` AND that row is
   `is_active`; `saleRpCost < rpCost` when present; both ≥ 0; `entryId` unique.
7. `min_build` is **immutable once published** — changing it on an existing row is a validation
   failure, not an edit (§5).
8. **Warn, do not block**, when `rpCost` falls outside the rarity band in `ECONOMY_MASTER.md` §3.
   Prices are the thing most likely to be deliberately tuned; blocking here would be inventing a
   rule Cesar did not set.

### D2. Mock mode

`lib/mode.ts` fails closed with no service key, and mock mode's login accepts any password
(`ADMIN_DASHBOARD_OPS.md` §4.5). Content routes must respect `isMockMode()` and read/write
`lib/mockStore.ts` like the other panels — **and the mock fixtures must be obviously fake**
(prices like `9999`), because §3.5 records a real incident where mock fixtures were read as
production facts.

---

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item MUST be `PASS` or `FAIL` with a one-sentence justification citing what was measured.

**Stage A**

- [ ] Migration applied to prod by Cesar; the verification query at the foot of the SQL returns 4 tables, 2 functions, RLS = true on all four, 7 catalog rows
- [ ] `EXECUTE` on `content_publish` / `content_rollback` is denied to `anon` and `authenticated` (show the `has_function_privilege` result)
- [ ] Seed applied; row counts match the CSVs exactly: clubs 799, characters 12, items 3, bags 10, balls 2, texts 500, shop_catalog 5
- [ ] Publishing a single-row edit bumps **exactly one** row's `version` (show the count before/after) — the `IS DISTINCT FROM` guard works
- [ ] `2026_08_24_golfin_characters_rarity_fix.sql` applied; all 12 `golfin_characters` rarities match `Characters.csv` (paste the 12 rows)
- [ ] The `characters` catalog was seeded from `Characters.csv`, NOT from the `golfin_characters` mirror
- [ ] `content_rollback` produced a version HIGHER than the one it rolled back from

**Stage B**

- [ ] `/health` 200 after `fly deploy`
- [ ] `GET /api/v1/content?since=0&build=999999` returns all 7 catalogs, `full: true`
- [ ] `GET /api/v1/content?since=<current>&build=999999` returns `changed: []` for every catalog and a body under 1 KB
- [ ] A row with `min_build=999` is absent for `build=1` and present for `build=999`
- [ ] `is_enabled=false` on one catalog ⇒ that catalog is absent AND top-level `enabled` is false
- [ ] A garbage catalog name is ignored, not a 400
- [ ] `Endpoints.Content()` compiles; **full unfiltered EditMode sweep green** (Assembly-CSharp is touched)

**Stage C**

- [ ] Round-trip: seed → export → `diff` against the seven repo CSVs is EMPTY (paste the command and its output)
- [ ] Running the exporter twice with no publish in between produces no diff
- [ ] `content_version.txt` written with one line per catalog
- [ ] `LocalizationTextTable.asset` regeneration path confirmed (or the gap documented)

**Stage D**

- [ ] Every route rejects a non-admin (show a 401/403)
- [ ] Every mutation wrote an `admin_audit_log` row with before/after
- [ ] Publish with an invalid row (bad rarity, dangling `shop_catalog.refId`, missing Japanese) 400s with the full problem list and publishes NOTHING — verify `published_version` did not move
- [ ] Publishing `characters` also upserts `golfin_characters`; edit a rarity in drafts, publish, and show the mirror row changed in the same request
- [ ] `?page=&limit=` on the clubs rows route returns a page, not 799 rows
- [ ] Spec deviations flagged at the bottom of the report with justification

## Files / hierarchy this task touches

**`playlife`**

- `backend/migrations/2026_08_24_content_catalog.sql` — NEW (archive of the chat SQL)
- `backend/migrations/2026_08_24_content_seed.sql` — NEW (generated by A2)
- `backend/migrations/2026_08_24_golfin_characters_rarity_fix.sql` — NEW (already written; Cesar applies)
- `backend/routers/content.py` — NEW
- `backend/main.py` — one `include_router` line

**`GolfinRedux`**

- `Tools/content/seed_from_csv.py` — NEW
- `Tools/content/export_content.py` — NEW
- `Tools/content/README.md` — NEW; how to run both, and the env vars they need
- `Tools/admin-dashboard/app/api/content/**` — NEW route handlers (Stage D)
- `Tools/admin-dashboard/lib/contentValidate.ts`, `lib/contentData.ts`, `lib/contentMutations.ts` — NEW
- `Tools/admin-dashboard/lib/mockStore.ts` — extended with content fixtures
- `Assets/Scripts/Net/Endpoints.cs` — one new property, nothing calls it yet
- `Docs/AI_CONTEXT.md` — status update

## Smoke evidence

Stage A/B are verified by curl against prod and SQL against Supabase — paste real commands and
real output into the report, not descriptions of them. Stage C is verified by an empty `diff`.
Stage D is verified by curl against `npm run dev` locally (`NODE_ENV=development npm run dev` —
§4.2) with a real service key, plus one publish against a staging catalog before prod.

The visual-fidelity rules in `_TEMPLATE/SPEC.md` do not apply — there is no rendered surface.

## Out of scope (do NOT do these)

- **Any Unity behaviour change.** No `Golfin.Content` asmdef, no `ContentService`, no
  `RemoteContentSource`, no `*DatabaseCSV.cs` edit, no `LocalizationManager.ApplyOverlay`.
  `Endpoints.cs` gains one unused property and that is all.
- **Any admin UI.** No panels, no `lib/registry.ts` entry, no pages. Route handlers only.
- **Player inventory.** No `profiles.golfin_inventory`, no `golfin_pending_grants`.
- **Addressables.** Not in this task and not in this phase (`CONTENT_PIPELINE_PLAN.md` §10).
- **Art URLs on content rows** (§10.2). The columns come with the Clubs overlay spec.
- **`LevelUpCosts.csv`** — deliberately NOT seeded. It is `CONTENT_PIPELINE_PLAN.md` §9 open
  question 2 and Cesar has not answered it. The schema is generic, so adding it later is one
  `content_catalogs` row and a seed run.
- **Server-authoritative purchases.** Prices being admin-managed does NOT make them enforced
  (§11.5). Do not touch `PointsSpendGate`.
- **`git commit`** in the admin-dashboard folder without checking for a running `next dev` first
  (§4.1), and never `npm install` without `NODE_ENV=development ... --include=dev` (§4.2).

## Open question for the Architect (answer before Stage A, do not guess)

`Bags.csv` and `Balls.csv` are seeded above on the reading that Cesar's "all existing clubs and
items and their stats" covers all inventory content. They are 12 rows between them and cost
nothing to include. **Confirm with Cesar rather than assuming**; if the answer is no, drop two
lines from the seed and two catalog rows.
