# STATUS — loop_v2_scope

**Status:** ✅ CORE SHIPPED — all stages closed in Notion (Orders 310–340) and `Docs/Specs/Completed/`. This umbrella is a historical scoping doc; it is NOT open work. (Retained for reference; safe to move to Completed/.)
**Type:** Scoping spec — produced the stage breakdown below. Implementation lived in the per-stage sub-specs.
**Next:** Loop v2 core loop is playable end-to-end (since 2026-05-19, first full playthrough at C0). Only possibly-remaining: Stage F formal polish pass (fades/button-feedback largely already landed inside C0+C1) and any Stage E hole-selection-entry hardening (covered by C0 + smoke-bot Hole-Selection-Browse scenario). Confirm with Cesar whether F is still wanted before reopening.

## History
- 2026-05-19 — Architect read-only audit of UI/Scripts + GameSession. Audit findings written to `Docs/Architecture/CODE_AUDIT_2026-05-19.md`. SPEC.md committed.
- 2026-06-03 — Reconciled: all core stages found already shipped (Notion + code + Completed/). Staleness cause: this STATUS "Stages queued" list was never advanced past Stage A after the stages closed, and TellCode's pointer was overwritten by the parallel green_ship_polish track. Notion stayed accurate throughout.

## Stages — actual status (authoritative source: Notion GOLFIN_Roadmap + `Docs/Specs/Completed/`)
- Stage A: `loop_v2_a_singletons_consolidation` — ✅ DONE (Notion Order 310; commit `8ee5c1d2`, 2026-05-19; `Completed/loop_v2_a_singletons_consolidation/`).
- Stage B: `loop_v2_b_session_state_plumbing` — ✅ DONE (Order 320; commit `0e61d497`, 2026-05-19; `Completed/loop_v2_b_session_state_plumbing/`). `GameSession`→`Golfin.Gameplay.Session`, seed fields, `OnHoleComplete`+`HoleCompletionData`, `ISessionStore`, `HoleCompletionBridge`.
- Stage C0: `loop_v2_c0_matchmaking_to_gameplay_transition` — ✅ DONE (Order 330; commit `ace9e1ec`, 2026-05-19; first end-to-end production playthrough). Closed the OPPONENT-FOUND→ball-at-tee gap; additive `GameplaySceneLoader`.
- `loop_v2_smoke_bot` — ✅ DONE (Order 335; commit `a8901d99`, 2026-05-20). Two-layer bot framework; default visual gate for later stages.
- Stage C1: `loop_v2_c1_result_modal` — ✅ DONE (Order 340; commit `1731e222`, 2026-05-21; FULL PIPELINE). ShellScene Result modal (SUCCESS/FAILED two-card), PLAY NEXT/MENU/RETRY, progression writes, reward grant, course-cleared toast.
- Stage D: `loop_v2_d_next_hole_autoflow` — ✅ ABSORBED into C0 + C1 (no separate spec; PLAY NEXT autoflow shipped in C1, LoadingScreen generalization in C0).
- Stage E: `loop_v2_e_hole_selection_entry` — ✅ effectively covered (HoleSelection→Matchmaking→Gameplay path shipped in C0; smoke-bot Hole-Selection-Browse scenario passes). Reopen only if hardening gaps surface.
- Stage F: `loop_v2_f_animated_ui_polish` — 🟡 PARTIAL (unified FadeController for modal+backdrop landed in C0; `ButtonPressFeedback` per Hard Rule 11). A formal F polish pass is the only stage not explicitly closed — confirm scope before reopening.
