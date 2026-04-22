using Golfin.Physics.Math;

namespace Golfin.Physics.Stats
{
    public readonly struct PutterStats
    {
        public readonly int Control;          // 0..120 — off-center forgiveness
        public readonly int Accuracy;         // 0..120 — gravity well radius (assist layer)
        public readonly int Weight;           // 0..120 — aim cycle count
        public readonly int Durability;       // 0..120 (informational)
        public readonly fp  LoftDegrees;
        public readonly fp  BaseVelocityMps;  // putter max velocity at full power gauge

        public PutterStats(int control, int accuracy, int weight, int durability,
                           fp loftDegrees, fp baseVelocityMps)
        {
            Control        = control;
            Accuracy       = accuracy;
            Weight         = weight;
            Durability     = durability;
            LoftDegrees    = loftDegrees;
            BaseVelocityMps = baseVelocityMps;
        }
    }
}
