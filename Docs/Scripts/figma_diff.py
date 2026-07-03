#!/usr/bin/env python3
"""
figma_diff.py — objective fidelity gate for Figma->Unity UI work.

Overlays a Unity isolated render on the Figma node render, computes the
per-region difference, and reports where they diverge — so a mismatch fails
the BUILDER (me), not Cesar. Use it before surfacing any UI prefab.

Usage:
    python Docs/Scripts/figma_diff.py <built.png> <reference_node.png> [out_prefix]

Outputs (next to out_prefix, default = built path without extension):
    <prefix>_diff.png  - built render with failing grid cells boxed in red
    <prefix>_sbs.png   - built (top) vs reference (bottom), same size, for eyeball A/B
Prints: overall mean diff %, number of failing cells, and the worst cells with
        normalized coordinates so the offending element is easy to locate.

Exit code 0 = PASS (overall < --overall and no cell over --cell), 1 = FAIL.

Pillow only (no numpy). Photographic cells (hero/thumbnail) naturally diff more;
the report is advisory per-cell — read WHERE it differs, don't just trust the total.
"""
import sys
import argparse
from PIL import Image, ImageChops, ImageStat, ImageDraw


def load_rgb(path):
    return Image.open(path).convert("RGB")


def run(built_path, ref_path, out_prefix, grid_x, grid_y, cell_thresh, overall_thresh):
    built = load_rgb(built_path)
    ref = load_rgb(ref_path)
    W, H = built.size
    ref = ref.resize((W, H))

    diff = ImageChops.difference(built, ref)
    overall = sum(ImageStat.Stat(diff).mean) / 3.0
    overall_pct = overall / 255.0 * 100.0

    cw, ch = max(1, W // grid_x), max(1, H // grid_y)
    failing = []
    for gy in range(grid_y):
        for gx in range(grid_x):
            box = (gx * cw, gy * ch,
                   (gx + 1) * cw if gx < grid_x - 1 else W,
                   (gy + 1) * ch if gy < grid_y - 1 else H)
            m = sum(ImageStat.Stat(diff.crop(box)).mean) / 3.0
            if m > cell_thresh:
                failing.append((round(m, 1), gx, gy, box))
    failing.sort(reverse=True)

    vis = built.copy()
    dr = ImageDraw.Draw(vis)
    for _, _, _, box in failing:
        dr.rectangle(box, outline=(255, 40, 40), width=2)
    vis.save(out_prefix + "_diff.png")

    sbs = Image.new("RGB", (W, H * 2 + 10), (18, 18, 20))
    sbs.paste(built, (0, 0))
    sbs.paste(ref, (0, H + 10))
    sbs.save(out_prefix + "_sbs.png")

    passed = overall <= overall_thresh and len(failing) == 0
    print(f"overall mean diff: {overall_pct:.2f}%  (raw {overall:.1f}/255, threshold {overall_thresh})")
    print(f"failing cells (> {cell_thresh}): {len(failing)} / {grid_x*grid_y}")
    for m, gx, gy, box in failing[:12]:
        nx, ny = gx / grid_x, gy / grid_y
        print(f"  cell ({gx:2d},{gy}) x~{nx:.2f} y~{ny:.2f}  diff={m}")
    print(f"diff map:  {out_prefix}_diff.png")
    print(f"A/B stack: {out_prefix}_sbs.png")
    print("RESULT:", "PASS" if passed else "FAIL")
    return 0 if passed else 1


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("built")
    ap.add_argument("reference")
    ap.add_argument("out_prefix", nargs="?", default=None)
    ap.add_argument("--grid-x", type=int, default=24)
    ap.add_argument("--grid-y", type=int, default=6)
    ap.add_argument("--cell", type=float, default=30.0, help="per-cell mean-diff fail threshold (0-255)")
    ap.add_argument("--overall", type=float, default=20.0, help="overall mean-diff fail threshold (0-255)")
    a = ap.parse_args()
    prefix = a.out_prefix or a.built.rsplit(".", 1)[0]
    sys.exit(run(a.built, a.reference, prefix, a.grid_x, a.grid_y, a.cell, a.overall))
