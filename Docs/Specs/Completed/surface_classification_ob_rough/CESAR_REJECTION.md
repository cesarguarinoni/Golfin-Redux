# CESAR REJECTION — `surface_classification_ob_rough`

**Rejected after `ARCHITECT_REVIEW_PASS`** (2026-07-29). Logged to `.claude/review_misses.log` (miss #3).

## The defect (Stage 1 clip)

In the Stage 1 OB clip (`videos/stage1_ob_after*.mp4`), **the camera bounces back and drops to/under the terrain** when the OOB clamp arms. In the settle frames the bottom ~40% of the frame is a flat, featureless green plane (the `ObGroundSkirt`) with blurry projected tree-shadows floating on it; the real course terrain only starts at the mid-frame horizon. The OB camera clamp is not holding a clean above-ground boundary view — the view sinks onto/below the skirt plane.

Evidence (same-angle full-res extracts from `stage1_ob_after.mp4`):
- `screenshots/CESAR_REJECT_stage1_camera_under_terrain_mid.jpg` (~t=10s)
- `screenshots/CESAR_REJECT_stage1_camera_under_terrain_final.jpg` (~t=12s, at rest)

The `ObBoundaryCaptureMenu` "ob_after" scenario is *supposed* to show "green ground plane, camera holds boundary" — the clip instead reads as the camera clamped below the ground.

## What is NOT wrong

The **classification code is correct and stays** — Stage 1 (`IsObAt` tri-state → OOB) and Stage 2 (`DefaultSurface = Rough`) both passed self-review, review, and the 5-attack red-team, and were independently re-derived from source by the orchestrator (`BakedZoneClassifierTests` 12/12, `RealHoleTerrainTests` 60/60). The Stage 2 before/after clips are fine. Do **not** revisit the classifier, the tests, `VersusBot.cs`, `ZoneData.cs`, `PHYSICS_TUNING_CHANGELOG`, or the Stage 2 clips.

## Root cause & scope (why the ban is lifted)

The camera clamp (`Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs`, the OOB branch near `:246` / `TryFindFirstOBHit` / `ComputeOBFreezePivot`) and the skirt (`Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs`) are **pre-existing** OB-boundary machinery from an earlier task. This SPEC touched none of it. Stage 1 merely made out-of-grid overshoots resolve `OOB` for the first time, which *arms* that clamp on this shot and thereby **surfaced** a latent clamp/skirt-quality bug.

Both files live under `Assets/Scripts/Physics/` — normally under the ZERO-edit ban. **Cesar has explicitly lifted the Physics/ ban for `LoopCameraDirector.cs` and `ObGroundSkirt.cs` for THIS task only**, and authorized fixing the camera here rather than in a separate task.

## Fix required (Stage 1 only)

1. **Diagnose first.** Determine why the OB clamp/skirt view ends up at/under the ground when armed by an out-of-grid OOB hit — the freeze pivot's Y, the skirt plane height, and the camera height/pitch the clamp settles to. Check whether it also happens for a pre-existing mask-hit OOB (in-grid OB) vs only the new out-of-grid path, and say so in the report.
2. **Fix** the clamp (and/or skirt height) so the camera holds a **sane above-ground boundary view** — the boundary/skirt visible ahead, camera above the ground plane, no under-terrain sink or bounce-back. Keep the change minimal and scoped to the clamp/skirt; do not refactor unrelated camera behavior.
3. **Re-shoot the Stage 1 clip** through the real `ObBoundaryCaptureMenu` flow (menu-driven, `screenshot-game-view`/recorder, full 1170×2532, runInBackground). Per Rule 15 (reproduce-the-rejection), include a **same-angle full-res** before/after of the exact settle moment proving the camera no longer sinks under the terrain. **Look at every frame.**
4. Re-confirm the OOB penalty path + provenance are unaffected by any camera change (they should be — camera is presentation only). Re-run `BakedZoneClassifierTests` + `RealHoleTerrainTests` to confirm no regression from touching the viewer.
5. Update `IMPLEMENTER_REPORT.md`: add a `## Rejection follow-up` section with a GONE/RESOLVED verdict on the camera-under-terrain defect + the same-angle re-shoot citation, and add `LoopCameraDirector.cs`/`ObGroundSkirt.cs` to the Files table with the diagnosis.

When done, set STATUS to `READY_FOR_SELF_REVIEW`.
