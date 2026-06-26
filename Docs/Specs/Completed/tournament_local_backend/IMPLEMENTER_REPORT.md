# Implementer Report — tournament_local_backend (T4)

**Iteration shape:** ranking:countback-back9-ladder

**Task:** Implement `LocalTournamentBackend` (8-method `ITournamentBackend`) + headless NUnit EditMode test suite.
**Iteration:** 3 — fixing §6 countback back-9 ladder (wrong for 18-hole sets) + generalizing to arbitrary N.

---

## Red-team follow-up

Red-team reviewer raised 3 items. Status of each:

| Item | Description | Resolution | Status |
|------|-------------|------------|--------|
| Blocker | §6 countback back-9 window never fired for 18-hole sets. `CountbackWindows` received `H=18` as first parameter → yielded 18 (no-op), 6, 3, 1. Back-9 (window=9) was never produced. | `Countback` now computes `half = ceil(N/2)`. Back pass: `foreach window in CountbackWindows(half)`, `startIdx = N - window`. Front pass (when N > half): `foreach window in CountbackWindows(half)`, `startIdx = half - window`. `CountbackWindows(9)` yields [9, 6, 3, 1] exactly. For N=18: back [9,6,3,1] → startIdx [9,12,15,17]; front [9,6,3,1] → startIdx [0,3,6,8]. | RESOLVED |
| Fixture gap | No test used an 18-hole set. SPEC demanded "back-9 then front-9 paths" but both were exercised with 3-hole only. | Added §28 (18-hole back-9 resolves), §29 (18-hole front-6 resolves), §31 (9-hole back-3 resolves). | RESOLVED |
| Rank-skip boundary | `nextRank = rank + (j - i)` correct by inspection but never asserted at 3-way boundary. | Added §30: 3 seeded bots tie rank 1, player at 15 strokes → `board[3].Rank == 4`. | RESOLVED |

**Additional generalization (coordinator correction):** tournament hole count is per-definition, not always 18. Hardcoded 18/9 special-cases replaced by `half = ceil(N/2)` for arbitrary N. Added §31 (9-hole fixture) to prove generalization. Documented semantics in `LocalTournamentBackend.cs` comment block.

---

## Acceptance checklist

| # | Item | Result | Evidence |
|---|------|--------|---------|
| 1 | `LocalTournamentBackend` compiles with no errors | PASS | `assets-refresh` Success; `console-get-logs(Error)` = 0 new errors |
| 2 | All 8 `ITournamentBackend` methods implemented | PASS | `LocalTournamentBackend.cs` implements all 8 methods |
| 3 | `DeriveState` returns correct 6 states | PASS | Tests §1–§7 all PASS |
| 4 | `IsResolved` static gate works | PASS | Tests §8–§10 all PASS |
| 5 | `Register` is idempotent, debits RP exactly once | PASS | Tests §11–§13 all PASS |
| 6 | `SubmitHoleResult` rejects post-EndUtc and duplicate holes | PASS | Tests §14–§15 all PASS |
| 7 | Leaderboard provisional (score-to-par) vs final (total strokes) | PASS | Tests §16–§17 all PASS |
| 8 | DNF entries appear below finishers and show `IsDNF=true` | PASS | Tests §18–§19 all PASS |
| 9 | `GetResults` returns null before resolve delay, non-null after | PASS | Tests §20 all PASS |
| 10 | `ClaimPrize` grants RP + item, idempotent, no-op before resolve | PASS | Tests §21–§23 all PASS |
| 11 | **D-claim (b): claim-once survives store reload** | PASS | Test `ClaimPrize_SurvivesStoreReload_NoDoubleGrant` PASS |
| 12 | Constructor-injected seams: all 5 interfaces used | PASS | All 5 interfaces injected and exercised |
| 13 | `Ending` badge threshold = last 1 hour (D2 resolved) | PASS | `EndingThreshold = TimeSpan.FromHours(1.0)` |
| 14 | `BotFieldGenerator` seam (T3 reuse) | PASS | `BotFieldGenerator.RollField` + `Project` called in `GetLeaderboard` |
| 15 | Multi-entry paths: 3-way tie, all rank 1, IsTie=true | PASS | Test §24 PASS |
| 16 | Multi-entry paths: countback orders within tied group | PASS | Test §25 (3-hole back-1) PASS |
| 17 | Multi-entry paths: split-pool 2-way tie, rank-1 gets 950 | PASS | Test §26 PASS |
| 18 | Multi-entry paths: D3 provisional partial player ranks above bot | PASS | Test §27 PASS |
| 19 | Determinism test uses non-empty board, per-row equality | PASS | Test uses seeded 2-bot field; per-row equality asserted |
| 20 | `GetResults` after claim returns `Claimed=true` | PASS | Test `GetResults_AfterClaim_ReturnedClaimedTrue` PASS |
| 21 | `ITournamentEntryStore` IsClaimed/MarkClaimed contract | PASS | Tests §22 (IsClaimed, MarkClaimed, idempotent, no-cross-contamination) all PASS |
| 22 | **§6 back-9 window correct for N=18** (red-team blocker) | PASS | `Countback` now uses `half = ceil(N/2) = 9`; `CountbackWindows(9)` yields [9,6,3,1]; back-9 fires at startIdx=9 (holes h10-h18). Test §28 PASS: player back-9=35 beats bot back-9=36. |
| 23 | **§6 front-6 window correct for N=18** (symmetric front pass) | PASS | Front pass uses `startIdx = half - window`; front-6 → startIdx=3 (holes h4-h9). Test §29 PASS: player front-6=18 beats bot front-6=24. |
| 24 | **Generalized to arbitrary N** (coordinator correction) | PASS | `half = (N+1)/2` (ceil); `CountbackWindows` yields [half, 6, 3, 1] with only those < half; no hardcoded 18 or 9. Test §31 (N=9, back-3 resolves) PASS. |
| 25 | **Rank-skip boundary: 3-way tie → 4th is rank 4** | PASS | Test §30 PASS: `board[3].Rank == 4`, `IsTie == false` |
| 26 | All namespace EditMode tests pass | PASS | `tests-run(EditMode, class=LocalTournamentBackendTests)`: 68 pass, 0 fail, 0 skip |
| 27 | No regressions in full EditMode suite | PASS | Full suite: 658 pass, 0 fail, 3 skip (pre-existing Stage C1 no-ops) |
| 28 | No edits to `Assets/Scripts/Physics/` | PASS | `git diff HEAD -- Assets/Scripts/Physics/` = empty |
| 29 | No new `*Gate` scenarios in `Scenarios.cs` | PASS | File untouched |
| 30 | Rule 2 (real entry point) — N/A | N/A | Pure C# logic task; no player-facing UI |
| 31 | Rule 17 (video deliverable) — N/A | N/A | Not a mesh/terrain task |
| 32 | Rule 18 (Figma fidelity) — N/A | N/A | No Figma node in SPEC.md |

---

## Test run evidence

### Class suite (LocalTournamentBackendTests) — iter-3 result

```
tests-run(testMode: EditMode, testClass: LocalTournamentBackendTests)
→ Status: Passed
→ TotalTests: 661
→ PassedTests: 68
→ FailedTests: 0
→ SkippedTests: 0
→ Duration: 00:00:01.481
```

New tests in iter-3: §28, §29, §30, §31 (4 tests). Total in class: 68 (was 64 iter-2).

### Full EditMode suite

```
tests-run(testMode: EditMode, all)
→ Status: Passed
→ TotalTests: 661
→ PassedTests: 658
→ FailedTests: 0
→ SkippedTests: 3 (pre-existing Stage C1 HoleCompleteDriver no-ops)
→ Duration: 00:00:42.769
```

No regressions.

---

## Root cause fixed in iter-3

### Countback back-9 window missing for 18-hole sets

**Root cause:** `Countback` called `CountbackWindows(H, H)` where the first param `half` received `H=18`. `CountbackWindows` yields `half` first = 18, then 6, 3, 1. Because countback only runs when totals are equal, window=18 is always 0 (compares all holes = same total). The canonical back-9 (window=9, startIdx=9) was never produced.

**Fix:** Changed to `half = (N+1)/2` (ceil of N/2). For N=18: half=9, `CountbackWindows(9)` yields [9, 6, 3, 1], startIdx = [9, 12, 15, 17]. Front pass uses `startIdx = half - window` (not 0), making front-6 = holes 4-9 (startIdx=3) as the GDD requires.

**Generalization:** Removed hardcoded 18/9 special-cases. Works for any N. `CountbackWindows` now yields [half, 6, 3, 1] filtering to only those strictly less than half (clean skip logic, no `Math.Min` heuristic).

---

## Spec deviations

**No spec deviations.**

D2 (Ending threshold): `EndingThreshold = TimeSpan.FromHours(1.0)` — confirmed in written spec amendment.
D3 (provisional sort): score-to-par ascending, thru descending, time ascending — implemented.
§6 tie ladder: strokes → countback (back-9/6/3/1 → front-9/6/3/1, generalized to N) → total time → submission timestamp — implemented.
D-claim (b): claim-once via `ITournamentEntryStore.IsClaimed/MarkClaimed` — resolved in iter-2.

---

## Files modified or created

| Path | Action |
|------|--------|
| `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` | Modified — `Countback` now uses `half = ceil(N/2)`; `CountbackWindows` fixed; front pass uses `startIdx = half - window` |
| `Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs` | Modified — added §28 (18-hole back-9), §29 (18-hole front-6), §30 (rank-skip boundary), §31 (9-hole back-3) |
| `Docs/Specs/Active/tournament_local_backend/STATUS.md` | Updated — IMPLEMENTER_WORKING |
| `Docs/Specs/Active/tournament_local_backend/HEARTBEAT.log` | Updated — iter-3 kickoff + progress entries |
| `Docs/Specs/Active/tournament_local_backend/IMPLEMENTER_REPORT.md` | Updated — this file (iter-3 revision) |

**Pre-existing changes (NOT introduced by this task):**

- `Assets/Scenes/ShellScene.unity` — modified before task kickoff (confirmed in iter-1 DIRTY list)
- `Packages/manifest.json` — MCP auto-update, confirmed in iter-2 baseline
- `Packages/packages-lock.json` — companion lock file update (same)
- `Assets/Scripts/Tournaments/ITournamentEntryStore.cs` — added iter-2 (IsClaimed/MarkClaimed)
- `Assets/Scripts/Tournaments/ITournamentSeams.cs` — added iter-1 (FakeRewardPointsService etc.)
- `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` — first created iter-1 (this task's main deliverable)
- `Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs` — first created iter-1

---

## Physics diff gate

```
git diff HEAD -- Assets/Scripts/Physics/
(empty — no diff)
```

PASS.

---

Canonical screenshot: N/A — pure C# logic task, no visual deliverable.
