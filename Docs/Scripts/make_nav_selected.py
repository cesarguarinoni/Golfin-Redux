#!/usr/bin/env python3
"""
make_nav_selected.py — bake the bottom-nav SELECTED state: a gold halo and a
brighter ring, for the game bar and the GPS bar (game_polish_a §D7).

    python3 Docs/Scripts/make_nav_selected.py

WHY A SCRIPT AND NOT A FIGMA EXPORT. There is no node to export: `New Nav Bar
Buttons` (2098:8164) is `Property 1=Default` only and `Nav Bar Container`
(2098:7988) shows five identical slots, so Figma has no selected variant at all.
The constraint is the palette — the project's gold stroke
`#FCF195 -> #D6AB42 @0.6 -> #BB7F1D` (UI_ELEMENT_PALETTE.md) — plus the geometry
of the sprites that are already on the bar. Same reasoning as
make_daily_pill_panel.py / make_gps_icon_ring.py: EDIT THIS, NEVER THE PNGs.

GEOMETRY — measured off the shipped slot sprites, not eyeballed. Scanning the
horizontal centre line of each nav PNG for the gold band:

    Home.png / Gacha.png / Inventory.png   156x156   gold band r = 64.5 .. 74.5
    Character.png                          158x158   gold band r = 64.5 .. 75.0
    Hole Selection.png (the TEE slot)      238x238   gold band r = 105.0 .. 115.5

The band is ~10 px wide in BOTH sizes — it does not scale with the disc — which
is why the ring is baked twice instead of once and stretched. The four 156/158
slots share one bake: the 0.5 px radius difference on Character is below what a
10 px stroke can show, and NavSlotHighlight sizes the ring child from the
SPRITE's native size rather than from the button rect, so nothing is stretched.

THE HALO IS NOT A SECOND OUTLINE. It is the disc silhouette in gold with the
alpha carrying an outward falloff, drawn BEHIND the button on the additive
material the Home pill's glow uses (TapSparkle_Additive). The button sprite is
opaque out to the ring, so the half of the halo that lands under the disc is
covered and only the bloom outside it reads — which is what "halo" means.

SIZE. The halo is baked at HALF resolution and scaled up by the component. A
falloff has no high-frequency detail, so the upscale is invisible, and it takes
the four new textures from ~91 KB of ASTC 6x6 to ~50 KB. The rings are baked 1:1
because a ring edge does have an edge. New PNGs import as Texture, not Sprite
(memory: reference_new_png_imports_as_texture_not_sprite) — the importer pass in
GamePolishBuilder forces spriteImportMode and the iPhone ASTC 6x6 override that
build_size_diet phase 3 requires of every new UI texture.
"""
import math
import os

from PIL import Image

OUT = "Assets/Art/HomeScreen"

SS = 8          # supersample for the ring's edges
FEATHER = 24.0  # how far the halo reaches past the disc, in canvas px (§D7.1)
HALO_PEAK = 0.85

RING_RGB = (0xFC, 0xF1, 0x95)   # #FCF195 — the top stop of the gold stroke: "brighter ring"
HALO_RGB = (0xD6, 0xAB, 0x42)   # #D6AB42 — the stroke's mid stop; the halo's flat colour


def bake_ring(size, r_in, r_out, path):
    """The gold band only, at its exact measured radius and width. 1:1 with the slot."""
    px = size * SS
    c = px / 2.0
    ri, ro = r_in * SS, r_out * SS
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    pix = img.load()
    for y in range(px):
        dy = y + 0.5 - c
        for x in range(px):
            dx = x + 0.5 - c
            d = math.hypot(dx, dy)
            if ri <= d <= ro:
                pix[x, y] = RING_RGB + (255,)
    img = img.resize((size, size), Image.LANCZOS)
    img.save(path)
    return img.size


def bake_halo(disc_r, path, scale=0.5):
    """
    The disc silhouette with an outward falloff. Baked at `scale` (see the header's
    SIZE note); the component scales it back to 2*(disc_r + FEATHER).
    """
    outer = disc_r + FEATHER
    size = int(round(2 * outer * scale))
    c = size / 2.0
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    pix = img.load()
    rd, rf = disc_r * scale, FEATHER * scale
    for y in range(size):
        dy = y + 0.5 - c
        for x in range(size):
            dx = x + 0.5 - c
            d = math.hypot(dx, dy)
            if d <= rd:
                a = 1.0
            elif d >= rd + rf:
                a = 0.0
            else:
                t = (d - rd) / rf
                # Raised cosine, biased tight: smooth at both ends, most of the
                # light in the first third — a bloom hugging the disc, not a fog.
                a = (0.5 * (1.0 + math.cos(math.pi * t))) ** 1.6
            if a > 0.0:
                pix[x, y] = HALO_RGB + (int(round(255 * HALO_PEAK * a)),)
    img.save(path)
    return img.size


def main():
    os.makedirs(OUT, exist_ok=True)
    made = []

    # Rings — 1:1, measured band.
    made.append(("S_NavSlotRing_156.png",
                 bake_ring(156, 64.5, 74.5, os.path.join(OUT, "S_NavSlotRing_156.png"))))
    made.append(("S_NavSlotRing_238.png",
                 bake_ring(238, 105.0, 115.5, os.path.join(OUT, "S_NavSlotRing_238.png"))))

    # Halos — half resolution, disc radius = the ring's OUTER edge.
    made.append(("S_NavSlotGlow_156.png",
                 bake_halo(74.5, os.path.join(OUT, "S_NavSlotGlow_156.png"))))
    made.append(("S_NavSlotGlow_238.png",
                 bake_halo(115.5, os.path.join(OUT, "S_NavSlotGlow_238.png"))))

    total = 0
    for name, size in made:
        p = os.path.join(OUT, name)
        b = os.path.getsize(p)
        total += b
        print(f"  {name:26s} {size[0]}x{size[1]}  {b/1024:6.1f} KB source")
    print(f"  {'total source':26s} {'':9s} {total/1024:6.1f} KB")


if __name__ == "__main__":
    main()
