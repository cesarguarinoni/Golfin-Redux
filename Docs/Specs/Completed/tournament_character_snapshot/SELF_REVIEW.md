# Self-Review — `tournament_character_snapshot`

**Iteration:** 1
**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-06-26 21:35 JST
**Verdict:** **FORWARD_TO_ARCHITECT**

> TELLCODE-tier headless task. No Figma node, no screenshot, no video. Gate = EditMode test suite. Visual-review checklist (pixel scan, Figma A/B, bbox geometry) does NOT apply. Substituted a code-walk against SPEC §6 acceptance items.

---

## Visual diff notes

N/A — headless code task, no UI deliverable.

## Figma fidelity

N/A — no Figma node referenced in SPEC.md.

## Code review — SPEC §6 acceptance walk

| Item | Verdict | Evidence |
|---|---|---|
| `CharacterSnapshot` is immutable, primitives-only, value-equality | **CONFIRM-PASS** | `CharacterSnapshot.cs:26-99` — sealed class, get-only auto-props, ctor-only assignment, custom `Equals` covers all 6 fields with `StringComparison.Ordinal` for the id, `GetHashCode` matches. No `UnityEngine` import. |
| `ICharacterStatsProvider` is pure interface (no `UnityEngine`) | **CONFIRM-PASS** | `ICharacterStatsProvider.cs:11-38` — only `System` + `System.Collections.Generic` imports. Documented contract: throw `KeyNotFoundException` on unknown id. |
| `FakeStatsProvider` has mutable source for the freeze-invariant test | **CONFIRM-PASS** | `ICharacterStatsProvider.cs:53-75` — `Register` does `_snapshots[characterId] = snapshot` (replace-or-add), so a test can call Register a second time after backend.Register and verify the held snapshot didn't change. |
| `CharacterManagerStatsProvider` reads `CharacterManager.Instance.GetCharacterData` and throws on null | **CONFIRM-PASS** | `CharacterManagerStatsProvider.cs:29-45` — `if (charData == null) throw new KeyNotFoundException(...)`. Copies the five fields (`characterId`, `currentLevel`, four `current*` stats) into a new `CharacterSnapshot`. |
| `EntryState.Snapshot` field added (additive, nullable) | **CONFIRM-PASS** | `EntryState.cs:39` — `public CharacterSnapshot? Snapshot { get; }`. Primary 7-param ctor at line 63, legacy 6-param delegating ctor at line 85 with `snapshot: null`. Backward-compatible. |
| Freeze happens at `Register`, NOT round-start | **CONFIRM-PASS** | `LocalTournamentBackend.cs:131-145` — after RP debit, before EntryState build: `CharacterSnapshot? snapshot = _stats?.SnapshotFor(characterId);` then passed via `snapshot: snapshot` to the EntryState ctor. No other code path calls `SnapshotFor`. |
| `SubmitHoleResult` preserves Snapshot when cloning | **CONFIRM-PASS** | `LocalTournamentBackend.cs:192` — `snapshot: entry.Snapshot,` with the comment "preserve frozen snapshot across hole submissions". |
| All other `new EntryState(` call sites preserve / explicitly null Snapshot | **CONFIRM-PASS** | Grep audit — 6 call sites: 2 in `LocalTournamentBackend` (both intentional, covered above), 3 in tests (legacy 6-param ctor → snapshot null, intentional for non-snapshot tests), 1 in `StubTournamentBackend` (stub, irrelevant). No silent drop. |
| Scoring/leaderboard logic untouched (SPEC §5 last bullet) | **CONFIRM-PASS** | `git diff HEAD -- LocalTournamentBackend.cs` shows hunks only in: ctor field+param, Register, SubmitHoleResult ctor call. `GetLeaderboard`, `Countback`, `AssignRanks`, `ResolvePrize`, `CompareProvisional`, `CompareFinal` — zero edits. |
| Adapter throws `KeyNotFoundException` on unknown id (SPEC §4) | **CONFIRM-PASS** | `CharacterManagerStatsProvider.cs:34-36` — explicit throw. `FakeStatsProvider.cs:72-73` — explicit throw. |
| Test §6.1 — captures from provider | **CONFIRM-PASS** | `LocalTournamentBackendTests.cs:1384-1403` — registers expected snap, calls Register, asserts `entry.Snapshot.Equals(expectedSnap)`. |
| Test §6.2 — freeze invariant (THE GATE) | **CONFIRM-PASS — genuine** | `LocalTournamentBackendTests.cs:1408-1430` — Register("before"), backend.Register("t1", …), Register("after"), then reloads via `GetMyEntry("t1")` and asserts snapshot still == before AND != after. Because `CharacterSnapshot` is immutable AND `FakeStatsProvider.Register` REPLACES the dict entry with a different object instance (not a mutation of the same object), the test genuinely proves that the EntryState holds its own reference to the original snapshot. A shallow-reference bug could only false-pass if the fake returned the same instance both times — but the test instantiates two distinct `CharacterSnapshot` objects with different field values. Solid. |
| Test §6.3 — round-trip via InMemoryEntryStore | **CONFIRM-PASS** | `LocalTournamentBackendTests.cs:1434-1448` — Register, then GetMyEntry (which reads `_store.Load`), asserts snapshot equality. |
| Test §6.4 — unknown id throws | **CONFIRM-PASS** | `LocalTournamentBackendTests.cs:1452-1460` — `Assert.Throws<KeyNotFoundException>`. |
| Test count: 158/158 in `Golfin.Tournaments.Tests`, all green | **CONFIRM-PASS** (citation accepted) | Implementer report shows `PassedTests: 158, FailedTests: 0, SkippedTests: 0` plus per-test PASS for all 4 new tests. Not independently re-run; report citation is consistent with the diff scope (no other tests touched). |
| HEARTBEAT iter-1 baseline block present | **CONFIRM-PASS** | `HEARTBEAT.log:3-10` — HEAD SHA `679144e6a` + DIRTY porcelain captured at start. |
| No edits to `Assets/Scripts/Physics/` | **CONFIRM-PASS** | Confirmed in report; consistent with the diff scope. |

## Capture-helper compliance check (Step 5)

N/A — no screenshots produced; this is a headless C# task gated by EditMode tests, not by visual capture. Rule does not apply.

## Bbox verification (Step 6)

N/A — no containment claims (no UI).

## Scene-mutation audit (Step 7)

N/A — no scene edits in this task; diff is .cs files + .cs.meta only. (HEARTBEAT shows `Assets/Scenes/ShellScene.unity` as dirty in the kickoff baseline but that is a pre-existing dirty state predating the task — not introduced by this iter.)

## Production-flow capture check (Step 8)

N/A — no UI/layout deliverable.

---

## Flagged notes for the architect (NOT blockers)

1. **Optional `stats = null` ctor default creates a future production-wiring trap.** SPEC §5 says "production passes `new CharacterManagerStatsProvider()`". The implementer made the param OPTIONAL (defaulting to `null`) for backward compatibility with existing tests. Currently there is **NO production call site** for `new LocalTournamentBackend(` — the UI controllers (`TournamentSelectionScreenController`) are still on static stub data, with comments saying "blocked on T1→T4". So nothing in production silently no-ops the freeze TODAY. But when the production wiring lands (T6 or a UI factory in a later task), the compiler will NOT catch a forgotten `stats` arg and snapshots will silently be `null`. Recommend the architect either (a) require the param in a follow-up (after migrating test ctors to pass `new FakeStatsProvider()` explicitly) or (b) add a runtime guard in `Register` that throws if `_stats == null` AND a non-empty `characterId` is passed — to make the misconfiguration fail loudly. Not a blocker for this task because SPEC §7 only requires "production call sites pass CharacterManagerStatsProvider" and there are none yet.

2. **Minor nullable-annotation inconsistency in `LocalTournamentBackend`.** Field `_stats` is declared **non-nullable** (`private readonly ICharacterStatsProvider _stats;`) but assigned via `_stats = stats!;` (bang-suppress) from a nullable parameter, then accessed via the null-conditional `_stats?.SnapshotFor(…)` (which only makes sense if it can be null). Field type lies about its nullability. Functionally correct, but a follow-up should declare the field as `ICharacterStatsProvider?` to match reality.

3. **Test class placement — minor deviation from SPEC §6.** SPEC said "extend `LocalTournamentBackendTests`"; implementer added a new `[TestFixture] CharacterSnapshotTests` class in the same file (`LocalTournamentBackendTests.cs`) and same assembly. Reasonable — a separate fixture keeps the snapshot tests cohesive instead of grafting them onto a 1100-line class. Mentioned per protocol; not a blocker.

4. **Adapter assembly placement deviation (per implementer report § Spec deviations).** `CharacterManagerStatsProvider` lives in `Assets/Scripts/TournamentsRuntime/` (Assembly-CSharp) rather than under `Golfin.Tournaments.asmdef`. Justified: an asmdef cannot reference Assembly-CSharp, so the production adapter has to live outside the asmdef. This is the standard pattern for production adapters that bridge asmdef code to global singletons (same as `RewardPointsService` adapters etc.). Architect should still note it as a structural decision worth confirming.

---

## Verdict reasoning

Every SPEC §6 acceptance item passes against the actual code. The freeze-invariant test (§6.2 — the gate) is genuine: it relies on `CharacterSnapshot` being immutable and on `FakeStatsProvider.Register` replacing the dict entry with a different object instance, so an EntryState holding a stale reference is the only way the mutated state could leak — and it doesn't, because the assertion compares against both `before` (must equal) and `after` (must NOT equal). Scoring/leaderboard logic is untouched per the diff. Both `EntryState` clone sites preserve `Snapshot`. The adapter throws on unknown id as specified.

The four flagged items above are forward-looking notes, not gate failures: there is no current production call site that the optional-param default silently no-ops, the nullable-annotation inconsistency is functionally correct, the test class placement is a defensible variation, and the asmdef placement deviation is well-justified.

Forwarding to architect.

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
