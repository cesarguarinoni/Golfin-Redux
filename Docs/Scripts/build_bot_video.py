#!/usr/bin/env python3
"""
build_bot_video.py — burn captions into a Loop v2 smoke-bot demo recording.

The Unity Recorder (driven by BotVideoRecorder.cs) captures a smooth, full-framerate
MP4 of a bot run. This script is the caption pass: it reads that recording + the bot's
history.log and burns in ffmpeg drawtext captions, producing the final demo video.

Inputs (produced by a bot run with video recording armed — BotVideoRecorder.RecordVideo):
  - raw video : tasks/loop_v2_smoke_bot/<scenario>/video/raw.mp4
  - sidecar   : tasks/loop_v2_smoke_bot/<scenario>/video/record_info.json
                ({record_start_realtime, mp4, fps}) — record_start_realtime is the
                bot-clock Time.realtimeSinceStartup at record start, the SAME clock
                BotDriver.LogStep stamps history.log with, so captions sync exactly.
  - log       : tasks/loop_v2_smoke_bot/<scenario>/screenshots/history.log

Output:
  - Docs/Videos/<scenario>_<suffix>.mp4   (H.264, burned-in captions)

Captions are auto-derived from the log's `Click: '<name>'` lines plus a title card.
Edit parse_captions() to recaption — no Unity rebuild, no re-record needed.

Usage:
  python3 Docs/Scripts/build_bot_video.py --scenario settings_round_trip
  python3 Docs/Scripts/build_bot_video.py --scenario hole1_play_next --keep-raw
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# The Unity Recorder takes a short spin-up between StartRecording() (when
# record_start_realtime is stamped) and its first actually-recorded frame.
# Measured ~0.4 s; this shifts captions earlier to compensate. Tune if needed.
RECORDER_LEAD = 0.40

FONT_CANDIDATES = [
    "/System/Library/Fonts/Helvetica.ttc",
    "/System/Library/Fonts/Supplemental/Arial.ttf",
    "/System/Library/Fonts/SFNS.ttf",
    "/Library/Fonts/Arial.ttf",
]


def find_bin(name):
    """Locate a binary on PATH or in ~/.local/bin."""
    p = shutil.which(name) or os.path.expanduser(f"~/.local/bin/{name}")
    if not (p and os.path.exists(p)):
        sys.exit(f"ERROR: '{name}' not found (PATH or ~/.local/bin). Install it first.")
    return p


def find_font():
    for f in FONT_CANDIDATES:
        if os.path.exists(f):
            return f
    sys.exit("ERROR: no usable system font found. Add one to FONT_CANDIDATES.")


def parse_captions(log_path, rec_start, rec_end):
    """
    history.log -> [(start, end, text)] in video-relative seconds.
    Caption every `Click: '<name>'`; each shows until the next click (capped ~2.6s).
    """
    events = []
    if not os.path.exists(log_path):
        print(f"WARN: history.log not found ({log_path}) — title caption only.")
        return events
    click_re = re.compile(r"\[t=([\d.]+)\]\s+Click:\s+'([^']+)'")
    raw = []
    with open(log_path) as fh:
        for line in fh:
            m = click_re.search(line)
            if m:
                raw.append((float(m.group(1)), m.group(2)))
    span = rec_end - rec_start
    for i, (t, name) in enumerate(raw):
        start = t - rec_start
        nxt = (raw[i + 1][0] - rec_start) if i + 1 < len(raw) else span
        end = min(start + 2.6, nxt)
        if end <= start:
            end = start + 1.2
        start = max(start, 0.0)
        if start >= span:
            continue
        # Plain ASCII marker — fancy arrow glyphs tofu in most system fonts.
        events.append((start, min(end, span), f"Tap   {name}"))
    return events


def esc(p):
    """Escape a path for an ffmpeg filter option value."""
    return p.replace("\\", "\\\\").replace(":", "\\:").replace("'", "\\'")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scenario", required=True, help="smoke-bot scenario key")
    ap.add_argument("--title", default="Loop v2 — Stage F\nButton Press Feedback")
    ap.add_argument("--suffix", default="stageF_buttons", help="output filename suffix")
    ap.add_argument("--keep-raw", action="store_true", help="keep the raw Recorder mp4 + sidecar")
    args = ap.parse_args()

    ffmpeg = find_bin("ffmpeg")
    ffprobe = find_bin("ffprobe")
    font = find_font()

    vdir = os.path.join(REPO, "tasks/loop_v2_smoke_bot", args.scenario, "video")
    info_path = os.path.join(vdir, "record_info.json")
    if not os.path.exists(info_path):
        sys.exit(f"ERROR: {info_path} not found.\n"
                 f"Run the scenario with video recording armed "
                 f"(BotVideoRecorder.RecordVideo = true) first.")
    with open(info_path) as fh:
        info = json.load(fh)
    rec_start = float(info["record_start_realtime"]) + RECORDER_LEAD
    raw = info["mp4"]
    if not os.path.isabs(raw):
        raw = os.path.join(REPO, raw)
    if not os.path.exists(raw):
        sys.exit(f"ERROR: raw recording not found: {raw}")

    # Probe the raw video: width, height (stream) then duration (format).
    probe = subprocess.check_output(
        [ffprobe, "-v", "error", "-select_streams", "v:0",
         "-show_entries", "stream=width,height:format=duration",
         "-of", "default=nw=1:nk=1", raw]
    ).decode().split()
    w, h, duration = int(probe[0]), int(probe[1]), float(probe[2])
    print(f"Raw recording: {w}x{h}, {duration:.1f}s")

    log_path = os.path.join(REPO, "tasks/loop_v2_smoke_bot", args.scenario,
                            "screenshots/history.log")
    captions = parse_captions(log_path, rec_start, rec_start + duration)
    captions.insert(0, (0.0, 3.6, args.title))
    print(f"Captions: {len(captions)}")

    fontsize = max(22, h // 32)
    title_fontsize = max(28, h // 40)  # smaller — title is 2 lines, must not clip width

    tmp = tempfile.mkdtemp(prefix="botvid_")
    try:
        # drawtext per caption — textfile= avoids all text-escaping headaches.
        draw = ["scale=trunc(iw/2)*2:trunc(ih/2)*2"]
        for i, (start, end, text) in enumerate(captions):
            cap_file = os.path.join(tmp, f"cap_{i}.txt")
            with open(cap_file, "w", encoding="utf-8") as fh:
                fh.write(text)
            is_title = (i == 0)
            fs = title_fontsize if is_title else fontsize
            y = "(h-text_h)/2" if is_title else f"h-text_h-{max(80, h // 12)}"
            draw.append(
                f"drawtext=textfile='{esc(cap_file)}'"
                f":fontfile='{esc(font)}'"
                f":fontsize={fs}:fontcolor=white"
                f":box=1:boxcolor=black@0.62:boxborderw={max(10, fs // 3)}"
                f":x=(w-text_w)/2:y={y}"
                f":enable='between(t,{start:.3f},{end:.3f})'"
            )
        vf = ",".join(draw)

        out_dir = os.path.join(REPO, "Docs/Videos")
        os.makedirs(out_dir, exist_ok=True)
        out_path = os.path.join(out_dir, f"{args.scenario}_{args.suffix}.mp4")
        cmd = [
            ffmpeg, "-y", "-i", raw,
            "-vf", vf,
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "medium", "-crf", "23",
            "-movflags", "+faststart", "-an",
            out_path,
        ]
        print(f"Encoding -> {out_path}")
        subprocess.run(cmd, check=True)
        size_mb = os.path.getsize(out_path) / 1e6
        print(f"DONE: {out_path}  ({size_mb:.1f} MB, {w}x{h}, {duration:.1f}s)")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    if not args.keep_raw:
        for f in (raw, info_path):
            try:
                os.remove(f)
            except OSError:
                pass
        print("Cleaned up raw recording + sidecar.")


if __name__ == "__main__":
    main()
