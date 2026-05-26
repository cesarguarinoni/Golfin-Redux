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

Rules 10–12 are the green_authoring_editor_tool scar-tissue rules
(Cesar-approved 2026-05-26). They enforce baseline attribution and synthetic-
capture detection that the iter-2/iter-3 of that task only caught by hand:

10. **Pre-flight baseline block in HEARTBEAT.log.** Every iter-N kickoff must
    append a baseline block. Missing/malformed block blocks the STATUS write.
    See `feedback_preflight_baseline_attribution.md` in user memory.
11. **"Pre-existing" claims require baseline citation.** Any IMPLEMENTER_REPORT
    line mentioning "pre-existing", "from previous session", "predates this",
    "not introduced by", or "was already in" must have a backticked / fenced
    citation within ±5 lines that quotes a path from the baseline DIRTY block.
12. **Synthetic flat-color frame detection.** Every PNG/JPG referenced in the
    report under `screenshots/` is variance-sampled (Pillow, falling back to a
    manual stdev pass). Variance < 5.0 on a ≥10000-pixel sample = synthetic
    fabrication; blocks the STATUS write with file path + variance.
"""
import json
import math
import re
import struct
import sys
import time
import zlib
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

# Rule 11 — phrases that trigger the "show your baseline" requirement. Each
# occurrence in IMPLEMENTER_REPORT.md must be accompanied by a citation within
# ±5 lines that quotes a path from the iter-N baseline DIRTY block. The first
# tuple element is the human-readable label used in error messages; the second
# is the compiled regex that matches the phrase variants we treat as triggers.
PREEXISTING_TRIGGER_PATTERNS: list[tuple[str, re.Pattern]] = [
    ("pre-existing", re.compile(r"pre[-\s]?existing", re.IGNORECASE)),
    ("from (a) previous session", re.compile(r"from\s+(?:a\s+)?previous\s+session", re.IGNORECASE)),
    ("not introduced by", re.compile(r"not\s+introduced\s+by", re.IGNORECASE)),
    ("predates this", re.compile(r"predates?\s+this", re.IGNORECASE)),
    ("was already in", re.compile(r"was\s+already\s+in", re.IGNORECASE)),
]

# Rule 12 — variance threshold. Variance < this on a sample patch ≥ MIN_SAMPLE_PIXELS
# means the frame is effectively a single colour and almost certainly fabricated
# (iter-2 of green_authoring shipped a uniform-grey PNG; this would have caught it).
VARIANCE_THRESHOLD = 5.0
MIN_SAMPLE_PIXELS = 10_000


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
    # Find the "## Screenshot" (or "## Screenshots") section and pull the FIRST
    # backticked `screenshots/X.png` path out of it. We used to require a
    # "Captured at: `path`" prefix but iter-4 of green_authoring uses
    # "Canonical frame for visual review: `path`" instead; the substantive rule
    # is "section exists AND first path under it points to a real file."
    ss_path = None
    section_m = re.search(
        r"^##\s+Screenshots?\b.*$",
        content,
        re.MULTILINE,
    )
    if not section_m:
        errors.append(
            "IMPLEMENTER_REPORT.md: '## Screenshot' (or 'Screenshots') section "
            "missing. Add a section that names at least one canonical capture "
            "as `screenshots/<file>.png`."
        )
    else:
        section_text = content[section_m.end():]
        next_h = re.search(r"^##\s+\S", section_text, re.MULTILINE)
        section_text = section_text[: next_h.start()] if next_h else section_text
        ss_in_section = re.search(r"`(screenshots/[^`]+)`", section_text)
        if not ss_in_section:
            errors.append(
                "IMPLEMENTER_REPORT.md: '## Screenshot' section contains no "
                "backticked `screenshots/<file>` path. Name the canonical frame."
            )
        else:
            ss_rel = ss_in_section.group(1).strip()
            candidate = (report_path.parent / ss_rel).resolve()
            if candidate.exists():
                ss_path = candidate
            else:
                alt = (REPO_ROOT / ss_rel).resolve()
                if alt.exists():
                    ss_path = alt
                else:
                    errors.append(
                        f"Screenshot path '{ss_rel}' does not point to an "
                        f"actual file. Run python "
                        f".claude/hooks/capture_screenshot.py <task> first."
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
      - '362 total / 359 PASS / 0 FAIL / 3 SKIP' (iter-4 green_authoring idiom)
      - '362 total (359 pass, 0 fail, 3 skip)' (parenthesised variant)
      - '362 total' + '359 pass' (named counts without colon)
    """
    if not report_path.exists():
        return False
    content = report_path.read_text(encoding="utf-8", errors="ignore")

    # Compact summary: "N/N PASS" or "N / N pass"
    if re.search(r"\b\d+\s*/\s*\d+\s*(?:tests?\s*)?(?:PASS|pass|passed)\b", content):
        return True

    # Iter-4 idiom: "N total / N PASS / N FAIL / N SKIP" or
    # "N total (N pass, N fail, N skip)". One regex covers both shapes.
    if re.search(
        r"\b\d+\s+total\s*(?:[/,]|\()\s*\d+\s+(?:PASS|pass|passed)\b",
        content,
        re.IGNORECASE,
    ):
        return True

    # Named counts: must have at least Total + Passed (or TotalTests + PassedTests).
    # Accept both 'Total: N' / 'Total = N' and the colon-less 'N total' / 'N passed'.
    has_total = bool(re.search(r"\bTotal(?:Tests)?\s*[:=]\s*\d+", content, re.IGNORECASE)) \
        or bool(re.search(r"\b\d+\s+total\b", content, re.IGNORECASE))
    has_passed = bool(re.search(r"\bPassed(?:Tests)?\s*[:=]\s*\d+", content, re.IGNORECASE)) \
        or bool(re.search(r"\b\d+\s+(?:PASS|pass|passed)\b", content))
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


# ─────────────────────────────────────────────────────────────────────────────
# Rule 10–12 helpers: baseline attribution + synthetic-frame detection.
# Scar-tissue from green_authoring_editor_tool (2026-05-26). See module
# docstring and feedback_preflight_baseline_attribution.md (user memory).
# ─────────────────────────────────────────────────────────────────────────────

def extract_iteration_number(report_text: str) -> int | None:
    """Derive the current iteration number from IMPLEMENTER_REPORT.md content.

    Looks for these patterns (case-insensitive), in order of specificity:
      - `**Iteration:** N` (or `**Iteration**: N`)
      - `Iteration: N`
      - `iter-N` near the top of the document (first 40 lines)

    Returns None if no iteration mention found. Caller should default to
    requiring ANY valid baseline block (latest one wins) in that case so
    legacy reports without an iteration marker still get baseline coverage.
    """
    # Pattern 1 + 2: explicit Iteration: N (with or without bold).
    m = re.search(
        r"\*{0,2}\s*Iteration\s*\*{0,2}\s*:?\s*\*{0,2}\s*(\d+)",
        report_text,
        re.IGNORECASE,
    )
    if m:
        return int(m.group(1))
    # Pattern 3: iter-N near the top.
    head = "\n".join(report_text.splitlines()[:40])
    m = re.search(r"\biter[-_]?(\d+)\b", head, re.IGNORECASE)
    if m:
        return int(m.group(1))
    return None


# Baseline block markers — flexible enough to allow the surrounding whitespace
# variants Cesar's heartbeat scripts have produced, but strict on structure.
_BASELINE_HEADER_RE = re.compile(
    r"^={3,}\s*iter-(\d+)\s+kickoff\s+baseline\s+([^\s=][^\n]*?)\s*={3,}\s*$",
    re.IGNORECASE | re.MULTILINE,
)
_BASELINE_FOOTER_RE = re.compile(r"^={3,}\s*END\s+baseline\s*={3,}\s*$", re.IGNORECASE | re.MULTILINE)


def parse_baseline_blocks(heartbeat_text: str) -> list[dict]:
    """Parse every valid baseline block in HEARTBEAT.log.

    Returns a list (in source order) of dicts with keys:
      - 'iter' (int)
      - 'timestamp' (str, freeform — usually ISO-ish)
      - 'head' (str — 40-char SHA, lowercased)
      - 'dirty_lines' (list[str] — raw lines as they appeared)
      - 'dirty_paths' (set[str] — file paths only, leading status code stripped)
      - 'start' (int char offset of the header)
      - 'end' (int char offset just past the footer)

    Malformed blocks (missing HEAD, missing DIRTY:, missing END marker) are
    skipped, NOT raised — the validator wraps this and reports the absence as
    a single human-readable error.
    """
    blocks = []
    for header_m in _BASELINE_HEADER_RE.finditer(heartbeat_text):
        iter_n = int(header_m.group(1))
        timestamp = header_m.group(2).strip()
        # Find the next END marker after this header.
        footer_m = _BASELINE_FOOTER_RE.search(heartbeat_text, header_m.end())
        if not footer_m:
            continue
        body = heartbeat_text[header_m.end():footer_m.start()]
        # HEAD line.
        head_m = re.search(r"^\s*HEAD:\s*([0-9a-fA-F]{7,40})\s*$", body, re.MULTILINE)
        if not head_m:
            continue
        head_sha = head_m.group(1).lower()
        # DIRTY: marker.
        dirty_m = re.search(r"^\s*DIRTY:\s*$", body, re.MULTILINE)
        if not dirty_m:
            continue
        dirty_body = body[dirty_m.end():]
        dirty_lines: list[str] = []
        dirty_paths: set[str] = set()
        for raw in dirty_body.splitlines():
            stripped = raw.rstrip()
            if not stripped.strip():
                continue
            # Skip nested markers if any (shouldn't be there).
            if re.match(r"^={3,}", stripped.strip()):
                break
            dirty_lines.append(stripped)
            # Strip the porcelain status code (1-2 chars + space) when present.
            # Accept ' M Assets/...', '?? Assets/...', 'M  Assets/...', 'A  Assets/...', etc.
            path_m = re.match(
                r"^\s*(?:[?!MADRCU ]{1,2}\s+|[?!MADRCU]{2}\s+)?(.+?)\s*$",
                stripped,
            )
            if path_m and path_m.group(1):
                dirty_paths.add(path_m.group(1).strip())
        blocks.append({
            "iter": iter_n,
            "timestamp": timestamp,
            "head": head_sha,
            "dirty_lines": dirty_lines,
            "dirty_paths": dirty_paths,
            "start": header_m.start(),
            "end": footer_m.end(),
        })
    return blocks


def validate_baseline(
    heartbeat_path: Path,
    expected_iter: int | None,
) -> tuple[list[str], dict | None]:
    """Rule 10: ensure HEARTBEAT.log contains a baseline block for the iter.

    Returns (errors, baseline_dict). When errors is non-empty, baseline_dict
    may still be populated with the latest-found block for downstream rules
    to operate on best-effort (Rule 11 needs the DIRTY paths).
    """
    errors: list[str] = []
    if not heartbeat_path.exists():
        errors.append(
            "HEARTBEAT.log not found. Every iteration must append a kickoff "
            "baseline block before any STATUS=READY_FOR_*_REVIEW write. "
            "See feedback_preflight_baseline_attribution.md (user memory)."
        )
        return errors, None

    text = heartbeat_path.read_text(encoding="utf-8", errors="ignore")
    blocks = parse_baseline_blocks(text)
    if not blocks:
        errors.append(
            "HEARTBEAT.log contains no valid '=== iter-N kickoff baseline ... ==='\n"
            "    block. Required structure:\n"
            "      === iter-N kickoff baseline <ISO timestamp> ===\n"
            "      HEAD: <40-char SHA>\n"
            "      DIRTY:\n"
            "      <git status --porcelain lines, zero or more>\n"
            "      === END baseline ===\n"
            "    See feedback_preflight_baseline_attribution.md (user memory)."
        )
        return errors, None

    if expected_iter is not None:
        match = next((b for b in blocks if b["iter"] == expected_iter), None)
        if match is None:
            iters_found = ", ".join(f"iter-{b['iter']}" for b in blocks)
            errors.append(
                f"HEARTBEAT.log has baseline blocks for [{iters_found}] but no "
                f"block for iter-{expected_iter}. IMPLEMENTER_REPORT.md claims "
                f"iteration {expected_iter}; the matching kickoff baseline "
                "must be appended to HEARTBEAT.log before STATUS can move to "
                "review. See feedback_preflight_baseline_attribution.md."
            )
            # Fall back to the latest block so Rule 11 can still cite something.
            return errors, blocks[-1]
        return errors, match

    # No iteration in report — accept latest block as the active baseline.
    return errors, blocks[-1]


def _find_code_spans(text: str) -> list[tuple[int, int, str]]:
    """Return (start_line, end_line, content) for every code span in `text`.

    Captures BOTH inline backtick spans (`like-this`) and fenced ``` blocks.
    Line numbers are 0-indexed against `text.splitlines(keepends=False)`.

    Used by Rule 11 to look for baseline citations within ±5 lines of a
    trigger phrase. Inline backticks are accepted as "citations" because
    iter-4 of green_authoring uses that form in its table-cell narrative;
    the user explicitly wants iter-4 to pass.
    """
    spans: list[tuple[int, int, str]] = []
    lines = text.splitlines()

    # 1) Fenced code blocks.
    in_fence = False
    fence_start_line = -1
    fence_buf: list[str] = []
    for i, line in enumerate(lines):
        stripped = line.lstrip()
        if stripped.startswith("```"):
            if not in_fence:
                in_fence = True
                fence_start_line = i
                fence_buf = []
            else:
                # Close the fence; emit one span spanning all body lines.
                spans.append((fence_start_line, i, "\n".join(fence_buf)))
                in_fence = False
                fence_buf = []
            continue
        if in_fence:
            fence_buf.append(line)
    # Unclosed fence — treat the rest of the file as a span anyway.
    if in_fence:
        spans.append((fence_start_line, len(lines) - 1, "\n".join(fence_buf)))

    # 2) Inline backticks. Find them line-by-line but skip lines that fall
    # inside a fenced block (already covered).
    fenced_lines: set[int] = set()
    for s, e, _ in spans:
        for j in range(s, e + 1):
            fenced_lines.add(j)
    inline_re = re.compile(r"`([^`\n]+)`")
    for i, line in enumerate(lines):
        if i in fenced_lines:
            continue
        for m in inline_re.finditer(line):
            spans.append((i, i, m.group(1)))

    return spans


def validate_preexisting_claims(report_text: str, dirty_paths: set[str]) -> list[str]:
    """Rule 11: every 'pre-existing'-style claim needs a sourced citation.

    For each trigger phrase occurrence, look at lines [N-5, N+5] for a code
    span (fenced or inline backticks) that contains at least one DIRTY path.
    No citation found = the claim is unsourced and FAILs the hook.
    """
    errors: list[str] = []
    if not dirty_paths:
        # Without a baseline (Rule 10 already errored), we can't enforce
        # citations meaningfully. Bail — Rule 10's error covers this case.
        return errors

    lines = report_text.splitlines()
    code_spans = _find_code_spans(report_text)

    # Pre-compute, for each line, the union of code-span text in window [N-5, N+5].
    # Approach: index code spans by line range, then per trigger line collect
    # all spans that touch the window.
    for trigger_label, regex in PREEXISTING_TRIGGER_PATTERNS:
        for line_idx, line in enumerate(lines):
            if not regex.search(line):
                continue
            # Collect all code-span text whose [start, end] intersects [line-5, line+5].
            window_lo = line_idx - 5
            window_hi = line_idx + 5
            joined_spans: list[str] = []
            for s, e, content in code_spans:
                if e < window_lo or s > window_hi:
                    continue
                joined_spans.append(content)
            joined_text = "\n".join(joined_spans)

            cited = False
            for path in dirty_paths:
                if path and path in joined_text:
                    cited = True
                    break

            if not cited:
                # Extract a short excerpt of the offending phrase for the error.
                excerpt = line.strip()
                if len(excerpt) > 120:
                    excerpt = excerpt[:117] + "..."
                errors.append(
                    f"IMPLEMENTER_REPORT.md line {line_idx + 1}: \"{trigger_label}\" "
                    f"claim is UNSOURCED. Within ±5 lines, no backticked or fenced "
                    f"code span contains a path from the baseline DIRTY block. "
                    f"Offending line: \"{excerpt}\". Quote the matching baseline "
                    f"line (e.g. ` M path/to/file`) inside backticks within "
                    f"5 lines, OR remove the 'pre-existing' attribution. "
                    f"See feedback_preflight_baseline_attribution.md."
                )
    return errors


def _extract_image_paths(report_text: str) -> set[str]:
    """Find every PNG/JPG path mentioned in the report under screenshots/.

    Matches both relative-style (`screenshots/foo.png`) and
    task-rooted-style (`Docs/Specs/Active/<task>/screenshots/foo.png`).
    Returns a set of path strings (de-duplicated).
    """
    pattern = re.compile(
        r"(?:Docs/Specs/(?:Active|Completed)/[\w.\-]+/)?screenshots/[\w./\-]+\.(?:png|jpg|jpeg)",
        re.IGNORECASE,
    )
    return {m.group(0) for m in pattern.finditer(report_text)}


def _resolve_image(report_path: Path, ss_rel: str) -> Path | None:
    """Resolve a screenshot path mentioned in the report to an absolute file."""
    candidate = (report_path.parent / ss_rel).resolve()
    if candidate.exists():
        return candidate
    alt = (REPO_ROOT / ss_rel).resolve()
    if alt.exists():
        return alt
    return None


def _variance_pillow(img_path: Path) -> float | None:
    """Compute variance using Pillow. Returns None if Pillow unavailable.

    Samples a 3x3 grid of patches across the image and returns the MAX
    variance across them. The grid sampling guards against the case where
    the geometric centre happens to land on a uniform region (e.g. the
    empty editor canvas between sidebars in iter-4's step3 polygon
    capture — a real frame whose dead-centre 100x100 patch is variance 0
    but whose off-centre regions are clearly non-synthetic).
    Each patch is sized to satisfy MIN_SAMPLE_PIXELS individually.
    """
    try:
        from PIL import Image, ImageStat  # type: ignore
    except Exception:
        return None
    try:
        img = Image.open(img_path)
        img.load()
    except Exception:
        return None
    w, h = img.size
    side = max(int(math.sqrt(MIN_SAMPLE_PIXELS)), 100)
    if w < side or h < side:
        # Image is tiny — fall back to a single full-image sample.
        gs = img.convert("L")
        stat = ImageStat.Stat(gs)
        return float(stat.stddev[0]) ** 2

    half = side // 2
    # Anchor points: 9 positions on a 3x3 grid (¼, ½, ¾ of each axis).
    xs = [w // 4, w // 2, 3 * w // 4]
    ys = [h // 4, h // 2, 3 * h // 4]
    max_var = 0.0
    for cy in ys:
        for cx in xs:
            left = max(0, min(w - side, cx - half))
            top = max(0, min(h - side, cy - half))
            patch = img.crop((left, top, left + side, top + side))
            gs = patch.convert("L")
            stat = ImageStat.Stat(gs)
            var = float(stat.stddev[0]) ** 2
            if var > max_var:
                max_var = var
    return max_var


def _variance_fallback(img_path: Path) -> float | None:
    """Manual stdev fallback when Pillow isn't installed.

    Decodes uncompressed PNG IDAT chunks well enough to sample greyscale
    variance. For JPEGs (or any image we can't decode here), returns None
    and the validator treats that as "couldn't sample, skip".
    """
    try:
        data = img_path.read_bytes()
    except Exception:
        return None
    # Only attempt PNG fallback (JPEG decoding without a library is intractable).
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        return None
    try:
        # PNG: skip 8-byte signature, read chunks.
        i = 8
        width = height = 0
        bit_depth = 0
        color_type = 0
        idat = bytearray()
        while i < len(data):
            chunk_len = struct.unpack(">I", data[i:i + 4])[0]
            chunk_type = data[i + 4:i + 8]
            chunk_data = data[i + 8:i + 8 + chunk_len]
            if chunk_type == b"IHDR":
                width = struct.unpack(">I", chunk_data[0:4])[0]
                height = struct.unpack(">I", chunk_data[4:8])[0]
                bit_depth = chunk_data[8]
                color_type = chunk_data[9]
            elif chunk_type == b"IDAT":
                idat.extend(chunk_data)
            elif chunk_type == b"IEND":
                break
            i += 12 + chunk_len  # 4 len + 4 type + data + 4 CRC
        if not idat or width == 0 or height == 0 or bit_depth != 8:
            return None
        # Channels per pixel for the supported color types.
        # 0=grey,2=RGB,3=palette(unsupported here),4=greyA,6=RGBA
        channels = {0: 1, 2: 3, 4: 2, 6: 4}.get(color_type)
        if channels is None:
            return None
        raw = zlib.decompress(bytes(idat))
        stride = width * channels + 1  # +1 for filter byte per scanline
        if len(raw) < stride * height:
            return None
        # Reconstruct greyscale samples — for variance, we only need a rough
        # luminance estimate, so we approximate by averaging the colour channels
        # of each pixel. We ignore PNG filter types (treat filter-byte as 0
        # everywhere) because we only need variance, not pixel fidelity. For
        # filter type 0 ("None") this is exact; for other types it inflates
        # variance, which is acceptable — we only block on VERY low variance
        # so a noisier estimate just makes the check more conservative.
        samples: list[int] = []
        step = max(1, (width * height) // MIN_SAMPLE_PIXELS)
        idx = 0
        for row in range(height):
            row_start = row * stride + 1
            for col in range(width):
                if idx % step != 0:
                    idx += 1
                    continue
                idx += 1
                px_off = row_start + col * channels
                if channels == 1:
                    samples.append(raw[px_off])
                elif channels == 2:
                    samples.append(raw[px_off])
                else:
                    r, g, b = raw[px_off], raw[px_off + 1], raw[px_off + 2]
                    samples.append((r + g + b) // 3)
                if len(samples) >= MIN_SAMPLE_PIXELS:
                    break
            if len(samples) >= MIN_SAMPLE_PIXELS:
                break
        if len(samples) < 100:
            return None
        mean = sum(samples) / len(samples)
        var = sum((s - mean) ** 2 for s in samples) / len(samples)
        return float(var)
    except Exception:
        return None


def compute_image_variance(img_path: Path) -> float | None:
    """Variance of a small grey-scale patch from the centre of the image.

    Tries Pillow first; falls back to a manual PNG-decode path. Returns
    None when neither route can read the file (e.g. JPEG with no Pillow).
    The validator treats None as "skip, can't sample" — it does NOT block.
    """
    v = _variance_pillow(img_path)
    if v is not None:
        return v
    return _variance_fallback(img_path)


def validate_image_variances(report_path: Path) -> list[str]:
    """Rule 12: variance-sample every screenshot/* PNG/JPG cited in the report."""
    errors: list[str] = []
    content = report_path.read_text(encoding="utf-8", errors="ignore")
    for ss_rel in sorted(_extract_image_paths(content)):
        resolved = _resolve_image(report_path, ss_rel)
        if resolved is None:
            # Rule 5 already flags missing screenshot paths; don't double-error.
            continue
        var = compute_image_variance(resolved)
        if var is None:
            # Couldn't sample — log nothing. The hook isn't a substitute for
            # eyeballs; we only block on a positive synthetic signal.
            continue
        if var < VARIANCE_THRESHOLD:
            errors.append(
                f"Screenshot '{ss_rel}' has variance {var:.3f} "
                f"(< {VARIANCE_THRESHOLD:.1f} on a {MIN_SAMPLE_PIXELS}-pixel "
                f"sample) — almost certainly a synthetic flat-colour frame. "
                f"Iter-2 of green_authoring_editor_tool shipped exactly this "
                f"failure mode (a uniform-grey PNG faked as a real capture). "
                f"Re-capture via CaptureCore / CaptureHelper and re-attach."
            )
    return errors


# ─────────────────────────────────────────────────────────────────────────────
# Orchestrator.
# ─────────────────────────────────────────────────────────────────────────────


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
    heartbeat_path = task_dir / "HEARTBEAT.log"

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

    # Rule 10: HEARTBEAT.log must carry a baseline block for the current iter.
    # Rule 11 piggy-backs on the parsed DIRTY paths; Rule 12 is independent.
    report_text = ""
    if report_path.exists():
        report_text = report_path.read_text(encoding="utf-8", errors="ignore")
    iter_n = extract_iteration_number(report_text) if report_text else None
    baseline_errors, baseline = validate_baseline(heartbeat_path, iter_n)
    errors.extend(baseline_errors)

    # Rule 11: pre-existing claims need baseline citations.
    if report_text:
        dirty_paths = baseline["dirty_paths"] if baseline else set()
        errors.extend(validate_preexisting_claims(report_text, dirty_paths))

    # Rule 12: variance check on every screenshot path in the report.
    if report_path.exists():
        errors.extend(validate_image_variances(report_path))

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
