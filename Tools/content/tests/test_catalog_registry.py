#!/usr/bin/env python3
"""Every catalog in `catalogs.py` actually reads — the table-driven sweep.

    python3 -m unittest discover Tools/content/tests

The other three test modules are about ONE catalog each (`balls`) because they
test BEHAVIOUR — the import plan, the `--check` direction report, the scoped-seed
guard — and one catalog is enough to pin behaviour. What none of them tests is
the registry itself: that each of the twenty `Catalog(...)` entries names a file
that exists, whose header contains the declared id column, whose ids are unique
and non-empty, and whose rows are all the width of the header.

Every one of those is already enforced by `read_csv` (it raises), so this module
is thin on purpose — it is the sweep that makes the enforcement RUN over the
whole table on every `discover`, instead of only over whichever catalog the
release engineer happened to export.

Added by `gacha_admin_catalogs` §4, which brought the table to twenty: the four
gacha catalogs are the ones that would otherwise reach production having been
read by nothing but a manual export.
"""

from __future__ import annotations

import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, TOOLS)
sys.path.insert(0, HERE)

from catalogs import CATALOGS, CATALOGS_BY_NAME, read_csv  # noqa: E402

#: Catalogs whose row COUNT is pinned here, with the value from the module
#: docstring's CSV-facts list. Only the four this task added: pinning the other
#: sixteen would make every legitimate content edit a test failure, which is how
#: a suite gets switched off. These four are seeded and round-tripped in the same
#: task, so a change to their size is a change to what was just verified.
GACHA_ROW_COUNTS = {
    "gacha_banners": 4,
    "gacha_rates": 6,
    "gacha_pools": 11,
    "ticket_types": 2,
}


class TestEveryCatalogReads(unittest.TestCase):
    def test_every_catalog_file_parses(self):
        for cat in CATALOGS:
            with self.subTest(catalog=cat.name):
                f = read_csv(cat)
                self.assertIn(cat.id_column, f.header,
                              f"{cat.csv_path}: id column {cat.id_column!r} is not in the header")
                self.assertGreater(len(f.rows), 0, f"{cat.csv_path}: no data rows")

    def test_row_ids_are_unique_and_non_empty(self):
        # read_csv raises on both, so this asserts the invariant holds rather
        # than re-implementing the check.
        for cat in CATALOGS:
            with self.subTest(catalog=cat.name):
                ids = [ln.row_id for ln in read_csv(cat).rows]
                self.assertTrue(all(ids), f"{cat.csv_path}: an empty {cat.id_column}")
                self.assertEqual(len(ids), len(set(ids)), f"{cat.csv_path}: duplicate ids")

    def test_every_row_is_the_width_of_its_header(self):
        for cat in CATALOGS:
            with self.subTest(catalog=cat.name):
                f = read_csv(cat)
                for ln in f.rows:
                    self.assertEqual(len(f.header), len(ln.values or []),
                                     f"{cat.csv_path}: ragged row {ln.row_id!r}")


class TestGachaCatalogsAreRegistered(unittest.TestCase):
    """gacha_admin_catalogs §4 — the four new entries, by name and id column."""

    EXPECTED_ID_COLUMN = {
        "gacha_banners": "bannerId",
        "gacha_rates": "id",
        "gacha_pools": "id",
        "ticket_types": "id",
    }

    def test_all_four_are_in_the_table(self):
        for name, id_column in self.EXPECTED_ID_COLUMN.items():
            with self.subTest(catalog=name):
                self.assertIn(name, CATALOGS_BY_NAME, f"{name} is not registered in catalogs.py")
                self.assertEqual(id_column, CATALOGS_BY_NAME[name].id_column)

    def test_the_table_holds_twenty_catalogs(self):
        self.assertEqual(20, len(CATALOGS))

    def test_the_seeded_row_counts_are_what_was_round_tripped(self):
        for name, expected in GACHA_ROW_COUNTS.items():
            with self.subTest(catalog=name):
                self.assertEqual(expected, len(read_csv(CATALOGS_BY_NAME[name]).rows))

    def test_gacha_banners_keeps_the_nine_columns_the_shipped_client_reads(self):
        # The client parser is header-indexed since gacha_admin_catalogs §3, so
        # a column REORDER is now harmless — but a column REMOVAL still blanks a
        # field on every installed build. These nine are the contract.
        header = read_csv(CATALOGS_BY_NAME["gacha_banners"]).header
        for column in ("bannerId", "nameKey", "artSprite", "costX1", "costX10",
                       "endUtc", "rulesUrl", "sortOrder", "active"):
            self.assertIn(column, header, f"gacha_banners.csv lost {column!r}")

    def test_ticket_type_ids_are_integers(self):
        # `id` is the `ticketTypeInt` persisted in player saves — never renumber,
        # append only (SPEC §2.4). A non-integer id would be a ticket kind no
        # save can hold.
        for ln in read_csv(CATALOGS_BY_NAME["ticket_types"]).rows:
            with self.subTest(row=ln.row_id):
                self.assertTrue(ln.row_id.isdigit(), f"ticket_types id {ln.row_id!r} is not an integer")


if __name__ == "__main__":
    unittest.main()
