namespace Golfin.Physics.Stats
{
    public readonly struct CharacterStats
    {
        public readonly int Strength;    // 0..120 — overpower forgiveness
        public readonly int ClubControl; // 0..120 — slows aim arrow
        public readonly int Recovery;    // 0..120 — stamina/hour (informational; not used per-shot)
        public readonly int Stamina;     // 0..120 — stamina cap (informational; current stamina passed separately)

        public CharacterStats(int strength, int clubControl, int recovery, int stamina)
        {
            Strength    = strength;
            ClubControl = clubControl;
            Recovery    = recovery;
            Stamina     = stamina;
        }

        public static CharacterStats Neutral => new CharacterStats(0, 0, 0, 0);
    }
}
