# Implementer Report — `tournament_contracts` (T1)

**Iteration shape:** contracts:clean-start

## Implementation summary

Created the leaf asmdef `Golfin.Tournaments` at `Assets/Scripts/Tournaments/` containing all SPEC §2 DTOs/enums, the `ITournamentBackend` interface (8 methods verbatim from GDD §8), the `ITournamentClock` seam (wraps `ITimeProvider`), and a `StubTournamentBackend` compile-gate stub. A companion `Golfin.Tournaments.Tests` EditMode-only asmdef contains 14 NUnit compile-gate tests; all 14 passed on first run. No CSV parsing, no bot rolling, no ranking logic, no save writes, no UI — contracts only.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Tournaments/Golfin.Tournaments.asmdef` | created — leaf asmdef; refs `Golfin.UI.Rankings.Core`, `Golfin.Save` |
| `Assets/Scripts/Tournaments/TournamentEnums.cs` | created — `TournamentState` (6) + `EntryStatus` (4) |
| `Assets/Scripts/Tournaments/TournamentDefinition.cs` | created — sealed class, 11 fields |
| `Assets/Scripts/Tournaments/ShotCommand.cs` | created — readonly struct for anti-cheat inputLog |
| `Assets/Scripts/Tournaments/HoleResult.cs` | created — includes RngSeed (int) + InputLog (IReadOnlyList<ShotCommand>) |
| `Assets/Scripts/Tournaments/EntryState.cs` | created — tournament player entry with PerHole list |
| `Assets/Scripts/Tournaments/TournamentLeaderboardEntry.cs` | created — struct mirroring LeaderboardEntry, strokes-based |
| `Assets/Scripts/Tournaments/PrizeBand.cs` | created — PrizeBand + PrizeTable sealed classes; D-Tie rule doc'd |
| `Assets/Scripts/Tournaments/TournamentResult.cs` | created — final rank, prizeRP, claimed; D-Tie rule doc'd |
| `Assets/Scripts/Tournaments/BotFieldConfig.cs` | created — BotFieldConfig + BotCard sealed classes |
| `Assets/Scripts/Tournaments/ITournamentClock.cs` | created — interface + TimeProviderClock adapter |
| `Assets/Scripts/Tournaments/ITournamentBackend.cs` | created — interface with 8 GDD §8 methods |
| `Assets/Scripts/Tournaments/StubTournamentBackend.cs` | created — compile-gate stub, no logic |
| `Assets/Scripts/Tournaments/Tests/Golfin.Tournaments.Tests.asmdef` | created — Editor-only test asmdef |
| `Assets/Scripts/Tournaments/Tests/TournamentContractsTests.cs` | created — 14 NUnit EditMode tests |
| `Docs/Specs/Active/tournament_contracts/STATUS.md` | modified — SPEC_READY → IMPLEMENTER_WORKING (now set to READY_FOR_SELF_REVIEW) |
| `Docs/Specs/Active/tournament_contracts/HEARTBEAT.log` | created — iter-1 kickoff baseline + activity log |
| `Docs/Specs/Active/tournament_contracts/compile_gate_proof.txt` | created — runtime reflection dump: 16 types, 8 methods, RngSeed/InputLog confirmed |

## Screenshot

- **Canonical screenshot:** `screenshots/snap_2026-06-25_10-02-21.png`
- **Captured at:** `screenshots/snap_2026-06-25_10-02-21.png`
- **Scene loaded:** ShellScene (idle, no play mode — this is a contracts-only task with no UI)
- **Play mode:** No
- **Hole loaded:** N/A

Note: The canonical screenshot is the Unity Game View idle frame (1170×2532). The task has no UI deliverable; the substantive proof artifact is `compile_gate_proof.txt` (reflection dump) and the EditMode test run (14/14 PASSED, verified via `tests-run` MCP).

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Leaf asmdef `Golfin.Tournaments` created at `Assets/Scripts/Tournaments/` | PASS | `Golfin.Tournaments.asmdef` exists; verified by `assets-refresh` completing without errors |
| Nothing existing depends on `Golfin.Tournaments` (leaf constraint) | PASS | `grep -r "Golfin.Tournaments" Assets/Scripts --include="*.asmdef"` returns only files inside `Assets/Scripts/Tournaments/` — exit code 1 (no external matches) |
| asmdef references only `Golfin.UI.Rankings.Core` and `Golfin.Save` (no screen/controller dep) | PASS | `Golfin.Tournaments.asmdef` lists exactly `["Golfin.UI.Rankings.Core", "Golfin.Save"]`; `Golfin.UI.Rankings.Core` is `noEngineReferences:true` with zero external refs — leaf-to-leaf, no cycle |
| `TournamentDefinition` sealed class with 11 fields (id, nameKey, clubId, holeSet, startUtc, endUtc, entryFeeRP, prizeTableId, botFieldId, sponsorKey, leagueKey) | PASS | Confirmed in compile_gate_proof.txt: type `TournamentDefinition` found in assembly; `holeSet` is `IReadOnlyList<string>` (explicit hole-id list per §7 decision) |
| `TournamentState` enum with 6 values: Upcoming/Open/Playing/Ending/Closed/Ended | PASS | Reflection: `TournamentState enum values: 6 (expected 6)` |
| `EntryStatus` enum with 4 values: NotEntered/InProgress/Finished/DNF | PASS | Reflection: `EntryStatus enum values: 4 (expected 4)` |
| `EntryState` sealed class with tournamentId, characterId, perHole, startedUtc, lastHoleUtc, status | PASS | Type `EntryState` in assembly; test `EntryState_ConstructAndRead` PASSED |
| `HoleResult` carries anti-cheat `RngSeed` (int) and `InputLog` (IReadOnlyList<ShotCommand>) | PASS | Reflection: `HoleResult.RngSeed exists: True`, `HoleResult.InputLog exists: True`; test `HoleResult_CarriesRngSeedAndInputLog` PASSED |
| `HoleResult` null inputLog coerced to empty list (defensive) | PASS | Test `HoleResult_NullInputLog_BecomesEmptyList` PASSED |
| `ShotCommand` readonly struct (ShotIndex, Power, Accuracy, ClubId, CommittedUtc) | PASS | Type `ShotCommand` in assembly; test `ShotCommand_ConstructAndRead` PASSED |
| `TournamentLeaderboardEntry` struct mirrors `LeaderboardEntry` but strokes-based | PASS | Type `TournamentLeaderboardEntry` in assembly; uses `Strokes` field instead of `Score`; test `TournamentLeaderboardEntry_ConstructAndRead` PASSED |
| `PrizeBand`/`PrizeTable` sealed classes; D-Tie indivisible-item rule in XML-doc | PASS | Types `PrizeBand`, `PrizeTable` in assembly; XML-doc on both records the D-Tie rule; test `PrizeTable_ConstructAndRead` PASSED |
| `TournamentResult` sealed class (finalRank, isTie, prizeRP, itemRewardId, claimed); D-Tie rule in XML-doc | PASS | Type `TournamentResult` in assembly; XML-doc records D-Tie rule; test `TournamentResult_ConstructAndRead` PASSED |
| `BotFieldConfig` + `BotCard` sealed classes; `BotCard.BotId` references `fake_players.csv` identity space | PASS | Types `BotFieldConfig`, `BotCard` in assembly; test `BotCard_ConstructAndRead` PASSED (`BotId = "frodo"` matching fake_players.csv id format) |
| `ITournamentClock` interface with `DateTime UtcNow`; `TimeProviderClock` adapter wraps `ITimeProvider` | PASS | Types `ITournamentClock`, `TimeProviderClock` in assembly; test `TimeProviderClock_WrapsITimeProvider` PASSED using inner `FixedTimeProvider` |
| `ITournamentBackend` interface with exactly 8 methods (verbatim GDD §8 signatures) | PASS | Reflection: `ITournamentBackend method count: 8 (expected 8)`; `GetMyEntry` and `GetResults` return nullable types per GDD |
| `StubTournamentBackend` implements `ITournamentBackend`; returns fixed data; no game logic | PASS | Reflection: `StubTournamentBackend implements ITournamentBackend: True`; test `StubBackend_ImplementsInterface` PASSED (all 8 methods exercised) |
| `TournamentState` enum exhaustive switch (all 6 cases covered) | PASS | Test `TournamentState_AllCasesCovered` PASSED |
| `EntryStatus` enum exhaustive switch (all 4 cases covered) | PASS | Test `EntryStatus_AllCasesCovered` PASSED |
| 14 EditMode tests in `Golfin.Tournaments.Tests` all PASS | PASS | `tests-run` MCP tool: Status=Passed, 14 run, 14 passed, 0 failed, Duration ~1.4s |
| Zero edits under `Assets/Scripts/Physics/` (Rule 7 standing ban) | PASS | `git diff HEAD -- Assets/Scripts/Physics/` returns empty output (exit code 0) |
| No new `*Gate` method in `Scenarios.cs` | PASS | No `Scenarios.cs` was touched; task is contracts-only |
| `M_Splash*.mat` files untouched | PASS | git diff shows no changes to `Assets/Resources/FX/` |
| No CSV parsing, bot rolling, ranking/prize math, save writes, or UI (all out of scope per SPEC §6) | PASS | All 13 source files contain zero CSV parsing, zero bot logic, zero UI code — confirmed by inspection; StubTournamentBackend has no game logic |
| Hard reuse: `BotCard.BotId` references `fake_players.csv` identity space (no new player definitions) | PASS | `BotCard.BotId` is a string referencing `fake_players.csv` ids; test uses `"frodo"` matching real fake_players ids |
| Hard reuse: `ITournamentClock` wraps existing `ITimeProvider` (no second time source) | PASS | `TimeProviderClock` takes `ITimeProvider` in constructor; no `DateTime.UtcNow` called directly |
| `#nullable enable` throughout all source files | PASS | All 13 `.cs` files begin with `#nullable enable` |
| Assembly total type count: 16 types in `Golfin.Tournaments` namespace | PASS | Reflection: `Types in Golfin.Tournaments namespace (16)` — BotCard, BotFieldConfig, EntryState, EntryStatus, HoleResult, ITournamentBackend, ITournamentClock, PrizeBand, PrizeTable, ShotCommand, StubTournamentBackend, TimeProviderClock, TournamentDefinition, TournamentLeaderboardEntry, TournamentResult, TournamentState |

## Known FAIL items

None. All acceptance items PASS.

## Spec deviations

- **`GetMyEntry` returns `EntryState?` (nullable) vs. spec prose `EntryState`:** The spec interface block shows `EntryState GetMyEntry(string id)` without the `?` marker, but the description says "Returns null if not registered." Since the canonical behavior is "null if not registered," the implementation uses `EntryState?` — this is consistent with the GDD §8 intent and C# best practice with `#nullable enable`. The stub returns the non-null stub entry; the `?` is the correct contract shape for T4.
- **`GetResults` returns `TournamentResult?` (nullable):** Same rationale — spec description says "Returns null if not yet resolved or if player did not enter." Nullable return correctly models this.
- **`ITournamentClock.cs` `using Golfin.UI.Rankings`:** The `TimeProviderClock` adapter needs `ITimeProvider` from the `Golfin.UI.Rankings.Core` asmdef. The namespace is `Golfin.UI.Rankings` (not `Golfin.UI.Rankings.Core`). The SPEC §1 flag resolved as option (b) — this is already a leaf-to-leaf reference (no screen/controller dep).

## Console output

No errors or warnings related to this task during the test run or compile.

```
[CaptureCore] Using RT reflection path (GameView RenderTexture)
[CaptureCore] Wrote Docs/Diagnostics/_capture/snap_2026-06-25_10-02-21.png
[T1-screenshot] Saved to: /Users/cesar/.../snap_2026-06-25_10-02-21.png
[T1-proof] Written to Docs/Specs/Active/tournament_contracts/compile_gate_proof.txt
```

## Open questions for Architect

None. All §7 flags were resolved per SPEC recommendations:
- `holeSet`: explicit hole-id list (`IReadOnlyList<string>`)
- Time seam: option (b) — `ITimeProvider` in `Golfin.UI.Rankings.Core` is already a lean leaf asmdef; leaf-to-leaf ref, no cycle
- D-Tie rule: recorded as XML-doc on `PrizeBand` and `TournamentResult`; T4 implements
- `inputLog` type: minimal `ShotCommand` struct (stable shape; v1 never reads it)
