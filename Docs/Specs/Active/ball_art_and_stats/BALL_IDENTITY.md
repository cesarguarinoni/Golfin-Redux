# Ball Identity & Stat Sheet — **APPROVED 2026-08-31** (Cesar, via the decision round; wind-ball trims per his "go with your recommendation")

> Companion to `SPEC.md`. The **Look** column describes the existing 1000×1000 base sprite, which is the ground truth (ART WINS). The stat columns are the numbers that ship in `Balls.csv`; the arithmetic is shown below and was checked by script (`Docs/Specs/Active/ball_art_and_stats/reference/build_docs.py`).

**Decisions this table depends on (both taken 2026-08-31):** ball rarity lives in a new `rarity` column on `Balls.csv`; `BallWindCutPerPoint` goes 0.01 → **0.02**, so +10 wind buys 0.20 of the 0.30 cap. The three wind balls (Klyro, Fyloe Aim, Soralis) were trimmed a touch on their other positives to pay for the doubled stat — net budget unchanged.

Stat order matches the CSV: `power, rebound, windResistance, roll, spin`. Range −10..+10. Budget by tier: Common 0 · Uncommon +3 · Rare +5 · Mythic +7 · Legendary +9.

**Sign traps, repeated because they are easy to get backwards:** `+Roll` = rolls FARTHER (fairway *and* green) → a ball that stops on greens needs **negative** Roll. `+Rebound` = livelier bounce. `+Wind` = cuts through wind. Rebound and Roll saturate their clamps exactly at ±10.

## Tier spread

Common 5 · Uncommon 6 · Rare 5 · Mythic 3 · Legendary 1 — a gacha-shaped curve, not a flat one.

## The table

| Ball | id | Tier | Pwr | Reb | Wind | Roll | Spin | Net | |x| | Look (the existing base sprite) | Identity in one line |
|---|---|---|--:|--:|--:|--:|--:|--:|--:|---|---|
| **Golfin** | `ball_golfin` | Common | 0 | 0 | 0 | 0 | 0 | +0 | 0 | white ball, large green gradient "G" | The default. Every player owns it; it is granted for every "a ball" reward. Never a prize, never a shop row. **Ships — do not touch.** |
| **Par Perfect** | `ball_par_perfect` | Common | 0 | +3 | +3 | -3 | -3 | +0 | 12 | white, pink + navy vertical stripes down the left flank, "PAR" over "PERFECT" in blue italic | The metronome. Predictable bounce and steady in wind; it will not run out and it will not bite. |
| **Fyloe Soft** | `ball_fyloe_soft` | Common | -3 | -2 | 0 | -3 | +8 | +0 | 16 | white, magenta band round the equator with "FYLOE" repeated, bright green stripe above and below | Soft feel, big spin, checks up fast. Gives away distance to do it. |
| **Ace Attire** | `ball_ace_attire` | Common | +6 | +2 | -4 | +2 | -6 | +0 | 20 | red ball, golden-yellow equator band edged white, "ATTIRE" over "ACE" sharing one big red A (monogram) | All swagger. Long and lively, hopeless in wind, no shot-shaping at all. |
| **Birdie V1** | `ball_birdie_v1` | Common | +3 | 0 | +3 | 0 | -6 | +0 | 12 | off-white cream, small red crossed-flags mark, "BIRDIE" in black serif | The starter distance ball. Goes far enough, holds a line, has no feel whatsoever. |
| **Golfin MK2** | `ball_golfin_mk2` | Uncommon | +4 | +3 | 0 | +2 | -6 | +3 | 15 | white, one large bold green-gradient "G", heavier than the standard Golfin's | The house ball's distance evolution. More of everything the default has, minus the spin. |
| **G&F** | `ball_gf` | Uncommon | +2 | +4 | +4 | -3 | -4 | +3 | 17 | black, red-and-white double pinstripes, "G&F" in white serif, small red comma mark | Tour heritage. Rock solid off the bounce and through wind, modest power, settles rather than runs. |
| **Tifto** | `ball_tifto` | Uncommon | +1 | +6 | 0 | +3 | -7 | +3 | 17 | off-white, cyan concentric-ripple mark, tiny "VOIGT94" beneath | Built like a tool. Hard, lively, runs a long way, and will not stop for anybody. |
| **Fairloft** | `ball_fairloft` | Uncommon | -2 | -3 | +5 | -4 | +7 | +3 | 21 | white, black lozenge badge "FAIRLOFT / JAPAN", two thin black lines crossing behind it | High and soft. Cuts wind on the way up, lands quiet, checks hard. Costs power and bounce. |
| **Fyloe Aim** | `ball_fyloe_aim` | Uncommon | 0 | +1 | +7 | 0 | -5 | +3 | 13 | white, red crosshair ring with four ticks and a small F at the centre | Point and shoot. The wind specialist at Uncommon — holds its line and refuses to curve. (Trimmed for the 0.02 wind coefficient: Pwr +1→0, Roll +1→0, Spin −7→−5.) |
| **Clover Pro** | `ball_clover_pro` | Uncommon | +2 | +2 | +2 | +2 | -5 | +3 | 13 | mint/teal, large white four-petal clover, green disc at the centre with a white swinging-golfer silhouette (ART WINS — the disc is a golfer, not a swirl) | The pro-shop staple. Mildly good at everything except making the ball talk. |
| **GolfinIX** | `ball_golfinix` | Rare | +2 | -2 | +3 | -6 | +8 | +5 | 21 | off-white, black rectangular badge, "GOLFIN" in white with "IX" in orange | The boutique touch ball. Sits down where it lands and spins hard; you give up the roll to get it. |
| **Klyro** | `ball_klyro` | Rare | +1 | +2 | +7 | -2 | -3 | +5 | 15 | navy, "KLYRO" repeated four times around a ring, white/light-blue triangle mark at the centre | Technical and unbothered. The controlled wind ball — steady bounce, checks slightly, not spinny. (Trimmed for 0.02 wind: Pwr +2→+1, Reb +3→+2, Roll −3→−2, Spin −4→−3.) |
| **Royal Swing** | `ball_royal_swing` | Rare | +8 | +3 | 0 | +2 | -8 | +5 | 21 | bright orange, black equator band edged white, "ROYAL" in orange and "SWING" in white | Aggressive distance. Hits hard, bounces, runs out, and does not shape or stop. |
| **Fairway THREADS** | `ball_fairway_threads` | Rare | +4 | +4 | +2 | +2 | -7 | +5 | 19 | black, vertical white stripe with red inner stripe, blue-outlined cream round badge, "Fairway" red script over "THREADS" | Looks expensive, plays long and lively. Every stat except bite. |
| **Putt Ace** | `ball_putt_ace` | Rare | **+10** | -6 | 0 | +5 | -4 | +5 | 25 | yellow, black "PUTT ACE" over two lines, black four-point sparkles | Maximum power, dead bounce, long roll. **Ships — legal under the budget, no retune.** |
| **MireO** | `ball_mireo` | Mythic | -3 | -2 | +4 | -2 | **+10** | +7 | 21 | black, gold Greek-key bands top and bottom, gold Greek-key medallion with "MireO" in black script and two sparkles | The craftsman's spin ball. Maxed spin, lands soft, holds greens — bought with distance. |
| **Cirq** | `ball_cirq` | Mythic | +6 | +7 | -4 | +3 | -5 | +7 | 25 | bright royal blue, "CIRQ" in white/cyan outline over grey "GOLF", yellow spiral mark | Explosive off the face and off the bounce. Genuinely wild once the wind gets up. |
| **Soralis** | `ball_soralis` | Mythic | +3 | +1 | **+10** | 0 | -7 | +7 | 21 | navy-teal, lime-yellow equator band with "SORALIS" in white, lime crescent/eclipse mark | The wind ball. Goes where you aimed regardless of the flag, and cannot be shaped at all. (Trimmed for 0.02 wind: Pwr +4→+3, Reb +2→+1, Spin −9→−7.) |
| **Shimmer G** | `ball_shimmer_g` | Legendary | +7 | +5 | +5 | +2 | **-10** | +9 | 29 | iridescent oil-slick rainbow surface, white disc at the centre with a grey G | The trophy ball. Everything a ball can do except stop or curve. |

### Arithmetic check (per ball)

- `ball_golfin` (Common, budget +0): (+0) + (+0) + (+0) + (+0) + (+0) = **+0** ✓ · |x| = 0 · worst negative +0
- `ball_par_perfect` (Common, budget +0): (+0) + (+3) + (+3) + (-3) + (-3) = **+0** ✓ · |x| = 12 · worst negative -3
- `ball_fyloe_soft` (Common, budget +0): (-3) + (-2) + (+0) + (-3) + (+8) = **+0** ✓ · |x| = 16 · worst negative -3
- `ball_ace_attire` (Common, budget +0): (+6) + (+2) + (-4) + (+2) + (-6) = **+0** ✓ · |x| = 20 · worst negative -6
- `ball_birdie_v1` (Common, budget +0): (+3) + (+0) + (+3) + (+0) + (-6) = **+0** ✓ · |x| = 12 · worst negative -6
- `ball_golfin_mk2` (Uncommon, budget +3): (+4) + (+3) + (+0) + (+2) + (-6) = **+3** ✓ · |x| = 15 · worst negative -6
- `ball_gf` (Uncommon, budget +3): (+2) + (+4) + (+4) + (-3) + (-4) = **+3** ✓ · |x| = 17 · worst negative -4
- `ball_tifto` (Uncommon, budget +3): (+1) + (+6) + (+0) + (+3) + (-7) = **+3** ✓ · |x| = 17 · worst negative -7
- `ball_fairloft` (Uncommon, budget +3): (-2) + (-3) + (+5) + (-4) + (+7) = **+3** ✓ · |x| = 21 · worst negative -4
- `ball_fyloe_aim` (Uncommon, budget +3): (+0) + (+1) + (+7) + (+0) + (-5) = **+3** ✓ · |x| = 13 · worst negative -5
- `ball_clover_pro` (Uncommon, budget +3): (+2) + (+2) + (+2) + (+2) + (-5) = **+3** ✓ · |x| = 13 · worst negative -5
- `ball_golfinix` (Rare, budget +5): (+2) + (-2) + (+3) + (-6) + (+8) = **+5** ✓ · |x| = 21 · worst negative -6
- `ball_klyro` (Rare, budget +5): (+1) + (+2) + (+7) + (-2) + (-3) = **+5** ✓ · |x| = 15 · worst negative -3
- `ball_royal_swing` (Rare, budget +5): (+8) + (+3) + (+0) + (+2) + (-8) = **+5** ✓ · |x| = 21 · worst negative -8
- `ball_fairway_threads` (Rare, budget +5): (+4) + (+4) + (+2) + (+2) + (-7) = **+5** ✓ · |x| = 19 · worst negative -7
- `ball_putt_ace` (Rare, budget +5): (+10) + (-6) + (+0) + (+5) + (-4) = **+5** ✓ · |x| = 25 · worst negative -6
- `ball_mireo` (Mythic, budget +7): (-3) + (-2) + (+4) + (-2) + (+10) = **+7** ✓ · |x| = 21 · worst negative -3
- `ball_cirq` (Mythic, budget +7): (+6) + (+7) + (-4) + (+3) + (-5) = **+7** ✓ · |x| = 25 · worst negative -5
- `ball_soralis` (Mythic, budget +7): (+3) + (+1) + (+10) + (+0) + (-7) = **+7** ✓ · |x| = 21 · worst negative -7
- `ball_shimmer_g` (Legendary, budget +9): (+7) + (+5) + (+5) + (+2) + (-10) = **+9** ✓ · |x| = 29 · worst negative -10

Rules 1–6 of SPEC §4.3 hold for all 20: every stat in −10..+10; every ball except `ball_golfin` carries a −3 or worse; four balls use a ±10 (Putt Ace Pwr, MireO Spin, Soralis Wind, Shimmer G Spin), all Rare or above, none uses two; |x| ≥ 8 for everything above Common; `ball_golfin` is `0,0,0,0,0` / `isDefault=true`; every line was written against its art.

### Deliberate shape of the set

- **Spin is the currency.** Fourteen of the twenty pay for their upside in spin, because spin is the stat a distance-hungry player misses least and a scoring player misses most.
- **The three spin balls are kept distinct.** Fyloe Soft is soft-feel spin at Common; Fairloft is wind-and-landing at Uncommon; MireO is maxed artisan spin at Mythic. GolfinIX is the roll-killer.
- **Wind is now a real stat** (0.02/pt). Klyro, Fyloe Aim and Soralis are the wind lane and were made *more* wind-specialised, not stronger overall: their other positives came down and their negatives softened by the same amount, so each stays exactly on budget.
- **Nothing is strictly better than the default.** Golfin is flat zero, so every other ball is a trade, which is what keeps a consumable stat item from being a pay-to-win lane.

## Blurbs (D4) — EN + JA, house style: what the brand is known for, then what the ball actually does, naming the trade-off

Each claim below was checked against the sign of its stats (negative Roll = checks up / settles; positive Roll = runs out). The same text ships as `Balls.csv` `info` (EN) and as `BALL_INFO_<ID>` in `LocalizationText.csv` (EN + JA) — see `Docs/Specs/Active/ball_data_wiring/SPEC.md`.

- **Golfin** (`BALL_INFO_GOLFIN`) — ships as-is; no change.
- **Par Perfect** (`BALL_INFO_PAR_PERFECT`)
  - EN: PAR PERFECT builds the metronome of golf balls—predictable above everything else. A steady bounce and a little wind resistance, but it checks up short and won't spin much for you either.
  - JA: PAR PERFECTはメトロノームのような安定性を追求するブランド。跳ね方は読みやすく風にも少し強いが、ランは短めでスピンも控えめ。
- **Fyloe Soft** (`BALL_INFO_FYLOE_SOFT`)
  - EN: FYLOE's Soft line is all about feel—a soft cover that grabs the face and spins hard. It checks up fast on the green, but you give away distance and a little liveliness off the bounce to get it.
  - JA: FYLOEのSoftシリーズはフィーリング重視。柔らかいカバーがフェースに食いつき、強烈なスピンを生む。グリーンでは素早く止まるが、飛距離と跳ねの勢いは犠牲になる。
- **Ace Attire** (`BALL_INFO_ACE_ATTIRE`)
  - EN: ACE ATTIRE is a fashion house first and a ball maker second—this one is all swagger. Long and lively with plenty of run-out, but it gets shoved around in the wind and barely spins at all.
  - JA: ACE ATTIREは本業がファッションブランドで、このボールも見た目通りの派手さ。よく飛びよく跳ねよく転がるが、風にはめっぽう弱く、スピンはほとんどかからない。
- **Birdie V1** (`BALL_INFO_BIRDIE_V1`)
  - EN: BIRDIE's V1 is the entry-level distance ball found in every pro-shop basket. It goes a little farther and holds its line in a breeze, but there is almost no feel—don't expect it to check or shape.
  - JA: BIRDIE V1はどのプロショップにも並ぶ入門用のディスタンスボール。少し飛び、そよ風程度なら直進性も保つが、フィーリングは皆無で、止めることも曲げることも苦手。
- **Golfin MK2** (`BALL_INFO_GOLFIN_MK2`)
  - EN: Golfin's MK2 is the house ball's distance evolution. More power, a livelier bounce and a touch more roll than the standard Golfin—paid for almost entirely in spin.
  - JA: Golfin MK2は標準Golfinボールのディスタンス進化版。パワーと跳ねが増し、ランも少し伸びるが、その代償はほぼすべてスピンで支払っている。
- **G&F** (`BALL_INFO_GF`)
  - EN: G&F has been making tour balls since before most of these brands existed. Rock-solid off the bounce and unbothered by wind, with modest power; it settles where it lands rather than running out, and it isn't a big spinner.
  - JA: G&Fは他のブランドが生まれる前からツアーボールを作り続けてきた老舗。跳ねは安定し風にも強く、パワーは控えめ。落ちた場所に収まるタイプで、スピンは多くない。
- **Tifto** (`BALL_INFO_TIFTO`)
  - EN: TIFTO makes golf balls the way it makes tools—hard, functional, no frills. This one is lively off the bounce and runs a long way after landing, but it will not stop for anybody and has almost no spin.
  - JA: TIFTOは工具を作るようにゴルフボールを作るブランド。硬く機能的で飾り気がない。跳ねは強くランも長いが、止まってはくれず、スピンもほとんどかからない。
- **Fairloft** (`BALL_INFO_FAIRLOFT`)
  - EN: FAIRLOFT's Japanese engineers chase a high, soft ball flight. It cuts through the wind, lands quietly and checks hard with plenty of spin—at the cost of a little power and a dead bounce.
  - JA: 日本のFAIRLOFTが追い求めるのは高くて柔らかい弾道。風を切り裂き、静かに着地して強いスピンでピタリと止まるが、パワーと跳ねの勢いは少し失われる。
- **Fyloe Aim** (`BALL_INFO_FYLOE_AIM`)
  - EN: FYLOE's Aim is a point-and-shoot ball: it holds its line through the wind better than almost anything at this price. The flip side is that it refuses to curve—spin is not on the menu.
  - JA: FYLOE Aimは狙って打つだけのボール。この価格帯では随一の耐風性能で、狙った線を保つ。その裏返しとしてほとんど曲がらず、スピンは期待できない。
- **Clover Pro** (`BALL_INFO_CLOVER_PRO`)
  - EN: CLOVER PRO is the pro-shop staple with the lucky four-leaf mark. Mildly better than the default at power, bounce, wind and roll, and mildly worse at the one thing that makes a ball talk: spin.
  - JA: CLOVER PROは幸運の四つ葉マークでおなじみのプロショップ定番。パワー・跳ね・耐風・ランのすべてが標準をわずかに上回り、唯一スピンだけがはっきり劣る。
- **GolfinIX** (`BALL_INFO_GOLFINIX`)
  - EN: GolfinIX is Golfin's boutique touch ball, built for scoring rather than showing off. It sits down where it lands and spins hard, with a little help in the wind; you give up the roll and a touch of bounce to get it.
  - JA: GolfinIXはGolfinのブティックライン。飛ばすためではなく、スコアを作るためのボール。落ちた場所に座り込むほどのスピンと少しの耐風性を持つ代わりに、ランと跳ねの勢いを手放している。
- **Klyro** (`BALL_INFO_KLYRO`)
  - EN: KLYRO makes technical gear for people who read wind charts for fun. This is the controlled wind ball—it holds its line in a gale with a steady bounce and checks up slightly; the price is a little spin.
  - JA: KLYROは風向図を読むのが趣味という人向けのテクニカルブランド。強風でも狙った線を保ち、跳ねも安定、ランは少し短め。その代わりスピンは少し犠牲になる。
- **Royal Swing** (`BALL_INFO_ROYAL_SWING`)
  - EN: ROYAL SWING sells aggression, and this ball is the loudest thing in its catalogue. It hits hard, bounces lively and runs out after landing—but it will not shape a shot or stop on a green.
  - JA: ROYAL SWINGが売るのは攻撃性で、このボールはそのカタログで最も騒がしい一品。強く飛び、よく跳ね、着地後も転がるが、球筋を曲げることもグリーンで止めることもできない。
- **Fairway THREADS** (`BALL_INFO_FAIRWAY_THREADS`)
  - EN: Fairway THREADS is a lifestyle label that happens to make a very good ball. Long, lively and a little steadier in wind, with a touch more run—every stat but bite, because it barely spins.
  - JA: Fairway THREADSはライフスタイル系ブランドだが、ボールの出来は本物。飛んで、跳ねて、風にもやや強く、ランも少し伸びる。足りないのは食いつきだけで、スピンはほとんどかからない。
- **Putt Ace** (`BALL_INFO_PUTT_ACE`) — ships as-is; no change.
- **MireO** (`BALL_INFO_MIREO`)
  - EN: MireO is the craftsman's brand, and this is its masterpiece spin ball. Maximum spin, a quiet landing and a bit of help in the wind—bought with distance, a softer bounce and a short roll.
  - JA: MireOは職人のブランド。その最高傑作がこのスピンボールだ。最大スピンと静かな着地、少しの耐風性を、飛距離と跳ねの勢い、そして短いランと引き換えに手に入れている。
- **Cirq** (`BALL_INFO_CIRQ`)
  - EN: CIRQ builds balls for showmen—explosive off the face and just as explosive off the bounce. It runs out hard, gets genuinely wild once the wind picks up, and offers little spin control.
  - JA: CIRQはショーマンのためのボールを作る。フェースを離れた瞬間も跳ねた瞬間も爆発的で、ランも長い。ただし風が出ると本当に暴れ、スピンで抑えることも難しい。
- **Soralis** (`BALL_INFO_SORALIS`)
  - EN: SORALIS is the eclipse brand, and this is the wind ball. It goes where you aimed no matter what the flag is doing, with a little extra power—but it cannot be shaped at all.
  - JA: SORALISは日食をシンボルにしたブランド。そしてこれは風のためのボール。旗がどう揺れていようと狙った場所へ飛び、パワーも少し上乗せ。ただし球筋を曲げることはまったくできない。
- **Shimmer G** (`BALL_INFO_SHIMMER_G`)
  - EN: Shimmer G is the trophy ball—an oil-slick finish that exists to be noticed. Long, lively, steady in wind and it keeps rolling; it does everything a ball can do except stop or curve.
  - JA: Shimmer Gはトロフィーのようなボール。オイルスリックの輝きは見せびらかすためにある。飛んで、跳ねて、風に強く、よく転がる。止まることと曲がること以外、ボールにできることはすべてこなす。
