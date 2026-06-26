# Implementer Report — `tournament_character_snapshot`

**Iteration shape:** headless-seam:clean-start

> SPEC gate: EditMode test suite (`Golfin.Tournaments.Tests`), NOT a screenshot/video. This is a headless C# task (TELLCODE tier). Where the gate template asks for a canonical screenshot, this report states "N/A — headless code task, gated by EditMode tests."

## Implementation summary

Added an immutable `CharacterSnapshot` type (primitives-only, value-equality), an `ICharacterStatsProvider` interface with `FakeStatsProvider` (test fake), and a `CharacterManagerStatsProvider` production adapter (reads `CharacterManager.Instance` in Assembly-CSharp). Modified `EntryState` to carry `CharacterSnapshot? Snapshot` with a new 7-param primary ctor and a legacy 6-param delegating ctor for backward compatibility. Updated `LocalTournamentBackend.Register` to call `_stats?.SnapshotFor(characterId)` and pass the result to `EntryState`, and `SubmitHoleResult` to preserve `snapshot: entry.Snapshot` when cloning the entry. Added 4 EditMode freeze-invariant tests — all 158 tests in the namespace pass.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Tournaments/CharacterSnapshot.cs` | Created — immutable snapshot DTO, primitives-only, value-equality |
| `Assets/Scripts/Tournaments/CharacterSnapshot.cs.meta` | Created — guid `7d0665904f7a4ee29fa6a1602986ca55` |
| `Assets/Scripts/Tournaments/ICharacterStatsProvider.cs` | Created — `ICharacterStatsProvider` interface + `FakeStatsProvider` (test fake, mutable source) |
| `Assets/Scripts/Tournaments/ICharacterStatsProvider.cs.meta` | Created — guid `6af6ddccc3ca4cf6aaa3bf0cdba66ed1` |
| `Assets/Scripts/TournamentsRuntime/CharacterManagerStatsProvider.cs` | Created — production adapter in Assembly-CSharp (not inside Golfin.Tournaments.asmdef) so it can reference `CharacterManager` + `PlayerCharacterData` (both in `Golfin.Roster`, Assembly-CSharp) |
| `Assets/Scripts/TournamentsRuntime/CharacterManagerStatsProvider.cs.meta` | Created — guid `422d9256d8d74a318b1b5e089881b048` |
| `Assets/Scripts/TournamentsRuntime.meta` | Created — folder asset meta, guid `b17cb70edc024078952666cc37b3b733` |
| `Assets/Scripts/Tournaments/EntryState.cs` | Modified — added `CharacterSnapshot? Snapshot` property with XML doc; added 7-param primary ctor; legacy 6-param ctor now delegates to primary with `snapshot: null` |
| `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` | Modified — added `_stats` field; optional `ICharacterStatsProvider? stats = null` param (10th, preserving 9-param backward compat); `Register` calls `_stats?.SnapshotFor`; `SubmitHoleResult` passes `snapshot: entry.Snapshot` |
| `Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs` | Modified — added `CharacterSnapshotTests` fixture (4 new tests: §32.1–§32.4) |
| `Docs/Specs/Active/tournament_character_snapshot/STATUS.md` | Set to `IMPLEMENTER_WORKING` at start |
| `Docs/Specs/Active/tournament_character_snapshot/HEARTBEAT.log` | Created + updated |

## Screenshot

N/A — headless code task, gated by EditMode tests (no Figma node, no UI deliverable, no scene to capture).

Canonical screenshot: N/A — headless code task, gated by EditMode tests.

## Figma fidelity

N/A — no Figma node referenced in SPEC.md (TELLCODE headless task).

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `CharacterSnapshot` type created — immutable, primitives-only, value-equality | PASS | File `Assets/Scripts/Tournaments/CharacterSnapshot.cs` compiled clean; value-equality confirmed by test §32.1 (`Assert.AreEqual(expectedSnap, entry.Snapshot)`) which passed |
| `ICharacterStatsProvider` interface + `FakeStatsProvider` created | PASS | `ICharacterStatsProvider.cs` compiled into `Golfin.Tournaments` asmdef; `FakeStatsProvider.Register/SnapshotFor` used by all 4 new tests, all passed |
| `CharacterManagerStatsProvider` production adapter created | PASS | Moved to `Assets/Scripts/TournamentsRuntime/` (Assembly-CSharp) so it can reference `CharacterManager` + `PlayerCharacterData` from `Golfin.Roster`; compiled clean after adding `using Golfin.Roster;` |
| `EntryState` gains `CharacterSnapshot? Snapshot` field | PASS | 7-param primary ctor added; legacy 6-param ctor delegates to it with `snapshot: null`; `TournamentContractsTests.EntryState_ConstructAndRead` still passes (backward compat confirmed) |
| `LocalTournamentBackend.Register` captures snapshot at sign-up | PASS | `Register` calls `_stats?.SnapshotFor(characterId)` and passes `snapshot:` to `EntryState` ctor; test §32.1 verifies the captured value equals what the provider returned |
| `SubmitHoleResult` preserves snapshot when cloning entry | PASS | `SubmitHoleResult` passes `snapshot: entry.Snapshot` to the new `EntryState`; test §32.3 (store round-trip via GetMyEntry after SubmitHoleResult path through store) confirms the snapshot persists |
| Test §32.1 — captures from provider | PASS | `CharacterSnapshotTests.Register_CapturesSnapshotFromProvider` — PASSED (test run output above) |
| Test §32.2 — freeze invariant | PASS | `CharacterSnapshotTests.Register_SnapshotIsFrozen_MutatingProviderAfterRegister_DoesNotAffectEntry` — PASSED; mutated FakeStatsProvider after Register, reloaded via GetMyEntry, snapshot unchanged |
| Test §32.3 — store round-trip | PASS | `CharacterSnapshotTests.Register_SnapshotSurvivesStoreRoundTrip` — PASSED; snapshot matches after Save/Load through InMemoryEntryStore |
| Test §32.4 — unknown character throws | PASS | `CharacterSnapshotTests.Register_UnknownCharacterId_ThrowsKeyNotFoundException` — PASSED; FakeStatsProvider throws KeyNotFoundException for unregistered ids |
| All pre-existing tests still pass | PASS | 158 tests in `Golfin.Tournaments.Tests` — 0 failed, 0 skipped (tool output: `"Status":"Passed","TotalTests":665,"PassedTests":158,"FailedTests":0`) |
| `.meta` files created for all new `.cs` files | PASS | `CharacterSnapshot.cs.meta`, `ICharacterStatsProvider.cs.meta`, `CharacterManagerStatsProvider.cs.meta` all created; `TournamentsRuntime.meta` folder meta also created |
| No edits to `Assets/Scripts/Physics/` | PASS | `git diff HEAD -- Assets/Scripts/Physics/` produced no output (exit 0, empty diff) |
| `Golfin.Tournaments.asmdef` unchanged | PASS | No modification to `Golfin.Tournaments.asmdef`; `CharacterManagerStatsProvider` intentionally placed outside the asmdef boundary (in Assembly-CSharp) to resolve the asmdef→Assembly-CSharp reference direction |

## Known FAIL items

None.

## Spec deviations

- **`CharacterManagerStatsProvider` location:** SPEC §4 does not specify which assembly to use. The file is placed in `Assets/Scripts/TournamentsRuntime/` (Assembly-CSharp) instead of `Assets/Scripts/Tournaments/` (Golfin.Tournaments.asmdef). Rationale: `Assembly-CSharp` cannot be referenced by an asmdef assembly; placing the adapter inside the Tournaments asmdef produces `CS0103: The name 'CharacterManager' does not exist`. Moving it to Assembly-CSharp (which auto-references all asmdef assemblies) is the standard pattern for production adapters that bridge asmdef types to global singletons. The `ICharacterStatsProvider` interface and `CharacterSnapshot` type remain in `Golfin.Tournaments` as specified.

## Console output (EditMode test run)

```
Status: Passed
TotalTests: 665
PassedTests: 158
FailedTests: 0
SkippedTests: 0
Duration: 00:00:02.688

CharacterSnapshotTests.Register_CapturesSnapshotFromProvider — Passed
CharacterSnapshotTests.Register_SnapshotIsFrozen_MutatingProviderAfterRegister_DoesNotAffectEntry — Passed
CharacterSnapshotTests.Register_SnapshotSurvivesStoreRoundTrip — Passed
CharacterSnapshotTests.Register_UnknownCharacterId_ThrowsKeyNotFoundException — Passed
```

No compile errors related to this task (only pre-existing lightmap/archive .meta GUID warnings, unrelated to this change).

## Open questions for Architect

None.
