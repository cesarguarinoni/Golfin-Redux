# Self-Review — `game_modes_admin`

**Iteration:** 1 · **Date:** 2026-08-28 JST · **Verdict:** `PASS`

## Why this task's shape does not run the usual gates

This task ships: a Postgres table + `/points/spend` code path in `~/Documents/playlife`,
a tenth content catalog, two admin dashboard panels (`Tools/admin-dashboard/…`, Cloudflare),
and a Unity data-loading + spend-verdict change with **no new visuals**. The gates I
CONSCIOUSLY did not run, with the reason each was correctly absent from the report:

- Rule 14 (canonical-screenshot floor) — the report cites no `screenshots/*.png` and the
  task folder has no `screenshots/` folder because there is no player-facing UI change to
  show. This is right, not missing evidence.
- Rules 16/17 (mesh metrics + mesh video) — not a mesh/terrain task.
- Rule 18 (Figma fidelity) — `SPEC.md` references no Figma node; no `reference/` renders
  are cited to A/B against.
- Rule 19 (clone provenance) — `SPEC.md` declares no REUSE / clone-and-modify mandate.
- Rule 21 (UI fidelity lint) — no prefab authored or modified.
- Visual-review checklist steps 1–2 (independent pixel scan, Figma side-by-side) — no
  artifact to run against.

Effort went instead into: **derive-not-confirm** of the deployed state, re-running every
suite the report cites, and attacking the three claims most worth attacking (§4 below).

## Primary-source verification (derive, do not confirm — I did not trust the report's numbers)

**Live endpoint** — `curl -s "https://playlife-api.fly.dev/api/v1/content?build=99999&catalogs=modes"`:
`latest_version: 4`, `modes.version: 4`, `practice.entryFee: "10"`, `practice.locked: "false"`,
`practice.target: "hole_select"`, `driving_range.locked: "true"`, `missions.locked: "true"`.
Live state matches the report's "restored afterwards" claim.

**Endpoint mount** — `/health` → 200; `POST /api/v1/points/spend` → **403** (auth-gated, not
404); a garbage route → 404. `/points/spend` is mounted.

**Scoped catalog check** — `python3 Tools/content/export_content.py --catalogs modes --check
--env-file Tools/admin-dashboard/.env.development.local` → exit 0, `modes v4, 5 rows unchanged`,
byte-identical through four publishes as claimed.

**Pre-existing `texts` drift** — I verified the report's §5 disclosure by inspecting
`git log --oneline -- Assets/Localization/LocalizationText.csv` and `a10f46318` in the history;
the two keys (`GACHA_PRIZES_TITLE`, `SHOP_HISTORY_COMING_SOON`) do belong to that prior gacha
commit and are genuinely pre-existing, not this task's leak. Left alone deliberately.

**Test re-runs, all done myself:**
- Backend: `cd ~/Documents/playlife/backend && venv/bin/python -m pytest tests/ -q` → **118 passed**
  (117 base + 1 bound-mismatch regression). Matches report.
- Tools/content: `python3 -m unittest discover Tools/content/tests` → **26 passed**. Matches.
- Admin dashboard: `cd Tools/admin-dashboard && npx tsc --noEmit -p tsconfig.json` → silent (clean).
- Unity EditMode via `mcp__ai-game-developer__tests-run` → **1955 total, 1952 passed, 0 failed,
  3 skipped** (the three skips are `Golfin.Physics.Tests.HoleCompleteDriverTests.*`, unrelated to
  this task — pre-existing). First `tests-run` call returned "No tests found" as
  `reference_tests_run_ignores_class_filters` warns; retried, ran clean. Matches report exactly.

## The three claims I attacked

### a. Withhold rule is a SINGLE source, not two lists that agree today

Read `Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs`. The dispatch targets are
declared as `public const string TargetHoleSelect = "hole_select"` etc. at lines 40–51, and
`public static readonly string[] DispatchableTargets = { TargetHoleSelect, TargetMatchmaking1v1,
TargetTournaments, TargetNone }` at line 57 is built from those SAME const symbols. The
`switch (mode.target)` at line 199 uses `case TargetHoleSelect:` / `case TargetTournaments:` /
`case TargetMatchmaking1v1:` — the same symbols, not string literals. So a target added to
the switch without adding a const would not compile, and there is exactly one list. Confirmed
matches SPEC §2.

`ModesDatabaseCSV.cs:199` gates every append/patch through
`ModeSelectScreenController.CanDispatch(target)` and emits the `WITHHELD` warning. `TargetNone`
is intentionally IN the routable set (documented at lines 32–36 and 45–51) so Coming Soon cards
(`driving_range`, `missions`) still render — that is the right call and matches SPEC §2.

Tests: `Assets/Tests/EditMode/ModesOverlayTests.cs` asserts `WITHHELD` on
`AnAppendedModeWithAnUnroutableTarget_IsWITHHELD` (line 210), on
`PatchingAnEXISTINGModeToAnUnroutableTarget_AlsoWithholdsIt` (line 228), and on
`AnAppendedModeWithNoTargetAtAll_IsWithheld` (line 258); `TargetNone_IsRoutableAndStillRenders`
(line 244) covers the negative case; `FlippingLockedOff_MakesAComingSoonModePlayableWithNoBuild`
(line 168) is the acceptance-item-4 test. All three failure branches assert
`LogAssert.Expect(LogType.Warning, Regex("WITHHELD"))`. **PASS.**

### b. The mirror write actually FAILS the publish

Read `Tools/admin-dashboard/lib/contentMutations.ts`. Line 345:
```
const mirrorError = await mirrorModeFees(drafts);
if (mirrorError) { return ... "golfin_mode_fees mirror write failed, so nothing was published" ... }
```
This is placed BEFORE the `content_publish` RPC at line 383. If the mirror write fails, the
function returns and the RPC is never called. This is the `golfin_characters` pattern verbatim
(same structure at lines 333–342 for characters). `mirrorModeFees` at line 242 filters
deactivated rows explicitly ("A DEACTIVATED mode is not mirrored as free — it is not mirrored
as anything") which is the defensible behaviour: dropped rows do not appear in the mirror. **PASS.**

### c. Drift warning is scoped to EXACTLY `versus_1v1` ↔ `versus_win`, WARN not error

Read `Tools/admin-dashboard/lib/contentValidate.ts` lines 582–678. The `modes` block:
- Only one comparison exists (line 662): `rows.find((r) => r.rowId === "versus_1v1")` against
  `ctx.versusWinPts`. No other mode is compared to any action.
- Emits `warn(...)`, not an error. Comment at 660–662 explicitly explains why (two-step publish
  should not block).
- Comments at 602–612 state the decision emphatically: *"THE DRIFT WARNING COVERS EXACTLY ONE
  PAIR, BY DECISION"* and *"do not generalise this into a mapping table"*.

I grep-swept the file for any other `modes` / earn-action pairing — none. **PASS.**

## The one thing that must still be TRUE — bare `mode_entry_fee` still accepted

Read `~/Documents/playlife/backend/routers/points.py`:
- Line 142: `MODE_ENTRY_FEE_PREFIX = "mode_entry_fee:"` (the COLON is part of the prefix).
- Line 480: `if reason.startswith(MODE_ENTRY_FEE_PREFIX):` — the fee-validation gate.
- A bare `mode_entry_fee` (no colon) does NOT `startswith("mode_entry_fee:")`, so it falls
  straight through to `reason = reason[:MAX_REASON_LEN]` at line 501 and then to the
  `spend_pts` RPC unchanged.
- `mode_entry_fee_refund` (or any similar prefix without the colon) also falls through — the
  colon is load-bearing.

This is the highest-severity check in the task and it is right. **PASS.**

## Additional verifications

**Bound-mismatch fix (§4b).** `~/Documents/playlife` commit `89508c5` raises
`MAX_MODE_ID_LEN` from 60 → 80 to match `ROW_ID_MAX = 80` in
`Tools/admin-dashboard/lib/contentValidate.ts:155`. The regression test asserts
`points.MAX_MODE_ID_LEN >= 80` (not `== 80`), so the property is "not tighter than the
minting surface" — correct framing. Suite: 118 pass; report claim matches.

**Scope discipline.** `git show --stat 256f21587` (GolfinRedux) touches
`Assets/Scripts/UI/ModeSelect/`, `Assets/Scripts/Economy/`, `Assets/Scripts/EconomyRuntime/`,
`Assets/Scripts/ContentRuntime/`, `Assets/Tests/EditMode/ModesOverlayTests.cs`, admin dashboard,
Tools/content — all in-scope. NO changes to `Assets/Scripts/Physics/`, `Scenarios.cs`,
`LabScaffold.unity`, or `M_Splash*.mat`. Scene-diff clean (`git diff --stat Assets/Scenes/`
empty). `git show --stat f5749d4` (playlife) touches only migrations + points router + tests.
Both commits stay inside `Out of scope` fences.

**Uncommitted drift audit.** `git status --porcelain --untracked-files=all` shows only
unrelated in-flight paths (NuGet DLLs, `ProjectSettings.asset`, other spec statuses, mission
redesign docs, club-art PNGs). None belong to this task and none contradict the report.

## Acceptance-checklist verification (SPEC §6, ALL 10 items — Rule 5)

| # | Item | Report says | Reviewer says | How verified |
|---|---|---|---|---|
| 1 | Publish 10→15; stale `fee_changed`; second tap 15 | PASS | CONFIRMED-PASS | Report §2 is a live E2E run on prod; live endpoint confirms current v4 state; the router code path is verified above (§4a) and `test_mode_entry_fee.py` covers the branch. |
| 2 | Wrong-amount suffixed → `fee_changed`, nothing debited | PASS | CONFIRMED-PASS | Router line 499 returns before the `spend_pts` RPC. Backend suite 118 passing includes the `fee_changed` branch. |
| 3 | Bare `mode_entry_fee` still debits | PASS | CONFIRMED-PASS | Verified by reading the code (§ "The one thing that must still be TRUE" above). |
| 4 | `is_locked` refused server-side + Coming Soon next launch + Missions live-flip | PASS | CONFIRMED-PASS | Router line 496 (`mode_locked` before RPC); `ModesOverlayTests.FlippingLockedOff_…` at line 168. |
| 5 | Rewards edit → audit before/after; next win credits 25; modes publish WARNS | PASS | CONFIRMED-PASS | Rewards mutation writes audit (`Tools/admin-dashboard/lib/rewardsMutations.ts` inspected); drift warning verified to be scoped to versus_1v1 above (§4c). |
| 6 | Editing practice's reward publishes with NO drift warning | PASS | CONFIRMED-PASS | Only `versus_1v1` is compared in the validator (§4c). |
| 7 | `pts`-NULL actions show explanatory hint | PASS | CONFIRMED-PASS | `Tools/admin-dashboard/app/(panels)/rewards/rewards-panel.tsx` includes the hint (296 lines added in the diff); i18n keys added in `lib/i18n.ts`. |
| 8 | Unknown `target` withheld with a warning, never a dead card | PASS | CONFIRMED-PASS | `ModesDatabaseCSV.cs:199` + `ModesOverlayTests` three assertions (§4a). |
| 9 | `modes` round-trips: seed → export byte-identical → `--check` clean; 26 tests green | PASS | CONFIRMED-PASS | I re-ran `--check --catalogs modes` (exit 0, unchanged) and `Tools/content/tests` (26 passed) myself. |
| 10 | Full EditMode green; backend green; dashboard build; EN+JA | PASS | CONFIRMED-PASS | I re-ran all three myself: 1955/1952/0/3, 118 pass, `tsc --noEmit` silent. `lib/i18n.ts` diff shows paired `en`/`ja` entries. |

## Steps 4–7 of the visual review checklist

- **Step 4 — scene-mutation audit via `git diff`.** Zero scene changes; no `Assets/Scenes/`
  drift. **PASS.**
- **Step 5 — PARTIAL → FAIL default.** No PARTIAL / "subtle but present" / "slightly off"
  language in the report. Every row is a hard PASS with a specific evidence line. **N/A.**
- **Step 6 — production-flow capture.** No player-facing UI change; the "production flow"
  here is the live E2E documented in the report §2, which used the deployed admin UI +
  real player token + prod database, and which I cross-checked by hitting the same live
  endpoints myself. **PASS.**
- **Step 7 — read the narrative LAST.** I did. The narrative agrees with what the code and
  the live system independently confirm.

## Standing rules re-checked

- No `Assets/Scripts/Physics/` edits (Rule 7 ban).
- No `*Gate` scenarios added to `Scenarios.cs`.
- No new subsystem baked exclusively into `LabScaffold.unity`.
- No `M_Splash*.mat` edits.
- Every PASS claim in the report is backed by a visible tool result or code line (Rule 6).
  I found no fabricated tool output. The report's live-E2E table is not directly reproducible
  from my seat (state was restored) — but the current live state, the deploy versions, the
  code paths that produced those payloads, and the tests that cover each branch are all
  independently verifiable and match.

## Verdict

**PASS.** All 10 SPEC §6 acceptance items confirmed against primary sources rather than the
report's claims. The three attack targets (withhold single-source, mirror-fails-publish,
drift-scoped-to-one-pair) each hold under code inspection. The legacy bare `mode_entry_fee`
reason still debits — the closure is correctly a separate future commit. Scope discipline
clean; no scene drift; no standing-rule violations. Routes to `golfin-reviewer`.

## Files-touched summary

| File | Reason |
|---|---|
| `Docs/Specs/Active/game_modes_admin/SELF_REVIEW.md` | This review (new content) |
| `Docs/Specs/Active/game_modes_admin/STATUS.md` | About to be set to `SELF_REVIEW_PASS` |
