# Quick task — map-view icon must not be on screen during the ball's flight

**Reported by Cesar (2026-08-25):** *"In Multiplayer, the map view icon is still present during the ball's flight."*
**Scope decision (Cesar, same day):** hide it **everywhere** — solo/Practice as well as 1v1. The rest of
the HoleCard (course / hole / par chip stack) still stays up during flight, as before.

## Why it was there

`ShotInProgressUiGate` (added 2026-08-06) hides the shot-input UI from flick-commit until the next shot is
armed. Its original scope call kept the HoleCard map thumbnail **visible but inert** — the Button went
`interactable = false` and `HoleCardWidget.OpenMapView()` early-returns on `ShotInProgress`, but the
thumbnail stayed on screen.

In 1v1 that reads worse than in solo: `VersusHudController.ActivateVersusLayout()` hides the `ChipStack`
and shrinks the HoleCard to 180x180, so during flight the *only* thing left of the card is a lone map
icon floating above the Fade/Draw button — it looks like a live control.

## The change

- `LabScaffold.unity` — `ShotUI_Canvas/ShotInProgressUiGate._hideDuringShot` gains a third entry:
  `ShotUI_Canvas/HoleCard/HoleMapContainer` (fileID 7000007). One-line scene diff; the gate already
  remembers each entry's `activeSelf` and restores it at re-arm, so nothing else changes.
- `ShotInProgressUiGate.cs` / `HoleCardWidget.cs` — doc comments updated (behaviour comments only, no
  logic change). The Button is still flipped inert and the `ShotInProgress` guard stays as belt-and-braces.

No mode branch: one gate, one HoleCard, so solo and 1v1 get identical behaviour.

## Verification

Real 1v1 bot match through the production flow (`GOLFIN/Capture 1v1/Record Full Match Flow`),
Hole 04, 1170x2532 full-res, 54.5 s / 1581 frames.

- **Live play-mode state samples:** `ShotInProgress=True` <-> `HoleMapContainer.activeInHierarchy=False`;
  `ShotInProgress=False` <-> `activeInHierarchy=True`.
- **Per-frame template match** of the icon rect (full-res crop x 930-1110, y 1700-1940, NCC against a
  confirmed icon-present frame, all 1581 frames decoded consecutively) alternates cleanly:
  present while aiming, absent for every flight/resolve window
  (gone 1.7-3.7 s, 7.2-12.3 s, 14.1-18.1 s, 21.8-38.2 s, 43.8-50.0 s, 51.9 s-end).
- **Frames:** f300 (10.0 s, `BALL: Flying`) and f1000 (33.3 s, `BALL: Flying`) show the icon gone;
  f136 (4.5 s) and f1300 (43.3 s, `BALL: Aiming`) show it back with the rest of the shot UI.

Clip: [`Docs/Specs/Quick/_attachments/mapview_icon_hidden_during_flight_1v1.mp4`](../_attachments/mapview_icon_hidden_during_flight_1v1.mp4)
(aiming -> flight -> re-arm, captioned).

Solo was not re-recorded — same component, same GameObject, no `IsVersus` branch anywhere in the gate.

Status: DONE — approved by Cesar 2026-08-25. Clip also copied to `Docs/Reports/Media/mapview_icon_hidden_during_flight_2026-08-25.mp4` for the daily report.
