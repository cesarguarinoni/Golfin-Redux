# NOTES — Ball Visual Rotation

> Architect pre-spec analysis. Captures the issue, options, and mobile-perf considerations. Lock SPEC when ready to ship.

## Status

`Docs/Specs/Queued/controls_i_ball_visual_rotation/` — STATUS=`NOTES_DRAFT` 2026-05-08.

Architect-locked Option A as the first ship per Cesar 2026-05-08 14:00 JST. Option C (full physics-spin) deferred to a follow-up after A lands and is evaluated visually.

## The issue

`BallAnimator` (`Assets/Scripts/Physics/Viewer/BallAnimator.cs`) animates ball flight + roll by lerping `transform.position` between trajectory samples. It NEVER touches `transform.rotation`. Result: the ball is a sphere SLIDING through the world rather than ROLLING. On a smooth-textured ball this is unnoticeable. On any ball with dimples, logos, brand markings, or non-uniform shading, the missing rotation is visually wrong — features stay fixed in world space while the ball translates.

Cesar reported this 2026-05-08 14:00 JST. Confirmed by code inspection: `BallAnimator.Update()` only writes `_instance.transform.position` (line 117), never `transform.rotation`.

The trajectory data DOES carry spin info (`SpinState` in `ShotInput`, used by `AeroModel` for Bearman–Harvey lift physics) but it's not propagated through to per-sample visual orientation, and `BallAnimator` doesn't read it.

## Why this isn't a bug, just polish

Gameplay is unaffected. Physics simulation includes spin correctly. Camera tracking is correct. Distance, carry, and roll computations are all right. The deliverable that's wrong is purely visual fidelity — the ball doesn't LOOK like it's rolling/spinning.

P2 — post-Loop-v1 polish. Not blocking §2c, §2d, §2e, or §2f.

## Three options

### Option A — frame-to-frame position-delta (CHEAPEST, ship first)

Derive rotation from how far the ball moved this frame:
- `deltaPos = currentPos - previousPos`
- `axis = Vector3.Cross(deltaPos, Vector3.up).normalized` (or `Vector3.right` fallback if delta is purely vertical)
- `angularDistance = deltaPos.magnitude / ballRadius` (radians)
- `transform.Rotate(axis, angularDistance * Mathf.Rad2Deg, Space.World)`

**Pros:** ~10 lines added to `BallAnimator.Update()`. Zero new data dependencies. Works for flight AND roll uniformly. Looks correct for ~95% of shots — eye reads "ball tumbling/rolling along trajectory."

**Cons:** Doesn't reflect backspin/sidespin physics. A driver shot with 2686 rpm backspin should visually rotate backward relative to flight direction; with Option A it visually rotates FORWARD (because the rotation is derived from translation direction). Putters on green will look correct (their rotation IS purely from rolling). High-spin chip shots that "stop dead and spin back" will look wrong.

### Option B — hybrid (medium fidelity)

Use Option A during roll states (Rolling, AtRest). During Flying, read `Trajectory.samples[i].spin` (if present) and derive per-sample rotation from spin axis + rate.

**Pros:** Visually correct during flight (backspin visible on drivers, sidespin visible on hooks/slices). Putters still visually correct on rolls. ~25 lines added.

**Cons:** Requires `Trajectory.Sample` to carry spin data per sample, which it MAY OR MAY NOT today (architect needs to verify). If samples don't carry per-frame spin, this option is blocked on a `BallSimulation` change to record per-sample spin, which is much larger scope. Architect leans: do NOT add per-sample spin recording; it's a sim-side change.

### Option C — full physics-derived per-sample quaternion (HIGHEST FIDELITY)

Every trajectory sample carries a `Quaternion` representing the ball's orientation at that time, computed during `BallSimulation.Simulate` by integrating angular velocity over the sim timestep. `BallAnimator.Update()` slerps between sample quaternions.

**Pros:** Visually exact match to the physics. Backspin, sidespin, rolling-on-green all visually correct. Slow-motion replay would look right.

**Cons:** Significantly bigger change. Requires:
- Adding `Quaternion orientation` field to `Trajectory.Sample` (memory cost: 16 bytes per sample × ~200 samples per shot = +3.2 KB per shot, manageable)
- Modifying `BallSimulation.Simulate` to integrate angular velocity per timestep
- Determinism — angular velocity integration must use fixed-point math to match the existing replay-determinism contract
- Tests — new EditMode tests for spin-axis correctness

Estimated effort: 4–6 hours. Worth doing eventually for replay quality but not the first ship.

## Cesar's lock 2026-05-08 14:00 JST

> "We go with A first but move to C later. Make sure the frame-to-frame is not overly costly for mobile."

## Mobile perf analysis (the explicit ask)

`BallAnimator.Update()` runs once per frame while a ball is animating. Adding Option A's rotation code adds the following per-frame cost:

| Operation | Cost on mid-tier mobile (e.g. iPhone 11) |
|---|---|
| `Vector3.Cross(a, b)` | ~6 floating-point multiplies + 3 subtractions. Sub-microsecond. |
| `Vector3.normalized` | One `Sqrt` + 3 divisions. ~50–100 ns on ARM Cortex-A77. |
| `Vector3.magnitude` | One `Sqrt` + 3 multiplies + 2 adds. Same order as normalize. |
| `transform.Rotate(axis, angle, Space.World)` | Internally: builds quaternion (4 ops) + multiplies into existing rotation (16 ops) + writes to native Transform. ~1–2 microseconds. |
| **Total per frame** | **~2–3 microseconds.** |

At 60 FPS that's `2–3 µs × 60 = 120–180 µs per second`, or **~0.02% of frame budget**. Imperceptible.

The cost is bounded by:
1. Only ONE ball is animating at a time in this game (single-player).
2. The rotation code only runs while `_playing == true` (animation active). When the ball is at rest, `Update()` already early-returns at line 79.
3. We're not allocating any GC objects — `Vector3.Cross`, `Vector3.normalized`, etc. are struct-returning value types.

**Conclusion: Option A's mobile cost is negligible.** No GC allocs, sub-microsecond math, only runs during ~3-second shot windows. Even the cheapest target devices (iPhone SE 1st gen, Android 7-era hardware) handle this without measurable frame-time impact.

## Mobile perf landmines to AVOID in implementation

The following ARE problems if introduced naïvely; SPEC will explicitly forbid them:

1. **Don't allocate Vector3 or Quaternion via `new` inside `Update`.** Use the static methods (`Vector3.Cross`, `Quaternion.AngleAxis`) which return value types without GC.
2. **Don't call `_instance.GetComponent<...>()` per frame** to find the visual mesh. Cache the Transform reference on Spawn.
3. **Don't store delta pos as `Vector3`** if the implementer writes `Vector3 prevPos = ...; Vector3 delta = currentPos - prevPos;` that's fine (struct-on-stack). DO NOT box into `object`.
4. **Don't normalize a near-zero vector.** When `deltaPos.magnitude < epsilon` (e.g., ball at rest or moving sub-mm), skip the rotation update entirely. Otherwise `Vector3.Cross` returns near-zero, `normalized` returns NaN, and `transform.Rotate` corrupts orientation.
5. **Don't multiply rotation each frame indefinitely without normalizing.** Quaternion drift accumulates from float error. Re-normalize every ~60 frames OR re-baseline rotation in `SpawnInstance` (which is what we'd do anyway since the ball is destroyed+respawned per shot).

## Implementation outline (Option A, for SPEC)

In `BallAnimator.cs`:

```csharp
// New private field
Vector3 _previousPos;
const float BallRadiusMeters = 0.0215f;  // 43mm diameter golf ball / 2

// Modify SpawnInstance to seed _previousPos
void SpawnInstance(fp3 startPos)
{
    // ... existing code ...
    _instance.transform.position = ToVec3(startPos);
    _previousPos = _instance.transform.position;  // §controls_i: seed for rotation derivation
}

// Modify Update to apply rotation after position write
void Update()
{
    if (!_playing || _trajectory == null || _instance == null) return;
    
    // ... existing position-lerp code unchanged through line `_instance.transform.position = Vector3.Lerp(posA, posB, frac);` ...
    
    // §controls_i: derive rotation from position delta. ~2–3 µs/frame on mid-tier mobile.
    Vector3 currentPos = _instance.transform.position;
    Vector3 delta = currentPos - _previousPos;
    float deltaMag = delta.magnitude;
    
    if (deltaMag > 0.0001f)  // skip if ball barely moved (prevents NaN from normalizing zero vector)
    {
        Vector3 axis = Vector3.Cross(delta / deltaMag, Vector3.up);
        float axisMag = axis.magnitude;
        if (axisMag > 0.0001f)  // skip if delta is purely vertical (axis would be zero)
        {
            axis /= axisMag;
            float angleDegrees = (deltaMag / BallRadiusMeters) * Mathf.Rad2Deg;
            _instance.transform.Rotate(axis, angleDegrees, Space.World);
        }
    }
    _previousPos = currentPos;
}
```

Total: ~12 lines added. No new fields except `_previousPos` (cached Vector3) and one `const float BallRadiusMeters`.

`SnapToEnd()` should reset `_previousPos = _instance.transform.position` so the post-rest "previous frame" is correct if a future call re-enables play — though since we destroy+respawn per shot, this is defensive only.

## Tests

Two EditMode tests:

```csharp
[Test]
public void BallAnimator_Update_AppliesRotationProportionalToTranslation()
{
    // Spawn ball at origin, manually translate it 1m forward over 1 frame, assert rotation is ~2654° (1m / 0.0215m * 180/π)
    // Use reflection or a [SerializeField] hook to simulate translate + invoke Update once.
}

[Test]
public void BallAnimator_Update_DoesNotRotateWhenBallStationary()
{
    // Ball at rest, run 60 frames of Update with no trajectory progress, assert rotation unchanged from spawn.
}
```

Tests are EditMode in `Golfin.Physics.Tests`. Because `BallAnimator.Update()` is private, tests need an internal seam — either `[InternalsVisibleTo]` already wired (yes, line 17 of PhysicsLabController.cs has `assembly: InternalsVisibleTo("Golfin.Physics.Tests")` but BallAnimator's asmdef may differ — verify before SPEC).

## Future Option C planning

When Option C is shipped (post-Loop-v1):
- Add `Quaternion Orientation` field to `Trajectory.Sample`
- Add per-sample spin integration to `BallSimulation.Simulate` using fixed-point quaternion math (need to add `fpQuat` type or use float quaternions with a determinism note — investigate first)
- `BallAnimator.Update()` slerps between sample quaternions (replaces Option A's frame-delta math)
- Determinism tests: 1000-shot replay, all rotations bit-exact across runs

Out of scope for THIS task. File `controls_j_ball_physics_rotation` when ready.

## Cross-references

- **Camera polish queue companion:** the OBFreeze framing forward flag in `Docs/TellCode.md`. Both items live in the post-Loop-v1 visual polish bucket.
- **Camera System Future Design doc:** `Docs/Game Design/CAMERA_SYSTEM_FUTURE_DESIGN.md` — different domain (camera, not ball) but same "polish, not critical-path" framing.

## Open questions to lock before SPEC

1. Should Option A apply during Aiming (ball at rest) too? Currently `Update()` early-returns when `_playing == false`, so Aiming doesn't get rotation updates. Architect lean: NO — ball is at rest, no rotation should change.

2. Should the rotation reset on `SpawnInstance` (i.e., new ball starts with `Quaternion.identity`)? Architect lean: YES — destroy+respawn already happens per shot, identity rotation is fine. New `_previousPos` seeding handles the edge case.

3. Does the ball prefab have any visual features (dimples, brand markings) that REQUIRE this fix to be visible, or is it a smooth-shaded sphere where the fix won't be obvious? **Open** — Cesar to confirm. If smooth sphere, even Option A won't be visible until prefab gets a texture. Worth checking before kickoff so we don't ship A and have it look identical to current.

4. Test seam — is `Golfin.Physics.Tests` asmdef configured with `InternalsVisibleTo` for `Golfin.Physics.Viewer`? If not, the tests need to either use public reflection OR a `internal void UpdateForTests()` test seam.

These four are quick lock items when Cesar fires the SPEC.

## Files this task likely touches

- `Assets/Scripts/Physics/Viewer/BallAnimator.cs` (~12 lines added)
- `Assets/Scripts/Physics/Tests/BallAnimatorTests.cs` (NEW — ~30 lines, 2 tests)
- Possibly `Assets/Scripts/Physics/Tests/Golfin.Physics.Tests.asmdef` (only if `InternalsVisibleTo` isn't already wired)

That's the entire task. SPEC will be short.
