using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Gameplay.Defaults
{
    // Single seam between gameplay and inventory.
    // BagManager/CharacterManager live in Assembly-CSharp (no custom asmdef).
    // Until promoted, this always returns defaults — gameplay never breaks if inventory is absent.
    public static class DefaultStatProvider
    {
        public static StatBundle BuildSwingBundle()
        {
            return new StatBundle(
                ClubStats.DefaultDriver,
                BallStats.Neutral,
                CharacterStats.Neutral,
                fp.FromFloat(100f),
                fp.FromFloat(100f));
        }

        public static StatBundle BuildPuttBundle()
        {
            return new StatBundle(
                PutterStats.DefaultPutter,
                BallStats.Neutral,
                CharacterStats.Neutral,
                fp.FromFloat(100f),
                fp.FromFloat(100f));
        }
    }
}
