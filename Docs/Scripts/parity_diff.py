#!/usr/bin/env python3
"""Rest-parity diff: animated-arrival captures vs motion-off captures, same session.

    python3 Docs/Scripts/parity_diff.py <task_slug>

WHAT IT IS ACTUALLY COMPARING, because "0 px rest parity" is easy to claim and easy to fake.
GamePolishProbeB's `parity` mode walks every shell screen TWICE in ONE play session: once with
UiMotion.Enabled true, once false. Motion off makes every helper settle its target immediately
and start no coroutine, so the two passes can only differ if something this task added leaves a
mark on the SETTLED screen. Comparing against a baseline captured an hour ago would instead
diff a moved RP balance and a ticking countdown — tens of thousands of pixels with nothing to
do with motion.

WHY A TOLERANCE AT ALL, and why it is not a fudge. These screens carry live text: a countdown
pill re-renders every second, and a screen visited forty seconds apart in the same session can
legitimately show a different number of seconds. So the report is the DIFFERING PIXEL COUNT and
WHERE, never a bare pass/fail — a handful of pixels in one text run is a clock, and a block of
thousands across a panel is a regression. The script prints both so the reader can tell them
apart rather than trusting a threshold someone picked.
"""
from __future__ import annotations
import os
import sys

try:
    from PIL import Image, ImageChops
except ImportError:
    sys.exit("Pillow required:  pip install Pillow")


def bbox_area(bb):
    if bb is None:
        return 0
    return (bb[2] - bb[0]) * (bb[3] - bb[1])


def main() -> int:
    task = sys.argv[1] if len(sys.argv) > 1 else "game_polish_b"
    shots = f"Docs/Specs/Active/{task}/screenshots"
    if not os.path.isdir(shots):
        sys.exit(f"no screenshots dir at {shots}")

    # PAIR ON THE SCREEN NAME, not by rewriting the filename. The capture helper numbers shots
    # sequentially across the whole run, so the same screen is e.g. parity_01_..._anim_Home and
    # parity_12_..._instant_Home — a string substitution builds a path that does not exist and
    # the script silently finds zero pairs.
    anim, inst = {}, {}
    for f in sorted(os.listdir(shots)):
        if not f.endswith(".png"):
            continue
        for tag, bucket in (("_parity_anim_", anim), ("_parity_instant_", inst)):
            if tag in f:
                bucket[f.split(tag, 1)[1][:-4]] = os.path.join(shots, f)

    pairs = [(anim[k], inst[k]) for k in sorted(anim) if k in inst]
    missing = sorted(set(anim) ^ set(inst))
    if missing:
        print("unpaired (captured in only one pass):", ", ".join(missing))

    if not pairs:
        sys.exit("no parity_anim_* / parity_instant_* pairs found — run the probe's parity mode")

    print(f"{'screen':<34}{'differing px':>14}{'of total':>12}{'  bbox':<22}verdict")
    print("-" * 96)

    worst = 0
    for a_path, b_path in pairs:
        name = os.path.basename(a_path).split("_parity_anim_", 1)[1][:-4]
        a = Image.open(a_path).convert("RGB")
        b = Image.open(b_path).convert("RGB")
        if a.size != b.size:
            print(f"{name:<34}{'SIZE MISMATCH':>14}  {a.size} vs {b.size}")
            continue

        diff = ImageChops.difference(a, b)
        # Any channel differing by more than 8/255 counts. Below that is encoder noise, not a
        # moved pixel — these are PNGs off the same RT, so in practice the count is 0 or large.
        mask = diff.convert("L").point(lambda v: 255 if v > 8 else 0)
        n = sum(mask.histogram()[255:])
        total = a.size[0] * a.size[1]
        bb = mask.getbbox()
        worst = max(worst, n)
        verdict = "IDENTICAL" if n == 0 else ("text-sized" if n < 4000 else "REGION — INSPECT")
        print(f"{name:<34}{n:>14,}{total:>12,}  {str(bb):<22}{verdict}")

    print("-" * 96)
    print(f"pairs: {len(pairs)}   worst differing-pixel count: {worst:,}")
    print("0 everywhere is the A3 claim. A few thousand in one text-shaped bbox is a live clock")
    print("or a live balance; a large bbox over a panel is a regression and must be opened.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
