# SPEC — tournaments_server_side (schedule moves to the server; admin gets a Tournaments panel)

**Status:** ✅ PHASE 1 APPLIED (prod, playlife `02fb177`) · ✅ PHASE 2 SHIPPED (GolfinRedux `0e5c509d0`) · Phase 3 (Unity) NOT STARTED — needs its own kickoff
**Phase 2 note (2026-08-14):** built as specced, with one addition worth recording — `lib/courses.ts` now holds the six valid `course_id` values, because the Unity project has no course registry to read from (`CheckReferentialIntegrity` validates `prizeTableId` and `botFieldId` only). The dropdown IS the validation.
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

## 3. Scope — schedule AND async multiplayer results (amended by Cesar, 2026-08-13)

The first draft deferred results. **Cesar amended it the same day: tournaments become asynchronous multiplayer** — "player plays like practice and the game saves results" — so real players share one leaderboard and the server owns the outcome. That is now in scope and is the heart of the epic; the schedule move is its prerequisite.

**Cesar's three architecture calls (2026-08-13):**
1. **Anti-cheat: trust the client, with server-side plausibility checks.** Reject the impossible, not the improbable. Real verification waits until there is something worth cheating for.
2. **Bots stay as field filler.** Real entries are ranked among them so a young tournament never looks empty; **bots are never paid**.
3. **One entry, one run.** Register → character locks → play the hole set → that is your score. No replays.

**Still out of scope:** real-time play; server-side physics/replay verification; cross-app (GPS) unification of tournaments.

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

## 5b. Images — the thing that decides whether "server-side" really means "no rebuild" (Cesar, 2026-08-13)

**How art resolves today** (`TournamentSelectionScreenController.ResolveSprite`, line 323):
1. `_courseImageMap` — an Inspector-wired list of (tournamentId → Sprite)
2. fallback `_courseImages[csvIndex]` — a serialized Sprite array indexed by **CSV row position**

Both are **scene-serialized**, so art is assigned at build time by hand.

**Why this breaks a server-driven schedule.** A tournament created in the dashboard has no map entry, so it falls through to `_courseImages[index]` and renders **another course's photograph** — or nothing. And because the fallback is positional, reordering tournaments (something the dashboard makes trivial) silently reshuffles which photo lands on which card. Shipping the panel without fixing this makes "create a tournament" an unsafe button.

**The fix is nearly free, because the naming convention already exists.** The art at `Assets/Art/Tournaments/CourseImages/` is already named exactly by course id — `gotemba.png`, `hirono.png`, `kasumigaseki.png`, `kawana.jpg`, `kisarazu.png`, `lomond.png` — a 1:1 match with every `courseId` in `tournaments.csv`. It simply is not loadable at runtime because the folder is not under `Resources/`.

**Phase 3 rule:** move (or mirror) that folder to `Resources/TournamentImages/` and resolve art as `Resources.Load<Sprite>($"TournamentImages/{course_id}")`. `Resources.Load` is extension-agnostic, so `kawana.jpg` needs no rename. Then:
- **DELETE the positional `_courseImages[csvIndex]` fallback** — it is the bug, not a safety net.
- Keep `_courseImageMap` as an optional per-tournament override for one-off art.
- Missing art renders an explicit **placeholder** sprite + a warning log. Never silently show the wrong course.

**Why bundled art is the right FIRST step (but not the whole answer).** A tournament can only be scheduled on a course that already ships — it needs the hole scenes, terrain and baked sim data — so a default course photo keyed by `course_id` correctly covers every tournament the dashboard can create, with zero new infrastructure. ⚠️ **It does not deliver Cesar's actual goal** (2026-08-13: *"I want to add tournaments without new build"*): that requires **per-tournament** art — seasonal key art, a new sponsor's branding, a differently-dressed event on a course that already ships. Presentation is not bounded by playability. Bundled art is therefore the fallback layer, not the destination — see §5c.

**Promoted, not deferred (§5c):** the server's `banner_url` column carries per-tournament art fetched at runtime. Note also that **sponsor is text-only today** (`"{SPONSOR} PRESENTS"` from `SponsorKey`, no logo image) and league/tier drives `_badgeBackground` — if a sponsor logo is ever wanted, the same convention rule applies.

**Phase 2 consequence (dashboard):** `course_id` is a **dropdown of the courses known to ship**, never free text, and the create/edit form shows the resolved image name so a missing asset is visible before saving. The panel must not be able to author a tournament the game cannot draw.

## 5c. Remote tournament art — what actually delivers "no new build" (Cesar, 2026-08-13)

**Requirement:** adding a tournament in the dashboard — including its artwork — must reach players without a client release. Bundled art (§5b) cannot do this; it is the offline/default layer beneath this one.

**5c.1 Resolution order (client, Phase 3b).** First hit wins, and every step degrades safely:
1. **`banner_url`** — per-tournament art from the server, downloaded and disk-cached.
2. **`Resources/TournamentImages/{course_id}`** — the shipped default course photo (§5b).
3. **Placeholder sprite** + warning log. Never a blank card, never another course's photo.
While a remote image is still downloading the card shows layer 2 immediately and swaps on arrival — no empty rectangle, no layout jump.

**5c.2 Storage: Supabase Storage, not arbitrary URLs.** Create a public-read bucket `tournament-art` in the same project. The dashboard uploads the file (service_role) and writes the resulting public URL into `banner_url`.
- 🔒 **The client MUST validate the host** and accept only URLs under the project's Storage domain. A free-text URL field shipped to every player is a way to serve arbitrary content (and leak player IPs to third parties) if the DB is ever touched by something other than the dashboard — remember `/tournaments/admin/create` still writes these tables behind a static key (§4.5).
- Immutable naming (`{slug}-{content_hash}.jpg`) so the cache key can be the URL and a changed image is a new URL — no cache-invalidation problem to solve.

**5c.3 Client caching (Phase 3b).** `UnityWebRequestTexture` → disk cache under `Application.persistentDataPath/tournament-art/`, keyed by URL hash; cache survives launches, so a given image downloads once ever. Prefetch on the tournament-list fetch (boot/sign-in) so the T7 screen is already warm. Bounded cache with LRU eviction (say 50 MB) and eviction of art for tournaments long ended. Offline: cached art shows; uncached falls to layer 2.

**5c.4 Dashboard (Phase 2 addition).** Image upload control in the tournament editor with **validation before upload** — accepted types (JPG/PNG/WebP), max ~500 KB, and the card's expected aspect ratio, with a live preview at card size. This is the guardrail that matters: without it someone uploads a 12 MB PNG and every mobile player pays for it on a metered connection. Show which layer a tournament will resolve to (remote / bundled / placeholder) right in the row, so a missing asset is visible before publish, not after.

**5c.5 What still needs a build.** A brand-new *course* (hole scenes, terrain, baked sim data) and new *sponsor logo images* if those ever become sprites rather than text. Everything else about a tournament — existence, timing, holes played, fees, prizes, artwork — becomes live-editable. That is the honest boundary of "no new build".

## 6. Unity (Phase 3 — own kickoff, Claude Code)

- New `RemoteTournamentSource` feeding the **existing** `TournamentDefinition`/prize-table DTOs — `ITournamentBackend` and `LocalTournamentBackend` keep working unchanged; only where the definitions come from changes (`TournamentService.Compose()`).
- Fetch on boot/sign-in via `ApiClient` (reuse `Golfin.Net`), cache to disk; **the shipped CSV becomes the offline fallback**, so a cold launch with no network behaves exactly as today.
- `CheckReferentialIntegrity` must run against server data too (a bad `bot_field_id` from the dashboard must fail loudly at load, not silently drop a tournament).
- Endpoint: `GET /tournaments/golfin` (or `?kind=golfin` on the existing `/active`) returning definitions + prize bands in one payload.
- Acceptance: with the network on, changing a date in the dashboard changes the T7 screen after a relaunch — **no rebuild**. With the network off, the shipped CSV still drives everything.

## 6b. Async multiplayer — entries, scoring, resolution (Phases 4–5)

### 6b.1 Entry model
One row per (tournament, user) in `tournament_entries`, `unique (tournament_id, user_id)`. Add golfin columns (nullable, same discipline as §4.1): `character_id text` (**locked at entry** — the client already snapshots stats via `ICharacterStatsProvider` at `Register`; the lock is a GDD rule and must be enforced server-side too), `holes_completed int`, `status text` in `('in_progress','finished','forfeited')`, `is_bot boolean not null default false`, `display_name text` (for bots and for leaderboard rendering without a profile join), `submitted_at timestamptz`.
- `best_score` carries **total strokes for the hole set** (lower is better). `rounds_submitted` stays a GPS column; the game uses `holes_completed`.
- Entry costs `entry_fee_pts` RP, debited through the existing `spend_pts` path (server-first, audited, idempotent) — **not** a new payment path.

### 6b.2 Per-hole results
New `tournament_hole_results`: `(id uuid pk, entry_id uuid fk → tournament_entries on delete cascade, hole_number int, strokes int, submitted_at timestamptz default now(), idempotency_key uuid, unique (entry_id, hole_number))`.
Three jobs at once: **resume** a part-played tournament on another device, **evidence** for plausibility checks, and **idempotent** submission for the offline queue.

### 6b.3 Submission + plausibility (the anti-cheat surface)
`POST /tournaments/{id}/submit-hole` `{hole_number, strokes, idempotency_key}` — one atomic RPC, rejecting:
- tournament missing / `kind <> 'golfin'` / caller has no entry / entry belongs to another user
- `hole_number` not in the tournament's `hole_set`
- duplicate `(entry_id, hole_number)` — replay returns the stored row (offline queue safety)
- `strokes` outside `[1, 15]` (absurd-value guard, not a skill judgement)
- submission window closed — **accepted until `end_at + resolve_delay_minutes`**, see 6b.5
- **pace guard:** total elapsed from `entered_at` shorter than ~20s × holes submitted — catches instant-complete scripting, which is the realistic cheat, while never punishing a fast human.
Everything rejected is logged with the user id: the point of v1 checks is to *see* tampering, even before we can stop all of it.

### 6b.4 Bots — same field for everyone
Bots are generated **client-side today** (T3 generator + `bracketWeights` + `bot_score_brackets`), which cannot work for a shared leaderboard: two players would see two different fields. **The server generates the bot field once**, at tournament creation, from `bot_field_id` + a stored `bot_seed`, persisting bot rows into `tournament_entries` (`is_bot=true`, `user_id=null`, `display_name` from the `fake_players` pool). Everyone then reads one field. The client's generator stays only as the offline/fallback path.
- **Bots occupy leaderboard positions but never prize money.** Prize bands are applied to the **human-only ordering**.
- ⚠️ **Open UX question for Cesar:** a human who places 3rd on the visible board but is the top human gets the 1st-place band. Show the blended rank and label the prize honestly ("top finisher"), or rank humans separately in the UI? Needs a call before the leaderboard screen is touched.

### 6b.5 Resolution + payout
At `end_at + resolve_delay_minutes` a resolver: orders entries by `best_score` (ties → earlier `submitted_at`), writes `final_rank` for all, computes `prize_pts_awarded` for humans from `tournament_prize_bands`, and pays via **`earn_pts_v2` with action `tournament_prize`** and a **deterministic idempotency key (uuid5 of entry_id)** — so re-running the resolver can never double-pay. The existing `game_point_actions` cap (`tournament_prize`, `max_per_event 2000`) already matches the top prize band exactly.
- **The resolve delay is also the late-submission grace window** — a round finished before close but submitted after (offline queue, backgrounded app) still counts, with no separate rule to invent.
- Trigger: automatic (scheduled task) **plus** a manual **"Resolve now"** button in the dashboard, audited. Manual first — watching the first few resolve by hand is worth more than a cron that silently misfires.
- ⚠️ `ClaimPrize` semantics change: RP is credited at resolution, so the client's claim becomes an acknowledgement/animation, not a grant. `LocalTournamentBackend.ClaimPrize` and the result modal's auto-claim (GDD §17.6) must be re-pointed, not left double-granting.

### 6b.6 Client (Phase 4)
- Entry, per-hole submission and leaderboard read go through `Golfin.Net`; **submissions ride the existing pending-ops queue pattern** from `reward_points_backend` (idempotency key per hole, FIFO replay) so a tournament played on the train submits itself on reconnect.
- `GET /tournaments/{id}/ranking` already exists — extend it for golfin rows (blended field, `is_bot`, strokes, rank, display name). `TournamentLeaderboardScreenController` currently loads `Data/fake_players`; that becomes the server's bot rows.
- Offline: a player can still *play*; they cannot *enter* (entry costs RP, and RP spends are online-only by decision of record).

## 7. Risks
1. **Cheating once RP is real money-adjacent** — v1 plausibility checks are a tripwire, not a wall (§6b.3). Revisit before tournaments pay anything a player would miss.
2. **Bot/human prize ambiguity** — unresolved UX question in §6b.4; decide before the leaderboard is built.
3. **Remote art is a content channel into every client** — §5c.2's host allowlist is the control; do not ship a free-text URL that the client trusts blindly.
4. **Double-payout on resolve** — mitigated only by the deterministic idempotency key in §6b.5; that key is load-bearing, do not make it random.
2. **Schedule drift between server and shipped CSV** during Phase 2→3 — mitigated by the export button and the panel banner.
3. **`create_weekly_open_tournament()` and `auto_enter_score`** already write these tables for GPS; the `kind` discriminator must be respected by anything that writes, or GPS automation will start creating rows the game tries to render.
4. Free-tier Supabase still auto-pauses — a paused project during Phase 3 means the game falls back to CSV (acceptable, by design).
