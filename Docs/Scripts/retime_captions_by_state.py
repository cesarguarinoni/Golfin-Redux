#!/usr/bin/env python3
"""Re-time a DemoRecorder caption sidecar from what is ACTUALLY ON SCREEN.

    python3 Docs/Scripts/retime_captions_by_state.py \
        --raw Docs/Specs/Active/<task>/videos/raw.mp4 \
        --templates Docs/Specs/Active/<task>/screenshots \
        --plan Docs/Specs/Active/<task>/videos/caption_plan.json \
        --out Docs/Reports/Media/<task>/captions.json

WHY THIS EXISTS
───────────────
A UI DemoRecorder stamps each caption at `Time.realtimeSinceStartup - RecordStart`, which is WALL
time. The Unity Recorder writes VARIABLE-frame-rate video and drops frames whenever the editor
cannot keep up, so wall time and the encoded timeline are not the same clock — and the gap is not a
constant lead you can subtract. On `asset_loans` the runner's stamps ran 2.9 s to 4.8 s ahead of the
video, growing and shrinking through the clip, which put four captions a whole step late: "Lent: a
ribbon, a dim" was burned over the BORROWED panel, and "Borrowed…" over the return popup.

A caption that describes a screen the viewer is not looking at is worse than no caption. So instead
of trusting either clock, this measures the video: it classifies each sampled frame against known
STATE SCREENSHOTS (the ones the capture bot already produced), collapses the run of frames into
[start, end] windows per state, and writes the sidecar from those.

The comparison is a mean-absolute-difference on a small greyscale downscale — crude, but these
states differ by whole panels, not by pixels, and `--max-distance` refuses a match that is not
actually close rather than snapping to the nearest wrong thing.

⚠️ RUN IT ON THE RAW CLIP, never on a captioned one: a burned-in caption box changes the frame and
biases the match toward whichever template happens to be darkest.

THE PLAN FILE is an ordered list of what to say over which state:

    {"plan": [
      {"state": "roster_free",  "text": "A character you own\\nnow has a LEND button"},
      {"state": "modal_list",   "text": "Pick a duration",        "slice": [0.0, 0.45]},
      {"state": "modal_list",   "text": "Then someone you follow", "slice": [0.5, 1.0]}
    ]}

`state` is a template basename prefix (matched against the files in --templates). `slice` cuts a
fraction of that state's measured window, for the case where one screen carries several beats —
the fractions are of the MEASURED window, so they move with it rather than needing re-timing.
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import subprocess
import sys
import tempfile

try:
    import numpy as np
    from PIL import Image
except ImportError:  # pragma: no cover
    sys.exit("numpy and Pillow are required: pip install numpy Pillow")


# Small enough that a panel swap dominates the metric and a 1-frame animation does not.
THUMB = (117, 253)

# A caption inset from both ends of its measured window, so it can never straddle the transition
# into or out of the state it describes.
EDGE_INSET_S = 0.3


def sample_frames(raw: str, fps: float, workdir: str) -> list[str]:
    subprocess.run(
        ["ffmpeg", "-v", "error", "-i", raw,
         "-vf", f"fps={fps},scale={THUMB[0]}:{THUMB[1]}",
         "-y", os.path.join(workdir, "f_%05d.png")],
        check=True,
    )
    return sorted(glob.glob(os.path.join(workdir, "f_*.png")))


def load_templates(folder: str) -> dict[str, np.ndarray]:
    out = {}
    for path in sorted(glob.glob(os.path.join(folder, "*.png"))):
        stem = os.path.splitext(os.path.basename(path))[0]
        out[stem] = np.asarray(Image.open(path).convert("L").resize(THUMB)).astype(float)
    if not out:
        sys.exit(f"no template PNGs in {folder}")
    return out


def classify(frames: list[str], templates: dict[str, np.ndarray], fps: float,
             max_distance: float) -> list[tuple[float, str]]:
    labels = []
    for i, f in enumerate(frames):
        a = np.asarray(Image.open(f).convert("L")).astype(float)
        best, best_d = None, float("inf")
        for name, t in templates.items():
            d = float(np.abs(a - t).mean())
            if d < best_d:
                best, best_d = name, d
        labels.append((i / fps, best if best_d <= max_distance else "other"))
    return labels


def runs(labels: list[tuple[float, str]], fps: float) -> list[tuple[float, float, str]]:
    out, prev, start = [], None, 0.0
    for t, lab in labels:
        if lab != prev:
            if prev is not None:
                out.append((start, t, prev))
            prev, start = lab, t
    if prev is not None:
        out.append((start, labels[-1][0] + 1.0 / fps, prev))
    return out


def longest_window(windows, state: str) -> tuple[float, float] | None:
    """The LONGEST run of a state, not the first.

    A screen the clip returns to briefly — the borrowed panel behind a popup that has just closed —
    produces a second, short run, and captioning that one instead of the real beat is exactly the
    mistake this script exists to prevent.
    """
    hits = [(s, e) for s, e, lab in windows if lab.startswith(state)]
    if not hits:
        return None
    return max(hits, key=lambda se: se[1] - se[0])


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--raw", required=True, help="the RAW recorder mp4 (no burned captions)")
    ap.add_argument("--templates", required=True, help="folder of state screenshots")
    ap.add_argument("--plan", required=True, help="ordered {state, text, slice?} list")
    ap.add_argument("--out", required=True, help="captions.json to write")
    ap.add_argument("--fps", type=float, default=2.0, help="sampling rate (default 2)")
    ap.add_argument("--max-distance", type=float, default=26.0,
                    help="reject a template match worse than this mean abs difference")
    args = ap.parse_args()

    with open(args.plan, encoding="utf-8") as fh:
        plan = json.load(fh)["plan"]

    work = tempfile.mkdtemp(prefix="retime_")
    try:
        frames = sample_frames(args.raw, args.fps, work)
        templates = load_templates(args.templates)
        windows = runs(classify(frames, templates, args.fps, args.max_distance), args.fps)
    finally:
        for f in glob.glob(os.path.join(work, "*.png")):
            os.remove(f)
        os.rmdir(work)

    print("Measured timeline:")
    for s, e, lab in windows:
        print(f"  {s:6.1f}s -> {e:6.1f}s   {lab}")

    captions, missing = [], []
    for item in plan:
        win = longest_window(windows, item["state"])
        if win is None:
            missing.append(item["state"])
            continue
        s, e = win
        lo, hi = item.get("slice", [0.0, 1.0])
        span = e - s
        cs = s + span * lo + EDGE_INSET_S
        ce = s + span * hi - EDGE_INSET_S
        if ce - cs < 0.8:            # too thin to read — take the window as-is
            cs, ce = s + EDGE_INSET_S, e - EDGE_INSET_S
        captions.append({"start": round(cs, 2), "end": round(ce, 2), "text": item["text"]})

    if missing:
        # LOUD, not silent. A state the plan names and the clip never shows means the run did not do
        # what the plan says it did, and burning the remaining captions would ship that as fact.
        sys.exit("ERROR: these planned states never appear in the clip: " + ", ".join(missing))

    os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        json.dump({"captions": captions}, fh, indent=2, ensure_ascii=False)
        fh.write("\n")

    print(f"\nwrote {len(captions)} captions -> {args.out}")
    for c in captions:
        print(f"  {c['start']:6.2f} -> {c['end']:6.2f}  {c['text'].splitlines()[0]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
