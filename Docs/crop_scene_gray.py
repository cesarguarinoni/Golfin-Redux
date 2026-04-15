"""Crop gray border from Scene captures in a given folder.

The Scene capture dumps the full SceneView window — the terrain render
is surrounded by Unity's gray editor chrome (toolbar, tabs, background).
This script finds the bounding box of non-gray pixels and crops to it.

Usage: python crop_scene_gray.py <folder>
Only files matching '* - Scene - *.png' are touched.
"""
from __future__ import annotations

import sys
from pathlib import Path
from PIL import Image

# Unity editor gray — roughly (45-60, 45-60, 45-60). Treat any pixel where
# R/G/B are within ~8 of each other AND all below 90 as "gray background".
def is_gray_chrome(r: int, g: int, b: int) -> bool:
    if r > 90 or g > 90 or b > 90:
        return False
    return max(r, g, b) - min(r, g, b) <= 12


def find_content_bbox(img: Image.Image) -> tuple[int, int, int, int] | None:
    rgb = img.convert("RGB")
    w, h = rgb.size
    px = rgb.load()

    left, right = w, -1
    top, bottom = h, -1
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if not is_gray_chrome(r, g, b):
                if x < left: left = x
                if x > right: right = x
                if y < top: top = y
                if y > bottom: bottom = y
    if right < 0 or bottom < 0:
        return None
    return (left, top, right + 1, bottom + 1)


def crop_one(path: Path) -> None:
    img = Image.open(path)
    bbox = find_content_bbox(img)
    if bbox is None:
        print(f"  skip (all gray?): {path.name}")
        return
    out = img.crop(bbox)
    out.save(path)
    print(f"  {path.name}: {img.size} -> {out.size}")


def main() -> None:
    if len(sys.argv) != 2:
        print("usage: crop_scene_gray.py <folder>")
        sys.exit(1)
    folder = Path(sys.argv[1])
    if not folder.is_dir():
        print(f"not a directory: {folder}")
        sys.exit(1)

    pngs = sorted(folder.rglob("*.png"))
    if not pngs:
        print(f"no PNGs in {folder}")
        return
    print(f"cropping {len(pngs)} file(s) under {folder}")
    for p in pngs:
        crop_one(p)


if __name__ == "__main__":
    main()
