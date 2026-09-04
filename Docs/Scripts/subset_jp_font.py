#!/usr/bin/env python3
"""build_size_diet Phase 4 option (c) — instance + subset NotoSansJP-VariableFont_wght.ttf.

  python3 Docs/Scripts/subset_jp_font.py [--apply]

Without --apply it only MEASURES (writes the candidate beside the original as .subset.ttf and
prints the sizes). With --apply it overwrites the .ttf in place, keeping the file name and
therefore the GUID, so no prefab, material or TMP asset changes and the TMP asset stays
Dynamic.

THE WEIGHT IS MEASURED, NOT ASSUMED. This font's fvar default is wght 100, and TMP's stored
face info for the asset says m_StyleName: Thin — so Thin IS what the game renders today, and
instancing at 400 "because that is Regular" would silently make every Japanese string in the
game heavier. The instance weight therefore defaults to the measured 100 and has to be
overridden deliberately.

THE GLYPH SET is JIS X 0208 (levels 1+2, enumerated from the shift_jis codec rather than
copied from a list), plus the full Hiragana and Katakana blocks (player names are typed, not
translated), CJK punctuation, halfwidth/fullwidth forms, ASCII/Latin-1 and the currency and
symbol characters the UI uses — union'd with every character that appears in ANY project CSV
carrying Japanese. Note that is SEVEN CSVs, not just LocalizationText.csv: Clubs, missions,
stamina_shop_items, stamina_shops, gacha_banners and ticket_types carry Japanese too.
"""
import glob, os, sys
from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

SRC = "Assets/Fonts/NotoSansJP-VariableFont_wght.ttf"
MEASURED_WEIGHT = 100          # TMP face info m_StyleName: Thin; fvar default 100

def jis_x0208():
    """Every character JIS X 0208 can encode, by enumerating the shift_jis codec."""
    out = set()
    for lead in list(range(0x81, 0xA0)) + list(range(0xE0, 0xF0)):
        for trail in range(0x40, 0xFD):
            try:
                out.add(bytes([lead, trail]).decode("shift_jis"))
            except UnicodeDecodeError:
                pass
    return out

def csv_glyphs():
    def cjk(c):
        o = ord(c)
        return (0x3040 <= o <= 0x30FF or 0x4E00 <= o <= 0x9FFF or
                0x3400 <= o <= 0x4DBF or 0xF900 <= o <= 0xFAFF or 0xFF00 <= o <= 0xFFEF)
    out = set()
    for p in glob.glob("Assets/**/*.csv", recursive=True):
        if "/HoleData/" in p:
            continue
        try:
            t = open(p, encoding="utf-8").read()
        except Exception:
            continue
        if any(cjk(c) for c in t):
            out |= set(t)
    return out

def ranges(*pairs):
    out = set()
    for a, b in pairs:
        out |= {chr(c) for c in range(a, b + 1)}
    return out

def build_set():
    keep = set()
    keep |= ranges((0x0020, 0x007E),   # ASCII
                   (0x00A0, 0x00FF),   # Latin-1 supplement
                   (0x2000, 0x206F),   # general punctuation
                   (0x20A0, 0x20BF),   # currency symbols
                   (0x2190, 0x21FF),   # arrows
                   (0x2200, 0x22FF),   # maths (x, +/-)
                   (0x25A0, 0x25FF),   # geometric shapes (bullets)
                   (0x3000, 0x303F),   # CJK punctuation
                   (0x3040, 0x309F),   # Hiragana
                   (0x30A0, 0x30FF),   # Katakana
                   (0x31F0, 0x31FF),   # Katakana phonetic extensions
                   (0xFF00, 0xFFEF))   # halfwidth / fullwidth forms
    keep |= jis_x0208()
    keep |= csv_glyphs()
    keep = {c for c in keep if c.isprintable() or c == " "}
    return keep

def main():
    apply_it = "--apply" in sys.argv
    keep = build_set()
    src_bytes = os.path.getsize(SRC)

    f = TTFont(SRC)
    n_before = f["maxp"].numGlyphs
    axes = {a.axisTag: (a.minValue, a.defaultValue, a.maxValue) for a in f["fvar"].axes} if "fvar" in f else {}

    # 1. Pin the variable axis at the MEASURED weight — this drops fvar/gvar/avar/HVAR/STAT.
    f = instancer.instantiateVariableFont(f, {"wght": MEASURED_WEIGHT}, inplace=True, updateFontNames=False)

    # 2. Subset to the glyph set.
    opts = subset.Options()
    opts.layout_features = ["*"]        # keep vert/vrt2 etc.; Japanese needs its GSUB
    opts.name_IDs = ["*"]
    opts.notdef_outline = True
    opts.recalc_bounds = True
    opts.drop_tables += ["BASE"]
    s = subset.Subsetter(options=opts)
    s.populate(unicodes=[ord(c) for c in keep])
    s.subset(f)

    # NOT beside the source when measuring: a stray .ttf under Assets/Fonts is an asset Unity
    # imports, and this pass is supposed to change nothing.
    dst = SRC if apply_it else os.path.join(
        os.environ.get("SUBSET_OUT_DIR", "."), "NotoSansJP.subset.ttf")
    f.save(dst)
    out_bytes = os.path.getsize(dst)

    print(f"source      : {SRC}")
    print(f"fvar axes   : {axes}   (wght default {axes.get('wght', ('?',)*3)[1]})")
    print(f"instanced at: wght {MEASURED_WEIGHT}  — MEASURED from the TMP asset's m_StyleName: Thin")
    print(f"glyph set   : {len(keep)} codepoints requested "
          f"(JIS X 0208 {len(jis_x0208())} + kana/punct/latin blocks + {len(csv_glyphs())} from project CSVs)")
    print(f"glyphs      : {n_before} -> {f['maxp'].numGlyphs}")
    print(f"bytes       : {src_bytes:,} -> {out_bytes:,}  ({src_bytes/1048576:.2f} MiB -> {out_bytes/1048576:.2f} MiB, "
          f"{100*(src_bytes-out_bytes)/src_bytes:.1f}% smaller)")
    print(f"written to  : {dst}{'   (IN PLACE — same name, same GUID)' if apply_it else '   (measurement only)'}")

    for ch in "齋藤あアン円×→":
        print(f"  covers {ch!r} U+{ord(ch):04X}: {ord(ch) in f.getBestCmap()}")

main()
