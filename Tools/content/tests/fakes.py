"""A PostgREST client that lives in a dict — shared by the import and export tests.

WHY A FAKE AND NOT A LIVE DATABASE. These scripts decide what reaches players.
The cases worth pinning are the ones that are hard to produce on purpose against
prod — a draft somebody is mid-editing, a catalog row with no CSV line, a value
that differs on exactly one field — and reproducing those live means writing to
the real `content_drafts`. The plan/apply split is pure logic over two row sets,
so the honest test substitutes the row sets.

It implements the four verbs `import_content` and `export_content` actually call
(`select`, `upsert`, `insert_ignore_duplicates`, and `patch` for completeness),
with the PostgREST filter syntax they actually use (`col=eq.value`). Anything
else raises rather than silently returning `[]` — a fake that answers a query it
does not understand would make a test pass for the wrong reason.
"""

from __future__ import annotations

import copy
from typing import Any, Dict, List, Optional


class FakePostgrestClient:
    """`{table: [row, ...]}`, queried the way the real client is queried."""

    def __init__(self, tables: Optional[Dict[str, List[dict]]] = None):
        self.tables: Dict[str, List[dict]] = copy.deepcopy(tables or {})
        # Every write, in order, so a test can assert that a REFUSED run wrote nothing.
        self.writes: List[tuple] = []

    # -- helpers for tests ---------------------------------------------------

    def rows(self, table: str) -> List[dict]:
        return self.tables.setdefault(table, [])

    def seed(self, table: str, rows: List[dict]) -> None:
        self.tables[table] = copy.deepcopy(rows)

    def find(self, table: str, catalog: str, row_id: str) -> Optional[dict]:
        for row in self.tables.get(table, []):
            if row.get("catalog") == catalog and str(row.get("row_id")) == row_id:
                return row
        return None

    def publish(self, catalog: str) -> int:
        """What the admin's publish does, reduced to what these tests need: every
        draft of one catalog becomes the published row. Returns the row count."""
        drafts = [r for r in self.tables.get("content_drafts", []) if r.get("catalog") == catalog]
        keep = [r for r in self.tables.get("content_rows", []) if r.get("catalog") != catalog]
        self.tables["content_rows"] = keep + copy.deepcopy(drafts)
        for row in self.tables.get("content_catalogs", []):
            if row.get("name") == catalog:
                row["published_version"] = int(row.get("published_version", 0)) + 1
        return len(drafts)

    # -- the verbs -----------------------------------------------------------

    def select(self, table: str, params: Dict[str, Any]) -> List[dict]:
        rows = self.tables.get(table, [])
        for key, value in params.items():
            if key in ("select", "order", "limit", "offset"):
                continue
            if not isinstance(value, str) or not value.startswith("eq."):
                raise AssertionError(f"FakePostgrestClient: unsupported filter {key}={value!r}")
            wanted = value[3:]
            rows = [r for r in rows if str(r.get(key)) == wanted]
        return copy.deepcopy(sorted(rows, key=lambda r: str(r.get("row_id", ""))))

    def upsert(self, table: str, rows: List[dict], on_conflict: str) -> None:
        if not rows:
            return
        self.writes.append(("upsert", table, copy.deepcopy(rows)))
        keys = [k.strip() for k in on_conflict.split(",")]
        existing = self.tables.setdefault(table, [])
        for new in rows:
            for i, old in enumerate(existing):
                if all(str(old.get(k)) == str(new.get(k)) for k in keys):
                    existing[i] = {**old, **copy.deepcopy(new)}
                    break
            else:
                existing.append(copy.deepcopy(new))

    def insert_ignore_duplicates(self, table: str, rows: List[dict]) -> None:
        if not rows:
            return
        self.writes.append(("insert", table, copy.deepcopy(rows)))
        self.tables.setdefault(table, []).extend(copy.deepcopy(rows))

    def patch(self, table: str, filters: Dict[str, Any], values: Dict[str, Any]) -> None:
        self.writes.append(("patch", table, {"filters": dict(filters), "values": dict(values)}))
        for row in self.select(table, filters):
            for real in self.tables.get(table, []):
                if all(str(real.get(k)) == str(row.get(k)) for k in ("catalog", "row_id")):
                    real.update(values)


def published_row(catalog: str, row_id: str, data: dict, *, min_build: int = 0,
                  is_active: bool = True) -> dict:
    """One `content_rows` / `content_drafts` row in the shape both scripts read."""
    return {
        "catalog": catalog,
        "row_id": row_id,
        "data": dict(data),
        "min_build": min_build,
        "is_active": is_active,
    }
