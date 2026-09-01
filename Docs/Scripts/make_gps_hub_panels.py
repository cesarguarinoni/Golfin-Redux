#!/usr/bin/env python3
"""Bake the GPS Hub's panel sprites — the hub's half of the score_upload_flow fidelity rework.

The hub (gps_hub_entry) was built with the OPAQUE `Next Hole Panel` sprite on every card, which is
the same mistake the first Score Upload build made: the node draws each of them as
`bg-gradient-to-b from-[rgba(19,52,83,0.6)] to-[rgba(9,27,51,0.6)] border-3 border-white`, so the
scene is meant to show through. Verified on the live nodes 14012:32489 (Hero) and 14012:98859
(Action Tiles) — the token is byte-identical to the Score Upload card family.

This reuses `make_score_upload_panels`'s generator wholesale; the only thing that changes is the
background the fit runs against (the hub's own `Home Background.png`) and the panel rects. Run it
the same way:

    python3 Docs/Scripts/make_gps_hub_panels.py
"""
import importlib.util
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
_spec = importlib.util.spec_from_file_location(
    "su_panels", os.path.join(REPO, "Docs", "Scripts", "make_score_upload_panels.py"))
su = importlib.util.module_from_spec(_spec)
try:
    _spec.loader.exec_module(su)
except SystemExit:
    pass

HUB_BACKGROUND = "Assets/Art/HomeScreen/Home Background.png"

# Screen-space rects, read off the live prefab with GetWorldCorners (canvas 1170x2532, scale 1),
# not guessed: `x, y, w, h, radius`. Radius is the sprite border / pixelsPerUnitMultiplier.
PANELS = [
    # name                        w     h   r    x     y
    ("S_HUB_HeroPanel.png",      989,  327, 50,   90,  418),
    ("S_HUB_StepsStrip.png",     978,  138, 32,   96,  737),
    ("S_HUB_ActionTile.png",     246,  173, 32,   96,  875),
    ("S_HUB_GiftsPanel.png",     989,  365, 50,   90, 1045),
    ("S_HUB_VotesPanel.png",     989,  337, 50,   90, 1399),
    ("S_HUB_RoundsPanel.png",    989,  503, 50,   90, 1725),
]


def main():
    # `fit_over_background` reads its backdrop through `_background(step)`; point step 0 at the
    # hub's own background and leave the Score Upload table untouched.
    su.BACKGROUNDS[0] = HUB_BACKGROUND
    su._BG_CACHE.clear()

    # Fitted PER PANEL, not once for the family. These are all the same node token, so one shared
    # solve looked like the tidier answer — but it measured WORSE against the node render (hub
    # dE 25.1 vs 22.7), because each panel sits on a different part of the background and the fit
    # is solving for what Unity must store to land on Figma's composite THERE. Same token, six
    # backdrops, six answers. `fit_over_background` clamps v to [0,1], so a low-variance backdrop
    # degrades to a sane alpha rather than a wild one.
    for name, w, h, r, x, y in PANELS:
        fit = su.fit_over_background(0, (x, y, w, h), su.BLUE_TOP, su.BLUE_BOTTOM)
        W, H = su.bake_card(os.path.join(su.OUT_DIR, name), w, h, r,
                            su.BLUE_TOP, su.BLUE_BOTTOM, fit=fit)
        rgb, a = fit
        print("%-24s %dx%d  (node %dx%d r%d) -> rgb(%d,%d,%d) a=%.3f"
              % (name, W, H, w, h, r, rgb[0], rgb[1], rgb[2], a))


if __name__ == "__main__":
    sys.exit(main())
