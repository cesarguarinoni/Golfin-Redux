#!/usr/bin/env python3
"""build_size_diet — A/B frame diff WITH ITS OWN NOISE FLOOR.

  python3 Docs/Scripts/ab_frame_diff.py <dir> <labelA> <labelB> [labelNoise]

Reports, per frame, mean and max per-pixel max-channel |dRGB| and the share of pixels that
moved at all.

WHY THE THIRD LABEL MATTERS. The hole scenes render wind-animated foliage, so two captures of
the SAME project state through the SAME camera are not identical. Without a noise floor the
before/after numbers are unreadable: the CONTROL holes (01 and 06, which contain no bridge and
whose art did not change) came out MORE different than the bridge close-ups. Passing a third
label — a second capture of the "after" state — measures that noise directly, so each
before/after number can be read against what the scene does on its own.
"""
import glob, os, sys
from PIL import Image

def stats(pa, pb):
    n = len(pa); tot = 0; mx = 0; changed = 0
    for i in range(0, n, 3):
        m = max(abs(pa[i]-pb[i]), abs(pa[i+1]-pb[i+1]), abs(pa[i+2]-pb[i+2]))
        tot += m
        if m > mx: mx = m
        if m: changed += 1
    px = n // 3
    return tot/px, mx, 100.0*changed/px

def load(d, name, label):
    p = f"{d}/{name}_{label}.png"
    return Image.open(p).convert("RGB").tobytes() if os.path.exists(p) else None

def main():
    d, a, b = sys.argv[1], sys.argv[2], sys.argv[3]
    noise = sys.argv[4] if len(sys.argv) > 4 else None
    names = sorted(os.path.basename(p)[:-len(f"_{b}.png")] for p in glob.glob(f"{d}/*_{b}.png"))
    head = f"{'frame':<24} {'mean|d|':>8} {'max|d|':>7} {'%moved':>8}"
    if noise: head += f"   |   {'mean|d|':>8} {'max|d|':>7} {'%moved':>8}   verdict"
    print(f"# {a} vs {b}" + (f"   |   noise floor: {b} vs {noise}" if noise else ""))
    print(head)
    for name in names:
        pa, pb = load(d, name, a), load(d, name, b)
        if pa is None or pb is None or len(pa) != len(pb):
            print(f"{name:<24}  (missing or size mismatch)"); continue
        m, x, c = stats(pa, pb)
        row = f"{name:<24} {m:>8.3f} {x:>7} {c:>7.2f}%"
        if noise:
            pn = load(d, name, noise)
            if pn is None or len(pn) != len(pb):
                row += "   |   (no noise frame)"
            else:
                nm, nx, nc = stats(pb, pn)
                verdict = ("BELOW the scene's own noise" if m <= nm
                           else f"{m/nm:.1f}x the noise floor" if nm > 0 else "noise floor is 0")
                row += f"   |   {nm:>8.3f} {nx:>7} {nc:>7.2f}%   {verdict}"
        print(row)

main()
