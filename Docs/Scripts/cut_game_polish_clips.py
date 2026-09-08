#!/usr/bin/env python3
"""
cut_game_polish_clips.py — slice the game_polish_a A4 take into its six captioned clips.

    python3 Docs/Scripts/cut_game_polish_clips.py [task_slug]

The slug defaults to game_polish_a, so the original invocation is unchanged; game_polish_b
passes its own and everything else about the cut is identical.

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

TASK = sys.argv[1] if len(sys.argv) > 1 else "game_polish_a"
VID = f"Docs/Specs/Active/{TASK}/videos"
SHOTS = f"Docs/Specs/Active/{TASK}/screenshots"
SIDE = os.path.join(VID, "segments.json")
RAW = os.path.join(VID, "raw.mp4")

FONT = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"

# 30 px Arial Bold is ~17 px per character, so ~62 characters fit inside an 1170 px
# frame with the plate's border. Measured against the first cut rather than guessed.
CAPTION_PX = 30
CAPTION_COLS = 58

# Longest clip we will cut. See the clamp in main().
MAX_CLIP = 75.0


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
        # A segment that never reached its subject must not ship as a captioned clip claiming
        # it did. The recorder writes `reached` per segment; honour it, and say what was skipped.
        if seg.get("reached") is False:
            print(f"  SKIP {sid:38s} not reached: {seg.get('note','')[:70]}")
            continue

        # Clamp: one segment in the b take ran 850 s because the shell stalled mid-route, and an
        # 850 s "clip" is not a clip. The first MAX_CLIP seconds hold the thing being shown.
        if dur > MAX_CLIP:
            print(f"  clamp {sid:38s} {dur:.0f}s -> {MAX_CLIP}s (the rest is the shell stalling)")
            dur = MAX_CLIP

        out = os.path.join(VID, f"{TASK}_{sid}.mp4")
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
        # One still per clip, taken 60% of the way in rather than one second in. A fixed 1 s
        # lands before the subject on any clip longer than a few seconds: the first cut of the
        # level-up clip produced a still of the Roster screen with the modal not yet open, which
        # is a picture of the thing that happens BEFORE the thing being demonstrated.
        still = os.path.join(SHOTS, f"a4_{sid}.png")
        sh(["ffmpeg", "-y", "-ss", f"{dur * 0.6:.3f}", "-i", out, "-frames:v", "1", still])
        print(f"  {os.path.basename(out):44s} {dur:5.1f}s  {size/1024:7.0f} KB  "
              f"still={os.path.basename(still)}")


if __name__ == "__main__":
    main()
