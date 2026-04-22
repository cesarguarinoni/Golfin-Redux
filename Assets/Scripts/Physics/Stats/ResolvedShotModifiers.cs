using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    /// <summary>
    /// All resolved per-shot modifiers. Builders consume this to construct ShotInput,
    /// then pass the BallPhysicsModifiers slice into BallSimulation alongside surface configs.
    /// </summary>
    public readonly struct ResolvedShotModifiers
    {
        // Pre-shot — consumed by ShotInputBuilder to construct ShotInput.
        public readonly fp VelocityMultiplier;       // 1.0 = base; 2.0 = max
        public readonly fp AimConeReductionFraction; // 0.0 = base cone; 0.95 = cone × 0.05
        public readonly fp SpinMagnitudeMultiplier;  // 1.0 = base; ball Spin stat adjusts ±10%

        // Post-shot — consumed during simulation.
        public readonly Golfin.Physics.BallPhysicsModifiers BallPhysics;

        // Informational / assist-layer — resolver outputs but sim does NOT consume.
        public readonly fp  LieResistanceFraction;        // 0..0.75
        public readonly fp  OverpowerForgivenessFraction; // 0..0.75
        public readonly fp  PutterOffCenterForgiveness;   // 0..0.50
        public readonly fp  PutterGravityWellRadiusM;     // 0.10..1.00 m (assist; gameplay layer applies)
        public readonly int PutterAimCycles;              // 5..20 (UI layer applies)

        public ResolvedShotModifiers(
            fp velocityMultiplier, fp aimConeReductionFraction, fp spinMagnitudeMultiplier,
            Golfin.Physics.BallPhysicsModifiers ballPhysics,
            fp lieResistanceFraction, fp overpowerForgivenessFraction,
            fp putterOffCenterForgiveness, fp putterGravityWellRadiusM, int putterAimCycles)
        {
            VelocityMultiplier       = velocityMultiplier;
            AimConeReductionFraction = aimConeReductionFraction;
            SpinMagnitudeMultiplier  = spinMagnitudeMultiplier;
            BallPhysics              = ballPhysics;
            LieResistanceFraction        = lieResistanceFraction;
            OverpowerForgivenessFraction = overpowerForgivenessFraction;
            PutterOffCenterForgiveness   = putterOffCenterForgiveness;
            PutterGravityWellRadiusM     = putterGravityWellRadiusM;
            PutterAimCycles              = putterAimCycles;
        }
    }
}
