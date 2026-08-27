#!/usr/bin/env python3
"""`import_content.py` — the repo CSV → `content_drafts` proposal (content_two_way §2).

    python3 -m unittest discover Tools/content/tests

WHAT THESE PIN, AND WHY EACH ONE IS HERE

The importer was written under time pressure during `shop_stocking`, when the
release lane found five `SETTINGS_*` keys sitting in LocalizationText.csv with no
`texts` row. It shipped without tests. Every case below is a decision that was
made deliberately and that nothing else in the repo would notice being reversed:

  ADD / CHANGE / same        the three verdicts, and that `same` writes nothing
  min_build on an ADD        set from the run's default (high on purpose)
  min_build on a CHANGE      CARRIED from published — immutable once published (§D1.7)
  absent from the CSV        reported, never deactivated and never deleted (I6)
  a dirty draft              REFUSES THE WHOLE RUN, exit 1, and writes nothing
  --overwrite-dirty          the CSV wins, and every clobbered row is named
  the is_active column       splits out of `data` and round-trips as a column
  never content_rows         publish stays the only way in

and the ROUND-TRIP PROPERTY, which is the one that makes the loop trustworthy:
import → publish → export leaves the CSV byte-identical. Both halves read
`catalogs.py`, so a mapping that is lossy is lossy in both directions or in
neither — the property is what proves it is neither.

Everything runs against `FakePostgrestClient` (see `fakes.py`) over a temp repo
root, so no test can touch prod and the awkward states — a half-edited draft, a
catalog row with no CSV line — are one dict literal instead of a live mutation.
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
REPO = os.path.dirname(os.path.dirname(TOOLS))
sys.path.insert(0, TOOLS)
sys.path.insert(0, HERE)

import export_content  # noqa: E402
import import_content  # noqa: E402
from catalogs import CATALOGS_BY_NAME, read_csv  # noqa: E402
from fakes import FakePostgrestClient, published_row  # noqa: E402

# `balls` is the smallest real catalog (2 rows, 11 columns, several quoted
# fields that carry no comma) — small enough to write inline, and exactly the
# shape that breaks a naive re-quoting exporter.
BALLS = CATALOGS_BY_NAME["balls"]

HEADER = "id,name,brand,power,rebound,windResistance,roll,spin,thumbnailSprite,fullSprite,info"
ROW_GOLFIN = 'ball_golfin,Golfin,Golfin,0,0,0,0,0,Golfin,Golfin,"The standard ball."'
ROW_ACE = 'ball_putt_ace,Putt Ace,Putt Ace,10,-6,0,5,-4,PuttAce,PuttAce,"Short-game mastery."'

NOW = "2026-08-27T12:00:00Z"
BY = "tests@golfin"


def data_golfin(**overrides) -> dict:
    row = {
        "id": "ball_golfin", "name": "Golfin", "brand": "Golfin",
        "power": "0", "rebound": "0", "windResistance": "0", "roll": "0", "spin": "0",
        "thumbnailSprite": "Golfin", "fullSprite": "Golfin",
        "info": "The standard ball.",
    }
    row.update(overrides)
    return row


def data_ace(**overrides) -> dict:
    row = {
        "id": "ball_putt_ace", "name": "Putt Ace", "brand": "Putt Ace",
        "power": "10", "rebound": "-6", "windResistance": "0", "roll": "5", "spin": "-4",
        "thumbnailSprite": "PuttAce", "fullSprite": "PuttAce",
        "info": "Short-game mastery.",
    }
    row.update(overrides)
    return row


class TempRepo:
    """A repo root holding one catalog's CSV at its real relative path."""

    def __init__(self, lines):
        self.root = tempfile.mkdtemp(prefix="content_two_way_")
        self.write(lines)

    def write(self, lines):
        path = os.path.join(self.root, BALLS.csv_path)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write("\n".join(lines) + "\n")

    def read_bytes(self) -> bytes:
        with open(os.path.join(self.root, BALLS.csv_path), "rb") as fh:
            return fh.read()

    def close(self):
        shutil.rmtree(self.root, ignore_errors=True)


def plan_for(repo: TempRepo, client: FakePostgrestClient, *, min_build=9999,
             overwrite_dirty=False):
    return import_content.build_plan(
        BALLS,
        repo.root,
        import_content.table_rows(client, "content_rows", BALLS.name),
        import_content.table_rows(client, "content_drafts", BALLS.name),
        min_build,
        BY,
        NOW,
        overwrite_dirty,
    )


# ---------------------------------------------------------------------------


class PlanVerdicts(unittest.TestCase):
    def setUp(self):
        self.repo = TempRepo([HEADER, ROW_GOLFIN, ROW_ACE])
        self.addCleanup(self.repo.close)

    def test_a_row_in_neither_table_is_an_ADD_at_the_runs_min_build(self):
        plan = plan_for(self.repo, FakePostgrestClient(), min_build=2400)

        self.assertEqual(2, len(plan.adds))
        self.assertEqual(0, len(plan.changes))
        self.assertEqual(0, plan.unchanged)
        self.assertEqual({"ball_golfin", "ball_putt_ace"}, {r["row_id"] for r in plan.adds})
        for row in plan.adds:
            self.assertEqual(2400, row["min_build"],
                             "an ADD takes the run's min_build — high on purpose (§2).")
            self.assertTrue(row["is_active"])
            self.assertNotIn("is_active", row["data"],
                             "is_active is a COLUMN, never a field of data.")

    def test_a_published_row_the_csv_disagrees_with_is_a_CHANGE(self):
        client = FakePostgrestClient({
            "content_rows": [published_row("balls", "ball_golfin", data_golfin(power="3"),
                                           min_build=1200)],
            "content_drafts": [published_row("balls", "ball_golfin", data_golfin(power="3"),
                                             min_build=1200)],
        })
        plan = plan_for(self.repo, client)

        self.assertEqual(1, len(plan.changes))
        self.assertEqual(1, len(plan.adds), "the other row is still an ADD")
        change = plan.changes[0]
        self.assertEqual("ball_golfin", change["row_id"])
        self.assertEqual("0", change["data"]["power"], "the CSV value is what gets proposed")

    def test_a_CHANGE_never_moves_min_build(self):
        # §D1.7: immutable once published. Re-deriving it here would move a live
        # row's floor under builds already in the field.
        client = FakePostgrestClient({
            "content_rows": [published_row("balls", "ball_golfin", data_golfin(power="3"),
                                           min_build=1200)],
            "content_drafts": [published_row("balls", "ball_golfin", data_golfin(power="3"),
                                             min_build=1200)],
        })
        plan = plan_for(self.repo, client, min_build=9999)
        self.assertEqual(1200, plan.changes[0]["min_build"])

    def test_a_matching_row_is_counted_and_not_written(self):
        rows = [published_row("balls", "ball_golfin", data_golfin()),
                published_row("balls", "ball_putt_ace", data_ace())]
        client = FakePostgrestClient({"content_rows": rows, "content_drafts": rows})
        plan = plan_for(self.repo, client)

        self.assertEqual(0, plan.touched)
        self.assertEqual(2, plan.unchanged)
        self.assertEqual([], plan.writes)

    def test_a_row_deleted_from_the_csv_is_reported_never_deactivated(self):
        rows = [published_row("balls", "ball_golfin", data_golfin()),
                published_row("balls", "ball_putt_ace", data_ace()),
                published_row("balls", "ball_retired", data_golfin(id="ball_retired"))]
        client = FakePostgrestClient({"content_rows": rows, "content_drafts": rows})
        self.repo.write([HEADER, ROW_GOLFIN, ROW_ACE])   # ball_retired is not in the CSV

        plan = plan_for(self.repo, client)

        self.assertEqual(["ball_retired"], plan.catalog_only)
        self.assertEqual(0, plan.touched, "reporting it is the whole action (I6)")
        for row in client.rows("content_rows"):
            self.assertTrue(row["is_active"],
                            "a CSV that omits a row must never deactivate it")


class DirtyDrafts(unittest.TestCase):
    """The clobber rule: somebody's unfinished admin edit is not ours to overwrite."""

    def setUp(self):
        self.repo = TempRepo([HEADER, ROW_GOLFIN, ROW_ACE])
        self.addCleanup(self.repo.close)

        # ball_golfin: published at power=3, draft moved to power=7 — mid-edit.
        self.client = FakePostgrestClient({
            "content_rows": [
                published_row("balls", "ball_golfin", data_golfin(power="3"), min_build=1200),
                published_row("balls", "ball_putt_ace", data_ace()),
            ],
            "content_drafts": [
                published_row("balls", "ball_golfin", data_golfin(power="7"), min_build=1200),
                published_row("balls", "ball_putt_ace", data_ace()),
            ],
        })

    def test_a_mid_edit_draft_is_a_conflict_and_is_not_written(self):
        plan = plan_for(self.repo, self.client)

        self.assertEqual(1, len(plan.conflicts))
        self.assertIn("ball_golfin", plan.conflicts[0])
        self.assertIn("edited in the admin", plan.conflicts[0],
                      "the message has to say WHY, or it is not actionable")
        self.assertEqual(0, plan.touched)

    def test_overwrite_dirty_lets_the_csv_win_and_names_the_row(self):
        plan = plan_for(self.repo, self.client, overwrite_dirty=True)

        self.assertEqual([], plan.conflicts)
        self.assertEqual(["ball_golfin"], plan.overwritten,
                         "a clobbered edit must be NAMED — the person who made it has to see it")
        self.assertEqual(1, len(plan.changes))
        self.assertEqual("0", plan.changes[0]["data"]["power"])

    def test_an_unpublished_admin_row_matching_the_csv_is_not_a_conflict(self):
        # Created in the admin, never published, and the CSV agrees with it. There
        # is nothing to propose and nothing to argue about.
        repo = TempRepo([HEADER, ROW_GOLFIN])
        self.addCleanup(repo.close)
        client = FakePostgrestClient({
            "content_rows": [],
            "content_drafts": [published_row("balls", "ball_golfin", data_golfin(), min_build=77)],
        })
        plan = plan_for(repo, client)

        self.assertEqual([], plan.conflicts)
        self.assertEqual(0, plan.touched)
        self.assertEqual(1, plan.unchanged)

    def test_overwriting_an_unpublished_admin_row_keeps_ITS_min_build(self):
        # It was chosen deliberately in the admin; this run's default is a guess.
        repo = TempRepo([HEADER, ROW_GOLFIN])
        self.addCleanup(repo.close)
        client = FakePostgrestClient({
            "content_rows": [],
            "content_drafts": [published_row("balls", "ball_golfin", data_golfin(power="7"),
                                             min_build=77)],
        })
        plan = plan_for(repo, client, min_build=9999, overwrite_dirty=True)

        self.assertEqual(1, len(plan.changes))
        self.assertEqual(77, plan.changes[0]["min_build"])


class IsActiveColumn(unittest.TestCase):
    """`is_active` is a table COLUMN the exporter appends only when some row is
    inactive. The importer has to split it back out, or the catalog grows a
    phantom `data.is_active` every parser downstream would have to ignore."""

    def test_the_flag_leaves_data_and_becomes_the_column(self):
        repo = TempRepo([
            HEADER + ",is_active",
            ROW_GOLFIN + ",true",
            ROW_ACE + ",false",
        ])
        self.addCleanup(repo.close)

        plan = plan_for(repo, FakePostgrestClient())
        by_id = {r["row_id"]: r for r in plan.adds}

        self.assertTrue(by_id["ball_golfin"]["is_active"])
        self.assertFalse(by_id["ball_putt_ace"]["is_active"])
        for row in plan.adds:
            self.assertNotIn("is_active", row["data"])

    def test_a_csv_that_reactivates_a_deactivated_row_is_a_change(self):
        repo = TempRepo([HEADER + ",is_active", ROW_GOLFIN + ",true"])
        self.addCleanup(repo.close)
        rows = [published_row("balls", "ball_golfin", data_golfin(), is_active=False)]
        client = FakePostgrestClient({"content_rows": rows, "content_drafts": rows})

        plan = plan_for(repo, client)
        self.assertEqual(1, len(plan.changes))
        self.assertTrue(plan.changes[0]["is_active"])


class Applying(unittest.TestCase):
    def setUp(self):
        self.repo = TempRepo([HEADER, ROW_GOLFIN, ROW_ACE])
        self.addCleanup(self.repo.close)

    def test_apply_writes_drafts_and_an_audit_row_and_never_content_rows(self):
        client = FakePostgrestClient()
        plan = plan_for(self.repo, client, min_build=2400)
        import_content.apply_plan(client, plan, BY)

        self.assertEqual(2, len(client.rows("content_drafts")))
        self.assertEqual([], client.tables.get("content_rows", []),
                         "publish is the only way into content_rows (§D1)")

        audit = client.rows("admin_audit_log")
        self.assertEqual(2, len(audit))
        self.assertEqual("content.draft.create:balls", audit[0]["action"])
        self.assertEqual(BY, audit[0]["admin_email"])
        self.assertEqual("import_content.py", audit[0]["after"]["via"],
                         "the Audit panel has to be able to tell a script from a person typing")


class CliRefusal(unittest.TestCase):
    """The refusal is a WHOLE-RUN decision, so it only exists at the CLI level —
    `build_plan` merely reports the conflicts. This drives `main()`."""

    def setUp(self):
        self.repo = TempRepo([HEADER, ROW_GOLFIN, ROW_ACE])
        self.addCleanup(self.repo.close)
        self.client = FakePostgrestClient({
            "content_rows": [published_row("balls", "ball_golfin", data_golfin(power="3"))],
            "content_drafts": [published_row("balls", "ball_golfin", data_golfin(power="7"))],
        })

    def run_main(self, *argv):
        args = ["import_content.py", "--repo-root", self.repo.root, "--catalogs", "balls",
                "--min-build", "2400", "--by", BY, *argv]
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(sys, "argv", args), \
             mock.patch.object(import_content.PostgrestClient, "from_env",
                               classmethod(lambda cls, env=None: self.client)), \
             redirect_stdout(out), redirect_stderr(err):
            code = import_content.main()
        return code, out.getvalue() + err.getvalue()

    def test_a_dirty_draft_refuses_the_whole_run_and_writes_nothing(self):
        code, text = self.run_main("--apply")

        self.assertEqual(1, code)
        self.assertIn("REFUSED", text)
        self.assertIn("ball_golfin", text, "the refusal must NAME the row")
        self.assertIn("--overwrite-dirty", text, "and say what unblocks it")
        self.assertEqual([], self.client.writes,
                         "REFUSED means nothing was written — including the clean row, "
                         "because a half-applied import is a state nobody can reason about")

    def test_overwrite_dirty_applies_and_says_what_it_clobbered(self):
        code, text = self.run_main("--apply", "--overwrite-dirty")

        self.assertEqual(0, code)
        self.assertIn("OVERWRITING", text)
        self.assertIn("ball_golfin", text)
        self.assertEqual(2, len(self.client.rows("content_drafts")))
        self.assertEqual("0", self.client.find("content_drafts", "balls", "ball_golfin")["data"]["power"])

    def test_plan_only_is_the_default_and_writes_nothing(self):
        code, text = self.run_main("--overwrite-dirty")   # no --apply

        self.assertEqual(0, code)
        self.assertIn("PLAN ONLY", text)
        self.assertEqual([], self.client.writes)


class RoundTripProperty(unittest.TestCase):
    """import → publish → export leaves the CSV BYTE-IDENTICAL.

    This is the property the whole two-way loop rests on. It runs against the
    REAL `Assets/Data/Balls.csv`, copied into a temp root: a synthetic fixture
    would not carry the quoting that makes the claim non-trivial (several fields
    are quoted without containing a comma, which `QUOTE_MINIMAL` would drop).
    """

    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="content_two_way_rt_")
        self.addCleanup(shutil.rmtree, self.root, True)
        src = os.path.join(REPO, BALLS.csv_path)
        dst = os.path.join(self.root, BALLS.csv_path)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        self.dst = dst
        with open(dst, "rb") as fh:
            self.original = fh.read()

        self.client = FakePostgrestClient({
            "content_catalogs": [{"name": "balls", "published_version": 1}],
        })

    def import_publish_export(self):
        plan = import_content.build_plan(
            BALLS, self.root,
            import_content.table_rows(self.client, "content_rows", "balls"),
            import_content.table_rows(self.client, "content_drafts", "balls"),
            2400, BY, NOW, False,
        )
        import_content.apply_plan(self.client, plan, BY)
        self.client.publish("balls")

        published = export_content.fetch_catalog(self.client, "balls")
        text, _ = export_content.render_csv(BALLS, published, self.root)
        with open(self.dst, "wb") as fh:
            fh.write(text.encode("utf-8"))
        return plan

    def test_the_loop_is_byte_identical(self):
        plan = self.import_publish_export()

        self.assertEqual(2, len(plan.adds), "a catalog with no rows means every CSV row is new")
        self.assertEqual(self.original, self.read(),
                         "import → publish → export must not move a single byte of the CSV")

    def test_a_value_edited_in_unity_survives_the_loop_in_canonical_form(self):
        # Seed the catalog from the pristine file first, so the edit below is the
        # only difference — the real shape of "somebody changed a number in Unity".
        self.import_publish_export()
        self.assertEqual(self.original, self.read())

        edited = self.original.decode("utf-8").replace(
            "ball_putt_ace,Putt Ace,Putt Ace,10,", "ball_putt_ace,Putt Ace,Putt Ace,9,", 1)
        self.assertNotEqual(self.original.decode("utf-8"), edited, "the fixture must actually change")
        with open(self.dst, "w", encoding="utf-8", newline="") as fh:
            fh.write(edited)

        plan = self.import_publish_export()

        self.assertEqual(1, len(plan.changes), "exactly one row differs")
        self.assertEqual("ball_putt_ace", plan.changes[0]["row_id"])
        self.assertEqual(edited.encode("utf-8"), self.read(),
                         "after publish, the export reproduces the edit and nothing else")

        # …and the loop has converged: a second pass proposes nothing.
        again = import_content.build_plan(
            BALLS, self.root,
            import_content.table_rows(self.client, "content_rows", "balls"),
            import_content.table_rows(self.client, "content_drafts", "balls"),
            2400, BY, NOW, False,
        )
        self.assertEqual(0, again.touched)
        self.assertEqual(2, again.unchanged)

    def read(self) -> bytes:
        with open(self.dst, "rb") as fh:
            return fh.read()


if __name__ == "__main__":
    unittest.main()
