using Golfin.Physics.Math;

namespace Golfin.Gameplay.Loop
{
    /// <summary>
    /// Returns true if the given world position lies inside the cup geometry.
    /// MUST be deterministic and side-effect free — called from sim-time scans.
    /// </summary>
    public interface ICupDetector
    {
        bool IsInCup(fp3 position, fp ballRadius);
    }
}
