namespace Golfin.Physics
{
    public enum SurfaceType : byte
    {
        Fairway = 0,    // default for unmarked terrain
        Green,
        GreenCollar,
        Semirough,
        Rough,
        Tee,
        Sand,
        BunkerLip,
        CartPath,
        Water,
        OOB,
    }
}
