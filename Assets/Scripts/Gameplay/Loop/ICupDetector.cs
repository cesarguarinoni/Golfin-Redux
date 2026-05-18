using Golfin.Physics.Math;

namespace Golfin.Gameplay.Loop
{
    /// <summary>
    /// Returns true if the given world position lies inside the cup geometry.
    /// MUST be deterministic and side-effect free — called from sim-time scans.
    /// </summary>
    public interface ICupDetector
    {
        /// <summary>
        /// Legacy overload — velocity-unaware. Called by code that does not have velocity context.
        /// </summary>
        bool IsInCup(fp3 position, fp ballRadius);

        /// <summary>
        /// Velocity-aware overload. Speed gate: if the ball's speed exceeds the cup-capture
        /// threshold at the moment of cup-volume entry, the capture is rejected (fly-over).
        /// Callers with trajectory velocity data (e.g. BallStateMachine sample scan) MUST
        /// use this overload to enforce the USGA lip-out speed gate.
        /// </summary>
        bool IsInCup(fp3 position, fp ballRadius, fp3 velocity);
    }
}
