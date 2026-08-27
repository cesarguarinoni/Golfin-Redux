#!/usr/bin/env python3
"""PostToolUse hook: a bundled catalog CSV was just edited — say what is owed.

Reads the Claude Code hook payload as JSON on stdin. If the Write/Edit targeted
one of the seven CSVs that mirror a published content catalog, print what that
edit still owes and exit 2 — which, for a PostToolUse hook, surfaces the message
to the model WITHOUT undoing the write. This warns; it never blocks.

WHY THIS EXISTS (2026-08-27, found by shop_stocking §5's new lane gate).
`quality_tiers` added five keys — SETTINGS_GRAPHICS and four SETTINGS_QUALITY_* —
to Assets/Localization/LocalizationText.csv and never put them in the admin
`texts` catalog. Nothing noticed for weeks, because nothing ran
`export_content.py --check`. It finally surfaced at the worst possible moment:
the FIRST run of the new release-lane gate, i.e. at archive time, when the whole
point was to get a build out.

That direction of drift — CSV AHEAD of catalog — is the one the exporter cannot
fix. It never deletes (I6), so it keeps the extra lines verbatim and the drift
simply persists. The only remedy is to create the rows in the admin and publish
them, which is a human, out-of-band step that nothing was asking for.

So this asks for it, at the one moment somebody is definitely paying attention:
the edit itself.

The path list is IMPORTED from Tools/content/catalogs.py, never restated here.
That module is what the exporter, the seeder and this hook all agree on; a second
copy would be a second thing to forget.

Fires ONCE per (session, catalog) — a task that rewrites a CSV forty times should
be told once, not forty times.

Allow: exit 0 silently. Warn: exit 2 with the message on stderr.
"""
import hashlib
import json
import os
import sys
import tempfile

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def _catalog_for(path: str):
    """The Catalog whose CSV this path is, or None.

    Imports the shared registry rather than restating the seven paths. If that
    import fails (a partial checkout, a renamed module), the hook stays SILENT:
    a broken guard must not become a broken session.
    """
    try:
        sys.path.insert(0, os.path.join(REPO_ROOT, "Tools", "content"))
        from catalogs import CATALOGS  # type: ignore
    except Exception:
        return None

    try:
        rel = os.path.relpath(os.path.abspath(path), REPO_ROOT).replace(os.sep, "/")
    except Exception:
        return None

    for catalog in CATALOGS:
        if rel == catalog.csv_path:
            return catalog
    return None


def _already_warned(session_id: str, catalog_name: str) -> bool:
    """True when this session has already been told about this catalog.

    State lives in the system temp dir, keyed by session — never in the repo,
    which would show up as dirt in exactly the `git status` this project checks
    before every close-out commit.
    """
    key = hashlib.sha1(f"{session_id}:{catalog_name}".encode("utf-8")).hexdigest()[:16]
    marker = os.path.join(tempfile.gettempdir(), f"golfin_catalog_warn_{key}")
    if os.path.exists(marker):
        return True
    try:
        with open(marker, "w") as fh:
            fh.write(catalog_name)
    except Exception:
        pass  # Cannot dedupe -> warn again. Noisy beats silent.
    return False


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return 0

    tool_input = payload.get("tool_input") or {}
    path = tool_input.get("file_path") or tool_input.get("filePath") or ""
    if not path:
        return 0

    catalog = _catalog_for(path)
    if catalog is None:
        return 0

    if _already_warned(str(payload.get("session_id", "")), catalog.name):
        return 0

    print(
        f"CONTENT CATALOG CSV EDITED: {catalog.csv_path} mirrors the published "
        f"`{catalog.name}` catalog.",
        file=sys.stderr,
    )
    print("", file=sys.stderr)
    print(
        "If this edit ADDED or RENAMED a row, that row does not exist in the admin "
        "catalog yet, and the CSV is now AHEAD of it. `export_content.py --check` "
        "will fail and `fastlane ios testflight_build` will ABORT at archive time — "
        "the exporter cannot repair this direction, because it never deletes (I6) "
        "and simply keeps the extra line verbatim.",
        file=sys.stderr,
    )
    print("", file=sys.stderr)
    print(
        f"  What is owed: create each new {catalog.id_column} in the admin "
        f"({catalog.name} panel -> `+ New row`), publish, then re-run the exporter "
        "and commit its output.",
        file=sys.stderr,
    )
    print("", file=sys.stderr)
    print(
        "  Check it now:  python3 Tools/content/export_content.py "
        "--env-file Tools/admin-dashboard/.env.development.local --check",
        file=sys.stderr,
    )
    print("", file=sys.stderr)
    print(
        "Editing an EXISTING row's values is fine and needs none of this — publish "
        "order is the only thing that differs. This warning fires once per catalog "
        "per session and never blocks the write. Scar: SETTINGS_QUALITY_* / "
        "SETTINGS_GRAPHICS, added 2026-08 and still missing from `texts` weeks later.",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    sys.exit(main())
