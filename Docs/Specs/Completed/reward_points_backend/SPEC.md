# SPEC — reward_points_backend (RP → one server ledger)

**Status:** SPEC_READY (Slice 1 + Phase A) — Phase A UNBLOCKED 2026-08-12: repo at `/Users/cesar/Documents/playlife` (fresh copy, mtimes 2026-08-12)
**Author:** Architect (Claude, Cowork session 2026-08-12) with Cesar
**Supersedes-in-part:** answers `GPS_UNITY_PORT_SPEC.md` §2 (points-ledger fork) — decision: **unify**.
**Decisions of record (Cesar, 2026-08-12):**
1. Reward Points move server-side **before** the admin dashboard build (dashboard kickoff ON HOLD — its points panel will then show the one true ledger).
2. Offline policy: **online-required spends, queued earns** (earns queue locally with idempotency keys and replay on reconnect; spends need a connection).
3. Backend (FastAPI playlife-api) edits happen in the repo on Cesar's Mac: `/Users/cesar/Documents/playlife` (backend in `backend/`).
4. **ONE shared RP value** (Cesar, 2026-08-12 — overrides the interim `golfin_rp` proposal): GOLFIN RP *is* the PLAYLIFE points balance (`total_points` = `activity_pts` + `gift_pts`). No third currency. Game prices/rewards get rebalanced to the shared GPS scale, and **that rebalance is folded into this task** — Slice 2 ships rebalance + cutover together (Cesar, same day). Avatar-XP coupling on earns is retained: one economy, one growth track.
5. **Cesar is in charge of the GPS app too** (2026-08-12) — all former "Ken's nod" items are Cesar's own calls; no cross-team sign-offs remain.
6. **No welcome grant** (Cesar, 2026-08-12): the 50,000 client seed was for testing only and is NOT ported. New accounts start at 0 RP; test balances are granted manually by the admin (dashboard points panel once it exists; Supabase table editor/SQL until then). `RP_REBALANCE.md` approved same day with this amendment (level-up = ceil(level/2), stamina global rounding rule, §3 caps — all approved).

---

## 1. Goal

`RewardPointsManager` stops being the source of truth. The Supabase
`points_transactions` ledger (already live for the GPS/PLAYLIFE app) becomes the
one shared Reward Points balance, server-authoritative, visible to both apps and
to the upcoming admin dashboard. This is also the first slice of Track B
(cloud save) — one effort, two payoffs.

## 2. Current state (verified 2026-08-12)

**Client (all paths real):**
- `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` — singleton façade
  over `SaveDataHost.Data.rewardPoints`. API: `GetPoints()`, `CanAfford(int)`,
  `SpendPoints(int)→bool` (sync), `EarnPoints(int)` (also feeds leaderboard
  accumulators `rpDaily/rpWeekly/rpMonthly/lifetimeRpEarned` via
  `NetworkTimeProvider` + fires `SfxBus.Play(SfxId.RpEarn)`), `SetPoints`,
  `ResetToDefault`, event `OnPointsChanged(int)`. `DEFAULT_STARTING_POINTS = 50000`
  seeded on first run.
- Known call sites (from repo search, for the Slice-2 inventory):
  spends — `CharacterManager` (level-up cost), `UI/Inventory/ClubLevelUpModalController`,
  `TournamentsRuntime/RewardPointsServiceAdapter` (implements
  `ITournamentSeams.IRewardPointsService.TrySpend/Grant` for
  `LocalTournamentBackend` entry fees/prizes), `UI/Tournaments/TournamentSignupModalController`
  (pre-check), `UI/ModeSelect/ModeCardController` (fee check + `OnPointsChanged` subscriber);
  earns — `UI/RewardGranter` (`RewardType.Points`, hole complete), Versus RP grant
  (`GameSession.OnMatchComplete` → `VersusResultHandler`), tournament prize claim;
  debug — `Debug/RewardPointsDebugPanel` (`SetPoints`).
- Auth: Supabase session (email/Google/Apple) verified on-device 2026-08-11/12 —
  the JWT needed for `/points/*` calls exists in the client. (NOTE: confirm exact
  accessor symbol from the auth-epic code before wiring; do not guess.)

**Server (verified against `backend/routers/points.py` + `backend/migrations/2026_06_29_points_atomic.sql`, 2026-08-12):**
- `GET /points/balance` → `{activity_pts, gift_pts, total_points, avatar_level, avatar_xp}`;
  `GET /points/history?skip=&limit=&currency=` over `points_transactions`
  `(user_id, type, amount, currency, description, created_at)`;
  `POST /points/earn?action=` — fixed catalog `ACTIVITY_PTS_REWARDS`
  (screenshot 50, gps_checkin 30, vote_cast 10, vote_hit 30, daily_login 5, game_play 10),
  delegates to the **atomic `earn_activity_pts` Postgres function** (security definer,
  EXECUTE revoked from public/anon/authenticated, service_role only);
  `POST /points/redeem` = placeholder ("現金化機能は法務確認後に実装予定" — cash-out, legal-gated; leave untouched).
- ⚠️ `earn_activity_pts` couples every earn to avatar growth: `avatar_xp += pts`,
  level-up each `level*500` XP. PLAYLIFE amounts are 5–50 pts; the game today
  runs at 50,000-start scale. Under the one-value decision this coupling is
  correct and kept — but it is why the flag CANNOT flip until the game-side
  rebalance lands (§4 Rebalance): current game amounts through this path would
  explode avatar levels and PLAYLIFE totals.
- The router *depends* on the RPC, so the atomic migration is probably applied in
  prod — **verify the function exists in Supabase before building on it** (the
  2026-07 eval note claiming "unapplied" predates the current router).
- Base: `https://playlife-api.fly.dev/api/v1`, Bearer Supabase JWT, `{data:…}` envelope.

## 3. Phase A — backend (`/Users/cesar/Documents/playlife/backend`). UNBLOCKED; kickoff issued 2026-08-12.

**Design (one-value, Cesar 2026-08-12): GOLFIN RP == the existing PLAYLIFE
balance.** The game reads `total_points` as its RP, earns post to `activity_pts`
through the existing atomic/avatar-coupled path, and spends debit the same
buckets. The backend work is scale-agnostic — it lands now; the game-side
rebalance (§4) gates when the game starts writing.

1. **Migration `backend/migrations/2026_08_12_points_spend_idempotency.sql`** —
   mirror the points_atomic file's conventions (idempotent CREATE OR REPLACE,
   the revoke-from-public/anon/authenticated + grant-to-service_role security
   block on EVERY new function, staging verification footer):
   - `points_transactions.idempotency_key uuid` + partial unique index
     `(user_id, idempotency_key) where idempotency_key is not null`.
   - `public.earn_pts_v2(p_user_id uuid, p_action text, p_pts int, p_description text, p_key uuid)`
     — same body/semantics as `earn_activity_pts` (activity_pts + total_points +
     avatar_xp increment, level-up loop, ledger row — avatar coupling INTACT)
     plus idempotent replay: on key conflict return the original result, no
     double credit. Leave `earn_activity_pts` itself untouched (PLAYLIFE app
     keeps calling it).
   - `public.spend_pts(p_user_id uuid, p_amount int, p_reason text, p_key uuid)`
     — one transaction, row-locked like earn: require
     `activity_pts + gift_pts >= p_amount` else distinct `insufficient` result;
     debit **activity_pts first, then gift_pts** (order = server-side constant;
     revisit only if paid IAP ever feeds gift_pts — §6.2); keep `total_points` consistent;
     negative ledger row(s) with the bucket split; **no avatar_xp change on
     spend**; idempotent like earn.
   - New `game_point_actions` table
     (`action pk, pts int null, max_per_event int null, daily_cap int null, once_per_user bool default false`):
     fixed server amount when `pts` is set, otherwise client amount validated
     against caps (needed for variable payouts like tournament prizes). Seed
     `hole_complete`, `hole_replay`, `versus_win`, `tournament_prize` — final
     values are in `RP_REBALANCE.md` §3 (approved 2026-08-12); NO welcome-grant
     or migration actions (decision of record #6 — new accounts start at 0,
     test balances are admin-set).
2. **Router (`backend/routers/points.py`, existing style — `{data}` envelope,
   `get_current_user`, service client):**
   - `POST /points/earn-game` `{action, amount?, idempotency_key}` → resolve
     amount (catalog-fixed, else validated client amount) → rpc `earn_pts_v2`.
   - `POST /points/spend` `{amount, reason, idempotency_key}` → rpc `spend_pts`;
     insufficient → explicit error payload the client can branch on.
   - `GET /points/balance` unchanged — `total_points` IS the game's RP balance.
   - Leave `/points/earn` and `/points/redeem` untouched.
3. **Verify-first:** confirm `earn_activity_pts` exists in prod Supabase (and
   whether `score_submit_atomic` is applied) before layering the new migration.
4. **Apply + deploy:** Cesar applies the SQL in the Supabase dashboard (or the
   Architect drives it via the browser session, staging block first); then
   `fly deploy` from `backend/` (app `playlife-api`, region nrt) — flag in the
   report if flyctl isn't authenticated on this Mac.

## 4. Phase B — Unity

### Slice 1 (kickoff NOW — no backend dependency, no behavior change)
- New asmdef `Golfin.Net`: `ApiClient` singleton (UnityWebRequest, Bearer attach,
  `{data}` unwrap, retry on 408/connection failure, **401 → refresh → retry once**),
  static `Endpoints` (only `/points/*` + `/health` for now), `ApiResult<T>`.
- `PointsService` singleton (`Golfin.Economy`): `RefreshBalanceAsync()`,
  cached server balance, and a **persistent pending-ops queue** (JSON in
  `Application.persistentDataPath`, one idempotency GUID per op, FIFO replay on
  connectivity/login regained; earn ops only in v1).
- **Feature flag `PointsBackendEnabled`, default OFF** — with the flag off the
  game is byte-identical in behavior. Flip mechanism at implementer's discretion
  (const + debug-panel toggle acceptable).
- EditMode tests: queue round-trip serialization, idempotency-key stability,
  replay ordering, ApiClient envelope/401 paths (mocked transport).
- Manual acceptance: flag ON + logged in on device/simulator →
  `RefreshBalanceAsync` logs the test account's server balance.

### Rebalance (part of Slice 2 — GATES the flag flip; Cesar 2026-08-12)

The game economy converts to the shared GPS scale (reference anchors:
daily_login 5 · game_play 10 · gps_checkin 30 · vote_hit 30 · screenshot 50)
**before** the game writes to the server balance. Deliverable: an
`RP_REBALANCE.md` table (Architect drafts, **Cesar approves every number**)
covering every RP-denominated value in the game — `CharacterLevelUpDatabase`
level-up costs, club level-up costs, hole-complete rewards (`RewardGranter`
data), versus win grant, tournament entry fees + prize tables
(`tournaments.csv`, `tournament_prizes.csv`), mode entry fees (`modes.csv`),
and the debug panel deltas. Claude Code then applies the CSV/code edits and
mirrors the final amounts into the server `game_point_actions` seed. The
`PointsBackendEnabled` flip and the rebalance ship in the same Slice-2 cutover.
**✅ APPROVED by Cesar 2026-08-12** (with the welcome grant removed — decision
of record #6): `RP_REBALANCE.md` in this folder is now the binding number set.

### Slice 2 (after Phase A lands + rebalance table approved; own kickoff)
- `EarnPoints(int)` grows a reason parameter at call sites; when flag ON it also
  enqueues `earn-game` (local balance/leaderboard/SFX behavior unchanged — the
  queue reconciles the server).
- Spend flows go async: `PointsService.SpendAsync(amount, reason)` → server debit
  precedes the action. All spend call sites are UI/modal flows (level-up, club
  level-up, tournament signup, mode entry), so awaiting a round-trip with a
  busy state fits; `IRewardPointsService` gains an async variant for
  `LocalTournamentBackend.Register`. Exact seam adaptation is implementer's
  call under the minimal-diff rule — spec the contract, not the plumbing.
  Offline spend → existing toast pattern ("Connection required").
- `SetPoints`/`ResetToDefault`/debug panel: flag-OFF only (guard, don't delete).
- Remove the client seed: `DEFAULT_STARTING_POINTS` (50,000) is testing-only and
  does NOT survive cutover — when the flag is ON, fresh saves start at the
  server balance (0 for new accounts), and `RewardPointsManager.Awake` skips the
  seed (decision of record #6).
- Cutover: admin-set test balances (§5), then flag ON by default; local save
  keeps working as the offline cache.

## 5. Starting balance & cutover (simplified 2026-08-12 — no migration logic)

No client-side migration and no welcome/migration grant actions. New accounts
start at 0 RP. The 5 test accounts get their balances set by hand before the
flag flips — via the admin dashboard's points panel once it exists, via the
Supabase table editor/SQL until then (Cesar has admin). At first flag-ON login
the client simply trusts the server balance and overwrites the local cache.
Leaderboard accumulators stay local in v1 (fed from `EarnPoints` exactly as
today).

## 6. Open items

1. ~~Backend repo location~~ ✅ RESOLVED 2026-08-12: `/Users/cesar/Documents/playlife`.
2. Spend debit order (activity_pts first, then gift_pts) — Cesar's call now (he
   runs GPS too); revisit only if paid IAP ever feeds gift_pts (JP prepaid rules,
   資金決済法, may constrain which bucket spends draw from).
3. Supabase free-tier auto-pause — upgrade before the game depends on `/points/*`
   at runtime (a paused project would fail every spend).
4. ~~Welcome grant~~ ✅ REMOVED 2026-08-12 (decision of record #6) — admin-set
   balances replace it.

## 7. Sequencing

Slice 1 (now) ∥ Phase A (now) → ~~rebalance table~~ ✅ approved 2026-08-12 →
Slice 2 (rebalance CSV/code edits + client-seed removal + re-point call sites +
flag flip, one cutover) → `admin_dashboard` resumes (its v1.5 points panel reads
this ledger). Estimates: A ≈ 1 session, Slice 1 ≈ 1 session, Slice 2 ≈ 2
sessions.
