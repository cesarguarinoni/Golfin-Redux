#!/usr/bin/env python3
"""C2 — Clubs.csv roster generator (GOLFIN Redux, tournament_restrictions plan Workstream C).

19 brands x 7 types x 6 rarities = 798 combos; the 7 legacy rows keep their ids/values verbatim
and their (brand,type,rarity) combos are not regenerated. Art refs use the club_art_batches
naming so sprites land automatically as batches complete. New column `info_ja` appended
(ClubDatabaseCSV reads by header name; unknown columns are ignored until Code wires JA display).

Anchors: physics inversion fitted to the 7 shipped bot-validated rows; carry ladders monotonic
per rarity with PW > AW (fixes the shipped 136/136 overlap) and the new S.Wedge below AW.
"""
import csv, hashlib, io, sys

RARITIES = ["Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme"]
R_IDX = {r: i for i, r in enumerate(RARITIES)}
LEVELS = {"Common": (10, 39), "Uncommon": (40, 79), "Rare": (80, 119),
          "Mythic": (120, 159), "Legendary": (160, 199), "Supreme": (200, 239)}
RARITY_MULT = [0.55, 0.70, 0.85, 1.00, 1.15, 1.30]           # of Mythic-anchored profile
DURABILITY = [60, 70, 80, 90, 100, 120]
R_ADJ_EN = ["entry-level", "step-up", "refined", "elite", "masterwork", "flagship"]
R_ADJ_JA = ["入門用", "上位モデルの", "洗練された", "エリート級の", "傑作級の", "最高峰の"]

# type: (slug, display, profile power/acc/lie/loft at MYTHIC, carry per rarity, launch, spin base, anchor ms->yd)
TYPES = {
    "Driver":  ("driver", "Driver",  (85, 38, 11, 13), [250, 262, 275, 287, 298, 310], 10.9, 2686, (75.0, 275)),
    "Wood":    ("wood",   "Wood",    (75, 43, 13, 16), [222, 232, 243, 253, 263, 273], 9.2, 3655, (70.6, 243)),
    "Iron":    ("iron",   "Iron",    (65, 55, 22, 27), [155, 163, 172, 180, 188, 196], 16.3, 7097, (52.5, 172)),
    "P.Wedge": ("pwedge", "P.Wedge", (45, 60, 48, 48), [126, 133, 140, 146, 152, 158], 22.0, 8800, (46.0, 136)),
    "A.Wedge": ("awedge", "A.Wedge", (33, 55, 60, 58), [112, 118, 124, 130, 136, 142], 24.0, 9300, (46.0, 136)),
    "S.Wedge": ("swedge", "S.Wedge", (27, 48, 70, 66), [98, 104, 110, 116, 122, 128], 26.5, 9800, (46.0, 136)),
    "Putter":  ("putter", "Putter",  (32, 80, 32, 5),  [30, 30, 30, 30, 30, 30],       5.0,  0,    (5.0, 30)),
}
YD_PER_MS = 4.79  # global carry/ballspeed slope fitted across the shipped set

# brand: (slug, art tag, display, bias {stat: mult}, carry_pct, EN template, JA template)
# bias mults: ++ 1.18, + 1.10, - 0.90, -- 0.82 (applied inside the rarity budget, clamped 5..120)
BRANDS = {
 "G&F":            ("gf", "GF", {}, 0,
   "A dependable {radjen} {tname} from G&F — balanced feel you can trust on any lie.",
   "G&Fの{radjja}{tja}。どんなライでも信頼できるバランス型。"),
 "GOLFIN":         ("golfin", "GOLFIN", {"power":1.05,"accuracy":1.05}, 0,
   "The house {radjen} {tname} by GOLFIN — confident, complete, tour-ready.",
   "GOLFIN純正の{radjja}{tja}。自信と完成度を兼ね備えたツアー仕様。"),
 "KLYRO":          ("klyro", "KLYRO", {"accuracy":1.18,"power":0.90}, -2,
   "KLYRO's {radjen} {tname} — precision engineering that turns aim into certainty.",
   "KLYROの{radjja}{tja}。狙いを確信に変える精密設計。"),
 "MireO":          ("mireo", "MIREO", {"loft":1.18,"accuracy":1.10,"power":0.90}, -2,
   "Refined by MireO, this {radjen} {tname} spins the ball with artisan control.",
   "MireOが磨き上げた{radjja}{tja}。職人技のスピンコントロール。"),
 "FYLOE":          ("fyloe", "FYLOE", {"lie":1.18,"power":0.90}, -2,
   "FYLOE's {radjen} {tname} escapes anything — rough, sand, bad decisions.",
   "FYLOEの{radjja}{tja}。ラフも砂も、判断ミスさえも脱出させる。"),
 "ROYAL SWING":    ("royal", "ROYAL", {"accuracy":1.10,"power":1.10,"durability":0.90}, 2,
   "The Royal Swing {radjen} {tname} — prestige performance with a gilded touch.",
   "Royal Swingの{radjja}{tja}。気品と性能を併せ持つ一本。"),
 "EAGLEZ":         ("eaglez", "EAGLEZ", {"power":1.18,"accuracy":0.82}, 6,
   "EAGLEZ built this {radjen} {tname} for one thing: outrageous distance. Aim is your problem.",
   "EAGLEZの{radjja}{tja}。目的はただ一つ、規格外の飛距離。方向は自己責任。"),
 "FOREFIT":        ("forefit", "FOREFIT", {"durability":1.18,"lie":1.10,"power":0.90}, -2,
   "FOREFIT's {radjen} {tname} forgives everything and never wears out on you.",
   "FOREFITの{radjja}{tja}。ミスに寛容で、壊れ知らずの相棒。"),
 "PAR PERFECT":    ("parperfect", "PARPERFECT", {"accuracy":1.18,"loft":0.90}, 0,
   "PAR PERFECT's {radjen} {tname} — the same swing, the same result, every time.",
   "PAR PERFECTの{radjja}{tja}。同じスイングに、常に同じ結果を。"),
 "BogeyB":         ("bogeyb", "BOGEYB", {"durability":1.10,"power":0.95,"accuracy":0.95}, -3,
   "BogeyB's {radjen} {tname} won't make you a pro — but it will never quit on you.",
   "BogeyBの{radjja}{tja}。プロにはしてくれないが、決して裏切らない。"),
 "Fairway THREADS":("fairwaythreads", "FAIRWAY", {"accuracy":1.05}, 0,
   "Fairway THREADS' {radjen} {tname} — stitched style, honest performance.",
   "Fairway THREADSの{radjja}{tja}。スタイルと実力を織り込んだ一本。"),
 "GREEN SWING":    ("greenswing", "GREENSWING", {"lie":1.10,"loft":1.10}, -1,
   "GREEN SWING's {radjen} {tname} glides through turf with an organic, easy tempo.",
   "GREEN SWINGの{radjja}{tja}。芝を滑るようなオーガニックな打感。"),
 "FairX":          ("fairx", "FAIRX", {"power":1.10,"accuracy":0.90,"durability":1.10}, 3,
   "FairX's {radjen} {tname} — angular, aggressive, tuned for players who attack.",
   "FairXの{radjja}{tja}。攻める者のための鋭いチューニング。"),
 "FAIRLOFT":       ("fairloft", "FAIRLOFT", {"loft":1.18,"power":0.90}, -3,
   "FAIRLOFT's {radjen} {tname} floats the ball high and drops it soft as snow.",
   "FAIRLOFTの{radjja}{tja}。高く舞い上げ、雪のように柔らかく落とす。"),
 "GOLFINIX":       ("golfinix", "GOLFINIX", {"accuracy":1.10}, 0,
   "GOLFINIX's {radjen} {tname} — boutique feel from GOLFIN's experimental bench.",
   "GOLFINIXの{radjja}{tja}。GOLFIN実験工房が生んだ極上のフィーリング。"),
 "PUTT ACE":       ("puttace", "PUTTACE", {"accuracy":1.10,"power":0.90}, -2,
   "PUTT ACE's {radjen} {tname} carries green-side calm into every part of the bag.",
   "PUTT ACEの{radjja}{tja}。グリーン際の冷静さをバッグ全体に。"),
 "TeePit WNDRWLL": ("teepit", "TEEPIT", {"power":1.10,"loft":1.18}, 2,
   "The TeePit WNDRWLL {radjen} {tname} launches high and hangs forever.",
   "TeePit WNDRWLLの{radjja}{tja}。高く打ち出し、いつまでも滞空する。"),
 "TIFTO":          ("tifto", "TIFTO", {"durability":1.18,"power":0.90}, -2,
   "TIFTO's {radjen} {tname} — industrial-grade, over-built, practically immortal.",
   "TIFTOの{radjja}{tja}。工業製品級の頑丈さ、ほぼ不滅。"),
 "VBOOOT":         ("vboot", "VBOOOT", {"power":1.18,"loft":1.10,"accuracy":0.82,"durability":0.90}, 8,
   "VBOOOT's {radjen} {tname} is loud, unhinged, and hits like a rumor. No refunds.",
   "VBOOOTの{radjja}{tja}。派手で、常識外れで、噂のように飛ぶ。返品不可。"),
}
T_JA = {"Driver": "ドライバー", "Wood": "ウッド", "Iron": "アイアン", "P.Wedge": "ピッチングウェッジ",
        "A.Wedge": "アプローチウェッジ", "S.Wedge": "サンドウェッジ", "Putter": "パター"}

LEGACY = {  # (brand, type, rarity) -> keep shipped row, skip generation
    ("G&F", "Driver", "Common"), ("G&F", "Wood", "Common"), ("KLYRO", "Iron", "Uncommon"),
    ("MireO", "Iron", "Rare"), ("FYLOE", "A.Wedge", "Mythic"),
    ("ROYAL SWING", "P.Wedge", "Legendary"), ("GolfinX", "Putter", "Supreme"),
}
# GolfinX legacy putter is treated as GOLFINIX's Supreme putter slot? NO — GolfinX is its own
# legacy brand name; GOLFINIX generates all 6 putter variants. Legacy row simply coexists.
LEGACY = {(b, t, r) for (b, t, r) in LEGACY if b != "GolfinX"}

def iron_number(brand):
    if brand == "KLYRO": return 9
    if brand == "MireO": return 7
    return 4 + int(hashlib.md5(brand.encode()).hexdigest(), 16) % 5  # 4..8 deterministic

def clamp(v, lo=5, hi=120): return max(lo, min(hi, int(round(v))))

def gen_rows():
    rows = []
    for brand, (bslug, tag, bias, carry_pct, en_t, ja_t) in BRANDS.items():
        for tkey, (tslug, tdisp, prof, carries, launch, spin, (a_ms, a_yd)) in TYPES.items():
            for r in RARITIES:
                if (brand, tkey, r) in LEGACY: continue
                i = R_IDX[r]
                mult = RARITY_MULT[i]
                power = clamp(prof[0] * mult * bias.get("power", 1))
                acc   = clamp(prof[1] * mult * bias.get("accuracy", 1))
                lie   = clamp(prof[2] * mult * bias.get("lie", 1))
                loft  = clamp(prof[3] * mult * bias.get("loft", 1), 3, 120)
                dur   = clamp(DURABILITY[i] * bias.get("durability", 1), 30, 120)
                if tkey == "Putter":
                    carry = 30; ms = 5.0; la = 5.0; sp = 0
                else:
                    carry = int(round(carries[i] * (1 + carry_pct / 100)))
                    ms = round(a_ms + (carry - a_yd) / YD_PER_MS, 1)
                    la = launch
                    sp = int(round(spin * (1 + 0.02 * (i - 3)))) if spin else 0
                base_dist = int(round(carry * 0.91)) if tkey != "Putter" else 30
                num = f" {iron_number(brand)}" if tkey == "Iron" else ""
                name = f"{tdisp}{num} {brand}"
                cid = f"club_{tslug}_{bslug}_{r.lower()}"
                wedge = tkey in ("P.Wedge", "A.Wedge", "S.Wedge")
                art_type = "Wedge" if wedge else tdisp
                portrait = f"S_Menu_{art_type}_{tag}"
                full = f"{art_type}-{brand.title().replace(' ', '')}" if brand != "G&F" else f"{art_type}-G&F"
                ctrl = f"S_Controls_{art_type}_{tag}"
                lo, hi = LEVELS[r]
                EN_NOUN = {"Driver": "driver", "Wood": "fairway wood", "P.Wedge": "pitching wedge",
                           "A.Wedge": "approach wedge", "S.Wedge": "sand wedge", "Putter": "putter"}
                tnoun = EN_NOUN.get(tkey, f"{iron_number(brand)}-iron")
                en = en_t.format(radjen=R_ADJ_EN[i], tname=tnoun)
                ja = ja_t.format(radjja=R_ADJ_JA[i], tja=T_JA[tkey])
                rows.append([cid, name, tkey, r, brand, power, acc, lie, loft, dur, base_dist,
                             ms, la, sp, carry, portrait, full, ctrl, lo, hi, en, ja])
    return rows

HEADER = ["id","name","type","rarity","brand","basePower","baseAccuracy","baseLieResistance",
          "baseLoft","maxDurability","baseDistance","ballSpeedMps","launchAngleDeg","spinRateRpm",
          "expectedCarryYd","portraitSprite","portraitFull","controlSprite","startLevel","maxLevel",
          "info","info_ja"]

if __name__ == "__main__":
    rows = gen_rows()
    print(f"generated {len(rows)} rows (+7 legacy = {len(rows)+7} total)")
    with open("Clubs_generated.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f); w.writerow(HEADER); w.writerows(rows)
    with open("Clubs_sample_EAGLEZ_FYLOE.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f); w.writerow(HEADER)
        w.writerows([r for r in rows if r[4] in ("EAGLEZ", "FYLOE")])
    # ladder sanity: monotonic carries per rarity
    for i, r in enumerate(RARITIES):
        seq = [TYPES[t][3][i] for t in ("Driver","Wood","Iron","P.Wedge","A.Wedge","S.Wedge")]
        assert seq == sorted(seq, reverse=True), (r, seq)
        print(r, "carry ladder:", seq, "gaps ok")
