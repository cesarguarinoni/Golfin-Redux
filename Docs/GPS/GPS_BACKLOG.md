# GPS / PLAYLIFE — deferred-scope backlog

> Everything consciously left OUT of the 2026-09 GPS build, in one place, with where it was
> deferred and what it needs. Maintained by the Architect — every future GPS spec that defers
> something adds a row here in the same session. Last updated: 2026-09-03 (gps_polish DONE, device pass queued).

## Player-visible promises (highest priority — the UI already implies them)

| Item | Implied by | Needs |
|---|---|---|
| **Settings: edit golf profile** (nickname, colour, experience, handicap) | Golf Profile copy: "You can change all of this later in Settings" (`auth_golf_profile`) | A Settings entry or GPS-profile edit screen reusing the same panel + `PUT /user/update`. NOTE: `PUT /user/update` cannot clear a field to NULL (omitted = preserved) — un-setting a handicap needs an explicit-null contract or a clear endpoint (auth_golf_profile deviation 7) |
| **Rounds tab destination** | Hub nav bar has a Rounds tab; no Rounds screen was ever designed or built (hub shows only the latest rounds inline) | Design round (no Ken mockup), then a spec: full score history list over `/score/history` paging |
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
| `gps_checkin_screen` | roadmap | Next GPS feature after current queue; needs a design round (no Ken mockup); adds `GPS_ERR_*` keys EN+JA; second `RecordFix` call site — makes trust-core K4 reachable |
| Android GPS build variants | `punch_it_gps_variants` | One `Android-Full-GPS` profile clone + lane when Android builds resume |
| Android mock-GPS detection plugin | decision 2026-09-01 | `IMockLocationDetector` seam exists; iOS first |
| In-app build-variant watermark | `punch_it_gps_variants` | The Home **GPS pill** is the tell since `gps_pill_entry` (banner restored to plain admin behaviour); revisit only if Cesar asks |
| Home promo banner still deep-links `golfin://gps` on no-GPS builds | `gps_pill_entry` | Banner shows on both variants again; on a "Punch it" build the tap hits GpsGate's refusal. Unpublish/retarget the `home_promo` row for no-GPS audiences, or gate the route in the admin |
| Rubik Medium TMP asset | `auth_golf_profile` | `Rubik:Medium` resolves to the variable face — Medium runs render ~5% narrow, Welcome sub wraps one word late. Import a real Medium SDF asset once, fixes every GPS screen |
| Real map view (venues/courses) | decision 2026-09-01 | v1 is list-only via geohash prefixes |
| Standalone PLAYLIFE shell | DECIDED 2026-09-02 | Unity thin-shell, Flutter retired (one codebase). Spec `gps_standalone_shell` after `gps_gifts_votes` + `gps_polish` |
| Haptics (game + GPS) with Settings on/off toggle | `gps_polish` map, 2026-09-02 | Cesar: not yet — must land in the game AND GPS together, with a toggle. Notion Order 2130 `haptics_option` |
| Avatar photo upload (`/user/avatar`) | `auth_golf_profile` | Endpoint exists; game uses character art + colour instead |
| Golf-profile prompt is per-device | `auth_golf_profile` | PlayerPrefs flag: a second device re-prompts once. Server-side "prompted" flag if it annoys |
| Badge names JA-only in seeds | `gps_profile_pack` | EN localization of the 24 badge names rides the existing keys; verify EN column quality |
| bioKey/nameKey localization wiring | pre-GPS | Long-standing partial wiring, unrelated to GPS but adjacent |

## How this file is used
When a spec defers something: add the row in the same session (Architect). When an item is taken up: move its row into the new spec's Goal and delete it here. Cesar prunes anything he decides is never-do.
