READY_FOR_ARCHITECT_REVIEW

Phase 4 implemented DIRECTLY by the main Claude Code thread at Cesar's instruction — no
golfin-implementer / self-reviewer / red-team chain ran on this one, so SELF_REVIEW.md and
ARCHITECT_REVIEW.md are still the unfilled template. It goes straight to Cesar.

Built and verified:
  · backend — migration + routers/golfin_inventory.py + 15 tests (whole suite 25 green)
  · Unity  — Golfin.InventorySync (10 files) + 55 EditMode tests; SaveData schema v10 -> v11
  · admin  — Inventory tab + Grant items in the Users drawer; tsc clean, next build green

Full unfiltered EditMode sweep: 1761 / 1758 / 0 / 3 (baseline 1706 / 1703 / 0 / 3; +55 = exactly
this task's tests, zero failures, same 3 pre-existing skips).

SHIPPED TO PROD 2026-08-26 — the two blockers are cleared:
  1. migration APPLIED (Cesar). All 7 verification rows as expected, including
     grants_rls 1 / grants_policies 0 and user_inventory_untouched 1.
  2. playlife-api deployed v51 -> v52 (image deployment-01M0XZD461YMEZZ2X53PFCYWGJ,
     confirmed by flyctl status, never the exit code). /health /notices /banners
     /tournaments/golfin all still 200; the four new routes answer 403
     unauthenticated and 401 on a bad token — mounted and auth-gated, not a
     route miss. PostgREST's schema cache has the new columns and the grants
     table (checked directly — a cached-away column is the silent failure the
     router's _missing_relation handler would otherwise hide).

ALL 11 ACCEPTANCE ITEMS PASS. Remaining: Cesar's approval, then the device pass
(restore-after-reinstall, and a grant applying exactly once across three launches).

See IMPLEMENTER_REPORT.md § Acceptance checklist.
