# REDTEAM_REVIEW — `weekly_rotation_admin`

**Iteration:** 2
**Reviewer:** `golfin-redteam-reviewer` (adversarial gate)
**Date:** 2026-09-11 13:30 JST
**Verdict:** **ARCHITECT_REVIEW_PASS**
**STATUS after this review:** `ARCHITECT_REVIEW_PASS`

Prior rejections: **none** — no `CESAR_REJECTION.md`; this is the first pass to Cesar.

Scope: admin dashboard (Next.js/Cloudflare) + content pipeline + one applied SQL migration +
one new catalog CSV + a placeholder PNG. No Unity C#, no Figma node, no world→screen feature,
no mesh, no clone mandate. The Unity/Figma/UI-lint/bbox/mesh/clone gates do not apply and I did
not fail for their absence. I attacked the surfaces the kickoff named and **re-derived every
material claim from primary sources** rather than trusting the report or the two prior gates.

---

## The angle I captured (for a no-render task: my own re-derived evidence, not re-read)

Everything below I ran myself this pass. Scripts in the session scratchpad; commands quoted.

### Adversarial vitest against the pure generator (`lib/rotation.ts`) — I wrote 7 probes

I authored a throwaway `lib/__tests__/redteam_probe.test.ts`, ran it, extracted the findings,
then **deleted it** (working tree confirmed clean; only pipeline artifacts uncommitted). Results:

| Probe | What I tried to break | Result |
|---|---|---|
| P1 | Feed the eligible clubs/balls/chars in **reversed order** → does the seeded shuffle give a different unpinned lineup? | **YES it does** (hash `d53833d7` vs `ccdef177`). The Fisher–Yates shuffle is input-order-sensitive. **Mitigated:** the panel loads every catalog via `fetchRows` which `.order("row_id")` (contentData.ts:301), so prod always feeds a stable order → cross-session determinism holds. The shipped plan is fully pinned, so it is immune regardless. Latent fragility, **not a defect**. |
| P2 | Over-fill: can total picks EXCEED the quota total via fill? | **No.** 9/9 clubs at seeds 1/42/999/31337; `fillPlan` caps `left` at `quota.total − pins`, so fill never overshoots. |
| P3 | Pin a Legendary on a `Common:1` quota | 1 club (the pinned Legendary), quota rarity displaced. This is the documented "pins count by total first" behaviour (Deviation 7). Not a bug. |
| P4 | `characterQuota="Any:1;Legendary:1"` with 4 chars → double-fill the same ref? | **No** — 2 distinct refs (`char_shae` + `char_mike`); the `taken` set prevents any bucket re-picking. |
| P5 | Duplicate `pinnedClubs` (`x;x`) → two `shop_<rot>_x` rows with the same rowId? | The **generator does emit a colliding rowId and does NOT error**. Mitigated twice: validator **R1 blocks the publish** (`R1: pinnedClubs lists x twice`, contentValidate.ts:2135) and `upsertDraftRow` collapses the duplicate id. Never reaches prod. Not a blocker. |
| P6 | Re-seed (seed 1 → seed 77) with `existing` → are stale rows left live (two lineups)? | **No** — 9 rows deactivated, 13 live shop rows, and **no rowId is both live and stale-off**. `materializeLineup`'s stale-row deactivation works, and the panel **does pass `existing`** (lineup-workbench.tsx:202-207). |
| P7 | 6 prior rotations, `excludeWeeks=4` → is exclusion by rotation COUNT? | Yes — `recentRotations` slices the `n` most recent by `startUtc`; the pinned ref of recent rotations was excluded. |

### Prod state — re-derived via `Tools/content/rest.py` PostgrestClient (read-only, no writes)

```
catalog versions:  gacha_banners v12 · gacha_pools v5 · gacha_rates v6 · rotations v4 · shop_catalog v9
rotations rows:    total=54 active=52  (wk_2026_36/37 inactive)  materialized: wk_2026_36/37/38
shop wk_2026_38:   total=13 active=13  window 2026-09-14T00:00:00Z → 2026-09-21T00:00:00Z
                   categories {ball,character,club}  quantity="" on every row (one-ball, Dev 1)
banners weekly:    banner_wk_2026_38 active thr=50/Legendary · banner_wk_2026_36/37 inactive thr=50/Legendary (R3-consistent)
pity (Cratilo):    weekly counter=3 total=3  ·  banner_wk_2026_36 counter=0 total=2  ·  banner_wk_2026_37 counter=0 total=1
```

Every number matches the report and the kickoff. **The pity E2E is proven independently:**
the `weekly` counter=3 is 2 pulls on banner_36 + 1 on banner_37 under the shared key, while each
per-banner row holds its own cap count (2 and 1). The `weekly`-keyed row cannot exist unless the
migration is live — so the migration is definitively applied and correct on prod.

### Server refusals — re-derived with the synthetic uuid `00000000-0000-4000-8000-0000000f0f0f`

```
gacha_pull  banner_wk_2026_38 (pre-window):  {status:not_available, reason:window}
            banner_wk_2026_37 (archived):    {status:not_available, reason:inactive}
            banner_wk_2026_36 (archived):    {status:not_available, reason:inactive}
shop_buy    shop_wk_2026_38_ball_ace_attire (pre-window): {status:not_listed, reason:window}
            shop_wk_2026_36_club_driver_klyro_common (archived): {status:not_listed, reason:inactive}
            shop_ticket_standard_50 (Cesar off-sale):    {status:not_listed, reason:inactive}
```

Matches the report exactly (incl. the flagged copy nit — an inactive banner refuses `inactive`,
not `banner`). Deviation 5 (stale ticket draft aligned) is proven: the server refuses to sell it.

### Test suites + export — re-run myself

- `npx vitest run` rotation suites → **54 passed** (rotation.test.ts 34 + rotationValidate.test.ts 20).
- `python3 -m unittest discover Tools/content/tests` → **Ran 53 tests … OK**.
- `export_content.py --check` → **clean — no file would change, no catalog has drifted**.

### The shipped 52-week plan actually resolves — the one thing NO prior gate checked

I cross-referenced every pinned refId in all 54 `rotations.csv` rows against the LIVE
clubs (799) / balls (20) / characters (12) catalogs:

```
UNRESOLVED pins: 0     DUPLICATE pins (R1 would block): 0
default-ball pins: 0   starter-character pins: 0
Supreme character pins: wk_2027_01 → char_freda, wk_2027_18 → char_freda  (exactly the two §3.1a flags as a Cesar DECISION)
```

Every future week is generatable — no typo'd pin will block a publish months from now. The only
Supreme pins are the two the spec itself flagged for Cesar's decision (Freda). Clean.

---

## Migration (`2026_09_11_gacha_pity_group.sql`) — diffed against its base, hunk by hunk

`diff` of the `golfin_gacha_pull` body (base `2026_09_02_default_ball_guard.sql`, comments/blanks
stripped) shows the **ONLY** substantive changes are the pity-key hunks:
`v_pity_key` declaration; replay reads counter under key / total_pulls under banner; step-6 same;
step-11 primary upsert keyed by `v_pity_key` (DO UPDATE increments `total_pulls` from the TABLE,
not the stale read) + a second per-banner row `if v_pity_key <> v_banner`; and `'key'` in both
responses. **Counter, prize, cost, pool logic are byte-identical.** `v_bdata` is loaded (line 207)
before the replay key is computed (line 214) — no null-key bug. Both repo mirrors
(`admin-dashboard/migrations/` and `playlife/backend/migrations/`) are md5-identical for both the
pity and the seed migration.

---

## Validator R1–R4 (`lib/contentValidate.ts`) — read + reasoned

- **R1 overlap:** sort-by-start + compare-to-`prev.end`. I checked the classic nested-interval
  hole: whenever ANY two active rotations overlap, at least one adjacent pair also overlaps
  (a nested `[a,b]⊂[c,d]` always trips `a<d`), so it always raises ≥1 error → publish blocked.
  Sound. Also dedups pins (R1 line 2135), rejects commas, bad windows, bad grammar.
- **R2:** a tagged shop/banner row with a blank end OR an end after the rotation's end → error
  (line 422 `e === null || e > re`). A tagged row **cannot outlive its week**.
- **R3:** collects only `isActive && active` banners; two in one group with differing
  `pityThreshold`/`pityMinRarity` → error (blank≡0). Inactive ignored. Cannot publish a mismatch.
- **R4:** warn (never block) when a tagged ref was listed within `excludeWeeks` rotations.
- **`SHOP_REFERENCED_CATALOGS`** = `Array.from(new Set(Object.values(SHOP_CATEGORY_TO_CATALOG)))`
  — complete for every category value **by construction** (includes `ticket_types`, the fix).
- **Publish order** puts `rotations` last, but `publishCatalog` loads other catalogs as **drafts**
  (contentMutations.ts:483) and adds `rotations` for shop/banners (line 441), so R2 resolves the
  tag against the draft rotation regardless of order — no first-publish ordering bug (live E2E
  published all 5).

---

## Report integrity (Rule 6) — every asserted number re-derives, nothing fabricated

| Claim | Re-derived |
|---|---|
| 72 new DICT keys in `05f0f7da1` | `git show … grep -c` → **72** |
| Deploy stamp `f8063af6b` in built worker | `grep` route.js → **`"f8063af6b"`** |
| Migration mirrors byte-identical | md5 pity `4af2065a…` == ; seed `661eed04…` == |
| Seed byte-identity md5 `11a9211b…` | `tr -d '\r' < reference/rotations_seed.csv \| md5` → **`11a9211bc77e365d4d8dbe34fb42cea6`** |
| Reference is CRLF, 52 rows, wk_2026_38→wk_2027_36 (Dev 8) | 53 CR chars, 52 data rows, first/last confirmed |
| Shipped CSV = 52 plan + 2 archived (Dev 12) | 54 rows, 52 active; all 52 reference ids present |
| Banner withheld (not broken) when art unbundled + blank artUrl (Dev 11) | `GachaBannerArt.Resolve` ladder ends at step 0 → **null = WITHHELD** |

---

## The three break-attempts, and why each FAILED

1. **Break the generator behaviourally** (7 vitest probes): tried overshoot, `Any`+rarity
   double-fill, duplicate-pin collision, re-seed two-live-lineups, exclusion-by-count,
   input-order sensitivity, pins-beyond-quota. **Failed to break:** the only two real findings
   (P1 order-sensitivity, P5 dup-pin collision) are both mitigated before prod (row_id ordering;
   R1 + upsert) and neither affects the fully-pinned shipped plan.
2. **Bypass the validator to publish something unsafe**: tried two overlapping active rotations
   (nested-interval sort hole — proved it still fires an error), a tagged shop row outliving its
   week (R2 blocks blank/out-of-bounds end), and two grouped banners with different pity (R3
   blocks when both active). **No bypass found.**
3. **Find a non-pity change or a logic error in the migration**: full body diff shows ONLY the
   5 pity-key hunks; `total_pulls` for the cap is read/written under `v_banner` (a second row);
   the grouped counter is under the key; replay loads `v_bdata` before the key. **Verified live**
   on prod by the counter split (weekly=3 vs per-banner 2/1). **No defect.**

Spec-intent check: the SPEC goal — a weekly rotation of clubs/balls/characters in shop + a
rate-up gacha banner, authored as one unit, generated deterministically, published behind one
button, pity surviving the week — is met and live for `wk_2026_38`.

---

## Decisions surfaced to Cesar (already in the report; not review-gate blockers)

These are product/ops decisions, correctly surfaced by the implementer — they reach Cesar at his
final-approval step (which `ARCHITECT_REVIEW_PASS` advances to), so I do not FAIL/ESCALATE for them:

- **Deviation 1 — a ball listing delivers ONE ball, not ten.** The SPEC §3.3 wrote "quantity 10",
  but `golfin_shop_purchase` reads `quantity` for tickets only and rule G3-Q refuses any other
  value on a non-ticket row (a `quantity:10` ball row would be REFUSED at publish). So generated
  ball rows carry blank quantity at the ladder price (30/60/120…). This makes a ball ~10× more
  expensive per-ball than the spec's intent implied. Editable before publish; documented in
  `ECONOMY_MASTER §3` flagged for Architect wording. **Ten-ball delivery is a server change, not a
  content edit** — Cesar's call.
- **Deviation 11 — `banner_wk_2026_38` needs `artUrl` set before Monday 00:00 UTC.** Installed
  builds 2873/2874 don't bundle `GachaBanner_Weekly.png`, so on Monday the banner is **withheld**
  on those builds (safe — never a broken card) until Cesar uploads art via the admin. Operational
  deadline, not a defect.
- Ticket listing back on sale?, retire the two shop_stocking placeholders that collide with the
  week's picks?, the named `rotation_*` audit action?, ECONOMY_MASTER wording — all flagged.

---

## Verdict

I attacked the generator (7 probes), the validator (3 bypass attempts), the migration (full
body diff + live proof), the publish-order ordering, the shipped plan's 52-week pin resolution,
and re-derived all prod state, the pity E2E, the RPC refusals, both test suites, `--check`, the
deploy stamp, the DICT count, the migration mirrors and the seed hash — from primary sources, not
from the report or the two prior gates. **I could not find a blocker.** Every acceptance criterion
holds against evidence I generated this pass. The disclosed deviations are sound tradeoffs; the
two that are product decisions are surfaced for Cesar's final approval.

**ARCHITECT_REVIEW_PASS.**

---

## Files summary

| Path | Change |
|---|---|
| [`/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/weekly_rotation_admin/REDTEAM_REVIEW.md`](Docs/Specs/Active/weekly_rotation_admin/REDTEAM_REVIEW.md) | NEW — adversarial red-team verdict PASS. |
| [`/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/weekly_rotation_admin/STATUS.md`](Docs/Specs/Active/weekly_rotation_admin/STATUS.md) | UPDATED — `READY_FOR_REDTEAM` → `ARCHITECT_REVIEW_PASS`. |
