#!/usr/bin/env python3
"""Bake the two plates the gacha card's tagline overlay draws (gacha_banner_tagline §4).

    python3 Docs/Scripts/make_gacha_tagline_sprites.py

Follows the palette's generated-sprite convention (`Docs/Architecture/UI_ELEMENT_PALETTE.md`
§ "Baked-from-tokens sprites"): the SCRIPT is the source of truth for size and colour, and the PNG
is a build product. Edit this, never the PNGs.

Both fills are GRADIENTS, which neither a tinted atom nor a flat `Image.color` can reproduce (a
tint is one colour by definition — memory `reference_figma_css_hides_gradient_stops`). The tokens
below are read off the Figma nodes, not the CSS summary:

  S_GachaTaglineRibbon   876x64   `Tagline/Ribbon` 14281:33634 — horizontal #E4007F → #FF4FA3 @ 96 %.
  S_GachaTaglineHook     876x188  `Tagline/Hook`   14281:33636 — navy #0B1B3A, alpha 0 → 0.93 (16 %)
                                                                 → 0.93 (84 %) → 0, horizontal.

WIDTH IS 876, NOT THE NODE'S 882: the Unity `ArtImage` is inset 3 px each side of the 882 card
(SPEC §4 fidelity table), and both plates are its children. Baked at final size and drawn
`Image.Type.Simple`, 1:1 — nothing here ever resizes, so there is nothing 9-slicing would buy and
no corner to distort.

Colours are written STRAIGHT (non-premultiplied) with the node's alpha in the pixels: the Image
tint stays white so the fidelity linter reads the sprite, not a tint.

THE ALPHA IS COMPENSATED FOR LINEAR-SPACE BLENDING. Figma composites in sRGB; this project renders
in linear colour space, where the same 0.93 navy over the bright glow of the artwork reads visibly
lighter — measured on the first bake: (77,79,86) where the node render shows (28,43,69), an
sRGB-equivalent alpha of ~0.77 (memory `reference_linear_space_alpha_and_canvas_sorting`). Fitting
Unity's linear blend against the node render over the text-free band rows gives
`a_linear = 1 - (1 - a_srgb) ** 1.8` (RMS error 26.5 -> 11.8 over 9 500 samples; 0.93 -> 0.992).
The node's stops stay the source of truth; the exponent is the colour-space fix.

ONLY THE HOOK IS COMPENSATED. The discrepancy is a bright-underlay effect: over a dark underlay the
two blends agree, and over-compensating there reads too bright. The ribbon sits in the artwork's
title zone, which is dark on every safe-area artwork (the title has to be legible on it), and its
0.96 measured within 6 levels of the node render uncompensated — compensated it overshot by ~10.
So the ribbon keeps the node's 0.96 verbatim and the hook, which sits over the glow, gets the fit.
"""


from __future__ import annotations

import os
import sys

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    sys.exit("Pillow is required: pip install Pillow")

OUT_DIR = os.path.join("Assets", "Art", "Gacha")
# sRGB-authored alpha -> the linear-blend alpha that reproduces it over the artwork (see above).
LINEAR_BLEND_GAMMA = 1.8


def _linear_alpha(a_srgb: float) -> float:
    return 1.0 - (1.0 - a_srgb) ** LINEAR_BLEND_GAMMA


RIBBON_W, RIBBON_H = 876, 64
RIBBON_FROM, RIBBON_TO, RIBBON_A = (0xE4, 0x00, 0x7F), (0xFF, 0x4F, 0xA3), 0.96

HOOK_W, HOOK_H = 876, 188
HOOK_RGB = (0x0B, 0x1B, 0x3A)
# (position 0..1, alpha 0..1) — the node's four stops, verbatim.
HOOK_STOPS = ((0.00, 0.00), (0.16, 0.93), (0.84, 0.93), (1.00, 0.00))


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def _ramp(stops, t: float) -> float:
    """Piecewise-linear alpha at horizontal position t (0..1)."""
    for (p0, a0), (p1, a1) in zip(stops, stops[1:]):
        if p0 <= t <= p1:
            return _lerp(a0, a1, 0.0 if p1 == p0 else (t - p0) / (p1 - p0))
    return stops[-1][1]


def ribbon(path: str) -> None:
    img = Image.new("RGBA", (RIBBON_W, RIBBON_H))
    px = img.load()
    a = round(RIBBON_A * 255)   # dark underlay by design — see the docstring
    for x in range(RIBBON_W):
        t = x / (RIBBON_W - 1)
        rgb = tuple(round(_lerp(RIBBON_FROM[i], RIBBON_TO[i], t)) for i in range(3))
        for y in range(RIBBON_H):
            px[x, y] = (*rgb, a)
    img.save(path)


def hook(path: str) -> None:
    img = Image.new("RGBA", (HOOK_W, HOOK_H))
    px = img.load()
    for x in range(HOOK_W):
        a = round(_linear_alpha(_ramp(HOOK_STOPS, x / (HOOK_W - 1))) * 255)
        for y in range(HOOK_H):
            px[x, y] = (*HOOK_RGB, a)
    img.save(path)


def main() -> int:
    if not os.path.isdir(OUT_DIR):
        sys.exit(f"{OUT_DIR} not found — run from the repo root.")
    for name, fn in (("S_GachaTaglineRibbon.png", ribbon), ("S_GachaTaglineHook.png", hook)):
        path = os.path.join(OUT_DIR, name)
        fn(path)
        print(f"wrote {path} ({os.path.getsize(path)} B)")
    print("\nNow force the Sprite import in Unity — a new PNG imports as a TEXTURE by default "
          "(TextureImporter.textureType = Sprite, mipmaps off, wrap Clamp).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
