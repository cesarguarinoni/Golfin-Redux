#!/usr/bin/env python3
"""Build the da_q4 before/after crop sheet.

One row per fixed element: the pre-fix crop on the left, the shipped crop on the right,
both cut from the SAME screen rect in the SAME play session (DesignAuditRunner `q4` mode),
so a difference in the pair is a difference in the pixels and nothing else. Also prints,
per row, the bounding box of the differing pixels — the acceptance line the spec asks for
("the only differing pixels are the corners/dividers themselves").
"""
import sys, os
from PIL import Image, ImageDraw, ImageChops, ImageFont

ATT = "Docs/Specs/Quick/_attachments"
ROWS = [
    ("shop_divider",        "GeneralShopCard  ·  HDiv rule"),
    ("entry_badge",         "TournamentSelectionCard  ·  entry-fee badge"),
    ("close_button",        "TournamentCloseButton"),
    ("holecomplete_button", "HoleComplete  ·  main button"),
]
PAD, GAP, LABEL_H, SCALE_MIN = 18, 28, 30, 3

def load(tag, side):
    p = os.path.join(ATT, f"da_q4_{side}_{tag}.png")
    return Image.open(p).convert("RGB") if os.path.exists(p) else None

def diff_bbox(a, b):
    if a.size != b.size:
        return None, "size mismatch %s vs %s" % (a.size, b.size)
    d = ImageChops.difference(a, b).convert("L")
    # ignore <=8/255 noise so AA jitter does not widen the box
    d = d.point(lambda v: 255 if v > 8 else 0)
    bb = d.getbbox()
    return bb, ("identical" if bb is None else "%dx%d at (%d,%d)" % (bb[2]-bb[0], bb[3]-bb[1], bb[0], bb[1]))

def main():
    pairs, notes = [], []
    for tag, title in ROWS:
        a, b = load(tag, "before"), load(tag, "after")
        if a is None or b is None:
            notes.append(f"{tag}: MISSING ({'before ' if a is None else ''}{'after' if b is None else ''})")
            continue
        bb, desc = diff_bbox(a, b)
        if bb is None:
            # A byte-identical pair is not evidence of "no change" — SnapPlayModeSafe returns
            # real, byte-identical STALE frames, and it did exactly that for close_button while
            # an isolated render of the same prefab at the same two ppu values differed by
            # 12,519 px. An identical pair means the CAPTURE failed; excluded, never shown.
            notes.append(f"{tag}: EXCLUDED — before/after byte-identical (stale capture, not a no-op)")
            continue
        notes.append(f"{tag}: {a.size[0]}x{a.size[1]}  diff bbox = {desc}")
        pairs.append((tag, title, a, b, bb))

    if not pairs:
        print("\n".join(notes)); print("NO PAIRS — nothing to composite"); return 1

    scale = max(SCALE_MIN, 1)
    w = max(p[2].width + p[3].width for p in pairs) * scale + GAP + PAD * 2
    h = sum(max(p[2].height, p[3].height) * scale + LABEL_H + GAP for p in pairs) + PAD * 2
    sheet = Image.new("RGB", (w, h), (24, 26, 31))
    dr = ImageDraw.Draw(sheet)
    try:    font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial.ttf", 15)
    except Exception: font = ImageFont.load_default()

    y = PAD
    for tag, title, a, b, bb in pairs:
        dr.text((PAD, y), f"{title}      BEFORE (ppu as shipped pre-fix)  |  AFTER", fill=(226, 232, 240), font=font)
        y += LABEL_H
        A = a.resize((a.width*scale, a.height*scale), Image.NEAREST)
        B = b.resize((b.width*scale, b.height*scale), Image.NEAREST)
        sheet.paste(A, (PAD, y)); sheet.paste(B, (PAD + A.width + GAP, y))
        dr.rectangle([PAD-1, y-1, PAD+A.width, y+A.height], outline=(90, 96, 108))
        dr.rectangle([PAD+A.width+GAP-1, y-1, PAD+A.width+GAP+B.width, y+B.height], outline=(90, 96, 108))
        y += max(A.height, B.height) + GAP

    out = os.path.join(ATT, "da_q4_corner_crop_sheet.png")
    sheet.save(out)
    print("\n".join(notes))
    print("wrote", out, sheet.size)
    return 0

if __name__ == "__main__":
    sys.exit(main())
