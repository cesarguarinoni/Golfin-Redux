# STATUS — loop_v2_scope

**Status:** SPEC_READY (architect scoping pass, 2026-05-19)
**Type:** Scoping spec — produces stage breakdown, not implementation.
**Next:** Cesar answers 5 open questions in SPEC.md § "Open questions for Cesar". Then Stage A fires as its own active spec.

## History
- 2026-05-19 — Architect read-only audit of UI/Scripts + GameSession. Audit findings written to `Docs/Architecture/CODE_AUDIT_2026-05-19.md`. SPEC.md committed.

## Stages queued (will become their own Active specs as they fire)
- Stage A: `loop_v2_a_singletons_consolidation` — Audit P0-1 + P0-2 fixes
- Stage B: `loop_v2_b_session_state_plumbing` — GameSession extension + namespace move
- Stage C: `loop_v2_c_result_modal` — Production result modal (FULL PIPELINE)
- Stage D: `loop_v2_d_next_hole_autoflow` — PLAY NEXT auto-flow
- Stage E: `loop_v2_e_hole_selection_entry` — wire HoleSelection → Matchmaking → Gameplay
- Stage F: `loop_v2_f_animated_ui_polish` — fades, tweens, button feedback
