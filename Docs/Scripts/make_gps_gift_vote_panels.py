#!/usr/bin/env python3
"""gps_gifts_votes — bake every panel, disc and pill the Gift and Vote screens need.

    python3 Docs/Scripts/make_gps_gift_vote_panels.py

Edit THIS, not the PNGs (Build rule 1: gradients come from tokens and are baked, never tinted).
Same contract and the same solver as `make_score_upload_panels.py` / `make_gps_profile_panels.py`
/ `make_gps_auth_swatches.py`; nothing here re-implements a routine one of those already owns.

TWO SCREENS, TWO BACKDROPS — read off the node, then MATCHED against the project's own art
──────────────────────────────────────────────────────────────────────────────────────────
The `Backgrounds` instances name their variants:

    GPS Gift  14027:101844  ->  "Rewards"
    GPS Vote  14028:33535   ->  "Rsnkings Day Illustration"

Both plates were downloaded from the node and compared against every 1170x2532-capable PNG in
the project, `object-cover`-fitted the way the node draws them (a naive resize compares two
different crops and is what makes this look ambiguous). Mean |dRGB| over the whole frame:

    Gift  ->  Assets/Art/Shop/Background - Rewards.png    0.485     (next: Shop 0.368*, Blurred 15.2)
    Vote  ->  Assets/Art/ClubsInventory/Background.png    0.175     (next: BG_SU_GpsProof 83.4)

    * `Background - Shop.png` scores marginally closer, but it and `Background - Rewards.png`
      differ from EACH OTHER by only 0.745 — they are two exports of one photo, and the node
      names the variant "Rewards". The name decides; the difference is below the noise floor of
      the comparison either way.

So neither screen needs a new background asset, and every translucent card below is fitted over
the plate it actually sits on.

WHAT IS BAKED, AND WHY EACH ONE HAS TO BE
  cards        the standard GPS atom: `bg-gradient-to-b rgba(19,52,83,.6) -> rgba(9,27,51,.6)`
               + 3px solid white + r50 (r28 on the small item cell). A vertical gradient cannot
               survive 9-slicing, so each is baked at its node size.
  gift hero    the ONE non-standard panel on either screen: an OPAQUE plum gradient
               #6b2140 -> #3a1226 (14027:102100). Opaque, so no fit — there is no background
               left to get wrong.
  photo areas  the two vote-card placeholders, `#3f6b3a -> #1c3a1f` (14028:33837) and
               `#6b4a2a -> #3a2a16` (14029:102242). They sit INSIDE a card whose 3px border and
               r50 clip them, so they are baked 6px narrower with their TOP corners rounded to
               47 and their bottom left square — which is exactly what `overflow-clip` produces
               and what a plain rounded rect would not.
  stories/chips the two flat `rgba(9,27,51,0.70)` strips (14028:33791 r32, :33827 r100). Flat,
               but still fitted: 0.70 in Figma's sRGB compositing is not 0.70 in Unity's linear
               one (the pager-dot scar from auth_golf_profile).
  avatars      the icon-ring atom at THREE sizes, in the project's four avatar colours. The node
               draws every avatar as `<circle r=R stroke=W>` with `R + W/2 == size/2` — the
               atom's own geometry — so they are baked by `make_gps_icon_ring.bake()` rather
               than by a second circle routine. Stroke is NOT proportional across the three
               sizes (3.0 / 3.52 / 3.0 at 72 / 88 / 48), which is why one sprite scaled three
               ways would be wrong and three bakes are right.
  rings        9-SLICEABLE capsule outlines, at TWO stroke calibrations. Both the unselected
               filter chip (1px #818ea1, the strip showing straight through) and every status /
               option pill (1px accent over a translucent accent fill) need a rim with a HOLLOW
               interior — a tinted `S_PillStadium` would paint the middle too, and a pill built
               that way composited its own translucent fill on top of its own opaque rim and read
               as solid gold. Widths are content-driven, so a fixed-size bake cannot serve either.
               Baked 176x176 with an 88px border, the same construction `S_SU_GoldSegment.png`
               uses. TWO of them because 9-slicing scales the stroke by radius/88: one bake is 1px
               at exactly one radius, and the chips (r26) and pills (r19) are not the same.
  separator    the panel-header rule: a horizontal white gradient 0 -> 0.9 -> 0 (SVG
               `paint0_linear_0_77`), which is a fill no tint can produce.

NOT BAKED, DELIBERATELY — these reuse an existing atom (Build rule 9 / Rule 19 provenance):
  gift-item icon ring  `S_GpsIconRing_Tile.png`. Node 14027:102196 is 72px r34 stroke 4 with the
                       atom's exact token pair (#204B76->#0B203D fill, #F3ECC2->#98855B stroke);
                       the 88px atom scaled to 72 lands its stroke at 4.09 against the node's 4.
  status / option pills `S_PillStadium.png` for the translucent fill + `S_GV_PillRing.png` for the
                       1px rim. Both 9-sliced, so the pill hugs its label at any width.
  gold chip fill       `S_SU_GoldSegment.png` — already the #f3ecc2 -> #c9a94f capsule the
                       selected chip needs (14028:33828).
  bars                 `S_PillStadium.png` at ppum 88/8, driven by WIDTH (never `Image.Type.Filled`).
  buttons              `Play Button.png` / `ButtonCancel.png`, the Main Buttons Gold/Silver atoms.
  story "NEW" disc     exported from the node itself (14028:33794 is a raster fill, not a shape) —
                       see `S_GV_StoryNew.png` below.
"""
import importlib.util
import os
import sys

from PIL import Image, ImageChops, ImageDraw

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def _load(mod_name, filename):
    spec = importlib.util.spec_from_file_location(
        mod_name, os.path.join(REPO, "Docs", "Scripts", filename))
    mod = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(mod)
    except SystemExit:
        pass
    return mod


su = _load("su_panels", "make_score_upload_panels.py")
ring = _load("gps_ring", "make_gps_icon_ring.py")

OUT_DIR = su.OUT_DIR
SCALE = su.SCALE            # 2 — corner arcs stay clean on a 3x device
CIRCLE_SCALE = 4            # same as make_gps_auth_swatches

BG_GIFT = "Assets/Art/Shop/Background - Rewards.png"
BG_VOTE = "Assets/Art/ClubsInventory/Background.png"

# The shared card token family, from its one owner.
BLUE_TOP = su.BLUE_TOP          # (0x13, 0x34, 0x53, 0.60)
BLUE_BOTTOM = su.BLUE_BOTTOM    # (0x09, 0x1B, 0x33, 0.60)

# Node 14027:102100 — the plum hero. OPAQUE (the CSS carries no rgba()).
PLUM_TOP = (0x6B, 0x21, 0x40, 1.00)
PLUM_BOTTOM = (0x3A, 0x12, 0x26, 1.00)

# Node 14028:33837 / 14029:102242 — the two photo placeholders. Also opaque.
PHOTO_GREEN = ((0x3F, 0x6B, 0x3A, 1.00), (0x1C, 0x3A, 0x1F, 1.00))
PHOTO_BROWN = ((0x6B, 0x4A, 0x2A, 1.00), (0x3A, 0x2A, 0x16, 1.00))

# Node 14028:33791 / :33827 — the two flat strips.
STRIP = (0x09, 0x1B, 0x33, 0.70)

# ── Canvas geometry (1170x2532) ───────────────────────────────────────────────────────
# Content Container is (96, 361, 978, 1860) on BOTH frames, and every panel sits at x=10
# inside it, i.e. canvas x = 106. canvas y = 361 + the node's own y.
CC_X, CC_Y = 96, 361
PX = CC_X + 10          # 106

# bg index (into su.BACKGROUNDS): 0 = the Gift plate, 1 = the Vote plate.
# name, w, h, radius, top, bottom, bg, canvas_x, canvas_y, node
CARDS = [
    # ── GPS Gift 14027:101843 ─────────────────────────────────────────────────────────
    ("S_GV_GiftHero.png",    958, 288, 50, PLUM_TOP,  PLUM_BOTTOM, 0, PX,  CC_Y + 0,    "14027:102100"),
    ("S_GV_Supporters.png",  958, 376, 50, BLUE_TOP,  BLUE_BOTTOM, 0, PX,  CC_Y + 312,  "14027:102114"),
    ("S_GV_Golfers.png",     958, 568, 50, BLUE_TOP,  BLUE_BOTTOM, 0, PX,  CC_Y + 712,  "14027:102146"),
    ("S_GV_BuyGifts.png",    958, 312, 50, BLUE_TOP,  BLUE_BOTTOM, 0, PX,  CC_Y + 1304, "14027:102190"),
    # The item cell: node 14027:102194 is 287.33 x 168 r28, first of three in a row that starts
    # at the panel's x + 32 padding, i.e. canvas 106 + 32 = 138, y 361 + 1304 + 110 + 10.
    ("S_GV_ItemCell.png",    287, 168, 28, BLUE_TOP,  BLUE_BOTTOM, 0, 138, CC_Y + 1424, "14027:102194"),

    # ── GPS Vote 14028:33534 ──────────────────────────────────────────────────────────
    ("S_GV_StoriesRow.png",  958, 143, 32, STRIP,     None,        1, PX,  CC_Y + 0,    "14028:33791"),
    ("S_GV_ChipsRow.png",    958,  78, 39, STRIP,     None,        1, PX,  CC_Y + 167,  "14028:33827"),
    ("S_GV_CardPhoto.png",   958, 530, 50, BLUE_TOP,  BLUE_BOTTOM, 1, PX,  CC_Y + 269,  "14028:33836"),
    ("S_GV_CardSimple.png",  958, 232, 50, BLUE_TOP,  BLUE_BOTTOM, 1, PX,  CC_Y + 823,  "14028:33877"),
    ("S_GV_CardMulti.png",   958, 200, 50, BLUE_TOP,  BLUE_BOTTOM, 1, PX,  CC_Y + 1079, "14028:33901"),
    ("S_GV_CardPhoto2.png",  958, 450, 50, BLUE_TOP,  BLUE_BOTTOM, 1, PX,  CC_Y + 1303, "14029:102241"),
]

# The two flat strips carry NO border on the node (no `border-` class on either), unlike every
# card above. Baked with stroke 0 rather than by a second routine.
NO_BORDER = {"S_GV_StoriesRow.png", "S_GV_ChipsRow.png"}

# name, w, h, top-corner radius, (top, bottom), node.
# Sized to sit INSIDE its card's 3px border: 958-6 wide, and the card's r50 minus that border.
PHOTOS = [
    ("S_GV_PhotoGreen.png", 952, 297, 47, PHOTO_GREEN, "14028:33837"),
    ("S_GV_PhotoBrown.png", 952, 217, 47, PHOTO_BROWN, "14029:102242"),
]

# ── Avatar discs — the icon-ring atom, three sizes x four colours ─────────────────────
# size, radius, stroke  (read out of the node SVGs 2026-09-02, NOT scaled from one another)
AVATAR_SIZES = [
    (72, 34.5,  3.00),    # gift rows          14027:102122
    (88, 42.24, 3.52),    # vote stories       14028:33799
    (48, 22.5,  3.00),    # vote card author   14028:33845
]
# The project's four avatar colours — the SAME pairs make_gps_auth_swatches uses, because
# `profiles.avatar_color` is the one enum both screens read.
AVATAR_COLOURS = [
    ("Pink",  "#E57A97", "#B84E6B"),
    ("Green", "#4FA36B", "#2D6F45"),
    ("Blue",  "#4F86D6", "#2C5AA0"),
    ("Gold",  "#C7A04A", "#8A6A22"),
]
AVATAR_RIM = "#F3ECC2"     # SOLID on every avatar node, unlike the icon ring's gradient rim


def bake_top_rounded(path, w, h, radius, top, bottom, scale=SCALE):
    """A gradient block with only its TOP corners rounded.

    This is what a photo area inside an `overflow-clip rounded-[50px]` card actually looks like:
    the card's clip rounds the two corners the photo shares with it and leaves the two that meet
    the vote body square. A fully rounded rect here would cut two visible notches out of the
    middle of the card.
    """
    W, H, R = w * scale, h * scale, radius * scale

    fill = Image.new("RGBA", (W, H))
    px = fill.load()
    for y in range(H):
        t = y / max(1, H - 1)
        row = tuple(int(round(top[i] + (bottom[i] - top[i]) * t)) for i in range(3)) + \
              (int(round((top[3] + (bottom[3] - top[3]) * t) * 255)),)
        for x in range(W):
            px[x, y] = row

    # Rounded on all four, then the bottom half of the mask is squared off again — the arcs stay
    # anti-aliased because they come from the shared mask routine rather than a second draw.
    mask = su._rounded_mask(W, H, R)
    ImageDraw.Draw(mask).rectangle([0, H // 2, W - 1, H - 1], fill=255)

    fill.putalpha(ImageChops.multiply(fill.getchannel("A"), mask))
    fill.save(path)
    return W, H


def bake_chip_ring(path, size=176, border=88, stroke=3.385, aa=4):
    """A 9-SLICEABLE capsule outline in white, tinted at runtime.

    Baked 176x176 with an 88px border — the same construction `S_SU_GoldSegment.png` uses — so
    `Image.Type.Sliced` + `pixelsPerUnitMultiplier = 88 / radius` renders it at any width with a
    true capsule cap at each end.

    STROKE IS PRE-DIVIDED BY THE SLICE FACTOR. 9-slicing scales the corner blocks by
    `radius / 88`, and the stroke rides along: at the filter chip's r26 a sprite stroke of S px
    arrives as `S * 26 / 88`. The node wants 1px (14028:33830 `border border-[#818ea1]`), so
    S = 88/26 = 3.385. A "1px" stroke in the sprite would have rendered at 0.3px and vanished.
    """
    W = size * aa
    R = (size // 2) * aa
    S = max(1, int(round(stroke * aa)))
    m = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([0, 0, W - 1, W - 1], radius=R, fill=255)
    d.rounded_rectangle([S, S, W - 1 - S, W - 1 - S], radius=max(0, R - S), fill=0)
    m = m.resize((size, size), Image.LANCZOS)

    # White everywhere, with the ring mask as the alpha — so `Image.color` tints the rim and
    # nothing else. (A tinted SOLID capsule would paint the chip's interior too, which is the
    # defect the ring exists to avoid.)
    img = Image.new("RGBA", (size, size), (255, 255, 255, 255))
    img.putalpha(m)
    img.save(path)
    return size, size, border


def bake_separator(path, w=958, h=2, scale=SCALE):
    """The panel-header rule (SVG `paint0_linear_0_77`): white, alpha 0 -> 0.9 -> 0 across the
    width. A horizontal alpha ramp is a FILL, so no tint of a flat sprite can produce it."""
    W, H = w * scale, h * scale
    img = Image.new("RGBA", (W, H))
    px = img.load()
    for x in range(W):
        t = x / max(1, W - 1)
        a = 0.9 * (1.0 - abs(2.0 * t - 1.0))     # 0 at both ends, 0.9 at the centre
        for y in range(H):
            px[x, y] = (255, 255, 255, int(round(a * 255)))
    img.save(path)
    return W, H


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    # Install both plates into the shared solver's cache, so `fit_over_background` — the proven
    # least-squares solve — is reused unchanged for both screens.
    su.BACKGROUNDS[0] = BG_GIFT
    su.BACKGROUNDS[1] = BG_VOTE
    su._BG_CACHE.clear()

    print("── Cards ───────────────────────────────────────────────────────────────────")
    for name, w, h, r, top, bottom, bg, cx, cy, node in CARDS:
        stroke = 0 if name in NO_BORDER else None
        fit = su.fit_over_background(bg, (cx, cy, w, h), top, bottom if bottom else top)
        W, H = su.bake_card(os.path.join(OUT_DIR, name), w, h, r, top, bottom,
                            stroke=stroke, fit=fit)
        if fit:
            rgb, a = fit
            print("%-24s %4dx%-4d node %-13s %3dx%-3d r%-2d -> rgb(%3d,%3d,%3d) a=%.3f"
                  % (name, W, H, node, w, h, r, rgb[0], rgb[1], rgb[2], a))
        else:
            print("%-24s %4dx%-4d node %-13s %3dx%-3d r%-2d -> opaque"
                  % (name, W, H, node, w, h, r))

    print("\n── Photo placeholders (top-rounded, inside the card's 3px border) ──────────")
    for name, w, h, r, (top, bottom), node in PHOTOS:
        W, H = bake_top_rounded(os.path.join(OUT_DIR, name), w, h, r, top, bottom)
        print("%-24s %4dx%-4d node %-13s %3dx%-3d top-r%-2d -> #%02X%02X%02X -> #%02X%02X%02X"
              % (name, W, H, node, w, h, r, top[0], top[1], top[2], bottom[0], bottom[1], bottom[2]))

    print("\n── Avatar discs (icon-ring atom) ───────────────────────────────────────────")
    for size, radius, stroke in AVATAR_SIZES:
        for label, top, bottom in AVATAR_COLOURS:
            name = "S_GV_Avatar%s_%d.png" % (label, size)
            img = ring.bake(size, radius, stroke, top, bottom, AVATAR_RIM, AVATAR_RIM,
                            CIRCLE_SCALE)
            img.save(os.path.join(OUT_DIR, name))
            print("%-24s %4dx%-4d (node %dpx r=%.2f stroke=%.2f rim %s, fill %s->%s)"
                  % (name, img.width, img.height, size, radius, stroke, AVATAR_RIM, top, bottom))

    print("\n── 9-sliceable atoms ───────────────────────────────────────────────────────")
    # TWO rings, not one scaled two ways. 9-slicing scales the sprite's stroke by radius/88, so a
    # single bake renders 1px at exactly ONE radius and something else everywhere. The chips are
    # h52 (r26) and the pills are h38 (r19); pre-dividing gives each a true 1px rim.
    for name, radius in (("S_GV_ChipRing.png", 26), ("S_GV_PillRing.png", 19)):
        stroke = 88.0 / radius
        W, H, B = bake_chip_ring(os.path.join(OUT_DIR, name), stroke=stroke)
        print("%-24s %4dx%-4d border %d — sprite stroke %.3f -> 1px at r%d"
              % (name, W, H, B, stroke, radius))

    W, H = bake_separator(os.path.join(OUT_DIR, "S_GV_Separator.png"))
    print("%-24s %4dx%-4d white alpha 0 -> 0.9 -> 0 (node 14027:102118)" % ("S_GV_Separator.png", W, H))

    return 0


if __name__ == "__main__":
    sys.exit(main())
