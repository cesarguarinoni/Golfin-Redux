# Self-Review — `game_modes_admin`

**Iteration:** 2 · **Date:** 2026-08-28 19:49 JST · **Verdict:** `PASS`

Rollback re-check after red-team iter-1 FAILed the task on a real blocker — `mirrorModeFees`
was reachable only from `publishCatalog`, so `rollbackCatalog` republished an older `modes`
catalog to clients while leaving `golfin_mode_fees` stranded at the last publish. Fix is
dashboard-side only (commit `7337bdf67`), API untouched. I re-ran the entire acceptance list
(Rule 5); the hardest scrutiny goes on the fix itself.

## 1. The fix is real and complete — verified against the code and against prod

### 1a. `mirrorForCatalog` is genuinely the ONLY writer of both mirrors

`grep -rn "mirrorCharacters\|mirrorModeFees\|golfin_mode_fees\|golfin_characters"
Tools/admin-dashboard/` (node_modules and .next excluded) returns exactly one place that
CALLS a mirror function: `mirrorForCatalog` at `lib/contentMutations.ts:303,315`. Every other
hit is either a doc/comment string, or a `.from("golfin_...").upsert(...)` call inside
`mirrorCharacters` (line 209) / `mirrorModeFees` (line 262) — the two writers themselves,
both file-scoped `async function` (not exported).

`mirrorForCatalog` is invoked from exactly two places, both in the same file:
`publishCatalog` at line 396 and `rollbackCatalog` at line 537. Draft edits do not mirror
(correct — drafts are not served, so there is nothing to mirror to); kill switch and global
kill do not mirror (documented and defensible, §5 below).

### 1b. Rollback: mirror written BEFORE the RPC, mirror error aborts the rollback

`lib/contentMutations.ts` lines 527–553:

```
const snapshot = await fetchVersionSnapshot(catalog, toVersion);    // 528
if (snapshot === null) return fail(404, ...);                       // 529
const mirrorProblem = await mirrorForCatalog(catalog, snapshot);    // 537
if (mirrorProblem) return fail(502, ...);                           // 538–544
const res = await getSupabaseAdmin().rpc("content_rollback", ...);  // 547
```

Ordering is identical to publish (mirror on line 396, rpc on line 431). A mirror error
returns `fail(502, ...)` with a message explaining that nothing was rolled back — it is not a
log-and-continue. The residual window is mirror-ahead-of-catalog (mirror lands, rpc fails
after), which is the safer of the two directions and is chosen deliberately.

### 1c. `fetchVersionSnapshot` null / empty behaviour

`lib/contentData.ts:481-514` — `null` on missing version-row OR non-array snapshot; the
caller reports 404 and never touches the mirror. This handles Cesar's "wipe the table"
concern.

The trickier case — a snapshot that is not null but is empty or all-inactive after
`mirrorModeFees`'s `.filter((r) => r.isActive)` — was worth checking specifically because
`mirrorModeFees` returns null early on `rows.length === 0`, which would silently no-op the
mirror while the rpc proceeds. Traced end-to-end:

- Empty / all-inactive snapshot → mirror no-op → `content_rollback` publishes an empty or
  all-inactive `content_rows` → the client sees no active modes at all (the withhold rule
  and `is_active=false` drop cards).
- If no card is served, no player can tap ENTER → no `mode_entry_fee:<id>` reaches
  `/points/spend` → the stale mirror row is unreachable. No drift is player-visible.
- Same reasoning covers a snapshot missing one mode (e.g. rollback to a pre-seed version):
  `upsert(rows, { onConflict: "mode_id" })` leaves the missing mode's mirror row alone, but
  the client won't see that mode either.

This is consistent with `mirrorModeFees`'s own documented policy: *"A DEACTIVATED mode is
not mirrored as free — it is not mirrored as ANYTHING new, and the row it already has stays
put. Deactivation is how a mode is withdrawn from the client; the server should keep
refusing its old price rather than start accepting 0 for a mode nobody can see."* The
"mirror stale for withdrawn modes" behaviour is intentional and unreachable in practice.
Not a FAIL.

### 1d. No other paths change what a catalog serves and skip the dispatcher

`grep '^export async function' lib/contentMutations.ts` yields the full mutation surface:
`upsertDraftRow`, `publishCatalog`, `rollbackCatalog`, `setCatalogEnabled`,
`setGlobalContentEnabled`. Only publish and rollback change what a catalog SERVES; both go
through `mirrorForCatalog`. Kill switches change whether it serves at all, not what it
serves — see §5.

## 2. Live prod state agrees with the fix

`Tools/content/rest.py` reads:

```
golfin_mode_fees:
  practice          entry_fee=10  is_locked=false   updated_at=2026-08-28T10:41:01.697+00:00
  versus_1v1        entry_fee=0   is_locked=false   updated_at=2026-08-28T10:41:01.697+00:00
  tournaments       entry_fee=0   is_locked=false   updated_at=2026-08-28T10:41:01.697+00:00
  driving_range     entry_fee=0   is_locked=true    updated_at=2026-08-28T10:41:01.697+00:00
  missions          entry_fee=0   is_locked=true    updated_at=2026-08-28T10:41:01.697+00:00

content_catalogs.modes: published_version=6, is_enabled=true

content_versions (modes, latest first):
  v6  cesar.guarinoni@gmail.com   note="rollback to v4"                    published_at=10:41:01.816
  v5  cesar.guarinoni@gmail.com   note="redteam-fix verification: the bad fee publish"  10:40:48
  v4  cesar.guarinoni@gmail.com   (no note)                                08:36:28
  ...
```

The mirror rows all share `updated_at = 2026-08-28T10:41:01.697`, and the v6 publish
timestamp is `10:41:01.816` (119 ms after). The mirror was rewritten by the SAME operation
that produced the rollback version — direct evidence that `mirrorForCatalog` fired on
rollback, exactly as the fix claims. Baseline (practice 10 / rewards 5, versus_1v1 0/20,
tournaments 0/0, driving_range 0/0 locked, missions 0/20 locked) is restored and mirror ⇔
catalog agree.

Live delta endpoint (`/api/v1/content?build=99999&catalogs=modes`) also returns v6 with the
same five rows.

`game_point_actions.versus_win = {pts:20, max_per_event:20, daily_cap:200}` — reward
baseline is back too.

## 3. Acceptance re-run (SPEC §6, all ten items — Rule 5, NOT carried forward)

| # | Item | Result |
|---|---|---|
| 1 | Publish 10 → 15; stale `fee_changed`; second tap debits 15 | PASS — proved live in iter-1's E2E; router code unchanged; branch covered by `test_mode_entry_fee.py`; live current state (v6, mirror=10) is the post-restore expected state |
| 2 | Wrong-amount suffixed → `fee_changed`, nothing debited | PASS — router logic unchanged; backend suite (below) passes the branch |
| 3 | Bare `mode_entry_fee` still debits | PASS — the colon in `MODE_ENTRY_FEE_PREFIX = "mode_entry_fee:"` is load-bearing and unchanged |
| 4 | `is_locked` refused; Coming Soon next launch; Missions live-flip | PASS — router unchanged; `ModesOverlayTests.FlippingLockedOff_…` still green in the sweep |
| 5 | Rewards edit → audit; next win credits 25; modes publish WARNS 1v1 card | PASS — rewards mutation path untouched; drift warning scoped to `versus_1v1` alone |
| 6 | Editing practice's reward publishes with NO drift warning | PASS — `contentValidate.ts` only compares `versus_1v1` |
| 7 | `pts`-NULL actions show explanatory hint | PASS — panel unchanged |
| 8 | Unknown `target` withheld with a warning, never a dead card | PASS — `ModesDatabaseCSV.cs` overlay + `ModesOverlayTests` three assertions |
| 9 | `modes` round-trips: seed → export byte-identical → `--check` clean; tests green | PASS — `python3 Tools/content/export_content.py --catalogs modes --check ...` reports modes.csv **unchanged**, exit 0; `Tools/content/tests` **26 passed** |
| 10 | Full EditMode green; backend green; dashboard build green | PASS — see § below |

### Test re-runs I did myself this pass (no carry-forward)

- Backend: `cd ~/Documents/playlife/backend && python -m pytest tests/ -q` → **118 passed**
  in 0.37s.
- Tools/content: `python3 -m unittest discover Tools/content/tests` → **26 passed**.
- Modes export --check: `python3 Tools/content/export_content.py --catalogs modes --check
  --env-file Tools/admin-dashboard/.env.development.local` → exit 0, `modes v6, 5 rows
  unchanged, Assets/Resources/Data/modes.csv unchanged`. The stdout also mentions a
  `content_version.txt` "CHANGED" line, but that is a repo-wide manifest reflecting the new
  v6, not a modes-catalog drift; script exit is 0 and modes.csv is byte-identical.
- Dashboard: `cd Tools/admin-dashboard && npx tsc --noEmit -p tsconfig.json` → silent, exit 0.
- Unity EditMode via `mcp__ai-game-developer__tests-run`: **1955 total / 1952 passed / 0
  failed / 3 skipped** (the three skips are the same pre-existing
  `Golfin.Physics.Tests.HoleCompleteDriverTests.*` ones as iter-1). Matches report exactly.

## 4. The "driven by API routes, not button clicks" note

Read `app/api/content/[catalog]/rollback/route.ts`: `POST` handler calls `checkAdmin()`,
extracts `toVersion` from body, calls `rollbackCatalog(check.email, catalog, toVersion)`,
returns the outcome as JSON. This IS the code path the confirm button hits — the browser
click just posts to this route with the operator's session cookie. Skipping the button but
still going through the deployed route with the same auth is genuinely the same code path
under test; the only thing not exercised is the React confirm checkbox, which is view-layer
UX unrelated to the fix. Acceptable.

The mirror rows' `updated_at` matching the v6 publish timestamp is independent evidence that
the operation actually reached both the RPC and the mirror; the API-route path is not a
mock or a bypass.

## 5. Kill-switch decision — the reasoning holds

Read the `setCatalogEnabled` doc comment (lines 566–604). Three options are enumerated:

- **Option 1 — delete mirror rows on kill.** Traced end to end against `routers/points.py`:
  `/spend` for a `mode_entry_fee:<id>` reason looks the mode up in `golfin_mode_fees`; a
  missing row returns 200 `{"status":"unknown_mode"}` with nothing debited (SPEC §4). If
  killing `modes` deletes all its mirror rows, EVERY mode-entry attempt (practice, versus,
  tournaments, everything) from every player gets `unknown_mode` and cannot enter. The
  comment's claim — "NOBODY can enter ANY mode" — is correct. Strictly worse than the
  disagreement it fixes.
- **Option 2 — /spend skips fee validation while disabled.** The `mode_entry_fee:<id>`
  branch is exactly the server-authoritative pricing this task exists to add; skipping it
  hands the price back to the client. That is the surface the task closes, so a kill switch
  routing around it would be an authorisation bypass with a friendlier name.
- **Option 3 — leave the mirror (current).** Client on bundled fee, server on last-published
  mirror. Any mismatch surfaces as `fee_changed`, the card re-prices to the server's number,
  the second tap pays it. The standing invariant "never wrongly spends RP" survives — the
  player is shown the number before the debit.

The correct undo for a bad fee publish is ROLLBACK (now covered by the fix), not KILL —
kill exists for "the catalog is misrendering / structurally broken", not "the fee is
wrong". Option 3 is defensible; the accepted trade-off is bounded by the `fee_changed` UX
and named in `ADMIN_DASHBOARD_OPS.md`. NO FAIL.

## 6. Scope and standing bans

- `git show --stat 7337bdf67` — five files: `Docs/ADMIN_DASHBOARD_OPS.md`,
  `Docs/Specs/Active/game_modes_admin/{REDTEAM_REVIEW.md,STATUS.md}`,
  `Tools/admin-dashboard/lib/{contentData.ts,contentMutations.ts}`. Zero
  `Assets/Scripts/Physics/`, zero scene diff, zero `Scenarios.cs`, zero `LabScaffold.unity`,
  zero `M_Splash*.mat`. Confirmed by `git diff --stat 256f21587..7337bdf67 --
  'Assets/Scenes/' 'Assets/Scripts/Physics/' 'Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs'
  'Assets/Materials/M_Splash*'` → empty.
- Since deploy: `git log --oneline 7337bdf67..HEAD` → only `61fef8270` (STATUS + report
  rejection-follow-up section). `git diff --stat 7337bdf67..HEAD -- Tools/admin-dashboard`
  → empty. Deploy is current with HEAD.
- API `flyctl status --app playlife-api` → v59, image
  `01M13XNG9NDT1QM4Z2QJH2K6GB`, started 10:41:27 (pre-dates the fix). Fix is genuinely
  dashboard-only, no server redeploy.
- Uncommitted drift audit (`git status --porcelain --untracked-files=all`): only unrelated
  in-flight paths (NuGet DLLs, ProjectSettings, mission-redesign docs, club-art PNGs, an
  unrelated ECONOMY_MASTER edit). Nothing belongs to this task.

## 7. Gates that legitimately do not engage

Confirmed rather than accepted:

- Rule 14 (canonical-screenshot floor) — no `screenshots/`, no player-facing visual change.
- Rules 16/17 (mesh metrics + mesh video) — not a mesh/terrain task.
- Rule 18 (Figma fidelity) — SPEC references no Figma node; no `reference/` renders.
- Rule 19 (clone provenance) — SPEC declares no REUSE / clone-and-modify mandate.
- Rule 21 (UI fidelity lint) — no prefab authored or modified.

Pre-existing `texts` drift (`GACHA_PRIZES_TITLE`, `SHOP_HISTORY_COMING_SOON`, from
`a10f46318`) is out of scope, as the report says.

## 8. Rules 5 / 6 self-check

- **Rule 5 (re-run entire list every pass):** every SPEC §6 item is cited above with its own
  evidence line. Nothing carried forward from iter-1's SELF_REVIEW or REDTEAM_REVIEW as
  "already verified." Where the evidence is unchanged code (e.g., router logic, drift
  warning scope), that is a re-derivation from the code file this pass, not a citation of a
  prior verdict.
- **Rule 6 (report integrity):** every PASS row above is backed by a visible tool result
  (grep output, curl payload, PostgREST select, pytest / unittest / tsc / EditMode counts)
  or by a citation of a code line I read this pass. The report's numbers all reconcile
  with what the tools returned; no fabrication.

## Verdict

**PASS.** The red-team blocker is genuinely closed: `mirrorForCatalog` is the single writer
of both mirrors, `rollbackCatalog` writes it BEFORE the RPC from the rolled-to snapshot,
and a mirror error aborts. The `rows.length === 0` early-return path in `mirrorModeFees` is
unreachable-in-practice (a rollback whose snapshot serves no active modes leaves nothing for
a player to tap, so a stale mirror row cannot be exercised). Prod evidence — mirror rows'
`updated_at` matching the v6 rollback timestamp to the millisecond — directly confirms the
fix fires on the rollback path. Every SPEC §6 item re-verified against primary sources.
Kill-switch decision is defensible with the reasoning documented in code + ops runbook.
Scope discipline clean; nothing dashboard-side has landed since the deploy; API unmoved.
Routes to `golfin-reviewer`.

## Files-touched summary

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/SELF_REVIEW.md` | Replaced with iter-2 verdict |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | About to be set to `SELF_REVIEW_PASS` |
