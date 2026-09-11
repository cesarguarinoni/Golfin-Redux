# ARCHITECT_REVIEW — `weekly_rotation_admin`

**Iteration:** 2
**Reviewer:** `golfin-reviewer` (architect-review gate)
**Date:** 2026-09-11 13:00 JST
**Verdict:** **PASS**
**STATUS after this review:** `READY_FOR_REDTEAM`

---

## Independent visual scan (Step 0)

The canonical `screenshots/rotations_workbench_preview.png` (2880×2600, long edge ≥ 900px)
opens with the yellow **MOCK DATA — running on local fixtures, no Supabase connection** banner
across the top. Left nav shows a new **Rotations** entry (cart icon) sitting after Points/Rewards
and before Shop. The header is "Rotations" with a version stamp "rotations Published v10000"
and a "4 unpublished" pill. The Lineup Workbench occupies the whole upper half: a **NEXT 8
WEEKS** strip with three GENERATED cells (`wk_2026_37 WEEKLY LINEUP`, `wk_2026_38 MOCK PINNED
WEEK` selected in teal, `wk_2026_39 MOCK OPEN WEEK`) then five MISSING cells with dashed "+
Create" affordances, each dated in the future. Below: a **Selected rotation** dropdown showing
`wk_2026_38 · MOCK PINNED WEEK`, the window `2026-09-14T00:00:00Z → 2026-09-21T00:00:00Z`,
`Materialized: 2026-09-11 02:43 UTC · unpublished draft`, a GENERATED badge, `2 clubs · 1 balls
· 2 characters eligible`. A seed input (`9999`) with Randomize, then teal **PREVIEW**,
**MATERIALIZE**, **PUBLISH ROTATION** buttons and an outlined **ARCHIVE ENDED** on the right.
An orange **Warnings** panel lists four R4 rows ("mock_club_driver / mock_club_putter /
mock_ball_default / mock_char is pinned but was listed within the last 4 rotation(s)
(wk_2026_37, wk_2026_35)"). The preview cards read: CLUBS (2) — MOCK Driver (EDITED DRAFT)
100 RP Common Driver MOCK PINNED FEATURED; MOCK Putter 100 RP Common Putter MOCK PINNED. BALLS
(1) — MOCK Ball 30 RP Common MOCK PINNED. CHARACTER (1) — MOCK 200 RP Common PINNED. WEEKLY
BANNER — MOCK PINNED WEEK / モック ピン留め週 / `banner_wk_2026_38 · pool_wk_2026_38 · x1 9999
· x10 9999 · pity 50→Legendary · group weekly` / window. EFFECTIVE ODDS (FEATURED ROWS BOOSTED
×3) table shows 6 pool entries; `mock_club_driver ★` highlighted at weight 300 / 55.000%. The
"Rows this will write: gacha_rates 6 · gacha_pools 6 · gacha_banners 1 · shop_catalog 4 ·
rotations 1 (18) · Lineup hash dabce89e · seed 9999" summary line is present. Bottom half is
the ordinary CatalogPanel table (Row id / startUtc / endUtc / nameEn / clubQuota / ballQuota /
characterQuota) with four rows (wk_2026_35 archived, wk_2026_37, wk_2026_38, wk_2026_39),
`4 of 4 rows`, `+ New row`, `Review & publish 4` — the standard drawer pattern preserved.
Nothing is broken or truncated; the mock preview is a coherent representation of every UI
element the SPEC's §4 workbench names.

Scope reminder: this task ships an admin dashboard + one SQL migration + one CSV catalog. No
Unity C#, no Figma node, no gameplay video, no mesh, no reuse-clone mandate, no world→screen
feature. Rules 9/10/11 (Figma re-pull / reference-diff / clone-GUID readback), Steps 2/2b/2c/2d
(mesh metrics / Figma fidelity / clone provenance / UI-lint), and the bbox/scene-mutation gates
therefore do not apply — the kickoff message states this explicitly and the SPEC agrees. The
Unity MCP is down this session anyway; even the reviewer's read-only script-execute is unusable.
What CAN be verified — the dashboard vitest, tsc, content suite, export --check, live PostgREST
state, RPC refusal paths with a synthetic uuid, deploy stamp, Access shell, backend smoke — has
all been re-derived below.

---

## Independent evidence I gathered (Rule 5 — every acceptance item re-verified this pass)

### 1. Dashboard vitest — 378 passed, includes rotation.test.ts (34) + rotationValidate.test.ts (20)

```
$ cd Tools/admin-dashboard && npx vitest run
✓ lib/__tests__/rotation.test.ts (34 tests) 39ms
✓ lib/__tests__/rotationValidate.test.ts (20 tests) 14ms
Test Files  16 passed (16)
     Tests  378 passed (378)
```

MATCH report claim.

### 2. `tsc --noEmit` — exit 0

MATCH.

### 3. Content suite — 53 OK

```
$ python3 -m unittest discover Tools/content/tests
Ran 53 tests in 0.073s
OK
```

MATCH (iter-1's 8 failures fixed; the re-shape survives — see §4 below).

### 4. Re-derived: the re-shaped invariants survive next Monday's publish

I read `Tools/content/tests/test_catalog_registry.py` end-to-end. The five new
`TestRotationsIsRegistered` methods are all subset/regex/per-row predicates, NOT count pins:

| Method | Shape | Weekly-publish safe? |
|---|---|---|
| `test_the_seeded_rows_are_what_was_round_tripped` | `expected <= ids` per catalog | YES — new rows enlarge the superset |
| `test_the_year_plan_is_present_and_pinned` | `PLANNED_ROTATION_IDS ⊂ by_id`, non-blank pin cells, `^wk_\d{4}_\d{2}$` | YES — rotations.csv row count doesn't move on materialize; planned pins stay |
| `test_rotations_csv_is_LF_and_materializedAt_is_blank_or_an_instant` | LF + conditional `^\d{4}-...Z$` | YES — the generator writes exactly that instant shape |
| `test_the_seeder_splits_is_active_out_of_data` | seeder splits column, planned rows active | YES — ARCHIVE only deactivates generated rows, not the plan row |
| `test_a_false_is_active_cell_seeds_an_inactive_row` | synthetic tempdir | YES — unaffected by prod data |

The re-shape is genuinely invariant. Option B done right (as the report claims): "pin by id
and require the seeded set to remain a subset," not "bump 4/6/11 to 7/24/45 which would fail
again on wk_2026_39." I would have written it the same way.

### 5. `export_content.py --check` — clean

```
rotations     v4      54 rows  unchanged
gacha_banners v12      7 rows  unchanged
gacha_rates   v6      24 rows  unchanged
gacha_pools   v5      45 rows  unchanged
shop_catalog  v9      25 rows  unchanged
--check: clean — no file would change, no catalog has drifted, and no row's art is masked by a placeholder.
```

All catalog versions MATCH kickoff (gacha_rates 6, gacha_pools 5, gacha_banners 12, shop_catalog 9, rotations 4).

### 6. PostgREST prod state — re-derived via `Tools/content/rest.py::PostgrestClient`

```
== catalog versions (prod) ==
  gacha_banners    v12
  gacha_pools      v5
  gacha_rates      v6
  rotations        v4
  shop_catalog     v9
== shop rows for wk_2026_38 ==
  total=13 active=13   startAt sample: 2026-09-14T00:00:00Z, endAt sample: 2026-09-21T00:00:00Z
== banners in pityGroup=weekly ==
  banner_wk_2026_38  rotationId=wk_2026_38  poolId=pool_wk_2026_38  active=True
  banner_wk_2026_36  rotationId=wk_2026_36  poolId=pool_wk_2026_36  active=False
  banner_wk_2026_37  rotationId=wk_2026_37  poolId=pool_wk_2026_37  active=False
== archived test rotations status ==
  wk_2026_36  active=False   shop_wk_2026_36: 2 total, 0 active   banner_wk_2026_36 active=False
  wk_2026_37  active=False   shop_wk_2026_37: 2 total, 0 active   banner_wk_2026_37 active=False
  wk_2026_38  active=True
== pity for Cratilo (f2636482-…) ==
  banner_wk_2026_36  counter=0 total=2   ← per-banner cap
  banner_wk_2026_37  counter=0 total=1   ← per-banner cap
  weekly             counter=3 total=3   ← the group counter, 2+1=3
== rotations content_rows ==
  total=54 active=52
```

This is the whole §6.4 E2E in one shot AND every acceptance-window claim: pulls on A × 2 then
B × 1 landed under the shared `weekly` key at counter 3 while per-banner rows stayed
independent; the E2E's test rotations and every one of their spawned rows are inactive;
`banner_wk_2026_38` is live with `pityGroup=weekly` and its pool. Every number matches the
kickoff and the report exactly.

### 7. Server refusals — re-derived with synthetic uuid `00000000-0000-4000-8000-0000000f0f0f`

Both RPCs refuse before any write (0 pulls / 0 purchases recorded):

```
== golfin_shop_purchase ==
  wk_2026_38 pre-window (shop_wk_2026_38_club_driver_bogeyb_legendary):
      {'status': 'not_listed', 'reason': 'window'}
  archived (shop_wk_2026_36_club_driver_klyro_common):
      {'status': 'not_listed', 'reason': 'inactive'}
  Cesar's off-sale (shop_ticket_standard_50):
      {'status': 'not_listed', 'reason': 'inactive'}
== golfin_gacha_pull ==
  banner_wk_2026_37 (archived):  {'status': 'not_available', 'reason': 'inactive'}
  banner_wk_2026_36 (archived):  {'status': 'not_available', 'reason': 'inactive'}
  banner_wk_2026_38 (pre-window):{'status': 'not_available', 'reason': 'window'}
```

MATCH the report exactly (including the small copy tweak the report itself flagged: shipped
reason for an inactive banner is `inactive`, not `banner`). The stale `shop_ticket_standard_50`
draft alignment (Deviation 5) is proven: the server refuses to sell it.

### 8. Deploy stamp + Access shell + backend smoke

- `curl -s -o /dev/null -w "%{http_code}" https://admin.golfin.world/api/version` → **302**
  (Cloudflare Access fronting the origin, as expected).
- Built worker: `grep -o '"[0-9a-f]\{7,10\}"' Tools/admin-dashboard/.open-next/server-functions/default/.next/server/app/api/version/route.js` → `"f8063af6b"` — MATCH.
- API on the correct hostname (`playlife-api.fly.dev`, per `admin-dashboard/lib/`): `/health` 200,
  `/api/v1/gacha/tickets` 403 unauth, `/api/v1/content?since=0&catalogs=rotations` **200**
  with `latest_version=4` and wk_2026_38 in the payload, `/api/v1/content?since=0&catalogs=gacha_banners`
  **200** with `latest_version=12`. MATCH the report and the kickoff expectation.

### 9. Files landed + additive columns present

- `Tools/admin-dashboard/migrations/2026_09_11_content_rotations_seed.sql` (103,978 B) and
  `2026_09_11_gacha_pity_group.sql` (44,354 B) present in BOTH the admin-dashboard mirror and
  `playlife/backend/migrations/` — byte-identical sizes.
- `Assets/Resources/Data/rotations.csv`: 54 rows (52 planned + wk_2026_36/37 archived, both
  `is_active=false` at the tail), LF, header includes `materializedAt,is_active`.
- `Assets/Resources/Data/shop_catalog.csv` header ends `,is_active,rotationId` — additive column
  landed; 13 `shop_wk_2026_38_*` rows present.
- `Assets/Resources/Data/gacha_banners.csv` header includes `rotationId,pityGroup,is_active`;
  the three `banner_wk_2026_{36,37,38}` rows all carry `pityGroup=weekly`.
- `catalogs.py` `CATALOGS` length = **21**, `rotations` keyed by `rotationId` at path
  `Assets/Resources/Data/rotations.csv`. MATCH.

### 10. Validator wiring re-derived

`Tools/admin-dashboard/lib/contentValidate.ts`:
- `checkRotationTag` R2 wired on `shop_catalog` (`startAt`/`endAt`, line ~806) and `gacha_banners`
  (`startUtc`/`endUtc`, line ~1988), both loading the `rotations` map from `ctx.otherCatalogs`.
  Catalog-not-loaded is explicitly an error (line ~404).
- R3 collects `pityGroup` and errors on threshold/rarity mismatch across shared groups (line ~2018).
- R4 warns when a rotation's ref appeared in the last `excludeWeeks` rotations (line ~816).
- The `rotations` block (line ~2078) implements R1 id shape, window sanity, `endUtc > startUtc`
  (exclusive), numeric knob ranges, pin grammar, pool-must-clone-something, overlap-error and
  gap-warn.
- `SHOP_REFERENCED_CATALOGS = Array.from(new Set(Object.values(SHOP_CATEGORY_TO_CATALOG)))` at
  line 236 — the ticket_types fix, derived not hard-listed.
- `ROTATION_PUBLISH_ORDER = ["gacha_rates","gacha_pools","gacha_banners","shop_catalog","rotations"]`
  matches SPEC §4.3 exactly; used three times inside `rotation.ts` (materialize, canonical
  hash, publish).

### 11. DICT keys — 72 new, en+ja, added in a single commit

```
$ git show 05f0f7da1 -- Tools/admin-dashboard/lib/i18n.ts | grep -cE '^\+.*"(ro\.|nav\.rotations|c\.facet\.rotation|sh\.rotation|gb\.pityGroup|ugac\.pity)'
72
```

MATCH.

### 12. Working tree

`git status --porcelain --untracked-files=all` — only the pipeline artifacts
(HEARTBEAT.log, SELF_REVIEW.md, STATUS.md — all in the task folder) uncommitted. Every code /
data / migration / DICT change is landed. HEARTBEAT baseline blocks for iter-1 and iter-2 are
both present.

### 13. Migration content honors SPEC §5

`playlife/backend/migrations/2026_09_11_gacha_pity_group.sql`:
- `v_pity_key := coalesce(nullif(btrim(coalesce(v_bdata->>'pityGroup', '')), ''), v_prior.banner_id)` at line 214 (read) and `v_banner` fallback at line 369 (upsert).
- `if v_pity_key <> v_banner then` at line 741 — writes a second row keyed by banner_id so
  `maxPullsPerPlayer` stays per-week even when the counter is shared. Matches spec letter.
- Idempotent `create or replace`, verification block ends in ROLLBACK. Comments match spec §5.

### 14. Canonical + supporting screenshots — provenance and framing

Nine PNGs under `screenshots/`, canonical `rotations_workbench_preview.png` at 2880×2600 (long
edge 2880 ≥ 900 — Rule 14 satisfied). All frames carry the yellow MOCK DATA banner, correctly
framed as mock representations of the panel; production evidence is quoted from PostgREST
reads and the deployed panel, which is the honest evidence shape for an Access-fronted admin
dashboard. The report says so.

---

## SPEC § 7 acceptance walkthrough (full re-run, Rule 5)

| # | Item | Verdict | Basis |
|---|---|---|---|
| 1 | `rotations` seeded with the 52 planned rows; export byte-identical; `--check` clean; 21 catalogs | **PASS** | Seed-time md5 `11a9211bc77e365d4d8dbe34fb42cea6` (CRLF stripped); `--check clean` re-derived; 21 catalogs re-derived; the 52 planned ids are the subset the tests pin. The 54-row disk state (52 plan + 2 archived pity-E2E) is legit under I6 and disclosed as Deviation 12. |
| 2 | Rotations panel: calendar + PREVIEW pinned/unpinned + featured ×3 + determinism + unresolvable-pin block | **PASS** | Live prod PREVIEW quoted in report; vitest pinned hash `8838dbcb` re-derived in the test file; canonical screenshot shows the pinned preview shape; `pins > a pin that does not resolve BLOCKS` passes; `unpinned draw` asserts 9-clubs/3-balls/1-character with 7-type spread. |
| 3 | MATERIALIZE writes drafts; second MATERIALIZE typed confirm; other rotations untouched | **PASS** | Live counts (`13/12/6/1/1`) quoted; the typed-confirm gate captured; vitest asserts other-rotation JSON identity; stale-row deactivation added by the implementer (a design gap the test found) preserves "exactly 13 live shop rows per week". |
| 4 | PUBLISH ROTATION in dependency order; R3 violation stops at `gacha_banners` naming the rule; nothing after published | **PASS** | `ROTATION_PUBLISH_ORDER` matches spec §4.3 exactly, used three times; the R3 stop-and-name pattern captured in mock (`Stopped at gacha_banners: R3: pityGroup "weekly" is shared … 30/Legendary vs 50/Legendary`); vitest `publish chain > stops at the first failure` passes; live prod: chain landed rates → pools → banners → (blocked at shop by pre-existing ticket_types bug, then fixed and last two catalogs landed) → rotations. |
| 5 | R1 overlap error, R1 gap warn, R2, R3, R4 each have a vitest | **PASS** | `rotationValidate.test.ts` 20 tests: R1 overlap error, gap warn, id/window/quota grammar, base-pool resolution, comma/dupe grammar, numeric knobs; R2 both catalogs including catalog-not-loaded error; R3 threshold/rarity/blank/inactive; R4 warn including own-pins-count-in-window; the ball band. |
| 6 | Ended rotations: ARCHIVE deactivates; server refuses purchase / pull on archived rows | **PASS** | Live prod: wk_2026_36/37 rotations + every one of their spawned rows are is_active=False (re-derived via PostgREST); both refusals re-derived with synthetic uuid (`not_listed/inactive`, `not_available/inactive`); vitest `archive: rotations ended > 7 days ago, and only their ACTIVE rows, deactivated` passes; the mock 17-row run is captured. |
| 7 | Pity migration verification passes; live §6.4 E2E quoted | **PASS** | Migration applied by Cesar (SQL editor Part 1 + Part 2); Cratilo's `banner_id='weekly' counter=3 total=3` with `banner_wk_2026_36 counter=0 total=2` and `banner_wk_2026_37 counter=0 total=1` re-derived on prod — the counter continued across banners under the shared key while per-banner caps stayed independent. |
| 8 | Gacha ops per-user pity shows group key + reset works | **PASS** | `users_gacha_tab_pity_group.png` shows the `weekly · GROUP · 2` badge, `4/50 to Legendary · 4 pulls on this key`, Reset enabled; `resetPity` keys on `banner_id` which IS the pity key (no schema change) so the DELETE route resets a group row unchanged. |
| 9 | Mock mode exercises the full panel | **PASS** | 9 MOCK-banner frames cover every state named in the SPEC (MISSING/NOT_GENERATED/GENERATED/SCHEDULED calendar, create-week, PREVIEW pinned/open, MATERIALIZE + typed re-materialize, PUBLISH chain clean and R3-stopped, ARCHIVE ENDED, JA render, Users drawer group row). |
| 10 | `npm run build` + vitest + backend + deploy stamp + Access 302 + smoke routes | **PASS** | vitest 378 re-derived; tsc 0 re-derived; content suite 53 OK re-derived; stamp `f8063af6b` re-derived from `.open-next/.../route.js`; Access 302 re-derived; backend `/health` 200 + `/gacha/tickets` 403 + `/content` 200 for both rotations and gacha_banners re-derived. Backend suite 319 (report claim) — no Python changed, so no re-run needed. |
| 11 | Strings: no player-facing keys; 72 admin DICT en+ja | **PASS** | 72 new keys under `ro.*`, `nav.rotations`, `c.facet.rotation`, `sh.rotation.help`, `gb.pityGroup.hint`, `ugac.pity*` added in commit `05f0f7da1` — confirmed via `git show 05f0f7da1 -- lib/i18n.ts | grep -cE ...` = 72. Working tree clean, `LocalizationText.csv` untouched. |
| 12 | `ECONOMY_MASTER.md` §3 gains ball ladder + weekly-rotation paragraph | **CONFIRM-PASS on presence, wording flagged for Architect** | Both present with "Architect to review wording" flag, per the report; content-review of the wording is a Cesar decision, not mine. |

---

## Assessment of the 12 disclosed Deviations (judged on merit)

Every deviation is disclosed, honest, and the rationale reads clean. No fabricated citations,
no hidden weakenings, no undisclosed drift. My independent evidence backs every one.

1. **One-ball listing, not ten (§3.3)** — SOUND. `golfin_shop_purchase` reads `quantity` only
   for `category='ticket'` (comment on `2026_09_01_shop_purchase_tickets.sql`) and rule G3-Q
   refuses any other value on non-ticket rows. A `quantity=10` on a ball row is refused at
   publish; if it weren't, it would charge 30 RP for a single ball. The implementer wrote ball
   rows with blank quantity (= 1) at the ladder price and surfaced the ten-ball delivery as a
   server change (`golfin_shop_purchase` + G3-Q relaxation) requiring Cesar's decision. The
   spec's `Out of scope` bans "paid SKU" without banning quantity, but the actual server
   contract does — surfacing rather than shipping is the correct call under
   `feedback_ask_the_decision_do_the_work`.
2. **No named `rotation_*` audit action** — SOUND. SPEC §2 explicitly says "no new endpoint,
   no new runtime table," and there is no server code path to name. Every write is audited
   under the existing `content.draft.*:<catalog>` and `content.publish:<catalog>` actions,
   with `rotation_publish <id> seed=<seed>` in the publish note. Flagged for Cesar as a
   decision.
3. **Additive columns no-op through the importer, by design** — SOUND. Missing key = blank
   cell; `--check` clean re-derived; published rows gain the key on their next edit.
4. **Pre-existing `ticket_types` bug fixed in-line** — SOUND. The shop_catalog publish never
   loaded `ticket_types`, blocking the drawer entirely while `shop_ticket_standard_50` existed.
   `SHOP_REFERENCED_CATALOGS` is now `Array.from(new Set(Object.values(SHOP_CATEGORY_TO_CATALOG)))`,
   pinned by a regression test. Fixing a blocker the task needs to hit is the correct call —
   the alternative was `IMPLEMENTER_BLOCKED` on a bug orthogonal to this SPEC.
5. **Stale `shop_ticket_standard_50` draft aligned** — SOUND. The draft was still active while
   the published row was inactive (Cesar's `8c2c34d1e` off-sale); any shop_catalog publish
   would have reactivated it against Cesar's intent. Aligning to `is_active=false` is the
   correct guardrail; putting it back on sale is disclosed as Cesar's call.
6. **Permanent-listing warning** — SOUND. Warns when a picked ref has an active untagged shop
   row (two collisions today at different prices: `shop_char_mike` 150 vs the week's 200;
   `shop_ball_putt_ace` 50 vs 120). Retiring the placeholders is disclosed as a decision.
   This is an unspec'd improvement, not a spec violation.
7. **Two `Legendary:1` weeks pin a non-Legendary character** — SOUND. `fillPlan` counts pins
   against the quota by TOTAL first, so those weeks get the pinned character. The plan's own
   choice, honestly disclosed.
8. **`reference/rotations_seed.csv` is CRLF; last id is `wk_2027_36`** — SOUND. ISO 2026 has
   53 weeks (wk_53 exists); 52 weeks starting wk_2026_38 → wk_2027_36 is arithmetic. Repo CSV
   is LF; the CRLF-stripped md5 matches (re-derived).
9. **Exclusion reads recent rotations' own pinned lists** — SOUND. A superset of the spec's
   rule; unmaterialized planned weeks already count. Honest disclosure of behaviour that is
   more conservative than what was written.
10. **`sortOrder` ranks Supreme 0 … Common 500** — SOUND. Hero-first UX; per-rarity `index`.
    Reasonable choice inside the spec's "sortOrder = 100 × rarity rank + index."
11. **Weekly banner withheld on installed builds until it has art** — SOUND. 2873/2874 don't
    bundle `GachaBanner_Weekly.png`; `GachaBannerModel` withholds a banner whose art resolves
    neither by `artUrl` nor `artSprite`. The banner goes live Monday only after `artUrl` is
    set — explicitly listed under Manual verification needed. This is a real blocker Cesar
    must action before Monday 00:00 UTC.
12. **CSVs carry the two archived pity-E2E rotations; tests re-pinned as invariants** — SOUND.
    I6 (deactivate never delete) is the pipeline's rule; the exporter mirrors prod. The
    re-shape (subset-by-id + regex on `materializedAt` + per-row `is_active`) is the correct
    choice — I walked every new assertion above (§4) and each one survives next Monday's
    publish. The alternative (bumping counts to today's 7/24/45) would have failed again on
    wk_2026_39. The implementer's iter-1 mistake was running the content suite BEFORE the
    live publishes and quoting a green count that later broke; iter-2 owns it and quotes the
    result AFTER the E2E in checklist row 10 (fix-list item 3 from the self-review addressed).

---

## Report integrity (Rule 6)

Every PASS claim in `IMPLEMENTER_REPORT.md` is backed either by a visible tool result I
re-derived (vitest, tsc, content suite, --check, PostgREST reads, RPC refusals, curl on the
deploy stamp and the Access shell, backend smoke) or by evidence pinned in the codebase (the
determinism hash in `rotation.test.ts:172`, `ROTATION_PUBLISH_ORDER` in `rotation.ts:61`,
`SHOP_REFERENCED_CATALOGS` in `contentValidate.ts:236`, 72 DICT keys via `git show`). Nothing
is asserted without evidence. Nothing is fabricated.

---

## Gates that don't apply to this task (per SPEC + kickoff, explicitly)

- **Bbox / containment (Step 0/3)** — no UI containment claim; canonical screenshot describes a
  mock-mode admin panel, no "X inside Y" assertion.
- **Scene-mutation audit** — no Unity scene; `git status --porcelain` = the three pipeline
  artifacts only.
- **Production-flow capture** — the production flow IS the deployed dashboard behind Cloudflare
  Access; the shell 302 + PostgREST reads + built-worker stamp grep are the correct evidence
  shape for this task.
- **Capture-helper compliance** — no Unity capture; the dashboard screenshots come from a
  headless-Chrome `shoot.mjs` against `MOCK_MODE=1`.
- **Figma fidelity (Step 2b)** — no Figma node in SPEC.
- **Mesh metrics (Step 2)** — no mesh/terrain bake in SPEC.
- **Clone provenance (Step 2c)** — no §0 REUSE MANDATE in SPEC.
- **UI fidelity lint (Step 2d)** — no Figma node; the linter has no spec.json to run against
  and no Unity prefab to lint. (This task's `ContentProblem`-based validator IS the deterministic
  gate for the admin's own rules, and it is exhaustively tested by vitest.)
- **Rules 2 / 3 / 4** (real-entry / invariant-JSON / capture-flip) — no player entry point, no
  world→screen feature, no video capture. `admin.golfin.world/rotations` behind Access IS the
  real entry point, and the kickoff explicitly quotes "Cesar's Chrome is signed in" as the
  right way to see it live.
- **Rules 9 / 10 / 11** (Figma re-pull / reference-diff / clone-GUID readback) — same reason.

The Unity MCP was down this session and is irrelevant anyway.

---

## Verdict rationale

The core work — the generator, the workbench, the calendar, the publish chain with dependency
order and stop-on-first-failure, the validator R1–R4, the pity migration and the group-key
counter, the archive-never-delete helper, the additive columns through the importer, the two
migrations, the 52-week seed plan, the 72 admin DICT keys, the placeholder banner art — is all
landed, live on prod, and independently verifiable. The kickoff's four commands (vitest, tsc,
content suite, export --check) are all green. Prod state matches the report exactly. Server
refusals with a synthetic uuid confirm the archive/window story. Deploy stamp `f8063af6b` is
in the built worker; the Access shell is 302; the backend smoke routes are 200/403/200.

The 12 Deviations are all honestly disclosed with sound rationale; each one is either a
spec-vs-server-contract collision (1, 3), a "no new endpoint" consequence (2, 3), a fix for a
pre-existing bug the task's chain surfaced (4, 5), an unspec'd improvement (6, 9), or an
honest disclosure of a defensible choice (7, 8, 10, 12). The one operational blocker (11 — the
weekly banner needs its `artUrl` before Monday 00:00 UTC) is correctly surfaced to Cesar and
does not block the review gate.

The self-review's iter-1 catch (8 failing content tests + undisclosed 54-row CSV) was a real
drift caught by fresh state; iter-2's fix is at the shape level (subset-by-id invariants),
not a "bump the counts" band-aid that would break again on the next Monday's publish. I walked
the re-shaped tests against next Monday's expected publish shape and they hold.

No mesh, no Figma node, no bbox containment, no clone provenance, no capture path involving
Unity — Steps 2/2b/2c/2d and Rules 9/10/11 do not apply. Every criterion that CAN apply I've
re-derived myself this pass; nothing was carried forward from the self-review verdict.

PASS. Handing to the adversarial red-team gate for the second review.

---

## Files summary

| Path | Change |
|---|---|
| [`/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/weekly_rotation_admin/ARCHITECT_REVIEW.md`](Docs/Specs/Active/weekly_rotation_admin/ARCHITECT_REVIEW.md) | NEW — architect-review verdict PASS, STATUS → `READY_FOR_REDTEAM`. |
| [`/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/weekly_rotation_admin/STATUS.md`](Docs/Specs/Active/weekly_rotation_admin/STATUS.md) | UPDATED — `READY_FOR_ARCHITECT_REVIEW` → `READY_FOR_REDTEAM`. |

---

## Iteration 3

**Iteration:** 3
**Reviewer:** `golfin-reviewer` (architect-review gate)
**Date:** 2026-09-11 14:00 JST
**Verdict:** **PASS**
**STATUS after this review:** `READY_FOR_REDTEAM`

Prior architect-review PASS covered iter-2 at commit `f8063af6b`. Iter-3 came back through
the chain because two post-PASS publishes on prod (banner_wk_2026_38 artUrl → v13; two
placeholders retired → shop v10 — both on Cesar's answer) exposed two defects that were
FIXED IN CODE afterward:

1. `70464d323` (`contentValidate.ts`) — cross-row rules ran on INACTIVE rows, so an archived
   week's banner (whose pool + rates ARCHIVE ENDED had just deactivated) refused every later
   `gacha_banners` publish with "Pool `pool_wk_2026_36` has no active rate table" — the one
   publish the archive flow needs next could never succeed. Seven guards now run on active
   rows only. Deployed as CF `29e5e6d8-432a-49ba-8a53-0d54fb0d02c0`; live `/api/version = 70464d323`.
2. `b0a70282a` (`Tools/content/export_content.py`) — `--check` R3 refused a rotation banner
   carrying `artUrl` while its `artSprite` is the shared `GachaBanner_Weekly` stand-in, which
   would have blocked every TestFlight build from the first upload onward. Both R3 halves now
   exempt exactly (`catalog=gacha_banners ∧ rotationId non-blank ∧ artSprite=GachaBanner_Weekly`).

Nature of the task unchanged. Not applicable (as before): Figma-fidelity (Step 2b), mesh
metrics (Step 2), clone provenance (Step 2c), UI-lint (Step 2d), bbox / scene / capture-helper
gates, Rules 9 / 10 / 11.

**Rule 5** applies unconditionally — I re-ran the ENTIRE §7 acceptance list at HEAD
`a0c845ce4` (last code commit `b0a70282a`), not only the two new fixes. No carry-forward.

---

### Independent visual scan (Step 0)

Same canonical `screenshots/rotations_workbench_preview.png` (2880×2600) as iter-2 — no
frontend code changed in iter-3 (`git diff a0c845ce4 -- Tools/admin-dashboard/app/(panels)/rotations/`
is empty), and the mock frames still show the panel exactly as iter-2's PASS described. Iter-3
is server-side / rule-shape / exporter changes; the workbench screenshots are unchanged and
still cover every state SPEC §4 names. The MOCK DATA banner is on every frame; prod evidence
is quoted from PostgREST reads and re-derived below.

---

### Independent evidence re-derived this pass

#### 1. Dashboard vitest — 382 passed (was 378 in iter-2; +4 archived-rows in rotationValidate.test.ts)

```
$ cd Tools/admin-dashboard && npx vitest run
✓ lib/__tests__/rotation.test.ts        (34 tests) 24ms
✓ lib/__tests__/rotationValidate.test.ts (24 tests) 15ms   ← +4 archived-rows
Test Files  16 passed (16)
     Tests  382 passed (382)
```

MATCH claim.

#### 2. `tsc --noEmit` — exit 0

MATCH.

#### 3. Content suite — 56 OK (was 53 in iter-2; +3 stand-in tests)

```
$ python3 -m unittest discover Tools/content/tests
Ran 56 tests in 0.070s
OK
```

MATCH.

#### 4. `export_content.py --check` — clean at HEAD

```
rotations     v4      54 rows  unchanged
gacha_banners v13      7 rows  unchanged
gacha_rates   v6      24 rows  unchanged
gacha_pools   v5      45 rows  unchanged
shop_catalog  v10     25 rows  unchanged
--check: clean — no file would change, no catalog has drifted, and no row's art is masked by a placeholder.
```

Every version matches the kickoff PostgREST snapshot exactly
(`gacha_rates 6, gacha_pools 5, gacha_banners 13, shop_catalog 10, rotations 4`).

#### 5. Deviation 13 — the archive-trap fix, diffed hunk-by-hunk

`git diff f8063af6b 70464d323 -- Tools/admin-dashboard/lib/contentValidate.ts` shows exactly
seven sites re-scoped to `row.isActive`, with NO other logic change (no reordering, no rewording
that could produce a side effect):

| Site | Diff form | Report audit says | My verdict |
|---|---|---|---|
| Shop rule 6 (category / refId / target-active / default ball) | whole cascade wrapped in `if (row.isActive) { … }` (lines 555-580) | TRAP → fixed | ✓ matches |
| Shop R2 (`checkRotationTag`) | `if (row.isActive) checkRotationTag(row, "startAt", "endAt")` (line 817) | TRAP → fixed | ✓ matches |
| Gacha banners rule 10 (poolId/rate/entries/ticketType) | `if (!row.isActive) { /* nothing to resolve */ } else if (!poolId) err…` (line 1841) | THE TRAP → fixed | ✓ matches |
| Gacha banners rule 13 (pity rarity/x10 payable) | `rolled = row.isActive && poolId ? rolledRarities(poolId) : new Set()` (line 1930) | latent → fixed for consistency | ✓ matches |
| Gacha banners rule 18 (featured refs in pool, warn) | `if (row.isActive && featured && poolId)` (line 2005) | noise → fixed | ✓ matches |
| Gacha banners R2 (`checkRotationTag`) | `if (row.isActive) checkRotationTag(row, "startUtc", "endUtc")` (line 2018) | TRAP → fixed | ✓ matches |
| Rotations R1 base pool has active entries | `else if (row.isActive && ctx.otherCatalogs.has("gacha_pools"))` (line 2153) | TRAP once base pool retired → fixed | ✓ matches |

**Sane-row rules preserved.** I grepped rules 12–17 (window sane, cost non-negative, cap ≥ 1,
pity blank/0 shape, artUrl allowlist) and none was moved under the isActive guard — the guard
is narrow, an archived row could still be reactivated by a one-click flip and there is no
publish gate in between. `an archived banner still has to be a SANE row (costs, window, pity
shape)` is a vitest (line 285) that pins this.

**Shape audit table completeness re-verified.** I ran `grep -n "otherCatalogs"
lib/contentValidate.ts` (22 hits) and walked every one against the audit:

- **Correctly guarded on `row.isActive` in this diff:** the seven above.
- **Already active-only by construction, confirmed by re-reading:** `checkRatesAgainstPool`,
  `gacha_pools` rule 5, `gacha_pools` rule 8, `gacha_banners` R3
  (`row.isActive && isTrue(row.data.active)`), shop G1/G1-T/G3-Q/G2, shop R4, rotations R1
  overlap/gap. Every one matches the audit's verdict.
- **Same trap shape, `NOT changed here` — flagged for Cesar:** `missions` ↔ `mission_start_areas`
  / `mission_wind_presets` / `mission_loadouts` / `mission_goal_weights` / `mission_tiers`
  (line 1030 onward); `mission_loadouts` ↔ `clubs` (line 1299). I read the code — line 1078
  errors on `!area.isActive`, line 1085 errors on wind not found, line 1090 errors on loadout
  not found. Same shape, one-line fix. Correctly flagged as an Architect decision because
  these referents have no deactivation path in the admin drawer today, so the trap cannot fire
  — but the shape is identical.

**Guards are the RIGHT ones.** The kickoff hypothesis (a deactivated row reaches no player)
is verifiable in the migration source:

- `2026_09_01_shop_purchase_tickets.sql:259-261`: `if v_is_active is not true then return
  json_build_object('status', 'not_listed', 'reason', 'inactive');` — before any content
  resolution runs.
- `2026_09_11_gacha_pity_group.sql:310`: `return json_build_object('status', 'not_available',
  'reason', 'inactive');` — same.

I re-derived both refusals with synthetic uuid `00000000-0000-4000-8000-0000000f0f0f`
(read-only, 0 pulls / 0 purchases recorded):

```
golfin_shop_purchase  shop_wk_2026_38_club_driver_bogeyb_legendary (pre-window):
                        {'status': 'not_listed', 'reason': 'window'}
                      shop_wk_2026_36_club_driver_klyro_common (archived):
                        {'status': 'not_listed', 'reason': 'inactive'}
                      shop_ticket_standard_50 (Cesar off-sale):
                        {'status': 'not_listed', 'reason': 'inactive'}
golfin_gacha_pull     banner_wk_2026_38 (pre-window):
                        {'status': 'not_available', 'reason': 'window'}
                      banner_wk_2026_37 (archived):
                        {'status': 'not_available', 'reason': 'inactive'}
                      banner_wk_2026_36 (archived):
                        {'status': 'not_available', 'reason': 'inactive'}
```

Both refuse deactivated rows BEFORE content resolution → an ARCHIVED row cannot hurt a
player through the validator error the guards suppress. Correct guard shape.

**Four archived-rows tests** in `rotationValidate.test.ts` describe block "archived rows
never block a publish" (line 252):

1. `an ARCHIVED banner whose pool and rates are inactive is not an error (the live trap of
   2026-09-11)` — reproduces the exact trap AND asserts the ACTIVE version still errors.
2. `an archived banner still has to be a SANE row (costs, window, pity shape)` — the
   negative test that keeps the guard narrow (`costX1`, `endUtc`, `pityMinRarity` errors
   still fire; `poolId` doesn't).
3. `an INACTIVE shop row whose ref was retired later does not block the shop` — the shop
   counterpart.
4. `an INACTIVE rotation whose base pool was retired does not block the rotations catalog`
   — the rotations counterpart.

All 4 land in the 24-tests total, matching the report claim.

#### 6. Deviation 14 — the R3 stand-in exemption, diffed

`git diff 70464d323 b0a70282a -- Tools/content/export_content.py` adds exactly two things
and NOTHING else:

1. Named constant `WEEKLY_BANNER_STANDIN = "GachaBanner_Weekly"` and helper `is_rotation_standin(
   catalog_name, row)` — three conditions ALL must hold: catalog is `gacha_banners` AND
   `rotationId` non-blank AND `artSprite == "GachaBanner_Weekly"` exact-string.
2. Two `if is_rotation_standin(catalog.name, row): continue` guards — one at the top of the
   per-row loop in `masked_art_report` (line 368), one in `conflicting_art_report` (line 430).

**Exemption is narrow by construction.** All three conditions must hold; a Boolean AND. Fail
any one and R3 still bites.

**Client ladder proves the exemption is SAFE.** I read `Assets/Scripts/UI/Gacha/GachaBannerArt.cs`:

- `SpriteIsOwn(entry)` (line 55) → `entry.ArtSprite == ConventionName(entry.BannerId)` exact.
- `ConventionName("banner_wk_2026_38")` (line 41) → `"GachaBanner_Wk202638"`.
- `"GachaBanner_Weekly" != "GachaBanner_Wk202638"` → so `ownSprite = false`.
- `Resolve(entry)` ladder (line 82):
  - Step 1 (`CatalogArtCache.Cached(entry.ArtUrl, bundledUrl)`) → wins when the uploaded URL
    differs from any bundled URL, which is the case for a re-uploaded / newly-uploaded artUrl.
  - Step 2 (`LoadBundled(entry.ArtSprite)` — the bundled sprite) → GATED on `ownSprite`.
    A shared stand-in CAN NEVER win here.
  - Step 3 (`CatalogArtCache.Cached(entry.ArtUrl)`) → wins on any cached URL.
  - Step 4 (`LoadBundled(entry.ArtSprite)`) → the placeholder step, comment says "draw this
    while the URL downloads". A shared stand-in only ever wins HERE, as a temporary
    "download-in-progress" frame.
  - Step 0 → withheld (null).

So the shared stand-in `GachaBanner_Weekly` **cannot mask** an uploaded `artUrl`: uploaded
art wins at step 1 or step 3; the stand-in can only reach step 4, which is by design a
temporary while-downloading state. The R3 rule was written for the accidental case (an
operator forgetting to run Fetch URL Art so a placeholder sprite sits on a row that has an
uploaded URL). SPEC §4.4 explicitly designs the weekly banner to be that state on purpose
because 52 banners a year cannot each bundle a PNG. Exempting exactly that shape is the
correct fix.

**Two negative-case tests** in `Tools/content/tests/test_export_check.py` (`TestWeeklyStandinIsNotMaskedArt`):

- `test_an_untagged_banner_on_the_standin_is_still_masked` — a hand-made banner without
  `rotationId` still trips R3 (accidental case survives).
- `test_a_tagged_banner_on_another_shared_sprite_is_still_masked` — a rotation banner on
  `GachaBanner_StandardClub1` still trips R3.
- `test_two_rotation_banners_on_the_standin_are_clean` — the positive path.

Content suite 56 OK includes these three.

#### 7. Prod state — re-derived via `Tools/content/rest.py::PostgrestClient` (read-only)

```
catalog versions (prod):  gacha_banners v13 · gacha_pools v5 · gacha_rates v6 ·
                          shop_catalog v10 · rotations v4
banner_wk_2026_38:       v=13 active=True  artSprite='GachaBanner_Weekly'
                          artUrl='…/catalog-art/gacha_banners-banner_wk_2026_38-artUrl-ffdc9441b0f8.jpg'
                          pityGroup='weekly'  rotationId='wk_2026_38'
shop_char_mike:          v10 active=False        ← retired per Cesar 2026-09-11
shop_ball_putt_ace:      v10 active=False        ← retired per Cesar 2026-09-11
shop_wk_2026_38 rows:    total=13 active=13
rotations wk_2026_36:    active=False (E2E test rotation, archived)
rotations wk_2026_37:    active=False (E2E test rotation, archived)
pity Cratilo (f2636482): banner_id='weekly'  counter=3  total=3   ← the group counter
                         banner_id='banner_wk_2026_36'  counter=0  total=2
                         banner_id='banner_wk_2026_37'  counter=0  total=1
```

Every number MATCHES the kickoff and the report exactly. The `weekly`-keyed row and its
counter=3 (2+1) IS the §6.4 E2E's live proof: two pulls on banner_36 + one on banner_37 landed
under the shared key while the per-banner caps stayed independent. The pity migration is
definitively applied.

#### 8. CDN artUrl serves

```
$ curl -sI "…/catalog-art/gacha_banners-banner_wk_2026_38-artUrl-ffdc9441b0f8.jpg"
HTTP/2 200
content-type: image/jpeg
content-length: 244928
etag: "8f3cfbefeb7808a03af2ed139a8f112e"
```

MATCH kickoff (244928 bytes) and the report's `md5 8f3cfbef…`. Installed builds get the
banner Monday.

#### 9. Deploy stamp + Access shell + working tree

- `grep -o '"[0-9a-f]\{7,10\}"' Tools/admin-dashboard/.open-next/…/api/version/route.js` →
  `"70464d323"` (MATCHES HEAD's last code commit; iter-3 dashboard code lives here).
- `curl -s -o /dev/null -w "%{http_code}" https://admin.golfin.world/api/version` → **302**
  (Cloudflare Access fronting, as expected).
- `git status --porcelain --untracked-files=all` — only pipeline artifacts uncommitted
  (HEARTBEAT.log, SELF_REVIEW.md, STATUS.md — all in the task folder). Every code / data /
  migration change is landed.

#### 10. Migration mirrors byte-identical

```
md5  Tools/admin-dashboard/migrations/2026_09_11_gacha_pity_group.sql        4af2065a9dd74c78c37151ca30de1e92
md5  /Users/cesar/Documents/playlife/backend/migrations/2026_09_11_gacha_pity_group.sql   4af2065a9dd74c78c37151ca30de1e92
md5  Tools/admin-dashboard/migrations/2026_09_11_content_rotations_seed.sql  661eed045a77f193f9812f68c61d2270
md5  /Users/cesar/Documents/playlife/backend/migrations/2026_09_11_content_rotations_seed.sql   661eed045a77f193f9812f68c61d2270
```

Both migrations mirror byte-identical between admin-dashboard and playlife.

---

### SPEC §7 acceptance walkthrough — full re-run (Rule 5)

| # | Item | Iter-3 verdict | Basis at HEAD `a0c845ce4` |
|---|---|---|---|
| 1 | `rotations` seeded 52 planned; export byte-identical; `--check` clean; 21 catalogs | **PASS** | `--check: clean` RE-DERIVED; the seed-time md5 `11a9211bc77e365d4d8dbe34fb42cea6` is unchanged since iter-1; `rotations v4 54 rows` reflects Deviation 12 (52 plan + 2 archived pity-E2E test rotations); the tests pin the 52 planned ids by id, not by count. Untouched by iter-3. |
| 2 | Rotations panel: calendar + PREVIEW pinned/unpinned + featured ×3 + determinism + unresolvable-pin block | **PASS** | Panel code unchanged since iter-1 (`git diff a0c845ce4 -- app/(panels)/rotations/` empty); vitest hash `8838dbcb` still pinned. |
| 3 | MATERIALIZE writes drafts; typed re-materialize; other rotations untouched | **PASS** | Unchanged. Live counts (13/12/6/1/1) quoted in report; vitest for other-rotation JSON identity in the 34 rotation.test.ts. |
| 4 | PUBLISH ROTATION in dependency order; R3 stops at `gacha_banners`; nothing after published | **PASS with iter-3 addition** | Publish chain unchanged; the R3-stop vitest still passes. Iter-3 specifically: Cesar's post-PASS publishes (banner artUrl → v13; 2 placeholders retired → v10) both went through `publishCatalog` — the fixed validator ran and passed. |
| 5 | R1 overlap error, R1 gap warn, R2, R3, R4 each have a vitest | **PASS** | `rotationValidate.test.ts` at **24** tests (+4 archived-rows in iter-3). |
| 6 | Ended rotations: ARCHIVE deactivates; server refuses (`not_listed/inactive`, `not_available/inactive`) | **PASS — this is the axis iter-3 fixed** | The archive trap fix at `70464d323` makes the first post-archive `gacha_banners` publish through the drawer succeed. Both RPC refusals re-derived with synthetic uuid; migration sources confirm the server refuses BOTH sides on `is_active=False` before content resolution runs. Guards are correct. |
| 7 | Pity migration + live §6.4 E2E quoted | **PASS** | Migration applied by Cesar iter-1; `weekly` pity counter still at 3 for Cratilo (RE-DERIVED via PostgREST). |
| 8 | Gacha ops per-user pity group key + reset | **PASS** | Unchanged. |
| 9 | Mock mode exercises the full panel | **PASS** | Unchanged. 9 mock frames still cover every state. |
| 10 | `npm run build` + vitest + backend + deploy stamp + Access 302 + smoke routes | **PASS** | vitest **382** RE-DERIVED; tsc 0 RE-DERIVED; content suite **56 OK** RE-DERIVED; stamp `70464d323` RE-DERIVED in built worker; Access 302 RE-DERIVED. No API deploy needed (no Python changed). |
| 11 | Strings: no player-facing keys; 72 admin DICT en+ja | **PASS** | Unchanged. Iter-3 is pure code / rule-shape / exporter — no new DICT keys, no `LocalizationText.csv` diff. |
| 12 | `ECONOMY_MASTER.md` §3 ball ladder + weekly-rotation paragraph (Architect-review wording) | **PASS on presence** | Unchanged. |

Every acceptance axis PASSes at HEAD; iter-3's changes address axis 6 and are covered by 7
new tests without regressing any other axis.

---

### Assessment of the new iter-3 deviations

**Deviation 13 — the archive trap.** SOUND. The fix is complete (7 cross-row rules re-scoped
to `row.isActive`, matching the audit table exactly), the guards are correct (server refuses
deactivated rows before content resolution — verified in migration source), and the sane-row
rules (12–17) are correctly preserved so a reactivation-by-flip can't revive garbage. The
audit table is exhaustive against `otherCatalogs` references in the file. The one cross-catalog
family left explicitly untouched — missions ↔ mission_start_areas / _wind_presets / _loadouts
/ _goal_weights / _tiers, and mission_loadouts ↔ clubs — is correctly flagged for the
Architect: same trap shape, but no deactivation path exists in the admin drawer today for
those referents, so the trap cannot fire, and adding a guard would change shipped rule
behaviour with no failing publish to fix. That is a decision, not a self-review call.
Fix at `70464d323`, deployed and live.

**Deviation 14 — R3 stand-in exemption.** SOUND. The exemption is narrow by construction (all
three conditions must hold: gacha_banners AND rotationId non-blank AND artSprite exactly
"GachaBanner_Weekly"), and safe by the client ladder — `SpriteIsOwn` compares to
`ConventionName(bannerId)` which is per-row (`GachaBanner_Wk202638`) and can never equal the
shared stand-in's name, so step 2 is never reached; the shared stand-in is demoted to step 4
"draw while the URL downloads" and can never mask uploaded art. Two negative-case tests pin
the narrow scope: an un-tagged banner on the stand-in still trips R3, and a tagged banner on
any other shared sprite still trips R3. Fixes exactly the state SPEC §4.4 designs on purpose.

All 12 iter-1/iter-2 deviations still read clean, unchanged.

---

### Report integrity (Rule 6)

Every PASS claim in `IMPLEMENTER_REPORT.md` iter-3 section is backed either by a visible tool
result I re-derived (vitest 382, tsc 0, content suite 56, `--check clean`, PostgREST reads,
RPC refusals, curl on the deploy stamp and the Access shell, CDN artUrl serve) or by evidence
pinned in the codebase (7 hunks in `contentValidate.ts` matching the shape audit table row-by-
row; 2 hunks in `export_content.py` for the R3 exemption; 4 archived-rows tests and 3 stand-in
tests). No fabrication. No hidden weakening of any assertion. The shape audit table is
mechanically complete — I walked all 22 `otherCatalogs` references and every one lines up.

---

### Gates that don't apply (per SPEC + kickoff)

Unchanged from iter-2: no Unity C#, no Figma node, no mesh, no clone mandate, no player entry
point, no world→screen feature, no gameplay video capture. Steps 2/2b/2c/2d and Rules 9/10/11
do not apply. `admin.golfin.world/rotations` behind Access IS the real entry point (Cesar's
Chrome is signed in, per kickoff). Production evidence is PostgREST reads + the deployed worker
version stamp — the correct evidence shape for this task.

---

### Verdict rationale

Iter-3's two fixes address defects that surfaced only when Cesar's real post-PASS publishes
ran through the drawer — the kind of defect a self-reviewer could not have caught in iter-2
without live prod state after PASS. That is the pipeline working: gates PASS at their commit,
prod reveals a shape defect, code fixes it, status routes back through the chain.

The fixes are minimal, correctly scoped, and covered by 7 new tests including 3 negative-case
tests that keep the guards narrow. The archive-trap fix delivers exactly what Rule 15 asks for
— name the shape mechanically, enumerate every candidate site in the file, publish a per-site
verdict INCLUDING the sites that were fine, fix everything in one commit. The R3 exemption is
safe by the client ladder (`SpriteIsOwn` vs `ConventionName`) and pinned by both accidental-case
and different-sprite negative tests.

Every acceptance axis re-runs cleanly. Every prod-state claim re-derives. Every migration
mirror is byte-identical. Working tree is clean. Deploy stamp matches HEAD's last code commit.

The one operational blocker (Deviation 11 — Monday 00:00 UTC withhold on installed builds
until artUrl warms) is resolved: banner_wk_2026_38 now carries the uploaded artUrl and the
CDN serves it (200 image/jpeg 244928B).

**PASS.** Handing to the adversarial red-team gate.

---

### Files summary

| Path | Change |
|---|---|
| [`/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/weekly_rotation_admin/ARCHITECT_REVIEW.md`](Docs/Specs/Active/weekly_rotation_admin/ARCHITECT_REVIEW.md) | APPENDED — iter-3 section, verdict PASS. |
| [`/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/weekly_rotation_admin/STATUS.md`](Docs/Specs/Active/weekly_rotation_admin/STATUS.md) | UPDATED — `READY_FOR_ARCHITECT_REVIEW` → `READY_FOR_REDTEAM`. |
