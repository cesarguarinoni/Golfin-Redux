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
read by nothing but a manual export. `weekly_rotation_admin` §3.1 brought it to
twenty-one (`rotations`), and is also the first catalog whose CSV carries an
`is_active` column at SEED time — so the seeder's handling of that column is
pinned here too.
"""

from __future__ import annotations

import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, TOOLS)
sys.path.insert(0, HERE)

from catalogs import CATALOGS, CATALOGS_BY_NAME, IS_ACTIVE_COLUMN, read_csv  # noqa: E402
from seed_from_csv import seed_rows  # noqa: E402

#: The rows gacha_admin_catalogs seeded and round-tripped, pinned BY ID.
#:
#: These used to be row COUNTS (4 / 6 / 11 / 2). weekly_rotation_admin made a
#: count the wrong shape: every Monday's rotation publish adds a banner, a
#: pool and a rate table to three of these catalogs by construction, so a
#: pinned count would fail on the first live rotation — and did, on 2026-09-11,
#: 8 of 53 tests, caught by the self-reviewer. What the pin MEANT was "the
#: seeded rows survived the round trip"; the ids say that and do not drift.
SEEDED_GACHA_ROW_IDS = {
    "gacha_banners": {"banner_standard_club1", "banner_test_a", "banner_test_b", "banner_inactive"},
    "gacha_rates": {f"pool_standard_club1_{r}" for r in
                    ("common", "uncommon", "rare", "mythic", "legendary", "supreme")},
    "gacha_pools": {"psc1_driver_gf", "psc1_wood_gf", "psc1_ball_golfin", "psc1_iron9_klyro",
                    "psc1_repairkit_common", "psc1_iron7_mireo", "psc1_repairkit_rare",
                    "psc1_awedge_fyloe", "psc1_repairkit_mythic", "psc1_pwedge_royal",
                    "psc1_putter_golfinx"},
    "ticket_types": {"0", "1"},
}

#: The year plan weekly_rotation_admin seeded: ISO 2026 has 53 weeks, so the
#: 52 planned ids run wk_2026_38 .. wk_2026_53, wk_2027_01 .. wk_2027_36.
PLANNED_ROTATION_IDS = (
    [f"wk_2026_{w:02d}" for w in range(38, 54)] + [f"wk_2027_{w:02d}" for w in range(1, 37)]
)


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

    def test_the_table_holds_twenty_one_catalogs(self):
        self.assertEqual(21, len(CATALOGS))

    def test_the_seeded_rows_are_what_was_round_tripped(self):
        # By id, not by count — a rotation publish appends to three of these
        # every week (see SEEDED_GACHA_ROW_IDS).
        for name, expected in SEEDED_GACHA_ROW_IDS.items():
            with self.subTest(catalog=name):
                ids = {ln.row_id for ln in read_csv(CATALOGS_BY_NAME[name]).rows}
                self.assertTrue(expected <= ids, f"{name} lost seeded rows: {sorted(expected - ids)}")

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


class TestRotationsIsRegistered(unittest.TestCase):
    """weekly_rotation_admin §3.1 — catalog #21, and the seeder rule it forced."""

    def test_rotations_is_in_the_table_keyed_by_rotationId(self):
        self.assertIn("rotations", CATALOGS_BY_NAME)
        self.assertEqual("rotationId", CATALOGS_BY_NAME["rotations"].id_column)
        self.assertEqual("Assets/Resources/Data/rotations.csv", CATALOGS_BY_NAME["rotations"].csv_path)

    def test_the_year_plan_is_present_and_pinned(self):
        # The 52 planned weeks are pinned BY ID. Not "the file has 52 rows": a
        # rotation row is content, and the admin adds rows — the two archived
        # pity-E2E test rotations (wk_2026_36 / wk_2026_37, 2026-09-11) are the
        # first — so the file grows and the plan must simply still be in it.
        f = read_csv(CATALOGS_BY_NAME["rotations"])
        by_id = dict(f.as_dicts())
        missing = [rid for rid in PLANNED_ROTATION_IDS if rid not in by_id]
        self.assertEqual([], missing, "planned weeks missing from rotations.csv")
        for rid in PLANNED_ROTATION_IDS:
            with self.subTest(row=rid):
                for col in ("pinnedClubs", "pinnedBalls", "pinnedCharacter", "pinnedFeatured"):
                    self.assertNotEqual("", by_id[rid][col], f"{rid}: the year plan pins every bucket")
        for rid in by_id:
            self.assertRegex(rid, r"^wk_\d{4}_\d{2}$")

    def test_rotations_csv_is_LF_and_materializedAt_is_blank_or_an_instant(self):
        # LF is the exporter's canonical form (the reference plan is CRLF).
        # `materializedAt` is NOT pinned blank: a materialized week is the
        # normal state of a rotation once the admin has generated it — but when
        # it is set it must be the ISO instant the generator writes.
        f = read_csv(CATALOGS_BY_NAME["rotations"])
        self.assertFalse(f.crlf, "rotations.csv must be LF — the exporter's canonical form")
        for rid, data in f.as_dicts():
            with self.subTest(row=rid):
                at = data["materializedAt"]
                if at:
                    self.assertRegex(at, r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")

    def test_the_seeder_splits_is_active_out_of_data(self):
        # The one non-obvious rule of the pipeline, now applied by all three
        # scripts: `is_active` is `content_rows.is_active`, never a `data` field,
        # and its value follows the CSV cell — the archived test rotations read
        # `false`, the plan rows `true`.
        cat = CATALOGS_BY_NAME["rotations"]
        f = read_csv(cat)
        self.assertIn(IS_ACTIVE_COLUMN, f.header, "rotations.csv carries is_active from day one")
        cells = {rid: data[IS_ACTIVE_COLUMN] for rid, data in f.as_dicts()}
        rows = seed_rows(cat)
        self.assertEqual(len(f.rows), len(rows))
        for rid, data, active in rows:
            with self.subTest(row=rid):
                self.assertNotIn(IS_ACTIVE_COLUMN, data, "is_active leaked into data")
                self.assertEqual(cells[rid].strip().lower() != "false", active)
                self.assertEqual(len(f.header) - 1, len(data))
        for rid in PLANNED_ROTATION_IDS:
            self.assertTrue(dict((r, a) for r, _, a in rows)[rid], f"{rid} is a planned week and must be active")

    def test_a_false_is_active_cell_seeds_an_inactive_row(self):
        import os as _os
        import tempfile
        cat = CATALOGS_BY_NAME["rotations"]
        with tempfile.TemporaryDirectory() as root:
            _os.makedirs(_os.path.join(root, "Assets", "Resources", "Data"))
            with open(_os.path.join(root, cat.csv_path), "w", encoding="utf-8", newline="\n") as fh:
                fh.write("rotationId,startUtc,is_active\n")
                fh.write("wk_2026_01,2026-01-05T00:00:00Z,false\n")
                fh.write("wk_2026_02,2026-01-12T00:00:00Z,true\n")
            rows = seed_rows(cat, root)
        self.assertEqual([("wk_2026_01", False), ("wk_2026_02", True)],
                         [(rid, active) for rid, _, active in rows])
        for _, data, _ in rows:
            self.assertNotIn(IS_ACTIVE_COLUMN, data)


if __name__ == "__main__":
    unittest.main()
