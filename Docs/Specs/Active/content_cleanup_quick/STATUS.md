READY_FOR_ARCHITECT_REVIEW

Implemented directly by Claude Code at Cesar's request (2026-08-26). No SPEC.md — the five items
are specified in `Docs/TellCode.md` § `content_cleanup_quick`, with the decisions of record for
items 4 and 5 in `Docs/CONTENT_PIPELINE_PLAN.md` §6.5. SPEC_KIND: backend + admin tooling; no Figma
node, no screenshot deliverable mandated (the two dashboard items were still verified visually —
see IMPLEMENTER_REPORT.md).

All five items are done and verified. Nothing outstanding.

| # | Item | Where | Verified by |
|---|---|---|---|
| 1 | Drop the per-catalog `enabled` field | playlife `routers/content.py`, Unity `Golfin.Content` | 11 backend tests + 4 EditMode tests, both tripwired |
| 2 | Dashboard control for the GLOBAL kill switch | `Tools/admin-dashboard` | driven through the real UI, mock mode, EN + JA |
| 3 | Shared `TestBoot.SaveDataHost()` | `Assets/Scripts/TestSupport/` | tripwired — 5 tests across 3 fixtures go red without it |
| 4 | Revoke an UNAPPLIED grant from the Users drawer | `Tools/admin-dashboard` | driven through the real UI + all four route outcomes |
| 5 | Log every merge that RAISES a quantity | `Golfin.InventorySync` + `TelemetryHooks` | 7 EditMode tests, tripwired |

Test state at hand-off:

* Unity EditMode — **1768 total, 1765 passed, 0 failed, 3 skipped** (the 3 skips are pre-existing
  `HoleCompleteDriverTests` Stage-C1 skips, unrelated).
* playlife backend — **26 passed** (`tests/test_content_kill_switch.py` + `tests/test_golfin_inventory.py`).
* admin dashboard — `tsc --noEmit` clean, `next build` clean, new route present in the manifest.

⚠️ NOT YET DEPLOYED / NOT YET APPLIED. The playlife change is committed to the working tree only;
`playlife-api` still serves the per-catalog `enabled` field until Cesar deploys. That is safe in
either order — the field is additive-to-remove and the client ignores unknown fields — but the
device pass should run against a deployed API so the wire shape matches the client under test.

Awaiting Cesar's sign-off. The batched device pass is what comes next.
