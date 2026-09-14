# Quick spec — `rankings_list_drag_anywhere`

**Filed:** 2026-09-14, from Cesar: *"In the rankings, in order to scroll the list of players right
now the user has to click exactly on a player and then drag instead of dragging from anywhere in the
list."*

## Problem

`RankingsScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea` is the `ScrollRect`.
A drag reaches it only when the EventSystem raycast lands on a graphic **inside its subtree** —
`ExecuteEvents.GetEventHandler<IDragHandler>` bubbles up from whatever the press hit. In the shipped
prefab nothing in that subtree covered the empty list space:

| Object | Graphic | State |
|---|---|---|
| `ScrollArea` (the ScrollRect) | none | — |
| `ScrollArea/Viewport` | `Image`, `raycastTarget = 1` | **`m_Enabled: 0`** ([RankingsScreen.prefab:1138](../../../Assets/Prefabs/UI/Rankings/RankingsScreen.prefab)) — a disabled Graphic is not in the `GraphicRegistry`, so it never raycasts |
| `RankingsCards` row root / `RankingsCard` body | `Image` | disabled (`RankingsCards.prefab`) |
| row `Rank`, `NameLabel`, `RartityLabel`, `LevelLabel`, `Mask/Portrait`, `RewardPoints/Background`+`Icon`, `Divider` | enabled, raycast on | the ONLY hittable pixels |

So a press on the text / portrait / RP pill works, and a press on the 24 px gap between rows, on the
row's own dark body, or below the last row hits the panel *behind* the list and nothing scrolls.
Measured at HEAD, real boot (StartButton → Home ▸ `LeaderboardButton.onClick`), 30 rows, content
4004 px in a 1050 px viewport:

| Press (screen px, 1170×2532) | Top raycast hit | Resolved `IDragHandler` | `content.y` after drag |
|---|---|---|---|
| gap between rank 4 and rank 5 — `(548, 1430.5)`, 12 px below row 0, 12 px above row 1 | `…/RankingsArea` (the panel behind the list) | **none** | **0 → 0** |
| rank 4's `NameLabel` — `(516, 1505)` | `…/RankingsCards(Clone)/RankingsCard/Name+Level/NameLabel` | `…/ScrollArea` ✓ | 0 → 1309.6 |

`media/rankings_list_drag_anywhere/drag_anywhere_baseline.json` — verdict `FAIL`, control press `PASS`
(the instrument scrolls when the raycast resolves, so the FAIL is the list's, not the harness's).

## Fix

`Assets/Prefabs/UI/Rankings/RankingsScreen.prefab` — the `Viewport`'s existing `Image`, written through
`SerializedObject` on `PrefabUtility.LoadPrefabContents` (trap C1), **2 lines**:

```
m_Enabled: 0 → 1
m_Color: {r: 1, g: 1, b: 1, a: 1} → {r: 1, g: 1, b: 1, a: 0}
```

`raycastTarget` was already 1. Alpha 0 with the Viewport's `CanvasRenderer.cullTransparentMesh = 1`
draws nothing (no draw call, no tint over the panel) and still raycasts — `Graphic.Raycast` does not
look at colour, and the play-mode proof below shows the transparent image IS the top hit. The Viewport
already sits under the rows and the scrollbar in draw order, so nothing that was tappable lost its tap.
No `ShellScene` change: the scene instance carried no override on either property, so it inherits.

Not chosen: a raycast image on `ScrollArea` itself (the Viewport already had the component and is the
list's visible extent, minus the scrollbar strip which owns its own drag), a non-drawing `Graphic`
subclass (a new script for what one existing component does), or enabling the row root's image (would
leave the gaps and the space under the last row dead).

## Evidence (after)

`media/rankings_list_drag_anywhere/drag_anywhere_after.json` — verdict **`PASS`**:

| Press | Top raycast hit | Resolved `IDragHandler` | `content.y` after drag |
|---|---|---|---|
| gap `(548, 1430.5)` | `…/ScrollArea/Viewport` | `…/ScrollArea` ✓ | **0 → 965.3** |
| `NameLabel` `(516, 1505)` | the label | `…/ScrollArea` ✓ | 0 → 967.6 |

- `media/rankings_list_drag_anywhere/rankings_list_drag_anywhere_after.mp4` — 10 s, 1170×2532, real
  boot → StartButton → Home ▸ LeaderboardButton, both presses. The green square is the press point,
  burned in post from the JSON (the game frame is untouched); the list moving under it is the drag.
- `after_gap_press.jpg` / `after_gap_mid.jpg` / `after_gap_after.jpg` — at rest with the marker in the
  gap between rank 4 and rank 5 → three rows moved, marker fixed → seven rows moved after release.
- Consecutive-frame scan of the header band across the clip: max jump 6.95 (a y-flip would be > 40).

## Instrument — `Assets/Scripts/UI/Polish/Editor/ScrollDragAnywhereVerify.cs`

`GOLFIN ▸ Game Polish ▸ Verify — drag-anywhere on the Rankings list (record | no video)`, or
`ScrollDragAnywhereVerify.Launch(label, record)` from `script-execute`. Boots through the real title
gate, taps the real `LeaderboardButton`, closes a first-visit `ScreenHintModal` through its real
CONTINUE / CLOSE (the first baseline run was INCONCLUSIVE because that modal — landed by
`screen_hints` — owned every press), then does what `InputSystemUIInputModule` does minus the OS layer:
`EventSystem.RaycastAll` at the press point → `GetEventHandler<IDragHandler>` from the top hit →
`initializePotentialDrag` / `beginDrag` / 24 × `drag` / `endDrag` to **that** handler only. Nothing
resolved → nothing dispatched → the bug reproduces instead of being papered over. `GamePolishDemoRecorderC`'s
over-scroll clip calls `sr.OnBeginDrag` directly and would PASS a broken list — right for §C2's
subject (movement type), wrong for this one.

The verdict is the JSON (rule 3); the mp4 is for Cesar. Re-pointing at another list is one more
`Target` row (entry button, screen, scroll-rect path, control label path).

Recorder note: motion onset in the frames sat 0.82 s after the logged press (0.857 / 0.789 s on the two
presses), not the 0.40 s `build_bot_video.RECORDER_LEAD` assumes — captions and stills were cut on the
measured offset, and every caption window was checked against a decoded frame (the first cut had the
"Scrolled 965 px" caption outliving the runner's snap-back to the top; the runner now holds the
scrolled frame for the caption's whole window).

## Shape audit (PIPELINE_HARDENING rule 15) — every `ScrollRect`, including the ones that are fine

Static check on the YAML: does the ScrollRect object **or** its Viewport carry an **enabled**
`raycastTarget` graphic? (`Docs/Specs/Quick/media/…` has the script's output; play-mode verified for
rankings only.)

| List | Verdict |
|---|---|
| `RankingsScreen …/Bottom97/ScrollArea` | **FIXED** — the reported one |
| `GeneralShopScreen …/RankingsArea/Modal/Bottom97/ScrollArea` | **SAME SHAPE** — clone of the rankings block, Viewport image disabled |
| `StaminaShopSelectionScreen …/CardsPanel/Modal/Bottom97/ScrollArea` | **SAME SHAPE** — same clone |
| `TournamentSelectionScreen …/Bottom97/ScrollArea` | **SAME SHAPE** — same clone |
| `ShellScene: TournamentLeaderboardScreen …/Bottom97/ScrollArea` | **SAME SHAPE** — same clone, scene-authored |
| `LoginScreen`, `SignUpScreen`, `CreateUsernameScreen`, `ShellScene: ResetPasswordScreen` `…/CardBody/ScrollView` | NO raycast graphic on rect or Viewport — presses between the form fields reach nothing |
| `GpsVoteScreen …/VoteList`, `VenuePickerModal …/List` | NO raycast graphic (Gps/ — SPEC § Untouched territory for the polish tracks) |
| `GachaRatesModal …/BodyScroll`, `LoanModal …/RecipientScroll` | NO raycast graphic |
| `ShellScene: InventoryScreen …/ItemUseModal/…/ScrollArea`, `…/BagsClubModal/…/ScrollArea` | NO raycast graphic |
| `GachaHistoryScreen`, `StoreHistoryScreen` `…/CardsContainer` | fine — Viewport image enabled (Mask host) |
| `RosterScreen/CarouselSection/ScrollView` | fine — Viewport image enabled |
| `ModeSelection`, `MissionSelection`, `HoleSelection`, `TournamentHoleSelection` `CardsScrollView` | fine — image on the rect AND the Viewport |
| Inventory `Club` / `Ball` / `Item` / `Bags` carousels | fine — image on the rect (Viewport's is disabled, the rect's covers it) |

Not swept here: the request named the rankings; the Shop prefab is in another session's working tree
right now, and five of the rows above live in other screens' prefabs / the scene. The four clones
of the rankings block are the same two-line change each; the eight "no graphic" lists need the same
instrument pointed at them first, because a list whose rows are full-width raycast images (a form)
only has dead gaps, not a dead body.

## Files

| File | Change |
|---|---|
| `Assets/Prefabs/UI/Rankings/RankingsScreen.prefab` | Viewport `Image` enabled, colour alpha 0 (2 lines) |
| `Assets/Scripts/UI/Polish/Editor/ScrollDragAnywhereVerify.cs` (+ `.meta`) | new — the raycast-resolved drag instrument + recorder |
| `Docs/Specs/Quick/media/rankings_list_drag_anywhere/` | baseline + after verdict JSON/logs, captioned clip, six stills, caption sidecars |
