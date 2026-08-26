# Implementer Report — `content_player_inventory`

> **Phase 4, the last piece.** Implemented DIRECTLY by the main Claude Code thread at Cesar's
> instruction, NOT through the `golfin-implementer` subagent chain — so there is no `SELF_REVIEW.md`
> or red-team pass on this one. It goes straight to Cesar.
>
> There is no canonical screenshot and no Figma node: this task has no in-game UI surface. Its
> visual artefact is the admin panel, verified in mock mode and captured below. The pipeline's
> screenshot/Figma/lint gates (Rules 14, 17, 18, 19, 21) do not apply — nothing in this task
> authors a prefab or a scene object.

## Implementation summary

The player's inventory now lives server-side as **one JSONB blob per player** on
`profiles.golfin_inventory`, written by the client behind a 30-second coalescing window and read
back at boot, merged **additively**. An admin can see a tester's inventory in the Users drawer and
issue **additive-only, idempotent grants** through a queue the client drains at launch. The blob is
**deltas from the catalog default** — a club sitting at its catalog default is stored as a bare id
string, which is both the cost constraint and the mechanism that makes catalog rebalances reach
every untouched instance for free.

Three code bases: the FastAPI backend + a migration (in `~/Documents/playlife`, a separate repo),
a new `Golfin.InventorySync` assembly plus a save-schema bump in Unity, and an Inventory tab in the
admin dashboard.

## Files modified or created

### Backend — `~/Documents/playlife` (separate repo)

| Path | Change |
|---|---|
| `backend/migrations/2026_08_26_golfin_inventory.sql` | created — `profiles.golfin_inventory` / `_rev` / `_at`, and the `golfin_pending_grants` table (RLS on, no policies, `amount > 0` CHECK). **APPLIED to prod 2026-08-26.** |
| `backend/routers/golfin_inventory.py` | created — `GET`/`PUT /api/v1/user/golfin-inventory`, `GET /api/v1/user/golfin-grants`, `POST /api/v1/user/golfin-grants/ack`. Auth required; `user_id` stamped from the token. |
| `backend/main.py` | modified — mounts the router on the `/api/v1/user` prefix (a second router on that prefix, the way `tournaments_golfin` rides `/api/v1/tournaments`). |
| `backend/tests/test_golfin_inventory.py` | created — 15 tests against the shipped coroutines with an in-memory fake Supabase. |

### Unity — `GolfinRedux`

| Path | Change |
|---|---|
| `Assets/Scripts/InventorySync/Golfin.InventorySync.asmdef` | created — refs `Golfin.Save`, `Golfin.Net`, `Golfin.Auth`. |
| `Assets/Scripts/InventorySync/InventorySnapshot.cs` | created — the synced subset, and the three-way moves/server-owned/device-local split stated where the code is. |
| `Assets/Scripts/InventorySync/IInventoryCatalog.cs` | created — the catalog-default seam + `EmptyInventoryCatalog` (no compression, never wrong). |
| `Assets/Scripts/InventorySync/InventoryProjector.cs` | created — `SaveData` ↔ snapshot. `Apply` is the additive fold that cannot subtract. |
| `Assets/Scripts/InventorySync/InventoryMerge.cs` | created — union ids, max levels/quantities, OR ownership, `-1` unlimited beats every finite count. |
| `Assets/Scripts/InventorySync/InventoryCodec.cs` | created — the wire format: bare id when a row is at catalog default, otherwise `{id, …only the differing fields}`. |
| `Assets/Scripts/InventorySync/InventoryGrants.cs` | created — grant DTOs + the apply-once ledger logic. |
| `Assets/Scripts/InventorySync/InventoryWriteBehind.cs` | created — the ≤1 PUT / 30 s + force-on-pause rule, with an injected clock. |
| `Assets/Scripts/InventorySync/IInventoryTransport.cs` | created — the network seam + `ApiInventoryTransport` (ApiClient-backed). |
| `Assets/Scripts/InventorySync/InventorySyncService.cs` | created — boot read → merge → apply → drain grants; write-behind push with a single additive stale-rev retry. |
| `Assets/Scripts/InventorySync/InventorySyncBehaviour.cs` | created — self-bootstrapping host: `SaveDataHost.OnSaved`, frame clock, pause/quit flush. |
| `Assets/Scripts/InventorySync/Tests/*` (5 files + asmdef) | created — 55 EditMode tests. |
| `Assets/Scripts/InventoryCatalogAdapter.cs` | created (Assembly-CSharp) — builds the catalog defaults from `ClubManager` / `CharacterManager` and installs them into the sync service. |
| `Assets/Scripts/Save/SaveData.cs` | modified — `appliedGrantIds` (schema v11), the client half of the grant idempotency lock. |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | modified — `CurrentSchemaVersion` 10 → 11 + the v10→v11 step. |
| `Assets/Scripts/Net/Endpoints.cs` | modified — `UserGolfinInventory`, `UserGolfinGrants`, `UserGolfinGrantsAck`. |
| `Assets/Scripts/ClubManager.cs` | modified — `BuildCatalogSpecs()` made public so the adapter reuses the rarity → starting-level table instead of re-deriving it. |
| `Assets/Scripts/CharacterManager.cs` | modified — `BuildCharacterClampDefinitions()` made public, same reason. |

### Admin — `Tools/admin-dashboard`

| Path | Change |
|---|---|
| `lib/inventoryData.ts` | created — reads `profiles.golfin_inventory` + the grant queue, decodes the blob shallowly (a bare id renders as "default" because this app has no catalog). |
| `lib/inventoryMutations.ts` | created — `issueInventoryGrant`, additive-only, `checkAdmin()` + `writeAudit()`. |
| `lib/mockInventory.ts` | created — deliberately absurd fixtures (`club_MOCK_NOT_REAL`, every count 9999). |
| `app/api/users/[id]/inventory/route.ts` | created — GET (blob + grants) / POST (queue a grant). |
| `app/(panels)/users/inventory-tab.tsx` | created — the tab, incl. the red "NOT server-enforced" notice. |
| `app/(panels)/users/action-modals.tsx` | modified — `GrantInventoryModal`. |
| `app/(panels)/users/user-drawer.tsx` | modified — Inventory tab + "Grant items" action. |
| `lib/types.ts` | modified — inventory/grant types + `INVENTORY_GRANT_KINDS`. |
| `lib/i18n.ts` | modified — 43 new EN + JA keys. |

### Everything else in `git status` is pre-existing (Rule 13)

Every uncommitted path outside this task's folder that is NOT in the tables above was already dirty
at session start and is not mine. The exact list is quoted in `HEARTBEAT.log`'s kickoff baseline
block: `Assets/Localization/LocalizationManager.cs`, `Assets/Scenes/ShellScene.unity`,
`Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs`,
`Assets/Scripts/Save/Tests/{ClubOwnershipTests,GachaTicketTests,SaveLayerTests}.cs`,
`Assets/Scripts/Tournaments/Tests/Golfin.Tournaments.Tests.asmdef`,
`Assets/Scripts/UI/BuildInfo/AppVersion.cs`, `Docs/TellCode.md`,
`Docs/Versioning/last_uploaded_build.txt`, `_to_delete/**`,
`Assets/Scripts/Tournaments/Tests/TournamentSnapshotImmunityTests.cs`,
`Docs/Reports/perf_baseline_2026-08-26.md`, `tasks/quit_transition_demo/`.

## Screenshot

No in-game screenshot: this task adds no in-game UI. The visual surface is the admin panel, verified
live at `localhost:3111` in `MOCK_MODE=1` and shown to Cesar inline in chat:

- Users drawer → **Inventory** tab, EN — the red notice, `rev`, last-sync, blob size, the clubs list
  with the `DEFAULT` badge on the bare-id row and `lv=9999 sPow=99` on the delta row.
- Same tab, **JA** — `このインベントリはサーバーで検証されていません。`
- **Grant inventory** modal, and the queued grant appearing as `PENDING` with the admin's email.
- **Audit Log** panel showing `inventory_grant` / `golfin_pending_grants`.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Blob round-trips; a fresh install with no local save restores from it | PASS | `InventoryCodecTests.The_blob_round_trips_through_encode_and_decode` asserts every field back out; `InventoryProjectorTests.A_fresh_install_with_no_local_save_restores_the_whole_blob` applies a projected snapshot to a `SaveData.CreateFresh()` and asserts clubs/characters/items/balls/tickets/holes/starter/selected; `InventorySyncServiceTests.A_fresh_install_restores_from_the_server_and_owes_a_push_back` drives it through the real `Boot()`. Server-side: `test_put_then_get_round_trips_and_bumps_the_rev`. |
| Blob is deltas-from-default — a default-state club is just its id; paste a real blob and its byte size | PASS | `A_default_state_club_encodes_as_a_bare_id` asserts the literal `"clubs":["club_iron9_klyro"]`; `A_levelled_club_encodes_only_the_fields_that_differ` asserts `dur`/`maxDur`/`sPow` are ABSENT. **Measured, 40-club tester blob = 765 bytes** (logged by `A_realistic_tester_blob_is_small_and_the_bytes_are_reported`), pasted verbatim in § Measured blob below. |
| Write-behind coalesces: 10 rapid mutations produce ONE PUT, plus one on pause | PASS | `Ten_rapid_mutations_produce_exactly_one_put` → `PutCount == 1`. `A_pause_flush_adds_exactly_one_more_put_and_bypasses_the_window` → 1 then 2. `The_next_window_opens_after_thirty_seconds` pins the 30 s edge at t+20 (no) and t+31 (yes). `A_pause_with_nothing_pending_sends_nothing` → 0. |
| `rev` mismatch merges additively — union ids, max levels; nothing lost on either side | PASS | `A_stale_put_merges_additively_and_retries_once_losing_nothing`: exactly 2 PUTs, and the stored blob contains BOTH devices' clubs with the higher level. `InventoryMergeTests.The_merge_is_a_superset_of_both_sides_so_nothing_is_lost_either_way` applies the merge back onto each original and asserts it only ever adds. Server-side `test_a_stale_put_stores_nothing_and_hands_back_the_server_blob` proves the refusal does not overwrite. |
| A grant applies once and is idempotent across three boots | PASS | `A_grant_applies_once_and_is_idempotent_across_three_boots` runs three fresh sessions against the same save + server and asserts the quantity stays 3. `A_grant_whose_ack_was_lost_is_re_acked_but_not_re_applied` covers the apply-then-lost-ack window the client ledger exists for. Server-side `test_ack_is_idempotent_and_drains_the_queue`. |
| Admin shows a real tester's inventory and can grant; audit row written | PASS | Verified live in `MOCK_MODE=1`: the Inventory tab rendered the full blob (clubs / characters / items / balls / tickets / holes / starter / selected / raw disclosure / grants), the Grant modal queued `3× item_repair_kit`, it appeared as `PENDING` attributed to `cesar.guarinoni@wonderwall-g.com`, and the Audit Log showed `inventory_grant` on `golfin_pending_grants`. Screenshots shown in chat. Against PROD the data layer is verified directly — the two PostgREST selects `fetchPlayerInventory` runs both return live rows (see § Prod verification); the UI that renders them was exercised in mock, because prod-mode login needs real Supabase auth I cannot perform. |
| RP, leaderboard accumulators and tournament entries are NOT in the blob | PASS | Two independent locks, both tested. Client: `InventoryProjectorTests.Server_owned_and_device_local_fields_never_reach_the_wire` encodes a save carrying `rewardPoints=12345`, all four RP accumulators, the period keys, a tournament entry and `playedHoles`, then asserts each string is absent from the JSON. Server: `_STRIP_KEYS` drops them anyway, pinned by `test_server_owned_fields_are_stripped_from_a_stored_blob`. |
| Offline: no sync, no exception, local save unaffected | PASS | `Offline_syncs_nothing_throws_nothing_and_leaves_the_save_untouched` wraps boot + 5 ticks + a pause flush in `Assert.DoesNotThrow` and asserts the save is byte-identical and was never marked dirty. `A_failed_push_is_retried_on_the_next_window_not_immediately` proves the retry is paced, not a hot loop. |
| "Not server-enforced" notice visible on the panel | PASS | `uinv.notice.headline` / `uinv.notice.body` render as a red banner at the TOP of the Inventory tab, above any data — same treatment as the Shop panel's price notice (`sh.notice.*`, PLAN §11.5). Verified on screen in EN and JA. |
| `/health`, `/notices`, `/banners`, `/tournaments/golfin` still 200 after deploy | PASS | Migration applied by Cesar, `playlife-api` deployed **v51 → v52** (image `deployment-01M0XZD461YMEZZ2X53PFCYWGJ`, confirmed by `flyctl status`, never the exit code). All four re-probed after deploy: **200 / 200 / 200 / 200**. See § Prod verification. |
| Full unfiltered EditMode sweep green (baseline 1706 / 1703 / 0 / 3) | PASS | **1761 / 1758 / 0 / 3.** +55 tests, exactly the 55 this task adds (confirmed by a namespace-filtered run before the sweep), **zero failures**, the same 3 pre-existing `HoleCompleteDriverTests` skips. |

## Measured blob

40 starter-state clubs, one of them levelled, plus a character, items, tickets and holes —
**765 bytes**, well inside SPEC §1's ~3 KB budget. The 39 bare ids are the compression doing its job.

```json
{"v":1,"clubs":[{"id":"club_seed_0","lv":31,"sPow":6},"club_seed_1","club_seed_2","club_seed_3","club_seed_4","club_seed_5","club_seed_6","club_seed_7","club_seed_8","club_seed_9","club_seed_10","club_seed_11","club_seed_12","club_seed_13","club_seed_14","club_seed_15","club_seed_16","club_seed_17","club_seed_18","club_seed_19","club_seed_20","club_seed_21","club_seed_22","club_seed_23","club_seed_24","club_seed_25","club_seed_26","club_seed_27","club_seed_28","club_seed_29","club_seed_30","club_seed_31","club_seed_32","club_seed_33","club_seed_34","club_seed_35","club_seed_36","club_seed_37","club_seed_38","club_seed_39"],"characters":["char_ken"],"items":{"item_repair_kit":3},"tickets":{"0":10},"holes":[1,2,3],"starter":"char_ken","selected":"char_ken"}
```

## Prod verification — 2026-08-26

Both former blockers are cleared. **All 11 acceptance items PASS.**

**Migration applied** (Cesar, Supabase project `wmszyghwwkaptgqdunel`). All 7 verification rows as
expected: `inventory_column 1 · rev_column 1 · rev_not_null 1 · grants_table 1 · grants_rls 1 ·
grants_policies 0 · user_inventory_untouched 1`. That last row is the one that matters most — the
PARTNER APP's gift table is still there, unchanged.

The Supabase SQL editor warns *"creates a table without enabling RLS"* on the `create table`
statement. **False positive** — it lints that statement in isolation and the enable is three
statements later. `grants_rls = 1` is the proof it ran, and RLS-on-with-zero-policies is deny-all
for `anon`/`authenticated` while `service_role` bypasses: the intended posture, same as
`telemetry_events` and the four content tables.

**Deployed** `playlife-api` **v51 → v52**, image `deployment-01M0XVQSXZJVQQG2T71ZAR40DR` →
`deployment-01M0XZD461YMEZZ2X53PFCYWGJ`, confirmed by `flyctl status` and live probes — never the
deploy exit code, per the standing `flyctl`-401 scar.

| Probe | Result |
|---|---|
| `/health` · `/notices` · `/banners` · `/tournaments/golfin` | **200 · 200 · 200 · 200** — no regression |
| `GET`/`PUT /user/golfin-inventory`, `GET /user/golfin-grants`, `POST /user/golfin-grants/ack` | **403** unauthenticated — mounted and auth-gated, NOT a 404 route miss |
| Body of an unauthenticated call | `{"detail":"Not authenticated"}` — the auth gate, not a routing accident |
| Same call with a garbage bearer | **401** — the refresh-and-replay trigger `ApiClient` handles |
| PostgREST `select id,golfin_inventory,golfin_inventory_rev,golfin_inventory_at from profiles` | live rows, `null / 0 / null` — the never-synced state, and the **schema cache has the new columns** |
| PostgREST `select … from golfin_pending_grants` | `[]` — table selectable by `service_role`, queue empty |

The schema-cache probe is not ceremony: PostgREST caches its schema, so a column can exist in
Postgres while the API cannot see it. That failure is SILENT here by design — the router's
`_missing_relation` handler degrades a missing relation to "never synced" so deploy order does not
matter — which means it would have looked exactly like a healthy empty inventory. Checking it
directly is the only way to tell those apart.

**Not yet proven, and it is the device pass's job:** a full authenticated round-trip against prod.
That needs a real user JWT, which means signing in as a tester — so the honest end-to-end proof is
the two device checks: (a) play, background, wipe, reinstall, sign in, confirm the bag comes back;
(b) issue a grant from the admin drawer, relaunch, confirm it applied exactly once and did not
return on the launch after that.

## Spec deviations

Three, all deliberate, none of them narrowing the ask.

1. **Stamina condition is not in the blob.** SPEC §1 moves "`ownedCharacters` (level, SP,
   allocation)" — condition is none of those three, so excluding it follows the spec rather than
   departing from it, but it is worth naming because `PersistedCharacter` carries the field. It is a
   time-regenerating pool: an additive merge on it (take the max) would hand a player a free refill
   every time they touched a second device, which is a live economy exploit dressed as a sync rule.
   `InventoryProjector` zeroes it out and never writes it back.
2. **`equippedBagSlot` is not raised to the max on merge; the local device wins.** SPEC §3 says
   "take the max of levels and quantities" — a bag slot is neither. Maxing it would silently equip a
   club the player deliberately left out of the bag on this device, and there is no "more equipped".
   A club already present keeps this device's slot; a club arriving from the blob keeps the slot it
   arrived with.
3. **A stale PUT is a 200, not a 409.** The rev mismatch is a business outcome the client must
   handle, the same shape as the existing "taken" username and "insufficient" tournament-entry
   replies. A 409 would make `ApiClient` treat a normal two-device outcome as a failure and log it as
   one. The body carries the server's blob so the merge needs no extra round trip.

Also worth flagging, though not a deviation: **the server deliberately does not merge.** The merge
needs catalog defaults to expand a bare-id club, and those live in the client's bundled CSVs. A
server-side merge would be a second implementation of the same rules against data the server does
not have.

## Console output

No warnings or errors attributable to this task. The full EditMode sweep is 0 failed; the only
Unity warnings in the compile are the project's pre-existing `CS0618` obsolete-API warnings in
editor recorder scripts, none of them in files this task touches.

## Open questions for Architect

None on the implementation. One decision to confirm before this goes to testers:

- ~~When do you want the migration applied and the API deployed?~~ **Answered and done** — see
  § Prod verification. The Unity side is uncommitted and scoped, ready to commit on your approval. Say the word and I will run the migration verification query and
  re-probe the four endpoints.
