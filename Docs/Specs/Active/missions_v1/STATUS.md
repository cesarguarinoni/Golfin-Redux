READY_FOR_SELF_REVIEW

Phases A and B complete AND LIVE ON PROD. Phase C (Mission Selection screen) not started — C1
(the mode-card target wiring) was pulled forward into B.

The mode is still LOCKED and still has no screen, so nothing is reachable by a player. That is
the intended Phase-B end state: the economy is server-authoritative before the door opens.

## Deployed state (verified 2026-08-29)

  schema      2026_08_29_missions.sql applied — 6 tables, 2 functions, 5 earn actions,
              RLS on with no policies, anon refused (HTTP 401) on all six
  catalogs    all seven seeded at v1; missions v2, mission_tiers v2
  mirrors     golfin_mission_rewards 40 (10 per tier, RP 15/25/40/60)
              golfin_mission_tier_bonus 4 (50/100/200/300, missions_in_tier 10)
              the two agree, so the 10-of-10 bonus is reachable in every tier
  API         playlife-api image deployment-01M16C18A8ETDJ6HRPVJ0PNRWR (v63, nrt)
              /api/v1/missions/* answer 403 (auth), not 404
  dashboard   Cloudflare d1f9befc-b216-4b42-b34d-798d8789ef43
  content     mission 37 re-sited hole 13 -> hole 8, published

## Gates

  Unity EditMode   2021 tests / 2018 passed / 0 failed / 3 pre-existing skips
                   (both new mission suites proven live with a tripwire)
  Backend          172 passed
  Dashboard        126 passed, tsc clean, next build green
  Tools/content     35 passed

## Still open, none of them blocking

  * publish `texts` (131 keys) — nothing renders them until Phase C
  * PLAYLIFE_API_URL + PLAYLIFE_ADMIN_KEY on Cloudflare — only the Daily preview needs them
  * HOLD `modes` until Phase C — publishing it routes the Missions card at a screen
    that does not exist yet
  * two design confirmations: UPPER_SNAKE localization keys, and mission_start_areas as the
    baked per-hole table (both recorded as deviations in IMPLEMENTER_REPORT.md)
