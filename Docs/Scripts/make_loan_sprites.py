#!/usr/bin/env python3
"""Bake the four generated sprites the asset_loans UI needs.

    python3 Docs/Scripts/make_loan_sprites.py

Follows the palette's generated-sprite convention (`Docs/Architecture/UI_ELEMENT_PALETTE.md`
§ "Generated sprites"): the SCRIPT is the source of truth for size, radius and colour, and the PNG
is a build product. Edit this, never the PNGs.

WHY THESE FOUR ARE BAKED RATHER THAN REUSED. Each is a FIXED-SIZE element with a shape no shipped
atom carries:

  S_LoanRibbon        537x72, TOP corners rounded r=20 and the bottom square. Every rounded atom in
                      the project rounds all four; using one would put a visible notch where the
                      ribbon meets the portrait it sits on. Baked WHITE and tinted at runtime, so
                      the Figma fill (#050F1F @ 72 %) lives on the Image, not in the pixels.
  S_LoanBadge         44x44, two colours in one sprite (a #050F1F @ 85 % disc inside a 2 px white
                      ring), so it cannot be a tinted white shape. Baked with its real colours.
  S_LoanRow           732x96 r=12, WHITE — the recipient row's unselected fill (#050F1F @ 60 %) is
                      a tint of this.
  S_LoanRowSelected   the same rect with the selected fill (#2775DD @ 35 %) AND its 3 px #2775DD
                      stroke baked in. A SPRITE SWAP, not an `Outline` component: PIPELINE_HARDENING
                      C5 — an Outline is four offset copies of the graphic, not a crisp N-px border,
                      and the UI fidelity linter fails it.

Everything else the loan UI draws is REUSED, with provenance in IMPLEMENTER_REPORT §Clone provenance.

Sizes are 1:1 canvas px: the game canvas is 1170x2532 and so is the Figma frame.
"""

from __future__ import annotations

import os
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:  # pragma: no cover
    sys.exit("Pillow is required: pip install Pillow")

# 4x supersampling, then LANCZOS down. Straight PIL rounded_rectangle at 1x leaves visibly
# stair-stepped corners at r=12 and r=20, which reads as a rendering bug rather than a design.
SS = 4

OUT_DIR = os.path.join("Assets", "Art", "RosterScreen")


def _canvas(w: int, h: int) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def _down(img: Image.Image, w: int, h: int) -> Image.Image:
    return img.resize((w, h), Image.LANCZOS)


def ribbon(path: str, w: int = 537, h: int = 72, radius: int = 20) -> None:
    """White, TOP corners rounded, bottom square. Tinted #050F1F @ 72 % by LoanRibbonView."""
    img, d = _canvas(w, h)
    r = radius * SS
    # A full rounded rect, then the bottom half of the corner band squared off again — cheaper and
    # more accurate than composing two shapes with a seam between them.
    d.rounded_rectangle([0, 0, w * SS - 1, h * SS - 1], radius=r, fill=(255, 255, 255, 255))
    d.rectangle([0, h * SS - r, w * SS - 1, h * SS - 1], fill=(255, 255, 255, 255))
    _down(img, w, h).save(path)


def badge(path: str, size: int = 44, stroke: int = 2) -> None:
    """#050F1F @ 85 % disc, 2 px white ring. Used UNTINTED."""
    img, d = _canvas(size, size)
    s = size * SS
    sw = stroke * SS
    d.ellipse([0, 0, s - 1, s - 1], fill=(255, 255, 255, 255))
    d.ellipse([sw, sw, s - 1 - sw, s - 1 - sw], fill=(5, 15, 31, 217))  # 217/255 = 85 %
    _down(img, size, size).save(path)


def row(path: str, w: int = 732, h: int = 96, radius: int = 12) -> None:
    """White r=12. Tinted #050F1F @ 60 % for the unselected recipient row."""
    img, d = _canvas(w, h)
    d.rounded_rectangle([0, 0, w * SS - 1, h * SS - 1], radius=radius * SS,
                        fill=(255, 255, 255, 255))
    _down(img, w, h).save(path)


def row_selected(path: str, w: int = 732, h: int = 96, radius: int = 12, stroke: int = 3) -> None:
    """#2775DD @ 35 % fill with a 3 px #2775DD stroke, both baked. Used UNTINTED."""
    img, d = _canvas(w, h)
    r = radius * SS
    sw = stroke * SS
    d.rounded_rectangle([0, 0, w * SS - 1, h * SS - 1], radius=r, fill=(39, 117, 221, 255))
    d.rounded_rectangle([sw, sw, w * SS - 1 - sw, h * SS - 1 - sw],
                        radius=max(r - sw, 0), fill=(39, 117, 221, 89))  # 89/255 = 35 %
    _down(img, w, h).save(path)


def main() -> int:
    if not os.path.isdir(OUT_DIR):
        sys.exit(f"{OUT_DIR} not found — run from the repo root.")

    made = [
        ("S_LoanRibbon.png", ribbon),
        ("S_LoanBadge.png", badge),
        ("S_LoanRow.png", row),
        ("S_LoanRowSelected.png", row_selected),
    ]
    for name, fn in made:
        path = os.path.join(OUT_DIR, name)
        fn(path)
        print(f"wrote {path} ({os.path.getsize(path)} B)")

    print("\nNow force the Sprite import in Unity — a new PNG imports as a TEXTURE by default "
          "(memory: reference_new_png_imports_as_texture_not_sprite).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
