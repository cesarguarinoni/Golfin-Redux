#!/usr/bin/env python3
"""PreToolUse hook: enforce that IMPLEMENTER_REPORT.md is properly filled before
the Implementer can move STATUS.md forward.

Reads stdin as JSON (Claude Code hook payload). If the tool call is a Write or
Edit on a STATUS.md file inside Docs/Specs/Active/<task>/, AND the new status
would be READY_FOR_SELF_REVIEW or READY_FOR_ARCHITECT_REVIEW, validate that the
sibling IMPLEMENTER_REPORT.md exists and is properly filled.

If validation fails, exit code 2 with reason on stderr, blocking the tool call.
If validation passes, exit code 0 silently.

Validation rules (applied to ALL gating statuses):
1. IMPLEMENTER_REPORT.md must exist alongside STATUS.md.
2. The "Acceptance checklist" table must have at least one row beyond the header.
3. No row in that table may contain literal placeholder strings:
     "<check 1 from spec>", "<check 2>", "<...>",
     "<one sentence...", "<...>", "PASS / FAIL"
4. Every row in the table must have either "PASS" or "FAIL" in the Result column,
   and the Justification column must not be empty/placeholder.
5. The "Screenshot" section must have a valid relative path that points to an
   actual file under the task's screenshots/ folder.
6. Screenshot file must be recent (modified within MAX_SCREENSHOT_AGE_HOURS). This
   prevents stale-screenshot reuse from prior attempts.

Additional rule for READY_FOR_SELF_REVIEW only:
7. NO checklist rows may have Result=FAIL. The Implementer cannot ship with open
   FAILs. If FAILs exist, the only legal transition is to
   READY_FOR_ARCHITECT_REVIEW (escalation), not READY_FOR_SELF_REVIEW (which is
   the happy-path-confident-no-issues route).

Why this asymmetry: the self-reviewer's job is catching false PASSes, not
relitigating known FAILs. If the Implementer ALREADY knows something failed, it
should be surfaced to the architect for a judgment call, not run through the
self-reviewer (which would just FAIL it back, wasting Opus tokens).

Additional rule for ALL gating statuses:
8. If SPEC.md mentions the test runner (`tests-run`, `Test Runner`,
   `EditMode test`, `PlayMode test`), IMPLEMENTER_REPORT.md must contain
   test-result evidence (counts of Total/Passed/Failed/Skipped, or an "N/N PASS"
   summary). This stops the implementer from escalating around the test runner
   to Cesar with "MCP wasn't available" — the test runner is granted to the
   implementer agent only, so the implementer is the only role that can run it.
9. If SPEC.md § Reference cites a Figma frame, screenshots/figma-reference.png
   must exist. This backstops the golfin-implementer.md Step 5a instruction
   that implementer save the Figma frame on activation. If the spec's Figma
   reference is ambiguous/broken, implementer must escalate via STATUS =
   IMPLEMENTER_BLOCKED — NOT proceed without it.
"""
import json
import re
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
ACTIVE_DIR = REPO_ROOT / "Docs" / "Specs" / "Active"

PLACEHOLDER_PATTERNS = [
    r"<check\s*\d*[^>]*>",
    r"<\.\.\.>",
    r"<one sentence",
    r"<path>",
    r"<created /",
    r"PASS / FAIL",
    r"<timestamp>",
]

GATING_STATUSES = {"READY_FOR_SELF_REVIEW", "READY_FOR_ARCHITECT_REVIEW"}

# Screenshot must be modified within this many hours of the STATUS write to
# count as "this session's screenshot." Generous default for Cesar's workflow
# (might step away for hours mid-task), tight enough to catch "reused last
# week's screenshot" cheats.
MAX_SCREENSHOT_AGE_HOURS = 24


def read_payload() -> dict:
    """Claude Code passes hook payload as JSON on stdin."""
    try:
        return json.loads(sys.stdin.read() or "{}")
    except Exception:
        return {}


def get_target_path(payload: dict) -> Path | None:
    """Extract the file path the tool is about to write/edit."""
    tool_input = payload.get("tool_input", {}) or {}
    p = tool_input.get("file_path") or tool_input.get("path")
    if not p:
        return None
    try:
        return Path(p).resolve()
    except Exception:
        return None


def get_target_content(payload: dict) -> str:
    """Extract the new content the tool will write (Write or Edit)."""
    tool_input = payload.get("tool_input", {}) or {}
    # Write tool: 'content' or 'file_text'
    # Edit tool: 'new_string' (we'd need old+new merged, but for STATUS.md
    # which is one line, the new_string IS the new content effectively).
    return (
        tool_input.get("content")
        or tool_input.get("file_text")
        or tool_input.get("new_string")
        or ""
    )


def is_status_md(p: Path) -> bool:
    """True if path is a STATUS.md inside Docs/Specs/Active/<task>/."""
    if p.name != "STATUS.md":
        return False
    try:
        rel = p.relative_to(ACTIVE_DIR)
    except ValueError:
        return False
    parts = rel.parts
    # Must be Active/<task>/STATUS.md, not Active/<task>/sub/STATUS.md or Active/STATUS.md.
    return len(parts) == 2 and parts[0] != "_TEMPLATE"


def parse_table_rows(md: str, section_header: str) -> list[list[str]]:
    """Find a Markdown table under a section header and return its data rows."""
    # Locate the section.
    pattern = re.compile(rf"^#+\s*{re.escape(section_header)}\b.*$", re.MULTILINE)
    m = pattern.search(md)
    if not m:
        return []
    after = md[m.end():]
    # Stop at the next header.
    next_header = re.search(r"^#+\s+\S", after, re.MULTILINE)
    section = after[: next_header.start()] if next_header else after

    rows = []
    for line in section.splitlines():
        line = line.strip()
        if not line.startswith("|"):
            continue
        # Skip the header separator row (---|---|).
        if re.fullmatch(r"\|[\s:|-]+\|", line):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        rows.append(cells)

    # Drop the header row (first row).
    return rows[1:] if len(rows) >= 2 else []


def validate_report(report_path: Path) -> list[str]:
    """Return list of validation errors. Empty list = valid."""
    errors = []
    if not report_path.exists():
        errors.append(f"IMPLEMENTER_REPORT.md not found at {report_path}")
        return errors

    content = report_path.read_text(encoding="utf-8")

    # Rule 2: Acceptance checklist table must have rows.
    checklist_rows = parse_table_rows(content, "Acceptance checklist")
    if not checklist_rows:
        errors.append(
            "IMPLEMENTER_REPORT.md: 'Acceptance checklist' table has no data rows. "
            "Copy the checklist from SPEC.md and fill every line."
        )

    # Rule 3+4: Validate each row.
    for i, row in enumerate(checklist_rows, start=1):
        if len(row) < 3:
            errors.append(f"Checklist row {i}: malformed (need 3 columns: Item, Result, Justification)")
            continue

        item, result, justification = row[0], row[1], row[2]

        # Placeholders.
        for pat in PLACEHOLDER_PATTERNS:
            if re.search(pat, item, re.IGNORECASE) or re.search(pat, justification, re.IGNORECASE):
                errors.append(
                    f"Checklist row {i}: contains placeholder text. Fill with real content."
                )
                break

        # Result must be PASS or FAIL exactly.
        result_clean = result.upper().strip()
        if result_clean not in {"PASS", "FAIL"}:
            errors.append(
                f"Checklist row {i} ('{item[:40]}'): Result is '{result}'; must be PASS or FAIL."
            )

        # Justification must be non-trivial.
        if len(justification.strip()) < 8:
            errors.append(
                f"Checklist row {i} ('{item[:40]}'): Justification too short. "
                "Cite what was measured."
            )

    # Rule 5: Screenshot path must exist.
    # Look for "screenshots/<file>" path under "## Screenshot" section.
    ss_match = re.search(
        r"##\s+Screenshot.*?Captured at:\*\*\s*`([^`]+)`",
        content,
        re.IGNORECASE | re.DOTALL,
    )
    ss_path = None
    if not ss_match:
        errors.append(
            "IMPLEMENTER_REPORT.md: 'Screenshot' section missing or 'Captured at: `path`' line not found."
        )
    else:
        ss_rel = ss_match.group(1).strip()
        # Resolve relative to the task folder.
        candidate = (report_path.parent / ss_rel).resolve()
        if candidate.exists():
            ss_path = candidate
        else:
            # Also try relative to repo root (in case path was given that way).
            alt = (REPO_ROOT / ss_rel).resolve()
            if alt.exists():
                ss_path = alt
            else:
                errors.append(
                    f"Screenshot path '{ss_rel}' does not point to an actual file. "
                    f"Run python .claude/hooks/capture_screenshot.py <task> first."
                )

    # Rule 6: screenshot must be recent.
    if ss_path is not None:
        age_hours = (time.time() - ss_path.stat().st_mtime) / 3600.0
        if age_hours > MAX_SCREENSHOT_AGE_HOURS:
            errors.append(
                f"Screenshot at '{ss_path.name}' is {age_hours:.1f} hours old "
                f"(max allowed: {MAX_SCREENSHOT_AGE_HOURS}h). Capture a fresh "
                f"play-mode screenshot before proceeding. Reusing stale "
                f"screenshots from prior attempts hides regressions."
            )

    return errors


def spec_requires_tests(spec_path: Path) -> bool:
    """True if SPEC.md mentions the Unity test runner.

    Triggers on: 'tests-run', 'Test Runner' (case-insensitive),
    'EditMode test', 'PlayMode test'. Plain words like 'test' or 'testing'
    alone are NOT enough — we want explicit reference to the runner so we
    don't false-positive on every spec that mentions QA.
    """
    if not spec_path.exists():
        return False
    content = spec_path.read_text(encoding="utf-8", errors="ignore")
    patterns = [
        r"tests-run",
        r"Test\s+Runner",
        r"EditMode\s+test",
        r"PlayMode\s+test",
        r"\bTestRunnerApi\b",
    ]
    for pat in patterns:
        if re.search(pat, content, re.IGNORECASE):
            return True
    return False


def report_has_test_evidence(report_path: Path) -> bool:
    """True if the report contains test-runner result evidence.

    Accepts any of these shapes:
      - 'Total: 211' AND 'Passed: 211'  (named counts)
      - 'TotalTests: 211' AND 'PassedTests: 211'  (raw JSON shape)
      - '211/211 PASS' or '211 / 211 pass'  (compact summary)
      - '211 tests pass' / '0 failed' / '0 skipped' (sentence form is OK as long as at least two of these appear)
    """
    if not report_path.exists():
        return False
    content = report_path.read_text(encoding="utf-8", errors="ignore")

    # Compact summary: "N/N PASS" or "N / N pass"
    if re.search(r"\b\d+\s*/\s*\d+\s*(?:tests?\s*)?(?:PASS|pass|passed)\b", content):
        return True

    # Named counts: must have at least Total + Passed (or TotalTests + PassedTests).
    has_total = bool(re.search(r"\bTotal(?:Tests)?\s*[:=]\s*\d+", content, re.IGNORECASE))
    has_passed = bool(re.search(r"\bPassed(?:Tests)?\s*[:=]\s*\d+", content, re.IGNORECASE))
    if has_total and has_passed:
        return True

    # Sentence form fallback: at least two of {N tests pass, N failed, N skipped}.
    sentence_hits = 0
    for pat in [
        r"\b\d+\s+tests?\s+(?:pass|passed)",
        r"\b\d+\s+(?:test\s+)?(?:failure|failed)",
        r"\b\d+\s+(?:test\s+)?(?:skipped|ignored)",
    ]:
        if re.search(pat, content, re.IGNORECASE):
            sentence_hits += 1
    return sentence_hits >= 2


def has_open_fails(report_path: Path) -> bool:
    """True if the Acceptance checklist contains any rows with Result=FAIL."""
    if not report_path.exists():
        return False
    content = report_path.read_text(encoding="utf-8")
    rows = parse_table_rows(content, "Acceptance checklist")
    for row in rows:
        if len(row) < 3:
            continue
        result = row[1].upper().strip()
        # Match exact FAIL, not strings that contain it.
        if result == "FAIL":
            return True
    return False


def spec_requires_figma_reference(spec_path: Path) -> bool:
    """True if SPEC.md has a § Reference section that mentions Figma.

    Detection is conservative: look for a Markdown `## Reference` (or `## Visual
    Reference`, `## References`) heading AND the substring `figma` within that
    section. Non-UI tasks (physics, backend) typically have no Reference
    section, so they're naturally exempt. This is the gate that backstops the
    `golfin-implementer` prompt's Step 5a: if the implementer skips saving
    figma-reference.png and tries to move STATUS to review, this rule blocks
    the transition.
    """
    if not spec_path.exists():
        return False
    content = spec_path.read_text(encoding="utf-8", errors="ignore")
    m = re.search(r"^##\s+(?:Visual\s+)?References?\b.*$", content, re.MULTILINE)
    if not m:
        return False
    after = content[m.end():]
    next_header = re.search(r"^##\s+\S", after, re.MULTILINE)
    section = after[: next_header.start()] if next_header else after
    return "figma" in section.lower()


def figma_reference_present(task_dir: Path) -> bool:
    """True if screenshots/figma-reference.png exists in the task folder."""
    return (task_dir / "screenshots" / "figma-reference.png").exists()


def main() -> int:
    payload = read_payload()
    target = get_target_path(payload)
    if target is None or not is_status_md(target):
        return 0  # not our concern

    new_content = get_target_content(payload)
    new_status = new_content.strip().splitlines()[0].strip() if new_content.strip() else ""
    if new_status not in GATING_STATUSES:
        return 0  # only gate the transition into review

    task_dir = target.parent
    report_path = task_dir / "IMPLEMENTER_REPORT.md"
    spec_path = task_dir / "SPEC.md"

    # Rules 1-6 apply to both gating statuses.
    errors = validate_report(report_path)

    # Rule 7: only READY_FOR_SELF_REVIEW disallows open FAILs.
    # READY_FOR_ARCHITECT_REVIEW is the legitimate escalation path for tasks
    # the Implementer cannot complete on its own.
    if new_status == "READY_FOR_SELF_REVIEW" and has_open_fails(report_path):
        errors.append(
            "Acceptance checklist contains FAIL items. Cannot transition to "
            "READY_FOR_SELF_REVIEW. Either fix the FAILs and re-mark them PASS, "
            "or set STATUS to READY_FOR_ARCHITECT_REVIEW (escalation path). "
            "Self-review is for confident-PASS submissions only."
        )

    # Rule 8: if SPEC.md asks for test-runner verification, the report must
    # contain test-result counts. Applies to BOTH gating statuses — the
    # implementer is the only agent with `tests-run` access, so escalating
    # around the test runner to the architect/Cesar is never valid.
    if spec_requires_tests(spec_path) and not report_has_test_evidence(report_path):
        errors.append(
            "SPEC.md references the Unity test runner (tests-run / Test Runner / "
            "EditMode/PlayMode test) but IMPLEMENTER_REPORT.md has no test-result "
            "evidence. Invoke `mcp__ai-game-developer__tests-run` (or the "
            "TestRunnerApi via script-execute fallback) and append a summary "
            "with Total/Passed/Failed/Skipped counts (or an N/N PASS line) to "
            "the report. Escalating 'MCP wasn't available' is not valid: "
            "tests-run is granted to the implementer agent only — no other role "
            "can run it on your behalf."
        )

    # Rule 9: if SPEC.md § Reference cites a Figma frame, the figma-reference.png
    # must exist before the implementer can transition to a review state. This
    # is the structural backstop for the putter_p1_ui failure mode: implementer
    # captured Game View but never saved the Figma frame, so reviewers had no
    # left-hand pane for the side-by-side diff and rubber-stamped.
    # If the Figma reference itself is ambiguous/broken, the implementer should
    # set STATUS to IMPLEMENTER_BLOCKED (per golfin-implementer.md Step 5a) and
    # surface to Cesar rather than reaching this hook.
    if spec_requires_figma_reference(spec_path) and not figma_reference_present(task_dir):
        errors.append(
            "SPEC.md § Reference cites a Figma frame but "
            "screenshots/figma-reference.png is missing. Save the Figma frame "
            "to the task's screenshots/ folder before moving to review. Use "
            "`mcp__figma__get_design_context` or `get_screenshot` on the node "
            "id in SPEC.md § Reference. If the Figma reference is "
            "missing/ambiguous/broken in the spec, set STATUS to "
            "IMPLEMENTER_BLOCKED and surface to Cesar rather than guessing — "
            "see golfin-implementer.md Step 5a."
        )

    if errors:
        print(
            "BLOCKED: cannot move STATUS to {} - IMPLEMENTER_REPORT.md issues:".format(
                new_status
            ),
            file=sys.stderr,
        )
        print("", file=sys.stderr)
        for e in errors:
            print(f"  - {e}", file=sys.stderr)
        print("", file=sys.stderr)
        if new_status == "READY_FOR_SELF_REVIEW":
            print(
                "Two paths forward: (a) fix the issues and retry, or (b) if you cannot "
                "verify everything yourself, set STATUS to READY_FOR_ARCHITECT_REVIEW "
                "to escalate.",
                file=sys.stderr,
            )
        else:
            print(
                "Fill IMPLEMENTER_REPORT.md properly, then retry the STATUS update.",
                file=sys.stderr,
            )
        return 2  # exit code 2 blocks the tool call in Claude Code

    return 0


if __name__ == "__main__":
    sys.exit(main())
