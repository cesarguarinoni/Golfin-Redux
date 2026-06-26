READY

# tournament_backend_bootstrap — STATUS

- **State:** READY (specced, not fired)
- **Tier:** FULL PIPELINE (integration wiring) — wireup test + adapter tests + on-device smoke
- **Slug / kickoff:** `Use the golfin-implementer subagent on "tournament_backend_bootstrap"`
- **Depends:** T4 ✓, T5 ✓, tournament_character_snapshot ✓
- **Unblocks:** T7/T9 screen Stage-2 binds (live data) + T6 (round loop)
- **Notion order:** 509 (Queued)

## Why
The backend is fully built + tested but never constructed in production — no composition root. This
adds the 3 missing Unity adapters (RP/items/par) + a `TournamentService` singleton that news up
`LocalTournamentBackend` with all real seams and exposes `ITournamentBackend` to the game.

## Done when
`TournamentService.Compose()` builds a non-null backend; `GetTournaments().Count == 6`; a `Register`
yields a non-null `Snapshot` (proves stats provider wired — guards the `_stats?.` trap); adapter tests
green; asmdef graph compiles; on-device smoke logged.
