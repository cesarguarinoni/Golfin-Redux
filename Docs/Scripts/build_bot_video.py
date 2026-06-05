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


def parse_steps_captions(log_path, rec_start, rec_end):
    """
    Generic UI-walkthrough captioner. Reads `[t=T] Step: '<text>'` lines and renders
    <text> VERBATIM (no 'Tap' prefix) from each step until the next (capped ~3.2s).
    Use for non-click UI demos (scroll / swipe / expand / collapse) where 'Tap' is wrong.
    Supports literal '\\n' in the step text for multi-line captions.
    """
    events = []
    if not os.path.exists(log_path):
        print(f"WARN: history.log not found ({log_path}) — title caption only.")
        return events
    step_re = re.compile(r"\[t=([\d.]+)\]\s+Step:\s+'([^']+)'")
    raw = []
    with open(log_path) as fh:
        for line in fh:
            m = step_re.search(line)
            if m:
                raw.append((float(m.group(1)), m.group(2)))
    span = rec_end - rec_start
    for i, (t, text) in enumerate(raw):
        start = t - rec_start
        nxt = (raw[i + 1][0] - rec_start) if i + 1 < len(raw) else span
        end = min(start + 3.2, nxt)
        if end <= start:
            end = start + 1.4
        start = max(start, 0.0)
        if start >= span:
            continue
        events.append((start, min(end, span), text.replace("\\n", "\n")))
    return events


def _dist3(a, b):
    """Euclidean distance between (x,y,z) tuples."""
    return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2) ** 0.5


def parse_visualgate_captions(log_path, rec_start, rec_end):
    """
    Visual-gate scenario captioner. Reads stroke events + at-rest positions to
    surface "Stroke N (club, power) → carry Xm" captions during gameplay, plus a
    pre-arm subtitle and a hole-complete card.

    Expected log lines (LoopV2SmokeBot.live_stat_provider_visual_gate_*):
      [t=T] === Live Stat Provider Visual Gate — <BUILD> ===
      [t=T]   PreArm: char=<id> lv=<lv> STR=<a> CTRL=<b> REC=<c> STAM=<d> (<BUILD>)
      [t=T] Stroke <N>: ball=(x, y, z) cup=(...) dist=<d>m — <name> club=<c> power=<p>
      [t=T]   Stroke <N> terminal=<t> endSurface=<s> ball=(x, y, z)
      [t=T] === PlayHoleToCup done: <N> strokes, holed=<seam|true> ===
    """
    events = []
    if not os.path.exists(log_path):
        print(f"WARN: history.log not found ({log_path}) — title caption only.")
        return events

    span = rec_end - rec_start

    prearm_re = re.compile(r"\[t=([\d.]+)\]\s+PreArm:\s+char=(\S+)\s+lv=(\d+)\s+STR=(\d+)\s+CTRL=(\d+)\s+REC=(\d+)\s+STAM=(\d+)\s+\((\w+)\)")
    stroke_fire_re = re.compile(r"\[t=([\d.]+)\]\s+Stroke\s+(\d+):\s+ball=\(([-\d.,\s]+)\)\s+cup=\([^)]+\)\s+dist=([\d.]+)m\s+—\s+(.+?)\s+club=(\d+)\s+power=([\d.]+)")
    stroke_rest_re = re.compile(r"\[t=([\d.]+)\]\s+Stroke\s+(\d+)\s+terminal=(\w+)\s+endSurface=(\w+)\s+ball=\(([-\d.,\s]+)\)")
    hole_done_re   = re.compile(r"\[t=([\d.]+)\]\s+===\s+PlayHoleToCup done:\s+(\d+)\s+strokes,\s+holed=(\w+)\s+===")

    def to_video_t(log_t):
        return max(0.0, log_t - rec_start)

    def parse_xyz(s):
        return tuple(float(p) for p in s.split(","))

    stroke_starts = {}   # n -> (video_t, ball_xyz, label, club, power)
    with open(log_path) as fh:
        for line in fh:
            m = prearm_re.search(line)
            if m:
                t = to_video_t(float(m.group(1)))
                build = m.group(8).upper()
                char  = m.group(2)
                lv    = m.group(3)
                # Portrait video is narrow (250px); wrap stats across 2 lines so they don't clip.
                line1_stats = f"STR {m.group(4)}  CTRL {m.group(5)}"
                line2_stats = f"REC {m.group(6)}  STAM {m.group(7)}"
                # Subtitle: visible after title card fades, before first stroke.
                events.append((t, t + 8.0,
                               f"{build} BUILD\n{char.replace('char_','')} Lv {lv}\n{line1_stats}\n{line2_stats}"))
                continue
            m = stroke_fire_re.search(line)
            if m:
                n = int(m.group(2))
                ball = parse_xyz(m.group(3))
                # Trim the parenthetical noise off the label so it fits a portrait
                # caption ("Driver full power (first stroke, dist=463m to cup)" -> "Driver full power").
                raw_label = m.group(5).strip()
                paren = raw_label.find("(")
                label = raw_label[:paren].strip() if paren > 0 else raw_label
                stroke_starts[n] = (
                    to_video_t(float(m.group(1))),
                    ball,
                    label,
                    int(m.group(6)),
                    float(m.group(7)),
                )
                continue
            m = stroke_rest_re.search(line)
            if m:
                n = int(m.group(2))
                if n not in stroke_starts:
                    continue
                vt_fire, ball_start, label, club, power = stroke_starts[n]
                vt_rest = to_video_t(float(m.group(1)))
                ball_end = parse_xyz(m.group(5))
                carry = _dist3(ball_start, ball_end)
                surface = m.group(4)
                # Caption rendered from fire to rest, capped at 3.5s so it doesn't
                # linger over the next aim.
                cap_end = min(vt_rest, vt_fire + 3.5, span)
                if cap_end > vt_fire:
                    # Portrait-friendly two-line caption: action + outcome.
                    arrow = "to" if surface in ("OOB",) else "->"
                    events.append((vt_fire, cap_end,
                                   f"Stroke {n}  {label}\nCarry {carry:.0f}m  {arrow}  {surface}"))
                continue
            m = hole_done_re.search(line)
            if m:
                t = to_video_t(float(m.group(1)))
                strokes = m.group(2)
                holed = m.group(3)
                tag = "Hole complete" if holed != "seam" else "Stroke cap hit  →  seam"
                events.append((t, min(t + 4.0, span), f"{tag}\n{strokes} strokes"))
    return events


def parse_spinshape_captions(log_path, rec_start, rec_end):
    """
    Spin-and-shot-shape visual gate captioner. Reads per-stroke labels and [Build]
    log lines to produce captions showing spin position + spinAxis + spinRate per stroke.

    Expected log lines (from SpinAndShapeVisualGate + LiveStatLogTee extended filter):
      [t=T] [BotDriver] Stroke N: LABEL spinInput=(X,Y)
      [t=T] [BotDriver] [TeeDiag] ResetLabToTee OK: ...
      [t=T] [Build] isPutt=False ... spinInput=(X.XX,Y.YY) spinAxis=(...) spinRate=ZZZ.Zrad/s

    Produces one caption per stroke visible from the fire moment to ball-at-rest:
      "Stroke N: LABEL\nspinInput=(X, Y)\nspinRate=Z rad/s"
    """
    events = []
    if not os.path.exists(log_path):
        print(f"WARN: history.log not found ({log_path}) — title caption only.")
        return events

    span = rec_end - rec_start

    # Pattern for the stroke step log (BotDriver.LogStep writes to file WITHOUT [BotDriver] prefix).
    # history.log format: "[t=T] Stroke N: LABEL spinInput=(X,Y)"
    stroke_label_re = re.compile(
        r"\[t=([\d.]+)\]\s+Stroke\s+(\d+):\s+(\w+)\s+spinInput=\(([-\d.]+),([-\d.]+)\)")
    # Pattern for the [Build] line emitted by DiagBuildLogger (captured via LiveStatLogTee into live_stat_log.txt).
    # NOTE: [Build] lines come from live_stat_log.txt, not history.log. The parser reads the same
    # log_path argument; if it doesn't find [Build] lines, build_rate will be None and caption
    # will show "n/a" for rate (still acceptable — label + spinInput are the primary visual).
    build_re = re.compile(
        r"\[t=([\d.]+)\].*\[Build\].*spinInput=\(([-\d.]+),([-\d.]+)\)"
        r".*spinAxis=\(([-\d.]+),([-\d.]+),([-\d.]+)\)"
        r".*spinRate=([-\d.]+)rad/s")
    # Pattern for capture (landed) events to time the end of a stroke caption.
    # history.log format: "[t=T] Capture: sXX_strokeN_..._landed → /path"
    capture_re = re.compile(r"\[t=([\d.]+)\]\s+Capture:\s+\S+stroke(\d+)_\w+_landed")

    def to_video_t(log_t):
        return max(0.0, log_t - rec_start)

    stroke_info = {}   # n -> {label, spin_x, spin_y, vt_start, build_rate}
    landed_times = {}  # n -> vt_landed

    # Read stroke labels and capture times from history.log.
    with open(log_path) as fh:
        for line in fh:
            m = stroke_label_re.search(line)
            if m:
                n = int(m.group(2))
                stroke_info[n] = {
                    "label":   m.group(3),
                    "spin_x":  float(m.group(4)),
                    "spin_y":  float(m.group(5)),
                    "vt_start": to_video_t(float(m.group(1))),
                    "build_rate": None,
                }
                continue
            m = capture_re.search(line)
            if m:
                n = int(m.group(2))
                landed_times[n] = to_video_t(float(m.group(1)))

    # Read [Build] lines from live_stat_log.txt (same scenario folder, one level up from screenshots/).
    # The [Build] prefix is emitted by DiagBuildLogger, captured by LiveStatLogTee.
    scenario_dir = os.path.dirname(log_path)
    stat_log_path = os.path.join(scenario_dir, "live_stat_log.txt")
    build_sources = [stat_log_path, log_path]  # prefer stat log; fall back to history.log
    for build_src in build_sources:
        if not os.path.exists(build_src):
            continue
        with open(build_src) as fh:
            for line in fh:
                m = build_re.search(line)
                if m:
                    # Associate this Build line with the most recent stroke by time.
                    vt = to_video_t(float(m.group(1)))
                    rate = float(m.group(8))
                    # Find the stroke whose vt_start is closest (and before) this build line.
                    best_n = None
                    for n, info in stroke_info.items():
                        if info["vt_start"] <= vt + 5.0:
                            if best_n is None or info["vt_start"] > stroke_info[best_n]["vt_start"]:
                                best_n = n
                    if best_n is not None and stroke_info[best_n]["build_rate"] is None:
                        stroke_info[best_n]["build_rate"] = rate
        if any(info["build_rate"] is not None for info in stroke_info.values()):
            break  # found rates from this source, done

    for n, info in sorted(stroke_info.items()):
        vt_start = info["vt_start"]
        vt_end = landed_times.get(n, vt_start + 15.0)
        cap_end = min(vt_end + 1.5, span)
        if cap_end <= vt_start:
            continue
        sx, sy = info["spin_x"], info["spin_y"]
        label = info["label"]
        rate_str = f"{info['build_rate']:.0f} rad/s" if info["build_rate"] is not None else "n/a"
        caption = f"Stroke {n}: {label}\nspinInput=({sx:.0f}, {sy:.0f})\nspinRate={rate_str}"
        events.append((vt_start, cap_end, caption))

    return events


def esc(p):
    """Escape a path for an ffmpeg filter option value."""
    return p.replace("\\", "\\\\").replace(":", "\\:").replace("'", "\\'")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scenario", required=True, help="smoke-bot scenario key")
    ap.add_argument("--mode", choices=["clicks", "steps", "visualgate", "spinshape"], default="clicks",
                    help="Caption parser to use. 'clicks' = tap-event captions (default, "
                         "for UI flows). 'steps' = verbatim `Step: '<text>'` captions "
                         "(for scroll/swipe/expand UI walkthroughs). 'visualgate' = per-stroke "
                         "carry captions (for live_stat_provider_visual_gate_* scenarios). "
                         "'spinshape' = per-stroke spin position + rate captions "
                         "(for SpinAndShapeVisualGate scenario).")
    ap.add_argument("--title", default="Loop v2 — Stage F\nButton Press Feedback")
    ap.add_argument("--suffix", default="stageF_buttons", help="output filename suffix")
    ap.add_argument("--output-dir", default=None,
                    help="Override output directory (default: Docs/Videos/). Use to land "
                         "captioned videos in a per-task videos/ folder, e.g. "
                         "Docs/Specs/Active/<task>/videos/.")
    ap.add_argument("--raw-mp4", default=None,
                    help="Override raw mp4 source path. Use this when the raw recording "
                         "has already been moved out of tasks/loop_v2_smoke_bot/<scenario>/video/ "
                         "(e.g. copied into a task's videos/ folder).")
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
    if args.raw_mp4:
        raw = args.raw_mp4
        if not os.path.isabs(raw):
            raw = os.path.join(REPO, raw)
    else:
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
    if args.mode == "visualgate":
        captions = parse_visualgate_captions(log_path, rec_start, rec_start + duration)
    elif args.mode == "spinshape":
        captions = parse_spinshape_captions(log_path, rec_start, rec_start + duration)
    elif args.mode == "steps":
        captions = parse_steps_captions(log_path, rec_start, rec_start + duration)
    else:
        captions = parse_captions(log_path, rec_start, rec_start + duration)
    # Allow callers to pass literal \n in --title (bash strips backslash semantics).
    title_text = args.title.replace("\\n", "\n")
    captions.insert(0, (0.0, 3.6, title_text))
    print(f"Captions: {len(captions)} (mode={args.mode})")

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

        if args.output_dir:
            out_dir = args.output_dir if os.path.isabs(args.output_dir) else os.path.join(REPO, args.output_dir)
        else:
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
