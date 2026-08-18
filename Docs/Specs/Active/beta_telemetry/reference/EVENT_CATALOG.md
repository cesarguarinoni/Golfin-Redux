# Event catalog — pointer

The canonical event catalog + wire format + table schema live in `../SPEC.md`
§1–§2. This file exists so `telemetry_admin_panel` has a stable path to cite;
it deliberately does NOT duplicate the tables (duplication drifts).

## As-built deltas (2026-08-18) — read these before building the panel

The SPEC tables are still canonical for the wire format. These are the places the
shipped implementation deviates from them, resolved against the codebase:

- **`sp_allocated` is NOT emitted.** SP allocation is unwired in the client:
  `CharacterManager.Awake` constructs a `ManualSPAllocation` into `allocationStrategy`
  and nothing ever calls `AllocateSP`, and `ConfirmPendingSP()` has no callers outside
  `PlayerCharacterData`. There is no commit call site to hook, so per SPEC §1 #13 the
  event was skipped. 12 of 13 events ship. Do not build a panel column for it.
- **`round_start` fires on every hole**, not once per session — from both
  `GameSession.SeedSession` (session seed) and `GameSession.SetCurrentHole` (the
  PLAY NEXT path). Treat it as "a hole began", not "a session began".
- **`round_abandoned` also fires on the tournament menu screens**
  (`TournamentHoleSelection`, `TournamentSelection`) in addition to
  Home / HoleSelection / ModeSelection.
- **`points_changed.delta` is `null` on the first event of a session** — there is no
  previous balance to diff against. Read the event as "balance observed", not
  "points earned": it also fires on the boot-time server balance sync.
- **`hole_complete.par`** comes from `Golfin.Gameplay.UI.HUD.HoleContext.Par`, the same
  value the result modal and hole card render.
- **The ingest response carries a third field**: `{"data":{"accepted":N,"duplicates":M,"rejected":R}}`.
  `rejected` counts events dropped by per-event validation (bad UUID, unparseable `ts`,
  empty/over-long `name`, payload > 4KB, duplicate `event_id` within one batch). A
  non-zero `rejected` in the logs means a client bug, not a server one.
