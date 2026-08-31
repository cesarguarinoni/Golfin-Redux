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

---

# PART 2 — the migrations are APPLIED; §7 and §8 are run (2026-08-31)

Cesar applied both migrations. Verification came back clean on both:
**file 1 — 16 of 16 rows as expected**, **file 2 — 11 of 11**. Every `AWAITING_MIGRATION`
item below is now resolved, and the dashboard deploy (held under
`ADMIN_DASHBOARD_OPS.md` §3.2) went out immediately afterwards.

## The three deploy proofs (§23)

| # | Surface | Proof |
|---|---|---|
| 1 | Fly API | `flyctl status` → **Image `playlife-api:deployment-01M1B5F2YV1ZJT84RX7RSGN5WW`** (v64). Live probe, not the exit code: `/health` 200, `/api/v1/content` 200, `/api/v1/gacha/{pull,history,tickets}` **403**, an unmounted path **404** — the last line is what makes the three 403s mean auth rather than a routing accident. |
| 2 | Fly API, after §5 | **Not a second image, and deliberately not faked.** §5's server half is entirely SQL (`create or replace golfin_shop_purchase`); no Python changed. A second deploy would produce a new deployment id for a byte-identical app and prove nothing. The §5 server behaviour is proven live instead — see the shop probe below. |
| 3 | Dashboard | `npm run deploy` → **Version ID `bbfdb132-ed74-4507-9f4b-ee7bb2b99536`**, build stamped **`83564c011`** (the deploy script prints `→ stamping build as 83564c011` and the tree was clean, so no `-DIRTY`). Access: `curl https://admin.golfin.world/` → **302** to `late-cake-f2a4.cloudflareaccess.com`; `/gacha` → **302**. The footer stamp itself is only readable in a browser behind Access (memory `reference_admin_version_stamp_is_readable_in_browser`) — the deploy line is the machine-checkable half. |

## §7 — roll parity

**Two runs, and the second one is why the first exists.** The spec names
`banner_standard_club1`, which carries pity AND an x10 guarantee — so on that banner
"non-forced slots" is not the same population on the two sides (see the guarantee note
below). `banner_test_a` has *neither* (`pityThreshold` and `guaranteeMinRarityX10` both
blank), so every slot is unforced on both sides and the comparison is exact. That is the
control the parity claim actually rests on.

Throwaway prod user `gacha-parity-…@golfin.invalid`, created and deleted for this.
TS side: `simulate(rates, pool, banner, 20000, seed)` over the LIVE published rows, three
seeds so one unlucky seed cannot read as a parity failure.

### Run A — `banner_test_a`, 2 000 × x10 = 20 000 slots (137.6 s). THE PARITY PROOF.

| tier | published | SQL | TS | SQL−pub | TS−pub | SQL−TS |
|---|---|---|---|---|---|---|
| Common | 55.00% | 55.63% | 54.73% | +0.63 | −0.27 | +0.90 |
| Uncommon | 25.00% | 24.90% | 25.32% | −0.10 | +0.32 | −0.42 |
| Rare | 12.00% | 11.62% | 11.82% | −0.38 | −0.18 | −0.21 |
| Mythic | 5.50% | 5.29% | 5.67% | −0.21 | +0.17 | −0.39 |
| Legendary | 2.00% | 2.10% | 1.94% | +0.10 | −0.06 | +0.16 |
| Supreme | 0.50% | 0.46% | 0.50% | −0.04 | +0.00 | −0.04 |

**Worst |SQL − published| = 0.63 pt. Worst |SQL − TS| = 0.90 pt. Tolerance ±1.50 → PASS.**
Pity hits 0 on both sides, guarantee hits 0 on both sides — which is also the acceptance
item "`pityThreshold = 0` → never forced", proven at 20 000 slots rather than by argument.

### Run B — `banner_standard_club1`, 2 000 × x10, the forced mechanics

Compared as a RATE PER PULL, not as raw totals, because the two sides ran different pull
counts. Numbers filed under "Run B" below once complete.

**⚠️ THE GUARANTEE FLAG COUNTS DIFFER BY DESIGN, AND THE OUTCOMES DO NOT.** `simulate()`
decides the guarantee from the first NINE slots and forces slot 9 (`blockBest` is read
before slot 9 rolls). The server rolls slot 9 normally and re-rolls it only if the whole
block missed. So the TS flag fires at P(9 misses) = 0.8⁹ ≈ 13.4 % of pulls and the server
at P(10 misses) = 0.8¹⁰ ≈ 10.7 %. **The prize distribution is identical either way**: a
forced draw over the tiers ≥ Rare renormalised by `rateBp` IS the conditional distribution
of an unforced draw given ≥ Rare, so "force it" and "roll it, keep it if it qualifies,
else force it" produce the same law for slot 9. The flag is a report of how the slot was
reached, not of what it paid. Run A is unaffected (no guarantee) and is where the ±1.5 pt
claim is made.

### Run B — `banner_standard_club1`, 2 000 × x10 = 20 000 slots (131.0 s)

Pity 50 / Legendary, x10 guarantee Rare. Non-forced population 19 623 of 20 000 slots
(the pull-level flags cannot name WHICH slot was forced, so the pull's highest slot is
dropped once per flag — the conservative rule `lib/gachaAudit.ts` uses and states).

| tier | published | SQL non-forced | SQL all slots | SQL nf − published |
|---|---|---|---|---|
| Common | 55.00% | 55.06% | 54.02% | **+0.06** |
| Uncommon | 25.00% | 24.64% | 24.18% | **−0.36** |
| Rare | 12.00% | 12.27% | 12.62% | **+0.27** |
| Mythic | 5.50% | 5.51% | 5.67% | **+0.01** |
| Legendary | 2.00% | 2.03% | 2.81% | **+0.03** |
| Supreme | 0.50% | 0.48% | 0.71% | **−0.02** |

**Worst |SQL non-forced − published| = 0.36 pt → PASS.** The "all slots" column is the
same data WITHOUT excluding forced slots, and it is why the exclusion exists: Legendary
reads 2.81 % against a published 2.00 % and Supreme 0.71 % against 0.50 % purely because
pity and the guarantee put them there. An audit that counted those would flag a banner
that is working exactly as designed.

**Forced mechanics, as a rate per pull** (the two sides ran different pull counts, so raw
totals are not comparable):

| | SQL | TS (3 seeds) | naive theory |
|---|---|---|---|
| pity fired | **188 / 2000 = 9.40 %** | 9.35 % / 10.45 % / 9.50 % | — |
| guarantee fired | **189 / 2000 = 9.45 %** | 12.15 % / 13.10 % / 11.85 % | server 0.8¹⁰ = 10.74 %, TS 0.8⁹ = 13.42 % |

Pity matches to within 0.05 pt of the first seed and sits inside the seed spread. The
guarantee gap is the by-design flag difference explained above, and BOTH sides land the
same distance below their OWN theory (9.45 vs 10.74; 12.37 avg vs 13.42) — because a
block containing a pity-forced Legendary can never miss the guarantee, which the naive
0.8ⁿ ignores. The observed ratio SQL/TS = 0.764 against the predicted 0.8⁹→0.8¹⁰ ratio of
0.800. The two implementations agree; the flag counts what it says it counts.

**Cleanup done.** The throwaway user and every row it wrote are gone: 1 000 pull rows,
1 000 ledger rows, 1 000 pending grants, 1 000 `points_transactions`, its ticket and pity
rows, and the auth user itself (the `profiles` row cascaded). Verified after: 0 rows for
that user anywhere, and **0 orphaned `golfin_gacha_prizes`** — a live proof of the
`on delete cascade` on the prize FK.

## §8 — live E2E, on prod, through the real API

Account `cesar.guarinoni@wonderwall-g.com` (`8e7f96ed…`), real bearer token minted
without a password via `admin/generate_link` → `POST /auth/v1/verify {type, token_hash}`
(the token's `user.id` was asserted to be that account). Every pull below is a real
`POST https://playlife-api.fly.dev/api/v1/gacha/pull`.

| Step | Result |
|---|---|
| **1** admin grant 1 000 tickets | `golfin_ticket_credit` → `balance 1000`. `golfin_tickets` row present; `golfin_ticket_transactions` row `delta 1000, reason admin_grant, created_by cesar.guarinoni@wonderwall-g.com`. **NOT** a `golfin_pending_grants` row. |
| **2** x1 | `ok`, `charged 50`, `ticket_balance 950`, 1 prize (`club_wood_gf`, Common, `grant_id` set). Ledger `−50 → 950, reason gacha:banner_standard_club1:x1`. Prize row, grant row (`note gacha:<pull>`, `created_by gacha`) and pity row (`counter 1, total_pulls 1`) all present. |
| **2b** replay same key | `replayed: true`, **same `pull_id`**, `charged 50`, and the ledger balance still **950** — no second debit. |
| **3** x10 + guarantee | 10 prizes per pull. Guarantee observed twice; on both, slots 0–8 were all below Rare and slot 9 came back **Mythic** / **Rare** — the re-rolled slot, ≥ the guarantee floor. |
| **4** `expected_cost 999` | `cost_changed / 50`. Pull rows, ledger rows and pity rows **byte-identical before and after** (asserted programmatically, not by eye). |
| **insufficient** (incidental) | The balance ran out mid-run: `{"status":"insufficient","balance":50,"requested":450,"shortfall":400}`, pull-row count unchanged, **0 ledger rows for that key**. Unplanned, and the best kind of evidence. |
| **5a/b** publish a rate change | `Common 5500→500`, `Rare 1200→6200` (sum still 10 000) written to `content_rows` at 06:09:15Z. The next 200 pulls came back **Rare 59.5 %** (published 62 %) and **Common 3.5 %** (published 5 %), against a baseline run of Common 8 / Rare 2. **No deploy, no build, no client change** — the function reads the published row per call. Restored and asserted identical to the pre-change snapshot. |
| **5c** publish `costX1 = 60` | On `banner_test_a`. Pull with `expected_cost 50` → **`cost_changed / 60`**; pull with `expected_cost 60` → `ok, charged 60`. Restored, asserted identical. |
| **6** pause / resume | `content_settings.gacha_enabled = false` → next pull `not_available / paused`. Set back to true → next pull `ok, charged 50`. **Instant, no cache.** (Proved harder than intended — see below.) |
| **7** no pity on `banner_test_a` | 60 pulls: `pity_forced` **never true**, every row `(pity_before, pity_after) = (0, 0)`, pity row `counter 0 / total_pulls 60`. |
| **8** grants drain | `GET /api/v1/user/golfin-grants` with the same token → 61 pending, **all** with `note = gacha:<pull_id>`; kinds `club 6, ball 29, item 26`. Only 6 clubs because the rest repeated and were paid as dupe RP — the dupe path, visible from the outside. |

**§5.2 shop ticket sale, live.** A temporary `category = ticket` row (`refId 0`,
`rpCost 100`, `quantity 5`) inserted directly into `content_rows`, bought through
`POST /api/v1/shop/purchase` with the real token:
`status ok`, **`grant: {id: null, kind: "ticket", amount: 5}`**, RP `22763 → 22663` (−100),
tickets `49940 → 49945` (+5), ledger row `delta 5, reason shop:tmp_ticket_probe`,
**0 pending grants for that purchase**, and the `golfin_shop_purchases` row carrying
`grant_id = null, amount = 5`. Probe row and purchase row deleted afterwards; existence
re-checked. This is the whole of §5.2 proven end to end — and exactly the payload rule
G1-T exists to keep away from the shipped client.

**The pause switch proved itself by accident.** §8 step 6 ran while the §7 background job
was mid-run on the other account, and the parity job died on
`{"status":"not_available","reason":"paused"}` at pull 350. Unintended, and a stronger
demonstration than the scripted one: the switch stopped a pull loop that was already in
flight, for a different user, within one call. Run B was re-run from scratch afterwards
(the 2 000-pull table above); nothing was salvaged from the killed run.

## ⚠️ WHAT §8 LEFT ON YOUR LIVE ACCOUNT — read this and decide

The E2E was authorised by the spec, but it ran hot: step 5 needed ~350 pulls of signal to
show the rate change, so the account took **293 pulls** rather than the handful §8
describes. Current state of `cesar.guarinoni@wonderwall-g.com`:

| | baseline | now | delta |
|---|---|---|---|
| RP (`total_points`) | 823 | **22 663** | **+21 840** (320 `gacha_dupe` rows totalling 21 940, less the 100 spent on the shop probe) |
| tickets (type 0) | none | **49 945** | 71 000 granted, 21 060 spent, +5 from the shop probe |
| pull rows | 0 | 293 | |
| unapplied pending gacha grants | 0 | **117** | the client will drain these at next launch |

Nothing here is wrong — it is what the feature does — but 22 663 RP is not a balance you
chose, and 117 grants will land in your inventory at next launch. **I have not reverted
any of it: adjusting your RP is your call, not mine.** Say the word and I will run the
revert (delete the pull/prize/ledger/pity rows, delete the 117 unapplied grants, and take
RP back to 823 through the ledger so the correction is itself auditable).

## Acceptance (SPEC §10) — final

| # | Item | Verdict |
|---|---|---|
| 1 | §8 steps 1–8 on prod, pasted | **PASS** — table above, all eight |
| 2 | §7 parity within ±1.5 pt; throwaway deleted | **PASS** — worst 0.90 pt (Run A, SQL vs TS), 0.63 pt vs published; user and all 4 000+ rows deleted, 0 orphans |
| 3 | every §2.3 status reachable; `cost_changed`/`insufficient` write nothing | **PASS** — router 200s for all 16 in tests; both no-write claims asserted live by row counts |
| 4 | x10 guarantee | **PASS** — observed twice live, slots 0–8 sub-Rare and slot 9 ≥ Rare; 189/2000 at scale |
| 5 | pity threshold / reset / 0 = never | **PASS** — 9.40 % vs TS 9.35 %; live traces show the counter resetting on a Supreme mid-pull (43 → 0 → 3); `banner_test_a` 0/20 060 forced |
| 6 | `pool_for_build` | **PARTIAL** — implemented and code-reviewed; not exercised live, because it needs a published pool entry with `min_build 9999` and publishing one to prod to prove a refusal was not worth the blast radius. The sibling refusals (`rates`, `invalid_price`, `ticket_type`) share the code path and the router test covers the payload. **Flagged, not claimed.** |
| 7 | admin ticket grant writes the ledger | **PASS** — live, step 1 |
| 8 | shop ticket: unpublishable (G1-T) + server credits | **PASS** — 9 vitest cases + the live probe above |
| 9 | panel: pause/resume audited, log + export, odds excludes forced, pity reset audited | **PASS** — deployed; forced-slot exclusion additionally validated against 20 000 real slots (the "all slots" column above is what it prevents) |
| 10 | three deploy proofs | **PASS** — table at the top; #2 explained rather than faked |
| 11 | suites green, zero player strings | **PASS** — 233 / 216 / build green; no Unity file touched |
| 12 | deviations flagged | **PASS** — Part 1 §Deviations, plus the guarantee-flag note above |

---

# PART 3 — acceptance #6 closed, and the prod account reverted (2026-08-31)

## Acceptance #6 `pool_for_build` — now a live PASS

Cesar's correction, and it was right: **my own §5.2 shop probe already showed the safe
pattern** — write throwaway rows straight into `content_rows`, exercise the server, delete
them — and I had failed to apply it to the one item I left PARTIAL. Publishing to prod was
never necessary; `content_rows` is what the function reads, and a row can live there for a
second.

Throwaway pool `pool_probe_mb`, built to the acceptance shape exactly:

* rates: **Common 9500 + Supreme 500 = 10 000**. The other four tiers carry no rate row, so
  `rateBp = 0` and the "every rated tier must be payable" rule does not demand entries.
* entries: Common → `club_driver_gf` at `min_build 0`; **Supreme → `club_putter_golfinx` at
  `min_build 9999`, the ONLY Supreme entry**.
* banner `banner_probe_mb`, `costX1 = 0` — a FREE banner, so the probe never touches the
  ticket ledger at all. (That the function accepts cost 0 is itself the §2.3 step 7 rule.)

```
build  2000 → {"status":"not_available","reason":"pool_for_build","rarity":"Supreme"}
build  9998 → {"status":"not_available","reason":"pool_for_build","rarity":"Supreme"}
build  9999 → ok   prize Supreme club_putter_golfinx   charged 0
```

9998 is the boundary and it is the row that matters: the refusal is `min_build > build`, not
a coarse "high build" check. Catalog rows removed after a **1.0 s** window; `gacha_pools`
verified back to 11 rows; throwaway user and its rows deleted.

**Acceptance #6: PASS.** Every item in SPEC §10 is now PASS.

## The §8 footprint is REVERTED

Cesar asked for the revert. A full backup of all nine affected tables was written first
(`/tmp/cesar_backup.json`, 1 579 rows) so the revert was itself reversible.

**What made an exact restore possible rather than a guess:** replaying the level-up loop
over the lifetime positive XP in `points_transactions` reproduced the LIVE `profiles` row
exactly — `replay(24 073 XP) → (level 10, xp 1 573)` and `profiles` said `(10, 1 573)`. That
validated the model, so `replay(24 073 − 21 940) → (level 3, xp 633)` is the pre-test avatar
state, not an estimate. Independently, the 102 pre-existing transactions sum to **823**,
which is exactly the baseline `total_points` captured before any of this ran.

| | before revert | after revert | pre-test baseline |
|---|---|---|---|
| `activity_pts` / `gift_pts` / `total_points` | 22 663 / 0 / 22 663 | **823 / 0 / 823** | 823 / 0 / 823 |
| `avatar_level` / `avatar_xp` | 10 / 1 573 | **3 / 633** | 3 / 633 |
| `points_transactions` | 423 | **102** | 102 |
| pending grants | 121 (117 gacha) | **4** | 4 |
| tickets | 49 945 | **none** | none |
| pull / prize / ledger / pity rows | 293 / 437 / 297 / 2 | **0** | 0 |

Deleted: 320 `gacha_dupe` rows + 1 shop-probe `spend` row, 117 gacha pending grants, all
gacha rows, the probe's `golfin_shop_purchases` row. **Kept, and verified by id:** the four
pre-existing pending grants (`shop_char_mike`, `shop_club_iron9_klyro`, two
`shop_ball_putt_ace`) and the four pre-existing shop purchases.

The grant delete carried **both** filters `note like 'gacha:%'` AND `applied_at is null` —
the same pair `revokeInventoryGrant` uses, for the same reason: a grant the client had
already drained must not be deleted, because deleting the queue row would not take the item
back. It was asserted up front that 0 of the 117 were applied.

Three independent checks after the fact, none of which is "I set it back":
* the surviving ledger **sums to 823** and `profiles.total_points` **is** 823;
* the surviving ledger **replays to (3, 633)** and `profiles` **is** (3, 633);
* the four surviving grant **ids are identical** to the pre-test set.

All five gacha tables are now **globally empty** — nothing but this task ever wrote to them,
so the tables are in the state the migration created. The four gacha catalogs are untouched
(4 / 6 / 11 / 2 rows) and both `content_settings` flags read `true`.

**What is deliberately NOT reverted:** the migrations, the deploys, and the two schema
changes. Those are the feature.
