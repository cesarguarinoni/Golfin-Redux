#!/usr/bin/env python3
"""Bake the store's `-N%` corner badge from the Figma node's own tokens.

iap_plumbing — Figma `5gEAHjl6xAtW8iYY7NMvWd` › `14287:33138` ("Badge/Discount", the
dual-priced card's corner badge), pulled with `get_design_context` + a 4× node render on
2026-09-15:

    109×47, rounded 24 (a full capsule at this height), border 2px #FFB3BD (flat — the
    4× render's edge rows are a single colour, unlike the gold pills' gradient stroke),
    bg-gradient-to-b #F0566A → #A8172B, drop shadow 0 4 4 rgba(0,0,0,0.35),
    text "-25%" Rubik SemiBold 26 white, tracking 1.04 (the text is NOT baked — it is a
    TMP child, the percentage is per row).

WHY BAKED. A whole-project scan of the pill atoms found no red gradient capsule:
`S_PillStadium` is a flat white stadium meant to be TINTED (one colour by definition), and
the two-layer badge pattern draws a dark inner fill — neither can carry a two-stop red
gradient. The node's style is four numbers, so it is reproduced exactly here.

WHY NOT A 9-SLICE. The badge is a FIXED 109×47 element whose 24 px radius is more than half
its height — 9-slicing that is the corner-collapse trap (reference_fixed_size_pill_capsule_sprite,
PIPELINE_HARDENING C3 / Rule 21 render-health). A full bake at 2×, drawn `Image.Type.Simple`
at 125×67, has no corners to collapse.

THE CANVAS CARRIES THE SHADOW. 8 px bleed left/right/top and 12 px bottom (1×), so the
sprite is 125×67 and the badge body sits at (8, 8) inside it — the prefab positions the
BODY at the node's offset from the price plate (+8 right, +18 up) and adds the bleed.

Run:  python3 Docs/Scripts/make_discount_badge.py
Out:  Assets/Art/Shop/S_DiscountBadge.png   (250×134, 2×)
"""
from __future__ import annotations

import os

from PIL import Image, ImageDraw, ImageFilter

# ── The node's tokens, 1x ────────────────────────────────────────────────────
W, H = 109, 47
RADIUS = 24
BORDER_PX = 2
BORDER = (0xFF, 0xB3, 0xBD, 255)
TOP = (0xF0, 0x56, 0x6A)
BOT = (0xA8, 0x17, 0x2B)
SHADOW_DY, SHADOW_BLUR, SHADOW_ALPHA = 4, 4, 0.35
BLEED_L = BLEED_R = BLEED_T = 8
BLEED_B = 12

SCALE = 2
OUT = os.path.join("Assets", "Art", "Shop", "S_DiscountBadge.png")


def capsule_mask(w: int, h: int, radius: int, ss: int = 4) -> Image.Image:
    """Anti-aliased rounded rect, supersampled."""
    big = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(big).rounded_rectangle((0, 0, w * ss - 1, h * ss - 1), radius=radius * ss, fill=255)
    return big.resize((w, h), Image.LANCZOS)


def vertical_gradient(w: int, h: int, top: tuple, bot: tuple) -> Image.Image:
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(round(top[i] + (bot[i] - top[i]) * t) for i in range(3)) + (255,)
        for x in range(w):
            px[x, y] = c
    return img


def main() -> None:
    s = SCALE
    w, h = W * s, H * s
    cw, ch = (W + BLEED_L + BLEED_R) * s, (H + BLEED_T + BLEED_B) * s
    ox, oy = BLEED_L * s, BLEED_T * s

    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))

    # shadow: the capsule silhouette, black @ 0.35, offset down, blurred
    outer = capsule_mask(w, h, RADIUS * s)
    shadow = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    sh_alpha = Image.new("L", (cw, ch), 0)
    sh_alpha.paste(outer.point(lambda a: int(a * SHADOW_ALPHA)), (ox, oy + SHADOW_DY * s))
    sh_alpha = sh_alpha.filter(ImageFilter.GaussianBlur(SHADOW_BLUR * s / 2))
    shadow.putalpha(sh_alpha)
    canvas = Image.alpha_composite(canvas, shadow)

    # border capsule (flat #FFB3BD)
    border_layer = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    border_fill = Image.new("RGBA", (w, h), BORDER)
    border_layer.paste(border_fill, (ox, oy), outer)
    canvas = Image.alpha_composite(canvas, border_layer)

    # inner gradient capsule, inset by the border
    bp = BORDER_PX * s
    iw, ih = w - 2 * bp, h - 2 * bp
    inner = capsule_mask(iw, ih, RADIUS * s - bp)
    grad = vertical_gradient(iw, ih, TOP, BOT)
    inner_layer = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    inner_layer.paste(grad, (ox + bp, oy + bp), inner)
    canvas = Image.alpha_composite(canvas, inner_layer)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    canvas.save(OUT)
    print(f"wrote {OUT} {canvas.size} (badge body {w}x{h} at ({ox},{oy}), 1x = {W}x{H} at ({BLEED_L},{BLEED_T}))")


if __name__ == "__main__":
    main()
