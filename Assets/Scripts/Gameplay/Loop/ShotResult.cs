using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Loop
{
    public readonly struct ShotResult
    {
        public readonly BallState   TerminalState;  // AtRest, InCup, or OB
        public readonly OBReason?   OBReason;       // populated only when TerminalState == OB
        public readonly fp3         StartPosition;  // origin of the shot
        public readonly fp3         EndPosition;    // resting position (or in-cup position)
        public readonly SurfaceType StartSurface;
        public readonly SurfaceType EndSurface;
        public readonly fp          SimDuration;
        public readonly int         BounceCount;    // number of non-stop terrainHits before terminal

        public ShotResult(BallState terminalState, OBReason? obReason,
                          fp3 startPosition, fp3 endPosition,
                          SurfaceType startSurface, SurfaceType endSurface,
                          fp simDuration, int bounceCount)
        {
            TerminalState = terminalState;
            OBReason      = obReason;
            StartPosition = startPosition;
            EndPosition   = endPosition;
            StartSurface  = startSurface;
            EndSurface    = endSurface;
            SimDuration   = simDuration;
            BounceCount   = bounceCount;
        }
    }
}
