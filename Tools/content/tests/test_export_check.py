#!/usr/bin/env python3
"""`export_content.py --check` — the VALUE-level direction half (content_two_way §3).

    python3 -m unittest discover Tools/content/tests

The id-level half (`drift_report`) already had a home; what did not was the
question an operator actually asks at the release gate: *the check failed — which
loop do I run?*

`--check` fails identically whether

  * somebody edited a value in Unity and never imported it   → import, then publish
  * somebody published in the admin and never exported       → run the exporter

and running the WRONG one is not neutral: an export over an un-imported CSV edit
overwrites that edit with the still-published value, silently. So the draft is
consulted, because it is the one fact that distinguishes them — a draft equal to
the CSV means the import has already run and only the publish is outstanding.

The exit code is deliberately NOT part of what this adds, and one test below
pins that: a value difference was already a file difference, and `--check` was
already failing on it.
"""

from __future__ import annotations

import io
import os
import shutil
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from unittest import mock

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, TOOLS)
sys.path.insert(0, HERE)

import export_content  # noqa: E402
from catalogs import CATALOGS_BY_NAME  # noqa: E402
from fakes import FakePostgrestClient, published_row  # noqa: E402

BALLS = CATALOGS_BY_NAME["balls"]

HEADER = "id,name,brand,power,rebound,windResistance,roll,spin,thumbnailSprite,fullSprite,info"
ROW_GOLFIN = 'ball_golfin,Golfin,Golfin,0,0,0,0,0,Golfin,Golfin,"The standard ball."'


def data_golfin(**overrides) -> dict:
    row = {
        "id": "ball_golfin", "name": "Golfin", "brand": "Golfin",
        "power": "0", "rebound": "0", "windResistance": "0", "roll": "0", "spin": "0",
        "thumbnailSprite": "Golfin", "fullSprite": "Golfin",
        "info": "The standard ball.",
    }
    row.update(overrides)
    return row


class ValueDirection(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="content_two_way_check_")
        self.addCleanup(shutil.rmtree, self.root, True)
        path = os.path.join(self.root, BALLS.csv_path)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write("\n".join([HEADER, ROW_GOLFIN]) + "\n")

    def report(self, published, drafts=None):
        return export_content.value_direction_report(
            BALLS, published, self.root,
            drafts=(lambda: drafts) if drafts is not None else None,
        )

    def test_no_line_when_the_values_agree(self):
        self.assertEqual([], self.report([published_row("balls", "ball_golfin", data_golfin())]))

    def test_a_draft_equal_to_the_csv_reads_as_imported_not_yet_published(self):
        lines = self.report(
            [published_row("balls", "ball_golfin", data_golfin(power="3"))],
            {"ball_golfin": published_row("balls", "ball_golfin", data_golfin())},
        )

        self.assertEqual(1, len(lines))
        self.assertIn("imported, not yet published", lines[0])
        self.assertIn("publish `balls` in the admin", lines[0])
        self.assertIn("ball_golfin", lines[0], "the row has to be named")
        self.assertNotIn("run the exporter", lines[0],
                         "telling them to export here would DESTROY the imported edit")

    def test_no_matching_draft_names_both_loops(self):
        lines = self.report(
            [published_row("balls", "ball_golfin", data_golfin(power="3"))],
            {},
        )

        self.assertEqual(1, len(lines))
        self.assertIn("values differ from published", lines[0])
        self.assertIn("import_content.py --apply", lines[0])
        self.assertIn("run the exporter", lines[0])
        self.assertIn("ball_golfin", lines[0])

    def test_a_draft_that_matches_neither_side_is_still_undecided(self):
        # Someone is mid-edit in the admin AND the CSV moved. Nothing here can
        # tell those apart from the outside, so it must not pretend to.
        lines = self.report(
            [published_row("balls", "ball_golfin", data_golfin(power="3"))],
            {"ball_golfin": published_row("balls", "ball_golfin", data_golfin(power="9"))},
        )
        self.assertIn("values differ from published", lines[0])

    def test_an_id_only_one_side_has_is_left_to_the_id_level_report(self):
        # `drift_report` owns that direction; duplicating it here would print the
        # same row twice under two different headings.
        lines = self.report([published_row("balls", "ball_other", data_golfin(id="ball_other"))])
        self.assertEqual([], lines)

    def test_the_drafts_query_is_not_made_when_nothing_differs(self):
        calls = []

        def drafts():
            calls.append(1)
            return {}

        export_content.value_direction_report(
            BALLS, [published_row("balls", "ball_golfin", data_golfin())], self.root, drafts=drafts)
        self.assertEqual([], calls, "a clean catalog must cost no extra round trip")


class CheckExitCode(unittest.TestCase):
    """The new report must not change what `--check` DOES — the fastlane gate
    reads its exit code, and a §3 that moved it would be a §3 that broke the
    release lane."""

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="content_two_way_exit_")
        self.addCleanup(shutil.rmtree, self.root, True)
        self.path = os.path.join(self.root, BALLS.csv_path)
        os.makedirs(os.path.dirname(self.path), exist_ok=True)
        self.write([HEADER, ROW_GOLFIN])

    def write(self, lines):
        with open(self.path, "w", encoding="utf-8", newline="") as fh:
            fh.write("\n".join(lines) + "\n")

    def run_check(self, client):
        argv = ["export_content.py", "--repo-root", self.root, "--catalogs", "balls", "--check"]
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(sys, "argv", argv), \
             mock.patch.object(export_content.PostgrestClient, "from_env",
                               classmethod(lambda cls, env=None: client)), \
             redirect_stdout(out), redirect_stderr(err):
            code = export_content.main()
        return code, out.getvalue() + err.getvalue()

    def test_clean_is_still_zero_and_prints_no_direction_block(self):
        client = FakePostgrestClient({
            "content_catalogs": [{"name": "balls", "published_version": 3}],
            "content_rows": [published_row("balls", "ball_golfin", data_golfin())],
        })
        # content_version.txt counts as a file the export would write, so a repo
        # that has never been exported is "stale" by definition. Seed it, or this
        # test measures the version file rather than the catalog.
        vpath = os.path.join(self.root, export_content.VERSION_FILE)
        os.makedirs(os.path.dirname(vpath), exist_ok=True)
        with open(vpath, "w", encoding="utf-8") as fh:
            fh.write("balls=3\n")

        code, text = self.run_check(client)

        self.assertEqual(0, code)
        self.assertNotIn("VALUE differences", text)

    def test_a_value_difference_is_still_exit_1_and_now_says_which_loop(self):
        client = FakePostgrestClient({
            "content_catalogs": [{"name": "balls", "published_version": 3}],
            "content_rows": [published_row("balls", "ball_golfin", data_golfin(power="3"))],
            "content_drafts": [published_row("balls", "ball_golfin", data_golfin())],
        })
        code, text = self.run_check(client)

        self.assertEqual(1, code, "exit code unchanged — a value difference was always a failure")
        self.assertIn("VALUE differences", text)
        self.assertIn("imported, not yet published", text)

    def test_is_active_survives_a_SECOND_export(self):
        """The exporter must be IDEMPOTENT for a catalog carrying a deactivated row.

        `is_active` is a table COLUMN, never a field of `data`. The first export
        appends the column and writes `false`; the second used to fall through to
        `data.get("is_active")` — because the column was now in the file's header —
        which is None, so every cell came back BLANK. A blank reads as ACTIVE
        downstream, silently re-admitting a row the operator had turned off.
        Found on gacha_pools by `--check` (gacha_ops_polish, 2026-08-31).
        """
        client = FakePostgrestClient({
            "content_catalogs": [{"name": "balls", "published_version": 3}],
            "content_rows": [published_row("balls", "ball_golfin", data_golfin(), is_active=False)],
        })
        vpath = os.path.join(self.root, export_content.VERSION_FILE)
        os.makedirs(os.path.dirname(vpath), exist_ok=True)
        with open(vpath, "w", encoding="utf-8") as fh:
            fh.write("balls=3\n")

        argv = ["export_content.py", "--repo-root", self.root, "--catalogs", "balls"]

        def run_export():
            out, err = io.StringIO(), io.StringIO()
            with mock.patch.object(sys, "argv", argv), \
                 mock.patch.object(export_content.PostgrestClient, "from_env",
                                   classmethod(lambda cls, env=None: client)), \
                 redirect_stdout(out), redirect_stderr(err):
                export_content.main()

        run_export()
        first = open(self.path, encoding="utf-8").read()
        self.assertIn("is_active", first.splitlines()[0])
        self.assertTrue(first.splitlines()[1].endswith(",false"), first)

        run_export()
        second = open(self.path, encoding="utf-8").read()
        self.assertEqual(first, second,
                         "the second export must be byte-identical — it blanked is_active")

        # And --check must now agree that nothing is stale.
        code, _ = self.run_check(client)
        self.assertEqual(0, code, "a re-exported file must pass --check")

    def test_the_version_file_is_left_alone_by_check(self):
        # (and, by construction, that a repo which has never been exported at all
        # still fails --check — the version file is part of what must be current.)
        client = FakePostgrestClient({
            "content_catalogs": [{"name": "balls", "published_version": 3}],
            "content_rows": [published_row("balls", "ball_golfin", data_golfin(power="3"))],
        })
        self.run_check(client)
        self.assertFalse(os.path.exists(os.path.join(self.root, export_content.VERSION_FILE)),
                         "--check writes nothing, including the version file")


if __name__ == "__main__":
    unittest.main()
