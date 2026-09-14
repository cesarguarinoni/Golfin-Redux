# Quick spec — `gps_rounds_map_fills_panel`

**Filed:** 2026-09-14, from Cesar's device screenshot of the GPS Rounds tab: *"the google map does
not adapt to the container (sharp corners and does not touch the borders)."*

## Problem

`GpsRoundsBuilder.BuildMapPanel` built the node literally: a 918×420 **Map Surface** at (20, 20)
inside the 958×560 Map Panel, clipped by a `RectMask2D`. Two consequences on a real tile:

1. **Sharp corners.** `RectMask2D` clips to a rectangle; the node's `rounded-[36px]` never
   existed in the build (only the baked *fallback* tile was rounded — the live `RawImage` was not).
2. **A navy gutter.** The 20 px inset put a band of panel fill between the tile and the card's
   ring on every side, and the panel's last 100 px (below the legend) were empty. Read off the
   node (`get_metadata` 14077:33884), the surface is at (0, 0) with **no padding** and is 40 px
   narrower than the panel — the Figma frame is a placeholder drawing, never a layout; the
   `(20,20)` in the gps_checkin SPEC table was an assumption.

## Fix

The map surface is now **the panel's interior**, and the card's ring is drawn **above** it:

| | before | after |
|---|---|---|
| Surface rect (in the 958×560 panel) | (20, 20) 918×420 | (2, 2) **954×494** — one pixel under the ring's opaque core on left / top / right |
| Corners | square (`RectMask2D`) | stencil `Mask` + baked `S_GR_MapMask.png`: top corners **r48** (= 50 − 2), bottom edge straight, alpha hard 0/255 |
| Ring | baked into `S_GR_MapPanel.png`, under the map | `S_GR_MapPanel.png` re-baked **stroke-less**; the stroke is `S_GR_MapPanelRing.png`, the panel's last child, over the map |
| Below the map | 20 gap + 40 legend + 60 empty | a 61 px legend strip: 11 + 40 row + 10 to the ring's inner edge |
| Legend row y | 440 | 507 |
| NEAR ME pill | 16 in from the surface (36 from the ring) | 20 in from the surface |
| Tile request (`/venue/map` w×h) | 918×420 (459×210 @2×) | **954×494** (477×247 @2×) — `GpsRoundsScreenController.MapW/MapH` |
| Fallback tile | 918×420, r36 baked in | 954×494, unrounded (the mask rounds it) |

Why the ring moved above the map — two takes, both measured on 1170×2532 frames:
1. Ring under the map, map inset 3 / r47: the stencil clip is a hard one-pixel staircase and it
   met the bright ring along the arc → a jagged white hairline at both top corners.
2. Ring above, but the mask baked anti-aliased: `_rounded_mask` is Lanczos-downsampled and
   Lanczos **rings** — alpha 1..5 two or three texels outside the arc, which `Mask`'s
   `alpha > 0.001` reads as inside → single dark map pixels poking past the ring at every corner.
   The mask alpha is now thresholded to 0/255; the third take is clean at 16× zoom.

Files:
- `Docs/Scripts/make_gps_rounds_panels.py` — `MAP_SURFACE_*` constants, `STROKE_ON_TOP`
  (stroke-less card + `bake_card_ring`), `bake_map_mask()`, `bake_map_fallback()` re-drawn at the
  surface size with the node's road proportions scaled. Full re-bake is deterministic: only the
  map PNGs changed.
- `GpsRoundsBuilder.BuildMapPanel` — geometry from the controller's `MapW/MapH` (single source of
  the tile size), `Mask` with `showMaskGraphic = false`, `Ring` last child; comments name the scar.
- `GpsRoundsScreenController.MapW/MapH` = 954 × 494; `MapProjection.cs` header updated (the
  projection maths is unchanged — it takes the size as parameters).
- `GpsRoundsScreen.prefab` rebuilt with `GOLFIN ▸ Gps ▸ Build Rounds Screen`. The two modal
  prefabs the builder also writes came out semantically identical (fileID renumbering only) and
  were restored to HEAD; the nested-instance references resolve (verified in the Editor).

Not touched: the backend proxy (`w`/`h` are request parameters; its 918/420 are only defaults),
`MapProjectionTests` (pure-function tests pass literal sizes), the pan/zoom projection.

## Verification

Real navigation in play mode (boot → StartButton → GpsPill → hub → `NavRoundsButton`, first-visit
hints closed through their real `NextButton`), a live 954×494 tile from `/venue/map` on the
Editor session, `GOLFIN ▸ Screenshot ▸ Capture Full Res` at 1170×2532. Measured: tile world rect
(108, 1535)–(1062, 2029) inside panel (106, 1471)–(1064, 2031) → 2 px under the ring on three
sides; legend row at (126, 1484)–(1044, 1524). Corner crops at 16× in the chat record.
