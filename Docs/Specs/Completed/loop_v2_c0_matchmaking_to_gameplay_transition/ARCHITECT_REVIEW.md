# Architect Review — `loop_v2_c0_matchmaking_to_gameplay_transition`

**Reviewer:** golfin-reviewer
**Date:** 2026-05-19 JST
**Iteration:** N=1 (no prior ARCHITECT_REVIEW.md or CESAR_REJECTION.md)
**Verdict:** **APPROVE_FOR_CESAR** (PASS)

---

## Step 0 — Independent pixel scan

Skipped intentionally: this is a flow/architecture stage with no Figma reference and no visual-fidelity contract. The single screenshot (`screenshots/c0_smoke_editmode_20260519_113948.png`) is a featureless EditMode-frame procedural artifact (sky gradient + flat grey ground, no UI/scene contents), explicitly framed as non-load-bearing by SPEC §Goal, IMPLEMENTER_REPORT §Screenshot, and the self-reviewer. SPEC mandates Cesar's first end-to-end playthrough as the canonical visual gate; I am not relitigating that, per the review brief.

---

## Cross-cutting / architectural checks

### A. GameplaySceneLoader / PhysicsLabController split — clean?

**Verdict: clean.** Responsibility is split along a sensible seam:

- `GameplaySceneLoader` owns the *scene-load orchestration* (additive `LabScaffold.unity` + additive `Hole_NN_Geo.unity`, plus loading-screen and bottom-nav side effects). It lives in ShellScene and survives the load. (`GameplaySceneLoader.cs:76-119`.)
- `PhysicsLabController` owns the *runtime hole binding* (tee position, surface providers, camera prime, HoleContext seed) via `OnHoleLoaded(string)` at `PhysicsLabController.cs:1300`. It exists only inside LabScaffold.
- `PhysicsLabHolePicker` (editor-only, `Assets/Scripts/Editor/Physics/PhysicsLabHolePicker.cs:66`) is the dev/lab equivalent of `GameplaySceneLoader` — same additive-load → `OnHoleLoaded(sceneName)` funnel.

Both production (`GameplaySceneLoader`) and editor (`PhysicsLabHolePicker`) converge on the same `OnHoleLoaded(sceneName)` entry point on the controller. There is no parallel "owning the hole scene" code path — the controller never decides which hole to load, it only reacts to whichever was loaded into the SceneManager. That's the correct architecture for a controller that has to support two distinct activation paths.

The implementer's Spec deviation §1 (no new runtime `LoadHole` on `PhysicsLabController`) is the right call: adding one would create two ways to load a hole and require keeping them in sync. The chosen route — both production and editor additively load the Hole scene, the controller's coroutine polls SceneManager — is a single canonical surface.

### B. LoadingScreenController generalization — leak of C0 assumptions?

**Verdict: clean, with one minor concern noted.** The `LoadTarget` enum (`LoadingScreenController.cs:17`) is small and orthogonal: `LegacyBootHome` vs `HoleLoad`. The legacy path is unchanged (`OnEnable → BeginLoading → Update auto-finishes → FinishLoading → ScreenManager.ShowScreen(Home)` at line 130-133, 140-141). The HoleLoad path is opt-in via `PrepareForHoleLoad(int)` and finishes via the externally-driven `FinishLoadingCoroutine` (lines 151-170).

The implementer-graded "Spec deviation §4" (deactivating GameObject rather than navigating) is correct for additive load — navigating to Home would hide the additively-loaded gameplay scene under the Home shell. There is no leak: the enum value `HoleLoad` is generic enough to extend (a future `MatchmakingResultLoad` or whatever would slot in cleanly). `_target` is reset to `LegacyBootHome` in `FinishLoadingCoroutine` line 167 so re-shows behave per default.

**Minor concern (non-blocking, noted for Stage D / future):** `LoadingScreenController.SetRealProgress` (line 67) is now functionally a duplicate of `SetProgress` (line 100); both set `_realProgress` and `_useExternalProgress = true`. They are kept distinct to avoid touching the legacy `SetRealProgress` call sites, but this is the kind of small-API-bloat that should be consolidated in a follow-up. Not gating.

### C. PersistentUIManager.SetBottomNavVisible alias — API surface concern?

**Verdict: acceptable, slight smell.** The existing surface already has `ShowBottomNav(bool)` (line 255-259) and the broader `ShowBars/HideBars` pair (lines 108-121). Adding `SetBottomNavVisible(bool)` as a thin alias (line 266) is technically redundant.

The implementer's Spec deviation §3 rationale (avoiding two Inspector slots pointing at the same GameObject) is sound — using the existing `bottomNavPanel` SerializeField is the right call. The alias method itself is the only cost: one more entry in IntelliSense for an action that's already expressible. Not a problem to land, but worth a `// TODO: collapse SetBottomNavVisible into ShowBottomNav after Stage D / consolidate naming` note for the audit P1 follow-up sweep. Not gating; the call site (`GameplaySceneLoader.ApplyPreloadSetup` line 73) is self-documenting either way.

### D. Risk #5 mitigation — 5s polling vs event-driven handshake?

**Verdict: acceptable for C0, replace with event in Stage D or follow-up.** The 5s `unscaledDeltaTime` poll in `ScanForLoadedHoleSceneAtStartup` (`PhysicsLabController.cs:351-372`) is a defensible short-term solution because:

- The `expectedSceneName` gate (line 367) means the original immediate-fallback behaviour is preserved when GameSession is unseeded (smoke runners, editor flow) — no regression risk.
- The poll cadence is `yield return null` (per-frame), which is the minimum CPU cost for a wait-loop. A typical scene load is sub-second; 5s is generous headroom.
- The poll terminates either on detection or timeout; no infinite loop possible.

**The architectural smell is real but bounded:** the handshake between "GameplaySceneLoader has additively loaded the Hole_NN_Geo scene" and "PhysicsLabController noticed and ran OnHoleLoaded" is implicit through `SceneManager.sceneCount`. A cleaner long-term design is either:

1. `SceneManager.sceneLoaded` event subscription in `PhysicsLabController.Awake`, removing the poll entirely; or
2. `GameplaySceneLoader` calling `PhysicsLabController.Instance?.OnHoleLoaded(sceneName)` directly after `holeOp.isDone`.

Option (2) is closer to the existing editor pattern (`PhysicsLabHolePicker` invokes it directly). Either is a 5-line follow-up. **Not gating C0**, but I recommend filing as a Stage D / cleanup follow-up:

- Reason to defer: C0 ships a working flow with explicit graceful-degradation (the 5s timeout + flat-ground fallback at line 374-375). The poll itself is benign.
- Reason to file: future contributors will read the poll and wonder why it isn't event-driven. The code-comment at lines 340-345 already explains the situation, which softens the smell.

### E. Spec deviation §1 (no runtime LoadHole on PhysicsLabController) — Stage C1/D sharp edge?

**Verdict: self-contained, no sharp edge.** The deviation routes both additive loads through `GameplaySceneLoader` and uses the existing `ScanForLoadedHoleSceneAtStartup` coroutine. This means:

- Stage C1 (ShellScene Result modal subscribing to `GameSession.OnHoleComplete`) is unaffected — the Result modal lives in ShellScene, doesn't care how the hole was loaded.
- Stage D (MENU / RETRY / PLAY NEXT) is well-positioned: `GameplaySceneLoader.UnloadGameplay()` (lines 124-148) already exists as the symmetric teardown, including the bottom-nav restore and unloading both the host and hole scenes in reverse order. A PLAY NEXT handler will simply call `UnloadGameplay()` → re-seed GameSession with the next hole → call `BeginGameplayLoad(nextHole)`. Cleanly composable.
- The poll-then-scan pattern works equally well for re-loads (next-hole-on-PLAY-NEXT) as for first-load — `ScanForLoadedHoleSceneAtStartup` is wired in `Start()` so a fresh LabScaffold load will re-run it.

The only minor sharp edge is that `OnHoleLoaded` is `public` (line 1300) and gets invoked by the coroutine from inside `LabScaffold`. If Stage D wants to bypass the loader and call `OnHoleLoaded` directly (e.g. fast retry of the same hole), it's available, but that's a feature not a defect.

### F. Scene-mutation audit (git diff)

**Verdict: PASS.** `git diff c381a161^ c381a161 -- '*.unity' '*.asset' '*.prefab'` returns ONLY changes to `ProjectSettings/EditorBuildSettings.asset` — the explicit, documented build-settings edit (LabScaffold added, 17 Hole_NN_Geo entries added, ghost Hole_01.unity entry removed, lines 12-66 of the new file). No `.unity`, no `.prefab`, no other `.asset`. No `m_IsActive: 0`, no `sizeDelta`, no GameObject-state changes. The screenshot capture bypass that the self-reviewer flagged (CaptureCore.IsCreated() guard sidestep) did NOT mutate any scene state, confirming the bypass was a read-only RT grab. The risk profile the screenshot rule was written to prevent (iter-12 LabScaffold corruption) did not materialize.

### G. Spec coverage — anything skipped?

**Verdict: full coverage with documented deviations.** All eight DoD greps pass (independently re-verified per self-reviewer); all Files-CREATED / Files-EDITED items in SPEC §Scope are accounted for in IMPLEMENTER_REPORT §Files modified, with five clearly-flagged deviations (all sound — see Decision notes below). Pre-flight items 1-4 are accounted for in the build-settings diff and the merged `Start()` (item 4 / Risk #5). The 5 new EditMode tests are present in `GameplaySceneLoaderTests.cs` and pass (305/305 full suite per implementer's tests-run output).

The one item NOT covered by the implementer (intentionally) is the Cesar visual gate — by SPEC design, that is Cesar's manual playthrough and is correctly held outside implementer scope.

---

## Decision notes on the five flagged spec deviations

| # | Deviation | Verdict |
|---|---|---|
| 1 | No new runtime `LoadHole(int)` on `PhysicsLabController`; route both additive loads through `GameplaySceneLoader` + extend `ScanForLoadedHoleSceneAtStartup` poll | **Approved.** Avoids dual surface area; converges production + editor on the same `OnHoleLoaded(sceneName)` entry. |
| 2 | Tests use reflection to access Assembly-CSharp types | **Approved with reservation.** Asmdef refactor is correctly scoped out of C0. Reflection brittleness (field rename → silent test break at runtime, not compile) is the accepted cost. Adding a single `Assert.IsNotNull` per reflection lookup (as the tests do, lines 50-53, 63, 98, 106, etc.) mitigates the silent-break risk. Long-term: consolidate UI scripts into an asmdef, but that's audit P0/P1 follow-up territory. |
| 3 | `SetBottomNavVisible(bool)` alias rather than new `bottomNavBar` SerializeField | **Approved.** Avoids duplicate Inspector slots. Slight API-surface bloat noted in Check C above. |
| 4 | `FinishLoadingCoroutine` deactivates GameObject rather than navigating to Home | **Approved.** Navigating to Home would hide the additively-loaded gameplay scene. The current behaviour is correct for additive load. |
| 5 | `MatchmakingModalController` calls `Hide()` before `BeginGameplayLoad` with 0.6s beat (`MatchmakingModalController.cs:409-415`) | **Approved.** Reasonable interpretation of SPEC "after fade-out." Beat duration may need tuning in Cesar's visual gate; OQ3 (potential double-fade flicker between modal-Hide and ScreenManager fade-in) is the more substantive concern, surfaced for Cesar. |

---

## Procedural observations

1. **CaptureCore `IsCreated()` guard bypass.** Implementer's screenshot capture path used a one-shot RT read that sidestepped `CaptureCore.SnapGameViewWithLabel`'s `IsCreated()` guard, per IMPLEMENTER_REPORT §Screenshot. The self-reviewer correctly flagged this as a CLAUDE.md rule #6 procedural concern but did not gate on it because (a) the SPEC explicitly de-emphasizes the screenshot's load-bearing role, and (b) the scene-corruption failure mode the rule was written to prevent did NOT materialize (git diff confirms zero scene mutations). I agree with the self-reviewer's call: **NOT gating C0**, but I am noting it explicitly because normalizing the bypass is the risk. The queued backlog item `Docs/Specs/Queued/capture_core_frozen_time_fallback/SPEC.md` is the right home for the proper fix. Cesar may want the implementer to add a one-line acknowledgement in IMPLEMENTER_REPORT.md pointing at that queued ticket so future iterations don't read the bypass as a precedent.

2. **`_opponentScanCoroutine = null` unreachable assignment** (OQ2). Cosmetic. Not gating.

3. **Black-fade flicker between modal-Hide and ScreenManager.ShowScreen(Loading)** (OQ3). This IS Cesar's visual-gate concern — if it surfaces, the fix is `ShowScreen(ScreenId.Loading, instant: true)` in `GameplaySceneLoader.ApplyPreloadSetup`. Flagged for Cesar; not gating implementer.

---

## Verdict rationale

This stage is the first end-to-end production playthrough wiring in the project's history. The mechanical work is solid: all DoD greps pass independently, the architecture split (GameplaySceneLoader for orchestration / PhysicsLabController for binding) is the right seam, the LoadingScreenController generalization doesn't leak C0-specific assumptions into a legacy path, the LabScaffold + 18 Hole_NN_Geo build-settings edit is correct, the 5 EditMode tests exercise real production code paths via reflection (verified independently of self-reviewer's claim), `git diff` confirms zero unwanted scene mutations, and all five Spec deviations are well-reasoned and explicitly surfaced.

The architectural smells (5s poll instead of event-driven handshake; `SetBottomNavVisible` alias; reflection-based tests) are bounded, documented, and have follow-up homes (Stage D / audit P1 sweep / queued capture_core fallback). None of them rises to a gating concern.

The screenshot is not load-bearing per SPEC design — Cesar's visual gate is the canonical proof — and that framing is consistent across SPEC, IMPLEMENTER_REPORT, and SELF_REVIEW. I am not second-guessing that decision.

**Forward to Cesar.**

---

## File summary

| Path | Read |
|---|---|
| `Docs/Specs/Active/loop_v2_c0_matchmaking_to_gameplay_transition/SPEC.md` | yes |
| `Docs/Specs/Active/loop_v2_c0_matchmaking_to_gameplay_transition/IMPLEMENTER_REPORT.md` | yes |
| `Docs/Specs/Active/loop_v2_c0_matchmaking_to_gameplay_transition/SELF_REVIEW.md` | yes |
| `Docs/Specs/Active/loop_v2_c0_matchmaking_to_gameplay_transition/STATUS.md` | yes |
| `Docs/Specs/Active/loop_v2_c0_matchmaking_to_gameplay_transition/screenshots/c0_smoke_editmode_20260519_113948.png` | yes |
| `Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs` | yes |
| `Assets/Scripts/UI/LoadingScreenController.cs` | yes |
| `Assets/Scripts/UI/PersistentUIManager.cs` | yes |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (Start + ScanForLoadedHoleSceneAtStartup) | yes |
| `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` (OpponentScanRoutine end) | yes |
| `Assets/Scripts/UI/Tests/GameplaySceneLoaderTests.cs` | yes |
| `ProjectSettings/EditorBuildSettings.asset` (diff only) | git diff |
