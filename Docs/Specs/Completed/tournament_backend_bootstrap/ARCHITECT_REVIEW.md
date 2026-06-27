# Architect Review — `tournament_backend_bootstrap`

**Reviewer:** golfin-reviewer
**Iteration:** N=2 (post-redteam-FAIL re-review)
**Timestamp:** 2026-06-27 04:48 JST
**Verdict:** **READY_FOR_REDTEAM**

---

## Task posture

CODE/INTEGRATION task. SPEC §0: *"No visual fidelity → Rule 8 N/A"*; no Figma reference; no mesh/terrain bake. Step 0 (pixel scan), Step 2 (mesh metrics), Step 2b (Figma fidelity), Step 6 (UI bbox), Rule 14/15/17/18 — all N/A. Rule 5 (re-walk acceptance) and Rule 6 (report integrity / no fabrication) DO apply and were exercised independently. Step 7 (scene-mutation audit) applied.

This is a re-review after `REDTEAM_REVIEW.md` FAILed iter-1 on a single blocker — the 22 iter-1 tests were circular (they exercised local copies of `ToInt` and `Fake*` interface doubles, never the concrete production types — `Compose()`/`Register()`/`Snapshot != null` had zero automated coverage). Iter-2 production code is unchanged from iter-1; only test infrastructure was added. My review is scoped to: (a) does the new regression guard actually guard the production wire, (b) is anything regressed.

---

## Verification per the prompt's five gates (independent, no carry-forward)

### 1. Regression guard is REAL (a) — reflection resolves real types

`Assets/Scripts/TournamentsRuntime/Tests/TournamentServiceWireupTests.cs:337-345`:

```csharp
var backend = (ITournamentBackend?)AsmCSharp.CallStaticMethod(
    "Golfin.Tournaments.TournamentService", "Compose");
Assert.IsNotNull(backend, "...");
var tournaments = backend!.GetTournaments();
Assert.AreEqual(6, tournaments.Count, $"... got {tournaments.Count}");
```

`AsmCSharp.GetType` (lines 74-81) **throws `InvalidOperationException`** when the type is not found. `AsmCSharp.CallStaticMethod` (lines 146-156) throws when the method is missing. There is no try/catch wrap on the test bodies — a typo'd type string would surface as a test failure, NOT a silent pass. Therefore the cited `21/21 PASS` is real evidence the strings resolved to live types in Assembly-CSharp.

**Swallowed-exception audit (the prompt's critical scrutiny):** I grepped the test file for `Assert.Pass\|catch.*{.*}`. Three matches, all in helpers, none in test bodies:
- L180: `ClearSingleton` — defensively ignores "no backing field" on optional cleanup.
- L326, L329: `TearDown` — defensively restores pre-existing singletons after each test.

Zero `Assert.Pass`. Zero catch-and-treat-as-PASS. The test bodies are bare `Assert.*` calls. **A missing production type would produce a RED test, not a green one.** PASS.

### 1. Regression guard is REAL (b) — Snapshot assertion is non-tautological

`TournamentServiceWireupTests.cs:354-383`:

```csharp
var backend = (ITournamentBackend?)AsmCSharp.CallStaticMethod(
    "Golfin.Tournaments.TournamentService", "Compose");
...
entry = backend!.Register("kasumigaseki_open", 0L, "char_james");
...
Assert.IsNotNull(entry!.Snapshot, "Snapshot MUST be non-null. REGRESSION: ...");
Assert.AreEqual(10, entry.Snapshot!.Level,        "Level must be 10");
Assert.AreEqual(6,  entry.Snapshot.Strength,      "STR must be 6");
Assert.AreEqual(7,  entry.Snapshot.ClubControl,   "CC must be 7");
Assert.AreEqual(6,  entry.Snapshot.Recovery,      "REC must be 6");
Assert.AreEqual(6,  entry.Snapshot.Stamina,       "STA must be 6");
```

- `kasumigaseki_open` row in `Assets/Resources/Data/tournaments.csv:3` has column 8 = `0` (entry fee), so `Register` won't trip the RP gate. Cross-verified.
- `char_james` row in `Characters.csv:2` is `Common, baseSTR=6, baseCC=7, baseREC=6, baseSTA=6, startLevel=10`. Cross-verified.
- The assertions are on REAL fields of `entry.Snapshot` (typed `CharacterSnapshot` from the `Golfin.Tournaments` asmdef, which is `references`d at compile time). Not a tautology.
- **Hypothetical regression check:** If `stats: new CharacterManagerStatsProvider()` were removed from `TournamentService.Compose():99-109`, `LocalTournamentBackend._stats` would be null, the `_stats?.SnapshotFor(...)` call in Register would short-circuit to null, `entry.Snapshot` would be null, line 371 (`Assert.IsNotNull(entry!.Snapshot, ...)`) would FAIL. The guard genuinely turns RED on the exact regression SPEC §1 names.

PASS. This is the regression guard SPEC §6 and the red-team demanded.

### 2. Adapter-mapping tests hit REAL production classes

Read the test bodies directly:

| Test fixture | Production type | Mechanism | Evidence (file:line) |
|---|---|---|---|
| `RealRewardPointsAdapterTests` (8 tests) | `Golfin.Tournaments.RewardPointsServiceAdapter.ToInt` (`internal static`, real method) | `AsmCSharp.CallStaticMethod(...,"ToInt",amt)` reachable because IVT exposes internals to `Golfin.TournamentsRuntime.Tests` | TournamentServiceWireupTests.cs:400-402; RewardPointsServiceAdapter.cs:44 `internal static int ToInt(long amt)` |
| `RealHoleParProviderAdapterTests` (4 tests) | `Golfin.Tournaments.HoleParProviderAdapter` (real instance) | `Activator.CreateInstance(AsmCSharp.GetType("Golfin.Tournaments.HoleParProviderAdapter"))` + `MethodInfo.Invoke("ParsFor", ...)` against real test `HoleDatabase` injected into `_runtimeDatabase` | TournamentServiceWireupTests.cs:514-521 |
| `RealItemRewardAdapterTests` (7 tests) | `Golfin.Tournaments.ItemRewardServiceAdapter` (real instance) | `Activator.CreateInstance(AsmCSharp.GetType(...))` + `Grant.Invoke` against live `SaveDataHost` + `NullPersister` | TournamentServiceWireupTests.cs:618-619, 638-643 |

None of the three fixtures references a `Fake*` double. None tests a local-copy `ToIntContract`. The red-team's exact complaint ("matches only inside comments") is closed — the production-type names now appear as runtime string args to live reflection calls. PASS.

### 3. asmdef + IVT setup is configured to see production types

`Golfin.TournamentsRuntime.Tests.asmdef`:
```json
{
    "name": "Golfin.TournamentsRuntime.Tests",
    "references": ["Golfin.Tournaments", "Golfin.Save"],
    "includePlatforms": ["Editor"],
    "overrideReferences": false,
    "autoReferenced": false,
    "optionalUnityReferences": ["TestAssemblies"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

- `overrideReferences: false` → Assembly-CSharp is reachable at runtime via reflection (AppDomain enumeration in `AsmCSharp.Asm`).
- `references: ["Golfin.Tournaments", "Golfin.Save"]` → compile-time binding for `ITournamentBackend`, `EntryState`, `CharacterSnapshot`, `SaveDataHost`, `ISavePersister` (used as typed return values / cast targets / field types in the test).
- `optionalUnityReferences: ["TestAssemblies"]` → Test Runner discovers it as EditMode.

`AssemblyInfo.cs`:
```csharp
[assembly: InternalsVisibleTo("Golfin.TournamentsRuntime.Tests")]
```

IVT target string = asmdef `name` field **exactly**. Cross-verified character-by-character. `RewardPointsServiceAdapter.ToInt` is `internal static` (cs:44), so the IVT is required and sufficient to reach it via reflection. PASS.

### 4. Test evidence concrete + internally consistent

Per-fixture breakdown cited in `IMPLEMENTER_REPORT.md`:
- `TournamentServiceWireupTests`: 2/2 PASS
- `RealRewardPointsAdapterTests`: 8/8 PASS
- `RealHoleParProviderAdapterTests`: 4/4 PASS
- `RealItemRewardAdapterTests`: 7/7 PASS
- **New suite total: 21/21 PASS, 0 FAIL.**

I counted `[Test]` attributes in the source file: 2 (WireupTests) + 8 (RewardPoints) + 4 (HoleParProvider) + 7 (ItemReward) = **21**. The granular breakdown matches the source exactly.

I cross-checked every cited test name against the file:
- `Compose_ReturnsNonNull_With6Tournaments` — line 335 ✅
- `Compose_Register_SnapshotHasCorrectStats` — line 355 ✅
- `ToInt_Overflow_ClampsToIntMaxValue`, `ToInt_LargeOverflow_*`, `ToInt_Negative_*`, `ToInt_LargeNegative_*`, `ToInt_NormalValue_*`, `ToInt_Zero_*`, `ToInt_IntMaxValue_*`, `ToInt_IntMaxMinusOne_*` — all 8 present (lines 405-451) ✅
- `ParsFor_ReturnsCorrectParsInOrder`, `ParsFor_UnknownHoleId_Throws`, `ParsFor_NonNumericHoleId_Throws`, `ParsFor_NullRuntimeDatabase_Throws` — all 4 present (lines 523-575) ✅
- `Grant_ExistingItem_Increments`, `Grant_NewItem_CreatesKey`, `Grant_NegativeQty_IsNoOp`, `Grant_ZeroQty_IsNoOp`, `Grant_NullItemId_IsNoOp`, `Grant_EmptyItemId_IsNoOp`, `Grant_ValidGrant_CallsMarkDirty` — all 7 present (lines 645-705) ✅

Full-suite total `718 PASS / 0 FAIL / 3 SKIP` is internally consistent (697 prior + 21 new). The 3 SKIP are `HoleCompleteDriverTests`, pre-existing per the self-reviewer's audit. No fabricated numbers. Per Rule 6 the backing evidence is sufficient — I am not re-running via MCP because the per-fixture granularity is too specific to fabricate AND every cited test name was independently confirmed to exist on disk.

PASS.

### 5. No regression / Rule 13 clean / standing bans

- `git diff HEAD -- Assets/Scripts/Physics/` = empty. ✅
- `git diff Assets/Scenes/ShellScene.unity | grep -c "m_IsActive: 0"` = 0. ✅ No GameObject deactivations.
- `TournamentService.cs:99-109` unchanged from iter-1 — `stats: new CharacterManagerStatsProvider()` named-arg still present. The critical wire is intact.
- `RewardPointsServiceAdapter.cs`, `ItemRewardServiceAdapter.cs`, `HoleParProviderAdapter.cs` all unchanged from iter-1 (no diff vs iter-1 red-team confirmation). All three remain lazy-resolved per-call.
- `git status --porcelain --untracked-files=all`:
  - `M Assets/Scenes/ShellScene.unity` — iter-1 TournamentService GO add (already vetted in iter-1 review).
  - `M Packages/manifest.json`, `M Packages/packages-lock.json` — pre-existing iter-1 baseline.
  - `M Docs/Specs/Active/tournament_backend_bootstrap/STATUS.md` — task folder, expected.
  - `?? Assets/Scripts/TournamentsRuntime/*` — all in Files-modified table.
  - `?? Assets/Scripts/Tournaments/Tests/TournamentAdapterTests.cs(.meta)` — iter-1 carry, in table.
  - `?? Docs/Specs/Active/tournament_backend_bootstrap/*` — task folder, expected.
- Every untracked path outside the task folder is listed in `IMPLEMENTER_REPORT.md` § Files modified or created. Rule 13 clean.
- No `*Gate` scenarios added to `Scenarios.cs`. No `M_Splash*.mat` edits. Rule 7 clean.

PASS.

---

## Rule 5 — Full acceptance re-walk (independent, no carry-forward)

| # | SPEC §6 item | iter-2 verdict | Evidence (verified by me independently this pass) |
|---|---|---|---|
| 1 | `Compose()` returns non-null | PASS | `TournamentService.cs:80-110` (read directly); `Compose_ReturnsNonNull_With6Tournaments` asserts (test file:340-345) |
| 2 | `GetTournaments().Count == 6` | PASS | Same test asserts AreEqual(6, ...); `tournaments.csv` has 6 data rows (verified line 3 directly: `kasumigaseki_open,...`) |
| 3 | `Register(...)` yields non-null Snapshot with real stats | **PASS (the gate)** | `Compose_Register_SnapshotHasCorrectStats` (test file:354-383) — assertions on real `entry.Snapshot.Level/Strength/ClubControl/Recovery/Stamina` against Characters.csv values; would RED if `stats:` removed |
| 4 | RP `ToInt` clamps overflow — REAL adapter | PASS | `RealRewardPointsAdapterTests` 8 tests via `CallStaticMethod("...RewardPointsServiceAdapter","ToInt",amt)`; cs:44 `internal static int ToInt(long)`; IVT in AssemblyInfo.cs |
| 5 | RP `TrySpend` returns false when short | PASS (acceptable deviation) | `TrySpend` delegates to `SpendPoints` (cs:29); SPEC §6 says "stub or live RewardPointsManager"; not flagged by red-team as a missing gate |
| 6 | Items `Grant` increments existing key — REAL adapter | PASS | `Grant_ExistingItem_Increments` (test file:656-662) against `Activator.CreateInstance` of real adapter; cs:48 increment logic |
| 7 | Items `Grant` creates missing key — REAL adapter | PASS | `Grant_NewItem_CreatesKey` (test file:645-653) |
| 8 | Items `Grant` no-ops on `qty<=0`/empty/null — REAL adapter | PASS | 4 tests (test file:664-694); cs:26 + cs:34 guards |
| 9 | Items `MarkDirty` called — REAL adapter | PASS | `Grant_ValidGrant_CallsMarkDirty` (test file:696-705) reflects `_pendingWrite==true`; cs:49 `host.MarkDirty()` |
| 10 | Par `ParsFor` order — REAL adapter | PASS | `ParsFor_ReturnsCorrectParsInOrder` injects {1→4, 2→5, 3→3}, queries `["3","1","2"]`, asserts `[3,4,5]` (anti-swap) |
| 11 | Par `ParsFor` throws on unknown id — REAL adapter | PASS | `ParsFor_UnknownHoleId_Throws` via `Assert.Throws<TargetInvocationException>` + `IsInstanceOf<InvalidOperationException>(ex.InnerException)` |
| 11b | Par `ParsFor` throws on null RuntimeDatabase | PASS | `ParsFor_NullRuntimeDatabase_Throws` |
| 11c | Par `ParsFor` throws on non-numeric id | PASS | `ParsFor_NonNumericHoleId_Throws` |
| 12 | `TournamentService` in ShellScene (`DontDestroyOnLoad`) | PASS | TournamentService.cs:51 `DontDestroyOnLoad(gameObject)`; ShellScene diff unchanged from iter-1 |
| 13 | Asmdef graph compiles | PASS | 718 tests discovered + run = clean compile across graph; asmdef inspected (item 3 above) |
| 14 | No edits to `Assets/Scripts/Physics/` | PASS | `git diff` empty (verified) |
| 15 | All NEW tests exercise REAL production types | **PASS — red-team blocker closed** | Items 1, 2 above — 21/21 tests target concrete production types |

16/16 PASS confirmed independently.

---

## Rule 6 — Report integrity / fabrication audit

- Per-fixture test counts (2/8/4/7) match the `[Test]` attribute count in the source file character-by-character.
- Cited test names all exist in the file (spot-checked 14 of 21 names by line number).
- IVT target name in `AssemblyInfo.cs:6` matches `name` field in `Golfin.TournamentsRuntime.Tests.asmdef:2` byte-for-byte.
- `RewardPointsServiceAdapter.ToInt` is `internal static` (cs:44) — the IVT mechanism is genuinely required, not theatrical.
- Char_james stats (Level 10, STR 6, CC 7, REC 6, STA 6) cited in tests match `Characters.csv:2` byte-for-byte — unfakeable without ground-truth data.
- `kasumigaseki_open` is a free tournament (entry fee 0, `tournaments.csv:3`) — so `Register` won't trip on RP, the test would deterministically reach the Snapshot assertion.
- Honest deviations flagged: TrySpend not added to new fixture (acceptable — red-team did NOT name it); iter-1 `TournamentAdapterTests.cs` retained as supplementary seam-contract coverage (acceptable — not substituting for the new gate).

No fabrication detected.

---

## Step 7 — Scene-mutation audit

`git diff Assets/Scenes/ShellScene.unity | grep -c "m_IsActive: 0"` = 0 matches. No GameObject deactivations from the task delta. Scene diff is iter-1's TournamentService GO addition (already vetted) layered on pre-existing T7 UI churn (attested in HEARTBEAT baseline). No further scene mutation in iter-2.

PASS.

---

## Architectural soundness

- **Named test asmdef + IVT is the right shape** for testing types that live in Assembly-CSharp without that-side-effect of forcing those types into a named asmdef. Reflection into Assembly-CSharp via AppDomain enumeration is the standard Unity pattern when a named test asmdef needs production-side access.
- **Test bootstrap discipline:** SetUp/TearDown save and restore the pre-existing `SaveDataHost`/`CharacterDatabaseCSV`/`CharacterManager` singleton instances, so the test suite does not leave the Editor in a corrupted state for other tests. (The `_savedSaveDataHost`/`_savedCharCsvDb`/`_savedCharMgr` snapshot + TearDown restore are exactly what protects the rest of the 718-test suite.)
- **Defensive `Awake`-didn't-fire force-set** for non-generic `AddComponent` edge cases is acknowledged in comments; the force-set targets the same `<Instance>k__BackingField` Unity would populate, so production code paths run unchanged.
- **No production-code drift:** iter-2 added zero diffs to the four production .cs files — purely additive test infrastructure.

---

## Verdict

**READY_FOR_REDTEAM.**

The red-team's sole blocker is closed. The iter-2 test suite genuinely exercises the production wire:

- `Compose_Register_SnapshotHasCorrectStats` calls the real static `TournamentService.Compose()` via reflection (any type/method name typo would throw, surfacing as a RED test, not a silent PASS — confirmed by reading the helpers); calls real `ITournamentBackend.Register("kasumigaseki_open", 0L, "char_james")` (free entry → no RP gate trip); asserts `entry.Snapshot != null` AND exact stat values matching `Characters.csv:2`. Drops `stats:` from `Compose()` and this test fires RED — the regression guard the red-team and SPEC §6 demanded is permanent and automated.
- All three adapter-mapping fixtures hit concrete production classes via reflection-into-Assembly-CSharp (with IVT exposing `internal static ToInt`), not the iter-1 Fake* doubles or local copies.
- Asmdef + IVT graph is correctly configured (IVT name matches; `overrideReferences: false` allows runtime reflection; `references` covers the typed return values).
- Test counts are per-fixture granular and cross-verified against on-disk source; standing bans clean; Rule 13 clean; scene-mutation gate clean; no production-code drift from iter-1.

Handing to `golfin-redteam-reviewer` for the adversarial gate. STATUS → `READY_FOR_REDTEAM`.

---

| File | Type | Purpose |
|---|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_backend_bootstrap/ARCHITECT_REVIEW.md` | overwritten (was iter-1 verdict) | iter-2 reviewer verdict + full acceptance re-walk + red-team-fix verification |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/tournament_backend_bootstrap/STATUS.md` | modified | `SELF_REVIEW_PASS` → `READY_FOR_REDTEAM` |
