#!/usr/bin/env python3
"""
make_gps_icon_ring.py — bake the GPS hub's circular icon rings from their Figma tokens.

    python3 Docs/Scripts/make_gps_icon_ring.py

WHY A SCRIPT AND NOT A FIGMA EXPORT (same reasoning as make_daily_pill_panel.py):
the ring is four numbers and four colours. Baking it here means a token change is a
one-line edit and a re-run, and the PNG in the repo can always be re-derived. Edit
THIS, not the PNGs.

TOKENS — read out of the node SVGs on 2026-09-01, not eyeballed off a render:

  Step ring   node 14017:32695  64x64  r=30.5  stroke 3  (how-it-works strip)
      fill    linear #204B76 (top) -> #0B203D (bottom)
      stroke  SOLID #F3ECC2

  Tile ring   node 14012:98861  88x88  r=41.5  stroke 5  (action tiles)
      fill    linear #204B76 (top) -> #0B203D (bottom)
      stroke  linear #F3ECC2 (top) -> #98855B (bottom)   <- note: NOT solid

The two are deliberately different: the bigger tile ring's stroke darkens toward the
bottom, the small step ring's does not. Building both from one "cream ring" guess is
exactly the flat-fill defect this script replaces (Cesar, 2026-09-01: "the rounded
icons like GPS Proof and Earn PTS have a flat blue background when the figma ones
have a gradient").

In both nodes `r + stroke/2 == size/2`, i.e. the stroke's OUTER edge is the bounding
box — so the sprite is drawn edge to edge with no padding.
"""
from PIL import Image
import os

SS = 8  # supersample factor; the whole shape is curves, so this is what buys clean edges


def _lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def _rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def bake(size, radius, stroke, fill_top, fill_bottom, stroke_top, stroke_bottom, scale):
    """Render one ring at `size * scale`, supersampled `SS`x and box-filtered down."""
    px = size * scale * SS
    r_out = (radius + stroke / 2.0) * scale * SS
    r_in = (radius - stroke / 2.0) * scale * SS
    c = px / 2.0

    ft, fb = _rgb(fill_top), _rgb(fill_bottom)
    st, sb = _rgb(stroke_top), _rgb(stroke_bottom)

    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    pix = img.load()
    for y in range(px):
        t = y / (px - 1)                     # the gradients are userSpaceOnUse over the full box
        frow, srow = _lerp(ft, fb, t), _lerp(st, sb, t)
        dy2 = (y + 0.5 - c) ** 2
        for x in range(px):
            d = (dy2 + (x + 0.5 - c) ** 2) ** 0.5
            if d > r_out:
                continue
            # The fill runs under the stroke to r_out; the stroke then paints over
            # r_in..r_out, so the seam between them can never show a hairline.
            pix[x, y] = (srow if d >= r_in else frow) + (255,)

    return img.resize((size * scale, size * scale), Image.LANCZOS)


RINGS = [
    # name,                       size, radius, stroke, fill top, fill bottom, stroke top, stroke bottom
    ("S_GpsIconRing_Step", 64, 30.5, 3, "#204B76", "#0B203D", "#F3ECC2", "#F3ECC2"),
    ("S_GpsIconRing_Tile", 88, 41.5, 5, "#204B76", "#0B203D", "#F3ECC2", "#98855B"),

    # auth_golf_profile — the Welcome tutorial draws the SAME atom at two more sizes.
    #   Hero ring     node 14029:34190  150x150  r=70.8333  stroke 8.33333
    #   Feature ring  node 14029:34207   96x96   r=45.3333  stroke 5.33333
    # Both carry the Tile ring's exact token pair (fill #204B76->#0B203D, stroke
    # #F3ECC2->#98855B), so they are the atom at a new size, not a new atom. They are baked
    # rather than achieved by scaling S_GpsIconRing_Tile because a scaled 88px sprite lands its
    # stroke at 8.52 / 5.45 instead of 8.33 / 5.33 — small, but there is no reason to carry an
    # approximation when the token is known.
    ("S_GpsIconRing_Welcome", 150, 70.8333, 8.33333, "#204B76", "#0B203D", "#F3ECC2", "#98855B"),
    ("S_GpsIconRing_Feature",  96, 45.3333, 5.33333, "#204B76", "#0B203D", "#F3ECC2", "#98855B"),
]

OUT = "Assets/Art/UI/Gps"
SCALE = 4   # 64 -> 256, 88 -> 352; matches the 238px nav sprites already in this folder

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, size, radius, stroke, ft, fb, st, sb in RINGS:
        img = bake(size, radius, stroke, ft, fb, st, sb, SCALE)
        path = os.path.join(OUT, name + ".png")
        img.save(path)
        print(f"  {path}  {img.width}x{img.height}  "
              f"(node {size}px, r={radius}, stroke={stroke}, fill {ft}->{fb}, stroke {st}->{sb})")
