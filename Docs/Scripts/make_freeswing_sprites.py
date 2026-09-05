#!/usr/bin/env python3
"""
make_freeswing_sprites.py — bake the TWO sprites the Free Swing scheme cannot make from shapes
it already has (scheme_freeswing SPEC §3.3, Figma section "3b — Free Swing (club handle)"
14091:102934).

    python3 Docs/Scripts/make_freeswing_sprites.py

WHY ONLY TWO.

Most of this scheme is not a sprite at all. The finger trace is `FreeSwingTraceGraphic` — a mesh
— because its shape is the player's own gesture, which is different on every swing and grows
sixty times a second; there is no PNG of that. The ticks and the impact line are flat stadiums,
which is `S_PillStadium` tinted (with `pixelsPerUnitMultiplier = 88 / radius`, or the caps
collapse into an oval — the Rule 21 render-health failure). The green impact window is the same
stadium, tinted with the linear-corrected colour and re-WIDTHED every drag frame. The ball-rest
ghost is `S_PendulumBallGhost.png`, reused as the spec asks.

That leaves two, and each is a translucent-or-gradient fill INSIDE a translucent stroke, which
stacking two tinted stadiums cannot draw (the solid "stroke" layer paints the whole shape and the
fill on top cannot hide it — the bug that made the Pendulum's lane read as a ~57%-white slab):

  S_FreeSwingLane.png          the 140-wide r70 pill: white 14% fill, 3px white 50% stroke.
  S_FreeSwingAnalyzerChip.png  the 840x150 r32 chip: a vertical navy gradient inside a 3px
                               white-90% border, with its blurred drop shadow baked in.

WHY THE LANE IS ITS OWN PNG AND NOT THE PENDULUM'S.

The tokens are identical (white 14% / 50%) but the RADIUS is not: 70 here against 60 there, and a
9-slice reproduces a true stadium at any LENGTH only when its border equals its radius on all four
sides. Re-pointing `S_PendulumLane` at a 70px radius through `pixelsPerUnitMultiplier` would scale
the 3px stroke with it, and a Free Swing retune of either would silently move the other — which is
the cross-scheme coupling carry-over 1 exists to forbid. Same method, own file.

WHY THE CHIP IS NOT `S_NeedleResultChip`.

Same tokens again (#133453 → #091B33, r32, 3px white 90%, shadow 0/6/12 black 50%) and a different
SIZE: 840x150 against 420x120. Both are Simple sprites at 2x rather than 9-slices — fixed in the
node and fixed in the build, so there is no border to collapse and no ppum to get wrong — which
means the size IS the sprite and the Needle's cannot be reused. SPEC §3.3 says so in as many words.

THE LANE'S ALPHAS ARE NOT THE NODE'S ALPHAS, AND THAT IS DELIBERATE.

Figma composites in sRGB and Unity blends in LINEAR, so a translucent element handed the node's own
alpha renders too light — measurably: on the Pendulum the lane fill came out +28/+51/+17 RGB and
Cesar named it. The fix is to SOLVE for the alpha whose LINEAR blend over the backdrop the element
actually sits on lands on Figma's sRGB composite. That backdrop is fairway (94,124,56) — the lane
sits on grass under the ball, always — and for these exact two tokens the solve is already done and
verified on a built render in `make_pendulum_sprites.py`: .050 for the white-14% fill and .365 for
the white-50% stroke. Reusing the SOLVE is not reusing a tuning knob; it is the same transfer
function evaluated on the same two node colours over the same backdrop, and re-deriving it here
would produce the same two numbers. Re-run the sampler in the task report if the node ever moves.

Edit THIS FILE, never the PNGs (UI_ELEMENT_PALETTE § Baked-from-tokens sprites).
"""
from PIL import Image, ImageDraw, ImageFilter
import os

SCALE = 2
SS    = 8          # supersample: the lane's caps are semicircles and its stroke is 3px

OUT = "Assets/Art/ShotUI"

# ── Lane tokens (SwingLane 14092:34686) ──────────────────────────────────────
LANE_SIZE   = 140          # node width; the 9-slice makes any HEIGHT from this
LANE_RADIUS = 70           # node rounded-[70px] — exactly half the width, i.e. a stadium
LANE_STROKE = 3            # node border-3
LANE_FILL_A   = 0.050      # node white 14%, SOLVED over fairway (see the header)
LANE_STROKE_A = 0.365      # node white 50%, same solve

# ── Chip tokens (AnalyzerChip 14091:103270) ──────────────────────────────────
CHIP_W, CHIP_H = 840, 150
CHIP_RADIUS    = 32
CHIP_TOP       = (0x13, 0x34, 0x53)      # gradient from
CHIP_BOTTOM    = (0x09, 0x1B, 0x33)      # gradient to
CHIP_BORDER    = (255, 255, 255)
CHIP_BORDER_A  = 0.90
CHIP_BORDER_PX = 3
CHIP_SHADOW_A    = 0.50                  # shadow 0px 6px 12px rgba(0,0,0,0.5)
CHIP_SHADOW_DY   = 6
CHIP_SHADOW_BLUR = 12
CHIP_PAD         = 24                    # room for the blurred shadow, in node px


def stadium_mask(w, h, r, inset=0.0):
    """A white stadium mask of (w, h) with radius r, shrunk inward by `inset` px."""
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    i = inset * SS
    d.rounded_rectangle([i, i, w * SS - 1 - i, h * SS - 1 - i],
                        radius=max(r * SS - i, 0), fill=255)
    return m.resize((w, h), Image.LANCZOS)


def rounded_mask(size, box, radius, supersample=8):
    """An antialiased rounded-rect mask inside a padded canvas."""
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


def bake_lane():
    """
    The pill: fill inside, stroke as the outermost `LANE_STROKE`. Drawn as two masks and
    composited so the stroke does NOT sit under the fill — that stacking is exactly the bug this
    file exists to avoid, and reproducing it in the bake would be no better.

    Only the CAPS are baked: with border = radius on all four sides the middle is zero-width,
    which is what makes a 9-slice reproduce a true stadium at any length.
    """
    W = H = LANE_SIZE * SCALE
    R = LANE_RADIUS * SCALE
    S = LANE_STROKE * SCALE

    outer = stadium_mask(W, H, R)                 # stroke + fill silhouette
    inner = stadium_mask(W, H, R, inset=S)        # fill only

    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    fill_layer = Image.new("RGBA", (W, H), (255, 255, 255, 255))
    fill_layer.putalpha(inner.point(lambda a: int(a * LANE_FILL_A)))

    ring = Image.new("L", (W, H))
    ring.putdata([max(0, o - i) for o, i in zip(outer.getdata(), inner.getdata())])
    stroke_layer = Image.new("RGBA", (W, H), (255, 255, 255, 255))
    stroke_layer.putalpha(ring.point(lambda a: int(a * LANE_STROKE_A)))

    img = Image.alpha_composite(img, fill_layer)
    img = Image.alpha_composite(img, stroke_layer)

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "S_FreeSwingLane.png")
    img.save(path)
    print(f"{path}  {W}x{H}  border={int(R)} sprite-px  (ppum {SCALE} -> {LANE_RADIUS} UI px)")
    return path


def bake_analyzer_chip():
    w, h, r = CHIP_W * SCALE, CHIP_H * SCALE, CHIP_RADIUS * SCALE
    pad     = CHIP_PAD * SCALE
    bw      = CHIP_BORDER_PX * SCALE
    cw, ch  = w + pad * 2, h + pad * 2
    box     = (pad, pad, pad + w, pad + h)

    outer = rounded_mask((cw, ch), box, r)
    inner = rounded_mask((cw, ch), (box[0] + bw, box[1] + bw, box[2] - bw, box[3] - bw),
                         max(r - bw, 0))

    img = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))

    # Shadow first, so nothing draws under it.
    shadow = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    shadow.putalpha(outer.point(lambda a: int(a * CHIP_SHADOW_A)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(CHIP_SHADOW_BLUR * SCALE / 2.0))
    shadow = shadow.transform(shadow.size, Image.AFFINE, (1, 0, 0, 0, 1, -CHIP_SHADOW_DY * SCALE))
    img = Image.alpha_composite(img, shadow)

    # Fill: the node's vertical gradient, clipped to the INNER rounded rect. The ramp spans the
    # BODY, not the padded canvas — running it over the padding would compress it and neither end
    # would reach the node's own colour. FreeSwingColors.ChipGradientAt samples this same linear
    # ramp so a label's backdrop is read off the gradient rather than guessed.
    fill = Image.new("RGB", (cw, ch), CHIP_BOTTOM)
    fill.paste(vertical_gradient((w, h), CHIP_TOP, CHIP_BOTTOM), (pad, pad))
    fill = fill.convert("RGBA")
    fill.putalpha(inner)
    img = Image.alpha_composite(img, fill)

    # Border: the ring between outer and inner, so the fill never lands on top of it.
    ring = Image.new("L", (cw, ch))
    ring.putdata([max(0, o - i) for o, i in zip(outer.getdata(), inner.getdata())])
    border = Image.new("RGBA", (cw, ch), CHIP_BORDER + (255,))
    border.putalpha(ring.point(lambda a: int(a * CHIP_BORDER_A)))
    img = Image.alpha_composite(img, border)

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "S_FreeSwingAnalyzerChip.png")
    img.save(path)
    print(f"{path}  {cw}x{ch}  (node {CHIP_W}x{CHIP_H} + {CHIP_PAD}px shadow pad, baked at {SCALE}x)")
    # The BUILD size the chip must be given, padding included, so the node's 840x150 body comes
    # out at exactly 840x150 on the canvas.
    print(f"  build rect: {CHIP_W + CHIP_PAD*2} x {CHIP_H + CHIP_PAD*2}")
    return path


if __name__ == "__main__":
    bake_lane()
    bake_analyzer_chip()
