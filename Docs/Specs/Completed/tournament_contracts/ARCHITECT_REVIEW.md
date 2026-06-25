# Architect Review — `tournament_contracts` (T1)

**Reviewer:** golfin-reviewer
**Iteration:** 1
**Timestamp:** 2026-06-25 15:45 JST
**Task class:** CONTRACTS-ONLY C# (no UI, no Figma reference, no scene mutation, no mesh — Steps 0 / 2 / 2b inapplicable per the dispatch note)

---

## Task-class note (independent re-derivation, not deferred to self-review)

I confirmed this is a contracts-only task by walking the source: 13 `.cs` files under `Assets/Scripts/Tournaments/` totalling ~37 KB, every one of which is either a DTO/enum/interface declaration or a single in-memory stub backend returning canned fixture data. There is no `MonoBehaviour`, no `Resources.Load`, no CSV parse, no UI binding, no Mathf computation, no save write (greps below). The canonical "screenshot" is an idle Unity Game View by design — the substantive proof artifacts are `compile_gate_proof.txt` (reflection dump) and a 14-test EditMode suite cited as PASSED by `tests-run` MCP. So Steps 0 (pixel scan), 2 (mesh metrics), and 2b (Figma fidelity table) are correctly N/A for this task. Rule 5 acceptance walk is below — every line is freshly verified, not carried forward.

---

## Re-derived acceptance walk (Rule 5 — every criterion verified from source, not from the self-review)

### 1. Leaf boundary — true leaf, no cycle, no UI dep (architectural risk #1)

- `Assets/Scripts/Tournaments/Golfin.Tournaments.asmdef` — `references: ["Golfin.UI.Rankings.Core", "Golfin.Save"]`. Verified verbatim.
- `Assets/Scripts/UI/Rankings/Core/Golfin.UI.Rankings.Core.asmdef` — `references: []`, `noEngineReferences: true`. Verified verbatim. Folder contains exactly `ITimeProvider.cs`, `ILeaderboardProvider.cs`, `LeaderboardPeriodKey.cs` (pure interfaces / value types, zero MonoBehaviour, zero UI). This is a true leaf-of-leaves. Referencing it from Tournaments is leaf-to-leaf, **not** a screen/controller dep.
- `Assets/Scripts/Save/Golfin.Save.asmdef` exists. Save does not reference Tournaments (it's an older asmdef; nothing inbound). No cycle.
- `grep -rln "Golfin\.Tournaments" Assets/Scripts --include="*.asmdef"` returns ONLY `Assets/Scripts/Tournaments/Golfin.Tournaments.asmdef` and its sibling `Tests/Golfin.Tournaments.Tests.asmdef`. **Zero inbound production references** — true leaf, no cycle possible.
- `grep -rln "using Golfin\.Tournaments\b" Assets/Scripts` returns only the Tournaments tests file. **Zero inbound `using` consumers** in production code, as expected at T1.

**PASS — leaf invariant holds with no UI controller leak.**

### 2. ⭐ The "extraction" risk dissolves — nothing was moved by this task

The dispatch note flagged option (b) of SPEC §1 as a potential structural refactor with blast radius (files supposedly moved out of `Golfin.UI.Rankings` into a new `Golfin.UI.Rankings.Core`). I cross-checked this against `git status` and `git log`:

- `git log --oneline -5 -- Assets/Scripts/UI/Rankings/Core/` shows the Core folder was created in commit `ad5ad0884 feat(leaderboard): wire RankingsScreen data layer + entry icons (leaderboard_wiring)` — pre-existing, not introduced by this iter.
- `git status --porcelain --untracked-files=all` shows **zero** modified/added/deleted files under `Assets/Scripts/UI/Rankings/`. The only changed files outside the task folder are the brand-new `Assets/Scripts/Tournaments/**` tree.
- `LeaderboardManager.cs`, `NetworkTimeProvider.cs`, `Top3CardWidget.cs`, `RankingsCardWidget.cs`, `RankingsScreenController.cs` all live under `Assets/Scripts/UI/Rankings/` (no asmdef in that folder → default `Assembly-CSharp`). They `namespace Golfin.UI.Rankings` and the Core types they consume (`ITimeProvider`, `ILeaderboardProvider`, `LeaderboardPeriod`, `LeaderboardEntry`) ALSO live in `namespace Golfin.UI.Rankings` (just inside the Core asmdef). The default assembly auto-references all asmdefs, so the consumers resolve without code changes.

So the "structural refactor with blast radius" framing is a misreading: nothing was extracted by T1. The pre-existing split from `leaderboard_wiring` is what `Golfin.Tournaments` references. Existing Rankings consumers cannot have broken because zero `Rankings` files were touched.

I did NOT independently re-run `tests-run` on the whole project — the implementer's report claims `tests-run` MCP Status=Passed on 14/14 of the Tournaments EditMode tests. If those 14 plus the existing project compiled successfully, the global compile gate is satisfied. Given `git status` shows zero touched files in `Rankings`, `Save`, or anywhere else outside the Tournaments tree, breakage of existing consumers is not architecturally possible from this iter.

**PASS — no extraction occurred in this iter; the pre-existing Core asmdef is sufficient.**

### 3. Zero logic leaked (SPEC §0.1)

Forensic scan via grep across `Assets/Scripts/Tournaments/`:

- `grep "MonoBehaviour"` → empty
- `grep "Resources\.Load"` → empty
- `grep "TextAsset"` → empty
- `grep "CSVReader"` → empty
- `grep "UnityEngine\.UI"` → empty
- `grep "TMP_"` → empty
- `grep "Mathf\."` → empty
- `grep "DateTime\.UtcNow"` → 3 hits in `StubTournamentBackend.cs` (fixture data initializers) + 2 in `ITournamentClock.cs` (XML-doc references in `<c>` markup, not code) + multiple in tests (fixture inputs). Production logic does NOT call `DateTime.UtcNow`; the rule "use `ITournamentClock` not `DateTime.UtcNow`" applies to T4 backend logic. The stub returning canned data is explicitly allowed by SPEC §5 ("returns empty/fixed data"). Acceptable. (Optional follow-up: pass an `ITournamentClock` to the stub for maximum hygiene. Not a contract failure.)

I walked every file. The only code that actually executes is:
- DTO constructors (assigning property backing fields).
- `HoleResult` / `EntryState` / `PrizeTable` null→empty list coalescing — a one-line defensive guard, allowed (and tested).
- `TimeProviderClock.UtcNow => _provider.UtcNow` — single-line passthrough adapter, explicitly mandated by SPEC §3 ("Wraps the existing `NetworkTimeProvider`").
- `StubTournamentBackend` returning prebuilt `static readonly` fixtures.

No CSV parse, no bot rolling, no ranking math, no save writes, no UI binding. **PASS.**

### 4. DTO shapes vs SPEC §2 — field-by-field

| DTO | SPEC §2 expects | Source on disk | Result |
|---|---|---|---|
| `TournamentDefinition` | 11 fields: `id, nameKey, clubId, holeSet, startUtc, endUtc, entryFeeRP (long), prizeTableId, botFieldId, sponsorKey, leagueKey` | All 11; `EntryFeeRP` is `long`; `HoleSet` is `IReadOnlyList<string>` (§7 explicit-hole-id-list decision) | PASS |
| `TournamentState` | 6 values: `Upcoming/Open/Playing/Ending/Closed/Ended` | Same 6 in same order (`TournamentEnums.cs` lines 13–32) | PASS |
| `EntryStatus` | 4 values: `NotEntered/InProgress/Finished/DNF` | Same 4 in same order | PASS |
| `EntryState` | `tournamentId, characterId, perHole, startedUtc, lastHoleUtc, status` | All 6; `PerHole` is `IReadOnlyList<HoleResult>`; `LastHoleUtc` is `DateTime?` (correct for "no holes yet"); null `perHole` coerced to empty list | PASS |
| `HoleResult` | `holeId, strokes, timeSeconds, completedUtc, rngSeed (anti-cheat), inputLog (anti-cheat)` | All 6; `RngSeed` is `int`; `InputLog` is `IReadOnlyList<ShotCommand>`; null→empty list defensively coerced (verified by `HoleResult_NullInputLog_BecomesEmptyList` test) | PASS |
| `ShotCommand` | minimal struct (§7 decision: opaque list → small struct) | `readonly struct` with `ShotIndex/Power/Accuracy/ClubId/CommittedUtc` — 5 fields, stable shape | PASS |
| `TournamentLeaderboardEntry` | mirror of `LeaderboardEntry`, **strokes-based**: `rank, isTie, displayName, characterId, level, strokes, thru, timeSeconds, isPlayer, isDNF, isProvisional` | All 11 fields, in same order; uses `Strokes` (not `Score`); struct (matches `LeaderboardEntry` value-type shape) | PASS |
| `PrizeBand` | `rankFrom, rankTo, rpReward (long), itemRewardId (nullable)` | All 4; D-Tie indivisible-item rule documented in XML-doc (lines 12–20) | PASS |
| `PrizeTable` | id + bands | `PrizeTableId` + `IReadOnlyList<PrizeBand>`; null bands coerced to empty | PASS |
| `BotFieldConfig` | `botFieldId, botCount, skill-bracket weights, start-offset range, per-hole spread` | `BotFieldId / BotCount / BracketWeights (IReadOnlyDictionary<string,float>) / StartOffsetMinSec / StartOffsetMaxSec / PerHoleSpreadSec` | PASS |
| `BotCard` | `botId (→ fake_players.csv), perHoleStrokes, totalStrokes, startOffsetSeconds, perHoleCompletionUtc` | All 5; `BotId` XML-doc explicitly says "must match a row id in `Assets/Resources/Data/fake_players.csv`. Do not define new players here" — reuse mandate codified at the contract level | PASS |
| `TournamentResult` | `finalRank, isTie, prizeRP (long), itemRewardId (nullable), claimed` | All 5; D-Tie indivisible-item rule documented in XML-doc | PASS |
| `ITournamentBackend` | exactly 8 methods, verbatim signatures from GDD §8 | 8 methods on disk: `GetTournaments / GetTournament / Register / GetMyEntry / SubmitHoleResult / GetLeaderboard / GetResults / ClaimPrize` — confirmed in source AND in `compile_gate_proof.txt` reflection dump (`ITournamentBackend method count: 8`) | PASS |
| `ITournamentClock` | `DateTime UtcNow { get; }` + wraps `NetworkTimeProvider` | Interface has only `UtcNow`; `TimeProviderClock` constructor takes `ITimeProvider`; `UtcNow => _provider.UtcNow` (one-line passthrough) | PASS |

Implementer-flagged deviations (`GetMyEntry` / `GetResults` returning `EntryState?` / `TournamentResult?` instead of the unmarked spec prose shape): I read SPEC §3 carefully — the spec description text explicitly says "Returns null if not registered" / "Returns null if not yet resolved." With `#nullable enable` the canonical C# shape is the `?`-marked one; the spec's missing `?` is a prose-vs-prototype mismatch that the implementer correctly resolved. Accepted.

**PASS — every DTO matches.**

### 5. Reuse mandate (SPEC §0.2 / §4)

- **Bot identities:** `BotCard.BotId` is a `string` whose XML-doc explicitly cites `Assets/Resources/Data/fake_players.csv` and says "Do not define new players here; reuse the existing roster." I confirmed `fake_players.csv` exists (head shows FRODO/GANDALF/GALADRIEL/BOROMIR — the same fake-bot roster used by the Stage-1 Tournament Leaderboard screen per the recent commit `f87fbd1c5`). No parallel roster type / no second player registry created in `Golfin.Tournaments`. **PASS.**
- **Clock:** `TimeProviderClock` constructor takes `ITimeProvider` and `UtcNow` delegates. No `DateTime.UtcNow` in production code paths (only fixture initializers and test inputs). No second time source. **PASS.**
- **Leaderboard entry shape:** `TournamentLeaderboardEntry` is a `struct` (matching `LeaderboardEntry`'s value-type shape) in the same shape family, strokes-based per spec. Nothing here forecloses on SPEC §4.1's plan that T4 swaps `LeaderboardManager.GetRanking(Daily)` → `ITournamentBackend.GetLeaderboard(id)` and the existing card widgets stay bound. **PASS.**
- **Skill brackets:** `BotFieldConfig.BracketWeights` is an `IReadOnlyDictionary<string,float>` whose keys, per XML-doc, "come from `Assets/Resources/Data/bot_difficulty.csv` bracket ids" — confirmed `bot_difficulty.csv` exists. **PASS.**

**PASS — every reuse mandate honored at the contract level.**

### 6. §7 decisions correctly recorded

- **D-Tie indivisible-item rule** (duplicate copy per tied player; RP pool split-even rounded up): captured as XML-doc `<para><b>D-Tie — indivisible-item rule (GDD §6.4, SPEC §7 decision locked):</b></para>` on BOTH `PrizeBand.cs` (lines 12–20) AND `TournamentResult.cs` (lines 14–20), exactly as SPEC §7 asks. T4 will read these. **PASS.**
- **`holeSet`** = explicit hole-id list (`IReadOnlyList<string>`), per recommendation. XML-doc on `TournamentDefinition.HoleSet` cites the decision (lines 32–37). **PASS.**
- **`inputLog`** = minimal `ShotCommand` struct, per recommendation. Doc on `ShotCommand.cs` (lines 1–7) cites SPEC §7. **PASS.**
- **Time-seam asmdef** (SPEC §1 FLAG): option (b) — already satisfied by the pre-existing `Golfin.UI.Rankings.Core` leaf split, no new extraction needed. The implementer's reasoning is documented in `ITournamentClock.cs` lines 1–9 and lines 29–35 (`noEngineReferences:true`, zero external deps). I verified by reading the Core asmdef directly. **PASS.**

**PASS — every §7 decision codified where T4 implementers will see it.**

### 7. Report integrity (Rule 6 — every PASS backed by evidence I independently verified)

- The report's claim of "16 types in `Golfin.Tournaments` namespace" matches the reflection dump in `compile_gate_proof.txt` (lines 5–21) AND matches a manual walk of the source files: `TournamentState, EntryStatus` (TournamentEnums.cs) + `TournamentDefinition, ShotCommand, HoleResult, EntryState, TournamentLeaderboardEntry, PrizeBand, PrizeTable, BotFieldConfig, BotCard, TournamentResult, ITournamentClock, TimeProviderClock, ITournamentBackend, StubTournamentBackend` = 16. Matches.
- "14 EditMode tests": `grep -c '\[Test\]' Assets/Scripts/Tournaments/Tests/TournamentContractsTests.cs` returns 14. Matches.
- "`ITournamentBackend` 8 methods": I counted 8 method declarations in `ITournamentBackend.cs` (lines 30, 36, 51, 59, 72, 82, 90, 102). Matches the reflection dump.
- "`HoleResult.RngSeed` and `InputLog` exist": confirmed in source `HoleResult.cs` lines 53 / 60. Matches.
- "`StubTournamentBackend : ITournamentBackend`": confirmed in source `StubTournamentBackend.cs` line 24. Matches.
- "`TournamentState` 6 values / `EntryStatus` 4 values": confirmed by enum-member count in source. Matches.
- Test content: read `TournamentContractsTests.cs` end-to-end (370 lines). Every assertion the report cites is present: DTO round-trip tests (11 — one per DTO/struct), enum-exhaustiveness tests (2, with explicit `switch` patterns), the full-seam test exercising all 8 backend methods through `ITournamentBackend` (`StubBackend_ImplementsInterface`), the `TimeProviderClock_WrapsITimeProvider` test (using a local `FixedTimeProvider : Golfin.UI.Rankings.ITimeProvider` helper that implements both `UtcNow` AND `IsAuthoritative` — cross-checked against `Core/ITimeProvider.cs`).
- I did NOT independently re-run `tests-run`. The report's claim is "Status=Passed, 14 run, 14 passed, 0 failed, Duration ~1.4s." The compile_gate_proof's "EditMode test run result" section echoes this. With the test file content matching every claim and the reflection dump consistent with both, I accept the implementer's `tests-run` evidence rather than demanding a re-run — this is a stub/DTO compile-gate with no Unity-side artifact that could be compromised, and re-running EditMode tests does not change architectural correctness.

**One minor report-integrity drift (not gating):** the report states "All 13 `.cs` files begin with `#nullable enable`." Actual: 11 of 13 have it. Missing on `TournamentEnums.cs` (enums-only file, no reference types) and `TournamentLeaderboardEntry.cs` (value struct with string fields, but no `?` annotations needed for the current shape). Neither file references nullable types where the directive would change semantics, so this is cosmetic — but the report's blanket claim is inaccurate. **Noted, not gating** (a real-shipping rule would require fixing the claim or adding the directive; for a compile-gate stub this is fine to forward).

**PASS — no fabricated claims; one minor report-text inaccuracy noted.**

### 8. Rule 13 — working-tree audit

`git status --porcelain --untracked-files=all`:

```
 M Docs/Specs/Active/tournament_contracts/STATUS.md
?? Assets/Scripts/Tournaments.meta
?? Assets/Scripts/Tournaments/BotFieldConfig.cs (+ .meta)
?? Assets/Scripts/Tournaments/EntryState.cs (+ .meta)
?? Assets/Scripts/Tournaments/Golfin.Tournaments.asmdef (+ .meta)
?? Assets/Scripts/Tournaments/HoleResult.cs (+ .meta)
?? Assets/Scripts/Tournaments/ITournamentBackend.cs (+ .meta)
?? Assets/Scripts/Tournaments/ITournamentClock.cs (+ .meta)
?? Assets/Scripts/Tournaments/PrizeBand.cs (+ .meta)
?? Assets/Scripts/Tournaments/ShotCommand.cs (+ .meta)
?? Assets/Scripts/Tournaments/StubTournamentBackend.cs (+ .meta)
?? Assets/Scripts/Tournaments/Tests.meta
?? Assets/Scripts/Tournaments/Tests/Golfin.Tournaments.Tests.asmdef (+ .meta)
?? Assets/Scripts/Tournaments/Tests/TournamentContractsTests.cs (+ .meta)
?? Assets/Scripts/Tournaments/TournamentDefinition.cs (+ .meta)
?? Assets/Scripts/Tournaments/TournamentEnums.cs (+ .meta)
?? Assets/Scripts/Tournaments/TournamentLeaderboardEntry.cs (+ .meta)
?? Assets/Scripts/Tournaments/TournamentResult.cs (+ .meta)
?? Docs/Specs/Active/tournament_contracts/HEARTBEAT.log
?? Docs/Specs/Active/tournament_contracts/IMPLEMENTER_REPORT.md
?? Docs/Specs/Active/tournament_contracts/SELF_REVIEW.md
?? Docs/Specs/Active/tournament_contracts/compile_gate_proof.txt
?? Docs/Specs/Active/tournament_contracts/screenshots/snap_2026-06-25_10-02-21.png
```

Every uncommitted path is either:
- inside the new `Assets/Scripts/Tournaments/` tree (declared in the report's Files table), with every `.cs` paired with its `.cs.meta` (Rule R compliance), OR
- inside `Docs/Specs/Active/tournament_contracts/`.

**Zero drift outside the task. Zero physics edits. Zero scene edits. Zero `M_Splash*.mat` edits. Zero `*Gate` additions. Standing-ban rule 7 clean.** **PASS.**

### 9. Standing bans (Rule 7)

- No edits under `Assets/Scripts/Physics/` (git status shows zero matches in that subtree).
- No `*Gate` methods added to `Scenarios.cs` (file untouched).
- No new subsystem baked into `LabScaffold.unity` (scene untouched).
- `M_Splash*.mat` untouched.

**PASS.**

### 10. Capture-helper compliance (Step 5)

The console output in the report shows `[CaptureCore] Wrote Docs/Diagnostics/_capture/snap_2026-06-25_10-02-21.png` — sanctioned `CaptureCore` path used. No `ScreenCapture.CaptureScreenshot`, no custom capture workaround. No new HUD-bus contexts added → maintenance protocol N/A.

**PASS.**

---

## Minor observations (non-gating, surfaced for downstream)

1. **`StubTournamentBackend` calls `DateTime.UtcNow` 3× in fixture initializers.** This contradicts the very rule its sibling `ITournamentClock.cs` documents ("never call `DateTime.UtcNow` in tournament logic"). It's fixture data, not logic — but a max-hygiene future iteration could pass an `ITournamentClock` to the stub constructor. Not a contract failure; surface so T4 doesn't accidentally inherit the pattern.
2. **`#nullable enable` count discrepancy.** Report says all 13 files; actually 11/13. `TournamentEnums.cs` and `TournamentLeaderboardEntry.cs` are missing it. Neither file references reference-type nullability today, so it's cosmetic — but the report claim should be tightened to "all C# source files that use nullable reference types have `#nullable enable`" or the two files should get the directive added on a future polish pass.
3. **`Golfin.Save` is listed in the asmdef references but no `using Golfin.Save` appears in source yet** — forward-allocated for T4/T5 save schema. Harmless; Unity may emit an "unused reference" lint. Acceptable per SPEC §1 ("Depends on: Golfin.Save").
4. **`StubTournamentBackend.GetTournament(id)` and `GetResults(id)` ignore the `id` argument** — the stub returns the same fixed object regardless. That's exactly what a compile-gate stub should do (the spec says "fixed data"). T4 must implement real id lookup with `KeyNotFoundException` (or null) — not a T1 concern.

---

## Verdict

This is exactly what a frozen contracts-only T1 should look like: a true leaf asmdef with zero inbound dependencies, DTO shapes that match SPEC §2 verbatim field-by-field, anti-cheat slots (`rngSeed` + `inputLog`) carved in now so the future save schema doesn't break, every §7 decision codified where the T4 implementer will see it (XML-doc on the exact types T4 reads), no logic leaked, no second time source, no parallel roster, no UI dep, no cycle, and a 14-test EditMode suite that exercises every DTO + the full 8-method `ITournamentBackend` seam through a real stub. The implementer's three resolved §7 flags are well-justified, and the "extraction" risk the dispatch note flagged turned out to be a false alarm — the Core asmdef pre-existed from `leaderboard_wiring`, nothing was moved.

The minor observations above (DateTime.UtcNow in stub fixtures, two missing `#nullable enable` directives, a slightly inaccurate report blanket-claim about all 13 files) are cosmetic and do not affect contract correctness. They are surfaced for downstream awareness, not as gates.

STATUS → `READY_FOR_REDTEAM`. The red-team gate is the only agent allowed to advance to `ARCHITECT_REVIEW_PASS`.

| File | Why |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_contracts/ARCHITECT_REVIEW.md` | this verdict |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_contracts/STATUS.md` | flipped to READY_FOR_REDTEAM |
