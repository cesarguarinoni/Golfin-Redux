#!/usr/bin/env python3
"""Compress all .png files in a folder to max 800px wide, save to _compressed subfolder.

Usage: python Docs/compress_screenshots.py <folder>
Requires: pip install Pillow
"""
import os
import sys
from PIL import Image

if len(sys.argv) < 2:
    sys.exit("Usage: python compress_screenshots.py <folder>")

folder = sys.argv[1]
out = os.path.join(folder, "_compressed")
os.makedirs(out, exist_ok=True)

for f in os.listdir(folder):
    if f.lower().endswith(".png") and not f.endswith(".meta"):
        img = Image.open(os.path.join(folder, f))
        ratio = 800 / max(img.size)
        if ratio < 1:
            img = img.resize(
                (int(img.size[0] * ratio), int(img.size[1] * ratio)),
                Image.LANCZOS,
            )
        img.save(os.path.join(out, f), optimize=True)
        print(f"Compressed {f}")
