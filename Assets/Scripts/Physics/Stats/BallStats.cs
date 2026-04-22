namespace Golfin.Physics.Stats
{
    public readonly struct BallStats
    {
        public readonly int Power;    // -10..+10: multiplies into velocityMultiplier
        public readonly int Rebound;  // -10..+10: multiplies surface restitution at bounce
        public readonly int WindCut;  // -10..+10: reduces wind-delta drag (more = less drift)
        public readonly int Roll;     // -10..+10: more = less rolling resistance (farther roll)
        public readonly int Spin;     // -10..+10: multiplies applied spin magnitude

        public BallStats(int power, int rebound, int windCut, int roll, int spin)
        {
            Power   = power;
            Rebound = rebound;
            WindCut = windCut;
            Roll    = roll;
            Spin    = spin;
        }

        public static BallStats Neutral => new BallStats(0, 0, 0, 0, 0);
    }
}
