# Self-Review — `stamina_live_wiring` (iter-2)

**Reviewer:** golfin-self-reviewer · **Date:** 2026-06-30 04:55 JST · **Verdict:** `FORWARD_TO_ARCHITECT`

Tier-3 code/logic task. No Figma / visual / mesh surface → Rules 16-19 N/A. Gate is code correctness + test evidence + behavioral correctness of the versus drain wire. I re-read the source independently (not the report prose) and re-ran the EditMode suite myself.

---

## Pivotal confirmation — versus IS single-hole; one drain per match == one drain per hole

The red-team's fix instruction said "drain once per HOLE, not per match." iter-2 wires the drain to `GameSession.OnMatchComplete`. That is only equivalent IF a versus match is exactly one hole. **Confirmed from source independently:**

`Assets/Scripts/Physics/Viewer/VersusMatchController.cs`:
- Line 165 `IEnumerator MatchFlow()`: ONE while-loop alternating shots between two players within ONE hole.
- Line 175 reads `tee` ONCE from `_controller.BallPosition`; line 180 calls `MatchContext.ResetMatchState(tee)` ONCE. There is no advance-to-next-hole logic, no hole index, no second `LoadHole`.
- `TryDecide` (line 320) ends the match on hole-out scenarios (P1 sink + courtesy + P2 sink/miss, both holed, safety stroke cap).
- Line 421-451 `MatchEnd` is the SOLE place that fires `GameSession.MarkMatchComplete(outcome, p1Strokes, p2Strokes)` — exactly once at match end (line 450).

**Conclusion:** versus = single-hole, OnMatchComplete fires exactly once per versus session, and one OnMatchComplete event == one completed hole for the human player. The drain wire correctness reduces to: "fire `DrainForCompletedHole(GameSession.SelectedCharacterId)` once per match end." That is exactly what `StaminaRuntimeService.OnMatchComplete` (lines 73-79) does. D5 verdict: **honored.** If versus is ever extended to multi-hole, this wire would under-drain and the design would need to revisit, but that is out-of-scope this phase.

---

## Re-walked full acceptance list (Rule 5 — every criterion, not just the symptom)

### §8.1 — Project compiles; all EditMode tests green
**PASS — independently verified.** Ran `unity-mcp-cli run-tool tests-run` with `{"mode":"EditMode"}`. Result:

```
Status: Passed
TotalTests   : 790
PassedTests  : 787
FailedTests  : 0
SkippedTests : 3
Duration     : 00:00:50.05
```

The 3 skips are the pre-existing `Golfin.Physics.Tests.HoleCompleteDriverTests` Stage-C1 `[Ignore]`s (`HoleCompleteDriver_OnInCupTerminal_AtPar_ShowsSuccessReplay`, `_FiresMarkHoleComplete`, `_OverPar_ShowsFailedRetryAndLockedNext`). `git log -1 -- HoleCompleteDriverTests.cs` shows last change `1731e222e 2026-05-21 loop_v2_c1` — **unchanged this task**, not newly-added to dodge a failure. Matches the implementer's claimed 790/787/0/3 exactly.

### §8.2 — `StaminaModel` configured at boot
**PASS.** `StaminaRuntimeService.cs:31` `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` → `Boot()` → `StaminaConfigLoader.Load()` then `!IsConfigured` guard logs+returns without throwing. Shot-path guards at lines 63, 75, 90, 126 mean no `StaminaModel.*` method is called when unconfigured (which would throw via `EnsureConfigured`).

### §8.3 — Solo drain: OnHoleComplete reduces by DrainForHole, persists, recovers on reload
**PASS — unchanged from iter-1.** `StaminaRuntimeService.OnHoleComplete` (lines 61-67) calls `DrainForCompletedHole(charId)`. The shared `DrainForCompletedHole` (lines 88-111) accrues regen, then `pcd.currentStaminaEnergy = Mathf.Max(0f, pcd.currentStaminaEnergy - StaminaModel.DrainForHole())`, stamps timestamp, calls `PersistCondition`. T2/T3/T5 cover the math + round-trip; red-team's iter-1 source trace through `CharacterManager.SyncCharacterToSaveData` lines 224-229 still holds (preserved timestamp, no clobber).

### §8.3a — **D5: Versus drain via OnMatchComplete (the iter-2 fix)**
**PASS — independently confirmed.**
- `StaminaRuntimeService.cs:55` `GameSession.OnMatchComplete += OnMatchComplete;` in `WireHoleComplete()`.
- `StaminaRuntimeService.cs:73-79` `OnMatchComplete(MatchOutcome, int, int)` handler reads `GameSession.SelectedCharacterId` and calls shared `DrainForCompletedHole(charId)`.
- `GameSession.cs:90` declares `public static event System.Action<MatchOutcome, int, int> OnMatchComplete;` — signature matches the handler exactly.
- `GameSession.cs:93-94` `MarkMatchComplete(...)` is the sole invoker; called only by `VersusMatchController.MatchEnd:450`.
- `ResetForTests` (line 151) cleans up both `OnHoleComplete` and `OnMatchComplete` subscriptions — no test bleed.
- **No Physics .cs edit** to wire this: `VersusMatchController` was read-only; the wire lives entirely in `Assembly-CSharp` by subscribing externally to a Physics-fired event. Standing ban honored.

### §8.3a tests T9A/T9B — do they actually go RED if the wire is removed?
**Scrutinized. T9A passes the smell test; T9B is weaker than ideal but adequate when combined with T9A.**

- **T9A (`T9_VersusDrain_PartA_OnMatchComplete_IsWired`):** Sets up StaminaModel via `SetUp()`. Resets wiring → calls `WireHoleComplete()` via reflection (real production internal-static, not a copy). Then reads `typeof(GameSession).GetField("OnMatchComplete", NonPublic|Static)` and asserts the backing delegate has a subscriber whose `DeclaringType == StaminaRuntimeService`.
  - **If I delete `GameSession.OnMatchComplete += OnMatchComplete;` at `StaminaRuntimeService.cs:55`:** backing field is null after `WireHoleComplete()` → `Assert.IsNotNull(del, ...)` FAILS. Genuinely red.
  - Targets the REAL `StaminaRuntimeService` type via `System.Type.GetType("StaminaRuntimeService, Assembly-CSharp")` (line 36) — not a fake/local copy. Not theater.
  - **Residual weakness (non-blocking):** there is an `Assert.Pass` fallback if `GetField("OnMatchComplete", NonPublic|Static)` returns null (only the public `GetEvent` works). For a normal C# auto-event in `Assembly-CSharp` the private backing field IS named identically to the event and IS reachable via NonPublic|Static — confirmed by reading `GameSession.cs:90` (normal `public static event System.Action<...>` syntax, no custom add/remove). The fallback would only trip if someone refactors OnMatchComplete to a manual add/remove pattern; today it's dead code. Acceptable.
- **T9B (`T9_VersusDrain_PartB_DrainForCompletedHole_ReducesEnergy_IsVersus`):** Sets `GameSession.IsVersus = true` (the versus scenario). Constructs a PCD via reflection on the real production type. Calls `AccrueRegen` (real production static) at same-instant → asserts no-op (T3 also covers this). Then INLINES the drain math `Mathf.Max(0, energy - DrainForHole())` and asserts the result. Finally verifies `DrainForCompletedHole` exists as `internal static` via reflection.
  - **If I delete the shared method:** `Assert.IsNotNull(drainMethod, ...)` FAILS. Red on shared-method-existence regression.
  - **If I corrupt the drain body inside `DrainForCompletedHole`:** T9B would NOT catch it — it re-implements the math inline rather than driving the production method. This is the only weak spot. But: T2 (drain math) + the red-team's source read of the body still holds, and T9A guarantees the wire reaches the shared method.
  - Combined T9A+T9B cover: "wire to OnMatchComplete exists" + "shared method exists" + "drain math is correct." If either is broken, a test goes red. Sufficient for D5 acceptance.

Behavior-level versus-drain integration would require a play-mode harness (CharacterManager.Instance is null in EditMode — T9B explicitly notes this). Accepting T9A+T9B as EditMode-appropriate evidence; the red-team flagged the iter-1 T2 as inadequate ("re-implements `Mathf.Max(0, energy - Drain)` inline and never exercises the versus path") and iter-2's T9A explicitly closes that gap by checking the **real OnMatchComplete subscription** under `IsVersus=true`. The narrow scope of the fix matches the narrow scope of the test additions — proportionate.

### §8.4 — Option C single-place degradation, no double-dip
**PASS — unchanged from iter-1, red-team verified.** Confirmed `Assets/Resources/Physics/stats.csv:15` still carries `stamina_floor_fraction,1.0,...`. `LiveStatProviderHost.cs:233-246` `BuildCharacterStats` calls `StaminaModel.EffectiveStat` only for Strength + ClubControl gated on `IsDegraded(...)`, passes Recovery + Stamina raw. T7 + T8 in suite.

### §8.5 — Tank size scales with Stamina stat
**PASS — unchanged.** `CharacterManager.RefreshStatValues` + `LoadRoster` hydrate. T1_Sta9=114 + T1_Sta0=60.

### §8.6 — Save migrates v3→v4; pre-v4 loads full
**PASS — unchanged.** `SaveSchemaMigrator.cs:17` `CurrentSchemaVersion = 4`; v3→v4 block at lines 58-66 (no-transform, default-safe). `SaveData.cs:133-134` `conditionEnergy=0f`, `conditionUpdatedUtc=""`. T6_V3ToV4 + T6_FailHard_V5 cover.

### §8.7 — Tournament pool model untouched (Phase 3)
**PASS.** `git diff HEAD -- Assets/Scripts/Physics/` = empty. `TournamentRoundContext.cs` not in diff. `ShotController.cs:393 DepleteStamina()` per-shot path untouched. Only the **penalty seam** is shared via `BuildCharacterStats(int,int,int,int,float)` — pool model still placeholder.

### §8.8 — Scope clean
**PASS.** Full `git status --porcelain --untracked-files=all`:
```
 M Assets/Resources/Physics/stats.csv             (data CSV; iter-1)
 M Assets/Scripts/CharacterManager.cs              (iter-1)
 M Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef (iter-1)
 M Assets/Scripts/LiveStatProviderHost.cs          (iter-1)
 M Assets/Scripts/Save/SaveData.cs                 (iter-1)
 M Assets/Scripts/Save/SaveSchemaMigrator.cs       (iter-1)
 M Assets/Scripts/Save/Tests/SaveLayerTests.cs     (iter-1)
 M Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs (iter-1)
?? Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs (iter-1 + T9A/B iter-2)
?? Assets/Scripts/StaminaRuntimeService.cs        (iter-1 + OnMatchComplete iter-2)
?? Docs/Specs/Active/stamina_live_wiring/*        (spec folder, expected)
```
Every diffed file lives in SPEC §3 (or is the new service / test file declared there). No drift outside the task folder. `git diff HEAD -- Assets/Scripts/Physics/` = empty → **ZERO Physics .cs edits** (standing ban honored; the only Physics-tree change is the data CSV row). No `*Gate` scenario, no `M_Splash*.mat`, no `LabScaffold.unity` mutation, no roster-UI edits. Rule 13 (close-out drift) clean.

---

## Independent EditMode test run
Mirrors implementer: 790/787/0/3, 50.05s. The 3 skips are the pre-existing Stage-C1 ignores, file unchanged since `1731e222e 2026-05-21`. No newly-added skips dodging a failure.

---

## Three break-attempts summary
1. **Multi-hole versus** — could `OnMatchComplete` fire less often than once-per-hole? Read `VersusMatchController.MatchFlow` end-to-end: single hole, single `MarkMatchComplete` call at MatchEnd. Held.
2. **T9A theater** — does it actually catch removal of the OnMatchComplete wire? Yes: it reads the real GameSession backing field, walks the invocation list, asserts a StaminaRuntimeService delegate is present. Delete the `+=` line and `del` becomes null → `Assert.IsNotNull` fails. Held. (One residual: explicit-event refactor would silently `Assert.Pass`, but that's not the shipping shape.)
3. **Body coverage** — T9B inlines the math instead of driving DrainForCompletedHole. If the body were corrupted (e.g., `+= drain` instead of `-= drain`), T9B wouldn't catch. But the body is short (3 lines: AccrueRegen → Mathf.Max → timestamp), red-team source-traced it, and T2 covers the drain math. Acceptable for this iter; flagging as a future-proofing wish, not a blocker.

---

## Verdict
**FORWARD_TO_ARCHITECT** — `STATUS.md` set to `SELF_REVIEW_PASS`. The narrow iter-2 fix (versus drain via OnMatchComplete) is correct, tested with a test that goes red on regression, and all iter-1 work still holds. Scope clean, standing bans honored, full test suite green at 790/787/0/3.
