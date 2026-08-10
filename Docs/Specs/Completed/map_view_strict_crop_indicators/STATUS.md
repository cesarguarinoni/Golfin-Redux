DONE

Cesar approved 2026-08-10 after reviewing `videos/map_view_strict_crop_indicators.mp4`.

Order 355 shipped in `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` (+ EditMode seams in
`Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs`, video recorder in
`Assets/Scripts/UI/Editor/MapViewStrictCropDemoRecorder.cs`). Full EditMode suite green
(1075 passed / 0 failed / 3 pre-existing skips). Verified through the real entry path on Holes 1, 5
and 6; zero `INVARIANT VIOLATION` lines across the session.

Still open, carried forward as on-device checks (see IMPLEMENTER_REPORT §6): the pinch and
two-finger-pan gestures themselves, and a green-side lie visually.
