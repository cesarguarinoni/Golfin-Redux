#!/usr/bin/env python3
"""Bake the PLAYLIFE shell's PLACEHOLDER launch image.

THE APP ICON IS NO LONGER BAKED HERE (round 2, R1). Cesar's real icon —
`Assets/Art/Standalone/AppIcon_GolfinGps_1024_opaque.png`, green gradient with a white map pin —
replaced the generated placeholder after he installed build 2635 and saw it. Do not re-add an icon
baker: a generated icon that silently outranks the real artwork is exactly the failure this
removal prevents.

gps_standalone_shell §D6. Real branding comes from Ken and is a backlog row — this exists so
the shell is distinguishable ON THE SPRINGBOARD from the game sitting next to it, which is the
one thing a tester genuinely cannot do without. A build with no icon at all installs as a grey
square identical to every other iOS placeholder, and "which of these two is the standalone?" is
exactly the question the whole third-variant exercise has to be able to answer.

    python3 Docs/Scripts/make_standalone_icon.py

Writes (idempotent, deterministic — re-running produces byte-identical files):
    Assets/Art/Standalone/S_StandaloneLaunch.png     1170x2532, opaque

NO ALPHA, on purpose: App Store Connect rejects an alpha channel in the marketing icon
(ITMS-90717), and the same discipline is kept here so the launch image can never introduce one.
Verified by reading the written file back at the bottom of this script.

The palette is the GPS surface's own navy (sampled from Assets/Art/UI/Gps/Backgrounds/*.png),
so the placeholder at least looks like the product it launches rather than like a test asset.

Requires Pillow:  pip3 install --user Pillow
"""

import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:                                        # pragma: no cover - operator message
    sys.exit("Pillow is required:  pip3 install --user Pillow")

REPO = Path(__file__).resolve().parents[2]
OUT_DIR = REPO / "Assets" / "Art" / "Standalone"

LAUNCH_PATH = OUT_DIR / "S_StandaloneLaunch.png"

LAUNCH_SIZE = (1170, 2532)          # iPhone 14, the project's capture resolution

# Sampled from BG_PROF_Profile.png — the GPS surface's own gradient ends.
NAVY_TOP = (29, 62, 107)
NAVY_BOTTOM = (18, 44, 71)
GPS_GREEN = (86, 214, 143)
WHITE = (255, 255, 255)

WORDMARK = "GPS"
SUBMARK = "PLAYLIFE"


def vertical_gradient(size, top, bottom):
    """A plain top-to-bottom gradient. RGB, never RGBA — see the alpha note in the docstring."""
    w, h = size
    img = Image.new("RGB", (w, h))
    draw = ImageDraw.Draw(img)
    for y in range(h):
        t = y / max(h - 1, 1)
        draw.line(
            [(0, y), (w, y)],
            fill=tuple(round(top[c] + (bottom[c] - top[c]) * t) for c in range(3)),
        )
    return img


def load_font(px):
    """A real font if the system has one, Pillow's bitmap default otherwise.

    Deliberately not fatal: the placeholder's job is to be TELLABLE APART, and a blocky
    fallback wordmark does that just as well as a crisp one. A bake that died because a font
    moved would be a placeholder generator that blocks a build.
    """
    for candidate in (
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/System/Library/Fonts/Helvetica.ttc",
        "/Library/Fonts/Arial Bold.ttf",
    ):
        if Path(candidate).exists():
            try:
                return ImageFont.truetype(candidate, px)
            except OSError:
                continue
    return ImageFont.load_default()


def centered(draw, text, font, cx, cy, fill):
    left, top, right, bottom = draw.textbbox((0, 0), text, font=font)
    draw.text((cx - (right - left) / 2 - left, cy - (bottom - top) / 2 - top), text, font=font, fill=fill)


def build_launch():
    img = vertical_gradient(LAUNCH_SIZE, NAVY_TOP, NAVY_BOTTOM)
    draw = ImageDraw.Draw(img)
    w, h = LAUNCH_SIZE
    centered(draw, WORDMARK, load_font(int(w * 0.26)), w / 2, h * 0.46, WHITE)
    centered(draw, SUBMARK, load_font(int(w * 0.075)), w / 2, h * 0.56, GPS_GREEN)
    return img


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    build_launch().save(LAUNCH_PATH, "PNG", optimize=True)

    # Read back rather than trust the write: the alpha rule is the one that costs an upload.
    for path in (LAUNCH_PATH,):
        with Image.open(path) as check:
            assert check.mode == "RGB", f"{path.name} carries an alpha channel ({check.mode})"
            print(f"wrote {path.relative_to(REPO)}  {check.size[0]}x{check.size[1]}  mode={check.mode}")

    print("\nUnity imports a new PNG as a Texture (not a Sprite), which is exactly what")
    print("PlayerSettings.SetIcons wants — no import-settings change needed.")


if __name__ == "__main__":
    main()
