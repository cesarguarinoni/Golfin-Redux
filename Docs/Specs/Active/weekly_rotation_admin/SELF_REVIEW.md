# SELF_REVIEW — `weekly_rotation_admin`

**Iteration:** 1
**Reviewer:** self-review pass (main Claude Code thread acting as self-reviewer)
**Date:** 2026-09-11 12:49 JST
**Verdict:** **BACK_TO_IMPLEMENTER**
**STATUS after this review:** `SELF_REVIEW_FAIL`

---

## Scope of this review

This is an admin-dashboard + content-pipeline + one-SQL-migration task. There is no Unity C#,
no Figma node and no game-view render. The Unity MCP, Figma-fidelity, UI-lint, bbox and mesh
gates therefore do not apply. What I could and did independently verify: the dashboard vitest
suite, `tsc --noEmit`, the pinned determinism hash, the content-test suite,
`export_content.py --check`, the live PostgREST catalog/row state, the deployed worker's
version stamp, and the presence + shape of every file the report says landed. The single
canonical screenshot plus eight supporting screenshots are all MOCK-mode by design; the
production evidence is quoted in the report from PostgREST reads and the deployed panel, and I
re-derived it against the tables myself.

---

## Independent evidence I gathered

### 1. Dashboard vitest — PASS, matches claim exactly

```
$ cd Tools/admin-dashboard && npx vitest run
Test Files  16 passed (16)
     Tests  378 passed (378)
```

Includes `lib/__tests__/rotation.test.ts` (34 tests) and `lib/__tests__/rotationValidate.test.ts`
(20 tests). Report claim: **378 passed** — MATCH.

### 2. Pinned determinism hash — PASS

`grep -n "8838dbcb" lib/__tests__/rotation.test.ts` → `172: expect(a.hash).toBe("8838dbcb");`
Matches the report claim and the kickoff expectation.

### 3. `tsc --noEmit` — PASS

`npx tsc --noEmit` → exit 0, no output.

### 4. Content test suite — **FAIL: 8 of 53 tests fail, undisclosed in the report**

```
$ python3 -m unittest discover Tools/content/tests
Ran 53 tests in 0.072s
FAILED (failures=8)
```

The failures are all in `test_catalog_registry.py`, all in tests that were either added by
this iteration or that this iteration's live publishes drifted through:

| Failing test | Expected | Actual | Root cause |
|---|---|---|---|
| `TestRotationsIsRegistered.test_the_seeded_row_count_is_the_year_plan` | 52 rows | 54 rows | The pity E2E left `wk_2026_36` and `wk_2026_37` in `rotations.csv` (both `is_active=false`, but not deleted from the CSV). |
| `TestRotationsIsRegistered.test_the_seeder_splits_is_active_out_of_data` | 52 rows | 54 rows | Same. |
| `TestRotationsIsRegistered.test_every_seed_row_is_unmaterialized_and_LF` (row=`wk_2026_38`) | `materializedAt == ""` | `2026-09-11T02:55:13Z` | Legitimate — the row was materialized on prod. Test hardcodes the seed-time invariant. |
| Same, row `wk_2026_36` | blank | `2026-09-11T03:14:00Z` | Same, from the E2E. |
| Same, row `wk_2026_37` | blank | `2026-09-11T03:14:13Z` | Same. |
| `TestGachaCatalogsAreRegistered.test_the_seeded_row_counts_are_what_was_round_tripped` (`gacha_banners`) | 4 | 7 | Live publish added `banner_wk_2026_38`, `banner_wk_2026_36`, `banner_wk_2026_37` to the CSV. `GACHA_ROW_COUNTS` was hardcoded in the prior `gacha_admin_catalogs` task and never bumped. |
| Same (`gacha_rates`) | 6 | 24 | Same — three rotations × 6 rates each = +18 rows. |
| Same (`gacha_pools`) | 11 | 45 | Same — three rotations × varying entries. |

**Report position on this.** The report says under Files table: *"Suite 48 → **53**"* — that
is a test-count claim, not a pass-count claim. The acceptance table's item 9 ("`npm run build`
green; vitest green; backend suite green") does not name the content suite. So this is not a
strict Rule-6 fabrication. But: Cesar's kickoff literally says *"Content tests: ... (claims
53)"* — meaning he expected all 53 to pass. And the "Suite 48 → 53" phrasing reads as "suite
now sized 53, still green." It isn't.

The failures themselves are legitimate — a live publish inevitably changes these counts, and a
materialize inevitably stamps `materializedAt`. The tests were written to snapshot seed-time
state and were never revisited after the E2E. This is a small, mechanical fix (either update
the assertions to invariants that don't drift on publish, or refresh the hardcoded numbers, or
delete the archived test rows via the drawer and reset the wk_2026_38 stamp so seed-time state
is restored).

### 5. `export_content.py --check` — PASS

```
rotations     v4      54 rows  unchanged
gacha_banners v12      7 rows  unchanged
gacha_rates   v6      24 rows  unchanged
gacha_pools   v5      45 rows  unchanged
shop_catalog  v9      25 rows  unchanged
--check: clean — no file would change, no catalog has drifted, and no row's art is masked by a placeholder.
```

Every version matches the kickoff expectation exactly: `gacha_rates 6, gacha_pools 5, gacha_banners 12, shop_catalog 9, rotations 4`.

### 6. Live PostgREST prod state — MATCHES kickoff expectations exactly

```
CATALOG VERSIONS
  gacha_banners: v12
  gacha_pools:   v5
  gacha_rates:   v6
  rotations:     v4
  shop_catalog:  v9

SHOP wk_2026_38 rows: 13 active, 0 inactive
banner_wk_2026_38: active=True, pityGroup='weekly', rotationId='wk_2026_38'
wk_2026_36 rotation: active=False
wk_2026_37 rotation: active=False
wk_2026_38 rotation: active=True
shop_wk_2026_36: total=2, active=0     (archived)
shop_wk_2026_37: total=2, active=0     (archived)
banner_wk_2026_36: total=1, active=0
banner_wk_2026_37: total=1, active=0

golfin_gacha_pity for f2636482-29aa-4233-a834-99526b202fe1 (Cratilo):
  banner_id='weekly'                counter=3  total=3     ← the group counter
  banner_id='banner_wk_2026_36'     counter=0  total=2     ← per-banner cap
  banner_id='banner_wk_2026_37'     counter=0  total=1     ← per-banner cap
  banner_id='banner_standard_club1' counter=43 total=179
  banner_id='banner_test_b'         counter=8  total=21
```

This is the whole §6.4 E2E in one shot: pulls on A × 2 then pulls on B × 1 landed under the
shared `weekly` key at counter 3 while the per-banner rows stayed independent. The pity
migration is live. Kickoff expectation `banner_id 'weekly' counter 3` → MATCH.

### 7. Deployed stamp — PASS

- `curl -s -o /dev/null -w "%{http_code}" https://admin.golfin.world/api/version` → **302**
  (Cloudflare Access fronting the origin, exactly as expected).
- `grep -o '"[0-9a-f]\{7,9\}"' Tools/admin-dashboard/.open-next/server-functions/default/.next/server/app/api/version/route.js`
  → `"f8063af6b"` — matches the report and the kickoff expectation exactly.

### 8. Files claimed vs on disk — PASS

Every claimed new file is present:
- `Tools/admin-dashboard/lib/rotation.ts` (46 KB)
- `Tools/admin-dashboard/lib/__tests__/rotation.test.ts` (34 KB)
- `Tools/admin-dashboard/lib/__tests__/rotationValidate.test.ts` (19 KB)
- `Tools/admin-dashboard/app/(panels)/rotations/{page,rotations-panel,lineup-workbench}.tsx`
- `Tools/admin-dashboard/migrations/2026_09_11_content_rotations_seed.sql` (103 KB)
- `Tools/admin-dashboard/migrations/2026_09_11_gacha_pity_group.sql` (44 KB)
- `Assets/Resources/Art/Gacha/Banners/GachaBanner_Weekly.png` (1.2 MB) + `.meta`
- `shop_catalog.csv`: header ends with `,is_active,rotationId` — column added; 13 `shop_wk_2026_38_*` rows present, all active.
- `gacha_banners.csv`: header includes `rotationId,pityGroup,is_active`; `banner_wk_2026_38` with `pool_wk_2026_38 / pityGroup=weekly` present.
- 72 new `DICT` keys under `ro.*`, `nav.rotations`, `c.facet.rotation`, `sh.rotation.help`, `gb.pityGroup.hint`, `ugac.pity*` (grep count matches the report's 72 claim).

### 9. Validator R1-R4 wiring — PASS

`contentValidate.ts` has `checkRotationTag` for R2 (used on shop and banners), R3 across
`pityGroup` collectives, R4 on shop `refId` history, R1 in the `rotations` block for
window/quota/pin grammar. `SHOP_REFERENCED_CATALOGS` is `Array.from(new Set(Object.values(SHOP_CATEGORY_TO_CATALOG)))`
per the ticket_types fix. `ballBand` present. All matches the report.

### 10. Working tree — clean

`git status --porcelain --untracked-files=all` → empty. The three implementer commits
(`d74f4f7bc`, `05f0f7da1`, `d2dc096c7`, `80e5cb8ba`, `f8063af6b`, `538f23730`, `b85d85b5f`)
and the playlife commit (`df84963`) are all landed.

### 11. Screenshots — provenance and mock-mode framing OK

`rotations_workbench_preview.png` (2880×2600, long edge ≥ 900) and the eight supporting
frames all carry the yellow `MOCK DATA — running on local fixtures, no Supabase connection`
banner. Report explicitly names them as mock frames and says the prod evidence is quoted
from PostgREST reads and the deployed panel. That is the honest framing for this task — the
prod dashboard is Access-fronted and Cesar's Chrome is the only way in; there is no shell
capture path.

The canonical preview shows all the elements the acceptance requires: 8-week calendar with
GENERATED / MISSING states, the selected rotation with its window and materializedAt, the
seed input, PREVIEW / MATERIALIZE / PUBLISH ROTATION / ARCHIVE ENDED buttons, the pinned
warnings block (4 warnings, since the mock catalog has 2 clubs / 1 ball / 1 char, all pinned
and all previously listed in mock recent rotations), the CLUBS / BALLS / CHARACTER cards
with rarity chip + PINNED + FEATURED tags, the weekly banner card with pool/pityGroup=weekly
and its window, the EFFECTIVE ODDS table with the featured ★ row highlighted, and the "Rows
this will write: gacha_rates 6 · gacha_pools 6 · gacha_banners 1 · shop_catalog 4 · rotations
1 (18) · Lineup hash dabce89e · seed 9999" summary line. The other frames cover: the amber
shortfall list (open week), the PUBLISH confirmation with the five-catalog reveal and typed
`PUBLISH`, the re-materialize confirmation with typed `wk_2026_38`, the JA render, the Users
drawer pity section with the `weekly · GROUP · 2` badge showing `4/50 to Legendary · 4 pulls
on this key` and Reset enabled.

---

## Spec § 7 acceptance walk-through

| # | Item | Report verdict | My verdict | Reasoning |
|---|---|---|---|---|
| 1 | `rotations` seeded with the 52 planned rows; export byte-identical; `--check` clean for `rotations`, `shop_catalog`, `gacha_banners`; 21 catalogs in README + runbook | PASS | **CONFIRM-PASS with caveat** | Seed was 52 rows at seed time (md5 `11a9211bc77e365d4d8dbe34fb42cea6` reproduces from the CRLF ref stripped to LF — VERIFIED). `--check` clean NOW — VERIFIED. `test_the_table_holds_twenty_one_catalogs` passes. **Caveat**: the CSV on disk now carries 54 rows (52 plan + 2 archived pity-E2E test rotations `wk_2026_36` / `wk_2026_37`) and `wk_2026_38.materializedAt` is stamped. The disk state is legitimate but no longer matches the "52 planned rows" phrase read literally. The report acknowledges the wk_2026_38 stamp but does not disclose that the CSV row count moved to 54 or that this breaks three of its own tests. |
| 2 | Rotations panel: calendar + PREVIEW of pinned/unpinned rows + featured ×3 + determinism + unresolvable-pin block | PASS | **CONFIRM-PASS** | Live prod evidence quoted (banner and shop rows land as described); the pinned-row determinism vitest hash `8838dbcb` is in the test file and the suite passes; canonical screenshot shows the pinned-preview shape; a rotationValidate test covers the "pin does not resolve" block. |
| 3 | MATERIALIZE writes drafts; second MATERIALIZE asks typed confirmation; other rotations untouched | PASS | **CONFIRM-PASS** | Live prod counts quoted (`13 shop / 1 banner / 12 pool / 6 rate / rotation`); `rotations_materialize_confirm.png` shows the typed confirmation gate with the rotation id disabled-until-typed pattern; vitest `materialize > a second materialize overwrites this rotation's rows and leaves other rotations' rows untouched` passes and asserts the other-rotation byte identity. |
| 4 | PUBLISH ROTATION publishes in order; R3 violation stops at `gacha_banners` naming the rule; nothing after published | PASS | **CONFIRM-PASS** | Live prod: the chain landed rates → pools → banners → shop → rotations; the pre-existing `ticket_types` bug stopped the first live run at shop_catalog (fixed and redeployed in `d2dc096c7`, then the last two catalogs published cleanly); the mock R3 run stops at `gacha_banners` naming the rule; vitest `publish chain > stops at the first failure and publishes nothing after it` passes. |
| 5 | R1 overlap error, R1 gap warn, R2, R3, R4 each have a vitest | PASS | **CONFIRM-PASS** | `rotationValidate.test.ts` has 20 tests covering all five rules including grammar, blank/inactive cases, and the "window counts rotations by startUtc" corner. |
| 6 | Ended rotations: ARCHIVE deactivates; server refuses purchase of archived row / pull on archived banner | PASS | **CONFIRM-PASS** | Live prod: post-E2E, wk_2026_36/37 rotations and every one of their 42 spawned rows are `is_active=False` — I verified via PostgREST; the two RPCs' refusals are quoted in the report (`not_listed / inactive`, `not_available / inactive` with a small copy tweak: shipped reason is `inactive`, not `banner`, which the report also notes); vitest `archive: rotations ended > 7 days ago, and only their ACTIVE rows, deactivated` passes; the mock 17-row run is captured. |
| 7 | Pity migration: verification block passes; live §6.4 E2E quoted | PASS | **CONFIRM-PASS** | Migration applied by Cesar; live pity read for user f2636482 shows `banner_id='weekly'` at counter 3 (verified by me over PostgREST). Two per-banner cap counters at 0/total=2 and 0/total=1 — the counter continued across banners under the shared key. |
| 8 | Gacha ops panel per-user pity shows group key; reset works | PASS | **CONFIRM-PASS** | `users_gacha_tab_pity_group.png` shows `weekly · GROUP · 2`, `4/50 to Legendary · 4 pulls on this key`, Reset enabled; `resetPity` keys on `banner_id` which IS the pity key (no schema change). |
| 9 | `npm run build` green; vitest green; backend suite green; deployment id + version stamp; Access 302; smoke routes 200 | PASS | **CONFIRM-PASS** | vitest 378 passed reproduced; `tsc --noEmit` clean; stamp `f8063af6b` in built worker route; Access shell 302 verified. Backend suite 319 quoted; no Python changed, no Fly deploy needed. |
| 10 | Strings: no player-facing keys; 72 admin DICT en+ja | PASS | **CONFIRM-PASS** | Grepped 72 new keys under the expected prefixes. No `LocalizationText.csv` diff (working tree clean, no CSV touched in this task). |
| 11 | `ECONOMY_MASTER.md` §3 gains ball ladder + weekly-rotation paragraph | PASS | **CONFIRM-PASS on presence, flagged for Architect wording review** | The report says both were written and flagged for wording. Not reviewed for content by me. |
| 12 | Mock mode exercises the full panel | PASS | **CONFIRM-PASS** | All nine screenshots are MOCK frames; every acceptance-mentioned state (MISSING/NOT_GENERATED/GENERATED/SCHEDULED, PREVIEW pinned/open, MATERIALIZE with typed re-confirm, PUBLISH chain clean and R3-stopped, ARCHIVE, JA render, Users group row) is captured. |

---

## Deviations §-by-§ (Cesar's kickoff bullets)

Cesar asked me to pay particular attention to the report's § Deviations. Each one is
disclosed and the rationale reads clean:

1. **One-ball listing instead of ten** (§3.3 spec) — disclosed as Deviation 1. Rationale is
   `golfin_shop_purchase` reads `quantity` only for tickets (`category='ticket'`) and
   validator rule G3-Q refuses any other value on non-ticket rows; a ten-ball listing would
   be a server change, not a content edit. Report is honest that this is a spec point the
   implementer could not ship as written. Sound.
2. **No named `rotation_*` audit action** — disclosed as Deviation 2. Rationale is that
   there is no new endpoint (per spec §2), so no server code path exists to name a
   `rotation_materialize` / `rotation_publish` action; every write is audited under the
   existing `content.draft.*` / `content.publish` actions, and the publish note carries the
   `rotation_publish <id> seed=<seed>` string. The report explicitly flags this as a decision
   for the Architect. Sound.
3. **Additive columns are an importer no-op by design** — Deviation 3. Correct: a missing
   key IS a blank cell to the importer/exporter/validator; `--check` is clean
   post-additive-column round trip; the columns gain values on their next edit.
4. **Pre-existing `ticket_types` bug fixed** — Deviation 4. I confirmed the fix
   (`SHOP_REFERENCED_CATALOGS` derived from `SHOP_CATEGORY_TO_CATALOG`) and the regression
   test. Sound decision to fix in-line since the shop-drawer publish was blocked and this
   task needed to run it.
5. **Stale `shop_ticket_standard_50` draft aligned** — Deviation 5. Documented as
   `is_active=false` set on the draft to match the published row; the report says putting the
   listing back on sale is Cesar's call. Sound — this would have re-activated an
   intentionally-off row on the first shop publish.
6. **Permanent-listing warning** — Deviation 6. Warns when a picked ref has an active
   untagged shop row (two collisions today: `shop_char_mike` 150 RP vs the week's 200 RP;
   `shop_ball_putt_ace` 50 RP vs 120 RP). Sound.
7. **Pity E2E done over PostgREST/RPC after Access session expired** — reported in the §7
   Acceptance item 6 evidence. The archive/deactivate write ran through the `content_publish`
   RPC; deactivation-only change set with a clean `--check` afterwards. Sound.

**Undisclosed** (my Step-4 findings that the report does NOT call out):

- **A** — CSV row count moved from 52 to 54 because the two pity-E2E test rotations
  (`wk_2026_36`, `wk_2026_37`) were archived-in-place (still in the CSV as inactive) rather
  than removed from the CSV. Operationally harmless (inactive, no live spend). Report only
  hints at this by mentioning the rotation ids under the E2E evidence; there is no
  Deviation entry stating "the plan CSV now carries the two test rotations as archived".
- **B** — 8 content-suite tests fail as a result of A and of `wk_2026_38.materializedAt`
  being stamped. All are hardcoded snapshots of seed-time state. Not disclosed anywhere in
  the report — the "Suite 48 → 53" phrasing under Files misleads by reading as pass count.

---

## Verdict rationale

Everything the customer needs is live and works: the pity migration is applied, `wk_2026_38`
is materialized and published, the panel is deployed, the calendar/preview/publish/archive UI
is exercisable in mock, and the invariant vitest suite (which is what actually gates behavior)
is green. The core work is done and done well.

The two undisclosed items are small but non-zero:

- **A** (54 rows vs 52 in the CSV) is a real spec drift that would land in the next
  export/commit as a permanent record; the plan file is now materially different from
  `reference/rotations_seed.csv` in ways the report doesn't call out.
- **B** (8 failing content tests) matters because Cesar's kickoff explicitly says
  *"Content tests: ... (claims 53)"* — he expected the suite green. It isn't. And the
  failures are all tests this task's shape (added-catalog, added-columns, added-rows) either
  authored or drifted through.

Both are mechanical, small-lift fixes. Sending this back to the implementer to close them out
before the architect gate keeps the pipeline honest and matches the standing rule that
implementer-graded PASS on items whose reality is worse than "PASS" should be routed back.

If A alone were the only issue I would forward with a note (it's arguably operational
housekeeping and Cesar knows the test rotations exist). But B — the content-suite failures on
tests the kickoff named — pushes this back.

---

## Fix list for the implementer (concrete, small-lift)

**Do these three, then re-submit at `READY_FOR_SELF_REVIEW`. This is not a
re-implementation; the core work stands.**

1. **Get `python3 -m unittest discover Tools/content/tests` back to green** (currently 45
   pass / 8 fail). Two paths, either is fine:
   - **Option A (preferred — restore seed-time state)**: delete `wk_2026_36` and `wk_2026_37`
     from `Assets/Resources/Data/rotations.csv` AND from all four dependent CSVs (their four
     shop rows, two banners, two pool sets, two rate sets), clear the `materializedAt` on
     `wk_2026_38` in the CSV, then run the importer to align server drafts with the CSV and
     re-publish (deactivation-only diff, `--check` clean afterwards). This restores the
     "52 planned rows, all un-materialized" invariant the tests check for.
   - **Option B (accept post-publish reality)**: update the four failing test methods to
     accept the current state — bump `GACHA_ROW_COUNTS` to `{gacha_banners:7, gacha_rates:24, gacha_pools:45}`
     with a comment naming `weekly_rotation_admin`, and either exclude
     `wk_2026_36`/`wk_2026_37`/`wk_2026_38` from the "un-materialized" and "row count is 52"
     assertions, or rephrase them as invariants that don't drift on publish (e.g. "the 52
     wk_2026_38 .. wk_2027_36 planned rows are present" and "planned rows have a blank
     `materializedAt` UNLESS the rotation has been materialized on the server").

2. **Add a Deviation entry** to `IMPLEMENTER_REPORT.md` § Deviations disclosing whichever of
   A/B applies after fix 1: either "the CSV was restored to the 52-row plan by removing the
   pity-E2E rotations" or "the CSV carries the 2 pity-E2E rotations as archived, tests
   updated to accept this — permanent operational artifact of the E2E".

3. **Re-run `python3 -m unittest discover Tools/content/tests` and quote `OK` + the test
   count in the report** so this doesn't get missed again. Also worth quoting `--check clean`
   after any CSV/server changes fix 1 introduces.

Estimated lift: 20-40 minutes for Option B, an hour or two for Option A (needs an importer
round trip). Either resolves this cleanly.

---

## Visual diff notes

Not applicable — no Figma node; screenshots are dashboard mocks. The canonical preview shows
every UI element the acceptance names and every state the mock is designed to reach; I
described it under § 11 above.

## Bbox verification

Not applicable — no Unity, no containment claim to verify.

## Scene-mutation audit

Not applicable — no Unity scene touched. Working tree is clean via `git status --porcelain`.

## Production-flow capture check

Not applicable — the "production flow" for this task is the deployed dashboard behind
Cloudflare Access, which the report captures by quoting PostgREST reads and the shell 302,
plus the live E2E on prod. That is the correct evidence shape for an admin-dashboard task.

## Capture-helper compliance check

Not applicable — no Unity capture, no new Unity static-bus context.

---

## Files summary

| Path | Change |
|---|---|
| `Docs/Specs/Active/weekly_rotation_admin/SELF_REVIEW.md` | NEW — this file (verdict `BACK_TO_IMPLEMENTER`). |
| `Docs/Specs/Active/weekly_rotation_admin/STATUS.md` | UPDATED — `READY_FOR_SELF_REVIEW` → `SELF_REVIEW_FAIL`. |
