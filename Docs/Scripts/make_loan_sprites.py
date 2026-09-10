#!/usr/bin/env python3
"""Bake the generated sprites the asset_loans / asset_loans_offers UI needs.

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

asset_loans_offers adds four more, for the same reason — a fixed-size shape no shipped atom has:

  S_LoanSearchField   732x88 r=12, WHITE with a 2 px ring baked in and the middle punched out, so
                      one Image draws BOTH the #050F1F @ 75 % fill and the white @ 35 % stroke the
                      node calls for. Two stacked Images would be the alternative and would have to
                      agree about the radius forever. NOT reused from S_SU_SearchField: that atom
                      is a 898x120 fixed bake for the GPS card and stretching it to 732x88 is
                      exactly the non-uniform corner distortion the linter fails.
  S_LoanSearchGlyph   36x36 magnifier, WHITE, drawn from the node's own SVG geometry (a ring plus a
                      45-degree handle). Tinted at runtime.
  S_LoanTogglePill    112x60 stadium, WHITE, r=30 — a FULL capsule baked at final size and drawn
                      Simple, per memory `reference_fixed_size_pill_capsule_sprite`. A 9-sliced
                      rounded rect at this aspect collapses its corners
                      (PIPELINE_HARDENING C3 / linter render-health), and the toggle never resizes,
                      so there is nothing 9-slicing would buy.
  S_LoanToggleKnob    48x48 white disc with the node's 0/2/3 @ 30 % drop shadow baked into a 54x54
                      canvas, so the shadow is pixels rather than a component.

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


# ── asset_loans_offers ───────────────────────────────────────────────────────


def search_field(path: str, w: int = 732, h: int = 88, radius: int = 12,
                 stroke: int = 2) -> None:
    """WHITE ring, transparent middle — ONE Image draws the node's fill AND its stroke.

    The fill is the Image's own tint (#050F1F @ 75 %); the ring is baked at white @ 35 % so the
    two alphas multiply to the node's values when the Image is drawn at full white. Punching the
    middle out rather than filling it is what lets the tint reach the interior without also
    tinting the border.
    """
    img, d = _canvas(w, h)
    r = radius * SS
    sw = stroke * SS
    # The ring, at the stroke's own alpha.
    d.rounded_rectangle([0, 0, w * SS - 1, h * SS - 1], radius=r, fill=(255, 255, 255, 89))
    # The interior, at the fill's alpha — drawn OVER the ring, so the ring survives as a border.
    d.rounded_rectangle([sw, sw, w * SS - 1 - sw, h * SS - 1 - sw],
                        radius=max(r - sw, 0), fill=(255, 255, 255, 255))
    _down(img, w, h).save(path)


def search_glyph(path: str, size: int = 36) -> None:
    """A magnifier: a 2.5 px ring in the upper-left, plus a 45-degree handle to the lower-right.

    Drawn rather than exported because the node's asset is an SVG and the project has no SVG
    importer — the geometry is four numbers and re-deriving it here keeps the sprite a build
    product like every other one in this file.
    """
    img, d = _canvas(size, size)
    s = size * SS
    lw = max(1, int(round(2.5 * SS)))

    # Ring: centred at 40 % / 40 % with radius 30 % of the box — the node's proportions.
    cx = cy = int(s * 0.40)
    rad = int(s * 0.30)
    d.ellipse([cx - rad, cy - rad, cx + rad, cy + rad], outline=(255, 255, 255, 255), width=lw)

    # Handle: from the ring's lower-right edge to 88 % / 88 %.
    k = 0.7071  # cos 45°
    x0 = int(cx + rad * k)
    y0 = int(cy + rad * k)
    d.line([x0, y0, int(s * 0.88), int(s * 0.88)], fill=(255, 255, 255, 255), width=lw)

    _down(img, size, size).save(path)


def toggle_pill(path: str, w: int = 112, h: int = 60) -> None:
    """A FULL capsule at final size, WHITE. Tinted #2775DD / #38597F by the toggle.

    ⚠️ Drawn as Image.Type.Simple, never 9-sliced — memory
    `reference_fixed_size_pill_capsule_sprite`. The toggle is exactly 112x60 forever, so the whole
    reason to 9-slice (one sprite at many widths) does not apply, and a 9-slice at r=30 on a 60 px
    tall rect collapses its own corners.
    """
    img, d = _canvas(w, h)
    d.rounded_rectangle([0, 0, w * SS - 1, h * SS - 1], radius=(h // 2) * SS,
                        fill=(255, 255, 255, 255))
    _down(img, w, h).save(path)


def toggle_knob(path: str, size: int = 48, pad: int = 3, shadow_alpha: int = 77) -> None:
    """A white disc with the node's 0/2/3 @ 30 % drop shadow BAKED IN.

    The canvas is `size + 2*pad` so the blur has room; the caller places it at 48x48 plus the pad,
    which is why the pad is small and symmetric. Baked rather than a component for the same reason
    the selected row's stroke is: a Unity `Shadow` is one offset copy of the graphic, not a blur
    (PIPELINE_HARDENING C5).
    """
    from PIL import ImageFilter

    box = size + pad * 2
    img, d = _canvas(box, box)
    s = size * SS
    o = pad * SS

    # Shadow first, offset +2 px down, then blurred.
    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ds = ImageDraw.Draw(shadow)
    ds.ellipse([o, o + 2 * SS, o + s - 1, o + 2 * SS + s - 1], fill=(0, 0, 0, shadow_alpha))
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=3 * SS * 0.5))
    img.alpha_composite(shadow)

    d.ellipse([o, o, o + s - 1, o + s - 1], fill=(255, 255, 255, 255))
    _down(img, box, box).save(path)


def main() -> int:
    if not os.path.isdir(OUT_DIR):
        sys.exit(f"{OUT_DIR} not found — run from the repo root.")

    made = [
        ("S_LoanRibbon.png", ribbon),
        ("S_LoanBadge.png", badge),
        ("S_LoanRow.png", row),
        ("S_LoanRowSelected.png", row_selected),
        # asset_loans_offers
        ("S_LoanSearchField.png", search_field),
        ("S_LoanSearchGlyph.png", search_glyph),
        ("S_LoanTogglePill.png", toggle_pill),
        ("S_LoanToggleKnob.png", toggle_knob),
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
