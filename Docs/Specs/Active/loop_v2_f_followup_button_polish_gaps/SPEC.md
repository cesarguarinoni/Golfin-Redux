# SPEC — `loop_v2_f_followup_button_polish_gaps`

**Authoritative spec.** Trivial TELLCODE follow-up to Stage F. ≤10 min of Code time. Can wait until after `save_layer_reactive_foundation` ships — not blocking anything.

## Status

**PIPELINE_READY.** TELLCODE. One MCP `add_component`, one doc note.

## Goal

Close two minor gaps surfaced by Stage F's IMPLEMENTER_REPORT (Findings F2 and F3) so the button-polish pass is complete and the dormant set is documented.

## Context

Stage F's IMPLEMENTER_REPORT flagged two architect-side under-specifications:

- **F2 — `NavGachaButton` was omitted from the attach table.** The BottomNavBar has 5 children: `NavHomeButton`, `NavGachaButton`, `NavTeeButton`, `NavInventoryButton`, `NavCharactersButton`. Architect enumerated 4 (Gacha screen is unimplemented, so it was skipped). Code correctly followed the SPEC. For visual consistency across the nav bar, the pulse should be on `NavGachaButton` too.

- **F3 — Three dormant buttons on `HoleCompleteWidget` lack the component.** The widget has 6 button GameObjects (Card1 + Card2 each contain ReplayButton/RetryButton/PlayButton). **Production controller `HoleCompleteModalController.cs:145-150` wires exactly 3 of them** — `Card1.ReplayButton`, `Card1.RetryButton`, `Card2.PlayButton`. The other 3 (`Card1.PlayButton`, `Card2.ReplayButton`, `Card2.RetryButton`) have no `AddListener` call anywhere in production; clicking them is a no-op. **No action needed beyond documenting it.**

## Tasks

**T1 — Attach `Golfin.UI.Polish.ButtonPressFeedback` to `NavGachaButton`.** Via Unity MCP, same procedure as the original Stage F Part B attach. Component defaults (`_pressedScale = 0.95`, `_duration = 0.12`). The target GO path is `PersistentUI/BottomNavBar/NavGachaButton` in `ShellScene.unity`.

**T2 — Document the dormant button set.** Add a paragraph at the top of `Assets/Prefabs/UI/HoleComplete/README.md` (create if absent) noting which buttons are wired vs dormant:

```
HoleCompleteWidget — production button wiring
=============================================

The lab widget has 6 button GameObjects but the production controller
(HoleCompleteModalController) wires only 3:

  WIRED (have ButtonPressFeedback attached):
    - Card1.ReplayButton  → OnReplay  (SUCCESS state)
    - Card1.RetryButton   → OnRetry   (FAILED state)
    - Card2.PlayButton    → OnPlayNext (SUCCESS state)

  DORMANT (no listener wired in production; ButtonPressFeedback NOT attached):
    - Card1.PlayButton
    - Card2.ReplayButton
    - Card2.RetryButton

  Inherited from the lab widget. If any of these are repurposed in
  future, attach ButtonPressFeedback then.
```

## Acceptance

- [ ] `NavGachaButton` has `ButtonPressFeedback` component attached (verify via Frame Debugger or scene-file diff showing the new component reference)
- [ ] `Assets/Prefabs/UI/HoleComplete/README.md` exists with the dormant-button paragraph
- [ ] `git diff Assets/Scenes/ShellScene.unity` shows the new component reference; no other scene mutations
- [ ] No code files touched
- [ ] No `.cs.meta` ghost references (per Lesson R)

## Commit

`loop_v2_f-followup: attach ButtonPressFeedback to NavGachaButton + document dormant HoleComplete buttons`

## Out of scope

- Repurposing the 3 dormant buttons (separate decision when/if any state requires them)
- Adding tactile feedback to non-Button surfaces (sliders, toggles) — separate future polish
- Matchmaking modal buttons (deliberately skipped per Stage F SPEC; the modal's own dismiss flow could conflict with the pulse animation timing)
