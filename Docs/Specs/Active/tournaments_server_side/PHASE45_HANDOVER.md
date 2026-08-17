# Context transfer — GOLFIN tournaments, Phases 4–5 (async multiplayer)

Paste this whole file into a fresh Cowork/Architect session. It is written to be
self-sufficient: read the repo for detail, but everything needed to know *where
to look and what has already been decided* is here.

---

## Your job

Write the spec for **Phases 4–5 of `tournaments_server_side`: async multiplayer**
— real entries, per-hole submission, a shared leaderboard, server-generated bot
fields, and the resolver that pays prizes. Then issue the Claude Code kickoff.

**Read `claude/WORKFLOW_NOTES.md` first and follow it exactly** — specs are
folders under `Docs/Specs/Active/<slug>/` with a `STATUS.md`, a pointer plus
kickoff block appended to `Docs/TellCode.md`, and the kickoff *also* delivered
in the chat message. Cesar has had to ask for that repeatedly; do it unprompted.

The parent epic is `Docs/Specs/Active/tournaments_server_side/SPEC.md`. Its §6b
is the existing sketch of this work — treat it as a starting point that predates
three phases of実 implementation, not as gospel. Verify every claim in it against
the code before you build on it.

---

## What already shipped (do not re-plan these)

| Phase | What | Where |
|---|---|---|
| 1 | Schema, applied to prod | `playlife` `02fb177` — `2026_08_13_tournaments_golfin.sql` |
| 2 | Admin Tournaments panel | GolfinRedux `0e5c509d0` — `Tools/admin-dashboard` |
| 3 | Client reads the server schedule + remote art | GolfinRedux `506b55b75`, spec in `Docs/Specs/Completed/tournaments_unity_wiring/` |
| — | Admin dashboard deployed | `https://admin.golfin.world`, Cloudflare Workers, behind Cloudflare Access |

**The schema for Phases 4–5 already exists.** The Phase-1 migration extended
`tournament_entries` with `character_id`, `holes_completed`, `status`
(`in_progress|finished|forfeited`), `is_bot`, `display_name`, `submitted_at`,
and created `tournament_hole_results` (`entry_id`, `hole_number`, `strokes`
bounded 1–15, `idempotency_key`, unique on `(entry_id, hole_number)`). There is
a partial unique index on `tournament_entries (tournament_id, user_id) where
user_id is not null`. Check it before assuming a migration is needed — this work
is mostly endpoints and client wiring.

Server API lives in `/Users/cesar/Documents/playlife/backend/routers/tournaments.py`,
mounted at `/api/v1/tournaments`. `GET /tournaments/golfin` (added in Phase 3,
no auth) returns the schedule with prize bands joined. Envelope is `{"data": …}`
written by hand per route; errors are FastAPI `{"detail": …}`. Every router makes
its own service_role Supabase client. There is **no scheduler of any kind** in
that repo — no cron, no worker process, no GitHub Actions — which matters for the
resolver.

---

## Decisions of record

1. **Trust the client, verify server-side with plausibility checks** (Cesar).
   Reject the impossible, not the improbable. Real verification waits until
   there is something worth cheating for.
2. **Bots are field filler and are never paid.** Prizes always follow the
   human-only ordering.
3. **One entry, one run.** Register → character locks → play the hole set →
   that is your score. No replays.
4. **Bots pad the board early, then disappear once enough humans have entered**
   (Cesar, 2026-08-17 — this resolves the long-open §6b.4 question). Three
   things the decision does not settle and your spec must: the threshold and
   whether it is absolute or a fraction of `bot_count`; whether hidden bots are
   merely hidden or also removed from the ranking (**removed is strongly
   preferred** — hiding-but-ranking leaves visible rank gaps and keeps rank ≠
   prize, which is the confusion this decision exists to kill); and whether the
   switch is evaluated live or frozen, so nobody's displayed rank yanks
   mid-round.
5. **RP is credited at resolution, not on claim.** `LocalTournamentBackend.ClaimPrize`
   and the result modal's auto-claim (GDD §17.6) must be re-pointed to an
   acknowledgement/animation, not left double-granting.
6. **Payout idempotency key is load-bearing** — `earn_pts_v2` with action
   `tournament_prize` and a **deterministic** uuid5 of the entry id, so
   re-running the resolver can never double-pay. Do not make it random. The live
   catalog caps `tournament_prize` at `max_per_event = 2000` with no daily cap,
   which exactly matches the top prize band; a resolver paying more gets a 400.

---

## Hard-won conventions — violating these has cost real time

- **Do not add `Golfin.Net` to `Golfin.Tournaments.asmdef`.** That assembly is
  deliberately dependency-light. Networking lives in
  `Assets/Scripts/TournamentsRuntime/` (no asmdef → Assembly-CSharp, which
  already sees `Golfin.Net` and `Golfin.Economy`).
- **Newtonsoft coerces date-shaped strings at the reader level**, before they
  reach a `string` field. `DateParseHandling.None` on both reader and serializer,
  or every player gets a schedule shifted by their own timezone.
- **Client state is derived from `start_at`/`end_at`**, never from
  `tournaments.status`, which is not maintained for `kind='golfin'` rows.
- **Submissions ride the existing pending-ops queue pattern** from
  `reward_points_backend` (idempotency key per hole, FIFO replay) so a round
  played on the train submits itself on reconnect.
- **RP spends are online-only**, so a player can play offline but cannot *enter*.
- **EditMode runs must be swept per assembly.** A filtered run reports
  `FailedTests` for the filter only while `TotalTests` counts the whole mode — a
  filtered green run proves nothing. Also `tests-run` intermittently returns "No
  tests found" for a valid assembly; retry, never read it as green.
- **A stale assembly reads exactly like a real failure.** Force an assets
  refresh before believing any post-revert probe.
- Platform: Cesar is on a Mac; repos at `/Users/cesar/Documents/GolfinRedux` and
  `/Users/cesar/Documents/playlife`. He pastes secrets himself.

---

## Things to check rather than assume

- `auto_enter_score` (`tournaments.py:225`) has a latent bug: its selects omit
  `status`, so `t.get("status")` is always `None` and the upcoming→active flip
  never fires. `min_rounds` is selected but never enforced. It now filters
  `kind='gps'`, so it cannot touch game tournaments — but if you extend it,
  know what you are extending.
- `/tournaments/admin/create` and `/admin/weekly-open` are still guarded only by
  an inline non-constant-time comparison against `settings.admin_preload_key`,
  and `admin_create` never sets `kind`. Closing them is overdue once these
  tables carry real entries.
- `GET /tournaments/{id}/ranking` already exists for GPS. Extending it for
  golfin rows is probably right, but read it first — its enrichment loop only
  runs `if user_ids:`, so an all-bot board would come back unenriched.
- The resolver needs a trigger and **there is no scheduler in the repo**. §6b.5
  proposes automatic plus a manual "Resolve now" button in the dashboard,
  audited, manual first. Re-examine that: watching the first few resolve by hand
  is worth more than a cron that silently misfires, but "manual only" is not a
  destination.
- Only `lomond-country-club` has playable hole data;
  `HoleParProviderAdapter.cs:35-37` ignores `clubId`. A tournament on Kawana
  still plays Lomond's holes. Not this phase's problem, but do not design as if
  courses differ.

---

## Scope judgement is yours

§6b bundles entries, submission, leaderboard, bot generation, resolution and
payout into one lump. That is likely too big for one kickoff. Cesar considered
splitting the server half from the resolver and chose to have you spec it
properly first — so make the phasing call explicitly in the spec and say why,
rather than inheriting §6b's shape by default.

Start by asking Cesar what he wants to see working first — a player entering and
their score appearing on a shared board is a different milestone from money
moving correctly.
