# SPEC — rp_save_test_isolation (Order 421, P3, Queued)

**Filed:** 2026-06-15 by Architect · **Surfaced by:** Order 349 (`water_splash_fx`) close-out · **Estimate:** XS

## Problem
`WaterSplashCaptureRig` (and potentially other bot/capture rigs) called `RewardPointsManager.SetPoints(999999)` and **flushed it to disk via `SaveDataHost`** to clear Practice's RP gate — without snapshotting the prior value first. The real dev save now permanently reads **RP = 999999**. The original value was never captured, so it can't be restored, only reset.

## Two parts
**1. One-time data reset (Cesar).** Set the dev save's RP to an intended value. Cesar's call on the number — Architect/Code can't know the "right" value.

**2. Durable guard (Code).** The real fix. Capture/bot rigs must not leak test state into the real save. Pick one:
- **Snapshot + restore (minimal):** before any `SetPoints`/save-mutation, read current value; on teardown (incl. failure/exception paths) write it back. Cover any *other* save fields the rigs override, not just RP.
- **Sandbox save profile (cleaner):** point capture/bot runs at an isolated `SaveDataHost` profile so the real save is never touched. Preferred if a save-profile switch already exists or is cheap to add.

## Acceptance
- Running a capture/bot rig leaves the real save's RP (and other touched fields) unchanged afterward.
- Restore path runs even if the capture throws mid-run.
- No gameplay-path behaviour change.

## Notes / leads
- Entry points: `WaterSplashCaptureRig.cs` (`Assets/.../Physics/Viewer/Bot/`), `RewardPointsManager`, `SaveDataHost`. Audit other `*CaptureRig` / `*SmokeBot` rigs for the same flush pattern.
- Related lesson candidate: bot/capture rigs that write through to persisted save state need isolation or snapshot-restore by default.
