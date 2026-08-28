# GOLFIN ECONOMY MASTER — RP economy + paid track

**2026-08-19 · Architect · companion workbook: `GOLFIN_Economy_Model.xlsx` (replaces the outdated
"New levels.xlsx" — that sheet was pre-÷10 and pre-cutover; every number here is read from the
LIVE repo/backend).** 日本語サマリーは末尾。

## 1. The economy as it actually runs today (verified in code/prod)

One currency: **RP == playlife `total_points`**, one shared ledger (`points_transactions`),
server-authoritative since 2026-08-12. Earns enter ONLY through the `game_point_actions` catalog;
spends only through `spend_pts` (row-locked, idempotent). New accounts start at 0.

**Sources:** hole complete **10** · hole replay **5** · 1v1 win **20** · **missions (designed 2026-08-28, not live — see §3)** · tournament prizes (bands:
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
cap in ~58 days; a Supreme-priced club (proposal below) in ~12 days. **With Missions (§3):
earn ≈ 361/day post-campaign → net ≈ 311/day → level cap ~47 days, full character roster
~60 days.**

**The structural takeaway:** with repairs, ball consumption and gacha dormant, the only recurring
drains are entry fees and stamina — once a player caps their roster the economy leaks upward.
Activating durability wear (which also makes the club roster's Durability stat and repair-kit
drops meaningful) is the highest-leverage sink to ship next.

**Related docs:** stamina system design + tunables: `Docs/Design/STAMINA_ECONOMY.md` +
`Docs/Design/stamina_economy.csv` (kept there — referenced by AI_CONTEXT and five completed
specs). Character roster & starter flow: `Docs/Game Design/CHARACTER_ROSTER_DESIGN.md`.
Outdated predecessor: `Docs/Game Design/New Levels.xlsx` (pre-÷10 — superseded by the workbook
beside this file).

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
- **Character unlocks (NEW — flow shipped 2026-08-20):** new players pick ONE starter (James or
  Olivia) in a Starting Character Selection screen; every other character is locked in Roster,
  including the unpicked starter. That makes character unlocking the second pillar sink beside
  clubs. Proposed RP ladder (workbook `CharacterEconomy`, editable): Common 200 / Uncommon 400 /
  Rare 800 / Mythic 1,600 / Legendary 3,000 / Supreme 6,000 — full roster ≈ **18,800 RP**
  (~74 days at net earn), and each unlock opens its own level track (up to 14,520 RP to cap), so
  the roster compounds long-term level-up demand. The unlock PURCHASE flow (RP price on the
  locked Roster card) is not built yet — needs its own spec. (Starter rarity asymmetry resolved
  2026-08-21: both James and Olivia are now Common — see
  `Docs/Game Design/CHARACTER_ROSTER_DESIGN.md`.)

- **Missions (NEW — designed 2026-08-28, `MISSIONS_REDESIGN.md` + `GOLFIN_Missions_Redesign.xlsx`
  sheet `Economy`):** entry fee **0** (retention loop, not a sink; stamina is the throttle at ≈ 4
  missions/day). One-off per account: 40-mission campaign first clears 15/25/40/60 per tier =
  **1,400 RP** + tier-clear bonuses 50/100/200/300 = **650** → **2,050 RP** plus 4 Repair Kits,
  3 Premium Repair Kits, 6 Gold Tickets. Recurring: Daily Mission **30 RP** once/UTC day +
  streak 15 (day 3) / 30 + Gold Ticket (day 7) ≈ 36/day averaged; mission replays **5 RP**
  under a **50/day** cap → **≈ 61 RP/day recurring**. New earn actions: `mission_clear`
  (pts NULL, max 60), `mission_replay` (5, cap 50/day), `mission_tier_clear` (pts NULL, max
  300), `daily_mission` (30, once/day), `daily_streak` (pts NULL, max 30); mission RP counts
  toward the mission leaderboards (Confluence), GPS RP still doesn't. Reward amounts are
  server truth via a `golfin_mission_rewards` mirror written on publish. Effect on the
  reference player: net 250 → **311/day**; level cap 58 → **47 days**; roster 74 → **60 days**.
  Acceptable because the recurring sinks are still dormant — durability wear stays the next
  sink to ship, and mission item rewards are kits so the two meet.

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

**ミッション（2026-08-28設計、未実装）：** 参加費0（継続率のためのループ、スタミナが上限）。40ミッションのキャンペーン初回クリア計1,400 RP＋ティア達成ボーナス650 RP＝**2,050 RP**（アカウントごと一回限り）＋修理キット・ゴールドチケット。恒常：デイリーミッション30 RP／日＋連続ボーナス（3日目15、7日目30＋チケット）、リプレイ5 RP（上限50／日）→ 恒常約**61 RP/日**。参考プレイヤーの純増250→約311 RP/日、カンスト約58→約47日、キャラ全解放約74→約60日。修理・ボール・ガチャのシンクが未稼働のため許容範囲。

**次の追加：** クラブ799本のRP価格帯（100〜3,000、レビュー中）、ガチャの実プール化、
**キャラクター解放（新）**：初回にJamesかOliviaを1体選択、他は全てロック → RPで解放
（提案：200〜6,000、全12体で約18,800 RP ≒ 純増換算で約74日）。解放ごとに育成トラックが
増えるため、レベルアップ需要も積み上がる。解放購入フローは未実装（要スペック）。

**要決定：** RP有効期限（3/6ヶ月）、カンスト後のループ設計、Supremeの入手経路
（ショップ3,000 RP か大会限定か）、ガチャ内容と確率公開。
