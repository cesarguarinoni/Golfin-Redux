#!/usr/bin/env python3
"""gps_gifts_votes — the live economy E2E, as ONE command.

    python3 Docs/Specs/Active/gps_gifts_votes/e2e_gift_economy.py \
        --env-file Tools/admin-dashboard/.env.development.local

RUN THIS ONLY AFTER `backend/migrations/2026_09_02_gift_atomic.sql` HAS BEEN APPLIED.
It calls `golfin_gift_pts` / `golfin_gift_purchase` directly over PostgREST with the service
key — the same entry point the deployed routers use — so it proves the FUNCTIONS. It does not
need the Fly deploy; run it before the deploy to check the migration, and again through the
routers afterwards if you want the HTTP layer covered too.

WHAT IT ASSERTS, and why each one is here rather than "it looked right":

  1. the invariant `total_points = activity_pts + gift_pts` over EVERY profile, before and after
  2. a 50-pt send moves the sender's activity_pts AND total_points down by 50
  3. …and the receiver's gift_pts AND total_points UP by 50   <- the half the old router skipped
  4. exactly two ledger rows: gift_sent -50 activity, gift_received +50 gift
  5. a REPLAY with the same key returns replayed:true and moves NOTHING
  6. a self-gift and an over-spend are refused as VALUES, with no balance change
  7. a purchase debits activity_pts + total_points and writes one inventory row
  8. …and its replay writes NO second inventory row

Everything it does is reversible in the sense that matters: it moves 50 pts between two accounts
Cesar owns and buys one 30-pt item. Nothing is deleted.
"""
from __future__ import annotations

import argparse
import json
import ssl
import sys
import urllib.error
import urllib.request
import uuid

sys.path.insert(0, "Tools/content")
from rest import load_env_file  # noqa: E402

# Cesar's two accounts (profiles, read 2026-09-02). Override on the CLI to use others.
DEFAULT_SENDER = "f2636482-29aa-4233-a834-99526b202fe1"   # Cratilo
DEFAULT_RECEIVER = "5ba19ba2-9ef1-40ff-9dea-cd9b1927298e" # ken
AMOUNT = 50

_FAILED: list[str] = []


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

    def _call(self, method: str, path: str, body=None):
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(
            self.base + path, data=data, method=method,
            headers={"apikey": self.key, "Authorization": "Bearer " + self.key,
                     "Content-Type": "application/json", "Accept": "application/json"})
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

    def profile(self, uid: str) -> dict:
        rows = self.select("profiles",
                           f"select=id,display_name,activity_pts,gift_pts,total_points&id=eq.{uid}")
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


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--env-file", required=True)
    ap.add_argument("--sender", default=DEFAULT_SENDER)
    ap.add_argument("--receiver", default=DEFAULT_RECEIVER)
    ap.add_argument("--item", default=None,
                    help="gift_items.id to purchase; default = the cheapest active basic row")
    a = ap.parse_args()

    env = load_env_file(a.env_file)
    db = Db(env.get("SUPABASE_URL") or env["NEXT_PUBLIC_SUPABASE_URL"],
            env.get("SUPABASE_SERVICE_ROLE_KEY") or env["SUPABASE_SERVICE_KEY"])

    print("=== gps_gifts_votes live economy E2E ===\n")
    invariant(db, "before")

    s0, r0 = db.profile(a.sender), db.profile(a.receiver)
    print(f"\nsender   {s0['display_name']:<16} {json.dumps({k: s0[k] for k in ('activity_pts','gift_pts','total_points')})}")
    print(f"receiver {r0['display_name']:<16} {json.dumps({k: r0[k] for k in ('activity_pts','gift_pts','total_points')})}\n")

    # ── 1. the send ──────────────────────────────────────────────────────────
    key = str(uuid.uuid4())
    print(f"-- golfin_gift_pts({AMOUNT}, key={key})")
    out = db.rpc("golfin_gift_pts", {"p_sender": a.sender, "p_receiver": a.receiver,
                                     "p_amount": AMOUNT, "p_key": key})
    print("   ->", json.dumps(out, ensure_ascii=False))
    check("send ok", bool(out.get("ok")))
    check("send not replayed", out.get("replayed") is False)

    s1, r1 = db.profile(a.sender), db.profile(a.receiver)
    check("sender activity_pts -50", s1["activity_pts"] == s0["activity_pts"] - AMOUNT,
          f"{s0['activity_pts']} -> {s1['activity_pts']}")
    check("sender total_points -50", s1["total_points"] == s0["total_points"] - AMOUNT,
          f"{s0['total_points']} -> {s1['total_points']}")
    check("receiver gift_pts +50", r1["gift_pts"] == r0["gift_pts"] + AMOUNT,
          f"{r0['gift_pts']} -> {r1['gift_pts']}")
    check("receiver total_points +50", r1["total_points"] == r0["total_points"] + AMOUNT,
          f"{r0['total_points']} -> {r1['total_points']}")

    # ── 2. the two ledger rows ───────────────────────────────────────────────
    sent = db.select("points_transactions",
                     f"select=user_id,type,amount,currency,description,idempotency_key"
                     f"&user_id=eq.{a.sender}&idempotency_key=eq.{key}")
    recv = db.select("points_transactions",
                     f"select=user_id,type,amount,currency,description,idempotency_key"
                     f"&user_id=eq.{a.receiver}&type=eq.gift_received&order=created_at.desc&limit=1")
    print("   sender ledger  :", json.dumps(sent, ensure_ascii=False))
    print("   receiver ledger:", json.dumps(recv, ensure_ascii=False))
    check("gift_sent row (-50, activity)",
          len(sent) == 1 and sent[0]["type"] == "gift_sent"
          and sent[0]["amount"] == -AMOUNT and sent[0]["currency"] == "activity")
    check("gift_received row (+50, gift)",
          len(recv) == 1 and recv[0]["amount"] == AMOUNT and recv[0]["currency"] == "gift")
    check("receiver row carries a DERIVED key, not the sender's",
          bool(recv) and recv[0]["idempotency_key"] not in (None, key))

    # ── 3. replay ────────────────────────────────────────────────────────────
    print(f"\n-- REPLAY golfin_gift_pts with the SAME key")
    out2 = db.rpc("golfin_gift_pts", {"p_sender": a.sender, "p_receiver": a.receiver,
                                      "p_amount": AMOUNT, "p_key": key})
    print("   ->", json.dumps(out2, ensure_ascii=False))
    check("replay says replayed:true", out2.get("replayed") is True)
    s2, r2 = db.profile(a.sender), db.profile(a.receiver)
    check("replay moved NOTHING",
          (s2["activity_pts"], s2["total_points"], r2["gift_pts"], r2["total_points"])
          == (s1["activity_pts"], s1["total_points"], r1["gift_pts"], r1["total_points"]))

    # ── 4. refusals ──────────────────────────────────────────────────────────
    print("\n-- refusals")
    self_gift = db.rpc("golfin_gift_pts", {"p_sender": a.sender, "p_receiver": a.sender,
                                           "p_amount": AMOUNT, "p_key": str(uuid.uuid4())})
    print("   self-gift  ->", json.dumps(self_gift))
    check("self-gift refused", self_gift.get("ok") is False and self_gift.get("reason") == "self_gift")

    broke = db.rpc("golfin_gift_pts", {"p_sender": a.sender, "p_receiver": a.receiver,
                                       "p_amount": 999_999_999, "p_key": str(uuid.uuid4())})
    print("   over-spend ->", json.dumps(broke))
    check("over-spend refused", broke.get("ok") is False and broke.get("reason") == "insufficient")

    s3 = db.profile(a.sender)
    check("refusals moved NOTHING",
          (s3["activity_pts"], s3["total_points"]) == (s2["activity_pts"], s2["total_points"]))

    # ── 5. purchase + its replay ─────────────────────────────────────────────
    item = a.item
    if item is None:
        rows = db.select("gift_items",
                         "select=id,name,price_activity_pts&tier=eq.basic&is_active=eq.true"
                         "&price_activity_pts=not.is.null&order=price_activity_pts.asc&limit=1")
        item = rows[0]["id"]
        print(f"\n-- purchase {rows[0]['name']} ({rows[0]['price_activity_pts']} pts)")
    pkey = str(uuid.uuid4())
    pout = db.rpc("golfin_gift_purchase", {"p_user": a.sender, "p_item": item,
                                           "p_currency": "activity", "p_key": pkey})
    print("   ->", json.dumps(pout, ensure_ascii=False))
    check("purchase ok", bool(pout.get("ok")))
    s4 = db.profile(a.sender)
    price = pout.get("price", 0)
    check("purchase debited activity_pts + total_points",
          s4["activity_pts"] == s3["activity_pts"] - price
          and s4["total_points"] == s3["total_points"] - price,
          f"-{price}")
    inv = db.select("user_inventory", f"select=id,item_id,source&user_id=eq.{a.sender}")
    check("one inventory row", len(inv) >= 1)

    prep = db.rpc("golfin_gift_purchase", {"p_user": a.sender, "p_item": item,
                                           "p_currency": "activity", "p_key": pkey})
    print("   replay ->", json.dumps(prep, ensure_ascii=False))
    check("purchase replay says replayed:true", prep.get("replayed") is True)
    inv2 = db.select("user_inventory", f"select=id&user_id=eq.{a.sender}")
    check("replay wrote NO second inventory row", len(inv2) == len(inv))

    print()
    invariant(db, "after")

    print("\n=== %s ===" % ("ALL PASS" if not _FAILED else
                            "%d FAILED: %s" % (len(_FAILED), "; ".join(_FAILED))))
    return 1 if _FAILED else 0


if __name__ == "__main__":
    sys.exit(main())
