"""club_art_batches post-processing — verbatim from the KLYRO pilot (2026-08-19).

Usage: import the functions, or edit __main__ per batch. Requires Pillow + numpy.
Raws live in /mnt/user-data/uploads/Downloads/golfin_club_gen/ after device_stage_files.
"""
import numpy as np
from PIL import Image, ImageFilter, ImageDraw
from collections import deque


def remove_white_bg(img, thresh=235):
    """Flood-fill whiteish background from the borders -> alpha 0.
    thresh=235 for clean white; drop to 195 when a soft shadow splits the bg
    (otherwise enclosed white pockets survive as splotches)."""
    im = img.convert("RGB")
    a = np.array(im).astype(int)
    whiteish = a.min(axis=2) >= thresh
    h, w = whiteish.shape
    bg = np.zeros((h, w), bool)
    dq = deque()
    for x in range(w):
        for y in (0, h - 1):
            if whiteish[y, x] and not bg[y, x]:
                bg[y, x] = True; dq.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if whiteish[y, x] and not bg[y, x]:
                bg[y, x] = True; dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and whiteish[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True; dq.append((ny, nx))
    alpha = np.where(bg, 0, 255).astype(np.uint8)
    out = img.convert("RGBA")
    out.putalpha(Image.fromarray(alpha, "L").filter(ImageFilter.GaussianBlur(1.0)))
    return out


def remove_shadow_ghost(img):
    """Kill soft gray drop-shadow remnants: unsaturated (spread<=30), mid-bright
    (min>=125) pixels reachable from already-transparent regions."""
    a = np.array(img)
    rgb = a[:, :, :3].astype(int)
    alpha = a[:, :, 3]
    h, w = alpha.shape
    gray = ((rgb.max(axis=2) - rgb.min(axis=2)) <= 30) & (rgb.min(axis=2) >= 125)
    kill = np.zeros((h, w), bool)
    dq = deque()
    ys, xs = np.where(alpha < 40)
    for y, x in zip(ys, xs):
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and gray[ny, nx] and not kill[ny, nx]:
                kill[ny, nx] = True; dq.append((ny, nx))
    while dq:
        y, x = dq.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and gray[ny, nx] and not kill[ny, nx]:
                kill[ny, nx] = True; dq.append((ny, nx))
    na = alpha.copy()
    na[kill] = 0
    sm = Image.fromarray(na, "L").filter(ImageFilter.GaussianBlur(1.2))
    return Image.fromarray(np.dstack([a[:, :, :3], np.array(sm)]), "RGBA")


def fit_canvas(img, tw, th, margin=0.02):
    """Crop to content, scale into (tw,th) with margin, center on transparency."""
    img = img.crop(img.getbbox())
    s = min(tw * (1 - margin * 2) / img.width, th * (1 - margin * 2) / img.height)
    img = img.resize((int(img.width * s), int(img.height * s)), Image.LANCZOS)
    c = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    c.paste(img, ((tw - img.width) // 2, (th - img.height) // 2), img)
    return c


def full_scene(src_path, out_path):
    """Full art: NO bg removal (the scene is the art). Resize + rounded corners."""
    im = Image.open(src_path).convert("RGBA").resize((537, 900), Image.LANCZOS)
    mask = Image.new("L", (537, 900), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, 536, 899], radius=30, fill=255)
    im.putalpha(mask)
    im.save(out_path)


def portrait(src_path, out_path, thresh=235):
    fit_canvas(remove_white_bg(Image.open(src_path), thresh), 264, 411).save(out_path)


def controls(src_path, out_path, thresh=235, deshadow=False):
    cut = remove_white_bg(Image.open(src_path), thresh)
    if deshadow:
        cut = remove_shadow_ghost(cut)
    cut.resize((1156, 649), Image.LANCZOS).save(out_path)
