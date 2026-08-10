DONE

Approved by Cesar 2026-08-10. Implemented by the orchestrator (main Claude Code thread) at his
direct request — the subagent chain was not used. Report: `IMPLEMENTER_REPORT.md`.

canonical surfaced: Docs/Specs/Active/auto_club_selection/screenshots/tee_turn1_DRIVER.png @ 2026-08-10T15:29+09:00

Shipped in `43d8a34c9`. Carried forward, NOT closed by this task:
- OB / water-drop reposition path has no real-flow evidence (neither playthrough went OB).
- §2f `ClubContext` gap is player-visible on the green — the club button keeps showing the last
  non-putter club because §2f commits with bare `SetClub`. Worth a follow-up Quick task.
- No on-device pass; no human finger-driven manual-override pass.
- P-006 evidence collected only: the bot's calibration has the driver carrying ~403 m at power
  0.96 against a `baseDistance` of 250 yd. Auto-select trusts `baseDistance` per SPEC.
