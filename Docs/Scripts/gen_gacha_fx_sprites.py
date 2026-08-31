#!/usr/bin/env python3
"""Generate the four gacha-reveal FX sprites (gacha_reveal_animation SPEC §4 "FX assets").

All four are WHITE with an alpha-only shape: the reveal modal tints every emitter and
overlay at runtime from RarityHelper.GetRarityColor, so nothing here may carry a colour.

    Assets/Art/Gacha/FX/S_Gacha_Glow.png      256x256  soft radial falloff
    Assets/Art/Gacha/FX/S_Gacha_Rays.png      512x512  12-spoke ray burst
    Assets/Art/Gacha/FX/S_Gacha_Spark.png      64x64    4-point star
    Assets/Art/Gacha/FX/S_Gacha_Confetti.png   16x24    rounded rect

Committed so the art is reproducible rather than a mystery binary. Re-run from the repo
root; it overwrites the PNGs and leaves the .meta files (and therefore the GUIDs) alone.

    python3 Docs/Scripts/gen_gacha_fx_sprites.py
"""
import os

import numpy as np
from PIL import Image

OUT_DIR = os.path.join("Assets", "Art", "Gacha", "FX")


def _write(name: str, alpha: np.ndarray) -> None:
    """Save a white RGB image carrying `alpha` (float 0..1) as its alpha channel."""
    h, w = alpha.shape
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.clip(alpha * 255.0, 0, 255).astype(np.uint8)
    path = os.path.join(OUT_DIR, name)
    Image.fromarray(rgba, "RGBA").save(path)
    print(f"wrote {path}  ({w}x{h})")


def _radial(size: int):
    """Normalised radius and angle grids centred on the image."""
    c = (size - 1) / 2.0
    y, x = np.mgrid[0:size, 0:size]
    dx = (x - c) / c
    dy = (y - c) / c
    return np.sqrt(dx * dx + dy * dy), np.arctan2(dy, dx)


def glow(size: int = 256) -> None:
    r, _ = _radial(size)
    # smoothstep falloff, squared to keep a bright core and a long soft tail
    a = np.clip(1.0 - r, 0.0, 1.0)
    _write("S_Gacha_Glow.png", a * a * (3 - 2 * a) * a)


def rays(size: int = 512, spokes: int = 12) -> None:
    r, theta = _radial(size)
    # sharp-ish spokes: a raised cosine of the spoke frequency, gated to the disc
    spoke = np.clip(np.cos(theta * spokes), 0.0, 1.0) ** 6
    radial_falloff = np.clip(1.0 - r, 0.0, 1.0) ** 1.5
    core = np.clip(1.0 - r * 6.0, 0.0, 1.0) ** 2       # small hot centre ties the spokes together
    _write("S_Gacha_Rays.png", np.clip(spoke * radial_falloff + core * 0.6, 0.0, 1.0))


def spark(size: int = 64) -> None:
    c = (size - 1) / 2.0
    y, x = np.mgrid[0:size, 0:size]
    dx = np.abs(x - c) / c
    dy = np.abs(y - c) / c
    # 4-point star: an astroid-ish body — bright on both axes, pinched on the diagonals
    d = (dx ** 0.5 + dy ** 0.5) ** 2
    _write("S_Gacha_Spark.png", np.clip(1.0 - d, 0.0, 1.0) ** 1.2)


def confetti(w: int = 16, h: int = 24) -> None:
    a = np.ones((h, w), dtype=float)
    a[0, :] = a[-1, :] = a[:, 0] = a[:, -1] = 0.5   # 1px soft edge so it doesn't alias hard
    for cy, cx in ((0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)):
        a[cy, cx] = 0.0                             # rounded corners
    _write("S_Gacha_Confetti.png", a)


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    glow()
    rays()
    spark()
    confetti()
