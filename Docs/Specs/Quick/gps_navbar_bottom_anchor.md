# Quick — GPS nav bar: make it the Game bar

Device-pass finding #1, in two rounds. Sibling of `gps_navbar_selected_tab.md`.

## Round 1 — the bar floated above the bottom edge

`gps_polish` §D9 wrapped the bar in a full-screen `SafeAreaFitter` (baseline 0). That inset the
WHOLE BAR: its bottom edge moved to the top of the home indicator and screen background showed
underneath. Cesar caught it on the first device pass. The fitter came off.

It survived every gate because at the 1170x2532 Editor reference `Screen.safeArea` IS the whole
screen, so the inset is zero and nothing moves. The verification was honest; the design was wrong.

## Round 2 — the replacement stretched the tray

Removing the fitter was right. What replaced it was not: a `GpsNavBarSafeArea` component that GREW
the bar by `Screen.safeArea.y` so the background still reached the edge while the content cleared
the indicator. Cesar, second device pass: *"Nav bar is stretched and does not match figma or game."*

Two faults, and the second is the one that matters:

1. It added a **screen-px** inset to a **canvas-unit** height. At 1290 px wide the canvas scale is
   1.103, so 102 screen px is 92.5 canvas units — it added 102.
2. **It grew the bar at all.** `Bottom Bar Background.png` is 1178x196 with `spriteBorder 0,0,0,0`,
   drawn at `Image.Type.Simple`, and its bottom edge is a soft alpha fade (alpha 227→2 down the
   left column). It cannot be 9-sliced, and a filler strip beneath it would seam. Growing 196→298
   stretched it **52% vertically**. Fixing only fault 1 would still have stretched it 47%.

## What it is now — the Game bar's rule, verbatim

`ShellScene/Canvas/PersistentUI/BottomNavBar` has RectTransform, CanvasRenderer, Image and
**nothing else** — no safe-area handling anywhere. Its geometry:

| | |
|---|---|
| bar | `anchorMin (0,0)`, `anchorMax (1,0)`, `pivot (0.5,0)`, `sizeDelta (0,196)`, `pos (0,0)` |
| the four side slots | fractional anchors **0.0929 / 0.2786 / 0.7197 / 0.9063** at `y=1`, `anchoredPosition (0,-98)`, 156x156 (profile 158) |
| centre slot | anchor `(0.5,0)`, `pos (0,155)`, 238x238 |

GPS already had identical sizes and y-offsets — it was cloned from this bar and then had the
x-anchoring flattened to absolute pixels against a fixed 1178-wide rect. That is what stopped it
adapting. The fractions are restored and the bar stretches, on all eight GPS screens
(`GpsRoundsScreen` included).

## Why it holds on every iPhone

The root `Canvas` scaler is `ScaleWithScreenSize`, reference 1170x2532, `matchWidthOrHeight = 0` —
**match width**. So canvas width is ALWAYS 1170 units and the scale factor is purely
`screenWidth / 1170`. A bar that stretches 0→1 is therefore always exactly one screen wide and
uniformly scaled; slots on fractions land on the same percentages everywhere. Measured, not argued:

| device | canvas W | scale | bar px | sprite fit | slot centres (% of width) |
|---|---|---|---|---|---|
| iPhone SE (3rd) | 1170 | 0.641 | 750x126 | 0.993w / 1.000h | 9.3 · 27.9 · 50.0 · 72.0 · 90.6 |
| iPhone 13 mini | 1170 | 0.923 | 1080x181 | 0.993w / 1.000h | 9.3 · 27.9 · 50.0 · 72.0 · 90.6 |
| iPhone 14 | 1170 | 1.000 | 1170x196 | 0.993w / 1.000h | 9.3 · 27.9 · 50.0 · 72.0 · 90.6 |
| iPhone 15 Pro Max | 1170 | 1.103 | 1290x216 | 0.993w / 1.000h | 9.3 · 27.9 · 50.0 · 72.0 · 90.6 |
| iPhone 16 Pro Max | 1170 | 1.128 | 1320x221 | 0.993w / 1.000h | 9.3 · 27.9 · 50.0 · 72.0 · 90.6 |

`1.000h` on every row is the whole point: the old code's defect was a vertical fit that varied with
the device, which is exactly why the Editor could never see it. The 0.993 width is the 1178-wide
sprite drawn into 1170 units — Game does the same thing, and it is uniform across devices.

## Verification

Live, in one play run (`GOLFIN/Gps/Polish Probe — nav bar vs the Game bar`), both bars read:

```
BAR GAME  sizeDelta=(0.00, 196.00)  renderedPx=1170x196  sprite=Bottom Bar Background
          native=1178x196  type=Simple  vStretch=1.00x  hStretch=0.99x
BAR GPS   sizeDelta=(0.00, 196.00)  renderedPx=1170x196  sprite=Bottom Bar Background
          native=1178x196  type=Simple  vStretch=1.00x  hStretch=0.99x
```

Identical field for field. Side-by-side crop:
`Docs/Specs/Completed/gps_polish/screenshots/navbar_game_vs_gps.png`.

`GpsNavBarSafeArea` and its six tests are deleted — the component was the bug, and Game's answer to
the home indicator is to have no such component.

## The lesson worth keeping

The Editor cannot see a safe-area bug: `Screen.safeArea` is the whole Game View, so every
inset-driven code path is dead there and every gate passes. Anything keyed off `Screen.safeArea`
needs either a device or a pure function fed simulated numbers — and the pure-function version
still would not have caught this one, because the geometry was right and the ART could not take it.
