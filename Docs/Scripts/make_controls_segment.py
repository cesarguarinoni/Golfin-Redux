#!/usr/bin/env python3
"""
make_controls_segment.py — bake the in-game Controls 2x2 segment backgrounds.

    python3 Docs/Scripts/make_controls_segment.py

Nodes 14096:104446 (Segment/FLICK, SELECTED) and 14096:104448 (Segment/PENDULUM,
UNSELECTED) inside "Controls Segments" 14096:104444, file 5gEAHjl6xAtW8iYY7NMvWd.
Spec: Docs/Specs/Active/control_scheme_seam/SPEC.md section 3.4.

WHY BAKED AND NOT REUSED. A whole-palette pass (Docs/Architecture/UI_ELEMENT_PALETTE.md)
finds no sprite with this combination: every gold-edged sprite in the project is a solid
GOLD-BORDERED button, and this segment is the inverse — a gold BODY with a 3px WHITE
border. The in-game modal's own RETURN button is `ButtonCancel` (silver), so there was
nothing to clone from there either. Surfaced rather than substituted, per the standing
rule; baking from the node's own tokens is the palette's documented answer.

TOKENS, verified by SAMPLING the 2x node render rather than trusting the CSS
(get_design_context silently collapses a gradient stroke to its first stop —
UI_ELEMENT_PALETTE warning):

    box       408 x 110, radius 20, border 3
    SELECTED  fill vertical #FCF195 -> #D6AB42, opaque; border solid #FFFFFF
              (render row y=50 -> (249,236,144); row y=252 -> (214,172,68); border (255,255,255))
    UNSEL     fill  rgba(255,255,255,0.10)          -> composites to (36,55,77) on the card
              border rgba(255,255,255,0.55) OVER it -> composites to (156,165,175)

THE UNSELECTED HALF IS BAKED OPAQUE, PRE-COMPOSITED. It cannot ship as white-with-alpha:
the project renders in LINEAR colour space, so Unity blends in linear and a translucent
white over the navy card lands far lighter than Figma's sRGB compositing predicts. Built
that way first and measured in play mode: the 10% fill rendered (91,95,105) against a
target of (36,55,77), and the 59.5% border (201,202,204) against (156,165,175) — both
reproduced to the unit by the linear-blend maths, which is what identifies the cause.

Nor is it a matter of picking a better alpha: solving a x 1 + (1-a) x card_linear for the
target in each channel separately gives a = 0.0144 (R), 0.0241 (G), 0.0333 (B). THREE
DIFFERENT ALPHAS. One white-with-alpha sprite cannot reproduce an sRGB-composited colour
under linear blending, so the composite is baked instead — the same pre-composite the GPS
screens' A(o, a, backdrop) helper does, and the same reason the palette's panels are baked
rather than tinted.

Trade-off, stated plainly: the segments no longer follow the card if the card's colour
changes. The card is a fixed sprite (Background - HoleCard), and its measured colour behind
the segment rows is a near-uniform (11,32,58)..(9,27,52), so this is safe today and would
need re-baking if that card is ever re-skinned.

Both are drawn Image.Type.Simple at a FIXED 408x110 (two per 834 row with an 18 gap),
so there is no 9-slice to get wrong.
"""
from PIL import Image, ImageDraw
import os

SCALE = 2
SS = 8                                  # supersample; the corners are quarter-circles

W, H = 408, 110
RADIUS = 20
BORDER = 3

SEL_FILL_STOPS = [(0.0, "#FCF195"), (1.0, "#D6AB42")]
SEL_BORDER     = (255, 255, 255, 255)

# Pre-composited against the card, OPAQUE — see the note above.
UNSEL_FILL     = (36, 55, 77, 255)      # 10%   white over the card, per the node render
UNSEL_BORDER   = (156, 165, 175, 255)   # 59.5% white over that,   per the node render

OUT = "Assets/Art/UI/Controls"


def _rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def sample(stops, t):
    t = max(0.0, min(1.0, t))
    for i in range(len(stops) - 1):
        t0, c0 = stops[i]
        t1, c1 = stops[i + 1]
        if t0 <= t <= t1:
            f = 0.0 if t1 == t0 else (t - t0) / (t1 - t0)
            a, b = _rgb(c0), _rgb(c1)
            return tuple(round(x + (y - x) * f) for x, y in zip(a, b))
    return _rgb(stops[-1][1])


def rounded_mask(w, h, r, inset):
    """1-bit mask of the rounded rect shrunk by `inset` on every side.

    PIL's rounded_rectangle box is INCLUSIVE, so the drawn extent is (x1-x0) = w-1;
    the radius is derived from the box actually drawn so the corners stay true."""
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    x0 = y0 = inset
    x1, y1 = w - 1 - inset, h - 1 - inset
    rr = min(max(0.0, r - inset), (y1 - y0) / 2.0, (x1 - x0) / 2.0)
    d.rounded_rectangle([x0, y0, x1, y1], radius=rr, fill=255)
    return m


def build(selected):
    w, h = W * SCALE * SS, H * SCALE * SS
    r = RADIUS * SCALE * SS

    m_outer = rounded_mask(w, h, r, 0)
    m_inner = rounded_mask(w, h, r, BORDER * SCALE * SS)

    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px, po, pi = img.load(), m_outer.load(), m_inner.load()

    for y in range(h):
        t = y / (h - 1)
        frow = sample(SEL_FILL_STOPS, t) + (255,) if selected else UNSEL_FILL
        brow = SEL_BORDER if selected else UNSEL_BORDER
        for x in range(w):
            if not po[x, y]:
                continue
            px[x, y] = frow if pi[x, y] else brow

    return img.resize((W * SCALE, H * SCALE), Image.LANCZOS)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for selected, name in ((True, "S_ControlsSegment_On.png"), (False, "S_ControlsSegment_Off.png")):
        img = build(selected)
        path = os.path.join(OUT, name)
        img.save(path)
        state = "SELECTED  gold #FCF195->#D6AB42, 3px solid white" if selected \
                else "UNSELECTED 10% white fill, 3px 59.5% white border"
        print(f"  {path}  {img.width}x{img.height}  (node {W}x{H} r={RADIUS} b={BORDER})  {state}")
