#!/usr/bin/env python3
"""auth_golf_profile — per-element A/B sheet + ΔRGB table, node render vs live capture.

    python3 Docs/Scripts/auth_golf_profile_ab.py

FIGMA_SCREEN_BUILD_PLAYBOOK §7: crop MATCHED regions from the node render and the live capture,
stack them, and enumerate the differences rather than asserting "it matches". On gps_profile_pack
this sheet found six defects Cesar had not named, after he named four.

Both sides are 1170x2532 and both draw the same Splash plate, so a region's ΔRGB is a like-for-like
comparison — the caveat the playbook raises (measuring against the wrong backdrop) does not apply.
Regions are given in canvas coordinates, derived from the geometry sheets in reference/nodes/.
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TASK = os.path.join(REPO, "Docs/Specs/Active/auth_golf_profile")
REF = os.path.join(TASK, "reference")
SHOT = os.path.join(TASK, "screenshots")
OUT = os.path.join(TASK, "fidelity")

# label, x, y, w, h   — canvas coords (1170x2532)
GOLF_REGIONS = [
    ("01 Top bar",            0,    0, 1170, 313),
    ("02 Panel (whole)",    106,  361,  958, 731),
    ("03 Intro title+sub",  146,  391,  878,  77),
    ("04 Colour swatches",  339,  494,  492, 120),
    ("05 Colour label",     146,  626,  878,  28),
    ("06 Nickname field",   146,  680,  878, 116),
    ("07 Experience chips", 146,  822,  878,  98),
    ("08 Handicap field",   146,  946,  878, 116),
    ("09 SAVE PROFILE",     106, 2257,  958, 120),
    ("10 Skip link",        106, 2401,  958,  31),
]

WELCOME_REGIONS = [
    ("01 Top bar",           0,    0, 1170, 313),
    ("02 Skip row",        106,  361,  958,  31),
    ("03 Welcome panel",   106,  416,  958, 385),
    ("04 Icon ring",       510,  452,  150, 150),
    ("05 Title + sub",     146,  616,  878, 112),
    ("06 Pager dots",      516,  752,  138,  24),
    ("07 Feature row 1",   106,  825,  958, 228),
    ("08 Feature row 2",   106, 1071,  958, 228),
    ("09 GET STARTED",     106, 2352,  958, 120),
]

SHEETS = [
    ("golf_profile", "golf_profile_14029-33628.png", "01_golf_profile_default.png", GOLF_REGIONS),
    ("welcome",      "welcome_14029-33929.png",      "04_welcome.png",              WELCOME_REGIONS),
]

PAD = 14
LABEL_H = 30


def _font(size=20):
    for p in ("/System/Library/Fonts/Supplemental/Arial Bold.ttf",
              "/System/Library/Fonts/Helvetica.ttc"):
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


def build(name, ref_png, built_png, regions):
    node = Image.open(os.path.join(REF, ref_png)).convert("RGB")
    live = Image.open(os.path.join(SHOT, built_png)).convert("RGB")
    assert node.size == live.size == (1170, 2532), (node.size, live.size)

    font = _font(20)
    head = _font(24)

    # Column width = the widest region, capped so the sheet stays readable.
    scale = min(1.0, 520.0 / max(w for _, _, _, w, _ in regions))
    cw = int(max(w for _, _, _, w, _ in regions) * scale)
    rows = []
    stats = []
    for label, x, y, w, h in regions:
        a = node.crop((x, y, x + w, y + h))
        b = live.crop((x, y, x + w, y + h))
        d = float(np.abs(np.asarray(a).astype(float) - np.asarray(b).astype(float)).mean())
        stats.append((label, w, h, d))
        sw, sh = int(w * scale), max(1, int(h * scale))
        rows.append((label, a.resize((sw, sh), Image.LANCZOS), b.resize((sw, sh), Image.LANCZOS), d))

    total_h = LABEL_H + sum(r[1].height + LABEL_H + PAD for r in rows) + PAD
    sheet = Image.new("RGB", (cw * 2 + PAD * 3, total_h), (24, 26, 30))
    dr = ImageDraw.Draw(sheet)
    dr.text((PAD, 6), "FIGMA NODE", font=head, fill=(255, 210, 120))
    dr.text((cw + PAD * 2, 6), "UNITY BUILT", font=head, fill=(150, 220, 255))

    yy = LABEL_H
    for label, a, b, d in rows:
        dr.text((PAD, yy), f"{label}   mean |dRGB| = {d:.2f}", font=font, fill=(230, 230, 230))
        yy += LABEL_H
        sheet.paste(a, (PAD, yy))
        sheet.paste(b, (cw + PAD * 2, yy))
        dr.rectangle([PAD - 1, yy - 1, PAD + a.width, yy + a.height], outline=(90, 90, 90))
        dr.rectangle([cw + PAD * 2 - 1, yy - 1, cw + PAD * 2 + b.width, yy + b.height],
                     outline=(90, 90, 90))
        yy += a.height + PAD

    path = os.path.join(OUT, f"{name}_ab.png")
    sheet.save(path)
    return path, stats


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, ref_png, built_png, regions in SHEETS:
        path, stats = build(name, ref_png, built_png, regions)
        print(f"\n=== {name} -> {os.path.relpath(path, REPO)} ===")
        print(f"{'region':24s} {'size':>12s} {'mean |dRGB|':>12s}")
        for label, w, h, d in stats:
            print(f"{label:24s} {f'{w}x{h}':>12s} {d:12.2f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
