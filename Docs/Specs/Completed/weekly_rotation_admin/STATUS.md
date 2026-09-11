DONE

weekly_rotation_admin — approved by Cesar 2026-09-11 ("get some sort of video evidence and then done").

The store and the gacha rotate weekly, authored in the admin as one unit. Catalog #21 `rotations`
(52 planned weeks seeded at v1, byte-identical round trip), the pure generator lib/rotation.ts
(pins win, mulberry32 fills the blanks), the Rotations panel (8-week calendar, PREVIEW ->
MATERIALIZE -> PUBLISH ROTATION in five-catalog order, ARCHIVE ENDED = deactivate), validator
R1-R4, and the pity migration 2026_09_11_gacha_pity_group.sql (counter keyed by pityGroup, cap per
banner). Cleared self-review, golfin-reviewer and the red-team gate after four iterations.

Live on production: wk_2026_38 (DRIVER WEEK · BOGEYB) published for Mon 2026-09-14 — shop_catalog
v10, gacha_banners v13, gacha_pools v5, gacha_rates v6, rotations v4; pity group proven on prod
(weekly counter 3 across two banners); dashboard /api/version = 70464d323 (Cloudflare
29e5e6d8-432a-49ba-8a53-0d54fb0d02c0); API v74 untouched. Video evidence in videos/ (local-only,
sent in chat): the production read-only walk-through and the mock-mode full flow.

Open, non-blocking, for the Architect on Tuesday (IMPLEMENTER_REPORT.md § Manual verification):
  - one-ball vs ten-ball listing; the named rotation_* audit action; ECONOMY_MASTER §3 wording;
    the two Freda (Supreme) weeks; four shop_stocking club placeholders (shop_club_driver_gf
    collides with wk_2026_39's pins); missions/loadouts active-only guard.
  - wk_2026_39's banner needs its own artUrl before Mon 2026-09-21, or installed builds
    withhold it (no art until Tuesday at the earliest, per Cesar).
