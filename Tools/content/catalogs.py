"""Shared catalog↔CSV mapping for the content pipeline (Phase 0).

One definition of "which CSV is which catalog, and what its id column is",
imported by both `seed_from_csv.py` (repo → Supabase) and `export_content.py`
(Supabase → repo). Having the two directions read the same table is what makes
the §3 round-trip test meaningful — a lossy mapping is lossy in both scripts or
in neither.

Spec: Docs/Specs/Active/content_catalog/SPEC.md §A2 / §C.
Plan: Docs/CONTENT_PIPELINE_PLAN.md §2 (invariants I1/I3/I4/I6), §3.

CSV FACTS this module encodes, all verified against the live repo 2026-08-25
(re-verify with `python3 Tools/content/catalogs.py` — it prints them):

  clubs         799 rows  Assets/Resources/Data/Clubs.csv       3 leading `#` lines
  characters     12 rows  Assets/Data/Characters.csv
  items           3 rows  Assets/Data/Items.csv
  bags           10 rows  Assets/Data/Bags.csv                  CRLF line endings
  balls           2 rows  Assets/Data/Balls.csv
  texts         501 rows  Assets/Localization/LocalizationText.csv   1 MID-FILE `#` line
  shop_catalog    5 rows  Assets/Resources/Data/shop_catalog.csv
  level_up_costs 240 rows Assets/Data/LevelUpCosts.csv
  modes           5 rows  Assets/Resources/Data/modes.csv
  missions       40 rows  Assets/Resources/Data/missions.csv
  mission_start_areas 162 rows  Assets/Resources/Data/mission_start_areas.csv
  mission_wind_presets  9 rows  Assets/Resources/Data/mission_wind_presets.csv
  mission_loadouts     13 rows  Assets/Resources/Data/mission_loadouts.csv
  mission_goal_weights 36 rows  Assets/Resources/Data/mission_goal_weights.csv
  mission_tiers         4 rows  Assets/Resources/Data/mission_tiers.csv
  daily_mission_weights 43 rows Assets/Resources/Data/daily_mission_weights.csv
  gacha_banners   4 rows  Assets/Resources/Data/gacha_banners.csv
  gacha_rates     6 rows  Assets/Resources/Data/gacha_rates.csv
  gacha_pools    11 rows  Assets/Resources/Data/gacha_pools.csv
  ticket_types    2 rows  Assets/Resources/Data/ticket_types.csv

Two of those facts contradict the SPEC's reference counts and both are handled
rather than papered over:

  * `texts` is 501 key rows, not 500. The 502nd parsed line is a `#` comment
    sitting in the MIDDLE of the file (above HOME_MAINTENANCE_TITLE), not at the
    top like Clubs.csv's. `LocalizationTextImporter` drops it because it has
    fewer than 3 columns; this module drops it because it starts with `#`, and
    the exporter puts it back where it was.
  * `Bags.csv` is CRLF while the other six are LF.

LINE STRUCTURE IS PART OF THE FILE, NOT PART OF THE CATALOG. Comment lines and
blank lines are never seeded — they carry no row_id and no player-facing data.
The exporter preserves them, in place, by rewriting the EXISTING file rather
than regenerating one from scratch. That is also how row ORDER survives the
round trip: the schema has no sort column (deliberately — §3), so the repo CSV
is the authority on order and Supabase is the authority on values.
"""

from __future__ import annotations

import csv
import io
import os
from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple

# Repo root = two levels up from this file (Tools/content/catalogs.py).
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


@dataclass(frozen=True)
class Catalog:
    """One content catalog and the repo CSV it mirrors."""

    name: str
    """`content_catalogs.name` — must already exist in the migration's registry."""

    csv_path: str
    """Repo-relative path. Resolved against REPO_ROOT."""

    id_column: str
    """The CSV column that becomes `content_rows.row_id`."""

    def abs_path(self, repo_root: str = REPO_ROOT) -> str:
        return os.path.join(repo_root, self.csv_path)


# Order here is the order the seed SQL and the export report use.
#
# `level_up_costs` is the NINTH... eighth entry and the ninth catalog counting the
# two that ride inside the Items panel — added 2026-08-28 by progress_server_side
# §2, which answered CONTENT_PIPELINE_PLAN.md §9 open question 2 ("should the
# level-up cost table be admin-tunable?") with yes. It is the one catalog whose
# rows the SERVER prices from directly: golfin_level_up() sums `cost_r` over the
# published rows, so a gap here is a level nobody can buy and an edit here moves
# what every player pays. Its id column is `level` — the only non-`id`/`key`/
# `entryId` id in the table, and an integer written as text, because a row_id is
# text everywhere else in the pipeline.

# `modes` is the TENTH — added 2026-08-28 by game_modes_admin §2. It is the
# SECOND catalog the server reads (level_up_costs was the first): a publish
# mirrors `entryFee`/`locked` into `golfin_mode_fees`, which POST /points/spend
# prices a `mode_entry_fee:<id>` debit against. So the same warning applies —
# an edit here is not display copy, it is what a player is charged to enter.
#
# Its CSV is the one with quoted, comma-bearing prose (three of the five
# `description` fields), which is exactly the case `parse_csv_line` /
# `write_csv_line` exist for; QUOTE_MINIMAL round-trips it unchanged.
# The SEVEN MISSIONS catalogs (#11-#17) — missions_v1 §A2, added 2026-08-29.
#
# `missions` is the THIRD catalog the server reads, after level_up_costs and
# modes, and it is read the same way modes is: a publish MIRRORS every row's
# tier and RP into `golfin_mission_rewards` in the same transaction, and
# POST /api/v1/missions/claim pays from THAT, never from the client's number.
# `mission_tiers` mirrors alongside it into `golfin_mission_tier_bonus`. So an
# edit to either is not card copy — it is what a player is actually paid.
#
# The other five are COMPONENTS a mission is composed from (hole start area,
# wind, loadout, the difficulty curve, the daily draw weights). They are pure
# client/​generator data with no server mirror, but they are not inert: the
# admin RECOMPUTES every mission's difficultyScore from `mission_goal_weights`
# on publish, so a weight edit re-tiers the campaign.
#
# `mission_tiers`'s id column is `tier` ("Beginner"), the second non-`id` id in
# the table after level_up_costs' `level`. Four rows, and the tier NAME is what
# a missions row references, so a synthetic id would be a second name for the
# same thing.
#
# ⚠️ `mission_start_areas` ships PARTLY BLANK on purpose: its 162 rows are the
# slots the Phase B bake fills with coordinates. See the CSV's own header.

# The FOUR GACHA catalogs (#17-#20) — gacha_admin_catalogs §4, added 2026-08-31.
#
# `gacha_banners` is not new data: the CSV has shipped since gacha_screen Stage 2
# and the client has always read it. What is new is that it is now a CATALOG —
# export/import/`--check`, an admin panel, publish validation — plus thirteen
# columns (§2.1: the scheduling window's start, the pool and ticket it rolls,
# pity, the x10 guarantee, the per-player cap, admin art, per-locale title and
# tagline, featured refs). The build in the wild ignores all thirteen; reading
# them is `gacha_client_real_pull`.
#
# `gacha_rates`, `gacha_pools` and `ticket_types` are new files. Together they
# are WHAT A PULL PAYS OUT and WHAT IT COSTS, so all four are catalogs the
# SERVER will read — `golfin_gacha_pull()` reads the published `content_rows`
# rows DIRECTLY, the way `golfin_shop_purchase()` prices from `shop_catalog`.
# There is deliberately NO mirror table (plan §2), so nothing here needs a
# `mirrorForCatalog` entry — but the same warning applies as to `modes` and
# `missions`: an edit to a rate or a weight is not display copy, it is what a
# player actually receives for a ticket.
#
# Two id columns are worth naming. `gacha_banners` keeps `bannerId` (the client
# has always resolved banners by it). `ticket_types` uses `id`, and that id is
# an INTEGER WRITTEN AS TEXT — it is the `ticketTypeInt` persisted in player
# saves (`TicketType.Standard = 0`), which is why the catalog may be appended to
# but never renumbered.

CATALOGS: Tuple[Catalog, ...] = (
    Catalog("clubs", "Assets/Resources/Data/Clubs.csv", "id"),
    Catalog("characters", "Assets/Data/Characters.csv", "id"),
    Catalog("items", "Assets/Data/Items.csv", "id"),
    Catalog("bags", "Assets/Data/Bags.csv", "id"),
    Catalog("balls", "Assets/Data/Balls.csv", "id"),
    Catalog("texts", "Assets/Localization/LocalizationText.csv", "key"),
    Catalog("shop_catalog", "Assets/Resources/Data/shop_catalog.csv", "entryId"),
    Catalog("level_up_costs", "Assets/Data/LevelUpCosts.csv", "level"),
    Catalog("modes", "Assets/Resources/Data/modes.csv", "id"),
    Catalog("missions", "Assets/Resources/Data/missions.csv", "id"),
    Catalog("mission_start_areas", "Assets/Resources/Data/mission_start_areas.csv", "id"),
    Catalog("mission_wind_presets", "Assets/Resources/Data/mission_wind_presets.csv", "id"),
    Catalog("mission_loadouts", "Assets/Resources/Data/mission_loadouts.csv", "id"),
    Catalog("mission_goal_weights", "Assets/Resources/Data/mission_goal_weights.csv", "id"),
    Catalog("mission_tiers", "Assets/Resources/Data/mission_tiers.csv", "tier"),
    Catalog("daily_mission_weights", "Assets/Resources/Data/daily_mission_weights.csv", "id"),
    Catalog("gacha_banners", "Assets/Resources/Data/gacha_banners.csv", "bannerId"),
    Catalog("gacha_rates", "Assets/Resources/Data/gacha_rates.csv", "id"),
    Catalog("gacha_pools", "Assets/Resources/Data/gacha_pools.csv", "id"),
    Catalog("ticket_types", "Assets/Resources/Data/ticket_types.csv", "id"),
)

CATALOGS_BY_NAME: Dict[str, Catalog] = {c.name: c for c in CATALOGS}

COMMENT_PREFIX = "#"


# ---------------------------------------------------------------------------
# Reading
# ---------------------------------------------------------------------------


@dataclass
class Line:
    """One physical line of a CSV, classified.

    `kind` is 'comment' | 'blank' | 'header' | 'row'. `raw` is the line exactly
    as it appeared (minus its terminator), which is what lets the exporter put
    an untouched line back byte-for-byte instead of re-quoting it.
    """

    kind: str
    raw: str
    values: Optional[List[str]] = None
    row_id: Optional[str] = None


@dataclass
class CsvFile:
    """A parsed CSV, keeping BOTH the row data and the physical line layout."""

    catalog: Catalog
    header: List[str]
    lines: List[Line]
    crlf: bool
    """True when the source file used \\r\\n. Recorded so the report can name the
    one file (Bags.csv) whose line endings the exporter normalises."""

    @property
    def rows(self) -> List[Line]:
        return [ln for ln in self.lines if ln.kind == "row"]

    def as_dicts(self) -> List[Tuple[str, Dict[str, str]]]:
        """(row_id, {column: value}) in file order, values verbatim as strings."""
        out: List[Tuple[str, Dict[str, str]]] = []
        for ln in self.rows:
            assert ln.values is not None and ln.row_id is not None
            out.append((ln.row_id, dict(zip(self.header, ln.values))))
        return out


def parse_csv_line(line: str) -> List[str]:
    """One CSV line → fields, with Python's csv dialect (RFC4180-ish).

    Line-at-a-time is safe for these seven files and asserted below: none of them
    contains a field with an embedded newline, so a physical line is always
    exactly one record. `read_csv` raises if that ever stops being true, rather
    than silently mangling the file.
    """
    return next(csv.reader([line]))


def write_csv_line(values: List[str]) -> str:
    """Fields → one CSV line, QUOTE_MINIMAL, no terminator (SPEC §C)."""
    buf = io.StringIO()
    csv.writer(buf, lineterminator="", quoting=csv.QUOTE_MINIMAL).writerow(values)
    return buf.getvalue()


def read_csv(catalog: Catalog, repo_root: str = REPO_ROOT) -> CsvFile:
    """Read a catalog's repo CSV into rows + preserved line layout."""
    path = catalog.abs_path(repo_root)
    with open(path, "rb") as fh:
        raw = fh.read().decode("utf-8")

    crlf = "\r\n" in raw
    text = raw.replace("\r\n", "\n")

    # A trailing newline terminates the last line; it is not an empty 8th row.
    had_trailing_newline = text.endswith("\n")
    if had_trailing_newline:
        text = text[:-1]
    if not had_trailing_newline:
        raise ValueError(f"{catalog.csv_path}: no trailing newline; refusing to guess")

    lines: List[Line] = []
    header: Optional[List[str]] = None
    id_index = -1
    seen_ids: Dict[str, int] = {}

    for lineno, raw_line in enumerate(text.split("\n"), start=1):
        if raw_line.strip() == "":
            lines.append(Line("blank", raw_line))
            continue
        if raw_line.lstrip().startswith(COMMENT_PREFIX):
            lines.append(Line("comment", raw_line))
            continue

        if raw_line.count('"') % 2 != 0:
            raise ValueError(
                f"{catalog.csv_path}:{lineno}: unbalanced quotes — this file has a field "
                "spanning multiple physical lines, which these scripts do not support. "
                "Fix the mapping before seeding; a lossy seed poisons every later phase."
            )

        values = parse_csv_line(raw_line)

        if header is None:
            header = values
            if catalog.id_column not in header:
                raise ValueError(
                    f"{catalog.csv_path}: id column {catalog.id_column!r} not in header {header}"
                )
            id_index = header.index(catalog.id_column)
            lines.append(Line("header", raw_line, values))
            continue

        if len(values) != len(header):
            raise ValueError(
                f"{catalog.csv_path}:{lineno}: {len(values)} columns, header has {len(header)}"
            )

        row_id = values[id_index].strip()
        if not row_id:
            raise ValueError(f"{catalog.csv_path}:{lineno}: empty {catalog.id_column}")
        if row_id in seen_ids:
            raise ValueError(
                f"{catalog.csv_path}:{lineno}: duplicate {catalog.id_column} {row_id!r} "
                f"(first seen on line {seen_ids[row_id]})"
            )
        seen_ids[row_id] = lineno

        lines.append(Line("row", raw_line, values, row_id))

    if header is None:
        raise ValueError(f"{catalog.csv_path}: no header line found")

    return CsvFile(catalog, header, lines, crlf)


def read_all(repo_root: str = REPO_ROOT) -> Dict[str, CsvFile]:
    return {c.name: read_csv(c, repo_root) for c in CATALOGS}


if __name__ == "__main__":  # `python3 Tools/content/catalogs.py` — re-verify the facts above
    for cat in CATALOGS:
        f = read_csv(cat)
        comments = sum(1 for ln in f.lines if ln.kind == "comment")
        print(
            f"{cat.name:<13} {len(f.rows):>4} rows  {len(f.header):>2} cols  "
            f"{comments} comment line(s)  {'CRLF' if f.crlf else 'LF'}  {cat.csv_path}"
        )
