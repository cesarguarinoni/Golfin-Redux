SUPERSEDED IN PART — 2026-08-17
§9 (`tournament_modal`: the placement, `tournaments.modal_banner_id`, the Tournaments-panel
picker, the `GET /tournaments/golfin` join) was amended into this spec after implementation had
started and was NEVER BUILT. It moved here with the rest of the spec and became invisible.
It is now refiled standalone as `Docs/Specs/Active/tournament_banners/`, which supersedes §9.
Everything else in this spec did ship and is live.

DONE

Approved by Cesar 2026-08-17. Shipped end to end:
  - migration applied to prod, verified over PostgREST
  - playlife-api deployed, GET /api/v1/banners live
  - golfin-admin deployed (4cab1bbb), Banners panel live at admin.golfin.world/banners
  - client half committed; a real banner created in the panel renders on device

Post-approval follow-ups, carried into AI_CONTEXT rather than held here:
  - Home slot aspect is now 4.53 (970x214); live art is 978x262 (3.73), so it is
    stretched. Either author at the slot ratio, or set Image.preserveAspect.
    lib/banner.ts already advertises 970x214 but the dashboard has NOT been
    redeployed with that guidance.
  - untested for lack of data, not for lack of code: JA swap (image_url_ja null),
    tap-through (link_url null), [BannerArt] Cache HIT across two cold launches,
    sort_order tie-break (one active row).
