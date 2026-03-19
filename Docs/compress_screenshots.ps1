# Compresses all .png files in a folder to max 800px wide
# Saves to a _compressed subfolder
# Usage: powershell -File Docs/compress_screenshots.ps1 "Assets/Screenshots"
# Also run on references: "Assets/References/Roster Screen", "Assets/References/Inventory"
# Requires: pip install Pillow

param([string]$folder)

python -c @"
import os, sys
from PIL import Image

folder = sys.argv[1]
out = os.path.join(folder, '_compressed')
os.makedirs(out, exist_ok=True)

for f in os.listdir(folder):
    if f.lower().endswith('.png') and not f.endswith('.meta'):
        img = Image.open(os.path.join(folder, f))
        ratio = 800 / max(img.size)
        if ratio < 1:
            img = img.resize((int(img.size[0]*ratio), int(img.size[1]*ratio)), Image.LANCZOS)
        img.save(os.path.join(out, f), optimize=True)
        print(f'Compressed {f}')
"@ $folder
