# Implementer Report — `loop_v2_c0_matchmaking_to_gameplay_transition`

## Implementation summary

Stage C0 closes the OPPONENT FOUND → ball-at-tee gap. Build settings now include `LabScaffold.unity` + all 18 `Hole_NN_Geo.unity` scenes (the ghost `Hole_01.unity` entry was removed). A new `GameplaySceneLoader` in ShellScene owns the transition: it shows the Loading screen, additively loads LabScaffold, additively loads the seeded hole's geo, hides the bottom nav, then hands off to `PhysicsLabController` (whose existing `ScanForLoadedHoleSceneAtStartup` was extended to poll for the seeded hole). `LoadingScreenController` was generalized with a `LoadTarget` enum so it can either complete to Home (legacy boot) or hand off via a coroutine (Stage C0 HoleLoad). `MatchmakingModalController` was edited so its `OpponentScanRoutine` calls `GameplaySceneLoader.Instance.BeginGameplayLoad(...)` after seeding `GameSession` and a brief beat on "OPPONENT FOUND".

## Files modified or created

| Path | Change |
|---|---|
| [ProjectSettings/EditorBuildSettings.asset](../../../../ProjectSettings/EditorBuildSettings.asset) | modified — added `LabScaffold.unity` + 17 missing `Hole_NN_Geo.unity` entries, removed ghost `Hole_01.unity` (zero-GUID) |
| [Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs](../../../../Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs) | created — singleton MonoBehaviour, owns the OPPONENT FOUND → gameplay-scene-loaded coroutine; exposes `BeginGameplayLoad(int)`, `UnloadGameplay()`, internal `ApplyPreloadSetup(int)` for tests |
| [Assets/Scripts/UI/LoadingScreenController.cs](../../../../Assets/Scripts/UI/LoadingScreenController.cs) | modified — added `LoadTarget` enum, `PrepareForHoleLoad(int)`, `ClearTarget()`, `SetProgress(float)`, `FinishLoadingCoroutine()`; legacy boot path unchanged |
| [Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs](../../../../Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs) | modified — `OpponentScanRoutine` now hides the modal and calls `GameplaySceneLoader.Instance.BeginGameplayLoad(seededHole)` after seeding `GameSession` |
| [Assets/Scripts/UI/PersistentUIManager.cs](../../../../Assets/Scripts/UI/PersistentUIManager.cs) | modified — added `SetBottomNavVisible(bool)` alias for `ShowBottomNav(bool)` (existing `bottomNavPanel` SerializeField reused) |
| [Assets/Scripts/Physics/Viewer/PhysicsLabController.cs](../../../../Assets/Scripts/Physics/Viewer/PhysicsLabController.cs) | modified — `Start()` logs `GameSession.CurrentHoleNumber` when seeded; `ScanForLoadedHoleSceneAtStartup` now polls up to 5s for the expected `Hole_NN_Geo` scene when `GameSession` is seeded (necessary because `GameplaySceneLoader` loads the hole geo *after* LabScaffold's `Start` fires) |
| [Assets/Scripts/UI/Tests/Golfin.UI.Tests.asmdef](../../../../Assets/Scripts/UI/Tests/Golfin.UI.Tests.asmdef) | created — EditMode TestAssemblies asmdef; tests use reflection to access Assembly-CSharp types (see Spec deviations §2) |
| [Assets/Scripts/UI/Tests/GameplaySceneLoaderTests.cs](../../../../Assets/Scripts/UI/Tests/GameplaySceneLoaderTests.cs) | created — 5 EditMode tests (reflection-based) |

## Screenshot

- **Captured at:** `screenshots/c0_smoke_editmode_20260519_113948.png`
- **Scene loaded:** Current EditMode scene (no hole loaded — see note below)
- **Play mode:** No (EditMode, scene state unchanged from pre-task)
- **Hole loaded (if applicable):** None — the screenshot is a smoke artifact, not a visual proof of the end-to-end flow

**Note on the screenshot:** This is not a visual proof of the production flow. The SPEC explicitly designates Cesar's first-end-to-end playthrough as the canonical visual gate ("Cesar's visual gate is unavoidably heavy" §Goal; "the real gate" §Tests). The implementer cannot run the production flow from cold without a logged-in character + interactive modal taps. The screenshot is the EditMode Game View RT at the moment of report finalization (captured via the same `m_RenderTexture` reflection path that `CaptureCore.GrabGameViewRT` uses — `CaptureCore.SnapGameViewWithLabel` itself failed in Unity 6 EditMode because its `IsCreated()` guard rejected the RT; a one-shot read without that guard succeeded and yielded a 1170×2532 PNG without mutating any scene state).

## Acceptance checklist (Audit grep + mechanical DoD from SPEC.md)

| Item | Result | Justification |
|---|---|---|
| `grep -n 'LabScaffold.unity' ProjectSettings/EditorBuildSettings.asset` → one hit | PASS | Single hit at line 12; GUID `d778a238279233a44bd60ae0ead95bf3` matches the `.meta` file. |
| `grep -n 'Hole_01.unity' ProjectSettings/EditorBuildSettings.asset \| grep -v _Geo` → zero hits | PASS | Pipe through `wc -l` returns 0; ghost zero-GUID entry removed. |
| `grep -c 'Hole_.._Geo.unity' ProjectSettings/EditorBuildSettings.asset` → 18 hits | PASS | All 18 `Hole_NN_Geo` scenes present; GUIDs harvested from each `.meta` and verified. |
| `ls Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs` → file exists | PASS | File created at the spec-mandated path; contains `BeginGameplayLoad`, `UnloadGameplay`, `ApplyPreloadSetup`, Awake/OnDestroy singleton lifecycle, and the additive-load coroutine. |
| `grep -n 'GameplaySceneLoader.Instance' Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` → one hit | PASS | Three matches total in the file (one comment, one assignment to `var loader = ...Instance`, one error-log string); the assignment at line 412 is the load-bearing call site that fires `BeginGameplayLoad(seededHole)`. |
| `grep -n 'PrepareForHoleLoad' Assets/Scripts/UI/LoadingScreenController.cs` → one hit | PASS | Method signature found at line 77 (`public void PrepareForHoleLoad(int holeNumber)`); generalizes the loader for the HoleLoad target. |
| `grep -n 'GameSession.CurrentHoleNumber' Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` → ≥1 hit | PASS | Four hits (lines 313, 315, 317, 346). Code references at lines 315 and 346 read `GameSession.CurrentHoleNumber` — line 315 logs the seed in `Start()`, line 346 gates the 5-second polling extension in `ScanForLoadedHoleSceneAtStartup`. |
| Project compiles clean | PASS | `UnityEditor.EditorUtility.scriptCompilationFailed=False` via MCP `script-execute`; all four expected types (`GameplaySceneLoader`, `LoadingScreenController`, `PersistentUIManager`, `PhysicsLabController`) resolve via reflection from their declared assemblies. |
| EditMode test gate: 305/305 PASS | PASS | `tests-run` reported `Status=Passed, TotalTests=305, PassedTests=305, FailedTests=0, SkippedTests=0, Duration=11.35s`. All five new tests pass: `BeginGameplayLoad_HidesBottomNav` (0.008s), `BeginGameplayLoad_PreparesLoadingScreenWithHoleNumber` (0.010s), `UnloadGameplay_RestoresBottomNav` (0.001s), `LoadingScreenController_PrepareForHoleLoad_SetsTarget`, `LoadingScreenController_ClearTarget_ResetsToLegacy`. |

## Cesar visual gate (manual, Cesar-only — not implementer scope per SPEC)

Per the SPEC, the visual gate is by-design Cesar's first-end-to-end production playthrough. The implementer cannot run these from EditMode. They remain as unchecked Cesar checkboxes; the implementer's responsibility is the mechanical wiring above plus the manual scene-wiring steps below.

- [ ] Launch from cold (Logo → Splash → Loading → Home)
- [ ] Tap PLAY on Home → matchmaking modal opens → OPPONENT FOUND fires → loading screen with tips appears
- [ ] Gameplay scene loads (Hole 1) → loading screen fades out → ball at tee, can be aimed
- [ ] Hit a putt or two; ball physics works
- [ ] Sink the ball into the cup → lab `HoleCompleteWidget` appears
- [ ] Bottom nav bar is HIDDEN during gameplay
- [ ] Tap CANCEL on matchmaking before OPPONENT FOUND → returns cleanly to Home, no scene-load fires
- [ ] Repeat from Hole Selection: select Hole 2 → PLAY → loads Hole 2 (verify hole number shows on HUD)

## Scene wiring (paste-for-Cesar — must be done before visual gate)

`GameplaySceneLoader.Awake` auto-wires `loadingScreen` (via `FindObjectOfType<LoadingScreenController>`) and `persistentUI` (via `PersistentUIManager.Instance` then `FindObjectOfType<PersistentUIManager>`) if the SerializeFields are left null, so manual wiring is optional but recommended for determinism. The steps:

1. Open `Assets/Scenes/ShellScene.unity` in the Unity Editor.
2. In the Hierarchy, find the GameObject that hosts `PersistentUIManager` (likely `PersistentUI` or similar — see `Docs/Architecture/UI_HIERARCHY.md`).
3. Add the `GameplaySceneLoader` component to that GameObject (or to a sibling under the same Canvas root — either works; Awake registers the static `Instance`).
4. In the Inspector for the new `GameplaySceneLoader`:
   - Drag the `LoadingScreenController` instance from the scene into the `loadingScreen` slot.
   - Drag the `PersistentUIManager` instance into the `persistentUI` slot.
5. On `PersistentUIManager` in the Inspector, confirm `bottomNavPanel` is wired to the bottom-nav container GameObject (existing field — should already be wired from Stage A; if not, drag the bottom-nav container in).
6. Save the scene (`File → Save` or Ctrl/Cmd+S).
7. (Optional) commit the scene change separately; this implementer's commit covers code only.

Sanity check after wiring: enter Play mode from ShellScene, open Matchmaking, wait through OPPONENT FOUND — the loading screen should appear briefly, then the gameplay scene with the ball at the seeded hole's tee.

## Known FAIL items

None — every mechanical Acceptance checklist row is PASS. Visual gate is Cesar's manual step, not implementer scope.

## Spec deviations

1. **`PhysicsLabController.LoadHole(n)` does not exist; the spec assumed it did.** Pre-flight grep confirmed there is no `LoadHole` method on `PhysicsLabController`; the editor-only `PhysicsLabHolePicker.LoadHole((int,string))` does the additive load in editor flows. Rather than add a new runtime `LoadHole` method on the controller (additional surface area, two ways to do one thing), Stage C0 has `GameplaySceneLoader` own both additive loads (LabScaffold + Hole_NN_Geo) and reuses the existing `ScanForLoadedHoleSceneAtStartup` coroutine, extended to poll for the seeded hole with a 5s timeout when `GameSession` is seeded.

2. **Tests use reflection to access Assembly-CSharp types.** `LoadingScreenController`, `PersistentUIManager`, and `GameplaySceneLoader` all live in the predefined Assembly-CSharp assembly (no asmdef). Unity disallows asmdefs from referencing predefined assemblies. Refactoring those three scripts into a new production asmdef would require moving their dependencies (`ScreenManager`, `RewardPointsManager`, `SettingsController`, `CharacterManager`, etc.) too — far outside C0 scope. Instead, the tests use `Type.GetType("…, Assembly-CSharp")` + reflection to access types, fields, and methods. The invariants under test (`Target` enum, hole number, `bottomNavPanel.activeSelf`) are still exercised end-to-end via the real production code paths.

3. **`PersistentUIManager` reuses the existing `bottomNavPanel` SerializeField.** The spec proposed adding a new `bottomNavBar` SerializeField. There is already a public `bottomNavPanel` field and a `ShowBottomNav(bool)` method that toggles it. Adding a duplicate would create two slots in the Inspector pointing at the same GameObject. C0 just adds `SetBottomNavVisible(bool)` as a thin alias for `ShowBottomNav(bool)` to honor the spec's call-site contract from `GameplaySceneLoader`.

4. **`LoadingScreenController.FinishLoadingCoroutine` deactivates the loading screen GameObject rather than navigating via `ScreenManager.ShowScreen`.** The spec sketch is fuzzy on the legacy/HoleLoad reset path. Deactivating the GameObject lets the additively-loaded gameplay scene become visible without a Home-screen flicker; the loading screen state is reset (target → LegacyBootHome, hole number → 0) on completion so its next show works correctly.

5. **`MatchmakingModalController.OpponentScanRoutine` calls `Hide()` before `BeginGameplayLoad`** so the modal disappears before the loading screen appears. The spec said "after the modal's existing fade-out" — implementer interpreted this as the existing `Hide()` (which `ModalController` fades via its own animator), preceded by a 0.6s `WaitForSeconds` so the player registers the "OPPONENT FOUND" text. Cesar may want to tune that beat.

## Console output

```
[CompileCheck] scriptCompilationFailed=False
[CompileCheck] OK assembly loaded: Assembly-CSharp
[CompileCheck] OK assembly loaded: Golfin.Gameplay.Loop
[CompileCheck] OK assembly loaded: Golfin.Physics.Viewer
[CompileCheck] Found 3 expected assemblies in current AppDomain.
[CompileCheck] GameplaySceneLoader=FOUND LoadingScreenController=FOUND PersistentUIManager=FOUND PhysicsLabController=FOUND
```

EditMode test results:

```
Golfin.UI.Tests.GameplaySceneLoaderTests.BeginGameplayLoad_HidesBottomNav                          Passed (0.008s)
Golfin.UI.Tests.GameplaySceneLoaderTests.BeginGameplayLoad_PreparesLoadingScreenWithHoleNumber     Passed (0.010s)
Golfin.UI.Tests.GameplaySceneLoaderTests.LoadingScreenController_ClearTarget_ResetsToLegacy        Passed
Golfin.UI.Tests.GameplaySceneLoaderTests.LoadingScreenController_PrepareForHoleLoad_SetsTarget     Passed
Golfin.UI.Tests.GameplaySceneLoaderTests.UnloadGameplay_RestoresBottomNav                          Passed (0.001s)

Full suite: TotalTests=305, PassedTests=305, FailedTests=0, SkippedTests=0, Duration=11.35s.
```

Pre-existing meta-file GUID warnings (Rindo Course lightmap PNGs/EXRs, `ExampleAutoWireScreen.cs.meta`, `UIAutoWire.cs.meta`) appeared on `assets-refresh` — unrelated to this task and present on `main` before C0 changes.

## Open questions for Architect

1. **`HoleSelection` PLAY entry point.** SPEC §Cesar visual gate item "select Hole 2 → PLAY → loads Hole 2" depends on `HoleSelectionScreen`'s PLAY button passing the selected hole to `MatchmakingModalController.Open(int holeIndex)`. Stage B made that method accept an index, but C0 did not audit whether the Hole Selection screen currently calls `Open(int)` or `Open()` (defaultHoleIndex). If Hole Selection always opens with the default, holes 2–18 won't be reachable from production flow even though they're now in build settings.

2. **`MatchmakingModalController._opponentScanCoroutine = null` is now unreachable** because the coroutine yields + Hides + returns before the previous-last statement. Left for clarity (Hide() may trigger OnHide which stops the coroutine, in which case the assignment is moot anyway). No-op risk only; flagging in case the reviewer prefers removal.

3. **`ScreenManager.ShowScreen(ScreenId.Loading)`** triggers a `FadeOutThenIn` if `FadeController.Instance != null` — a black-fade between matchmaking-hide and loading-screen-visible. Cesar may find this jarring; if so, the C0 fix is to call `ShowScreen(..., instant: true)` from `GameplaySceneLoader.ApplyPreloadSetup`. Flagging in case the visual gate sees a flicker.

4. **Screenshot is a smoke artifact, not a visual proof.** Per SPEC, Cesar's gate is the canonical visual proof. EditMode capture of a meaningful frame is not feasible without a from-cold playthrough. This is consistent with the spec's "Cesar visual gate is unavoidably heavy" framing, but worth flagging explicitly so the reviewer doesn't expect a Hole-1-at-tee screenshot here.
