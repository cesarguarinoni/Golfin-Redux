# Architect Review — `tournament_character_snapshot`

**Iteration:** 1
**Reviewer:** golfin-reviewer
**Timestamp:** 2026-06-26 14:49 CEST
**Verdict:** **PASS → READY_FOR_REDTEAM**

> TELLCODE-tier headless C# task. No Figma, no screenshot, no video, no mesh metrics. Gate = EditMode test suite (`Golfin.Tournaments.Tests`). Pixel-scan / Figma-fidelity / bbox / mesh-metrics steps are **N/A** for this task. Substituted a code-walk against SPEC §6 + independent re-run of the full EditMode suite via Unity MCP.

---

## Independent code re-verification (PIPELINE_HARDENING Rule 5 — NOT carried forward from self-reviewer)

Each row reverified by reading the file myself, not by accepting the self-reviewer's PASS.

### SPEC §6 acceptance walk

| # | SPEC item | Verdict | Independent evidence |
|---|---|---|---|
| 1 | `CharacterSnapshot` immutable, primitives-only, value-equality | **PASS** | `CharacterSnapshot.cs:26` — `sealed class`, all six props are `{ get; }` only with ctor assignment, ctor validates `characterId`. `Equals` (line 72-81) compares all six fields with `StringComparison.Ordinal` on the id; `GetHashCode` (83-95) combines all six. No `UnityEngine` reference — only `System`. Trivially serializable for T5. **Test §6.1 (`Register_CapturesSnapshotFromProvider`) genuinely depends on value-equality** (`Assert.AreEqual(expectedSnap, entry.Snapshot)`), so the equality contract is exercised. |
| 2a | `ICharacterStatsProvider` is pure interface, no `UnityEngine` | **PASS** | `ICharacterStatsProvider.cs:11-13` — only `System` + `System.Collections.Generic`. Interface (line 29-38) is one method `SnapshotFor(string)` with documented `KeyNotFoundException` contract. |
| 2b | `FakeStatsProvider` exists with mutable source | **PASS** | `ICharacterStatsProvider.cs:53-75` — `Register(id, snapshot)` does `_snapshots[id] = snapshot` (replace-or-add → mutable source). `SnapshotFor` throws `KeyNotFoundException` (line 72-73) on miss. |
| 2c | `CharacterManagerStatsProvider` adapter exists; throws on unknown id | **PASS** | `CharacterManagerStatsProvider.cs:29-45` — calls `CharacterManager.Instance.GetCharacterData(id)`, throws `KeyNotFoundException` on null (line 34-36). Copies the five fields (`characterId`, `currentLevel`, four `current*`) into a new `CharacterSnapshot`. **Read-path choice is sound**: `current*` are maintained on every `RefreshStatValues` call from `ConfirmSPAllocation`, so a direct read is correct without re-refreshing here. |
| 3a | `EntryState.Snapshot` field added, additive, preserves `CharacterId` | **PASS** | `EntryState.cs:39` — `public CharacterSnapshot? Snapshot { get; }`. `CharacterId` (line 26) untouched. Nullable to allow legacy data (pre-amendment entries). |
| 3b | All `new EntryState(` clone sites preserve `Snapshot` | **PASS** | Grep result: **6 sites total**. `LocalTournamentBackend.cs:136` (Register — sets via fresh snapshot, correct), `:189` (**SubmitHoleResult** — explicitly passes `snapshot: entry.Snapshot` with a comment "preserve frozen snapshot across hole submissions" — the critical clone site). `StubTournamentBackend.cs:43` is a static stub on the legacy ctor (snapshot=null, intentional — stub backend, never used in production). `TournamentContractsTests.cs:120`, `LocalTournamentBackendTests.cs:861, 872, 873` all use the 6-param legacy ctor (snapshot=null) — intentional, those tests don't exercise the snapshot path. **No silent drop anywhere.** |
| 4 | `Register` captures snapshot at sign-up (the freeze) | **PASS** | `LocalTournamentBackend.cs:131-145` — after RP debit, before EntryState build: `CharacterSnapshot? snapshot = _stats?.SnapshotFor(characterId);` passed via `snapshot:` to the EntryState ctor. **No other code path calls `SnapshotFor`** (grep-confirmed: only call site is line 134). The freeze point matches SPEC §5 ("capture happens at Register, not round-start"). |
| 4-gate | §6.2 freeze-invariant test genuinely proves immutability | **PASS — genuine** | `LocalTournamentBackendTests.cs:1408-1430`. The test creates two **distinct `CharacterSnapshot` instances** (`before` lvl-10, `after` lvl-99), registers `before` in the fake, calls `backend.Register("t1", …)`, then `statsProvider.Register("char_player", after)` to replace the dict entry with the second distinct instance, then `backend.GetMyEntry("t1")` and asserts `reloaded.Snapshot.Equals(before)` AND `!reloaded.Snapshot.Equals(after)`. **Two-pronged assertion (must-equal AND must-not-equal) defeats the trivial false-pass**, and because `CharacterSnapshot` is genuinely immutable (no setters, no mutating methods exist), a shallow-reference bug would require the fake to return the same instance both times — which it doesn't, two distinct allocations. **Immutability of `CharacterSnapshot` is what makes this freeze real** — if any field had a setter, mutating the source after Register could leak. None does. |
| 5 | Scoring/leaderboard untouched | **PASS** | `git diff HEAD -- LocalTournamentBackend.cs` (200 lines inspected) — hunks land in: ctor field+param (lines 33-44, 54-79), Register (128-140), SubmitHoleResult (189-192). **Zero edits** to `GetLeaderboard`, `GetResults`, `ClaimPrize`, `Countback`, `CompareProvisional`, `CompareFinal`, `AssignRanks`, `ResolvePrize`. |
| 6 | Tests green; new 4 + all pre-existing | **PASS** | Independently re-ran via Unity MCP CLI `tests-run` (see § EditMode test re-run below). |

### Self-reviewer flagged items — independent assessment

| Flag | Severity | Decision |
|---|---|---|
| **(a) ctor `stats` param is OPTIONAL, defaults `null`** | Forward-looking risk — **NOT a blocker for this task** | I independently grep-confirmed: **zero production `new LocalTournamentBackend(` call sites today.** The only constructions are the 4 test sites (lines 124, 186, 975, 1367 of `LocalTournamentBackendTests.cs`), and the new `CharacterSnapshotTests` site passes `stats: statsProvider` explicitly. SPEC §7 In-scope reads "ctor wiring at production call sites" — vacuously satisfied. The risk Cesar must own: **when production wiring lands (T6 or a UI factory), the compiler will not catch a forgotten `stats` arg → snapshots silently `null` → freeze silently no-ops.** Recommended follow-up (NOT this task): in the T6 wiring task, either (i) flip the param to required after migrating the 3 legacy test sites to pass `stats: null` explicitly, or (ii) add a runtime guard in `Register` that throws if `_stats == null` AND a non-empty `characterId` is provided. **Surface to Cesar as a known-but-deferred future-risk so it doesn't get lost when production wiring lands.** |
| **(b) `_stats` non-nullable field assigned via `stats!`, accessed via `_stats?.`** | Annotation lie, functionally correct — **NOT a blocker** | Field on line 36 is declared `private readonly ICharacterStatsProvider _stats;` (non-nullable), assigned `_stats = stats!;` on line 84 (bang-suppress from nullable param), then accessed `_stats?.SnapshotFor(characterId)` on line 134 (which only makes sense if it can be null). The field type lies about nullability. Functionally correct (null-conditional handles the null), but stylistically incoherent. **Surface to Cesar:** trivial follow-up fix — declare the field `ICharacterStatsProvider?` to match reality. Bundled with (a) — both go away once param is required. |
| **(c) tests in `CharacterSnapshotTests` fixture rather than extending `LocalTournamentBackendTests`** | Minor SPEC §6 wording deviation — **NOT a blocker** | SPEC §6 reads "extend `LocalTournamentBackendTests`". Implementer added a new `[TestFixture] CharacterSnapshotTests` class **inside the same file `LocalTournamentBackendTests.cs`** (line 1352) and same assembly. Cohesive, separates concerns, defensible. Not worth a re-spin. |

### Adapter assembly placement (SPEC deviation in implementer report § Spec deviations)

`CharacterManagerStatsProvider` lives in `Assets/Scripts/TournamentsRuntime/` (Assembly-CSharp) rather than `Assets/Scripts/Tournaments/` (Golfin.Tournaments.asmdef). **Justified and standard**: an asmdef cannot reference Assembly-CSharp, so the production adapter that touches the `CharacterManager` Unity singleton must live outside the asmdef boundary. Same pattern as other adapters bridging asmdef code to global singletons. The interface + DTO stay inside the asmdef as specified. PASS.

---

## EditMode test re-run (Unity MCP CLI `tests-run`, independent re-execution)

`testFilter`/`assembly` parameters are **not respected** by this Unity MCP build (confirmed by passing `"DEFINITELY_DOES_NOT_EXIST_XYZ"` and still getting the full 665 — same exact summary). So I ran the **whole project EditMode suite** three times. All three runs:

```
Status: Passed
TotalTests:   665
PassedTests:  662
FailedTests:    0
SkippedTests:   3
Duration: ~40-90s
```

The **3 skips are all in `Golfin.Physics.Tests.HoleCompleteDriverTests`** — pre-existing Stage-C1 deprecation skips with explicit messages ("HandleShotComplete is now a no-op", "HoleCompletionBridge is the sole caller"). **Zero skips and zero failures in `Golfin.Tournaments.Tests`.** The implementer's claim of `158 passed / 0 failed / 0 skipped` for the Tournaments namespace is consistent with: full suite green, no Tournaments tests appear in the skip list, no Tournaments tests appear in the fail list.

Per-test status of the **4 new tests** (cited from implementer report, verified against the actual test code at lines 1382-1460):

| Test | Method | Status |
|---|---|---|
| §6.1 — captures from provider | `Register_CapturesSnapshotFromProvider` | PASS |
| §6.2 — **freeze invariant (THE GATE)** | `Register_SnapshotIsFrozen_MutatingProviderAfterRegister_DoesNotAffectEntry` | **PASS — genuine** |
| §6.3 — round-trip | `Register_SnapshotSurvivesStoreRoundTrip` | PASS |
| §6.4 — unknown id throws | `Register_UnknownCharacterId_ThrowsKeyNotFoundException` | PASS |

Pre-existing tests in the namespace: 154 (158 total minus 4 new) — all PASS (zero in the 3-test skip list, zero failures suite-wide).

---

## Report integrity gate (PIPELINE_HARDENING Rule 6)

Every PASS in the implementer report is backed by a real tool output or a file citation I could re-verify. No fabrication detected. The §32 test names in the report exactly match the test method names in the source. The 665-test summary, while not subset to the namespace by the MCP, is consistent with namespace-green via the skip/fail set being entirely outside `Golfin.Tournaments.Tests`.

---

## Scene-mutation audit

`git diff HEAD -- Assets/Scenes/` shows only the pre-existing `ShellScene.unity` dirty state captured in the iter-1 kickoff baseline (HEARTBEAT.log:7). Not introduced by this iter. PASS.

---

## Verdict reasoning

All 11 SPEC §6 acceptance items verified independently against the actual source. The §6.2 freeze-invariant test is **genuine** — defeated the trivial false-pass via two-pronged assertion against two distinct `CharacterSnapshot` allocations, only possible because `CharacterSnapshot` is genuinely immutable. All six `new EntryState(` sites accounted for (the SubmitHoleResult clone correctly preserves `Snapshot`). Scoring/leaderboard untouched per diff. Adapter throws on unknown id as specified. Full EditMode suite re-run via Unity MCP: 662 PASS / 0 FAIL / 3 pre-existing Physics skips (no Tournaments skips).

The three self-reviewer flags are forward-looking — the optional-`stats` default is a future-wiring trap that is **vacuous today** (no production call sites exist), but Cesar should bundle a "make required + fix `_stats?` annotation" follow-up into the T6 wiring task before any production call site is introduced. **Calling this out as the single non-blocker thing the red-team and Cesar should explicitly acknowledge** so it doesn't get lost when T6 lands.

---

## Files reviewed

| Path | Notes |
|---|---|
| `Assets/Scripts/Tournaments/CharacterSnapshot.cs` | New — immutable DTO, value-equality |
| `Assets/Scripts/Tournaments/ICharacterStatsProvider.cs` | New — interface + FakeStatsProvider |
| `Assets/Scripts/TournamentsRuntime/CharacterManagerStatsProvider.cs` | New — production adapter (Assembly-CSharp) |
| `Assets/Scripts/Tournaments/EntryState.cs` | Modified — Snapshot field + dual ctors |
| `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` | Modified — ctor param, Register freeze, SubmitHoleResult preserve |
| `Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs` | Modified — new `CharacterSnapshotTests` fixture (4 tests) |
| `Assets/Scripts/Tournaments/StubTournamentBackend.cs` | Read only — legacy-ctor call site, snapshot=null (intentional stub) |
| `Assets/Scripts/Tournaments/Tests/TournamentContractsTests.cs` | Read only — legacy-ctor call site, snapshot=null (intentional) |

---

## Note for the red-team gate

Specific pressure points to attack if you're trying to break this:

1. **The freeze-invariant test reliance on immutability.** Are there any code paths that could mutate a `CharacterSnapshot` after creation? (I find none — sealed class, all properties get-only, no methods.)
2. **`SnapshotFor` exception type.** SPEC §4 says "throw"; both adapter and fake throw `KeyNotFoundException`. The §6.4 test asserts on `KeyNotFoundException` specifically. If the architect later switches to `ArgumentException` or similar, the test breaks. Documented contract on the interface (line 34-36) makes this stable.
3. **The optional-`stats` ctor default.** Today vacuous, tomorrow a trap. If the red-team holds the bar at "no future-risk landmines accepted," they may FAIL this and demand the param be required now. My judgment: SPEC §7 only requires production call sites to pass the real adapter, and none exist, so the bar is met. But this is the closest thing to a defensible FAIL.
4. **`SubmitHoleResult` preserve.** Line 192 explicitly copies `snapshot: entry.Snapshot`. There is no test that hits this clone path with a non-null snapshot AND then asserts the snapshot survives a `SubmitHoleResult` call. The §6.3 round-trip test goes Register → GetMyEntry (no SubmitHoleResult). A targeted test would be: Register with snapshot, SubmitHoleResult, GetMyEntry, assert snapshot unchanged. This is a minor coverage gap. The line is small + commented + visually obvious, and T5 will add its own SubmitHoleResult round-trip coverage, so I'm not failing on it — but red-team may legitimately push for it.
