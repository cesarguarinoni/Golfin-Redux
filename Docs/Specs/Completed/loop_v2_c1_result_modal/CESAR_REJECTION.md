# CESAR REJECTION — Iteration 6 redirection (2026-05-21)

Cesar rejected the iteration-5 modal **after it passed architect review**. Reason: the
modal had been reduced to a SINGLE card with PLAY NEXT + MENU buttons. That was wrong.
The production modal must reuse the **FULL lab widget design — BOTH cards** — exactly as
it appears in LabScaffold.

> "The modal should have the same info as in LabScaffold and the Next modal should also
> be there instead of those 2 buttons. The Locked one should appear when Failed (if the
> next hole was never unlocked). The whole design in LabScaffold was correct. I'm not
> sure why it was changed. Check all the info present in the LabScaffold modal."

**This SUPERSEDES the earlier (iteration-4) version of this file and overrides every
"single card / no Card 2" clause in SPEC §1, §2, §6, §7.** The earlier round wrongly
stripped Card 2 — that is reversed here.

## The correct design — the full lab `HoleCompleteWidget` (TWO stacked cards)

- Reference prefab: `Assets/Prefabs/UI/HoleComplete/HoleCompleteWidget.prefab`
- Reference scripts: `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs`
  (parent: `_root`, `_dimBackground`, `_card1`, `_card2`) + `HoleCompleteCardWidget.cs`
- Reference data type: `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteData.cs`
- Reference captures (the design Cesar confirmed CORRECT — replicate verbatim):
  - `Docs/Specs/Completed/loop_v1_2d_hole_complete_and_result_screen/screenshots/iter12_S2_success_unlocked.png`
  - `Docs/Specs/Completed/loop_v1_2d_hole_complete_and_result_screen/screenshots/iter11_S3_failed_over_par.png`

### Card 1 — current hole
- Header: green "✓ SUCCESS" or orange "✗ FAILED".
- Subhead: "{Course} Country Club  - Hole {N} - Par {P}".
- Hole-map graphic + stats block: `TEE OFF: {tee}` / `STROKES: {n} ({label})` (green
  `#50C878` success, orange `#D16A47` failed) / `BEST: —` / `TIME: 00:00:00` / `BEST: —`.
  (BEST/TIME stay as placeholders — no PB/time tracking yet, same as the lab.)
- Rewards row: coin / repair / ball.
- Button: REPLAY (success, or failed-with-PB) or RETRY (failed-no-PB).

### Card 2 — next hole
- Header: gold "NEXT" (unlocked) or gray "🔒 LOCKED" (locked).
- Subhead: "{Course} Country Club  - Hole {N+1} - Par {P2}".
- Unlocked: hole-map graphic + next-hole description/tip text + rewards row + gold "PLAY"
  button.
- Locked: header + subhead + dimmed rewards row only — no map, no description, no button
  (short collapsed card, `DarkenOverlay` on).
- **Card 2 LOCKED when:** the hole was FAILED and the next hole was never unlocked
  (`IsFailed && !HoleProgressionService.IsUnlocked(nextHole)`).

## What stays from C1 — the behavior layer (unchanged, verified GOOD)
- ShellScene-resident; subscribes `GameSession.OnHoleComplete`.
- `HoleCompletionBridge`, `BallManager.AddBalls`, `IHoleProgressionStore` + adapter — keep.
- Card 1 REPLAY/RETRY → reload the current hole (C0 path).
- Card 2 PLAY → load the next hole (C0 path); writes hole progression.
- Reward grant on SUCCESS (first-clear `rewards` vs `replayRewards`).
- `HoleCompleteDriver` double-fire strip stays.
- Hole 18 success: there is no Hole 19 → hide Card 2 entirely and fire the
  `ToastController` "COURSE CLEARED!" toast. (If this edge case needs different handling,
  surface it — do not block on it.)

## Approach
Reuse the lab `HoleCompleteWidget.prefab` + `HoleCompleteWidget.cs` +
`HoleCompleteCardWidget.cs` as the production modal VIEW — relocate to the ShellScene
Canvas and drive it from a thin production controller that subscribes to
`GameSession.OnHoleComplete`, assembles a `HoleCompleteData`, calls `Show(...)`, and
routes the REPLAY / PLAY button taps to reload / next-hole + rewards + progression.
**Do NOT author a new layout. Do NOT reduce to one card.** The lab widget IS the design.
