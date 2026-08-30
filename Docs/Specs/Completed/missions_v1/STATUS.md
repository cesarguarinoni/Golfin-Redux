READY_FOR_SELF_REVIEW

Phases A, B and C are built. A and B are LIVE ON PROD. C is built and verified in the Editor
through the real player entry path, but the mode is still LOCKED — the door opens with the
`modes` publish, not with a build, so nothing here is reachable by a player until Cesar
publishes.

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

## Phase C state (Editor-verified 2026-08-29)

  screen      MissionSelectionScreen cloned from HoleSelectionScreen; MissionCard.prefab
              cloned from HoleCard.prefab (guid 6717663c8484640909c58d78cd02f8c2)
  entry       reached by invoking the real ModeCardController.playButton.onClick, in both
              ModeSelectScreenController and ModeCarouselController (the carousel keeps
              three copies of every card, so only the real onClick proves it)
  daily       GET /api/v1/missions/daily bound live — hole, reset countdown, and the reward
              the server decided (today's draw hit DOUBLE_RP, hence x60 vs a campaign x15)
  layout      daily card sized to its measured content (374px, symmetric 24px padding) and
              CardsContainer gives back the same 34px, so its bottom edge stays at worldY 344,
              identical to HoleSelectionScreen's
  outline     the daily card carries a gold rim: S_GachaCardBorder3 (a real stroke-on-transparency
              9-slice atom) tinted #EEDC9A at ppu 0.5. Measured on the frame: (238,220,154)

## Gates

  Unity EditMode   2035 tests / 2032 passed / 0 failed / 3 pre-existing skips
  Scene guardrail  ShellScene vs HEAD: 0 fileIDs lost, 0 active-state flips
  Backend          172 passed
  Dashboard        126 passed, tsc clean, next build green
  Tools/content     35 passed

## Still open

  * publish `texts` (131 keys) — Phase C renders them now, so this one matters
  * publish `modes` — this is what opens the door; hold it until you have signed off on C
  * PLAYLIFE_API_URL + PLAYLIFE_ADMIN_KEY on Cloudflare — only the Daily preview needs them
  * two design confirmations: UPPER_SNAKE localization keys, and mission_start_areas as the
    baked per-hole table (both recorded as deviations in IMPLEMENTER_REPORT.md)
  * still to build: the Figma fidelity table + UI lint, the JA capture, the Hole Complete goal
    strip, §21's live end-to-end run, and the start-marker thumbnail calibration

## Video deliverable (2026-08-29)

  raw          Docs/Specs/Active/missions_v1/videos/raw.mp4 — 1170x2532, 29.7s, 30fps
  captioned    Docs/Specs/Active/missions_v1/videos/missions_v1_mission_selection.mp4
  report copy  Docs/Reports/Media/missions_v1_mission_selection.mp4
  recorder     Assets/Scripts/UI/Editor/MissionsDemoRecorder.cs
               (GOLFIN > Missions > Record Demo Video)

Driven through the real entry path: the title gate is tapped, then Home, then the PLAY
button on the Missions mode card itself — not ScreenManager.ShowScreen. The run spends the
real 50 RP entry fee, which is why the RP counter reads 458 rather than 508.
