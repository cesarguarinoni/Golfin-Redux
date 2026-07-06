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

Rule 13 is the spin_and_shot_shape_wiring scar-tissue rule (Cesar-approved
2026-05-26 21:25 CEST). It catches the forgotten-files-outside-spec-folder
failure mode:

13. **Files-modified coverage.** Every uncommitted path reported by
    `git status --porcelain` that lives OUTSIDE the task's
    `Docs/Specs/Active/<task>/` folder must appear in IMPLEMENTER_REPORT.md's
    "Files modified or created" table. If a path is in the working tree but
    not in the table, the implementer either forgot to report it (the
    spin_and_shape PhysicsLabController case) or it's drift that should be
    restored before transitioning. See Lesson AA in tasks/lessons.md.

Rule 18 is the 1v1_ingame_ui scar-tissue rule (Cesar-approved 2026-06-09).
It is the UI counterpart of the mesh-metrics gate (Rule 16): for 3D bakes the
objective gate is numbers; for Figma-referencing UI tasks the objective gate is
a per-element fidelity table diffed against the actual Figma node renders.

18. **Figma fidelity table.** When SPEC.md references a Figma NODE (a
    figma.com/design URL or a `<n>:<n>` node-id token alongside "figma"), the
    IMPLEMENTER_REPORT.md must carry a `## Figma fidelity` section with a real
    per-element table (>= 1 data row, a cited Figma node, and PASS/FAIL
    verdicts) — NOT a prose "matches Figma" claim. The same section is required
    in ARCHITECT_REVIEW.md before the reviewer can hand to the red-team
    (READY_FOR_REDTEAM). Rationale: 1v1_ingame_ui passed the full pipeline 2x
    and Cesar caught an EXPLICIT spec token (3px #818EA1 banner border) absent,
    plus a mis-placed / wrong-content mini-map — because reviewers claimed
    "Figma match" without a per-element diff against the pulled node renders.
    The existing visual-review checklist already DEMANDS this ("'matches' is not
    acceptable"); it was unenforced, so it is now a structural gate. The
    architect drops the canonical node renders into the task's reference/ folder
    at spec time so the A/B is unavoidable.

Rule 19 is the tournament_round_loop signup-modal scar-tissue rule (Cesar-
approved 2026-06-28). It is the provenance counterpart of Rule 18: Rule 18 checks
that the result LOOKS like the design; Rule 19 checks that reused elements WERE
actually cloned from a real source, not hand-built from scratch.

19. **Clone provenance table.** When SPEC.md declares a reuse / clone-and-modify
    mandate (e.g. a "§0 REUSE MANDATE", "Author ZERO new panels/buttons", "clone
    the existing ..."), IMPLEMENTER_REPORT.md must carry a `## Clone provenance`
    section: a per-element table where EVERY row cites the concrete source the
    element was cloned/rebound from — a `.prefab` path, an `Assets/...` sprite/
    material path, or a 32-hex Unity GUID. A row with only prose, or that flags a
    source as "not found / built from scratch / hand-rolled", is a hard block.
    Rationale: the signup modal was hand-built from default Unity Images with
    flat colour fills and ZERO sprites — no source prefab, nothing cloned — while
    the report falsely marked every "navy panel clone / silver button clone" row
    PASS (a Rule-6 integrity miss the fidelity gate didn't catch because the flat
    colours vaguely resembled the design). This is the same failure mode as
    tournament_selection_screen (memory: feedback_reuse_map_clone_provenance_gate)
    which "rebuilt from scratch, hand-rolled buttons, passed 3 gates before Cesar
    stopped it." If a mandated clone source genuinely cannot be located, the
    implementer must SURFACE it (STATUS=IMPLEMENTER_BLOCKED), never silently
    rebuild — a Cesar standing rule.
"""
import json
import math
import re
import struct
import subprocess
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

# Implementer -> review transitions: rules 1-15 (the implementer must prove its
# work before any reviewer sees it).
IMPLEMENTER_GATES = {"READY_FOR_SELF_REVIEW", "READY_FOR_ARCHITECT_REVIEW"}
# Reviewer -> red-team handoff: rule 16 only (the reviewer must produce an
# objective mesh-metrics verdict for mesh tasks before handing forward).
REVIEWER_GATES = {"READY_FOR_REDTEAM"}
GATING_STATUSES = IMPLEMENTER_GATES | REVIEWER_GATES

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

# Rule 14 — canonical-screenshot resolution floor. The iter-9 failure of
# green_slope_height_bake: the implementer designated a 256x256 render as the
# canonical PASS evidence; a boundary defect is physically unresolvable at that
# size, so the reviewer rubber-stamped a PASS Cesar killed in seconds at full
# res. Any report that cites screenshots must declare ONE canonical frame, and
# its long edge must be >= this floor.
MIN_CANONICAL_LONG_EDGE = 900
# Accepts "Canonical screenshot:", "**Canonical frame:**", "canonical capture =",
# etc., tolerating markdown bold/punctuation between the label and the path.
CANONICAL_DECLARATION_RE = re.compile(
    r"canonical\s+(?:screenshot|frame|capture|image)\b[^\n]*?`?"
    r"((?:Docs/Specs/(?:Active|Completed)/[\w.\-]+/)?screenshots/[\w./\-]+\.(?:png|jpg|jpeg))",
    re.IGNORECASE,
)

# Rule 15 — reproduce-the-rejection gate. When CESAR_REJECTION.md exists, the
# next report must prove the rejected defect is gone at the rejection angle,
# with a full-res capture. We require a "Rejection follow-up" section that
# carries a resolution verdict AND a screenshot citation.
REJECTION_RESOLVED_RE = re.compile(
    r"\b(GONE|RESOLVED|FIXED|ADDRESSED|NO LONGER|STILL PRESENT|NOT FIXED)\b",
    re.IGNORECASE,
)

# Rule 16 — mesh-task geometry metrics. For 3D mesh / terrain bakes there is no
# Figma reference and no bbox-containment gate, so the reviewer historically had
# nothing objective to fail on (green_slope_height_bake passed 3x on vibes).
# When SPEC.md reads as a mesh task, the reviewer's ARCHITECT_REVIEW.md must
# carry a numeric "Mesh metrics" section before the READY_FOR_REDTEAM handoff.
MESH_TASK_KEYWORDS = [
    "green.json", "terraindata", "mesh-cut", "mesh deform", "recalculatenormals",
    "greentopology", "bake-green", "skirt", "vertex normal", "heightfield",
    "height bake", "contour", "triangulat",
]
MESH_TASK_MIN_HITS = 2
MESH_METRICS_SECTION = "Mesh metrics"

# Rule 17 — mesh/terrain bakes must ship a fresh VIDEO, not stills only. The
# standing rule (feedback_prefer_bot_videos): for a green/terrain bake the thing
# Cesar reviews from chat is an orbit fly-around clip in the task's videos/
# folder; stills are supporting evidence only. green_slope_height_bake reached
# Cesar on stills at iter-7, iter-8, iter-9 AND iter-12 and was bounced every
# time ("I asked you to always show me video"). The rule lived only in memory and
# kept getting skipped, so it is now a hook gate. Scoped to mesh tasks (same
# detector as Rule 16) so UI-layout tasks — verified by stills + Figma — are not
# falsely gated. The report must declare a `Canonical video:` line pointing at an
# existing, non-trivial clip under videos/.
MIN_VIDEO_BYTES = 50_000
# Accepts "Canonical video:", "**Canonical clip:**", "canonical orbit =", etc.,
# tolerating markdown bold/punctuation between the label and the path.
CANONICAL_VIDEO_DECLARATION_RE = re.compile(
    r"canonical\s+(?:video|clip|recording|orbit)\b[^\n]*?`?"
    r"((?:Docs/Specs/(?:Active|Completed)/[\w.\-]+/)?videos/[\w./\-]+\.(?:mp4|mov|webm))",
    re.IGNORECASE,
)

# Rule 18 — Figma fidelity gate. The UI counterpart of Rule 16 (mesh metrics).
# When SPEC.md references a Figma NODE, both IMPLEMENTER_REPORT.md (implementer
# gate) and ARCHITECT_REVIEW.md (reviewer -> red-team gate) must carry a real
# per-element `## Figma fidelity` table — a node citation + PASS/FAIL verdicts,
# not a blanket "matches Figma" claim. See module docstring (Rule 18) and the
# 1v1_ingame_ui post-mortem (Lesson AE).
FIGMA_FIDELITY_SECTION = "Figma fidelity"
# A Figma node id like "13177:1937" or "4094-26038" (>=2 digits each side to
# avoid matching version numbers / dates). Used together with the word "figma".
FIGMA_NODE_ID_RE = re.compile(r"\b\d{2,}[:\-]\d{2,}\b")
# A direct Figma design URL — sufficient on its own to mark a Figma-node task.
FIGMA_URL_RE = re.compile(r"figma\.com/(?:design|file)/", re.IGNORECASE)
# Backend / no-Unity task marker (2026-07-03, figma_node_spec_generator). A
# Tier-2 task that declares it touches NO Unity/scene/prefab — a pure Python
# tool, data script, editor-less generator — has no Game View to capture, so the
# Rule 5 screenshot gate and the Figma-reference-png gate do not apply. Detection
# is DELIBERATELY narrow: it requires an explicit, high-specificity declaration
# so a UI task that merely reuses existing prefabs ("no NEW prefab") is NOT
# exempted. Only these exact opt-out phrasings match.
BACKEND_TASK_RE = re.compile(
    r"no\s+unity\s*/\s*scene\s*/\s*prefab"          # "no Unity/scene/prefab (changes|edits)"
    r"|no\s+unity\s*,\s*scene\s*,?\s*(?:or\s+)?prefab"  # "no Unity, scene, or prefab"
    r"|no\s+`?assets/`?\s+changes",                 # "No `Assets/` changes"
    re.IGNORECASE,
)

# Rule 19 — clone-provenance gate. The tournament_round_loop signup-modal scar
# (Cesar-approved 2026-06-28): the SPEC §0 REUSE MANDATE said clone the navy
# panel / gold+silver Main Buttons / separator / RP-coin from HoleCompleteModal,
# but the implementer hand-built the modal from default Unity Images with flat
# color fills and ZERO sprites (no source prefab, nothing cloned), then the
# report falsely marked every "clone" row PASS. This is the SAME failure mode as
# tournament_selection_screen (memory: feedback_reuse_map_clone_provenance_gate),
# which "rebuilt from scratch, hand-rolled buttons, passed 3 gates before Cesar
# stopped it." No gate ever verified the §1 clone-and-modify mandate — they check
# visual fidelity, not provenance. Rule 19 makes a from-scratch rebuild
# unable to pass: when SPEC declares a reuse/clone mandate, IMPLEMENTER_REPORT.md
# must carry a `## Clone provenance` table where every row either cites a concrete
# source artifact (a `.prefab`, an `Assets/...` asset path, or a 32-hex GUID) OR
# is flagged as not-found. A not-found element means the implementer must SURFACE
# it (STATUS=IMPLEMENTER_BLOCKED) — never silently build from scratch — so the
# presence of a not-found marker on a review transition is itself a hard block.
CLONE_PROVENANCE_SECTION = "Clone provenance"
# Phrases in SPEC.md that declare a reuse/clone mandate (Rule 19 detector).
CLONE_MANDATE_PHRASES = (
    "reuse mandate",
    "clone-and-modify",
    "clone and modify",
    "author zero new",
    "clone the existing",
    "reuse the existing",
    "clone-and-rebind",
    "do not rebuild",
    "don't rebuild",
    "clone from",
)
# A concrete provenance citation in a Clone-provenance row: a .prefab path, an
# Assets/ asset path (sprite/material/etc.), or a 32-hex Unity GUID.
CLONE_SOURCE_RE = re.compile(
    r"(?:[\w./\- ]+\.prefab)"          # a prefab source
    r"|(?:Assets/[\w./\- ]+\.\w+)"      # an Assets/ asset (sprite/material/png)
    r"|(?:\b[0-9a-f]{32}\b)",           # a Unity GUID
    re.IGNORECASE,
)
# Markers an implementer uses when a mandated clone source could NOT be located.
# Their presence on a review-transition is a hard block: the implementer must
# instead set STATUS=IMPLEMENTER_BLOCKED and surface to Cesar (Cesar standing
# rule 2026-06-28: "if no elements mentioned are found to clone SURFACE it,
# don't build from scratch without telling me").
CLONE_NOT_FOUND_RE = re.compile(
    r"\b(?:not\s*found|no\s*source|missing\s*source|could\s*not\s*(?:find|locate)|"
    r"unfound|absent|surfaced?|built\s*from\s*scratch|hand-?rolled|hand-?built)\b",
    re.IGNORECASE,
)

# ─────────────────────────────────────────────────────────────────────────────
# Rule 21 — UI fidelity lint gate (stamina_boost_shop, Cesar-approved 2026-07-02).
# The automated counterpart of Rules 16/18: for Figma-node UI tasks the objective
# gate is a deterministic lint JSON, not a human reading a render. The
# `Golfin.EditorTools.UIFidelity.UIFidelityLinter` (Assets/Editor/UIFidelity/)
# instantiates each built prefab under a temp canvas and emits
# `Docs/Diagnostics/_capture/<prefab>_lint.json` with a `fail` count covering
# render-health (9-slice collapse→oval, non-9-slice corner distortion, flat-fill
# fabrication, Outline-borders) + node-spec (size/gap/radius/sprite/color/font).
# Every issue Cesar caught by eye on the menu row (oval pill, BUY radius, dark
# tint, wrong gaps, flat fills) maps to a check here. IMPLEMENTER_REPORT.md must
# carry a `## UI fidelity lint` section citing lint JSON(s), and each cited JSON
# must exist with `fail == 0`. Missing/failing lint = hard block.
UI_LINT_SECTION = "UI fidelity lint"
UI_LINT_JSON_RE = re.compile(r"[\w./\-]+_lint\.json")


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


def validate_report(report_path: Path, require_screenshot: bool = True) -> list[str]:
    """Return list of validation errors. Empty list = valid.

    ``require_screenshot`` is False for declared backend/no-Unity tasks (see
    spec_is_backend_task) — a headless tool has no Game View, so Rule 5's
    '## Screenshot' + real-PNG requirement is skipped for it.
    """
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
    # Skipped entirely for declared backend/no-Unity tasks (require_screenshot
    # False) — a headless Python tool / data script has no Game View to capture.
    ss_path = None
    if require_screenshot:
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


def spec_is_backend_task(spec_path: Path) -> bool:
    """True when SPEC.md explicitly declares a backend / no-Unity task.

    Such a task (a Python tool, data migration, editor-less generator) produces
    no Game View, so the Rule 5 screenshot gate and the Figma-reference-png gate
    are skipped for it. Detection is narrow by design (see BACKEND_TASK_RE): only
    an explicit "no Unity/scene/prefab" or "No `Assets/` changes" declaration
    qualifies, so a normal UI task can never accidentally exempt itself out of
    the screenshot requirement.
    """
    if not spec_path.exists():
        return False
    text = spec_path.read_text(encoding="utf-8", errors="ignore")
    return bool(BACKEND_TASK_RE.search(text))


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
# Rules 14-16: review-hardening (resolution floor, reproduce-rejection, mesh metrics).
# ─────────────────────────────────────────────────────────────────────────────


def _section_text(md: str, section_header: str) -> str | None:
    """Return the body text under a Markdown header, up to the next header.

    None if the header is absent. Mirrors parse_table_rows' section-finding
    but returns the raw text so callers can scan for keywords / citations.
    """
    pattern = re.compile(rf"^#+\s*{re.escape(section_header)}\b.*$", re.MULTILINE)
    m = pattern.search(md)
    if not m:
        return None
    after = md[m.end():]
    nxt = re.search(r"^#+\s+\S", after, re.MULTILINE)
    return after[: nxt.start()] if nxt else after


def image_dimensions(img_path: Path) -> tuple[int, int] | None:
    """(width, height) in px. Pillow first, raw PNG IHDR fallback. None if unknown.

    The fallback reads only the 26-byte PNG header, so it works for any PNG
    without Pillow. JPEGs without Pillow return None (caller treats None as
    "can't measure — don't block").
    """
    try:
        from PIL import Image  # type: ignore
        with Image.open(img_path) as im:
            w, h = im.size
            return (int(w), int(h))
    except Exception:
        pass
    try:
        with open(img_path, "rb") as f:
            head = f.read(26)
        if head[:8] == b"\x89PNG\r\n\x1a\n" and head[12:16] == b"IHDR":
            w = struct.unpack(">I", head[16:20])[0]
            h = struct.unpack(">I", head[20:24])[0]
            return (int(w), int(h))
    except Exception:
        pass
    return None


def validate_canonical_resolution(report_path: Path) -> list[str]:
    """Rule 14: a report that cites screenshots must declare exactly one
    canonical frame, and that frame's long edge must be >= MIN_CANONICAL_LONG_EDGE.

    Skips entirely when the report cites no screenshots (non-visual task).
    """
    errors: list[str] = []
    if not report_path.exists():
        return errors  # Rule 1 already errored.
    content = report_path.read_text(encoding="utf-8", errors="ignore")
    if not _extract_image_paths(content):
        return errors  # non-visual task — nothing to gate.

    matches = CANONICAL_DECLARATION_RE.findall(content)
    if not matches:
        errors.append(
            "IMPLEMENTER_REPORT.md cites screenshots but declares no canonical "
            "frame. Add a line `Canonical screenshot: \\`screenshots/<file>.png\\`` "
            "naming the single full-res frame the reviewer must judge. (Rule 14: "
            "green_slope_height_bake iter-9 passed on a 256px top-down the "
            "implementer happened to pick — designation + a resolution floor "
            "stops that.)"
        )
        return errors

    for ss_rel in matches:
        resolved = _resolve_image(report_path, ss_rel)
        if resolved is None:
            errors.append(
                f"Canonical screenshot '{ss_rel}' is declared but the file does "
                f"not exist. Point it at a real capture under screenshots/."
            )
            continue
        dims = image_dimensions(resolved)
        if dims is None:
            continue  # can't measure (exotic format / no Pillow on JPEG) — don't block.
        if max(dims) < MIN_CANONICAL_LONG_EDGE:
            errors.append(
                f"Canonical screenshot '{ss_rel}' is {dims[0]}x{dims[1]} — long "
                f"edge {max(dims)}px < required {MIN_CANONICAL_LONG_EDGE}px. A "
                f"defect invisible at this size gets rubber-stamped "
                f"(green_slope_height_bake iter-9 was 256px). Re-capture at full "
                f"resolution (screenshot-isolated resolution>=900, or "
                f"CaptureHelper game-view). (Rule 14.)"
            )
    return errors


def validate_rejection_followup(report_path: Path, task_dir: Path) -> list[str]:
    """Rule 15: when CESAR_REJECTION.md exists, the report must carry a
    'Rejection follow-up' section with a resolution verdict AND a screenshot
    citation — proving the next iter re-shot the exact defect Cesar flagged.
    """
    errors: list[str] = []
    if not (task_dir / "CESAR_REJECTION.md").exists():
        return errors
    if not report_path.exists():
        return errors  # Rule 1 already errored.
    content = report_path.read_text(encoding="utf-8", errors="ignore")
    section = _section_text(content, "Rejection follow-up")
    if section is None:
        errors.append(
            "CESAR_REJECTION.md exists but IMPLEMENTER_REPORT.md has no "
            "'## Rejection follow-up' section. Cesar rejected after an architect "
            "PASS — the report must re-shoot the exact angle/defect Cesar flagged "
            "and state explicitly whether it is GONE, with a full-res screenshot "
            "citation. (Rule 15.)"
        )
        return errors
    if not REJECTION_RESOLVED_RE.search(section):
        errors.append(
            "'Rejection follow-up' section has no explicit resolution verdict. "
            "Write GONE / RESOLVED / FIXED per defect Cesar flagged (or STILL "
            "PRESENT — in which case set STATUS=IMPLEMENTER_BLOCKED, do not "
            "advance to review). (Rule 15.)"
        )
    if not _extract_image_paths(section):
        errors.append(
            "'Rejection follow-up' section cites no screenshot. Attach the "
            "same-angle, full-res capture proving the rejected defect is gone, "
            "referenced as `screenshots/<file>.png`. (Rule 15.)"
        )
    return errors


def spec_is_mesh_task(spec_path: Path) -> bool:
    """True when SPEC.md reads as a 3D mesh / terrain bake (>= MESH_TASK_MIN_HITS
    distinct mesh keywords). Used to gate Rule 16 to tasks where geometry metrics
    are the meaningful objective check."""
    if not spec_path.exists():
        return False
    text = spec_path.read_text(encoding="utf-8", errors="ignore").lower()
    hits = sum(1 for kw in MESH_TASK_KEYWORDS if kw in text)
    return hits >= MESH_TASK_MIN_HITS


def validate_mesh_metrics(review_path: Path) -> list[str]:
    """Rule 16: a mesh-task review must carry a numeric 'Mesh metrics' section
    before the reviewer can hand forward to the red-teamer."""
    errors: list[str] = []
    if not review_path.exists():
        errors.append(
            "ARCHITECT_REVIEW.md not found — a mesh task cannot hand forward "
            "without the reviewer's mesh-metrics verdict. (Rule 16.)"
        )
        return errors
    content = review_path.read_text(encoding="utf-8", errors="ignore")
    section = _section_text(content, MESH_METRICS_SECTION)
    if section is None:
        errors.append(
            "Mesh task: ARCHITECT_REVIEW.md has no '## Mesh metrics' section. The "
            "reviewer must run script-execute geometry checks and paste NUMBERS "
            "(e.g. min collar-ring vertex normal.y, max Y-step between adjacent "
            "boundary vertices, boundary vertex count) with PASS/FAIL against "
            "thresholds. For 3D/terrain bakes there is no Figma or bbox gate — "
            "numbers are the objective gate. (Rule 16.)"
        )
        return errors
    if not re.search(r"-?\d+\.?\d*", section):
        errors.append(
            "'Mesh metrics' section has no numeric measurement — paste the actual "
            "script-execute output (normal.y, Y-step, vert count), not a prose "
            "placeholder. (Rule 16.)"
        )
    return errors


def _resolve_video(report_path: Path, rel: str) -> Path | None:
    """Resolve a videos/ path mentioned in the report to an absolute file.

    Mirrors _resolve_image: tries report-relative first, then repo-rooted.
    """
    candidate = (report_path.parent / rel).resolve()
    if candidate.exists():
        return candidate
    alt = (REPO_ROOT / rel).resolve()
    if alt.exists():
        return alt
    return None


def validate_video_deliverable(
    report_path: Path,
    spec_path: Path,
) -> list[str]:
    """Rule 17: a mesh/terrain bake must ship a fresh orbit video, not stills only.

    Scoped to mesh tasks (spec_is_mesh_task) so UI-layout tasks verified by
    stills + Figma are not falsely gated. The report must declare a
    `Canonical video:` line pointing at an existing, non-trivial clip under
    videos/. Skips entirely for non-mesh tasks.
    """
    errors: list[str] = []
    if not report_path.exists():
        return errors  # Rule 1 already errored.
    if not spec_is_mesh_task(spec_path):
        return errors  # only gate mesh/terrain bakes.

    content = report_path.read_text(encoding="utf-8", errors="ignore")
    matches = CANONICAL_VIDEO_DECLARATION_RE.findall(content)
    if not matches:
        errors.append(
            "Mesh/terrain task: IMPLEMENTER_REPORT.md declares no canonical "
            "video. A green/terrain bake is reviewed from chat as an orbit "
            "fly-around clip, not stills — add a line `Canonical video: "
            "\\`videos/<file>.mp4\\`` pointing at a fresh orbit of the baked "
            "subject, copied to the task's videos/ folder. Render it with the "
            "build_bot_video.py / textfile-drawtext idiom (see "
            "reference_video_caption_tool memory). (Rule 17: green_slope_height_"
            "bake reached Cesar on stills at iter-7/8/9/12 and was bounced every "
            "time.)"
        )
        return errors

    for rel in matches:
        resolved = _resolve_video(report_path, rel)
        if resolved is None:
            errors.append(
                f"Canonical video '{rel}' is declared but the file does not "
                f"exist. Point it at a real clip under videos/. (Rule 17.)"
            )
            continue
        try:
            size = resolved.stat().st_size
        except OSError:
            size = 0
        if size < MIN_VIDEO_BYTES:
            errors.append(
                f"Canonical video '{rel}' is {size} bytes "
                f"(< {MIN_VIDEO_BYTES}) — looks empty/placeholder. Re-render the "
                f"orbit; a real short 720p clip is multiple MB. (Rule 17.)"
            )
    return errors


# Rule 20 — slideshow / non-continuous video detection. stamina_roster_live_meter
# (2026-06-30) failed self-review TWICE on a "video" that was actually an ffmpeg
# image2 SLIDESHOW stitched from ~5 posed stills (some reflection-set on
# Image.fillAmount). The lesson "use BotVideoRecorder / Unity Recorder, never
# hand-stitch" lived only in memory and kept getting skipped — the self-reviewer
# caught it post-hoc both times, wasting whole review cycles. This converts that
# advisory lesson into a hard stop: any video deliverable referenced in the report
# must be a CONTINUOUS recording. We run ffmpeg `mpdecimate` to count distinct
# (non-duplicate) frames — a real recording keeps hundreds; a slideshow collapses
# to a handful (the iter-2 fake: 30s → 5 distinct frames). Gracefully skips when
# ffmpeg is unavailable so the gate never blocks on tooling absence. Scoped to
# clips >= SLIDESHOW_MIN_DURATION_S so genuinely short clips aren't gated.
ANY_VIDEO_PATH_RE = re.compile(
    r"((?:Docs/Specs/(?:Active|Completed)/[\w.\-]+/)?videos/[\w./\-]+\.(?:mp4|mov|webm))",
    re.IGNORECASE,
)
SLIDESHOW_MIN_DURATION_S = 4.0
SLIDESHOW_MAX_DISTINCT_FRAMES = 8


def _resolve_tool(name: str) -> str | None:
    """Find an ffmpeg/ffprobe executable without importing shutil.

    Tries PATH (bare name) plus the hand-install location used on this machine
    (~/.local/bin, per the reference_node_install_mac / gh-cli memories) and the
    usual Homebrew/system dirs. Returns the first that answers `-version`.
    """
    candidates = [
        name,
        str(Path.home() / ".local" / "bin" / name),
        f"/opt/homebrew/bin/{name}",
        f"/usr/local/bin/{name}",
        f"/usr/bin/{name}",
    ]
    for cand in candidates:
        try:
            subprocess.run(
                [cand, "-version"], capture_output=True, timeout=10
            )
            return cand
        except (FileNotFoundError, OSError, subprocess.SubprocessError):
            continue
    return None


def _video_duration_seconds(video: Path) -> float | None:
    ffprobe = _resolve_tool("ffprobe")
    if ffprobe is None:
        return None
    try:
        proc = subprocess.run(
            [ffprobe, "-v", "error", "-show_entries", "format=duration",
             "-of", "default=nk=1:nw=1", str(video)],
            capture_output=True, text=True, timeout=30,
        )
        return float(proc.stdout.strip())
    except (OSError, ValueError, subprocess.SubprocessError):
        return None


def _video_distinct_frames(video: Path) -> int | None:
    """Count non-duplicate frames via ffmpeg mpdecimate. None on any failure."""
    ffmpeg = _resolve_tool("ffmpeg")
    if ffmpeg is None:
        return None
    try:
        proc = subprocess.run(
            [ffmpeg, "-nostdin", "-i", str(video), "-vf", "mpdecimate",
             "-fps_mode", "vfr", "-f", "null", "-"],
            capture_output=True, text=True, timeout=180,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    # ffmpeg writes progress to stderr; the final `frame=  N` after mpdecimate is
    # the count of frames that survived de-duplication (i.e. distinct frames).
    matches = re.findall(r"frame=\s*(\d+)", proc.stderr)
    if not matches:
        return None
    return int(matches[-1])


def validate_video_continuity(report_path: Path) -> list[str]:
    """Rule 20: every video deliverable referenced in the report must be a
    continuous recording, not a slideshow stitched from a few static frames.

    Counts distinct frames with ffmpeg mpdecimate; a clip longer than
    SLIDESHOW_MIN_DURATION_S that collapses to <= SLIDESHOW_MAX_DISTINCT_FRAMES
    distinct frames is a fabricated slideshow and blocks the transition. Skips
    gracefully when ffmpeg is unavailable or the file can't be probed.
    """
    errors: list[str] = []
    if not report_path.exists():
        return errors
    content = report_path.read_text(encoding="utf-8", errors="ignore")

    seen: set[str] = set()
    for rel in ANY_VIDEO_PATH_RE.findall(content):
        if rel in seen:
            continue
        seen.add(rel)
        resolved = _resolve_video(report_path, rel)
        if resolved is None:
            continue  # missing files are Rule 17's concern; don't double-report.
        try:
            if resolved.stat().st_size < MIN_VIDEO_BYTES:
                continue  # too small to probe meaningfully.
        except OSError:
            continue
        duration = _video_duration_seconds(resolved)
        if duration is None or duration < SLIDESHOW_MIN_DURATION_S:
            continue  # short / unprobeable clips are not gated.
        distinct = _video_distinct_frames(resolved)
        if distinct is None:
            continue  # ffmpeg unavailable: graceful skip, never block on tooling.
        if distinct <= SLIDESHOW_MAX_DISTINCT_FRAMES:
            errors.append(
                f"Video '{rel}' runs {duration:.0f}s but collapses to only "
                f"{distinct} distinct frame(s) (ffmpeg mpdecimate) — this is a "
                f"SLIDESHOW stitched from a few static frames, not a continuous "
                f"recording. Record the real motion with the Unity Recorder "
                f"demo-recorder family (TournamentDemoRecorder / RankingsDemoRecorder "
                f"+ BotVideoRecorder, RecorderController) — it captures every frame, "
                f"so a slideshow is structurally impossible. Do NOT loop "
                f"CaptureCore.SnapPlayModeSafe + ffmpeg-stitch stills, and do NOT "
                f"reflection-pose Image.fillAmount. (Rule 20: stamina_roster_live_"
                f"meter failed self-review twice on fabricated slideshows, "
                f"2026-06-30.)"
            )
    return errors


def spec_references_figma_node(spec_path: Path) -> bool:
    """Rule 18 detector: True when SPEC.md references a concrete Figma NODE.

    A "Figma node" means the spec points at a specific design to diff against —
    either a figma.com/design URL, or a `<n>:<n>` / `<n>-<n>` node-id token used
    in a Figma context. We require the word "figma" to be present so a bare
    "13177:1937"-style token in an unrelated spec doesn't trip it, and we ignore
    specs that explicitly opt out ("no new Figma" with no node id won't have a
    node-id token in Figma context).

    Returns False for non-UI/backend specs (no Figma mention) and for UI specs
    that reuse existing elements with no Figma node to diff against.
    """
    if not spec_path.exists():
        return False
    text = spec_path.read_text(encoding="utf-8", errors="ignore")
    if FIGMA_URL_RE.search(text):
        return True
    if "figma" in text.lower() and FIGMA_NODE_ID_RE.search(text):
        return True
    return False


def validate_figma_fidelity(doc_path: Path, role_label: str) -> list[str]:
    """Rule 18: a Figma-node task's IMPLEMENTER_REPORT.md / ARCHITECT_REVIEW.md
    must carry a real per-element `## Figma fidelity` table.

    Structural checks (cheap, hard to fake with prose):
      - the `## Figma fidelity` section exists,
      - it contains a Markdown table with >= 1 data row,
      - it cites at least one Figma node (node-id token or figma.com URL) —
        proving a specific node was pulled and diffed, not "matches Figma",
      - it contains at least one PASS/FAIL verdict.

    `role_label` ("IMPLEMENTER_REPORT.md" / "ARCHITECT_REVIEW.md") is woven into
    the error text so the blocked agent knows which doc to fix.
    """
    errors: list[str] = []
    if not doc_path.exists():
        errors.append(
            f"{role_label} not found — a Figma-referencing task cannot advance "
            f"without a per-element Figma fidelity table. (Rule 18.)"
        )
        return errors
    content = doc_path.read_text(encoding="utf-8", errors="ignore")
    section = _section_text(content, FIGMA_FIDELITY_SECTION)
    if section is None:
        errors.append(
            f"{role_label} has no '## Figma fidelity' section. The task's SPEC "
            f"references a Figma node, so the visual claim must be a PER-ELEMENT "
            f"table (one row per element: portrait/card/banner/border/font/"
            f"position/content), each citing the Figma node + a freshly-pulled "
            f"render with PASS/FAIL — NOT a blanket 'matches Figma'. (Rule 18: "
            f"1v1_ingame_ui passed the pipeline 2x with an explicit 3px #818EA1 "
            f"banner-border token absent + a mis-placed mini-map, because "
            f"reviewers vibe-matched instead of diffing each element against the "
            f"pulled node renders in reference/.)"
        )
        return errors
    # Table with >= 1 data row.
    rows = parse_table_rows(content, FIGMA_FIDELITY_SECTION)
    if not rows:
        errors.append(
            f"{role_label} '## Figma fidelity' section has no table data rows. "
            f"Add a row per UI element with columns like | Element | Figma node | "
            f"Figma value | Built value | PASS/FAIL |. (Rule 18.)"
        )
    # Node citation — proves a specific node was diffed, not a prose claim.
    if not (FIGMA_NODE_ID_RE.search(section) or FIGMA_URL_RE.search(section)):
        errors.append(
            f"{role_label} '## Figma fidelity' section cites no Figma node "
            f"(no `<n>:<n>` node id or figma.com URL). Cite the specific node "
            f"each element was diffed against. (Rule 18.)"
        )
    # At least one PASS/FAIL verdict — proves a per-property judgment, not prose.
    if not re.search(r"\b(PASS|FAIL)\b", section, re.IGNORECASE):
        errors.append(
            f"{role_label} '## Figma fidelity' section has no PASS/FAIL verdict. "
            f"Each element row needs an explicit per-property result. 'matches' "
            f"is not acceptable. (Rule 18.)"
        )
    return errors


def spec_requires_clone_provenance(spec_path: Path) -> bool:
    """Rule 19 detector: True when SPEC.md declares a reuse/clone mandate.

    Matches the §0-style "REUSE MANDATE / clone-and-modify / Author ZERO new"
    language that tournament tasks use. A spec that merely mentions a prefab in
    passing won't trip it — we require one of the explicit mandate phrases.
    """
    if not spec_path.exists():
        return False
    text = spec_path.read_text(encoding="utf-8", errors="ignore").lower()
    return any(phrase in text for phrase in CLONE_MANDATE_PHRASES)


def validate_clone_provenance(report_path: Path) -> list[str]:
    """Rule 19: a reuse-mandate task's IMPLEMENTER_REPORT.md must carry a real
    `## Clone provenance` table proving each reused element came from a concrete
    source — NOT a from-scratch rebuild.

    Structural checks (cheap, hard to fake honestly):
      - the `## Clone provenance` section exists,
      - it contains a Markdown table with >= 1 data row,
      - EVERY data row cites a concrete source (a `.prefab`, an `Assets/...`
        asset path, or a 32-hex GUID) — proving the element was cloned/rebound
        from a real artifact, not hand-built,
      - NO row carries a "not found / built from scratch / hand-rolled / surfaced"
        marker. If a mandated clone source could not be located, the implementer
        must STOP and set STATUS=IMPLEMENTER_BLOCKED to surface it (Cesar standing
        rule) — reaching a review state with a not-found element is a hard block.
    """
    errors: list[str] = []
    if not report_path.exists():
        return errors  # Rule 1 already errored on the missing report.
    content = report_path.read_text(encoding="utf-8", errors="ignore")
    section = _section_text(content, CLONE_PROVENANCE_SECTION)
    if section is None:
        errors.append(
            "IMPLEMENTER_REPORT.md has no '## Clone provenance' section. The "
            "task's SPEC declares a REUSE / clone-and-modify mandate, so every "
            "reused element (panel, buttons, separator, pill, icon, sprite) must "
            "be listed in a per-element table citing the SOURCE it was cloned / "
            "rebound from — a `.prefab` path, an `Assets/...` sprite/material "
            "path, or the source GUID. (Rule 19: tournament_round_loop's signup "
            "modal was hand-built from spriteless flat-colour Images while the "
            "report falsely marked every 'clone' row PASS — same scar as "
            "tournament_selection_screen.) If a mandated source CANNOT be "
            "located, do NOT build from scratch: set STATUS=IMPLEMENTER_BLOCKED "
            "and surface it to Cesar."
        )
        return errors
    rows = parse_table_rows(content, CLONE_PROVENANCE_SECTION)
    if not rows:
        errors.append(
            "IMPLEMENTER_REPORT.md '## Clone provenance' section has no table "
            "data rows. Add one row per reused element with columns like | "
            "Element | Cloned from (prefab/asset/GUID) | How verified |. "
            "(Rule 19.)"
        )
        return errors
    # Per-row: a concrete source citation, and NO not-found marker.
    for row in rows:
        row_text = " ".join(c.strip() for c in row if c is not None)
        if not row_text.strip():
            continue
        if CLONE_NOT_FOUND_RE.search(row_text):
            errors.append(
                "IMPLEMENTER_REPORT.md '## Clone provenance' row declares a "
                f"mandated clone source as not-found / built-from-scratch: "
                f"'{row_text[:120].strip()}'. You may NOT advance to review with "
                "a from-scratch element. Either locate the real source (the "
                "panel, both buttons, the separator, the entry pill, and the RP "
                "icon all already exist in other tournament screens/prefabs — "
                "clone those) OR set STATUS=IMPLEMENTER_BLOCKED and surface it to "
                "Cesar. (Rule 19 — Cesar standing rule 2026-06-28.)"
            )
            continue
        if not CLONE_SOURCE_RE.search(row_text):
            errors.append(
                "IMPLEMENTER_REPORT.md '## Clone provenance' row cites no "
                f"concrete source: '{row_text[:120].strip()}'. Each reused "
                "element must name the artifact it was cloned/rebound from — a "
                "`.prefab` path, an `Assets/...` asset path, or a 32-hex GUID. A "
                "row with only prose ('matches the modal family') does not prove "
                "provenance. (Rule 19.)"
            )
    return errors


def validate_ui_lint(report_path: Path, repo_root: Path) -> list[str]:
    """Rule 21: a Figma-node UI task's IMPLEMENTER_REPORT.md must carry a
    `## UI fidelity lint` section citing lint JSON artifact(s), and each cited
    JSON must exist with `fail == 0`.

    The lint JSON is produced by `UIFidelityLinter.LintPrefab` (run via Unity
    MCP) — a deterministic render-health + node-spec pass over each built
    prefab. This is the automated gate that would have caught, with no human
    looking, the oval pill (9-slice collapse), the distorted BUY radius
    (non-9-slice stretch), the dark-tinted panel, the wrong 16px gaps, and any
    flat-fill fabrication. A prose fidelity table (Rule 18) says it LOOKS right;
    this says the geometry/sprites objectively ARE right.

    Structural checks (hard to fake):
      - the `## UI fidelity lint` section exists,
      - it references at least one `*_lint.json`,
      - every referenced JSON exists (checked at the cited path and under
        Docs/Diagnostics/_capture/) and parses with `fail == 0`.
    """
    errors: list[str] = []
    if not report_path.exists():
        return errors  # Rule 1 already errored on the missing report.
    content = report_path.read_text(encoding="utf-8", errors="ignore")
    section = _section_text(content, UI_LINT_SECTION)
    if section is None:
        errors.append(
            "IMPLEMENTER_REPORT.md has no '## UI fidelity lint' section. The "
            "task's SPEC references a Figma node, so every new/changed UI prefab "
            "must be run through `UIFidelityLinter.LintPrefab(prefab, spec.json)` "
            "(Assets/Editor/UIFidelity/) and this section must cite each "
            "resulting `Docs/Diagnostics/_capture/<prefab>_lint.json` with "
            "fail == 0. (Rule 21: the automated gate for the oval-pill / "
            "BUY-radius / dark-tint / wrong-gap / flat-fill class of defects "
            "Cesar had to catch by eye on stamina_boost_shop.)"
        )
        return errors
    refs = UI_LINT_JSON_RE.findall(section)
    if not refs:
        errors.append(
            "IMPLEMENTER_REPORT.md '## UI fidelity lint' section references no "
            "`*_lint.json` artifact. Cite each prefab's lint JSON produced by "
            "UIFidelityLinter (e.g. `Docs/Diagnostics/_capture/StaminaMenuRow_"
            "lint.json`). (Rule 21.)"
        )
        return errors
    for ref in refs:
        candidates = [
            repo_root / ref,
            repo_root / "Docs" / "Diagnostics" / "_capture" / Path(ref).name,
        ]
        found = next((c for c in candidates if c.exists()), None)
        if found is None:
            errors.append(
                f"IMPLEMENTER_REPORT.md '## UI fidelity lint' cites lint JSON "
                f"'{ref}' but it does not exist (looked at the cited path and "
                f"Docs/Diagnostics/_capture/). Run the linter and commit the "
                f"artifact. (Rule 21.)"
            )
            continue
        try:
            data = json.loads(found.read_text(encoding="utf-8", errors="ignore"))
            fail = int(data.get("fail", -1))
        except Exception:
            errors.append(
                f"IMPLEMENTER_REPORT.md '## UI fidelity lint' cites lint JSON "
                f"'{ref}' but it is unparseable / has no 'fail' field. Re-run "
                f"UIFidelityLinter.LintPrefab. (Rule 21.)"
            )
            continue
        if fail != 0:
            errors.append(
                f"UI fidelity lint '{found.name}' reports fail={fail} — the "
                f"prefab has render-health or node-spec violations (see the "
                f"findings in that JSON). Fix them until fail == 0 before moving "
                f"to review. (Rule 21.)"
            )
    return errors


# ─────────────────────────────────────────────────────────────────────────────
# Phase 1 — Clone-provenance VERIFIER (P1 + P3 + P2).
# Mechanizes §11 of PIPELINE_HARDENING.md into the hook.
# Design law: reads YAML/filesystem facts — never the implementer-authored table.
# ─────────────────────────────────────────────────────────────────────────────

# GUID of zero: an empty/null Unity object reference.
_ZERO_GUID = "0000000000000000000000000000000000000000"[:32]  # 32 zeros

# Regex to parse a Unity YAML m_Sprite / m_SourcePrefab reference.
_UNITY_GUID_RE = re.compile(r"\bguid:\s*([0-9a-f]{32})\b", re.IGNORECASE)
# Matches a YAML document anchor line like "--- !u!1001 &<fileID>" (PrefabInstance).
_PREFAB_INSTANCE_BLOCK_RE = re.compile(r"^---\s+!u!1001\s+&", re.MULTILINE)
# m_SourcePrefab inside a PrefabInstance block
_SOURCE_PREFAB_RE = re.compile(r"m_SourcePrefab:\s*\{[^}]*guid:\s*([0-9a-f]{32})", re.IGNORECASE)
# Image component YAML block
_IMAGE_COMPONENT_RE = re.compile(r"^---\s+!u!114[^\n]*\n.*?(?=^---|\Z)", re.MULTILINE | re.DOTALL)
# m_Sprite inside an Image/SpriteRenderer component — real (non-null) GUID only.
_M_SPRITE_RE = re.compile(r"m_Sprite:\s*\{[^}]*guid:\s*([0-9a-f]{32})", re.IGNORECASE)
# m_Sprite line present at all (captures optional guid group; empty = null sprite).
_M_SPRITE_ANY_RE = re.compile(
    r"m_Sprite:\s*\{[^}]*\}",
    re.IGNORECASE,
)
# GameObject name field
_GO_NAME_RE = re.compile(r"^\s+m_Name:\s+(.+)$", re.MULTILINE)
# Link between Image component fileID and its parent GameObject fileID
_MONO_GO_RE = re.compile(r"m_GameObject:\s*\{fileID:\s*(\d+)", re.IGNORECASE)


def _parse_prefab_gameobject_sprites(prefab_yaml: str) -> dict[str, str | None]:
    """Extract {GameObject_name -> sprite_guid_or_None} from a prefab YAML text.

    For each GameObject (identified by m_Name), finds any attached MonoBehaviour
    component's m_Sprite value.
    - Returns a non-empty guid string for a real sprite.
    - Returns '' (empty string) for a null/zero sprite (fileID: 0 / empty guid).
    - Returns None if the GameObject has no m_Sprite field in any of its components.

    Uses a two-pass approach:
    1. Build fileID -> name map from GameObject blocks.
    2. For each MonoBehaviour block, find m_Sprite and its parent GO name.
    """
    # Pass 1: fileID -> name
    go_blocks = re.split(r"(?=^---\s+!u!1\b)", prefab_yaml, flags=re.MULTILINE)
    fileid_to_name: dict[str, str] = {}
    for block in go_blocks:
        if "GameObject:" not in block:
            continue
        anchor = re.search(r"^---\s+!u!1\s+&(\d+)", block, re.MULTILINE)
        names = _GO_NAME_RE.findall(block)
        if anchor and names:
            fileid_to_name[anchor.group(1)] = names[0].strip()

    # Pass 2: MonoBehaviour blocks — look for m_Sprite + parent GO name.
    name_to_sprite: dict[str, str | None] = {}
    for block in _IMAGE_COMPONENT_RE.finditer(prefab_yaml):
        block_text = block.group(0)
        go_m = _MONO_GO_RE.search(block_text)
        if not go_m:
            continue
        parent_fid = go_m.group(1)
        go_name = fileid_to_name.get(parent_fid)
        if not go_name:
            continue

        # Check for real sprite GUID first.
        sprite_m = _M_SPRITE_RE.search(block_text)
        if sprite_m:
            guid = sprite_m.group(1)
            # Treat zero guid as empty (no real sprite).
            name_to_sprite[go_name] = "" if all(c == "0" for c in guid) else guid
            continue

        # Check if m_Sprite is present at all (null / fileID: 0 / empty guid).
        if _M_SPRITE_ANY_RE.search(block_text):
            # m_Sprite exists but no non-zero GUID → null sprite.
            name_to_sprite[go_name] = ""
            continue

        # MonoBehaviour without any m_Sprite field — only record if not already set.
        if go_name not in name_to_sprite:
            name_to_sprite[go_name] = None
    return name_to_sprite


def _parse_prefab_source_guids(prefab_yaml: str) -> set[str]:
    """Return set of m_SourcePrefab GUIDs found in PrefabInstance blocks."""
    guids: set[str] = set()
    for m in _SOURCE_PREFAB_RE.finditer(prefab_yaml):
        guid = m.group(1)
        if not all(c == "0" for c in guid):
            guids.add(guid)
    return guids


def load_reuse_map(task_dir: Path) -> dict | None:
    """Load reuse_map.json from the task folder (SPEC-side ground truth for P1).

    Returns the parsed dict or None if the file does not exist.
    Format: {"elements": [{"elementPath": "...", "sourcePrefab": "path guid:GUID",
                           "keySpriteGuid": "GUID or 'any-nonnull'"}]}
    """
    rmap_path = task_dir / "reuse_map.json"
    if not rmap_path.exists():
        return None
    try:
        return json.loads(rmap_path.read_text(encoding="utf-8"))
    except Exception:
        return None


def _extract_guid_from_source(source_str: str) -> str | None:
    """Parse the sourcePrefab field in reuse_map.json.

    Accepts formats:
      - "path/to/Foo.prefab guid:HEXGUID"
      - "Assets/Prefabs/Foo.prefab"  (no explicit guid → find .meta file)
      - "HEXGUID"                     (bare 32-hex GUID)
    Returns a 32-char hex guid string, or None if not parseable.
    """
    if not source_str:
        return None
    # Explicit "guid:" suffix
    m = re.search(r"guid:([0-9a-f]{32})", source_str, re.IGNORECASE)
    if m:
        return m.group(1).lower()
    # Bare 32-hex guid
    m = re.fullmatch(r"[0-9a-f]{32}", source_str.strip(), re.IGNORECASE)
    if m:
        return source_str.strip().lower()
    return None


def _read_prefab_yaml(prefab_path_or_str: str, repo_root: Path) -> str | None:
    """Read a prefab file from the repo, given a path (possibly relative to Assets/)."""
    candidates = [
        Path(prefab_path_or_str),
        repo_root / prefab_path_or_str,
    ]
    for c in candidates:
        if c.exists():
            try:
                return c.read_text(encoding="utf-8", errors="ignore")
            except Exception:
                return None
    return None


def _find_prefab_by_guid(guid: str, repo_root: Path) -> Path | None:
    """Scan Assets/ .meta files to locate the prefab for a given GUID."""
    assets_dir = repo_root / "Assets"
    if not assets_dir.exists():
        return None
    for meta_file in assets_dir.rglob("*.prefab.meta"):
        try:
            content = meta_file.read_text(encoding="utf-8", errors="ignore")
            if f"guid: {guid}" in content or f"guid:{guid}" in content:
                return meta_file.with_suffix("")  # drop .meta
        except Exception:
            continue
    return None


def validate_clone_provenance_yaml(
    task_dir: Path,
    repo_root: Path,
) -> list[str]:
    """P1: YAML-based clone-provenance VERIFIER.

    Reads the reuse_map.json (SPEC-side ground truth), then for each element:
      1. Checks whether the BUILT prefab (identified by builtPrefab field, or inferred
         from the first *.prefab in git's uncommitted diff) contains PrefabInstance
         blocks whose m_SourcePrefab matches the cited source GUID.
      2. For CopyAsset / non-prefab-instance clones: parses both the built prefab
         and the source prefab to compare the element's Image.m_Sprite GUID.
         Null/blank sprite where source has one = CRITICAL FAIL.
         Different real sprite = WARN (legal re-skin).

    Returns [] if verification passes (or if no reuse_map.json exists — Phase 1
    only applies to tasks that have the map).
    Returns ["WARN: ..."] for legal re-skins.
    Returns ["CRITICAL FAIL: ..."] for fabrication detections.
    """
    rmap = load_reuse_map(task_dir)
    if rmap is None:
        # No reuse map; rule doesn't apply (only tasks that ship reuse_map.json
        # are covered by P1).
        return []

    elements = rmap.get("elements", [])
    if not elements:
        return []

    errors: list[str] = []

    # Determine built prefab path(s) from the map.
    built_prefab_paths: list[str] = []
    for elem in elements:
        bp = elem.get("builtPrefab") or elem.get("built_prefab")
        if bp and bp not in built_prefab_paths:
            built_prefab_paths.append(bp)

    for elem in elements:
        element_path = elem.get("elementPath", "?")
        source_str = elem.get("sourcePrefab", "")
        key_sprite_guid = elem.get("keySpriteGuid", "")  # or "any-nonnull"
        built_prefab_path = elem.get("builtPrefab") or elem.get("built_prefab") or (
            built_prefab_paths[0] if built_prefab_paths else None
        )

        if not source_str:
            errors.append(
                f"CRITICAL FAIL: reuse_map.json element '{element_path}' has no "
                f"sourcePrefab. Every element in the reuse map must cite its "
                f"clone source. (P1)"
            )
            continue

        source_guid = _extract_guid_from_source(source_str)
        if not source_guid:
            errors.append(
                f"P1 WARNING: reuse_map.json element '{element_path}' sourcePrefab "
                f"'{source_str[:80]}' has no parseable GUID — cannot verify YAML "
                f"lineage. Ensure sourcePrefab includes 'guid:HEXGUID' or is a bare "
                f"32-hex GUID."
            )
            continue

        # ----------------------------------------------------------------
        # Step 1: Check PrefabInstance lineage in the built prefab.
        # ----------------------------------------------------------------
        built_yaml: str | None = None
        if built_prefab_path:
            built_yaml = _read_prefab_yaml(built_prefab_path, repo_root)

        if built_yaml is None:
            # Try to find it by searching task_dir for *.prefab
            for candidate in task_dir.rglob("*.prefab"):
                try:
                    built_yaml = candidate.read_text(encoding="utf-8", errors="ignore")
                    break
                except Exception:
                    pass

        if built_yaml is None:
            # Cannot verify without the prefab — emit a warning, not a hard fail,
            # so this doesn't block tasks that don't have prefabs at P1 check time.
            errors.append(
                f"P1 WARNING: reuse_map.json element '{element_path}': built prefab "
                f"'{built_prefab_path or 'unknown'}' not found — cannot verify YAML "
                f"lineage. Ensure the built prefab is committed before the STATUS "
                f"transition."
            )
            continue

        source_guids_in_built = _parse_prefab_source_guids(built_yaml)
        has_prefab_instance_lineage = source_guid in source_guids_in_built

        # ----------------------------------------------------------------
        # Step 2: CopyAsset sprite-guid check.
        # ----------------------------------------------------------------
        go_name = element_path.split("/")[-1].strip()
        built_sprites = _parse_prefab_gameobject_sprites(built_yaml)
        built_sprite_guid = built_sprites.get(go_name)

        # Find source prefab YAML.
        source_prefab_file = _find_prefab_by_guid(source_guid, repo_root)
        source_yaml: str | None = None
        if source_prefab_file:
            try:
                source_yaml = source_prefab_file.read_text(encoding="utf-8", errors="ignore")
            except Exception:
                pass

        source_sprite_guid: str | None = None
        if source_yaml:
            source_sprites = _parse_prefab_gameobject_sprites(source_yaml)
            source_sprite_guid = source_sprites.get(go_name)

        # --- Verdict logic ---
        if has_prefab_instance_lineage:
            # PrefabInstance clone — lineage proven. Check the key sprite as extra.
            if key_sprite_guid and key_sprite_guid != "any-nonnull":
                if built_sprite_guid == "" or built_sprite_guid is None:
                    if source_sprite_guid:
                        errors.append(
                            f"CRITICAL FAIL (P1): element '{element_path}' has "
                            f"PrefabInstance lineage from source GUID {source_guid} "
                            f"but its Image.sprite is null/blank while the source "
                            f"carries sprite {source_sprite_guid}. This is the "
                            f"fabrication signature. (Rule 19 / P1)"
                        )
                        _log_p1_miss(task_dir, element_path, source_guid, repo_root)
        else:
            # No PrefabInstance lineage — check CopyAsset sprite equality.
            if source_yaml is None:
                # Can't find source to compare — warn, don't hard fail.
                errors.append(
                    f"P1 WARNING: element '{element_path}' has no PrefabInstance "
                    f"lineage from source {source_guid} and source prefab YAML could "
                    f"not be read — cannot do CopyAsset sprite check."
                )
                continue

            if built_sprite_guid == "" or built_sprite_guid is None:
                if source_sprite_guid:
                    # Null sprite where source has art — fabrication signature.
                    errors.append(
                        f"CRITICAL FAIL (P1): element '{element_path}' — no "
                        f"PrefabInstance lineage from source GUID {source_guid}, AND "
                        f"its Image.sprite is null/blank while the source element "
                        f"carries sprite {source_sprite_guid}. This matches the "
                        f"fabricated-provenance signature: element built from scratch "
                        f"with a false clone citation. (Rule 19 / P1)"
                    )
                    _log_p1_miss(task_dir, element_path, source_guid, repo_root)
                else:
                    # Neither side has a sprite — can't distinguish clone from scratch.
                    errors.append(
                        f"CRITICAL FAIL (P1): element '{element_path}' has no "
                        f"PrefabInstance lineage from source GUID {source_guid}, and "
                        f"neither the built element nor the source element carries a "
                        f"sprite — lineage cannot be proven. Block. (Rule 19 / P1)"
                    )
                    _log_p1_miss(task_dir, element_path, source_guid, repo_root)
            elif source_sprite_guid and built_sprite_guid != source_sprite_guid:
                # Different real sprites — legal re-skin, emit WARN.
                errors.append(
                    f"P1 WARN (legal re-skin): element '{element_path}' has no "
                    f"PrefabInstance lineage (CopyAsset clone expected), but carries "
                    f"a real sprite {built_sprite_guid} that differs from source "
                    f"sprite {source_sprite_guid}. Legal re-skin — not blocking. "
                    f"Reviewer should confirm intentional re-skin. (P1)"
                )
            # else: same sprite or source has none — PASS (or keySpriteGuid match).

    return errors


def _log_p1_miss(task_dir: Path, element_path: str, source_guid: str, repo_root: Path) -> None:
    """Append a CRITICAL FAIL entry to .claude/review_misses.log (same weight as Rule 6)."""
    log_path = repo_root / ".claude" / "review_misses.log"
    try:
        import datetime
        now = datetime.datetime.utcnow().isoformat(timespec="seconds") + "Z"
        task_name = task_dir.name
        line = (
            f"{now} | P1-CRITICAL-FAIL | task={task_name} | "
            f"element={element_path} | cited_source={source_guid} | "
            f"fabricated_provenance_detected\n"
        )
        with log_path.open("a", encoding="utf-8") as f:
            f.write(line)
    except Exception:
        pass  # Never let logging break the gate.


# ─────────────────────────────────────────────────────────────────────────────
# Phase 2 — Measure-before-surface gate (P7) + linter blind spots (P8).
# P8 is in UIFidelityLinter.cs (C# side); the hook enforces P7.
# ─────────────────────────────────────────────────────────────────────────────

# Files produced by P7's measure-before-surface requirement:
#   reference/<name>_ref_vs_built.png  (node render + built crop, stacked 1:1)
#   reference/<name>_deltas.json       (per-element measured deltas vs tolerance)
# The hook checks BOTH files exist and the deltas JSON is within tolerance.

_DELTAS_TOLERANCE_KEY = "tolerances"  # inside the deltas JSON: per-element tolerance map
_DELTAS_MEASURED_KEY = "measured"     # inside the deltas JSON: per-element measured map


def load_tolerance_table(task_dir: Path) -> dict | None:
    """Load tolerances.json from the task folder (per SPEC §4 template).

    Returns the parsed dict or None if the file does not exist. If absent the
    P7 gate is a no-op (tolerance table is optional — tasks without one skip P7).
    """
    tol_path = task_dir / "tolerances.json"
    if not tol_path.exists():
        return None
    try:
        return json.loads(tol_path.read_text(encoding="utf-8"))
    except Exception:
        return None


def validate_measure_before_surface(
    report_path: Path,
    task_dir: Path,
) -> list[str]:
    """P7: measure-before-surface gate for Figma-node card / panel tasks.

    Requires, for each surface named in tolerances.json:
      1. reference/<name>_ref_vs_built.png exists.
      2. reference/<name>_deltas.json exists and parses.
      3. Every element in the tolerance table is present in the deltas JSON.
      4. Every measured delta is within the tolerance.

    If tolerances.json is absent: no-op (P7 is opt-in per task).
    """
    tol_table = load_tolerance_table(task_dir)
    if tol_table is None:
        return []  # No tolerance table → P7 not required for this task.

    errors: list[str] = []
    surfaces = tol_table.get("surfaces", {})

    for surface_name, surface_spec in surfaces.items():
        # Check overlay image.
        overlay = task_dir / "reference" / f"{surface_name}_ref_vs_built.png"
        if not overlay.exists():
            errors.append(
                f"P7: measure-before-surface gate: missing overlay image "
                f"'reference/{surface_name}_ref_vs_built.png'. Produce the 1:1 "
                f"ref-vs-built overlay (node render + built crop stacked) before "
                f"surfacing. (P7 — postmortem Part 2: Cesar ran QA for 20 iters "
                f"because the builder eyeballed instead of measuring first.)"
            )
        # Check deltas JSON.
        deltas_path = task_dir / "reference" / f"{surface_name}_deltas.json"
        if not deltas_path.exists():
            errors.append(
                f"P7: measure-before-surface gate: missing deltas file "
                f"'reference/{surface_name}_deltas.json'. Measure each element's "
                f"delta against the tolerance table and write this file before "
                f"surfacing. (P7)"
            )
            continue
        try:
            deltas_data = json.loads(deltas_path.read_text(encoding="utf-8"))
        except Exception:
            errors.append(
                f"P7: 'reference/{surface_name}_deltas.json' is not valid JSON. "
                f"Re-generate it via the measurement script. (P7)"
            )
            continue

        measured = deltas_data.get("measured", {})
        element_tolerances = surface_spec.get("elements", {})

        for elem_name, tol_spec in element_tolerances.items():
            if elem_name not in measured:
                errors.append(
                    f"P7: '{surface_name}_deltas.json' missing element '{elem_name}'. "
                    f"Every element in the tolerance table must have a measured entry. "
                    f"(P7)"
                )
                continue
            m = measured[elem_name]
            for metric, allowed_delta in tol_spec.items():
                if metric not in m:
                    errors.append(
                        f"P7: '{surface_name}_deltas.json' element '{elem_name}' "
                        f"missing metric '{metric}'. (P7)"
                    )
                    continue
                actual_delta = abs(float(m[metric]))
                if actual_delta > float(allowed_delta):
                    errors.append(
                        f"P7: '{surface_name}_deltas.json' element '{elem_name}' "
                        f"metric '{metric}': delta {actual_delta:.1f}px exceeds "
                        f"tolerance ±{float(allowed_delta):.1f}px. Fix before "
                        f"surfacing. (P7)"
                    )
    return errors


# ─────────────────────────────────────────────────────────────────────────────
# Phase 3 — Guards (P4 + P5 + P6).
# ─────────────────────────────────────────────────────────────────────────────

_SHIPPED_MANIFEST_PATHS = [
    REPO_ROOT / "Docs" / "Specs" / "SHIPPED_MANIFEST.json",
    REPO_ROOT / "Docs" / "Specs" / "SHIPPED_MANIFEST.md",
]

# SaveData / save-schema content detector for P5.
_SAVE_SCHEMA_PATHS_RE = re.compile(
    r"SaveData|SaveSchemaMigrator|save.*schema|ShopTransaction",
    re.IGNORECASE,
)

# P6: canonical-surfaced line in STATUS.md.
# The orchestrator must write "canonical surfaced: <path> @ <timestamp>" before
# or when advancing a UI task to review.
_CANONICAL_SURFACED_RE = re.compile(
    r"canonical\s+surfaced\s*:\s*(.+?)\s*@\s*(\S+)",
    re.IGNORECASE,
)


def load_shipped_manifest(repo_root: Path) -> list[str]:
    """Load the shipped-asset manifest (P4). Returns list of asset paths.

    Checks paths relative to the passed repo_root first, then the module-level
    REPO_ROOT (for production use). This makes the function testable with temp dirs.
    """
    suffixes = ["Docs/Specs/SHIPPED_MANIFEST.json", "Docs/Specs/SHIPPED_MANIFEST.md"]
    candidates = [repo_root / s for s in suffixes] + list(_SHIPPED_MANIFEST_PATHS)
    seen: set[Path] = set()
    for manifest_path in candidates:
        resolved = manifest_path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        if not manifest_path.exists():
            continue
        try:
            text = manifest_path.read_text(encoding="utf-8", errors="ignore")
            if manifest_path.suffix == ".json":
                data = json.loads(text)
                return data.get("shipped_assets", [])
            else:
                # Markdown: extract lines that look like asset paths
                paths = []
                for line in text.splitlines():
                    stripped = line.strip().strip("`").strip()
                    if stripped.startswith("Assets/") and "." in stripped:
                        paths.append(stripped)
                return paths
        except Exception:
            continue
    return []


def validate_shipped_asset_guard(
    report_path: Path,
    spec_path: Path,
    repo_root: Path,
) -> list[str]:
    """P4: block any task whose working-tree diff touches a shipped manifest asset
    that the SPEC does not name as an explicit edit target.

    A 'shipped asset' is one listed in SHIPPED_MANIFEST.json/md.
    'Explicit SPEC authorization' means the asset path appears in SPEC.md.
    """
    manifest = load_shipped_manifest(repo_root)
    if not manifest:
        return []  # No manifest yet — gate is a no-op.

    # Get uncommitted paths from git.
    try:
        result = subprocess.run(
            ["git", "diff", "--name-only", "HEAD"],
            cwd=str(repo_root),
            capture_output=True,
            text=True,
            timeout=10,
        )
        staged_paths = set(result.stdout.splitlines())
    except Exception:
        staged_paths = set()

    try:
        result2 = subprocess.run(
            ["git", "status", "--porcelain", "--untracked-files=all"],
            cwd=str(repo_root),
            capture_output=True,
            text=True,
            timeout=10,
        )
        for line in result2.stdout.splitlines():
            if len(line) >= 3:
                p = line[3:].strip()
                if " -> " in p:
                    p = p.split(" -> ", 1)[1]
                staged_paths.add(p.strip('"'))
    except Exception:
        pass

    spec_text = ""
    if spec_path.exists():
        spec_text = spec_path.read_text(encoding="utf-8", errors="ignore")

    errors: list[str] = []
    for asset in manifest:
        asset_name = Path(asset).name
        # Check if this shipped asset appears in the current diff.
        touched = any(
            asset_name in p or asset in p
            for p in staged_paths
        )
        if not touched:
            continue
        # Check if SPEC explicitly authorizes it.
        if asset in spec_text or asset_name in spec_text:
            continue  # SPEC names it as an explicit edit target.
        errors.append(
            f"P4 (shipped-asset guard): working tree contains changes to "
            f"'{asset}' which is a SHIPPED deliverable in SHIPPED_MANIFEST. "
            f"The current task's SPEC does not name it as an explicit edit "
            f"target — touching a shipped asset without SPEC authorization is a "
            f"hard block. Either (a) add it to the SPEC as an authorized edit, "
            f"or (b) restore it to HEAD. (P4 — postmortem: 610 silently +68-line "
            f"edited the Order-517 StaminaShopSelectionScreen.prefab with no SPEC "
            f"mention; Rule 13 passed it because disclosure ≠ authorization.)"
        )
    return errors


def spec_touches_save_schema(spec_path: Path, repo_root: Path) -> bool:
    """P5 detector: True when the task's diff or SPEC.md mentions SaveData/schema."""
    if not spec_path.exists():
        return False
    spec_text = spec_path.read_text(encoding="utf-8", errors="ignore")
    if _SAVE_SCHEMA_PATHS_RE.search(spec_text):
        return True

    # Also scan git diff for save-schema paths.
    try:
        result = subprocess.run(
            ["git", "diff", "--name-only", "HEAD"],
            cwd=str(repo_root),
            capture_output=True,
            text=True,
            timeout=10,
        )
        for p in result.stdout.splitlines():
            if _SAVE_SCHEMA_PATHS_RE.search(p):
                return True
    except Exception:
        pass
    return False


# Matches the output of the Unity TestRunnerApi / tests-run tool: lines like
# "Total: 12", "Passed: 12", or "12/12 PASS" or "Tests passed: 12 of 12".
_TEST_RUN_RESULT_RE = re.compile(
    r"(?:Total|Passed|Failed|Skipped)\s*:\s*\d+"
    r"|(?:\d+\s*/\s*\d+\s*PASS)"
    r"|Tests\s+passed\s*:",
    re.IGNORECASE,
)

# Detect machine-authored (not human-prose) test result: must have a numeric count.
_TEST_MACHINE_COUNT_RE = re.compile(r"\bTotal\s*:\s*\d+", re.IGNORECASE)


def report_has_observed_test_run(report_path: Path) -> bool:
    """P5: True if the report contains machine-authored test-run evidence.

    Requires a 'Total: N' line (from the TestRunnerApi XML / tests-run tool),
    not just any prose mention of 'tests pass'.
    """
    if not report_path.exists():
        return False
    text = report_path.read_text(encoding="utf-8", errors="ignore")
    return bool(_TEST_MACHINE_COUNT_RE.search(text))


def validate_observed_test_run(report_path: Path, spec_path: Path, repo_root: Path) -> list[str]:
    """P5: for save-schema-touching tasks, require a machine-authored test-run result.

    A prose line saying 'tests passed' is not enough — the hook must see a
    'Total: N' / 'Passed: N' count that only a real test-runner invocation emits.
    """
    if not spec_touches_save_schema(spec_path, repo_root):
        return []
    if report_has_observed_test_run(report_path):
        return []
    return [
        "P5 (observed test-run gate): this task touches SaveData/SaveSchemaMigrator/"
        "save-schema code, but IMPLEMENTER_REPORT.md contains no machine-authored "
        "test-run result ('Total: N' / 'Passed: N' from the TestRunnerApi or "
        "tests-run tool). A prose claim that 'tests pass' is not accepted. Invoke "
        "`mcp__ai-game-developer__tests-run` (or the TestRunnerApi via "
        "script-execute) and paste the result summary. (P5 — postmortem: 488 lines "
        "of unverified save/economy code reached the gate on prose alone.)"
    ]


def validate_canonical_surfaced(task_dir: Path, spec_path: Path) -> list[str]:
    """P6: impl→review on a Figma-node UI task requires a logged 'canonical surfaced'
    line in STATUS.md (written by the orchestrator when it surfaces the image in chat).

    This makes the surface-image-in-chat rule structurally unbypassable — even when
    Cesar is away the gate fires, ensuring no iteration slips through unchecked.
    """
    if not spec_references_figma_node(spec_path):
        return []  # P6 only applies to Figma-node tasks.

    status_path = task_dir / "STATUS.md"
    if not status_path.exists():
        return [
            "P6 (canonical-surfaced gate): STATUS.md not found. Cannot verify "
            "the canonical-surfaced line. (P6)"
        ]
    status_text = status_path.read_text(encoding="utf-8", errors="ignore")
    m = _CANONICAL_SURFACED_RE.search(status_text)
    if not m:
        return [
            "P6 (canonical-surfaced gate): STATUS.md has no 'canonical surfaced: "
            "<path> @ <timestamp>' line. The orchestrator must surface the "
            "iteration's canonical screenshot in the main chat AND write this line "
            "to STATUS.md before dispatching any reviewer. This makes the "
            "surface-image-in-chat rule structurally unbypassable. (P6 — postmortem: "
            "the surface-in-chat rule was the ONLY gate that caught the 610 "
            "fabrication; it must not be silently skippable when Cesar is away.)"
        ]
    # Verify the referenced image file exists.
    image_path_str = m.group(1).strip().strip("`")
    image_path = task_dir / image_path_str
    if not image_path.exists():
        # Also try as repo-relative path.
        image_path_abs = REPO_ROOT / image_path_str
        if not image_path_abs.exists():
            return [
                f"P6 (canonical-surfaced gate): STATUS.md declares "
                f"'canonical surfaced: {image_path_str}' but that file does not "
                f"exist. The surfaced image must be the actual screenshot file in "
                f"the task's screenshots/ folder. (P6)"
            ]
    return []


# ─────────────────────────────────────────────────────────────────────────────
# Orchestrator.
# ─────────────────────────────────────────────────────────────────────────────


def get_uncommitted_paths_outside_spec(repo_root: Path, task_dir: Path) -> set[str]:
    """Rule 13 helper: paths from `git status --porcelain` that live outside the task's spec folder.

    Strips porcelain status codes (1-2 chars + space) and rename arrows
    ('old -> new' keeps 'new'). Quoted paths (whitespace in name) are unquoted.
    Paths inside `task_dir` are excluded; those are docs the close-out commit
    will carry, NOT the code-outside-the-folder class the rule targets.

    Empty set on git failure (not a repo, git missing, timeout) so a broken
    environment doesn't block the implementer with something they can't fix.
    """
    try:
        result = subprocess.run(
            ["git", "status", "--porcelain", "--untracked-files=all"],
            cwd=str(repo_root),
            capture_output=True,
            text=True,
            timeout=10,
        )
    except (subprocess.TimeoutExpired, FileNotFoundError, OSError):
        return set()
    if result.returncode != 0:
        return set()

    try:
        task_rel = task_dir.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        task_rel = None

    paths: set[str] = set()
    for raw_line in result.stdout.splitlines():
        if not raw_line or len(raw_line) < 3:
            continue
        line = raw_line[3:]
        if " -> " in line:
            line = line.split(" -> ", 1)[1]
        path = line.strip()
        if path.startswith('"') and path.endswith('"'):
            path = path[1:-1]
        if not path:
            continue
        if task_rel and (path == task_rel or path.startswith(task_rel + "/")):
            continue
        paths.add(path)
    return paths


def parse_files_modified_table(report_text: str) -> set[str]:
    """Rule 13 helper: parse 'Files modified or created' table; return path column.

    Falls back to 'Files modified' (without 'or created') for older formats.
    Strips backticks (canonical format wraps paths in backticks).
    """
    rows = parse_table_rows(report_text, "Files modified or created")
    if not rows:
        rows = parse_table_rows(report_text, "Files modified")
    paths: set[str] = set()
    for row in rows:
        if not row:
            continue
        cell = row[0].strip().strip("`").strip()
        if cell:
            paths.add(cell)
    return paths


def validate_files_modified_coverage(
    report_path: Path,
    repo_root: Path,
    task_dir: Path,
) -> list[str]:
    """Rule 13: uncommitted paths outside the spec folder must be in the report.

    For each path returned by `git status --porcelain` that doesn't live
    inside `task_dir`, verify that IMPLEMENTER_REPORT.md's "Files modified
    or created" table mentions it (substring match in either direction).

    Returns a list of errors, one per uncovered path. Implementer's two paths
    forward: (a) add the path to the report, or (b) restore/discard the path
    before retrying the STATUS write.
    """
    errors: list[str] = []
    if not report_path.exists():
        return errors  # Rule 1 already errored on this case.

    uncommitted = get_uncommitted_paths_outside_spec(repo_root, task_dir)
    if not uncommitted:
        return errors

    report_text = report_path.read_text(encoding="utf-8", errors="ignore")
    reported = parse_files_modified_table(report_text)

    for path in sorted(uncommitted):
        if any(path in r or r in path for r in reported):
            continue
        errors.append(
            f"IMPLEMENTER_REPORT.md: working tree has uncommitted path "
            f"'{path}' outside the task folder, but it does NOT appear in the "
            f"'Files modified or created' table. Add it to the report (if "
            f"intentional and part of this task) OR restore/discard before "
            f"the STATUS write. See Lesson AA: the spin_and_shot_shape_wiring "
            f"close-out missed PhysicsLabController.cs this exact way."
        )
    return errors


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
    review_path = task_dir / "ARCHITECT_REVIEW.md"

    # Reviewer -> red-team handoff (READY_FOR_REDTEAM). The reviewer must produce
    # an objective verdict before handing forward: Rule 16 mesh-metrics for mesh
    # tasks, Rule 18 Figma-fidelity table for Figma-node UI tasks. The
    # implementer-report rules (1-15) do NOT apply to the reviewer's own STATUS
    # write — this gate is about the reviewer's ARCHITECT_REVIEW.md.
    if new_status in REVIEWER_GATES:
        rt_errors: list[str] = []
        if spec_is_mesh_task(spec_path):
            rt_errors.extend(validate_mesh_metrics(review_path))
        if spec_references_figma_node(spec_path):
            rt_errors.extend(validate_figma_fidelity(review_path, "ARCHITECT_REVIEW.md"))
        if rt_errors:
            print(
                f"BLOCKED: cannot move STATUS to {new_status} - reviewer must "
                f"complete the objective-verdict gate:",
                file=sys.stderr,
            )
            print("", file=sys.stderr)
            for e in rt_errors:
                print(f"  - {e}", file=sys.stderr)
            print("", file=sys.stderr)
            print(
                "Mesh task: paste geometry numbers into ARCHITECT_REVIEW.md § Mesh "
                "metrics with PASS/FAIL. Figma task: add a § Figma fidelity table "
                "diffing each element against its pulled node render. Then retry.",
                file=sys.stderr,
            )
            return 2
        return 0

    # Rules 1-6 apply to both gating statuses.
    # Backend/no-Unity tasks (declared "no Unity/scene/prefab" / "No Assets/
    # changes") have no Game View — skip the screenshot + figma-reference gates.
    is_backend = spec_is_backend_task(spec_path)
    errors = validate_report(report_path, require_screenshot=not is_backend)

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
    if not is_backend and spec_requires_figma_reference(spec_path) and not figma_reference_present(task_dir):
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

    # Rule 13: uncommitted paths outside the spec folder must be in the
    # report's 'Files modified or created' table. Scar-tissue from
    # spin_and_shot_shape_wiring: the close-out commit was docs-only and
    # the 14-file implementation lived only in working tree.
    if report_path.exists():
        errors.extend(
            validate_files_modified_coverage(report_path, REPO_ROOT, task_dir)
        )

    # Rule 14: a report citing screenshots must declare one canonical frame and
    # it must clear the resolution floor (blocks the iter-9 256px-render PASS).
    errors.extend(validate_canonical_resolution(report_path))

    # Rule 15: when CESAR_REJECTION.md exists, the report must carry a
    # 'Rejection follow-up' section re-shooting the flagged defect at full res.
    errors.extend(validate_rejection_followup(report_path, task_dir))

    # Rule 17: mesh/terrain bakes must ship a fresh orbit video, not stills only.
    # The standing 'always show me video' rule, now enforced (green_slope_height_
    # bake skipped it on stills at iter-7/8/9/12). Scoped to mesh tasks.
    errors.extend(validate_video_deliverable(report_path, spec_path))

    # Rule 20: any video deliverable referenced in the report must be a continuous
    # recording, not an ffmpeg slideshow of a few posed stills. Blocks the
    # stamina_roster_live_meter fabricated-slideshow scar (failed self-review 2x,
    # 2026-06-30). mpdecimate distinct-frame count; gracefully skips if ffmpeg absent.
    errors.extend(validate_video_continuity(report_path))

    # Rule 18: Figma-node UI tasks must carry a per-element Figma fidelity table
    # in IMPLEMENTER_REPORT.md (the UI counterpart of Rule 16). Blocks the
    # vibe-match that let 1v1_ingame_ui ship with an explicit border token absent.
    if spec_references_figma_node(spec_path):
        errors.extend(validate_figma_fidelity(report_path, "IMPLEMENTER_REPORT.md"))
        # Rule 21: the automated counterpart — each built prefab must pass the
        # UIFidelityLinter (fail == 0), cited in a '## UI fidelity lint' section.
        # Catches the oval-pill / BUY-radius / dark-tint / flat-fill class of
        # defects deterministically, so Cesar stops being the visual gate.
        errors.extend(validate_ui_lint(report_path, REPO_ROOT))

    # Rule 19: reuse-mandate UI tasks must carry a per-element Clone provenance
    # table proving each reused element came from a real source artifact — not a
    # from-scratch rebuild. Blocks the tournament_round_loop signup-modal scar
    # (hand-rolled spriteless modal that falsely claimed clone PASS) and forces
    # the "surface, don't silently rebuild" rule when a source can't be found.
    if spec_requires_clone_provenance(spec_path):
        errors.extend(validate_clone_provenance(report_path))

    # ── Phase 1 (P1 + P3): YAML-based clone-provenance VERIFIER ──────────────
    # Supersedes the table-shape check for tasks that provide a reuse_map.json.
    # Design law: reads engine/YAML facts, never the implementer-authored table.
    # Critical-fail result → logged to .claude/review_misses.log (same weight as Rule 6).
    p1_errors = validate_clone_provenance_yaml(task_dir, REPO_ROOT)
    # CRITICAL FAILs are hard blocks; WARNs (legal re-skins) are surfaced but
    # do NOT block (they appear in the error list as informational context).
    hard_p1_errors = [e for e in p1_errors if "CRITICAL FAIL" in e]
    warn_p1_errors = [e for e in p1_errors if "CRITICAL FAIL" not in e]
    errors.extend(hard_p1_errors)
    if warn_p1_errors:
        # Surface warnings as non-blocking info lines so reviewers see them.
        # They don't contribute to the blocking error count.
        for w in warn_p1_errors:
            print(f"  [P1 info] {w}", file=sys.stderr)

    # ── Phase 2 (P7): measure-before-surface gate ────────────────────────────
    # For tasks with a tolerances.json: requires node-vs-built overlay + deltas
    # JSON within tolerance before any reviewer sees the surface.
    if not is_backend:
        errors.extend(validate_measure_before_surface(report_path, task_dir))

    # ── Phase 3a (P4): shipped-asset guard ───────────────────────────────────
    # Prevents silent modification of already-shipped deliverables without explicit
    # SPEC authorization. Seeds: SHIPPED_MANIFEST.json in Docs/Specs/.
    errors.extend(validate_shipped_asset_guard(report_path, spec_path, REPO_ROOT))

    # ── Phase 3b (P5): observed test-run gate ────────────────────────────────
    # For save-schema-touching tasks: prose "tests pass" is not evidence.
    errors.extend(validate_observed_test_run(report_path, spec_path, REPO_ROOT))

    # ── Phase 3c (P6): canonical-surfaced gate ───────────────────────────────
    # For Figma-node tasks: STATUS.md must carry a "canonical surfaced: <path> @ <ts>"
    # line proving the orchestrator surfaced the image in chat before dispatching reviewers.
    # DISABLED at impl→review: this gate only fires on the orchestrator side
    # (when route_subagent.py writes READY_FOR_REDTEAM → ARCHITECT_REVIEW_PASS).
    # P6 is a reminder constraint enforced later in the pipeline; skip here to
    # avoid blocking the implementer who has no access to write STATUS during dispatch.

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
