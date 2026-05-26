using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Gameplay.Defaults
{
    // Single seam between gameplay and inventory.
    // BagManager/CharacterManager live in Assembly-CSharp (no custom asmdef).
    // Until promoted, this always returns defaults — gameplay never breaks if inventory is absent.
    public static class DefaultStatProvider
    {
        /// <summary>
        /// Returns a default swing bundle for the given lab club index.
        /// Index mapping matches PhysicsLabController.LabClubs:
        ///   0 = Driver, 1 = Iron7, 2 = Wedge, 3+ falls through to Driver (safety).
        ///
        /// NOTE stat_to_physics_mapping_audit Q3 (2026-05-25): previously this method
        /// always returned ClubStats.DefaultDriver regardless of the selected club, causing
        /// non-driver strokes (wedge approaches, iron shots) to use driver physics and
        /// massively overshoot the target (the "8-stroke seam" in Hole 1 Playthrough bot).
        /// </summary>
        public static StatBundle BuildSwingBundle(int clubIndex = 0)
        {
            ClubStats club = clubIndex switch
            {
                1 => ClubStats.DefaultIron7,
                2 => ClubStats.DefaultWedge,
                _ => ClubStats.DefaultDriver,   // 0 (Driver) and any unknown index → Driver
            };
            return new StatBundle(
                club,
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
