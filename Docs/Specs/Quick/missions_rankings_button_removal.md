# Quick — `missions_rankings_button_removal`

Cesar, 2026-08-30: "Remove the rankings button in Missions for now."

## What

The Mission Selection screen (`ShellScene.unity`, the screen `MissionSelectionScreenController`
drives) has a Rankings button top-right of the content area (`rankingsButton`,
`MissionSelectionScreenController.cs:58–59`, wired at `:89`, handler `OnRankingsClicked` `:109–116`
which opens the "coming soon" Rankings flow). Mission leaderboards are not planned for now, so the
button goes.

## Do

1. In `ShellScene.unity`, **disable** the Rankings button GameObject on the Mission Selection screen
   (set inactive — do not delete it, so the layout and the serialized reference survive for when
   leaderboards come back). Nothing else in the scene moves.
2. `MissionSelectionScreenController`: keep the field and the handler; guard the
   `AddListener` at `:89` with the existing null check (already there) and add nothing. No code
   change is needed if the object is inactive — verify the screen boots with no
   `NullReferenceException` and no warning from the wiring.
3. Hole Selection's own leaderboard button is untouched.

## Verify

- Mission Selection opens (from the Home carousel and from Mode Select) with no Rankings button
  visible, EN and JA; Back still returns to the opening screen; daily card + tier tabs unchanged.
- Console clean on entering and leaving the screen.
- Scene diff is limited to the one `m_IsActive` flip (quote `git diff --stat`).

No strings added, no CSV touched.
