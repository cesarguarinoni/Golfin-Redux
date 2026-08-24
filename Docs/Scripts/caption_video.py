#!/usr/bin/env python3
"""
caption_video.py — burn a caption track into any recorded MP4.

The sibling build_bot_video.py does this for Loop-v2 smoke-bot runs, deriving captions
from the bot's history.log. This one is the generic version: it takes an explicit
caption sidecar, so ANY recorder (a demo recorder, a flyover, a UI clip) can emit
[{start, end, text}] and get the same burned-in caption treatment.

It reuses build_bot_video.py's `textfile=` drawtext idiom deliberately — writing each
caption to its own temp file sidesteps every ffmpeg text-escaping trap (colons, quotes,
commas, apostrophes) and is the reason inline drawtext is banned in this repo.

Inputs:
  --video     the raw recording (any resolution; portrait is the common case here)
  --captions  JSON array of {"start": <s>, "end": <s>, "text": "..."} — \n allowed
  --out       output path

The FIRST caption is treated as a title card: larger, vertically centred. The rest sit
near the bottom. Text is wrapped to --wrap characters because drawtext centres but never
wraps, so an over-long caption runs off BOTH edges of a portrait frame.

Usage:
  python3 Docs/Scripts/caption_video.py \
      --video  Docs/Diagnostics/_capture/langswitch/raw.mp4 \
      --captions Docs/Diagnostics/_capture/langswitch/captions.json \
      --out    Docs/Reports/Media/language_switch_repaint_2026-08-24.mp4
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import textwrap

# Arial Unicode first: the captions carry Japanese (日本語), and Helvetica renders CJK as
# tofu. Ordered most- to least-complete coverage.
FONT_CANDIDATES = [
    "/Library/Fonts/Arial Unicode.ttf",
    "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
    "/System/Library/Fonts/Hiragino Sans GB.ttc",
    "/System/Library/Fonts/Helvetica.ttc",
]


def pick_font() -> str:
    for f in FONT_CANDIDATES:
        if os.path.exists(f):
            return f
    sys.exit("No usable font found — add one to FONT_CANDIDATES.")


def esc(path: str) -> str:
    """Escape a path for use inside an ffmpeg filter argument."""
    return path.replace("\\", "\\\\").replace(":", "\\:").replace("'", "\\'")


def probe(video: str):
    out = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", "v:0",
         "-show_entries", "stream=width,height", "-show_entries", "format=duration",
         "-of", "json", video],
        capture_output=True, text=True, check=True).stdout
    d = json.loads(out)
    st = d["streams"][0]
    return int(st["width"]), int(st["height"]), float(d["format"]["duration"])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--video", required=True)
    ap.add_argument("--captions", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--wrap", type=int, default=34, help="wrap width in characters")
    ap.add_argument("--fontsize", type=int, default=0, help="0 = derive from height")
    ap.add_argument("--crf", type=int, default=23)
    args = ap.parse_args()

    video = os.path.abspath(args.video)
    w, h, duration = probe(video)
    with open(args.captions, encoding="utf-8") as fh:
        caps = json.load(fh)
    if not caps:
        sys.exit("Caption file is empty.")

    font = pick_font()
    fs = args.fontsize or max(22, h // 52)
    title_fs = max(fs, h // 42)
    bottom = max(80, h // 9)

    tmp = tempfile.mkdtemp(prefix="capvid_")
    try:
        draw = ["scale=trunc(iw/2)*2:trunc(ih/2)*2"]
        for i, c in enumerate(caps):
            lines = []
            for para in str(c["text"]).split("\n"):
                lines.extend(textwrap.wrap(para, args.wrap) or [""])
            text = "\n".join(lines)

            cap_file = os.path.join(tmp, f"cap_{i}.txt")
            with open(cap_file, "w", encoding="utf-8") as fh:
                fh.write(text)

            is_title = (i == 0)
            size = title_fs if is_title else fs
            y = "(h-text_h)/2" if is_title else f"h-text_h-{bottom}"
            draw.append(
                f"drawtext=textfile='{esc(cap_file)}'"
                f":fontfile='{esc(font)}'"
                f":fontsize={size}:fontcolor=white:line_spacing=8"
                f":box=1:boxcolor=black@0.62:boxborderw={max(10, size // 3)}"
                f":x=(w-text_w)/2:y={y}"
                f":enable='between(t,{float(c['start']):.3f},{float(c['end']):.3f})'"
            )

        out_path = os.path.abspath(args.out)
        os.makedirs(os.path.dirname(out_path), exist_ok=True)
        cmd = ["ffmpeg", "-y", "-i", video, "-vf", ",".join(draw),
               "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "medium",
               "-crf", str(args.crf), "-movflags", "+faststart", "-an", out_path]
        print(f"Font: {font}\nCaptions: {len(caps)}  fontsize={fs} title={title_fs} wrap={args.wrap}")
        print(f"Encoding -> {out_path}")
        subprocess.run(cmd, check=True)
        mb = os.path.getsize(out_path) / 1e6
        print(f"DONE: {out_path}  ({mb:.1f} MB, {w}x{h}, {duration:.1f}s)")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
