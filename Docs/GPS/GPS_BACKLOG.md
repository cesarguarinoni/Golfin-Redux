# GPS / PLAYLIFE — deferred-scope backlog

> Everything consciously left OUT of the 2026-09 GPS build, in one place, with where it was
> deferred and what it needs. Maintained by the Architect — every future GPS spec that defers
> something adds a row here in the same session. Last updated: 2026-09-03 (gps_checkin DONE).

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
| `ShimmerBlock.prefab` moves `Assets/Prefabs/UI/Gps/` → `Assets/Prefabs/UI/Common/` | `game_polish_b` (Cesar decision 2026-09-03) | ONE line in `GpsPolishBuilder` (the prefab path constant) changes — the only GPS-folder touch the game track will make; GUID preserved by `git mv`, so scene/prefab references survive. GPS session: expect that diff. |

## How this file is used
When a spec defers something: add the row in the same session (Architect). When an item is taken up: move its row into the new spec's Goal and delete it here. Cesar prunes anything he decides is never-do.
