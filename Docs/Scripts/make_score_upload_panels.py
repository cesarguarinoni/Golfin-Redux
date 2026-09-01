#!/usr/bin/env python3
"""score_upload_flow — bake every CARD in the flow from its Figma tokens.

    python3 Docs/Scripts/make_score_upload_panels.py

Edit THIS, not the PNGs — same contract as `make_daily_pill_panel.py`: the sprites are derived from
the Figma node's tokens, so a design change is a token change here and a re-run, never a hand edit
in an image editor.

THE RECIPE, WHICH IS THE SAME FOR EVERY CARD IN THE FLOW
────────────────────────────────────────────────────────
Pulled from the nodes (`get_design_context`, 2026-09-01) — every bordered card reads:

    border : 3px solid #FFFFFF          (r32 on the small fact tiles, r50 everywhere else)
    fill   : a gradient or a translucent flat
    effect : backdrop-blur 2px + shadow 0 10 20 rgba(0,0,0,0.4)

Only the FILL varies. That uniformity is the thing the first build missed: it reused the opaque
`Next Hole Panel` sprite (a navy gradient with a SILVER stroke) for all of them, which reads far too
solid over the photo backgrounds and has the wrong stroke colour.

WHY BAKED PER SIZE RATHER THAN 9-SLICED
───────────────────────────────────────
A vertical gradient cannot survive 9-slicing — the stretched middle row flattens it into three
bands. Every card here has a FIXED node size, so each is baked at that size and used with
`Image.Type.Simple`. Same conclusion as the fixed-size pill capsule
(`reference_fixed_size_pill_capsule_sprite`): when a fill cannot survive slicing, bake it.

The flat, BORDERLESS strips (step bar, source row, found row, success block, share block) are NOT
here: a flat colour with rounded corners 9-slices perfectly, so those stay on the palette's
`S_PillStadium` atom tinted by `Image.color`, at any size.

NOT REPRODUCED, and called out rather than faked: the 2px BACKDROP BLUR. Unity UI has no backdrop
filter; faking it would mean a grab-pass per card. The fill alpha is exact, so the cards are the
right colour over the photo — just not blurred behind.

TOKENS (Figma 5gEAHjl6xAtW8iYY7NMvWd)
  blue   rgba(19,52,83,0.6) -> rgba(9,27,51,0.6)   the flow's default card
  green  #1d6b46 -> #0f3d2a                        Score Hero 14024:101738, Share Card 14024:102057
  venue  rgba(15,61,42,0.85)                       Venue Card 14024:33471
  points rgba(59,47,15,0.85)                       Points Panel 14024:101778
  ink    #0a0f16                                   Viewfinder Panel 14022:32906
"""
import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(REPO, "Assets", "Art", "UI", "Gps")

# Baked at 2x so the corner arcs stay clean on a 3x device; Unity samples them down.
SCALE = 2

GREEN_TOP = (0x1D, 0x6B, 0x46)
GREEN_BOTTOM = (0x0F, 0x3D, 0x2A)

# (r, g, b, a) — alpha 1.0 for the opaque fills.
BLUE_TOP     = (0x13, 0x34, 0x53, 0.60)
BLUE_BOTTOM  = (0x09, 0x1B, 0x33, 0.60)
GREEN_TOP    = (0x1D, 0x6B, 0x46, 1.00)
GREEN_BOTTOM = (0x0F, 0x3D, 0x2A, 1.00)
VENUE        = (0x0F, 0x3D, 0x2A, 0.85)
POINTS       = (0x3B, 0x2F, 0x0F, 0.85)
INK          = (0x0A, 0x0F, 0x16, 1.00)

BORDER = (0xFF, 0xFF, 0xFF, 1.00)   # every bordered card: 3px solid white
FILL_ALPHA = 0.18                   # every status pill's interior
STROKE = 3


def _to_linear(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def _to_srgb(c):
    c = max(0.0, min(1.0, c))
    v = c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055
    return v * 255.0


BACKGROUNDS = [
    "Assets/Art/RankingsScreen/BackgroundRangkings.png",
    "Assets/Art/LoadingScreen/Loading Background.png",
    "Assets/Art/UI/Gps/Backgrounds/BG_SU_EditScore.png",
    "Assets/Art/UI/Gps/Backgrounds/BG_SU_GpsProof.png",
    "Assets/Resources/HoleImages/MissionsBackground.png",
    "Assets/Art/Shop/Background - Blurred.png",
]

_BG_CACHE = {}


def _background(step):
    """The step's background, as the 1170x2532 the screen actually draws."""
    if step not in _BG_CACHE:
        im = Image.open(os.path.join(REPO, BACKGROUNDS[step])).convert("RGB")
        if im.size != (1170, 2532):
            im = im.resize((1170, 2532), Image.LANCZOS)
        _BG_CACHE[step] = np.asarray(im).astype(np.float64)
    return _BG_CACHE[step]


def fit_over_background(step, rect, top, bottom):
    """Solve for the (alpha, colour) Unity must store to REPRODUCE Figma's composite.

    Figma composites in sRGB; this project renders in LINEAR colour space, so Unity's blend of the
    node's own (colour, alpha) lands both lighter AND the wrong hue — measured on the Confirm step's
    Course Row, the node's rgba(19,52,83,0.6) arrived as (37,59,61) where Figma has (48,73,42): a
    green card turned blue by its own blue channel coming through at the inflated alpha.

    Rather than hand-derive a correction, fit it. Over this card's OWN background B:

        target  T      = a*F + (1-a)*B                     (what Figma draws, in sRGB)
        unity   lin(T) = a'*F'_lin + (1-a')*lin(B)         (what Unity will draw)

    Substituting u = a'*F'_lin and v = 1-a', the second equation is LINEAR in (u, v), so a single
    least-squares solve over every pixel of the card's footprint gives the exact pair. Returns
    (rgb, alpha) to store.

    A fully opaque fill needs no fit — there is no background left to get wrong.
    """
    if top[3] >= 0.999 and bottom[3] >= 0.999:
        return None

    x, y, w, h = rect
    B = _background(step)[y:y + h, x:x + w, :]
    rows = np.linspace(0.0, 1.0, B.shape[0])[:, None]

    F = np.stack([np.full(B.shape[:2], 0.0) + top[i] + (bottom[i] - top[i]) * rows for i in range(3)], axis=2)
    a = (top[3] + (bottom[3] - top[3]) * rows)[:, :, None]

    T = a * F + (1.0 - a) * B                      # Figma's composite, sRGB
    T_lin, B_lin = _lin_arr(T), _lin_arr(B)

    # Least squares for [u_r, u_g, u_b, v]: lin(T)_c = u_c + v * lin(B)_c
    n = B_lin.shape[0] * B_lin.shape[1]
    A = np.zeros((n * 3, 4))
    rhs = np.zeros(n * 3)
    for c in range(3):
        sl = slice(c * n, (c + 1) * n)
        A[sl, c] = 1.0
        A[sl, 3] = B_lin[:, :, c].ravel()
        rhs[sl] = T_lin[:, :, c].ravel()
    sol, *_ = np.linalg.lstsq(A, rhs, rcond=None)

    v = float(np.clip(sol[3], 0.0, 1.0))
    a_fit = 1.0 - v
    if a_fit < 1e-3:
        return None
    rgb = tuple(float(np.clip(_to_srgb(max(0.0, sol[c]) / a_fit), 0, 255)) for c in range(3))
    return rgb, a_fit


def _lin_arr(v):
    c = v / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


# The pills are tinted at runtime with whichever accent the state calls for, and they all sit on
# the same family of dark translucent cards — so ONE solve, against the representative pair, is
# what a single baked alpha can honestly carry.
PILL_HUE = (126, 212, 136)          # Green #7ED488, the most-used tint
PILL_BACKDROP = (36, 55, 68)        # measured under the GPS ON pill on the node render
PILL_TARGET = (57, 91, 84)          # what the node render actually shows inside that pill


def alpha_for_target(target, overlay, background):
    """The alpha whose LINEAR blend of `overlay` over `background` lands on `target`.

    Same solve as `alpha_over`, but driven by what the node RENDER measures rather than by the
    design alpha — the render is the reference the build is diffed against, and it carries the
    node's blur and effects, which the ideal formula does not. (0.18 nominal -> 0.131 here.)
    """
    total, n = 0.0, 0
    for t, f, b in zip(target, overlay, background):
        lf, lb = _to_linear(float(f)), _to_linear(float(b))
        if abs(lf - lb) < 1e-4:
            continue
        total += min(1.0, max(0.0, (_to_linear(float(t)) - lb) / (lf - lb)))
        n += 1
    return 0.18 if n == 0 else total / n


def alpha_over(srgb_alpha, overlay, background):
    """The alpha whose LINEAR blend reproduces Figma's sRGB blend of `overlay` over `background`.

    Figma: T = a*F + (1-a)*B on sRGB values.  Unity: the same equation on LINEAR ones. Solving
    lin(T) = a'*lin(F) + (1-a')*lin(B) gives a' = (lin(T)-lin(B)) / (lin(F)-lin(B)), averaged over
    the channels that carry information. Mirrors A() in ScoreUploadScreenBuilder.cs.
    """
    total, n = 0.0, 0
    for f, b in zip(overlay, background):
        lf, lb = _to_linear(float(f)), _to_linear(float(b))
        if abs(lf - lb) < 1e-4:
            continue
        t = srgb_alpha * f + (1.0 - srgb_alpha) * b
        total += min(1.0, max(0.0, (_to_linear(t) - lb) / (lf - lb)))
        n += 1
    return srgb_alpha if n == 0 else total / n


def linear_alpha(a):
    """Figma's sRGB alpha -> the alpha Unity needs to LOOK the same.

    The project renders in LINEAR colour space (`m_ActiveColorSpace: 1`), so Unity composites a
    translucent panel in linear light while Figma composites it in sRGB. For a dark overlay the
    linear blend lands much lighter: measured on the Recognition Panel over its own background,
    the node's 0.60 produced an EFFECTIVE 0.36 on screen (bg 253,233,45 -> Figma 111,125,67 but
    Unity 166,164,74). That gap is the whole reason the panels read "not transparent enough" —
    they were, in fact, too transparent.

    Solving `((1-a)*B)^2.2 == (1-a')*B^2.2` for a dark fill gives `a' = 1 - (1-a)^2.2`, which is
    independent of the background. 0.60 -> 0.866, 0.70 -> 0.929, 0.18 -> 0.353.
    """
    return 1.0 - (1.0 - a) ** 2.2

# name, w, h, radius, top fill, bottom fill (None = flat)
# name, w, h, radius, top fill, bottom fill (None = flat), step index, canvas x, canvas y.
# Canvas position = the Content Container's (96, 361) plus the node's own x/y — needed so each
# card's translucent fill can be FITTED over the background it actually sits on.
CARDS = [
    # ── step 1 ──
    ("S_SU_ViewfinderPanel.png",  958, 1080, 50, INK,        None,         0, 106,  479),
    # ── step 2 ── height is 1139, not the node's 1045: a COURSE row is inserted under TOTAL
    #              because the recognition returns a course name the frame has no slot for.
    ("S_SU_RecognitionPanel.png", 958, 1139, 50, BLUE_TOP,   BLUE_BOTTOM,  1, 106,  479),
    # ── step 3 ──
    ("S_SU_SummaryPanel.png",     958,  182, 50, BLUE_TOP,   BLUE_BOTTOM,  2, 106,  479),
    ("S_SU_HolesPanel.png",       958, 1193, 50, BLUE_TOP,   BLUE_BOTTOM,  2, 106,  685),
    # ── step 4 ──
    ("S_SU_LocatingPanel.png",    958,  560, 50, BLUE_TOP,   BLUE_BOTTOM,  3, 106,  479),
    ("S_SU_VenueCard.png",        958,  177, 50, VENUE,      None,         3, 106, 1151),
    ("S_SU_FactTile.png",         307,  118, 32, BLUE_TOP,   BLUE_BOTTOM,  3, 431, 1352),
    # ── step 5 ──
    ("S_SU_HeroGradient.png",     958,  386, 50, GREEN_TOP,  GREEN_BOTTOM, 4, 106,  479),
    ("S_SU_CourseRow.png",        958,  110, 50, BLUE_TOP,   BLUE_BOTTOM,  4, 106,  889),
    ("S_SU_TrustPanel.png",       958,  267, 50, BLUE_TOP,   BLUE_BOTTOM,  4, 106, 1023),
    ("S_SU_PointsPanel.png",      958,   96, 50, POINTS,     None,         4, 106, 1314),
    ("S_SU_ErrorStrip.png",       958,  120, 24, (0xF0, 0x80, 0x80, 0.15), None, 4, 106, 1957),
    # ── step 6 ──
    ("S_SU_ShareGradient.png",    760,  417, 50, GREEN_TOP,  GREEN_BOTTOM, 5, 205,  705),
    ("S_SU_VotePrompt.png",       958,  197, 50, BLUE_TOP,   BLUE_BOTTOM,  5, 106, 1146),
    # ── the venue picker modal, over the GPS step dimmed by its 60% backdrop ──
    ("S_SU_ModalPanel.png",       978, 1400, 50, BLUE_TOP,   BLUE_BOTTOM,  3,  96,  500),
    ("S_SU_ModalRow.png",         898,   96, 24, BLUE_TOP,   BLUE_BOTTOM,  3, 136,  750),
    ("S_SU_SearchField.png",      898,   90, 24, (0x00, 0x00, 0x00, 0.35), None, 3, 136, 630),
]

# White so `Image.color` can tint them; the node colours are applied in Unity.
# (diameter at which the node uses it, stroke at that diameter) — baked square at 2x.
RINGS = [
    ("S_SU_RingThin.png", 300, 3),
    ("S_SU_RingThick.png", 220, 12),
]

GUIDE = ("S_SU_GuideFrame.png", 740, 460, 24, 4, 34, 24)  # w, h, radius, stroke, dash, gap

# ── status pills ──────────────────────────────────────────────────────────────
# Every pill in the flow is `border 1px <hue>` over `bg <hue>@0.18`, r100. Baked WHITE with the
# alpha carrying the structure — 1.0 in the rim band, 0.18 inside — so ONE `Image.color = hue`
# reproduces both layers at once and the colour can still change at runtime (green <-> red).
#
# The first build drew the rim as a full-size OPAQUE capsule with the translucent fill on top,
# which is not a border: the opaque capsule showed through everywhere and the pills rendered as
# solid blobs that swallowed their own label.
#
# Baked per node size rather than 9-sliced: a 1px rim cannot survive slicing at four different
# heights, and there are only five sizes.
PILLS = [
    ("S_SU_PillConfidence.png", 125, 43),   # 14023:33014
    ("S_SU_PillGps.png",        147, 40),   # 14024:33458
    ("S_SU_PillTrust.png",      156, 38),   # 14024:102060
    ("S_SU_PillRound.png",      192, 40),   # 14024:102066
]

# The 18/9 segmented control's TRACK is the one pill whose rim and fill are different colours
# (#818ea1 rim over rgba(0,0,0,0.35)), so it is baked with those colours rather than tinted.
SEGMENTED = ("S_SU_SegmentedTrack.png", 315, 50, (0x81, 0x8E, 0xA1), (0, 0, 0, 0.35))

# The 18/9 segmented control's ACTIVE half: a gold gradient capsule, no border
# (14035:101733). Baked because the gradient is vertical; 9-sliced at ppum 88/21 for its r21.
GOLD_SEGMENT = ("S_SU_GoldSegment.png", 176, 176, 88,
                (0xF3, 0xEC, 0xC2, 1.0), (0xC9, 0xA9, 0x4F, 1.0))


def _rounded_mask(W, H, R, aa=4):
    """Anti-aliased rounded-rect coverage mask. PIL's rounded_rectangle is hard-edged and a 100px
    radius shows every stair step, so it is drawn at 4x and downsampled."""
    m = Image.new("L", (W * aa, H * aa), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, W * aa - 1, H * aa - 1], radius=R * aa, fill=255)
    return m.resize((W, H), Image.LANCZOS)


def bake_card(path, w, h, radius, top, bottom, stroke=None, scale=None, fit=None):
    """A card: gradient-or-flat fill, a 3px white border, rounded corners, straight alpha.

    The border is the difference between the OUTER mask and an inset mask, so it is a crisp,
    uniformly-3px ring that follows the radius — not a UI `Outline` component, which Rule 21
    fails because it draws four offset copies instead of a stroke."""
    sc = SCALE if scale is None else scale
    W, H, R = w * sc, h * sc, radius * sc
    S = (STROKE if stroke is None else stroke) * sc

    if bottom is None:
        bottom = top

    # `fit` is the (rgb, alpha) solved against this card's own background — a flat pair, because
    # the fit collapses the gradient to the single colour+alpha that best reproduces the node's
    # composite in linear space. The gradient itself survives in the TARGET the fit was solved
    # against, so the visible result still carries it.
    if fit is not None:
        rgb, a_fit = fit
        top = (rgb[0], rgb[1], rgb[2], a_fit)
        bottom = top

    # Fill, premultiplied by nothing — straight RGBA, since Unity's UI shader expects straight alpha.
    fill = Image.new("RGBA", (W, H))
    px = fill.load()
    for y in range(H):
        t = y / max(1, H - 1)
        a = top[3] + (bottom[3] - top[3]) * t
        chans = [top[i] + (bottom[i] - top[i]) * t for i in range(3)]
        row = tuple(int(round(c)) for c in chans) + (int(round(a * 255)),)
        for x in range(W):
            px[x, y] = row

    outer = _rounded_mask(W, H, R)
    inner = Image.new("L", (W, H), 0)
    inner.paste(_rounded_mask(W - 2 * S, H - 2 * S, max(0, R - S)), (S, S))

    # Body = fill masked by the INNER shape; border = the ring between the two masks.
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    body = fill.copy()
    body.putalpha(ImageChops.multiply(fill.getchannel("A"), inner))
    out = Image.alpha_composite(out, body)

    ring = ImageChops.subtract(outer, inner)
    border = Image.new("RGBA", (W, H), (BORDER[0], BORDER[1], BORDER[2], 0))
    border.putalpha(ring)
    out = Image.alpha_composite(out, border)

    out.save(path)
    return W, H


def bake_pill(path, w, h, rim=(255, 255, 255), fill=None):
    """A capsule whose ALPHA carries the border: 1.0 in the 1px rim band, `fill` alpha inside.

    `fill` None means "white at 18%, tint me" — one `Image.color` then paints an opaque rim and an
    18% interior together, which is what the node draws. Passing an explicit (r,g,b,a) bakes a
    two-colour pill instead, for the one control whose rim and fill differ.
    """
    W, H, R, S = w * SCALE, h * SCALE, (h / 2.0) * SCALE, 1 * SCALE

    outer = _rounded_mask(W, H, R)
    inner = Image.new("L", (W, H), 0)
    inner.paste(_rounded_mask(W - 2 * S, H - 2 * S, max(0, R - S)), (S, S))
    ring = ImageChops.subtract(outer, inner)

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # The interior is a translucent tint and Unity composites it in LINEAR light while Figma
    # composites in sRGB. `linear_alpha` is the F~=0 special case and is the WRONG direction here:
    # a pill is tinted with a bright accent hue, so the linear blend lands far too BRIGHT and the
    # alpha has to SHRINK, not grow. Solve it properly against the dark card the pills sit on —
    # 0.18 -> 0.131, where linear_alpha gave 0.354. (Measured on the GPS ON pill: node
    # [57 91 84] over backdrop [36 55 68]; the old bake rendered [83 142 112].)
    fill_rgb = (255, 255, 255) if fill is None else fill[:3]
    fill_a = FILL_ALPHA if fill is None else fill[3]
    fill_a_lin = alpha_for_target(PILL_TARGET, PILL_HUE, PILL_BACKDROP)
    body = Image.new("RGBA", (W, H), tuple(int(c) for c in fill_rgb) + (0,))
    body.putalpha(inner.point(lambda v: int(v * fill_a_lin)))
    out = Image.alpha_composite(out, body)

    edge = Image.new("RGBA", (W, H), tuple(rim) + (0,))
    edge.putalpha(ring)
    out = Image.alpha_composite(out, edge)

    out.save(path)
    return W, H


def bake_ring(path, diameter, stroke):
    """A white annulus. Baked square so a Unity Image at any square size keeps it circular."""
    D = diameter * SCALE
    aa = 4
    mask = Image.new("L", (D * aa, D * aa), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([0, 0, D * aa - 1, D * aa - 1], fill=255)
    inset = stroke * SCALE * aa
    d.ellipse([inset, inset, D * aa - 1 - inset, D * aa - 1 - inset], fill=0)
    mask = mask.resize((D, D), Image.LANCZOS)

    out = Image.new("RGBA", (D, D), (255, 255, 255, 0))
    out.putalpha(mask)
    # putalpha on a fully-white image keeps RGB white where alpha > 0, which is what tinting needs.
    out = Image.merge("RGBA", (Image.new("L", (D, D), 255),) * 3 + (mask,))
    out.save(path)
    return D


def bake_guide(path, w, h, radius, stroke, dash, gap):
    """A dashed rounded-rect stroke, transparent inside. White; Unity tints it gold."""
    W, H, R, S = w * SCALE, h * SCALE, radius * SCALE, stroke * SCALE
    aa = 2
    AW, AH = W * aa, H * aa

    ring = Image.new("L", (AW, AH), 0)
    d = ImageDraw.Draw(ring)
    d.rounded_rectangle([0, 0, AW - 1, AH - 1], radius=R * aa, fill=255)
    d.rounded_rectangle([S * aa, S * aa, AW - 1 - S * aa, AH - 1 - S * aa],
                        radius=max(0, (R - S) * aa), fill=0)

    # Punch the gaps out with a comb of bars. The comb only covers the STRAIGHT runs — the
    # corner arcs stay solid, which is how the node's dash pattern renders them too.
    period = (dash + gap) * SCALE * aa
    on = dash * SCALE * aa
    comb = Image.new("L", (AW, AH), 0)
    cd = ImageDraw.Draw(comb)

    x = R * aa
    while x + on < AW - R * aa:
        x1 = min(x + period, AW - R * aa) - 1
        if x1 >= x + on:
            cd.rectangle([x + on, 0, x1, AH - 1], fill=255)
        x += period

    y = R * aa
    while y + on < AH - R * aa:
        y1 = min(y + period, AH - R * aa) - 1
        if y1 >= y + on:
            cd.rectangle([0, y + on, AW - 1, y1], fill=255)
        y += period

    # Vectorised "ring AND NOT comb" — the per-pixel Python loop over 5.4M samples was the
    # only slow part of this script.
    dashed = ImageChops.subtract(ring, comb)

    mask = dashed.resize((W, H), Image.LANCZOS)
    out = Image.merge("RGBA", (Image.new("L", (W, H), 255),) * 3 + (mask,))
    out.save(path)
    return W, H


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    for name, w, h, r, top, bottom, step, cx, cy in CARDS:
        fit = fit_over_background(step, (cx, cy, w, h), top, bottom if bottom else top)
        W, H = bake_card(os.path.join(OUT_DIR, name), w, h, r, top, bottom, fit=fit)
        note = "opaque" if fit is None else ("fit rgb(%.0f,%.0f,%.0f) a=%.3f" % (fit[0] + (fit[1],)))
        print("%-28s %dx%d  (node %dx%d r%d, %s)" % (name, W, H, w, h, r, note))

    for name, diameter, stroke in RINGS:
        D = bake_ring(os.path.join(OUT_DIR, name), diameter, stroke)
        print("%-28s %dx%d  (node dia %d stroke %d @%dx)" % (name, D, D, diameter, stroke, SCALE))

    # Baked at 1x, 176x176 with a slice border of 88 — a drop-in for S_PillStadium's geometry, so
    # the same `ppum = 88 / radius` rule applies. At the node's 42px segment height the effective
    # border is 21 top and bottom, i.e. NO stretched middle row, so the vertical gradient survives
    # the 9-slice intact. (That is the one height at which a gradient and slicing can coexist.)
    for name, w, h in PILLS:
        W, H = bake_pill(os.path.join(OUT_DIR, name), w, h)
        print("%-28s %dx%d  (node %dx%d, 1px rim + 18%% fill, white/tintable)" % (name, W, H, w, h))

    # The stepper is a RING on the node — a thin light rim with the row showing straight through —
    # not a filled disc. Baked as a pill whose interior alpha is 0 so the two-Image "outer tint +
    # inset fill" construction cannot accidentally paint a solid puck (which is what it did).
    W, H = bake_pill(os.path.join(OUT_DIR, "S_SU_StepperRing.png"), 50, 50,
                     rim=(255, 255, 255), fill=(0, 0, 0, 0.0))
    print("%-28s %dx%d  (node 50x50 stepper ring, rim only, tintable)" % ("S_SU_StepperRing.png", W, H))

    name, w, h, rim, fill = SEGMENTED
    W, H = bake_pill(os.path.join(OUT_DIR, name), w, h, rim=rim, fill=fill)
    print("%-28s %dx%d  (node %dx%d, #818ea1 rim over black@0.35)" % (name, W, H, w, h))

    name, w, h, r, top, bottom = GOLD_SEGMENT
    W, H = bake_card(os.path.join(OUT_DIR, name), w, h, r, top, bottom, stroke=0, scale=1)
    print("%-28s %dx%d  (gold segment gradient, 9-slice border 88, no stroke)" % (name, W, H))

    name, w, h, r, stroke, dash, gap = GUIDE
    W, H = bake_guide(os.path.join(OUT_DIR, name), w, h, r, stroke, dash, gap)
    print("%-28s %dx%d  (node %dx%d r%d dash %d/%d @%dx)" % (name, W, H, w, h, r, dash, gap, SCALE))


if __name__ == "__main__":
    main()
