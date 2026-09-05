#!/usr/bin/env python3
"""
make_needle_sprites.py — bake the ONE sprite the Needle scheme cannot make from shapes it
already has (scheme_needle SPEC §3.3, Figma section "2b — Needle (club handle)" 14091:102411).

    python3 Docs/Scripts/make_needle_sprites.py

WHY ONLY ONE.

Almost nothing in this scheme is a sprite at all. The three power rings, the overpower
crescent, the accuracy arc and its two zones are `NeedleArcGraphic` — a mesh — because
their radii are DERIVED from the pull thresholds and their angles are DERIVED from the
accuracy windows, so both change at runtime and neither can be a fixed-size PNG. The
needle bar, the hub and the tap pip are flat rounded shapes, which is `S_PillStadium`
tinted (with `pixelsPerUnitMultiplier = 88 / radius`, or the caps collapse into an oval —
the Rule 21 render-health failure). The ball-rest ghost is `S_PendulumBallGhost.png`,
reused as the spec asks.

That leaves `ResultChip` (14091:102737), and it is baked for the reason the Pendulum's
lane and track were: it is a VERTICAL GRADIENT inside a translucent-white border, and a
tinted stadium can draw neither. Fixed 420x120 in the node and fixed in the build, so it
is a Simple sprite at 2x rather than a 9-slice — no border to collapse, no ppum to get
wrong.

Its drop shadow is baked in, with transparent padding to hold it. A uGUI `Outline`/shadow
component is what UI Rule 21 flags as a fabricated border, and it cannot blur.

Edit THIS FILE, never the PNG (UI_ELEMENT_PALETTE § Baked-from-tokens sprites).
"""
from PIL import Image, ImageDraw, ImageFilter
import os

SCALE = 2
OUT   = "Assets/Art/ShotUI"

# ── Node tokens (14091:102737) ────────────────────────────────────────────────
W, H      = 420, 120
RADIUS    = 32
TOP       = (0x13, 0x34, 0x53)      # gradient from
BOTTOM    = (0x09, 0x1B, 0x33)      # gradient to
BORDER    = (255, 255, 255)
BORDER_A  = 0.90
BORDER_PX = 3
SHADOW_A  = 0.50                    # shadow 0px 6px 12px rgba(0,0,0,0.5)
SHADOW_DY = 6
SHADOW_BLUR = 12
PAD       = 24                      # room for the blurred shadow, in node px


def rounded_mask(size, box, radius, supersample=8):
    """An antialiased rounded-rect mask, drawn large and resampled down."""
    w, h = size
    m = Image.new("L", (w * supersample, h * supersample), 0)
    d = ImageDraw.Draw(m)
    x0, y0, x1, y1 = [v * supersample for v in box]
    d.rounded_rectangle([x0, y0, x1 - 1, y1 - 1], radius=radius * supersample, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def vertical_gradient(size, top, bottom):
    w, h = size
    g = Image.new("RGB", (1, h))
    for y in range(h):
        t = y / max(h - 1, 1)
        g.putpixel((0, y), tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return g.resize((w, h), Image.NEAREST)


def bake_result_chip():
    w, h, r  = W * SCALE, H * SCALE, RADIUS * SCALE
    pad      = PAD * SCALE
    bw       = BORDER_PX * SCALE
    cw, ch   = w + pad * 2, h + pad * 2
    box      = (pad, pad, pad + w, pad + h)

    outer = rounded_mask((cw, ch), box, r)
    inner = rounded_mask((cw, ch), (box[0] + bw, box[1] + bw, box[2] - bw, box[3] - bw),
                         max(r - bw, 0))

    img = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))

    # Shadow first, so nothing draws under it.
    shadow = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    shadow.putalpha(outer.point(lambda a: int(a * SHADOW_A)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(SHADOW_BLUR * SCALE / 2.0))
    shadow = shadow.transform(shadow.size, Image.AFFINE, (1, 0, 0, 0, 1, -SHADOW_DY * SCALE))
    img = Image.alpha_composite(img, shadow)

    # Fill: the node's vertical gradient, clipped to the INNER rounded rect. The ramp spans the
    # BODY, not the padded canvas — running it over the padding would compress it and neither end
    # would reach the node's own colour (measured: the top came out (17,48,77) against #133453).
    fill = Image.new("RGB", (cw, ch), BOTTOM)
    fill.paste(vertical_gradient((w, h), TOP, BOTTOM), (pad, pad))
    fill = fill.convert("RGBA")
    fill.putalpha(inner)
    img = Image.alpha_composite(img, fill)

    # Border: the ring between outer and inner, so the fill never lands on top of it.
    ring = Image.new("L", (cw, ch))
    ring.putdata([max(0, o - i) for o, i in zip(outer.getdata(), inner.getdata())])
    border = Image.new("RGBA", (cw, ch), BORDER + (255,))
    border.putalpha(ring.point(lambda a: int(a * BORDER_A)))
    img = Image.alpha_composite(img, border)

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "S_NeedleResultChip.png")
    img.save(path)
    print(f"{path}  {cw}x{ch}  (node {W}x{H} + {PAD}px shadow pad, baked at {SCALE}x)")
    # The BUILD size the chip must be given, padding included, so the node's 420x120 body
    # comes out at exactly 420x120 on the canvas.
    print(f"  build rect: {W + PAD*2} x {H + PAD*2}")
    return path


if __name__ == "__main__":
    bake_result_chip()
