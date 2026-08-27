#!/usr/bin/env python3
"""Importer — the repo CSVs → `content_drafts`, as a PROPOSAL.

    python3 Tools/content/import_content.py --env-file <dotenv>            # PLAN only (default)
    python3 Tools/content/import_content.py --env-file <dotenv> --apply    # write the drafts
    python3 Tools/content/import_content.py --catalogs texts --apply
    python3 Tools/content/import_content.py --min-build 2400 --apply       # for rows being ADDED

The other half of `export_content.py`, and the reason it exists:

    export:  content_rows  ──►  repo CSV        (a publish reaches the next build)
    import:  repo CSV      ──►  content_drafts  (a CSV edit reaches the admin)

PUBLISHED SUPABASE REMAINS THE SINGLE TRUTH. This never writes `content_rows` —
only `content_drafts`, which are never served to the game. A CSV edit becomes a
DRAFT the publish drawer shows as a diff, and Cesar publishes it or does not.
That is the whole design: an edit made in Unity is a *proposal*, not a fact.

──────────────────────────────────────────────────────────────────────────────
WHY THIS EXISTS (shop_stocking §7, and the incident that made it urgent)

Drift between a repo CSV and its catalog has two directions, and until now only
one of them could be repaired:

  * REPO BEHIND CATALOG — a row was published and never exported. `export_content.py`
    fixes it. Self-healing.
  * CSV AHEAD OF CATALOG — a row was added in Unity and never created in the
    admin. NOTHING could fix it. The exporter never deletes (I6), so it keeps the
    extra line verbatim, reports "unchanged", and the drift persists forever.

On 2026-08-27 the second kind surfaced the worst possible way: five
`SETTINGS_QUALITY_*` / `SETTINGS_GRAPHICS` keys had been sitting in
LocalizationText.csv since `quality_tiers`, absent from the `texts` catalog, and
the FIRST run of the new release-lane content gate found them — at archive time,
when the only thing anybody wanted was a build.

This script turns that failure mode into a normal step. `--check` on the exporter
stays the backstop; this is the thing that makes the backstop actionable.

──────────────────────────────────────────────────────────────────────────────
WHAT IT PROPOSES, AND WHAT IT REFUSES TO TOUCH

For each row in the repo CSV:

  ADD       in neither `content_rows` nor `content_drafts`  -> insert a draft
  CHANGE    published, and the CSV values differ            -> update the draft
  same      published, and the CSV values match             -> nothing

Never:

  * `content_rows`. Publish is the only way in. (§D1)
  * DELETIONS. A row in the catalog but absent from the CSV is REPORTED and left
    alone — I6, nothing is ever deleted, and a partial CSV must never be able to
    drop player-visible content. Deactivating in the admin is the delete.
  * AN IN-FLIGHT ADMIN EDIT. If a draft already differs from published, somebody
    is mid-edit in the dashboard; overwriting that from a CSV would silently
    destroy their work. Those rows are reported as CONFLICTS and skipped, and
    `--apply` REFUSES the whole run unless `--overwrite-dirty` says otherwise.
    Refusing the run and not just the row is deliberate: a half-applied import is
    a state nobody can reason about.

──────────────────────────────────────────────────────────────────────────────
min_build ON A ROW BEING ADDED — WHY IT DEFAULTS HIGH

A row that is new to the catalog needs a `min_build`, and the CSV does not carry
one. Getting it wrong in the LOW direction ships a row to builds that cannot
render it: an older client has neither the row in its bundled CSV nor the sprites
the row names, which is precisely the blank-card class of bug `shop_stocking` §6
exists to prevent.

Getting it wrong in the HIGH direction is benign. A build that already bundles
the row renders it from its own floor (I1) and simply receives no overlay for it.

So the default is `git rev-list --count HEAD` + 1 — the first build number that
can possibly contain the commit this CSV row is in, derived the same way
`BuildStampGenerator` derives a build number. Override with `--min-build`.
CHANGED rows never have their `min_build` touched: §D1.7 makes it immutable once
published, and the validator errors if a publish tries to move it.
"""

from __future__ import annotations

import argparse
import datetime
import os
import subprocess
import sys
from typing import Dict, List, Optional, Tuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from catalogs import CATALOGS, CATALOGS_BY_NAME, REPO_ROOT, Catalog, read_csv  # noqa: E402
from rest import PostgrestClient  # noqa: E402

IS_ACTIVE_COLUMN = "is_active"
CHUNK = 500  # rows per PostgREST write; keeps one request from carrying 1.3 MB


class ImportError_(RuntimeError):
    """Named with a trailing underscore so it cannot shadow the builtin."""


# ---------------------------------------------------------------------------
# Reading both sides
# ---------------------------------------------------------------------------


def csv_rows(catalog: Catalog, repo_root: str) -> Dict[str, Tuple[Dict[str, str], Optional[bool]]]:
    """`{row_id: (data, is_active_or_None)}` from the repo CSV.

    `is_active` is a COLUMN of the table, not a field of `data` — the exporter
    appends it to the header only when some row is inactive. So it is split back
    out here; leaving it inside `data` would put a phantom field in the catalog
    that the game's parsers would then have to ignore.
    """
    parsed = read_csv(catalog, repo_root)
    out: Dict[str, Tuple[Dict[str, str], Optional[bool]]] = {}
    for line in parsed.rows:
        data = dict(zip(parsed.header, line.values))
        flag: Optional[bool] = None
        if IS_ACTIVE_COLUMN in data:
            flag = str(data.pop(IS_ACTIVE_COLUMN)).strip().lower() != "false"
        rid = str(line.row_id)
        if rid in out:
            raise ImportError_(f"{catalog.name}: duplicate {catalog.id_column} {rid!r} in {catalog.csv_path}")
        out[rid] = (data, flag)
    return out


def table_rows(client: PostgrestClient, table: str, name: str) -> Dict[str, dict]:
    rows = client.select(
        table,
        {"catalog": f"eq.{name}", "select": "row_id,data,min_build,is_active", "order": "row_id"},
    )
    return {str(r["row_id"]): r for r in rows}


# ---------------------------------------------------------------------------
# Planning
# ---------------------------------------------------------------------------


class Plan:
    def __init__(self, catalog: Catalog):
        self.catalog = catalog
        self.adds: List[dict] = []          # draft payloads
        self.changes: List[dict] = []       # draft payloads
        self.before: Dict[str, dict] = {}   # row_id -> prior draft (audit `before`)
        self.conflicts: List[str] = []      # row_ids someone is mid-editing
        self.overwritten: List[str] = []    # in-flight rows clobbered on purpose
        self.catalog_only: List[str] = []   # in the catalog, absent from the CSV
        self.unchanged = 0

    @property
    def writes(self) -> List[dict]:
        return self.adds + self.changes

    @property
    def touched(self) -> int:
        return len(self.adds) + len(self.changes)


def build_plan(
    catalog: Catalog,
    repo_root: str,
    published: Dict[str, dict],
    drafts: Dict[str, dict],
    min_build_for_adds: int,
    by: str,
    now: str,
    overwrite_dirty: bool = False,
) -> Plan:
    plan = Plan(catalog)
    csv = csv_rows(catalog, repo_root)

    for rid, (data, csv_active) in csv.items():
        pub = published.get(rid)
        draft = drafts.get(rid)

        # An in-flight admin edit: the draft already says something different
        # from what is published. Whatever the CSV says, this row is somebody's
        # unfinished work and is not ours to overwrite.
        draft_is_dirty = (
            draft is not None
            and pub is not None
            and (
                draft.get("data") != pub.get("data")
                or draft.get("min_build") != pub.get("min_build")
                or draft.get("is_active") != pub.get("is_active")
            )
        )
        # A draft-only row (created in the admin, never published) is in-flight
        # by definition, unless the CSV agrees with it exactly.
        draft_only_pending = draft is not None and pub is None

        if pub is None and draft is None:
            plan.adds.append(
                {
                    "catalog": catalog.name,
                    "row_id": rid,
                    "data": data,
                    "min_build": min_build_for_adds,
                    "is_active": True if csv_active is None else csv_active,
                    "updated_by": by,
                    "updated_at": now,
                }
            )
            continue

        if draft_only_pending:
            # A row created in the admin and not yet published. Its min_build was
            # chosen deliberately there, so an overwrite keeps it rather than
            # resetting it to this run's default.
            if draft.get("data") == data:
                plan.unchanged += 1
            elif not overwrite_dirty:
                plan.conflicts.append(f"{rid} (unpublished draft differs from the CSV)")
            else:
                plan.overwritten.append(rid)
                plan.before[rid] = draft
                plan.changes.append(
                    {
                        "catalog": catalog.name,
                        "row_id": rid,
                        "data": data,
                        "min_build": draft.get("min_build", min_build_for_adds),
                        "is_active": draft.get("is_active") is not False if csv_active is None else csv_active,
                        "updated_by": by,
                        "updated_at": now,
                    }
                )
            continue

        assert pub is not None
        pub_active = pub.get("is_active") is not False
        want_active = pub_active if csv_active is None else csv_active

        if pub.get("data") == data and pub_active == want_active and not draft_is_dirty:
            plan.unchanged += 1
            continue

        if draft_is_dirty:
            if not overwrite_dirty:
                plan.conflicts.append(f"{rid} (draft edited in the admin since the last publish)")
                continue
            # Deliberate clobber. Named in the output, because "the CSV won" is
            # something the person who made that admin edit has to be able to see.
            plan.overwritten.append(rid)

        plan.before[rid] = draft or {}
        plan.changes.append(
            {
                "catalog": catalog.name,
                "row_id": rid,
                "data": data,
                # IMMUTABLE once published (§D1.7) — carried over, never re-derived.
                "min_build": pub.get("min_build", 0),
                "is_active": want_active,
                "updated_by": by,
                "updated_at": now,
            }
        )

    # I6: reported, never touched.
    plan.catalog_only = sorted(set(published) - set(csv))
    return plan


# ---------------------------------------------------------------------------
# Applying
# ---------------------------------------------------------------------------


def apply_plan(client: PostgrestClient, plan: Plan, by: str) -> None:
    """Upsert the drafts, then the audit rows. Drafts first: an audit row for a
    write that did not happen is worse than a write with no audit row, and the
    audit insert is the one that can fail harmlessly."""
    rows = plan.writes
    for i in range(0, len(rows), CHUNK):
        client.upsert("content_drafts", rows[i : i + CHUNK], on_conflict="catalog,row_id")

    audit: List[dict] = []
    for row in plan.adds:
        audit.append(_audit(by, f"content.draft.create:{plan.catalog.name}", None, row))
    for row in plan.changes:
        audit.append(_audit(by, f"content.draft.update:{plan.catalog.name}", plan.before.get(row["row_id"]), row))
    for i in range(0, len(audit), CHUNK):
        client.insert_ignore_duplicates("admin_audit_log", audit[i : i + CHUNK])


def _audit(by: str, action: str, before: Optional[dict], row: dict) -> dict:
    """Same shape the dashboard's writeAudit() produces, plus `via` so the Audit
    panel can tell a script-driven import from somebody typing in the editor."""
    return {
        "admin_email": by,
        "action": action,
        "target_user": None,
        "table_name": "content_drafts",
        "before": before or None,
        "after": {
            "catalog": row["catalog"],
            "rowId": row["row_id"],
            "data": row["data"],
            "minBuild": row["min_build"],
            "isActive": row["is_active"],
            "via": "import_content.py",
        },
    }


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def default_min_build(repo_root: str) -> Optional[int]:
    """`git rev-list --count HEAD` + 1 — see the module docstring."""
    try:
        out = subprocess.run(
            ["git", "rev-list", "--count", "HEAD"],
            cwd=repo_root, capture_output=True, text=True, check=True,
        )
        return int(out.stdout.strip()) + 1
    except Exception:
        return None


def default_by(repo_root: str) -> str:
    try:
        out = subprocess.run(
            ["git", "config", "user.email"],
            cwd=repo_root, capture_output=True, text=True, check=True,
        )
        email = out.stdout.strip()
        if email:
            return email
    except Exception:
        pass
    return "import_content.py"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--repo-root", default=REPO_ROOT)
    ap.add_argument("--env-file", default=None, help="dotenv with SUPABASE_URL / SUPABASE_SERVICE_ROLE_KEY")
    ap.add_argument("--catalogs", default=None, help="comma-separated subset (default: all seven)")
    ap.add_argument("--apply", action="store_true", help="write the drafts (default: plan only)")
    ap.add_argument("--min-build", type=int, default=None, help="min_build for rows being ADDED")
    ap.add_argument("--by", default=None, help="audit attribution (default: git config user.email)")
    ap.add_argument(
        "--overwrite-dirty",
        action="store_true",
        help="overwrite drafts someone edited in the admin since the last publish",
    )
    args = ap.parse_args()

    names = [c.name for c in CATALOGS]
    if args.catalogs:
        names = [n.strip() for n in args.catalogs.split(",") if n.strip()]
        unknown = [n for n in names if n not in CATALOGS_BY_NAME]
        if unknown:
            raise SystemExit(f"unknown catalog(s): {unknown}. Known: {[c.name for c in CATALOGS]}")

    min_build = args.min_build if args.min_build is not None else default_min_build(args.repo_root)
    if min_build is None:
        raise SystemExit(
            "could not derive a default min_build (git rev-list failed). Pass --min-build N. "
            "See the module docstring for why it defaults high."
        )
    by = args.by or default_by(args.repo_root)
    now = datetime.datetime.now(datetime.timezone.utc).isoformat().replace("+00:00", "Z")

    client = PostgrestClient.from_env(args.env_file)

    print(f"{'catalog':<14} {'add':>4} {'change':>7} {'same':>6} {'conflict':>9}  csv")
    plans: List[Plan] = []
    for name in names:
        catalog = CATALOGS_BY_NAME[name]
        plan = build_plan(
            catalog,
            args.repo_root,
            table_rows(client, "content_rows", name),
            table_rows(client, "content_drafts", name),
            min_build,
            by,
            now,
            args.overwrite_dirty,
        )
        plans.append(plan)
        print(
            f"  {name:<12} {len(plan.adds):>4} {len(plan.changes):>7} {plan.unchanged:>6} "
            f"{len(plan.conflicts):>9}  {catalog.csv_path}"
        )

    # stdout is block-buffered when piped while stderr is not, so the table would
    # otherwise land AFTER the warnings that refer to it. This tool's output gets
    # read in lane logs, where that reads as nonsense.
    sys.stdout.flush()

    for plan in plans:
        for rid in plan.conflicts:
            print(f"CONFLICT: {plan.catalog.name}: {rid}", file=sys.stderr)
        for rid in plan.overwritten:
            print(
                f"OVERWRITING: {plan.catalog.name}: {rid} — an in-flight admin edit is being "
                "replaced by the CSV (--overwrite-dirty).",
                file=sys.stderr,
            )
        if plan.catalog_only:
            sample = ", ".join(plan.catalog_only[:5])
            more = f" (+{len(plan.catalog_only) - 5} more)" if len(plan.catalog_only) > 5 else ""
            print(
                f"NOTE: {plan.catalog.name}: {len(plan.catalog_only)} row(s) in the catalog but not in "
                f"{plan.catalog.csv_path} — LEFT ALONE (I6, nothing is ever deleted): {sample}{more}",
                file=sys.stderr,
            )

    conflicts = [c for p in plans for c in p.conflicts]
    total = sum(p.touched for p in plans)
    adds = sum(len(p.adds) for p in plans)

    if conflicts and not args.overwrite_dirty:
        sys.stderr.flush()
        print(
            f"\nREFUSED — {len(conflicts)} row(s) are being edited in the admin right now. "
            "Publish or revert them there, or re-run with --overwrite-dirty to overwrite them "
            "from the CSV. Nothing was written.",
            file=sys.stderr,
        )
        return 1

    if total == 0:
        print("\nNothing to import — every CSV row already matches the catalog.")
        return 0

    if not args.apply:
        print(
            f"\nPLAN ONLY — {total} draft(s) would be written "
            f"({adds} new, at min_build {min_build}). Nothing was written.\n"
            "Re-run with --apply to propose them; then open the panel and "
            "Review & publish to make them real."
        )
        return 0

    for plan in plans:
        if plan.touched:
            apply_plan(client, plan, by)

    print(
        f"\nWrote {total} draft(s) as {by}"
        + (f" ({adds} new, min_build {min_build})" if adds else "")
        + ".\nNOTHING IS LIVE YET: drafts are never served to the game. Open the affected "
        "panel(s) -> Review & publish to see the diff and publish."
    )
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except ImportError_ as exc:
        print(f"import_content: {exc}", file=sys.stderr)
        sys.exit(2)
