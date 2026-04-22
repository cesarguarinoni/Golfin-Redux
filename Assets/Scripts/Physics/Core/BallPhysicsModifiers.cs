using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Per-shot ball-driven multipliers applied during simulation. Produced by the Stats
    /// resolver (or constructed manually for tests/lab); consumed by BallSimulation at
    /// bounce, roll, and aero phases.
    ///
    /// Default = Neutral = no modification (all multipliers = 1.0, WindCutFraction = 0).
    /// With Neutral, every injection point is a multiply-by-one or subtract-zero — bit-exact
    /// with Phase 1–5 results.
    /// </summary>
    public readonly struct BallPhysicsModifiers
    {
        public readonly fp ReboundMultiplier;        // multiplies SurfaceCoefficients.Restitution at bounce
        public readonly fp RollResistanceMultiplier; // multiplies SurfaceCoefficients.RollingResistance during roll/putt
        public readonly fp WindCutFraction;          // 0..0.30; subtracted from wind scale before aero drag

        public BallPhysicsModifiers(fp reboundMultiplier, fp rollResistanceMultiplier, fp windCutFraction)
        {
            ReboundMultiplier        = reboundMultiplier;
            RollResistanceMultiplier = rollResistanceMultiplier;
            WindCutFraction          = windCutFraction;
        }

        public static BallPhysicsModifiers Neutral => new BallPhysicsModifiers(
            fp.One, fp.One, fp.Zero);
    }
}
