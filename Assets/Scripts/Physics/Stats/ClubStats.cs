using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public readonly struct ClubStats
    {
        public static readonly ClubStats DefaultDriver = new ClubStats(
            power: 50, accuracy: 50, lieResistance: 50, durability: 100,
            loftDegrees: fp.FromFloat(10.9f),
            baseVelocityMps: fp.FromFloat(75f),
            baseBackspinRpm: fp.FromFloat(2686f));

        // NOTE stat_to_physics_mapping_audit (Q3, 2026-05-25):
        // DefaultIron7 and DefaultWedge match PhysicsLabController.LabClubs[1] and [2] verbatim
        // so the FALLBACK path (no player stats armed) uses the same club physics as the lab.
        public static readonly ClubStats DefaultIron7 = new ClubStats(
            power: 50, accuracy: 50, lieResistance: 50, durability: 100,
            loftDegrees: fp.FromFloat(25.5f),
            baseVelocityMps: fp.FromFloat(51f),
            baseBackspinRpm: fp.FromFloat(6500f));

        public static readonly ClubStats DefaultWedge = new ClubStats(
            power: 50, accuracy: 50, lieResistance: 50, durability: 100,
            loftDegrees: fp.FromFloat(41.2f),
            baseVelocityMps: fp.FromFloat(42f),
            baseBackspinRpm: fp.FromFloat(9000f));


        public readonly int Power;            // 0..120 (effective points across all rarities)
        public readonly int Accuracy;         // 0..120
        public readonly int LieResistance;    // 0..120
        public readonly int Durability;       // 0..120 (informational; not used by resolver)
        public readonly fp  LoftDegrees;      // fixed at club instantiation
        public readonly fp  BaseVelocityMps;  // from clubs.csv per club type
        public readonly fp  BaseBackspinRpm;  // from clubs.csv per club type

        public ClubStats(int power, int accuracy, int lieResistance, int durability,
                         fp loftDegrees, fp baseVelocityMps, fp baseBackspinRpm)
        {
            Power          = power;
            Accuracy       = accuracy;
            LieResistance  = lieResistance;
            Durability     = durability;
            LoftDegrees    = loftDegrees;
            BaseVelocityMps  = baseVelocityMps;
            BaseBackspinRpm  = baseBackspinRpm;
        }
    }
}
