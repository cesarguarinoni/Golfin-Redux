#!/usr/bin/env python3
"""Check that every file a spec report cites actually exists.

WHY THIS EXISTS. `game_polish_a` was failed at the red-team gate three times for one shape: a
report whose NARRATIVE drifted out of sync with the evidence it cited. Twice the implementer
"swept" for it by reading headings, declared the sweep complete, and missed citations sitting in
the body of sections that sweep had just certified — a dead clip name kept after a rename, and a
metrics block quoting a superseded run on the line above the JSON that contradicted it.

Reading does not catch this. A script does.

    python3 Docs/Scripts/check_report_citations.py Docs/Specs/Active/<task>/IMPLEMENTER_REPORT.md

Exits non-zero if any backticked path fails to resolve. Sections under a heading containing
"SUPERSEDED" are skipped: they describe history on purpose, and are required to be marked as such.
"""
from __future__ import annotations
import os, re, subprocess, sys

EXT = r"(?:png|jpg|jpeg|mp4|mov|webm|json|txt|cs|md|py|prefab|unity|asset|csv)"


def resolve(cited: str, task_dir: str) -> bool:
    if os.path.exists(cited):
        return True
    base = os.path.basename(cited)
    for sub in ("screenshots", "videos", "reference", ""):
        cand = os.path.join(task_dir, sub, base) if sub else os.path.join(task_dir, base)
        if os.path.exists(cand):
            return True
    found = subprocess.run(
        ["bash", "-lc", f'find Assets Docs -name {base!r} -not -path "*/Library/*" 2>/dev/null | head -1'],
        capture_output=True, text=True).stdout.strip()
    return bool(found)


def strip_superseded(text: str) -> str:
    """Drop every section whose heading says SUPERSEDED, up to the next same-or-higher heading."""
    out, skip_level = [], None
    for line in text.split("\n"):
        m = re.match(r"^(#{2,4})\s+(.*)", line)
        if m:
            level, title = len(m.group(1)), m.group(2)
            if skip_level is not None and level <= skip_level:
                skip_level = None
            if "SUPERSEDED" in title.upper() or title.lower().startswith("(superseded)"):
                skip_level = level
                continue
        if skip_level is None:
            out.append(line)
    return "\n".join(out)


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    report = sys.argv[1]
    task_dir = os.path.dirname(report)
    text = strip_superseded(open(report, encoding="utf-8").read())

    raw = set(re.findall(rf"`([^`\n]*\.{EXT})`", text))

    # Only score things that are actually FILE NAMES. Reports legitimately contain glob patterns
    # ("parity_{anim,instant}_NN_<label>.png"), bare extensions (".prefab") and mid-name fragments
    # left by an ellipsis ("…_f_cross_backdrop.mp4"); none of those are claims about a file that
    # exists, and scoring them buries the two real misses in eight false ones.
    def is_a_filename(c: str) -> bool:
        if any(ch in c for ch in "{}*<>?"):
            return False
        base = os.path.basename(c)
        if base.startswith((".", "_", "\u2026")):
            return False
        return bool(re.match(r"^[A-Za-z0-9][A-Za-z0-9_.\-/]*$", c))

    cited = sorted(c for c in raw if is_a_filename(c))
    skipped = sorted(raw - set(cited))
    missing = [c for c in cited if not resolve(c, task_dir)]

    if skipped:
        print(f"  (ignored {len(skipped)} pattern/fragment tokens, not file claims)")

    for c in missing:
        print(f"  MISSING  {c}")
    print(f"\n{len(cited)} cited, {len(missing)} unresolved  ({report})")
    if missing:
        print("\nA citation that resolves to nothing is a claim with no evidence behind it.")
    return 1 if missing else 0


if __name__ == "__main__":
    raise SystemExit(main())
