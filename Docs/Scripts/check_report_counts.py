#!/usr/bin/env python3
"""Reconcile the counts a spec report asserts against the JSON it cites.

WHY THIS EXISTS. `game_polish_a` was failed at the red-team gate THREE times for one shape: report
narrative quoting a superseded run. Each fix was narrower than the shape and was then described as
complete:

  round 1 — fixed two stale sections by reading; claimed "enumerated every heading".
            The shape is stale CONTENT, and content lives in bodies and tables.
  round 2 — wrote check_report_citations.py, which resolves FILE PATHS, and reported
            "78 cited, 0 unresolved" as report-integrity proof. It parses no numbers, so the
            superseded run's COUNTS survived untouched in four live PASS sections — including a
            table footnote fifteen lines below a summary block that contradicted it.

A path checker cannot see "4 of 48". This one can. It reads the invariants JSON, derives the
counts that actually hold, and greps the report for the fingerprint numbers of any OTHER run.

    python3 Docs/Scripts/check_report_counts.py <report.md> <invariants.json>

Reports every line quoting a stale fingerprint number outside a SUPERSEDED section, so the fix is
per-site rather than per-instance. Exits non-zero if any survive.
"""
from __future__ import annotations
import json, re, sys


def truth(js: dict) -> dict:
    R = js["pushes"]
    ok = [r for r in R if not r.get("frameStarved")]
    same = [r for r in R if r.get("sameBackground")]
    return {
        "measured": len(R),
        "starved": len(R) - len(ok),
        "unstarved": len(ok),
        "same_backdrop": len(same),
        "cross_backdrop": len(R) - len(same),
        "pairs": len({(r["from"], r["to"]) for r in R}),
        "same_pairs": len({(r["from"], r["to"]) for r in same}),
        "real_widget": len([r for r in R if r.get("realWidget")]),
    }


def live_lines(text: str):
    """Yield (lineno, line) outside sections whose heading says SUPERSEDED."""
    skip = None
    for i, line in enumerate(text.split("\n"), 1):
        m = re.match(r"^(#{2,4})\s+(.*)", line)
        if m:
            lvl, title = len(m.group(1)), m.group(2).upper()
            if skip is not None and lvl <= skip:
                skip = None
            if "SUPERSEDED" in title or title.startswith("(SUPERSEDED)"):
                skip = lvl
                continue
        if skip is None:
            yield i, line


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    report, jsonp = sys.argv[1], sys.argv[2]
    t = truth(json.load(open(jsonp)))
    text = open(report, encoding="utf-8").read()

    print("counts that actually hold (from %s):" % jsonp)
    for k, v in t.items():
        print(f"  {k:16s} {v}")

    # Any run has a fingerprint: its record count and its derived counts. Numbers that are NOT in
    # the current truth set, but read like record counts, are the tell of a superseded run.
    current = set(t.values())
    suspects = {48, 44, 24, 21, 12}   # the pre-option-(b) run's fingerprint
    stale = sorted(suspects - current)

    hits = []
    for n, line in live_lines(text):
        # a correction that NAMES the stale number on purpose is not a claim
        if re.search(r"read \"|used to|previously|earlier revision|pre-decision|pre-option|stale", line, re.I):
            continue
        for v in stale:
            if re.search(rf"\b{v}\b\s*(?:of|/|records|pushes|pairs|frames)", line) or \
               re.search(rf"(?:all|only|remaining|measured)\s+(?:\*\*)?{v}\b", line):
                hits.append((n, v, line.strip()[:110]))
                break

    print(f"\nstale fingerprint numbers to look for: {stale}")
    if hits:
        print("\n=== lines asserting a superseded run's counts ===")
        for n, v, line in hits:
            print(f"  L{n}  [{v}]  {line}")
    print(f"\n{len(hits)} stale count assertion(s) in live sections  ({report})")
    return 1 if hits else 0


if __name__ == "__main__":
    raise SystemExit(main())
