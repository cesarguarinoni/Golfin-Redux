#!/usr/bin/env python3
"""Unit tests for enforce_implementer_done.py.

Run with:  python -m unittest .claude/hooks/test_enforce_implementer_done.py
       or: python .claude/hooks/test_enforce_implementer_done.py

Coverage focus is Rules 10–12 (the green_authoring_editor_tool scar-tissue
rules added 2026-05-26). Pre-existing rules 1–9 have implicit coverage via
the dry-run-against-iter-4 integration check at the bottom.
"""
from __future__ import annotations

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


if __name__ == "__main__":
    unittest.main(verbosity=2)
