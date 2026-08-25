# QUEUED — `content_admin_panels`

Blocked on `content_catalog` (Docs/Specs/Active/content_catalog/SPEC.md) Stage D landing.

Six panels on `admin.golfin.world`, registered in `Tools/admin-dashboard/lib/registry.ts`,
all reading the route handlers Stage D builds — no new server logic:

| Panel | Notes |
|---|---|
| Clubs | 799 rows — server-side pagination + filter by brand/type/rarity is mandatory |
| Characters | 12 rows, simple grid |
| Items | Items + Bags + Balls as three tabs in one panel |
| Texts | 500 keys; EN and JA side by side, filter by key prefix |
| Shop | `CONTENT_PIPELINE_PLAN.md` §11 — `refId` typeahead against the live catalog, resolved preview (name/rarity/thumbnail), LIVE/SCHEDULED/ENDED badge |
| — | plus a **Publish** drawer shared by all of them: diff preview → confirm → publish; version history with one-click rollback; the per-catalog enable switch |

Hard requirements carried over:

- Every new UI string needs BOTH `en` and `ja` in `lib/i18n.ts` `DICT` — `DictKey` is derived
  from `DICT`, so a missing key is a type error, not a blank (`ADMIN_DASHBOARD_OPS.md` §3.4).
- Do not name a row-map parameter `t` — it shadows the translator and has bitten that file twice.
- JA needs `whitespace-nowrap` on badges and table headers; drop `tracking-wider` on JA badges.
- The language switcher is `z-30`; drawers and editors must be `z-40` to cover it.
- **Write on the Shop panel that prices are NOT server-enforced** (`CONTENT_PIPELINE_PLAN.md`
  §11.5) — purchases still debit RP client-side through `PointsSpendGate`.
- Never read identity or content facts off the dashboard in MOCK mode (§3.5).
