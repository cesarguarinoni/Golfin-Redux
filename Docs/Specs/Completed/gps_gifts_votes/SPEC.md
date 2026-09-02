# SPEC — `gps_gifts_votes`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. (Standard pipeline states — SPEC_READY → IMPLEMENTER_WORKING → … → DONE.)

## The nine build rules from `gps_profile_pack` apply verbatim

Baked gradients from token scripts; `GpsUiColor.A()`/`ADark()` translucency never over-painted; navy-disc-in-gold-ring atoms; Main Buttons labels 59 (small variants per component); geometry-JSON + invariants + lint gates with numbers quoted; SemiBold white interactive text; **every new key PUBLISHED**; builder scripts are prefab source of truth; reuse atoms. See `Docs/Specs/Completed/gps_profile_pack/SPEC.md`.

## Goal

The last two GPS screens: **Gift** (Figma `14027:101843`) and **Vote** (`14028:33534`), wired into the hub's nav bar (Gift tab; Vote reachable from the hub's LIVE VOTES tile / Home tiles — activate whatever hub affordances currently sit inert for these two). Scope per Cesar 2026-09-02:

- **Gift, live**: RP gift sends (fix `/gifts/send-pts` server-side — it currently breaks the `total_points = activity_pts + gift_pts` invariant on BOTH sides), live catalog purchases via `/gifts/purchase` (same invariant bug — same fix), Top Supporters aggregated client-side, Popular Golfers from `/user/discover`.
- **Vote, live core**: `/vote/list` cards, cast via `/vote/{id}/cast` (+10 RP via `/points/earn?action=vote_cast`), results bars, CREATE (Yes/No). Static per Figma: stories row, photo card areas; filters reduced to PUBLIC (all active) / MINE (client-side by `creator_id`), TRENDING and FRIENDS chips rendered disabled.

## Reference

- Renders in `reference/`: `gps_gift_14027-101843.png`, `gps_vote_14028-33534.png` (1170×2532, pulled 2026-09-02). All fills/fonts below pulled fresh from the nodes the same day.
- Both frames: GPS Nav Bar Container VISIBLE (Gift tab active on Gift; Vote reached off-bar). `Backgrounds` variants differ per screen — read them off the frames before building.
- **Placeholder vs canonical**: every name (Taro/Ken/Hiro/Yui/Misaki/Aiko/Rina/Hana), every number (4,820 pts, 2,340 followers, 68%, 47 votes), the CLAP/BIRDIE/EAGLE item names and the vote questions are MOCKUP data. Live bindings below. The status pill's "500 pts / 2,000 pts" pool amounts are a mockup concept with NO backend — v1 binds that pill to the real cast reward, rendering `+10 pts` (deviation flagged here, per the no-invented-rules rule; pools await a future backend).

## Backend changes (playlife repo — one migration + router edits + deploy)

All three legacy paths update `activity_pts`/`gift_pts` WITHOUT `total_points`, breaking the invariant every post-unification writer maintains (`2026_08_12_gift_pts_total_points_fix.sql` header documents it), and none are atomic or idempotent. Fix in the house pattern (one SECURITY DEFINER function per flow, service-role-only, idempotent by `points_transactions(user_id, idempotency_key)`, business outcomes as return values — model on `2026_06_29_points_atomic.sql` / `golfin_gacha_pull`):

1. **Migration `2026_09_02_gift_atomic.sql`** (implementer writes it; Architect pastes to Cesar for the Supabase SQL editor after review):
   - `golfin_gift_pts(p_sender uuid, p_receiver uuid, p_amount int, p_key text)` — one transaction: refuse self-gift/amount≤0/insufficient (balance check against `activity_pts`… the sendable balance is **activity_pts** — gift_pts are earnings, not spendable on gifting; refuse if `activity_pts < p_amount`); debit sender `activity_pts` **and** `total_points`; credit receiver `gift_pts` **and** `total_points`; two ledger rows (`gift_sent`/`gift_received`, the receiver row keyed `p_key||':recv'`); idempotent replay returns the original outcome.
   - `golfin_gift_purchase(p_user uuid, p_item uuid, p_currency text, p_key text)` — price lookup + tier rule as today, debit (`activity_pts` or `gift_pts`) **and** `total_points`, ledger row, `user_inventory` insert, one transaction, idempotent.
2. **`backend/routers/gifts.py`**: `/send-pts` and `/purchase` become thin wrappers over the RPCs; request models gain a required `idempotency_key`. `/gifts/send` (item gifts) is untouched — not in v1's client path. Feed-item insert stays in the router (best-effort, after commit), as does the receiver-verification 404.
3. **Deploy** to Fly from the Mac; verify with a real send between Cesar's two accounts: sender RP down, receiver RP AND gift_pts up, replay with the same key is a no-op.

Flutter unaffected? ⚠️ It calls both endpoints WITHOUT a key — make `idempotency_key` optional with a server-generated fallback (uuid4) so Flutter keeps working un-idempotently; Unity always sends one.

## Client data bindings

| UI | Source |
|---|---|
| Gift Hero value ("4,820 pts") | `UserDetailDto.gift_pts`; sub "from N supporters" = distinct senders in received pages (below); title/note static keys |
| Top Supporters (3 rows) | client aggregation: page `/gifts/received` (`skip/limit`, page 50, max 4 pages v1), group by sender, sum `gift_pts_awarded`+pts amounts, top 3 with rank/initial-avatar/name/`—` followers (received rows carry sender id+name? verify DTO — if display_name absent, fetch via `/user/{id}/profile`; NOTE in report) |
| Popular Golfers (5 rows) | `/user/discover` → first 5: display_name, followers_count ("N followers"), initial avatar (colour by `avatar_color` when present, else the 4-pair rotation); SEND GIFT button per row |
| SEND GIFT | modal (new `GiftSendModalController` on `ModalController` base, Pop-up panel language like `VenuePickerModalController`) — recipient header, RP balance line, amount presets **50 / 100 / 500 / 1000** + confirm; calls `UserService`-layer `GiftService.SendPts(receiverId, amount, key)`; success toast + `PointsService.RefreshBalanceAsync` so the Top-UI RP updates |
| Buy Gift Items strip (3 cells) | first 3 `basic`-tier rows of `/gifts/items` (real catalog: ベーシックキャップ 50 · サンバイザー 80 · ポロシャツ（白） 100): name, `price_activity_pts`, icon by `category` (hat→Star, tops→Sparkle, shoes→Pin, else Heart — GPS Icons set); tap → confirm modal → `GiftService.Purchase(itemId, "activity", key)` → toast + balance refresh |
| Vote list | `/vote/list?skip&limit` → cards; Yes/No votes render bar cards (`vote_options` labels/`percentage`/`vote_count`), >2 options render the Multi pill card (option pills `A(#7ed488,0.18)` etc., label "Label NN%") |
| Card states | not-yet-voted: VOTE button enabled, bars at live percentages; voted (`user_votes` conflict = 400 "Already voted" OR local record): VOTE disabled; cast → POST cast → repaint from response → `/points/earn?action=vote_cast` (+10) → balance refresh |
| Status pill | `+10 pts` (real cast reward — see Reference note) |
| Meta line | "N votes · D days left" from `total_votes` + `expires_at` (JA form via key with {0}/{1}) |
| Filters | PUBLIC = full list; MINE = `creator_id == my id` client-side; TRENDING/FRIENDS chips rendered at 0.45 opacity, non-interactive |
| CREATE | modal (`VoteCreateModalController`): question input + fixed Yes/No options + expiry choices (24h / 3d / 7d) → `/vote/create` → prepend card |
| Stories row / photo areas | static per Figma (stories = decorative avatars from discover's first 7 names; photo area keeps the placeholder gradient + icon); GIFT button on photo cards routes to the Gift screen |

New services in `Golfin.Social`: `GiftService` (SendPts/Purchase/Items/Received pages), `VoteService` (List/Cast/Create) — plain C# singletons on the `Instance`/`ConfigureForTest`/`ResetForTest` pattern, endpoints added to `Endpoints.cs` (`/api/v1/gifts/*`, `/api/v1/vote/*` — note gifts plural, vote singular). DTOs per the live JSON (verify against a real response, not this table).

## Figma Fidelity (node values, pulled 2026-09-02)

| Element | Node | Value |
|---|---|---|
| Gift Hero panel | `14027:102100` | 958×288; fill GRAD **#6b2140→#3a1226** (the one non-standard panel — pink/plum; bake via script) stroke std 3px r50; Gift icon 36 + "GIFTS RECEIVED" SemiBold 36 **#f07f9c**; sub Medium 26 **#f4b8c8**; value SemiBold **96** #ffffff; note Medium 24 #f4b8c8 |
| Supporters/Golfers panels | `14027:102114`/`102146` | std panel (GRAD #133453→#091b33, stroke 3 r50); Panel Header 80h: title 42 gold, "SEE ALL ›" muted (hidden v1 — no destination); separator line; rows 96h: rank SemiBold 30 #eedc9a x32, avatar 72 disc (grad pair + #f3ecc2 w3), name SemiBold 30 white, followers Medium 22 #b7c3d3, pts SemiBold 32 **#f07f9c** right / SEND GIFT = Main Buttons Gold-Small 240×54 |
| Buy Gifts | `14027:102190` | std panel 312h; title SemiBold 34 gold "BUY GIFT ITEMS"; sub Medium 24 muted; 3 cells 287×168 std-panel fill **r28**; icon ring 72 (atom); name SemiBold 22 white; price SemiBold 24 #eedc9a |
| Vote stories | `14028:33791` | 88px discs: NEW = dashed?/plain disc with "+" SemiBold 44 #eedc9a + label "NEW" Medium 18 gold; others avatar grad discs stroke #f3ecc2 w3.5, label Medium 18 white |
| Filter chips row | `14028:33827` | container fill #091b33@0.70 r100 (→ `ADark`); selected chip = gold-gradient #f3ecc2→#c9a94f stroke #422100 w1 r100, label SemiBold 24 **#2a1a00**; unselected = no fill, stroke #818ea1 w1, label SemiBold 24 #ffffff; + CREATE = Gold-Small 230×54 |
| Vote cards | `14028:33836/33877/33901`, `14029:102241` | std panel r50; photo area GRAD #3f6b3a→#1c3a1f (bake) with 80px Screenshot icon + author strip (48 avatar, name SemiBold 26, "2h ago" Medium 22 muted); question SemiBold 30 white; status pill `A(#eedc9a,0.18)` stroke gold w1 r100 label SemiBold 22 gold; bars: label Medium 26 white 70w, track 16h r8 `A(white,0.15)`, YES fill **#7ed488**, NO fill **#6fa5e8**, pct SemiBold 26 white; footer meta Medium 24 #b7c3d3; buttons GIFT=Silver-Small, VOTE=Gold-Small 230×54 |
| Multi option pills | `14028:33908` | `A(#7ed488,0.18)` stroke #7ed488 w1 r100, label SemiBold 22 #7ed488, "Name NN%" |

Enumerate ALL of these in the builder's spec.json — chips, pills, bars, both panel gradients, the hero plum gradient — so the lint constrains what tends to ship broken.

## Architecture context

- Builder `GpsGiftVoteBuilder.cs` (Editor) → `GpsGiftScreen.prefab` + `GpsVoteScreen.prefab`; controllers `GpsGiftScreenController` / `GpsVoteScreenController` (+ the two modal controllers) in `Assets/Scripts/UI/Gps/`, namespace `Golfin.Gps.UI`; hub-pattern OnEnable (paint cache → subscribe → `client.Run` fetches).
- New `ScreenId.GpsGift`, `ScreenId.GpsVote` → ScreenManager registration, ShowTopBarOnly, `NavTitleKeyFor` (`GPS_GIFT_TITLE`/`GPS_VOTE_TITLE`), **GpsGate list** (both are GPS surface).
- Hub wiring: nav-bar Gift tab → GpsGift; the hub's Vote/LIVE VOTES affordance → GpsVote (find the inert hooks from `gps_hub_entry`; reuse, don't rebuild).
- Bake scripts: extend `Docs/Scripts/make_gps_hub_panels.py` family with the plum hero + photo-placeholder gradients (`make_gps_gift_vote_panels.py`).

## Localization

~34 new keys EN+JA (`GPS_GIFT_*`: TITLE, HERO_TITLE, HERO_SUB{0}, HERO_NOTE, SUPPORTERS, POPULAR, SEND_GIFT, BUY_TITLE, BUY_SUB, MODAL_* (BALANCE{0}, CONFIRM, SENT{0}), PURCHASED{0}, INSUFFICIENT; `GPS_VOTE_*`: TITLE, NEW, TRENDING, FRIENDS, PUBLIC, MINE, CREATE, VOTE, GIFT, YES, NO, META{0}{1}, REWARD_PILL, ALREADY, CAST_TOAST{0}, CREATE_* (QUESTION_HINT, EXPIRY_24H/3D/7D, SUBMIT)) — importer PLAN (verdict) → APPLY → **publish `texts`** → `--check` clean. No hardcoded literals.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Per-element A/B crops vs both renders for every fidelity row; ΔRGB table incl. the plum hero, chips, bar fills.
- [ ] Geometry JSON + invariants + lint `fail=0`, both screens, spec.json covering the enumerated elements.
- [ ] Migration reviewed by Architect and applied by Cesar BEFORE deploy; deployed; live E2E quoted: send 50 RP between Cesar's accounts → sender RP −50, receiver RP +50 AND gift_pts +50, both ledger rows present, same-key replay is a no-op (quote the SQL/log evidence).
- [ ] Purchase E2E: buy the 50-pt item → RP −50, inventory row, replay no-op.
- [ ] Invariant audit query after both E2Es: `total_points = activity_pts + gift_pts` holds on the touched profiles (quote it).
- [ ] Vote E2E: list renders live votes; cast → 400-on-second-attempt handled as "voted" state; `+10` earned exactly once (ledger row quoted); create → card appears; MINE filter shows only own.
- [ ] Editor play-mode screenshots of both screens signed in, in `screenshots/`, with service log lines.
- [ ] Both new ScreenIds in GpsGate (EditMode-pinned via the two-arg overload); hub Gift tab and Vote affordance navigate.
- [ ] Balance in Top UI refreshes after send/purchase/cast.
- [ ] Importer: PLAN verdict quoted, APPLY, publish, `--check` clean; zero hardcoded `.text` literals (grep quoted).
- [ ] Full EditMode sweep green.
- [ ] Deviations flagged (the `+10 pts` pill binding is pre-flagged here).

## Files / hierarchy this task touches

- `Assets/Scripts/UI/Gps/`: `GpsGiftScreenController.cs`, `GpsVoteScreenController.cs`, `GiftSendModalController.cs`, `VoteCreateModalController.cs`, `Editor/GpsGiftVoteBuilder.cs` — NEW.
- `Assets/Prefabs/UI/Gps/GpsGiftScreen.prefab`, `GpsVoteScreen.prefab` — NEW (builder output).
- `Golfin.Social`: `GiftService.cs`, `VoteService.cs`, DTOs; `Golfin.Net/Endpoints.cs`.
- `ScreenManager.cs`, `PersistentUIManager.cs`, `GpsGate.cs`, `GpsHubScreenController.cs` (activate the inert hooks).
- `Docs/Scripts/make_gps_gift_vote_panels.py`; `Assets/Art/UI/Gps/` new bakes.
- `Assets/Localization/LocalizationText.csv` + publish.
- playlife: `backend/migrations/2026_09_02_gift_atomic.sql`, `backend/routers/gifts.py`, Fly deploy.

## Smoke evidence

Live E2E evidence quoted per checklist (this task moves real RP — every economy assertion needs the ledger row or query result pasted, not asserted). Any test rows/transactions on prod against Cesar's accounts are acceptable (his call 2026-09-02 scope answers); do NOT create throwaway third accounts without asking.

## Out of scope (do NOT do these)

- `/gifts/send` item-gifting path, IAP, revenue splits, inventory/equip UI.
- Photo upload, stories behaviour, TRENDING/FRIENDS backends, vote pools/odds, `vote_hit` resolution flow.
- SEE ALL destinations (hidden v1); followers/follow actions.
- No changes to `/vote/*` router (it is correct enough for v1; cast's non-atomic counters are tolerable at current volume — NOTE, don't fix).
- No Flutter changes.
