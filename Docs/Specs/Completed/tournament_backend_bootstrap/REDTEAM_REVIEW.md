# Red-Team Review — `tournament_backend_bootstrap` (iter-2)

**Reviewer:** golfin-redteam-reviewer
**Timestamp:** 2026-06-26 23:25 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS**

> Supersedes the iter-1 verdict below (kept in git history). Iter-1 was
> ARCHITECT_REVIEW_FAIL: "the regression gate this task exists to build does not
> exist" — all 22 tests exercised local copies / interface fakes, zero coverage
> of the production wire (`Compose()` / `Register()` / non-null `Snapshot`). Fix
> instruction: a test on the production wire that goes RED if `stats:` is dropped
> from `Compose()`. **Iter-2 delivers exactly that, and I proved it empirically.**

---

## Posture
CODE/INTEGRATION task. SPEC §0: no visual fidelity → Rule 8 N/A; no
Figma/screenshot/mesh gate manufactured. The gate is the test suite. I did not
trust the reviewer's or implementer's cited counts — I re-ran the tests myself
and additionally ran a destructive mutation to prove the guard's teeth.

---

## Attack #1 — does the regression guard ACTUALLY fire RED if `stats:` is removed? (the core question)

**PROVEN RED, empirically.** I temporarily removed
`stats: new CharacterManagerStatsProvider()` from `TournamentService.Compose()`
(line 109), `assets-refresh`, waited for recompile, and re-ran the assembly:

```
Summary: Status=Failed, TotalTests=721, Passed=20, Failed=1, Skipped=0
FAILED: TournamentServiceWireupTests.Compose_Register_SnapshotHasCorrectStats
  Message: "Snapshot MUST be non-null. REGRESSION: if this FAILS it means
            `stats: new CharacterManagerStatsProvider()` was removed from
            TournamentService.Compose() — the silent-null trap has fired.
            Expected: not null  But was: null"
```

Exactly one test went red — the guard — and the other 20 stayed green, proving
the guard is *specifically* coupled to the stats wire, not a flaky catch-all. I
then restored `TournamentService.cs` to its original SHA (`259b411a…`),
recompiled, and re-ran: **21/21 PASS again**. The editor is left clean and green.

This is the demonstration the implementer was asked for but did not perform; I
performed it. The guard is real, not a tautology, and not asserting on a value
the test itself set:
- `Compose_Register_SnapshotHasCorrectStats` (test file lines 354-383) calls the
  REAL `TournamentService.Compose()` by reflection-on-string-name, calls the REAL
  `ITournamentBackend.Register("kasumigaseki_open", 0, "char_james")`, asserts
  `entry.Snapshot != null` AND `Level/STR/CC/REC/STA == 10/6/7/6/6`. Those exact
  values come from `Characters.csv` via the real `CharacterManagerStatsProvider`,
  not from the test body.
- Root cause confirmed in `LocalTournamentBackend.cs`: `stats = null` default
  param (line 71); `CharacterSnapshot? snapshot = _stats?.SnapshotFor(...)`
  (line 134). Drop `stats:` → `_stats` null → `?.` short-circuits → null snapshot
  → assert fires. `CharacterSnapshot` has the asserted fields
  (Level/Strength/ClubControl/Recovery/Stamina) verified on disk.

**Swallowed-exception attack (strongest read-attack on reflection tests):** I
grepped every `catch` in the test file. There are exactly THREE
(lines 180, 326, 329) and ALL live in `ClearSingleton` / `TearDown` — they
swallow only when *clearing/restoring* singletons, never in an assertion or
Compose/Register/adapter-invocation path. No test body catches
`TypeLoadException` / `TargetInvocationException` / `NullReferenceException` and
lets the test pass. A mistyped type/method name (reflection-by-string) would
throw out of the test body and the test would FAIL — and indeed the par-throw
tests deliberately assert `TargetInvocationException` with
`InnerException is InvalidOperationException`, i.e. they correctly distinguish a
real production throw from a reflection plumbing error. The reviewer's claim that
"the only catches are in SetUp/TearDown" is correct — I verified it, didn't trust
it.

---

## Attack #2 — re-run the tests myself (I am the last gate; reviewer did not re-run)

Ran via `unity-mcp-cli run-tool tests-run` scoped to
`testAssembly: "Golfin.TournamentsRuntime.Tests"`, `includePassingTests: true`.
Editor verified idle (IsPlaying=false, IsCompiling=false) before the run.

```
Summary: Status=Passed, TotalTests=721, PassedTests=21, FailedTests=0, Skipped=0
21 / 21 rows PASS:
  TournamentServiceWireupTests.Compose_ReturnsNonNull_With6Tournaments        PASS
  TournamentServiceWireupTests.Compose_Register_SnapshotHasCorrectStats        PASS
  RealRewardPointsAdapterTests   (8 ToInt tests)                               8× PASS
  RealHoleParProviderAdapterTests (4 ParsFor tests)                            4× PASS
  RealItemRewardAdapterTests     (7 Grant tests)                               7× PASS
```

Matches the claimed 21/21 exactly. Full EditMode suite: 721 total, 718 PASS,
0 FAIL, 3 SKIP — the 3 skips are the pre-existing `HoleCompleteDriverTests`
Stage-C1 skips (messages confirm: "HandleShotComplete is now a no-op"),
unrelated to this task. No new failures introduced anywhere in the suite.

---

## Attack #3 — adapter tests hit REAL classes, not Fake* doubles

- **ToInt:** `RealRewardPointsAdapterTests` (file §2) calls the REAL
  `RewardPointsServiceAdapter.ToInt` (confirmed `internal static`, adapter line 44)
  via `CallStaticMethod(..., BindingFlags.NonPublic)`. Not the iter-1 local
  `ToIntContract` copy. Clamp tests pass `int.MaxValue+1`, `long.MaxValue`, `-1`,
  `long.MinValue` and assert clamp-to-MaxValue / clamp-to-0.
- **Par adapter:** `RealHoleParProviderAdapterTests` (file §3) does
  `Activator.CreateInstance(GetType("Golfin.Tournaments.HoleParProviderAdapter"))`
  — the real concrete type — and asserts `InvalidOperationException` (unwrapped
  from `TargetInvocationException`) on unknown id / non-numeric id / null
  RuntimeDatabase, plus correct ordering on a real injected `HoleDatabase`.
- **Item adapter:** `RealItemRewardAdapterTests` (file §4) instantiates the real
  `ItemRewardServiceAdapter`, bootstraps a real `SaveDataHost` + `NullPersister`,
  and asserts grant/increment/no-op(qty≤0, null/empty id)/MarkDirty against the
  real `SaveData.itemQuantities` and the real `_pendingWrite` field — not a value
  the test set.

---

## Attack #4 — asmdef / IVT correctness

- `Golfin.TournamentsRuntime.Tests.asmdef`: `overrideReferences: false`,
  `optionalUnityReferences: ["TestAssemblies"]`, references
  `["Golfin.Tournaments", "Golfin.Save"]`, `includePlatforms: ["Editor"]`.
- `AssemblyInfo.cs` (in `TournamentsRuntime/`, no asmdef there → compiles into
  Assembly-CSharp): `[assembly: InternalsVisibleTo("Golfin.TournamentsRuntime.Tests")]`.
  IVT target string == asmdef `name` EXACTLY. (ToInt is also reached via NonPublic
  reflection, so it would resolve regardless, but the IVT/name match is correct.)
- The assembly compiled and all 21 tests ran and were *discovered* — and the
  destructive mutation forced a recompile that still discovered + ran them —
  proving the asmdef graph resolves and is not silently excluding the fixture.

---

## Attack #5 — no regression vs iter-1

- `git diff HEAD -- Assets/Scripts/Physics/` → empty. No Physics edits.
- No `*Gate` scenarios; `Scenarios.cs` untouched.
- `git diff ShellScene.unity | grep -c "m_IsActive: 0"` → 0. No active-flips, no
  deleted GameObjects.
- Production `Compose()` unchanged — still passes `stats:` (restored to original
  SHA `259b411a…` after my mutation test).
- Rule 13: every untracked `??` path outside the task folder is the
  TournamentsRuntime production/test code listed in the report's file table.
  `Packages/manifest.json` + `packages-lock.json` were already dirty at session
  start (pre-existing T7 churn) — not introduced here.

---

## Three break-attempts and why each failed

1. **Visual/output** — N/A by SPEC §0; not manufactured.
2. **Geometric/contract (the reflection swallow attack)** — tried to find a
   `catch` that masks a broken wire or a tautological assert. All catches are in
   singleton clear/restore; the regression assert reads real CSV-derived stats
   the test never set. **Failed to break it.**
3. **Spec-intent (the iter-1 killer)** — the iter-1 FAIL was "letter satisfied,
   point missed: no test touches the production wire." Iter-2 inverts this: I
   *removed* the wire and the guard went RED, the literal definition of the
   regression gate SPEC §6 demands. **Failed to break it — this is the proof it
   holds.**

---

## Conclusion

The deliverable iter-1 was missing — an automated regression guard on the
production wire that fails if `stats:` is dropped — now exists, passes 21/21 on
my own re-run, and I have **empirically demonstrated it goes RED on the exact
regression** and GREEN again on restore. No code defect, no scene/Physics
regression, asmdef/IVT correct. Advancing to Cesar.

---
---

# (iter-1 verdict — retained for history)

**Verdict:** **ARCHITECT_REVIEW_FAIL** — "the regression gate this task exists to
build does not exist." All 22 tests exercised interface fakes / local contract
copies; zero coverage of `Compose()` / `Register()` / non-null `Snapshot`. Fix:
a PlayMode/EditMode test on the production wire that goes RED if `stats:` is
removed from `Compose()`. (Full iter-1 text preserved in git history of this
file; superseded by the iter-2 verdict above.)
