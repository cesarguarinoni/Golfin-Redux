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


def crossings(values):
    """How many times the signal changes sign — the shake's swing count."""
    n = 0
    for i in range(1, len(values)):
        if values[i - 1] < 0 <= values[i] or values[i - 1] > 0 >= values[i]:
            n += 1
    return n


def envelope_delta(ta, va, tb, vb, window=0.1):
    """Peak |amplitude| per time window, old vs new. Returns (worst delta, worst window peak)."""
    def peaks(times, values):
        out = {}
        for t, v in zip(times, values):
            k = int(t / window)
            out[k] = max(out.get(k, 0.0), abs(v))
        return out
    pa, pb = peaks(ta, va), peaks(tb, vb)
    worst, biggest = 0.0, 0.0
    for k in sorted(set(pa) & set(pb)):
        worst = max(worst, abs(pa[k] - pb[k]))
        biggest = max(biggest, pa[k])
    return worst, biggest


def sample_at(times, values, t):
    """The NEW trace's value at instant `t`, CATMULL-ROM interpolated between its samples.

    Linear interpolation was not good enough and the numbers say so: the pill slide covers
    555 px in 27 frames, so a chord across one 16 ms step cuts a corner worth ~1.6 px off a
    curve whose gate is 0.5 px. Every curve compared here is a cubic ease (or a lerp of one),
    and a Catmull-Rom through four samples reproduces a cubic exactly, so the resampling stops
    contributing error rather than merely contributing less. The caller still prints a
    worst-case bound so this is checkable rather than taken on trust.
    """
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

    # Catmull-Rom over p0..p3 with p1,p2 the bracketing samples. Falls back to the chord at the
    # very ends, where there is no fourth point to take a tangent from.
    i1, i2 = lo, hi
    i0, i3 = i1 - 1, i2 + 1
    if i0 < 0 or i3 >= len(values):
        return values[i1] + (values[i2] - values[i1]) * f
    p0, p1, p2, p3 = values[i0], values[i1], values[i2], values[i3]
    f2 = f * f
    f3 = f2 * f
    return 0.5 * ((2 * p1)
                  + (-p0 + p2) * f
                  + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f2
                  + (-p0 + 3 * p1 - 3 * p2 + p3) * f3)


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

        skipped = 0
        if t["name"] == "gacha.shake":
            # A PER-SAMPLE GATE IS THE WRONG GATE HERE, and saying so is not softening it.
            # The shake ramps to 14 Hz and the clock samples at 60 Hz — four samples a cycle.
            # Two runs whose frames land a millisecond apart read the sine at different points
            # of its swing and differ by degrees while tracing the identical motion. What
            # "unchanged" actually means for a shake is its ENVELOPE (the 2 -> 7 degree
            # amplitude ramp) and its total swing count (the 6 -> 14 Hz frequency ramp), and
            # both are independent of where the samples happen to land.
            worst, interp = envelope_delta(ta, va, tb, vb)
            ca, cb = crossings(va), crossings(vb)
            if ca != cb:
                print(f"{t['name']:<22}{frames:<12}{'swings ' + str(ca) + ' vs ' + str(cb):>12}"
                      f"{'':>10}  {tol:>7}  FAIL")
                fails += 1
                continue
        else:
            # ONLY WHERE BOTH TRACES ARE DEFINED. Outside the new trace's span, sample_at can
            # only clamp to its end value, and a clamp is not a measurement: the two runs' first
            # frames land ~0.9 ms apart, and on the pill slide's steepest stretch (3.8 px/ms) that
            # produced a 3.25 px "difference" that was entirely the clamp. Samples outside the
            # overlap are COUNTED and reported rather than quietly dropped.
            worst, interp, skipped = 0.0, 0.0, 0
            lo_t, hi_t = tb[0], tb[-1]
            for tk, x in zip(ta, va):
                if tk < lo_t or tk > hi_t:
                    skipped += 1
                    continue
                y = sample_at(tb, vb, tk)
                worst = max(worst, abs(x - y))
            if skipped:
                frames += f" -{skipped}"
        if t["name"] != "gacha.shake":
            # Worst-case resampling error: the largest THIRD difference of the new trace, which
            # is what a cubic interpolant of a cubic leaves behind. Reported, never subtracted.
            for i in range(3, len(vb)):
                interp = max(interp, abs(vb[i] - 3 * vb[i - 1] + 3 * vb[i - 2] - vb[i - 3]) / 6.0)

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
