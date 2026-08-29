#!/usr/bin/env python3
"""`seed_from_csv.py --catalogs <subset>` must not land on the day-one migration.

    python3 -m unittest discover Tools/content/tests

WHY THIS IS A TEST AND NOT A COMMENT. It already happened: on 2026-08-28 a run of
`seed_from_csv.py --catalogs texts` with no `--out` replaced the day-one seed —
seven catalogs, 799 clubs among them — with a texts-only file, and the run
printed its usual success summary while doing it. That is the shape worth
pinning: the failure was not an error anybody had to ignore, it was a
`wrote <path>` line that looked exactly like the good case.

The module docstring already states the rule ("the day-one migration is the
applied record of that day and is not re-generated"), and `generate()` already
draws the line for the filename it stamps into the header. What was missing was
the same line drawn for the path actually opened for writing.

`--stdout` is the deliberate hole and one test below pins it: it writes no file,
so there is nothing for a scoped run to clobber, and previewing a subset seed on
the terminal has to keep working.
"""

from __future__ import annotations

import io
import os
import sys
import unittest
from contextlib import redirect_stdout
from unittest import mock

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, TOOLS)
sys.path.insert(0, HERE)

import seed_from_csv  # noqa: E402
from catalogs import CATALOGS  # noqa: E402


def run_main(*argv):
    """Drive `main()` with argv, returning (exit_code_or_None, SystemExit or None)."""
    args = ["seed_from_csv.py", *argv]
    with mock.patch.object(sys, "argv", args):
        try:
            return seed_from_csv.main(), None
        except SystemExit as exc:
            return None, exc


class ScopedSeedRefusesTheDayOneMigration(unittest.TestCase):
    def test_a_subset_with_no_out_is_refused(self):
        code, exc = run_main("--catalogs", "texts")
        self.assertIsNotNone(exc, "a scoped seed with no --out must not be written")
        message = str(exc)
        self.assertIn(os.path.basename(seed_from_csv.DEFAULT_OUT), message)
        self.assertIn("--out", message, "the refusal has to say what to pass instead")
        self.assertIn("texts", message, "the refusal names the subset it refused")

    def test_the_refusal_writes_nothing(self):
        """The point of the guard: DEFAULT_OUT is untouched, not merely warned about."""
        with mock.patch("builtins.open", side_effect=AssertionError("opened a file")):
            _, exc = run_main("--catalogs", "texts")
        self.assertIsNotNone(exc)

    def test_an_explicit_out_equal_to_the_default_is_refused_too(self):
        """Spelling the day-one path out by hand is the same clobber, not consent."""
        _, exc = run_main("--catalogs", "texts", "--out", seed_from_csv.DEFAULT_OUT)
        self.assertIsNotNone(exc)

    def test_a_relative_out_that_resolves_to_the_default_is_refused(self):
        """The comparison is on the resolved path — `--out ./x/../x` does not slip past."""
        head, tail = os.path.split(seed_from_csv.DEFAULT_OUT)
        sneaky = os.path.join(head, ".", tail)
        _, exc = run_main("--catalogs", "texts", "--out", sneaky)
        self.assertIsNotNone(exc)

    def test_a_subset_to_its_own_file_is_allowed(self):
        """The refusal must not cost the scoped seed its actual purpose."""
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            out = os.path.join(tmp, "migrations", "content_seed_texts.sql")
            with redirect_stdout(io.StringIO()):
                code, exc = run_main("--catalogs", "texts", "--out", out)
            self.assertIsNone(exc, f"unexpected refusal: {exc}")
            self.assertEqual(code, 0)
            self.assertTrue(os.path.exists(out))
            sql = open(out, encoding="utf-8").read()
            self.assertIn("content_seed_texts.sql", sql)
            self.assertIn("-- texts:", sql)

    def test_stdout_is_not_guarded(self):
        """`--stdout` opens no file, so a subset preview keeps working."""
        buf = io.StringIO()
        with redirect_stdout(buf):
            code, exc = run_main("--catalogs", "texts", "--stdout")
        self.assertIsNone(exc, f"unexpected refusal: {exc}")
        self.assertEqual(code, 0)
        self.assertIn("-- texts:", buf.getvalue())

    def test_stdout_wins_even_when_out_names_the_day_one_file(self):
        """`--out` is inert under `--stdout`; refusing there would be refusing nothing."""
        buf = io.StringIO()
        with redirect_stdout(buf):
            code, exc = run_main("--catalogs", "texts", "--stdout",
                                 "--out", seed_from_csv.DEFAULT_OUT)
        self.assertIsNone(exc, f"unexpected refusal: {exc}")
        self.assertEqual(code, 0)

    def test_an_unknown_catalog_still_fails_on_the_name(self):
        """The new guard sits after the name check, so the clearer error stays first."""
        _, exc = run_main("--catalogs", "not_a_catalog")
        self.assertIsNotNone(exc)
        self.assertIn("unknown catalog", str(exc))

    def test_naming_every_catalog_is_still_a_scoped_invocation(self):
        """`--catalogs a,b,...` covering all seven is refused too — the day-one file is
        regenerated by the bare run and nothing else, and a caller who typed the whole
        list is a caller who meant to scope."""
        every = ",".join(c.name for c in CATALOGS)
        _, exc = run_main("--catalogs", every)
        self.assertIsNotNone(exc)


if __name__ == "__main__":
    unittest.main()
