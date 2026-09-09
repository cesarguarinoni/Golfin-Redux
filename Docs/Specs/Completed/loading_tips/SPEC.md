# SPEC — `loading_tips`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

**Status:** see `STATUS.md` (`SPEC_READY`, 2026-09-09, Architect / Cowork).
**Track:** Polish (Notion GOLFIN_Roadmap — row filed by the Architect).
**Architect defaults (Cesar did not decide these — report may flag):** pool-state persistence in `PlayerPrefs` (`loadingtips.state`, §3.4); "shown twice" = two full passes of the first pool; recent-history window of 5 for the randomiser; tap-pulse rest alpha 0.55; two rows ship `active=0` (§2.3). **Cesar decided (2026-09-09):** a new loading screen always opens on a new tip; "TAP FOR NEXT TIP" pulses.

## Goal

The loading screen's Pro Tips are the game's only always-on tutorial and they are two systems out of date: the seven `TIP_*` strings and `Assets/Art/LoadingScreen/Tip *.png` still describe the pre-`miss_grade_duff` grade rings (PERFECT / REDUCED POWER / MISSED), the pre-`control_scheme_seam` "flicker through the ball" aim, the old map toggle and the pre-`selector_carousel` gear swap, and nothing at all about Pendulum / Tap Timing / Free Swing, stats and condition, missions, tournaments, 1v1, gacha, the store or PLAYLIFE. Cesar (2026-09-09): update the Figma, add tips for **every** shipped system, and move the game from `ProTipCard`'s hard-coded `string[] tipKeys` + parallel `Sprite[]` to a **CSV catalog with two pools** — a *first pool* shown in fixed order, each tip twice, on a fresh install, then a *general pool* drawn at random — which is the original Confluence "Loading Tips System" design.

Figma is already updated (this session): page **Loading** (`4096:1181`) holds the **34 authored tip components** in section `Authored — OFFICIAL` (`14218:1220`) — the seven originals rewritten plus 27 new ones — each with a drawn diagram in its `Authored diagram` frame (library `Blue` panels, real game sprites for kits / balls / tickets / RP). That section is the implementation source; the `Screenshot — NOT CHOSEN` section is reference only.

## Reference

- **Figma page:** `Loading` / id `4096:1181`, file `5gEAHjl6xAtW8iYY7NMvWd`. Per-tip node ids in §2.2.
- **Existing card style (unchanged):** container 978 wide, Rubik SemiBold 51 white with `#EEDC9A` keyword highlights (the CSV already uses `<color=#EEDC9A>`), image column 806 wide, "TAP FOR NEXT TIP" footer.
- **Node renders dropped to `reference/`:** `loading_grid_2026-09-09.png` (whole page), `row1..row4.png` (the new grid at y 0–8400: each image is the ISOLATED Figma element — pop-up tiles, card, accordion, HUD widget — on a transparent background, no screen behind it), `authored_row1..4.png` (the same 26 tips again at y ≥ 11400, suffix "(authored)": images drawn in the Grades/Aim panel style from primitives, no screenshots). The original seven are redone in both styles too — `originals_element.png` (y 22600, suffix "(element)") and `originals_authored.png` (y 25400) — so every one of the 34 tips has an element variant and an authored variant. Authored panels use the library `Blue` paint style; sprites inside them are the real game art (`Resources/Items/Thumbnails/RepairKit-*`, `Resources/Balls/Thumbnails/*`, `Resources/Art/Gacha/Tickets/Ticket_Standard.png`) and the library `RP Icon Container`. **Decision 2026-09-09 (Cesar): the AUTHORED set is official for implementation** — section `Authored — OFFICIAL` on the Loading page (`14218:1220`); the element-crop variants sit in section `Screenshot — NOT CHOSEN` (`14218:1222`) and are deleted at close-out. Every one of the 34 tips therefore ships WITH an image; there are no text-only rows and no in-game capture pass. Pull any single component with `get_screenshot` on its id if a crop is ambiguous.
- **Design source for the pool rules:** Confluence "Loading Tips System" (project doc `Golfin - Confluence.txt` p.201–203).

## 1. What is wrong today (verified in the repo)

| Site | Today | Why it is wrong |
|---|---|---|
| `ProTipCard.tipKeys` (ShellScene `ProTipCard`, 7 entries) + `tipSprites[7]` matched by INDEX | order SWING, CLUB, FORECAST, RARITIES, ACCURACY, TIMING, VIEW; cycles in that order every 8 s, restarts at 0 every time the screen enables | no pools, no persistence, index-coupled sprites (adding a key without a sprite shifts every image) |
| `TIP_TIMING` "HIT THE BALL WHEN HIGHLIGHTED FOR A PURE SHOT" + `Tip Timing.png` (PERFECT / REDUCED POWER / MISSED rings) | grades are PURE / GOOD / HOOK / SLICE / THIN / DUFF since `miss_grade_duff` (`SHOT_GRADE_*`, texts v43); PERFECT/JUST/MISS/SHANK rows are `false` | retire key + image |
| `TIP_ACCURACY` "FLICKER THROUGH THE BALL FOR FULL ACCURACY" + `Tip Accuracy.png` | Flick aims by sliding inside the cone; green arrows = PURE, below the red line = DUFF (`SCHEME_POPUP_FLICK_LINE2`) | rewrite + re-export |
| `TIP_VIEW` "TAP THE MAP TO SWITCH…" + `Tip View.png` | map opens from the hole card (`HoleCardWidget.OpenMapView` → `MapViewController.OpenViaWidget`), B1 overlay with SHOT VIEW bottom-left (`map_view_v2`) | rewrite + re-export |
| `TIP_CLUB` "HOLD THE CLUB/BALL BUTTONS…" + `Tip Club.png` | hold still works (`SelectorDragRouter` HOLD mode), but a quick tap now opens the 4-slot carousel (MODAL mode, `selector_carousel`) | rewrite + re-export |
| `TIP_FORECAST` | claim unchanged (aim line is carry-only; wind indicator top-left) — reworded to say what to do about it | rewrite + re-export |
| `Tip Leaderboard.png` exists in `Assets/Art/LoadingScreen/` with NO key and NO scene slot | orphan asset | deleted with the rest of the old set (§3.6) |

## 2. Content

### 2.1 Pool rules (Confluence design, as specced)

- **First pool** (8 tips, §2.2 order 1–8): shown in fixed order. Each tip is shown **twice** = the list is walked twice end to end (pass 1, pass 2), across as many loading screens and taps as it takes. Progress persists (§3.4). Fresh install → tip 1 of pass 1.
- **General pool** (all rows with `active=1`, first-pool rows included): once pass 2 completes, every subsequent tip is a uniform random draw from the general pool minus the recent-history exclusion below. What persists for the general pool is the `recentKeys` ring (§3.4).
- Advancing = one call to `Advance()` — from the auto-cycle at `autoCycleInterval`, from a tap, **and once every time the loading screen is shown** (`OnEnable`). Cesar 2026-09-09: "the user gets different tips each run" — a loading screen must never open on the tip the previous one opened on. In the first pool that means each boot / hole load moves one step down the tutorial order (the order is kept, that is the point of the first pool); in the general pool it means a fresh random draw.
- **Randomiser (general pool):** the draw excludes the last **5** keys shown (`recentKeys`, persisted, §3.4), not just the previous one — with 34 rows that is a 29-way draw and no tip can come back within 5 shows across runs. If fewer than 6 active rows exist the exclusion shrinks to what is possible (never an empty candidate set). RNG = `UnityEngine.Random` behind the injectable `Func<int,int>` so tests are deterministic.
- `active=0` rows are skipped everywhere; if the first pool has fewer than 1 active row the controller falls straight to the general pool; if the general pool is empty the card shows `TIP_HEADER` only (no exception, no blank card).

### 2.2 The tips (EN copy is the Figma copy; `[..]` = `#EEDC9A` highlight)

**First pool — fixed order**

| # | key | Figma component (authored) | sprite ← `Authored diagram` frame | EN | JA |
|---|---|---|---|---|---|
| 1 | `TIP_SWING`  | `14217:33524` | `Tip_SWING` ← frame `14217:33586` | [PULL] THE CLUB BACK DOWN THE CONE FOR POWER, THEN [FLICK] UP TO HIT | コーンに沿ってクラブを[引いて]パワーを溜め、上に[フリック]して打とう |
| 2 | `TIP_ACCURACY`  | `14217:33596` | `Tip_ACCURACY` ← frame `14217:33658` | SLIDE LEFT OR RIGHT INSIDE THE CONE TO FINE-TUNE YOUR [AIM]. GREEN ARROWS = [PURE], BELOW THE RED LINE = [DUFF] | コーン内で左右にスライドして[狙い]を微調整。緑の矢印＝[ピュア]、赤線より下＝[ダフリ] |
| 3 | `TIP_GRADES` (new; replaces `TIP_TIMING`)  | `14217:33673` | `Tip_GRADES` ← frame `14217:33719` | EVERY SWING IS GRADED: [PURE], GOOD, HOOK, SLICE, THIN OR [DUFF]. A DUFF STILL MOVES THE BALL, JUST NOT FAR | すべてのスイングは[ピュア]・グッド・フック・スライス・トップ・[ダフリ]で判定。ダフリでもボールは少し飛ぶ |
| 4 | `TIP_VIEW`  | `14217:33740` | `Tip_VIEW` ← frame `14217:33791` | TAP THE MAP ON THE [HOLE CARD] TO PLAN FROM ABOVE. THE LIME ARC IS YOUR MAX REACH. [SHOT VIEW] BRINGS YOU BACK | [ホールカード]のマップをタップして上から作戦を立てよう。ライム色の弧が最大到達距離。[ショットビュー]で戻れる |
| 5 | `TIP_CLUB`  | `14217:33804` | `Tip_CLUB` ← frame `14217:33852` | [TAP] THE CLUB OR BALL BUTTON TO OPEN THE SELECTOR, OR [HOLD] AND SLIDE TO SWAP IN ONE MOVE | クラブ／ボールボタンを[タップ]でセレクターを開く。[長押し]してスライドすれば一動作で交換 |
| 6 | `TIP_FORECAST`  | `14217:33864` | `Tip_FORECAST` ← frame `14217:33900` | THE AIM LINE SHOWS [CARRY] ONLY. WIND AND ROLL ARE ON YOU: CHECK THE [WIND ARROW] TOP-LEFT BEFORE EVERY SHOT | エイムラインは[キャリー]のみ。風と転がりは自分で読もう。毎ショット左上の[風の矢印]を確認 |
| 7 | `TIP_RARITIES`  | `14217:33912` | `Tip_RARITIES` ← frame `14217:33987` | DIFFERENT [RARITIES] BESTOW DIFFERENT INITIAL STATS AND [MAX LEVEL] | [レア度]によって初期ステータスと[最大レベル]が異なる |
| 8 | `TIP_CONTROLS`  | `14203:33149` | `Tip_CONTROLS` ← frame `14203:33206` | NOT A FLICK FAN? [SETTINGS › CONTROLS] OFFERS PENDULUM, TAP TIMING AND FREE SWING. THE SHOT PHYSICS NEVER CHANGE | フリックが合わない？[設定 › 操作方法]で振り子・タップタイミング・フリースイングを選べる。ショットの物理は変わらない |

**General pool — additional rows** (`pool=general`, `order` = row number for stable diffs)

| # | key | Figma component (authored) | sprite ← `Authored diagram` frame | EN | JA |
|---|---|---|---|---|---|
| 9 | `TIP_PENDULUM`  | `14203:33218` | `Tip_PENDULUM` ← frame `14203:33371` | [PENDULUM]: PULL STRAIGHT BACK, THEN FLICK UP WHEN THE MARKER HITS THE [RED CENTRE]. HIGHER CLUB CONTROL SLOWS THE MARKER | [振り子]：まっすぐ引いて、マーカーが[赤い中心]に来た瞬間に上へフリック。クラブコントロールが高いほどマーカーは遅くなる |
| 10 | `TIP_TAPTIMING`  | `14203:33383` | `Tip_TAPTIMING` ← frame `14203:33531` | [TAP TIMING]: PULL AND RELEASE, THEN TAP TO STOP THE NEEDLE IN THE [GREEN]. EARLY HOOKS LEFT, LATE SLICES RIGHT | [タップタイミング]：引いて離し、針が[緑]に入ったらタップで止める。早いとフック、遅いとスライス |
| 11 | `TIP_FREESWING`  | `14203:33541` | `Tip_FREESWING` ← frame `14203:33705` | [FREE SWING]: DRAG DOWN FOR POWER, THEN SWING UP THROUGH THE [IMPACT LINE]. AN ANGLED PATH SHAPES A DRAW OR FADE | [フリースイング]：下にドラッグしてパワー、[インパクト線]を上に通過して打つ。斜めに振るとドローやフェードになる |
| 12 | `TIP_OVERPOWER`  | `14203:33716` | `Tip_OVERPOWER` ← frame `14203:33750` | PULL PAST THE [GOLD LINE] FOR UP TO [120%] POWER. HARDER TO TIME, AND NEVER ON A PUTT | [金線]を越えて引くと最大[120%]パワー。タイミングは難しくなり、パットでは使えない |
| 13 | `TIP_SPIN`  | `14203:33763` | `Tip_SPIN` ← frame `14203:33792` | DRAG THE DOT ON THE [SPIN] DISC TO SHAPE YOUR FLIGHT. A BALL WITH MORE SPIN UNLOCKS A BIGGER DISC | [スピン]ディスクのドットをドラッグして弾道を調整。スピンの高いボールほどディスクが広がる |
| 14 | `TIP_FADEDRAW`  | `14203:33803` | `Tip_FADEDRAW` ← frame `14203:33832` | ARM [FADE/DRAW] AND SLIDE THE CLUB IN THE CONE TO CURVE THE BALL AROUND TROUBLE | [フェード/ドロー]をオンにしてコーン内でクラブをスライド。障害物を曲げてかわそう |
| 15 | `TIP_AUTOCLUB`  | `14204:33149` | `Tip_AUTOCLUB` ← frame `14204:33178` | THE GAME PICKS A [CLUB] FOR EVERY SHOT: DRIVER OFF THE TEE, PUTTER ON THE GREEN. A MANUAL PICK LASTS ONE SHOT | [クラブ]は毎ショット自動で選ばれる：ティーではドライバー、グリーンではパター。手動選択はそのショット限り |
| 16 | `TIP_STATS`  | `14204:33193` | `Tip_STATS` ← frame `14204:33296` | [STRENGTH] ADDS POWER. [CLUB CONTROL] STEADIES YOUR AIM. [STAMINA] IS A BIGGER TANK. [RECOVERY] REFILLS IT FASTER | [ストレングス]はパワー。[クラブコントロール]は狙いの安定。[スタミナ]はタンクの大きさ。[リカバリー]は回復の速さ |
| 17 | `TIP_CONDITION`  | `14204:33317` | `Tip_CONDITION` ← frame `14204:33346` | EVERY HOLE COSTS [CONDITION]. BELOW 70% YOUR STRENGTH AND CLUB CONTROL SLIP. REST, OR [BOOST] FROM THE ROSTER | ホールごとに[コンディション]を消費。70%を下回るとストレングスとクラブコントロールが落ちる。休むか、ロスターから[ブースト] |
| 18 | `TIP_LEVELUP`  | `14204:33361` | `Tip_LEVELUP` ← frame `14204:33493` | [LEVEL UP] WITH REWARD POINTS AND SPEND [SKILL POINTS] ON THE STATS YOU WANT. RARITY SETS THE CAPS | リワードポイントで[レベルアップ]し、[スキルポイント]を好きなステータスに振ろう。上限はレア度で決まる |
| 19 | `TIP_CLUBSTATS`  | `14204:33514` | `Tip_CLUBSTATS` ← frame `14204:33543` | CLUBS TRADE [POWER], [ACCURACY] AND LIE RESISTANCE. HIGHER ACCURACY MEANS A TIGHTER AIM CONE | クラブは[パワー]・[精度]・ライ抵抗のトレードオフ。精度が高いほどエイムコーンは狭くなる |
| 20 | `TIP_BALLS`  | `14204:33560` | `Tip_BALLS` ← frame `14204:33589` | BALLS ARE TRADE-OFFS, NOT UPGRADES: POWER, REBOUND, [WIND RES.], ROLL AND [SPIN]. THE GOLFIN BALL IS THE NEUTRAL BASELINE | ボールは上位互換ではなくトレードオフ：パワー・跳ね・[耐風]・転がり・[スピン]。GOLFINボールが基準 |
| 21 | `TIP_SURFACES`  | `14204:33605` | `Tip_SURFACES` ← frame `14204:33634` | WHERE YOU LAND MATTERS: [FAIRWAY], FRINGE, ROUGH, [BUNKER], WATER, OB. READ THE BANNER AFTER EVERY SHOT | 落ちる場所が大事：[フェアウェイ]・カラー・ラフ・[バンカー]・ウォーター・OB。ショット後のバナーを確認 |
| 22 | `TIP_MISSIONS`  | `14205:33149` | `Tip_MISSIONS` ← frame `14205:33179` | 40 [MISSIONS] ACROSS BEGINNER, AMATEUR, PRO AND LEGEND. CLEAR [8 OF 10] TO UNLOCK THE NEXT TIER | ビギナー・アマチュア・プロ・レジェンドの40[ミッション]。[10個中8個]クリアで次のティアが開放 |
| 23 | `TIP_DAILY`  | `14205:33199` | `Tip_DAILY` ← frame `14205:33228` | THE [DAILY MISSION] PAYS 30 RP. KEEP THE [STREAK]: DAY 3 PAYS +15, DAY 7 PAYS +30. MISS A DAY AND IT RESETS | [デイリーミッション]は30 RP。[連続]を続けよう：3日目は+15、7日目は+30。1日逃すとリセット |
| 24 | `TIP_TOURNAMENT`  | `14205:33249` | `Tip_TOURNAMENT` ← frame `14205:33304` | [TOURNAMENTS] RUN ON THEIR OWN STAMINA POOL. IT NEVER REFILLS DURING THE EVENT, SO PACE YOUR ROUNDS | [トーナメント]は専用のスタミナプールで戦う。大会中は回復しないので配分を考えよう |
| 25 | `TIP_VERSUS`  | `14205:33316` | `Tip_VERSUS` ← frame `14205:33345` | [1V1]: ONE HOLE, ALTERNATE SHOTS, FIRST TO SINK WINS. TRAILING? YOU GET ONE SHOT TO TIE. A WIN PAYS [200 RP] | [1V1]：1ホール、交互にショット、先にカップインした方が勝ち。後攻には同点にする1打がある。勝てば[200 RP] |
| 26 | `TIP_LEADERBOARD`  | `14205:33354` | `Tip_LEADERBOARD` ← frame `14205:33444` | [LEADERBOARDS] RANK REWARD POINTS EARNED. DAILY, WEEKLY AND MONTHLY RESET; [HISTORY] NEVER DOES | [リーダーボード]は獲得リワードポイント順。デイリー・ウィークリー・マンスリーはリセット、[ヒストリー]は永久 |
| 27 | `TIP_RP`  | `14205:33455` | `Tip_RP` ← frame `14205:33484` | [REWARD POINTS] ARE THE ONLY CURRENCY. EARN THEM ON EVERY HOLE, MISSION AND ROUND. THEY ARE NEVER FOR SALE | [リワードポイント]が唯一の通貨。ホール・ミッション・ラウンドで稼ごう。課金では買えない |
| 28 | `TIP_GACHA`  | `14205:33502` | `Tip_GACHA` ← frame `14205:33602` | PULLS COST [TICKETS], AND A [X10] IS CHEAPER THAN TEN SINGLES. EVERY BANNER SHOWS ITS RATES, PITY AND X10 GUARANTEE. DUPLICATES CONVERT TO RP | ガチャには[チケット]が必要。[10連]は単発10回より安い。各バナーに確率・天井・10連保証を表示。重複はRPに変換 |
| 29 | `TIP_STORE`  | `14206:33149` | `Tip_STORE` ← frame `14206:33207` | THE STORE LINEUP [ROTATES WEEKLY]. WATCH FOR NEW TAGS AND THE COUNTDOWN. [HISTORY] KEEPS YOUR PURCHASE LOG | ストアの品揃えは[週替わり]。NEWタグとカウントダウンに注目。[ヒストリー]に購入履歴が残る |
| 30 | `TIP_GPS_CHECKIN`  | `14206:33223` | `Tip_GPS_CHECKIN` ← frame `14206:33273` | PLAYING FOR REAL? [CHECK IN] AT THE COURSE IN PLAYLIFE ROUNDS FOR +30 PTS, [CHECK OUT] FOR +15. GPS-VERIFIED ONLY | リアルゴルフの日は PLAYLIFE ラウンドでコースに[チェックイン]して+30 pts、[チェックアウト]で+15。GPS認証が必要 |
| 31 | `TIP_GPS_SOCIAL`  | `14206:33289` | `Tip_GPS_SOCIAL` ← frame `14206:33338` | [VOTE] ON OTHER GOLFERS' QUESTIONS FOR +10 PTS, OR SEND A [GIFT] TO SUPPORT YOUR FAVOURITE | 他のゴルファーの質問に[投票]して+10 pts。推しには[ギフト]を贈って応援しよう |
| 32 | `TIP_GPS_WALLET`  | `14206:33353` | `Tip_GPS_WALLET` ← frame `14206:33404` | PLAYLIFE POINTS AND GAME RP ARE [ONE WALLET]. EARN ON THE COURSE, SPEND IN THE GAME | PLAYLIFEのポイントとゲームのRPは[ひとつの財布]。コースで稼いでゲームで使おう |
| 33 | `TIP_GRAPHICS`  | `14206:33414` | `Tip_GRAPHICS` ← frame `14206:33443` | FRAMES DROPPING? [SETTINGS › GRAPHICS]. AUTO PICKS FOR YOUR DEVICE. THE COURSE IS IDENTICAL ON EVERY TIER | 動きが重い？[設定 › グラフィック]へ。AUTOが端末に合わせて選ぶ。コースはどの設定でも同じ |
| 34 | `TIP_REPAIR`  | `14206:33463` | `Tip_REPAIR` ← frame `14206:33492` | [REPAIR KITS] RESTORE CLUB DURABILITY: 50%, 75% OR 100% BY RARITY. REPAIR ALWAYS USES YOUR BEST KIT FIRST | [リペアキット]はクラブの耐久度を回復：レア度により50%・75%・100%。修理は常に最良のキットから使う |

Two copy corrections 2026-09-09 (Cesar: "make sure the texts are accurate"): gacha pulls are priced in tickets per banner — `gacha_banners.csv` `costX1 50` / `costX10 450`, so "1 ticket = 1 pull" is false and the tip now says a x10 is cheaper than ten singles; the daily streak pays +15 at day 3 and +30 at day 7 then wraps (`missions_v1` SPEC L126–127, `daily_streak` action) — the Gold Ticket at day 7 exists only in `MISSIONS_REDESIGN.md` and is NOT implemented, so it is out of the tip.

Fact sources, for the reviewer: grades `SHOT_GRADE_*` + `controls.csv` (`TimingPowerMulRed`, `MissPowerMul`); Flick aim/cone `SCHEME_POPUP_FLICK_LINE2`; schemes `SCHEME_POPUP_*`; overpower `PendulumPull120Px` + "putt caps at 100%"; spin `Docs/Specs/Completed/spin_selector_ux`; fade/draw `fade_draw_core_wiring`; auto club `auto_club_selection`; stats/condition `Docs/Design/STAMINA_ECONOMY.md` + `stamina_economy.csv` (8/hole, 70 % knee, Recovery = regen); level caps `Assets/Data/Characters.csv` `maxLevel`; missions `Docs/Game Design/MISSIONS_REDESIGN.md` (40, 4 tiers, 8-of-10 gate, daily 30 RP, streak day 3/7 + Gold Ticket); tournaments `stamina_tournament_wiring`; 1v1 `1v1_match_flow` §1 + `1v1_result_rewards_display` (200 RP); leaderboard `leaderboard_wiring` §1.2; RP `Docs/Economy/ECONOMY_MASTER.md` (never purchasable); gacha `GACHA_ADMIN_PLAN.md` §pity/x10/dupeRp; store `weekly_rotation_client` + `store_history` specs; GPS `gps_checkin` (+30/+15, GPS radius) + `gps_gifts_votes` (+10 vote_cast); quality `quality_tiers`; repair `Assets/Data/Items.csv`.

### 2.3 Rows that ship `active=0` (player-visible promise rule)

- `TIP_STORE` — `weekly_rotation_client` and `store_history` are `SPEC_READY`, not shipped. Flip to `active=1` in the same commit that closes them.
- `TIP_REPAIR` — durability wear is dormant (`ECONOMY_MASTER.md` §"nothing in gameplay ever DECREASES it"); a tip about repairing clubs that never wear is a promise. Flip when wear ships.

### 2.4 Retired

- `TIP_TIMING` — key stays in the CSV with the fourth column `false` (the pipeline never deletes, invariant I6); **Cesar deactivates the row in the admin** after publish, as with the retired `SHOT_GRADE_*` rows. `Tip Timing.png` is deleted from `Assets/Art/LoadingScreen/` (its Figma source is now `Loading Screen - Grades`).

## 3. Design

### 3.1 Data — `Assets/Resources/Data/LoadingTips.csv` (new, bundled, client-only in this spec)

```
key,pool,order,sprite,active
TIP_SWING,first,1,Tip_SWING,1
TIP_ACCURACY,first,2,Tip_ACCURACY,1
TIP_GRADES,first,3,Tip_GRADES,1
TIP_VIEW,first,4,Tip_VIEW,1
TIP_CLUB,first,5,Tip_CLUB,1
TIP_FORECAST,first,6,Tip_FORECAST,1
TIP_RARITIES,first,7,Tip_RARITIES,1
TIP_CONTROLS,first,8,Tip_CONTROLS,1
TIP_PENDULUM,general,9,Tip_PENDULUM,1
… one row per §2.2 line; `sprite` = `Tip_<key minus TIP_>` for every row (34 sprites, none text-only); `active` per §2.3 …
TIP_STORE,general,29,Tip_STORE,0
TIP_REPAIR,general,34,Tip_REPAIR,0
```

- `pool` ∈ {`first`,`general`}; `first` rows are ALSO members of the general pool (§2.1). `order` sorts the first pool and is otherwise cosmetic. `sprite` is a lookup name into `ProTipCard.tipSprites` (§3.3), NOT a Resources path — the art stays where it is, no GUID churn.
- Parsed by a new `LoadingTipCatalog` (`Assets/Scripts/UI/LoadingTipCatalog.cs`, `Assembly-CSharp`, `namespace GolfinRedux.UI`): `static IReadOnlyList<LoadingTip> Load(TextAsset csv)`; `#` comment lines skipped like `catalogs.py` does; a malformed row logs one warning and is dropped. `[Serializable] struct LoadingTip { string key; bool first; int order; string sprite; bool active; }`.
- **Not a content catalog in this spec.** It is not added to `ContentCatalogs.All`, has no migration and no admin panel — the tip *text* is admin-editable through `texts` (§3.5) already; only pool membership / order / active is bundled. Registering `loading_tips` as a real catalog (migration registry row, `ContentCatalogs.Data` + an applier, admin panel) is the deferred row in Notion.

### 3.2 Sequencing — `LoadingTipSequencer` (pure, testable)

`Assets/Scripts/UI/LoadingTipSequencer.cs`, `namespace GolfinRedux.UI`, no Unity dependencies beyond `UnityEngine.Random` behind an injectable `Func<int,int>`:

```csharp
public sealed class LoadingTipSequencer
{
    public LoadingTipSequencer(IReadOnlyList<LoadingTip> rows, LoadingTipState state, Func<int,int> rng);
    public LoadingTip Current { get; }          // what the card shows right now
    public LoadingTip Advance();                // NextTip(): moves the state, returns the new Current
    public LoadingTipState State { get; }       // to persist
}
[Serializable] public struct LoadingTipState { public int firstPass; /*0,1 = in pass 1/2; 2 = general*/ public int firstIndex; public string[] recentKeys; /* newest last, max 5 */ }
```

Rules (§2.1): while `firstPass < 2`, `Current` = active first-pool row at `firstIndex` (rows sorted by `order`); `Advance()` increments `firstIndex`, wrapping to `0` and `firstPass++`; when `firstPass` reaches 2 the next `Advance()` draws with `rng(candidates.Count)` from the general pool **minus `recentKeys`** (§2.1 randomiser; the exclusion is trimmed from the oldest end until at least one candidate remains), then pushes the drawn key onto `recentKeys` (cap 5, drop oldest). Every `Advance()` in the first pool also pushes its key, so the ring is already warm when the general pool starts. Deactivated rows are filtered at construction; if the persisted `firstIndex` is past the end of the (now shorter) first pool it clamps to 0 of the next pass. `Current` on a general-pool state with an empty ring draws once and stores it.

### 3.3 `ProTipCard` — bind to the sequencer, keep the card

Keep the component, the hierarchy, the tap handler and `autoCycleInterval` (the crossfade is replaced in §3.3a). Changes:

- Remove `string[] tipKeys`. Replace `Sprite[] tipSprites` with `[Serializable] struct TipSprite { public string name; public Sprite sprite; }` + `TipSprite[] tipSprites` looked up by `name` (scene wiring below). Add `[SerializeField] TextAsset tipsCsv` (drag `LoadingTips.csv`).
- `Initialize()` builds the sequencer from `LoadingTipCatalog.Load(tipsCsv)` + `LoadingTipStore.Load()`; `ShowTip(int)` becomes `Show(LoadingTip)`; `NextTip()` → `_seq.Advance()` then `Show` + `LoadingTipStore.Save(_seq.State)`. **`OnEnable` calls `_seq.Advance()` once before the first `Show`** (§2.1 — a new loading screen opens on a new tip; `Start`'s `Initialize` guard stays so the first enable does not advance twice — the report proves this with the state file: boot shows tip 1, not tip 2). `Initialize(string[] keys)` overload is deleted (no caller — grep in the report).
- `Show` still prefers `LocalizedText.SetKey(key)` and falls back to the raw key; sprite lookup by name, missing/empty → image object inactive (existing behaviour).
- Text-only rows and rows with a sprite share the layout the card already has (VerticalLayoutGroup + ContentSizeFitter); no new layout work.

### 3.3a Polish — the card moves like the rest of the game (Cesar 2026-09-09: "take advantage of our polish pass")

Verified against the repo 2026-09-09: `ProTipCard` predates `game_polish` and has none of its atoms — the tip change is a hand-rolled linear alpha loop on the TEXT only (`CrossfadeToTip`, `Time.deltaTime`, no easing), the image and card height just snap, and the card GameObject (`ProTipCard`, ShellScene fileID `2072892868`: ContentSizeFitter / VerticalLayoutGroup / LayoutElement / Image / ProTipCard) carries **no `ButtonPressFeedback`** — Rule 11's sweep (`game_polish_c`, `PressFeedbackCoverageTests`) only counts `Button`s and this is an `IPointerClickHandler`, so a tap to advance gives no press pulse today. Fix all of it with the shared atoms; no new motion code.

- **Tip swap = one `UiMotion.Fade` out → rebind → one `UiMotion.Fade` in on a `CanvasGroup` that covers text AND image** (`Assets/Scripts/UI/Polish/UiMotion.cs`: `Fade(CanvasGroup, from, to, dur = FadeDur)`, ease-out, unscaled time, settles on stop via the `Register` contract). Put the `CanvasGroup` on a new `TipContent` wrapper holding `TipText` + `TipImage` (or on each of the two and fade both — implementer's call, report which). Delete `CrossfadeToTip` and `_tipTextCanvasGroup`; hold the handle in `Coroutine? _swap` driven by `UiMotion.Run(this, ref _swap, …)` / `UiMotion.Stop(this, ref _swap)` in `OnDisable`, and use `UiMotion.Then(fadeOut, () => { Show(next); … })` to sequence the rebind between the two fades. `textFadeDuration` field is removed — `UiMotion.FadeDur` (0.15 s) is the one duration.
- **Height change eases, never snaps.** A tip with a 628 px image following one with a 300 px image today jumps the card in one frame. After the rebind, `LayoutRebuilder.ForceRebuildLayoutImmediate` as now, then tween the card's `LayoutElement.preferredHeight` (or the wrapper's) from the old height to the new one with `UiMotion.Tween(from, to, EntryDur, h => …, Ease.OutCubic)` while the fade-in runs. NOTE: check `ContentSizeFitter` vs a driven `LayoutElement` — driving `preferredHeight` while the fitter is on will fight; the implementer disables the fitter's vertical fit on the card and drives `preferredHeight` from the measured value, and quotes the measured before/after heights in the report.
- **Tap = press pulse.** Add `Golfin.UI.Polish.ButtonPressFeedback` (`Assets/Scripts/UI/Polish/ButtonPressFeedback.cs`, `IPointerDownHandler`, works without a `Button`) to the `ProTipCard` GameObject with the defaults (0.95 / 0.12 s). Then advance on `OnPointerClick` as now. Add the object to `PressFeedbackCoverageTests`' expectations so the tripwire covers it going forward (it is an `IPointerClickHandler`, not a `Button` — the test's enumeration needs to include it explicitly; say how in the report).
- **First tip on show = `UiMotion.Rise`** (`Rise(RectTransform, CanvasGroup?, dy = RiseDy 16, dur = EntryDur 0.25)`) on the card root when the loading screen enables, gated by `GpsPaintMotion.SuppressedByPush` like every other arrival (`Assets/Scripts/UI/Polish/PaintMotion.cs`) — the loading screen is not a pushed screen, so this is a plain `Rise`; if `LoadingScreenController` ever becomes a push target the guard is already there.
- **"TAP FOR NEXT TIP" pulses** (Cesar 2026-09-09). `tapNextText` gets a `CanvasGroup`; on every `Show` and after every tap, arm `UiMotion.Run(this, ref _tapPulse, UiMotion.Pulse(tapGroup, min: 0.55f, max: 1f, cycles: 1, dur: PulseDur))` **looped** exactly the way `DailyMissionPillController.StartGlow` chains single-cycle `Pulse` sweeps (`Assets/Scripts/UI/Home/DailyMissionPillController.cs` ~417–455) — copy that loop, do not write a new one. Rest alpha 0.55 → peak 1.0 on the Home pill's sine; a tap restarts the loop (so the label visibly answers the press); `UiMotion.Stop` in `OnDisable`. NOTE: read `PulseDur` from `UiMotion` and quote it; if the pill uses its own `glowPeriod` field, mirror it as `[SerializeField] float tapPulsePeriod` with the same default.
- **Motion kill switch** honoured for free: every atom checks `UiMotion.Enabled`.

Evidence the report owes: a frame strip (every 2 frames, 0.5 s) of one tip swap showing text + image fading together and the height easing; a scale trace of the tap pulse peaking < 1.0; `UiMotion*` and `ScrollFeelTests` still green; `PressFeedbackCoverageTests` now enumerates `ProTipCard`.

### 3.4 Persistence — `LoadingTipStore`

`PlayerPrefs` key `loadingtips.state`, JSON via `JsonUtility` — `firstPass`, `firstIndex`, `recentKeys[≤5]` (Architect default; SaveData would tie a cosmetic counter to the account and a reinstall should restart the tutorial anyway — flag if Cesar disagrees). Save after every `Advance()`, load in `Initialize()`. Corrupt/missing → default state.

### 3.5 Strings — through the two-way importer (standing rule 2026-08-28)

- Edit `Assets/Localization/LocalizationText.csv`: rewrite rows `TIP_SWING`, `TIP_ACCURACY`, `TIP_VIEW`, `TIP_CLUB`, `TIP_FORECAST`, `TIP_RARITIES` (EN + JA per §2.2, `<color=#EEDC9A>` around every `[..]` span); add `TIP_GRADES`, `TIP_CONTROLS` and rows 9–34; set `TIP_TIMING`'s fourth column to `false`. Note the two double-spaces in today's `TIP_RARITIES` / `TIP_SWING` rows and the stray `</color>` in `TIP_RARITIES` — fix in passing.
- `python3 Tools/content/import_content.py --env-file … --catalogs texts` → read the verdicts → `--apply` → publish `texts` from the admin → `export_content.py --check` clean. CONFLICTS = stop and report. `LocalizationTextTable.asset` regenerates on build.

### 3.6 Art — one export per tip, from Figma, all 34

- Export each authored component's `Authored diagram` frame (ids in §2.2) at **1×** (806 px wide, transparent background) as `Assets/Art/LoadingScreen/Tip_<NAME>.png` where NAME is the key without its `TIP_` prefix, e.g. `TIP_SWING` → `Tip_SWING.png`. Import settings as the existing `Tip *.png` (Sprite (2D and UI), no mipmaps, ASTC on device). The panels' blue fill is baked into the export; the card behind them is unchanged.
- **Delete the old set**: `Tip Swing`, `Tip Club`, `Tip Forecast`, `Tip Rarities`, `Tip Accuracy`, `Tip Timing`, `Tip View`, `Tip Leaderboard` (+ `.meta`) — every image is replaced, including the three that were still accurate (their authored versions supersede the 2025 composed art).
- No in-game captures and no text-only rows: every `LoadingTips.csv` row carries a sprite from day one.
- Figma stays the source: a copy or art change is made on the authored component and re-exported; nothing is hand-edited in the PNG.

### 3.7 Scene wiring (ShellScene `ProTipCard`, object `ProTipCard`, fileID `2072892875`)

`tipsCsv` → `LoadingTips.csv`; `tipSprites` → 34 entries, one per `Assets/Art/LoadingScreen/Tip_<KEY>.png`, `name` = file name without extension (= the CSV `sprite` cell). The seven old sprite GUIDs on the component are dropped. Everything else on the object is untouched.

## Architecture context

- **Asmdef:** `Assembly-CSharp` only (`ProTipCard`, `LoadingScreenController`, `LocalizedText` all live there). No new asmdef.
- **Existing code referenced:** `Assets/Scripts/UI/ProTipCard.cs` (`Initialize`, `ShowTip`, `NextTip`, `CrossfadeToTip`, `OnPointerClick`, `RestartAutoCycle`), `Assets/Scripts/UI/Polish/UiMotion.cs` (`Fade`, `Rise`, `Tween`, `Run`, `Stop`, `Then`, `Enabled`, `FadeDur`/`EntryDur`/`RiseDy`), `Assets/Scripts/UI/Polish/ButtonPressFeedback.cs`, `Assets/Scripts/UI/Polish/PaintMotion.cs` (`SuppressedByPush`), `Assets/Scripts/UI/Home/DailyMissionPillController.cs` (`StartGlow` — the looped `Pulse` pattern), `Assets/Tests/EditMode/PressFeedbackCoverageTests.cs`, `Assets/Scripts/UI/LoadingScreenController.cs` (untouched — it does not know the card exists), `LocalizedText.SetKey`, `Tools/content/catalogs.py` `COMMENT_PREFIX` convention.
- **Not touched:** `LoadingScreenController`, `GameplaySceneLoader`, `ScreenManager`, `ContentCatalogs`, the admin dashboard.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] `LoadingTipSequencerTests` (EditMode, `Assets/Tests/EditMode/`): fresh state walks the 8 first-pool keys in order twice (16 advances) then draws from the general pool; over 2 000 seeded advances no key repeats within any window of 6 consecutive draws; with only 3 active rows the draw still never yields an empty candidate set; an `active=0` first-pool row is never shown; a persisted `firstIndex` past the end clamps; an empty general pool yields a null-safe `Current`; `recentKeys` round-trips through `LoadingTipStore` (save → load → same ring).
- [ ] Different tips each run: boot → Home → hole load → quit → hole load: the three loading screens opened on three different tips (state file quoted after each); a cold boot on a cleared `PlayerPrefs` opens on `TIP_SWING` (not `TIP_ACCURACY` — the double-advance guard).
- [ ] "TAP FOR NEXT TIP" alpha trace: sine between 0.55 and 1.0 while idle, restarts on tap, settles at 0.55 on disable.
- [ ] `LoadingTipCatalogTests`: parses the shipped CSV to 34 rows, 8 `first`, 2 `active=0`, every row's `sprite` is `Tip_` + key-without-prefix and matches a `tipSprites` entry (test enumerates `Assets/Art/LoadingScreen/Tip_*.png` — exactly 34).
- [ ] Editor run: Logo → Splash → Loading shows `TIP_SWING` on a cleared `PlayerPrefs`; after three more advances (loads or taps) the state file reads `firstIndex 3`; after 16 advances the tip is not from the first-pool order.
- [ ] Hole load (`GameplaySceneLoader` path) opens on the NEXT tip from the persisted position, not tip 1 and not the tip the previous screen opened on.
- [ ] JA device language: every row renders Japanese (no raw keys) — screenshot two.
- [ ] `export_content.py --check` clean for `texts`; zero new hardcoded `.text` literals (grep quoted).
- [ ] `TIP_TIMING` row is `false` in the CSV; the eight old `Tip *.png` gone; no scene reference to any of their GUIDs (e.g. `02879563e1f454841bb4c79963031842`).
- [ ] §3.3a: tip swap fades text AND image through `UiMotion.Fade` (frame strip); card height eases via `UiMotion.Tween` (before/after heights quoted, no one-frame jump); `ButtonPressFeedback` on `ProTipCard` (scale trace); `Rise` on show; `CrossfadeToTip` and `textFadeDuration` gone; `PressFeedbackCoverageTests` enumerates the card.
- [ ] Unity Console clean on the loading screen in both target modes (`LegacyBootHome`, `HoleLoad`).
- [ ] Spec deviations flagged at the bottom of the report.

## Out of scope (filed as Notion `Deferred` rows by the Architect)

- `loading_tips` as a server content catalog (migration + `ContentCatalogs.Data` applier + admin panel with pool/order/active editing).
- Per-tip "seen" analytics / telemetry.
- Contextual pools (e.g. show scheme tips only for the selected scheme, GPS tips only after the golf profile exists).
- The element-crop variants in Figma section `Screenshot — NOT CHOSEN`: deleted at close-out, nothing exported from them.

## Files / hierarchy this task touches

- `Assets/Resources/Data/LoadingTips.csv` (new)
- `Assets/Scripts/UI/LoadingTipCatalog.cs`, `LoadingTipSequencer.cs`, `LoadingTipStore.cs` (new)
- `Assets/Scripts/UI/ProTipCard.cs` (edit)
- `Assets/Tests/EditMode/LoadingTipSequencerTests.cs`, `LoadingTipCatalogTests.cs` (new); `PressFeedbackCoverageTests.cs` (edit)
- `Assets/Localization/LocalizationText.csv` (rows per §3.5)
- `Assets/Art/LoadingScreen/` (34 new `Tip_<NAME>.png` per §3.6; the 8 old `Tip *.png` deleted)
- `Assets/Scenes/ShellScene.unity` (`ProTipCard` object: component fields, `ButtonPressFeedback`, the `TipContent` CanvasGroup wrapper)
