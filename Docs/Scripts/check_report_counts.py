#!/usr/bin/env python3
"""Diff every count a spec report asserts against the JSON it cites. No allow-list.

WHY THIS EXISTS, AND WHY IT WAS REWRITTEN. `game_polish_a` was failed at the red-team gate three
times for one shape — report narrative quoting a superseded run — and each fix was narrower than
the shape, then described as complete:

  round 1  fixed two sections by reading; claimed "enumerated every heading".
           The shape is stale CONTENT; content lives in bodies and tables.
  round 2  wrote check_report_citations.py (resolves FILE PATHS) and reported
           "78 cited, 0 unresolved" as report-integrity proof. It parses no numbers.
  round 3  wrote the FIRST version of this script — with a HARD-CODED suspect set
           {48,44,24,21,12}. A later superseded run's fingerprint was 84/52/4. Not in the
           set, so the script printed "0 stale" over a live defect, in the very section a
           previous FAIL had designated authoritative. A checker with an allow-list can only
           find what its author already knew.

So this version has no allow-list and no suspect set. It extracts EVERY integer adjacent to a
counting word in every live section and asks one question: is this a value the cited JSON actually
produces? Anything else is reported for a human to rule on. False positives are the point — a
checker that is silent unless you told it what to look for is the failure mode above.

    python3 Docs/Scripts/check_report_counts.py <report.md> <invariants.json>
"""
from __future__ import annotations
import json, re, sys

# Words that make a nearby integer a CLAIM ABOUT THE RUN rather than prose.
COUNT_CTX = (r"record|records|push|pushes|pair|pairs|frame|frames|starved|unstarved|"
             r"measured|same-backdrop|cross-backdrop|realWidget|real-widget|of\b")


def truth(js: dict) -> set[int]:
    """Every integer the cited JSON can legitimately produce.

    TWO SHAPES NOW. game_polish_a's invariants are a list of `pushes`; game_polish_b's modal
    gate is a list of `records`. The dispatch is on the KEY the file actually has, not on a
    task name, so a third shape fails loudly here instead of being silently mis-read as one
    of the first two.
    """
    if "records" in js and "pushes" not in js:
        return truth_records(js)
    R = js["pushes"]
    ok = [r for r in R if not r.get("frameStarved")]
    same = [r for r in R if r.get("sameBackground")]
    vals = {
        len(R), len(R) - len(ok), len(ok), len(same), len(R) - len(same),
        len({(r["from"], r["to"]) for r in R}),
        len({(r["from"], r["to"]) for r in same}),
        len({(r["from"], r["to"]) for r in R if not r.get("sameBackground")}),
        len([r for r in R if r.get("realWidget")]),
        len([r for r in R if not r.get("realWidget")]),
    }
    vals |= {r["frames"] for r in ok}                    # any legitimate frame count
    vals |= {min(r["frames"] for r in ok), max(r["frames"] for r in ok)}
    return vals


def truth_records(js: dict) -> set[int]:
    """game_polish_b's modal gate. Same principle as truth(): derive EVERY count the file can
    justify — totals, and the size of every subset a reader might reasonably quote — rather
    than listing the numbers the report happens to use."""
    R = js["records"]
    vals = {len(R), js.get("modals", len(R)), js.get("fail", 0)}
    for key in ("animateShow", "realWidget", "showIsNoOp", "activated", "visibleOnShowFrame",
                "visibleOnHideFrame"):
        on = [r for r in R if r.get(key)]
        vals |= {len(on), len(R) - len(on)}
    vals |= {sum(len(r.get("fails", [])) for r in R)}
    # the open-count readings, which the report quotes as timing evidence
    for r in R:
        for key in ("openCountBefore", "openCountOnShow", "openCountAfterHide"):
            v = r.get(key)
            if isinstance(v, int):
                vals.add(v)
    return vals


def live_lines(text: str):
    skip = None
    for i, line in enumerate(text.split("\n"), 1):
        m = re.match(r"^(#{2,4})\s+(.*)", line)
        if m:
            lvl, title = len(m.group(1)), m.group(2).upper()
            if skip is not None and lvl <= skip:
                skip = None
            if "SUPERSEDED" in title:
                skip = lvl
                continue
        if skip is None:
            yield i, line


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    report, jsonp = sys.argv[1], sys.argv[2]
    js = json.load(open(jsonp))
    good = truth(js)
    text = open(report, encoding="utf-8").read()

    print(f"values the cited JSON produces: {sorted(good)}")

    hits = []
    for n, line in live_lines(text):
        for m in re.finditer(rf"(\d+)\s*(?=[^|\n]{{0,40}}?(?:{COUNT_CTX}))", line):
            v = int(m.group(1))
            if v <= 1 or v > 100_000:          # not a run count
                continue
            # SYNTACTIC exclusions only — never value-based. A filter that skips a number
            # because of WHAT IT IS is how the previous version hid 84/52/4; a filter that
            # skips it because of how it is WRITTEN cannot hide "52 records".
            a, b = m.start(1), m.end(1)
            before, after = line[max(0, a - 2):a], line[b:b + 4]
            if before.endswith(".") or after.startswith("."):
                continue                       # part of a decimal: 0.250, 1.232
            if re.match(r"\s*(?:%|KB|MB|GB|B\b|ms\b|s\b|px\b|×|x\d)", after):
                continue                       # a unit, not a count
            if re.search(r"[A-Za-z]$", before) and not before.endswith(" "):
                continue                       # A4 / A13 / f643 — an identifier
            if re.search(r"(?:iteration|iter-?|round|phase|step|§|Rule)\s*$", line[:a], re.I):
                continue                       # an ordinal reference
            if v in good:
                continue
            hits.append((n, v, line.strip()[:105]))
            break

    if hits:
        print("\n=== integers next to a counting word that the JSON does NOT produce ===")
        print("    (each needs a human verdict: stale claim, or a number about something else)")
        for n, v, line in hits:
            print(f"  L{n:<5} [{v}]  {line}")
    print(f"\n{len(hits)} unexplained count(s) in live sections  ({report})")
    return 1 if hits else 0


if __name__ == "__main__":
    raise SystemExit(main())
