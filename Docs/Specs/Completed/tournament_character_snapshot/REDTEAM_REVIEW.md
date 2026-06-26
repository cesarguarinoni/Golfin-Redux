# Red-Team Review — `tournament_character_snapshot`

**Iteration:** 1
**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-06-26 14:55 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS**

> TELLCODE-tier headless C# task. No Figma / screenshot / video / mesh-metrics apply.
> Gate = EditMode suite (`Golfin.Tournaments.Tests`) + freeze-semantics correctness +
> snapshot shape for T5. I attacked the code and the freeze; I could not break it.

---

## Captured angle (the equivalent of "harshest angle" for a headless task)

There is no visual to re-shoot. The equivalent adversarial angle is the freeze
semantics and the snapshot shape — both re-derived below from the source I read
myself, not from prior verdicts.

## Tests re-run myself (Unity MCP CLI `tests-run`, EditMode)

Ran the full EditMode suite via `npx unity-mcp-cli run-tool tests-run` **three times**.
The aggregate `PassedTests` counter is unreliable across runs in this MCP build
(returned 25 / 502 on partial snapshots), and the `Results` array comes back empty
(per-test names not populated — same limitation the reviewer hit). The **load-bearing
fields are stable and green on every run**:

```
Status:       Passed
TotalTests:   665
FailedTests:  0
SkippedTests: 0
```

A failing snapshot test would flip `Status` to `Failed` and bump `FailedTests`; it
does not. The implementer's "158 passed in Golfin.Tournaments.Tests / 0 fail" and the
reviewer's "662 pass / 3 skip" are both consistent with my runs (665 total, 0 fail).
No fabrication: the 4 new test method names in the reports exactly match the source at
`LocalTournamentBackendTests.cs:1382-1460`.

## Freeze semantics — re-derived, not trusted (the gate)

- **`CharacterSnapshot` is TRULY immutable.** `CharacterSnapshot.cs:26` — `sealed class`;
  all six props are `{ get; }` only (lines 29-47); ctor-only assignment (49-66); ctor
  validates `characterId` non-empty; **no setters, no mutating method, no exposed mutable
  collection**, no `UnityEngine`. A caller cannot mutate a captured snapshot post-Register.
- **The freeze is eager and singular.** `SnapshotFor` is called **exactly once**, at
  `LocalTournamentBackend.cs:134` inside `Register`, before the `EntryState` is built
  (line 136-143). `GetMyEntry` (`:154`) and `SubmitHoleResult` (`:162`) **never** touch
  `_stats`/`SnapshotFor` (grep-confirmed: only refs are field decl :36, assign :84, call :134).
  So there is no "live object" to leak — the snapshot is copied into the immutable
  EntryState at sign-up and read back by reference forever after.
- **Adversarial probe — would §6.2 catch a broken freeze?** YES. The test
  (`:1408-1430`) registers a distinct `before` snapshot, calls `backend.Register`, then
  REPLACES the fake's dict entry with a second distinct `after` instance, reloads via
  `GetMyEntry`, and asserts `before == reloaded.Snapshot` **AND** `after != reloaded.Snapshot`.
  The only way a "stored a live reference / re-snapshots on read" bug could false-pass is
  if the fake returned the same instance twice — it does not (two distinct allocations).
  The two-pronged (must-equal + must-not-equal) assertion defeats the trivial false-pass.
  **This is a genuine gate, not a hollow one.**

## Snapshot SHAPE for T5 — re-verified field-by-field

SPEC §3 demands: CharacterId + Level + Strength + ClubControl + Recovery + Stamina-the-STAT,
primitives only, no UnityEngine, no rarity. Shipped `CharacterSnapshot` matches **exactly**
(6 fields, all `int`/`string`, `System`-only). Adapter `CharacterManagerStatsProvider.cs:38-44`
reads `currentLevel` + `currentStrength`/`currentClubControl`/`currentRecovery`/**`currentStamina`**.
Cross-checked `PlayerCharacterData.cs`: `currentStamina` is the STAT (line 48);
`currentStaminaEnergy` (line 65, the depleting energy bar) is **correctly NOT read**.
Stamina-energy is excluded per SPEC §2. No rarity. The shape T5 will bake is correct.

## Every `new EntryState(` clone site — Snapshot preservation audited

Grep of all 6 sites:
- `LocalTournamentBackend.cs:136` (Register) — sets `snapshot: snapshot` (fresh freeze). ✓
- `LocalTournamentBackend.cs:189` (**SubmitHoleResult**) — `snapshot: entry.Snapshot`
  ("preserve frozen snapshot across hole submissions"). **No silent null mid-tournament.** ✓
- `StubTournamentBackend.cs:43` — legacy 6-param ctor, snapshot=null (stub, never production). ✓
- `TournamentContractsTests.cs:120`, `LocalTournamentBackendTests.cs:861/872/873` — legacy
  ctor, snapshot=null (non-snapshot tests, intentional). ✓

## Adapter / isolation / scoring

- Adapter throws `KeyNotFoundException` on unknown id (`CharacterManagerStatsProvider.cs:34-36`)
  — matches §6.4 test assertion and the interface's documented contract.
- UnityEngine isolation holds: `CharacterSnapshot`, `ICharacterStatsProvider`, `FakeStatsProvider`
  are `System`-only inside the asmdef; the only Unity-touching code (`CharacterManagerStatsProvider`)
  lives in Assembly-CSharp (`Assets/Scripts/TournamentsRuntime/`) — justified (asmdef cannot
  reference Assembly-CSharp). No UnityEngine leaks into `Golfin.Tournaments`.
- Scoring/leaderboard **untouched**: `git diff HEAD -- LocalTournamentBackend.cs` has zero +/-
  lines mentioning GetLeaderboard/Countback/AssignRanks/ResolvePrize/CompareProvisional/CompareFinal/
  GetResults/ClaimPrize.

## Drift audit (Rule 13 echo)

`git status --porcelain` outside the spec folder = exactly the 3 modified .cs + 3 new .cs +
3 .cs.meta + 1 folder meta listed in IMPLEMENTER_REPORT's Files table. Every `.cs` has a
`.cs.meta`. Pre-existing dirty (ShellScene/manifest/packages-lock) is in the kickoff baseline,
unrelated. No unreported drift.

## Prior rejections

No `CESAR_REJECTION.md` exists for this task — first pass through the gate, nothing to replay.

## Three break-attempts and why each FAILED

1. **Break the freeze (store a live mutable reference):** FAILED — `CharacterSnapshot` is
   sealed/get-only/no-setters; `SnapshotFor` is called once at Register and never on read.
   A leak is impossible by construction, and §6.2 would catch it anyway (two distinct instances,
   two-pronged assertion).
2. **Wrong snapshot shape for T5 (e.g. froze stamina ENERGY, or included rarity):** FAILED —
   shape is exactly the 6 spec'd primitive fields; adapter reads `currentStamina` (stat) not
   `currentStaminaEnergy` (energy); no rarity. T5 bakes the right shape.
3. **Dropped snapshot at a clone site (silent mid-tournament data loss):** FAILED — all 6
   `new EntryState(` sites audited; SubmitHoleResult explicitly preserves `entry.Snapshot`.

## Carried-forward pressure points — my rulings

- **Optional `stats = null` ctor default (silent-no-op-freeze landmine):** **Not a blocker for
  THIS task.** Zero production `new LocalTournamentBackend(` call sites exist today (only test
  sites). SPEC §7 scopes "ctor wiring at production call sites" — vacuously satisfied. SPEC §5
  frames `stats` as an "append param," and optional-with-default is a faithful reading that keeps
  the 3 legacy test ctors compiling. The risk is real but **forward-looking (T6)** and explicitly
  surfaced by both prior reviewers. **Surfaced to Cesar as a known-deferred future-risk:** when
  T6/UI production wiring lands, make the param required (after migrating the 3 legacy test sites
  to pass `stats: null` explicitly) OR add a runtime guard in `Register` that throws when
  `_stats == null` with a non-empty `characterId`. Also declare `_stats` as
  `ICharacterStatsProvider?` to stop the annotation lie (`_stats = stats!`). I will not manufacture
  a FAIL against the spec as written.
- **SubmitHoleResult-with-non-null-snapshot coverage gap:** **Not a blocker.** The clone line
  (`:192`) is present and verified-correct by reading; SPEC §6 enumerates exactly 4 tests, none of
  which require this path, and all 4 exist and pass. A regression nulling it would not be caught by
  current tests, but the line is small, commented, and T5/T6 will add SubmitHoleResult+persistence
  coverage. Recommend (non-blocking) adding a Register→SubmitHoleResult→GetMyEntry snapshot-survives
  test in T5.

## Verdict

**ARCHITECT_REVIEW_PASS.** I tried to break the freeze, the shape, and the clone preservation,
and re-ran the suite myself — every attack failed and the suite is green (665/0-fail). The two
non-blocking risks (optional ctor default; SubmitHoleResult coverage gap) are correctly scoped to
T6/T5 and surfaced for Cesar to acknowledge, not fix here.
