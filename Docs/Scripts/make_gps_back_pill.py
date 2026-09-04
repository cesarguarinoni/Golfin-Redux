#!/usr/bin/env python3
"""
make_gps_back_pill.py — bake the GPS hub's BACK pill from its Figma tokens.

    python3 Docs/Scripts/make_gps_back_pill.py

Node 14060:4722 ("Mission Card Container" — legacy naming; it is the back pill,
confirmed by Cesar 2026-09-01) on frame 14011:32819.

TOKENS, read out of the node's SVG export, NOT the get_design_context CSS:

    shape    stadium, node box 138x92 (r = 46); sprite 140x94 incl. the 1px rule
    fill     linear #133453 (0) -> #091B33 (1), vertical
    stroke   linear #FCF195 (0) -> #D6AB42 (0.6) -> #BB7F1D (1), vertical, width 3
    rule     1px #0A1D35 outside the stroke (the child "Pop-Up"'s own border)

⚠️ THE STROKE IS A THREE-STOP GRADIENT AND THE CSS DOES NOT SAY SO. Tailwind renders
it as `border-[#fcf195]` — the first stop only. Sampling the node render is what caught
it: the rim measures #FAEE93 at the top, #DBB54E at mid-height and #BA7F1C at the
bottom. `Docs/Scripts/make_daily_pill_panel.py` bakes the SAME Figma component with a
solid `GOLD = #FCF195`, so the Home daily-mission pill's rim is flat where the design
has a gradient — flagged to Cesar, not changed here (it would alter a shipped screen).

Baked at 2x and drawn Image.Type.Simple at a FIXED 138x92: the pill never stretches
(Cesar: "keep the pill size and autosize text"), so there is no 9-slice to get wrong —
and at 92 tall a r=50 nine-slice would collapse anyway (2 x 50 > 92).
"""
from PIL import Image, ImageDraw
import os

SCALE = 2
SS = 8                      # supersample; the ends are semicircles

# The NODE box is 138x92 — that is the gold rim plus the fill. The child "Pop-Up"
# carries its own 1px #0A1D35 border at the SAME size as its parent, so that hairline
# lands OUTSIDE the parent's content box and Figma exports the pill as 140x94.
# Sampling row 0 of the export proves it: #0A1D35, with the gold only from row 1.
# So the sprite is the node box PLUS 1px of rule on every side, and the RectTransform
# is 140x94 placed 1px up-left of the node origin — the same "rect = node + outer
# decoration" pattern the hub panels already use (UI_ELEMENT_PALETTE drawn-body note).
NODE_W, NODE_H = 138, 92
RULE = 1                    # #0A1D35, outermost, OUTSIDE the node box
STROKE = 3                  # gold gradient, just inside the rule
W, H = NODE_W + 2 * RULE, NODE_H + 2 * RULE
RADIUS = H / 2              # stadium

FILL_STOPS   = [(0.0, "#133453"), (1.0, "#091B33")]
STROKE_STOPS = [(0.0, "#FCF195"), (0.6, "#D6AB42"), (1.0, "#BB7F1D")]
RULE_COLOR   = "#0A1D35"

OUT = "Assets/Art/UI/Gps"
NAME = "S_GpsBackPill.png"


def _rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def sample(stops, t):
    """Multi-stop linear ramp, clamped."""
    t = max(0.0, min(1.0, t))
    for i in range(len(stops) - 1):
        t0, c0 = stops[i]
        t1, c1 = stops[i + 1]
        if t0 <= t <= t1:
            f = 0.0 if t1 == t0 else (t - t0) / (t1 - t0)
            a, b = _rgb(c0), _rgb(c1)
            return tuple(round(x + (y - x) * f) for x, y in zip(a, b))
    return _rgb(stops[-1][1])


def stadium_mask(w, h, r, inset):
    """
    1-bit mask of the stadium shrunk by `inset` on every side.

    PIL's rounded_rectangle takes an INCLUSIVE pixel box, so the drawn extent is
    (x1-x0) = w-1. Passing r = h/2 there overshoots half the drawn height by half a
    pixel and PIL clamps it, which cost ~1px of silhouette on both curved ends when
    checked against the node render. Deriving the radius from the box actually drawn
    keeps the ends exactly semicircular.
    """
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    x0 = y0 = inset
    x1, y1 = w - 1 - inset, h - 1 - inset
    rr = min(max(0.0, r - inset), (y1 - y0) / 2.0, (x1 - x0) / 2.0)
    d.rounded_rectangle([x0, y0, x1, y1], radius=rr, fill=255)
    return m


def build():
    w, h = W * SCALE * SS, H * SCALE * SS
    r = RADIUS * SCALE * SS

    # Three nested silhouettes: rule (outermost) > stroke > fill.
    m_rule   = stadium_mask(w, h, r, 0)
    m_stroke = stadium_mask(w, h, r, RULE * SCALE * SS)
    m_fill   = stadium_mask(w, h, r, (RULE + STROKE) * SCALE * SS)

    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px, pr, ps, pf = img.load(), m_rule.load(), m_stroke.load(), m_fill.load()
    rule = _rgb(RULE_COLOR)

    for y in range(h):
        t = y / (h - 1)                       # both gradients span the full box
        srow = sample(STROKE_STOPS, t)
        frow = sample(FILL_STOPS, t)
        for x in range(w):
            if not pr[x, y]:
                continue
            if pf[x, y]:
                px[x, y] = frow + (255,)
            elif ps[x, y]:
                px[x, y] = srow + (255,)
            else:
                px[x, y] = rule + (255,)

    return img.resize((W * SCALE, H * SCALE), Image.LANCZOS)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    img = build()
    path = os.path.join(OUT, NAME)
    img.save(path)
    print(f"  {path}  {img.width}x{img.height}  "
          f"(node {NODE_W}x{NODE_H} + {RULE}px rule = {W}x{H}, stadium r={RADIUS:g}, "
          f"rule {RULE_COLOR}, stroke {STROKE}px "
          f"{STROKE_STOPS[0][1]}->{STROKE_STOPS[1][1]}->{STROKE_STOPS[-1][1]})")
