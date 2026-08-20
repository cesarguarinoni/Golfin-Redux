# GOLFIN ECONOMY MASTER — RP economy + paid track

**2026-08-19 · Architect · companion workbook: `GOLFIN_Economy_Model.xlsx` (replaces the outdated
"New levels.xlsx" — that sheet was pre-÷10 and pre-cutover; every number here is read from the
LIVE repo/backend).** 日本語サマリーは末尾。

## 1. The economy as it actually runs today (verified in code/prod)

One currency: **RP == playlife `total_points`**, one shared ledger (`points_transactions`),
server-authoritative since 2026-08-12. Earns enter ONLY through the `game_point_actions` catalog;
spends only through `spend_pts` (row-locked, idempotent). New accounts start at 0.

**Sources:** hole complete **10** · hole replay **5** · 1v1 win **20** · tournament prizes (bands:
small 300/150/50, medium 500/300/100, major 2000/1200/500/100 — pool totals 950 / 1,800 / 11,900;
server cap 2,000 per event) · GPS visit verification **10–50** (partner app, shared ledger, never
ranks on leaderboards) · admin grants (ops).

**LIVE sinks:** practice fee **10** · tournament entry **0/10/50** (admin-set, debited
server-side pre-entry) · level-ups **ceil(level/2)** per level (full 1–240 journey =
**14,520 RP**, verified by formula against LevelUpCosts.csv) · shop clubs **100–600** ·
stamina boosts **12–31** (tournament stamina is isolated per tournament, so these never touch
competition — Cesar decision [d]).

**DORMANT sinks (designed, mechanics missing — verified in code 2026-08-19):**
- **Repairs:** `UseBestRepairKit` restores durability, but nothing in gameplay ever DECREASES
  `currentDurability` — wear is unimplemented, so repair kits currently fix a bar that never drops.
- **Ball consumption:** balls stack as a quantity (`BallData`, max 99) but no play consumes them —
  one-time purchases, not a recurring sink.
- **Gacha:** banners price 50–75 / 450–675 but the prize pool is a STATIC MOCK
  (`GachaMockPrizePool.cs`) — no real ticket spend.

**Reference active player** (all editable in the workbook): earns ≈ **300 RP/day**, recurring
live spend (practice + entries + a stamina boost) ≈ 50/day → **NET ≈ 250 RP/day** → full level
cap in ~58 days; a Supreme-priced club (proposal below) in ~12 days.

**The structural takeaway:** with repairs, ball consumption and gacha dormant, the only recurring
drains are entry fees and stamina — once a player caps their roster the economy leaks upward.
Activating durability wear (which also makes the club roster's Durability stat and repair-kit
drops meaningful) is the highest-leverage sink to ship next.

## 2. Policy layer (agreed rules, restated as product law)

Decisions of record (Ken-doc §04 as amended by Cesar's comments [a][d][g][i]):

1. **No pay-to-win, ever.** Rarity and progression are acquired with RP only. Never sold for
   money: rarity items, RP/XP/materials, stamina recovery, repairs, stat-carrying skins,
   tournament slots/priority.
2. **RP is not purchasable and never cashes out** — sponsor prize exchange only.
3. **Nothing is sold for real money today, and won't be until real cosmetics exist** (no 3D
   avatars or club skins yet). The paid track is gated on cosmetic CONTENT, not on economy work.
4. **Tournament stamina is per-tournament** — recovery items stay sellable for RP without
   touching competition.
5. B2B revenue (course subscriptions, sponsor campaigns) is external value, split
   50% rewards / 40% GOLFIN / 10% host course.

## 3. Planned RP additions (near-term)

- **Club roster (C2, in review):** 799 clubs. Proposed shop ladder Common 100 / Uncommon 200 /
  Rare 400 / Mythic 800 / Legendary 1,500 / Supreme 3,000 RP (workbook `ClubEconomy`, editable).
  Recommendation: rotating shop subset; consider Supreme earn-only (tournament prizes) for
  scarcity. All within "RP only" — no money path.
- **Gacha realization:** wire a real prize pool (club variants are the natural filler) before
  treating gacha as a sink; odds published per banner `rulesUrl`.
- **Repairs:** keep item-based; if a direct RP repair price ships, anchor at ~10–30 RP per use
  so it stays below replay earn rates.

## 4. Paid track (future, cosmetics-gated)

When cosmetic content exists: skins/cosmetics with zero stats (usable in competitive as
appearance), emotes/effects/spectator features, season pass with cosmetic+title rewards only.
Prerequisites before the first SKU: real cosmetic assets, IAP plumbing (playlife `iap.py` exists
for the partner app), store-listing classification per policy §2, and the "stage for skins"
surfaces (rankings/replays/lobbies) so purchases are visible. No numbers proposed here — a
revenue model without content would be fiction.

## 5. Open decisions (for Ken / Cesar)

1. **RP expiration** — 3mo / 6mo / conditional (Business Model v0.5 open item). Must be in
   tournament rules before competitive launch.
2. **Endgame loops post-cap** (~58 active days to cap at net earn): seasons, new courses, tournament
   cycles, titles, periodic ranking resets — never paid strengthening.
3. **Supreme acquisition** — shop at 3,000 RP or earn-only?
4. **Gacha pool contents + published odds.**

---

## 日本語サマリー（Ken向け）

**現状（2026-08-19、実装値で検証済み）：** 通貨はRP一本（playlifeの`total_points`と同一、
サーバー管理の共有台帳）。獲得：ホール完了10 / リプレイ5 / 1v1勝利20 / 大会賞金（バンド制、
1イベント上限2,000）/ 来場認証10〜50。消費：練習料10、大会参加費0〜50、レベルアップ
（1〜240で合計**14,520 RP**、数式で検証済み）、ショップ、スタミナ（大会スタミナは大会ごとに
独立）。アクティブプレイヤーの目安：獲得約300 RP/日、恒常支出約50/日 → 純増約250 RP/日、
カンストまで約58日。**注意：ガチャ（賞品プールがモック）、修理（耐久値が減る処理が未実装）、
ボール消費（消費処理なし）の3つは設計上のシンクだが現状は未稼働。** 耐久摩耗の実装が
最も効果的な次のシンク。

**方針（合意済み）：** Pay-to-Winなし。レアリティ・育成はRPのみで獲得。RPは購入・換金不可。
現時点で有償販売はゼロ — 本物のコスメ（3Dアバター／クラブスキン）が完成するまで課金は
開始しない。B2B収益の配分は 50%リワード / 40% GOLFIN / 10%ゴルフ場。

**次の追加：** クラブ799本のRP価格帯（100〜3,000、レビュー中）、ガチャの実プール化。

**要決定：** RP有効期限（3/6ヶ月）、カンスト後のループ設計、Supremeの入手経路
（ショップ3,000 RP か大会限定か）、ガチャ内容と確率公開。
