using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public struct StatCaps
    {
        public fp VelocityMultiplierMax;          // 2.0 — Section 8 soft cap
        public fp AimConeReductionMax;            // 0.95 — never less than 5% of base cone
        public fp LieResistanceMax;               // 0.75 — Section 8 hard cap
        public fp OverpowerForgivenessMax;        // 0.75
        public fp StaminaCapMultiplierMax;        // 1.20 — character stamina stat can raise pool up to 120%
        public fp PutterOffCenterForgivenessMax;  // 0.50
        public fp ReboundMultiplierMax;           // 1.20
        public fp ReboundMultiplierMin;           // 0.80
        public fp RollMultiplierMax;              // 1.20
        public fp RollMultiplierMin;              // 0.80
        public fp WindCutMax;                     // 0.30 — wind-delta drag reduced by at most 30%

        public static StatCaps Default => new StatCaps
        {
            VelocityMultiplierMax         = fp.FromFloat(2.0f),
            AimConeReductionMax           = fp.FromFloat(0.95f),
            LieResistanceMax              = fp.FromFloat(0.75f),
            OverpowerForgivenessMax       = fp.FromFloat(0.75f),
            StaminaCapMultiplierMax       = fp.FromFloat(1.20f),
            PutterOffCenterForgivenessMax = fp.FromFloat(0.50f),
            ReboundMultiplierMax          = fp.FromFloat(1.20f),
            ReboundMultiplierMin          = fp.FromFloat(0.80f),
            RollMultiplierMax             = fp.FromFloat(1.20f),
            RollMultiplierMin             = fp.FromFloat(0.80f),
            WindCutMax                    = fp.FromFloat(0.30f),
        };
    }
}
