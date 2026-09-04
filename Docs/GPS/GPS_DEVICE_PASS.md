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

### 1b · Once per ACCOUNT, not per device (`gps_profile_prompt_server_flag`)

The Golf Profile offer is recorded on the account (`profiles.golf_profile_prompted_at`), so
completing OR skipping it anywhere means nowhere else ever offers it again. These rows need **two
installs** — the game and the standalone GOLFIN GPS app, side by side — and cannot be checked in
the Editor. Do 1b.1 and 1b.2 in whichever order you have the installs for; they are the same claim
in both directions.

| # | Check | Expect |
|---|---|---|
| 1b.1 | Answer the Golf Profile in the **GAME** (SAVE or "Skip for now"), then install the **standalone** and sign in with the same account | the shell boots **straight to the hub**. The Golf Profile is NOT offered. Expect a brief hold (~0.2 s) on the first launch only, while it asks the server — the second launch is instant because the answer is cached locally |
| 1b.2 | The reverse: answer it in the **standalone**, then open the **game** and tap the GPS pill | the hub, no capture. "Skip for now" counts as answering — that is the half that used to leave no trace at all |
| 1b.3 | A genuinely NEW account, in whichever app you open first | offered **exactly once**, in that app; the other app never offers it |
| 1b.4 | Airplane mode, on a device that has already answered | still no offer (the local cache covers it; the server is never consulted) |
| 1b.5 | Airplane mode, on a **fresh install** that has never answered | no offer, no hang — it gives up after ~2.5 s and goes to the hub. Turn the network back on, relaunch: now it decides properly |

## 2 · Navigation and motion (the gamble, on glass)

| # | Check | Expect |
|---|---|---|
| 2.0 | On the hub, look at the **bottom nav bar** | it sits ON the bottom edge, full screen width, and the tray is NOT stretched — same proportions as the Game bar. Both bars now carry the identical rule (anchors 0,0-1,0, height 196, slots on fractional anchors), so flip between GAME and GPS and the bar should look like the same object. The home indicator overlays its lower band; that is what Game ships too, and matching Game was the point |
| 2.0b | Walk Hub -> Score Upload -> Gift -> Profile -> Badges | exactly ONE slot cyan per screen and the other four white — Home, Camera, Gift, Profile, Profile. Vote lights nothing (not a nav destination); Rounds lights on the Rounds tab. Glyph AND ring take the tint — GPS slots are a single sprite, unlike Game's glyph-in-a-ring; flag it if the teal ring reads wrong on glass |
| 2.0c | Compare the **top bar** between GAME and GPS | it is literally the same GameObject (PersistentUI/TopBar), so it cannot differ. Worth one look anyway on a Dynamic Island phone: the top bar BACKGROUND should reach the physical top edge while its CONTENT (R points, ticket, gear) sits ~36 px lower than on an iPhone 14 — background full-bleed, content inset, baseline 141 |
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

## 3b · Rounds tab — real check-in (gps_checkin; do this twice: at the office, at home)

> Added 2026-09-03 by `gps_checkin`. This is the row set the Editor genuinely cannot cover: a real
> fix with real accuracy, the radius decided against it server-side, the app being backgrounded
> mid-round, and a raster map on glass. Everything else on the Rounds tab was verified in the
> Editor with the location mocked at **1993 TEST Office**.
>
> **Pre-req (Cesar, once):** both migrations applied (`2026_09_03_venue_partners.sql`, then
> `2026_09_03_seed_demo_spots.sql`), the API deployed, and **"Maps Static API" enabled** on the
> Google key `playlife-api` uses. Without the last one `/venue/map` answers 502 and every row
> below still works — the panel just shows the stylised placeholder with no attribution, which is
> row 3b.4's "fallback" outcome rather than a defect.

| # | Check | Expect |
|---|---|---|
| 3b.1 | Hub → **ROUNDS** nav slot (second from the left) | the Rounds tab, arriving with the layered push; the ROUNDS slot lit; top bar reads **ROUNDS** |
| 3b.2 | Cold open (kill the app first) | shimmer on NEAR YOU while the fix + fetch run, then rows stagger in; status row reads `NEARBY · N SPOTS` and the pill `● GPS ON` |
| 3b.3 | Deny location (Settings → Golfin → Location → Never), re-enter | pill `● GPS OFF` in grey, rows still listed, every CHECK IN dark. **Tap one** → toast "Turn on location to check in". No dead button anywhere |
| 3b.4 | The map panel | a REAL dark road map centred on you, a blue you-are-here dot, coloured pins that line up with the spot list, `Map · Google` bottom-right. If the Static Maps key is not enabled: the stylised placeholder and NO attribution — note which you got |
| 3b.5 | Drag the map, then **◎ NEAR ME** | the tile re-fetches after the drag settles (~¼ s) and the pins move WITH it, not after it; NEAR ME snaps back to you |
| 3b.6 | Chips: DRIVING RANGES → FOOD & DRINK → GOLF COURSES | the list cross-fades and the pins re-colour each time. FOOD & DRINK lists the 5 demo spots, DRIVING RANGES 4 |
| 3b.7 | **At the office**: 霞ヶ関 or any far course row | button reads `N KM AWAY`, dark. Tap → toast "You need to be at … — you're N km away" |
| 3b.8 | **At the office**: the TEST Office row | gold **CHECK IN**. Tap → the confirm modal pops; sub-line says "…km away · inside the course radius"; the three stats show +30 / +10 / `● HIGH` |
| 3b.9 | CHECK IN → confirm | `…` on the button, then the modal closes, RP **counts up +30** in the Top UI, toast "Checked in at … (+30 pts)", and the screen flips: chips gone, gold **LIVE ROUND** card in their place, list retitled **NEARBY FOOD & DRINK** |
| 3b.10 | Watch the card for ~90 s | ELAPSED ticks each minute with NO layout jitter; GPS shows `● HIGH`; GPS FIXES ≥ 1 |
| 3b.11 | Background the app for 10+ min, come back | the round is still there, elapsed correct against the wall clock (not restarted), GPS FIXES has gone up. **This is the D3 foreground-trail row** — there is no background location entitlement, so the fix is taken on resume |
| 3b.12 | Force-quit mid-check-in (start a check-in on a bad connection, kill the app, relaunch, retry) | ONE round, +30 paid ONCE. `/points/balance` before and after must differ by exactly 30 |
| 3b.13 | With a round live, tap another spot's **DETAILS** | a toast with that spot's offer/price. (NOTE: there is no venue-detail screen in the project — see the report's deviation D-3) |
| 3b.14 | **SCORE UPLOAD** from the card | the Score Upload flow opens with the venue ALREADY chosen and GPS-verified — no venue picker step |
| 3b.15 | Post the score | Score Posted as usual, and back on Rounds the live card is GONE. `/activity/history` shows **ONE** row for the round, not two |
| 3b.16 | Start a second round, then **CHECK OUT** | the Round Complete modal pops as a confirmation; confirm → it becomes a receipt with the SERVER's elapsed / `+15` / fix count; RP counts up +15; back on the list state |
| 3b.17 | **At home**: repeat 3b.8–3b.9 against **1992 TEST Home** | same behaviour; TEST Office now reads `4.x KM AWAY` |
| 3b.18 | Leave a round open overnight, check out the next day | card reads **ROUND EXPIRED**; CHECK OUT closes it and pays **0** — and says so rather than promising points |
| 3b.19 | Japanese (Settings → 日本語, then re-enter Rounds) | every chip, panel title, button, toast and both modals in Japanese; no raw `GPS_ROUNDS_*` keys anywhere |
| 3b.20 | Feel at 60 fps: the list↔card flip, the modal pops, the map fade | note any hitch — the first push after launch is the GC warm-up frame |

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

## 7 · PLAYLIFE standalone shell ("punch it standalone") — a separate app, installed beside the game

Built from the same commit by `./Tools/testflight.sh testflight_build_standalone`. It arrives in
TestFlight under the **GOLFIN GPS** app record (Apple ID 6737145432), not under GOLFIN — a
different tester invite, a different app on the springboard. Install it **without removing the
game**: two apps side by side is the state that has to work.

| # | Check | Expect |
|---|---|---|
| 7.1 | Install beside the game; look at the springboard | TWO icons: **Golfin** (the game) and the **GPS/PLAYLIFE** placeholder, name "GOLFIN GPS". If you cannot tell them apart, that is a defect — the icon is the only tell |
| 7.2 | Launch, tap the START/LOGIN gate, sign in with an account that has already answered the Golf Profile | lands straight on the **GPS hub**. No Home screen at any point, no starter-character picker, no wait on an inventory fetch |
| 7.3 | Fresh account (or Settings → log out → sign up) | Login → Create Username → **Golf Profile** → **Welcome** → hub. SKIP on Welcome lands on the **hub**, never on a blank screen |
| 7.4 | Look at the top and bottom of the hub | Top bar: RP pill, username, Settings gear — **present**. Ticket count + `+` button — **absent**. The game's five-slot bottom nav — **absent** (the hub draws its own). The hub's own BackPill — **absent** |
| 7.5 | Walk the whole surface: hub → Rounds → check in → Score Upload → Gift → Vote → Profile → Avatar → Badges | every screen identical to the GPS build; nothing golf-shaped is reachable from anywhere |
| 7.6 | Settings gear | Account (display name, log out), Language, About and the legal links only. **No Graphics tier, no Sound settings** |
| 7.7 | Android/iOS back gesture from the hub | nothing happens (the hub is the root) — never a quit, never a blank screen |
| 7.8 | Safari → type `golfingps://gps` → Open | opens the shell on the hub. With the shell **not** installed, Safari says it cannot open the address — it must NOT open the game |
| 7.9 | Check in / upload a score from the shell, then open the admin dashboard | the row's `client_platform` reads **`ios-playlife`** (the game's rows still read `ios`) |
| 7.10 | Play the GAME app once, same phone, same account | unchanged: Home, bottom nav, tickets, golf. The shell's existence must be invisible to it |

## 8 · Known, not defects (don't report these)

- Rubik Medium renders ~5 % narrow (variable face) — backlog, one font import later.
- The standalone shell's icon and launch screen are a generated PLACEHOLDER
  (`Docs/Scripts/make_standalone_icon.py`), not Ken's branding — backlog row.
- `PUT /user/update` can't clear a field to NULL — matters only for the future Settings edit screen.
- Only YES can be cast (single VOTE button in the design) — backlog.
- Top Supporters shows "— followers" — no follower counts in the sources yet.

## 9 · After the pass

- Send the row-by-row result to the Architect (chat). Defects become quick specs, ordered by you.
- `delete from venues where source = 'test_fixture';` — after deleting any test rounds that reference 1992/1993 (reminder set for 9 Sep 10:00 JST).
- `gps_standalone_shell` (Unity thin-shell) is BUILT — its rows are §7 above. Real PLAYLIFE
  branding (icon/launch/wordmark from Ken) is still a backlog row; §7.1 is checking a placeholder.
