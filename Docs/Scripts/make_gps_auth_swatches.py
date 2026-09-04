#!/usr/bin/env python3
"""Bake the auth_golf_profile sprites from Figma tokens.

    python3 Docs/Scripts/make_gps_auth_swatches.py

Edit THIS, not the PNGs (Build rule 1: gradients are baked from tokens, never tinted).

TWO SCREENS, ONE BACKDROP
  `Auth - Golf Profile (post-signup)` 14029:33628 and `Auth - Welcome Tutorial` 14029:33929 both
  name `Backgrounds` variant **Splash**, which matched `Assets/Art/SplashScreen/Splash - Background.png`
  at mean |dRGB| 6.3 over the un-panelled lower two thirds of the node render (every other
  background in the project scored 58+). So all the translucent cards on BOTH screens are fitted
  over that one photo.

WHAT IS BAKED, AND WHY EACH ONE HAS TO BE
  panels/tiles   the node's `bg-gradient-to-b rgba(19,52,83,.6) -> rgba(9,27,51,.6)` + 3px white
                 border — the standard GPS card atom, same token family as every S_PROF_* panel.
                 Fitted per card against its OWN footprint of the photo (fit_over_background).
  input / chip   `bg-[rgba(0,0,0,0.35)]` + a 2px #818EA1 border. Their backdrop is NOT the photo,
                 it is the PANEL — so the fit runs against a synthetic backdrop built by
                 compositing the panel over the photo exactly the way Figma does, in sRGB.
                 (Fitting these against the bare photo would over-darken them by the panel's own
                 contribution; that is the same class of error `fit_over_background` exists for.)
  chip ON        `#f3ecc2 -> #c9a94f` + 1px #422100 — OPAQUE, so no fit: there is no background
                 left to get wrong.
  swatches       four avatar-colour discs, each in two states. The node draws them as
                 `<circle r=R stroke=W>` where `R + W/2 == size/2`, i.e. the stroke's outer edge
                 IS the bounding box — the identical geometry the GPS icon-ring atom uses, so
                 they are baked by that atom's own `bake()` rather than by a second circle
                 routine. Unselected 100px / 4px #F3ECC2; selected 120px / 8px #EEDC9A.
  avatar rings   the GPS Profile hero disc, as the ICON-RING ATOM (88px / r41.5 / stroke 5,
                 #F3ECC2->#98855B rim) with the swatch gradient in place of the navy fill.
                 NOT a plain disc behind S_GpsIconRing_Tile: that atom is a FILLED circle, not
                 an annulus, so a disc drawn behind it is completely invisible.

TOKENS, read out of the node SVGs on 2026-09-02 (get_design_context asset URLs), not eyeballed.
"""
import importlib.util
import os
import sys

import numpy as np
from PIL import Image, ImageChops

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
SPLASH = "Assets/Art/SplashScreen/Splash - Background.png"

# The card token family, straight from make_score_upload_panels (one owner, no second copy).
BLUE_TOP = su.BLUE_TOP        # (0x13, 0x34, 0x53, 0.60)
BLUE_BOTTOM = su.BLUE_BOTTOM  # (0x09, 0x1B, 0x33, 0.60)

# Field / chip tokens — node 14029:33906, :33911, :33913.
FIELD_FILL = (0x00, 0x00, 0x00, 0.35)
FIELD_STROKE = (0x81, 0x8E, 0xA1)
CHIP_ON_TOP = (0xF3, 0xEC, 0xC2, 1.00)
CHIP_ON_BOTTOM = (0xC9, 0xA9, 0x4F, 1.00)
CHIP_ON_STROKE = (0x42, 0x21, 0x00)

# ── Canvas geometry (1170x2532). ContentContainer = (96, 361) on BOTH frames. ──────────
CC_X, CC_Y = 96, 361

# Golf Profile: panel at CC(10, 0) -> canvas (106, 361), 958x731.
GP_PANEL = (CC_X + 10, CC_Y + 0, 958, 731)
# Inside it: fields are at panel-local (40, 355) and (40, 621); chips row at (40, 499).
GP_INPUT_1 = (GP_PANEL[0] + 40, GP_PANEL[1] + 355, 878, 80)
GP_CHIP_0 = (GP_PANEL[0] + 40, GP_PANEL[1] + 499, 285, 60)

# Welcome: panel at CC(10, 55) -> canvas (106, 416), 958x385; tiles from CC(10, 464).
WC_PANEL = (CC_X + 10, CC_Y + 55, 958, 385)
WC_TILE = (CC_X + 10, CC_Y + 464, 470, 228)

# ── Panels fitted over the bare photo ─────────────────────────────────────────────────
# name, w, h, radius, canvas rect
PANELS = [
    ("S_AUTH_GolfProfilePanel.png", 958, 731, 50, GP_PANEL),
    ("S_AUTH_WelcomePanel.png",     958, 385, 50, WC_PANEL),
    # One tile sprite for all four features: they are identical cards, and the photo behind them
    # is the same blurred bunker wall across the 958x474 grid. Fitted at the top-left tile, the
    # same convention S_PROF_QuickStatTile / S_PROF_UnlockTile already use for repeated cards.
    ("S_AUTH_FeatureTile.png",      470, 228, 50, WC_TILE),
]

# ── Controls fitted over the PANEL (synthetic backdrop, see _panel_backdrop) ───────────
# name, w, h, radius, stroke rgb, stroke px, canvas rect
FIELDS = [
    ("S_AUTH_InputBox.png", 878, 80, 24, FIELD_STROKE, 2, GP_INPUT_1),
    ("S_AUTH_ChipOff.png",  285, 60, 30, FIELD_STROKE, 2, GP_CHIP_0),
]

# ── Circles: the icon-ring atom's own geometry (r + stroke/2 == size/2) ────────────────
# name, size, radius, stroke, fill top, fill bottom, stroke top, stroke bottom
SWATCH_COLOURS = [
    ("Pink",  "#E57A97", "#B84E6B"),
    ("Green", "#4FA36B", "#2D6F45"),
    ("Blue",  "#4F86D6", "#2C5AA0"),
    ("Gold",  "#C7A04A", "#8A6A22"),
]
SWATCH_OFF = (100, 48.0, 4.0, "#F3ECC2")   # node 14029:33892 / :33898 / :33901
SWATCH_ON = (120, 56.0, 8.0, "#EEDC9A")    # node 14029:33895 (the selected state the frame shows)

CIRCLE_SCALE = 4


def _splash():
    """The Splash plate as the 1170x2532 the screen actually draws."""
    im = Image.open(os.path.join(REPO, SPLASH)).convert("RGB")
    if im.size != (1170, 2532):
        im = im.resize((1170, 2532), Image.LANCZOS)
    return np.asarray(im).astype(np.float64)


def _panel_backdrop():
    """The Splash photo with the Golf Profile panel composited on top, IN sRGB — i.e. what Figma
    actually draws behind the input boxes and chips.

    Composite is `T = a*F + (1-a)*B` with F the panel's vertical gradient and a its 0.6, applied
    over the panel's footprint only. Everything outside the panel stays the bare photo, which
    keeps this usable as a drop-in background for any rect on the screen.
    """
    bg = _splash().copy()
    x, y, w, h = GP_PANEL
    rows = np.linspace(0.0, 1.0, h)[:, None]
    F = np.stack([np.full((h, w), 0.0) + BLUE_TOP[i] + (BLUE_BOTTOM[i] - BLUE_TOP[i]) * rows
                  for i in range(3)], axis=2)
    a = (BLUE_TOP[3] + (BLUE_BOTTOM[3] - BLUE_TOP[3]) * rows)[:, :, None]
    bg[y:y + h, x:x + w, :] = a * F + (1.0 - a) * bg[y:y + h, x:x + w, :]
    return bg


def bake_field(path, w, h, radius, fill, stroke_rgb, stroke_px, fit):
    """A control: translucent rounded fill + a crisp N-px solid border, straight alpha.

    Same construction as su.bake_card — the border is the difference between the outer mask and
    an inset one, so it follows the radius at a uniform width. It is NOT a UI `Outline` component
    (Rule 21 fails those: four offset copies, not a stroke). The only difference from bake_card is
    that the border colour is a parameter rather than always white.
    """
    sc = su.SCALE
    W, H, R, S = w * sc, h * sc, radius * sc, int(round(stroke_px * sc))

    rgb, alpha = (fill[:3], fill[3]) if fit is None else (fit[0], fit[1])

    body_rgba = tuple(int(round(c)) for c in rgb) + (int(round(alpha * 255)),)
    fill_img = Image.new("RGBA", (W, H), body_rgba)

    outer = su._rounded_mask(W, H, R)
    inner = Image.new("L", (W, H), 0)
    inner.paste(su._rounded_mask(W - 2 * S, H - 2 * S, max(0, R - S)), (S, S))

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    body = fill_img.copy()
    body.putalpha(ImageChops.multiply(fill_img.getchannel("A"), inner))
    out = Image.alpha_composite(out, body)

    border = Image.new("RGBA", (W, H), tuple(int(c) for c in stroke_rgb) + (0,))
    border.putalpha(ImageChops.subtract(outer, inner))
    out = Image.alpha_composite(out, border)

    out.save(path)
    return W, H


def bake_chip_on(path, w, h, radius):
    """The selected experience chip: an OPAQUE gold gradient + a 1px #422100 border.

    Opaque, so `fit` does not apply — su.bake_card handles it directly once its module-level
    BORDER/STROKE are pointed at this node's border instead of the cards' 3px white.
    """
    saved_border, saved_stroke = su.BORDER, su.STROKE
    su.BORDER = CHIP_ON_STROKE + (1.00,)
    su.STROKE = 1
    try:
        return su.bake_card(path, w, h, radius, CHIP_ON_TOP, CHIP_ON_BOTTOM)
    finally:
        su.BORDER, su.STROKE = saved_border, saved_stroke


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    # Both fit surfaces are installed into the shared solver's cache, so fit_over_background()
    # (the proven least-squares solve) is reused unchanged for both classes of card.
    su.BACKGROUNDS[0] = SPLASH
    su._BG_CACHE.clear()
    su._BG_CACHE[0] = _splash()
    su._BG_CACHE[1] = _panel_backdrop()

    print("── Cards over the Splash plate ─────────────────────────────────────────────")
    for name, w, h, r, rect in PANELS:
        fit = su.fit_over_background(0, rect, BLUE_TOP, BLUE_BOTTOM)
        W, H = su.bake_card(os.path.join(OUT_DIR, name), w, h, r, BLUE_TOP, BLUE_BOTTOM, fit=fit)
        rgb, a = fit
        print("%-32s %4dx%-4d (node %dx%d r%d) -> rgb(%d,%d,%d) a=%.3f"
              % (name, W, H, w, h, r, rgb[0], rgb[1], rgb[2], a))

    print("── Controls over the Golf Profile panel ────────────────────────────────────")
    for name, w, h, r, stroke_rgb, stroke_px, rect in FIELDS:
        fit = su.fit_over_background(1, rect, FIELD_FILL, FIELD_FILL)
        W, H = bake_field(os.path.join(OUT_DIR, name), w, h, r, FIELD_FILL,
                          stroke_rgb, stroke_px, fit)
        rgb, a = fit
        print("%-32s %4dx%-4d (node %dx%d r%d, %dpx #%02X%02X%02X border) -> rgb(%d,%d,%d) a=%.3f"
              % (name, W, H, w, h, r, stroke_px, stroke_rgb[0], stroke_rgb[1], stroke_rgb[2],
                 rgb[0], rgb[1], rgb[2], a))

    W, H = bake_chip_on(os.path.join(OUT_DIR, "S_AUTH_ChipOn.png"), 285, 60, 30)
    print("%-32s %4dx%-4d (node 284.67x60 r100, 1px #422100 border) -> opaque #F3ECC2->#C9A94F"
          % ("S_AUTH_ChipOn.png", W, H))

    print("── Avatar swatch discs (icon-ring atom geometry) ───────────────────────────")
    for label, top, bottom in SWATCH_COLOURS:
        for state, (size, radius, stroke, stroke_hex) in (("Off", SWATCH_OFF), ("On", SWATCH_ON)):
            name = "S_AUTH_Swatch%s_%s.png" % (label, state)
            img = ring.bake(size, radius, stroke, top, bottom, stroke_hex, stroke_hex,
                            CIRCLE_SCALE)
            img.save(os.path.join(OUT_DIR, name))
            print("%-32s %4dx%-4d (node %dpx r=%.1f stroke=%.1f %s, fill %s->%s)"
                  % (name, img.width, img.height, size, radius, stroke, stroke_hex, top, bottom))

        # The GPS Profile hero disc is the ICON-RING ATOM with the avatar colour as its FILL —
        # not a separate disc behind the ring.
        #
        # A fill-only disc under S_GpsIconRing_Tile is INVISIBLE: that atom is a filled navy
        # circle with a gold rim (make_gps_icon_ring.bake paints the fill out to r_out and the
        # stroke over it), not an annulus, so anything drawn behind it is completely covered.
        # The first build did exactly that and the hero disc stayed navy in every colour.
        # So the colour goes where it can be seen: same 88px / r41.5 / stroke 5 geometry and the
        # same #F3ECC2->#98855B gold rim as the Tile atom, with the swatch gradient in place of
        # the navy fill.
        name = "S_AUTH_AvatarRing_%s.png" % label
        img = ring.bake(88, 41.5, 5, top, bottom, "#F3ECC2", "#98855B", CIRCLE_SCALE)
        img.save(os.path.join(OUT_DIR, name))
        print("%-32s %4dx%-4d (icon-ring atom, fill %s->%s, gold rim — GPS Profile hero)"
              % (name, img.width, img.height, top, bottom))

    return 0


if __name__ == "__main__":
    sys.exit(main())
