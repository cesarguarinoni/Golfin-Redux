#!/usr/bin/env python3
"""Exporter — the PUBLISHED Supabase catalogs → the seven repo CSVs.

    python3 Tools/content/export_content.py --env-file <dotenv>   # rewrite in place
    python3 Tools/content/export_content.py --check               # exit 1 if anything would change
    python3 Tools/content/export_content.py --catalogs texts,clubs

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

    for name in names:
        catalog = CATALOGS_BY_NAME[name]
        published = fetch_catalog(client, name)
        if not published:
            warnings.append(f"{name}: content_rows is EMPTY — {catalog.csv_path} left untouched.")
            print(f"  {name:<13} SKIP (catalog empty)")
            continue

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

    if args.check:
        if changed:
            print(f"\n--check: {len(changed)} file(s) would change:", file=sys.stderr)
            for c in changed:
                print(f"  {c}", file=sys.stderr)
            return 1
        print("\n--check: clean, nothing would change.")
        return 0

    print(f"\n{len(changed)} file(s) written." if changed else "\nno changes.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
