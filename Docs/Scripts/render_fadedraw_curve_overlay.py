#!/usr/bin/env python3
"""
render_fadedraw_curve_overlay.py
Order 356 — fade_draw_core_wiring iter-4

Reads trajectory_points.json (written by FadeDrawSampleTrail in Scenarios.cs)
and renders a single 1400x1400 PNG showing all three flight paths in a shared
top-down reference frame:
  - cyan  = DRAW (FadeDraw armed, handle LEFT, fadeDrawInput=-1)
  - yellow = FADE (FadeDraw armed, handle RIGHT, fadeDrawInput=+1)
  - white  = STRAIGHT (FadeDraw OFF, handle LEFT, aim shift only)

Axes:
  - Horizontal = lateral (Z world), increasing rightward
  - Vertical   = downrange (X world), increasing upward
  - Origin      = tee position (0,0) from teeX/teeZ in JSON

Output: screenshots/curve_overlay_real_hole.png  (1400x1400, >=900px floor)
"""

import json
import os
import sys
import math
from PIL import Image, ImageDraw, ImageFont

TASK_DIR = os.path.join(os.path.dirname(__file__), "..", "Specs", "Active",
                        "fade_draw_core_wiring")
JSON_PATH   = os.path.join(TASK_DIR, "trajectory_points.json")
OUTPUT_PATH = os.path.join(TASK_DIR, "screenshots", "curve_overlay_real_hole.png")

IMG_SIZE  = 1400
MARGIN    = 100   # px border around the plot area
PLOT_SIZE = IMG_SIZE - 2 * MARGIN

# Colors: (R,G,B,A) — label → (line_color, label_color)
SHOT_STYLE = {
    "draw":     ((0,   220, 255, 255), "DRAW  (FadeDraw LEFT  fadeDrawInput=-1.0)", (0,   220, 255)),
    "fade":     ((255, 215,   0, 255), "FADE  (FadeDraw RIGHT fadeDrawInput=+1.0)", (255, 215,   0)),
    "straight": ((255, 255, 255, 255), "STRAIGHT (FadeDraw OFF  aim shift only)",   (255, 255, 255)),
}

BG_COLOR     = (20,  20,  30)    # dark navy background
GRID_COLOR   = (50,  50,  60)    # subtle grid
AXIS_COLOR   = (120, 120, 140)   # axis lines
TEE_COLOR    = (255, 100, 100)   # tee origin marker


def load_json(path):
    with open(path, "r") as f:
        return json.load(f)


def compute_world_bounds(shots):
    """Return (min_z, max_z, min_x, max_x) across all shots (world relative to tee)."""
    all_z = []
    all_x = []
    for shot in shots:
        for (rx, rz) in shot["points"]:
            all_x.append(rx)
            all_z.append(rz)
    if not all_x:
        return (-50, 50, 0, 150)
    pad_z = max(10, (max(all_z) - min(all_z)) * 0.15)
    pad_x = max(10, (max(all_x) - min(all_x)) * 0.15)
    return (
        min(all_z) - pad_z, max(all_z) + pad_z,
        min(0, min(all_x)) - pad_x, max(all_x) + pad_x
    )


def world_to_px(rx, rz, bounds, plot_origin, plot_size):
    """Map world (rx=downrange, rz=lateral) to pixel coords.
    Downrange (X) goes UP in the image. Lateral (Z) goes RIGHT.
    bounds: (min_z, max_z, min_x, max_x)
    """
    min_z, max_z, min_x, max_x = bounds
    span_z = max_z - min_z or 1
    span_x = max_x - min_x or 1

    # Normalised [0..1]: z goes right, x goes up (so invert y)
    norm_z = (rz - min_z) / span_z
    norm_x = (rx - min_x) / span_x

    px_x = plot_origin[0] + norm_z * plot_size
    px_y = plot_origin[1] + (1.0 - norm_x) * plot_size
    return (int(round(px_x)), int(round(px_y)))


def draw_grid(draw, bounds, plot_origin, plot_size, step=20):
    """Draw subtle grid lines every `step` world units."""
    min_z, max_z, min_x, max_x = bounds

    # Vertical grid lines (z-axis ticks)
    z_start = math.ceil(min_z / step) * step
    z = z_start
    while z <= max_z:
        px1 = world_to_px(min_x, z, bounds, plot_origin, plot_size)
        px2 = world_to_px(max_x, z, bounds, plot_origin, plot_size)
        draw.line([px1, px2], fill=GRID_COLOR, width=1)
        z += step

    # Horizontal grid lines (x-axis ticks)
    x_start = math.ceil(min_x / step) * step
    x = x_start
    while x <= max_x:
        px1 = world_to_px(x, min_z, bounds, plot_origin, plot_size)
        px2 = world_to_px(x, max_z, bounds, plot_origin, plot_size)
        draw.line([px1, px2], fill=GRID_COLOR, width=1)
        x += step


def draw_axes(draw, bounds, plot_origin, plot_size):
    """Draw the Z=0 and X=0 axes."""
    min_z, max_z, min_x, max_x = bounds
    # Z=0 axis (lateral zero = tee lateral position)
    p1 = world_to_px(min_x, 0, bounds, plot_origin, plot_size)
    p2 = world_to_px(max_x, 0, bounds, plot_origin, plot_size)
    draw.line([p1, p2], fill=AXIS_COLOR, width=2)

    # X=0 axis (tee downrange position = 0 = launch point)
    p1 = world_to_px(0, min_z, bounds, plot_origin, plot_size)
    p2 = world_to_px(0, max_z, bounds, plot_origin, plot_size)
    draw.line([p1, p2], fill=AXIS_COLOR, width=2)


def draw_polyline(draw, points, bounds, plot_origin, plot_size, color, width=6):
    """Draw a polyline in pixel space from world-relative (rx, rz) list."""
    if len(points) < 2:
        return
    px_pts = [world_to_px(rx, rz, bounds, plot_origin, plot_size) for (rx, rz) in points]
    for i in range(len(px_pts) - 1):
        draw.line([px_pts[i], px_pts[i+1]], fill=color, width=width)
    # Draw arrowhead at last point
    if len(px_pts) >= 2:
        tip = px_pts[-1]
        draw.ellipse([tip[0]-8, tip[1]-8, tip[0]+8, tip[1]+8], fill=color)


def draw_legend(draw, img_size, font_small):
    """Draw legend in bottom-left corner."""
    x0, y0 = MARGIN, img_size - MARGIN + 10
    for i, (key, (line_rgba, desc, rgb)) in enumerate(SHOT_STYLE.items()):
        yl = y0 + i * 28
        # Color swatch
        draw.rectangle([x0, yl, x0 + 20, yl + 18], fill=rgb)
        # Label
        draw.text((x0 + 28, yl), desc, fill=(220, 220, 220), font=font_small)


def render_overlay(json_path, output_path):
    shots = load_json(json_path)
    if not shots:
        print(f"ERROR: no shots in {json_path}")
        sys.exit(1)

    # Build a dict by label for easy access
    shot_map = {s["label"]: s for s in shots}
    print(f"Loaded {len(shots)} shot(s): {[s['label'] for s in shots]}")

    bounds = compute_world_bounds(shots)
    print(f"World bounds: Z=[{bounds[0]:.1f},{bounds[1]:.1f}] X=[{bounds[2]:.1f},{bounds[3]:.1f}]")

    img = Image.new("RGB", (IMG_SIZE, IMG_SIZE), BG_COLOR)
    draw = ImageDraw.Draw(img)

    plot_origin = (MARGIN, MARGIN)

    # Grid + axes
    draw_grid(draw, bounds, plot_origin, PLOT_SIZE, step=20)
    draw_axes(draw, bounds, plot_origin, PLOT_SIZE)

    # Draw each shot's polyline
    for key, (line_rgba, desc, rgb) in SHOT_STYLE.items():
        if key not in shot_map:
            print(f"  WARNING: no data for shot '{key}' — skipping")
            continue
        pts = shot_map[key]["points"]
        print(f"  {key}: {len(pts)} points")
        draw_polyline(draw, pts, bounds, plot_origin, PLOT_SIZE, line_rgba, width=7)

    # Tee origin marker
    tee_px = world_to_px(0, 0, bounds, plot_origin, PLOT_SIZE)
    r = 12
    draw.ellipse([tee_px[0]-r, tee_px[1]-r, tee_px[0]+r, tee_px[1]+r],
                 fill=TEE_COLOR, outline=(255, 255, 255), width=2)

    # Try to load a font; fall back to default
    try:
        font_title  = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 36)
        font_medium = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 28)
        font_small  = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 22)
    except Exception:
        font_title  = ImageFont.load_default()
        font_medium = font_title
        font_small  = font_title

    # Title
    title = "FadeDraw Curve Overlay — Real Hole 6 Run (iter-5)"
    draw.text((MARGIN, 18), title, fill=(220, 220, 220), font=font_title)
    subtitle = "Top-down plan view. Origin = tee. Horizontal = lateral (Z), Vertical = downrange (X)"
    draw.text((MARGIN, 58), subtitle, fill=(160, 160, 180), font=font_small)

    # Axis labels
    # X axis label (downrange, vertical)
    draw.text((8, MARGIN + PLOT_SIZE // 2 - 60), "DOWNRANGE\n(X world, m)", fill=AXIS_COLOR, font=font_small)
    # Z axis label (lateral, horizontal)
    mid_z_px = world_to_px(bounds[2], (bounds[0]+bounds[1])/2, bounds, plot_origin, PLOT_SIZE)
    draw.text((MARGIN + PLOT_SIZE // 2 - 60, IMG_SIZE - MARGIN + 30), "LATERAL (Z world, m)", fill=AXIS_COLOR, font=font_small)

    # Legend
    draw_legend(draw, IMG_SIZE, font_small)

    # Dimension annotation for DRAW vs FADE lateral spread
    if "draw" in shot_map and "fade" in shot_map:
        draw_pts  = shot_map["draw"]["points"]
        fade_pts  = shot_map["fade"]["points"]
        if draw_pts and fade_pts:
            draw_end_z  = draw_pts[-1][1]
            fade_end_z  = fade_pts[-1][1]
            lateral_sep = abs(fade_end_z - draw_end_z)
            annot = f"DRAW-FADE lateral sep at rest: {lateral_sep:.1f}m"
            draw.text((MARGIN + PLOT_SIZE - 420, IMG_SIZE - MARGIN + 30), annot,
                      fill=(200, 200, 100), font=font_medium)

    # Save
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    img.save(output_path, "PNG")
    print(f"Saved: {output_path} ({IMG_SIZE}x{IMG_SIZE})")
    return output_path


if __name__ == "__main__":
    json_in   = sys.argv[1] if len(sys.argv) > 1 else JSON_PATH
    img_out   = sys.argv[2] if len(sys.argv) > 2 else OUTPUT_PATH
    render_overlay(json_in, img_out)
