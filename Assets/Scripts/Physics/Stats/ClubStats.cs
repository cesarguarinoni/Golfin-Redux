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
