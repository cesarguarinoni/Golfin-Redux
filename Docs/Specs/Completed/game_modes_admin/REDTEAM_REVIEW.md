# Red-Team Review — `game_modes_admin` (iter-4)

**Gate:** `golfin-redteam-reviewer` (adversarial) · **Date:** 2026-08-28 20:55 JST
**Verdict:** `ARCHITECT_REVIEW_PASS` — I attacked the newest, least-reviewed code
(the deploy gate + the vitest suite) three ways and the SHIPPING code held every
time. The one thing I broke is a documentation-worthy suite blind spot that does
not affect correctness. My iter-3 escalation is resolved: Cesar chose "add the
suite first," the suite is real and non-vacuous, and the live-probe gap I could
not close is now closed and corroborated against primary source.

---

## 1. The deploy gate (`scripts/cf-deploy.sh`) — the newest code, verified

Read in full. Reproduced its shell semantics in an isolated harness (not a real
deploy):

| Question (kickoff #1) | Verdict | Evidence |
|---|---|---|
| Does `set -euo pipefail` + `if ! npm test` abort cleanly, or exit before the ABORT / swallow it? | **Correct** | `if !`-condition context disables errexit for the tested command. Isolated harness: failing test → prints `✘ ABORT`, `exit 1`, **never reaches build**. Passing test → reaches build. |
| Env-stash trap — do the tests behave differently inside the stash window? | **No — tests are pure** | `vitest.config.ts` sets no `env`/`dotenv`/`setupFiles`; test files have zero `process.env`/`import.meta.env` reads (grep clean); vitest mode is `test`, so it would never load `.env.development.local` (a *development*-mode file) even if it loaded env. Empirically identical: 36 pass standalone. |
| Does an aborted deploy still restore the env file? | **Yes** | `trap restore EXIT INT TERM`. Harness: stash → failing gate → `exit 1` → trap fires → `.env.development.local` restored, contents intact, no dangling stash. |
| Is `SKIP_TESTS=1` loud and does it still deploy? | **Yes** | Prints `⚠ SKIP_TESTS=1 … On your head.` and falls through to build+deploy. |
| Could the gate change the stamp (run before the dirty check)? | **No** | `BUILD_COMMIT` + dirty check are computed at the very top, before the stash AND before the test gate. `npm test` left zero working-tree drift (`git status` clean), so it cannot dirty a future stamp; the stash file is `.gitignore`d anyway. |

## 2. The suite as an adversary — one real blind spot found (not a blocker)

- **`contentValidate.test.ts` is genuinely non-vacuous.** It imports the REAL
  `@/lib/contentValidate`. Probe A: I disabled the real order-clash `err` →
  `refuses a duplicate order` went **RED** (1 failed / 35 passed). Reverted.
- **Blind spot (kickoff #2 confirmed):** the order-uniqueness rule is exercised
  only at *exactly 2 rows*. Probe B: gating the real check on `rows.length < 3`
  breaks it for 3+ row catalogs (the shipped `modes` catalog has **5**), yet all
  **36 stay green**. This is a suite-thoroughness gap, **not a shipping defect** —
  the real code errors on any clash regardless of row count (verified). Reverted;
  tree clean; 36 green again.
- **The two `server-only` files are self-disclosed characterisation tests** —
  `checkNumber`, `field`, and the mirror row-mapping are re-implemented in-test,
  so they cannot catch the real modules drifting. I confirmed the copies are
  **faithful to current source** (byte-for-byte for `checkNumber`; matching logic
  for `field` and `mirrorModeFees`). The disclosed integration backstop is the
  six live probes + the prod rollback reproduction. Honest, reasonable tradeoff.

## 3. Report integrity on the newest claims (Rule 6) — no fabrication

| Claim | My re-run / re-derivation this pass | Verdict |
|---|---|---|
| `npm test` = 36 | ran → **36 passed (3 files: 21/10/5)** | matches |
| cf-deploy.sh tripwire (exit 1, no build) | reproduced the gate abort in isolation → exit 1, build unreached | consistent |
| six-probe table (400×5 / 404, 4 rows, versus_win 20/20/200) | could not run browser probes (no browser/service key — same boundary as iter-3), but rewards diff `7337bdf67..HEAD` is **empty** (guards I read ARE deployed bytes) and 404-no-create is **structural** (`fetchRewardAction`→404, `.update().eq()`, no upsert/insert) | corroborated |

## 4. Code nobody looked at since iter-1 (kickoff #4)

- **`ModeCardController.HandleSpendDenied`** — correct. `FeeChanged` re-renders at
  `outcome.ServerFee` and does **not** auto-debit (the whole anti-charge-unshown
  point); `UnknownMode`/`ModeLocked` re-render from DB; null-guarded.
- **`ModesDatabaseCSV` withhold rule** — **NOT log-only**. `BuildMode` returns
  `null` on `!CanDispatch(target)`; both call sites (bundled + overlay-append)
  do `if (mode == null) { withheld++; continue; }`, so an unroutable mode is
  genuinely excluded, not merely logged.
- **Fallback fees** — display-only (server prices authoritatively via
  `golfin_mode_fees`; `fee_changed` corrects any skew before a charge) and they
  match the prod baseline anyway. Runs only if the CSV fails to load.

## 5. Baseline confirmations (all re-run this pass)

- HEAD == `3143fd639`; `git diff --stat 3143fd639..HEAD -- Tools/admin-dashboard`
  **empty**.
- Cursor `Assets/Resources/Data/content_version.txt` → `modes=6`; `modes.csv`
  md5 `c36e4288a969eb7367d2fe6535382d62` (matches report).
- Scope bans: `git diff --stat 256f21587..HEAD` over Scenes / Physics /
  Scenarios.cs / Materials → **empty**. No Unity C# churn since the EditMode
  baseline commit (only `content_version.txt` changed under `Assets/`), so
  1955/1952/0/3 remains valid.
- `tsc --noEmit` **exit 0**; content tests **26 OK**.
- **Accepted from prior gates** (access boundary, not skipped): `--check
  --catalogs modes` exit 0 and backend 118 both need prod service key /
  playlife repo absent from this checkout; API unchanged at v59 (fix is
  dashboard-only). No code churn invalidates either.
- Gates 14/16/17/18/19/21 do not engage (server + tests, no Unity UI/mesh/Figma).
- Pre-existing `texts` drift (`a10f46318`) is genuine and out of scope.

## 6. Three break-attempts and why each failed

1. **Deploy-gate shell semantics** — tried to make a test failure slip through to
   a real deploy, or leave the developer without their env file. Both held: abort
   is clean and before the build; the trap restores on abort.
2. **Green-while-broken** — broke the real validator two ways. The direct break
   went red (coverage is real); the 3+-row-scoped break stayed green (a blind
   spot in the suite, reported — but the shipping code is correct).
3. **Report fabrication** — re-ran the suite, reproduced the gate, and derived the
   probe results from deployed==disk + structural no-create. Every number
   consistent; nothing invented.

## Non-blocking note for Cesar

The deploy gate's protective value has two documented limits worth a future
hardening ticket (neither blocks this task): (a) the order-uniqueness rule is
only tested at 2 rows, so a regression scoped to realistic 5-row catalogs would
pass the gate; (b) the two `server-only` modules are characterisation-tested, so
the gate cannot catch them drifting from their pinned rules. Both are disclosed
in the code/report and backstopped by the live probes. The suite satisfies your
"add it first" decision — `contentValidate`, the one place a bad publish is
stopped, is now genuinely under test.

## Files touched by this review

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/REDTEAM_REVIEW.md` | This verdict (replaces iter-3) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | Set to `ARCHITECT_REVIEW_PASS` |
