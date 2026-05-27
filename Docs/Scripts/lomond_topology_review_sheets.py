#!/usr/bin/env python3
'''
Regenerate per-hole manifest review sheets from the Lomond topology YAML.

For each hole, produces a side-by-side composite:
  LEFT  — source PDF panel (cropped from A4_ホール攻略冊子.pdf, page N+1)
  RIGHT — manifest interpretation: regions as colored rects, slope vectors as
          arrows, ridges as dashed polylines, pins as flags.

Usage (from repo root):
    python3 Docs/Scripts/lomond_topology_review_sheets.py

Outputs:
    Docs/Reference/manifest_review/hole_NN_review.png  (18 PNGs)
    Docs/Reference/manifest_review/Lomond_Greens_Manifest_Review.pdf  (combined)

Requires:
    pip install pymupdf pyyaml matplotlib pillow
'''
import os, sys, yaml
import fitz
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from matplotlib.patches import Rectangle, Circle
from PIL import Image
import numpy as np

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
YAML_PATH = os.path.join(REPO, 'Docs/Reference/Lomond_Green_Topology.yaml')
PDF_PATH = os.path.join(REPO, 'Docs/Specs/Queued/green_topology_and_pin_authoring/A4_ホール攻略冊子.pdf')
OUT_DIR = os.path.join(REPO, 'Docs/Reference/manifest_review')
os.makedirs(OUT_DIR, exist_ok=True)

with open(YAML_PATH) as f:
    manifest = yaml.safe_load(f)

# Render + crop GREEN攻略法 panels from the PDF
print('Rendering PDF panel crops...')
doc = fitz.open(PDF_PATH)
panel_crops = {}
for hole in range(1, 19):
    pix = doc[hole].get_pixmap(dpi=300)
    img = Image.frombytes('RGB', (pix.width, pix.height), pix.samples)
    W, H = img.size
    panel_crops[hole] = img.crop((int(W*0.46), int(H*0.62), int(W*0.83), int(H*0.97)))

region_colors = ['#ffaa66', '#aaaaff', '#aaffaa', '#ffaaaa', '#ffffaa']

print('Generating per-hole review sheets...')
sheet_imgs = []
for hole_num, hole_data in manifest['holes'].items():
    fig, axes = plt.subplots(1, 2, figsize=(18, 9))
    axes[0].imshow(panel_crops[hole_num])
    axes[0].set_title(f'HOLE {hole_num} — Source PDF Panel', fontsize=14, weight='bold')
    axes[0].axis('off')

    ax = axes[1]
    green_bg = patches.Ellipse((0.5, 0.5), 0.96, 0.96, facecolor='#5fa05f',
                                edgecolor='#2a6a2a', linewidth=3, zorder=1)
    ax.add_patch(green_bg)

    for i, r in enumerate(hole_data['regions']):
        x0, y0, x1, y1 = r['boundsFrac']
        rect = Rectangle((x0, y0), x1-x0, y1-y0,
                         linewidth=1.5, edgecolor=region_colors[i % len(region_colors)],
                         facecolor=region_colors[i % len(region_colors)], alpha=0.25, zorder=2)
        ax.add_patch(rect)
        ax.text(x0 + 0.01, y1 - 0.025, f"{r['name']}\n{r['magnitudePct']}%",
                fontsize=7, color='#333', zorder=4, verticalalignment='top', alpha=0.85)
        sd = r['slopeDir']
        arrow_len = 0.05 + 0.015 * r['magnitudePct']
        cx_range = np.linspace(x0 + (x1-x0)*0.2, x0 + (x1-x0)*0.8, 3)
        cy_range = np.linspace(y0 + (y1-y0)*0.2, y0 + (y1-y0)*0.8, 3)
        for cx in cx_range:
            for cy in cy_range:
                ax.annotate('', xy=(cx + sd[0]*arrow_len, cy + sd[1]*arrow_len),
                            xytext=(cx, cy),
                            arrowprops=dict(arrowstyle='->', color='black', lw=1.5), zorder=3)

    for ridge in hole_data.get('ridges', []):
        pts = ridge['polylineFrac']
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        ax.plot(xs, ys, 'w--', linewidth=4, zorder=5, alpha=0.95)
        ax.plot(xs, ys, 'k--', linewidth=1.5, zorder=6, alpha=0.7)
        ax.text(xs[len(xs)//2], ys[len(ys)//2] + 0.04,
                f"{ridge['name']}\nband={ridge['transitionBandMeters']}m, {ridge['transitionMagnitudePct']}%",
                fontsize=7, ha='center', color='white',
                bbox=dict(boxstyle='round,pad=0.3', facecolor='#444', alpha=0.85), zorder=7)

    for p in hole_data['pins']:
        px, py = p['posFrac']
        flag_color = '#ff3333' if p.get('isDefault') else '#ff8833'
        circle = Circle((px, py), 0.018, facecolor=flag_color, edgecolor='black',
                       linewidth=1, zorder=8)
        ax.add_patch(circle)
        ax.text(px + 0.025, py, p['label'], fontsize=6.5, color='#222',
                verticalalignment='center',
                bbox=dict(boxstyle='round,pad=0.15', facecolor='white', alpha=0.7,
                         edgecolor='none'), zorder=9)

    ax.set_xlim(-0.02, 1.02); ax.set_ylim(-0.02, 1.02); ax.set_aspect('equal')
    ax.set_xticks([0, 0.25, 0.5, 0.75, 1.0]); ax.set_yticks([0, 0.25, 0.5, 0.75, 1.0])
    ax.set_xticklabels(['L (0)', '', 'mid', '', 'R (1)'], fontsize=8)
    ax.set_yticklabels(['front (0)', '', 'mid', '', 'back (1)'], fontsize=8)
    ax.grid(alpha=0.2)
    dims = hole_data['pdfDimensionsMeters']
    ax.set_title(f"HOLE {hole_num} — My Interpretation  [{dims[0]}×{dims[1]}m, confidence={hole_data['confidence']}]",
                 fontsize=14, weight='bold')
    fig.text(0.5, 0.02,
             f"Feature: {hole_data['feature']}    |    JP: {hole_data.get('jpNote', '')}    |    EN: {hole_data.get('enNote', '')}",
             ha='center', fontsize=10, style='italic', wrap=True)
    plt.tight_layout(rect=[0, 0.05, 1, 1])
    out_png = os.path.join(OUT_DIR, f'hole_{hole_num:02d}_review.png')
    plt.savefig(out_png, dpi=110, bbox_inches='tight')
    plt.close(fig)
    sheet_imgs.append(Image.open(out_png).convert('RGB'))
    print(f'  ✓ hole {hole_num}')

combined_pdf = os.path.join(OUT_DIR, 'Lomond_Greens_Manifest_Review.pdf')
sheet_imgs[0].save(combined_pdf, save_all=True, append_images=sheet_imgs[1:], resolution=110)
print(f'\nDone. PDF: {combined_pdf}')
