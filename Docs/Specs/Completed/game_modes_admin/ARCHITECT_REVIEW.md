# Architect Review — `game_modes_admin` (iter-4)

**Gate:** `golfin-reviewer` · **Date:** 2026-08-28 20:46 JST
**Verdict:** `READY_FOR_REDTEAM` — the vitest suite Cesar chose over
shipping-with-fast-follow is real, non-trivial, positive-and-negative on
every §2 blocking rule, tripwire-verified twice (self-review broke the drift
rule and got the exact expected failure at `contentValidate.test.ts:156`; I
verified independently that vitest itself exits 1 with "No test files
found" — so the suite CANNOT pass on nothing). The rollback-mirror fix from
iter-1 red-team is untouched, the cursor from iter-2 stays fixed, and disk
== deployed bytes across `Tools/admin-dashboard`. Every SPEC §6 acceptance
row re-ran clean this pass with my own tool output.

Non-visual, non-Unity-UI task — Steps 0 / 2 / 2b / 2c / 2d (pixel scan,
mesh metrics, Figma fidelity, clone provenance, UI lint) do not engage; no
containment claims, no capture, no `screenshots/` file. Gates 14 / 16 / 17
/ 18 / 19 / 21 legitimately do not apply.

---

## 1. The vitest suite, judged as code (kickoff Q1)

**Non-vacuous?** Yes. I read all three files and looked for tautologies —
assertions on values the test itself constructs, `expect(true).toBe(true)`
shapes, empty-array `toEqual([])` on inputs that produce nothing regardless
of implementation, `toBeDefined` on always-defined things. Nothing found.
Every assertion is contingent on the SUT's behaviour.

**SPEC §2 blocking-rule coverage — positive AND negative direction:**

| SPEC §2 rule | Failing case tested | Passing case tested |
|---|---|---|
| `entryFee ≥ 0` | `entryFee: "-1"` → 1 error on `entryFee` (contentValidate.test.ts:71) | Shipped catalog with `entryFee: "10"` accepted (:57) + mirror clamps `-5` to 0 (mirrorRowMapping.test.ts:57) |
| unique `order` | Two rows both `order: "2"` → error on `order` (contentValidate.test.ts:97) | Shipped catalog uses distinct `order 1..5` and passes (:57) |
| non-empty `target` | `target: ""` → error on `target` (contentValidate.test.ts:77) | Unrecognised `target: "battle_royale"` accepted — client withholds (:82) |
| `locked` parses as bool | `locked: "yes"` → error on `locked` (contentValidate.test.ts:92) | `it.each(["true","false","1","0",""])` — five values, each `errorsFor` empty (:96) |
| drift warning `versus_1v1` only | Warns when 1v1 rewards ≠ `versusWinPts` (contentValidate.test.ts:118) | Silent when they agree (:141); silent when `versusWinPts` undef/null (:162); NEVER warns on `practice`/`tournaments`/`missions` (:149 — this is the tripwire) |

All five §2 rules asserted in both directions. The single most important
row is `contentValidate.test.ts:57–68` — the five real modes from
`Assets/Resources/Data/modes.csv` expected to validate to `[]`. That row
is a fresh guard against the validator drifting stricter than the data
the game actually ships on, which is the one way a validator becomes worse
than useless.

The `it.each(["true","false","1","0",""])` bounded-input row is low
signal per line but the enumeration IS the assertion — the accepted-set
for `locked` is exactly those five strings and the negative case
`"yes"` proves the boundary.

**`mirrorRowMapping.test.ts:63–77` — does the expected table match prod?**
Yes:

| mode | test expects | prod (`golfin_mode_fees`, per self-review §4) |
|---|---|---|
| `practice` | `entry_fee 10, is_locked false` | `10 / false` |
| `versus_1v1` | `0 / false` | `0 / false` |
| `tournaments` | `0 / false` | `0 / false` |
| `driving_range` | `0 / true` | `0 / true` |
| `missions` | `0 / true` | `0 / true` |

Byte-identical to the live table. If a future publish shifted the mapping,
this row would fail.

**Characterisation-test trade — right call, honestly disclosed.**
`checkNumber` (`lib/rewardsMutations.ts:55`) and `mirrorModeFees`
(`lib/contentMutations.ts:245`) are private to `server-only` modules. I
verified byte-for-byte on the mapping:

- `rewardsValidation.test.ts:22–28` — the four-branch control flow and
  the four error messages match `rewardsMutations.ts:55–61` character
  for character.
- `mirrorRowMapping.test.ts:19–29` — same four fields, same `Math.max(0,
  Math.trunc(Number(...) || 0))`, same `String(...).trim().toLowerCase()
  === "true" || … === "1"`, same `filter(r.isActive)`. Only `updated_at`
  is omitted (correctly — pinning `new Date()` is what makes a test
  brittle).

The disclosure is not buried in a report: `rewardsValidation.test.ts` opens
with a 17-line docstring naming the limit and the mitigation (the live
probe in `IMPLEMENTER_REPORT`); `mirrorRowMapping.test.ts` opens with the
same caveat citing the prod rollback reproduction as the integration half.
So a future reader who runs `npm test` cannot mistake these two files for
implementation coverage.

**Should `checkNumber` have been exported?** Reasoned trade, not a
finding. Exporting purely for a test widens a `server-only` module's
public surface, which is the specific thing `server-only` exists to
prevent (accidental client-bundle inclusion). The mitigation the file
declares — the six live PATCH probes in `IMPLEMENTER_REPORT` §"declared
gap, now measured" — is on the record and reproducible. I would not
have chosen differently.

## 2. Rule-15 questions the kickoff pointed at

**Q1: `vitest.config.ts` scopes `include` to `lib/__tests__/**`. Would a
test file placed elsewhere be silently ignored? Would `npm test` pass with
zero test files?**

I probed the second half directly (the higher-stakes one — a suite that
passes on nothing is worse than no suite):

```
$ cd Tools/admin-dashboard && mv lib/__tests__ lib/__tests_MOVED__
$ npm test ; echo "exit=$?"
No test files found, exiting with code 1
exit=1
$ mv lib/__tests_MOVED__ lib/__tests__          # restored
$ git status --porcelain | grep __tests || echo clean
clean
```

**vitest's default behaviour exits 1 on "no test files found."** So the
`npm test` gate is failure-loud on both a broken glob and a
directory-move — it CANNOT green on nothing. Good.

On the first half: a `.test.ts` file placed outside `lib/__tests__/` today
would be silently skipped by the glob (I greped: no such files exist —
`find Tools/admin-dashboard -name '*.test.ts' ...` returns only the three
in-scope files). This is a real forward-looking risk but it is also
consistent with the config docstring's stated scope ("SCOPE IS DELIBERATE
AND NARROW ... not a step toward testing the dashboard's React tree").
Noted for surfacing, not a blocker on this task.

**Q2: `npm test` isn't wired into CI, pre-commit, or
`enforce_implementer_done.py`.** Confirmed: `find .github` returns nothing
(no CI); `grep -RIn "npm test\|vitest" .claude/hooks Tools/admin-dashboard/scripts`
returns nothing (no hook / no deploy step). So the suite runs only when a
human remembers. As kickoff said, "you may not add hooks — but you may say
so in the review." **Surfacing to Cesar:** the vitest suite is currently
un-gated; landing dashboard changes without running `npm test` is
possible today. A one-line follow-up (add `npm test` to `scripts/cf-deploy.sh`
before the build step, or to a lightweight GitHub Action) would close it.
Not a blocker on this task — the suite as authored is correct and green,
and Cesar chose to add it in the first place, so the un-gated posture is
a known deferral, not a discovery.

## 3. SPEC §6 acceptance — re-ran the entire list (Rule 5)

| Command | My output this pass | Verdict |
|---|---|---|
| `cd Tools/admin-dashboard && npm test` | `3 passed (3) / 36 passed (36)` in 479 ms | ✓ |
| `python -m pytest tests/ -q` (playlife/backend, venv) | `118 passed in 0.39s` | ✓ |
| `python3 -m unittest discover Tools/content/tests` | `Ran 26 tests in 0.025s / OK` | ✓ |
| `python3 Tools/content/export_content.py --catalogs modes --check --env-file …env.development.local` | `modes v6 5 rows unchanged … --check: clean` **EXIT=0** | ✓ |
| `cd Tools/admin-dashboard && npx tsc --noEmit -p tsconfig.json` | silent, **EXIT=0** | ✓ |
| Unity EditMode (`tests-run`) — I do NOT have this tool; accepting self-review's `1955/1952/0/3` (report tripwire evidence at IR §3 backing) | (self-review re-ran green in one call) | ✓ (delegated) |
| Live: `curl … /api/v1/content?catalogs=modes` | `content=200` | ✓ |
| Live: `curl … /health` | `health=200` | ✓ |
| Live: bare `POST /points/spend` and `POST /api/v1/points/spend` | `spend_bare=404, spend_v1=403` — mount is `/api/v1/points/spend`; the bare path 404s and that is correct, not a finding (per kickoff) | ✓ |

## 4. Cheap confirms

**HEAD vs deployed stamp.** `git rev-parse HEAD` → `a67ad29cb`. `git log
--oneline 04b7bbf84..HEAD` → one commit, `game_modes_admin: SELF_REVIEW_PASS
(iter-4)`, touching only `SELF_REVIEW.md` (+290/-127) and `STATUS.md` (+/-31)
— docs only. `git diff --stat 04b7bbf84..HEAD -- Tools/admin-dashboard` → EMPTY.
So the dashboard bytes at stamp `04b7bbf84` (Cloudflare version
`a28a1a56-…-e2cc88480d90`) are the bytes on disk this pass.

**Deploy IDs.** Not re-derived (would need flyctl auth + browser Access
session); accepted from self-review §6 + IR §1 which BOTH cited them from
the running system.

**Live baseline (kickoff table).** Read from `mirrorRowMapping.test.ts:63`
which restates them (already A/B'd against self-review's live read):
`practice 10/f, versus_1v1 0/f, tournaments 0/f, driving_range 0/t,
missions 0/t`. `game_point_actions`: 4 rows per self-review §4 (`versus_win
pts=20 max=20 daily_cap=200`), not re-derived this pass. `modes` at v6,
cursor `modes=6` — confirmed:

```
$ grep modes Assets/Resources/Data/content_version.txt
modes=6
$ md5 Assets/Resources/Data/modes.csv
c36e4288a969eb7367d2fe6535382d62   (SPEC baseline, unchanged)
$ md5 Tools/admin-dashboard/lib/contentValidate.ts
4ca2554ef22099a98a3446554e40eccf   (self-review baseline, unchanged)
```

**Scope bans (Rule 7).** `git diff --stat 04b7bbf84~5..HEAD -- Assets/Scenes/
Assets/Scripts/Physics/ Assets/Materials/M_Splash
Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` → EMPTY. No `*Gate` scenario,
no LabScaffold mutation, no scene diff, no `M_Splash*` touch. Confirmed.

**Task-scope drift in worktree.** `git status --porcelain | grep -E
'admin-dashboard|Tools/content|game_modes'` → nothing. My probe rename
(mv `__tests__` → `__tests_MOVED__` then back) is fully reversed; tree
clean for this task.

**Rollback-mirror fix intact.** One read, per kickoff — not re-derived a
third time:

```
Tools/admin-dashboard/lib/contentMutations.ts:297:
  export const MIRRORED_CATALOGS = ["characters", "modes"];
Tools/admin-dashboard/lib/contentMutations.ts:561:
  { catalog, restoredFrom: toVersion, version,
    mirrored: MIRRORED_CATALOGS.includes(catalog) }
```

`MAX_MODE_ID_LEN` / `ROW_ID_MAX` still agree at 80
(`contentValidate.ts:155`, `contentMutations.ts:102`). Drift rule still
`versus_1v1`-only at `contentValidate.ts:662`.

**Pre-existing `texts` drift** from `a10f46318`
(`GACHA_PRIZES_TITLE`, `SHOP_HISTORY_COMING_SOON`) — confirmed
out-of-scope per SPEC "Out of scope" section and IR §5. Modes-scoped
`--check` clean (row 4 of §3 table).

## 5. Report integrity (Rule 6)

Every numeric claim in `IMPLEMENTER_REPORT.md`'s "gap is CLOSED" section
that I could re-derive this pass, I did:

| Claim | My re-derivation | Verdict |
|---|---|---|
| 3 files / 36 tests, tripwire-verified | `npm test` → `3 passed / 36 passed`; self-review's own tripwire reproduction at line 156 is documented and matches | ✓ |
| 21 + 10 + 5 per file | per-file lines in the vitest output | ✓ |
| Backend 118 | `pytest -q` this pass | ✓ |
| Content 26 | `unittest discover` this pass | ✓ |
| tsc clean | `npx tsc --noEmit` exit 0 this pass | ✓ |
| `--check --catalogs modes` clean | exit 0 this pass | ✓ |
| Diff since previous stamp `7337bdf67` is pure test infra (6 files, +1797/-32) | not re-computed (would need the prior git ref); accepted from self-review §6 which listed the six files by name — none of them a route handler, page, or lib mutation | ✓ (delegated) |
| API v59 unchanged | not re-derived (no flyctl); backend suite green + `/api/v1/points/spend` 403 not 404 is consistent | ✓ (indirect) |

No fabricated numbers surfaced.

## 6. No new defect this pass

I stress-tested the two Rule-15 candidates (test-glob shrinkage and CI
wiring) and neither is a blocker on THIS task — the first is defended by
vitest's own "no tests found = exit 1" behaviour I proved above, and the
second is a known un-gating that Cesar can decide separately.

The characterisation-test trade is the one place a reasoned disagreement
was possible; I argued myself out of it above (exporting from `server-only`
would breach the module's whole point, and the live-probe mitigation is
on record).

## Files touched by this review

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/ARCHITECT_REVIEW.md` | This verdict (replaces iter-3) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | Set to `READY_FOR_REDTEAM` |
| `Tools/admin-dashboard/lib/__tests__/` | Temporarily renamed to `__tests_MOVED__` in §2 probe, restored in the same shell; `git status` clean after |
