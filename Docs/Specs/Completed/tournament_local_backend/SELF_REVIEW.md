# Self Review — tournament_local_backend (T4)

**Iteration:** 3
**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-06-26 12:12 CEST
**Verdict:** **FORWARD_TO_ARCHITECT** (SELF_REVIEW_PASS)

---

## How I reviewed (iter-3, post red-team rejection, Rule 5 full re-walk)

Headless C# logic task — no screenshot / no Figma / no mesh / no video. Gate = the EditMode test
suite (`Golfin.Tournaments.Tests`) + correctness of the §6 countback ladder for the only hole-set
size that ships (N=18) AND for the other shipped size (N=9). Visual-review steps 1, 6, 7, 8 do not
apply. Per Rule 5 I re-walked the entire SPEC §6 acceptance list, not just the countback fix the
red-team named.

I:
1. Re-ran the EditMode suite myself with three filters (class, namespace, full) — confirmed the
   implementer's counts against my own results.
2. Read `LocalTournamentBackend.Countback` / `CountbackWindows` and re-derived the back-9 / front-6
   math by hand on the §28 / §29 / §31 fixtures.
3. Cross-checked the GDD §6.1 source ("fewest strokes over the **closing** holes … rewards finishing
   strong" — `Docs/Game Design/Tournaments_GDD.md` L89), confirming the closing-window reading.
4. Read all 4 new tests (§28/§29/§30/§31) to verify they assert real board ORDER (not tautologies),
   then verified the earlier iter-2 fixes (D-claim persistence, multi-entry coverage, determinism)
   did not regress.
5. Audited git status against HEARTBEAT.log baselines (Rule 13) and confirmed the physics + Scenarios
   standing bans (Rule 7) are clean.

---

## Tests re-run by reviewer (the gate)

Driven via `unity-mcp-cli run-tool tests-run` against the live Editor (localhost:21573):

```
tests-run(EditMode, testClass=LocalTournamentBackendTests)
  Status: Passed   Total: 661   Passed: 68    Failed: 0   Skipped: 0    (1.48s)

tests-run(EditMode, testNamespace=Golfin.Tournaments.Tests)
  Status: Passed   Total: 661   Passed: 154   Failed: 0   Skipped: 0    (2.79s)

tests-run(EditMode, all)
  Status: Passed   Total: 661   Passed: 658   Failed: 0   Skipped: 3    (41.87s)
```

The 3 skips are pre-existing `Golfin.Physics.Tests.HoleCompleteDriverTests` (Stage C1 no-ops),
confirmed by the `Message` field on each skipped result — NOT from this task. The class count (68)
and full-suite count (658) match the implementer report exactly. No fabrication (Rule 6 clean).

Note: the implementer's report L77 says "Total in class: 68 (was 64 iter-2)" → +4 = §28/§29/§30/§31.
My class run = 68 PASS / 0 FAIL. Confirmed.

---

## Red-team blocker — re-derived from the code

### The fix (LocalTournamentBackend.cs L521-561)

```csharp
int N    = holePars.Count;
int half = (N + 1) / 2;   // ceil(N/2): N=18→9, N=9→5

// Back pass: startIdx = N - window
foreach (int window in CountbackWindows(half))
{
    int startIdx = N - window;
    ...
}
// Front pass: startIdx = half - window  (skipped when N <= half)
if (N > half) { foreach (...) { int startIdx = half - window; ... } }

// CountbackWindows(half):
yield return half;
if (6 < half) yield return 6;
if (3 < half) yield return 3;
if (1 < half) yield return 1;
```

### N=18 trace (the production path)

`half = (18+1)/2 = 9`. `CountbackWindows(9)` yields **[9, 6, 3, 1]** (6<9, 3<9, 1<9 all true; first
yield is `half=9` directly).
- Back pass: startIdx = 18 - window = **[9, 12, 15, 17]** → windows = **back-9 (h10-h18) → back-6
  (h13-h18) → back-3 (h16-h18) → back-1 (h18)**. The red-team's iter-2 defect was that the first
  yield was `half=H=18` (whole round, always a no-op once totals tie); now it correctly emits 9 FIRST.
- Front pass: startIdx = 9 - window = **[0, 3, 6, 8]** → windows = **front-9 (h1-h9) → front-6 (h4-h9)
  → front-3 (h7-h9) → front-1 (h9)**. These are the *closing* windows of the front nine.

**Front-window semantics check.** Per the brief and GDD §6.1 ("fewest strokes over the CLOSING holes
… rewards finishing STRONG"), every window is a closing window of its segment, so front-6 = h4-h9
(the last 6 of the front nine), NOT h1-h6. The code emits front-6 = startIdx 3 = holes 4-9 → matches
the closing-window reading. CORRECT. (The red-team's offhand "holes 13-18" / "holes 1-6" comments
labelled the back side right but were imprecise on the front side; the implementation is correct
to the GDD's "closing/finishing strong" language. Per the brief, do NOT flag this.)

### N=9 generalization trace

`half = (9+1)/2 = 5`. `CountbackWindows(5)` yields **[5, 3, 1]** (`6 < 5` false → skipped; 3<5 and
1<5 true). No hardcoded 18 or 9 in the implementation.
- Back pass: startIdx = 9 - window = **[4, 6, 8]** → back-5 (h5-h9) → back-3 (h7-h9) → back-1 (h9).
- Front pass: startIdx = 5 - window = **[0, 2, 4]** → front-5 (h1-h5) → front-3 (h3-h5) → front-1
  (h5).

Cesar's clarification (hole count is per-tournament; 9 and 18 are real cases; do not hardcode 18) is
honoured by the `half = ceil(N/2)` + filtered `CountbackWindows` pair — generalizes to any N ≥ 1.

### Manual fixture re-derivation (the proof, not just the test passing)

**§28 18-hole back-9 (L1161-1198):** player h1=5, h2-h9=4, h10=3, h11-h18=4. Total = 5+32+3+32 = 72;
bot par-4 ×18 = 72. TIED on total. Back-9 (h10-h18) player = 3+4×8 = **35**, bot = 4×9 = **36** →
player wins on back-9. Test asserts `playerIdx < botIdx` after sort → matches.

**§29 18-hole front-6 (L1212-1253):** player h1-h3=6 (+3 each), h4-h6=2 (−3 each), h7-h18=4. Total
= 18+6+48 = 72 = bot. Back-9 (h10-h18) player = 4×9 = 36 = bot. Back-6/3/1: all 4s on both sides →
TIED. Front-9 (h1-h9) player = 18+6+12 = 36 = bot. **Front-6 (h4-h9, startIdx=3)** player = 2+2+2+
4+4+4 = **18**, bot = 4×6 = **24** → player wins on front-6 (the FIRST surviving window). Test asserts
`playerIdx < botIdx` → matches.

**§31 9-hole back-3 (L1305-1344):** player h1-h4=4, h5=5, h6=4, h7=3, h8-h9=4. Total = 16+5+4+3+8 =
36 = bot. Back-5 (h5-h9): player=5+4+3+4+4=20, bot=20. TIED. **Back-3 (h7-h9, startIdx=6):** player
=3+4+4=**11**, bot=4×3=**12** → player wins. Test asserts `playerIdx < botIdx` → matches.

**§30 rank-skip boundary (L1260-1292):** 3 bots all par-4 ×3 = 12, player 5×3 = 15 (strictly worse).
Expected board: `[0..2].Rank=1, IsTie=true; [3].Rank=4, IsTie=false`. Asserts `fourth.Rank == 4` and
`fourth.IsPlayer == true` and `fourth.IsTie == false`. AssignRanks (L645) `nextRank = rank + (j - i)`
with j-i=3 → 1+3 = 4. Correct.

These are real ORDER assertions, not tautologies (`board[0].Rank == board[1].Rank` would be
tautological; `Assert.Less(playerIdx, botIdx)` is not). CONFIRM-PASS on all four new fixtures.

---

## Acceptance re-walk (Rule 5 — entire SPEC §6 list, not just the countback fix)

| SPEC §6 acceptance bullet | Test(s) | Status |
|---|---|---|
| State derivation: 6 states at boundary `now` | L219-277 | CONFIRM-PASS |
| Resolve gate flips at `EndUtc + resolveDelay` | L283-303 | CONFIRM-PASS |
| Register: debits once, idempotent, insufficient RP rejected, free-entry skip, char locked | L337-380 | CONFIRM-PASS |
| SubmitHoleResult: append + Finished + dup/post-EndUtc reject + persisted reload | L407-467 | CONFIRM-PASS |
| Leaderboard final: strokes asc + **countback (back-9 then front-9 paths)** + time + timestamp | §28 (back-9), §29 (front-6), §25 (3-hole back-1), §31 (9-hole back-3) | **CONFIRM-PASS** (NEW: 18-hole paths actually exercised) |
| Ties shared rank + "T" flag + next rank skips (N-way) | §24 (3-way at rank 1), §25, §30 (rank-skip boundary at rank 4) | CONFIRM-PASS |
| DNF: below finishers, hidden from ranked rows, player DNF in sticky row, DNF ordering | L564-592 | CONFIRM-PASS |
| Provisional: score-to-par-so-far (D3); `IsProvisional` true pre / false post | §27, L483-533 | CONFIRM-PASS |
| Prizes / split-pool: band match, 2-way + N-way tie pool, RP rounded-up, indivisible item duplicated | §26 (2-way 950 split), PrizeSplitFormula L1159+ (incl. (100,3,34)) | CONFIRM-PASS |
| GetResults / ClaimPrize: null pre-resolve, correct after, claim-once incl. **survives store reload** | L599-654, L986-997, `ClaimPrize_SurvivesStoreReload_NoDoubleGrant` | CONFIRM-PASS |
| Determinism via clock: same (seed, fixedNow) → identical board, per-row equality on non-empty | `Determinism_SameClockAndSeed_ProduceSameLeaderboard` L741-761 | CONFIRM-PASS |
| §6 generalization to arbitrary N (Cesar correction) | §31 (N=9), code uses `(N+1)/2` not hardcoded | CONFIRM-PASS |

### T2/T3 API binding regression (red-team confirmed clean, re-spot-checked)

- `def.HoleSet` / `def.ClubId` / `def.EndUtc` / `def.StartUtc` / `def.ResolveDelayMinutes` /
  `def.EntryFeeRP` / `def.PrizeTableId` / `def.BotFieldId` — read at the expected sites.
- `BotFieldGenerator.RollField(def, cfg, holePars)` L201; `Project(card, now) → BotProjection
  {Thru, RevealedStrokes, Complete}` L214-219 — match T3 shipped (commit `a5a099ab6` close-out).
- `BotCard.{BotId, PerHoleStrokes, TotalStrokes, StartOffsetSeconds, PerHoleCompletionUtc}` — read
  at L233-247.
- `FakePlayerRow(id, username, characterId, level)` + `BotScoreBracketRow(minLevel, meanDelta,
  stdev)` — used in `MakeWithBots`. Match T3 signatures.

No invented overloads. CONFIRM-PASS.

---

## Drift audit (Rule 13)

`git status --porcelain --untracked-files=all` outside the task folder:

| Path | State | Attributed in report? |
|---|---|---|
| `Assets/Scenes/ShellScene.unity` | `M` (pre-existing per iter-1/iter-2/iter-3 baselines L1/L8/L30) | YES (report L130) |
| `Packages/manifest.json` | `M` (MCP auto-bump, iter-2 baseline L10) | YES (report L131) |
| `Packages/packages-lock.json` | `M` (companion, iter-2 baseline L11) | YES (report L132) |
| `Assets/Scripts/Tournaments/ITournamentEntryStore.cs` (+ .meta) | `??` (added iter-2) | YES (report L133) |
| `Assets/Scripts/Tournaments/ITournamentSeams.cs` (+ .meta) | `??` (added iter-1) | YES (report L134) |
| `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` (+ .meta) | `??` (iter-1, main deliverable) | YES (report L135) |
| `Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs` (+ .meta) | `??` (iter-1) | YES (report L136) |
| Task-folder docs (HEARTBEAT/IMPLEMENTER_REPORT/SELF_REVIEW/REDTEAM_REVIEW/ARCHITECT_REVIEW) | `??` (in-folder) | N/A (in-folder, exempt) |

Every outside-task path is attributed. No undeclared drift.

`git diff HEAD -- Assets/Scripts/Physics/` = empty (Rule 7 standing ban — CONFIRM-PASS).
`git diff HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` = empty (no `*Gate` scenarios —
CONFIRM-PASS).

### Boundary check (T5 / T6 / T9)
Re-spot-checked the three files — no `SaveData` / `SaveSchemaMigrator` / `PersistedTournamentEntry`
(T5 owns save schema), no round-loop symbols (T6 owns round loop), no `LeaderboardManager` /
roster-screen refs (T9 owns leaderboard screen). Zero `using UnityEngine` in `LocalTournamentBackend
.cs` / `ITournamentEntryStore.cs` / `ITournamentSeams.cs` — headless discipline maintained.
CONFIRM-PASS.

---

## Bbox / scene-mutation / production-flow / capture compliance

N/A — headless C# logic task; no UI / no scene mutation possible / no capture / no Figma node.
Visual-review steps 1, 6, 7, 8 don't apply. Step 5 (capture-helper compliance) N/A: no `*Context.cs`
files added; no screenshot.

---

## Report integrity (Rule 6)

Every PASS line in the report's acceptance table cites either a concrete test name (`§NN`,
`ClaimPrize_SurvivesStoreReload_NoDoubleGrant`, etc.) or a verifiable code line (`EndingThreshold =
TimeSpan.FromHours(1.0)`). I re-ran the gates that back the claims (class run 68/0/0, full run
658/0/3) and the counts match. No fabricated tool result detected.

---

## Iteration / circuit-breaker

Iter-3 of shape `ranking:countback-back9-ladder` (per the report's `Iteration shape` declaration L3).
The prior iter-1/iter-2 iterations were of different shapes (`d-claim:persisted-store` and
similar). This is the first iter of THIS shape → far below the 3-strike circuit-breaker. Forward.

---

## Verdict

**FORWARD_TO_ARCHITECT** (SELF_REVIEW_PASS).

The red-team blocker (countback back-9 missing for 18-hole sets) is RESOLVED in code at L521-561 by
moving from `CountbackWindows(H, H)` to `half = ceil(N/2); CountbackWindows(half)`. The fix produces
the GDD §6.1-LOCKED back-9 → back-6 → back-3 → back-1 ladder for N=18 and generalizes to N=9 (and
arbitrary N) per Cesar's clarification. Three new pinned fixtures (§28 18-hole back-9, §29 18-hole
front-6, §31 9-hole back-3) assert real board ORDER, not tautologies; a fourth (§30) strengthens the
rank-skip boundary the red-team called out as un-asserted.

Test gate I ran myself:
- `LocalTournamentBackendTests` class: 68 PASS / 0 FAIL / 0 SKIP
- `Golfin.Tournaments.Tests` namespace: 154 PASS / 0 FAIL / 0 SKIP
- Full EditMode: 658 PASS / 0 FAIL / 3 pre-existing-skip (Stage C1 HoleCompleteDriver no-ops)

No regressions on the iter-2 fixes. Outside-task drift fully attributed. Standing bans (Physics,
Scenarios) clean. Cross-boundary leakage (T5/T6/T9) none.

Setting `STATUS.md` → `SELF_REVIEW_PASS`.

| File | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_local_backend/SELF_REVIEW.md` | This review (iter-3 overwrite) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_local_backend/STATUS.md` | Set to SELF_REVIEW_PASS |
