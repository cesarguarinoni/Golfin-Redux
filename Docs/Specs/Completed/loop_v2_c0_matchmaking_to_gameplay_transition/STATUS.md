DONE

# STATUS — loop_v2_c0_matchmaking_to_gameplay_transition

**Status:** DONE (Cesar, 2026-05-19)
**Type:** TELLCODE — Cesar-visual gate was heavier than typical TELLCODE (first end-to-end production playthrough in project history)
**Parent:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (Stage C splits into C0 + C1)
**Root cause:** `Docs/Specs/Queued/ARCHITECT_NOTE_matchmaking_to_gameplay_transition.md` (Stage B finding)
**Notion:** Loop v2 Order 330 (sub-item of 300)

## History
- 2026-05-19 — Stage B surfaced the missing transition: no production code loads any gameplay scene.
- 2026-05-19 (architect) — SPEC.md written. Three architecture decisions locked: D1 LabScaffold as gameplay host scene, D2 additive load, D3 all 18 holes via existing LoadHole(n).
- 2026-05-19 (implementer) — All audit grep PASS. Build settings: LabScaffold + 18 Hole_NN_Geo added, ghost Hole_01.unity removed. GameplaySceneLoader created. LoadingScreenController generalized with LoadTarget enum. MatchmakingModalController.OpponentScanRoutine hands off to BeginGameplayLoad. PersistentUIManager.SetBottomNavVisible added as alias. PhysicsLabController.Start logs GameSession.CurrentHoleNumber; ScanForLoadedHoleSceneAtStartup now polls 5s for the seeded hole. 5 new EditMode tests pass; full suite 305/305. Compile clean.
- 2026-05-19 (self-reviewer) — FORWARD_TO_ARCHITECT. All mechanical DoD greps confirmed independently. Risk-area traces sound. Open Question 1 (HoleSelection PLAY entry) resolved via grep — holes 2-18 reachable. Scene-mutation audit clean.
- 2026-05-19 (architect-reviewer) — APPROVE_FOR_CESAR. Six cross-cutting checks PASS. Spec deviations §1–§5 all approved.
- 2026-05-19 (Cesar visual gate) — three in-flight fixes applied during playthrough:
  - (1) **LoadingScreen rendered beneath gameplay UI.** ShellScene root Canvas at `sortingOrder=-1` (legacy) lost to LabScaffold's `ShotUI_canvas` at default `sortingOrder=0`. Fixed in code: `LoadingScreenController.Awake` auto-adds Canvas + GraphicRaycaster with `overrideSorting=true, sortingOrder=1000`.
  - (2) **Modal-then-bg fade staging looked wrong.** Reworked to unified FadeController: modal + home backdrop fade together to black, midpoint callback hides modal (under full-black overlay so `_isVisible` resets for re-entry) + swaps to LoadingScreen + hides nav, FadeController fades back from black revealing LoadingScreen.
  - (3) **Scene wiring done via MCP, not paste-for-Cesar.** GameplaySceneLoader added to PersistentUI GameObject + SerializeFields wired + scene saved, all via `script-execute`. New memory `feedback_never_manual_wiring.md` saved durably.
- 2026-05-19 (Cesar) — **DONE.** Approved after final visual playthrough.

## Commits
- `c381a161` — initial code, build settings, tests
- `f0fcbdd7` — scene wiring (MCP) + SELF_REVIEW.md + ARCHITECT_REVIEW.md
- `3e304654` — LoadingScreen Canvas sortOrder=1000 fix
- `99fba3e8` — unified FadeController for modal + home backdrop

## Open follow-ups (queued, non-blocking)
- Replace 5s polling in `PhysicsLabController.ScanForLoadedHoleSceneAtStartup` with `SceneManager.sceneLoaded` event (Stage D / cleanup).
- `capture_core_frozen_time_fallback` ticket already exists for the EditMode capture path issue (CaptureCore's `IsCreated()` guard rejects valid RTs in Unity 6 EditMode).
- Minor: deduplicate `SetRealProgress` / `SetProgress` on LoadingScreenController.
- Audit P1: `SetBottomNavVisible` is an alias for `ShowBottomNav` — consider collapsing the public API surface in a future polish pass.

## Next stage
Stage C1 — ShellScene Result modal (subscribe to `GameSession.OnHoleComplete` from a ShellScene-resident modal, replace the lab `HoleCompleteWidget` for production builds). Then Stage D (MENU / RETRY / PLAY NEXT button handlers + `UnloadGameplay` invocation).
