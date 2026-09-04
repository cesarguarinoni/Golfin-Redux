# Quick — GPS nav bar: light the selected tab

**Asked for (Cesar, 2026-09-03):** *"The selected tab in the bottom nav bar should be colored in the
same way the one in Game is."* Sibling of `gps_navbar_bottom_anchor.md`, same device pass.

## What Game does

`PersistentUIManager.UpdateScreenHighlight()` tints one `Image` per nav slot:
`iconActiveColor` for the current screen, `iconNormalColor` for the rest. ShellScene serializes
those as white and cyan — cyan is the code default and has never been overridden, which is worth a
look one day, but it IS the shipped Game colour, so GPS reads the same two fields off the live
`PersistentUIManager` rather than hardcoding a second palette.

## What GPS does now

`Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs` — one component per GPS screen prefab, applied in
`OnEnable`, mapping screen → slot:

| Screen | Lit slot |
|---|---|
| `GpsHubScreen` | `NavHomeButton` |
| `ScoreUploadScreen` | `NavCameraButton` |
| `GpsGiftScreen` | `NavGiftButton` |
| `GpsProfileScreen` / `GpsBadgesScreen` / `GpsAvatarScreen` | `NavProfileButton` |
| everything else (e.g. `GpsVoteScreen`) | nothing lit |

`GpsVoteScreen` carries the bar but is not a nav destination, so it lights nothing — that is the
intended reading of "no tab is selected", not a gap.

`NavRoundsButton` never lights today because nothing routes to it yet; `gps_checkin` gives it a
destination, and the row is one line in `SlotFor` when it lands.

## The one deviation from Game

Game's `homeIcon` is a glyph-only `Image` sitting inside a separate ring frame, so only the glyph
turns cyan. Every GPS slot is a SINGLE `Image` whose sprite already contains ring + glyph and has no
children (verified on the prefab: `kids=0` on all five). So the tint takes the whole badge — glyph
cyan, ring teal. Same colour, larger area. Splitting the glyph out would need new art, so this
ships as-is unless Cesar wants the ring left gold.

## Verification

- `GpsNavBarHighlightTests` (5, EditMode) pin the screen→slot map including the deliberate nulls.
- Play-mode pass, `GOLFIN/Gps/Polish Probe — nav tint`: navigates the six reachable screens by real
  `onClick` and reads back all five slots' `Image.color` off the LIVE bar. 6/6 exactly one slot at
  `#00FFFF` and four at `#FFFFFF`; log `Docs/Diagnostics/_capture/gps_polish_run.log`, stills
  `Docs/Specs/Active/gps_polish/screenshots/navtint_*.png`, crop sheet `navtint_bar_sheet.png`.

A component that never ran, or a tint the Button's own `ColorTint` transition swallowed, both look
identical to "it works" in a mapping test — which is why the gate is the rendered colour, not the map.
