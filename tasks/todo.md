# game_modes_admin — DONE 2026-08-28

Implemented in the SPEC's §5 order. Deployment was a step, not an epilogue.

## 1. Backend — server-validated entry fees ✅
- [x] `migrations/2026_08_28_golfin_mode_fees.sql` — RLS on / no policies, seeded, verification block. APPLIED.
- [x] `/spend`: `mode_entry_fee:` prefix → unknown_mode / mode_locked / fee_changed(+fee), all 200, refused BEFORE the rpc
- [x] `tests/test_mode_entry_fee.py` — 16 tests (backend suite 117 passed)
- [x] Deployed: **v58**, `playlife-api:deployment-01M13PM5NTDK20FB5E7HKRKFD5`
- [x] Smoke: /health 200 · friends 403-not-404 · garbage 404

## 2. Content — modes as the TENTH catalog ✅
- [x] `Tools/content/catalogs.py` += `Catalog("modes", …)`
- [x] `2026_08_28_content_modes_seed.sql` via `--catalogs modes`. APPLIED.
- [x] Export byte-identical (md5 `c36e4288…` before AND after four publishes); `--check --catalogs modes` clean

## 3. Admin dashboard ✅
- [x] Modes panel (shared CatalogPanel) + registry + icon + EN/JA + validation rules
- [x] Rewards panel over `game_point_actions` — checkAdmin + writeAudit, live-on-save, no create/delete
- [x] `golfin_mode_fees` mirror on publish (publish FAILS on mirror error)
- [x] Drift warning: versus_1v1 ↔ versus_win ONLY
- [x] `npm run build` green → deployed `429883ff-…`, stamp `256f21587` verified in-browser

## 4. Unity ✅
- [x] `ModesDatabaseCSV` overlay + withhold rule (target set read from `ModeSelectScreenController`'s dispatch consts)
- [x] Suffixed reason at `ModeCardController.cs:604` via `SpendReasons.ModeEntryFeeFor`
- [x] `SpendVerdict.FeeChanged` / `UnknownMode` / `ModeLocked`; card re-prices, second tap pays
- [x] EditMode sweep 1955 / 1952 passed / 0 failed; both new suites tripwire-verified

## 5. Docs ✅
- [x] `Tools/content/README.md` catalog table; `Docs/ADMIN_DASHBOARD_OPS.md` §3.0 (content vs live panels)
- [x] STATUS.md, IMPLEMENTER_REPORT.md, AI_CONTEXT.md

## Review

**What went right.** Running the deploys early — the API before the admin work,
the dashboard before the E2E — meant the live E2E was a 15-minute exercise
instead of a scramble at the end. Reading the real dispatch `const`s instead of
re-listing the targets is the one design choice most likely to still be correct
in a year.

**One surprise worth recording.** Both spend test files patch the SAME
`routers.points` module global at import time, so whichever imported last owned
it for the whole pytest session and eleven tests failed on a file that had
nothing wrong with it. Fixed by re-asserting the fake in each file's autouse
fixture. Any future test file touching `routers.points` needs the same line.

**Left alone deliberately.** The full `export_content.py --check` exits 1 on a
pre-existing `texts` drift (two gacha keys in the CSV but not the catalog, from
`a10f46318`). The repo is AHEAD of the catalog, so it needs a re-seed of those
two keys — not this task's, and not an export.

**Still open by design.** The bare `mode_entry_fee` reason is still accepted; it
is what every installed build sends. Closing it is one line, on Cesar's word,
after the build carrying the suffix ships.
