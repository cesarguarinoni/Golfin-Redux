#!/usr/bin/env python3
"""gps_checkin — bake every panel, disc and pin the Rounds tab needs.

    python3 Docs/Scripts/make_gps_rounds_panels.py

Edit THIS, not the PNGs (Build rule 1: gradients come from tokens and are baked, never tinted).
Same contract and the same solvers as `make_score_upload_panels.py` /
`make_gps_gift_vote_panels.py`; nothing here re-implements a routine one of those already owns.

ONE SCREEN, ONE BACKDROP — read off the node, then MATCHED against the project's own art
────────────────────────────────────────────────────────────────────────────────────────
Both Rounds frames instance `Backgrounds` with `property1 = "Test"` (node 2419:8131). That plate
was downloaded and `object-cover`-fitted against every ≥800x1400 PNG in `Assets/Art` and
`Assets/Resources` the way the node draws it. Mean |dRGB| over the whole frame:

    Assets/Art/HomeScreen/Home Background.png                  0.002
    Assets/Art/Original UI/ClubsScreen/S_Inventory_Clubs_Bg1    34.3
    Assets/Art/HoleSelectScreen/Background.png                  38.2

0.002 against a next-best of 34 is not a close call — it is the same file. So the Rounds screen
needs NO new background asset, and every translucent card below is fitted over the plate it
actually sits on.

WHAT IS BAKED, AND WHY EACH ONE HAS TO BE
  cards        the standard GPS atom (`rgba(19,52,83,.6) -> rgba(9,27,51,.6)`, 3px border, r50).
               A vertical gradient cannot survive 9-slicing, so each is baked at its node size.
               The ACTIVE ROUND CARD and both MODAL SHELLS take the same fill with a #EEDC9A
               border instead of white — the one visual signal that says "this is live" / "this
               needs an answer", and it is a stroke colour, so it has to be in the bake.
  modal shell  958x760 over a 60% black scrim. It is fitted against the SCRIMMED plate, not the
               bare one: a card solved over the wrong backdrop is the `gps_profile_pack` scar
               (three gate rounds, rejected on sight each time).
  map fallback the stylised tile from the frame, for when /venue/map cannot answer (§C4). Opaque,
               so no fit — and DELIBERATELY not a pretty map: it is a placeholder, and one that
               looked real would hide an outage instead of showing it.
  spot disc    the 80px icon ring, SPLIT INTO TWO IMAGES — a navy gradient disc and a white
               annulus. The node draws three variants of this ring differing only in stroke
               colour (gold course / green partner / orange food) and the row is ONE template
               bound at runtime, so the stroke has to be tintable. A single baked ring would need
               three prefabs, and a fourth (a partner range) would need a fourth.
  pins         same split, same reason: 44px disc tinted by category + a white rim-and-centre
               drawn over it.
  player dot   fixed colours (#4F86D6 at 25% + a 24px core with a 3px white stroke), so it is one
               bake.
  legend dot   an 18px white disc, tinted three ways by the legend row.

NOT BAKED, DELIBERATELY — these reuse an existing atom (Build rule 9 / Rule 19 provenance):
  gold buttons        `Assets/Art/HomeScreen/Play Button.png` — the Main Buttons Gold atom, which
                      is what node I14077:34016;2541:11884 IS (gradient #FCF149->#BB7F1D, 2px
                      #FFE48B rim, r20, 39px SemiBold #321506 label).
  dark capsules       `Assets/Art/Tournaments/S_PillStadium.png` at ppum 88/r for the TOO FAR /
                      NO GPS / DETAILS fill, the unselected chip, the recenter pill and every
                      status pill's translucent interior.
  1px rims            `S_GV_PillRing.png` (calibrated at r19) and `S_GV_ChipRing.png` (r26) — the
                      hollow capsule outlines gps_gifts_votes baked for exactly this job.
  gold chip fill      `S_SU_GoldSegment.png` — already the #F3ECC2 -> #C9A94F capsule the selected
                      category chip needs.
  panel separator     `S_GV_Separator.png` — the white 0 -> 0.9 -> 0 header rule.
  pin glyph           `ICO_GpsPin.png`, the GPS Icons component's Pin (14019:32947).
"""
import importlib.util
import os
import sys

import numpy as np
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
CIRCLE_SCALE = 4

BLUE_TOP = su.BLUE_TOP          # (0x13, 0x34, 0x53, 0.60)
BLUE_BOTTOM = su.BLUE_BOTTOM    # (0x09, 0x1B, 0x33, 0.60)

GOLD = (0xEE, 0xDC, 0x9A, 1.00)
NAVY_TOP = "#204B76"
NAVY_BOTTOM = "#0B203D"

BG_ROUNDS = "Assets/Art/HomeScreen/Home Background.png"

# ── Canvas geometry (1170x2532) ──────────────────────────────────────────────────────
# Content Container is (96, 361, 978, 1860) on BOTH frames and every panel sits at x=10
# inside it, i.e. canvas x = 106. canvas y = 361 + the node's own y.
CC_X, CC_Y = 96, 361
PX = CC_X + 10          # 106

# ── The two backgrounds, registered with the shared solver ───────────────────────────
# `su.fit_over_background(step, ...)` indexes su.BACKGROUNDS by path and caches the loaded array
# in su._BG_CACHE. Index BG is a real file appended to that list; index SCRIM has NO file — it is
# the same plate with 60% black composited over it, seeded straight into the cache. Reusing the
# solver this way is the point: the least-squares fit that made every other GPS card correct is
# the one that runs here, and the only thing this file supplies is the backdrop.
BG = len(su.BACKGROUNDS)
su.BACKGROUNDS.append(BG_ROUNDS)
SCRIM = BG + 1

# The two modal frames put the panel over `bg-[rgba(0,0,0,0.6)]`.
SCRIM_ALPHA = 0.6


def _seed_scrim_cache():
    """Seed su._BG_CACHE[SCRIM] with the plate under a 60% black scrim."""
    im = Image.open(os.path.join(REPO, BG_ROUNDS)).convert("RGB")
    if im.size != (1170, 2532):
        im = im.resize((1170, 2532), Image.LANCZOS)
    arr = np.asarray(im).astype(np.float64)
    su._BG_CACHE[SCRIM] = arr * (1.0 - SCRIM_ALPHA)


# ═════════════════════════════════════════════════════════════════════════════════════
# Cards
# ═════════════════════════════════════════════════════════════════════════════════════

# ── One panel does NOT solve over its own footprint, and the number says why ─────────
# MY RECENT ROUNDS sits at canvas y 1631..2103, where the plate is flat dark grass: its RGB
# standard deviation over that rect is (4.9, 5.6, 1.9), against (20, 14, 12) under the spot list
# and (72, 63, 54) under the map. A least-squares fit needs the backdrop to VARY — with a
# near-constant B the system is under-determined and the solve wanders off to a value that
# reproduces this one rect and means nothing:
#
#     own footprint      rgb=(0, 0, 95)   a=0.215   ->  mean |dRGB| 4.85
#     spot list's rect   rgb=(15, 43, 56) a=0.752   ->  mean |dRGB| 3.68   <- BETTER, and sane
#
# So it borrows the well-conditioned solve from the panel 130 px above it, which is the SAME atom
# on the SAME plate. Measured, not assumed: both numbers above are from the check in the report.
# (Shipped GPS cards sit at 5.1–7.8 with this solver, so 3.68 is comfortably inside the family.)
FIT_RECT_OVERRIDE = {
    "S_GR_History.png": (PX, CC_Y + 780, 958, 470),      # the Spot List Panel's footprint
}

# name, w, h, radius, border rgb, bg index, canvas_x, canvas_y, node
CARDS = [
    # ── list state — GPS Rounds - Check-in (list) 14076:33800 ────────────────────────
    ("S_GR_MapPanel.png",   958, 560, 50, (255, 255, 255, 1.0), BG,    PX, CC_Y + 140,  "14077:33884"),
    ("S_GR_SpotList.png",   958, 470, 50, (255, 255, 255, 1.0), BG,    PX, CC_Y + 780,  "14077:33961"),
    ("S_GR_History.png",    958, 472, 50, (255, 255, 255, 1.0), BG,    PX, CC_Y + 1270, "14077:100404"),

    # ── active state — GPS Rounds - Active round 14077:100447 ────────────────────────
    # The ONE card with a gold border. Fitted at its own y (60), which is where it sits when the
    # chips are gone; the map/list/sort below it shift down by 280 and are the SAME sprites,
    # fitted at their list-state y. That is a real approximation and it is bounded: the plate is a
    # slow sunset gradient, so 280 px of vertical drift is under a unit of dRGB. Measured rather
    # than assumed — see the report's per-panel table.
    ("S_GR_ActiveCard.png", 958, 340, 50, GOLD,            BG,    PX, CC_Y + 60,   "14077:100661"),

    # ── the shared modal shell — 14080:34292 / 14078:34155 ───────────────────────────
    # y = (2532 - 760) / 2 - 120 = 766, centred horizontally at (1170 - 958) / 2 = 106.
    ("S_GR_ModalPanel.png", 958, 760, 50, GOLD,            SCRIM, 106, 766,        "14080:34292"),
]


def bake_cards():
    for name, w, h, r, border, bg, x, y, node in CARDS:
        rect = FIT_RECT_OVERRIDE.get(name, (x, y, w, h))
        fit = su.fit_over_background(bg, rect, BLUE_TOP, BLUE_BOTTOM)
        # su.bake_card paints su.BORDER; swap it for this card's own colour around the call
        # rather than adding a parameter to a routine four other bakers already depend on.
        prev = su.BORDER
        su.BORDER = border
        try:
            W, H = su.bake_card(os.path.join(OUT_DIR, name), w, h, r,
                                BLUE_TOP, BLUE_BOTTOM, fit=fit)
        finally:
            su.BORDER = prev
        fit_txt = "opaque" if fit is None else f"fit rgb={tuple(round(c) for c in fit[0])} a={fit[1]:.3f}"
        print(f"  {name:24s} {W}x{H}  node {node}  {fit_txt}")


# ═════════════════════════════════════════════════════════════════════════════════════
# The map fallback tile
# ═════════════════════════════════════════════════════════════════════════════════════

# Node 14077:33927 — the stylised placeholder the frame draws where the live tile goes.
MAP_BODY = (0x0B, 0x20, 0x38)
MAP_ROAD = (0x1B, 0x3B, 0x5C)
MAP_COURSE = (0x14, 0x44, 0x2F)

# name, x, y, w, h  (in the surface's own 918x420 space, from the node's children)
MAP_ROADS = [
    (0, 150, 918, 10),
    (0, 300, 918, 8),
    (420, 0, 10, 420),
]
MAP_COURSE_RECT = (560, 190, 300, 180)


def bake_map_fallback(path, w=918, h=420, radius=36):
    """The stylised tile, for when /venue/map cannot answer.

    OPAQUE, so there is no fit: it stands where a photograph would, and anything showing through
    it would read as a rendering bug rather than as a fallback. The pins, the player dot and the
    legend are drawn OVER it by the screen exactly as they are over a real tile, so the panel
    keeps working — only the map underneath is a drawing.
    """
    sc = SCALE
    W, H, R = w * sc, h * sc, radius * sc

    img = Image.new("RGBA", (W, H), MAP_BODY + (255,))
    d = ImageDraw.Draw(img)
    for rx, ry, rw, rh in MAP_ROADS:
        d.rounded_rectangle([rx * sc, ry * sc, (rx + rw) * sc - 1, (ry + rh) * sc - 1],
                            radius=min(rw, rh) * sc / 2.0, fill=MAP_ROAD + (255,))
    cx, cy, cw, ch = MAP_COURSE_RECT
    d.rounded_rectangle([cx * sc, cy * sc, (cx + cw) * sc - 1, (cy + ch) * sc - 1],
                        radius=40 * sc, fill=MAP_COURSE + (255,))

    img.putalpha(su._rounded_mask(W, H, R))
    img.save(path)
    return W, H


# ═════════════════════════════════════════════════════════════════════════════════════
# Discs, rings and pins
# ═════════════════════════════════════════════════════════════════════════════════════

def bake_disc(path, size, top, bottom, scale=CIRCLE_SCALE, aa=4):
    """A vertical-gradient disc with NO stroke — the inside half of the split icon ring.

    Split from its stroke on purpose (see the module header): the Rounds spot row is one template
    whose ring colour is bound per category at runtime, and a stroke baked into the disc could
    only be tinted by tinting the disc with it.
    """
    D = size * scale
    ft, fb = ring._rgb(top), ring._rgb(bottom)

    img = Image.new("RGBA", (D, D))
    px = img.load()
    for y in range(D):
        t = y / max(1, D - 1)
        row = ring._lerp(ft, fb, t) + (255,)
        for x in range(D):
            px[x, y] = row

    mask = Image.new("L", (D * aa, D * aa), 0)
    ImageDraw.Draw(mask).ellipse([0, 0, D * aa - 1, D * aa - 1], fill=255)
    img.putalpha(mask.resize((D, D), Image.LANCZOS))
    img.save(path)
    return D


def bake_ring_hi(path, size, stroke, scale=CIRCLE_SCALE, aa=4):
    """A white annulus at CIRCLE_SCALE, tinted at runtime.

    `su.bake_ring` does the same thing at SCALE (2), which is right for the atoms it was written
    for; an 80pt ring at 2x is 160 px and lands soft on a 3x device. Same construction, one
    argument different — and it stays here rather than becoming a parameter on su.bake_ring
    because four other bakers depend on that signature.
    """
    D = size * scale
    A = D * aa
    mask = Image.new("L", (A, A), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([0, 0, A - 1, A - 1], fill=255)
    inset = stroke * scale * aa
    d.ellipse([inset, inset, A - 1 - inset, A - 1 - inset], fill=0)
    mask = mask.resize((D, D), Image.LANCZOS)
    Image.merge("RGBA", (Image.new("L", (D, D), 255),) * 3 + (mask,)).save(path)
    return D


def bake_dot(path, size, scale=CIRCLE_SCALE, aa=4):
    """A plain white disc, tinted at runtime. The legend bullets and the pin fills."""
    D = size * scale
    mask = Image.new("L", (D * aa, D * aa), 0)
    ImageDraw.Draw(mask).ellipse([0, 0, D * aa - 1, D * aa - 1], fill=255)
    mask = mask.resize((D, D), Image.LANCZOS)
    Image.merge("RGBA", (Image.new("L", (D, D), 255),) * 3 + (mask,)).save(path)
    return D


def bake_pin_rim(path, size=44, stroke=3, centre=14, scale=CIRCLE_SCALE, aa=4):
    """The white parts of a map pin: a `stroke`px rim and a `centre`px core, one image.

    Both are white and both sit OVER the tinted fill disc, so they are one sprite rather than two
    — a pin is three GameObjects at 50 pins on screen otherwise, and the pin layer is repainted on
    every category switch and every pan.
    """
    D = size * scale
    A = D * aa
    mask = Image.new("L", (A, A), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([0, 0, A - 1, A - 1], fill=255)
    inset = stroke * scale * aa
    d.ellipse([inset, inset, A - 1 - inset, A - 1 - inset], fill=0)
    c = A / 2.0
    r = centre * scale * aa / 2.0
    d.ellipse([c - r, c - r, c + r, c + r], fill=255)
    mask = mask.resize((D, D), Image.LANCZOS)
    Image.merge("RGBA", (Image.new("L", (D, D), 255),) * 3 + (mask,)).save(path)
    return D


def bake_player_dot(path, size=60, halo_alpha=0.25, core=24, stroke=3,
                    scale=CIRCLE_SCALE, aa=4):
    """"You Are Here" (node 14077:33945): a 60px #4F86D6 halo at 25%, a 24px core of the same
    hue, and a 3px white ring around the core. Fixed colours, so it is ONE bake — nothing about
    this marker varies with the data."""
    hue = ring._rgb("#4F86D6")
    D = size * scale
    A = D * aa

    halo = Image.new("L", (A, A), 0)
    ImageDraw.Draw(halo).ellipse([0, 0, A - 1, A - 1], fill=int(round(halo_alpha * 255)))
    halo = halo.resize((D, D), Image.LANCZOS)
    out = Image.merge("RGBA", tuple(Image.new("L", (D, D), c) for c in hue) + (halo,))

    c = A / 2.0
    r_out = (core / 2.0 + stroke) * scale * aa
    rim = Image.new("L", (A, A), 0)
    ImageDraw.Draw(rim).ellipse([c - r_out, c - r_out, c + r_out, c + r_out], fill=255)
    rim = rim.resize((D, D), Image.LANCZOS)
    out = Image.alpha_composite(
        out, Image.merge("RGBA", (Image.new("L", (D, D), 255),) * 3 + (rim,)))

    r_core = core / 2.0 * scale * aa
    core_mask = Image.new("L", (A, A), 0)
    ImageDraw.Draw(core_mask).ellipse([c - r_core, c - r_core, c + r_core, c + r_core], fill=255)
    core_mask = core_mask.resize((D, D), Image.LANCZOS)
    out = Image.alpha_composite(
        out, Image.merge("RGBA", tuple(Image.new("L", (D, D), ch) for ch in hue) + (core_mask,)))

    out.save(path)
    return D


# ═════════════════════════════════════════════════════════════════════════════════════

def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    _seed_scrim_cache()

    print("cards (fitted over " + BG_ROUNDS + "):")
    bake_cards()

    print("map fallback:")
    w, h = bake_map_fallback(os.path.join(OUT_DIR, "S_GPS_MapFallback.png"))
    print(f"  S_GPS_MapFallback.png    {w}x{h}  node 14077:33927")

    print("discs, rings and pins:")
    d = bake_disc(os.path.join(OUT_DIR, "S_GR_SpotDisc.png"), 80, NAVY_TOP, NAVY_BOTTOM)
    print(f"  S_GR_SpotDisc.png        {d}x{d}  node 14077:34006 (fill only)")

    d = bake_ring_hi(os.path.join(OUT_DIR, "S_GR_SpotRing.png"), 80, 3)
    print(f"  S_GR_SpotRing.png        {d}x{d}  node 14077:34006 (stroke only, tinted)")

    d = bake_dot(os.path.join(OUT_DIR, "S_GR_PinFill.png"), 44)
    print(f"  S_GR_PinFill.png         {d}x{d}  node 14077:33933 (fill, tinted)")

    d = bake_pin_rim(os.path.join(OUT_DIR, "S_GR_PinRim.png"))
    print(f"  S_GR_PinRim.png          {d}x{d}  node 14077:33933 (rim + centre)")

    d = bake_player_dot(os.path.join(OUT_DIR, "S_GR_PlayerDot.png"))
    print(f"  S_GR_PlayerDot.png       {d}x{d}  node 14077:33945")

    d = bake_dot(os.path.join(OUT_DIR, "S_GR_Dot18.png"), 18)
    print(f"  S_GR_Dot18.png           {d}x{d}  node 14077:33951 (legend, tinted)")

    # The modal's 120px icon ring. The atom's own routine at the node's own numbers
    # (14080:34xxx: 120 box, r=57, stroke 6, #F3ECC2 solid) rather than S_GpsIconRing_Feature
    # scaled from 96, which would land the stroke at 6.67.
    img = ring.bake(120, 57.0, 6.0, NAVY_TOP, NAVY_BOTTOM, "#F3ECC2", "#F3ECC2", CIRCLE_SCALE)
    img.save(os.path.join(OUT_DIR, "S_GR_ModalRing.png"))
    print(f"  S_GR_ModalRing.png       {img.size[0]}x{img.size[1]}  node 14080:34292 icon ring")

    print("\nDone. Re-import in Unity (the builder's EnsureImport forces Sprite mode).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
