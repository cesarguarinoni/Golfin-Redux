# SPEC — `controls_i_ball_visual_rotation`

> **Authoritative spec.** Implementer reads this and ONLY this for the work definition. `NOTES.md` is architect context only — read it for background but do not treat its code samples as authoritative; use what's in this SPEC. `STATUS.md` tracks pipeline state.

## Status

`SPEC_READY` 2026-05-12 (architect, JST). Tier 3 Full Pipeline. Visual fidelity work → live human-in-the-loop play-and-confirm required (Lesson O).

## Goal

Make the ball *visually* roll/spin when it's animating along a trajectory. Today `BallAnimator.Update()` writes `transform.position` per frame but never touches `transform.rotation` — so the ball is a textured sphere SLIDING through world space. The GOLFIN logo and dimple normal map stay locked in world orientation while the ball translates, which reads as wrong. This SPEC adds frame-to-frame position-delta–derived rotation (Option A from `NOTES.md`). Option C (full physics-spin integration in `BallSimulation`) is deferred to a follow-up `controls_j_ball_physics_rotation` and is explicitly out of scope here.

## Reference

- **Issue:** `Docs/TellCode.md` OPEN FLAGS entry `[2026-05-08 14:00 JST]`.
- **Notion:** `35a31e0e-9a36-81c0-9fc7-ea47902ef700` (Phase 10 Polish, P2, Order 260). Flip to **In Progress** when picked up; flip to **Done** at end-to-end close.
- **NOTES.md (sibling file):** full architect pre-spec analysis including mobile-perf budget (~2–3 µs/frame, ~0.02% of 60 FPS budget — negligible).
- **Ball prefab (architect-confirmed has visible features):** `Assets/Art/3D/Balls/GolfinBall/Pf_Golfin_Ball.prefab` uses `MAT_Golfin_Ball.mat` → `Golfin_AlbedoTransparency.png` (logo) + `Golfin_Normal.png` (dimple normal map). Rotation WILL be visible.

## Locked open questions (architect, 2026-05-12)

These are the four NOTES.md "open questions". All resolved before SPEC write — do not re-open during implementation; escalate `IMPLEMENTER_BLOCKED` if you disagree.

1. **Apply rotation during Aiming?** NO. `Update()` already early-returns when `!_playing`. Aiming-state ball stays at rest, rotation untouched.
2. **Reset rotation on `SpawnInstance`?** YES. New ball per shot = identity rotation is correct. `_previousPos` is seeded inside `SpawnInstance` after the position write.
3. **Ball has visible features for rotation to show on?** YES — confirmed by architect. Logo + dimple normal map. Ship it.
4. **Test seam.** `Assets/Scripts/Physics/Viewer/AssemblyInfo.cs` already has `[assembly: InternalsVisibleTo("Golfin.Physics.Tests")]`. Use `internal` test seam — DO NOT use public reflection in tests.

## Architecture context

- **Asmdef boundaries affected:** none. Pure `Golfin.Physics.Viewer` change + new file in `Golfin.Physics.Tests`. No new dependencies on either side.
- **Existing code referenced:**
  - `Assets/Scripts/Physics/Viewer/BallAnimator.cs` — file under modification. Class `BallAnimator : MonoBehaviour`.
  - `BallAnimator.SpawnInstance(fp3 startPos)` — seed `_previousPos` here after position write.
  - `BallAnimator.Update()` — apply rotation here after the existing `Vector3.Lerp` position write.
  - `BallAnimator.SnapToEnd()` — re-seed `_previousPos` after snapping (defensive; in practice `_playing` flips false right after so it doesn't matter, but keep the invariant clean).
  - `Assets/Scripts/Physics/Viewer/AssemblyInfo.cs` — existing `InternalsVisibleTo` wiring, no change needed.
- **No managers / singletons / static buses touched.** This is a single-file behavioral change.

## Implementation

### Edit 1 — `Assets/Scripts/Physics/Viewer/BallAnimator.cs`

Add two new private state fields and one constant near the top of the class (alongside the existing `_trajectory` / `_instance` / `_currentSimTime` / `_playing` fields):

```csharp
// §controls_i: ball visual rotation derived from position delta
Vector3 _previousPos;
const float BallRadiusMeters = 0.0215f;  // 43mm diameter golf ball / 2
```

In `SpawnInstance(fp3 startPos)`, AFTER the line `_instance.transform.position = ToVec3(startPos);` add:

```csharp
_instance.transform.rotation = Quaternion.identity;  // §controls_i: reset orientation per shot
_previousPos = _instance.transform.position;          // §controls_i: seed rotation derivation
```

In `Update()`, AFTER the existing position-lerp line `_instance.transform.position = Vector3.Lerp(posA, posB, frac);` add:

```csharp
// §controls_i: derive rotation from position delta. ~2–3 µs/frame on mid-tier mobile.
// Skip when delta is below ~0.1mm (prevents NaN from normalizing zero vector when ball is effectively stationary).
Vector3 currentPos = _instance.transform.position;
Vector3 delta = currentPos - _previousPos;
float deltaMag = delta.magnitude;

if (deltaMag > 0.0001f)
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
```

In `SnapToEnd()`, AFTER the existing `_instance.transform.position = ToVec3(fp);` write add (inside the same `if (_instance != null)` guard):

```csharp
_previousPos = _instance.transform.position;  // §controls_i: keep delta-derivation invariant clean
```

`PlaceAtRest(Vector3 worldPos)` does NOT need a `_previousPos` seed — it calls `SpawnInstance` which already does it.

### Edit 2 — Add internal test seam

`BallAnimator.Update()` is currently `void Update()` (implicitly private, called by Unity). Tests cannot drive it directly. Add an `internal` test seam:

Immediately after the existing `void Update()` body (still inside the `BallAnimator` class), add:

```csharp
// §controls_i: internal seam so EditMode tests can drive a single frame's rotation logic
// without instantiating Unity's runtime Update loop. Mirrors private Update; do NOT call from production code.
internal void DriveUpdateForTests()
{
    if (_instance == null) return;
    // Reuse the same rotation-derivation block as Update; tests will set _instance.transform.position
    // BEFORE calling this so the delta is non-zero.
    Vector3 currentPos = _instance.transform.position;
    Vector3 delta = currentPos - _previousPos;
    float deltaMag = delta.magnitude;
    if (deltaMag > 0.0001f)
    {
        Vector3 axis = Vector3.Cross(delta / deltaMag, Vector3.up);
        float axisMag = axis.magnitude;
        if (axisMag > 0.0001f)
        {
            axis /= axisMag;
            float angleDegrees = (deltaMag / BallRadiusMeters) * Mathf.Rad2Deg;
            _instance.transform.Rotate(axis, angleDegrees, Space.World);
        }
    }
    _previousPos = currentPos;
}

// §controls_i: internal seam to spawn a ball at a known position without a trajectory
// (so tests can drive rotation without setting up a full Trajectory).
internal void SpawnAtForTests(Vector3 worldPos)
    => SpawnInstance(new fp3(fp.FromFloat(worldPos.x), fp.FromFloat(worldPos.y), fp.FromFloat(worldPos.z)));

internal Transform InstanceForTests => _instance == null ? null : _instance.transform;
```

This keeps the production hot-path private/inlined and avoids any test-only branching in `Update()`.

### Edit 3 — `Assets/Scripts/Physics/Tests/BallAnimatorTests.cs` (NEW file)

Two EditMode tests. File namespace: `Golfin.Physics.Tests`. Reference assemblies are already wired (Tests asmdef references `Golfin.Physics.Viewer`).

```csharp
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    public class BallAnimatorTests
    {
        GameObject _go;
        BallAnimator _animator;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("BallAnimator_TestHost");
            _animator = _go.AddComponent<BallAnimator>();
            _animator.SpawnAtForTests(Vector3.zero);
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Update_AppliesRotation_WhenBallTranslatesHorizontally()
        {
            // Arrange: ball spawned at origin, identity rotation.
            var t = _animator.InstanceForTests;
            Assert.IsNotNull(t, "Ball instance should be spawned");
            Assert.AreEqual(Quaternion.identity, t.rotation, "Spawn should reset to identity rotation");

            // Act: translate 1m along +Z, drive one Update frame.
            t.position = new Vector3(0f, 0f, 1f);
            _animator.DriveUpdateForTests();

            // Assert: rotation should be ~2664° about +X axis
            // (1m / 0.0215m radius = ~46.51 rad ≈ 2664.5°), wrapped into a quaternion.
            // We test the angle off identity, not the exact value (quaternion wrap-around makes raw eulers misleading).
            float angle;
            Vector3 axis;
            t.rotation.ToAngleAxis(out angle, out axis);
            Assert.AreNotEqual(0f, angle, 1e-4f, "Rotation must be non-zero after 1m translation");
            // Expected axis is Cross((0,0,1), (0,1,0)) = (-1, 0, 0). Quaternion ToAngleAxis returns a positive angle
            // and may flip the axis sign accordingly; accept either (-1,0,0) at +angle or (+1,0,0) at -angle equivalent.
            Assert.AreEqual(1f, Mathf.Abs(axis.x), 1e-3f, "Rotation axis should be ±X");
            Assert.AreEqual(0f, axis.y, 1e-3f, "Rotation axis Y should be zero");
            Assert.AreEqual(0f, axis.z, 1e-3f, "Rotation axis Z should be zero");
        }

        [Test]
        public void Update_DoesNotRotate_WhenBallStationary()
        {
            // Arrange: ball at origin, identity rotation.
            var t = _animator.InstanceForTests;
            var initialRotation = t.rotation;

            // Act: drive 60 frames of Update with NO position change.
            for (int i = 0; i < 60; i++) _animator.DriveUpdateForTests();

            // Assert: rotation unchanged.
            Assert.AreEqual(initialRotation, t.rotation, "Stationary ball should not accumulate rotation");
        }
    }
}
```

## Mobile perf landmines — do NOT introduce

Architect already verified the chosen approach is ~2–3 µs/frame on mid-tier mobile. The following ARE problems if introduced naïvely; pipeline will reject the report if any of these slip in:

1. **DO NOT** `new` a `Vector3` or `Quaternion` inside the hot path. Use static value-returning methods (`Vector3.Cross`, `Quaternion.AngleAxis`).
2. **DO NOT** call `GetComponent<...>()` per frame. Cache references in `SpawnInstance` if any new ones are needed (none are required by this spec).
3. **DO NOT** normalize a near-zero vector. The `deltaMag > 0.0001f` guard above is load-bearing — keep it.
4. **DO NOT** skip the second guard (`axisMag > 0.0001f`). A purely vertical delta (e.g., ball at apex with zero horizontal velocity) makes `Cross(delta_normalized, Vector3.up)` return zero; normalizing that produces NaN and corrupts the Transform.
5. **DO NOT** add per-frame allocations or `Debug.Log` calls.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. The Implementer cannot mark the task done without filling every line. The self-reviewer will reject any report with unfilled or unjustified checklist items.

- [ ] `BallAnimator.Update()` writes `transform.rotation` (via `transform.Rotate`) every frame the ball is animating and moves >0.1mm horizontally
- [ ] `BallAnimator.SpawnInstance` resets `_instance.transform.rotation = Quaternion.identity` and seeds `_previousPos` AFTER the position write
- [ ] `BallAnimator.SnapToEnd` re-seeds `_previousPos` after the final position write (defensive)
- [ ] New `BallAnimatorTests.cs` file in `Assets/Scripts/Physics/Tests/` with both tests passing in Unity Test Runner (EditMode)
- [ ] Full EditMode test gate run: count = previous count + 2, ALL PASS, 0 IGNORED (or document any pre-existing IGNOREDs)
- [ ] No new GC allocations in `Update()` hot path (verify by code review — no `new Vector3()`, no `new Quaternion()`, no string concat, no `Debug.Log`)
- [ ] Unity Console has no errors related to this task on play-mode entry or during a smoke shot
- [ ] **Visual-fidelity verification (Lesson O) — live play-and-confirm.** Implementer enters Play Mode in `LabScaffold.unity` (or any physics lab scene with a `BallAnimator`), fires a preset Driver shot, watches the ball animate. Records a written content-sanity description: "Ball visibly tumbles/rolls during flight; GOLFIN logo rotates around the ball rather than staying world-locked; on green roll-out, ball rolls in direction of motion." Also fires a putter shot on green and confirms the ball rolls correctly along the green. Description goes in IMPLEMENTER_REPORT.md.
- [ ] **Visual evidence:** at least one screenshot/GIF showing ball mid-flight with the logo at a non-identity orientation. Use `CaptureHelper.SnapAtEndOfFrameAndPause` per project capture rules.
- [ ] All `[SerializeField]` references wired in the Inspector (no scene/prefab changes expected by this spec — flag if anything had to change)
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification

## Files this task touches

- `Assets/Scripts/Physics/Viewer/BallAnimator.cs` — modify (~15 net lines added including the test-seam methods)
- `Assets/Scripts/Physics/Tests/BallAnimatorTests.cs` — NEW (~60 lines, 2 tests)
- No asmdef, scene, prefab, or CSV changes expected

## Smoke evidence

Per **Visual-fidelity verification (Lesson O)** above: combination of (a) the two new EditMode tests for the rotation math, and (b) live human-in-the-loop play-and-confirm with a written content-sanity description plus at least one in-flight screenshot showing the logo at a non-identity orientation. EditMode tests alone are NECESSARY but NOT SUFFICIENT for visual-fidelity work.

## Out of scope (do NOT do these)

- Option C — full physics-derived per-sample quaternion stored in `Trajectory.Sample`. Tracked separately as `controls_j_ball_physics_rotation`; do not add `Quaternion Orientation` to `Trajectory.Sample`, do not modify `BallSimulation.Simulate`.
- Option B — hybrid (read spin from samples during flight). Same reasoning; out of scope.
- Backspin / sidespin visual correctness. Option A intentionally derives rotation from translation, which means a high-backspin driver shot will visually rotate FORWARD instead of backward. This is documented as a known limitation in NOTES.md and is acceptable for first ship per Cesar's lock 2026-05-08.
- Touching `BallSimulation.cs`. Out of scope.
- Touching the ball prefab, its material, its textures, or its scale. Out of scope.
- Touching `BallContext`, populators, or any HUD elements. Out of scope.
- Wiring rotation to a future replay system. Out of scope.

## Escalation paths

- **If `Update()` rotation produces NaN/Infinity on any test shot:** stop, file `IMPLEMENTER_BLOCKED`, paste the offending delta values + Transform state. Most likely cause is one of the two zero-vector guards being miswritten.
- **If the new tests reveal an existing test failure unrelated to this spec:** flag in the report; do NOT fix unrelated failures in this task.
- **If `InternalsVisibleTo` doesn't expose the new internal members at compile time:** verify `Assets/Scripts/Physics/Viewer/AssemblyInfo.cs` has `[assembly: InternalsVisibleTo("Golfin.Physics.Tests")]` and that the Tests asmdef references `Golfin.Physics.Viewer` — both architect-confirmed present 2026-05-12. If still failing, escalate.
- **If the live play-and-confirm shows the ball ROLLING (translating) but the logo still appears world-locked:** the rotation math is being applied to the wrong Transform (likely the `BallAnimator` GameObject itself rather than the spawned `_instance`). Re-check that `_instance.transform.Rotate(...)` is what was called.

## Sequencing

Not on Loop v1 critical path. Concurrent with §2d (Hole-complete + result screen) work happening on the Mac. Closes one P2 polish flag from Phase 10. Companion polish items still open after this lands: OBFreeze framing question, future Option C (`controls_j_ball_physics_rotation`).
