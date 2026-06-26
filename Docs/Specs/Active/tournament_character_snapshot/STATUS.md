READY

# tournament_character_snapshot — STATUS

- **State:** READY (specced, not fired)
- **Tier:** TELLCODE (additive seam) + required EditMode freeze-invariant test
- **Slug / kickoff:** `Use the golfin-implementer subagent on "tournament_character_snapshot"`
- **Reopens:** T4 `LocalTournamentBackend` + T1 `EntryState` (additive); does not change T4 scoring
- **Depends:** T1 ✓, T4 ✓
- **Blocks:** T5 (persist snapshot — T5 spec updated to include it), T6 (consume)
- **Fire order:** this amendment → T5 → T6
- **Notion order:** 508 (Queued)

## Why
Owner ruling: freeze character state per tournament; treat the tournament character as a separate,
immutable snapshot captured at sign-up. Must land before T5 fires so the snapshot ships in the single
v2→v3 save migration (no second migration).

## Done when
4 tests green (capture, freeze-invariant, store round-trip, unknown-id throw); `EntryState.Snapshot`
populated at `Register`; production call sites pass `CharacterManagerStatsProvider`.
