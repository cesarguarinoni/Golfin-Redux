# GOLFIN MONETIZATION PLAN — game + standalone GPS (PLAYLIFE)

**2026-09-03 · Architect · companion: `ECONOMY_MASTER.md` (RP economy, policy §2), `GACHA_ADMIN_PLAN.md`,
`GPS_BACKLOG.md`.** Every SKU below was checked against the no-pay-to-win rules already agreed with Ken.
Numbers are proposals, not decisions — prices in JPY, all editable. 日本語サマリーは末尾。

---

## 0. The one-line rule and the test every SKU must pass

**Money buys appearance, time-savers that everyone can also earn, and support for other people. Money never buys strength.**

Before any SKU ships it answers all five with "no":

| # | Question | Why it matters here |
|---|---|---|
| T1 | Does it change a shot? (any stat, distance, accuracy, spin, stamina, durability, RP, XP, tickets, kits) | `ECONOMY_MASTER` §2.1 — rarity and progression are RP-only |
| T2 | Does it convert to RP or tickets, directly or through a chain? (cash → gift → gift_pts → total_points is a chain) | §2.2 — RP is never purchasable; see §5.2 below, this chain exists today |
| T3 | Does it give tournament access, priority, a better bracket, or extra attempts? | §2.1 — tournament slots never sold |
| T4 | Is it a paid random draw that can land on anything stat-carrying? | Gacha stays RP-ticket only; a paid draw is cosmetic-only with published odds |
| T5 | Does a paying player see something a free player cannot reach in a reasonable time by playing? | Cosmetics: fine. Content (courses, modes, characters, clubs): never paywalled |

A "yes" anywhere = not a SKU. This list goes in the store-listing review checklist and in the admin (§7).

---

## 1. What is sellable, mapped to the content that exists

Inventory today: **16 characters · 20 ball types · 799 clubs**, all RP/earn/gacha. None of it is for sale for money and none of it will be. What we sell is a **cosmetic layer over it**.

| Lane | SKU family | Uses existing content how | Needs (content) |
|---|---|---|---|
| **C1 Character outfits** | Alternate outfit / colourway per character; 2–3 per character at launch (16 × 2 = 32 SKUs) | Same model, new material set; zero stats | 3D outfit variants or texture swaps — the cosmetic gate `ECONOMY_MASTER` §2.3 |
| **C2 Club finishes** | Finish packs (matte black, chrome, wood, neon…) applied to **any owned club** — NOT per-club skins for 799 clubs | Finish = material override on the club mesh; ownership of the club stays RP | Per-club-TYPE finish materials (driver/wood/iron/wedge/putter ≈ 5 meshes × N finishes) — cheap vs 799 |
| **C3 Ball cosmetics** | Ball trails, impact effects, ball logos — applied over whichever of the 20 balls is equipped | Ball stats unchanged; the 20 stat balls stay RP | Particle/decal assets |
| **C4 Identity** | Titles, profile frames, home backgrounds, victory poses, emotes, caddie-cart colours | Shown on leaderboards/replays/lobbies — the "stage" §2.4 asks for | 2D + a few animations |
| **C5 Golf Pass (season)** | 8-week pass: **paid track = cosmetics + titles only**; free track = RP, kits, tickets (same as today's earn) | Rides Missions + tournaments as the XP source | Season theme art; one C1 + one C2 + C4 set per season |
| **C6 Cosmetic gacha (optional, later)** | Paid **cosmetic** tickets → cosmetic-only pool, published odds, pity | Reuses the whole `golfin_gacha_pull` machinery with a new `ticket_types` row and a pool whose `kind` is cosmetic-only | A `cosmetic` pool kind + validator rule "paid ticket ⇒ pool must be 100 % cosmetic" |

**Explicitly not for sale:** the 799 clubs, the 16 characters, the 20 balls, RP, Standard/Gold tickets, repair kits, stamina, level-ups, tournament entry, extra tournament attempts, bag slots (bag slots gate power — keep RP or earn).

**Why no paid "gem" currency:** selling SKUs directly (a) removes the bridge that every P2W drift crosses ("gems can buy tickets… just this once"), (b) keeps us out of the prepaid-payment-instrument obligations under Japan's 資金決済法 (a stored paid balance carries reporting/deposit duties above threshold), (c) makes the store-listing "no loot box for money" statement true unless C6 ships. If C6 ships, its tickets are the only paid balance and they are consumed, not stored — still check the 資金決済法 position with counsel before launch.

### 1.1 Price ladder (JPY, App Store tiers, proposal)

| SKU | Price | Note |
|---|---|---|
| Character outfit | ¥480 | Bundle of 3 for ¥1,200 |
| Club finish pack (applies to all clubs of a type) | ¥320 | All-types bundle ¥980 |
| Ball trail / effect | ¥160–¥320 | Impulse tier |
| Title / frame / background | ¥120–¥320 | Many are **earn-only** (tournament wins) so paid never outranks earned |
| Golf Pass | ¥980 / season | No "premium+" tier that skips levels — pass levels are play-only |
| Cosmetic gacha ticket (C6, if ever) | ¥160 ×1 / ¥1,500 ×10 | Odds on the in-app RATES modal (already built for RP gacha) |

### 1.2 Weekly rotation of clubs / balls / characters (RP, shop + gacha) — added 2026-09-08

Rotation is the free economy's retention engine and the thing that makes 799 clubs feel like a
catalogue rather than a wall. It is **RP-only** and sits under the same five tests: nothing
paid touches the lineup, and everything that rotates out rotates back in — a stat item is never
made permanently exclusive by the schedule (Supreme stays earn-only by quota, not by paywall).

| Piece | Weekly (Mon 00:00 UTC = 09:00 JST) | Where it is controlled |
|---|---|---|
| STORE lineup | Clubs by rarity quota (default 3 Common / 2 Uncommon / 2 Rare / 1 Mythic / 1 Legendary / 0 Supreme, spread across the 7 club types), 3 balls (quantity 10), 1 locked character; prices from the `ECONOMY_MASTER` §3 ladders + a new ball ladder (30 / 60 / 120 / 250 / 500) | Admin **Rotations** panel: quotas, seed, window, preview, materialize, one-click publish of the five catalogs, calendar of the next 8 weeks, archive of ended weeks |
| GACHA weekly banner | Clone of the standard pool with the week's top clubs **rate-up ×3 inside their rarity** and shown as featured; published odds update automatically in the RATES modal | Same panel; `featuredWeightMul`, base pool, banner copy per locale |
| Pity | Carries **across weeks** (`pityGroup = weekly`) so a weekly banner never resets a player's counter | One server rule (`golfin_gacha_pull` keys pity by group) |
| Fairness | Refs featured in the last 4 weeks are ineligible; deterministic seed so a regenerate is reproducible; validator blocks overlapping rotations and pity-group mismatches | Validator rules R1–R4 |
| Player surface | Lineup header with a countdown, `NEW` tags on the week's cards, `THIS WEEK` on the banner, mid-session roll-over at the boundary with no relaunch | `weekly_rotation_client` |

**Year plan (2026-09-08):** all 52 weeks from 2026-09-14 are planned item by item in
`Docs/Economy/GOLFIN_Rotation_Plan_2026-27.xlsx` and seeded as pinned `rotations` rows — headline
club type cycles weekly, brand of the week cycles through all 20 brands, 12 marquee weeks on the JST
calendar carry two Legendaries; 457 distinct clubs, every ball 6–11×, every non-starter character
5–7×; Freda (Supreme) on two marquee weeks is a flagged decision.

Specs: `Docs/Specs/Active/weekly_rotation_admin/` (catalog + generator + publish + pity rule)
then `Docs/Specs/Active/weekly_rotation_client/` (badges, countdown, roll-over). Interaction with
the paid track later: when cosmetics exist, the same rotation row can carry a cosmetic feature
of the week — but cosmetic rows never enter the RP lineup quotas, and lineup rows never enter a
paid SKU.

Guardrail on prestige: the rarest-looking cosmetics are **tournament/season-earned** (title + frame for a Major win, a Supreme-run finish). Paid cosmetics are good-looking, never the *best*-looking. That is what keeps "no P2W" believable to players, not just true.

---

## 2. Ads option — rewarded video only

### 2.1 Placement policy

- **Rewarded video only.** No interstitials, no banners, no forced ads. Premium presentation + JP audience + a no-P2W promise all point the same way: ads a player *chooses*, capped.
- Rewards are **things RP already buys, at RP-equivalent value, capped per day** — never anything money can't buy and never anything that touches a tournament. Because the reward equals an RP amount a free player earns in a few holes, T1/T2 hold: the ad is a *faucet identical to playing*, not a power path.

| Placement | Reward | Cap | RP-equivalent |
|---|---|---|---|
| **Post-hole "double up"** (result screen) | Doubles the hole's RP (10 → 20) | 3/day | +30 RP/day ≈ 10 % of a reference day (≈300) |
| **Practice fee waiver** | Practice fee 10 → 0 | 3/day | 30 RP |
| **Mission replay bonus** | +5 RP on a mission replay (counts inside the existing 50/day replay cap — no new faucet) | inside cap | 0 net new |
| **Stamina boost (non-tournament)** | The smallest shop boost (12 RP tier) | 2/day | 24 RP |
| **Gacha "bonus pull" — NO** | — | — | Tickets stay RP-only; ads never touch gacha |
| **Tournament anything — NO** | — | — | Isolated stamina, entries, attempts: never |

Ceiling ≈ **84 RP/day** for a player who watches every ad, i.e. ~25 % of a reference day's earn, all spendable only where RP is already spendable. The `ECONOMY_MASTER` reference player's net goes 311 → ~395/day; level-cap 47 → ~37 days. Acceptable while durability wear is still dormant, and the caps are admin-tunable (see §7) so they can come down when the sinks arrive.

**No "remove ads" SKU** — there is nothing to remove. If a rewarded ad ever feels like a tax, the placement is wrong.

### 2.2 Which network

| Option | Verdict |
|---|---|
| **Unity LevelPlay (ironSource) as mediation, with AdMob + AppLovin as bidders** — **recommended** | Native Unity package (lightest SDK path for our project), strong Asian-market demand, no UA coupling, mid-tier support is reachable. LevelPlay's dashboard also carries the rewarded-video server-side callback (SSV) we need for §2.3. |
| AppLovin MAX | Largest bidder pool and top iOS share (44 % of iOS game ad revenue in Q2 2026), but larger SDK footprint with documented Unity package conflicts, and its ROAS UA campaigns are MAX-only — irrelevant until we buy users. Revisit if ad revenue passes ~$10k/mo. |
| AdMob mediation alone | Simplest, Android-leading demand (25 % share), weaker on iOS rewarded. Fine as a *bidder inside* LevelPlay; not as the mediation layer. |

AdMob is added as a demand source in either case — it requires its own approval, so open the AdMob account at the start, not the day of launch.

### 2.3 Server truth for ad rewards (non-negotiable)

The client never grants an ad reward. Flow: LevelPlay **SSV callback → playlife `POST /ads/reward` (signature verified) → `earn_pts_v2`** with new `game_point_actions` rows `ad_double_up` (pts NULL, max 10, cap 3/day), `ad_practice_waiver` (10, cap 3/day), `ad_stamina_small` (grant path via `golfin_pending_grants`, cap 2/day). Caps live in the catalog — same shape as `mission_replay` 50/day. Idempotent by the network's transaction id. Kill switch: `content_settings.ads_enabled`, and per-placement enable flags in a new `ad_placements` catalog (CSV ↔ admin two-way, like gacha).

### 2.4 Revenue formula (editable, no fiction)

`ad_rev/day = DAU × opt-in% × views/DAU × eCPM / 1000`

Tier-1 rewarded video (US/UK/JP) currently clears **$15–$40 eCPM**, completion > 90 %, and JP rewarded on iOS showed the strongest Q2 2026 growth (+17 %) — but iOS ATT opt-out and a new app's unproven traffic land you at the **bottom** of that band for the first 2–4 weeks while the mediation ML learns. Worked example at launch scale: **1,000 DAU × 40 % × 2 views × $15 = $12/day (~¥1,800)**. At 10,000 DAU and a matured $25 eCPM: **$200/day (~¥30,000)**. Ads are a floor, not the business — but they are the **only revenue that needs zero cosmetic content**, which is why they are Phase 1.

---

## 3. Standalone GPS / PLAYLIFE monetization

The GPS app has different economics: fewer sessions, real-world venues, and the sponsor/course side already agreed (B2B split **50 % rewards / 40 % GOLFIN / 10 % host course**). Same five tests apply because it shares the RP ledger.

| Lane | What | Who pays | P2W check |
|---|---|---|---|
| **G1 Course & venue subscriptions** (the anchor) | Course listing tiers: Free (appears, check-in works) → **Listed** (¥9,800/mo: featured venue card, verified badge, visit campaign "+50 RP this month" funded by the course, round-history stats for their players) → **Partner** (¥29,800/mo: all of Listed + own tournament in-game, sponsored title/frame, monthly report) | Golf courses / driving ranges | Player earns RP by *going there*; course money funds the rewards pool (the 50 %). No power sold |
| **G2 Sponsor campaigns** | Brand-funded challenges ("Play 3 rounds at X, win a Y voucher"), branded club finishes (C2 — a real brand's finish as a *cosmetic*, the `CLUB_BRAND_IDENTITY_SHEET` opening), sponsored vote pools (the "500 pts" pill in the backlog becomes sponsor-funded) | Brands | RP prizes come from the sponsor pool; cosmetics only |
| **G3 Prize exchange** | RP → sponsor goods/vouchers (the one legal "cash-out": §2.2 sponsor prize exchange only). Sponsor supplies goods at cost; GOLFIN takes the 40 % on the campaign fee, not on the redemption | Sponsors | This is what makes RP worth playing for without RP ever being buyable |
| **G4 Gifting (support)** | Cash gift items sent to golfers you follow (Top Supporters / SEND GIFT already built for RP gifts). **Revenue split 30/20/50** per backlog (receiver / platform / rewards — confirm the order with Ken) | Fans / friends | ⚠ **Blocked until §5.2 is fixed** — today a gift credits `gift_pts` which is part of `total_points` = RP |
| **G5 PLAYLIFE Pro** (¥480/mo or ¥3,800/yr) | Unlimited score history + trends, handicap tracking, round photos/stories, public profile customization, priority in "Popular Golfers", export | Serious amateur golfers | Pure utility/social; nothing crosses into the game's stats. Free tier keeps the 4-page history the v1 client already fetches |
| **G6 Native sponsor tiles** | Venue cards and hub tiles marked SPONSORED — the "ad" format for GPS; no video ads in the GPS flow (people are on a course) | Courses/brands | Content, not power |

G1+G2+G3 are one system: **sponsors buy reach, players get RP and prizes, GOLFIN keeps 40 %.** G5 is the only consumer subscription and it stays outside gameplay. Rewarded video in the GPS app: **not at v1** — a check-in flow is the wrong place for a 30-second video; revisit if G5 conversion is weak.

---

## 4. Phasing — tied to what actually exists

| Phase | Ships | Gate | Revenue |
|---|---|---|---|
| **0 — now** | Nothing paid. Finish the RP sinks (durability wear, gacha pools, character unlock purchase flow) so the free economy is tight before money enters | — | 0 (as decided) |
| **1 — Ads (rewarded)** | §2: LevelPlay + AdMob bidder, 3 placements, SSV → `earn_pts_v2`, `ad_placements` catalog + admin caps + kill switch, telemetry `ad_view` on the beta rail | None on content; ~1 spec. **Store policy: ads disclosure in listing + privacy labels (ATT prompt on iOS)** | Small, immediate |
| **2 — Cosmetics + Golf Pass** | C1–C5 direct SKUs. IAP plumbing: StoreKit 2 / Play Billing → playlife receipt validation (extend `iap.py`) → `golfin_entitlements` ledger (the tickets-ledger shape: server truth, client cache) → `golfin_pending_grants(kind='cosmetic')`. Cosmetic catalogs `cosmetics`, `cosmetic_skus` two-way with the admin (price tier, window, `min_build`, ref art by URL). Season pass = `seasons` + `pass_tracks` catalogs with a validator rule **paid track rows may only reference kind=cosmetic|title** | **Real cosmetic assets** (the gate Ken and Cesar set). Spec order: entitlements → cosmetic catalogs → store screen → pass | The business |
| **3 — GPS B2B + Pro** | G1 tiers in the admin (venue row gains `tier`, `campaign_rp`, `sponsor_pool`), G2 campaign catalog, G3 exchange catalog + fulfilment ops panel, G5 Pro entitlement, G4 gifting **after §5.2** | GPS standalone shell decision (backlog: Unity thin-shell vs Flutter — reached) | Recurring, contract-based |
| **4 — optional** | C6 cosmetic gacha; MAX as a second mediation if ad rev > $10k/mo | Legal review (景表法 odds display, no コンプガチャ) | Incremental |

---

## 5. Things found while checking the current code against the rules

1. **Rewarded-ad RP is a faucet** — capped and admin-tunable per §2.3, but it lands on an economy whose recurring sinks are still dormant. Ship durability wear before or with Phase 1, or set the ad caps to 2/2/1 at launch.
2. **`gift_pts` is inside `total_points`** (`gps_gifts_votes`: `total_points = activity_pts + gift_pts`). RP gifts are fine (RP → RP). The moment a **cash** gift item credits `gift_pts`, real money becomes RP through a chain — T2 fails and §2.2 is broken. Before G4: cash-origin gifts credit a separate `support_pts` (or grant the cosmetic item only, 0 pts — the backlog already notes the item path awards 0 gift_pts, which is the right precedent), and `gift_pts` stays RP-only-origin. Needs its own migration + a one-line rule in `ECONOMY_MASTER` §2.
3. **Gacha `dupeRp`** — a paid cosmetic pool (C6) must have `dupeRp = 0` or a cosmetic-only dupe credit; otherwise paid pulls leak RP. Add to the validator when/if C6 ships.
4. **Golf Pass free track can carry Gold Tickets** (they exist in `ticket_types`), the paid track cannot. Same validator idea as G1/G2 in `shop_stocking` — a rule, not a memory.
5. **Bag slots**: if a bag-slot expansion is ever proposed as a SKU, it fails T1 (more clubs available on-course = power). Keep it RP or level-gated.

---

## 6. What the plan needs from Ken / Cesar (decisions)

1. **Ads at all?** Yes/no on Phase 1 — it is the only pre-cosmetic revenue, and it is reversible (kill switch).
2. **Ad caps** at launch: 3/3/2 as above, or the conservative 2/2/1.
3. **Cosmetic scope for the first drop**: which 4–6 characters get outfits first, how many finishes (C2 is cheapest per SKU because it is per club *type*).
4. **Golf Pass price** ¥980 vs ¥1,200, and season length 8 weeks vs a calendar quarter.
5. **Gift split order** for 30/20/50 and the `support_pts` decoupling (§5.2).
6. **Course tier prices** — G1 numbers need a sales conversation with two or three courses first.
7. **C6 cosmetic gacha**: on the table or off. Off is the cleaner store-listing story.

---

## 7. Admin/ops surface (so none of this needs a build)

`ad_placements` (id, placement, rewardAction, cap, enabled, min_build), `cosmetics` / `cosmetic_skus` (price tier, window, art URL), `seasons` / `pass_tracks`, `venue_tiers`, `sponsor_campaigns`, `prize_exchange` — all catalogs on the shared `CatalogPanel`, two-way CSV, `min_build`, drafts → publish → export, kill switches beside `gacha_enabled`. A **Revenue** ops panel: ad views/day, eCPM by placement (from the LevelPlay reporting API), SKU sales, pass conversion, ARPDAU split ads/IAP/B2B — the Telemetry-panel shape.

---

## 日本語サマリー（Ken向け）

**原則：** お金で買えるのは「見た目」「誰でもプレイで得られる時間短縮」「他人への応援」のみ。**強さは絶対に売らない。** 全SKUは5つのテスト（ショットに影響するか／RPやチケットに変換されるか／大会の枠・優先権か／有償ランダムで性能物が出るか／無課金で到達不能か）にすべて「いいえ」で答えられる必要がある。

**週替わりローテーション（2026-09-08追加、RPのみ）：** 毎週月曜09:00 JSTにストアのラインナップ（レアリティ別クォータでクラブ9本・ボール3種・ロックキャラ1体、価格は既存のRP価格帯＋新設のボール価格帯）とガチャの週替わりバナー（注目クラブをレアリティ内で排出率3倍、天井は週をまたいで継続）が入れ替わる。管理画面の「Rotations」パネルでクォータ・シード・期間を設定→プレビュー→生成→5カタログを一括公開、ビルド不要。直近4週に登場したものは除外、Supremeは既定で対象外（大会限定のまま）。**課金要素はいっさい含まない**。

**販売するもの（ゲーム）：** キャラ衣装（16体×2〜3種、¥480）、クラブ仕上げパック（799本個別ではなくクラブ種別ごと、所持クラブ全部に適用、¥320）、ボールの軌跡・エフェクト（20種のボール性能は不変）、称号・フレーム・背景、**ゴルフパス（¥980／シーズン、有償トラックはコスメと称号のみ、RP・チケット・キットは無償トラックのみ）**。有償ジェム通貨は作らない（P2Wへの橋渡しを断つ、資金決済法の前払式支払手段の負担を避ける）。**799クラブ・16キャラ・20ボール・RP・チケット・修理キット・スタミナ・大会参加権はいっさい販売しない。**

**広告：** リワード動画のみ（インタースティシャル・バナーなし）。報酬はRPで既に買えるものと同等・1日上限付き（ホール報酬2倍×3、練習料無料×3、小スタミナ回復×2 ≒ 最大84 RP/日、通常獲得の約25%）。大会・ガチャには一切触れない。付与はサーバー側（SSV→`earn_pts_v2`）、上限と停止スイッチは管理画面。ネットワークは **Unity LevelPlay をメディエーション、AdMob と AppLovin を入札者として** 推奨（Unity純正、アジア需要が強い）。Tier-1のリワード動画eCPMは現在$15〜40。試算式：DAU×視聴率×回数×eCPM÷1000（例：DAU1,000で約¥1,800/日、DAU10,000で約¥30,000/日）。コスメ資産が不要な唯一の収益なのでフェーズ1。

**GPS単体（PLAYLIFE）：** 軸はB2B（50/40/10の合意済み配分）。ゴルフ場のリスティング階層（無料／Listed ¥9,800／Partner ¥29,800 月額、来場RPキャンペーンはゴルフ場負担）、スポンサー企画（ブランド仕上げはコスメとして）、RP→スポンサー景品交換（唯一の「換金」）、応援ギフト課金（30/20/50）、**PLAYLIFE Pro**（¥480/月：スコア履歴無制限・ハンディ・統計・プロフィール拡張、ゲーム性能には無関係）。GPSアプリ内に動画広告は入れない。

**要注意（コード確認済み）：** 現状 `gift_pts` は `total_points`（=RP）に含まれるため、**現金ギフトを実装すると現金→RPの経路ができてしまう**。ギフト課金の前に現金由来分を別ポイント（または0pts＋アイテムのみ）に分離する必要あり。

**要決定：** 広告導入の可否と上限、最初のコスメ範囲、パス価格、ギフト配分の順序、コース階層価格（営業ヒアリング後）、コスメガチャの有無。
