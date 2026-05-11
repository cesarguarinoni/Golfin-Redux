using Golfin.Physics.Math;

namespace Golfin.Gameplay.Loop
{
    /// <summary>
    /// Cup detector backed by a fixed pin world position. Constructed once per hole
    /// load; pin position captured at construction and not re-read.
    /// Determinism rules: pure fp math, no Unity API calls, no Time/Random.
    /// Assembly: Golfin.Gameplay.Loop has noEngineReferences=true — no Vector3 here.
    /// Callers in Unity assemblies convert Vector3→fp3 before constructing.
    /// </summary>
    public sealed class RealCupDetector : ICupDetector
    {
        // Regulation cup mouth: 4.25 inch diameter → 0.054 m radius.
        public static readonly fp DefaultCupRadius = fp.FromFloat(0.054f);

        readonly fp3 _pin;
        readonly fp  _cupRadius;

        public RealCupDetector(fp3 pin) : this(pin, DefaultCupRadius) { }

        public RealCupDetector(fp3 pin, fp cupRadius)
        {
            _pin = pin;
            _cupRadius = cupRadius;
        }

        public bool IsInCup(fp3 position, fp ballRadius)
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

        // Test seam — static so tests can call directly without constructing an instance.
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
    }
}
