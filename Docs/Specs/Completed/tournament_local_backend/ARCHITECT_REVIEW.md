# Architect Review — tournament_local_backend (T4)

**Gate:** golfin-reviewer
**Iteration reviewed:** 3
**Timestamp:** 2026-06-26 12:30 CEST
**Verdict:** **PASS → READY_FOR_REDTEAM**

This is a HEADLESS C# logic task — no screenshot / no Figma / no mesh / no video. Visual-fidelity
rules 14/16/17/18 do NOT apply (no canonical screenshot, no Figma node, no mesh/terrain, no video
deliverable). The gate is the EditMode test suite and the correctness of the §6 countback ladder
that the red-team failed iter-2 on. Per Rule 5 I re-walked the entire SPEC §6 acceptance list, not
just the countback fix.

---

## What I re-ran myself (Rule 5, Rule 6)

Driven via `unity-mcp-cli run-tool tests-run` against the live Editor (localhost:21573):

```
tests-run(EditMode, testClass=LocalTournamentBackendTests)
  Status: Passed   Total: 661   Passed: 68    Failed: 0   Skipped: 0    (1.78s)

tests-run(EditMode, testNamespace=Golfin.Tournaments.Tests)
  Status: Passed   Total: 661   Passed: 154   Failed: 0   Skipped: 0    (2.09s)

tests-run(EditMode, full)
  Status: Passed   Total: 661   Passed: 658   Failed: 0   Skipped: 3    (41.18s)
```

The 3 skips are the pre-existing `Golfin.Physics.Tests.HoleCompleteDriverTests` Stage C1 no-ops
(Names + Messages returned by the runner = `HandleShotComplete is now a no-op` /
`HoleCompletionBridge is the sole caller`). They are NOT from this task. The class / namespace /
full-suite counts (68 / 154 / 658) reproduce the implementer's report and the self-reviewer's
counts exactly — no fabrication, no padding (Rule 6 clean).

---

## Red-team blocker — RESOLVED, re-derived from the code

The iter-2 defect was that `Countback` called `CountbackWindows(H, H)` so the FIRST emitted back
window for an 18-hole set was `H=18` (always 0 once totals tie — guaranteed no-op), and back-9
(window=9) was never produced. The canonical GDD §6.1 LOCKED back-9 was bypassed and front-9 ended
up deciding instead — wrong winner on real data.

**The iter-3 fix (`LocalTournamentBackend.cs` L521-561), re-derived by hand:**

```csharp
int N    = holePars.Count;
int half = (N + 1) / 2;       // ceil(N/2): N=18→9, N=9→5

foreach (int window in CountbackWindows(half))   // Back pass
{
    int startIdx = N - window;
    ...
}
if (N > half)
{
    foreach (int window in CountbackWindows(half))   // Front pass
    {
        int startIdx = half - window;
        ...
    }
}

// CountbackWindows(half):
yield return half;
if (6 < half) yield return 6;
if (3 < half) yield return 3;
if (1 < half) yield return 1;
```

### N=18 trace (the shipped production path — all 6 tournaments_csv rows have holeSet=1-18)

- `half = 9`. `CountbackWindows(9) = [9, 6, 3, 1]`.
- Back pass startIdx = 18 − window = **[9, 12, 15, 17]** → back-9 (h10-h18) → back-6 (h13-h18)
  → back-3 (h16-h18) → back-1 (h18). Matches GDD §6.1 LOCKED ladder.
- Front pass startIdx = 9 − window = **[0, 3, 6, 8]** → front-9 (h1-h9) → front-6 (h4-h9)
  → front-3 (h7-h9) → front-1 (h9). These are the CLOSING windows of the front nine.

**Front-window semantics check (per the brief):** GDD §6.1 L89 = "fewest strokes over the CLOSING
holes … rewards finishing STRONG." Every window is a closing window of its segment, so front-6 =
LAST 6 of the front nine = holes 4-9 (startIdx = half − window = 3). The code emits exactly this.
**CORRECT per the locked rule** — do NOT flag as a deviation. The red-team's earlier prose
"holes 1-6" was the wrong reading and the brief explicitly tells me to confirm the closing-window
interpretation is right.

### N=9 generalization trace (Cesar's clarification — hole count is per-tournament)

- `half = 5`. `CountbackWindows(5) = [5, 3, 1]` (`6 < 5` is false → skipped).
- Back startIdx = [4, 6, 8] → back-5 (h5-h9) → back-3 (h7-h9) → back-1 (h9).
- Front startIdx = [0, 2, 4] → front-5 (h1-h5) → front-3 (h3-h5) → front-1 (h5).

**No hardcoded 18 or 9.** Grep on `LocalTournamentBackend.cs` for the literal `18` returns only
comment lines (doc-comment L515, comment-only L524). The ladder is driven by `holePars.Count`
exclusively. Generalizes to any N ≥ 1 (the `if (N > half)` guard suppresses the front pass when
N=1, the only degenerate case). PASS.

---

## Pinned fixtures — real ORDER assertions, not tautologies

Re-derived each by hand from the test bodies (`Tests/LocalTournamentBackendTests.cs` L1161-1344):

| Test | Player total | Bot total | Decisive window | Math (player vs bot) | Assertion |
|---|---|---|---|---|---|
| §28 `_18Hole_Back9Resolves_PlayerWins` | 5+4×8+3+4×8 = 72 | 4×18 = 72 | back-9 (h10-h18, startIdx=9) | 3+4×8=**35** < 4×9=**36** | `Assert.Less(playerIdx, botIdx)` |
| §29 `_18Hole_Front6Resolves_AfterAllBackAndFront9Tie` | 18+6+4×12 = 72 | 4×18 = 72 | back-9/6/3/1 + front-9 ALL TIE; front-6 (h4-h9, startIdx=3) | 2+2+2+4+4+4=**18** < 4×6=**24** | `Assert.Less(playerIdx, botIdx)` |
| §30 `_ThreeWayTie_ThenFourthFinisher_FourthIsRank4` | 5×3 = 15 | 4×3 = 12 (×3 bots) | rank-skip (not countback) | `nextRank = rank + (j−i) = 1 + 3 = 4` | `board[3].Rank == 4 && IsPlayer && !IsTie` |
| §31 `_9Hole_Back3Resolves_PlayerWins` | 4×7+5+3 = 36 | 4×9 = 36 | back-5 TIES (20=20); back-3 (h7-h9, startIdx=6) | 3+4+4=**11** < 4×3=**12** | `Assert.Less(playerIdx, botIdx)` |

All four are real ORDER assertions (`Assert.Less` on positional index after the live sort, or a
positional `Rank` check), not `rank==rank` tautologies. `Assert.Less(playerIdx, botIdx)` would
flip and fail if the sort produced the wrong order — that's the proof, not the test merely passing.

§29 also confirms the front-pass actually runs and selects the FIRST surviving window — proving
the ladder isn't short-circuiting on the back pass for a tie that should reach front. §31 proves
the N=9 path runs with `CountbackWindows(5)` filtering 6 out cleanly. §30 confirms `AssignRanks`'s
`nextRank = rank + (j − i)` works at a 3-way boundary (rank 1 → rank 4, never 2 or 3).

---

## Acceptance re-walk (Rule 5 — entire SPEC §6 list)

| SPEC §6 acceptance bullet | Evidence | Status |
|---|---|---|
| State derivation: 6 states at boundary `now`; resolve gate flips at `EndUtc + resolveDelay` | Tests §1-§10 (PASS in class run) | RE-VERIFIED |
| Register: debits once, idempotent re-register no double-charge, insufficient RP rejected, free entry skips debit, character locked | Tests §11-§13 | RE-VERIFIED |
| SubmitHoleResult: append + Finished on last hole, dup/late-submit reject, persisted via store reload | Tests §14-§15 | RE-VERIFIED |
| Leaderboard final: strokes asc + countback (back-9 then front-9 paths) + time + timestamp | §28 (18-hole back-9), §29 (18-hole front-6), §31 (9-hole back-3), §25 (3-hole back-1); `Countback` L521-561 inspected by hand | **RE-VERIFIED** (NEW: real 18-hole paths now exercised) |
| Ties shared rank + "T" flag + next rank skips N-way | §24 (3-way at rank 1), §30 (rank-skip → 4), `AssignRanks` L645 `nextRank = rank + (j-i)` | RE-VERIFIED |
| DNF: below finishers, hidden from ranked rows, player DNF visible in sticky row, ordering | Tests §18-§19; `AssignRanks` L599-678 | RE-VERIFIED |
| Provisional: rank by score-to-par-so-far (D3); `IsProvisional` true pre / false post-resolve | §27 (thru-3 −1 above thru-9 E) | RE-VERIFIED |
| Prizes / split-pool: band match, 2-way + N-way tie pool, RP rounded-up, indivisible item duplicated | §26 (2-way 950 split), `PrizeSplitFormulaTests` L1352+ | RE-VERIFIED |
| GetResults / ClaimPrize: null pre-resolve; correct after; claim-once incl. survives store reload (D-claim b) | Tests §20-§23, `ClaimPrize_SurvivesStoreReload_NoDoubleGrant` L953 (PASS); code L361-383 inspected | RE-VERIFIED |
| Determinism via clock: same `(seed, fixedNow)` → identical board, per-row equality on non-empty board | `Determinism_SameClockAndSeed_ProduceSameLeaderboard` L741 | RE-VERIFIED |
| §6 generalization to arbitrary N (Cesar correction) | §31 (N=9), code uses `(N+1)/2` — no hardcoded 18 or 9 (verified via grep) | RE-VERIFIED |

Every PASS row backed by visible tool output (test counts I produced) or a code citation I read.
No "carried forward from prior iter" rows (Rule 5 obeyed).

---

## Persisted claim-once (Rule 3 / D-claim b) — re-derived

`ClaimPrize` (L361-383): `if (_store.IsClaimed(id)) return;` THEN grant RP + item THEN
`_store.MarkClaimed(id)`. The guard reads the STORE, not an in-memory dict, so a fresh
`LocalTournamentBackend` over the same store still short-circuits. Grant-then-mark ordering means
a crash mid-claim leaves the entry un-claimed and re-claimable (acceptable — not a double-grant
risk). `ClaimPrize_SurvivesStoreReload_NoDoubleGrant` (L953) constructs `backend2` over the SAME
store with fresh `rp2(balance=0)` and asserts `rp2.Balance == 0L` after a second `ClaimPrize`
call → proves persisted claim-once. PASS.

---

## Standing bans + drift (Rule 7, Rule 13)

```
git diff HEAD -- Assets/Scripts/Physics/                            → (empty)
git diff HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs     → (empty)
git status --porcelain --untracked-files=all (outside task folder):
   M Assets/Scenes/ShellScene.unity        ← pre-existing (iter-1/2/3 baselines, attributed L130)
   M Packages/manifest.json                ← MCP auto-bump (iter-2 baseline, attributed L131)
   M Packages/packages-lock.json           ← companion (iter-2 baseline, attributed L132)
   ?? Assets/Scripts/Tournaments/ITournamentEntryStore.cs (+ .meta)   ← attributed L133
   ?? Assets/Scripts/Tournaments/ITournamentSeams.cs (+ .meta)        ← attributed L134
   ?? Assets/Scripts/Tournaments/LocalTournamentBackend.cs (+ .meta)  ← main deliverable L135
   ?? Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs (+ .meta) ← L136
```

Every outside-task path is attributed in the report's drift table with a baseline citation in
HEARTBEAT.log (iter-1 L1, iter-2 L8-11, iter-3 L30-33). No undeclared drift. Physics + Scenarios
clean.

### Boundary (T5 / T6 / T9)

- Zero `using UnityEngine` in `LocalTournamentBackend.cs`, `ITournamentEntryStore.cs`,
  `ITournamentSeams.cs` (verified via grep).
- No `SaveData` / `SaveSchemaMigrator` / `PersistedTournamentEntry` references (T5 owns save schema).
- No round-loop / stamina symbols (T6 owns the round loop).
- No `LeaderboardManager` / roster-screen refs (T9 owns the screen binding).

Headless discipline maintained.

---

## API binding (T2/T3) — spot-checked, clean

- `def.HoleSet / ClubId / StartUtc / EndUtc / ResolveDelayMinutes / EntryFeeRP / PrizeTableId /
  BotFieldId` — all read at expected sites in `LocalTournamentBackend.cs`. Matches T2-shipped
  `TournamentDefinition` ctor.
- `BotFieldGenerator.RollField(def, cfg, holePars)` + `Project(card, now) → BotProjection{Thru,
  RevealedStrokes, Complete}` — match T3-shipped (commit `a5a099ab6`).
- `BotCard.{BotId, PerHoleStrokes, TotalStrokes, StartOffsetSeconds, PerHoleCompletionUtc}` — match.
- `FakePlayerRow / BotScoreBracketRow` — match.

No invented overloads.

---

## Iteration / circuit-breaker

Iter-3 of shape `ranking:countback-back9-ladder` — this is the **first** iter of THIS shape (iter-1
was `claim-persistence` and iter-2 was `claim-persistence-fix` per the prior reviews; the countback
defect surfaced only at iter-2 red-team gate). No 3-strike risk. The red-team blocker is fixed at
its root cause (the `CountbackWindows(H, H)` arg confusion), not patched at the symptom, and the
SPEC-mandated 18-hole back-9 / front-9 fixtures the red-team called out as missing are now in place
(§28 + §29), plus a generalization-proof fixture (§31) and a rank-skip-boundary fixture (§30).

---

## Verdict

**PASS → READY_FOR_REDTEAM.**

The red-team's iter-2 blocker (countback ladder bypassing back-9 on the only shipped hole-set size)
is resolved at its root in `Countback` / `CountbackWindows`, generalized cleanly to arbitrary N
via `half = (N+1)/2` (no hardcoded 18 or 9), and proven by three new pinned fixtures that assert
real board ORDER rather than tautological identities. The N=18 ladder now emits the GDD §6.1
LOCKED [back-9 → back-6 → back-3 → back-1 → front-9 → front-6 → front-3 → front-1] sequence with
correct CLOSING-window startIdx values, matching the GDD §6.1 L89 "rewards finishing STRONG"
reading. The N=9 path filters cleanly (`6 < 5` skipped) and §31 proves the back-3 window resolves
a real tie.

Test gate I produced myself: 68 / 154 / 658 PASS, 0 FAIL, 3 pre-existing-skip (Stage C1
HoleCompleteDriver no-ops). No regressions. Persisted claim-once (D-claim b) carries forward
unchanged and is re-verified. Outside-task drift fully attributed in the implementer report.
Physics + Scenarios standing bans clean. T5 / T6 / T9 boundary clean. Zero `using UnityEngine` in
logic files.

Setting STATUS → `READY_FOR_REDTEAM`. The adversarial red-team gate is the only gate that may
write `ARCHITECT_REVIEW_PASS`.

| File | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_local_backend/ARCHITECT_REVIEW.md` | This review (iter-3, overwrites iter-2) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_local_backend/STATUS.md` | Set to READY_FOR_REDTEAM |
