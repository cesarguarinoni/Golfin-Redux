#!/usr/bin/env python3
"""
make_pendulum_sprites.py — bake the Pendulum scheme's two BORDERED pills from their
Figma tokens (scheme_pendulum SPEC §3.3, node "Scheme — Pendulum" 14091:33885).

    python3 Docs/Scripts/make_pendulum_sprites.py

WHY THESE ARE BAKED AND THE OTHER FIVE SHAPES ARE NOT.

Every solid shape in this scheme (the ticks, the two bands, the centre pip, the marker
discs) is a flat stadium, and a flat stadium is `S_PillStadium` tinted — no new art, per
the reuse mandate. These TWO are different: they are a translucent fill INSIDE a
translucent stroke, and stacking two tinted `S_PillStadium`s cannot draw that (the solid
"stroke" layer paints the whole shape and the fill on top cannot hide it).

──────────────────────────────────────────────────────────────────────────────
THE ALPHAS ARE NOT THE NODE'S ALPHAS, AND THAT IS DELIBERATE (2026-09-05).

Cesar: "the green and yellow colors for the window don't seem to match figma." They did
not, and neither did the lane or the track. The cause is not a wrong colour — it is that
**Figma composites in sRGB and Unity blends in LINEAR space**. Measured against
`reference/pendulum_timing.png`, every translucent element came out too light:

    element      figma            built (node alphas)   delta
    lane fill    (100,100,100)    (128,151,117)         +28 +51 +17
    lane stroke  (163,173,163)    (192,204,190)         +29 +31 +27
    track        ( 19, 49, 55)    ( 43, 71, 58)         +24 +22  +3
    BandGood     (196,188,138)    (224,208,148)         +28 +20 +10
    BandJust     (175,230,170)    (178,231,169)         +3  +1   -1   (opaque enough already)

Worked for one channel: the node's lane fill is white @ 14%. Over a backdrop of 19, sRGB
gives 0.14*255 + 0.86*19 = 52; linear gives srgb(0.14*1.0 + 0.86*lin(19)) = 128. The
sRGB-blend check confirms Figma's side: 0.78*navy + 0.22*backdrop reproduces its track
pixel to within 3.

So each alpha here is SOLVED, not copied: the value `a` for which a LINEAR blend of the
node's colour over the reference's own backdrop lands on the reference's own composite,

    a = (lin(target) - lin(backdrop)) / (lin(colour) - lin(backdrop))

evaluated per channel against `reference/pendulum_timing.png`. The lane solved
consistently across channels (fill .109/.085/.110, stroke .353/.389/.354) so it stays
translucent at the solved value. The TRACK did not (.915/.859/.699 — navy and grass are
too far apart in blue for one alpha to fit), so its fill is pre-composited OPAQUE at the
node's own rendered pixel instead: exact, and backdrop-independent. Re-derive by
re-running the sampler in the task report if the node ever moves.

Edit THIS FILE, never the PNG (UI_ELEMENT_PALETTE § Baked-from-tokens sprites).
"""
from PIL import Image, ImageDraw
import os

SCALE = 2
SS    = 8          # supersample: the caps are semicircles and the stroke is 2px

OUT = "Assets/Art/ShotUI"


def stadium_mask(w, h, r, inset=0.0):
    """A white stadium mask of (w, h) with radius r, shrunk inward by `inset` px."""
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    i = inset * SS
    d.rounded_rectangle([i, i, w * SS - 1 - i, h * SS - 1 - i],
                        radius=max(r * SS - i, 0), fill=255)
    return m.resize((w, h), Image.LANCZOS)


def bake(name, w, h, radius, fill_rgba, stroke_rgba, stroke_px):
    """
    fill inside, stroke as the outermost `stroke_px`. Drawn as two masks and composited
    so the stroke does NOT sit under the fill — that stacking is exactly the bug this
    file exists to fix, and reproducing it in the bake would be no better.
    """
    W, H, R, S = w * SCALE, h * SCALE, radius * SCALE, stroke_px * SCALE

    outer = stadium_mask(W, H, R)                 # stroke + fill silhouette
    inner = stadium_mask(W, H, R, inset=S)        # fill only

    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # Stroke: the ring = outer minus inner, so the fill's alpha never lands on top of it.
    ring = Image.new("L", (W, H))
    ring.putdata([max(0, o - i) for o, i in zip(outer.getdata(), inner.getdata())])
    stroke_layer = Image.new("RGBA", (W, H), stroke_rgba[:3] + (255,))
    stroke_layer.putalpha(ring.point(lambda a: int(a * stroke_rgba[3] / 255)))

    fill_layer = Image.new("RGBA", (W, H), fill_rgba[:3] + (255,))
    fill_layer.putalpha(inner.point(lambda a: int(a * fill_rgba[3] / 255)))

    img = Image.alpha_composite(img, fill_layer)
    img = Image.alpha_composite(img, stroke_layer)

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    img.save(path)
    print(f"{path}  {W}x{H}  border={int(R)} sprite-px  (ppum {SCALE} -> {radius} UI px)")
    return path


if __name__ == "__main__":
    # Only the CAPS are baked: border = radius on all four sides leaves a zero-width
    # middle, which is what makes a 9-slice reproduce a true stadium at any length.
    # Lane: node white 14% / 50%.
    # FILL solved against TURF, not against the reference render's backdrop. A single alpha cannot
    # match an sRGB composite across backdrops once the renderer blends in linear, so it has to be
    # fitted to the backdrop the element actually sits on — and the lane sits on grass under the
    # ball, always. Fitted to the reference's darker patch it solved to .101 and then read +15/+10/
    # +28 too light over real fairway; fitted to fairway (94,124,56) it solves to .048/.060/.034,
    # so .05. The STROKE stays at its .365 solve: measured over the same fairway it lands within
    # (-3,-6,+5) of the node's 50% sRGB composite, because the linear residual shrinks as alpha rises.
    bake("S_PendulumLane",  120, 120, 60, (255, 255, 255, int(round(0.050 * 255))),
                                          (255, 255, 255, int(round(0.365 * 255))), 3)
    # Track: fill pre-composited OPAQUE at the node's rendered pixel (19,49,55) — one alpha
    # could not fit all three channels. Stroke keeps the lane's solved ratio (0.35 x 0.73).
    bake("S_PendulumTrack",  44,  44, 22, (0x13, 0x31, 0x37, 255),
                                          (255, 255, 255, int(round(0.256 * 255))), 2)
