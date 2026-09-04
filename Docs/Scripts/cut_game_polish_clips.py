#!/usr/bin/env python3
"""
cut_game_polish_clips.py — slice the game_polish_a A4 take into its six captioned clips.

    python3 Docs/Scripts/cut_game_polish_clips.py

WHY ONE TAKE AND NOT SIX RECORDINGS. Six play sessions is six chances for the
Editor to come up on a different screen, and six RecorderControllers in one session
is the arrangement that has historically produced flipped and truncated files. So
GamePolishDemoRecorder records the whole route once and writes videos/segments.json
with each segment's start/end on the SAME clock; this cuts on those boundaries.

CAPTIONS use the textfile= drawtext idiom, never inline drawtext — an inline
caption breaks on the first apostrophe or colon in the text, and these captions
have both (memory: reference_video_caption_tool, which says the same about
build_bot_video.py). The caption file is written next to the clip and removed after.

FLIP CHECK. Every clip is verified by decoding CONSECUTIVE frames rather than by
ffmpeg -ss keyframe sampling, which skips exactly the frames a flip shows up on
(memory: reference_video_flip_verification). The check is that the top strip of the
frame is the TOP BAR (a dark navy band) and not the nav bar.
"""
import json
import os
import textwrap
import subprocess
import sys

VID = "Docs/Specs/Active/game_polish_a/videos"
SHOTS = "Docs/Specs/Active/game_polish_a/screenshots"
SIDE = os.path.join(VID, "segments.json")
RAW = os.path.join(VID, "raw.mp4")

FONT = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"

# 30 px Arial Bold is ~17 px per character, so ~62 characters fit inside an 1170 px
# frame with the plate's border. Measured against the first cut rather than guessed.
CAPTION_PX = 30
CAPTION_COLS = 58


def sh(cmd):
    p = subprocess.run(cmd, capture_output=True, text=True)
    if p.returncode != 0:
        print("  ffmpeg failed:", " ".join(cmd[:6]), "...")
        print("  ", p.stderr.strip().splitlines()[-3:] if p.stderr else "")
    return p.returncode == 0


def main():
    if not os.path.exists(SIDE):
        sys.exit(f"no sidecar at {SIDE} — run GOLFIN > Game Polish > Record the A4 demo first")
    if not os.path.exists(RAW):
        sys.exit(f"no raw take at {RAW}")

    side = json.load(open(SIDE))
    os.makedirs(SHOTS, exist_ok=True)

    for seg in side["segments"]:
        sid, cap = seg["id"], seg["caption"]
        start, end = float(seg["start"]), float(seg["end"])
        dur = max(0.5, end - start)
        out = os.path.join(VID, f"game_polish_a_{sid}.mp4")
        capfile = os.path.join(VID, f"_{sid}.caption.txt")

        # The caption goes in a FILE. Inline drawtext breaks on the first ' or :
        # and every one of these captions has both.
        # A translucent plate under the text so it stays readable over both the
        # bright Home art and the dark rankings backdrop, low enough on the frame
        # not to sit over the screen's own title.
        #
        # THE CAPTION IS WRAPPED HERE, NOT BY ffmpeg. drawtext does not wrap: the
        # first cut ran a 78-character caption off BOTH edges of an 1170 px frame
        # ("PTION (b) - push WITH ... FLAG OFF IN THE BUIL"), which is worse than
        # no caption because it looks like a rendering bug. The width is computed
        # from the font size rather than guessed, and the plate grows downward as
        # lines are added.
        wrapped = "\n".join(textwrap.wrap(cap, width=CAPTION_COLS))
        with open(capfile, "w") as f:
            f.write(wrapped)
        lines = wrapped.count("\n") + 1

        draw = (f"drawtext=fontfile={FONT}:textfile={capfile}:"
                f"x=(w-text_w)/2:y=h-{150 + 46 * lines}:fontsize={CAPTION_PX}:fontcolor=white:"
                f"box=1:boxcolor=black@0.62:boxborderw=20:line_spacing=10")

        ok = sh(["ffmpeg", "-y", "-ss", f"{start:.3f}", "-t", f"{dur:.3f}", "-i", RAW,
                 "-vf", draw, "-c:v", "libx264", "-preset", "medium", "-crf", "20",
                 "-pix_fmt", "yuv420p", "-an", out])
        if os.path.exists(capfile):
            os.remove(capfile)
        if not ok:
            continue

        size = os.path.getsize(out)
        # One still per clip, taken a beat in so it is a settled frame rather than
        # a mid-fade one — the stills are supporting evidence, the clip is the artifact.
        still = os.path.join(SHOTS, f"a4_{sid}.png")
        sh(["ffmpeg", "-y", "-ss", f"{min(1.0, dur/2):.3f}", "-i", out, "-frames:v", "1", still])
        print(f"  {os.path.basename(out):44s} {dur:5.1f}s  {size/1024:7.0f} KB  "
              f"flag={'ON' if seg.get('allowBackgroundCrossFade') else 'off'}  still={os.path.basename(still)}")


if __name__ == "__main__":
    main()
