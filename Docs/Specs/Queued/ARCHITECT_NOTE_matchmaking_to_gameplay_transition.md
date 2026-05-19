# Architect Note — Matchmaking → Gameplay transition is missing

**Status:** Backlog / spec authoring needed
**Raised by:** Stage B visual-smoke (loop_v2_b_session_state_plumbing, 2026-05-19)
**Owner:** Architect (next spec round)

## Summary

The matchmaking modal at `Canvas/ScreensRoot/MatchMakingModal` reaches the "OPPONENT FOUND" state correctly, fires `GameSession.SeedSession(...)` (Stage B), and… sits there forever. There is no code path that takes the player from OPPONENT FOUND into actual gameplay. Both PLAY entry points (HomeScreenController + HoleSelectionScreenController) terminate at "modal opens and seeds GameSession" — the user has only the CANCEL button as an exit.

## Evidence

```
$ grep -rln 'GameplayScene\|SceneManager.LoadScene' Assets/Scripts/ | grep -v Tests
Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs   # editor utility only
```

No production code loads `Assets/Scenes/GameplayScene.unity`. The legacy `ShowScreen(ScreenId.Loading)` path in `HomeScreenController.OnPlayClicked` is dead-code (only fires if `matchmakingModal` ref is null) — and even `LoadingScreenController.FinishLoading` just bounces back to `ScreenId.Home`. There is no scene transition wired anywhere in the matchmaking → gameplay flow.

Git archaeology confirms this has never been implemented:

```
$ git log --format='%H %s' -- Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs
1532581b loop_v2_b_session_state_plumbing: fix MatchmakingModal not appearing on PLAY
7de69659 loop_v2_b_session_state_plumbing: GameSession namespace move + OnHoleComplete + Matchmaking seed
661de726 matchmaking_modal: complete       # original — never wired a load transition
```

The matchmaking modal was checked in 2026-05-02 as "complete" but it was only ever a cosmetic stub. The Stage B spec described the smoke check as "ball spawns at tee (existing flow). No regression." — that "existing flow" assumed an end-to-end path that doesn't exist.

## Scope of the missing transition

What needs to happen after `[Stage B] GameSession seeded — Hole=N, ...`:

1. Stop / fade the matchmaking modal.
2. Decide what loading screen (if any) to show. The current `LoadingScreenController` is a fake 2-second bar that bounces back to Home — it does not actually load anything.
3. Either:
   - **(a)** Load `GameplayScene` additively or singly via `SceneManager.LoadSceneAsync`, OR
   - **(b)** Activate a gameplay-shell-screen inside `ShellScene` if Loop v2 is moving toward a single-scene shell pattern (matches the singletons consolidation Stage A pattern).
4. Once gameplay is up: the gameplay code reads `GameSession.CurrentHoleNumber` / `SelectedCharacterId` / `EquippedBagSlot` (Stage B seed) to spawn the right hole/character/bag.

Stage B already supplies the seed. The missing piece is the **transition** (the "what scene/screen do we go to" decision and the loader code).

## Why this matters

- Cesar cannot test Stage B's `OnHoleComplete` event via the production flow because gameplay never starts. The lab path (PhysicsLab + smoke runners) still works for the event test.
- The PLAY button on Home and Hole Selection is functionally a dead-end UX — modal pops, cycles, freezes on OPPONENT FOUND, user must CANCEL.
- Stage C's ShellScene Result modal (consumer of `OnHoleComplete`) is dependent on gameplay actually running. Stage C will hit a wall until this transition is wired.

## Suggested spec hook

This belongs in **Loop v2 Stage C** (already on the roadmap as the ShellScene Result modal stage). Stage C currently focuses on the cup-out → result-modal handoff but it implicitly assumes there's a gameplay loop to handoff FROM. A clean Stage C SPEC needs to specify:

1. The OPPONENT FOUND → gameplay-load transition (what scene, what loader, fade timing).
2. Where `GameplayScene` lives in the asmdef/scene graph (single ShellScene vs. multi-scene additive).
3. Whether `LoadingScreenController` gets ripped out, repurposed, or generalized into the loading shell from the recently-completed `loop_v2_scope` Stage D Part 1 (LoadingScreen generalization).

## What Stage B leaves you

- `GameSession.CurrentHoleNumber` / `SelectedCharacterId` / `EquippedBagSlot` — seeded at OPPONENT FOUND, readable by anyone post-transition.
- `GameSession.OnHoleComplete(HoleCompletionData)` — event fires when ball state hits InCup (via `HoleCompleteDriver`). Stage C's modal subscribes here.

Stage B does not own the transition. Capturing this here so Stage C (or a dedicated transition stage) picks it up.

## Related (in scope for this note)

- **Pre-existing UX bug:** Hole Selection card-tap click bubbling. Originally diagnosed as "card collapses, modal doesn't appear" — the `CardTapButton` was rendering on top of the action button. **FIXED in `1532581b…` follow-up commit (`HoleCardController.Awake` now calls `cardTapButton.transform.SetAsFirstSibling()`).** Documented here so future card-prefab edits don't reintroduce it.
