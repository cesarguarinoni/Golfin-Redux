#!/usr/bin/env python3
"""
make_controls_settings_icon.py — bake the Settings "Controls" icon.

    python3 Docs/Scripts/make_controls_settings_icon.py

Figma node 14096:32949 ("Property 1=Controls"), the tenth variant of the Settings Icons
component set 4060:7534, file 5gEAHjl6xAtW8iYY7NMvWd.

WHY THIS EXISTS INSTEAD OF A PLAIN EXPORT. Every PNG that comes back from the Figma MCP
`download_assets` on this file is FULLY OPAQUE with a flat #808080 where the transparency
should be — verified by exporting the Language variant (4060:7982), whose shipped
`Assets/Art/Settings/Language Icon.png` is a normal transparent glyph: through this path it
comes back opaque grey too. So the artifact is the export pipeline, not the Controls node.
Dropping that PNG in gives the row a visible grey plate behind the glyph, which is exactly
what the first attempt shipped and what the play-mode capture caught.

Keying the grey out is NOT safe here: the glyph's own gradient ends at #818EA1 = (129,142,161),
whose red channel is one unit from the background's 128, so a colour key eats the bottom of
the icon.

So the icon is rasterised from the node's own SVG, which carries the real geometry:

    path    one stroked path, stroke-width 6, no fill
    stroke  linear gradient, white (0) -> #818EA1 (1), vertical (userSpaceOnUse
            x1,y1 = 27.14,3  ->  x2,y2 = 26.66,52.996)
    canvas  72x72 to match the nine sibling icons

Placement is not guessed: the render is composited over #808080 and matched against the
Figma symbol export, and the offset with the lowest error is used (printed at the end).
"""
from PIL import Image, ImageDraw
import math, os, re

SS = 8                      # supersample
OUT = "Assets/Art/Settings/Controls Icon.png"
BOX = 72                    # sibling icon size

STROKE_W = 6.0
GRAD = ((255, 255, 255), (0x81, 0x8E, 0xA1))
GRAD_Y0, GRAD_Y1 = 3.0, 52.9958      # userSpaceOnUse gradient span

# The single "d" of node 14096:32950, verbatim.
D = ("M7.12132 14V46M12.1213 19L7.12132 14L2.12132 19M12.1213 41L7.12132 46L2.12132 41"
     "M24.1213 33V9C24.1213 5.7 26.8213 3 30.1213 3C33.4213 3 36.1213 5.7 36.1213 9V26"
     "L45.1213 29C49.1213 30.3 52.1213 33.5 52.1213 38L50.1213 46C49.4213 50 46.1213 53 "
     "42.1213 53H28.1213C24.6213 53 21.6213 51 20.1213 48L14.1213 37C12.6213 34.5 14.1213 "
     "31.5 17.1213 31C19.6213 30.5 22.1213 31.5 24.1213 33Z")

VIEW_W, VIEW_H = 55.1213, 56.0


def parse(d):
    """M/L/V/H/C/Z only — everything this path uses. Returns a list of (points, closed)."""
    toks = re.findall(r"[MLVHCZmlvhcz]|-?\d*\.?\d+", d)
    subs, pts, i = [], [], 0
    cur = (0.0, 0.0)
    cmd = None
    while i < len(toks):
        t = toks[i]
        if t.isalpha():
            cmd = t
            i += 1
            if cmd in "Zz":
                if pts:
                    subs.append((pts, True))
                    pts = []
                continue
            if cmd in "Mm":
                if pts:
                    subs.append((pts, False))
                x, y = float(toks[i]), float(toks[i + 1]); i += 2
                cur = (x, y); pts = [cur]
                continue
        n = lambda k: float(toks[i + k])
        if cmd in "Ll":
            cur = (n(0), n(1)); i += 2; pts.append(cur)
        elif cmd in "Vv":
            cur = (cur[0], n(0)); i += 1; pts.append(cur)
        elif cmd in "Hh":
            cur = (n(0), cur[1]); i += 1; pts.append(cur)
        elif cmd in "Cc":
            p0 = cur
            c1, c2, p3 = (n(0), n(1)), (n(2), n(3)), (n(4), n(5)); i += 6
            for s in range(1, 25):                      # flatten the cubic
                u = s / 24.0; v = 1 - u
                pts.append((v*v*v*p0[0] + 3*v*v*u*c1[0] + 3*v*u*u*c2[0] + u*u*u*p3[0],
                            v*v*v*p0[1] + 3*v*v*u*c1[1] + 3*v*u*u*c2[1] + u*u*u*p3[1]))
            cur = p3
        else:
            i += 1
    if pts:
        subs.append((pts, False))
    return subs


def stroke_mask(w, h, ox, oy, scale):
    """Round-capped, round-joined stroke of the path, as an L mask."""
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    r = STROKE_W * scale / 2.0
    for pts, closed in parse(D):
        p = [(ox + x * scale, oy + y * scale) for x, y in pts]
        if closed:
            p = p + [p[0]]
        if len(p) > 1:
            d.line(p, fill=255, width=int(round(r * 2)))
        for x, y in p:                                   # round caps + joins
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    return m


def build(ox, oy, scale, ss=SS):
    w = h = BOX * ss
    m = stroke_mask(w, h, ox * ss, oy * ss, scale * ss)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px, pm = img.load(), m.load()
    y0, y1 = (GRAD_Y0 * scale + oy) * ss, (GRAD_Y1 * scale + oy) * ss
    for y in range(h):
        t = 0.0 if y1 == y0 else max(0.0, min(1.0, (y - y0) / (y1 - y0)))
        col = tuple(round(a + (b - a) * t) for a, b in zip(*GRAD))
        for x in range(w):
            if pm[x, y]:
                px[x, y] = col + (255,)
    return img.resize((BOX, BOX), Image.LANCZOS)


def error_vs_export(img, ref):
    """Composite over #808080 (what the Figma export bakes) and score."""
    e = 0
    a, b = img.load(), ref.load()
    for y in range(0, BOX, 2):
        for x in range(0, BOX, 2):
            fg = a[x, y]; al = fg[3] / 255.0
            got = [round(fg[i] * al + 128 * (1 - al)) for i in range(3)]
            e += sum(abs(g - w) for g, w in zip(got, b[x, y][:3]))
    return e


if __name__ == "__main__":
    ref = Image.open("/tmp/controls_symbol.png").convert("RGBA").resize((BOX, BOX), Image.LANCZOS)
    # The node metadata puts a 50x50 vector at (11,17); the SVG viewBox is 55.12x56 because
    # the 6-wide stroke overhangs the geometry. Search a neighbourhood cheaply and let the
    # export decide, rather than trusting either number — then re-render the winner at full
    # supersample. Coarse pass first so this stays a few seconds, not a few minutes.
    best = None
    for scale in [round(0.80 + 0.02 * k, 2) for k in range(13)]:
        for ox in range(4, 16):
            for oy in range(8, 22):
                e = error_vs_export(build(ox, oy, scale, ss=2), ref)
                if best is None or e < best[0]:
                    best = (e, ox, oy, scale)
    _, bx, by, bs = best
    best = None
    for scale in [round(bs + 0.005 * k, 3) for k in range(-3, 4)]:
        for ox in [bx - 1, bx, bx + 1]:
            for oy in [by - 1, by, by + 1]:
                e = error_vs_export(build(ox, oy, scale, ss=4), ref)
                if best is None or e < best[0]:
                    best = (e, ox, oy, scale)
    _, ox, oy, scale = best
    img = build(ox, oy, scale, ss=SS)
    e = error_vs_export(img, ref)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print(f"  {OUT}  {BOX}x{BOX}  offset=({ox},{oy}) scale={scale}  "
          f"mean |delta| vs Figma export = {e / (36*36*3):.1f}/255 per channel")
