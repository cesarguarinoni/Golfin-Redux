#!/usr/bin/env python3
"""Exporter — the PUBLISHED Supabase catalogs → the seven repo CSVs.

    python3 Tools/content/export_content.py --env-file <dotenv>   # rewrite in place
    python3 Tools/content/export_content.py --check               # exit 1 if anything would change OR drifted
    python3 Tools/content/export_content.py --catalogs texts,clubs

`--check` answers TWO questions, and the second one was added by
content_cursor_per_catalog §5:

  1. IDEMPOTENCE — would exporting change a file? (the repo is behind a publish)
  2. DRIFT — does each catalog hold exactly the ids the repo CSV holds?

Question 2 is not implied by question 1. A row that is in the CSV but NOT in the
catalog changes no file: the exporter keeps it verbatim (I6 — nothing is ever
deleted, so a missing row means the CATALOG is incomplete) and only warns. So
the case that motivated this check — a CSV that gained a row from in-flight work
while the catalog stayed behind — passed `--check` cleanly while the admin
dashboard was quietly serving a catalog the repo disagreed with. That is exactly
the drift the catalog exists to prevent, so it is now a non-zero exit that names
the offending ids.

Spec: Docs/Specs/Active/content_catalog/SPEC.md §C.
Plan: Docs/CONTENT_PIPELINE_PLAN.md §2 I1/I3/I6, §3 ("run it before every release build").

This is invariant I3 made real: the admin is UPSTREAM of the CSV. Publishing in
the dashboard writes Supabase; this puts those edits back into the repo so the
next build ships them as its bundled floor (I1) and the delta the client has to
download stays small (I2).

Writes:
  Assets/Resources/Data/Clubs.csv          (3 leading `#` lines preserved verbatim)
  Assets/Data/{Characters,Items,Bags,Balls}.csv
  Assets/Resources/Data/shop_catalog.csv
  Assets/Localization/LocalizationText.csv (1 MID-FILE `#` line preserved in place)
  Assets/Resources/Data/content_version.txt   <catalog>=<version>, one per line — NEW

──────────────────────────────────────────────────────────────────────────────
WHY THIS REWRITES THE EXISTING FILE INSTEAD OF REGENERATING ONE

Two things a content catalog does not carry, and the repo CSV does:

  1. ROW ORDER. `content_rows` has no sort column (deliberately — §3). Clubs.csv
     is "7 shipped rows, then 792 generated"; LocalizationText.csv is grouped by
     screen. Emitting sorted-by-id would reorder 1332 lines on the first run and
     make every later diff unreviewable.
  2. LINE LAYOUT. Comment lines, and the exact quoting an author used.

So: the repo file is the authority on ORDER and LAYOUT, Supabase is the
authority on VALUES. Each existing data line is rewritten in place from its
catalog row; comment and blank lines pass through untouched; rows present in the
catalog but absent from the file are appended, sorted by row_id, at the end.

A line whose values are UNCHANGED is emitted BYTE-FOR-BYTE as it was. That is
not cosmetic. Items.csv, Balls.csv and LocalizationText.csv quote several fields
that contain no comma, which `csv.QUOTE_MINIMAL` would not quote — re-emitting
every line would produce a ~40-line phantom diff on the very first export and
break the §A3 round-trip test that is Stage A's acceptance. Only a line whose
values actually changed is re-quoted, with QUOTE_MINIMAL as §C specifies.

A row in the file but NOT in the catalog is KEPT and warned about. I6 says
nothing is ever deleted, so that combination means the catalog is incomplete,
and dropping player-visible content on the strength of a partial fetch is the
one failure mode this script must not have.

`is_active` (SPEC §C): deactivated rows are still exported, carrying the flag in
an `is_active` column appended at the END of the header — the only safe position
under I4. The column appears ONLY when at least one exported row is inactive;
adding it unconditionally would put a phantom column on all seven CSVs on day
one and, again, break the round-trip test.

LINE ENDINGS: `\\n` and a trailing newline, per §C. Six of the seven files are
already LF. `Assets/Data/Bags.csv` is CRLF today, so its first export is a
whole-file line-ending normalisation — expected, one-time, and reported.
"""

from __future__ import annotations

import argparse
import os
import sys
from typing import Dict, List, Optional, Tuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from catalogs import CATALOGS, CATALOGS_BY_NAME, REPO_ROOT, Catalog, read_csv, write_csv_line  # noqa: E402
from rest import PostgrestClient  # noqa: E402

VERSION_FILE = "Assets/Resources/Data/content_version.txt"
IS_ACTIVE_COLUMN = "is_active"


class ExportError(RuntimeError):
    pass


# ---------------------------------------------------------------------------
# Fetch
# ---------------------------------------------------------------------------


def fetch_catalog(client: PostgrestClient, name: str) -> List[dict]:
    """Every PUBLISHED row of one catalog. Drafts are never read here."""
    return client.select(
        "content_rows",
        {"catalog": f"eq.{name}", "select": "row_id,data,is_active,min_build", "order": "row_id"},
    )


def fetch_versions(client: PostgrestClient) -> Dict[str, int]:
    rows = client.select("content_catalogs", {"select": "name,published_version", "order": "name"})
    return {r["name"]: int(r["published_version"]) for r in rows}


# ---------------------------------------------------------------------------
# Render
# ---------------------------------------------------------------------------


def render_csv(catalog: Catalog, published: List[dict], repo_root: str) -> Tuple[str, List[str]]:
    """(new file text, warnings). See the module docstring for the strategy."""
    existing = read_csv(catalog, repo_root)
    warnings: List[str] = []

    by_id: Dict[str, dict] = {}
    for row in published:
        rid = str(row["row_id"])
        if rid in by_id:
            raise ExportError(f"{catalog.name}: duplicate row_id {rid!r} in content_rows")
        by_id[rid] = row

    any_inactive = any(r.get("is_active") is False for r in published)
    header = list(existing.header)
    if any_inactive and IS_ACTIVE_COLUMN not in header:
        header.append(IS_ACTIVE_COLUMN)
    header_changed = header != existing.header

    unknown_columns: set = set()

    def row_line(rid: str, row: dict, original: Optional[List[str]], original_raw: Optional[str]) -> str:
        data = row.get("data") or {}
        if not isinstance(data, dict):
            raise ExportError(f"{catalog.name}/{rid}: data is {type(data).__name__}, expected object")
        unknown_columns.update(k for k in data if k not in header)

        values: List[str] = []
        for col in header:
            if col == IS_ACTIVE_COLUMN and col not in existing.header:
                values.append("true" if row.get("is_active") is not False else "false")
            else:
                values.append("" if data.get(col) is None else str(data[col]))

        # Unchanged and no new column ⇒ hand back the author's own bytes.
        if original is not None and original_raw is not None and not header_changed and values == original:
            return original_raw
        return write_csv_line(values)

    out: List[str] = []
    seen: set = set()
    for line in existing.lines:
        if line.kind in ("comment", "blank"):
            out.append(line.raw)
            continue
        if line.kind == "header":
            out.append(write_csv_line(header) if header_changed else line.raw)
            continue

        rid = line.row_id or ""
        seen.add(rid)
        row = by_id.get(rid)
        if row is None:
            warnings.append(
                f"{catalog.name}: {rid!r} is in {catalog.csv_path} but NOT in content_rows — "
                "line kept verbatim. Nothing is ever deleted (I6), so this means the catalog "
                "is incomplete; seed it before trusting this export."
            )
            out.append(line.raw)
            continue
        out.append(row_line(rid, row, line.values, line.raw))

    appended = sorted(rid for rid in by_id if rid not in seen)
    for rid in appended:
        out.append(row_line(rid, by_id[rid], None, None))
    if appended:
        warnings.append(f"{catalog.name}: appended {len(appended)} new row(s): {', '.join(appended[:5])}"
                        + (" …" if len(appended) > 5 else ""))

    if unknown_columns:
        warnings.append(
            f"{catalog.name}: content_rows carries column(s) {sorted(unknown_columns)} that the "
            f"repo header does not have — NOT exported. Columns are added by editing the CSV "
            f"header first (I4: additive-only, client parses by name)."
        )

    return "\n".join(out) + "\n", warnings


def drift_report(catalog: Catalog, published: List[dict], repo_root: str) -> List[str]:
    """Id-set drift between one catalog and its repo CSV. [] means in sync.

    (content_cursor_per_catalog §5.) Counts alone are not enough — two files can
    both hold 501 rows and disagree about which 501 — so this compares the ID
    SETS and names what is missing on each side.

    The two directions mean different things and both are wrong:

      IN THE CSV, NOT IN THE CATALOG — the catalog is behind the repo. This is
        the one `--check` used to miss entirely, because the exporter keeps such
        a line verbatim (I6) and changes no bytes. It means the dashboard is
        editing a catalog that does not know about content the game already
        ships, and a publish will not put it back.
      IN THE CATALOG, NOT IN THE CSV — the repo is behind the catalog. An export
        would append the row, so `--check` already catches this via the file
        diff; it is reported here too so one message explains the whole picture.

    Rows are compared by id only, not by value: a value difference is already a
    file difference, which `write_if_changed` reports.
    """
    csv_ids = {ln.row_id for ln in read_csv(catalog, repo_root).rows if ln.row_id}
    catalog_ids = {str(r["row_id"]) for r in published}

    missing = sorted(csv_ids - catalog_ids)   # in the CSV, absent from the catalog
    extra = sorted(catalog_ids - csv_ids)     # in the catalog, absent from the CSV
    if not missing and not extra:
        return []

    out = [
        f"{catalog.name}: DRIFT — {len(csv_ids)} row(s) in {catalog.csv_path} vs "
        f"{len(catalog_ids)} in the catalog."
    ]
    if missing:
        out.append(
            f"  {len(missing)} id(s) in the CSV but NOT in the catalog "
            f"(the catalog is behind the repo; re-seed it): {_sample(missing)}"
        )
    if extra:
        out.append(
            f"  {len(extra)} id(s) in the catalog but NOT in the CSV "
            f"(the repo is behind the catalog; run this exporter without --check): {_sample(extra)}"
        )
    return out


def _sample(ids: List[str], limit: int = 12) -> str:
    """Name the ids — an unnamed count is not actionable."""
    head = ", ".join(ids[:limit])
    return head + (f" … (+{len(ids) - limit} more)" if len(ids) > limit else "")


def render_version_file(versions: Dict[str, int], names: List[str]) -> str:
    return "".join(f"{n}={versions.get(n, 0)}\n" for n in sorted(names))


# ---------------------------------------------------------------------------
# Drive
# ---------------------------------------------------------------------------


def write_if_changed(path: str, text: str, check_only: bool) -> bool:
    """True when the file differs from `text` (and was written unless --check)."""
    new = text.encode("utf-8")
    old = open(path, "rb").read() if os.path.exists(path) else None
    if old == new:
        return False
    if not check_only:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as fh:
            fh.write(new)
    return True


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--repo-root", default=REPO_ROOT)
    ap.add_argument("--env-file", default=None, help="dotenv file with SUPABASE_URL / SUPABASE_SERVICE_ROLE_KEY")
    ap.add_argument("--catalogs", default=None, help="comma-separated subset (default: all seven)")
    ap.add_argument("--check", action="store_true", help="write nothing; exit 1 if anything would change")
    args = ap.parse_args()

    names = [c.name for c in CATALOGS]
    if args.catalogs:
        names = [n.strip() for n in args.catalogs.split(",") if n.strip()]
        unknown = [n for n in names if n not in CATALOGS_BY_NAME]
        if unknown:
            raise SystemExit(f"unknown catalog(s): {unknown}. Known: {[c.name for c in CATALOGS]}")

    client = PostgrestClient.from_env(args.env_file)
    versions = fetch_versions(client)

    changed: List[str] = []
    warnings: List[str] = []
    drift: List[str] = []

    for name in names:
        catalog = CATALOGS_BY_NAME[name]
        published = fetch_catalog(client, name)
        if not published:
            warnings.append(f"{name}: content_rows is EMPTY — {catalog.csv_path} left untouched.")
            drift.append(f"{name}: DRIFT — content_rows is EMPTY but {catalog.csv_path} has rows.")
            print(f"  {name:<13} SKIP (catalog empty)")
            continue

        drift.extend(drift_report(catalog, published, args.repo_root))

        text, warn = render_csv(catalog, published, args.repo_root)
        warnings.extend(warn)
        path = os.path.join(args.repo_root, catalog.csv_path)
        did = write_if_changed(path, text, args.check)
        if did:
            changed.append(catalog.csv_path)
        print(f"  {name:<13} v{versions.get(name, 0):<4} {len(published):>4} rows  "
              f"{'CHANGED' if did else 'unchanged'}  {catalog.csv_path}")

    # content_version.txt tracks every catalog the run touched, so a partial run
    # never silently rewrites the whole file with stale numbers for the rest.
    vpath = os.path.join(args.repo_root, VERSION_FILE)
    existing_versions: Dict[str, int] = {}
    if os.path.exists(vpath):
        for line in open(vpath, encoding="utf-8"):
            if "=" in line:
                k, v = line.strip().split("=", 1)
                existing_versions[k] = int(v)
    merged = {**existing_versions, **{n: versions.get(n, 0) for n in names}}
    if write_if_changed(vpath, render_version_file(merged, list(merged)), args.check):
        changed.append(VERSION_FILE)
    print(f"  {'version file':<13}      {len(merged):>4} lines {'CHANGED' if VERSION_FILE in changed else 'unchanged'}  {VERSION_FILE}")

    for w in warnings:
        print(f"WARNING: {w}", file=sys.stderr)

    if drift:
        print("\nCSV-vs-catalog DRIFT:", file=sys.stderr)
        for d in drift:
            print(f"  {d}" if d.startswith("  ") else f"  {d}", file=sys.stderr)

    if args.check:
        # Two independent failure modes, reported separately so the message says
        # which one it is. Drift is checked even on a partial --catalogs run,
        # scoped to the catalogs that ran.
        if changed:
            print(f"\n--check: {len(changed)} file(s) would change:", file=sys.stderr)
            for c in changed:
                print(f"  {c}", file=sys.stderr)
        if changed or drift:
            reasons = []
            if changed:
                reasons.append(f"{len(changed)} stale file(s)")
            if drift:
                reasons.append("CSV-vs-catalog drift")
            print(f"\n--check: FAILED — {' and '.join(reasons)}.", file=sys.stderr)
            return 1
        print("\n--check: clean — no file would change and no catalog has drifted.")
        return 0

    if drift:
        # A plain export cannot repair drift in the CSV→catalog direction (that
        # needs a re-seed), so it must not exit 0 and look successful.
        print("\nexport wrote its files, but the drift above is UNRESOLVED.", file=sys.stderr)
        return 1

    print(f"\n{len(changed)} file(s) written." if changed else "\nno changes.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
