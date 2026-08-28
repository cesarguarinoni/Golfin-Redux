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
CATALOGS: Tuple[Catalog, ...] = (
    Catalog("clubs", "Assets/Resources/Data/Clubs.csv", "id"),
    Catalog("characters", "Assets/Data/Characters.csv", "id"),
    Catalog("items", "Assets/Data/Items.csv", "id"),
    Catalog("bags", "Assets/Data/Bags.csv", "id"),
    Catalog("balls", "Assets/Data/Balls.csv", "id"),
    Catalog("texts", "Assets/Localization/LocalizationText.csv", "key"),
    Catalog("shop_catalog", "Assets/Resources/Data/shop_catalog.csv", "entryId"),
    Catalog("level_up_costs", "Assets/Data/LevelUpCosts.csv", "level"),
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
