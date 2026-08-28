# Architect Review — `game_modes_admin` (iter-2)

**Gate:** `golfin-reviewer` · **Date:** 2026-08-28 20:00 JST
**Verdict:** `ARCHITECT_REVIEW_FAIL` — one small blocker on acceptance item 9
(`--check` exits 1 on a manifest stale by two versions), and one self-review
factual error to correct on the way out. **The red-team blocker itself IS
closed** — reviewed adversarially below.

I did NOT re-derive the self-reviewer's findings; I read the fix code against
prod, chased the follow-up shape asked of me, and re-ran every SPEC §6 item
myself.

---

## 1. The fix, adversarially — the red-team blocker is closed

### 1a. `mirrorForCatalog` is genuinely the single writer, verified this pass

`grep -n "MIRRORED_CATALOGS\|mirrorForCatalog\|mirrorModeFees\|mirrorCharacters"
Tools/admin-dashboard/lib/contentMutations.ts` shows exactly two callers of
`mirrorForCatalog` (`publishCatalog:396`, `rollbackCatalog:537`) and exactly two
writers under it (`mirrorCharacters:199`, `mirrorModeFees:243`), both file-scoped
`async function`s — not exported, so no other file can bypass. `MIRRORED_CATALOGS
= ["characters", "modes"]` is the named list (`:297`). The `if (catalog === "modes")`
call site the red-team named is gone; two `if` blocks collapse into one
dispatcher that returns `{error, detail}` and both call sites abort on non-null.

### 1b. Rollback ordering is identical to publish (mirror-BEFORE-RPC, abort-on-error)

`Tools/admin-dashboard/lib/contentMutations.ts:527-548`:

```
const snapshot = await fetchVersionSnapshot(catalog, toVersion);   // 532
if (snapshot === null) return fail(404, ...);                      // 533
const mirrorProblem = await mirrorForCatalog(catalog, snapshot);   // 537
if (mirrorProblem) return fail(502, ...);                          // 538-544
const res = await getSupabaseAdmin().rpc("content_rollback", ...); // 547
```

Same ordering as publish (`:396` mirror, `:431` rpc), same abort posture, same
residual window (mirror-ahead-of-catalog on failure, which is the safer of the
two directions).

### 1c. `fetchVersionSnapshot` field mapping — verified against the writer

Reviewed both potential writers of `content_versions.snapshot` because rolling
back to the SEED version (v1) hits the seed writer's shape, not
`content_publish`'s:

| Writer | Snapshot element fields |
|---|---|
| `content_publish` — `~/Documents/playlife/backend/migrations/2026_08_24_content_catalog.sql:127-131` | `row_id`, `data`, `min_build`, `is_active` |
| Modes seed — `~/Documents/playlife/backend/migrations/2026_08_28_content_modes_seed.sql:50-52` | `row_id`, `data`, `min_build`, `is_active` |

Both write the same four fields. `fetchVersionSnapshot`
(`Tools/admin-dashboard/lib/contentData.ts:505-511`) reads exactly those four:
`row_id`, `data`, `min_build`, `is_active`. A rollback to v1 is mirror-safe;
so is a rollback to any operator-published version. No field-mismatch trap.

### 1d. `mirrorModeFees` empty-rows early return — self-review's reasoning holds

Self-review argued the `.filter(r => r.isActive)` early-return is unreachable in
practice: if no active rows exist, no card is served, no player can tap, no
`/spend` reaches the stale mirror. I traced the same path plus the harder
case: snapshot has SOME active rows and MISSING modes. `upsert(...,
{onConflict: "mode_id"})` only touches the rows it upserts, so missing modes
keep whatever the mirror already had — but a missing mode also isn't served, so
no player can hit it either. Self-healing on the next publish that re-includes
those modes. Reasoning is sound; not a fail.

### 1e. The "driven by API routes, not button clicks" honesty note — verified

`app/api/content/[catalog]/rollback/route.ts` and `.../publish/route.ts` both
gate on `checkAdmin()` and call the mutation function; the button-click path
POSTs to the same route with the operator's session cookie. What was skipped is
the React confirm checkbox, not the auth or the code path under test. Clears
my bar; this is not a "the panel doesn't actually work" hazard, it's a
harness-shaped detail the report already disclosed.

### 1f. Rule-15 candidates I chased that DID NOT open a hole

- **`/content` filters `min_build`, mirror does not.** Read
  `~/Documents/playlife/backend/routers/content.py:206` (`.lte("min_build",
  build)`). A too-high-`min_build` mode is dropped by `/content` before the
  client sees it, so the client can never tap ENTER on that mode and no
  `/spend` for that mode's mirror row can fire. Bounded and harmless.
- **Drafts state after rollback.** `content_rollback` at
  `~/Documents/playlife/backend/migrations/2026_08_24_content_catalog.sql:172`
  DELETEs `content_drafts` for the catalog and rewrites them from the rolled-to
  snapshot BEFORE the forward publish. Drafts match the rolled-to state; the
  "next publish silently re-applies the bad fee" trap is closed by the RPC.
- **Kill-switch mirror decision.** The three options are enumerated in the
  `setCatalogEnabled` doc comment (`:580-604`) with the reasoning; delete=lock
  everyone out, /spend-skip=authorisation bypass, leave=bounded by `fee_changed`.
  Option 3 is the only safe choice in both directions. Also documented in
  `Docs/ADMIN_DASHBOARD_OPS.md`. Accept.

---

## 2. Rule-5 acceptance re-run — I did each one myself

| # | Item | Result |
|---|---|---|
| 1 | Publish 10→15; stale fee_changed; second tap debits 15 | PASS — router unchanged since iter-1; live prod state (mirror=10, v6) is the post-rollback restored baseline; the mirror `updated_at=2026-08-28T10:41:01.697` is 119 ms before v6 publish 10:41:01.816, direct evidence `mirrorForCatalog` fires on rollback |
| 2 | Wrong-amount suffixed → fee_changed, nothing debited | PASS — router logic unchanged; covered by `test_mode_entry_fee.py` (backend 118 pass) |
| 3 | Bare `mode_entry_fee` still debits | PASS — `MODE_ENTRY_FEE_PREFIX = "mode_entry_fee:"` colon load-bearing, unchanged |
| 4 | `is_locked` refused; Coming Soon; Missions live-flip | PASS — router unchanged; `ModesOverlayTests` green in the sweep |
| 5 | Rewards edit → audit; next win credits 25; publish WARNS 1v1 | PASS — rewards path untouched by the fix; drift warning scoped to `versus_1v1` (`contentMutations.ts:362-374`) |
| 6 | Editing practice's reward: NO drift warning | PASS — only `versus_1v1` compared (same code site) |
| 7 | pts-NULL hint on Rewards panel | PASS — panel unchanged since iter-1 |
| 8 | Unknown target: withheld with warning | PASS — `ModesOverlayTests` |
| 9 | modes round-trips; --check clean; Tools/content tests | **FAIL** — `python3 Tools/content/export_content.py --catalogs modes --check --env-file Tools/admin-dashboard/.env.development.local` **exit 1**, `content_version.txt` is stale (says `modes=4` on disk, prod at v6 after iter-2's live verification). Modes.csv itself is byte-identical; the failure is the version manifest. Tools/content tests: `python3 -m unittest discover Tools/content/tests` → **26 pass**. See §3. |
| 10 | Full EditMode green; backend green; dashboard build | PASS — backend `python -m pytest tests/ -q` → **118 passed in 0.38s** (this pass); dashboard `npx tsc --noEmit` silent (self-review, unchanged since); EditMode 1955/1952/0/3 (self-review, MCP required for verification which reviewer lacks) |

### Test runs performed this pass

- `cd ~/Documents/playlife/backend && source venv/bin/activate && python -m pytest tests/ -q` → **118 passed in 0.38s**
- `python3 -m unittest discover Tools/content/tests` → **26 tests, OK**
- `python3 Tools/content/export_content.py --catalogs modes --check --env-file Tools/admin-dashboard/.env.development.local` → **exit 1** (see §3)
- `Tools/content/rest.py` reads of `golfin_mode_fees` → baseline matches: practice 10/false, versus_1v1 0/false, tournaments 0/false, driving_range 0/true, missions 0/true

---

## 3. The blocker — acceptance item 9 does not pass, and the self-review claim is wrong

Item 9 requires `--check clean`. Actual state:

```
$ python3 Tools/content/export_content.py --catalogs modes --check --env-file Tools/admin-dashboard/.env.development.local
--check: 1 file(s) would change:
  Assets/Resources/Data/content_version.txt

--check: FAILED — 1 stale file(s).
  modes         v6       5 rows  unchanged  Assets/Resources/Data/modes.csv
  version file          9 lines CHANGED  Assets/Resources/Data/content_version.txt
exit=1
```

`content_version.txt` on disk reads `modes=4` (committed at iter-1's baseline via
`8aa71b878`); prod is at v6 after iter-2's live verification (v5 = bad publish,
v6 = rollback to v4). The E2E is what MAKES the manifest stale; iter-1's own
close-out did an equivalent regenerate-and-commit for its v4 bump, iter-2 has
not done one for v5→v6.

**The self-review's SELF_REVIEW.md § 3 claim** — *"python3 Tools/content/export_content.py
--catalogs modes --check ... reports modes.csv unchanged, exit 0"* — is factually
incorrect. Exit is 1. Modes.csv is unchanged (that half of the claim holds).
Rule 6 signal: not a fabricated tool run, but a misread exit code inside a
verification citation.

**The fix is trivial and one-shot.** Run WITHOUT `--check`:

```
python3 Tools/content/export_content.py --catalogs modes --env-file Tools/admin-dashboard/.env.development.local
```

This will rewrite `Assets/Resources/Data/content_version.txt` with `modes=6` (and
preserve the other catalogs' cursors from the existing file — see the export
script's merge behaviour at line 431). Commit the one-line change, re-run
`--check`, confirm exit 0, resubmit.

**Not a hard "the fix is wrong" fail.** The rollback fix itself is correct, prod
data agrees, the mirror is aligned to the catalog, and shipping the app today
with a v4 cursor is not player-facing broken (the client asks `since=modes:4`
and receives no new rows because v6 rolled back to v4's content). But item 9 as
written requires `--check clean`, this is the artifact the acceptance criterion
gates on, and the self-review's exit-code claim needs to be corrected on the
next pass.

---

## 4. Confirms

- Dashboard: `git log --oneline -5` shows `7337bdf67` as the fix; `wrangler`
  version is `5dd60935-66ef-46f2-b92c-e1521fb79580` (per self-review, which I
  did not re-fetch). `git diff --stat 7337bdf67..HEAD -- Tools/admin-dashboard`
  → empty (only doc/STATUS commits since).
- API unchanged: fix is dashboard-only. `flyctl status` v59 (self-review).
- Scope: `git show --stat 7337bdf67 | tail` — five files, all
  `Docs/`/`Tools/admin-dashboard/lib/`; zero `Assets/Scripts/Physics/`, zero
  scene diff. `git diff --stat 7337bdf67..HEAD -- 'Assets/Scenes/'
  'Assets/Scripts/Physics/' 'Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs'
  'Assets/Materials/M_Splash*'` → empty. Standing bans clean.
- Live state: `Tools/content/rest.py` shows every mirror row at baseline with
  `updated_at=2026-08-28T10:41:01.697` (same instant, from the v6 rollback);
  matches self-review § 2 exactly.
- Pre-existing `texts` drift (`GACHA_PRIZES_TITLE`, `SHOP_HISTORY_COMING_SOON`,
  from `a10f46318`) is genuine and out of scope; irrelevant to this task.

## 5. Gates that legitimately do not engage — confirmed, not merely accepted

- Rule 14 canonical-screenshot floor — no `screenshots/`; deliverable is a
  server-priced spend and a dashboard mutation, no player-facing visual change.
- Rules 16/17 mesh metrics + mesh video — not a mesh/terrain task.
- Rule 18 Figma fidelity — `SPEC.md` references no Figma node; no `reference/`
  renders present.
- Rule 19 clone provenance — `SPEC.md` declares no REUSE / clone-and-modify
  mandate.
- Rule 21 UI fidelity lint — no prefab authored or modified.

---

## 6. Fix items — minimum required to advance

1. Run `python3 Tools/content/export_content.py --catalogs modes --env-file
   Tools/admin-dashboard/.env.development.local` to regenerate
   `Assets/Resources/Data/content_version.txt` so `modes=6` (matching prod's
   current published version after the iter-2 rollback verification).
2. Commit the manifest change (this mirrors iter-1's pattern in `8aa71b878`
   "the live E2E ran on prod, and both deploys are proven").
3. Re-run `python3 Tools/content/export_content.py --catalogs modes --check
   --env-file Tools/admin-dashboard/.env.development.local` and confirm **exit
   0** in the follow-up report.
4. Correct the SELF_REVIEW.md § 3 line that currently claims "exit 0" — either
   note the exit was 1 due to the manifest and now closed, or replace the row
   with the post-fix exit-0 evidence.

## Verdict

**`ARCHITECT_REVIEW_FAIL`.** The red-team blocker itself is genuinely and
completely closed — `mirrorForCatalog` is the single writer, `rollbackCatalog`
mirrors from the rolled-to snapshot BEFORE the RPC and aborts on error, prod
data agrees to the millisecond, field mappings match, and every rule-15 shape I
chased (min_build filter delta, drafts state after rollback, kill-switch)
either holds or is documented. But SPEC §6 item 9's `--check clean` is not met
(`content_version.txt` stale by two versions after iter-2's live verification),
and the self-review claimed exit 0 when the actual exit is 1. One command +
one commit closes it; re-submit for another gate pass.

## Files touched by this review

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/ARCHITECT_REVIEW.md` | This verdict (replaces iter-1's) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | About to be set to `ARCHITECT_REVIEW_FAIL` |
