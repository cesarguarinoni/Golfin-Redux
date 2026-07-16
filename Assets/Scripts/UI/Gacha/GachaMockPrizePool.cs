// Assets/Scripts/UI/Gacha/GachaMockPrizePool.cs
// gacha_prizes Stage 1 — Static mock prize pool (10 entries; no real ticket spend).
// Uses real club IDs from Assets/Resources/Data/Clubs.csv with VARIED rarities so the
// card frames show as silver / green / blue / gold (matching the Figma node variety).
// Not persisted; rebuilt on every screen open.
#nullable enable

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// One mock prize record — a club reward the player "wins" in this pull.
    /// </summary>
    public readonly struct PrizeRecord
    {
        public readonly string ClubId;
        public PrizeRecord(string clubId) { ClubId = clubId; }
    }

    /// <summary>
    /// Static list of 10 mock prizes with varied rarities for the Prizes screen.
    /// x10 mode uses all 10; x1 mode uses index 0 (the Legendary marquee prize).
    /// </summary>
    public static class GachaMockPrizePool
    {
        // 10 prizes that map to 4 rarities visible in the Figma node render:
        //   Common    (silver frame)  — club_driver_gf,   club_wood_gf
        //   Uncommon  (light frame)   — club_iron9_klyro
        //   Rare      (blue frame)    — club_iron7_mireo
        //   Mythic    (purple frame)  — club_awedge_fyloe
        //   Legendary (gold frame)   — club_pwedge_royal
        //
        // Layout in the 4/4/2 grid (left→right, row by row):
        //   Row 1: Legendary, Mythic, Rare, Rare
        //   Row 2: Common, Common, Common, Common
        //   Row 3 (2, centered): Uncommon, Uncommon
        //
        // Index 0 (Legendary) is also used as the single x1-mode prize.
        private static readonly PrizeRecord[] s_pool = new PrizeRecord[]
        {
            new PrizeRecord("club_pwedge_royal"),    // 0 — Legendary (gold)   ← x1 marquee
            new PrizeRecord("club_awedge_fyloe"),    // 1 — Mythic    (purple)
            new PrizeRecord("club_iron7_mireo"),     // 2 — Rare      (blue)
            new PrizeRecord("club_iron7_mireo"),     // 3 — Rare      (blue)
            new PrizeRecord("club_driver_gf"),       // 4 — Common    (silver)
            new PrizeRecord("club_wood_gf"),         // 5 — Common    (silver)
            new PrizeRecord("club_driver_gf"),       // 6 — Common    (silver)
            new PrizeRecord("club_wood_gf"),         // 7 — Common    (silver)
            new PrizeRecord("club_iron9_klyro"),     // 8 — Uncommon  (light)
            new PrizeRecord("club_iron9_klyro"),     // 9 — Uncommon  (light)
        };

        /// <summary>Returns all 10 mock prize records.</summary>
        public static PrizeRecord[] GetMockPrizes() => s_pool;

        /// <summary>Returns the single marquee prize used in x1 mode.</summary>
        public static PrizeRecord GetX1Prize() => s_pool[0];
    }
}
