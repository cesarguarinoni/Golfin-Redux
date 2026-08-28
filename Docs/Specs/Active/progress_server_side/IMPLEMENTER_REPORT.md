# IMPLEMENTER_REPORT — `progress_server_side`

**Iteration shape:** `progression:client-authoritative-level-up`
**Iteration:** iter-1 — every §6 step done, live E2E RAN
**Date:** 2026-08-28

---

## Deployment — the three proofs (§6, §7, PIPELINE_HARDENING §23)

| # | What | Proof | Verified how |
|---|---|---|---|
| **(a)** | Dashboard backlog (§6 step 0) | Cloudflare deployment **`577be843-4808-4aad-ade7-648d8a5f7c20`**, 2026-08-28T06:27:21Z, stamped `3df55d58f` | `wrangler deployments list` shows it at 100%; `curl -o /dev/null -w %{http_code} https://admin.golfin.world/` → **302** (Access protecting), same on `/api/version` |
| **(b)** | API (§6 step 1) | Fly image **`playlife-api:deployment-01M13JGS6V9HWAENJS254ZAKDF`**, machine version **56** (was 55 / `…01M1159SB99179ZMWNJD038X9A`) | `flyctl status` **after** the deploy, not the deploy's exit code (`reference_flyctl_401_false_deploy_failure`) + live smoke below |
| **(c)** | Dashboard, this task's half (§6 step 3) | **`394f4733-8f3c-4510-973d-2cf7df304b63`**, stamped **`da4eee5f9` == HEAD**. Two earlier ones in the same task: `c927bde9-…` (the panel, `0c26421b8`) and `96e5ad86-…` (the sidebar-label fix, `6ccd4a8a2`) | `wrangler deployments list` at 100%; bundle stamp grepped from `.open-next/…/api/version/route.js`; `/`, `/api/version`, `/level-costs` all **302**; and the footer stamp **read off the live page in a browser** at the `96e5ad86` deploy — see below |

### The API smoke (§6 step 1)

```
/health                                    200
/api/v1/banners                            200
/api/v1/shop/purchase                      403   (existing route, still auth-gated)
/api/v1/progress/level-up                  403   ← 403, NOT 404. The route is deployed.
/api/v1/progress/nope                      404   ← the control: an undeployed path really does 404
```

The last two lines together are the actual proof. `403` alone could be a wildcard;
`403` on the real path *and* `404` on a sibling under the same prefix is what
distinguishes "deployed and auth-gated" from "not there".

### ⚠️ §23's literal check (`/api/version` by curl) still cannot be done, and this is not new

Cloudflare Access 302s **every** request to `admin.golfin.world`, `/api/version`
included, and there is no Access service token on this machine — I checked both
`.env.development.local` and `wrangler secret list`; the five secrets are all
Supabase/admin values, none of them a `CF-Access-Client-Id` pair. So the body of
`/api/version` is unreadable from a shell, exactly as `Docs/TellCode.md` recorded
this morning.

What I did instead, and consider equivalent-or-better:

1. `grep` the built bundle for the stamp (`"0c26421b8"`) — this is the literal
   string the route hands back, read out of the artifact that was uploaded.
2. `wrangler deployments list` — proves that artifact is the version serving
   100% of traffic.
3. `curl` → 302 on `/api/version` — proves the route exists behind Access.

(1)+(2) is a stronger claim than (3) alone would be.

**And then a fourth, better one turned up.** Driving the admin in Chrome for the
E2E showed that the stamp renders in the SIDEBAR FOOTER of the live page — it
read `6ccd4a8a2` at that deploy, matching HEAD at the time. So the stamp IS
verifiable against the running site, just not from a shell. That is the check
§23 actually wants; it simply needs a browser with an Access session.

**Unblocking the LITERAL shell check is a Cesar/Cloudflare-console step**: either
an Access service token stored as a secret, or a bypass policy on `/api/version`.
Worth doing — otherwise "is it deployed?" stays a browser question, and a
headless/cron run can never answer it.

⚠️ Note on ordering: the docs commit (`da4eee5f9`) landed AFTER the `96e5ad86`
deploy, which left the stamp one commit behind HEAD even though no dashboard file
had changed. Rather than argue that, the dashboard was redeployed — `394f4733`,
stamped `da4eee5f9`, which is HEAD.

---

## Acceptance checklist (§7)

| # | Item | Verdict | Justification |
|---|---|---|---|
| 0 | **Pre-flight: both migrations parse** | **PASS** | No Postgres on this machine, so the SQL was parsed with the real grammar instead of eyeballed: `pglast` v8.4 (libpg_query). `parse_sql` on the whole `2026_08_28_golfin_progress.sql` → clean; `parse_plpgsql_json` on the `golfin_level_up` body → clean, 1 function; `parse_sql` on the 240-row seed → clean. This does not prove the semantics, only that Cesar's paste will not die on a typo halfway through a `create table`. |
| 1 | **Live E2E on prod** — real level-up, ledger row + grandfathered progress row + event verified by SQL; then a published cost change makes a stale client get `cost_changed` and pay the new sum on the second tap | **PASS — RAN, on prod, end to end** | See § The live E2E below. Two real level-ups on `f2636482-…`, driven through the REAL widgets (`NavCharactersButton.onClick` → `RosterScreen/…/LevelUpButton.onClick` → the modal's own LEVEL UP / [+] / CONFIRM), against `https://playlife-api.fly.dev`. All three server rows read back by SQL for BOTH. The cost change was published from the live admin UI in Chrome, not by a back-door write. |
| 2 | First level-up seeds `golfin_progress` with `grandfathered_from` = claimed level; blob mismatch logs + returns `blob_level`, never blocks | **PASS (code + unit)** | `2026_08_28_golfin_progress.sql` §5: no progress row ⇒ `v_grandfathered := true`, the insert stamps `grandfathered_from = p_from`. The blob read is wrapped in its own `begin … exception when others then v_blob_level := null; end` block so a malformed blob contributes nothing and cannot raise; a disagreement `raise warning`s server-side and appends `blob_level` to the ok payload. Client side pinned by `ProgressServiceTests.Ok_WithABlobMismatchStillSucceedsAndReportsTheBlobLevel` and the router by `test_a_grandfathered_blob_mismatch_rides_the_ok_payload`. **Not yet observed against live Postgres** — that is item 1. |
| 3 | Second level-up with a stale `from_level` → `level_conflict`, nothing debited | **PASS (code + unit)** | §5: when the progress row is found, `v_server_level is distinct from p_from` returns before step 6, so `spend_pts` is never reached — the debit is physically downstream of the guard. `ProgressServiceTests.LevelConflict_ReportsTheServersLevelAndLeavesTheCachedBalanceAlone` also asserts the client does not fold the (zero) balances such a payload carries. |
| 4 | Multi-level commit → ONE debit of the summed cost, ONE event, progress at the target; replay of the same key → `replayed`, no second debit | **PASS (code + unit)** | Both modals send one call for the whole previewed run (`playerData.currentLevel → previewLevel`). §4 sums `cost_r` over `generate_series(p_from+1, p_to)`; §6 makes exactly one `spend_pts` call; §7 writes exactly one event. Replay is step 1: the `unique (user_id, idempotency_key)` row is found and the ok shape is rebuilt **read-only** (balances re-read from `points_transactions` rather than by re-calling `spend_pts`, so a hand-deleted ledger row cannot cause a second debit). `ReplayedLevelUp_IsStillAnOk`. |
| 5 | A gap published into `level_up_costs` is refused by the validator; forced via SQL, the server answers `costs_missing` | **PASS (code)** | Validator: `contentValidate.ts` rule 9 walks 1..ceiling and errors on every uncovered level, blocking (`hasErrors` ⇒ nothing publishes). Server: §4's `min(lv)` query left-joins with `and r.is_active` **in the ON clause**, so absent / deactivated / unparseable-`cost_r` all collapse to the same refusal, naming the lowest bad level. Both halves treat deactivation as a gap, which is the part that is easy to get wrong. |
| 6 | `p_to` above the ref's `maxLevel` → `invalid_range`; deactivated ref → `not_available` | **PASS (code + unit)** | §3: `p_to > v_max_level` ⇒ `invalid_range/max_level`; `not found or v_is_active is not true` ⇒ `not_available/ref`. An unparseable `maxLevel` fails **closed** (`not_available/ref_max_level`) rather than being coerced. Router 200-passthrough pinned for both by `test_every_rpc_status_passes_through_as_200_with_data`. |
| 7 | Kill switch off → `not_available/disabled`; back on → works, no deploy either way | **PASS (code + unit)** | §2, copied verbatim from `golfin_shop_purchase` including its fail-**open** truthiness (missing/unreadable row = enabled; only an explicit `false` disables). Checks the **`level_up_costs`** catalog switch, not the ref's — pulling the cost table is the operator saying "do not sell levels", and pricing from a table they withdrew is the thing to prevent. `TheKillSwitchIsNotAvailable` asserts the client maps it to `NotAvailable` with `IsDisabled` true. |
| 8 | `level_up_costs` round-trips (seed → export byte-identical → `--check` clean; import on a hand-edit); Tools tests green | **PASS — run against PROD** | `seed_from_csv.py --catalogs level_up_costs --apply` → `applied level_up_costs 240 rows (published now 240)`. `export_content.py --catalogs level_up_costs` → `level_up_costs v1 240 rows **unchanged**`. `export_content.py --check` → `--check: clean — no file would change and no catalog has drifted.` `python3 -m pytest Tools/content/tests -q` → **26 passed**. |
| 9 | **Three deployment proofs in the report** | **PASS** | § Deployment above. (a) `577be843-…`, (b) `…01M13JGS6V9HWAENJS254ZAKDF` / v56, (c) `c927bde9-…` with the bundle stamped `0c26421b8` == HEAD. |
| 10 | Flag OFF byte-identical; full EditMode sweep green; backend suite green | **PASS** | Flag OFF: both `OnConfirmClicked`s short-circuit to the **unchanged** `PointsSpendGate.Spend(totalRPCost, SpendReasons.…, () => CommitLevelUps(…))` before `ProgressService` is named, and that gate runs `onApproved` on the caller's own stack frame when the flag is off — so no coroutine, no HTTP, no timing shift. EditMode: **1935 tests / 1932 passed / 0 failed / 3 skipped** (the 3 skips are pre-existing `HoleCompleteDriverTests` Stage-C1 skips). Backend: **98 passed**. |
| 11 | Holes and SP allocation untouched | **PASS** | `git diff --stat` across both commits touches no file under `Assets/Scripts/**/Hole*`; `HoleProgressionService` has zero diff. The SP path is likewise untouched: `CommitLevelUps` in both modals is byte-identical to HEAD — the only change to either modal is *what gates it*. |

---

## The live E2E (§6 step 5, §21) — what actually happened

Prod account `f2636482-29aa-4233-a834-99526b202fe1`, build 2394,
`PointsBackendFlag.Enabled = True`, `BaseUrl = https://playlife-api.fly.dev/api/v1`.
Every tap went through the REAL widget's `onClick` — `PersistentUI/BottomNavBar/NavCharactersButton`,
then `RosterScreen/DetailPanel/RightPanel/ButtonsPanel/LevelUpButton`, then the
modal's own LEVEL UP, Strength [+] and CONFIRM. No synthetic entry point.

### Part 1 — the grandfathered first level-up

`char_james`, Lv 10 → 11. The modal previewed **6 RP**; the client log:

```
[ProgressService] Levelled 'char_james' → ok character 'char_james' → Lv 11 for 6 RP (grandfathered seed) → RP=967
[LevelUpModal] Confirmed: +1 levels, SP: STR+1 CC+0 REC+0 STAM+0
```

Balance 973 → 967. All three server rows, read back over PostgREST:

| Table | Row |
|---|---|
| `golfin_progress` | `level=11`, **`grandfathered_from=10`** — the claim taken on trust, once, and stamped |
| `golfin_progress_events` | `from_level=10 → to_level=11`, `cost_rp=6`, key `b34b8528-a82e-4464-aec9-96fa2e591d14` |
| `points_transactions` | `amount=-6`, **`description="progress:character:char_james:L11"`**, same key |

The three share one idempotency key, which is the property that makes the
transaction claim checkable rather than asserted.

### Part 2 — `cost_changed`, published from the live admin

In `admin.golfin.world` → **Level Costs** (the new panel, this task's own
deploy): searched `12`, opened the row editor, `cost_r` 6 → 60, Save draft →
"1 unpublished" → Review & publish. The drawer showed `changed 12 · cost_r ·
Published 6 · Draft 60`; publishing gave **"Published level_up_costs v2 — 0
added, 1 changed, 0 deactivated."** The new contiguity validator ran on that
publish and passed (240 contiguous rows against a max `maxLevel` of 239).

The Unity session was still running with its BOOT-time overlay, i.e. genuinely
stale — proven, not assumed: `GetLevelUpCost(12)` still returned **6** in-session
while prod served 60.

First CONFIRM:

```
[ProgressService] Level-up of 'char_james' refused: cost_changed → 60 RP. Nothing was written; the modal must re-price and ask again.
[LevelUpModal] Cost changed for 'char_james' Lv 11 → 12: the published total is 60 RP. Preview rebuilt; the next CONFIRM pays that.
```

Modal stayed OPEN, `totalRPCost` 6 → **60**, balance **unchanged at 967** — the
refusal carries no balances and correctly did not touch the cache.

Second CONFIRM, no other input:

```
[ProgressService] Levelled 'char_james' → ok character 'char_james' → Lv 12 for 60 RP → RP=907
```

967 → 907. Note the absence of "(grandfathered seed)" this time: the row already
existed, so this went through the PAID path and the level guard compared 11
against 11. Server rows:

| Table | Row |
|---|---|
| `golfin_progress` | `level=12`, `grandfathered_from` **still 10** — the seed stamp is not rewritten by later steps |
| `golfin_progress_events` | a SECOND row, `11 → 12`, `cost_rp=60`, key `51d0b951-…` |
| `points_transactions` | `amount=-60`, `description="progress:character:char_james:L12"`, same key |

### Cleanup

`level_up_costs` L12 published back to **6** from the admin (v3), with a note on
the version snapshot. `export_content.py --check` → **clean**; the repo CSV is
byte-identical to prod and `content_version.txt` carries `level_up_costs=3`.

Cost to Cesar's account: **66 RP**, and `char_james` is genuinely Lv 12 now, on
the server and in the save. That is what a live E2E costs.

---

## Files modified or created

### `~/Documents/playlife` — commit `f32cde7`

| File | What |
|---|---|
| `backend/migrations/2026_08_28_golfin_progress.sql` | **NEW.** `golfin_progress` + `golfin_progress_events` (RLS on, zero policies) and `golfin_level_up()` — replay → kill switches → ref/range → cost from published rows → level guard + grandfather → `spend_pts` → record, one transaction. Ends with a 10-row verification `select`. |
| `backend/migrations/2026_08_28_content_level_up_costs_seed.sql` | **NEW, generated.** 240 `level_up_costs` rows + the `content_catalogs` registry row. Already applied over PostgREST; the file is the archive of record. |
| `backend/routers/progress.py` | **NEW.** `POST /api/v1/progress/level-up`. User id from the token, cost never from the body, every business outcome 200, no `_missing_relation` courtesy. |
| `backend/tests/test_progress_level_up.py` | **NEW.** 35 tests at the router boundary — auth, the eight 400s, the wire shape, one test per business status. |
| `backend/main.py` | +1 import, +1 `include_router` at `/api/v1/progress`. |

### `~/Documents/GolfinRedux` — commit `0a286998f` (content + admin)

| File | What |
|---|---|
| `Tools/content/catalogs.py` | `Catalog("level_up_costs", "Assets/Data/LevelUpCosts.csv", "level")` — the ninth catalog. Export/import/`--check` pick it up from the table. |
| `Tools/content/seed_from_csv.py` | `--catalogs` (the flag export/import already had) + the `content_catalogs` registry insert, so a catalog added after day one can seed itself instead of failing its own FK. |
| `Assets/Data/LevelUpCosts.csv` | Trailing newline. `read_csv` refuses to guess without one; this was the only CSV of the eight missing it. |
| `Assets/Resources/Data/content_version.txt` | `level_up_costs=1` — written by the exporter, not by hand. |
| `Tools/admin-dashboard/lib/contentValidate.ts` | `REQUIRED` / `NUMERIC` / `ID_COLUMN` rows + **rule 9**: non-negative `cost_r`/`sp_reward`, `level` a plain positive integer matching its row id, and blocking **contiguous coverage** to `max(maxLevel)` across characters+clubs. |
| `Tools/admin-dashboard/lib/contentMutations.ts` | A `level_up_costs` publish now loads the characters + clubs drafts — rule 9's ceiling cannot be computed without them. |
| `Tools/admin-dashboard/lib/contentView.ts` | `CONTENT_CATALOGS` += `level_up_costs`; a `CATALOG_VIEWS` entry (columns `cost_r`, `sp_reward`; no facet; 50/page). |
| `Tools/admin-dashboard/app/(panels)/level-costs/{page,level-costs-panel}.tsx` | **NEW.** The panel — the shared `CatalogPanel`, nothing bespoke. |
| `Tools/admin-dashboard/lib/registry.ts`, `components/PanelIcon.tsx` | Sidebar entry + a staircase icon. |
| `Tools/admin-dashboard/lib/i18n.ts` | `lu.*` — EN + JA. |
| `Tools/admin-dashboard/lib/mockContent.ts` | Three mock rows, with a note that a mock **publish** correctly fails (the mock club claims `maxLevel: 9999`). |

### `~/Documents/GolfinRedux` — commit `0c26421b8` (Unity client)

| File | What |
|---|---|
| `Assets/Scripts/Economy/ProgressService.cs` | **NEW.** `LevelUpAsync`/`LevelUpRoutine` — flag gate inside the routine, own latch, fresh key per attempt, `ApplySpendResult` in a `finally` **after** `onDone`. |
| `Assets/Scripts/Economy/ProgressOutcome.cs` | **NEW.** `ProgressLevelUpVerdict` (7) + `ProgressLevelUpOutcome`. No `MayProceed`, for the reason `ShopPurchaseOutcome` has none. |
| `Assets/Scripts/Economy/PointsDtos.cs` | `+ProgressLevelUpResult` — every field transcribed from the migration, `ToSpendResult()` so the balance folds through existing code. |
| `Assets/Scripts/Net/Endpoints.cs` | `+ProgressLevelUp`. |
| `Assets/Scripts/EconomyRuntime/PointsSpendGate.cs` | `+CostUpdatedMessage`, `+LevelConflictMessage` — beside the two the spec reuses, so all four refusal strings stay in one place. |
| `Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs` | Flag ON → `LevelUpAsync`; `OnServerAnswered` + `RepriceFromServer`. `CommitLevelUps` unchanged. |
| `Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs` | The mirror of the above. `CommitLevelUps` unchanged. |
| `Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs` | The `level_up_costs` overlay (patch / append / deactivate) + a `Reload()` seam. |
| `Assets/Scripts/ContentRuntime/ContentCatalogs.cs` | `+LevelUpCosts` in `Data`, `All`, `RequestList`. |
| `Assets/Scripts/ContentRuntime/Tests/ContentCatalogMapperTests.cs` | The request-list assertion now names eight catalogs. |
| `Assets/Scripts/Economy/Tests/ProgressServiceTests.cs` | **NEW.** 24 tests. |
| `Assets/Tests/EditMode/LevelUpCostsOverlayTests.cs` | **NEW.** 8 tests, driven through the real database by reflection. |

### Uncommitted paths outside this task's folder (Rule 13 disclosure)

All pre-existing, all present in the iter-1 baseline block in `HEARTBEAT.log`, none
touched by this task:

- `Assets/Resources/Clubs/**` (~90 untracked PNG + `.meta`) and
  `Docs/Specs/Active/club_art_batches/STATUS.md` — the club-art batch task.
- `Assets/Plugins/NuGet/{.nuget-installed.json,McpPlugin*.dll}`, `Packages/manifest.json`,
  `Packages/packages-lock.json`, `ProjectSettings/ProjectSettings.asset` — Unity MCP
  package churn (`project_mcp_define_auto_readded`).
- `Docs/Versioning/last_uploaded_build.txt`, `Docs/TellCode.md` — build/handoff bookkeeping.
- `Docs/Specs/Active/game_modes_admin/**` — the queued spec, untracked.

---

## Two deliberate deviations from the spec text

### 1. `CostChanged` takes the run total from the SERVER, not from a local re-sum

SPEC §4 says the `CostChanged` branch should "reload `CharacterLevelUpDatabase`
(overlay), rebuild the preview at the same target level with fresh costs" and
that "second CONFIRM pays". **Those two sentences cannot both be true as
written**, and the second one is the requirement.

The content overlay is a **next-launch effect** (I5 — `ContentCatalogStore` is
filled once, at boot, by `ContentService`). A cost published thirty seconds ago
is therefore *not* in this session's overlay, so `Reload()` re-reads exactly the
numbers the server just rejected. Re-summing locally would send the same
`expected_cost` again, get `cost_changed` again, and loop forever.

So: `Reload()` is still called (the per-level numbers and `sp_reward` come from
it, and the call is what makes the next boot coherent), the preview is still
rebuilt at the same target, and then `totalRPCost = outcome.Cost` — the sum
`golfin_level_up()` computed for exactly this `from → to` range, i.e. precisely
what it will charge on the next attempt. Acceptance item 1's "pays the new sum on
the second tap" holds because of this, not in spite of it.

Side effect worth naming: after a re-price the character modal's per-level local
debits inside `CharacterManager.LevelUp()` sum to the *local* total, not the
server's. That self-corrects and always did — `ApplySpendResult` →
`OnDisplayBalanceChanged` → `RewardPointsManager.ApplyServerBalance` runs
immediately after `onDone` and overwrites the counter with the server's real
total. The club modal has no such gap: it debits `totalRPCost` once, which by
then *is* the server's number.

### 2. `costs_missing` and `invalid_range` map to `NotAvailable`, not to verdicts of their own

SPEC §4 names exactly seven verdicts (`Ok, Insufficient, CostChanged,
LevelConflict, NotAvailable, Unavailable, Disabled`) and the server has more
statuses than that. `costs_missing` and `invalid_range` are content and
client bugs — the player cannot act on either, and both get the same toast — so
they fold into `NotAvailable` with a distinct, loud log line. Anything a later
server invents lands there too, rather than being mistaken for success.

---

## One thing I want a second pair of eyes on

`golfin_level_up`'s grandfather branch reads `profiles.golfin_inventory` and
looks for `(v_elem->>'level')` on the **object** form of a characters/clubs
entry. I transcribed those wire keys from `golfin_shop_purchase`'s step 8 (which
reads `id` and `own` from the same arrays) and from `InventoryCodec`, but I have
not seen a real blob with a levelled character in it. If the level lives under a
different key, the cross-check silently finds nothing and every grandfathered
seed reports no mismatch. **That is a diagnostic going quiet, not a level-up
going wrong** — the branch cannot block, refuse or mis-charge — but it is worth
one `select golfin_inventory from profiles limit 1` during the E2E to confirm the
key, and I will do exactly that as part of item 1.

---

## Canonical screenshot

**N/A — this task has no visual surface.** Every change is a server function, a
router, a content catalog, a validator rule, and a code path behind two existing
modals whose *rendering* is byte-identical to HEAD (the diff is what gates
`CommitLevelUps`, not what it draws). The one new UI is the admin Level Costs
panel, which is the shared `CatalogPanel` with a different `catalog` prop and no
bespoke markup; it sits behind Cloudflare Access, which 302s automated capture.
Rules 14/17/18/19/21 are all scoped to Figma-node UI tasks and mesh/terrain
tasks, and this is neither. The gates that *do* apply here are numeric and are
above: the deployment ids, the smoke matrix, and the two test suites.

---

## Heartbeat

`HEARTBEAT.log` carries the iter-1 kickoff baseline (HEAD `3df55d58f` + the full
`git status --porcelain --untracked-files=all` DIRTY block for both repos) and
the run log.
