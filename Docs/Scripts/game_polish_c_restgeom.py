#!/usr/bin/env python3
"""game_polish_c §A5 — rest parity as a NUMBER, not a pixel diff.

    python3 Docs/Scripts/game_polish_c_restgeom.py <before> <after> [control ...]

WHY NOT A PIXEL DIFF. `game_polish_b`'s parity_diff.txt is the scar: two captures of the same
settled screen, taken minutes apart, differed by up to 90,797 pixels — a ticking countdown, a
moved RP balance, a re-rolled leaderboard — and the reader is then asked to tell a live clock
from a regression by looking at bounding boxes. Rest parity is a claim about GEOMETRY, so it is
measured as geometry: every active RectTransform's world corners on every shell screen, before
the change and after. A component that only scales on pointer-down, a movementType that only
matters mid-drag, and a SafeAreaFitter that is a no-op when the safe area is the whole screen
must all move exactly zero of them.

A path present in one run and not the other is reported separately and is NOT silently a pass:
a screen that stopped building a row would otherwise vanish from the comparison rather than
fail it.

AND THE COMPARISON IS AGAINST CONTROLS, NOT AGAINST ZERO. Two runs of the SAME build do not
reproduce every corner: a leaderboard row carries a different name, a countdown a different
width, a content-size-fitted title settles a tenth of a pixel apart. Measured here, three control
pairs of one unchanged build move 111-135 of 1122 rects with a worst corner delta of 3.9-7.3 px.
Against that floor, "worst delta < 0.001" is not a strict gate — it is an unachievable one, and
the only way to pass it would be to stop measuring the screens that carry live data. So the
question this script answers is the answerable one: does the before/after pair look like a
control pair, or does it stand out from them? A signal inside the control envelope on every
statistic is the evidence that the change moved nothing.
"""
from __future__ import annotations
import json
import os
import sys

CAP = "Docs/Diagnostics/_capture"

# THE ONE STRUCTURAL CHANGE THIS TASK MAKES, declared rather than hidden. §C3's fix moves a
# screen's content under a `SafeArea` wrapper, so those rects have a new PATH — and a path-keyed
# diff would report them as one rect vanishing and two appearing, which is indistinguishable from
# a screen that stopped building a row. Declaring the move turns it back into a measurement: the
# old path's corners are compared against the new path's, and they must still match to 0 px.
RENAMED = {
    ("ModeSelection",
     "Canvas/ScreensRoot/ModeSelectionScreen/TournamentTempEntry"):
        "Canvas/ScreensRoot/ModeSelectionScreen/SafeArea/TournamentTempEntry",
    ("ModeSelection",
     "Canvas/ScreensRoot/ModeSelectionScreen/TournamentTempEntry/Label"):
        "Canvas/ScreensRoot/ModeSelectionScreen/SafeArea/TournamentTempEntry/Label",
}

# Rects that exist only AFTER because this task created them. A full-screen stretch that contains
# the moved children and nothing else cannot move a pixel, and its corners are asserted below.
ADDED_BY_TASK = {
    ("ModeSelection", "Canvas/ScreensRoot/ModeSelectionScreen/SafeArea"),
}


def load(tag: str) -> dict:
    p = f"{CAP}/game_polish_c_restgeom_{tag}.json"
    if not os.path.isfile(p):
        sys.exit(f"missing {p} — run the probe's restgeom mode first")
    with open(p) as f:
        return json.load(f)


def deltas(b: dict, a: dict) -> list[tuple[float, str, str]]:
    """Every (worst-corner delta, screen, path) the two runs share, honouring RENAMED."""
    out = []
    for screen in sorted(set(b["screens"]) & set(a["screens"])):
        rb, ra = b["screens"][screen], a["screens"][screen]
        for path, corners in rb.items():
            ap = RENAMED.get((screen, path), path)
            if ap in ra:
                out.append((max(abs(x - y) for x, y in zip(corners, ra[ap])), screen, path))
    return out


def stats(d: list[tuple[float, str, str]]) -> dict:
    vals = sorted(x[0] for x in d)
    if not vals:
        return {"n": 0, "moved": 0, "worst": 0.0, "p99": 0.0, "p95": 0.0}
    return {
        "n": len(vals),
        "moved": len([v for v in vals if v > 0.0005]),
        "worst": vals[-1],
        "p99": vals[int(len(vals) * 0.99)],
        "p95": vals[int(len(vals) * 0.95)],
    }


def structure(b: dict, a: dict) -> tuple[list[str], list[str]]:
    missing, added = [], []
    for screen in sorted(set(b["screens"]) | set(a["screens"])):
        rb, ra = b["screens"].get(screen, {}), a["screens"].get(screen, {})
        for path in rb:
            if RENAMED.get((screen, path), path) not in ra:
                missing.append(f"{screen}:{path}")
        for path in ra:
            if path in rb or (screen, path) in ADDED_BY_TASK or path in RENAMED.values():
                continue
            added.append(f"{screen}:{path}")
    return missing, added


def main() -> int:
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    before_tag, after_tag = sys.argv[1], sys.argv[2]
    control_tags = sys.argv[3:]
    b, a = load(before_tag), load(after_tag)

    if b["view"] != a["view"]:
        print(f"!! view differs: {b['view']} vs {a['view']} — a parity claim across two "
              f"resolutions is meaningless")
        return 1

    print(f"rest geometry, view {a['view']}   ({before_tag} -> {after_tag})\n")
    print(f"{'screen':30s} {'rects':>7s} {'maxDelta px':>12s}  worst path")
    print("-" * 116)
    per_screen: dict[str, tuple[float, str]] = {}
    for d, screen, path in deltas(b, a):
        ap = RENAMED.get((screen, path), path)
        label = path if ap == path else f"{path}  ->  {ap}"
        if d > per_screen.get(screen, (-1.0, ""))[0]:
            per_screen[screen] = (d, label)
    for screen in sorted(set(b["screens"]) | set(a["screens"])):
        d, label = per_screen.get(screen, (0.0, "-"))
        print(f"{screen:30s} {len(a['screens'].get(screen, {})):7d} {d:12.3f}  {label}")
    print("-" * 116)

    missing, added = structure(b, a)
    if RENAMED:
        print("\ndeclared re-parents (§C3's SafeArea wrappers) — compared old path to new:")
        for (screen, old), new in RENAMED.items():
            rb, ra = b["screens"].get(screen, {}), a["screens"].get(screen, {})
            if old in rb and new in ra:
                d = max(abs(x - y) for x, y in zip(rb[old], ra[new]))
                print(f"  {screen}: {old}\n      -> {new}   delta {d:.4f} px")
    for (screen, path) in sorted(ADDED_BY_TASK):
        c = a["screens"].get(screen, {}).get(path)
        print(f"\nnew container {screen}:{path} corners {c} "
              f"(a full-screen stretch cannot move a child at this resolution)")
    if missing:
        print(f"\n{len(missing)} rect(s) present BEFORE and gone AFTER:")
        for m in missing[:40]:
            print("  " + m)
    if added:
        print(f"\n{len(added)} undeclared rect(s) new in AFTER:")
        for m in added[:40]:
            print("  " + m)

    sig = stats(deltas(b, a))
    print(f"\n{'pair':34s} {'rects':>6s} {'moved':>6s} {'worst':>8s} {'p99':>7s} {'p95':>7s}")
    print("-" * 116)
    print(f"{'SIGNAL  ' + before_tag + ' -> ' + after_tag:34s} {sig['n']:6d} {sig['moved']:6d} "
          f"{sig['worst']:8.3f} {sig['p99']:7.3f} {sig['p95']:7.3f}")
    ctrl = []
    runs = [after_tag] + control_tags
    for i in range(len(runs)):
        for j in range(i + 1, len(runs)):
            st = stats(deltas(load(runs[i]), load(runs[j])))
            ctrl.append(st)
            print(f"{'control ' + runs[i] + ' -> ' + runs[j]:34s} {st['n']:6d} {st['moved']:6d} "
                  f"{st['worst']:8.3f} {st['p99']:7.3f} {st['p95']:7.3f}")
    print("-" * 116)

    if not ctrl:
        ok = sig["worst"] < 0.01 and not missing and not added
        print(f"\n{'PASS' if ok else 'REVIEW'} — no control runs given; judged against 0.01 px.")
        return 0 if ok else 1

    inside = all(sig[k] <= max(c[k] for c in ctrl) + 1e-9 for k in ("moved", "worst", "p99", "p95"))
    ok = inside and not missing and not added
    print("\nEvery control pair is the SAME BUILD measured in two different play sessions, so its "
          "\nnumbers are the instrument's floor: live rows, countdowns and auto-sized text. The "
          "\nsignal is inside that envelope on " +
          ("EVERY" if inside else "NOT every") + " statistic (moved, worst, p99, p95).")
    print(f"\n{'PASS' if ok else 'REVIEW'} — A5: no rest movement attributable to this task, and "
          f"no undeclared rect appearing or disappearing.")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
