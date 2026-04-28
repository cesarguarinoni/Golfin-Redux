#!/usr/bin/env python3
"""Copy the most recent screenshot from Assets/Screenshots/ into the task folder.

Usage: python .claude/hooks/capture_screenshot.py <task_slug> [--max-age-minutes N]

Reads the latest .jpg or .png from Assets/Screenshots/ (the project's standard
Unity screenshot output, captured via GOLFIN > Screenshot > Capture Game View
or via Unity MCP screenshot-game-view skill).

Copies it into Docs/Specs/Active/<task_slug>/screenshots/<timestamp>.jpg|png
so the self-reviewer and architect can read it from a stable, per-task path.

Returns the destination path on stdout (the Implementer pastes it into
IMPLEMENTER_REPORT.md section: Screenshot).

Fails loudly if the latest screenshot is older than --max-age-minutes (default 30).
This prevents accidental reuse of a stale screenshot from a prior session.
Override with --max-age-minutes if you have a legitimate reason.
"""
import sys
import shutil
import time
from pathlib import Path
from datetime import datetime

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SRC_DIR = REPO_ROOT / "Assets" / "Screenshots"
DEST_BASE = REPO_ROOT / "Docs" / "Specs" / "Active"
DEFAULT_MAX_AGE_MINUTES = 30


def parse_args(argv):
    """Return (task_slug, max_age_minutes). Exits on usage errors."""
    if len(argv) < 2:
        print("ERROR: Usage: capture_screenshot.py <task_slug> [--max-age-minutes N]", file=sys.stderr)
        sys.exit(2)

    task_slug = argv[1]
    max_age = DEFAULT_MAX_AGE_MINUTES

    i = 2
    while i < len(argv):
        if argv[i] == "--max-age-minutes" and i + 1 < len(argv):
            try:
                max_age = int(argv[i + 1])
            except ValueError:
                print(f"ERROR: --max-age-minutes requires an integer, got '{argv[i + 1]}'", file=sys.stderr)
                sys.exit(2)
            i += 2
        else:
            print(f"ERROR: unknown argument '{argv[i]}'", file=sys.stderr)
            sys.exit(2)

    return task_slug, max_age


def main():
    task_slug, max_age_minutes = parse_args(sys.argv)

    task_folder = DEST_BASE / task_slug
    if not task_folder.exists():
        print(f"ERROR: task folder not found: {task_folder}", file=sys.stderr)
        print("Did you create the per-task folder before running this?", file=sys.stderr)
        return 2

    if not SRC_DIR.exists():
        print(f"ERROR: source folder not found: {SRC_DIR}", file=sys.stderr)
        return 2

    # Find the most recent screenshot in Assets/Screenshots/
    candidates = []
    for ext in ("*.jpg", "*.png"):
        candidates.extend(SRC_DIR.glob(ext))

    if not candidates:
        print(f"ERROR: no screenshots found in {SRC_DIR}", file=sys.stderr)
        print("Capture one first via:", file=sys.stderr)
        print("  - Unity menu: GOLFIN > Screenshot > Capture Game View, OR", file=sys.stderr)
        print("  - Unity MCP skill: screenshot-game-view, OR", file=sys.stderr)
        print("  - Unity MCP skill: script-execute with ScreenshotTool.CaptureGameView()", file=sys.stderr)
        return 2

    latest = max(candidates, key=lambda p: p.stat().st_mtime)

    # Age check: refuse to copy a stale screenshot. This catches the failure
    # mode where a screenshot capture silently failed and the script picks up
    # last week's screenshot from the same folder.
    age_seconds = time.time() - latest.stat().st_mtime
    age_minutes = age_seconds / 60.0
    if age_minutes > max_age_minutes:
        print(
            f"ERROR: latest screenshot is {age_minutes:.1f} minutes old "
            f"(max allowed: {max_age_minutes}). The screenshot capture likely failed silently.",
            file=sys.stderr,
        )
        print(f"  Latest file: {latest.name}", file=sys.stderr)
        print(
            f"  Modified: {datetime.fromtimestamp(latest.stat().st_mtime).strftime('%Y-%m-%d %H:%M:%S')}",
            file=sys.stderr,
        )
        print("", file=sys.stderr)
        print("Capture a fresh screenshot first, then re-run this helper.", file=sys.stderr)
        print(
            "If this is a legitimate use of an older screenshot, override with --max-age-minutes <N>.",
            file=sys.stderr,
        )
        return 2

    # Destination: screenshots/<timestamp>.<ext> (using current time, not source mtime,
    # so iterations come out in obvious chronological order).
    dest_dir = task_folder / "screenshots"
    dest_dir.mkdir(parents=True, exist_ok=True)
    timestamp = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
    dest = dest_dir / f"{timestamp}{latest.suffix}"

    shutil.copy2(latest, dest)

    # Output the destination path so the Implementer can quote it in the report.
    # Use forward slashes so it's clean to paste into Markdown regardless of platform.
    rel = dest.relative_to(REPO_ROOT).as_posix()
    print(rel)
    return 0


if __name__ == "__main__":
    sys.exit(main())
