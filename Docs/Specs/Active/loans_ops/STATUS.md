READY_FOR_SELF_REVIEW

loans_ops iter-1, 2026-09-10.

Built: the Loans ops panel, the Users drawer Loans tab, the Telemetry Loans section, seven API
routes, and the playlife migration + GET /loans/rules. API deployed v73 -> v74 and verified live.
Dashboard deployed (id in IMPLEMENTER_REPORT.md).

BLOCKED ON CESAR (not on the pipeline): the migration 2026_09_10_golfin_loan_events.sql needs to
be pasted into the Supabase SQL editor. Until then the timeline reads "not migrated" naming the
file and an admin action answers 503 naming it -- both probed against production, neither 500s.
Everything else on the panel works today.
