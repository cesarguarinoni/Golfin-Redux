#!/usr/bin/env python3
"""Unit tests for enforce_implementer_done.py.

Run with:  python -m unittest .claude/hooks/test_enforce_implementer_done.py
       or: python .claude/hooks/test_enforce_implementer_done.py

Coverage focus is Rules 10–12 (the green_authoring_editor_tool scar-tissue
rules added 2026-05-26). Pre-existing rules 1–9 have implicit coverage via
the dry-run-against-iter-4 integration check at the bottom.
"""
from __future__ import annotations

import json
import os
import struct
import sys
import tempfile
import textwrap
import time
import unittest
import zlib
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parent.parent
sys.path.insert(0, str(HERE))

import enforce_implementer_done as eid  # noqa: E402


# ─────────────────────────────────────────────────────────────────────────────
# Helpers — tiny PNG generator (uniform colour vs noise) so we can test the
# variance check without depending on golden fixtures in the repo.
# ─────────────────────────────────────────────────────────────────────────────


def _png_bytes(width: int, height: int, pixels: list[tuple[int, int, int]]) -> bytes:
    """Encode an RGB PNG with the given RAW pixel data (no compression magic)."""
    assert len(pixels) == width * height
    raw = bytearray()
    for row in range(height):
        raw.append(0)  # filter type "None"
        for col in range(width):
            r, g, b = pixels[row * width + col]
            raw.extend((r, g, b))
    idat = zlib.compress(bytes(raw))

    def chunk(tag: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)  # 8-bit RGB
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", idat)
        + chunk(b"IEND", b"")
    )


def _write_flat_png(path: Path, width: int = 200, height: int = 200, rgb=(128, 128, 128)) -> None:
    pixels = [rgb] * (width * height)
    path.write_bytes(_png_bytes(width, height, pixels))


def _write_noisy_png(path: Path, width: int = 200, height: int = 200) -> None:
    # Deterministic pseudo-noise so the test is reproducible.
    pixels = []
    n = width * height
    for i in range(n):
        v = (i * 2654435761) & 0xFF  # Knuth multiplicative hash
        pixels.append((v, (v + 73) & 0xFF, (v + 149) & 0xFF))
    path.write_bytes(_png_bytes(width, height, pixels))


# ─────────────────────────────────────────────────────────────────────────────
# Rule 10 — baseline block parsing & validation.
# ─────────────────────────────────────────────────────────────────────────────


class TestBaselineParsing(unittest.TestCase):
    SAMPLE = textwrap.dedent("""\
        2026-05-26T09:27:28 activated
        === iter-4 kickoff baseline 2026-05-26T12:00:00Z ===
        HEAD: d1d339c152d936087d8a98b5d4935fdfe85aeb0c
        DIRTY:
         M Assets/Plugins/NuGet/.nuget-installed.json
         M Assets/Scenes/ShellScene.unity
        ?? Assets/Scripts/Editor/GreenAuthoring/
        === END baseline ===
        2026-05-26T12:05:00Z analyzing iter-3 failures
    """)

    def test_finds_single_block(self):
        blocks = eid.parse_baseline_blocks(self.SAMPLE)
        self.assertEqual(len(blocks), 1)
        self.assertEqual(blocks[0]["iter"], 4)
        self.assertEqual(blocks[0]["head"], "d1d339c152d936087d8a98b5d4935fdfe85aeb0c")
        self.assertIn("Assets/Scenes/ShellScene.unity", blocks[0]["dirty_paths"])
        self.assertIn("Assets/Plugins/NuGet/.nuget-installed.json", blocks[0]["dirty_paths"])
        self.assertIn("Assets/Scripts/Editor/GreenAuthoring/", blocks[0]["dirty_paths"])

    def test_strips_porcelain_status_codes(self):
        blocks = eid.parse_baseline_blocks(self.SAMPLE)
        for path in blocks[0]["dirty_paths"]:
            self.assertFalse(path.startswith(" "), f"path '{path}' has leading space")
            self.assertFalse(path.startswith("M "), f"path '{path}' has porcelain prefix")
            self.assertFalse(path.startswith("?? "), f"path '{path}' has porcelain prefix")

    def test_missing_head_block_is_skipped(self):
        broken = self.SAMPLE.replace("HEAD: d1d339c152d936087d8a98b5d4935fdfe85aeb0c\n", "")
        self.assertEqual(eid.parse_baseline_blocks(broken), [])

    def test_missing_end_marker_block_is_skipped(self):
        broken = self.SAMPLE.replace("=== END baseline ===\n", "")
        self.assertEqual(eid.parse_baseline_blocks(broken), [])

    def test_validate_baseline_missing_file(self):
        with tempfile.TemporaryDirectory() as td:
            hb = Path(td) / "HEARTBEAT.log"
            errors, baseline = eid.validate_baseline(hb, expected_iter=1)
            self.assertEqual(baseline, None)
            self.assertTrue(any("HEARTBEAT.log not found" in e for e in errors))

    def test_validate_baseline_empty_file(self):
        with tempfile.TemporaryDirectory() as td:
            hb = Path(td) / "HEARTBEAT.log"
            hb.write_text("just some heartbeat lines, no baseline block\n")
            errors, baseline = eid.validate_baseline(hb, expected_iter=1)
            self.assertEqual(baseline, None)
            self.assertTrue(any("no valid" in e for e in errors))

    def test_validate_baseline_iter_mismatch(self):
        with tempfile.TemporaryDirectory() as td:
            hb = Path(td) / "HEARTBEAT.log"
            hb.write_text(self.SAMPLE)
            errors, baseline = eid.validate_baseline(hb, expected_iter=2)
            self.assertIsNotNone(baseline)  # falls back to latest
            self.assertTrue(any("no block for iter-2" in e for e in errors))

    def test_validate_baseline_iter_match(self):
        with tempfile.TemporaryDirectory() as td:
            hb = Path(td) / "HEARTBEAT.log"
            hb.write_text(self.SAMPLE)
            errors, baseline = eid.validate_baseline(hb, expected_iter=4)
            self.assertEqual(errors, [])
            self.assertEqual(baseline["iter"], 4)


class TestIterationExtraction(unittest.TestCase):
    def test_bold_iteration_marker(self):
        text = "# Implementer Report — foo\n\n> **Iteration:** 4 (some context)\n"
        self.assertEqual(eid.extract_iteration_number(text), 4)

    def test_plain_iteration_marker(self):
        text = "Iteration: 7\n"
        self.assertEqual(eid.extract_iteration_number(text), 7)

    def test_iter_dash_fallback(self):
        text = "# Implementer Report\n\nResumed iter-3 after FAIL.\n"
        self.assertEqual(eid.extract_iteration_number(text), 3)

    def test_no_marker_returns_none(self):
        text = "# Implementer Report\n\nFirst pass through the pipeline.\n"
        self.assertIsNone(eid.extract_iteration_number(text))


# ─────────────────────────────────────────────────────────────────────────────
# Rule 11 — "pre-existing" claim citation enforcement.
# ─────────────────────────────────────────────────────────────────────────────


class TestPreExistingClaims(unittest.TestCase):
    DIRTY = {
        "Assets/Scenes/ShellScene.unity",
        "Assets/Scripts/Editor/GreenAuthoring/",
        "Assets/Plugins/NuGet/.nuget-installed.json",
    }

    def test_inline_backtick_citation_passes(self):
        # Mirrors the iter-4 IMPLEMENTER_REPORT.md item 13 format.
        report = textwrap.dedent("""\
            | 13. No file modified outside new boundaries | PASS | The diff was pre-existing — `M Assets/Scenes/ShellScene.unity` appears on line 51 of the iter-4 kickoff baseline. |
        """)
        errors = eid.validate_preexisting_claims(report, self.DIRTY)
        self.assertEqual(errors, [], f"unexpected errors: {errors}")

    def test_fenced_code_citation_passes(self):
        report = textwrap.dedent("""\
            The 4-line TMP override is pre-existing in the working tree:

            ```
             M Assets/Scenes/ShellScene.unity
            ```

            Iter-4's gate did not introduce new contamination.
        """)
        errors = eid.validate_preexisting_claims(report, self.DIRTY)
        self.assertEqual(errors, [], f"unexpected errors: {errors}")

    def test_unsourced_claim_fails(self):
        # The iter-3 misattribution case: claim without citation.
        report = textwrap.dedent("""\
            The 4-line TMP override is from a previous session.

            Nothing more to say.
        """)
        errors = eid.validate_preexisting_claims(report, self.DIRTY)
        self.assertTrue(errors)
        self.assertTrue(any("UNSOURCED" in e for e in errors))

    def test_pre_existing_with_unrelated_backticks_fails(self):
        # Backticks present but content does NOT match any DIRTY path.
        report = textwrap.dedent("""\
            The diff is pre-existing — see `Foo.cs` for details.

            Nothing else.
        """)
        errors = eid.validate_preexisting_claims(report, self.DIRTY)
        self.assertTrue(errors)
        self.assertTrue(any("UNSOURCED" in e for e in errors))

    def test_predates_this_trigger_phrase(self):
        report = textwrap.dedent("""\
            That dirty file predates this iteration. No citation here.
        """)
        errors = eid.validate_preexisting_claims(report, self.DIRTY)
        self.assertTrue(errors)

    def test_was_already_in_trigger_phrase(self):
        report = textwrap.dedent("""\
            The change was already in `Assets/Scenes/ShellScene.unity` before iter-4.
        """)
        # 'was already in' is the trigger; the inline backtick on the same line cites the DIRTY path → PASS.
        errors = eid.validate_preexisting_claims(report, self.DIRTY)
        self.assertEqual(errors, [])

    def test_citation_5_lines_below_passes(self):
        report = textwrap.dedent("""\
            The diff is pre-existing per Item 13.
            Line 2.
            Line 3.
            Line 4.
            Line 5.
            ```
             M Assets/Scenes/ShellScene.unity
            ```
        """)
        errors = eid.validate_preexisting_claims(report, self.DIRTY)
        self.assertEqual(errors, [])

    def test_citation_more_than_5_lines_away_fails(self):
        report = textwrap.dedent("""\
            The diff is pre-existing per Item 13.
            Line 2.
            Line 3.
            Line 4.
            Line 5.
            Line 6.
            Line 7.
            Line 8.
            ```
             M Assets/Scenes/ShellScene.unity
            ```
        """)
        errors = eid.validate_preexisting_claims(report, self.DIRTY)
        self.assertTrue(errors, "citation 8 lines away should NOT count")

    def test_no_dirty_paths_skips_check(self):
        # When the baseline is missing entirely (Rule 10 already errored),
        # Rule 11 should not pile on with cascade errors.
        report = "The diff is pre-existing.\n"
        self.assertEqual(eid.validate_preexisting_claims(report, set()), [])


# ─────────────────────────────────────────────────────────────────────────────
# Rule 12 — synthetic flat-colour frame detection.
# ─────────────────────────────────────────────────────────────────────────────


class TestSyntheticFrameDetection(unittest.TestCase):
    def test_flat_grey_png_is_synthetic(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "flat.png"
            _write_flat_png(p, rgb=(128, 128, 128))
            var = eid.compute_image_variance(p)
            self.assertIsNotNone(var)
            self.assertLess(var, eid.VARIANCE_THRESHOLD)

    def test_flat_white_png_is_synthetic(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "white.png"
            _write_flat_png(p, rgb=(255, 255, 255))
            var = eid.compute_image_variance(p)
            self.assertIsNotNone(var)
            self.assertLess(var, eid.VARIANCE_THRESHOLD)

    def test_noisy_png_is_not_synthetic(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "noise.png"
            _write_noisy_png(p)
            var = eid.compute_image_variance(p)
            self.assertIsNotNone(var)
            self.assertGreater(var, eid.VARIANCE_THRESHOLD * 10)

    def test_real_greenauth_screenshot_is_not_synthetic(self):
        # Sanity-check against the iter-4 canonical frame.
        real = (
            REPO_ROOT
            / "Docs"
            / "Specs"
            / "Completed"
            / "green_authoring_editor_tool"
            / "screenshots"
            / "step6_post_pin_2026-05-26_16-37-04.png"
        )
        if not real.exists():
            self.skipTest("iter-4 canonical frame not present in this checkout")
        var = eid.compute_image_variance(real)
        self.assertIsNotNone(var)
        self.assertGreater(var, eid.VARIANCE_THRESHOLD)

    def test_validate_image_variances_flags_flat_frame(self):
        with tempfile.TemporaryDirectory() as td:
            task_dir = Path(td)
            (task_dir / "screenshots").mkdir()
            flat = task_dir / "screenshots" / "step8_fabricated.png"
            _write_flat_png(flat)
            report = task_dir / "IMPLEMENTER_REPORT.md"
            report.write_text(
                "## Screenshot\n\n"
                "Captured at: `screenshots/step8_fabricated.png`\n"
            )
            errors = eid.validate_image_variances(report)
            self.assertTrue(errors)
            self.assertTrue(any("step8_fabricated.png" in e for e in errors))
            self.assertTrue(any("variance" in e for e in errors))


# ─────────────────────────────────────────────────────────────────────────────
# Integration — verify the iter-4 green_authoring_editor_tool folder passes.
# ─────────────────────────────────────────────────────────────────────────────


class TestGreenAuthoringIter4Integration(unittest.TestCase):
    """Dry-run the three new rules against the real iter-4 task folder."""

    TASK_DIR = REPO_ROOT / "Docs" / "Specs" / "Completed" / "green_authoring_editor_tool"

    @classmethod
    def setUpClass(cls):
        if not cls.TASK_DIR.exists():
            raise unittest.SkipTest("green_authoring_editor_tool not present in this checkout")

    def test_baseline_block_exists_for_iter4(self):
        hb = self.TASK_DIR / "HEARTBEAT.log"
        errors, baseline = eid.validate_baseline(hb, expected_iter=4)
        self.assertEqual(errors, [], f"baseline validation errors: {errors}")
        self.assertEqual(baseline["iter"], 4)
        self.assertIn("Assets/Scenes/ShellScene.unity", baseline["dirty_paths"])

    def test_iter4_preexisting_claims_are_sourced(self):
        hb = self.TASK_DIR / "HEARTBEAT.log"
        report = self.TASK_DIR / "IMPLEMENTER_REPORT.md"
        _, baseline = eid.validate_baseline(hb, expected_iter=4)
        report_text = report.read_text(encoding="utf-8")
        errors = eid.validate_preexisting_claims(report_text, baseline["dirty_paths"])
        self.assertEqual(errors, [], f"iter-4 should have all citations sourced; errors: {errors}")

    def test_iter4_captures_pass_variance_check(self):
        report = self.TASK_DIR / "IMPLEMENTER_REPORT.md"
        errors = eid.validate_image_variances(report)
        self.assertEqual(errors, [], f"iter-4 captures should pass variance check; errors: {errors}")

    def test_iter4_iteration_extracted_as_4(self):
        report_text = (self.TASK_DIR / "IMPLEMENTER_REPORT.md").read_text(encoding="utf-8")
        self.assertEqual(eid.extract_iteration_number(report_text), 4)


# ─────────────────────────────────────────────────────────────────────────────
# End-to-end failure-mode regressions: simulate the iter-2 fabrication and
# iter-3 misattribution scars and confirm `main()` blocks the STATUS write.
# ─────────────────────────────────────────────────────────────────────────────


class TestEndToEndBlocking(unittest.TestCase):
    """Run the hook as a subprocess against synthetic task folders."""

    BASELINE_BLOCK = textwrap.dedent("""\
        === iter-1 kickoff baseline 2026-05-26T12:00:00Z ===
        HEAD: 0123456789abcdef0123456789abcdef01234567
        DIRTY:
         M Assets/Scenes/ShellScene.unity
        === END baseline ===
    """)

    SCAFFOLD_REPORT = textwrap.dedent("""\
        # Implementer Report — `_e2e_test`

        > **Iteration:** 1

        ## Acceptance checklist

        | Item | Result | Justification |
        |---|---|---|
        | 1. Stuff | PASS | Did stuff with great care and attention to detail. |

        ## Screenshot

        - **Canonical frame:** `screenshots/test.png`
    """)

    def _make_task(self, *, with_baseline=True, with_synthetic_png=False,
                   with_unsourced_claim=False) -> Path:
        # Resolve through symlinks (macOS aliases /tmp -> /private/tmp) so the
        # monkey-patched ACTIVE_DIR matches Path.resolve() output downstream.
        td = Path(tempfile.mkdtemp(prefix="hook_e2e_")).resolve()
        active = td / "Docs" / "Specs" / "Active" / "_e2e_test"
        (active / "screenshots").mkdir(parents=True)
        # SPEC.md (no test-runner or figma triggers; minimal).
        (active / "SPEC.md").write_text("# Spec\n\nSomething trivial.\n")
        # HEARTBEAT.log.
        hb_content = self.BASELINE_BLOCK if with_baseline else "no baseline here\n"
        (active / "HEARTBEAT.log").write_text(hb_content)
        # Screenshot.
        ss = active / "screenshots" / "test.png"
        if with_synthetic_png:
            _write_flat_png(ss)
        else:
            # >= MIN_CANONICAL_LONG_EDGE so the Rule 14 resolution floor passes.
            _write_noisy_png(ss, 960, 600)
        # IMPLEMENTER_REPORT.md.
        report = self.SCAFFOLD_REPORT
        if with_unsourced_claim:
            report = report.replace(
                "Did stuff with great care and attention to detail.",
                "Did stuff. The diff was from a previous session, unrelated to this work.",
            )
        (active / "IMPLEMENTER_REPORT.md").write_text(report)
        return td

    def _run_hook(self, task_root: Path, new_status: str = "READY_FOR_SELF_REVIEW"):
        import io
        import json as _json
        active = task_root / "Docs" / "Specs" / "Active" / "_e2e_test"
        payload = {
            "tool_input": {
                "file_path": str(active / "STATUS.md"),
                "content": new_status + "\n",
            },
        }
        # Monkey-patch the module constants in-place; do NOT reload (a reload
        # would reset them right back to the real repo paths).
        orig_repo = eid.REPO_ROOT
        orig_active = eid.ACTIVE_DIR
        eid.REPO_ROOT = task_root
        eid.ACTIVE_DIR = task_root / "Docs" / "Specs" / "Active"
        saved_stdin, saved_stderr = sys.stdin, sys.stderr
        try:
            sys.stdin = io.StringIO(_json.dumps(payload))
            sys.stderr = io.StringIO()
            rc = eid.main()
            err = sys.stderr.getvalue()
            return rc, err
        finally:
            sys.stdin, sys.stderr = saved_stdin, saved_stderr
            eid.REPO_ROOT = orig_repo
            eid.ACTIVE_DIR = orig_active

    def test_happy_path_passes(self):
        td = self._make_task()
        try:
            rc, err = self._run_hook(td)
            self.assertEqual(rc, 0, f"expected pass, got blocked with:\n{err}")
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_missing_baseline_blocks(self):
        td = self._make_task(with_baseline=False)
        try:
            rc, err = self._run_hook(td)
            self.assertEqual(rc, 2)
            self.assertIn("no valid", err)
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_iter2_synthetic_frame_scar_blocks(self):
        # The iter-2 green_authoring failure mode: a uniform-grey fabricated PNG
        # in the slideshow. The variance check should catch this.
        td = self._make_task(with_synthetic_png=True)
        try:
            rc, err = self._run_hook(td)
            self.assertEqual(rc, 2)
            self.assertIn("synthetic flat-colour frame", err)
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_iter3_misattribution_scar_blocks(self):
        # The iter-3 green_authoring failure mode: a "from a previous session"
        # claim that wasn't actually in git's working tree at iter start.
        # Our baseline has only ShellScene.unity dirty; the claim doesn't cite it.
        td = self._make_task(with_unsourced_claim=True)
        try:
            rc, err = self._run_hook(td)
            self.assertEqual(rc, 2)
            self.assertIn("UNSOURCED", err)
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)


class TestFilesModifiedCoverage(unittest.TestCase):
    """Rule 13 — uncommitted paths outside the spec folder must be in the report."""

    def _make_repo(self):
        """Create a real tiny git repo in a temp dir."""
        import subprocess
        td = Path(tempfile.mkdtemp(prefix="hook_rule13_")).resolve()
        subprocess.run(["git", "init", "-q"], cwd=td, check=True)
        subprocess.run(["git", "config", "user.email", "t@t.t"], cwd=td, check=True)
        subprocess.run(["git", "config", "user.name", "t"], cwd=td, check=True)
        (td / "seed.txt").write_text("seed\n")
        subprocess.run(["git", "add", "."], cwd=td, check=True)
        subprocess.run(["git", "commit", "-qm", "seed"], cwd=td, check=True)
        active = td / "Docs" / "Specs" / "Active" / "demo_task"
        active.mkdir(parents=True)
        return td, active

    def test_no_uncommitted_passes(self):
        td, active = self._make_repo()
        try:
            (active / "IMPLEMENTER_REPORT.md").write_text("# Report\n\n## Files modified or created\n\n| Path | Change |\n|---|---|\n")
            errors = eid.validate_files_modified_coverage(
                active / "IMPLEMENTER_REPORT.md", td, active
            )
            self.assertEqual(errors, [])
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_uncommitted_inside_spec_folder_passes(self):
        """Files inside the task folder don't count — those are docs."""
        td, active = self._make_repo()
        try:
            (active / "SPEC.md").write_text("# spec\n")  # untracked but inside task
            (active / "IMPLEMENTER_REPORT.md").write_text(
                "# Report\n\n## Files modified or created\n\n| Path | Change |\n|---|---|\n"
            )
            errors = eid.validate_files_modified_coverage(
                active / "IMPLEMENTER_REPORT.md", td, active
            )
            self.assertEqual(errors, [], f"unexpected errors: {errors}")
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_uncommitted_outside_spec_folder_not_in_report_fails(self):
        """The spin_and_shape PhysicsLabController case: file modified, not reported."""
        td, active = self._make_repo()
        try:
            (td / "Assets").mkdir()
            (td / "Assets" / "PhysicsLabController.cs").write_text("// untracked\n")
            (active / "IMPLEMENTER_REPORT.md").write_text(
                "# Report\n\n## Files modified or created\n\n"
                "| Path | Change |\n|---|---|\n"
                "| `Assets/SomeOtherFile.cs` | Modified |\n"
            )
            errors = eid.validate_files_modified_coverage(
                active / "IMPLEMENTER_REPORT.md", td, active
            )
            self.assertTrue(errors)
            self.assertTrue(any("PhysicsLabController.cs" in e for e in errors))
            self.assertTrue(any("Lesson AA" in e for e in errors))
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_uncommitted_outside_spec_folder_in_report_passes(self):
        td, active = self._make_repo()
        try:
            (td / "Assets").mkdir()
            (td / "Assets" / "Foo.cs").write_text("// untracked\n")
            (active / "IMPLEMENTER_REPORT.md").write_text(
                "# Report\n\n## Files modified or created\n\n"
                "| Path | Change |\n|---|---|\n"
                "| `Assets/Foo.cs` | Created |\n"
            )
            errors = eid.validate_files_modified_coverage(
                active / "IMPLEMENTER_REPORT.md", td, active
            )
            self.assertEqual(errors, [], f"unexpected errors: {errors}")
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_substring_match_either_direction(self):
        """Report may list parent dir while git lists individual files."""
        td, active = self._make_repo()
        try:
            (td / "Assets" / "GreenAuth").mkdir(parents=True)
            (td / "Assets" / "GreenAuth" / "A.cs").write_text("//\n")
            (active / "IMPLEMENTER_REPORT.md").write_text(
                "# Report\n\n## Files modified or created\n\n"
                "| Path | Change |\n|---|---|\n"
                "| `Assets/GreenAuth/` | New folder with 1 file |\n"
            )
            errors = eid.validate_files_modified_coverage(
                active / "IMPLEMENTER_REPORT.md", td, active
            )
            self.assertEqual(errors, [], f"unexpected errors: {errors}")
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_fallback_to_files_modified_heading(self):
        """Older reports use 'Files modified' (no 'or created') — must still match."""
        td, active = self._make_repo()
        try:
            (td / "Assets").mkdir()
            (td / "Assets" / "Foo.cs").write_text("//\n")
            (active / "IMPLEMENTER_REPORT.md").write_text(
                "# Report\n\n## Files modified\n\n"
                "| Path | Change |\n|---|---|\n"
                "| `Assets/Foo.cs` | Modified |\n"
            )
            errors = eid.validate_files_modified_coverage(
                active / "IMPLEMENTER_REPORT.md", td, active
            )
            self.assertEqual(errors, [])
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)

    def test_git_unavailable_returns_empty_safely(self):
        """No git repo → set() not exception; the hook treats this as 'nothing to check'."""
        td = Path(tempfile.mkdtemp(prefix="hook_rule13_nogit_")).resolve()
        try:
            paths = eid.get_uncommitted_paths_outside_spec(td, td)
            self.assertEqual(paths, set())
        finally:
            import shutil
            shutil.rmtree(td, ignore_errors=True)


class TestCanonicalResolution(unittest.TestCase):
    """Rule 14 — canonical-screenshot declaration + resolution floor."""

    def _report(self, body: str, tmp: Path) -> Path:
        p = tmp / "IMPLEMENTER_REPORT.md"
        p.write_text(body)
        return p

    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="hook_rule14_")).resolve()
        (self.tmp / "screenshots").mkdir()

    def tearDown(self):
        import shutil
        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_no_screenshots_skips(self):
        rp = self._report("# R\n\nNo images here.\n", self.tmp)
        self.assertEqual(eid.validate_canonical_resolution(rp), [])

    def test_cites_screenshot_but_no_canonical_blocks(self):
        _write_noisy_png(self.tmp / "screenshots" / "a.png", 960, 600)
        rp = self._report("# R\n\nSee `screenshots/a.png`.\n", self.tmp)
        errs = eid.validate_canonical_resolution(rp)
        self.assertTrue(any("no canonical" in e for e in errs), errs)

    def test_low_res_canonical_blocks(self):
        _write_noisy_png(self.tmp / "screenshots" / "small.png", 256, 256)
        rp = self._report(
            "# R\n\nCanonical screenshot: `screenshots/small.png`\n", self.tmp
        )
        errs = eid.validate_canonical_resolution(rp)
        self.assertTrue(any("256x256" in e for e in errs), errs)
        self.assertTrue(any("Rule 14" in e for e in errs), errs)

    def test_full_res_canonical_passes(self):
        _write_noisy_png(self.tmp / "screenshots" / "big.png", 1280, 720)
        rp = self._report(
            "# R\n\n**Canonical frame:** `screenshots/big.png`\n", self.tmp
        )
        self.assertEqual(eid.validate_canonical_resolution(rp), [])

    def test_iter9_256px_top_down_would_have_blocked(self):
        # The literal failure: only a 256px top-down designated canonical.
        _write_noisy_png(self.tmp / "screenshots" / "h07_iter9_overhead.png", 256, 256)
        rp = self._report(
            "# R\n\nCanonical screenshot: `screenshots/h07_iter9_overhead.png`\n",
            self.tmp,
        )
        errs = eid.validate_canonical_resolution(rp)
        self.assertEqual(len(errs), 1)
        self.assertIn("256px", errs[0])

    def test_png_dimensions_fallback_without_pillow(self):
        p = self.tmp / "screenshots" / "dim.png"
        _write_noisy_png(p, 901, 480)
        self.assertEqual(eid.image_dimensions(p), (901, 480))


class TestRejectionFollowup(unittest.TestCase):
    """Rule 15 — reproduce-the-rejection gate."""

    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="hook_rule15_")).resolve()
        (self.tmp / "screenshots").mkdir()

    def tearDown(self):
        import shutil
        shutil.rmtree(self.tmp, ignore_errors=True)

    def _report(self, body: str) -> Path:
        p = self.tmp / "IMPLEMENTER_REPORT.md"
        p.write_text(body)
        return p

    def test_no_rejection_file_skips(self):
        rp = self._report("# R\n\nNothing.\n")
        self.assertEqual(eid.validate_rejection_followup(rp, self.tmp), [])

    def test_rejection_present_no_section_blocks(self):
        (self.tmp / "CESAR_REJECTION.md").write_text("rejected: waves\n")
        rp = self._report("# R\n\nNo follow-up section.\n")
        errs = eid.validate_rejection_followup(rp, self.tmp)
        self.assertTrue(any("Rejection follow-up" in e for e in errs), errs)

    def test_section_without_verdict_or_image_blocks(self):
        (self.tmp / "CESAR_REJECTION.md").write_text("rejected: waves\n")
        rp = self._report("# R\n\n## Rejection follow-up\n\nLooked at it.\n")
        errs = eid.validate_rejection_followup(rp, self.tmp)
        self.assertEqual(len(errs), 2)  # missing verdict + missing image

    def test_complete_followup_passes(self):
        (self.tmp / "CESAR_REJECTION.md").write_text("rejected: waves\n")
        rp = self._report(
            "# R\n\n## Rejection follow-up\n\n"
            "The boundary waves are GONE — see `screenshots/grazing.png`.\n"
        )
        self.assertEqual(eid.validate_rejection_followup(rp, self.tmp), [])

    def test_still_present_is_accepted_verdict(self):
        # STILL PRESENT is a legitimate verdict (implementer should then BLOCK,
        # but the section itself is well-formed for Rule 15's purposes).
        (self.tmp / "CESAR_REJECTION.md").write_text("rejected: waves\n")
        rp = self._report(
            "# R\n\n## Rejection follow-up\n\n"
            "Waves STILL PRESENT — see `screenshots/grazing.png`.\n"
        )
        self.assertEqual(eid.validate_rejection_followup(rp, self.tmp), [])


class TestMeshMetrics(unittest.TestCase):
    """Rule 16 — mesh-task geometry-metrics gate on the reviewer's verdict."""

    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="hook_rule16_")).resolve()

    def tearDown(self):
        import shutil
        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_spec_mesh_detection(self):
        spec = self.tmp / "SPEC.md"
        spec.write_text("This bakes green.json and deforms the mesh via GreenTopology.\n")
        self.assertTrue(eid.spec_is_mesh_task(spec))

    def test_spec_non_mesh_not_flagged(self):
        spec = self.tmp / "SPEC.md"
        spec.write_text("Add a button to the roster screen and wire OnClick.\n")
        self.assertFalse(eid.spec_is_mesh_task(spec))

    def test_single_keyword_not_enough(self):
        spec = self.tmp / "SPEC.md"
        spec.write_text("The contour of the UI card is rounded.\n")  # 1 hit only
        self.assertFalse(eid.spec_is_mesh_task(spec))

    def test_missing_review_blocks(self):
        errs = eid.validate_mesh_metrics(self.tmp / "ARCHITECT_REVIEW.md")
        self.assertTrue(any("not found" in e for e in errs), errs)

    def test_no_metrics_section_blocks(self):
        rv = self.tmp / "ARCHITECT_REVIEW.md"
        rv.write_text("# Review\n\n## Verdict\n\nPASS, looks great.\n")
        errs = eid.validate_mesh_metrics(rv)
        self.assertTrue(any("no '## Mesh metrics'" in e for e in errs), errs)

    def test_metrics_without_numbers_blocks(self):
        rv = self.tmp / "ARCHITECT_REVIEW.md"
        rv.write_text("# Review\n\n## Mesh metrics\n\nLooks fine, no numbers.\n")
        errs = eid.validate_mesh_metrics(rv)
        self.assertTrue(any("no numeric" in e for e in errs), errs)

    def test_metrics_with_numbers_passes(self):
        rv = self.tmp / "ARCHITECT_REVIEW.md"
        rv.write_text(
            "# Review\n\n## Mesh metrics\n\n"
            "min collar normal.y = 0.62 (PASS, >0.3); max boundary Y-step = 0.04m "
            "(PASS, <0.08); boundary verts = 170.\n"
        )
        self.assertEqual(eid.validate_mesh_metrics(rv), [])


class TestVideoDeliverable(unittest.TestCase):
    """Rule 17 — mesh/terrain bakes must ship a fresh orbit video, not stills."""

    MESH_SPEC = (
        "This task bakes green.json and deforms the mesh via GreenTopology "
        "(skirt + contour).\n"
    )
    UI_SPEC = "Add a button to the roster screen and wire OnClick.\n"

    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="hook_rule17_")).resolve()
        (self.tmp / "videos").mkdir()
        self.spec = self.tmp / "SPEC.md"

    def tearDown(self):
        import shutil
        shutil.rmtree(self.tmp, ignore_errors=True)

    def _report(self, body: str) -> Path:
        p = self.tmp / "IMPLEMENTER_REPORT.md"
        p.write_text(body)
        return p

    def _video(self, name: str, nbytes: int) -> None:
        (self.tmp / "videos" / name).write_bytes(b"\0" * nbytes)

    def test_non_mesh_task_skips(self):
        self.spec.write_text(self.UI_SPEC)
        rp = self._report("# R\n\nNo video, but this is a UI task.\n")
        self.assertEqual(eid.validate_video_deliverable(rp, self.spec), [])

    def test_mesh_task_without_video_blocks(self):
        self.spec.write_text(self.MESH_SPEC)
        rp = self._report("# R\n\nHere are some stills, no clip.\n")
        errs = eid.validate_video_deliverable(rp, self.spec)
        self.assertTrue(any("declares no canonical video" in e for e in errs), errs)
        self.assertTrue(any("Rule 17" in e for e in errs), errs)

    def test_mesh_task_declared_video_missing_file_blocks(self):
        self.spec.write_text(self.MESH_SPEC)
        rp = self._report(
            "# R\n\nCanonical video: `videos/h07_orbit.mp4`\n"
        )  # no file written
        errs = eid.validate_video_deliverable(rp, self.spec)
        self.assertTrue(any("does not" in e for e in errs), errs)

    def test_mesh_task_tiny_video_blocks(self):
        self.spec.write_text(self.MESH_SPEC)
        self._video("h07_orbit.mp4", 1234)  # < MIN_VIDEO_BYTES
        rp = self._report("# R\n\nCanonical video: `videos/h07_orbit.mp4`\n")
        errs = eid.validate_video_deliverable(rp, self.spec)
        self.assertTrue(any("empty/placeholder" in e for e in errs), errs)

    def test_mesh_task_real_video_passes(self):
        self.spec.write_text(self.MESH_SPEC)
        self._video("h07_orbit.mp4", eid.MIN_VIDEO_BYTES + 1)
        rp = self._report(
            "# R\n\n**Canonical clip:** `videos/h07_orbit.mp4`\n"
        )
        self.assertEqual(eid.validate_video_deliverable(rp, self.spec), [])

    def test_task_rooted_video_path_resolves(self):
        # Report cites the full Docs/Specs path; resolver falls back to REPO_ROOT.
        self.spec.write_text(self.MESH_SPEC)
        self._video("h07_orbit.mp4", eid.MIN_VIDEO_BYTES + 1)
        rp = self._report(
            "# R\n\nCanonical video: `videos/h07_orbit.mp4`\n"
        )
        # report-relative resolution should find it under self.tmp/videos
        self.assertEqual(eid.validate_video_deliverable(rp, self.spec), [])


class TestFigmaFidelity(unittest.TestCase):
    """Rule 18 — Figma-node UI tasks need a per-element Figma fidelity table."""

    UI_FIGMA_SPEC = (
        "## Reference\n\nFigma frame 'In-Game - 1v1' node 13177:1937 in file "
        "https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/x?node-id=13177-1937\n"
    )
    UI_NODE_ONLY_SPEC = "Match the Figma banner node 4094:26038 exactly.\n"
    NO_FIGMA_SPEC = "Add a button to the roster screen and wire OnClick. NO new Figma.\n"
    GOOD_TABLE = (
        "# Report\n\n## Figma fidelity\n\n"
        "| Element | Figma node | Figma value | Built | Result |\n"
        "|---|---|---|---|---|\n"
        "| Banner border | 4094:26038 | 3px #818EA1 top+bottom | 3px #818EA1 | PASS |\n"
        "| Map position | 13177:1937 | above Fade/Draw | above Fade/Draw | PASS |\n"
    )

    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="hook_rule18_")).resolve()
        self.spec = self.tmp / "SPEC.md"

    def tearDown(self):
        import shutil
        shutil.rmtree(self.tmp, ignore_errors=True)

    # --- detector ---
    def test_detect_figma_url(self):
        self.spec.write_text(self.UI_FIGMA_SPEC)
        self.assertTrue(eid.spec_references_figma_node(self.spec))

    def test_detect_figma_word_plus_node_id(self):
        self.spec.write_text(self.UI_NODE_ONLY_SPEC)
        self.assertTrue(eid.spec_references_figma_node(self.spec))

    def test_no_figma_not_flagged(self):
        self.spec.write_text(self.NO_FIGMA_SPEC)
        self.assertFalse(eid.spec_references_figma_node(self.spec))

    def test_bare_node_id_without_figma_word_not_flagged(self):
        # A "12:34"-style token with no Figma context must NOT trip the gate.
        self.spec.write_text("The build ran at 13:37 and touched lines 10-20.\n")
        self.assertFalse(eid.spec_references_figma_node(self.spec))

    # --- validator ---
    def test_missing_doc_blocks(self):
        errs = eid.validate_figma_fidelity(self.tmp / "IMPLEMENTER_REPORT.md", "IMPLEMENTER_REPORT.md")
        self.assertTrue(any("not found" in e for e in errs), errs)

    def test_no_section_blocks(self):
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text("# Report\n\n## Notes\n\nLooks like Figma, matches the design.\n")
        errs = eid.validate_figma_fidelity(rp, "IMPLEMENTER_REPORT.md")
        self.assertTrue(any("no '## Figma fidelity' section" in e for e in errs), errs)

    def test_section_without_table_blocks(self):
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text("# Report\n\n## Figma fidelity\n\nEverything matches node 13177:1937. PASS.\n")
        errs = eid.validate_figma_fidelity(rp, "IMPLEMENTER_REPORT.md")
        self.assertTrue(any("no table data rows" in e for e in errs), errs)

    def test_table_without_node_citation_blocks(self):
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text(
            "# Report\n\n## Figma fidelity\n\n"
            "| Element | Result |\n|---|---|\n| Banner | PASS |\n"
        )
        errs = eid.validate_figma_fidelity(rp, "IMPLEMENTER_REPORT.md")
        self.assertTrue(any("cites no Figma node" in e for e in errs), errs)

    def test_table_without_passfail_blocks(self):
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text(
            "# Report\n\n## Figma fidelity\n\n"
            "| Element | Figma node | Note |\n|---|---|---|\n"
            "| Banner | 4094:26038 | matches |\n"
        )
        errs = eid.validate_figma_fidelity(rp, "IMPLEMENTER_REPORT.md")
        self.assertTrue(any("no PASS/FAIL verdict" in e for e in errs), errs)

    def test_good_table_passes(self):
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text(self.GOOD_TABLE)
        self.assertEqual(eid.validate_figma_fidelity(rp, "IMPLEMENTER_REPORT.md"), [])


class TestCloneProvenance(unittest.TestCase):
    """Rule 19 — reuse-mandate tasks need a per-element Clone provenance table
    proving each element was cloned from a real source, not built from scratch."""

    REUSE_SPEC = (
        "# SPEC\n\n## 0. REUSE MANDATE (read first)\n\n"
        "> Clone-and-modify existing GameObjects. Author ZERO new panels, "
        "buttons, separators, or sprites.\n"
    )
    NO_REUSE_SPEC = "Add a new TournamentRoundContext static class and wire the seam.\n"
    GOOD_TABLE = (
        "# Report\n\n## Clone provenance\n\n"
        "| Element | Cloned from | How verified |\n"
        "|---|---|---|\n"
        "| Navy panel | `Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab` Root/Card1 | sprite GUID 064cba0b0bc85154995fa70dd470817b on Panel Image |\n"
        "| CONFIRM button | gold Main Button, guid d7b1c62bfcb4e844ab498b958b38aede | Image.sprite set, ButtonPressFeedback present |\n"
        "| Separator | `Assets/Art/LoadingScreen/Divider.png` | sprite assigned |\n"
    )

    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="hook_rule19_")).resolve()
        self.spec = self.tmp / "SPEC.md"

    def tearDown(self):
        import shutil
        shutil.rmtree(self.tmp, ignore_errors=True)

    # --- detector ---
    def test_detect_reuse_mandate(self):
        self.spec.write_text(self.REUSE_SPEC)
        self.assertTrue(eid.spec_requires_clone_provenance(self.spec))

    def test_detect_clone_from_phrase(self):
        self.spec.write_text("Step 0: clone from the existing MatchmakingModal prefab.\n")
        self.assertTrue(eid.spec_requires_clone_provenance(self.spec))

    def test_no_reuse_mandate_not_flagged(self):
        self.spec.write_text(self.NO_REUSE_SPEC)
        self.assertFalse(eid.spec_requires_clone_provenance(self.spec))

    # --- validator ---
    def test_missing_section_blocks(self):
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text("# Report\n\n## Notes\n\nBuilt the modal, matches the family.\n")
        errs = eid.validate_clone_provenance(rp)
        self.assertTrue(any("no '## Clone provenance' section" in e for e in errs), errs)

    def test_section_without_table_blocks(self):
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text("# Report\n\n## Clone provenance\n\nEverything cloned from HoleCompleteModal.\n")
        errs = eid.validate_clone_provenance(rp)
        self.assertTrue(any("no table data rows" in e for e in errs), errs)

    def test_prose_only_row_blocks(self):
        # The exact failure mode: a row claiming a clone with no source artifact.
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text(
            "# Report\n\n## Clone provenance\n\n"
            "| Element | Cloned from | How |\n|---|---|---|\n"
            "| Navy panel | the modal family navy panel | looks right |\n"
        )
        errs = eid.validate_clone_provenance(rp)
        self.assertTrue(any("cites no concrete source" in e for e in errs), errs)

    def test_not_found_marker_hard_blocks(self):
        # "built from scratch" / "not found" must block and force IMPLEMENTER_BLOCKED.
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text(
            "# Report\n\n## Clone provenance\n\n"
            "| Element | Cloned from | How |\n|---|---|---|\n"
            "| Navy panel | NOT FOUND built from scratch | flat color Image |\n"
        )
        errs = eid.validate_clone_provenance(rp)
        self.assertTrue(any("from-scratch" in e or "not-found" in e for e in errs), errs)

    def test_good_table_passes(self):
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text(self.GOOD_TABLE)
        self.assertEqual(eid.validate_clone_provenance(rp), [])

    def test_tournament_round_loop_scar_would_block(self):
        # The actual scar: prose-only "clone" claims with no source artifacts.
        rp = self.tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text(
            "# Report\n\n## Clone provenance\n\n"
            "| Element | Cloned from | Result |\n|---|---|---|\n"
            "| Panel | navy panel clone | PASS |\n"
            "| CANCEL | silver button clone | PASS |\n"
            "| Separator | cloned from HoleCompleteModal | PASS |\n"
        )
        errs = eid.validate_clone_provenance(rp)
        self.assertGreaterEqual(len(errs), 2, errs)


class TestVideoContinuity(unittest.TestCase):
    """Rule 20: a video deliverable must be a continuous recording, not a
    slideshow. Probes are monkeypatched so the test needs no ffmpeg."""

    def _report_with_video(self, tmp: Path, distinct, duration, size_ok=True):
        vids = tmp / "videos"
        vids.mkdir(parents=True, exist_ok=True)
        clip = vids / "demo.mp4"
        clip.write_bytes(b"\x00" * (eid.MIN_VIDEO_BYTES + 1 if size_ok else 10))
        rp = tmp / "IMPLEMENTER_REPORT.md"
        rp.write_text("Canonical video: `videos/demo.mp4`\n")
        eid._video_distinct_frames = lambda v: distinct
        eid._video_duration_seconds = lambda v: duration
        return rp

    def setUp(self):
        self._orig_df = eid._video_distinct_frames
        self._orig_dur = eid._video_duration_seconds

    def tearDown(self):
        eid._video_distinct_frames = self._orig_df
        eid._video_duration_seconds = self._orig_dur

    def test_slideshow_blocks(self):
        with tempfile.TemporaryDirectory() as d:
            rp = self._report_with_video(Path(d), distinct=5, duration=30.0)
            errs = eid.validate_video_continuity(rp)
            self.assertEqual(len(errs), 1, errs)
            self.assertIn("SLIDESHOW", errs[0])

    def test_continuous_passes(self):
        with tempfile.TemporaryDirectory() as d:
            rp = self._report_with_video(Path(d), distinct=420, duration=14.0)
            self.assertEqual(eid.validate_video_continuity(rp), [])

    def test_short_clip_not_gated(self):
        # Below SLIDESHOW_MIN_DURATION_S: skipped even with few distinct frames.
        with tempfile.TemporaryDirectory() as d:
            rp = self._report_with_video(Path(d), distinct=3, duration=2.0)
            self.assertEqual(eid.validate_video_continuity(rp), [])

    def test_ffmpeg_absent_skips_gracefully(self):
        # Probe returning None (ffmpeg unavailable) must never block.
        with tempfile.TemporaryDirectory() as d:
            rp = self._report_with_video(Path(d), distinct=None, duration=30.0)
            self.assertEqual(eid.validate_video_continuity(rp), [])

    def test_boundary_exactly_max_distinct_blocks(self):
        with tempfile.TemporaryDirectory() as d:
            rp = self._report_with_video(
                Path(d),
                distinct=eid.SLIDESHOW_MAX_DISTINCT_FRAMES,
                duration=10.0,
            )
            self.assertEqual(len(errs := eid.validate_video_continuity(rp)), 1, errs)


class TestBackendExemption(unittest.TestCase):
    """Rule 5 screenshot gate is skipped for declared backend/no-Unity tasks
    (spec_is_backend_task) — figma_node_spec_generator (2026-07-03)."""

    def _spec(self, text: str) -> Path:
        td = Path(tempfile.mkdtemp(prefix="hook_backend_")).resolve()
        p = td / "SPEC.md"
        p.write_text(text, encoding="utf-8")
        return p

    def test_detects_no_unity_scene_prefab(self):
        self.assertTrue(eid.spec_is_backend_task(self._spec(
            "Tier 2 — one Python script + unit tests, no Unity/scene/prefab changes.")))

    def test_detects_no_assets_changes(self):
        self.assertTrue(eid.spec_is_backend_task(self._spec(
            "## Files\n- No `Assets/` changes. No Unity/scene/prefab edits.")))

    def test_ui_task_reusing_prefabs_not_exempted(self):
        # "no NEW prefab" / clone language must NOT trip the backend detector.
        self.assertFalse(eid.spec_is_backend_task(self._spec(
            "Reuse existing atoms; author no NEW prefab, clone the navy panel.")))

    def test_plain_figma_ui_spec_not_exempted(self):
        self.assertFalse(eid.spec_is_backend_task(self._spec(
            "# Spec\nBuild the shop card from Figma node 13156:1232.")))

    # ── content_cursor_per_catalog §7 (2026-08-25) ──────────────────────────
    # content_catalog/SPEC.md wrote "No `Assets/` **edits**" where the regex
    # only knew "changes". One word forced four inapplicable gates (screenshot,
    # figma-reference.png, Figma fidelity table, UI lint) onto a spec that opens
    # "No Figma. This task has no UI surface." The implementer left them failing
    # rather than fabricating a screenshot — the right call — so the HOOK is what
    # got fixed, both narrowly (synonyms) and durably (an explicit field).

    def test_detects_no_assets_edits(self):
        """The exact wording content_catalog used."""
        self.assertTrue(eid.spec_is_backend_task(self._spec(
            "## Out of scope\n- No `Assets/` edits — the client reader is Phase 1.")))

    def test_detects_no_assets_modifications(self):
        self.assertTrue(eid.spec_is_backend_task(self._spec(
            "No `Assets/` modifications are made by this task.")))

    def test_spec_kind_backend_field_is_honoured(self):
        """The DURABLE detector: a declared field, not a matched phrase."""
        self.assertTrue(eid.spec_is_backend_task(self._spec(
            "# SPEC — `thing`\n\nSPEC_KIND: backend\n\nBuilds a Python tool.")))

    def test_spec_kind_tolerates_markdown_decoration(self):
        for line in ("> SPEC_KIND: backend", "- SPEC_KIND: backend",
                     "  SPEC_KIND:  backend", "spec_kind: Backend"):
            with self.subTest(line=line):
                self.assertTrue(eid.spec_is_backend_task(self._spec(f"# SPEC\n\n{line}\n")))

    def test_spec_kind_ui_is_not_backend(self):
        self.assertFalse(eid.spec_is_backend_task(self._spec(
            "# SPEC\n\nSPEC_KIND: ui\n\nBuild the modal from Figma node 13156:1232.")))

    def test_spec_kind_in_prose_backticks_does_not_declare(self):
        """Discussing the field is not declaring it — otherwise this very hook's
        own documentation would exempt every spec that quotes it."""
        self.assertFalse(eid.spec_is_backend_task(self._spec(
            "# SPEC\n\nHonour an explicit `SPEC_KIND: backend` line near the top of SPEC.md.")))

    def test_backend_spec_skips_the_figma_node_ui_gates(self):
        """Rules 18/21 are scoped to non-backend specs.

        FIGMA_NODE_ID_RE matches a DATE ("2026-08"), so a backend spec that says
        the word "figma" even to say it has none was being handed a Figma
        fidelity table + UI lint requirement it could only satisfy by inventing
        one. The node detector may still fire; `is_backend` is what gates.
        """
        spec = self._spec(
            "# SPEC\n\nSPEC_KIND: backend\n\nNo Figma. Measured on prod 2026-08-25.\n")
        self.assertTrue(eid.spec_references_figma_node(spec),
                        "precondition: the node detector still fires on the date token")
        self.assertTrue(eid.spec_is_backend_task(spec),
                        "so is_backend is the thing that must gate Rules 18/21")

    def test_backend_spec_skips_the_clone_provenance_gate(self):
        """Rule 19 is scoped to non-backend specs (content_admin_panels).

        CLONE_SOURCE_RE accepts only a .prefab path, an Assets/ path or a 32-hex
        Unity GUID, so a Next.js dashboard task cannot cite one truthfully. A
        spec that says "do not rebuild" (meaning: build on the existing API
        routes) would otherwise hit a gate satisfiable only by inventing an
        Assets/ path.
        """
        spec = self._spec(
            "# SPEC\n\nSPEC_KIND: backend\n\n"
            "## What already exists (verified live — do not rebuild)\n"
            "Six route handlers. Build the UI on top of them.\n"
        )
        self.assertTrue(eid.spec_requires_clone_provenance(spec),
                        "precondition: 'do not rebuild' still trips the Rule 19 detector")
        self.assertTrue(eid.spec_is_backend_task(spec),
                        "so is_backend is what must gate it")

    def test_real_spec_files_are_detected_as_backend(self):
        """The two live specs this change exists for."""
        repo = Path(__file__).resolve().parents[2]
        for name in ("content_catalog", "content_cursor_per_catalog"):
            spec = repo / "Docs/Specs/Active" / name / "SPEC.md"
            if not spec.exists():
                spec = repo / "Docs/Specs/Completed" / name / "SPEC.md"
            if not spec.exists():
                continue  # moved on; the synthetic cases above still cover it
            with self.subTest(spec=name):
                self.assertTrue(eid.spec_is_backend_task(spec), f"{name} must be backend")

    def _report_without_screenshot(self) -> Path:
        td = Path(tempfile.mkdtemp(prefix="hook_backend_rep_")).resolve()
        p = td / "IMPLEMENTER_REPORT.md"
        p.write_text(textwrap.dedent("""\
            # Implementer Report

            ## Acceptance checklist

            | Item | Result | Justification |
            |---|---|---|
            | 1. Generator emits valid spec.json | PASS | Round-trips JsonUtility; 12 unit tests green. |
        """), encoding="utf-8")
        return p

    def test_require_screenshot_true_blocks_when_missing(self):
        errs = eid.validate_report(self._report_without_screenshot(), require_screenshot=True)
        self.assertTrue(any("Screenshot" in e for e in errs), errs)

    def test_require_screenshot_false_allows_missing(self):
        errs = eid.validate_report(self._report_without_screenshot(), require_screenshot=False)
        self.assertEqual(errs, [], f"backend task should not require a screenshot; got {errs}")


class TestCloneProvenanceYAML(unittest.TestCase):
    """Order-611 Phase 1 acceptance tests (A1-A5).

    Design law §0: gates read engine/YAML facts, never implementer-authored prose.

    All tests construct minimal synthetic .prefab YAML in tempfiles — no dependency
    on real project assets or scratchpad/general_shop_ui_discarded_tracked.patch.
    """

    # ── Helpers ────────────────────────────────────────────────────────────────

    _SOURCE_GUID = "aaaa0000bbbb1111cccc2222dddd3333"
    _OTHER_GUID  = "ffff0000eeee1111dddd2222cccc3333"
    _SPRITE_GUID = "1234567890abcdef1234567890abcdef"
    _SPRITE_B_GUID = "fedcba0987654321fedcba0987654321"

    def _make_temp_prefab(self, yaml_text: str, name: str = "TestPrefab.prefab") -> Path:
        """Write a synthetic prefab YAML to a temp file and return the Path."""
        d = Path(tempfile.mkdtemp(prefix="hook_yaml_"))
        p = d / name
        p.write_text(yaml_text, encoding="utf-8")
        return p

    def _prefab_with_prefab_instance(self, source_guid: str, sprite_guid: str | None = None) -> str:
        """Build minimal prefab YAML containing a PrefabInstance + an Image."""
        null_sprite = "m_Sprite: {fileID: 0, guid: , type: 0}"
        real_sprite = (
            f"m_Sprite: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}"
            if sprite_guid else null_sprite
        )
        return textwrap.dedent(f"""\
            %YAML 1.1
            %TAG !u! tag:unity3d.com,2011:
            --- !u!1 &100
            GameObject:
              m_Name: CardRoot
            --- !u!1001 &200
            PrefabInstance:
              m_SourcePrefab: {{fileID: 100100000, guid: {source_guid}, type: 3}}
              m_Modification:
                m_Modifications: []
            --- !u!114 &300
            MonoBehaviour:
              m_GameObject: {{fileID: 100}}
              m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
              {real_sprite}
        """)

    def _prefab_scratch_null_sprite(self) -> str:
        """Fabricated prefab: no PrefabInstance, null sprite — the fabrication signature."""
        return textwrap.dedent("""\
            %YAML 1.1
            %TAG !u! tag:unity3d.com,2011:
            --- !u!1 &100
            GameObject:
              m_Name: CardRoot
            --- !u!114 &300
            MonoBehaviour:
              m_GameObject: {fileID: 100}
              m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
              m_Sprite: {fileID: 0, guid: , type: 0}
        """)

    def _prefab_scratch_real_sprite(self, sprite_guid: str) -> str:
        """Built-from-scratch prefab that carries a real sprite (not a clone)."""
        return textwrap.dedent(f"""\
            %YAML 1.1
            %TAG !u! tag:unity3d.com,2011:
            --- !u!1 &100
            GameObject:
              m_Name: CardRoot
            --- !u!114 &300
            MonoBehaviour:
              m_GameObject: {{fileID: 100}}
              m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
              m_Sprite: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}
        """)

    def _make_reuse_map(
        self,
        task_dir: Path,
        element_path: str,
        source_guid: str,
        built_prefab_path: str,
        key_sprite_guid: str = "",
    ) -> None:
        """Write a minimal reuse_map.json to task_dir."""
        rmap = {
            "elements": [
                {
                    "elementPath": element_path,
                    "sourcePrefab": f"Assets/Prefabs/Source.prefab guid:{source_guid}",
                    "keySpriteGuid": key_sprite_guid,
                    "builtPrefab": built_prefab_path,
                }
            ]
        }
        (task_dir / "reuse_map.json").write_text(
            json.dumps(rmap, indent=2), encoding="utf-8"
        )

    # ── A1: fabricated prefab (no PrefabInstance + null sprite) ───────────────

    def test_A1_fabrication_null_sprite_critical_fail(self):
        """A1 — A fabricated prefab (no PrefabInstance, null sprite where source has one)
        must produce a CRITICAL FAIL and log to review_misses.log."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            (repo_root / ".claude").mkdir()
            (repo_root / "Assets" / "Prefabs").mkdir(parents=True)

            # Built prefab: scratch + null sprite (fabrication).
            built_prefab = repo_root / "Assets" / "Prefabs" / "BuiltCard.prefab"
            built_prefab.write_text(self._prefab_scratch_null_sprite(), encoding="utf-8")

            # Source prefab: has real sprite.
            source_prefab = repo_root / "Assets" / "Prefabs" / "Source.prefab"
            source_prefab.write_text(
                self._prefab_with_prefab_instance(self._SOURCE_GUID, sprite_guid=self._SPRITE_GUID),
                encoding="utf-8",
            )
            # Write source .meta so _find_prefab_by_guid can locate it.
            (repo_root / "Assets" / "Prefabs" / "Source.prefab.meta").write_text(
                f"fileFormatVersion: 2\nguid: {self._SOURCE_GUID}\n", encoding="utf-8"
            )

            task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_task"
            task_dir.mkdir(parents=True)
            self._make_reuse_map(
                task_dir,
                element_path="CardRoot",
                source_guid=self._SOURCE_GUID,
                built_prefab_path=str(built_prefab),
                key_sprite_guid=self._SPRITE_GUID,
            )

            errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            critical = [e for e in errs if "CRITICAL FAIL" in e]
            self.assertTrue(
                len(critical) >= 1,
                f"Expected at least one CRITICAL FAIL for fabricated prefab; got: {errs}",
            )
            # A1b: verify logging to review_misses.log.
            log_path = repo_root / ".claude" / "review_misses.log"
            self.assertTrue(log_path.exists(), "review_misses.log must be created by _log_p1_miss")
            log_text = log_path.read_text(encoding="utf-8")
            self.assertIn("P1-CRITICAL-FAIL", log_text)
            self.assertIn("test_task", log_text)

    # ── A1-mutant: from-scratch prefab with SOURCE's sprite guid pasted → CRITICAL FAIL ──

    def test_A1_mutant_guid_paste_critical_fail(self):
        """A1-mutant (iter-3 fix) — a from-scratch fabrication that carries ZERO
        !u!1001 PrefabInstance blocks but has the source's m_Sprite guid pasted into
        the Image component MUST CRITICAL FAIL when the live-editor structural check
        detects a structure mismatch.

        Iter-3 mechanism: the no-lineage + same-sprite branch now calls
        _do_live_editor_structure_check. For a from-scratch fabrication the live
        editor returns "MISMATCH" → CRITICAL FAIL. We monkeypatch the seam so the
        test suite runs without a live editor.
        SPEC §1.1: 'Deliberately NOT checked: sprite equality as such.'
        """
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            (repo_root / ".claude").mkdir()
            (repo_root / "Assets" / "Prefabs").mkdir(parents=True)

            # Built prefab: FROM SCRATCH — zero PrefabInstance blocks,
            # but the m_Sprite guid is COPIED from the source (the bypass).
            built_prefab = repo_root / "Assets" / "Prefabs" / "BuiltCard.prefab"
            built_prefab.write_text(
                self._prefab_scratch_real_sprite(self._SPRITE_GUID),  # same guid as source
                encoding="utf-8",
            )

            # Source prefab: real clone with the same sprite.
            source_prefab = repo_root / "Assets" / "Prefabs" / "Source.prefab"
            source_prefab.write_text(
                self._prefab_scratch_real_sprite(self._SPRITE_GUID), encoding="utf-8"
            )
            (repo_root / "Assets" / "Prefabs" / "Source.prefab.meta").write_text(
                f"fileFormatVersion: 2\nguid: {self._SOURCE_GUID}\n", encoding="utf-8"
            )

            task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_task"
            task_dir.mkdir(parents=True)
            self._make_reuse_map(
                task_dir,
                element_path="CardRoot",
                source_guid=self._SOURCE_GUID,
                built_prefab_path=str(built_prefab),
                key_sprite_guid=self._SPRITE_GUID,
            )

            # Monkeypatch: live editor reports structure MISMATCH.
            original_fn = eid._do_live_editor_structure_check
            try:
                eid._do_live_editor_structure_check = lambda *a, **kw: "MISMATCH"
                errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            finally:
                eid._do_live_editor_structure_check = original_fn

            # FIDELITY REFRAME (Cesar 2026-07-06): provenance is unprovable for
            # CopyAsset, so a structural MISMATCH on an element that carries the
            # source's REAL sprite is NOT a hard fail — sprite-fidelity is met, and
            # the mismatch is a WARN for the reviewer + reference-diff (Rule 18). The
            # only hard fabrication signature is a NULL sprite (see the A1 null test).
            critical = [e for e in errs if "CRITICAL FAIL" in e]
            self.assertEqual(
                critical, [],
                f"Reframe: real-sprite element with structure MISMATCH must WARN, not "
                f"CRITICAL FAIL. Got: {critical}",
            )
            self.assertTrue(
                any("WARN (structure differs)" in e for e in errs),
                f"Structure MISMATCH on a real-sprite element must emit a WARN. Got: {errs}",
            )

    # ── A2: true PrefabInstance clone → PASS ──────────────────────────────────

    def test_A2_true_clone_prefab_instance_pass(self):
        """A2 — A built prefab with PrefabInstance.m_SourcePrefab matching the cited
        source GUID and matching key sprite must produce zero errors."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            (repo_root / ".claude").mkdir()
            (repo_root / "Assets" / "Prefabs").mkdir(parents=True)

            # Built prefab: has PrefabInstance from source + same sprite.
            built_yaml = self._prefab_with_prefab_instance(
                self._SOURCE_GUID, sprite_guid=self._SPRITE_GUID
            )
            built_prefab = repo_root / "Assets" / "Prefabs" / "BuiltCard.prefab"
            built_prefab.write_text(built_yaml, encoding="utf-8")

            # Source prefab: same sprite.
            source_prefab = repo_root / "Assets" / "Prefabs" / "Source.prefab"
            source_prefab.write_text(
                self._prefab_with_prefab_instance(self._SOURCE_GUID, sprite_guid=self._SPRITE_GUID),
                encoding="utf-8",
            )
            (repo_root / "Assets" / "Prefabs" / "Source.prefab.meta").write_text(
                f"fileFormatVersion: 2\nguid: {self._SOURCE_GUID}\n", encoding="utf-8"
            )

            task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_task"
            task_dir.mkdir(parents=True)
            self._make_reuse_map(
                task_dir,
                element_path="CardRoot",
                source_guid=self._SOURCE_GUID,
                built_prefab_path=str(built_prefab),
                key_sprite_guid=self._SPRITE_GUID,
            )

            errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            critical = [e for e in errs if "CRITICAL FAIL" in e]
            self.assertEqual(
                critical, [],
                f"True PrefabInstance clone should produce zero CRITICAL FAILs; got: {critical}",
            )

    # ── A2b: real CopyAsset clone (no PrefabInstance, same sprite, MATCH) → PASS ─

    def test_A2_copyasset_clone_matching_structure_pass(self):
        """A2b (iter-3, new) — A real CopyAsset clone produced by
        AssetDatabase.CopyAsset has NO !u!1001 PrefabInstance blocks but an
        identical component structure and the same sprite GUID.  The live-editor
        structural check returns "MATCH".  This MUST produce ZERO errors.

        This is the case iter-2 BROKE: iter-2's CRITICAL FAIL branch made
        PrefabInstance lineage mandatory, rejecting all CopyAsset clones.
        Iter-3 replaces it with the engine structural comparison; when the
        editor says MATCH the element is accepted as a legitimate clone.
        """
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            (repo_root / ".claude").mkdir()
            (repo_root / "Assets" / "Prefabs").mkdir(parents=True)

            # Built prefab: CopyAsset clone — same structure as source, same sprite,
            # but NO PrefabInstance blocks (that's what CopyAsset produces).
            built_prefab = repo_root / "Assets" / "Prefabs" / "BuiltCard.prefab"
            built_prefab.write_text(
                self._prefab_scratch_real_sprite(self._SPRITE_GUID),
                encoding="utf-8",
            )

            # Source prefab: the original.
            source_prefab = repo_root / "Assets" / "Prefabs" / "Source.prefab"
            source_prefab.write_text(
                self._prefab_scratch_real_sprite(self._SPRITE_GUID),
                encoding="utf-8",
            )
            (repo_root / "Assets" / "Prefabs" / "Source.prefab.meta").write_text(
                f"fileFormatVersion: 2\nguid: {self._SOURCE_GUID}\n", encoding="utf-8"
            )

            task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_task"
            task_dir.mkdir(parents=True)
            self._make_reuse_map(
                task_dir,
                element_path="CardRoot",
                source_guid=self._SOURCE_GUID,
                built_prefab_path=str(built_prefab),
                key_sprite_guid=self._SPRITE_GUID,
            )

            # Monkeypatch: live editor confirms identical structure (real clone).
            original_fn = eid._do_live_editor_structure_check
            try:
                eid._do_live_editor_structure_check = lambda *a, **kw: "MATCH"
                errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            finally:
                eid._do_live_editor_structure_check = original_fn

            critical = [e for e in errs if "CRITICAL FAIL" in e]
            block = [e for e in errs if "unreachable" in e.lower() or "BLOCK" in e or "fail-closed" in e.lower()]
            self.assertEqual(
                critical, [],
                f"Real CopyAsset clone (MATCH from live editor) must produce ZERO "
                f"CRITICAL FAILs; iter-2 regressed this case. Got: {critical}",
            )
            self.assertEqual(
                block, [],
                f"Real CopyAsset clone must not BLOCK; got: {block}",
            )

    # ── A1-leaf: bare-leaf guid-paste → INSUFFICIENT → BLOCK (iter-5 red-team) ─

    def test_A1_leaf_guid_paste_blocks(self):
        """iter-5 (red-team iter-4): a from-scratch prefab whose cited element is a
        BARE LEAF Image carrying the source's pasted sprite guid produces a
        trivially-replicable leaf skeleton — the live check returns INSUFFICIENT
        and the caller MUST CRITICAL FAIL. (Structural MATCH on a leaf proves
        nothing about lineage; this was the surviving iter-1-class bypass.)"""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            (repo_root / ".claude").mkdir()
            (repo_root / "Assets" / "Prefabs").mkdir(parents=True)
            built_prefab = repo_root / "Assets" / "Prefabs" / "BuiltCard.prefab"
            built_prefab.write_text(self._prefab_scratch_real_sprite(self._SPRITE_GUID), encoding="utf-8")
            source_prefab = repo_root / "Assets" / "Prefabs" / "Source.prefab"
            source_prefab.write_text(self._prefab_scratch_real_sprite(self._SPRITE_GUID), encoding="utf-8")
            (repo_root / "Assets" / "Prefabs" / "Source.prefab.meta").write_text(
                f"fileFormatVersion: 2\nguid: {self._SOURCE_GUID}\n", encoding="utf-8")
            task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_task"
            task_dir.mkdir(parents=True)
            self._make_reuse_map(task_dir, element_path="LeafBorder",
                                 source_guid=self._SOURCE_GUID,
                                 built_prefab_path=str(built_prefab),
                                 key_sprite_guid=self._SPRITE_GUID)
            original_fn = eid._do_live_editor_structure_check
            try:
                eid._do_live_editor_structure_check = lambda *a, **kw: "INSUFFICIENT"
                errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            finally:
                eid._do_live_editor_structure_check = original_fn
            # FIDELITY REFRAME: a bare leaf carrying the source's REAL sprite is
            # faithful — sprite-fidelity is met. Provenance of a leaf is unprovable,
            # so we ACCEPT (no hard fail). The prior "leaf guard block" was dropped
            # because it can't be made sound and it blocked legit leaf reuse.
            critical = [e for e in errs if "CRITICAL FAIL" in e]
            self.assertEqual(
                critical, [],
                f"Reframe: bare-leaf element with the source's real sprite is faithful "
                f"and must be ACCEPTED (not blocked). Got: {critical}",
            )

    # ── A2c: editor unreachable → BLOCK (fail-closed) ─────────────────────────

    def test_A2_editor_unreachable_accepts_real_sprite(self):
        """FIDELITY REFRAME (Cesar 2026-07-06) — When the element carries the source's
        REAL sprite (sprite-fidelity met) and the live editor is unreachable (the
        best-effort structure comparison returns None), the P1 gate ACCEPTS. The hard
        fabrication check (null sprite) is pure-Python and already ran; the structure
        comparison is only a WARN-level assist, so an unreachable editor does not
        block. (Provenance is unprovable for CopyAsset; fidelity is the gate.)
        """
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            (repo_root / ".claude").mkdir()
            (repo_root / "Assets" / "Prefabs").mkdir(parents=True)

            built_prefab = repo_root / "Assets" / "Prefabs" / "BuiltCard.prefab"
            built_prefab.write_text(
                self._prefab_scratch_real_sprite(self._SPRITE_GUID),
                encoding="utf-8",
            )

            source_prefab = repo_root / "Assets" / "Prefabs" / "Source.prefab"
            source_prefab.write_text(
                self._prefab_scratch_real_sprite(self._SPRITE_GUID),
                encoding="utf-8",
            )
            (repo_root / "Assets" / "Prefabs" / "Source.prefab.meta").write_text(
                f"fileFormatVersion: 2\nguid: {self._SOURCE_GUID}\n", encoding="utf-8"
            )

            task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_task"
            task_dir.mkdir(parents=True)
            self._make_reuse_map(
                task_dir,
                element_path="CardRoot",
                source_guid=self._SOURCE_GUID,
                built_prefab_path=str(built_prefab),
                key_sprite_guid=self._SPRITE_GUID,
            )

            # Monkeypatch: simulate unreachable editor (returns None).
            original_fn = eid._do_live_editor_structure_check
            try:
                eid._do_live_editor_structure_check = lambda *a, **kw: None
                errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            finally:
                eid._do_live_editor_structure_check = original_fn

            # Reframe: real-sprite element + unreachable editor → ACCEPT (no hard
            # fail). The null-sprite fabrication check already passed in pure Python;
            # the structure comparison is a best-effort WARN assist only.
            critical = [e for e in errs if "CRITICAL FAIL" in e]
            self.assertEqual(
                critical, [],
                f"Reframe: real-sprite element must be ACCEPTED when the editor is "
                f"unreachable (sprite-fidelity met; structure check is best-effort). "
                f"Got: {critical}",
            )

    # ── A3: legal re-skin (real clone, different real sprite) → WARN not FAIL ─

    def test_A3_legal_reskin_warn_not_block(self):
        """A3 — A prefab that has NO PrefabInstance lineage but carries a DIFFERENT
        real sprite (a CopyAsset re-skin) should produce a WARN, not a CRITICAL FAIL,
        and must NOT block the transition."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            (repo_root / ".claude").mkdir()
            (repo_root / "Assets" / "Prefabs").mkdir(parents=True)

            # Built prefab: no PrefabInstance, but real (different) sprite.
            built_prefab = repo_root / "Assets" / "Prefabs" / "BuiltCard.prefab"
            built_prefab.write_text(
                self._prefab_scratch_real_sprite(self._SPRITE_B_GUID), encoding="utf-8"
            )

            # Source prefab: has the original sprite.
            source_prefab = repo_root / "Assets" / "Prefabs" / "Source.prefab"
            source_prefab.write_text(
                self._prefab_scratch_real_sprite(self._SPRITE_GUID), encoding="utf-8"
            )
            (repo_root / "Assets" / "Prefabs" / "Source.prefab.meta").write_text(
                f"fileFormatVersion: 2\nguid: {self._SOURCE_GUID}\n", encoding="utf-8"
            )

            task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_task"
            task_dir.mkdir(parents=True)
            self._make_reuse_map(
                task_dir,
                element_path="CardRoot",
                source_guid=self._SOURCE_GUID,
                built_prefab_path=str(built_prefab),
                key_sprite_guid=self._SPRITE_GUID,
            )

            errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            critical = [e for e in errs if "CRITICAL FAIL" in e]
            warns = [e for e in errs if "WARN" in e or "legal re-skin" in e.lower()]
            # Legal re-skin must NOT produce a hard block.
            self.assertEqual(
                critical, [],
                f"Legal re-skin must not produce CRITICAL FAIL; got: {critical}",
            )
            # But it should produce at least a WARN so reviewers see it.
            self.assertTrue(
                len(warns) >= 1,
                f"Legal re-skin should produce a WARN for reviewer confirmation; got: {errs}",
            )

    # ── A4: P4 shipped-asset guard fires when touched without SPEC auth ────────

    def test_A4_shipped_asset_guard_fires_without_spec_auth(self):
        """A4 — validate_shipped_asset_guard must return an error when the working
        tree contains a shipped manifest asset that the SPEC does not name."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)

            # Create a minimal SHIPPED_MANIFEST.json.
            spec_dir = repo_root / "Docs" / "Specs"
            spec_dir.mkdir(parents=True)
            manifest = {"shipped_assets": ["Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab"]}
            (spec_dir / "SHIPPED_MANIFEST.json").write_text(
                json.dumps(manifest), encoding="utf-8"
            )

            # Simulate a git diff that touches the shipped asset (mock subprocess).
            import unittest.mock as mock

            task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_task2"
            task_dir.mkdir(parents=True)
            report_path = task_dir / "IMPLEMENTER_REPORT.md"
            report_path.write_text("# Report\n", encoding="utf-8")
            spec_path = task_dir / "SPEC.md"
            # SPEC does NOT mention the shipped asset.
            spec_path.write_text("# Spec\n\nBuild the general shop UI.\n", encoding="utf-8")

            # Patch subprocess.run to simulate git diff returning the shipped asset.
            def fake_run(cmd, **kwargs):
                result = mock.MagicMock()
                result.returncode = 0
                if "diff" in cmd and "--name-only" in cmd:
                    result.stdout = "Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab\n"
                else:
                    result.stdout = ""
                return result

            # Must patch the subprocess.run that enforce_implementer_done itself calls.
            with mock.patch.object(eid.subprocess, "run", side_effect=fake_run):
                errs = eid.validate_shipped_asset_guard(report_path, spec_path, repo_root)

            self.assertTrue(
                len(errs) >= 1,
                f"P4 should block when shipped asset touched without SPEC auth; got: {errs}",
            )
            self.assertIn("P4", errs[0])

    # ── A5: unit tests for new gate edge-cases ─────────────────────────────────

    def test_A5a_reuse_map_missing_noops(self):
        """A5a — validate_clone_provenance_yaml is a no-op when reuse_map.json absent."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            task_dir = repo_root / "Docs" / "Specs" / "Active" / "task_no_map"
            task_dir.mkdir(parents=True)
            # No reuse_map.json written.
            errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            self.assertEqual(errs, [], f"No reuse_map.json → must be no-op; got: {errs}")

    def test_A5b_reuse_map_missing_source_guid_critical(self):
        """A5b — A reuse_map.json element without a parseable sourcePrefab GUID
        produces a CRITICAL FAIL (prevents untethered elements)."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root = Path(root_str)
            task_dir = repo_root / "Docs" / "Specs" / "Active" / "task_no_guid"
            task_dir.mkdir(parents=True)
            rmap = {
                "elements": [
                    {
                        "elementPath": "SomeElement",
                        "sourcePrefab": "just a prose note, no GUID",
                        "keySpriteGuid": "",
                        "builtPrefab": "Assets/Prefabs/SomePrefab.prefab",
                    }
                ]
            }
            (task_dir / "reuse_map.json").write_text(json.dumps(rmap), encoding="utf-8")
            errs = eid.validate_clone_provenance_yaml(task_dir, repo_root)
            # Should produce a warning (not a hard CRITICAL FAIL — unparseable GUID
            # is a config error, not a fabrication detection).
            self.assertTrue(
                len(errs) >= 1,
                f"Unparseable sourcePrefab GUID should produce an error; got: {errs}",
            )

    def test_A5c_tolerance_deltas_out_of_range_blocks(self):
        """A5c — validate_measure_before_surface returns an error when a measured
        delta exceeds the tolerance bound."""
        with tempfile.TemporaryDirectory() as root_str:
            task_dir = Path(root_str)
            ref_dir = task_dir / "reference"
            ref_dir.mkdir()

            # Write tolerances.json.
            tol = {
                "surfaces": {
                    "CardRow": {
                        "elements": {
                            "TitleText": {"fontSize": 2.0, "width": 4.0}
                        }
                    }
                }
            }
            (task_dir / "tolerances.json").write_text(json.dumps(tol), encoding="utf-8")

            # Write the overlay PNG stub (just needs to exist).
            (ref_dir / "CardRow_ref_vs_built.png").write_bytes(b"\x89PNG\r\n\x1a\n")

            # Write deltas.json with an out-of-tolerance value.
            deltas = {
                "measured": {
                    "TitleText": {"fontSize": 5.0, "width": 2.0}  # fontSize 5.0 > tolerance 2.0
                }
            }
            (ref_dir / "CardRow_deltas.json").write_text(json.dumps(deltas), encoding="utf-8")

            report_path = task_dir / "IMPLEMENTER_REPORT.md"
            report_path.write_text("# Report\n", encoding="utf-8")

            errs = eid.validate_measure_before_surface(report_path, task_dir)
            self.assertTrue(
                any("TitleText" in e and "fontSize" in e for e in errs),
                f"Out-of-tolerance delta should block; got: {errs}",
            )

    def test_A5d_p5_save_schema_prose_claim_blocked(self):
        """A5d — P5: a task touching SaveData/save-schema code with only a prose
        'tests pass' claim in the report (no machine Total: N) must be blocked."""
        with tempfile.TemporaryDirectory() as root_str:
            task_dir = Path(root_str)

            spec_path = task_dir / "SPEC.md"
            spec_path.write_text(
                "## Files\nEditSaveData.cs, SaveSchemaMigrator.cs — schema migration for new field.\n",
                encoding="utf-8",
            )
            report_path = task_dir / "IMPLEMENTER_REPORT.md"
            report_path.write_text(
                "## Acceptance\n| 1. Tests pass | PASS | All tests pass (manually verified). |\n",
                encoding="utf-8",
            )
            repo_root = task_dir  # git call will gracefully fail; that's OK for this test.

            errs = eid.validate_observed_test_run(report_path, spec_path, repo_root)
            self.assertTrue(
                len(errs) >= 1,
                f"P5 should block prose-only test claim for save-schema task; got: {errs}",
            )
            self.assertIn("P5", errs[0])

    def test_A5e_p5_machine_total_line_passes(self):
        """A5e — P5 gate passes when the report contains a 'Total: N' machine line."""
        with tempfile.TemporaryDirectory() as root_str:
            task_dir = Path(root_str)

            spec_path = task_dir / "SPEC.md"
            spec_path.write_text("## Files\nSaveData.cs — add field.\n", encoding="utf-8")
            report_path = task_dir / "IMPLEMENTER_REPORT.md"
            report_path.write_text(
                "## Test results\nTotal: 42  Passed: 42  Failed: 0  Skipped: 0\n",
                encoding="utf-8",
            )
            repo_root = task_dir  # git won't run; that's OK — spec text triggers

            errs = eid.validate_observed_test_run(report_path, spec_path, repo_root)
            self.assertEqual(
                errs, [],
                f"Machine Total: N line should satisfy P5; got: {errs}",
            )

    def test_A5f_parse_prefab_source_guids_extracts_correctly(self):
        """A5f — _parse_prefab_source_guids pulls the source GUID out of a
        PrefabInstance block and ignores zero-GUIDs."""
        yaml_text = textwrap.dedent(f"""\
            --- !u!1001 &200
            PrefabInstance:
              m_SourcePrefab: {{fileID: 100100000, guid: {self._SOURCE_GUID}, type: 3}}
            --- !u!1001 &300
            PrefabInstance:
              m_SourcePrefab: {{fileID: 100100000, guid: 00000000000000000000000000000000, type: 3}}
        """)
        guids = eid._parse_prefab_source_guids(yaml_text)
        self.assertIn(self._SOURCE_GUID, guids)
        self.assertNotIn("00000000000000000000000000000000", guids)

    _IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"

    def test_A5g_parse_prefab_gameobject_sprites_null_vs_real(self):
        """A5g — _parse_prefab_gameobject_sprites returns '' (empty str) for a
        null/zero sprite and a real guid string for a real sprite. Sprites are
        read only from genuine Image components (Image m_Script guid)."""
        yaml_text = textwrap.dedent(f"""\
            --- !u!1 &100
            GameObject:
              m_Name: NullSpriteGO
            --- !u!114 &200
            MonoBehaviour:
              m_GameObject: {{fileID: 100}}
              m_Script: {{fileID: 11500000, guid: {self._IMAGE_GUID}, type: 3}}
              m_Sprite: {{fileID: 0, guid: , type: 0}}
            --- !u!1 &300
            GameObject:
              m_Name: RealSpriteGO
            --- !u!114 &400
            MonoBehaviour:
              m_GameObject: {{fileID: 300}}
              m_Script: {{fileID: 11500000, guid: {self._IMAGE_GUID}, type: 3}}
              m_Sprite: {{fileID: 21300000, guid: {self._SPRITE_GUID}, type: 3}}
        """)
        sprites = eid._parse_prefab_gameobject_sprites(yaml_text)
        self.assertEqual(sprites.get("NullSpriteGO"), "")
        self.assertEqual(sprites.get("RealSpriteGO"), self._SPRITE_GUID)

    def test_A5h_decoy_component_does_not_mask_null_image(self):
        """A5h (red-team iter-6) — a null-sprite Image (white box) whose GameObject
        ALSO carries a NON-Image MonoBehaviour with a stray real `m_Sprite` must
        still read as NULL. The decoy's m_Sprite (last-write-wins across all !u!114)
        was the white-box bypass; sprites are now attributed only to the genuine
        Image component (by m_Script guid)."""
        decoy_guid = "abcdef0123456789abcdef0123456789"  # NOT the Image guid
        yaml_text = textwrap.dedent(f"""\
            --- !u!1 &100
            GameObject:
              m_Name: WhiteBox
            --- !u!114 &200
            MonoBehaviour:
              m_GameObject: {{fileID: 100}}
              m_Script: {{fileID: 11500000, guid: {self._IMAGE_GUID}, type: 3}}
              m_Sprite: {{fileID: 0, guid: , type: 0}}
            --- !u!114 &201
            MonoBehaviour:
              m_GameObject: {{fileID: 100}}
              m_Script: {{fileID: 11500000, guid: {decoy_guid}, type: 3}}
              m_Sprite: {{fileID: 21300000, guid: {self._SPRITE_GUID}, type: 3}}
        """)
        sprites = eid._parse_prefab_gameobject_sprites(yaml_text)
        self.assertEqual(
            sprites.get("WhiteBox"), "",
            "A decoy component's m_Sprite must NOT mask a null-sprite Image "
            "(the white-box fabrication signature). Got: "
            f"{sprites.get('WhiteBox')!r}",
        )

    def test_A5i_real_prefab_images_resolve_sprites(self):
        """A5i (red-team iter-7) — REGRESSION against the REAL project prefab, not a
        synthetic fixture. A phantom Image m_Script guid made the parser return {}
        for the shipped card while the synthetic-fixture suite stayed green (a
        self-consistent bubble). Assert the real GeneralShopCard's Image elements
        resolve to NON-EMPTY sprite guids — this test would have caught the bug."""
        repo = Path(eid.__file__).resolve().parents[2]
        card = repo / "Assets" / "Prefabs" / "UI" / "Shop" / "GeneralShopCard.prefab"
        if not card.exists():
            self.skipTest("GeneralShopCard.prefab not present")
        sprites = eid._parse_prefab_gameobject_sprites(
            card.read_text(encoding="utf-8", errors="ignore")
        )
        for elem in ("CardBorder", "BadgePill"):
            self.assertTrue(
                sprites.get(elem),  # non-None and non-empty
                f"Real prefab element '{elem}' must resolve to a real sprite guid via "
                f"the parser (phantom-Image-guid regression). Got: {sprites.get(elem)!r}",
            )


class TestValidateUILintLiveRerun(unittest.TestCase):
    """Tests for the iter-3 P2 live-editor re-run in validate_ui_lint.

    The rule: after the cached lint JSON shows fail == 0, the hook tries to
    re-run UIFidelityLinter via the live Unity editor.
      - If fresh run returns fail > 0 → the cached JSON is stale → FAIL.
      - If editor is unreachable (returns None from _rerun_ui_lint_via_editor)
        → accept the cached JSON (P2 is a quality gate, not a security gate).
      - If fresh run returns fail == 0 → PASS (consistent).
    """

    SPEC_MD_WITH_FIGMA = (
        "## Reference\n"
        "Figma node: https://figma.com/design/ABC123/file?node-id=1:2\n"
    )
    LINT_JSON_PASS = json.dumps({"fail": 0, "pass": 5, "warn": 0})
    LINT_JSON_FAIL = json.dumps({"fail": 2, "pass": 3, "warn": 1})

    def _make_task(self, tmpdir: str) -> tuple:
        repo_root = Path(tmpdir)
        task_dir = repo_root / "Docs" / "Specs" / "Active" / "test_ui_task"
        task_dir.mkdir(parents=True)
        (task_dir / "SPEC.md").write_text(self.SPEC_MD_WITH_FIGMA, encoding="utf-8")
        cap_dir = repo_root / "Docs" / "Diagnostics" / "_capture"
        cap_dir.mkdir(parents=True)
        return repo_root, task_dir, cap_dir

    def _make_report_with_lint_section(self, task_dir: Path, lint_json_name: str) -> Path:
        report = task_dir / "IMPLEMENTER_REPORT.md"
        report.write_text(
            "## UI fidelity lint\n"
            f"`Docs/Diagnostics/_capture/{lint_json_name}` — 0 FAIL\n",
            encoding="utf-8",
        )
        return report

    def test_p2_cached_pass_editor_unreachable_blocks(self):
        """P2 fail-CLOSED: cached JSON shows fail=0 but the fresh live re-run
        cannot run (editor unreachable) → BLOCK. The cited JSON is NOT accepted
        as evidence (SPEC §1.3 / §0). iter-3 shipped this fail-OPEN (accept
        cached); both parallel reviewers flagged it as a §0 violation."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root, task_dir, cap_dir = self._make_task(root_str)
            lint_path = cap_dir / "MyWidget_lint.json"
            lint_path.write_text(self.LINT_JSON_PASS, encoding="utf-8")
            report = self._make_report_with_lint_section(task_dir, "MyWidget_lint.json")

            original_fn = eid._rerun_ui_lint_via_editor
            try:
                eid._rerun_ui_lint_via_editor = lambda *a, **kw: None  # editor unreachable
                errs = eid.validate_ui_lint(report, repo_root)
            finally:
                eid._rerun_ui_lint_via_editor = original_fn

            self.assertTrue(
                any("P2 fail-closed" in e for e in errs),
                f"Cached pass + editor unreachable must BLOCK (fail-closed), not "
                f"trust the cited JSON. Got: {errs}",
            )

    def test_p2_cached_pass_live_rerun_also_passes(self):
        """P2: cached JSON shows fail=0; live re-run also returns 0 → PASS."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root, task_dir, cap_dir = self._make_task(root_str)
            lint_path = cap_dir / "MyWidget_lint.json"
            lint_path.write_text(self.LINT_JSON_PASS, encoding="utf-8")
            report = self._make_report_with_lint_section(task_dir, "MyWidget_lint.json")

            original_fn = eid._rerun_ui_lint_via_editor
            try:
                eid._rerun_ui_lint_via_editor = lambda *a, **kw: 0  # fresh run also 0
                errs = eid.validate_ui_lint(report, repo_root)
            finally:
                eid._rerun_ui_lint_via_editor = original_fn

            self.assertEqual(
                errs, [],
                f"Cached pass + live re-run pass should produce no errors. Got: {errs}",
            )

    def test_p2_cached_pass_live_rerun_detects_failures(self):
        """P2: cached JSON shows fail=0 but live re-run returns fail=3 → FAIL.

        This is the stale-artifact scenario: the implementer ran the linter,
        got 0 FAILs, then modified the prefab and re-cited the old JSON without
        re-running. The live re-run catches it.
        """
        with tempfile.TemporaryDirectory() as root_str:
            repo_root, task_dir, cap_dir = self._make_task(root_str)
            lint_path = cap_dir / "MyWidget_lint.json"
            lint_path.write_text(self.LINT_JSON_PASS, encoding="utf-8")  # old JSON says 0
            report = self._make_report_with_lint_section(task_dir, "MyWidget_lint.json")

            original_fn = eid._rerun_ui_lint_via_editor
            try:
                eid._rerun_ui_lint_via_editor = lambda *a, **kw: 3  # fresh run finds 3 fails
                errs = eid.validate_ui_lint(report, repo_root)
            finally:
                eid._rerun_ui_lint_via_editor = original_fn

            fail_errs = [e for e in errs if "stale" in e.lower() or "live re-run" in e.lower()]
            self.assertTrue(
                len(fail_errs) >= 1,
                f"Cached-pass + live-rerun-fail=3 must produce a stale-artifact error. Got: {errs}",
            )

    def test_p2_cached_fail_still_blocks_without_rerun(self):
        """P2: cached JSON already shows fail>0 — the existing block fires before the
        re-run is attempted (no double-error)."""
        with tempfile.TemporaryDirectory() as root_str:
            repo_root, task_dir, cap_dir = self._make_task(root_str)
            lint_path = cap_dir / "MyWidget_lint.json"
            lint_path.write_text(self.LINT_JSON_FAIL, encoding="utf-8")  # fail=2
            report = self._make_report_with_lint_section(task_dir, "MyWidget_lint.json")

            rerun_called = []
            original_fn = eid._rerun_ui_lint_via_editor
            try:
                def _mock_rerun(*a, **kw):
                    rerun_called.append(True)
                    return 2
                eid._rerun_ui_lint_via_editor = _mock_rerun
                errs = eid.validate_ui_lint(report, repo_root)
            finally:
                eid._rerun_ui_lint_via_editor = original_fn

            fail_errs = [e for e in errs if "fail=2" in e or "fail == 0" in e.lower()]
            self.assertTrue(
                len(fail_errs) >= 1,
                f"Cached fail=2 should produce an error. Got: {errs}",
            )
            # Re-run should NOT have been called (cached fail short-circuits to continue).
            self.assertEqual(
                rerun_called, [],
                "Re-run should not be attempted when the cached JSON already shows fail > 0.",
            )


class TestLiveEditorIntegration(unittest.TestCase):
    """NON-MOCKED integration: drives the REAL Unity editor via the MCP HTTP seam
    (localhost:21573) against real repo prefabs. SKIPS when the editor is
    unreachable (CI) so it never blocks the suite; when the editor IS up it
    exercises the actual class-name / return-value / SSE-parse path that the
    monkeypatched unit tests bypass — the exact gap that let iter-3 ship a dead
    live-editor check behind 113 green tests. (red-team iter-3 requirement.)"""

    REPO_ROOT = Path(eid.__file__).resolve().parents[2]
    SHOP_CARD = REPO_ROOT / "Assets" / "Prefabs" / "UI" / "Shop" / "GeneralShopCard.prefab"
    TOURNAMENT_CARD_GUID = "baac145d1783f41758376281a61c83e0"   # real clone SOURCE of GeneralShopCard
    STAMINA_CARD_GUID = "717d118c7be214838ab65e0bd65731f2"      # structurally DIFFERENT prefab

    def _editor_reachable(self) -> bool:
        ping = eid._call_live_editor(
            'public static class Script { public static string Main() { return "PING"; } }'
        )
        return ping is not None and "PING" in ping

    def setUp(self):
        if not self.SHOP_CARD.exists():
            self.skipTest("GeneralShopCard.prefab not present")
        if not self._editor_reachable():
            self.skipTest("Unity editor not reachable at localhost:21573 (CI / editor down)")

    def test_real_clone_matches(self):
        """A real (modified) CopyAsset clone MATCHes its source modulo root name."""
        verdict = eid._do_live_editor_structure_check(
            str(self.SHOP_CARD), self.TOURNAMENT_CARD_GUID, "", self.REPO_ROOT
        )
        self.assertEqual(verdict, "MATCH", f"real clone should MATCH its source; got {verdict!r}")

    def test_unrelated_prefab_mismatches(self):
        """A structurally different prefab MISMATCHes — proves the check
        discriminates rather than always returning MATCH (or always None)."""
        verdict = eid._do_live_editor_structure_check(
            str(self.SHOP_CARD), self.STAMINA_CARD_GUID, "", self.REPO_ROOT
        )
        self.assertEqual(verdict, "MISMATCH", f"unrelated prefab should MISMATCH; got {verdict!r}")

    def test_bare_leaf_insufficient(self):
        """A bare-leaf element (CardBorder: Image, no children) returns
        INSUFFICIENT — its skeleton is trivially replicable so lineage cannot be
        proven (the leaf guid-paste bypass the red-team found in iter-4). Proven
        against the real editor, not mocked."""
        verdict = eid._do_live_editor_structure_check(
            str(self.SHOP_CARD), self.TOURNAMENT_CARD_GUID, "CardBorder", self.REPO_ROOT
        )
        self.assertEqual(verdict, "INSUFFICIENT", f"bare leaf should be INSUFFICIENT; got {verdict!r}")


if __name__ == "__main__":
    unittest.main(verbosity=2)
