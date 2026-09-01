import csv, io, json

# id, name, brand, tier, pwr, reb, wind, roll, spin, fullSprite, thumbnailSprite, token
BALLS = [
 ("ball_golfin",          "Golfin",          "Golfin",          "Common",    0, 0, 0, 0, 0,  "Golfin",         "Golfin",                     "GOLFIN"),
 ("ball_par_perfect",     "Par Perfect",     "PAR PERFECT",     "Common",    0, 3, 3,-3,-3,  "ParPerfect",     "S_Controls_Ball_PARPERFECT", "PARPERFECT"),
 ("ball_fyloe_soft",      "Fyloe Soft",      "FYLOE",           "Common",   -3,-2, 0,-3, 8,  "FyloeSoft",      "S_Controls_Ball_FYLOESOFT",  "FYLOESOFT"),
 ("ball_ace_attire",      "Ace Attire",      "ACE ATTIRE",      "Common",    6, 2,-4, 2,-6,  "AceAttire",      "S_Controls_Ball_ACEATTIRE",  "ACEATTIRE"),
 ("ball_birdie_v1",       "Birdie V1",       "BIRDIE",          "Common",    3, 0, 3, 0,-6,  "BirdieV1",       "S_Controls_Ball_BIRDIEV1",   "BIRDIEV1"),
 ("ball_golfin_mk2",      "Golfin MK2",      "Golfin",          "Uncommon",  4, 3, 0, 2,-6,  "GolfinMK2",      "S_Controls_Ball_GOLFINMK2",  "GOLFINMK2"),
 ("ball_gf",              "G&F",             "G&F",             "Uncommon",  2, 4, 4,-3,-4,  "GF",             "S_Controls_Ball_GF",         "GF"),
 ("ball_tifto",           "Tifto",           "TIFTO",           "Uncommon",  1, 6, 0, 3,-7,  "Tifto",          "S_Controls_Ball_TIFTO",      "TIFTO"),
 ("ball_fairloft",        "Fairloft",        "FAIRLOFT",        "Uncommon", -2,-3, 5,-4, 7,  "Fairloft",       "S_Controls_Ball_FAIRLOFT",   "FAIRLOFT"),
 ("ball_fyloe_aim",       "Fyloe Aim",       "FYLOE",           "Uncommon",  0, 1, 7, 0,-5,  "FyloeAim",       "S_Controls_Ball_FYLOEAIM",   "FYLOEAIM"),
 ("ball_clover_pro",      "Clover Pro",      "CLOVER PRO",      "Uncommon",  2, 2, 2, 2,-5,  "CloverPro",      "S_Controls_Ball_CLOVERPRO",  "CLOVERPRO"),
 ("ball_golfinix",        "GolfinIX",        "GOLFINIX",        "Rare",      2,-2, 3,-6, 8,  "GolfinIX",       "S_Controls_Ball_GOLFINIX",   "GOLFINIX"),
 ("ball_klyro",           "Klyro",           "KLYRO",           "Rare",      1, 2, 7,-2,-3,  "Klyro",          "S_Controls_Ball_KLYRO",      "KLYRO"),
 ("ball_royal_swing",     "Royal Swing",     "ROYAL SWING",     "Rare",      8, 3, 0, 2,-8,  "RoyalSwing",     "S_Controls_Ball_ROYAL",      "ROYAL"),
 ("ball_fairway_threads", "Fairway THREADS", "Fairway THREADS", "Rare",      4, 4, 2, 2,-7,  "FairwayThreads", "S_Controls_Ball_FAIRWAY",    "FAIRWAY"),
 ("ball_putt_ace",        "Putt Ace",        "Putt Ace",        "Rare",     10,-6, 0, 5,-4,  "PuttAce",        "PuttAce",                    "PUTTACE"),
 ("ball_mireo",           "MireO",           "MireO",           "Mythic",   -3,-2, 4,-2,10,  "MireO",          "S_Controls_Ball_MIREO",      "MIREO"),
 ("ball_cirq",            "Cirq",            "CIRQ",            "Mythic",    6, 7,-4, 3,-5,  "Cirq",           "S_Controls_Ball_CIRQ",       "CIRQ"),
 ("ball_soralis",         "Soralis",         "SORALIS",         "Mythic",    3, 1,10, 0,-7,  "Soralis",        "S_Controls_Ball_SORALIS",    "SORALIS"),
 ("ball_shimmer_g",       "Shimmer G",       "SHIMMER G",       "Legendary", 7, 5, 5, 2,-10, "ShimmerG",       "S_Controls_Ball_SHIMMERG",   "SHIMMERG"),
]
BUDGET = {"Common":0,"Uncommon":3,"Rare":5,"Mythic":7,"Legendary":9}

LOOK = {
 "ball_golfin":"white ball, large green gradient \"G\"",
 "ball_par_perfect":"white, pink + navy vertical stripes down the left flank, \"PAR\" over \"PERFECT\" in blue italic",
 "ball_fyloe_soft":"white, magenta band round the equator with \"FYLOE\" repeated, bright green stripe above and below",
 "ball_ace_attire":"red ball, golden-yellow equator band edged white, \"ATTIRE\" over \"ACE\" sharing one big red A (monogram)",
 "ball_birdie_v1":"off-white cream, small red crossed-flags mark, \"BIRDIE\" in black serif",
 "ball_golfin_mk2":"white, one large bold green-gradient \"G\", heavier than the standard Golfin's",
 "ball_gf":"black, red-and-white double pinstripes, \"G&F\" in white serif, small red comma mark",
 "ball_tifto":"off-white, cyan concentric-ripple mark, tiny \"VOIGT94\" beneath",
 "ball_fairloft":"white, black lozenge badge \"FAIRLOFT / JAPAN\", two thin black lines crossing behind it",
 "ball_fyloe_aim":"white, red crosshair ring with four ticks and a small F at the centre",
 "ball_clover_pro":"mint/teal, large white four-petal clover, green disc at the centre with a white swinging-golfer silhouette (ART WINS — the disc is a golfer, not a swirl)",
 "ball_golfinix":"off-white, black rectangular badge, \"GOLFIN\" in white with \"IX\" in orange",
 "ball_klyro":"navy, \"KLYRO\" repeated four times around a ring, white/light-blue triangle mark at the centre",
 "ball_royal_swing":"bright orange, black equator band edged white, \"ROYAL\" in orange and \"SWING\" in white",
 "ball_fairway_threads":"black, vertical white stripe with red inner stripe, blue-outlined cream round badge, \"Fairway\" red script over \"THREADS\"",
 "ball_putt_ace":"yellow, black \"PUTT ACE\" over two lines, black four-point sparkles",
 "ball_mireo":"black, gold Greek-key bands top and bottom, gold Greek-key medallion with \"MireO\" in black script and two sparkles",
 "ball_cirq":"bright royal blue, \"CIRQ\" in white/cyan outline over grey \"GOLF\", yellow spiral mark",
 "ball_soralis":"navy-teal, lime-yellow equator band with \"SORALIS\" in white, lime crescent/eclipse mark",
 "ball_shimmer_g":"iridescent oil-slick rainbow surface, white disc at the centre with a grey G",
}

IDENTITY = {
 "ball_golfin":"The default. Every player owns it; it is granted for every \"a ball\" reward. Never a prize, never a shop row. **Ships — do not touch.**",
 "ball_par_perfect":"The metronome. Predictable bounce and steady in wind; it will not run out and it will not bite.",
 "ball_fyloe_soft":"Soft feel, big spin, checks up fast. Gives away distance to do it.",
 "ball_ace_attire":"All swagger. Long and lively, hopeless in wind, no shot-shaping at all.",
 "ball_birdie_v1":"The starter distance ball. Goes far enough, holds a line, has no feel whatsoever.",
 "ball_golfin_mk2":"The house ball's distance evolution. More of everything the default has, minus the spin.",
 "ball_gf":"Tour heritage. Rock solid off the bounce and through wind, modest power, settles rather than runs.",
 "ball_tifto":"Built like a tool. Hard, lively, runs a long way, and will not stop for anybody.",
 "ball_fairloft":"High and soft. Cuts wind on the way up, lands quiet, checks hard. Costs power and bounce.",
 "ball_fyloe_aim":"Point and shoot. The wind specialist at Uncommon — holds its line and refuses to curve. (Trimmed for the 0.02 wind coefficient: Pwr +1→0, Roll +1→0, Spin −7→−5.)",
 "ball_clover_pro":"The pro-shop staple. Mildly good at everything except making the ball talk.",
 "ball_golfinix":"The boutique touch ball. Sits down where it lands and spins hard; you give up the roll to get it.",
 "ball_klyro":"Technical and unbothered. The controlled wind ball — steady bounce, checks slightly, not spinny. (Trimmed for 0.02 wind: Pwr +2→+1, Reb +3→+2, Roll −3→−2, Spin −4→−3.)",
 "ball_royal_swing":"Aggressive distance. Hits hard, bounces, runs out, and does not shape or stop.",
 "ball_fairway_threads":"Looks expensive, plays long and lively. Every stat except bite.",
 "ball_putt_ace":"Maximum power, dead bounce, long roll. **Ships — legal under the budget, no retune.**",
 "ball_mireo":"The craftsman's spin ball. Maxed spin, lands soft, holds greens — bought with distance.",
 "ball_cirq":"Explosive off the face and off the bounce. Genuinely wild once the wind gets up.",
 "ball_soralis":"The wind ball. Goes where you aimed regardless of the flag, and cannot be shaped at all. (Trimmed for 0.02 wind: Pwr +4→+3, Reb +2→+1, Spin −9→−7.)",
 "ball_shimmer_g":"The trophy ball. Everything a ball can do except stop or curve.",
}

BLURB = {
 "ball_par_perfect": (
  "PAR PERFECT builds the metronome of golf balls—predictable above everything else. A steady bounce and a little wind resistance, but it checks up short and won't spin much for you either.",
  "PAR PERFECTはメトロノームのような安定性を追求するブランド。跳ね方は読みやすく風にも少し強いが、ランは短めでスピンも控えめ。"),
 "ball_fyloe_soft": (
  "FYLOE's Soft line is all about feel—a soft cover that grabs the face and spins hard. It checks up fast on the green, but you give away distance and a little liveliness off the bounce to get it.",
  "FYLOEのSoftシリーズはフィーリング重視。柔らかいカバーがフェースに食いつき、強烈なスピンを生む。グリーンでは素早く止まるが、飛距離と跳ねの勢いは犠牲になる。"),
 "ball_ace_attire": (
  "ACE ATTIRE is a fashion house first and a ball maker second—this one is all swagger. Long and lively with plenty of run-out, but it gets shoved around in the wind and barely spins at all.",
  "ACE ATTIREは本業がファッションブランドで、このボールも見た目通りの派手さ。よく飛びよく跳ねよく転がるが、風にはめっぽう弱く、スピンはほとんどかからない。"),
 "ball_birdie_v1": (
  "BIRDIE's V1 is the entry-level distance ball found in every pro-shop basket. It goes a little farther and holds its line in a breeze, but there is almost no feel—don't expect it to check or shape.",
  "BIRDIE V1はどのプロショップにも並ぶ入門用のディスタンスボール。少し飛び、そよ風程度なら直進性も保つが、フィーリングは皆無で、止めることも曲げることも苦手。"),
 "ball_golfin_mk2": (
  "Golfin's MK2 is the house ball's distance evolution. More power, a livelier bounce and a touch more roll than the standard Golfin—paid for almost entirely in spin.",
  "Golfin MK2は標準Golfinボールのディスタンス進化版。パワーと跳ねが増し、ランも少し伸びるが、その代償はほぼすべてスピンで支払っている。"),
 "ball_gf": (
  "G&F has been making tour balls since before most of these brands existed. Rock-solid off the bounce and unbothered by wind, with modest power; it settles where it lands rather than running out, and it isn't a big spinner.",
  "G&Fは他のブランドが生まれる前からツアーボールを作り続けてきた老舗。跳ねは安定し風にも強く、パワーは控えめ。落ちた場所に収まるタイプで、スピンは多くない。"),
 "ball_tifto": (
  "TIFTO makes golf balls the way it makes tools—hard, functional, no frills. This one is lively off the bounce and runs a long way after landing, but it will not stop for anybody and has almost no spin.",
  "TIFTOは工具を作るようにゴルフボールを作るブランド。硬く機能的で飾り気がない。跳ねは強くランも長いが、止まってはくれず、スピンもほとんどかからない。"),
 "ball_fairloft": (
  "FAIRLOFT's Japanese engineers chase a high, soft ball flight. It cuts through the wind, lands quietly and checks hard with plenty of spin—at the cost of a little power and a dead bounce.",
  "日本のFAIRLOFTが追い求めるのは高くて柔らかい弾道。風を切り裂き、静かに着地して強いスピンでピタリと止まるが、パワーと跳ねの勢いは少し失われる。"),
 "ball_fyloe_aim": (
  "FYLOE's Aim is a point-and-shoot ball: it holds its line through the wind better than almost anything at this price. The flip side is that it refuses to curve—spin is not on the menu.",
  "FYLOE Aimは狙って打つだけのボール。この価格帯では随一の耐風性能で、狙った線を保つ。その裏返しとしてほとんど曲がらず、スピンは期待できない。"),
 "ball_clover_pro": (
  "CLOVER PRO is the pro-shop staple with the lucky four-leaf mark. Mildly better than the default at power, bounce, wind and roll, and mildly worse at the one thing that makes a ball talk: spin.",
  "CLOVER PROは幸運の四つ葉マークでおなじみのプロショップ定番。パワー・跳ね・耐風・ランのすべてが標準をわずかに上回り、唯一スピンだけがはっきり劣る。"),
 "ball_golfinix": (
  "GolfinIX is Golfin's boutique touch ball, built for scoring rather than showing off. It sits down where it lands and spins hard, with a little help in the wind; you give up the roll and a touch of bounce to get it.",
  "GolfinIXはGolfinのブティックライン。飛ばすためではなく、スコアを作るためのボール。落ちた場所に座り込むほどのスピンと少しの耐風性を持つ代わりに、ランと跳ねの勢いを手放している。"),
 "ball_klyro": (
  "KLYRO makes technical gear for people who read wind charts for fun. This is the controlled wind ball—it holds its line in a gale with a steady bounce and checks up slightly; the price is a little spin.",
  "KLYROは風向図を読むのが趣味という人向けのテクニカルブランド。強風でも狙った線を保ち、跳ねも安定、ランは少し短め。その代わりスピンは少し犠牲になる。"),
 "ball_royal_swing": (
  "ROYAL SWING sells aggression, and this ball is the loudest thing in its catalogue. It hits hard, bounces lively and runs out after landing—but it will not shape a shot or stop on a green.",
  "ROYAL SWINGが売るのは攻撃性で、このボールはそのカタログで最も騒がしい一品。強く飛び、よく跳ね、着地後も転がるが、球筋を曲げることもグリーンで止めることもできない。"),
 "ball_fairway_threads": (
  "Fairway THREADS is a lifestyle label that happens to make a very good ball. Long, lively and a little steadier in wind, with a touch more run—every stat but bite, because it barely spins.",
  "Fairway THREADSはライフスタイル系ブランドだが、ボールの出来は本物。飛んで、跳ねて、風にもやや強く、ランも少し伸びる。足りないのは食いつきだけで、スピンはほとんどかからない。"),
 "ball_mireo": (
  "MireO is the craftsman's brand, and this is its masterpiece spin ball. Maximum spin, a quiet landing and a bit of help in the wind—bought with distance, a softer bounce and a short roll.",
  "MireOは職人のブランド。その最高傑作がこのスピンボールだ。最大スピンと静かな着地、少しの耐風性を、飛距離と跳ねの勢い、そして短いランと引き換えに手に入れている。"),
 "ball_cirq": (
  "CIRQ builds balls for showmen—explosive off the face and just as explosive off the bounce. It runs out hard, gets genuinely wild once the wind picks up, and offers little spin control.",
  "CIRQはショーマンのためのボールを作る。フェースを離れた瞬間も跳ねた瞬間も爆発的で、ランも長い。ただし風が出ると本当に暴れ、スピンで抑えることも難しい。"),
 "ball_soralis": (
  "SORALIS is the eclipse brand, and this is the wind ball. It goes where you aimed no matter what the flag is doing, with a little extra power—but it cannot be shaped at all.",
  "SORALISは日食をシンボルにしたブランド。そしてこれは風のためのボール。旗がどう揺れていようと狙った場所へ飛び、パワーも少し上乗せ。ただし球筋を曲げることはまったくできない。"),
 "ball_shimmer_g": (
  "Shimmer G is the trophy ball—an oil-slick finish that exists to be noticed. Long, lively, steady in wind and it keeps rolling; it does everything a ball can do except stop or curve.",
  "Shimmer Gはトロフィーのようなボール。オイルスリックの輝きは見せびらかすためにある。飛んで、跳ねて、風に強く、よく転がる。止まることと曲がること以外、ボールにできることはすべてこなす。"),
}

def key_for(bid): return "BALL_INFO_" + bid[len("ball_"):].upper()

# ---- validate rules
counts = {}
for b in BALLS:
    bid,name,brand,tier,p,r,w,ro,s = b[:9]
    stats=[p,r,w,ro,s]; net=sum(stats)
    assert net == BUDGET[tier], (bid, net, tier)
    assert all(-10<=x<=10 for x in stats), bid
    if bid!="ball_golfin": assert min(stats) <= -3, (bid,"needs a -3 or worse")
    tens=[x for x in stats if abs(x)==10]
    assert len(tens)<=1, bid
    if tens: assert tier in ("Rare","Mythic","Legendary"), bid
    if tier!="Common": assert sum(abs(x) for x in stats)>=8, bid
    counts[tier]=counts.get(tier,0)+1
print("rules OK", counts)

# ---- Balls.csv (new header with rarity after brand)
hdr = ["id","name","brand","rarity","power","rebound","windResistance","roll","spin","thumbnailSprite","fullSprite","info","thumbnailUrl","fullUrl","isDefault"]
existing_info = {
 "ball_golfin":"The standard Golfin ball. Perfectly balanced with no stat bonuses or penalties—reliable in any situation.",
 "ball_putt_ace":"Designed by PUTT ACE, a name synonymous with short-game mastery, this ball delivers exceptional spin, subtle roll, and balanced power—tailored for precision play in any condition.",
}
buf=io.StringIO(); wcsv=csv.writer(buf, lineterminator="\n", quoting=csv.QUOTE_MINIMAL)
wcsv.writerow(hdr)
for b in BALLS:
    bid,name,brand,tier,p,r,w,ro,s,full,thumb,_ = b
    info = existing_info.get(bid) or BLURB[bid][0]
    wcsv.writerow([bid,name,brand,tier,p,r,w,ro,s,thumb,full,info,"","","true" if bid=="ball_golfin" else "false"])
open("/home/claude/balls/Balls.csv","w",encoding="utf-8").write(buf.getvalue())

# ---- texts rows (18 new)
buf=io.StringIO(); wcsv=csv.writer(buf, lineterminator="\n", quoting=csv.QUOTE_MINIMAL)
for b in BALLS:
    bid=b[0]
    if bid in existing_info: continue
    en,ja=BLURB[bid]; wcsv.writerow([key_for(bid),en,ja])
open("/home/claude/balls/texts_rows.csv","w",encoding="utf-8").write(buf.getvalue())

# ---- BALL_IDENTITY.md
L=[]
L.append("# Ball Identity & Stat Sheet — **APPROVED 2026-08-31** (Cesar, via the decision round; wind-ball trims per his \"go with your recommendation\")\n")
L.append("> Companion to `SPEC.md`. The **Look** column describes the existing 1000×1000 base sprite, which is the ground truth (ART WINS). The stat columns are the numbers that ship in `Balls.csv`; the arithmetic is shown below and was checked by script (`Docs/Specs/Active/ball_art_and_stats/reference/build_docs.py`).\n")
L.append("**Decisions this table depends on (both taken 2026-08-31):** ball rarity lives in a new `rarity` column on `Balls.csv`; `BallWindCutPerPoint` goes 0.01 → **0.02**, so +10 wind buys 0.20 of the 0.30 cap. The three wind balls (Klyro, Fyloe Aim, Soralis) were trimmed a touch on their other positives to pay for the doubled stat — net budget unchanged.\n")
L.append("Stat order matches the CSV: `power, rebound, windResistance, roll, spin`. Range −10..+10. Budget by tier: Common 0 · Uncommon +3 · Rare +5 · Mythic +7 · Legendary +9.\n")
L.append("**Sign traps, repeated because they are easy to get backwards:** `+Roll` = rolls FARTHER (fairway *and* green) → a ball that stops on greens needs **negative** Roll. `+Rebound` = livelier bounce. `+Wind` = cuts through wind. Rebound and Roll saturate their clamps exactly at ±10.\n")
L.append("## Tier spread\n\nCommon 5 · Uncommon 6 · Rare 5 · Mythic 3 · Legendary 1 — a gacha-shaped curve, not a flat one.\n")
L.append("## The table\n")
L.append("| Ball | id | Tier | Pwr | Reb | Wind | Roll | Spin | Net | |x| | Look (the existing base sprite) | Identity in one line |")
L.append("|---|---|---|--:|--:|--:|--:|--:|--:|--:|---|---|")
for b in BALLS:
    bid,name,brand,tier,p,r,w,ro,s = b[:9]
    st=[p,r,w,ro,s]
    fmt=lambda x: f"**{x:+d}**" if abs(x)==10 else (f"{x:+d}" if x else "0")
    L.append(f"| **{name}** | `{bid}` | {tier} | {fmt(p)} | {fmt(r)} | {fmt(w)} | {fmt(ro)} | {fmt(s)} | {sum(st):+d} | {sum(abs(x) for x in st)} | {LOOK[bid]} | {IDENTITY[bid]} |")
L.append("")
L.append("### Arithmetic check (per ball)\n")
for b in BALLS:
    bid,name,brand,tier,p,r,w,ro,s = b[:9]
    st=[p,r,w,ro,s]
    L.append(f"- `{bid}` ({tier}, budget {BUDGET[tier]:+d}): {' + '.join(f'({x:+d})' for x in st)} = **{sum(st):+d}** ✓ · |x| = {sum(abs(x) for x in st)} · worst negative {min(st):+d}")
L.append("")
L.append("Rules 1–6 of SPEC §4.3 hold for all 20: every stat in −10..+10; every ball except `ball_golfin` carries a −3 or worse; four balls use a ±10 (Putt Ace Pwr, MireO Spin, Soralis Wind, Shimmer G Spin), all Rare or above, none uses two; |x| ≥ 8 for everything above Common; `ball_golfin` is `0,0,0,0,0` / `isDefault=true`; every line was written against its art.\n")
L.append("### Deliberate shape of the set\n")
L.append("- **Spin is the currency.** Fourteen of the twenty pay for their upside in spin, because spin is the stat a distance-hungry player misses least and a scoring player misses most.")
L.append("- **The three spin balls are kept distinct.** Fyloe Soft is soft-feel spin at Common; Fairloft is wind-and-landing at Uncommon; MireO is maxed artisan spin at Mythic. GolfinIX is the roll-killer.")
L.append("- **Wind is now a real stat** (0.02/pt). Klyro, Fyloe Aim and Soralis are the wind lane and were made *more* wind-specialised, not stronger overall: their other positives came down and their negatives softened by the same amount, so each stays exactly on budget.")
L.append("- **Nothing is strictly better than the default.** Golfin is flat zero, so every other ball is a trade, which is what keeps a consumable stat item from being a pay-to-win lane.\n")
L.append("## Blurbs (D4) — EN + JA, house style: what the brand is known for, then what the ball actually does, naming the trade-off\n")
L.append("Each claim below was checked against the sign of its stats (negative Roll = checks up / settles; positive Roll = runs out). The same text ships as `Balls.csv` `info` (EN) and as `BALL_INFO_<ID>` in `LocalizationText.csv` (EN + JA) — see `Docs/Specs/Active/ball_data_wiring/SPEC.md`.\n")
for b in BALLS:
    bid,name=b[0],b[1]
    if bid in existing_info:
        L.append(f"- **{name}** (`{key_for(bid)}`) — ships as-is; no change.")
        continue
    en,ja=BLURB[bid]
    L.append(f"- **{name}** (`{key_for(bid)}`)\n  - EN: {en}\n  - JA: {ja}")
L.append("")
open("/home/claude/balls/BALL_IDENTITY.md","w",encoding="utf-8").write("\n".join(L))
print("written")
