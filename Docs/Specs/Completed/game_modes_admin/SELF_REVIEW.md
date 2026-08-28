# Self-Review — `game_modes_admin` (iter-4)

**Gate:** `golfin-self-reviewer` · **Date:** 2026-08-28 20:35 JST
**Verdict:** `PASS` — the test suite exists, runs green, the two tests that
actually matter are non-vacuous (I broke one on purpose and it caught it), the
declared characterisation-test limits are true and disclosed in the test files
themselves, every SPEC §6 row re-ran clean, the rollback-mirror fix is intact,
the dashboard bytes I read ARE the bytes serving prod, and the diff since the
last deploy is test infra only. This closes exactly what Cesar asked to close
before shipping.

Iteration count = **4** (SELF_REVIEW.md exists from iters 1–3; iter-3 was a
PASS the red-team then ESCALATE'd on). This is not a FAIL, so the ESCALATE
threshold at N ≥ 3 does not apply.

Standing caution acknowledged: I ran every command below and pasted what it
printed. The one place a command's meaning was ambiguous (kickoff's
`/points/spend` — actual mount is `/api/v1/points/spend`) I say so and show
both.

---

## 1. The suite exists, runs, and is not vacuous

`cd Tools/admin-dashboard && npm test`:

```
✓ lib/__tests__/mirrorRowMapping.test.ts (5 tests) 2ms
✓ lib/__tests__/rewardsValidation.test.ts (10 tests) 2ms
✓ lib/__tests__/contentValidate.test.ts (21 tests) 7ms

Test Files  3 passed (3)
     Tests  36 passed (36)
   Duration  486ms
```

3 files, 36 tests. Matches the kickoff's claim exactly.

### Reproduced the drift-warning tripwire myself

Per kickoff, I refused to trust green. I edited `lib/contentValidate.ts`
lines 662–671, replacing the `versus_1v1`-only `find` with a `for (const r of
rows)` loop that fires the warn for EVERY mode whose reward disagrees with
`ctx.versusWinPts`:

```
- const versus = rows.find((r) => r.rowId === "versus_1v1");
- if (versus && ctx.versusWinPts !== undefined && ctx.versusWinPts !== null) {
+ if (ctx.versusWinPts !== undefined && ctx.versusWinPts !== null) {
+   const paid = ctx.versusWinPts;
+   for (const r of rows) { ... warn(r.rowId, "rewards", ...) }
- }
```

Re-ran `npm test`:

```
❯ lib/__tests__/contentValidate.test.ts:156:60
    expect(warningsFor(others, ctx({ versusWinPts: 25 }))).toEqual([]);
Test Files  1 failed | 2 passed (3)
     Tests  1 failed | 35 passed (36)
```

Failure is on the **exact** test the SPEC's decision-of-record turns on:
`"NEVER warns about any other mode, whatever its reward says"`, line 156 of
`contentValidate.test.ts`. Received warnings named `practice`, `tournaments`,
`missions` — the three "card copy" modes the SPEC forbids the drift check
from touching.

Reverted the edit via `Edit` (`git checkout` is banned per house rule).
Verification the file is clean:

```
$ md5 lib/contentValidate.ts
4ca2554ef22099a98a3446554e40eccf   (== original)
$ git diff -- lib/contentValidate.ts
(empty)
```

Re-ran `npm test`: back to `3 passed (3) / 36 passed (36)`.

Conclusion: the drift-generalisation tripwire is real; test line 156 catches
it in one run, unambiguously, with a helpful failure message.

## 2. The tests test the right thing

Read all three files. Answers to the kickoff's questions:

**Drift warning block genuinely asserts the SPEC's rule?** Yes. Four
positive assertions (`describe("the drift warning covers versus_1v1 and
NOTHING else")`):
- warns when `versus_1v1.rewards` disagrees with `versusWinPts` (line 122)
- is a WARNING not an error (line 128)
- prefers `reward1Amount` over legacy `rewards` (line 133)
- NEVER warns about any other mode (line 149 — the tripwire I proved)
- stays silent when `versusWinPts` is undefined or null (line 162)

All match SPEC lines 80–86 ("the drift warning covers exactly that pair and
nothing else… do not generalise this into a mapping table").

**Asserts the five shipped modes validate clean?** Yes,
`contentValidate.test.ts:57–68` uses the exact rows off
`Assets/Resources/Data/modes.csv` and expects `[]`. The comment names why:
"if this ever fails, the validator has become stricter than the data the game
runs on — which is the one way a validator makes itself useless." A validator
that refuses live prod data is uncaught by every other test in the file, so
this row is load-bearing.

**Any assertions that would pass no matter what?** I looked for
`toBeDefined()` on always-defined things, `expect(true).toBe(true)` shapes,
and empty-array `toEqual([])` on inputs that produce nothing regardless of
implementation. Nothing found. Every green here is contingent — the
tripwire I ran demonstrates that for the most delicate one. The
`it.each(["true","false","1","0",""])` bounded-input row (line 91) is
low-signal per assertion but the enumeration itself asserts the accepted-set,
which is the right shape.

## 3. The declared limit is real and disclosed

Both characterisation-test files carry the caveat in their own opening
docstring (not only in the report):

`rewardsValidation.test.ts` lines 3–19: *"WHY THE LOGIC IS RESTATED HERE
RATHER THAN IMPORTED. `checkNumber` is private to `lib/rewardsMutations.ts`
and that module is `server-only`… ⚠️ SO THIS FILE IS A CHARACTERISATION
TEST, AND ITS HONEST LIMIT IS THAT IT PINS THE RULES, NOT THE IMPLEMENTATION.
It cannot catch `rewardsMutations.ts` drifting…"*

`mirrorRowMapping.test.ts` lines 3–14: *"Same characterisation-test caveat as
rewardsValidation.test.ts — both functions live in `server-only` modules, so
the RULES are pinned here and the live behaviour is evidenced by the prod
rollback reproduction in IMPLEMENTER_REPORT."*

Both disclosures name the mitigation: the prod probes + rollback reproduction
in the implementer report.

### The restated logic MATCHES the current source, right now

`checkNumber` in `lib/rewardsMutations.ts:55–61`:

```
function checkNumber(label: string, value: number | null): string | null {
  if (value === null) return null;
  if (!Number.isFinite(value)) return `${label} must be a whole number or empty.`;
  if (!Number.isInteger(value)) return `${label} must be a whole number (no decimals).`;
  if (value < 0) return `${label} must be 0 or more.`;
  return null;
}
```

Test copy at `rewardsValidation.test.ts:22–28`: **byte-for-byte the same
control flow, same messages.** No drift.

`mirrorModeFees` row mapping in `lib/contentMutations.ts:243–259`:

```
.filter((r) => r.isActive)
.map((r) => ({
  mode_id: r.rowId,
  entry_fee: Math.max(0, Math.trunc(Number(r.data.entryFee) || 0)),
  is_locked: String(r.data.locked ?? "").trim().toLowerCase() === "true"
    || String(r.data.locked ?? "").trim() === "1",
  updated_at: new Date().toISOString(),
}))
```

Test copy at `mirrorRowMapping.test.ts:19–29`: **the shape and formulae
match exactly** (the test omits `updated_at`, which is by design — that
field's value is a wall-clock timestamp and pinning it is what makes a test
brittle, not resilient). No drift.

Both characterisation files are correct AND clearly labelled as such. No
embarrassment.

## 4. SPEC §6 acceptance list — re-ran every one, this pass (Rule 5)

| # | Command | Output | Verdict |
|---|---|---|---|
| backend | `cd ~/Documents/playlife/backend && source venv/bin/activate && python -m pytest tests/ -q` | `118 passed in 0.40s` | ✓ |
| content | `python3 -m unittest discover Tools/content/tests` | `Ran 26 tests in 0.025s / OK` | ✓ |
| export --check | `python3 Tools/content/export_content.py --catalogs modes --check --env-file …env.development.local` | `modes v6 5 rows unchanged … --check: clean` **EXIT=0** | ✓ |
| tsc | `cd Tools/admin-dashboard && npx tsc --noEmit -p tsconfig.json` | silent, **EXIT=0** | ✓ |
| Unity EditMode | `tests-run` | Passed **1955 / 1952 passed / 0 failed / 3 skipped**, first call this pass — no "No tests found" flake | ✓ |
| content endpoint | `curl -s -o /dev/null -w "%{http_code}" "https://playlife-api.fly.dev/api/v1/content?catalogs=modes"` | `200` | ✓ |
| /health | `curl … https://playlife-api.fly.dev/health` | `200` | ✓ |
| /points/spend 403-not-404 | POST `/points/spend` (bare) → 404; POST `/api/v1/points/spend` → **403**; same for `/api/v1/progress/level-up`, `/api/v1/shop/purchase` | 403 on the real mount; the bare path in the kickoff is shorthand — no mount is actually served without the `/api/v1` prefix | ✓ |
| Live mode fees | Supabase `golfin_mode_fees` | practice 10/f, versus_1v1 0/f, tournaments 0/f, driving_range 0/t, missions 0/t | ✓ |
| Live earn actions | Supabase `game_point_actions` | 4 rows, `versus_win pts=20 max=20 daily_cap=200`, other three `pts=null` | ✓ |
| modes catalog live | Supabase `content_catalogs` | `[{"name":"modes","published_version":6}]` | ✓ |

Every row matches the kickoff and the implementer report.

## 5. Nothing regressed — the rollback-mirror fix is intact

`lib/contentMutations.ts:297`: `export const MIRRORED_CATALOGS =
["characters", "modes"];`. `mirrorForCatalog` is at :298; callers are
`publishCatalog:396` and `rollbackCatalog:537`. `rollbackCatalog` body
(:525–545 read this pass): reads snapshot, calls `mirrorForCatalog` first,
returns 502 on mirror error before the rpc — identical ordering to publish.

Cursor: `grep modes Assets/Resources/Data/content_version.txt` → `modes=6`,
== the live `published_version=6`. The staleness the reviewer caught in
iter-2 stays fixed.

## 6. Deploy + scope

**Cloudflare deployment id.** `CLOUDFLARE_ACCOUNT_ID=c2c4b9869449639abcc77e5437c28dab
npx wrangler deployments list --name golfin-admin` last row:

```
Created:     2026-08-28T11:30:30.149Z
Version(s):  (100%) a28a1a56-d6cf-4a7c-b27f-e2cc88480d90
```

Matches STATUS.md. (Aside: it is a Workers deploy, not Pages — `pages
deployment list` returns "Project not found"; the deploy script uses
`opennextjs-cloudflare deploy`. Not a defect, just a note for the next
reviewer who tries `wrangler pages`.)

**Stamp `04b7bbf84`.** `git rev-parse HEAD` → `04b7bbf84…`. `git log -1
--format='%H %s' 04b7bbf84` → `test(admin): vitest over the pure validators
— 36 tests, tripwire-verified`. So HEAD == the built stamp, i.e. the
04b7bbf84 dashboard bundle is what serves prod.

**`git diff --stat 04b7bbf84..HEAD -- Tools/admin-dashboard`** → empty
(HEAD == stamp).

**Diff since the previously-deployed stamp `7337bdf67`:**

```
Tools/admin-dashboard/lib/__tests__/contentValidate.test.ts    | 181 +++
Tools/admin-dashboard/lib/__tests__/mirrorRowMapping.test.ts   |  90 ++
Tools/admin-dashboard/lib/__tests__/rewardsValidation.test.ts  |  89 ++
Tools/admin-dashboard/package-lock.json                        | 1436 +++-
Tools/admin-dashboard/package.json                             |   7 +-
Tools/admin-dashboard/vitest.config.ts                         |  26 +
6 files changed, 1797 insertions(+), 32 deletions(-)
```

**Pure test infra.** No route handler, no lib mutation, no page. That is
what STATUS.md said and what git says.

**API unchanged at v59.** `fly status --app playlife-api` → machine 148e03…
`VERSION 59 nrt started`. Suite is dashboard-side only, as declared.

**vitest is a devDep.** `python3 -c "…json…"` → `deps: False / devDeps:
True`. Bundle unaffected.

**`npm run build` still passes.** Ran it. Tail: `✓ Compiled successfully
in 1705ms`, followed by the standard Next route table (Middleware 92.2 kB,
Modes page 503B/143kB, Rewards page 2.27kB/135kB — the new panels ship). No
errors.

**Scope bans (Rule 7):**

```
$ git diff --stat 04b7bbf84~5..HEAD -- Assets/Scenes/ Assets/Scripts/Physics/ Assets/Materials/M_Splash
(empty)
$ git diff --stat 04b7bbf84~5..HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs
(empty)
```

No `*Gate` scenario, no LabScaffold mutation, no scene diff, no
`M_Splash*` touch.

**Gates 14/16/17/18/19/21.** None engage: no Unity UI/prefab/mesh/Figma
node/screenshots, no capture, no clone-provenance mandate. Confirmed by
walking the SPEC — the deliverable is server routes + two Next.js panels +
now a vitest suite.

**Pre-existing `texts` drift** (`GACHA_PRIZES_TITLE`,
`SHOP_HISTORY_COMING_SOON` from `a10f46318`, why the FULL `--check` exits 1).
Confirmed out of scope; the modes-scoped `--check` exits 0 (see §4 row 3).

## 7. Report integrity (Rule 6)

Every numeric claim in `IMPLEMENTER_REPORT.md`'s "The gap is CLOSED"
section that I could re-derive, I re-derived:

| Claim | My re-run | Verdict |
|---|---|---|
| 36 tests over 3 files | `npm test` → `3 passed (3) / 36 passed (36)` | ✓ |
| 21 contentValidate + 10 rewards + 5 mirror | vitest per-file line: 21, 10, 5 | ✓ |
| Tripwire: generalising drift → `NEVER warns about any other mode` fails | reproduced myself in §1 | ✓ |
| Deploy `a28a1a56-…-e2cc88480d90` | wrangler list, last row | ✓ |
| Stamp `04b7bbf84` | `git rev-parse HEAD` | ✓ |
| API v59 | `fly status` | ✓ |
| Backend 118 | `pytest -q` | ✓ |
| Content 26 | `unittest discover` | ✓ |
| Unity 1955/1952/0/3 | `tests-run` | ✓ |
| modes cursor 6 == prod 6 | `grep` + Supabase | ✓ |

No fabricated numbers.

## 8. What I did NOT do, and why

- Did **NOT** independently trigger a live rollback on prod. The iter-1
  rollback reproduction and its audit trail are cited in
  `IMPLEMENTER_REPORT.md` and `REDTEAM_REVIEW.md`; the rollback CODE PATH I
  read from disk this pass, and the disk == deployed diff is empty. Adding
  another rollback would be an unnecessary prod mutation.
- Did **NOT** re-run the six malformed PATCH probes against the deployed
  Rewards route. Same reason: the source I read IS the source serving prod,
  the six probes are logged in the report, and this gate has no browser tool
  to hit the Access-gated route directly. The characterisation tests plus the
  disk-equals-deployed diff cover the same guards from the specification
  side.
- Did **NOT** run the full `export_content.py --check` (unscoped). The
  kickoff explicitly restricts §6 verification to `--catalogs modes` because
  the pre-existing `texts` drift is out of scope, so an unscoped exit 1 would
  be expected and uninformative.

---

## Files touched by this review

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/SELF_REVIEW.md` | This verdict (replaces iter-3) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | Set to `SELF_REVIEW_PASS` |
| `Tools/admin-dashboard/lib/contentValidate.ts` | Edited to reproduce the drift tripwire in §1, then reverted via `Edit`; md5 back to `4ca2554…`, `git diff` empty |
