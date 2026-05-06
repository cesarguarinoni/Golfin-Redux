using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Loop
{
    public readonly struct BallStateChange
    {
        public readonly BallState  Previous;
        public readonly BallState  Next;
        public readonly fp3        Position;   // ball position at the transition
        public readonly SurfaceType Surface;   // surface under the ball at the transition
        public readonly OBReason?  OBReason;   // populated only when Next == OB
        public readonly fp         SimTime;    // trajectory time at transition (fp.Zero for Aiming)

        public BallStateChange(BallState previous, BallState next, fp3 position,
                               SurfaceType surface, OBReason? obReason, fp simTime)
        {
            Previous = previous;
            Next     = next;
            Position = position;
            Surface  = surface;
            OBReason = obReason;
            SimTime  = simTime;
        }
    }
}
