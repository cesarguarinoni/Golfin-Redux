#!/usr/bin/env python3
"""Bake the Home GPS pill's sprite from the Figma node's own tokens.

gps_pill_entry — Figma `5gEAHjl6xAtW8iYY7NMvWd` node `14060:4638`
("Mission Card Container", the GPS pill), pulled with `get_design_context` 2026-09-02:

    root  "Mission Card Container"  138x92, rounded 50, border 3px #FCF195,
                                    bg-gradient-to-b from #133453 to #091B33
    child "Pop-Up"                  rounded 50, border 1px #0A1D35
    label "GPS"                     Rubik SemiBold 45 / lh 60 / tracking -0.69, #EEDC9A

SAME FAMILY, DIFFERENT BOX — AND THAT IS WHY THIS IS A SECOND SPRITE.
`Assets/Art/HomeScreen/S_DailyPillPanel.png` already carries these exact four tokens, so the
first instinct is to reuse it. It cannot serve this pill: it is 9-sliced with a 50px corner
(border 100 @ ppum 2) authored at height 122. This pill is **92** tall, and 50+50 corners do
not fit in 92 — the two halves overlap and Unity renders the collapsed-corner oval that
PIPELINE_HARDENING Rule 21's render-health check exists to catch.

So the pill is baked whole at its authored size and drawn `Image.Type.Simple`, per
`reference_fixed_size_pill_capsule_sprite`. That is safe here precisely because the size is
FIXED: Cesar's requirement is that localization keeps the pill's box and autosizes the TEXT
inside it, so this sprite never stretches and has no corners to collapse.

Radius 50 exceeds half the height (46), so the ends are true semicircles — a capsule, which is
what the node's render shows.

Recipe (gold plate → 1px inner rule → masked vertical gradient) is cloned deliberately from
`Docs/Scripts/make_daily_pill_panel.py`; the two sprites are the same component family and
should stay visually identical apart from the box.

Run:  python3 Docs/Scripts/make_gps_pill.py
Out:  Assets/Art/HomeScreen/S_GpsPill.png   (276x184, 2x of 138x92)

⚠️ After baking, Unity must import it as a **Sprite** — a default-imported texture returns null
from LoadAssetAtPath<Sprite> and the pill draws as a WHITE BOX (playbook §3).
"""
from __future__ import annotations

import os

from PIL import Image, ImageDraw

# ── The node's tokens, 1x ────────────────────────────────────────────────────
W, H = 138, 92
RADIUS = 50
GOLD = (0xFC, 0xF1, 0x95, 255)   # the border's TOP stop; still the flat colour the glow uses

# ⚠️ THE BORDER IS A THREE-STOP VERTICAL GRADIENT, AND `get_design_context` DOES NOT SAY SO.
# Its Tailwind output renders this stroke as `border-[#fcf195]` — the first stop, silently — and
# that is what this script baked (flat) until 2026-09-02. The node's own SVG export is the truth:
#   <rect ... stroke="url(#paint1_linear...)" stroke-width="3"/>
#   <linearGradient id="paint1_linear..."> #FCF195 @0 · #D6AB42 @0.6 · #BB7F1D @1
# Verified on all three instances of this component: 13994:1963 (this pill), 14060:4638 (the Home
# GPS pill) and 14060:4722 (the GPS hub back pill). Read the SVG, never the CSS.
GOLD_STOPS = ((0.0, (0xFC, 0xF1, 0x95)),
              (0.6, (0xD6, 0xAB, 0x42)),
              (1.0, (0xBB, 0x7F, 0x1D)))
INNER = (0x0A, 0x1D, 0x35, 255)  # Pop-Up border-1 #0A1D35
TOP = (0x13, 0x34, 0x53)         # gradient from
BOT = (0x09, 0x1B, 0x33)         # gradient to
GOLD_PX = 3
INNER_PX = 1

SCALE = 2   # what ships
SS = 4      # supersample factor for clean curves
OUT = "Assets/Art/HomeScreen/S_GpsPill.png"


def rounded(size, radius, fill):
    """One rounded-rect layer on a transparent canvas."""
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle([0, 0, size[0] - 1, size[1] - 1], radius=radius, fill=fill)
    return img


def _ramp(stops, t):
    """Multi-stop linear colour ramp, clamped."""
    t = min(1.0, max(0.0, t))
    for i in range(len(stops) - 1):
        t0, c0 = stops[i]
        t1, c1 = stops[i + 1]
        if t0 <= t <= t1:
            f = 0.0 if t1 == t0 else (t - t0) / (t1 - t0)
            return tuple(round(a + (b - a) * f) for a, b in zip(c0, c1))
    return stops[-1][1]


def gold_plate(size, radius):
    """The border plate as a VERTICAL gradient, masked to the pill silhouette."""
    w, h = size
    col = Image.new("RGBA", (1, h))
    for y in range(h):
        col.putpixel((0, y), _ramp(GOLD_STOPS, y / max(1, h - 1)) + (255,))
    plate = col.resize((w, h), Image.NEAREST)
    plate.putalpha(rounded(size, radius, (255, 255, 255, 255)).getchannel("A"))
    return plate


def build() -> Image.Image:
    s = SCALE * SS
    w, h = W * s, H * s

    # 1. The gold plate — the outermost 3px ring is whatever of this stays uncovered.
    #    Graded top-to-bottom, per the node's three-stop stroke (see GOLD_STOPS).
    out = gold_plate((w, h), RADIUS * s)

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


def main() -> None:
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    img = build()
    img.save(os.path.join(root, OUT))
    print(f"wrote {OUT}  {img.size[0]}x{img.size[1]}  ({SCALE}x of {W}x{H}, radius {RADIUS})")


if __name__ == "__main__":
    main()
