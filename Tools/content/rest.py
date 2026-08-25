"""Minimal PostgREST client for the content pipeline scripts.

Stdlib only (urllib) — these scripts run on a build machine and in CI, and
adding a dependency for six HTTP calls is not worth the install step.

CREDENTIALS COME FROM THE ENVIRONMENT AND ARE NEVER COMMITTED (SPEC §C):

    SUPABASE_URL                 https://<ref>.supabase.co
    SUPABASE_SERVICE_ROLE_KEY    service_role key  (SUPABASE_SERVICE_KEY also accepted,
                                 which is the name the FastAPI backend's config.py uses)

`--env-file` on the CLI scripts sources a dotenv-style file instead; the
dashboard's gitignored `Tools/admin-dashboard/.env.development.local` already
holds both, which is why that is the documented convenience path in README.md.

The service key bypasses RLS. `content_*` has RLS on with ZERO policies, so
service_role is the ONLY way in — that is the intended posture (SPEC §A1.3),
not a shortcut around one.
"""

from __future__ import annotations

import json
import os
import ssl
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Dict, List, Optional

PAGE = 1000  # PostgREST's default max-rows; paginate rather than assume it is enough


def _ssl_context() -> ssl.SSLContext:
    """TLS context with a CA bundle that actually exists on this machine.

    python.org's macOS framework build ships no system CA bundle, so a bare
    urlopen against Supabase fails with CERTIFICATE_VERIFY_FAILED. certifi is
    installed here and is the same bundle requests would use. Verification is
    never disabled — a service key must not travel over an unverified socket.
    """
    try:
        import certifi

        return ssl.create_default_context(cafile=certifi.where())
    except ImportError:
        return ssl.create_default_context()


_SSL = _ssl_context()


class PostgrestError(RuntimeError):
    pass


def load_env_file(path: str) -> Dict[str, str]:
    """Parse a dotenv-style file. `KEY=value`, `#` comments, no interpolation."""
    out: Dict[str, str] = {}
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            out[key.strip()] = value.strip().strip('"').strip("'")
    return out


class PostgrestClient:
    def __init__(self, url: str, service_key: str):
        self.base = url.rstrip("/") + "/rest/v1"
        self.key = service_key

    @classmethod
    def from_env(cls, env_file: Optional[str] = None) -> "PostgrestClient":
        env = dict(os.environ)
        if env_file:
            env.update(load_env_file(env_file))

        url = env.get("SUPABASE_URL") or env.get("NEXT_PUBLIC_SUPABASE_URL") or ""
        key = env.get("SUPABASE_SERVICE_ROLE_KEY") or env.get("SUPABASE_SERVICE_KEY") or ""
        if not url or not key:
            raise PostgrestError(
                "SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY must be set (or pass --env-file). "
                "See Tools/content/README.md."
            )
        return cls(url, key)

    # -- plumbing ----------------------------------------------------------

    def _request(
        self,
        method: str,
        path: str,
        params: Optional[Dict[str, Any]] = None,
        body: Any = None,
        prefer: Optional[str] = None,
    ) -> Any:
        qs = urllib.parse.urlencode(params or {}, safe="*,().:")
        url = f"{self.base}/{path}" + (f"?{qs}" if qs else "")
        data = json.dumps(body, ensure_ascii=False).encode("utf-8") if body is not None else None

        headers = {
            "apikey": self.key,
            "Authorization": f"Bearer {self.key}",
            "Accept": "application/json",
        }
        if data is not None:
            headers["Content-Type"] = "application/json"
        if prefer:
            headers["Prefer"] = prefer

        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=120, context=_SSL) as resp:
                raw = resp.read().decode("utf-8")
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", "replace")
            raise PostgrestError(f"{method} {path} -> HTTP {exc.code}: {detail}") from None
        return json.loads(raw) if raw.strip() else None

    # -- verbs -------------------------------------------------------------

    def select(self, table: str, params: Dict[str, Any]) -> List[dict]:
        """SELECT, paginated. `params` is PostgREST query syntax verbatim."""
        rows: List[dict] = []
        offset = 0
        while True:
            page = dict(params)
            page["limit"] = PAGE
            page["offset"] = offset
            got = self._request("GET", table, page) or []
            rows.extend(got)
            if len(got) < PAGE:
                return rows
            offset += PAGE

    def insert_ignore_duplicates(self, table: str, rows: List[dict]) -> None:
        """INSERT ... ON CONFLICT DO NOTHING."""
        if not rows:
            return
        self._request(
            "POST", table, body=rows, prefer="resolution=ignore-duplicates,return=minimal"
        )

    def upsert(self, table: str, rows: List[dict], on_conflict: str) -> None:
        """INSERT ... ON CONFLICT (cols) DO UPDATE."""
        if not rows:
            return
        self._request(
            "POST",
            table,
            params={"on_conflict": on_conflict},
            body=rows,
            prefer="resolution=merge-duplicates,return=minimal",
        )

    def patch(self, table: str, filters: Dict[str, Any], values: Dict[str, Any]) -> None:
        self._request("PATCH", table, params=filters, body=values, prefer="return=minimal")

    def rpc(self, fn: str, args: Dict[str, Any]) -> Any:
        return self._request("POST", f"rpc/{fn}", body=args)
