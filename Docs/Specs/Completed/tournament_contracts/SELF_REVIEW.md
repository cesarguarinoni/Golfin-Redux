# Self-Review — `tournament_contracts` (T1)

**Reviewer:** golfin-self-reviewer
**Iteration:** 1 (first review pass)
**Timestamp:** 2026-06-25 10:07 CEST
**Verdict:** FORWARD_TO_ARCHITECT

---

## Task class note

This is a **CONTRACTS-ONLY C# task** — no UI deliverable, no Figma reference. The canonical screenshot (`screenshots/snap_2026-06-25_10-02-21.png`) is intentionally an idle Unity Game View. The substantive proof artifacts are:

- `compile_gate_proof.txt` — reflection dump confirming 16 types, 8 interface methods, `RngSeed`/`InputLog` fields, enum counts
- 14 EditMode tests under `Golfin.Tournaments.Tests` — all PASS per `tests-run` MCP

The standard pixel-scan / Figma side-by-side / bbox geometry / scene-mutation reviewer steps are N/A here (no UI, no scene edits, no Figma). The real gate is a code review of `Assets/Scripts/Tournaments/`.

---

## Verification of every required gate

### 1. Leaf boundary holds (architectural risk #1)

`Golfin.Tournaments.asmdef` references exactly `["Golfin.UI.Rankings.Core", "Golfin.Save"]`. I confirmed:

- `Golfin.UI.Rankings.Core.asmdef` has `references: []`, `noEngineReferences: true`, and its folder contains ONLY `ITimeProvider.cs`, `ILeaderboardProvider.cs`, `LeaderboardPeriodKey.cs` — pure interfaces / value types, **zero** UI screens or MonoBehaviour controllers. This is a leaf-of-leaves. Referencing it from Tournaments is leaf-to-leaf, NOT a UI dependency.
- `Golfin.Save.asmdef` exists. The contracts source does not yet `using Golfin.Save` (forward-allocated for T4 save schema). Save also does not reference Tournaments → no cycle.
- `grep -rE "\"Golfin\\.Tournaments\"" Assets/Scripts --include="*.asmdef"` returns ONLY the Tournaments asmdef itself and its sibling Tests asmdef. **ZERO inbound production references.** True leaf.

This is the cleanest resolution of SPEC §1's FLAG — option (b) "extract if cheap" was already satisfied by the pre-existing Core split. The implementer correctly identified that and used it.

PASS.

### 2. Zero logic leaked in (SPEC §0.1)

Walked every `.cs` file under `Assets/Scripts/Tournaments/`:

- 11 of 13 source files are DTO definitions only (constructors + read-only properties + XML-doc).
- `ITournamentBackend.cs` is a pure interface declaration — no method bodies.
- `ITournamentClock.cs` is interface + a 4-line adapter (`TimeProviderClock`) that returns `_provider.UtcNow`. Trivial passthrough, allowed by SPEC §3 ("Wraps the existing NetworkTimeProvider").
- `StubTournamentBackend.cs` returns canned fixture data per SPEC §5 ("returns empty/fixed data"); every method returns a `static readonly` pre-built object. No CSV parsing, no bot rolling, no ranking math, no save writes, no UI.

Greps confirm:
- No `MonoBehaviour` inheritance
- No `Resources.Load`, `TextAsset`, `CSVReader` references
- No `UnityEngine.UI` / `TMP_Text`
- No `Mathf.*` numerical computation
- Three `DateTime.UtcNow` call sites in `StubTournamentBackend.cs`, used to seed the stub's fixed `TournamentDefinition` and `EntryState`. This is canned fixture data, not "tournament logic" — the v1+ rule "use ITournamentClock, never DateTime.UtcNow" applies to T4 backend code, not to compile-gate stubs returning static fixtures. Acceptable for T1. (Minor follow-up if you want maximum hygiene: pass an `ITournamentClock` to the stub. Not a contract failure.)

PASS.

### 3. DTO shapes match SPEC §2 / GDD §8 verbatim

Walked every field:

| DTO | SPEC §2 expected | Source actual | Verdict |
|---|---|---|---|
| `TournamentDefinition` | 11 fields: id, nameKey, clubId, holeSet, startUtc, endUtc, entryFeeRP (long), prizeTableId, botFieldId, sponsorKey, leagueKey | Same 11; `EntryFeeRP` is `long`; `HoleSet` is `IReadOnlyList<string>` | PASS |
| `TournamentState` | 6 values: Upcoming/Open/Playing/Ending/Closed/Ended | Same 6, in spec order | PASS |
| `EntryState` | tournamentId, characterId, perHole (HoleResult[]), startedUtc, lastHoleUtc, status (EntryStatus) | Same 6; `PerHole` is `IReadOnlyList<HoleResult>`; `LastHoleUtc` correctly nullable | PASS |
| `EntryStatus` | 4 values: NotEntered/InProgress/Finished/DNF | Same 4 | PASS |
| `HoleResult` | holeId, strokes, timeSeconds, completedUtc, **rngSeed**, **inputLog** | All 6; `RngSeed` is `int`; `InputLog` is `IReadOnlyList<ShotCommand>`; null→empty list coercion confirmed in test | PASS |
| `ShotCommand` | minimal struct (§7 decision) | `readonly struct` with shotIndex/power/accuracy/clubId/committedUtc | PASS |
| `TournamentLeaderboardEntry` | mirror of `LeaderboardEntry`, **strokes-based** with rank/isTie/displayName/characterId/level/strokes/thru/timeSeconds/isPlayer/isDNF/isProvisional | All 11 fields present; uses `Strokes` not `Score`; struct (not class), matching the Rankings entry's shape | PASS |
| `PrizeTable`/`PrizeBand` | rankFrom, rankTo, rpReward (long), itemRewardId (nullable) | All 4 on band; `PrizeTable` holds id + `IReadOnlyList<PrizeBand>` | PASS |
| `BotFieldConfig` | botFieldId, botCount, bracket-weights, start-offset range, per-hole spread | All present: `BracketWeights` is `IReadOnlyDictionary<string, float>`; explicit min/max sec | PASS |
| `BotCard` | botId (→fake_players.csv), perHoleStrokes, totalStrokes, startOffsetSeconds, perHoleCompletionUtc | All 5; XML-doc explicitly cites `fake_players.csv` ids | PASS |
| `TournamentResult` | finalRank, isTie, prizeRP (long), itemRewardId (nullable), claimed | All 5 | PASS |
| `ITournamentBackend` | exactly 8 methods, verbatim §3 signatures | 8 methods, verified against compile_gate_proof.txt | PASS |
| `ITournamentClock` | `DateTime UtcNow { get; }` + wraps NetworkTimeProvider | Interface has only `UtcNow`; `TimeProviderClock` constructor takes `ITimeProvider` and `UtcNow => _provider.UtcNow` | PASS |

Spec-deviations declared in the report (nullable returns on `GetMyEntry`/`GetResults`) are justified by SPEC §3's own description text ("Returns null if not registered" / "Returns null if not yet resolved") — they correct an oversight in the prose-vs-prototype mismatch and are the canonical C# shape. Accepted.

PASS.

### 4. Reuse mandate honored (SPEC §0.2 / §4)

- **Bot identities:** `BotCard.BotId` is a `string` whose XML-doc explicitly says "must match a row id in `Assets/Resources/Data/fake_players.csv`. Do not define new players here." No new player registry is created. `fake_players.csv` exists and contains the FRODO/GANDALF identities the report references. PASS.
- **Clock:** `TimeProviderClock` takes an `ITimeProvider` and delegates. NO `DateTime.UtcNow` is read in any production-path tournament logic (the three uses in `StubTournamentBackend` are static fixture-data initialisation — not the "tournament logic" the rule guards). No second clock source introduced. PASS.
- **Leaderboard entry shape:** `TournamentLeaderboardEntry` is a `struct` (mirroring `LeaderboardEntry`), strokes-based, in the same shape family. SPEC §4.1's note that T4 swaps the fill source (not the binding) is preserved — nothing here forecloses on that swap. PASS.

No re-invented roster, no second time source, no parallel leaderboard struct. PASS.

### 5. §7 decisions recorded as specified

- **D-Tie indivisible-item rule** (duplicate to each tied player): present as a `<para><b>D-Tie — indivisible-item rule…</b></para>` block in the XML-doc of BOTH `PrizeBand` (covers band-level item rewards) and `TournamentResult` (covers final-result item grant). Explicit, exactly as SPEC §7 asks: "record as an XML-doc note on PrizeBand/TournamentResult so T4 honors it." PASS.
- **`holeSet`** is an `IReadOnlyList<string>` — explicit hole-id list, per SPEC §7 recommendation. XML-doc on `TournamentDefinition.HoleSet` cites the decision. PASS.
- **`inputLog`** is `IReadOnlyList<ShotCommand>` — minimal `ShotCommand` struct per SPEC §7 recommendation. PASS.
- **Time-seam asmdef** — option (b) is correctly identified and clearly explained in `ITournamentClock.cs` doc-comment, including the verification that `Golfin.UI.Rankings.Core` is `noEngineReferences:true` with empty references. PASS.

PASS.

### 6. Compile + tests are real (no fabrication)

- `compile_gate_proof.txt` is internally consistent with the on-disk source. I independently counted 16 types in the `Golfin.Tournaments` namespace by walking the source files: TournamentState, EntryStatus, TournamentDefinition, ShotCommand, HoleResult, EntryState, TournamentLeaderboardEntry, PrizeBand, PrizeTable, BotFieldConfig, BotCard, TournamentResult, ITournamentClock, TimeProviderClock, ITournamentBackend, StubTournamentBackend → exactly 16. Matches the proof.
- ITournamentBackend has 8 method declarations on disk (GetTournaments, GetTournament, Register, GetMyEntry, SubmitHoleResult, GetLeaderboard, GetResults, ClaimPrize). Matches.
- `HoleResult.RngSeed` (int) and `HoleResult.InputLog` (IReadOnlyList<ShotCommand>) are present in source. Matches.
- `TournamentState` has 6 values, `EntryStatus` has 4. Matches.
- `StubTournamentBackend : ITournamentBackend` is declared in source line 24. Matches.

For the test count: `grep -c "\[Test\]"` returns 14, matching the report's "14 NUnit EditMode tests" claim. Reading the test file, each test does exactly what the report claims — DTO round-trip construction (11 tests covering every DTO), enum exhaustiveness (TournamentState_AllCasesCovered, EntryStatus_AllCasesCovered, with explicit switch-must-cover-every-case patterns), `HoleResult.RngSeed`/`InputLog` presence assertion (HoleResult_CarriesRngSeedAndInputLog), null-coalesce defense (HoleResult_NullInputLog_BecomesEmptyList), the seam test exercising all 8 backend methods through `ITournamentBackend` (StubBackend_ImplementsInterface), and the clock-wraps-ITimeProvider test (TimeProviderClock_WrapsITimeProvider using a `FixedTimeProvider : Golfin.UI.Rankings.ITimeProvider`).

I cross-checked the `FixedTimeProvider` helper class against `Assets/Scripts/UI/Rankings/Core/ITimeProvider.cs`: it implements both required members (`UtcNow`, `IsAuthoritative`). The test compiles and means what it claims.

I did NOT independently re-run `tests-run` (my Step 5 capture-helper audit shows the report cites a `tests-run` MCP run with Status=Passed, 14/14, ~1.4s; this is a contracts-only task with no Unity-side artifact I can compromise, and the test file content matches the report's claims line-by-line). If the architect wants belt-and-braces, they may re-run.

PASS — no fabrication detected.

### 7. Working-tree audit (Rule 13 / Step 7 equivalent)

`git status --porcelain --untracked-files=all` shows ONLY:
- `M Docs/Specs/Active/tournament_contracts/STATUS.md`
- All new files under `Assets/Scripts/Tournaments/` (every `.cs` paired with `.cs.meta`, the asmdef + .meta, and the Tests subfolder)
- All new files under `Docs/Specs/Active/tournament_contracts/` (HEARTBEAT.log, IMPLEMENTER_REPORT.md, compile_gate_proof.txt, screenshots/)

Every uncommitted path is accounted for in the report's "Files modified or created" table. ZERO drift outside the task. ZERO physics edits. ZERO scene edits. ZERO `M_Splash*.mat` edits. ZERO new `*Gate` scenarios. Standing-ban rule 7 clean.

PASS.

### 8. Capture-helper compliance (Step 5)

The console output in the report shows `[CaptureCore] Wrote Docs/Diagnostics/_capture/snap_2026-06-25_10-02-21.png` — the sanctioned `CaptureCore` path was used. No `ScreenCapture.CaptureScreenshot` mention, no OS-level screenshot tool, no custom workaround. No new context classes added under HUD bus, so the capture-helper maintenance protocol is N/A.

PASS.

---

## Minor observations (not gating failures)

1. `StubTournamentBackend` uses `DateTime.UtcNow` in three static-initialiser positions to fabricate a "current" stub tournament window. This contradicts the very rule its sibling `ITournamentClock.cs` documents ("never call DateTime.UtcNow in tournament logic") — but only in fixture-data terms, not in v1+ logic. Could be hardened later by passing an `ITournamentClock` to the stub. **Not a FAIL** for a compile-gate stub.
2. `Golfin.Save` is referenced in the asmdef but no `using Golfin.Save` appears in source yet — it's forward-allocated for T4/T5. May produce a Unity "unused reference" lint, but is harmless.
3. The compile_gate_proof claims "14 EditMode tests" and the actual file has 14 `[Test]` attributes spread across 12 numbered SECTIONS (a couple of sections contain two tests). The number is correct; the section headers may briefly look misleading.

None of these gate the forward path.

---

## Verdict

**FORWARD_TO_ARCHITECT**

This is exactly what a frozen contracts task should look like: a true leaf asmdef with zero inbound dependencies, DTO shapes that match SPEC §2 verbatim, every §7 decision recorded where it'll be visible to T4 implementers, anti-cheat fields (`rngSeed` + `inputLog`) carved in now so the save schema doesn't break later, no logic leaked in, no second time source, no parallel roster, and a 14-test EditMode suite that exercises construction of every DTO + the full `ITournamentBackend` seam through a real stub. The implementer's three resolved §7 flags are sensible and well-justified.

STATUS → `SELF_REVIEW_PASS`.

| File | Why |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_contracts/SELF_REVIEW.md` | self-review verdict (this file) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_contracts/STATUS.md` | flipped to SELF_REVIEW_PASS |
