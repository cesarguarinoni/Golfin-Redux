using Golfin.Physics.Math;

namespace Golfin.Gameplay.Loop
{
    /// <summary>
    /// Cup detector backed by a fixed pin world position. Constructed once per hole
    /// load; pin position captured at construction and not re-read.
    /// Determinism rules: pure fp math, no Unity API calls, no Time/Random.
    /// Assembly: Golfin.Gameplay.Loop has noEngineReferences=true — no Vector3 here.
    /// Callers in Unity assemblies convert Vector3→fp3 before constructing.
    ///
    /// Speed gate (2026-05-18, cup_speed_gated_capture):
    ///   The velocity-aware overload IsInCup(position, ballRadius, velocity) rejects
    ///   captures where ball speed at cup-volume entry exceeds CupCaptureSpeed.
    ///   This implements the USGA lip-out rule: a putt travelling faster than ~5 ft/s
    ///   (≈1.5 m/s) at the cup rim has enough momentum to lip-out rather than drop.
    ///   Source: Penner, A.R. (2002) "The physics of putting." Canadian Journal of
    ///   Physics 80(2): 83–96 (see lip-out analysis).
    ///   Default threshold: 1.5 m/s (architect-locked 2026-05-14 09:30 JST).
    ///
    ///   The legacy overload IsInCup(position, ballRadius) is VELOCITY-BLIND — it
    ///   accepts any geometrically valid overlap. Retained for callers that do not
    ///   have trajectory velocity context; NOT used in the BallStateMachine sample scan.
    /// </summary>
    public sealed class RealCupDetector : ICupDetector
    {
        // Regulation cup mouth: 4.25 inch diameter → 0.054 m radius.
        public static readonly fp DefaultCupRadius = fp.FromFloat(0.054f);

        // Speed gate default: 1.5 m/s.
        // Source: USGA lip-out guidance; Penner, A.R. (2002) "The physics of putting."
        // Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis) —
        // lip-out condition at rim speed ≈5 ft/s = 1.524 m/s. Rounded to 1.5 m/s per
        // architect-locked decision 2026-05-14.
        public static readonly fp DefaultCupCaptureSpeed = fp.FromFloat(1.5f);

        readonly fp3 _pin;
        readonly fp  _cupRadius;
        readonly fp  _cupCaptureSpeedSq; // stored squared to avoid Sqrt in hot path

        public RealCupDetector(fp3 pin) : this(pin, DefaultCupRadius, DefaultCupCaptureSpeed) { }

        public RealCupDetector(fp3 pin, fp cupRadius) : this(pin, cupRadius, DefaultCupCaptureSpeed) { }

        public RealCupDetector(fp3 pin, fp cupRadius, fp cupCaptureSpeed)
        {
            _pin                = pin;
            _cupRadius          = cupRadius;
            _cupCaptureSpeedSq  = cupCaptureSpeed * cupCaptureSpeed;
        }

        // ── Velocity-blind overload (legacy) ──────────────────────────────────────
        // Does NOT apply the speed gate. Retained for callers without velocity context.
        // BallStateMachine sample scan MUST use the velocity-aware overload instead.
        public bool IsInCup(fp3 position, fp ballRadius)
            => IsInCupGeometry(position, ballRadius);

        // ── Velocity-aware overload (speed-gated) ─────────────────────────────────
        // Rejects capture if ball speed exceeds CupCaptureSpeed at the cup rim.
        // v1 trajectory consequence: fly-over (ball continues uninterrupted).
        // Source: Penner, A.R. (2002) "The physics of putting." Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis).
        public bool IsInCup(fp3 position, fp ballRadius, fp3 velocity)
        {
            // Speed gate: reject if speedSq > threshold. Compare squared to avoid Sqrt.
            fp speedSq = velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z;
            if (speedSq > _cupCaptureSpeedSq) return false;
            return IsInCupGeometry(position, ballRadius);
        }

        // ── Geometry check (shared) ───────────────────────────────────────────────
        bool IsInCupGeometry(fp3 position, fp ballRadius)
        {
            // Height gate: ball top must be at or below pin Y (filters flying samples
            // that happen to be over the cup XZ).
            if (position.y > _pin.y + ballRadius) return false;

            fp dx = position.x - _pin.x;
            fp dz = position.z - _pin.z;
            fp distSq = dx * dx + dz * dz;
            fp effRadius = _cupRadius - ballRadius;
            if (effRadius <= fp.Zero) return false; // ball larger than cup
            return distSq < effRadius * effRadius;
        }

        // ── Static test seams ─────────────────────────────────────────────────────

        // Geometry-only static seam (velocity-blind). Used by tests that verify
        // geometry independently of speed gate.
        public static bool IsInCupStatic(fp3 position, fp ballRadius, fp3 pin, fp cupRadius)
        {
            if (position.y > pin.y + ballRadius) return false;
            fp dx = position.x - pin.x;
            fp dz = position.z - pin.z;
            fp distSq = dx * dx + dz * dz;
            fp effRadius = cupRadius - ballRadius;
            if (effRadius <= fp.Zero) return false;
            return distSq < effRadius * effRadius;
        }

        // Velocity-aware static seam. Used by speed-gate tests.
        // cupCaptureSpeed in m/s; comparison done in speedSq space to avoid Sqrt.
        public static bool IsInCupStatic(fp3 position, fp ballRadius, fp3 velocity,
                                         fp3 pin, fp cupRadius, fp cupCaptureSpeed)
        {
            fp speedSq     = velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z;
            fp thresholdSq = cupCaptureSpeed * cupCaptureSpeed;
            if (speedSq > thresholdSq) return false;
            return IsInCupStatic(position, ballRadius, pin, cupRadius);
        }
    }
}
