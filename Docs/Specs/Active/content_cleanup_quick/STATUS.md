READY_FOR_ARCHITECT_REVIEW

Implemented directly by Claude Code at Cesar's request (2026-08-26). No SPEC.md — the five items
are specified in `Docs/TellCode.md` § `content_cleanup_quick`, with the decisions of record for
items 4 and 5 in `Docs/CONTENT_PIPELINE_PLAN.md` §6.5. SPEC_KIND: backend + admin tooling; no Figma
node, no screenshot deliverable mandated (the two dashboard items were still verified visually —
see IMPLEMENTER_REPORT.md).

All five items are done and verified. Nothing outstanding.

| # | Item | Where | Verified by |
|---|---|---|---|
| 1 | Drop the per-catalog `enabled` field | playlife `routers/content.py`, Unity `Golfin.Content` | 11 backend tests + 4 EditMode tests, both tripwired |
| 2 | Dashboard control for the GLOBAL kill switch | `Tools/admin-dashboard` | driven through the real UI, mock mode, EN + JA |
| 3 | Shared `TestBoot.SaveDataHost()` | `Assets/Scripts/TestSupport/` | tripwired — 5 tests across 3 fixtures go red without it |
| 4 | Revoke an UNAPPLIED grant from the Users drawer | `Tools/admin-dashboard` | driven through the real UI + all four route outcomes |
| 5 | Log every merge that RAISES a quantity | `Golfin.InventorySync` + `TelemetryHooks` | 7 EditMode tests, tripwired |

Test state at hand-off:

* Unity EditMode — **1768 total, 1765 passed, 0 failed, 3 skipped** (the 3 skips are pre-existing
  `HoleCompleteDriverTests` Stage-C1 skips, unrelated).
* playlife backend — **26 passed** (`tests/test_content_kill_switch.py` + `tests/test_golfin_inventory.py`).
* admin dashboard — `tsc --noEmit` clean, `next build` clean, new route present in the manifest.

## ✅ DEPLOYED 2026-08-26

Both halves are live. No migration was needed — nothing in this task touched the schema.

| Target | Result |
|---|---|
| `playlife-api` (fly.io) | v52 → **v53**, image `deployment-01M0Y4Z2M8RS03J97N431YDPBJ`, `/health` 200 |
| `golfin-admin` (Cloudflare, `admin.golfin.world`) | version **`cf90ee8a-d341-4e1d-b405-33f832ff6f36`** at 100% |
| GolfinRedux `main` | `45c290f01` pushed |
| playlife `main` | `e9047fa` pushed |

**Live prod evidence for item 1** — the same request before and after the deploy:

```
BEFORE (v52)  catalogs[texts] keys: ['changed', 'enabled', 'full', 'version']   enabled present = True
              catalogs[bags]  keys: ['changed', 'enabled', 'full', 'version']   enabled present = True

AFTER  (v53)  catalogs[texts] keys: ['changed', 'full', 'version']              enabled present = False
              catalogs[bags]  keys: ['changed', 'full', 'version']              enabled present = False
              top-level enabled=True  disabled=[]  latest_version=11
```

**And the property that regresses first still holds** — top-level `enabled` / `disabled` are
identical no matter which subset is requested, which is the invariant
`content_kill_switch_and_order` was written to protect:

```
catalogs=bags,items   enabled=True  disabled=[]
catalogs=items        enabled=True  disabled=[]
catalogs=texts        enabled=True  disabled=[]
(all)                 enabled=True  disabled=[]
```

⚠️ **No kill switch was flipped on prod.** Doing so would revert live testers to bundled content
for two launches; the switches themselves are covered by the 11 backend tests and were already
proven live under `content_kill_switch_and_order`. Flipping the new GLOBAL button for real is a
device-pass step, not a deploy step.

⚠️ **The dashboard could not be exercised through its live URL.** `admin.golfin.world` sits behind
Cloudflare Access, which 302s every unauthenticated request — so the deploy is verified by the
active version id and by the changed bundles that uploaded (`app/(panels)/users/page-*.js` among
them), not by driving the live UI. The functional verification of items 2 and 4 is the mock-mode
run recorded in IMPLEMENTER_REPORT.md. **First thing to do in the browser: open
admin.golfin.world ▸ Clubs ▸ Review & publish ▸ Kill switch and confirm both cards are there.**

Awaiting Cesar's sign-off. The batched device pass is what comes next, and it is now unblocked on
both sides.
