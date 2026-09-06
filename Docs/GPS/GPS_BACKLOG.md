# GPS / PLAYLIFE — deferred-scope backlog

> Everything consciously left OUT of the 2026-09 GPS build, in one place, with where it was
> deferred and what it needs. Maintained by the Architect — every future GPS spec that defers
> something adds a row here in the same session. Last updated: 2026-09-05 (golfer_3d_test deferrals added).

## Player-visible promises (highest priority — the UI already implies them)

| Item | Implied by | Needs |
|---|---|---|
| **Settings: edit golf profile** (nickname, colour, experience, handicap) | Golf Profile copy: "You can change all of this later in Settings" (`auth_golf_profile`) | A Settings entry or GPS-profile edit screen reusing the same panel + `PUT /user/update`. NOTE: `PUT /user/update` cannot clear a field to NULL (omitted = preserved) — un-setting a handicap needs an explicit-null contract or a clear endpoint (auth_golf_profile deviation 7) |
| **Rounds tab destination** | TAKEN UP by `gps_checkin` (2026-09-03): the Rounds tab is the Check-in screen and carries MY RECENT ROUNDS | Remaining: a full "ALL ROUNDS" history screen if none exists at build time |
| **SEE ALL ›** on Top Supporters / Popular Golfers | Gift screen headers (hidden in v1) | Full-list screens or modals; supporters wants the backend aggregation below |
| **Share row on Score Posted** | Score Upload step 6 pill strip (static) | iOS share sheet (native activity VC) — small task |

## Economy / backend

| Item | Deferred in | Notes |
|---|---|---|
| `vote_hit` resolution flow (+30) | `gps_gifts_votes` | NO server mechanism resolves a vote as "hit" — needs an outcome-marking path (creator? admin? score-linked) before the reward can exist |
| Vote pools/odds ("500 pts" pill concept) | `gps_gifts_votes` | Mockup concept with no backend; v1 pill shows the real +10 cast reward |
| `/gifts/supporters` aggregation endpoint | `gps_gifts_votes` | v1 aggregates client-side from `/gifts/received` + `/points/history` (4 pages max); replace when volumes grow |
| Item gifts (`/gifts/send`), IAP gifting + 30/20/50 revenue split | `gps_gifts_votes` | Big: IAP work, receipts, the split bookkeeping. Note the activity-pts item path awards the ITEM but 0 gift_pts by design |
| Inventory / equip UI (cosmetics) | `gps_gifts_votes` | Ties into the avatar↔cosmetic mapping design session (decision 2026-09-01, still owed) |
| `/vote/cast` non-atomic counters | `gps_gifts_votes` | Tolerated at current volume; atomicize if votes get traffic |
| **Vote NO path** | `gps_gifts_votes` deviation 8 | The node has ONE gold VOTE button, so v1 always casts the first (YES) option. Needs a YES/NO control design before a NO can be cast |
| `/points/earn?action=vote_cast` is unkeyed | `gps_gifts_votes` deviation 10 | Only reachable from the successful-cast branch today; a keyed earn action (idempotency like `golfin_gift_pts`) is a server change |
| Top Supporters merges `/gifts/received` + `/points/history?currency=gift` by name | `gps_gifts_votes` deviation 2 | RP gifts exist only as ledger rows; `SenderId` null for RP supporters, follower counts shown as em dash. The `/gifts/supporters` aggregation endpoint above closes all three |
| Two seeded test votes still uncast on prod | `gps_gifts_votes` / `gps_polish` | `e47a04bc` and `541bcde9` were burned by the pipeline; the device pass uses one, keep one for Ken, then prune all `GOLFIN AI` rows before real users see the Vote tab |
| AI vote generator (`/vote-generator/generate`) | never wired | Backend exists (Claude API); nothing schedules it — decide if daily auto-votes ship |
| PLAYLIFE `feed_items` | untouched | Gifts/votes write feed rows nobody in the game reads; a feed/notifications surface is unspecced |

## Social

| Item | Deferred in | Notes |
|---|---|---|
| Follow / followers actions in game UI | `gps_gifts_votes` | Backend complete (`/followers/*`, counts); no game UI. TRENDING/FRIENDS vote filters depend on it |
| Vote photo upload + stories behaviour | `gps_gifts_votes` | Static in v1; needs storage + votes schema change |
| Public profile view (`/user/{id}/profile`) | — | Tapping a supporter/golfer row goes nowhere in v1 |

## Platform / app

| Item | Deferred in | Notes |
|---|---|---|
| Background GPS trail during a round | `gps_checkin` D3 | Foreground-only in v1 (no "Always" entitlement); add the background location mode + entitlement if K4 counts prove too low on device |
| Partner offers redemption / coupons | `gps_checkin` | `partner_offer` is display text only; redemption flow, QR, partner reporting are unspecced |
| Unity Recorder hard-locks the Mac on the Rounds screen | `gps_checkin` (KNOWN_ISSUE_recorder_lockup.md) | Tooling only, video waived by Cesar; the encoder + Rounds screen together stall below the app (no crash report). Investigate with a smaller GameView / software encoder / capture outside Unity before the next Rounds video |
| Standalone launch image / brand assets from Ken | `gps_standalone_shell` | Icon supplied by Cesar 2026-09-03; launch screen still the GPS Splash background |
| Android GPS build variants | `punch_it_gps_variants` | One `Android-Full-GPS` profile clone + lane when Android builds resume |
| Android mock-GPS detection plugin | decision 2026-09-01 | `IMockLocationDetector` seam exists; iOS first |
| In-app build-variant watermark | `punch_it_gps_variants` | The Home **GPS pill** is the tell since `gps_pill_entry` (banner restored to plain admin behaviour); revisit only if Cesar asks |
| Home promo banner still deep-links `golfin://gps` on no-GPS builds | `gps_pill_entry` | Banner shows on both variants again; on a "Punch it" build the tap hits GpsGate's refusal. Unpublish/retarget the `home_promo` row for no-GPS audiences, or gate the route in the admin |
| Rubik Medium TMP asset | `auth_golf_profile` | `Rubik:Medium` resolves to the variable face — Medium runs render ~5% narrow, Welcome sub wraps one word late. Import a real Medium SDF asset once, fixes every GPS screen |
| Real map view (venues/courses) | decision 2026-09-01 | v1 is list-only via geohash prefixes |
| Standalone PLAYLIFE shell | DECIDED 2026-09-02 | Unity thin-shell, Flutter retired (one codebase). Spec `gps_standalone_shell` after `gps_gifts_votes` + `gps_polish` |
| Haptics (game + GPS) with Settings on/off toggle | `gps_polish` map, 2026-09-02 | Cesar: not yet — must land in the game AND GPS together, with a toggle. Notion Order 2130 `haptics_option` |
| Avatar photo upload (`/user/avatar`) | `auth_golf_profile` | Endpoint exists; game uses character art + colour instead |
| Golf-profile prompt is per-device | TAKEN UP by `gps_profile_prompt_server_flag` (2026-09-03) | — |
| Badge names JA-only in seeds | `gps_profile_pack` | EN localization of the 24 badge names rides the existing keys; verify EN column quality |
| bioKey/nameKey localization wiring | pre-GPS | Long-standing partial wiring, unrelated to GPS but adjacent |

## Shared with the GAME polish track (Architect, game_polish session — 2026-09-03)

| Item | Deferred in | Notes |
|---|---|---|
| `UiMotion` gains an optional `Ease` parameter (Pop / Tween / Slide; default = today's ease-out cubic) | `game_polish_b` (Cesar decision 2026-09-03) | GPS call sites unchanged; `UiMotionTests` easing-endpoint cases must still pass with the default. The gacha reveal's ease-out-back is the consumer. GPS session: no action, FYI. |
| Bottom-nav SELECTED state changes on both bars: cyan tint → gold halo + `#FCF195` ring overlay via a shared `NavSlotHighlight` (`Assets/Scripts/UI/Polish/`) | `game_polish_a` §D7 (Cesar 2026-09-03: "this should also be done in GPS when changed") | The game task edits `GpsNavBarHighlight.cs` (stops reading `iconActiveColor`, calls `NavSlotHighlight.SetSelected`) and adds the Glow/Ring children to the GPS bar through the builder hook — the ONE authorised game-track touch under `Gps/`. GPS session: do not re-tint; if `gps_checkin`'s Rounds slot lands first, its selected state comes for free once §D7 ships. |
| `GpsPaintMotion.cs` (`PaintGate`, `PanelReveal`, `StaggerRise`, `Shimmer`) and `ShimmerHost.cs` `git mv`'d to `Assets/Scripts/UI/Polish/` — GUIDs and the `Golfin.Gps.UI` namespace KEPT | `game_polish_b` §D0 | Zero GPS source edits; the classes now sit in `Polish/` under a GPS namespace. The rename to `Golfin.UI.Polish` (touches every GPS caller) is the GPS session's, whenever convenient. |
| `ShimmerBlock.prefab` moves `Assets/Prefabs/UI/Gps/` → `Assets/Prefabs/UI/Common/` | `game_polish_b` (Cesar decision 2026-09-03) | ONE line in `GpsPolishBuilder` (the prefab path constant) changes — the only GPS-folder touch the game track will make; GUID preserved by `git mv`, so scene/prefab references survive. GPS session: expect that diff. |
| Standalone shell still compiles the whole golf codebase (`UnityFramework` is byte-identical between `Golfin.ipa` and `GOLFINGPS.ipa`, 110.8 MB uncompressed — IL2CPP stripping removes nothing because every screen is reachable from `ScreenManager`). Carving it out = `defineConstraints: ["!GOLFIN_STANDALONE"]` on golf asmdefs + splitting the 6.7 MB `Assembly-CSharp` (ScreenManager, golf screens) behind interfaces. NOT `managedStrippingLevel: High` (silent UnityEvent/JSON/reflection breakage). Expected gain ~4–7 MB of download — parked until the store size hurts. | `gps_standalone_shell` round 2 (Architect 2026-09-04) | Loud path only (compile errors), never the silent one |
| Standalone still ships the nine `Assets/Skybox/*.hdr` (8.4 MB) although ShellScene uses `Default-Skybox`, plus `Fonts/NotoSansJP-VariableFont_wght.ttf` (8.7 MB, dynamic TMP atlas) and 12 × 920 KB `Resources/Characters/Homescreen/*.png` (all selectable on the Avatar screen — legitimate) | `gps_standalone_shell` round 2 report (2026-09-04) | Chase the skybox reference; the font is `build_size_diet` Phase 4 and lands in the shell for free |
| `testflight_build_standalone` uploads but the build is not offered to In-House Testers automatically on the GOLFIN GPS record (Cesar adds it by hand each time); the Fastfile comment assumes internal groups auto-distribute | Cesar 2026-09-04 | Fix = the group's "Enable automatic distribution" toggle in ASC (per record), or `groups:` + a processing wait in the lane |
| `Golfin.ipa` FILE under 350 MB: only reachable by not packing `Symbols/` into the .ipa (they zip to 127 MB; the dSYM zip already sits beside the .ipa in `Builds/ipa/`) — a fastlane/Xcode export option, not an asset change. Also parked: switching the iOS lane to `CompressWithLz4HC` if `build_size_diet` Phase 0b's numbers justify it (Cesar's call from the measurement). | `build_size_diet` (Architect 2026-09-04) | Export-option change in `Tools/testflight.sh` / fastlane; verify crash symbolication still works via the separate dSYM upload |

## Map view (game side) — deferred in `map_view_v2` (2026-09-04)

| Item | Deferred in | Notes |
|---|---|---|
| Wind ruler on the landing target (concentric rings = 1–2 mph each, Golf Clash / Golf Rival pattern) | `map_view_v2` | Wind is not shown on the map at all today; needs the wind vector exposed to `MapViewController` and a ring-spacing rule. Cesar left it out of B1 on purpose — ask before adding |
| Final SHOT VIEW icon art | `map_view_v2` | Placeholder camera glyph ships (`Assets/Resources/UI/Icon - ShotView.png`); Robin's icon drops into the same slot, zero code |
| Distance rings 80/120 + labels | `map_view_v2` (kept commented out since iter-28) | Delete or revive; B1 only uses the r100 landing ring |
| Hazard / OB markers on the map | competitor sweep 2026-09-04 (Golf Clash red signs) | Nothing designed; would need OB-mask → marker placement |

## How this file is used
When a spec defers something: add the row in the same session (Architect). When an item is taken up: move its row into the new spec's Goal and delete it here. Cesar prunes anything he decides is never-do.

## Control schemes (deferred in `control_scheme_seam` / `CONTROL_SCHEMES_PLAN.md`, 2026-09-04)

| Item | Deferred in | Notes |
|---|---|---|
| Haptics per timing grade (JUST / PERFECT / MISS) | `control_scheme_seam` §8 | Rides on `haptics_option` (Notion 2130) — one HapticService seam, Settings on/off first |
| TW 3-click meter as a fifth scheme | `control_scheme_seam` §8 | Accessibility option; cheap once the seam exists (tap-tap-tap on a vertical meter, no gesture) |
| Per-scheme first-shot hint / tutorial | `control_scheme_seam` §8 | One overlay per scheme on the first swing after a switch |
| Converging-circle timing (Confluence 2024/9/17) | `CONTROL_SCHEMES_PLAN.md` §9 | Unconfirmed in 白猫GOLF; only if the pendulum does not feel right |
| Grade SFX (JUST / GOOD / MISS chimes) | `scheme_pendulum` §7 | CC0 placeholders sourced by the Architect when taken up; one `SfxId` per grade through `SfxBus` |
| `pendulum_grade` telemetry key | `scheme_pendulum` §7 | `timing01` = 1 − |marker| already ships; add the string key only if the dashboard needs the grade, not the distribution |
| Bot personality per scheme (sweeps waited, pull tempo by level) | `bot_scheme_parity` §8 | Cosmetic pacing on top of `BotSwing`; no fairness impact |
| `BotDriver` (loop-v2 smoke harness) migrated to `BotSwing.Play` | `bot_scheme_parity` review | GRANDFATHERED on the Rule 23 allow-list; its determinism backs other acceptance runs, so migrate deliberately with a golden-file diff. Also widen the Rule 23 candidate glob to `*CaptureDriver.cs` / `*Capture.cs` / `*Recorder.cs` (`MapViewCaptureDriver` swings raw today) |
| Scheme comparison CSV export | `scheme_evaluation` §8 | One button on the new dashboard section; same shape as any existing export |
| Per-scheme retention curve (switched and stayed) | `scheme_evaluation` §8 | Needs per-player ordering of `shot_taken.scheme` over time; only if the switched-to counts are ambiguous |
| `needle_grade` telemetry key + Tap Timing grade SFX | `scheme_needle` §7 | Same shape as the Pendulum rows above |
| Free Swing grade SFX + `freeswing_path`/`tempo` telemetry keys | `scheme_freeswing` §7 | Same shape as the Pendulum / Needle rows |
| Ball launch delayed to the swing impact frame | `golfer_3d_test` §8 | Needs the Drive/Putt impact-frame seconds from the test report; then `ShotController` commit → delayed physics launch (or clip time-scaled to the launch) |
| `Characters.csv` `modelPrefab` column + per-character `PfGolfer_<Name>` | `golfer_3d_test` §8 | The real-roster spec; loader falls back to the starter model when a prefab is missing (same shape as `renderable`) |
| Golfer camera framing, club trail on `ClubStart/ClubEnd`, reactions/celebrations, cloth/hair, bot golfers | `golfer_3d_test` §8 | Polish once the stand-in proves the pipeline on device |
