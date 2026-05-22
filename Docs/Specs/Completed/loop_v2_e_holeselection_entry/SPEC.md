# SPEC — `loop_v2_e_holeselection_entry`

**Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Initial: **PART_A_SHIPPED_BY_ARCHITECT / PART_B_SPEC_READY** — Part A (OnReplay fix + tests) was applied surgically by Architect before this SPEC landed. Part B (smoke-bot scenario) is the Implementer's slice.

## Goal

Verify that the Hole-Selection → Matchmaking → Gameplay entry path is correctly wired end-to-end, AND fix the latent REPLAY rewards/progression gap surfaced during Stage E pre-flight: tapping REPLAY (Card 1 on SUCCESS) must write progression and grant rewards the same way PLAY NEXT (Card 2) already does. Without this, a player who picks REPLAY after a first clear silently loses first-clear rewards and Hole 2 stays locked.

## Pre-flight findings (locked 2026-05-22 ~09:00 CET)

| Check | Result |
|---|---|
| Repo HEAD before this stage | `1731e222` (C1 ship) |
| `HoleSelectionScreenController.HandleActionClicked` → `MatchmakingModalController.Open(card.HoleNumber - 1)` | ✓ Line 285. No change needed. |
| Locked-hole gate | ✓ Implicit. Action button lives inside `expandedContainer`; locked cards never expand (`cardTapButton.interactable = false`). Belt-and-suspenders check in `HandleCardTapped`. No change needed. |
| PLAY vs REPLAY label switch | ✓ Mode-driven in `HoleCardController.Bind` — label text, color, and button sprite all swap based on `HoleProgressionService.HasPlayed`. No change needed. |
| `_wasReplay = HasPlayed(...)` ordering in `HandleHoleComplete` | ✓ Captured **before** any progression write. Correct. |
| `instant: true` callers across user-driven paths (Stage F preview) | ✓ Only one: `GameplaySceneLoader.cs:74` — the legit C0 caller. Zero offenders to fix in Stage F. |
| **REPLAY (SUCCESS) writes progression + grants rewards** | ✗ **Gap.** Fixed in Part A below. |

## Part A — REPLAY-writes-progression fix (SHIPPED BY ARCHITECT)

**Pipeline:** SURGICAL (2 lines added + 2 tests added). Already committed by Architect before this SPEC landed.

### Code change

`Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs` → `OnReplay()` now calls `WriteProgressionIfSuccess()` + `GrantRewards()` before `GameSession.ResetForNewHole()`. Same call sequence `OnPlayNext` already uses. Both helpers internally guard on `_lastSuccess`, so FAILED-state defense-in-depth holds.

Top-of-file doc-comment updated to reflect the new contract: "Writes hole progression on PLAY NEXT or REPLAY (SUCCESS only). RETRY (FAILED) reloads the same hole without any progression or reward writes."

### Tests added

`Assets/Scripts/Gameplay/Tests/HoleCompleteModal/HoleCompleteModalControllerTests.cs`:
- `Modal_ReplayOnSuccessWritesProgression` — invokes `OnReplay` on SUCCESS hole=5, asserts `MarkHolePlayed(5)` + `UnlockHole(6)`.
- `Modal_ReplayOnFailedDoesNotWriteProgression` — invokes `OnReplay` on FAILED state, asserts zero progression writes (defense-in-depth — the lab widget binds RETRY not REPLAY on failed, but guard the path).

Both tests `LogAssert.Expect` the GameplaySceneLoader-not-found error, mirroring the existing `Modal_RetryReloadsSameHole` pattern.

### Acceptance for Part A

- [x] `OnReplay` calls `WriteProgressionIfSuccess()` + `GrantRewards()` before reset
- [x] Top-of-file summary doc-comment updated
- [x] Two new tests added, both mirroring established patterns
- [x] No other call-sites changed
- [x] No asmdef changes

## Part B — Smoke-bot scenario (TELLCODE)

**Pipeline:** TELLCODE. Three files touched, all `#if UNITY_EDITOR`-guarded, established pattern per `Docs/Architecture/BOT_FRAMEWORK.md` §7.

### Goal

A scenario that proves the Hole-Selection entry path **and** the Part A fix by going through it end-to-end:

1. Cold launch → Home
2. Navigate to Hole Selection via bottom-nav (`NavTeeButton`)
3. Hole 1 card is auto-expanded; tap PLAY (action button on the expanded card)
4. Matchmaking modal opens → wait for `OpponentFound` → gameplay scenes load
5. `ForceShotComplete("InCup")` → SUCCESS modal appears
6. Capture `result_modal_first_clear`
7. Tap REPLAY (Card 1) → modal dismisses, Hole 1 reloads (progression + rewards now written, per Part A)
8. Wait for `Hole_01_Geo` re-load → capture `gameplay_armed_after_replay`
9. `ForceShotComplete("InCup")` → SUCCESS modal appears again, now with `_wasReplay = true`
10. Capture `result_modal_replay_clear` — visual gate: the rewards row reflects `replayRewards` pool (visibly different from first-clear pool if the data differs)

Final visual gate: Cesar eyeballs the two `result_modal_*` captures side by side. First clear shows `rewards`; second clear shows `replayRewards`.

### Files to add/edit

1. `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` — add new static coroutine `HoleSelectionEntryToReplayRewards(BotDriver d)`. Pattern: copy `Hole1PlayNext` as the structural baseline (it already has the navigate → matchmaking → gameplay → force InCup → click button → wait-for-reload chain). Differences:
   - Enter via `NavTeeButton` → `WaitForScreen("HoleSelection")` → tap the hole card's PLAY button (button name TBD by Implementer; the action button on the expanded HoleCard prefab — likely named `ActionButton` or `PlayActionButton`, grep the prefab or `HoleCardController.cs` to confirm). The existing `HoleSelectionBrowse` scenario waits for `HoleSelection` screen but doesn't tap PLAY — that's the new step.
   - After first SUCCESS, tap **`ReplayButton`** (Card 1 REPLAY; name confirmed in `Hole1Menu` scenario which already taps it).
   - Then a second `ForceShotComplete("InCup")` after the reload.

2. `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` — add `case "hole_selection_entry_to_replay_rewards":` to the dispatch switch in `SafeRun()`.

3. `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` — add `[MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Entry → Replay Rewards")]` + its validate function.

### Architecture notes for Implementer

- HoleCardController's action button GameObject name should be identified via Unity MCP `find` or by reading the HoleCard prefab. The button names used elsewhere in `Scenarios.cs` are `PLAY` (Home), `PlayButton` (Card 2 PLAY in the result widget), `ReplayButton` (Card 1 REPLAY), `RetryButton` (Card 1 RETRY). The hole-card action button is a different surface — name TBD; the SerializeField in `HoleCardController.cs` is `actionButton` (its GO name in the prefab is what we need).
- After tapping the hole card's PLAY, the bot needs to wait for `MatchMakingModal` visibility (same as the `PLAY` shortcut from Home).
- The capture filenames should follow the existing `s01_home`, `s02_hole_selection`, `s03_matchmaking_searching`, … convention.
- `LogAssert.Expect` is NOT needed in scenario code (scenarios are PlayMode bot drives, not NUnit tests).

### Acceptance for Part B

Implementer fills `IMPLEMENTER_REPORT.md`:

- [ ] New scenario coroutine added to `Scenarios.cs` (30–60 lines)
- [ ] Dispatch case added to `LoopV2SmokeBot.SafeRun()`
- [ ] Menu item added to `LoopV2SmokeBotMenu.cs`
- [ ] Scenario runs end-to-end in the editor without timing out; produces all expected captures
- [ ] Both `result_modal_first_clear` and `result_modal_replay_clear` captures exist and are visibly distinct (the rewards row differs between first-clear and replay-clear pools, assuming `HoleData.replayRewards` differs from `HoleData.rewards` for Hole 1; if they're identical in the CSV, log a note and Cesar can decide whether to make them differ for visual gating)
- [ ] `history.log` shows the full flow with no errors
- [ ] No baked bot objects in `ShellScene.unity` (`git diff Assets/Scenes/ShellScene.unity` must be empty)

### Out of scope

- Any change to `HoleSelectionScreenController` (already correct).
- Any change to `HoleCardController` (already correct).
- Any change to `MatchmakingModalController` (already correct).
- Any change to the production result modal logic (Part A is the only behavior change).
- Stage F's `ButtonPressFeedback` component (separate SPEC — `loop_v2_f_button_press_feedback`).

## Definition of done (Stage E overall)

- Part A shipped, two new tests green
- Part B scenario added, runs clean, two distinct captures produced
- Cesar visually confirms the rewards row differs between first-clear and replay-clear captures (or confirms the Hole 1 CSV pools are intentionally identical and a different hole should be used for the visual gate)
- Notion Order flipped to Done, Closed date set
- Commit message for Part A: `loop_v2_e (Part A): OnReplay writes progression + grants rewards on SUCCESS`
- Commit message for Part B: `loop_v2_e (Part B): smoke-bot scenario for Hole Selection → Replay Rewards`
