# Architect Review — `game_modes_admin`

> Written by `golfin-reviewer`. Verdict on this pass: **PASS** (advances STATUS to
> `READY_FOR_REDTEAM`). The adversarial red-team gate is the ONLY agent allowed to
> write `ARCHITECT_REVIEW_PASS`.

## Independent visual scan

The task folder contains no `screenshots/`, `videos/`, or `reference/` directory,
and that is correct rather than missing evidence: the deliverable is a Postgres
mirror + `/points/spend` code path in `~/Documents/playlife`, a tenth content
catalog, two admin dashboard panels (Cloudflare-deployed Next.js), and a Unity
data-loading / spend-verdict change with no new visuals. There is no
pixel artifact to describe, no Figma node to A/B, and no player-facing UI change
that would benefit from a canonical PNG. Steps 1–2 of the visual review checklist
are legitimately N/A; the derived-not-confirmed primary-source pass below is
what stands in for them.

## Gates that do not engage on this task, why, and what I checked instead

- **Rule 14 (canonical screenshot ≥ 900px)** — the report cites no `screenshots/*.png`
  at all, so the rule never engages. Verified by directory listing (no `screenshots/`
  folder) and by grepping `IMPLEMENTER_REPORT.md` for `.png` (none).
- **Rules 16/17 (mesh metrics + mesh video)** — not a mesh/terrain task; SPEC touches
  no `green.json`, no `TerrainData`, no mesh cut/deform, no `GreenTopology`.
- **Rule 18 (Figma fidelity table)** — SPEC references no Figma node (no `figma.com`
  URL, no `<n>:<n>` node-id). Nothing to diff.
- **Rule 19 (clone provenance)** — SPEC declares no REUSE / clone-and-modify mandate.
  No "§0 REUSE MANDATE", no "author zero new panels" language.
- **Rule 21 (UI fidelity lint)** — no prefab authored or modified. `git show --stat
  256f21587` touches zero `.prefab` files.

Effort instead went into re-running every suite the report cites, hitting the
live prod endpoints myself, and reading the load-bearing code paths.

## Primary-source verification (derive, do not confirm)

- **Fly image** — `~/.fly/bin/flyctl status --app playlife-api` returns
  `playlife-api:deployment-01M13XNG9NDT1QM4Z2QJH2K6GB`, **v59** on both nrt
  machines. Matches the kickoff brief's expected v59 exactly (the report cited
  v58 pre the `MAX_MODE_ID_LEN` fix; v59 is the redeploy of `89508c5`). ✅
- **Dashboard staleness** — `git diff --stat 256f21587..HEAD -- Tools/admin-dashboard`
  → empty. Zero commits touch the dashboard since the task commit, so the
  Cloudflare deploy stamped `256f21587` is not stale. ✅
- **Live prod** — `GET /api/v1/content?build=99999&catalogs=modes` returns
  `latest_version=4`, `modes.version=4`, `practice.entryFee=10`, `locked=false`,
  `target=hole_select`, `driving_range.locked=true`, `missions.locked=true` —
  the restored state the report describes. `/health` 200; `POST /points/spend`
  403 (mounted, auth-gated); garbage route 404. ✅
- **Backend suite** — `cd ~/Documents/playlife/backend && venv/bin/python -m
  pytest tests/ -q` → **118 passed**. Matches. ✅
- **Tools/content** — `python3 -m unittest discover Tools/content/tests` →
  **26 passed**. Matches. ✅
- **Scoped catalog check** — `python3 Tools/content/export_content.py --catalogs
  modes --check --env-file Tools/admin-dashboard/.env.development.local` → exit
  0, `modes v4, 5 rows unchanged, byte-identical`. Matches. ✅
- **Dashboard TS** — `cd Tools/admin-dashboard && npx tsc --noEmit -p
  tsconfig.json` → silent, exit 0. Matches. ✅
- **Unity EditMode** via `mcp__ai-game-developer__tests-run` → **1955 total /
  1952 passed / 0 failed / 3 skipped** (the three skips are
  `Golfin.Physics.Tests.HoleCompleteDriverTests.*`, pre-existing, unrelated to
  this task). Matches report byte-for-byte. ✅

## The three claims worth attacking, independently held up

**a. Legacy bare `mode_entry_fee` STILL debits.** Read `backend/routers/points.py`.
Line 142: `MODE_ENTRY_FEE_PREFIX = "mode_entry_fee:"` — the colon IS part of the
constant. Line 480: `if reason.startswith(MODE_ENTRY_FEE_PREFIX):`. A bare
`mode_entry_fee` (no colon) fails `startswith`, falls straight through to line
501's `reason = reason[:MAX_REASON_LEN]` and then to the `spend_pts` RPC — the
same path today's installed builds have used all along. `mode_entry_fee_refund`
also fails the prefix (no colon). The test suite explicitly asserts
`the_bare_legacy_reason_still_debits`, `the_bare_legacy_reason_does_not_even_
read_the_mirror`, and `a_reason_that_merely_starts_similarly_is_not_swept_up`.
This is the single highest-severity check in the task and it holds. **PASS.**

**b. Mirror write actually FAILS the publish.** Read
`Tools/admin-dashboard/lib/contentMutations.ts` lines 344–354: after
`mirrorModeFees(drafts)`, if `mirrorError` is truthy the function `return`s a
502 with the message "golfin_mode_fees mirror write failed, so nothing was
published". This is placed BEFORE the `content_publish` RPC at line ~383. If
the mirror fails, the RPC never runs. Same shape as the characters mirror path
at lines 333–342. **PASS.**

**c. Drift warning scoped to EXACTLY `versus_1v1` ↔ `versus_win`, WARN not
error.** Read `lib/contentValidate.ts` lines 606–670. `rows.find((r) => r.rowId
=== "versus_1v1")` is the only pairing, comparing against `ctx.versusWinPts`.
Comments at 606–612 state emphatically *"THE DRIFT WARNING COVERS EXACTLY ONE
PAIR, BY DECISION"*. Emits `warn(...)` (line 667–670), not `error(...)`. No
other mode/action pairing exists in the file. **PASS.**

**d. `MAX_MODE_ID_LEN` bound-mismatch fix.** `backend/routers/points.py:160`
carries `MAX_MODE_ID_LEN = 80` — matches `ROW_ID_MAX = 80` in
`Tools/admin-dashboard/lib/contentValidate.ts`. The regression test
`test_the_length_bound_matches_the_admin_s_row_id_ceiling` asserts
`>= 80` (the "not tighter than the mint" property, not "80 is correct"). Fly
runs commit `89508c5` (v59 image ID confirmed above), so the fix is live. **PASS.**

**e. Single-source withhold list.** Read
`Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs`. Const targets are
declared at lines 40–49; `DispatchableTargets` (line 57) is built from those same
symbols; the `switch (mode.target)` at line 199+ uses the same const names, not
string literals. `ModesDatabaseCSV` gates every append/patch through
`CanDispatch(target)` (per report + code). One list, symbol-referenced three
ways — adding a case without a const would not compile. **PASS.**

## Acceptance checklist (SPEC §6, all 10 items — Rule 5)

| # | Item | Verdict | How I derived it |
|---|---|---|---|
| 1 | Publish 10→15; stale `fee_changed`; second tap debits 15 | PASS | Router lines 480–500 read directly; 17-test suite covers fee_changed/matches/unknown/locked; report §2 live-E2E on prod. State restored; declined to re-run destructive E2E (would need to mutate prod). |
| 2 | Wrong-amount suffixed → `fee_changed`, nothing debited | PASS | Line 498–499 returns before the `spend_pts` RPC; test `a_wrong_amount_is_fee_changed_and_debits_nothing` covers it. |
| 3 | Bare `mode_entry_fee` still debits | PASS | See §a above — colon is load-bearing; two tests assert. |
| 4 | `is_locked` refused server-side + Coming Soon next launch + Missions live-flip | PASS | Router line 494–495 (`mode_locked` before RPC); `ModesOverlayTests.FlippingLockedOff_MakesAComingSoonModePlayableWithNoBuild` in EditMode sweep. |
| 5 | Rewards edit → audit before/after; next win credits 25; publish WARNS the 1v1 card | PASS | Drift-warning scope verified above; report shows live audit row `points_action_update` and the `POST /points/earn-game` awarding 25 — I can't re-run this destructively but the mechanism holds. |
| 6 | Editing practice's reward publishes with NO drift warning | PASS | Only `versus_1v1` is compared in the validator (§c); grep-swept the file, no other mode/action pair. |
| 7 | `pts`-NULL actions show explanatory hint | PASS | `rewards-panel.tsx` (296 lines added) + `lib/i18n.ts` diff shows paired EN/JA hint keys. |
| 8 | Unknown `target` withheld with a warning, never a dead card | PASS | Single-source symbol table in `ModeSelectScreenController` (§e); overlay gates via `CanDispatch`; 3 `ModesOverlayTests` assertions. |
| 9 | `modes` round-trips: seed → export byte-identical → `--check` clean; 26 tests green | PASS | I re-ran `--check --catalogs modes` (exit 0, unchanged) and `unittest discover Tools/content/tests` (26 pass) myself. |
| 10 | Full EditMode green; backend; dashboard build; EN+JA | PASS | I ran all four myself: 1955/1952/0/3, 118 pass, `tsc --noEmit` silent, `i18n.ts` diff shows paired en/ja entries. |

## Steps 4–7 of the visual review checklist

- **Step 4 — scene-mutation audit.** `git diff --stat HEAD -- Assets/Scenes/` is
  empty. `git show --stat 256f21587` touches no `.unity` file. `git diff --stat
  256f21587..HEAD -- Assets/Scripts/Physics/ Assets/Scenes/LabScaffold.unity
  Assets/Materials/` empty. Clean. **PASS.**
- **Step 5 — PARTIAL → FAIL default.** No PARTIAL / "subtle but present" /
  "slightly off but acceptable" language in either the report or the
  self-review. Every acceptance row is a hard PASS with a specific evidence
  line. **N/A.**
- **Step 6 — production-flow verification.** No player-facing UI change; the
  production flow here is the deployed admin (Cloudflare 256f21587, verified
  not stale) + Fly API (v59, verified live) + real player token + prod DB. The
  live-endpoint checks I ran (content payload, /health, /points/spend 403) all
  hit the real production surface. **PASS.**
- **Step 7 — narrative read LAST.** I did. Report agrees with what the code
  and the live system independently confirm.

## Standing bans

- **Assets/Scripts/Physics/** — `git show --stat 256f21587` touches zero files
  under this tree. ✅
- **`Scenarios.cs`** — `git diff --stat 256f21587^..256f21587 -- Assets/Scripts/
  Physics/Viewer/Bot/Scenarios.cs` empty. No new `*Gate` scenarios. ✅
- **`LabScaffold.unity`** — no scene touches at all. ✅
- **`M_Splash*.mat`** — untouched. ✅
- **Uncommitted drift.** `git status --porcelain --untracked-files=all` shows
  NuGet DLLs, ProjectSettings.asset, other-spec STATUS.md docs, mission redesign
  docs, club-art PNGs, `TellCode.md`, `packages-lock.json` — all UNRELATED
  in-flight work; none belong to this task and none contradict the report.
  Rule 13 clean for this task. ✅

## Pre-existing texts drift (disclosed §5)

Verified against `git log --oneline -- Assets/Localization/LocalizationText.csv`:
`a10f46318` is real (gacha_history commit). `GACHA_PRIZES_TITLE` and
`SHOP_HISTORY_COMING_SOON` do exist in the CSV. Scoped `--check --catalogs
modes` is clean; the drift belongs to `texts`, not this task. Out of scope
disclosure is TRUE. ✅

## What I did NOT run, and why that's OK

The SPEC §6 item 1 live E2E (publish practice 10→15 on prod, stale-client
fee_changed, second tap debits 15) was executed by the implementer against
production, then live state restored. Re-running it would require admin
credentials, a real player token, and a full-state-restore afterwards, and would
mutate production for a claim already backed by (a) the router code path I read
directly, (b) 17 fee-branch tests in the backend suite (118 pass total), (c) the
Fly image v59 that carries the fix, and (d) the live `latest_version=4`
confirming the publish machinery works end-to-end. Declining the destructive
re-run is a legitimate reviewer choice per the kickoff brief; the claim's
supports are individually independently verifiable.

## Verdict

**PASS.** All 10 SPEC §6 acceptance items hold under primary-source verification.
The five attack targets (a legacy-bare-reason, b mirror-fails-publish, c
drift-scoped-to-one-pair, d MAX_MODE_ID_LEN bound-fix, e single-source withhold
list) each verify independently. Live deploy state matches the brief exactly
(Fly v59 `01M13XNG9NDT1QM4Z2QJH2K6GB`, dashboard `256f21587` with zero commits
touching `Tools/admin-dashboard` since). Scope discipline clean; no scene drift;
no standing-rule violations; disclosed pre-existing `texts` drift is genuine.

STATUS → `READY_FOR_REDTEAM`.

## Files touched by this review

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/ARCHITECT_REVIEW.md` | This review (replaces template) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | About to be set to `READY_FOR_REDTEAM` |
