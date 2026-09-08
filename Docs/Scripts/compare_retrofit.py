#!/usr/bin/env python3
"""Diff the OLD and NEW per-frame traces of the three game_polish_b D2 retrofits.

The gate D2 states, per routine:
    scale      max |delta| <= 0.005
    position   max |delta| <= 0.5 px
    alpha      max |delta| <= 0.01

COMPARED ON TIME, NOT ON FRAME INDEX, and that is a correction rather than a weakening.
The first attempt compared sample k against sample k, which is only meaningful if both runs
ran on a bit-identical clock -- and they did not: Time.captureDeltaTime is re-asserted every
frame by the recorder and the booted app still pulled unscaledDeltaTime to ~0.01633 on one
run and to 1/60 on another, so "frame 12" was a different instant in each. Every sample
therefore carries the cumulative unscaled time the routine had integrated when it was taken,
and the NEW trace is interpolated onto the OLD trace's instants. Both curves are smooth over
a ~16 ms step, so the interpolation error is orders below the tolerances above; the script
prints its own worst-case interpolation bound so that claim is checkable, not asserted.

    python3 Docs/Scripts/compare_retrofit.py <old.json> <new.json>
"""
from __future__ import annotations
import json, sys

# Per-quantity tolerance, keyed by the `quantity` field the recorder writes.
TOL = {
    "localScale.x":                    (0.005, "scale"),
    "bagScale.x":                      (0.005, "scale"),
    "easeOutBack(k)":                  (0.005, "curve"),
    "anchoredPosition.x":              (0.5,   "px"),
    "glow.alpha":                      (0.01,  "alpha"),
    "bagPivot.localEulerAngles.z":     (0.5,   "deg"),
}


def sample_at(times, values, t):
    """The NEW trace's value at instant `t`, linearly interpolated between its samples."""
    if t <= times[0]:
        return values[0]
    if t >= times[-1]:
        return values[-1]
    lo, hi = 0, len(times) - 1
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if times[mid] <= t:
            lo = mid
        else:
            hi = mid
    span = times[hi] - times[lo]
    if span <= 0:
        return values[lo]
    f = (t - times[lo]) / span
    return values[lo] + (values[hi] - values[lo]) * f


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    old = json.load(open(sys.argv[1]))
    new = json.load(open(sys.argv[2]))

    if old.get("side") != "old" or new.get("side") != "new":
        print(f"REFUSING: sides are {old.get('side')!r} / {new.get('side')!r} — "
              "the recorder stamps these, and comparing two runs of the same side proves nothing.")
        return 2
    print(f"old {old['headSha'][:9]} ({old['recordedUtc']})   "
          f"new {new['headSha'][:9]} ({new['recordedUtc']})")
    print()
    print(f"{'trace':<22}{'frames':<12}{'max |delta|':>12}{'interp':>10}  {'tol':>7}  verdict")
    print("-" * 78)

    by_name = {t["name"]: t for t in new["traces"]}
    fails = 0
    rows = []
    for t in old["traces"]:
        n = by_name.get(t["name"])
        if n is None:
            print(f"{t['name']:<22}{'MISSING in new':<14}{'':>12}  {'':>8}  FAIL")
            fails += 1
            continue
        tol, unit = TOL.get(t["quantity"], (0.005, "?"))
        ta, va = t.get("times"), t["values"]
        tb, vb = n.get("times"), n["values"]
        frames = f"{len(va)}v{len(vb)}"
        if not ta or not tb:
            print(f"{t['name']:<22}{frames:<12}{'no times':>12}{'':>10}  {tol:>7}  FAIL")
            fails += 1
            continue

        worst, interp = 0.0, 0.0
        for tk, x in zip(ta, va):
            y = sample_at(tb, vb, tk)
            worst = max(worst, abs(x - y))
        # Worst-case linear-interpolation error on the NEW trace: half the largest
        # sample-to-sample jump, which bounds the chord-vs-curve gap for a smooth curve.
        for i in range(1, len(vb)):
            interp = max(interp, abs(vb[i] - vb[i - 1]) / 2.0)

        ok = worst <= tol
        fails += 0 if ok else 1
        print(f"{t['name']:<22}{frames:<12}{worst:>12.6f}{interp:>10.6f}  {tol:>7}  "
              f"{'PASS' if ok else 'FAIL'}  ({unit})")
        rows.append((t["name"], worst, tol, ok))

    print("-" * 78)
    print(f"traces: {len(old['traces'])}   fail: {fails}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
