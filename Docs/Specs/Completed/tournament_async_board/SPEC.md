# SPEC — `tournament_async_board`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Current: `SPEC_READY`. **Unity client only** — the server half is built by
the Architect (Phase-4 endpoints in `playlife/backend/routers/tournaments_golfin.py`; deploy
status in TellCode CURRENT STATE — do not start before it says the endpoints are live).

## Goal

Tournaments become real async multiplayer: entry, per-hole submission and the leaderboard move
to the backend, so **every player sees the same board**. This is Phase 4 of the
`tournaments_server_side` epic (§6b); Phase 5 (resolver + server-side payout) is a separate
future task.

Decisions of record (Cesar, 2026-08-18): board first, payout after; bots retire one-way at
**10 human entries** and are **removed from the ranking**; the server sends **both ranks** —
display rank (blended) and `prize_rank` (human-only) — and the player's sticky row shows both
while bots are active (e.g. "#14 · PRIZE #3").

## What already exists (do NOT rebuild)

| Layer | What exists | Where |
|---|---|---|
| Seam | `ITournamentBackend` — "Later: RemoteTournamentBackend (REST). UI code never changes." | `Assets/Scripts/Tournaments/ITournamentBackend.cs` |
| Local impl | `LocalTournamentBackend` — bot sim, entry store, provisional/final comparators. STAYS as the bot/signed-out/offline-fallback path. | `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` |
| Definitions | Server schedule already live — `TournamentService.Compose()` + `GET /tournaments/golfin` | `Assets/Scripts/TournamentsRuntime/TournamentService.cs` |
| Net | `ApiClient` `Get<T>`/`Post<T>` (+`Put<T>` landing via `leaderboard_backend`), 401-refresh, transient retries | `Assets/Scripts/Net/ApiClient.cs` |
| Queue | Pending-ops pattern — idempotency key per op, FIFO replay on reconnect, atomic file store | `Assets/Scripts/Economy/PendingOpsStore.cs` |
| UI | `TournamentLeaderboardScreenController` + widgets binding `TournamentLeaderboardEntry` | `Assets/Scripts/UI/Tournaments/` |
| Signup | `TournamentSignupModalController` → `Register(id, fee, characterId)` via the seam | `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` |

⚠️ Asmdef rule (hard-won): do NOT add `Golfin.Net` to `Golfin.Tournaments.asmdef`. Everything
networked lives in `Assets/Scripts/TournamentsRuntime/` (Assembly-CSharp).

## 1. Server contract (built + tested; code against this verbatim)

All under `{Endpoints.BaseUrl}/tournaments`, **AUTH REQUIRED**, envelope `{data:…}`,
errors `{detail:…}`. `{slug}` is the game-facing id (`kasumigaseki_open`), never a uuid.

### POST `/golfin/{slug}/enter` · body `{"character_id": "char_james"}`
- Window: `start_at <= now < end_at`, else 400.
- Debits `entry_fee_pts` server-side via `spend_pts` with a **deterministic key**
  (uuid5 of user:slug) — retry after any failure cannot double-charge. **The client must NOT
  also debit** (see §4).
- Returns `{entered, already_entered, entry}`. Re-enter → `already_entered: true`, no charge.
- Insufficient funds → 200 `{entered:false, status:"insufficient", requested, total_points}`.

### POST `/golfin/{slug}/submit-hole` · body `{"hole_number", "strokes", "idempotency_key"}`
- Idempotent per (entry, hole): a replay returns 200 `{replayed:true, hole, entry}` — the
  offline queue depends on this; treat `replayed` as success.
- Rejects (400): hole not in hole set; strokes outside 1–15; no entry; entry not in_progress;
  window closed (`now > end_at + resolve_delay_minutes` — the resolve delay IS the
  late-submission grace); implausible pace (<20s/hole total elapsed — normal play can't trip it).
- On the last hole the server sets `status=finished`, `best_score`, `submitted_at`.

### GET `/golfin/{slug}/entry`
Caller's entry + `holes:[{hole_number, strokes, submitted_at}]` — cross-device resume.
`{data: null}` when not entered.

### GET `/golfin/{slug}/leaderboard`
```json
{"data": {
  "fetched_at": "…", "provisional": true, "bots_active": true,
  "end_at": "…", "resolve_delay_minutes": 60,
  "entries": [{"rank":1,"is_tie":false,"display_name":"SMAUG","character_id":"char_olivia",
               "level":232,"strokes":24,"thru":6,"score_to_par":1,
               "is_player":false,"is_bot":false}],
  "player": {"rank":14,"is_tie":false,"display_name":"…","character_id":"…","level":12,
             "strokes":4,"thru":1,"score_to_par":-1,"is_player":true,"is_bot":false,
             "is_dnf":false,"prize_rank":3}
}}
```
Facts to rely on: ranking semantics are a faithful port of `LocalTournamentBackend`
(provisional = score-to-par, thru desc, earlier-submit; no tie flags on partials; final =
finished-only, strokes asc, T-ties, earlier `submitted_at`). DNFs and thru-0 entries are
excluded from `entries`; the caller's row is ALWAYS in `player` when entered (`rank` null when
unranked). Bots reveal organically server-side (same field for everyone). Once `bots_active`
is false it never goes true again for that tournament. **Do NOT re-rank client-side.**

## 2. Endpoints.cs — four additions

`TournamentEnter(slug)`, `TournamentSubmitHole(slug)`, `TournamentEntry(slug)`,
`TournamentLeaderboard(slug)` → `BaseUrl + "/tournaments/golfin/" + slug + "/…"`.

## 3. `RemoteTournamentBackend` (new, `Assets/Scripts/TournamentsRuntime/`)

Implements `ITournamentBackend`, **delegating to a wrapped `LocalTournamentBackend` for
everything not listed below** (definitions, state derivation, results math against the served
board). Networked overrides:

- **`Register`** → POST enter (coroutine via `ApiClient.Instance.Run`). On success mirror the
  entry into the local store (gameplay flow reads it synchronously) and trigger the
  `rp_balance_sync` balance refresh (the fee was debited server-side). On
  `status:"insufficient"` → the existing insufficient-funds UX. Offline → the existing
  "Connection required" toast (entry is online-only by decision of record). **Do NOT call
  `IRewardPointsService` for the fee** — that would double-charge.
- **`SubmitHoleResult`** → local persist first (exactly as today — play is never blocked by
  network), then enqueue `{slug, hole_number, strokes, idempotency GUID}` on a pending-ops
  queue mirroring `PendingOpsStore` (FIFO, replay on reconnect/sign-in/resume, drop op on
  `replayed:true` or any 400 — a 400 is a rejection, log it, never retry forever).
- **`GetMyEntry`** → local store; on sign-in/app-resume reconcile from GET entry (server wins
  on conflict — cross-device resume).
- **`GetLeaderboard`** → snapshot of the last GET leaderboard payload mapped to
  `TournamentLeaderboardEntry` (Rank/IsTie/DisplayName/CharacterId/Level/Strokes/Thru/
  IsPlayer/IsDNF verbatim; `IsProvisional` = payload `provisional`; `TimeSeconds` = 0 — the
  server tiebreak is submission order, the time column is not displayed). Async
  `RefreshLeaderboard(slug, onDone)` + disk cache per slug (`RemoteBannerSource` atomic-write
  discipline), refresh driven from the leaderboard screen's `OnEnable`.
- **`GetResults` / `ClaimPrize`** — compute the player's final rank from the server FINAL
  board (`player.prize_rank`) instead of the local sim; prize amount from the served prize
  bands. The award itself keeps the EXISTING client path (earn-game `tournament_prize`,
  idempotent key) — unchanged behavior, and Phase 5 re-points it server-side (decision of
  record #5). NOTE in code where Phase 5 will cut this over.

Provider selection (mirror `leaderboard_backend` §4): `BotSessionOverride` / signed-out /
`DemoGate.IsDemo` → `LocalTournamentBackend` unchanged (bots are offline by design and must
never hit prod). Signed-in → `RemoteTournamentBackend`.

## 4. UI

- `TournamentLeaderboardScreenController`: fill source becomes the remote snapshot + refresh
  on `OnEnable` (cached board renders instantly, refresh rebuilds). No prefab edits.
- Player sticky row while `bots_active && prize_rank != rank`: rank label shows both —
  format `#{rank} · PRIZE #{prize_rank}` (Cesar's chosen presentation). When bots are
  retired the two are equal and the label reverts to the plain rank.
- Signup modal: unchanged visually; Register path per §3.

## 5. Acceptance

EditMode:
- [ ] DTO parse of §1 payloads (incl. `player:null`, `rank:null`, insufficient-funds enter).
- [ ] Snapshot → `TournamentLeaderboardEntry` mapping verbatim; no client re-ranking.
- [ ] Queue: op survives restart (disk), replays FIFO, drops on `replayed:true` and on 400.
- [ ] Register: no `IRewardPointsService` debit on the remote path (test the seam).
- [ ] Provider selection incl. BotSessionOverride → Local.
- [ ] Full per-assembly EditMode sweep green.

Manual (Cesar, device):
- [ ] Two accounts enter the same tournament → identical board (same bots, same reveal).
- [ ] Entry debits the fee exactly once, incl. after a mid-enter network drop + retry.
- [ ] Airplane-mode round → reconnect → queue flushes, board shows the score.
- [ ] Resume a half-played tournament on a second device (GET entry reconcile).
- [ ] Sticky row shows `#N · PRIZE #M` while bots active; 10th human entry retires bots
      (verify via SQL or a second fetch — bots gone, ranks compact, one-way).
- [ ] Ended tournament renders the final board with T-ties.

## 6. Out of scope

Phase 5 (resolver, server-side payout, `ClaimPrize` re-point), dashboard bot-field/retirement
editor, real anti-cheat beyond the server's v1 checks, GPS tournament endpoints, tournament
banners (own spec), Rankings screen (shipped separately as `leaderboard_backend`).

## 7. Files this task touches

**New:** `TournamentsRuntime/RemoteTournamentBackend.cs`, `TournamentsRuntime/TournamentNetDtos.cs`,
`TournamentsRuntime/TournamentSubmitQueue.cs`, EditMode tests.
**Modified:** `Net/Endpoints.cs` (+4), backend selection wiring (wherever
`LocalTournamentBackend` is constructed — `TournamentService`/bootstrap), 
`UI/Tournaments/TournamentLeaderboardScreenController.cs` (fill source + sticky-row label),
`Docs/AI_CONTEXT.md`, `Docs/TellCode.md`, this folder's STATUS/report.
