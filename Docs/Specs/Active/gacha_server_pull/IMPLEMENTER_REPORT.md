# IMPLEMENTER REPORT — `gacha_server_pull`

**Built:** 2026-08-31 · **STATUS:** `AWAITING_MIGRATION` · **Implementer:** Claude Code (direct)

> **No subagent pipeline on this task, deliberately.** The chain in CLAUDE.md is a UI pipeline —
> it gates on a canonical screenshot, a Figma node diff, mesh metrics and a UI-fidelity lint.
> This task touches **zero Unity assets**: it is two SQL migrations, a FastAPI router, a backend
> test suite and a Next.js panel. There is nothing for `golfin-self-reviewer` to pixel-scan and
> nothing `enforce_implementer_done.py` can gate on. The evidence here is test output, deploy
> ids, HTTP status codes and SQL.

---

## What is outstanding, and it is exactly one thing

**Cesar must paste two migrations into the Supabase SQL editor** (project
`wmszyghwwkaptgqdunel`), in this order:

1. `playlife/backend/migrations/2026_09_01_golfin_gacha.sql`
2. `playlife/backend/migrations/2026_09_01_shop_purchase_tickets.sql`

The order is not optional: file 2's body calls `golfin_ticket_credit()` and `golfin_ref_owned()`,
which file 1 creates. Both are idempotent and safe to re-run.

**Why I cannot do it:** Supabase's REST API has no DDL path and there is no Postgres connection
string on this machine — `ADMIN_DASHBOARD_OPS.md` §3.2 states this as the standing constraint
("You cannot run DDL yourself… Write the migration into `playlife/backend/migrations/`, hand
Cesar the SQL"). This is a genuine blocker, not a deferral.

**What is blocked behind it, and nothing else is:**

| Spec item | Why it needs the migration |
|---|---|
| §7 VERIFICATION blocks (both files) | They query `pg_proc` / `pg_indexes` for objects the migration creates |
| §7 roll parity (2 000 × x10 vs `simulate`) | Calls `golfin_gacha_pull()` |
| §8 live E2E steps 1–8 | Every step touches a table or function the migration creates |
| Third deploy proof (dashboard) | See "Deploys" below — held on purpose |

**Once they land, none of it needs Cesar again.** The `service_role` key in
`Tools/admin-dashboard/.env.development.local` reaches prod over PostgREST from this machine — I
confirmed that during the build by reading `content_rows` (spec A's seed IS applied: `gacha_pools`
11, `gacha_rates` 6, `gacha_banners` 4, `ticket_types` 2) and by probing `golfin_tickets`, which
404s exactly as an unapplied table should. §7 and §8 are a single unattended pass after the paste.

---

## Files changed

### `playlife` (backend)

| File | What it is |
|---|---|
| `backend/migrations/2026_09_01_golfin_gacha.sql` | **NEW, 1495 lines.** Five tables (`golfin_tickets`, `golfin_ticket_transactions`, `golfin_gacha_pulls`, `golfin_gacha_prizes`, `golfin_gacha_pity`), RLS on with zero policies + service-role grants on every one, `comment on` every table, the `gacha_enabled` setting row, the `gacha_dupe` earn action, and five functions: `golfin_ticket_credit`, `golfin_ref_owned`, `golfin_gacha_pull` + two roll internals. Verification + smoke blocks at the bottom. |
| `backend/migrations/2026_09_01_shop_purchase_tickets.sql` | **NEW, 703 lines.** `create or replace golfin_shop_purchase` with `category = ticket`, the `quantity` column, the `golfin_ref_owned` extraction, and `golfin_shop_purchases.grant_id` made nullable. |
| `backend/routers/gacha.py` | **NEW.** `/api/v1/gacha` — `POST /pull`, `GET /history`, `GET /tickets`. |
| `backend/main.py` | Mounts the gacha router at `/api/v1/gacha`. |
| `backend/tests/test_gacha.py` | **NEW, 58 tests.** Auth, validation, every rpc status as 200, history nesting + the two-query property, tickets defaulting to empty. |
| `backend/tests/test_shop_purchase.py` | +3 tests: the `invalid_ref` reason, the ticket `ok` shape with `grant.id = null`, and that `ticket` left the `unsupported_category` set. |

### `GolfinRedux` (dashboard + docs)

| File | What it is |
|---|---|
| `Tools/admin-dashboard/lib/gachaAudit.ts` | **NEW, pure.** The odds audit (forced slots excluded) and the pull-log CSV. The vitest-covered core. |
| `Tools/admin-dashboard/lib/__tests__/gachaAudit.test.ts` | **NEW, 20 tests.** |
| `Tools/admin-dashboard/lib/gachaData.ts` | **NEW, server-only.** Pull log with filters, odds audit, stats cards, per-player gacha state, the pause flag. Degrades to `notMigrated` instead of 500ing. |
| `Tools/admin-dashboard/lib/gachaMutations.ts` | **NEW, server-only.** Pause/resume, ticket grant/adjust through `golfin_ticket_credit`, pity reset. All three audited. |
| `Tools/admin-dashboard/lib/mockGacha.ts` | **NEW.** Mock fixtures, deliberately implausible (`@example.invalid`, 999 tickets) per §3.5. |
| `Tools/admin-dashboard/app/(panels)/gacha/{page,gacha-panel}.tsx` | **NEW.** The ops panel: pause switch, stats, pull log + CSV, odds audit. |
| `Tools/admin-dashboard/app/(panels)/users/gacha-tab.tsx` | **NEW.** The drawer's Gacha tab: balances + grant/adjust, ledger, pity + reset, recent pulls. |
| `Tools/admin-dashboard/app/api/gacha/{pulls,odds,enabled,export}/route.ts` | **NEW.** Four routes; `checkAdmin()` + `force-dynamic` on every one. |
| `Tools/admin-dashboard/app/api/gacha/users/[id]/{tickets,pity}/route.ts` | **NEW.** Per-player read + the two writes. |
| `Tools/admin-dashboard/lib/buildGates.ts` | `TICKET_SHOP_BUILD` (0) + `ticketShopBuildPending()`. |
| `Tools/admin-dashboard/lib/contentValidate.ts` | `ticket` → `ticket_types`; `quantity` numeric; **G1-T** (ticket build gate) and **G3-Q** (quantity is ticket-only); G1 skips `ticket` so one cause is one error. |
| `Tools/admin-dashboard/lib/contentView.ts` | `quantity` in the shop table columns. |
| `Tools/admin-dashboard/lib/gachaOdds.ts` | **The pity off-by-one fix** — see Deviations. |
| `Tools/admin-dashboard/lib/inventoryData.ts` | The inventory response carries the ticket LEDGER (third independent degradation). |
| `Tools/admin-dashboard/lib/inventoryMutations.ts` | `issueInventoryGrant` refuses `kind = 'ticket'` outright. |
| `Tools/admin-dashboard/app/api/users/[id]/inventory/route.ts` | The ticket kind routes to `creditTickets`, not the queue. |
| `Tools/admin-dashboard/app/(panels)/users/inventory-tab.tsx` | Server ledger section; the blob map relabelled "Device counter (legacy)". |
| `Tools/admin-dashboard/app/(panels)/users/user-drawer.tsx` | The Gacha tab, its fetch, and the pity-reset confirm. |
| `Tools/admin-dashboard/lib/{registry,i18n,types}.ts` | Panel entry, ~90 DICT entries (EN + JA), gacha types. |
| `Tools/admin-dashboard/lib/__tests__/{contentValidate,gachaOdds}.test.ts` | +9 and +2 tests. |
| `Tools/admin-dashboard/migrations/*.sql` | Dashboard copies of both migrations. |
| `Docs/{ADMIN_DASHBOARD_OPS,AI_CONTEXT,TellCode}.md`, `Tools/content/README.md` | Documented. |

---

## Acceptance (SPEC §10)

| # | Item | Verdict | What was measured |
|---|---|---|---|
| 1 | §8 steps 1–8 run on prod and pasted | **BLOCKED** | Needs the migration. Every step is written out in the migration's SMOKE block and in §8; the harness is ready. |
| 2 | §7 parity tables within ±1.5 pt; throwaway rows deleted | **BLOCKED** | Same. The TS side is runnable today (`simulate` is pure); the SQL side needs `golfin_gacha_pull()`. |
| 3 | Every §2.3 status reachable in the backend tests; `cost_changed` / `insufficient` write nothing | **PASS (router) / PARTIAL (SQL)** | All 16 statuses are asserted as HTTP 200 with the payload passed through verbatim (`test_every_rpc_status_passes_through_as_200_with_data`). The "writes nothing" half is a property of the plpgsql, not the router, and is proven by the migration's SMOKE steps 1 and 3 (row counts after `insufficient` and `cost_changed`) — which need the migration. Not ported into Python on purpose: that would test the port, not the function the server runs (the same line `test_shop_purchase.py` draws in its own header). |
| 4 | x10 guarantee: Rare+ at 1 bp still yields ≥ 1 Rare+ and `guarantee_forced = true` | **PASS (TS) / BLOCKED (SQL)** | `gachaOdds.test.ts` "fires the x10 guarantee only on blocks that did not already reach the rarity" covers the reference implementation. The plpgsql half is §7. |
| 5 | Pity: threshold 3 → the 3rd pull forced, counter resets, `pityThreshold = 0` never forces | **PASS (TS) / BLOCKED (SQL)** | Two NEW vitest cases pin the exact threshold semantics (`forces the THRESHOLD-th pull, not the one after it`; `allows exactly threshold - 1 sub-minimum prizes before forcing`), and the existing case pins `0` ⇒ never. This is where the off-by-one was found. |
| 6 | `pool_for_build` with a 9999 min_build Supreme entry | **BLOCKED** | Implemented (§2.3 step 8, per-tier `pool_for_build` carrying the offending rarity) and in the code path; needs the migration to exercise. |
| 7 | Admin ticket grant writes the LEDGER, not `golfin_pending_grants`; drawer shows it; adjust −N refuses below 0 | **PASS (code) / BLOCKED (live)** | Three independent locks: the route redirects `kind = 'ticket'` to `creditTickets`; `issueInventoryGrant` refuses the kind itself so a second caller cannot reintroduce it; and `golfin_ticket_credit` returns `insufficient` (nothing written) rather than clamping, surfaced as a 409. Live confirmation is §8 step 1. |
| 8 | Shop: a `ticket` row cannot be published while `TICKET_SHOP_BUILD = 0`; the server credits tickets | **PASS** | 9 new vitest cases (`shop_catalog — G1-T` ×4, `G3-Q` ×5), including that a deactivated row is exempt and that G1 does not double-report. The server half is in the migration + a router test for the `grant.id = null` shape. |
| 9 | Panel: pause/resume audited; log filters + export; odds audit excludes forced slots (fixture-checked); pity reset audited | **PASS** | `writeAudit` on all three writes with distinct actions (`gacha_pause` / `gacha_resume` / `ticket_grant` / `ticket_adjust` / `gacha_pity_reset`). Forced-slot exclusion is fixture-checked five ways in `gachaAudit.test.ts`, including the case that matters: *"a banner whose Legendaries come ENTIRELY from pity does not read as over-paying"*. |
| 10 | Three deploy proofs | **1 of 3** | See Deploys. |
| 11 | Backend suite, dashboard vitest + `npm run build` green. Zero player strings. | **PASS** | 233 / 216 / green. Every new string is a `DICT` entry with EN **and** JA; no `Assets/Localization` file was touched, and no Unity file of any kind. |
| 12 | Spec deviations flagged | **PASS** | Below. |

---

## Deploys

**API — DONE.** `flyctl deploy` → image `registry.fly.io/playlife-api:deployment-01M1B5F2YV1ZJT84RX7RSGN5WW`,
confirmed by `flyctl status` (**Image** `playlife-api:deployment-01M1B5F2YV1ZJT84RX7RSGN5WW`,
version 64). Not trusted from the exit code — probed live (memory
`reference_flyctl_401_false_deploy_failure`):

```
/health                                200      {"status":"ok","version":"0.1.0"}
/api/v1/content                        200
/api/v1/gacha/tickets                  403
/api/v1/gacha/history                  403
/api/v1/gacha/pull                     403
/api/v1/shop/purchase                  403
/api/v1/nope-does-not-exist            404
```

The last line is the one that makes the three 403s mean something: an unmounted path 404s, so a
403 is real auth on a real route rather than a routing accident.

**A second API deploy after §5 is not a second image.** SPEC §9 step 3 asks for a deploy after §5,
but §5's server half is entirely SQL (`create or replace golfin_shop_purchase`) — no Python
changed. Deploying again would produce a different deployment id for a byte-identical app and
would be a proof of nothing. Flagged rather than faked.

**Dashboard — HELD, deliberately, and this is the one place I did not follow the spec's
sequencing.** `ADMIN_DASHBOARD_OPS.md` §3.2 is unambiguous: *"Migration first, deploy second.
Always. Deploying code that references a column that does not exist yet 500s the endpoint."* This
deploy is exactly that case — until the migration lands, an admin ticket grant would stop working,
because the old grants-queue path is gone by design. I softened the failure (`creditTickets`
answers **503 naming the migration file** rather than a raw 500, and every read path renders
`notMigrated`), but softening a regression is not the same as not having one, and the rule is a
project rule rather than my judgement call.

The build is green and committed, so it is one command after the paste:

```bash
npm --prefix Tools/admin-dashboard run deploy
```

---

## Deviations from the spec, with justification

1. **`lib/gachaOdds.ts` pity fired one pull late, and I changed it.** SPEC §3 step 1 says the slot
   is forced when `counter + 1 >= pityThreshold`, and SPEC §10 spells out the consequence
   ("threshold 3 → the **3rd** pull after two sub-min prizes"). The shipped `simulate()` used
   `counter >= threshold`, which forces the 4th. SPEC §3 also says *"Both must match"* and §7 tests
   the match by distribution including pity hits — so leaving them apart would have made the parity
   harness compare two different algorithms. **The server implements the spec; `simulate()` was
   corrected to agree, in the same change, with two new tests pinning the exact semantics.** This
   is a one-line behaviour change to a module spec A shipped, so it is the deviation most worth
   Cesar's eye.

2. **Five functions, not three.** SPEC §2 names three (`golfin_ticket_credit`, `golfin_ref_owned`,
   `golfin_gacha_pull`) and its verification asks for "fn present ×3". I added two small internals
   — `golfin_gacha_draw_tier` and `golfin_gacha_draw_entry` — because the roll calls each of them
   from three places (the normal slot, the pity fallback, the guarantee re-roll) and three inline
   copies of a cumulative weight walk is three places for the walk to drift. They are the plpgsql
   halves of `drawRarity` / `drawEntry`, which is exactly the parity story. The verification block
   checks **3 contract functions and 2 roll internals as separate rows**, so the count is stated
   rather than quietly changed.

3. **`quantity` is honoured for `ticket` only.** SPEC §5.2 introduces it for the ticket path. I did
   NOT extend it to balls and items: doing so would change what already-published listings deliver,
   live, on rows an operator wrote under the old meaning. Validator rule **G3-Q** refuses a
   `quantity` other than 1 on a non-ticket row, so the column cannot silently mean nothing.

4. **`golfin_shop_purchases.grant_id` had to become nullable.** Not called out in the spec, and
   unavoidable: a ticket sale has no queue row to point at and the column was `not null`. The
   replay path already coalesced every field it reads off the grant, so a null degrades to the
   purchase row's own values with no other change.

5. **The `golfin_ref_owned` swap is a real behaviour change to the shop, and I took it.** SPEC §2.2
   says to do it in §5 "if the diff stays small" — it is a 41-line block for a 4-line call. The
   change: a club won from the **gacha** now refuses a shop sale without depending on the client
   having pushed its inventory blob first. The 08-27 migration's own comment already claimed that
   behaviour; this makes it true.

6. **`/history` and `/tickets` degrade on a missing table; `/pull` does not.** SPEC §4 says "no
   `_missing_relation` courtesy". I read that as scoped to the pull — which is where the shop's
   reasoning applies ("there is no state in which a sale silently did nothing") — and kept the
   courtesy on the two reads, where an empty answer is the true state of every player before their
   first pull. Two tests pin the asymmetry in both directions.

7. **The odds audit cannot know WHICH slot of a forced x10 was forced,** because
   `golfin_gacha_pulls` stores the flags per pull. It excludes the pull's highest-rarity slot, once
   per flag. That is provably right for the guarantee and the right guess for pity, and it is the
   conservative direction: it can only remove a high-rarity sample, so the failure mode is
   under-reporting a real over-payout rather than inventing one. Documented at the function and
   tested three ways.

8. **`STATUS.md` says `AWAITING_MIGRATION`, which is not one of CLAUDE.md's states.** The pipeline's
   vocabulary has no word for "the code is finished and correct and a human has to run DDL". The
   nearest listed state is `IMPLEMENTER_BLOCKED`, which reads as "something went wrong" and would
   route an implementer back in to fix nothing. Flagged rather than mislabelled.

---

## Things worth knowing that are not deviations

- **`ShopPurchaseResult` tolerates `grant.id = null`** — `ShopGrantDto.Id` defaults to `""` and
  `RecordAndAck` returns early on an empty id (SPEC §5.2 asked me to check). **But
  `ShopTransaction.ApplyPurchaseGrant` has no `ticket` case**: it switches over
  club/character/item/ball and falls to a `default:` that logs an error and returns
  `GeneralPurchaseResult.Invalid`. So a published ticket row today would charge the RP, credit the
  ledger correctly, and show the player a failure. That is why **G1-T is a hard error while
  `TICKET_SHOP_BUILD = 0`**, and why the constant must be set from
  `Docs/Versioning/last_uploaded_build.txt` only after the spec-C archive.
- **Two concurrency/name-resolution bugs were caught by review, not by the parser.** (a)
  `golfin_ticket_credit` originally used `on conflict do nothing` followed by
  `select … for update` — the classic upsert race: under READ COMMITTED the row can be invisible to
  the snapshot, the balance reads 0, and a ledger row gets written claiming a `balance_after` that
  was never true. Now a single `on conflict do update set updated_at = golfin_tickets.updated_at
  returning balance`. (b) The pity upsert used `public.golfin_gacha_pity.total_pulls` in an
  `ON CONFLICT SET`, where a schema-qualified name resolves against no FROM entry. Both parse
  identically; neither would have survived first contact with Postgres.
- **The one concurrency residual, stated rather than hidden:** two simultaneous pulls by the same
  player serialize on the ticket row lock `golfin_ticket_credit` takes — except on a **free banner**
  (cost 0), which takes no lock and can lose one pity-counter increment. `total_pulls` is
  incremented from the table's own value and is exact either way. Documented at step 9.
- **Pre-existing working-tree drift I did not touch or commit:** `Docs/Reports/content_art.txt`,
  `Docs/Versioning/last_uploaded_build.txt` (both `M` at session start), and the untracked spec
  folders `gacha_client_real_pull/`, `gacha_ops_polish/`, `gacha_admin_catalogs/ARCHITECT_REVIEW.md`
  (the Architect's, filed 2026-08-31).

---

## Test output

```
playlife/backend $ venv/bin/python -m pytest tests/ -q
233 passed in 0.67s          (58 new in test_gacha.py, +3 in test_shop_purchase.py)

Tools/admin-dashboard $ npx vitest run
Test Files  8 passed (8)
     Tests  216 passed (216)     (20 new gachaAudit, +9 contentValidate, +2 gachaOdds)

Tools/admin-dashboard $ npm run build
✓ Compiled successfully   ·  ƒ /gacha  4.04 kB  146 kB

/tmp $ python3 parsecheck.py …/2026_09_01_golfin_gacha.sql
PARSE OK (statement level)  ·  PLPGSQL OK: 5 function bodies parsed
/tmp $ python3 parsecheck.py …/2026_09_01_shop_purchase_tickets.sql
PARSE OK (statement level)  ·  PLPGSQL OK: 1 function bodies parsed
```

⚠️ `npm run build` fails with a misleading `<Html> should not be imported outside of
pages/_document` on `/404` **if you run it as `NODE_ENV=development npm run build`** — the override
stops Next setting `NEXT_PHASE=phase-production-build`, so `lib/mode.ts`'s missing-key guard throws
during the `/_not-found` prerender and Next reports the wrong error. Plain `npm run build` (and
`npm run deploy`) is green. Worth knowing because ADMIN_DASHBOARD_OPS §4.2 tells you to prefix
`NODE_ENV=development` for `npm run dev` and `npm install`, and the habit carries over to `build`,
where it breaks.
