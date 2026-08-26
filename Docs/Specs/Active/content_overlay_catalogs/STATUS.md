READY_FOR_SELF_REVIEW

Task: content_overlay_catalogs (CONTENT_PIPELINE_PLAN §5, Phase 2)
Set: 2026-08-26 by Claude Code (direct implementation, not the subagent chain — Cesar kicked this
off as "read the SPEC and implement it").

EditMode sweep: 1692 total / 1689 passed / 0 failed / 3 skipped (pre-existing intentional skips).
Baseline before any edit was 1615 / 1595 / 17 failed / 3 — all 17 stale assertions fixed.

Three findings need a human read before this is DONE:
  1. The DB-before-Manager guarantee is a committed .cs.meta field nothing re-asserts (now asserted
     at runtime). CharacterManager and SaveDataHost are BOTH at -100 — a tie, surfaced not fixed.
  2. SPEC §7 is half-closed and half-reported: the client now drops a requested-but-absent
     catalog cache, but the payload still cannot distinguish is_enabled=false from an unknown
     catalog name. Needs a one-field API addition.
  3. CharacterManager.GetMaxLevel ignored the CSV maxLevel column entirely — fixed, because the
     clamp and the UI were about to disagree and cost the player RP on every relaunch.

See IMPLEMENTER_REPORT.md for the full acceptance table and what still needs on-device verification.
