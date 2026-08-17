# QUICK SPEC — tournament_schedule_refresh (refetch the schedule when the player opens the tournament screen)

**Status:** SPEC_READY
**Author:** Architect (Cowork session), 2026-08-17, from Cesar: *"I want the tournaments to update whenever the player enters the tournament screen in the game, not just relaunching (show cached ones if there are connection issues)."*
**Size:** one throttled refresh entry point on `TournamentService`, one call from the screen, tests. No new systems — Phase 3 already built the fetch, the cache and the merge.
**Related and shipping alongside:** an admin **Activate/Deactivate** switch (`tournaments.is_active`). No Unity work of its own — the server simply stops sending deactivated rows — but it makes §3.3 load-bearing, so read that section.

---

## 1. Why

Phase 3 fetches the schedule once, at boot (`TournamentService.Awake` → compose from cache/CSV synchronously, then kick the fetch). A player who leaves the app open never sees a schedule change, and the admin panel's whole promise is that an edit reaches players without a build. "Without a build" currently still means "after a relaunch".

## 2. What already exists (reuse, do not rebuild)

- `Assets/Scripts/TournamentsRuntime/RemoteTournamentSource.cs` — the fetch, the atomic write to `<persistentDataPath>/tournaments_schedule.json`, and the live → cache → CSV precedence.
- `TournamentService.OnScheduleChanged` — raised after a successful refetch recomposes the backend.
- `TournamentScheduleMapper.MergePreservingEntered` — keeps a definition the player holds an entry in, even when the incoming payload drops it.
- `TournamentArtService` prefetch, and its disk cache keyed by URL.
- `TournamentSelectionScreenController.OnEnable:92` → `StartCoroutine(RebuildNextFrame())` → `RebuildCards()`.

## 3. The change

### 3.1 A throttled refresh on `TournamentService`

Add a public entry point — `RefreshScheduleAsync()` or similar — that:

- **returns immediately if a fetch is already in flight** (screen re-entry during a slow request must not queue a second one), and
- **returns immediately if the last successful fetch was under a cooldown ago.** 60s is the suggested starting value; put it in one named constant with a comment, not scattered. Bouncing between Home and T7 is a normal thing for a player to do and must not become a request per bounce.

Networking stays out of the UI class: the screen makes one call and subscribes to one event. Do **not** add `Golfin.Net` to `Golfin.Tournaments.asmdef` — this lives in `TournamentsRuntime/`, as everything else in Phase 3 does.

### 3.2 The screen

In `TournamentSelectionScreenController`:

- `OnEnable` — subscribe to `TournamentService.OnScheduleChanged`, then call the refresh. **Render what you already have first**; the existing `RebuildNextFrame()` path already does this, so the screen must never wait on the network or show a spinner over a perfectly good cached list.
- `OnDisable` — unsubscribe. (Project convention: subscribe in `OnEnable`, unsubscribe in `OnDisable`.)
- On the event, rebuild the cards. Prefetch art for any tournament that is new to the list.
- **Failure is silent.** No error toast, no empty state, no retry storm — keep showing what is on screen and log once. Cesar: *"show cached ones if there are connection issues."*

### 3.3 Deactivation makes disappearance routine — this is the part to get right

Until now a tournament vanishing from the payload was rare. With the new `is_active` switch **and** a refetch on every screen entry, it becomes an ordinary event: an admin flips a tournament off and every player who opens T7 gets a payload without it.

- A tournament the player **has entered** must survive that — `MergePreservingEntered` already does this, and it was the B1 bug in the Phase-3 review, so do not regress it. Add a test at this level, not just at the mapper's: enter a tournament, refetch a payload that omits it, confirm the card is still there and `GetTournament(id)` does not throw.
- A tournament the player has **not** entered simply disappears from the list. That is correct.
- ⚠️ **Do not rebuild the list while the player is inside a signup modal or a confirmation for a tournament that just left the payload.** Check what the screen does if `SelectedTournamentId` no longer resolves. If it can crash or dead-end, defer the rebuild until the modal closes.

### 3.4 Do not touch

State derivation stays client-side from `start_at`/`end_at`. `is_active` is not a client concept — the client never sees an inactive tournament, so there is no flag to read and nothing to render differently.

## 4. Acceptance

1. Open T7, background the app, flip a tournament's dates in the admin, return to Home then T7 → the card reflects the change without a relaunch.
2. Deactivate a tournament in the admin, re-enter T7 → it is gone from the list.
3. Deactivate a tournament **the player has an entry in**, re-enter T7 → still listed, still playable, no `KeyNotFoundException` anywhere.
4. Airplane mode, re-enter T7 → the list renders from cache exactly as before, no error UI, one log line.
5. Enter and leave T7 five times in ten seconds → **one** network request, not five.
6. A tournament whose art is new arrives on refresh → its card gets the remote art without a relaunch.
7. Full EditMode suite green, swept **per assembly** (a filtered run reports `FailedTests` for the filter only; `tests-run` also intermittently reports "No tests found" for a valid assembly — retry, never read that as green).
