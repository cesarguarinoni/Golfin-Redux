using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public struct StatCaps
    {
        public fp VelocityMultiplierMax;          // 2.6 — Section 8 soft cap (raised from 2.0 in F7, 2026-05-25; see Default below)
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
            // NOTE F7 (2026-05-25): raised from 2.0 to 2.6 (~30% headroom over prior cap) so
            // a Supreme-maxed triple-product (Club.Power=120, Ball.Power=+10, Char.Strength=50)
            // does not saturate the cap and erase the HIGH vs LOW delta.
            // Before: 2.0. After: 2.6. Full lane audit at Docs/Specs/Queued/stat_to_physics_mapping_audit/SPEC.md
            VelocityMultiplierMax         = fp.FromFloat(2.6f),
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
