# Red-Team Review — tournament_local_backend (T4)

**Gate:** golfin-redteam-reviewer (adversarial)
**Iteration reviewed:** 3
**Timestamp:** 2026-06-26 (JST)
**Verdict:** **ARCHITECT_REVIEW_PASS**

Headless C# logic task — no screenshot / Figma / mesh / video. Rules 14/16/17/18 do NOT apply
(wrong gate). The acceptance gate is the EditMode suite (SPEC §11 / Rule 3) + correctness of the
§6-LOCKED countback ladder I FAILED at iter-2. I re-ran every suite myself, re-derived the
countback ladder by hand for all realistic N, re-attacked the iter-2 blocker, and tried three
ways to break it. It survived. PASS.

---

## 1. Tests — re-run BY ME, real, reproduce exactly

Driven via `unity-mcp-cli run-tool tests-run --url http://localhost:21573` against the live Editor
(PID 6219):

```
tests-run(EditMode, testClass=LocalTournamentBackendTests)
  Status: Passed  Total: 661  Passed: 68   Failed: 0  Skipped: 0   (1.38s)

tests-run(EditMode, testNamespace=Golfin.Tournaments.Tests)
  Status: Passed  Total: 661  Passed: 154  Failed: 0  Skipped: 0   (2.59s)

tests-run(EditMode, full)
  Status: Passed  Total: 661  Passed: 658  Failed: 0  Skipped: 3   (41.78s)
```

68 / 154 / 658 — match the implementer, self-reviewer, and reviewer counts to the integer. The 3
skips are pre-existing `Golfin.Physics.Tests.HoleCompleteDriverTests` Stage C1 no-ops (I read the
`Message` field on each: "HandleShotComplete is now a no-op" / "HoleCompletionBridge is the sole
caller") — NOT from this task. No fabrication (Rule 6 clean). No padding.

---

## 2. Countback ladder — my iter-2 blocker, re-derived by hand (NOW CORRECT)

Read `Countback` / `CountbackWindows` (`LocalTournamentBackend.cs` L521-561). The fix:
`int half = (N+1)/2; foreach window in CountbackWindows(half)` — back pass `startIdx = N-window`,
front pass `startIdx = half-window` (guarded `if (N > half)`). `CountbackWindows(half)` yields
`[half, 6, 3, 1]` keeping only those `< half`.

I traced the windows for **all** N (script `scratchpad/countback_trace.py`):

| N | half | windows | BACK startIdx (holes 0-based) | FRONT startIdx | OOB? |
|---|---|---|---|---|---|
| **18** | 9 | [9,6,3,1] | 9/12/15/17 → back-9(h10-18)/6(h13-18)/3(h16-18)/1(h18) | 0/3/6/8 → front-9(h1-9)/6(h4-9)/3(h7-9)/1(h9) | clean |
| **9** | 5 | [5,3,1] | 4/6/8 → back-5(h5-9)/3(h7-9)/1(h9) | 0/2/4 → front-5(h1-5)/3(h3-5)/1(h5) | clean |
| 1 | 1 | [1] | 0 → hole 0; front pass SKIPPED (N≤half) | — | clean |
| 2 | 1 | [1] | 1 | 0 | clean |
| 3 | 2 | [2,1] | 1,2 | 0,1 | clean |
| 4-8 | … | … | … | … | clean |

- **N=18 (the shipped production path — all 6 tournaments.csv rows are holeSet=1-18):** the back
  pass now emits **back-9 (window=9, startIdx=9) FIRST**, exactly the GDD §6.1 LOCKED ladder. My
  iter-2 blocker (first yield was `H=18` → guaranteed no-op → back-9 never produced) is GONE.
- **Front-window semantics:** GDD §6.1 L89 = "fewest strokes over the CLOSING holes … rewards
  finishing STRONG." front-6 = startIdx 3 = holes 4-9 (last 6 of the front nine) — the closing
  window. The code emits exactly this. CORRECT per the GDD; my iter-2 parenthetical "holes 1-6"
  was the wrong reading. Did NOT fail it for this.
- **Generalization (Cesar's correction — hole count is per-tournament, not hardcoded 18):** driven
  purely by `holePars.Count`. `grep` for literal 18/9 in the countback logic → comment lines only,
  no code branch. N=9 path filters `6<5` cleanly → [5,3,1].
- **Degenerate-N attack:** N=1 cleanly skips the front pass; no negative startIdx, no out-of-range,
  no double-counted window in any N. `SumHoles` additionally clamps via `Math.Min(start+count,
  perHole.Count)`, so even a partial DNF entry can't index OOB. For odd N the middle hole appears
  in both a back and a front window (inherent to ceil-split) — harmless for tie-breaking, and never
  occurs for the two shipping sizes (9, 18) which are overlap-free.

### §28 genuinely PINS back-9 (would FAIL if back-9 regressed)

§28 player: h1=5, h10=3, rest 4 → total 72 = bot 72. They differ only on h1 (front) and h10 (back).
- **Fixed code:** back-9 = h10-h18 = 3+4×8 = **35** < bot 36 → player wins → `Assert.Less(playerIdx, botIdx)` PASSES.
- **Under the iter-2 bug** (back-9 absent): back-6/3/1 all tie → front-9 = 5+4×8 = **37** > bot 36 → bot wins → assertion would FLIP and FAIL.

So §28 is a true regression guard, not a tautology — exactly the worked example from my iter-2
review. §29 (front-6 resolves after all back + front-9 tie) and §31 (N=9 back-3) likewise assert
real positional ORDER after the live sort. §30 (3-way tie → 4th is rank 4) pins the `nextRank =
rank + (j-i)` skip. All four re-derived by hand against the test bodies — arithmetic checks out.

---

## 3. iter-1/iter-2 fixes — no regression under the refactor

- **Persisted claim-once (D-claim b):** `ClaimPrize` (L361-383) guard reads `_store.IsClaimed(id)`
  (store, not the `_resultMemo` display memo); grant RP → grant item → `_store.MarkClaimed(id)`
  (grant-then-mark). A fresh backend over the same store short-circuits at the guard.
  `ClaimPrize_SurvivesStoreReload_NoDoubleGrant` builds backend2 over the SAME store with fresh
  `rp2(0L)` and asserts `rp2.Balance == 0L`. Re-derived — correct, no double-grant. GONE/RESOLVED.
- **All 8 ITournamentBackend methods** bound to real T2/T3 APIs (`RollField`/`Project`/`BotCard.*`/
  `FakePlayerRow`/`BotScoreBracketRow`) — spot-checked, no invented overloads.
- **Split-pool §26** (pool 1900 → ceil/2 = 950, player-favorable), **DNF** (below finishers, holes-
  done desc then strokes asc, hidden from ranked rows, player DNF sticky), **provisional vs final**
  (§27 score-to-par) — all present and asserted; passed in my own runs.

---

## 4. Standing bans + drift — CLEAN (verified, not trusted)

- `git diff HEAD -- Assets/Scripts/Physics/` = empty. `…/Scenarios.cs` = empty (no `*Gate`).
- Outside-task drift = `M ShellScene.unity` + `M Packages/manifest.json` + `M packages-lock.json`,
  plus the 4 task `.cs`(+.meta) deliverables (`??`). I verified attribution, not just the claim:
  HEARTBEAT iter-1 baseline cites HEAD `a7a388953` with `M ShellScene.unity` already DIRTY; the
  current HEAD `159dfd914` is two spec-doc commits newer (T4/T5 spec reconciliation) — none staged
  ShellScene. ShellScene's last real commit was `fc672f86d` (T7), and its current dirty diff
  contains **zero** tournament/LocalTournament refs (grep empty). Genuinely pre-existing, not this
  task disguised. manifest/lock = MCP auto-bump (iter-2 baseline). No undeclared drift.
- **Headless + boundary:** zero `using UnityEngine` / `MonoBehaviour` in the 3 logic files. No
  actual `SaveData`/`SaveSchemaMigrator`/`PersistedTournamentEntry` code (only doc-comment mentions
  of the T5 seam). No `LeaderboardManager`/roster-screen (T9). No round-loop symbols (T6). In-lane.

---

## 5. Report integrity (Rule 6)

Every PASS in the implementer report maps to a test I re-ran (68/154/658) or a code line I read
(`half=(N+1)/2` L524, `ClaimPrize` guard L365, `EndingThreshold` D2). The "Total in class 68 (was
64)" delta = the 4 new fixtures §28-§31, which I read and which exist. No fabricated tool output,
no fabricated approval quote.

---

## Three break-attempts and why each FAILED to break it

- **Visual/pixel:** N/A (headless).
- **Geometric/numeric:** re-ran all three suites (real, green, exact counts); re-derived the
  countback ladder for N ∈ {1,2,3,4,5,6,7,8,9,18} — no OOB, no degenerate/duplicate window, back-9
  emits first for N=18; re-derived §28's pin (flips under the old bug); re-derived claim-once
  ordering and the 950 split. **Nothing past threshold. Could not break.**
- **Spec-intent:** GDD §6.1 LOCKS "back-9 first" + closing-window front ladder. The code now
  satisfies both the letter (3-hole back-1, 9-hole back-3, 18-hole back-9) AND the intent on the
  only shipped sizes. The exact coverage hole I named at iter-2 (no 18-hole fixture) is filled by
  §28/§29 with real ORDER assertions. **Could not break.**

## Prior rejections

No `CESAR_REJECTION.md` (T4 never reached Cesar). My own iter-2 blocker (back-9 absent for 18-hole
sets) is the only prior red-team finding — re-attacked above, **GONE**. No older defect regressed.

| File | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_local_backend/REDTEAM_REVIEW.md` | This review (iter-3, overwrites iter-2) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_local_backend/STATUS.md` | Set to ARCHITECT_REVIEW_PASS |
