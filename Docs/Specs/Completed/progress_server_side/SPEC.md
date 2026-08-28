# SPEC — `progress_server_side`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-28 (Architect via Cowork). The last leg of Cesar's original 2026-08-27 ask
> ("move progression to the server — RPs, unlocked holes, levelled characters and gear"):
> RP has been authoritative since the ledger day; inventory syncs as a blob; the shop made
> purchases authoritative. This makes **level-ups** authoritative on the same pattern.
> Decision of record 2026-08-28: **grandfathering = trust the claimed level once** — the first
> server level-up per (player, character/club) seeds the server record from the client's
> claimed `from_level`, cross-checked against the inventory blob with mismatches logged;
> from then on levels advance only through paid, recorded steps.
>
> Priority per Cesar: this runs BEFORE `game_modes_admin` (which stays SPEC_READY, queued).
> PIPELINE_HARDENING §21 and §23 apply: the live E2E must run, and the dashboard half is not
> done until deployed with the deployment id quoted.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Today a level-up debits RP server-side (`PointsSpendGate` with reason `character_level_up` /
`club_level_up`) but the AMOUNT is client-computed from bundled `LevelUpCosts.csv` and the
LEVEL itself is client-asserted — a modified client can level for free (skip the gate, write
the save) or level cheap (send amount 1). The server pays out real RP for tournament play
against stats those levels produce, so this is the one remaining self-grant that touches
competition.

After this task a level-up is **one server call** that reads the **published** cost table,
debits through `spend_pts`, and records the new level in a server-owned progress table — the
`shop_server_purchase` shape, applied to progression. Costs also become **admin-tunable**:
`LevelUpCosts` becomes the **ninth content catalog** (answering `CONTENT_PIPELINE_PLAN.md`
§9.2 — "the tuning knob most likely to be wanted mid-beta").

**Hole unlocks are deliberately OUT.** They are free and gameplay-derived (complete hole N →
`HoleProgressionService` unlocks N+1), already stored server-side via the inventory blob, and
"server-authoritative" would require server-side gameplay validation that exists nowhere
outside tournaments. Nothing is bought, so nothing can be wrongly spent — the invariant is
already satisfied. Same for **SP allocation**: it costs no RP and its totals are derivable
from the recorded level (`sp_reward` per step), so the server records levels, not builds.

## 1. What is true today (verified 2026-08-28)

| Piece | State |
|---|---|
| `Assets/Data/LevelUpCosts.csv` | 240 rows, `level,cost_r,sp_reward` (1..240; L239/240 cost 120); NOT a content catalog; read by `CharacterLevelUpDatabase` (`GetLevelUpCost(toLevel)`, `GetSpReward`, `GetMaxLevel`) — used by BOTH modals |
| Character commit | `LevelUpModalController` (~:467): multi-level preview accumulates `totalRPCost`, ONE `PointsSpendGate.Spend(totalRPCost, SpendReasons.CharacterLevelUp, () => CommitLevelUps(...))`, which calls `CharacterManager.LevelUp(characterId)` once per previewed level (each deducts local RP + adds SP) |
| Club commit | `ClubLevelUpModalController` (~:445): same shape, `SpendReasons.ClubLevelUp`, `ClubManager.LevelUp(clubId)` |
| Server | debits whatever amount arrives with those reasons; no record of levels; `maxLevel` lives on the `characters` / `clubs` content rows (`data->>'maxLevel'`) |
| Blob | `ownedCharacters`/`ownedClubs` carry levels, client-asserted, additive-merge (max of levels) |
| Precedent | `golfin_shop_purchase()` — content-priced, `spend_pts`-debited, idempotent, one transaction; `fee_changed`/`price_changed` client UX; legacy-reason closure pattern |

## 2. `level_up_costs` — the ninth content catalog

- `Tools/content/catalogs.py` += `Catalog("level_up_costs", "Assets/Data/LevelUpCosts.csv", "level")`.
  Export/import/`--check` pick it up from the table.
- Seed migration `2026_08_28_content_level_up_costs_seed.sql` via `seed_from_csv.py
  --catalogs level_up_costs`; first export byte-identical (the round-trip acceptance).
- Admin: **Level Costs panel** via the shared `CatalogPanel` (240 rows — enable the same
  pagination clubs uses). `REQUIRED_COLUMNS = [level, cost_r, sp_reward]`, all numeric.
  Publish validation: `cost_r ≥ 0`, `sp_reward ≥ 0`, `level` unique, and **contiguous
  coverage**: every level 1..max(`characters`/`clubs` `maxLevel`) has an active row — a gap
  is a level nobody can buy, which the client renders as a dead button and the server as
  `costs_missing`. Blocking. EN + JA `DICT` entries.
- Client: `CharacterLevelUpDatabase` gains the overlay (bundled row + patch by `level`,
  appended rows admitted, `RequireReady` for EditMode) — the standard
  `content_overlay_catalogs` treatment. Next-launch effect (I5). Both modals then preview
  admin-tuned costs with no further change.

## 3. Backend — migration + function + router

### 3.1 Migration `2026_08_28_golfin_progress.sql` (full SQL in chat for Cesar; verification block; RLS on, no policies on both tables)

```sql
create table if not exists public.golfin_progress (
  user_id    uuid not null,
  kind       text not null check (kind in ('character','club')),
  ref_id     text not null,
  level      integer not null check (level >= 0),
  grandfathered_from integer,          -- non-null on the seed row: the claimed from_level
  updated_at timestamptz not null default now(),
  primary key (user_id, kind, ref_id)
);
create table if not exists public.golfin_progress_events (
  id              uuid primary key default gen_random_uuid(),
  user_id         uuid not null,
  kind            text not null,
  ref_id          text not null,
  from_level      integer not null,
  to_level        integer not null,
  cost_rp         integer not null,
  idempotency_key uuid not null,
  created_at      timestamptz not null default now(),
  unique (user_id, idempotency_key)
);
```

### 3.2 `golfin_level_up(p_user_id, p_kind, p_ref_id, p_from, p_to, p_expected_cost, p_key, p_build) returns json`

`security definer`, EXECUTE revoked from public/anon/authenticated — the `golfin_shop_purchase`
posture, and its refusal style: **every business outcome is json, only faults raise.**

1. **Replay**: event exists for `(user, key)` → rebuild the `ok` shape from it, `replayed: true`,
   current balances.
2. **Kill switches**: global `content_settings.content_enabled` false or `level_up_costs`
   catalog disabled → `not_available / disabled` (if the operator pulled the cost table, the
   server must not price from it — the shop's rule 2, verbatim).
3. **Reference**: `p_kind ∈ character|club`; the ref exists + `is_active` in the matching
   catalog and `min_build ≤ p_build` → else `not_available / ref`. `p_to > p_from ≥ 0`;
   `p_to ≤ (data->>'maxLevel')::int` → else `invalid_range`. Cap the step count
   (`p_to - p_from ≤ 50`) — the modal previews a handful; 50 bounds a hostile loop.
4. **Cost**: sum of ACTIVE `level_up_costs` rows for levels `p_from+1 .. p_to`
   (`(data->>'cost_r')::int`); any level missing → `costs_missing` naming it.
   `p_expected_cost` present and ≠ sum → **`cost_changed`** with the sum (the shop's
   `price_changed`, renamed).
5. **Level guard**: progress row exists → `p_from` must equal its `level`, else
   `level_conflict` with the server's level. Row absent → **grandfather** (decision of
   record): seed at `p_from`, stamp `grandfathered_from = p_from`, and best-effort
   cross-check `profiles.golfin_inventory` (wrapped in an exception block — a malformed blob
   must not block a level-up): blob level ≠ claim → include `blob_level` in the response and
   log a warning server-side; proceed regardless.
6. **Debit**: `spend_pts(p_user_id, cost, 'progress:'||p_kind||':'||p_ref_id||':L'||p_to,
   p_key)`; `insufficient` returned as-is (nothing written). The reason string makes every
   level-up ledger-legible in the Points panel with no admin change.
7. **Record**: update progress `level = p_to`; insert the event. One plpgsql transaction —
   a failed insert rolls back the debit.

`ok` response carries `kind, ref_id, level, cost, grandfathered?, blob_level?` plus **every
`PointsSpendResult` field**, so the client folds the balance with `ApplySpendResult` unchanged.

### 3.3 Router `routers/progress.py` at `/api/v1/progress`

`POST /level-up`, `Depends(get_current_user)`, user id from the token never the body. Body
`{kind, ref_id, from_level, to_level, expected_cost, idempotency_key, build}`; validate the
key as UUID, kind in the pair, levels ints ≥ 0, ref_id ≤ 120 chars. All business outcomes 200.
No `_missing_relation` courtesy — an unapplied migration must 500, never fake-succeed.
Tests `test_progress_level_up.py`, fake-Supabase style: 403 unauth / 400 bad key / each status
passes through. Deploy, `flyctl status`, smoke: existing routes 200, `/progress/level-up`
403-not-404.

## 4. Client

- **`ProgressService`** in `Golfin.Economy` — mirror `ShopPurchaseService` exactly: `Instance`,
  `ConfigureForTest`, flag gate INSIDE the routine, own in-flight latch, fresh key per attempt,
  fold via `PointsService.ApplySpendResult` in a `finally` AFTER `onDone`. DTO
  `ProgressLevelUpResult` in `PointsDtos.cs`; outcome verdicts
  `Ok, Insufficient, CostChanged, LevelConflict, NotAvailable, Unavailable, Disabled`.
  `Endpoints.ProgressLevelUp`.
- **Both modals**, flag ON: replace the `PointsSpendGate.Spend(totalRPCost, …)` commit with
  `ProgressService.Instance.LevelUpAsync(kind, refId, currentLevel, previewLevel, totalRPCost,
  ContentBuildNumber.Current, outcome => …)`:
  - `Ok` → the existing commit body unchanged (`CommitLevelUps` / the club loop — each
    `LevelUp()` still deducts local RP per level; the server debited the same sum, and
    `ApplySpendResult` runs after, so the ordering note above `SpendRoutine` still holds).
  - `Insufficient` → `PointsSpendGate.InsufficientMessage` toast, clear busy.
  - `CostChanged` → reload `CharacterLevelUpDatabase` (overlay), rebuild the preview at the
    same target level with fresh costs, toast the price-updated copy; second CONFIRM pays.
  - `LevelConflict` → toast, close the modal, `InventorySyncService.Instance.MarkDirty()` (the
    next sync's additive merge reconciles; the modal reopens on reconciled state).
  - `Unavailable`/`NotAvailable` → `PointsSpendGate.OfflineMessage` toast, clear busy.
  - Flag OFF: today's path, byte-identical.
- Tests (EditMode): wire shape; each status → verdict; flag OFF no transport; latch; the
  overlay on `CharacterLevelUpDatabase` (patched cost read by `GetLevelUpCost`); a
  `CostChanged` rebuild recomputes `totalRPCost` from the reloaded DB.

## 5. Legacy closure (separate commit, Cesar's word — the §2.6 pattern)

Once testers are on the build carrying §4, `/points/spend` refuses reasons
`character_level_up` and `club_level_up`. Until then old builds still level at client-computed
cost; enforcement is only as good as the oldest build in the wild.

## 6. Sequencing — DEPLOYMENT IS PART OF THE TASK, not an epilogue

Three deploys are IN this task. Skipping any of them means the task is NOT DONE, whatever the
tests say. Each produces an id/hash that goes in IMPLEMENTER_REPORT.md; the architect review
fails any missing one on sight (§23).

0. **Dashboard backlog FIRST** — the remediation that is still outstanding: `npm run deploy`
   ships the three local-only commits (`15f2553f1` upload UI, `c15998c30` WebP-only,
   `541864b38` badge) **plus the `/api/version` + footer commit stamp**, before any new panel
   work stacks on top. Verify per `ADMIN_DASHBOARD_OPS.md` §2 (Access curl 302) and quote the
   deployment id. This step exists so the backlog cannot ride along silently or be forgotten
   again.
1. playlife: §3.1 SQL in chat → Cesar applies → §3.2/§3.3 → tests → **`flyctl deploy`, image
   id confirmed via `flyctl status`** → smoke (403-not-404 on `/progress/level-up`).
2. Content: §2 catalog + seed → Cesar applies → export round-trip byte-identical.
3. Admin: Level Costs panel + validation → **`npm run deploy` AGAIN** (this task's own
   dashboard half), deployment id quoted, **`/api/version` returns HEAD's hash** — checked by
   curl, not by memory.
4. Unity: §4 → full EditMode sweep.
5. **Live E2E (§21, the world-check):** on a prod account, level a character one step from the
   app — verify the ledger row (`progress:character:<id>:L<n>`), the `golfin_progress` row
   (grandfathered seed), and the event row by SQL; then publish a cost change in the admin and
   confirm a stale client gets `cost_changed` and pays the new sum on the second tap.
6. §5 on Cesar's word, later.

## 7. Acceptance

- [ ] Live E2E above, all three server rows verified by SQL, `cost_changed` observed. *(runs)*
- [ ] First level-up for a ref seeds `golfin_progress` with `grandfathered_from` = claimed
      level; blob mismatch logs and includes `blob_level`, does not block.
- [ ] Second level-up with a stale `from_level` → `level_conflict`, nothing debited.
- [ ] Multi-level commit (preview 3 levels) → ONE debit of the summed cost, ONE event,
      progress at the target level; replay of the same key → `replayed`, no second debit.
- [ ] A gap published into `level_up_costs` is refused by the validator; forced via SQL, the
      server answers `costs_missing`.
- [ ] `p_to` above the ref's `maxLevel` → `invalid_range`; deactivated ref → `not_available`.
- [ ] Kill switch off → `not_available / disabled`; back on → works. No deploy either way.
- [ ] `level_up_costs` round-trips (seed → export byte-identical → `--check` clean; import on
      a hand-edit); Tools tests green with the ninth catalog.
- [ ] **Three deployment proofs in the report**: (a) dashboard backlog deployment id (§6
      step 0) + the live admin showing the badge and upload UI; (b) `flyctl status` image id
      for the API deploy; (c) this task's dashboard deployment id with `/api/version` == HEAD,
      by curl. A report missing any of the three is an automatic architect FAIL.
- [ ] Flag OFF byte-identical (existing modal tests green); full EditMode sweep green;
      backend suite green.
- [ ] Holes and SP allocation untouched — grep confirms no diff in `HoleProgressionService`
      or the SP allocation path.

## Out of scope

- Hole unlocks and SP allocation (§ Goal — reasons stated there).
- Closing `character_level_up` / `club_level_up` (§5, Cesar's word).
- `game_modes_admin` (queued behind this per Cesar), stamina shop, durability/repairs.
- Any change to the inventory blob shape or merge (max-of-levels already agrees with a
  server-recorded level).
