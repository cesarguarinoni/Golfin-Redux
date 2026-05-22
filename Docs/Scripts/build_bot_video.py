#!/usr/bin/env python3
"""
build_bot_video.py — assemble a captioned demo video from a Loop v2 smoke-bot run.

Reusable, data-driven replacement for the removed in-engine BotVideoRecorder
(see Docs/Architecture/BOT_FRAMEWORK.md §8). The Unity side only dumps raw PNG
frames (BotFrameRecorder.cs); ALL encoding and captioning happens here, in ffmpeg.

Inputs (produced by a bot run with video recording armed):
  - frames    : Docs/Diagnostics/_capture/botframe_NNNNN_*.png
  - manifest  : tasks/loop_v2_smoke_bot/<scenario>/video/frames_manifest.csv
                (frame index -> Time.realtimeSinceStartup)
  - log       : tasks/loop_v2_smoke_bot/<scenario>/screenshots/history.log
                ([t=NN.NN] step lines — SAME clock as the manifest, so captions
                 sync exactly)

Output:
  - Docs/Videos/<scenario>_<suffix>.mp4   (H.264, real-time, burned-in captions)

Captions are auto-derived from the log's `Click: '<name>'` lines, plus a title
card. Edit CAPTION logic below to recaption — no Unity rebuild needed.

Usage:
  python3 Docs/Scripts/build_bot_video.py --scenario settings_round_trip
  python3 Docs/Scripts/build_bot_video.py --scenario hole1_play_next \\
      --title "Loop v2 Stage F — Button Press Feedback" --keep-frames
"""
import argparse
import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CAPTURE_DIR = os.path.join(REPO, "Docs/Diagnostics/_capture")
FRAME_GLOB = "botframe_*.png"

FONT_CANDIDATES = [
    "/System/Library/Fonts/Helvetica.ttc",
    "/System/Library/Fonts/Supplemental/Arial.ttf",
    "/System/Library/Fonts/SFNS.ttf",
    "/Library/Fonts/Arial.ttf",
]


def find_bin(name):
    """Locate a binary on PATH or in ~/.local/bin."""
    p = shutil.which(name)
    if p:
        return p
    local = os.path.expanduser(f"~/.local/bin/{name}")
    if os.path.exists(local):
        return local
    sys.exit(f"ERROR: '{name}' not found (PATH or ~/.local/bin). Install it first.")


def find_font():
    for f in FONT_CANDIDATES:
        if os.path.exists(f):
            return f
    sys.exit("ERROR: no usable system font found. Add one to FONT_CANDIDATES.")


def read_manifest(path):
    """Return list of (frame_index, realtime) from frames_manifest.csv."""
    if not os.path.exists(path):
        sys.exit(f"ERROR: manifest not found: {path}\n"
                 f"Run the bot scenario with video recording armed first.")
    rows = []
    with open(path) as fh:
        next(fh, None)  # header
        for line in fh:
            line = line.strip()
            if not line:
                continue
            idx, rt = line.split(",")
            rows.append((int(idx), float(rt)))
    if not rows:
        sys.exit(f"ERROR: manifest is empty: {path}")
    return rows


def parse_captions(log_path, rec_start, rec_end):
    """
    Parse history.log -> [(start, end, text)] in video-relative seconds.
    Caption every `Click: '<name>'`; each shows until the next click (capped).
    """
    events = []
    if not os.path.exists(log_path):
        print(f"WARN: history.log not found ({log_path}) — captions will be title only.")
        return events
    click_re = re.compile(r"\[t=([\d.]+)\]\s+Click:\s+'([^']+)'")
    raw = []
    with open(log_path) as fh:
        for line in fh:
            m = click_re.search(line)
            if m:
                raw.append((float(m.group(1)), m.group(2)))
    for i, (t, name) in enumerate(raw):
        start = t - rec_start
        nxt = (raw[i + 1][0] - rec_start) if i + 1 < len(raw) else (rec_end - rec_start)
        end = min(start + 2.6, nxt)
        if end <= start:
            end = start + 1.2
        if start < 0:
            start = 0.0
        # Plain ASCII marker — fancy arrow glyphs tofu in most system fonts.
        events.append((start, end, f"Tap   {name}"))
    return events


def esc_path(p):
    """Escape a path for an ffmpeg filter option value."""
    return p.replace("\\", "\\\\").replace(":", "\\:").replace("'", "\\'")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scenario", required=True, help="smoke-bot scenario key")
    ap.add_argument("--title", default="Loop v2 — Stage F\nButton Press Feedback")
    ap.add_argument("--suffix", default="stageF_buttons", help="output filename suffix")
    ap.add_argument("--fps", type=int, default=30, help="output framerate")
    ap.add_argument("--keep-frames", action="store_true", help="do not delete PNG frames after")
    args = ap.parse_args()

    ffmpeg = find_bin("ffmpeg")
    ffprobe = find_bin("ffprobe")
    font = find_font()

    scen_dir = os.path.join(REPO, "tasks/loop_v2_smoke_bot", args.scenario)
    manifest = read_manifest(os.path.join(scen_dir, "video/frames_manifest.csv"))
    log_path = os.path.join(scen_dir, "screenshots/history.log")

    frames = sorted(glob.glob(os.path.join(CAPTURE_DIR, FRAME_GLOB)))
    if not frames:
        sys.exit(f"ERROR: no frames matching {FRAME_GLOB} in {CAPTURE_DIR}")
    n = min(len(frames), len(manifest))
    if len(frames) != len(manifest):
        print(f"WARN: {len(frames)} frames vs {len(manifest)} manifest rows — using {n}.")
    frames = frames[:n]
    times = [manifest[i][1] for i in range(n)]
    rec_start, rec_end = times[0], times[-1]
    duration = rec_end - rec_start
    print(f"Frames: {n}  |  span: {duration:.1f}s real-time")

    captions = parse_captions(log_path, rec_start, rec_end)
    captions.insert(0, (0.0, 3.6, args.title))
    print(f"Captions: {len(captions)}")

    # Probe first frame size; ensure even dimensions for yuv420p.
    dims = subprocess.check_output(
        [ffprobe, "-v", "error", "-select_streams", "v:0",
         "-show_entries", "stream=width,height", "-of", "csv=p=0", frames[0]]
    ).decode().strip().split(",")
    w, h = int(dims[0]), int(dims[1])
    fontsize = max(22, h // 32)
    title_fontsize = max(28, h // 40)  # smaller — title is 2 lines, must not clip width

    tmp = tempfile.mkdtemp(prefix="botvid_")
    try:
        # 1. concat list with per-frame real-time durations.
        concat_path = os.path.join(tmp, "concat.txt")
        with open(concat_path, "w") as fh:
            fh.write("ffconcat version 1.0\n")
            for i in range(n):
                dur = (times[i + 1] - times[i]) if i + 1 < n else (1.0 / args.fps)
                dur = max(dur, 0.001)
                fh.write(f"file '{frames[i]}'\n")
                fh.write(f"duration {dur:.4f}\n")
            fh.write(f"file '{frames[-1]}'\n")  # concat-demuxer last-frame quirk

        # 2. drawtext filter per caption (textfile= avoids all text escaping).
        draw = [f"scale=trunc(iw/2)*2:trunc(ih/2)*2"]
        for i, (start, end, text) in enumerate(captions):
            cap_file = os.path.join(tmp, f"cap_{i}.txt")
            with open(cap_file, "w", encoding="utf-8") as fh:
                fh.write(text)
            is_title = (i == 0)
            fs = title_fontsize if is_title else fontsize
            y = "(h-text_h)/2" if is_title else f"h-text_h-{max(80, h // 12)}"
            draw.append(
                f"drawtext=textfile='{esc_path(cap_file)}'"
                f":fontfile='{esc_path(font)}'"
                f":fontsize={fs}:fontcolor=white"
                f":box=1:boxcolor=black@0.62:boxborderw={max(10, fs // 3)}"
                f":x=(w-text_w)/2:y={y}"
                f":enable='between(t,{start:.3f},{end:.3f})'"
            )
        vf = ",".join(draw)

        # 3. encode.
        out_dir = os.path.join(REPO, "Docs/Videos")
        os.makedirs(out_dir, exist_ok=True)
        out_path = os.path.join(out_dir, f"{args.scenario}_{args.suffix}.mp4")
        cmd = [
            ffmpeg, "-y",
            "-f", "concat", "-safe", "0", "-i", concat_path,
            "-vf", vf,
            "-r", str(args.fps),
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "medium", "-crf", "23",
            "-movflags", "+faststart",
            out_path,
        ]
        print(f"Encoding -> {out_path}")
        subprocess.run(cmd, check=True)
        size_mb = os.path.getsize(out_path) / 1e6
        print(f"DONE: {out_path}  ({size_mb:.1f} MB, {w}x{h}, {duration:.1f}s)")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    # 4. clean up frames unless asked to keep.
    if not args.keep_frames:
        for f in frames:
            try:
                os.remove(f)
            except OSError:
                pass
        print(f"Cleaned up {len(frames)} PNG frames from {CAPTURE_DIR}")


if __name__ == "__main__":
    main()
