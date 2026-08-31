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
        // Bridge deck (bridge_transplant, 2026-08-31). Appended as value 11 — never
        // renumber the values above; zones.json stores the enum NAME, but
        // tree/heightmap bakes and SurfaceConfig index by the numeric value.
        Bridge,
    }
}
