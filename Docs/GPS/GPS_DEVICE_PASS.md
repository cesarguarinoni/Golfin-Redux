# GPS on-device pass — "Punch it GPS" build (Architect checklist, 2026-09-03)

> The first time the whole GPS surface runs on a phone. Everything below was verified in the
> Editor by the pipeline; this pass is for what the Editor cannot show — real GPS, the real
> keyboard, real permissions, TestFlight install state, Japanese on a device, and feel.
> Report findings per row: **OK / DEFECT (what you saw) / SKIPPED**. Defects go to the Architect,
> who turns them into quick specs; nothing is fixed ad hoc during the pass.

## 0 · Before you build

| # | Step | Why |
|---|---|---|
| 0.1 | `git status` clean on `main` at or after `5506d2c67` (`gps_polish: DONE`) | the pass must be on the archived state |
| 0.2 | Test venues live: `select id, name, source from venues where source='test_fixture';` → **1992 TEST Home (Higashikanda)**, **1993 TEST Office (WeWork Harumi)**, radius 500 m each | Score Upload's GPS step needs a venue within 500 m of where you stand |
| 0.3 | Two seeded `GOLFIN AI` votes remain uncast on prod (`e47a04bc…` and `541bcde9…` were burned by the pipeline) | they are your Vote-tab fixtures; do not create more |
| 0.4 | Second account ready and signed in on another device or the Flutter app: `cesar.guarinoni@gmail.com` is the dev account (RP ≈ 6 9xx); `…@wonderwall-g.com` is the other | needed for the 409 duplicate-nickname path and to receive a gift |
| 0.5 | Build: `./Tools/testflight.sh testflight_build_gps` → TestFlight. Note the build number. | the GPS variant (pill visible, GPS screens open) |
| 0.6 | On the phone: **DELETE** the app (not Offload) before installing the new build | resets `gps_profile_prompted` and the starter-restore gate — you must see the Golf Profile prompt exactly once, on your first GPS entry (row 1.3) |

## 1 · Install and first launch (iPhone 15 Pro Max, English first)

| # | Check | Expect |
|---|---|---|
| 1.1 | Fresh install → Login with the dev account | no starter-character prompt (starter_restore_gate), Home |
| 1.2 | Home | **GPS pill** top-right, gold capsule "GPS", podium moved left of the username ridge, the home_promo banner still shows |
| 1.3 | Sit on Home for ~10 s, then tap the **GPS pill** (gps_profile_prompt_on_entry — the trigger is the first entry into GPS, NOT Home) | Home comes up and **STAYS** — nothing offered on arrival, no flash of another screen. The pill tap then lands on the **Golf Profile** screen, once. Nickname prefilled with the account's display name, swatch initials show the first letter |
| 1.4 | Golf Profile: tap the **handicap** field | numeric keypad; field scrolls above the keyboard (`R6`); "e.g. 18.4" hint; `abc` cannot be typed |
| 1.5 | Golf Profile: set nickname to the OTHER account's display name → SAVE | red `AUTH_USERNAME_TAKEN` message, no crash, still on the screen |
| 1.6 | Fix nickname, pick PINK + ADVANCED + handicap 18.4 → SAVE | Welcome tutorial; Profile later shows the pink hero disc |
| 1.7 | Welcome → GET STARTED | lands on the GPS hub with the **push** (content slides, background/nav bar static) |
| 1.8 | Kill the app, relaunch, sit on Home, tap the pill again | Home stays Home; the pill goes **straight to the hub** — Golf Profile NOT offered again |
| 1.9 | Still on that launch: tap the **home_promo banner** (its `golfin://gps` route) | the hub, no second offer. On a fresh install with the flag unset, this same tap is the OTHER way into the one-time Golf Profile offer — the intercept is in `ScreenManager.Navigate`, not on the pill |

## 2 · Navigation and motion (the gamble, on glass)

| # | Check | Expect |
|---|---|---|
| 2.0 | On the hub, look at the **bottom nav bar** (gps_navbar_bottom_anchor) | it sits ON the bottom of the screen, not floating above it. The bar GREW downward by the home-indicator inset — the icon row did NOT move up, and the camera badge still overhangs the top edge by the same amount as in the Editor. This row is the whole reason the fix needs a phone: `Screen.safeArea.y` is 0 in the Editor, so the growth is unobservable there |
| 2.0b | Now walk Hub → Score Upload → Gift → Profile → Badges (gps_navbar_selected_tab) | on each screen exactly ONE slot is cyan and the other four are white — Home, Camera, Gift, Profile, Profile. Vote lights **nothing** (it is not a nav destination) and Rounds never lights until `gps_checkin` gives it a screen. Both the glyph AND its ring take the tint — GPS slots are a single sprite, unlike Game's glyph-in-a-ring; flag it if the teal ring reads wrong on glass |
| 2.1 | Home → pill → hub | fade-to-black (boundary), then content rises 16 px |
| 2.2 | Hub → Profile → Badges → back → back | layered push both ways, ~0.25 s, no seam, no flash; nav bar reads static |
| 2.3 | Nav-bar sweep: Score Upload → Gift → Vote → Profile → hub | push direction follows slot order; Score Upload arrives by fade (by design) |
| 2.4 | Hub → Home via the back pill | fade-to-black |
| 2.5 | Rapid double-tap two nav slots | second push starts cleanly; no stuck half-slid content |
| 2.6 | Cold open of hub / Gift / Vote (kill app, relaunch, or airplane mode off→on) | **shimmer** bars on My Recent Rounds, Top Supporters/Popular Golfers, vote list; replaced by rows with a stagger. Warm re-entry: no shimmer |
| 2.7 | Feel at 60 fps? any hitch on the first push after launch? | note it — that is the GC warm-up frame |

## 3 · Score Upload with real GPS (do this twice: at the office, at home)

| # | Check | Expect |
|---|---|---|
| 3.1 | Camera CTA → OS camera permission prompt | copy: "GOLFIN uses the camera to photograph your scorecard." |
| 3.2 | Photograph a real scorecard (or a printed one) → Reading | reading strip slides in; AI result fills holes; total from holes note |
| 3.3 | Edit step: change a hole | total recomputes; 9-hole toggle greys 10–18 |
| 3.4 | GPS step → OS location permission prompt | copy: "GOLFIN uses your location to verify rounds at the golf…" — check it isn't truncated in the OS sheet |
| 3.5 | GPS step at the OFFICE | venue picker lists **TEST Office (WeWork Harumi)** (and TEST Home ~4.6 km away); pick Office; distance shown < 500 m; trust signals attached |
| 3.6 | Confirm → POST | `…` pending capsule on the button, then Score Posted with the total **Pop**, `+20 pts`, RP count-up in the Top UI |
| 3.7 | Hub → My Recent Rounds | the round is there with the venue name; Profile shows the updated best/handicap |
| 3.8 | Repeat 3.5 at HOME | TEST Home within 500 m; Office listed but far |
| 3.9 | Post a round WITHOUT granting location (deny the prompt on a re-install, or toggle off in Settings) | still submits, lower trust — no dead end |

## 4 · Gift and Vote (real economy — small amounts)

| # | Check | Expect |
|---|---|---|
| 4.1 | Gift → Popular Golfers → SEND GIFT to the other account, **50** | `…` pending, balance −50 with count-up; on the other account: GIFTS RECEIVED +50, Top Supporters shows you |
| 4.2 | Send 50 again, then force-quit mid-send and retry | never a double debit (idempotency) |
| 4.3 | Buy the cheapest gift item (グローブ 30) | RP −30, no second row on a retry |
| 4.4 | Vote → one of the two remaining seeded votes → VOTE | bar fills to 100 % YES, `+10` once, RP count-up, button stays disabled after relaunch |
| 4.5 | Vote → MINE filter | only your created votes; CREATE modal pops, keyboard avoidance on its fields (`R6`) |
| 4.6 | Vote → the last seeded vote: leave it | keep one for Ken |

## 5 · Japanese

| # | Check | Expect |
|---|---|---|
| 5.1 | iOS language → 日本語, relaunch, walk Golf Profile / Welcome / hub / Gift / Vote / Score Upload | every string localized (no raw `GPS_*` keys); "ハンディキャップ（任意）" fits the 878 px label; chip labels don't wrap; Welcome sub wraps acceptably |

## 6 · "Punch it" (no-GPS) build — 10 minutes, optional but cheap

| # | Check | Expect |
|---|---|---|
| 6.1 | `./Tools/testflight.sh testflight_build` (no `_gps`) | pill absent; the home_promo banner shows; tapping it does nothing (GpsGate refusal — backlog row: retarget the promo for no-GPS audiences) |

## 7 · Known, not defects (don't report these)

- Rubik Medium renders ~5 % narrow (variable face) — backlog, one font import later.
- `PUT /user/update` can't clear a field to NULL — matters only for the future Settings edit screen.
- Only YES can be cast (single VOTE button in the design) — backlog.
- Top Supporters shows "— followers" — no follower counts in the sources yet.

## 8 · After the pass

- Send the row-by-row result to the Architect (chat). Defects become quick specs, ordered by you.
- `delete from venues where source = 'test_fixture';` — after deleting any test rounds that reference 1992/1993 (reminder set for 9 Sep 10:00 JST).
- Then: `gps_standalone_shell` (Unity thin-shell) is the next spec.
