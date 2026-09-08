#!/usr/bin/env python3
"""game_polish_c §C5 — the three tables and every count in them, GENERATED from the probe JSON.

    python3 Docs/Scripts/game_polish_c_tables.py emit  [before_tag] [after_tag] [sim_tag]
    python3 Docs/Scripts/game_polish_c_tables.py check <report.md>

WHY A GENERATOR AND NOT A CHECKER. b's `check_report_counts.py` catches a number the report
typed that the JSON cannot produce. It cannot catch a number nobody typed — a row left out of a
table of 511. §C5 asks for a verdict per site over three tables whose largest is five hundred
rows long, so the tables themselves are emitted from the JSON and pasted whole; nothing in them
is typed, so nothing in them can drift.

`check` is the belt to that braces: it re-derives every headline count from the JSON and greps
the report for each one, so a count that WAS typed somewhere in the prose is still verified.

THE COUNTS ARE ALL DERIVED, NEVER STORED. The probe writes `total`, `covered` etc. into its own
JSON, and this script deliberately RECOMPUTES them from the rows instead of reading those fields:
a summary field and the rows it summarises are two claims, and only the rows are evidence.
"""
from __future__ import annotations
import json
import os
import re
import sys

CAP = "Docs/Diagnostics/_capture"


def load(table: str, tag: str) -> dict:
    p = f"{CAP}/game_polish_c_{table}_{tag}.json"
    if not os.path.isfile(p):
        sys.exit(f"missing {p} — run the probe first")
    with open(p) as f:
        return json.load(f)


# ── C1 ────────────────────────────────────────────────────────────────────────

def c1_counts(js: dict, scope: dict | None = None) -> dict:
    """Counts for one run.

    `scope`, when given, is the AFTER run's rows keyed by path, and the counts are then scored
    against ITS exclusion verdicts rather than this run's own.

    WHY THAT MATTERS. The before run was taken with the probe's first copy of the exclusion rules;
    the probe was then changed to delegate to PressFeedbackScope, so the builder, the probe and the
    regression test could not disagree — and that shared rule set excludes more (every GPS screen
    root, plus the full-screen dismiss catchers). Reporting "244 defects before, 0 after" would
    then be comparing two different populations and quietly flattering the result. Scored under one
    rule set, on the identical 511 paths, it is 242 -> 0.
    """
    rows = js["buttons"]

    def facing(r):
        if scope is None:
            return r["playerFacing"]
        s = scope.get(r["path"])
        return s["playerFacing"] if s else r["playerFacing"]

    pf = [r for r in rows if facing(r)]
    return {
        "total": len(rows),
        "playerFacing": len(pf),
        "excluded": len(rows) - len(pf),
        "covered": len([r for r in pf if r["hasFeedback"]]),
        "defects": len([r for r in pf if not r["hasFeedback"]]),
    }


def c1_table(before: dict, after: dict) -> str:
    """One row per BUTTON PATH, before-verdict and after-verdict side by side.

    Grouped by the prefab/scene it comes from, because that is the unit the builder acts on and
    the unit a reader can check: a reviewer opens one prefab and sees every row this table
    claims for it.
    """
    a = {r["path"]: r for r in after["buttons"]}
    b = {r["path"]: r for r in before["buttons"]}
    # Grouped by the SURFACE the route first saw the button on. Provenance is deliberately not a
    # column here: PrefabUtility reports no prefab membership at runtime (every object answers
    # "(scene)"), so an asset path in a live audit would be a confident fabrication. The authoring
    # side's real paths are in game_polish_c_pressfeedback_authoring.tsv.
    groups: dict[str, list[str]] = {}
    for path in sorted(set(a) | set(b)):
        r = a.get(path) or b[path]
        groups.setdefault(r["firstSeen"], []).append(path)

    out = ["| # | first seen on | button path | source | active | interactable | before | after | verdict |",
           "|---|---|---|---|---|---|---|---|---|"]
    n = 0
    for surface in sorted(groups):
        for path in groups[surface]:
            n += 1
            ra, rb = a.get(path), b.get(path)
            r = ra or rb
            if not r["playerFacing"]:
                verdict = f"excluded — {r['exclusionReason']}"
            elif rb and rb["hasFeedback"]:
                verdict = "already"
            elif ra and ra["hasFeedback"]:
                verdict = "**added**"
            elif ra is None:
                verdict = "not seen in the after run"
            else:
                verdict = "**STILL MISSING**"
            out.append(
                f"| {n} | {surface} | `{path}` | {r['prefab']} | {yn(r['active'])} | "
                f"{yn(r['interactable'])} | {fb(rb)} | {fb(ra)} | {verdict} |")
    return "\n".join(out)


def yn(v) -> str:
    return "yes" if v else "no"


def fb(r) -> str:
    return "—" if r is None else ("has" if r["hasFeedback"] else "none")


# ── C2 ────────────────────────────────────────────────────────────────────────

REF = {"movementType": "Elastic", "elasticity": 0.1, "inertia": True,
       "decelerationRate": 0.135, "scrollSensitivity": 20}


def conforms(r: dict) -> bool:
    return (r["movementType"] == REF["movementType"]
            and abs(r["elasticity"] - REF["elasticity"]) < 1e-4
            and r["inertia"] is True
            and abs(r["decelerationRate"] - REF["decelerationRate"]) < 1e-4
            and abs(r["scrollSensitivity"] - REF["scrollSensitivity"]) < 1e-4)


def c2_counts(js: dict) -> dict:
    rows = js["scrollRects"]
    ins = [r for r in rows if r["inScope"]]
    return {
        "total": len(rows),
        "inScope": len(ins),
        "excluded": len(rows) - len(ins),
        "conforming": len([r for r in ins if conforms(r)]),
        "offReference": len([r for r in ins if not conforms(r)]),
    }


def c2_table(before: dict, after: dict) -> str:
    a = {r["path"]: r for r in after["scrollRects"]}
    b = {r["path"]: r for r in before["scrollRects"]}
    out = ["| # | scroll rect | surface | controller | before (type / sens) | after (type / sens) | verdict |",
           "|---|---|---|---|---|---|---|"]
    for n, path in enumerate(sorted(set(a) | set(b)), 1):
        ra, rb = a.get(path), b.get(path)
        r = ra or rb
        if not r["inScope"]:
            verdict = f"excluded — {r['exclusionReason']}"
        elif rb and conforms(rb):
            verdict = "already at the reference"
        elif ra and conforms(ra):
            verdict = "**set to the reference**"
        else:
            verdict = "**STILL OFF-REFERENCE**"
        out.append(f"| {n} | `{path}` | {r['surface']} | {r['controller'] or '—'} | "
                   f"{sr(rb)} | {sr(ra)} | {verdict} |")
    return "\n".join(out)


def sr(r) -> str:
    if r is None:
        return "—"
    return f"{r['movementType']} / {r['scrollSensitivity']:g}"


# ── C3 ────────────────────────────────────────────────────────────────────────

def c3_counts(js: dict) -> dict:
    rows = js["surfaces"]
    nm = [r for r in rows if r["verdict"].startswith("NOT MEASURED")]
    return {
        "surfaces": len(rows),
        "clear": len([r for r in rows if r["verdict"] == "clear"]),
        "hits": len([r for r in rows if r["verdict"] != "clear" and r not in nm]),
        "notMeasured": len(nm),
    }


def c3_table(js: dict) -> str:
    out = ["| # | surface | measured at | verdict | offending element(s) |",
           "|---|---|---|---|---|"]
    for n, r in enumerate(js["surfaces"], 1):
        bad = [h for h in r["topHits"] + r["bottomHits"] if h.startswith("content")]
        cell = "—" if not bad else "<br>".join("`" + h + "`" for h in bad)
        view = r.get("measuredView", "?") + " / " + r.get("safeAreaSource", "?")
        out.append(f"| {n} | {r['surface']} | {view} | {r['verdict']} | {cell} |")
    return "\n".join(out)


def c3_chrome(js: dict) -> str:
    """The shared chrome, once. It is on every surface, so listing it per surface would bury
    the per-surface verdicts under thirty repetitions of the same two objects."""
    seen: dict[str, str] = {}
    for r in js["surfaces"]:
        for h in r["topHits"] + r["bottomHits"]:
            if h.startswith("chrome") or h.startswith("decorative"):
                key = h.split("[")[0].strip()
                seen.setdefault(key, h)
    out = ["| element | measurement |", "|---|---|"]
    for k in sorted(seen):
        kind, path = k.split(":", 1)
        out.append(f"| `{path.strip()}` | {kind} — {seen[k].split('[')[1].rstrip(']')} |")
    return "\n".join(out)


# ── main ──────────────────────────────────────────────────────────────────────

def emit(before_tag: str, after_tag: str, sim_tag: str) -> None:
    b1, a1 = load("buttons", before_tag), load("buttons", after_tag)
    b2, a2 = load("scrollrects", before_tag), load("scrollrects", after_tag)
    a3 = load("safearea", sim_tag)

    scope = {r["path"]: r for r in a1["buttons"]}
    cb, ca = c1_counts(b1, scope), c1_counts(a1, scope)
    sb, sa = c2_counts(b2), c2_counts(a2)
    v3 = c3_counts(a3)

    print("<!-- GENERATED by Docs/Scripts/game_polish_c_tables.py — do not hand-edit -->")
    print(f"\n### C1 counts\n")
    print(f"| | before | after |\n|---|---|---|")
    for k in ("total", "playerFacing", "excluded", "covered", "defects"):
        print(f"| {k} | {cb[k]} | {ca[k]} |")
    print(f"\n### C2 counts\n")
    print(f"| | before | after |\n|---|---|---|")
    for k in ("total", "inScope", "excluded", "conforming", "offReference"):
        print(f"| {k} | {sb[k]} | {sa[k]} |")
    print(f"\n### C3 counts ({a3['device']}, {a3['view']}, source={a3['safeAreaSource']}, "
          f"top {a3['topInsetPx']:g}px / bottom {a3['bottomInsetPx']:g}px)\n")
    print(f"| surfaces | clear | hits | not measured |\n|---|---|---|---|")
    print(f"| {v3['surfaces']} | {v3['clear']} | {v3['hits']} | {v3.get('notMeasured', 0)} |")

    print("\n### C1 · ButtonPressFeedback, every site\n")
    print(c1_table(b1, a1))
    print("\n### C2 · ScrollRect feel, every site\n")
    print(c2_table(b2, a2))
    print("\n### C3 · Safe area, every surface\n")
    print(c3_table(a3))
    print("\n#### C3 · shared chrome and decoration, measured once\n")
    print(c3_chrome(a3))


def check(report: str, before_tag: str, after_tag: str, sim_tag: str) -> int:
    with open(report) as f:
        text = f.read()
    derived = {}
    a_js = load("buttons", after_tag)
    scope = {r["path"]: r for r in a_js["buttons"]}
    for k, v in c1_counts(load("buttons", before_tag), scope).items():
        derived[f"C1.before.{k}"] = v
    for k, v in c1_counts(a_js, scope).items():
        derived[f"C1.after.{k}"] = v
    for k, v in c2_counts(load("scrollrects", before_tag)).items():
        derived[f"C2.before.{k}"] = v
    for k, v in c2_counts(load("scrollrects", after_tag)).items():
        derived[f"C2.after.{k}"] = v
    for k, v in c3_counts(load("safearea", sim_tag)).items():
        derived[f"C3.{k}"] = v

    print("derived from the JSON:")
    for k in sorted(derived):
        print(f"  {k:26s} {derived[k]}")

    # The gates that must hold, stated as assertions rather than printed for a reader to judge.
    bad = 0
    a1 = c1_counts(load("buttons", after_tag), scope)
    a2 = c2_counts(load("scrollrects", after_tag))
    a3 = c3_counts(load("safearea", sim_tag))
    for name, got, want in (("A1 playerFacing && !hasFeedback", a1["defects"], 0),
                            ("A3 in-scope off-reference", a2["offReference"], 0),
                            ("A4 surfaces with a content hit", a3["hits"], 0),
                            ("A4 surfaces NOT measured on the device", a3.get("notMeasured", 0), 0)):
        ok = got == want
        print(f"{'PASS' if ok else 'FAIL'}  {name}: {got} (want {want})")
        bad += 0 if ok else 1

    # Every integer the report states next to a counting word must be derivable.
    truth = set(derived.values())
    ctx = r"(?:button|buttons|scrollrect|scroll rects?|surface|surfaces|site|sites|defect|defects|" \
          r"covered|excluded|player-facing|conforming|clear|hits?)"
    stale = []
    for m in re.finditer(rf"(\d[\d,]*)\s+{ctx}\b|{ctx}\s*[:=]\s*(\d[\d,]*)", text, re.I):
        raw = m.group(1) or m.group(2)
        n = int(raw.replace(",", ""))
        if n not in truth and n > 3:
            stale.append((n, text[max(0, m.start() - 60):m.end() + 20].replace("\n", " ")))
    if stale:
        print(f"\n{len(stale)} count(s) in the report that the JSON does not produce "
              f"(each needs a human ruling — a legitimate one is e.g. a serialized baseline):")
        for n, ctxt in stale:
            print(f"  {n}: …{ctxt}…")
    else:
        print("\n0 counts in the report that the JSON does not produce.")
    return 1 if bad else 0


def main() -> int:
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    cmd = sys.argv[1]
    if cmd == "emit":
        emit(*(sys.argv[2:5] or ["before", "after", "sim_after"]))
        return 0
    if cmd == "check":
        return check(sys.argv[2], *(sys.argv[3:6] or ["before", "after", "sim_after"]))
    sys.exit(__doc__)


if __name__ == "__main__":
    raise SystemExit(main())
