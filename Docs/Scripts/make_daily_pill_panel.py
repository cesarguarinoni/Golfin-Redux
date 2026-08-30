#!/usr/bin/env python3
"""Bake the Daily Mission pill's panel sprite from the Figma node's own tokens.

daily_mission_home_pill — Figma `5gEAHjl6xAtW8iYY7NMvWd` node `13994:1963`
("Mission Card Container"), pulled with `get_design_context` on 2026-08-30:

    root  "Mission Card Container"  549x122, rounded 50, border 3px #FCF195,
                                    bg-gradient-to-b from #133453 to #091B33
    child "Pop-Up"                  rounded 50, border 1px #0A1D35

WHY THIS IS GENERATED AND NOT REUSED. A whole-project scan of every 9-sliced UI
sprite (44 of them) found no navy panel with a pale-gold border: the two navy
panels in the palette carry a steel-blue (`Background - Next Hole.png`, #3E7CA8)
and a silver-white (`Next Hole Panel.png`, #FFFFFF→#C0C6CE) edge, and every
gold-edged sprite is a solid-gold BUTTON. The atom is genuinely absent
(UI_ELEMENT_PALETTE "justify 'pulled from Figma' only if the atom is genuinely
absent"), and the node's style is four numbers, so it is reproduced exactly here
rather than approximated with the wrong-coloured neighbour.

WHY A BAKED SPRITE AND NOT A 9-SLICE. The pill is a FIXED 549x122 element that
never stretches, and its 50px radius on a 122px-tall box is most of the half
height — 9-slicing that is the corner-collapse trap (`reference_fixed_size_pill_
capsule_sprite`, PIPELINE_HARDENING C3/Rule 21 render-health). A full bake at 2x,
drawn as `Image.Type.Simple`, has no corners to collapse.

It also bakes the pill's GLOW. The spec allows "a soft-blurred copy of the border"; the first
attempt reused the panel sprite itself, outset 10px on an additive material, and it read as a
crisp second gold outline rather than a halo — a duplicated border, not a glow. So the glow is
its own sprite: the pill silhouette in the border gold, Gaussian-blurred, on a canvas with room
for the falloff.

Run:  python3 Docs/Scripts/make_daily_pill_panel.py
Out:  Assets/Art/HomeScreen/S_DailyPillPanel.png   (1098x244, 2x)
      Assets/Art/HomeScreen/S_DailyPillGlow.png    (1242x388, 2x, 36px bleed each side)
"""
from __future__ import annotations

import os

from PIL import Image, ImageDraw, ImageFilter

# ── The node's tokens, 1x ────────────────────────────────────────────────────
W, H = 549, 122
RADIUS = 50
GOLD = (0xFC, 0xF1, 0x95, 255)   # border-3 #FCF195
INNER = (0x0A, 0x1D, 0x35, 255)  # Pop-Up border-1 #0A1D35
TOP = (0x13, 0x34, 0x53)         # gradient from
BOT = (0x09, 0x1B, 0x33)         # gradient to
GOLD_PX = 3
INNER_PX = 1

SCALE = 2          # what ships
SS = 4             # supersample factor for clean curves
OUT = "Assets/Art/HomeScreen/S_DailyPillPanel.png"

GLOW_OUT = "Assets/Art/HomeScreen/S_DailyPillGlow.png"
GLOW_MARGIN = 36  # 1x px of falloff room on every side
GLOW_BLUR = 13    # 1x px Gaussian radius


def rounded(size, radius, fill):
    """One rounded-rect layer on a transparent canvas."""
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle([0, 0, size[0] - 1, size[1] - 1], radius=radius, fill=fill)
    return img


def build() -> Image.Image:
    s = SCALE * SS
    w, h = W * s, H * s

    # 1. The gold plate — the outermost 3px ring is whatever of this stays uncovered.
    out = rounded((w, h), RADIUS * s, GOLD)

    # 2. The 1px inner rule, inset by the gold border.
    i1 = GOLD_PX * s
    out.alpha_composite(rounded((w - 2 * i1, h - 2 * i1), max(1, (RADIUS - GOLD_PX) * s), INNER), (i1, i1))

    # 3. The body gradient, inset by gold + inner rule. Vertical, top → bottom.
    i2 = (GOLD_PX + INNER_PX) * s
    bw, bh = w - 2 * i2, h - 2 * i2
    grad = Image.new("RGBA", (1, bh))
    for y in range(bh):
        t = y / max(1, bh - 1)
        grad.putpixel((0, y), (
            round(TOP[0] + (BOT[0] - TOP[0]) * t),
            round(TOP[1] + (BOT[1] - TOP[1]) * t),
            round(TOP[2] + (BOT[2] - TOP[2]) * t),
            255,
        ))
    body = grad.resize((bw, bh), Image.NEAREST)
    # Mask it to the same rounded shape so the gradient does not square off the corners.
    body.putalpha(rounded((bw, bh), max(1, (RADIUS - GOLD_PX - INNER_PX) * s), (255, 255, 255, 255)).getchannel("A"))
    out.alpha_composite(body, (i2, i2))

    return out.resize((W * SCALE, H * SCALE), Image.LANCZOS)


def build_glow() -> Image.Image:
    """The pill silhouette in border-gold, blurred — a halo, not a second outline."""
    s = SCALE * SS
    m = GLOW_MARGIN * s
    w, h = W * s + 2 * m, H * s + 2 * m
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    img.alpha_composite(rounded((W * s, H * s), RADIUS * s, GOLD), (m, m))
    img = img.filter(ImageFilter.GaussianBlur(GLOW_BLUR * s))
    # Premultiplied-ish: keep the colour flat gold and let alpha carry the falloff, so the
    # additive material tints evenly instead of darkening toward the edge.
    a = img.getchannel("A")
    flat = Image.new("RGBA", img.size, GOLD[:3] + (255,))
    flat.putalpha(a)
    return flat.resize(((W + 2 * GLOW_MARGIN) * SCALE, (H + 2 * GLOW_MARGIN) * SCALE), Image.LANCZOS)


def main() -> None:
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    path = os.path.join(root, OUT)
    img = build()
    img.save(path)
    print(f"wrote {OUT}  {img.size[0]}x{img.size[1]}  ({SCALE}x of {W}x{H})")

    glow = build_glow()
    glow.save(os.path.join(root, GLOW_OUT))
    print(f"wrote {GLOW_OUT}  {glow.size[0]}x{glow.size[1]}  "
          f"({SCALE}x of {W + 2 * GLOW_MARGIN}x{H + 2 * GLOW_MARGIN}, blur {GLOW_BLUR}px)")


if __name__ == "__main__":
    main()
