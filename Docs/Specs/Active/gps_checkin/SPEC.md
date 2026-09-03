# SPEC — `gps_checkin`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. (Standard pipeline states — SPEC_READY → IMPLEMENTER_WORKING → … → DONE.)

## The nine build rules from `gps_profile_pack` apply verbatim

Bake gradients from tokens via scripts; translucency via `GpsUiColor.A()/ADark()`; icon rings are the navy-disc-in-gold-ring atom; Main Buttons labels size 59 (the small variant follows `gps_gifts_votes` D-9: SemiBoldSize applied); geometry-JSON + invariants + UI-fidelity lint gates with the numbers quoted; SemiBold white for interactive text; **every new text key PUBLISHED**; Editor builder scripts are the prefab source of truth; reuse the existing atoms (`S_HUB_*`, `S_GpsIconRing_*`, GPS Icons, `S_SU_ModalPanel`, `ShimmerBlock`, `UiMotion`). `gps_polish`'s motion applies to the new screen and modals IN FULL, as an acceptance item (Cesar 2026-09-03: "make sure the new screens have polished transitions like the previous ones"): the layered push into/out of GpsRounds (transition table + `GpsScreenTransition.CanPush` true for it), entry rise, `Stagger` on fetch-paint of spot rows and history rows, `ShimmerBlock` on cold fetch, list cross-fade on chip change and on the list↔active flip (chips fade out, the Active Round Card `Pop`s in, the list retitles under a cross-fade), `animateShow` pop on both modals, `PendingSpend` on CHECK IN / CHECK OUT / POST SCORE, `CountUp` on the +30/+15 in the Top UI and on the card's PTS EARNED, the elapsed digits tick without layout jitter (fixed-width digits or a fixed rect), pin placement animates on tile re-fetch (pins `Fade` out/in with the tile, no snap), and the 60 fps / GC gate from `gps_polish` A13 re-run on this screen.

## Goal

The hub's **Rounds** nav slot becomes the Rounds tab: PLAYLIFE's Rounds screen as designed 2026-09-03 (Cesar: "exactly like the PLAYLIFE app did it") — category chips, a **real map**, nearby spots, CHECK IN → a live round card → SCORE UPLOAD or CHECK OUT — **wired to the backend for real**, which PLAYLIFE's Flutter tab never was (its CHECK IN set local state and faked "+50 pts"). Plus an **admin Partners panel** so venues in all three categories (golf courses, driving ranges, food & drink) are managed online, seeded with PLAYLIFE's demo spots for now.

Player-visible promise closed by this task: the Welcome tutorial's "CHECK IN — Prove it with GPS" tile, and the hub nav bar's inert Rounds slot.

## Reference

- **Figma (page GPS / PLAYLIFE, section "Check-in — Rounds tab", file `5gEAHjl6xAtW8iYY7NMvWd`), approved by Cesar 2026-09-03 with two edits already applied (no GAME pill on these screens; modal pin centred):**
  - `14076:33800` GPS Rounds - Check-in (list) → `reference/rounds_list_14076-33800.png`
  - `14077:100447` GPS Rounds - Active round → `reference/rounds_active_14077-100447.png`
  - `14080:34097` GPS Rounds - Check-in confirm (modal) → `reference/checkin_confirm_14080-34097.png`
  - `14078:33991` GPS Rounds - Check-out summary (modal) → `reference/checkout_modal_14078-33991.png`
  - Re-pull each node with `get_metadata` + `get_design_context` at step 0 (PIPELINE_HARDENING §9); the table below is the convenience copy.
- **The stylised map tile in the frames is a placeholder.** In the build the panel shows a real Google map (§B4). The pins, "NEAR ME" pill, legend and attribution are ours and are built as designed.
- **PLAYLIFE source of truth for behaviour:** `playlife/lib/features/home/presentation/widgets/rounds_map_tab.dart` (layout order, category → visible spots, "after check-in show food first", elapsed format `h:mm`, the two sheets), `lib/features/activity/presentation/controller/activity_notifier.dart` (the never-called real check-in), `backend/routers/activity.py` (`/activity/checkin`, `/{id}/checkout`, `/{id}/cancel`, `/history`), `backend/routers/score.py` (`_verify_gps`, K4 `MULTI_GPS_THRESHOLD = 3` → `+20`, the activities insert at ~line 200), `backend/migrations/2026_04_24_gps_trust.sql` (activities GPS columns, `venues.gps_radius_m`), `backend/migrations/2026_09_02_gift_atomic.sql` (the RPC pattern to copy: SECURITY DEFINER, service_role-only, locks, idempotency key, ledger + invariant).
- **Unity already has:** `Golfin.Gps` — `GpsSessionTracker.RecordFix` (the K4 counter), `LocationProvider`, `GpsTrustSignals`, `GpsScoreAttachment`, `VenueService.Nearby(prefixes)`, `Geohash.Neighbors`; `GpsNavBarBinder` (from `gps_polish`, ROUNDS slot deliberately inert — this task wires it); `ScoreUploadFlowController` (`ScoreUploadDraft` is the seam for prefilling venue + attaching evidence); `VenuePickerModalController`.

## Figma Fidelity (Rule 18 — values pulled 2026-09-03; every row is a lint spec.json element)

| Element | Node | Property → value |
|---|---|---|
| Status Row | `14077:33873` | 958×40 at (10,0) in Content Container; left "NEARBY · 12 SPOTS" Rubik Medium 28 #b7c3d3; right **GPS Status Pill** 180×40 r100 fill #7ed488@0.16 stroke #7ed488 w1, text "● GPS ON" SemiBold 24 #7ed488 centred. Active state: "CHECKED IN · 08:12〜" / pill "● LIVE" #eedc9a (fill @0.18, stroke #eedc9a) |
| Category Chips | `14077:33877` | 958×60, three chips (958−24)/3 wide gap 12, r100; selected = gold gradient #f3ecc2→#c9a94f stroke #422100 w1 label #2a1a00; unselected = ADark(black,0.35) stroke #818ea1 w2 label #ffffff; labels SemiBold 24 "GOLF COURSES" / "DRIVING RANGES" / "FOOD & DRINK". Hidden while a round is active |
| Map Panel | `14077:33884` | 958×560 panel atom (fill #133453→#091b33, stroke white→#d1d5db(0.4)→#818ea1 w3, r50, shadow + blur); **Map Surface** 918×420 at (20,20) r36 clipped — the live tile goes here; **You Are Here** 60 ring #4f86d6@0.25 + 24 dot #4f86d6 stroke white w3; pins 44 (fill by category: partner #7ed488 / registered #7b9b8a / food #f0a050, stroke white w3, 14 white centre); **Recenter** pill 140×44 r100 ADark(black,0.45) stroke #818ea1 w2 "◎ NEAR ME" SemiBold 22 white, top-right inset 16; **Legend** row at y460: 18 dots + Medium 24 #b7c3d3 "PARTNER / REGISTERED / FOOD & DRINK"; attribution "Map · Google" Medium 20 #b7c3d3 @0.7 right |
| Sort Bar | `14077:33958` | 958×40: "NEAREST FIRST" Medium 24 #b7c3d3 left; "DISTANCE  ▾" Medium 24 #eedc9a right |
| Spot List Panel | `14077:33961` | panel atom; header 80 "NEAR YOU" SemiBold 42 #eedc9a (active state: "NEARBY FOOD & DRINK"); separator; rows 130 each |
| Spot Row | `14077:34004` / `34021` / `34037` | Icon ring 80 at (32,25): navy-disc gradient #204b76→#0b203d, stroke w3 #f3ecc2 (partner: #7ed488; food: #f0a050), Pin icon 40 centred; Info at (132,15): Name SemiBold 30 #ffffff, **Partner Tag** 112×30 r100 #7ed488@0.18 stroke #7ed488 "PARTNER" SemiBold 20, Subtitle Medium 24 #b7c3d3 ("Kawagoe, Saitama · East 18H · PAR 72"), Distance line Medium 24 #7ed488 ("2.4 km · ¥15,000〜"); **CHECK IN** = Main Buttons Gold-Small 230×54 at x 696 (active-state food rows: "DETAILS"); row separator 894 white@0.10 |
| My Recent Rounds Panel | `14077:100404` | the hub's Friends Rounds panel verbatim, title "MY RECENT ROUNDS", See-All "ALL ROUNDS  ›" visible; rows bound to `/activity/history` (own rounds). List state only |
| Active Round Card | `14077:100661` | 958×340 panel atom with **stroke #eedc9a w3**; Live Pill 150×40 r100 #e5484d@0.9 "● LIVE ROUND" SemiBold 22 white at (32,24); "Since 08:12" Medium 24 #b7c3d3 right; Venue SemiBold 40 #eedc9a at y78; Venue Sub Medium 24 #b7c3d3 y130; separator y172; four stats (894/4 each) at y186: value SemiBold 40 (ELAPSED white, PTS EARNED #eedc9a, GPS #7ed488 "● HIGH", GPS FIXES white) + label Medium 22 #b7c3d3; buttons y268: SCORE UPLOAD 430×54 at x32, CHECK OUT 430×54 at x496 (Gold-Small instances) |
| Check-in Confirm Modal | `14080:34292` | 958×760 panel atom stroke #eedc9a w3, centred (y = (2532−760)/2−120) over a 60 % black scrim; Title "CHECK IN HERE?" SemiBold 42 #eedc9a; Icon ring 120 (navy gradient, stroke #f3ecc2 w6) with Pin glyph 40×53 **centred**; Venue SemiBold 36 white; sub "2.4 km away · inside the course radius" Medium 24 #b7c3d3; three stats "+30 PTS ON CHECK-IN" / "+10 PTS ON CHECK-OUT" / "● HIGH GPS ACCURACY" (values SemiBold 48, labels Medium 22); note Medium 24 #b7c3d3 830 wide centred, 2 lines; CHECK IN = Gold-Small stretched 894×64; CANCEL = 894×64 r20 ADark(black,0.35) stroke #818ea1 w2 label SemiBold 28 white |
| Check-out Summary Modal | `14078:34155` | same shell; Title "ROUND COMPLETE"; sub "08:12 – 09:36 · GPS verified"; stats "1:24 ELAPSED" / "+40 PTS EARNED" (#eedc9a) / "7 GPS FIXES" (#7ed488); note "Post your scorecard now to add screenshot points (+50) and Trust +20 from this round's GPS."; POST SCORE (gold) / DONE (dark) |
| Top bar title | — | `NavTitleKeyFor(GpsRounds)` → `GPS_ROUNDS_TITLE` "ROUNDS" |
| GPS Nav Bar | instance | ROUNDS slot highlighted on this screen (the binder's active-slot state) |

## A · Backend (playlife — migration pasted in chat by the Architect for Cesar, then Fly deploy)

**A1 · `venues` becomes the spots table.** `2026_09_03_venue_partners.sql`, additive:
`category text not null default 'golf' check (category in ('golf','range','food'))`, `is_partner boolean not null default false`, `subtitle text`, `price_label text`, `chip_extra text`, `partner_offer text`, `is_active boolean not null default true`, `updated_at timestamptz default now()`; index on `(category, is_active)`. `sport_type` and Flutter's readers are untouched. Backfill: all existing rows `category='golf'`.

**A2 · `/venue/nearby` gains `category` (default `golf`), `is_active=true` filter, and returns `distance_m` computed server-side from an optional `lat,lon` (haversine) so the client sorts nothing. Cap 50 rows. `/venue/{id}` returns the new columns.

**A3 · Two atomic RPCs**, modelled line-for-line on `golfin_gift_pts` (SECURITY DEFINER, `revoke … from public`, `grant … to service_role`, profile row `for update`, idempotency key after the lock, ledger row, `activity_pts`/`total_points` moved TOGETHER, invariant kept):
- `golfin_activity_checkin(p_user uuid, p_venue int, p_lat, p_lon double precision, p_accuracy_m real, p_is_mock boolean, p_platform text, p_key uuid)` → refuses a second active round (`reason: 'already_active'` with the active id); computes `gps_verified` server-side (`haversine ≤ venues.gps_radius_m` and not mock); inserts the `activities` row (`status 'active'`, `gps_start_lat/lon`, `gps_check_count 1`, `gps_is_mock`, `client_platform`, `trust_level` 30 if verified else 0); awards **+30 `gps_checkin`** iff verified (ledger `type 'gps_checkin'`, description as `points.py` names it), 0 otherwise; returns the row + `awarded`.
- `golfin_activity_checkout(p_user, p_activity int, p_lat, p_lon, p_check_count int, p_is_mock boolean, p_key uuid)` → the row must be the caller's and `active`; sets `check_out_at`, `duration` (`"1h 24m"` like today), `gps_end_lat/lon`, `gps_check_count = greatest(existing, p_check_count)`, `gps_verified` recomputed at the END point too (both ends inside the radius = verified), `status 'completed'`; **points: 10 base + 5 if gps_verified** (PLAYLIFE numbers; the screenshot +5 stays on the score path); `activities_count + 1`; ledger `type 'activityComplete'`. Elapsed > 8 h → `status 'expired'`, 0 pts, no ledger. Replay → `replayed: true`.
- `routers/activity.py`: `/checkin` and `/{id}/checkout` become thin wrappers with `idempotency_key` (server-minted when absent — Flutter compatibility); the direct `profiles.total_points` update is DELETED (it broke the invariant). `/cancel` stays. `/history` unchanged. `/active` (new, GET) returns the caller's active round or null — the client's source of truth on launch.

**A4 · Map proxy.** `GET /venue/map?lat&lon&zoom&w&h` → Google **Maps Static API** (`https://maps.googleapis.com/maps/api/staticmap`, key = `settings.google_places_api_key`; `scale=2`, `maptype=roadmap`, a dark style string in one constant, no markers — the client draws pins); response cached 24 h in memory keyed by (lat, lon rounded to 4 dp, zoom, w, h). Rate-limit 60/min per user. ⚠️ **Cesar pre-req:** enable "Maps Static API" on the existing key in Google Cloud (Places-only restriction will 403). The report quotes the first 200.

**A5 · Score submit links the round.** `POST /score/submit` gains optional `activity_id`: when it is the caller's `active` round, the handler **updates that row** (screenshot_data, trust, points, `check_out_at`, `status 'completed'`, end coords) instead of inserting a second activity, and `gps_check_count`/start coords come from the row (max with the request). A round posted this way is ONE row in history, not two. Points for the score post are unchanged (`apply_score_submit`).

**A6 · Deploy** Fly (`playlife-api`), quote the deployment id and `/health`; run `e2e_activity_economy.py` (copy of `e2e_gift_economy.py`): check-in inside radius → +30 once; replay no-op; second check-in refused; checkout → +15, `activities_count +1`; expired path; invariant query before/after (`0 violations`); score submit with `activity_id` → one row.

## B · Admin dashboard (Tools/admin-dashboard — PIPELINE_HARDENING §23 applies: `npm run deploy` + Cloudflare deployment id + live footer hash)

**B1 · New panel `app/(panels)/venues` "Partners"** in the sidebar next to Shop: table over `venues` with filters (category, partner, active, source, text search), columns name / category / partner / subtitle / price / chip / offer / lat,lon / radius / active / source / updated; row editor drawer with every A1 field + `gps_radius_m` + `image_url`; **"Find on map"**: paste a Google Maps link or lat,lon → the API's `/venue/geocode` (Places text search, server key) fills lat/lon; geohash is ALWAYS computed by the API on save (`_geohash_encode`, precision 9) — never typed. New rows get `source='admin'`. Deactivate, don't delete (the FK from `activities`). i18n via `lib/i18n.ts` DICT (en + ja). Uses the existing `/api/admin` auth pattern.

**B2 · Seed the demo spots (Cesar's call: "use the fake ones in the admin for now").** `2026_09_03_seed_demo_spots.sql`: the 4 driving ranges and 5 food spots from `rounds_map_tab.dart` (`_rangeSpots`, `_foodSpots` — name, subtitle, `pos` lat/lon, priceLabel, chipExtra, partner flag → `is_partner`, "ゴルファー10%OFF"-style text → `partner_offer`), `source='demo'`, `category` range/food, `gps_radius_m 300`. The 9 mock golf courses are NOT seeded — they are real courses already present from the OSM import; instead the seed marks 霞ヶ関カンツリー倶楽部 `is_partner=true` with `partner_offer='ゴルファー10%OFF'` so the PARTNER tag has one live example. Cesar applies the SQL; the panel then shows them editable.

## C · Unity

**C1 · Screen + ids.** `ScreenId.GpsRounds` (prefab `GpsRoundsScreen`, builder `GpsRoundsBuilder`), modals `CheckInConfirmModal`, `RoundCompleteModal` (`ModalController`, `animateShow = true`, `S_SU_ModalPanel` family). `GpsGate.GpsScreens` + `GpsGateTests` gain `GpsRounds`; `NavTitleKeyFor` → `GPS_ROUNDS_TITLE`; `GpsNavBarBinder` wires the ROUNDS slot to `ShowScreen(GpsRounds)` and marks it active on this screen; `GpsScreenTransition` direction table gains the slot.

**C2 · Services (`Golfin.Social` or `Golfin.Gps` — follow where `VenueService` lives).** `ActivityService`: `CheckIn(venueId, fix, key)`, `CheckOut(activityId, fix, checkCount, key)`, `Active()`, `History(skip, limit)`; DTOs in `ActivityDtos.cs` (all A3 fields). `VenueService.Nearby(prefixes, category, lat, lon)`; `VenueService.MapTile(lat, lon, zoom, w, h, onTexture)` → `Texture2D` via `UnityWebRequestTexture` through the `ApiClient` auth path; `VenueDto` gains the A1 columns.

**C3 · `RoundSession` (new, `Golfin.Gps`).** One active round per player, source of truth = `/activity/active` on screen entry and app resume, mirrored in PlayerPrefs (`gps_active_round`) so the card paints instantly. While active: `GpsSessionTracker.RecordFix` on every `LocationProvider` fix (foreground only — no background location mode; on resume, one fix is taken; the K4 threshold is 3 fixes so a normal round crosses it); elapsed ticks every second from `check_in_at` (server time); `GPS` stat = "● HIGH/MED/LOW" from `AccuracyM` (<15 / <50 / else) and `GPS FIXES` = the tracker count. Idempotency keys are minted per intent and persisted until the response lands (a force-quit mid-check-in retries the same key).

**C4 · Rounds screen behaviour (mirrors the Flutter tab, then the real calls).**
- Entry: request a fix (`LocationProvider`), compute the 9 prefixes (`Geohash.Neighbors` at `NearbyPrefixPrecision`), fetch `Nearby(category)`, paint list + map pins; shimmer on cold fetch (5 rows), paint-cache on warm. `NEARBY · N SPOTS` = row count. No fix → pill "● GPS OFF" #b7c3d3, rows still listed by the last known location or Tokyo Station, CHECK IN disabled.
- Chips switch category (list cross-fade, pins re-paint). Sort: distance asc (server order) — the "DISTANCE ▾" toggle flips to name asc; pinned as `GPS_ROUNDS_SORT_*` keys.
- Map: the panel's Map Surface shows `MapTile(lat, lon, zoom 13, 918, 420)` at 2× as a `RawImage`; pins are `Image`s placed by Web-Mercator projection of each spot vs the tile centre/zoom (one static helper `MapProjection`, EditMode-tested against three known points); the player dot likewise. Drag = pan (re-fetch on release, 250 ms debounce), pinch = zoom ±1 (13…16), NEAR ME re-centres. Tile fetch failure → the stylised placeholder from the frame (baked `S_GPS_MapFallback.png`) + attribution hidden.
- **CHECK IN** enabled only when the spot's `distance_m ≤ gps_radius_m`. Beyond it the button stays TAPPABLE but reads `GPS_ROUNDS_TOO_FAR` ("2.4 KM AWAY", ADark fill, white label) and a tap raises the existing `ToastController` toast `GPS_ROUNDS_TOO_FAR_TOAST` ("You need to be at {0} to check in — you're {1} km away"); with no GPS fix the tap toasts `GPS_ROUNDS_NO_GPS_TOAST` ("Turn on location to check in"). Cesar 2026-09-03: the player must always be TOLD why check-in is unavailable, never left with a dead button. Tap → Check-in Confirm modal (venue, distance, the +30/+10/accuracy stats from live values) → CONFIRM → `PendingSpend` → `CheckIn` → on `awarded` the Top-UI RP `CountUp`s +30, toast `GPS_ROUNDS_CHECKED_IN` ("Checked in at {0} (+30 pts)") → screen flips to the active state.
- **Active state:** chips hidden, Active Round Card at slot 1, list becomes NEARBY FOOD & DRINK (category `food`, then other golf spots — PLAYLIFE order), rows' button = DETAILS (opens `/venue/{id}` in the existing Venue detail treatment if one exists; else a read-only modal with name/subtitle/offer/price — NOTE which). SCORE UPLOAD → `ShowScreen(ScoreUpload)` with `ScoreUploadDraft` prefilled (venue, `activity_id`, GPS evidence from the tracker) so the GPS step shows the venue already verified; on Score Posted the round is closed by A5 and the Rounds screen returns to the list state. CHECK OUT → Round Complete modal → CHECK OUT confirmed → `CheckOut` → modal shows the server's elapsed / pts / fixes (POST SCORE = Score Upload prefilled as above but without the round now active — evidence still attached; DONE = back to list).
- Launch with an active round older than 8 h: the card shows "ROUND EXPIRED — check out to clear" and CHECK OUT calls the same RPC (server returns `expired`, 0 pts).
- MY RECENT ROUNDS (list state): `/activity/history` first page, rows = the hub's round-row atom (venue, date, trust, pts; score when the row has `screenshot_data.score`); ALL ROUNDS › opens the hub's existing rounds surface if one exists, else is hidden (NOTE which — the Rounds-tab-destination backlog row is closed either way).

**C5 · Localization** — Build rule 7. ~30 keys EN+JA: `GPS_ROUNDS_TITLE`, `_NEARBY_COUNT` ("NEARBY · {0} SPOTS"), `_GPS_ON/_GPS_OFF/_LIVE`, `_CHECKED_IN_SINCE` ("CHECKED IN · {0}〜"), `_CAT_GOLF/_CAT_RANGE/_CAT_FOOD`, `_NEAR_ME`, `_LEGEND_PARTNER/_REGISTERED/_FOOD`, `_SORT_NEAREST/_SORT_DISTANCE/_SORT_NAME`, `_NEAR_YOU`, `_NEARBY_FOOD`, `_PARTNER`, `_CHECK_IN`, `_TOO_FAR` ("{0} KM AWAY"), `_TOO_FAR_TOAST`, `_NO_GPS_TOAST`, `_DETAILS`, `_LIVE_ROUND`, `_SINCE` ("Since {0}"), `_ELAPSED/_PTS_EARNED/_GPS/_GPS_FIXES`, `_SCORE_UPLOAD`, `_CHECK_OUT`, `_CONFIRM_TITLE` ("CHECK IN HERE?"), `_CONFIRM_SUB` ("{0} km away · inside the course radius"), `_PTS_ON_CHECKIN/_PTS_ON_CHECKOUT/_GPS_ACCURACY`, `_CONFIRM_NOTE`, `_CANCEL`, `_COMPLETE_TITLE` ("ROUND COMPLETE"), `_COMPLETE_SUB` ("{0} – {1} · GPS verified" / "… · GPS unverified"), `_COMPLETE_NOTE`, `_POST_SCORE`, `_DONE`, `_EXPIRED`, `_CHECKED_IN_TOAST`, `_ALREADY_ACTIVE`, `_MY_RECENT_ROUNDS`, `_ALL_ROUNDS`. Importer PLAN → APPLY → publish `texts` → `--check` clean. Admin strings in `lib/i18n.ts`.

## Decisions baked in (flag in the report if you deviate)

- **D1 · Check-in requires being inside the venue radius.** PLAYLIFE let you check in anywhere and only *labelled* GPS; here the button is disabled beyond `gps_radius_m` and the RPC verifies server-side regardless. Cheaper than a trust penalty, and it makes "+30 on check-in" honest.
- **D2 · One active round per player**, server-enforced (`already_active`).
- **D3 · Foreground GPS trail only.** No background location entitlement in this task (App Store review cost); K4 needs 3 fixes, which a foreground round with a resume fix reaches.
- **D4 · Points = PLAYLIFE's** (+30 check-in if verified; +10 +5 on checkout; screenshot +50/+5 on the score path as today), moved through the invariant-safe RPCs only.
- **D5 · Map = Static Maps proxy + client pins**, not an SDK (reverses list-only v1; Cesar: "I assume the map will be a real map"). Pan/zoom by re-fetch.
- **D6 · Score posted from a round updates the round's row** (A5) — one activity per round.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Per-element A/B crops vs the four renders for every fidelity row; ΔRGB table (panels, chips both states, pills, card stroke, modal shells).
- [ ] Geometry JSON + invariants + lint `fail=0` for `GpsRoundsScreen` (both states), both modals; spec.json covers chips/pins/card/stats/buttons.
- [ ] Migration reviewed by the Architect and applied by Cesar BEFORE deploy; `e2e_activity_economy.py` ALL PASS quoted (check-in +30 once, replay, `already_active`, checkout +15, expired 0, invariant 0 violations before/after, score submit with `activity_id` = one row).
- [ ] Static Maps proxy: one live 200 quoted with the cache hit on the second call; the client renders the tile (screenshot) and the pin projection test passes (3 known points, ≤ 2 px).
- [ ] Editor play-mode, real navigation, live API, with the Editor's location mocked at TEST Office (1993 — `LocationProvider` gets an Editor override; NOTE how): list paints (shimmer cold / cache warm), chips switch, CHECK IN disabled for far spots and enabled for 1993, confirm → +30 (`/points/balance` before/after quoted, ledger row), active card ticks, SCORE UPLOAD lands with the venue prefilled, CHECK OUT → +15 → list state; `/activity/history` shows one row. Screenshots of every state in `screenshots/`.
- [ ] Second check-in while active → `already_active` handled (toast, no crash); force-quit mid-check-in → same key replayed, no double award (quote).
- [ ] Admin Partners panel deployed (§23: `npm run deploy` id + footer hash quoted); create a range, edit the seeded 焼肉 GREEN offer, deactivate one row → the client's next fetch reflects all three (quote the JSON).
- [ ] Demo seed applied; the FOOD & DRINK chip lists 5 rows, DRIVING RANGES 4, 霞ヶ関 shows PARTNER.
- [ ] `GpsGate` includes `GpsRounds` (EditMode); ROUNDS nav slot active-state on this screen; push direction pinned.
- [ ] Importer PLAN/APPLY/publish/`--check` clean; zero hardcoded `.text` literals (grep).
- [ ] Full EditMode sweep green; new suites executed by name (`MapProjectionTests`, `RoundSessionTests`, `ActivityServiceJsonTests`).
- [ ] **Motion parity with `gps_polish`**: `gps_polish_invariants.json` re-run with GpsRounds in the transition table (`fail=0`), rest-state parity 0 px for GpsRounds (both states), a captioned video of list → chip switch → check-in modal → active card → check-out modal → list showing every motion above, and the A13 GC/frame measurement on this screen.
- [ ] Too-far and no-GPS taps raise the toasts (log + frame each).
- [ ] Deviations flagged; **on-device rows added to `Docs/GPS/GPS_DEVICE_PASS.md` §3** (real check-in at the office and at home, background/resume fix behaviour, the map on glass).

## Files / hierarchy this task touches

- Unity NEW: `Assets/Scripts/UI/Gps/GpsRoundsScreenController.cs`, `RoundSpotRowView.cs`, `CheckInConfirmModalController.cs`, `RoundCompleteModalController.cs`, `Assets/Scripts/UI/Gps/Editor/GpsRoundsBuilder.cs`, `Assets/Scripts/Gps/RoundSession.cs`, `MapProjection.cs`, `Assets/Scripts/Social/ActivityService.cs` + `ActivityDtos.cs` (or `Golfin.Gps` — follow `VenueService`), prefabs `GpsRoundsScreen`, `CheckInConfirmModal`, `RoundCompleteModal`, `Assets/Art/UI/Gps/S_GPS_MapFallback.png` (+ baker), tests.
- Unity touched: `ScreenManager`, `PersistentUIManager`, `GpsGate` + tests, `GpsNavBarBinder`, `GpsScreenTransition` (slot order), `VenueService` + `GpsDtos`, `ScoreUploadDraft` / `ScoreUploadFlowController` (prefill + `activity_id`), `LocalizationText.csv`.
- playlife: `migrations/2026_09_03_venue_partners.sql`, `2026_09_03_seed_demo_spots.sql`, `routers/activity.py`, `routers/venue.py` (+ `/map`, `/geocode`), `routers/score.py` (A5), `e2e_activity_economy.py`.
- admin-dashboard: `app/(panels)/venues/*`, sidebar entry, `lib/i18n.ts`, API routes.

## Smoke evidence

Play-mode video of list → check-in → active → score upload prefilled → check-out (captioned, Rule 17 idiom) + stills per state; the E2E transcript; the admin panel before/after crops; the projection test output.

## Out of scope (do NOT do these)

- Background location / "Always" permission; Android.
- Venue photos, ratings, opening hours, partner coupons/redemption (`partner_offer` is display text only).
- Rounds "ALL ROUNDS" full history screen if none exists (hide the link, backlog row stays).
- Public profile taps, follow, feed.
- Flutter changes (its readers keep working because A1 is additive and A3 mints keys server-side).
- Any change to `/points/earn` beyond what A3 replaces for `gps_checkin`.
