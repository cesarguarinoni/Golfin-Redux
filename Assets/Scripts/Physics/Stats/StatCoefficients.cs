using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public struct StatCoefficients
    {
        public fp ClubPowerPerPoint;            // velocity multiplier per Club Power point (120 pts = +60%)
        public fp ClubAccuracyPerPoint;         // aim cone reduction per Club Accuracy point
        public fp ClubLieResistancePerPoint;    // lie penalty reduction per Club Lie Resistance point

        public fp BallPowerPerPoint;            // velocity multiplier per Ball Power point (±10 pts = ±10%)
        public fp BallReboundPerPoint;          // restitution multiplier per Ball Rebound point
        public fp BallWindCutPerPoint;          // wind drag reduction per Ball Wind Cut point
        public fp BallRollPerPoint;             // rolling resistance reduction per Ball Roll point
        public fp BallSpinPerPoint;             // spin magnitude multiplier per Ball Spin point

        public fp CharStrengthPerPoint;               // overpower forgiveness per Character Strength point
        public fp CharStrengthVelocityPerPoint;       // velocity multiplier per Character Strength point (swing only)
        public fp CharClubControlPerPoint;            // aim cone reduction per Character Club Control point

        public fp PutterControlPerPoint;        // off-center forgiveness per Putter Control point
        public fp PutterAccuracyPerPoint;       // gravity well radius per Putter Accuracy point (assist)
        public fp PutterWeightPerPoint;         // aim cycles per Putter Weight point (UI layer)

        public fp StaminaFloorFraction;         // stamina-modulated stats retain this fraction at zero stamina

        public static StatCoefficients Default => new StatCoefficients
        {
            ClubPowerPerPoint         = fp.FromFloat(0.005f),
            ClubAccuracyPerPoint      = fp.FromFloat(0.0042f),
            ClubLieResistancePerPoint = fp.FromFloat(0.0042f),

            BallPowerPerPoint   = fp.FromFloat(0.01f),
            BallReboundPerPoint = fp.FromFloat(0.01f),
            BallWindCutPerPoint = fp.FromFloat(0.01f),
            BallRollPerPoint    = fp.FromFloat(0.01f),
            BallSpinPerPoint    = fp.FromFloat(0.01f),

            CharStrengthPerPoint          = fp.FromFloat(0.00625f),
            CharStrengthVelocityPerPoint  = fp.FromFloat(0.004f),    // NOTE F7 (2026-05-25): Strength→velocity coupling
            CharClubControlPerPoint       = fp.FromFloat(0.0035f),

            PutterControlPerPoint  = fp.FromFloat(0.0042f),
            PutterAccuracyPerPoint = fp.FromFloat(0.0075f),
            PutterWeightPerPoint   = fp.FromFloat(0.125f),

            StaminaFloorFraction = fp.FromFloat(0.20f),
        };
    }
}
