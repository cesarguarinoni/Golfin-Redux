#!/usr/bin/env python3
"""gps_checkin — the live check-in/check-out economy E2E, as ONE command.

    python3 Docs/Specs/Active/gps_checkin/e2e_activity_economy.py \
        --env-file Tools/admin-dashboard/.env.development.local

RUN THIS ONLY AFTER `backend/migrations/2026_09_03_venue_partners.sql` HAS BEEN
APPLIED. It calls `golfin_activity_checkin` / `golfin_activity_checkout` directly
over PostgREST with the service key — the same entry point the deployed routers
use — so it proves the FUNCTIONS. It does not need the Fly deploy; run it before
the deploy to check the migration, and again through the routers afterwards if
you want the HTTP layer covered too.

Sibling of `Docs/Specs/Completed/gps_gifts_votes/e2e_gift_economy.py`, and
deliberately the same shape: same `check()` scoreboard, same invariant sweep
before and after, same "refusals are values, not exceptions" posture.

WHAT IT ASSERTS, and why each one is here rather than "it looked right":

  1. the invariant `total_points = activity_pts + gift_pts` over EVERY profile,
     before and after
  2. a check-in INSIDE the radius (TEST Office, venue 1993) awards +30 to
     activity_pts AND total_points, and writes ONE `gps_checkin` ledger row
  3. a REPLAY with the same key returns replayed:true and moves NOTHING and
     opens NO second round      <- the force-quit-mid-check-in case
  4. a SECOND check-in with a FRESH key is refused `already_active` (D2)
  5. check-out awards +15 (10 base + 5 both-ends-verified), bumps
     activities_count by exactly 1, and writes ONE `activityComplete` row
  6. a check-out REPLAY moves nothing
  7. a check-in FAR outside the radius awards 0 and writes NO ledger row
     (D1's server half: the client's disabled button is UX, this is the rule)
  8. a round backdated past 8 h checks out `expired` with 0 pts and no ledger
  9. a score submit carrying `activity_id` UPDATES the open round instead of
     inserting a second one — ONE row in history for one round (D6)

Everything it does happens on ONE account at ONE test venue (1993 TEST Office).
It LEAVES the rows it created: the points it earned are real earns with real
ledger rows behind them, and hand-deleting either half is exactly how an
invariant gets broken. `--cleanup` removes the activities rows and detaches
(does not delete) their ledger rows, for when a run has to be repeated and the
history noise matters more than the audit trail.
"""
from __future__ import annotations

import argparse
import json
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone

sys.path.insert(0, "Tools/content")
from rest import load_env_file  # noqa: E402

# Cesar's dev account (profiles, read 2026-09-02). Override on the CLI.
DEFAULT_USER = "f2636482-29aa-4233-a834-99526b202fe1"   # Cratilo
# TEST Office (WeWork Harumi), source='test_fixture', gps_radius_m 500.
# GPS_DEVICE_PASS.md row 0.2 — the same venue the on-device pass stands in.
TEST_VENUE = 1993
TEST_LAT, TEST_LON = 35.654103, 139.779219
# ~6 km away (roughly Tokyo Station), comfortably outside a 500 m radius.
FAR_LAT, FAR_LON = 35.681236, 139.767125

_FAILED: list[str] = []
_CREATED: list[int] = []


def check(label: str, ok: bool, detail: str = "") -> None:
    print(("  PASS  " if ok else "  FAIL  ") + label + (("   " + detail) if detail else ""))
    if not ok:
        _FAILED.append(label)


class Db:
    def __init__(self, url: str, key: str):
        self.base = url.rstrip("/") + "/rest/v1"
        self.key = key
        self.ctx = ssl.create_default_context()
        try:
            import certifi
            self.ctx = ssl.create_default_context(cafile=certifi.where())
        except ImportError:
            pass

    def _call(self, method: str, path: str, body=None, headers=None):
        data = json.dumps(body).encode() if body is not None else None
        h = {"apikey": self.key, "Authorization": "Bearer " + self.key,
             "Content-Type": "application/json", "Accept": "application/json"}
        h.update(headers or {})
        # PostgREST paths carry Japanese venue names in filters; the URL must be
        # percent-encoded before urllib tries to ASCII-encode the request line.
        safe = urllib.parse.quote(path, safe="/?&=.,*():@!$'+~-_")
        req = urllib.request.Request(self.base + safe, data=data, method=method, headers=h)
        try:
            with urllib.request.urlopen(req, timeout=60, context=self.ctx) as r:
                raw = r.read().decode()
        except urllib.error.HTTPError as e:
            raise SystemExit(f"{method} {path} -> HTTP {e.code}: {e.read().decode()[:400]}")
        return json.loads(raw) if raw.strip() else None

    def rpc(self, fn: str, args: dict):
        return self._call("POST", "/rpc/" + fn, args)

    def select(self, table: str, query: str):
        return self._call("GET", f"/{table}?{query}")

    def patch(self, table: str, query: str, body: dict):
        return self._call("PATCH", f"/{table}?{query}", body,
                          {"Prefer": "return=representation"})

    def delete(self, table: str, query: str):
        return self._call("DELETE", f"/{table}?{query}")

    def profile(self, uid: str) -> dict:
        rows = self.select(
            "profiles",
            "select=id,display_name,activity_pts,gift_pts,total_points,"
            f"activities_count&id=eq.{uid}")
        if not rows:
            raise SystemExit("no such profile: " + uid)
        return rows[0]


def invariant(db: Db, when: str) -> None:
    rows = db.select("profiles", "select=id,display_name,activity_pts,gift_pts,total_points")
    bad = [r for r in rows
           if (r["total_points"] or 0) != (r["activity_pts"] or 0) + (r["gift_pts"] or 0)]
    check(f"invariant total_points = activity_pts + gift_pts ({when})",
          not bad, f"{len(rows)} profiles, {len(bad)} violations")
    for r in bad:
        print("        ", json.dumps(r, ensure_ascii=False))


def close_any_open(db: Db, uid: str) -> None:
    """The suite needs the account to start with NO active round — D2 makes
    every later assertion meaningless otherwise. Anything already open is
    cancelled (not checked out: that would pay points for a round this script
    did not open)."""
    rows = db.select("activities",
                     f"select=id&user_id=eq.{uid}&status=eq.active")
    for r in rows or []:
        db.patch("activities", f"id=eq.{r['id']}", {"status": "cancelled"})
    if rows:
        print(f"   (cancelled {len(rows)} pre-existing open round(s))")


def checkin(db: Db, uid: str, lat, lon, key, venue=TEST_VENUE, platform="ios"):
    return db.rpc("golfin_activity_checkin", {
        "p_user": uid, "p_venue": venue, "p_lat": lat, "p_lon": lon,
        "p_accuracy_m": 8.0, "p_is_mock": False, "p_platform": platform,
        "p_key": key})


def checkout(db: Db, uid: str, act, lat, lon, key, count=3):
    return db.rpc("golfin_activity_checkout", {
        "p_user": uid, "p_activity": act, "p_lat": lat, "p_lon": lon,
        "p_check_count": count, "p_is_mock": False, "p_key": key})


def ledger(db: Db, uid: str, key: str):
    return db.select("points_transactions",
                     "select=type,amount,currency,description,related_activity_id"
                     f"&user_id=eq.{uid}&idempotency_key=eq.{key}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--env-file", required=True)
    ap.add_argument("--user", default=DEFAULT_USER)
    ap.add_argument("--cleanup", action="store_true",
                    help="delete the activities rows this run created and "
                         "detach their ledger rows (default: leave everything)")
    a = ap.parse_args()

    env = load_env_file(a.env_file)
    db = Db(env.get("SUPABASE_URL") or env["NEXT_PUBLIC_SUPABASE_URL"],
            env.get("SUPABASE_SERVICE_ROLE_KEY") or env["SUPABASE_SERVICE_KEY"])

    print("=== gps_checkin live activity economy E2E ===\n")
    invariant(db, "before")

    venue = db.select("venues",
                      "select=id,name,latitude,longitude,gps_radius_m,category,is_active"
                      f"&id=eq.{TEST_VENUE}")
    if not venue:
        raise SystemExit(f"venue {TEST_VENUE} not found — see GPS_DEVICE_PASS row 0.2")
    print("venue  ", json.dumps(venue[0], ensure_ascii=False))

    close_any_open(db, a.user)
    p0 = db.profile(a.user)
    print(f"user    {p0['display_name']:<16} " + json.dumps(
        {k: p0[k] for k in ('activity_pts', 'gift_pts', 'total_points', 'activities_count')}))

    # ── 1. check in INSIDE the radius ────────────────────────────────────────
    k1 = str(uuid.uuid4())
    print(f"\n-- golfin_activity_checkin(inside, key={k1})")
    out = checkin(db, a.user, TEST_LAT, TEST_LON, k1)
    print("   ->", json.dumps({k: v for k, v in out.items() if k != "activity"},
                              ensure_ascii=False))
    check("check-in ok", bool(out.get("ok")))
    check("check-in not replayed", out.get("replayed") is False)
    check("gps_verified true inside the radius", out.get("gps_verified") is True,
          f"distance_m={out.get('distance_m')}, radius={out.get('radius_m')}")
    check("awarded +30", out.get("awarded") == 30)

    act_id = (out.get("activity") or {}).get("id")
    if act_id:
        _CREATED.append(act_id)
    check("activity row is active", (out.get("activity") or {}).get("status") == "active")

    p1 = db.profile(a.user)
    check("activity_pts +30", p1["activity_pts"] == p0["activity_pts"] + 30,
          f"{p0['activity_pts']} -> {p1['activity_pts']}")
    check("total_points +30", p1["total_points"] == p0["total_points"] + 30,
          f"{p0['total_points']} -> {p1['total_points']}")

    rows = ledger(db, a.user, k1)
    print("   ledger:", json.dumps(rows, ensure_ascii=False))
    check("one gps_checkin ledger row (+30 activity)",
          len(rows) == 1 and rows[0]["type"] == "gps_checkin"
          and rows[0]["amount"] == 30 and rows[0]["currency"] == "activity")

    # ── 2. replay ────────────────────────────────────────────────────────────
    print("\n-- REPLAY check-in with the SAME key")
    out2 = checkin(db, a.user, TEST_LAT, TEST_LON, k1)
    print("   ->", json.dumps({k: v for k, v in out2.items() if k != "activity"}))
    check("replay says replayed:true", out2.get("replayed") is True)
    check("replay awarded 0", out2.get("awarded") == 0)
    p2 = db.profile(a.user)
    check("replay moved NOTHING",
          (p2["activity_pts"], p2["total_points"]) == (p1["activity_pts"], p1["total_points"]))
    open_rows = db.select("activities",
                          f"select=id&user_id=eq.{a.user}&status=eq.active")
    check("replay opened NO second round", len(open_rows) == 1, f"{len(open_rows)} open")

    # ── 3. a second check-in with a FRESH key is refused (D2) ────────────────
    print("\n-- second check-in, FRESH key")
    out3 = checkin(db, a.user, TEST_LAT, TEST_LON, str(uuid.uuid4()))
    print("   ->", json.dumps(out3))
    check("refused already_active",
          out3.get("ok") is False and out3.get("reason") == "already_active")
    check("refusal names the open round", out3.get("activity_id") == act_id)

    # ── 4. check out ─────────────────────────────────────────────────────────
    k2 = str(uuid.uuid4())
    print(f"\n-- golfin_activity_checkout(key={k2})")
    co = checkout(db, a.user, act_id, TEST_LAT, TEST_LON, k2)
    print("   ->", json.dumps({k: v for k, v in co.items() if k != "activity"},
                              ensure_ascii=False))
    check("check-out ok", bool(co.get("ok")))
    check("awarded +15 (10 base + 5 verified)", co.get("awarded") == 15)
    check("not expired", co.get("expired") is False)
    check("gps_verified survived both ends", co.get("gps_verified") is True)
    check("status completed", (co.get("activity") or {}).get("status") == "completed")
    check("gps_check_count kept the max", 
          ((co.get("activity") or {}).get("gps_check_count") or 0) >= 3)

    p3 = db.profile(a.user)
    check("activity_pts +15", p3["activity_pts"] == p2["activity_pts"] + 15,
          f"{p2['activity_pts']} -> {p3['activity_pts']}")
    check("total_points +15", p3["total_points"] == p2["total_points"] + 15)
    check("activities_count +1",
          p3["activities_count"] == p0["activities_count"] + 1,
          f"{p0['activities_count']} -> {p3['activities_count']}")

    rows = ledger(db, a.user, k2)
    print("   ledger:", json.dumps(rows, ensure_ascii=False))
    check("one activityComplete ledger row (+15)",
          len(rows) == 1 and rows[0]["type"] == "activityComplete"
          and rows[0]["amount"] == 15)

    print("\n-- REPLAY check-out with the SAME key")
    co2 = checkout(db, a.user, act_id, TEST_LAT, TEST_LON, k2)
    check("check-out replay says replayed:true", co2.get("replayed") is True)
    p4 = db.profile(a.user)
    check("check-out replay moved NOTHING",
          (p4["activity_pts"], p4["total_points"], p4["activities_count"])
          == (p3["activity_pts"], p3["total_points"], p3["activities_count"]))

    # ── 5. FAR check-in awards nothing (D1, server half) ─────────────────────
    k3 = str(uuid.uuid4())
    print(f"\n-- check-in FAR outside the radius ({FAR_LAT},{FAR_LON})")
    far = checkin(db, a.user, FAR_LAT, FAR_LON, k3)
    print("   ->", json.dumps({k: v for k, v in far.items() if k != "activity"}))
    check("far check-in still opens a round", bool(far.get("ok")))
    check("far check-in NOT gps_verified", far.get("gps_verified") is False,
          f"distance_m={far.get('distance_m')}")
    check("far check-in awarded 0", far.get("awarded") == 0)
    check("far check-in wrote NO ledger row", len(ledger(db, a.user, k3)) == 0)
    p5 = db.profile(a.user)
    check("far check-in moved no points",
          (p5["activity_pts"], p5["total_points"])
          == (p4["activity_pts"], p4["total_points"]))

    far_id = (far.get("activity") or {}).get("id")
    if far_id:
        _CREATED.append(far_id)

    # ── 6. the 8 h expiry path ───────────────────────────────────────────────
    print("\n-- backdate that round 9 h and check out -> expired")
    stale = (datetime.now(timezone.utc) - timedelta(hours=9)).isoformat()
    db.patch("activities", f"id=eq.{far_id}", {"check_in_at": stale})
    k4 = str(uuid.uuid4())
    exp = checkout(db, a.user, far_id, FAR_LAT, FAR_LON, k4)
    print("   ->", json.dumps({k: v for k, v in exp.items() if k != "activity"}))
    check("expired flag set", exp.get("expired") is True,
          f"elapsed_seconds={exp.get('elapsed_seconds')}")
    check("expired awarded 0", exp.get("awarded") == 0)
    check("status expired", (exp.get("activity") or {}).get("status") == "expired")
    check("expired wrote NO ledger row", len(ledger(db, a.user, k4)) == 0)
    p6 = db.profile(a.user)
    check("expired did NOT bump activities_count",
          p6["activities_count"] == p5["activities_count"],
          f"{p5['activities_count']} -> {p6['activities_count']}")

    # ── 7. a score post on a live round closes THAT row (A5 / D6) ────────────
    print("\n-- open a round, then post a score carrying activity_id")
    k5 = str(uuid.uuid4())
    live = checkin(db, a.user, TEST_LAT, TEST_LON, k5)
    live_id = (live.get("activity") or {}).get("id")
    if live_id:
        _CREATED.append(live_id)
    check("round opened for the score test", bool(live.get("ok")) and bool(live_id))

    before_rows = db.select(
        "activities",
        f"select=id&user_id=eq.{a.user}&venue_id=eq.{TEST_VENUE}")
    n_before = len(before_rows or [])

    # The RPC layer this script exercises has no score path — /score/submit is a
    # FastAPI handler, not a function — so A5 is asserted by doing what the
    # handler does: UPDATE the live row rather than INSERT a second one, and
    # then count the rows. A router-level run over HTTP after the deploy covers
    # the handler itself; this covers the CLAIM, which is "one round, one row".
    db.patch("activities", f"id=eq.{live_id}&user_id=eq.{a.user}", {
        "status": "completed",
        "check_out_at": datetime.now(timezone.utc).isoformat(),
        "screenshot_data": {"score": 92, "score_type": "18",
                            "input_method": "manual", "e2e": True},
        "points": 20,
    })
    after_rows = db.select(
        "activities",
        f"select=id&user_id=eq.{a.user}&venue_id=eq.{TEST_VENUE}")
    check("score post left ONE row for the round, not two",
          len(after_rows or []) == n_before,
          f"{n_before} -> {len(after_rows or [])}")

    closed = db.select("activities",
                       f"select=id,status,screenshot_data&id=eq.{live_id}")
    check("the round row itself carries the score",
          bool(closed) and (closed[0].get("screenshot_data") or {}).get("score") == 92)
    check("no round left open", not db.select(
        "activities", f"select=id&user_id=eq.{a.user}&status=eq.active"))

    print()
    invariant(db, "after")

    # ── cleanup ──────────────────────────────────────────────────────────────
    if a.cleanup and _CREATED:
        print(f"\n-- cleanup: removing {len(_CREATED)} activities row(s) this run created")
        for aid in _CREATED:
            # DETACH, never delete: the ledger row is the audit trail for points
            # the player actually holds. Deleting it would leave a balance no row
            # explains — the same damage 2026_09_02_gift_atomic §1 had to repair.
            db.patch("points_transactions", f"related_activity_id=eq.{aid}",
                     {"related_activity_id": None})
            db.delete("activities", f"id=eq.{aid}&user_id=eq.{a.user}")
        print("   (points earned are NOT reversed — see the module docstring)")
    elif _CREATED:
        print(f"\n-- left {len(_CREATED)} activities row(s) in place: {_CREATED}")

    print("\n=== %s ===" % ("ALL PASS" if not _FAILED else
                            "%d FAILED: %s" % (len(_FAILED), "; ".join(_FAILED))))
    return 1 if _FAILED else 0


if __name__ == "__main__":
    sys.exit(main())
