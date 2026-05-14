# STATUS — `putter_aim_yaw_in_groundlevel`

**Current:** `DONE` (2026-05-14 14:00 JST)

**History:**
- **2026-05-14 14:00 JST** — Cesar Lesson O complete. EditMode tests all green. Putter Aiming camera works identically to iron Aiming. Ball stays at same on-screen vertical position. No wobble on 2nd-putt. Architect-executed close-out.
- **2026-05-14 13:30 JST** — Architect executed Steps 1–4 of SPEC_AMENDMENT directly (skipped implementer pipeline after 5 failed iterations). Net diff: +20 / −53 lines. Zero `SetMode(GroundLevel)` calls remaining in production code.
- **2026-05-14 12:00 JST** — SPEC_AMENDMENT_2026-05-14.md written. Cesar directive: putter Aiming must place 3D ball at same vertical screen position as iron Aiming → ruled out `GroundLevel` mode for Aiming → ruled out `GroundLevel` for putter entirely. Revised L4: putter uses `Mode.Chase` for everything.
- **2026-05-14 09:30 JST → 11:30 JST** — 5 implementer iterations on original SPEC's L4 ("Reuse `ChaseCamera.GroundLevel`"). Each iter added defense-in-depth around a putter-specific divergence. Hard rollback executed at iter-5 close.
- **2026-05-14 09:30 JST** — Original SPEC locked SPEC_READY.

**Resolution:** `loop_v1_2f`'s L4 ("Reuse `ChaseCamera.GroundLevel`") was the root cause. Every putter-specific divergence (`willFlipToPutter`, `isPutt` early-return, `EnterPutterMode.SetMode`, etc.) existed to preserve that decision. Deleting the divergences + the `SetMode` calls + reusing iron's camera path eliminates the wobble class of bugs structurally.

**Files changed (final):**
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — 5 surgical deletions
- `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` — `isPutt` early-return + dead local deleted
- `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` — null-target early-return extended to `{Chase, GroundLevel}` (forward-looking guardrail)
- `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` — 1 test renamed + assertion inverted as regression guard
