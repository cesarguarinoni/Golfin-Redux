# QUEUED (Polish phase) — map_view_aiming polish backlog

**Source:** Cesar, 2026-06-22, at `map_view_aiming` (Order 352) close-out. The feature is DONE/approved; these are deferred polish items to schedule later. None blocking.

Code anchors: `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs`, `Assets/Resources/MapView/MapOverlayConform.shader`.

## 1. Landing zone over trees
The landing-zone blob is a terrain-CONFORMING mesh drawn with `MapView/OverlayConform` (`ZTest Always`), so it always renders on top — including over tree canopies, which can look odd when the landing falls in/behind trees (the blob paints over the canopy rather than tucking under). Decide the intended look (visible but subordinate to canopy / softer blend / depth-aware fade where canopy occludes) and tune so it reads cleanly on fairway, green, AND tree-dense landings — without regressing back to being hidden/clipped under terrain.

## 2. Zoom-out distance (framing tuning)
The map camera zoom (`_initialZoom`, default ~45° FOV; lower = tighter) wants a tuning pass for how far it zooms relative to the shot. Dial the default framing so the shot reads well across hole lengths.

## 3. Bring back the distance bands (rings)
The 80/100/120% power-band rings were commented out (`UpdateConformingRing` calls in `UpdateGuideAndRings` are disabled; the radius math is kept). Re-enable them as proper bands around the landing — translucent, on-terrain, matching the reference — now that the landing/aim model is stable.

## 4. Open-hiccup (map recenters for a frame or two)
Opening map view has a small visual hiccup — the map appears to recenter/settle for 1–2 frames before stabilizing. Likely the camera framing (`PositionMapCamera`) or first-frame marker placement settling. Make the first rendered frame already correct (pre-warm framing before the overlay becomes visible) so the open is clean.
