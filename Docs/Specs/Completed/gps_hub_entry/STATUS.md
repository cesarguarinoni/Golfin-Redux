DONE

Task: gps_hub_entry
Iteration: 1
Approved by: Cesar — 2026-09-01
Implementation commit: 2c7ea1eca

The Home promo banner carries one in-app route (`golfin://gps`) that navigates to
`ScreenId.GpsHub`; the hub is built from Figma `14011:32819`, bound to live
`/user/detail` + `/score/history`, and localized EN + JA (`texts` v22).

Evidence: `IMPLEMENTER_REPORT.md`.
Canonical screenshot: `screenshots/gps_hub_rounds_populated.png`.

Remaining, both the Architect's (SPEC § Out of scope):
  1. Activate the prod `home_promo` banner row — sprite `Assets/Art/HomeScreen/GPS Banner.png`,
     `link_url = golfin://gps`, active, no schedule.
  2. Deploy the admin dashboard (`Tools/admin-dashboard/scripts/cf-deploy.sh`); until then the live
     Banners panel still rejects `golfin://gps` in its inline validation.
