AWAITING_MIGRATION

Built 2026-08-31 (Claude Code, direct implementation — no subagent pipeline: this task
touches zero Unity assets, so there is no screenshot, no Figma node and no scene to review).

BLOCKED ON CESAR, and only on this: apply the two migrations in the Supabase SQL editor,
in order, then paste each VERIFICATION block back.

  1. playlife/backend/migrations/2026_09_01_golfin_gacha.sql
  2. playlife/backend/migrations/2026_09_01_shop_purchase_tickets.sql   (calls functions from 1)

DDL has no path from this machine — Supabase's REST API has no DDL endpoint and there is no
Postgres connection string here (ADMIN_DASHBOARD_OPS §3.2). Both files are parse-checked with
pglast (statement level AND every plpgsql body) and pasted in full in the session.

DONE and verifiable now: the migrations, routers/gacha.py + main.py, tests/test_gacha.py (58),
the extended tests/test_shop_purchase.py, the whole dashboard half, the docs. Backend suite 233
green, dashboard vitest 216 green, `npm run build` green, API deployed
(playlife-api:deployment-01M1B5F2YV1ZJT84RX7RSGN5WW, v64) and smoke-tested.

STILL TO RUN, all of it in one pass once the migrations land (see IMPLEMENTER_REPORT.md
§ "What is outstanding"): the two VERIFICATION blocks, SPEC §7 roll parity, SPEC §8 live E2E
steps 1–8. The service key reaches prod over PostgREST from here, so none of it needs Cesar
beyond the paste.
