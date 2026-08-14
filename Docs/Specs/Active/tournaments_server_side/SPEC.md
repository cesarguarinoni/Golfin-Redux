# SPEC — tournaments_server_side (schedule moves to the server; admin gets a Tournaments panel)

**Status:** SPEC_READY (Phase 1–2); Phase 3 needs its own kickoff
**Author:** Architect (Cowork session), 2026-08-13, from Cesar: *"Add tournaments to the Admin."*
**Decision of record (Cesar, 2026-08-13):** GOLFIN tournaments **move server-side**. Dashboard becomes where they are created and edited. Spec first, panel after.
**Related:** `Docs/Specs/Completed/reward_points_backend` (same shared-ledger pattern), `Docs/Specs/Active/admin_dashboard`.

---

## 1. Why (the problem this kills)

The game's tournaments live in `Assets/Resources/Data/tournaments.csv`, which **ships inside the build**. Consequences today:
- **The schedule expires.** Absolute UTC dates are deliberate (every build must show the same tournaments — a clock-relative form was tried and reverted, see AI_CONTEXT 2026-08-11). But it means refreshing dates = editing a CSV + rebuilding + shipping. Last refresh 2026-08-11; `hirono_invitational` ends 2026-08-14T12:00Z, `lomond` opens Aug 18. This recurs forever.
- **No live control.** Cannot add, retime, or pull a tournament without a release.
- **Nothing to administer.** A dashboard panel over today's data is impossible: the dashboard reads Supabase, and the tournaments are in a client bundle.

## 2. What exists (verified 2026-08-13, live DB + both repos)

**Server — `public.tournaments` (17 cols) and `public.tournament_entries` (12 cols), BOTH EMPTY (0 rows).** They were built for the **GPS/real-world** product, not the game:
- `tournaments`: id, title, description, tier, status, entry_fee_pts, prize_pool_pts, prize_description, sponsor_name, **min_trust_level, min_rounds, requires_membership_tier**, start_at, end_at, banner_url, rules_text, created_at
- `tournament_entries`: id, tournament_id, user_id, **best_score, best_activity_id, rounds_submitted, entry_trust_snapshot**, final_rank, prize_pts_awarded, prize_claimed, entered_at, updated_at
- Also live server-side: `create_weekly_open_tournament()` RPC, and `auto_enter_score(...)` in `backend/routers/tournaments.py` which enters GPS score submissions into tournaments automatically.

**Game — CSV + `LocalTournamentBackend` (the only backend).**
- `tournaments.csv`: id, nameKey, courseId, holeSet, startUtc, endUtc, resolveDelayMinutes, entryFeeRP, botFieldId, prizeTableId, sponsorKey, leagueKey (6 rows)
- `tournament_prizes.csv`: reusable rank-band tables (`prize_small/medium/major`), rpReward + optional itemRewardId — post-rebalance values (2000/1200/500/100 for major)
- `tournament_bot_fields.csv`: botCount, bracketWeights, start offsets, perHoleSpreadSec — **pure client simulation tuning**
- Seams that already exist and must be preserved: `ITournamentBackend` (`GetTournament`, `Register`, `SubmitHoleResult`, `ClaimPrize`), `TournamentCsvLoader` (`ExpandHoleSet`, `ParseBracketWeights`, `CheckReferentialIntegrity`, absolute-only `ParseUtc`), `TournamentService.Compose()`, `IRewardPointsService`, `ITournamentClock`.

**The mismatch:** the server tables model *"submit your real round, gated by trust and membership"*. The game models *"play N holes against a simulated bot field, win rank-band RP"*. Overlap is only id/title/dates/entry-fee/sponsor/tier.

## 3. Scope decision — schedule now, results later

**IN:** the tournament **schedule and definition** (what exists, when, on which holes, fees, prizes) becomes server-authoritative and dashboard-editable.

**OUT (explicitly deferred):** results, rankings and prize resolution stay **local** (`LocalTournamentBackend` + `InMemoryEntryStore`). GOLFIN tournaments today are single-player-vs-simulated-bots resolved on device; making *that* server-authoritative is real cross-player competition — a separate, much larger project with anti-cheat implications. Do not let it ride along on a schedule change.

## 4. Schema (Phase 1)

The tables are empty, so reshape rather than add parallel ones — one table keeps one admin panel, and lets GPS events coexist later.

**4.1 `tournaments` — add:**
- `kind text not null default 'gps'` with `check (kind in ('gps','golfin'))` — the discriminator.
- GOLFIN-only columns, all **nullable** (meaningless for `kind='gps'`): `slug text unique` (the CSV `id`, e.g. `kasumigaseki_open` — the game's stable key; the table's own `id` stays uuid), `name_key text`, `course_id text`, `hole_set text` (raw range form, e.g. `1-18`; expansion stays client-side via `ExpandHoleSet`), `bot_field_id text`, `resolve_delay_minutes int`, `league_key text`.
- Constraint: `check (kind <> 'golfin' or (slug is not null and course_id is not null and hole_set is not null and bot_field_id is not null))` — a golfin row cannot be half-defined.
- `entry_fee_pts` carries `entryFeeRP` (same unit — RP is `total_points`, per the ledger decision).
- ⚠️ **`status` is NOT maintained for golfin rows.** State is derived from `start_at`/`end_at` exactly as `LocalTournamentBackend.DeriveState` does today (Upcoming / Open / Ending-final-hour / Ended). Two sources of truth for "is it open" is how schedules rot. Document it in a column comment.

**4.2 New `tournament_prize_bands`:** `(id uuid pk, tournament_id uuid fk → tournaments on delete cascade, rank_from int, rank_to int, rp_reward int, item_reward_id text, check (rank_from <= rank_to))`, unique on `(tournament_id, rank_from)`.
- **Per-tournament bands, not reusable templates** (a deliberate departure from `prizeTableId`): the dashboard must be able to raise one tournament's first prize without silently changing three others. The CSV's three templates become seed values, not a runtime indirection. Trade-off accepted: editing "all majors" becomes N edits.

**4.3 Bot fields stay client-side.** `tournament_bot_fields.csv` is simulation tuning, not schedule; the server stores only the `bot_field_id` string. Referential integrity for it remains `CheckReferentialIntegrity`'s job at load.

**4.4 Seed** the 6 current fixtures + their prize bands from the CSVs, `kind='golfin'`, preserving slugs and dates exactly, so the server starts as a faithful mirror of what ships today.

**4.5 Security to fix in the same migration:** `POST /tournaments/admin/create` and `/admin/weekly-open` are guarded only by a static shared secret (`settings.admin_preload_key`) and can write these tables. Either retire them or gate them behind the same service-role path the dashboard uses. Flag before the tables carry anything real.

## 5. Dashboard panel (Phase 2 — Architect/Cowork builds, after Cesar approves §4)

New `Tournaments` panel in `Tools/admin-dashboard`, registered in `lib/registry.ts` like the others.
- **List:** slug, title, kind badge, derived state (computed from dates, same rules as the client — Upcoming/Open/Ending/Ended), course, holes, entry fee, prize-pool summary, start/end, entry count. Filter by kind + state; sort by start.
- **Detail/edit:** every golfin field; **rank-band prize editor** (add/remove/reorder bands, validate no gaps or overlaps, warn if band 1 is missing); duplicate-tournament action (the fastest way to make next month's schedule); create + delete (delete double-confirms and states the entry count that will cascade).
- **Guardrails:** editing a tournament that is currently **Open** warns loudly (players may be mid-entry); changing `entry_fee_pts` or prize bands on an Open tournament requires the same typed confirmation as a destructive action.
- **Entries tab:** read-only `tournament_entries` for the row.
- Every mutation goes through `lib/audit.ts` with before/after, exactly like the RP adjust path.
- ⚠️ **Honest limitation to surface in the UI:** until Phase 3 ships, edits here do **not** reach players — the game still reads the shipped CSV. Panel shows a persistent banner saying so, and offers **"Export tournaments.csv + tournament_prizes.csv"** so the dashboard is immediately useful as the authoring tool (edit here → export → commit → build), rather than a UI that pretends to be live.

## 6. Unity (Phase 3 — own kickoff, Claude Code)

- New `RemoteTournamentSource` feeding the **existing** `TournamentDefinition`/prize-table DTOs — `ITournamentBackend` and `LocalTournamentBackend` keep working unchanged; only where the definitions come from changes (`TournamentService.Compose()`).
- Fetch on boot/sign-in via `ApiClient` (reuse `Golfin.Net`), cache to disk; **the shipped CSV becomes the offline fallback**, so a cold launch with no network behaves exactly as today.
- `CheckReferentialIntegrity` must run against server data too (a bad `bot_field_id` from the dashboard must fail loudly at load, not silently drop a tournament).
- Endpoint: `GET /tournaments/golfin` (or `?kind=golfin` on the existing `/active`) returning definitions + prize bands in one payload.
- Acceptance: with the network on, changing a date in the dashboard changes the T7 screen after a relaunch — **no rebuild**. With the network off, the shipped CSV still drives everything.

## 7. Risks
1. **Scope creep into live results** — §3 draws the line; hold it.
2. **Schedule drift between server and shipped CSV** during Phase 2→3 — mitigated by the export button and the panel banner.
3. **`create_weekly_open_tournament()` and `auto_enter_score`** already write these tables for GPS; the `kind` discriminator must be respected by anything that writes, or GPS automation will start creating rows the game tries to render.
4. Free-tier Supabase still auto-pauses — a paused project during Phase 3 means the game falls back to CSV (acceptable, by design).
