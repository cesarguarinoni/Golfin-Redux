#!/usr/bin/env python3
"""
C# linter for GOLFIN Unity project.
Catches issues BEFORE pushing — no more back-and-forth compile errors.

Usage:
  python3 QA/lint_cs.py                    # Check all changed .cs files
  python3 QA/lint_cs.py Assets/Code/       # Check specific path
  python3 QA/lint_cs.py --all              # Check ALL .cs files
"""

import sys, os, re, glob
from pathlib import Path

ERRORS = []
WARNINGS = []

def check_file(filepath):
    try:
        content = Path(filepath).read_text(encoding='utf-8-sig')
    except Exception as e:
        ERRORS.append((filepath, 0, f"Cannot read file: {e}"))
        return

    lines = content.split('\n')

    for i, line in enumerate(lines, 1):
        # ── ERRORS (will break compilation) ──

        # Smart/curly quotes
        if re.search(r'[\u2018\u2019\u201C\u201D]', line):
            ERRORS.append((filepath, i, f"Smart quotes found: {line.strip()[:80]}"))

        # Invisible unicode (BOM, zero-width)
        if re.search(r'[\uFEFF\u200B\u200C\u200D]', line):
            ERRORS.append((filepath, i, "Invisible Unicode character (BOM/zero-width)"))

        # Obvious missing semicolons: lines ending with ) or value but no ; { or comment
        stripped = line.rstrip()
        if stripped and not stripped.lstrip().startswith('//') and not stripped.lstrip().startswith('*'):
            # Assignment without semicolon
            if re.match(r'\s+(public|private|protected)\s+\w+\s+\w+\s*=\s*[^;{]+$', stripped):
                if not stripped.endswith(',') and not stripped.endswith('{'):
                    WARNINGS.append((filepath, i, f"Possible missing semicolon: {stripped.strip()[:80]}"))

        # ── WARNINGS (deprecated/bad practice) ──

        # Deprecated TMP APIs
        if 'enableWordWrapping' in line and '//' not in line.split('enableWordWrapping')[0]:
            WARNINGS.append((filepath, i, "Deprecated: enableWordWrapping → use textWrappingMode"))

        if '.enableAutoSizing' in line and '//' not in line.split('enableAutoSizing')[0]:
            WARNINGS.append((filepath, i, "Deprecated: enableAutoSizing → use enableAutoSizing (check TMP version)"))

        # Common Unity typos
        typos = {
            'Destory': 'Destroy', 'GetCompoent': 'GetComponent',
            'AddCompoent': 'AddComponent', 'SetActve': 'SetActive',
            'gameObect': 'gameObject', 'tranform': 'transform',
            'Instantate': 'Instantiate', 'destory': 'destroy',
        }
        for wrong, right in typos.items():
            if wrong in line:
                WARNINGS.append((filepath, i, f"Typo: '{wrong}' → '{right}'"))

        # Debug.Log left in non-editor code
        if 'Debug.Log' in line and '/Editor/' not in filepath and '//' not in line.split('Debug.Log')[0]:
            pass  # Allow for now, but could flag in production

    # Brace balance check
    open_count = content.count('{')
    close_count = content.count('}')
    if open_count != close_count:
        ERRORS.append((filepath, 0, f"Unbalanced braces: {{ ={open_count} }} ={close_count}"))

    # Check for common C# patterns that indicate issues
    # Empty catch blocks
    for m in re.finditer(r'catch\s*\([^)]*\)\s*\{\s*\}', content):
        line_num = content[:m.start()].count('\n') + 1
        WARNINGS.append((filepath, line_num, "Empty catch block — swallowing exceptions"))


def main():
    args = sys.argv[1:]

    if '--all' in args:
        files = glob.glob('**/*.cs', recursive=True)
    elif args:
        path = args[0]
        if os.path.isdir(path):
            files = glob.glob(f'{path}/**/*.cs', recursive=True)
        else:
            files = [path]
    else:
        # Check git diff
        import subprocess
        try:
            result = subprocess.run(
                ['git', 'diff', '--name-only', 'HEAD~1', 'HEAD', '--', '*.cs'],
                capture_output=True, text=True
            )
            files = [f for f in result.stdout.strip().split('\n') if f and os.path.exists(f)]
        except:
            files = glob.glob('Assets/**/*.cs', recursive=True)

    if not files:
        print("No C# files to check.")
        return 0

    print(f"Checking {len(files)} C# file(s)...\n")

    for f in files:
        check_file(f)

    # Report
    if ERRORS:
        print(f"❌ ERRORS ({len(ERRORS)}):")
        for filepath, line, msg in ERRORS:
            loc = f":{line}" if line else ""
            print(f"  {filepath}{loc} — {msg}")
        print()

    if WARNINGS:
        print(f"⚠️  WARNINGS ({len(WARNINGS)}):")
        for filepath, line, msg in WARNINGS:
            print(f"  {filepath}:{line} — {msg}")
        print()

    if not ERRORS and not WARNINGS:
        print("✅ All checks passed!")
        return 0

    if ERRORS:
        print(f"🚫 {len(ERRORS)} error(s) must be fixed before pushing.")
        return 1

    print(f"✅ No errors. {len(WARNINGS)} warning(s) to review.")
    return 0


if __name__ == '__main__':
    sys.exit(main())
