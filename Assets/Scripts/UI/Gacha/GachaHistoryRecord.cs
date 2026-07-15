// Assets/Scripts/UI/Gacha/GachaHistoryRecord.cs
// Plain C# DTO — no MonoBehaviour, no Unity deps.
// Fields (not auto-properties) so EditMode tests can locate them via reflection
// (BindingFlags.Public | BindingFlags.Instance on GetField).
#nullable enable

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// One pull-history entry. Newest-first ordering in GachaHistoryStore.
    /// pulledUtc is ISO-8601 UTC string (e.g. "2026-07-14T12:34:56Z").
    /// </summary>
    public class GachaHistoryRecord
    {
        public GachaRewardType RewardType  = GachaRewardType.Club;
        public string          RewardId    = "";   // clubId / ballId / etc.
        public int             Quantity    = 1;    // always 1 for club/character; ball quantity
        public string          BannerId    = "";   // from gacha_banners.csv
        public TicketType      TicketType;
        public int             PullCount   = 1;    // 1 or 10
        public string          PulledUtc   = "";   // ISO-8601

        public GachaHistoryRecord() { }

        public GachaHistoryRecord(
            GachaRewardType rewardType, string rewardId, int quantity,
            string bannerId, TicketType ticketType, int pullCount, string pulledUtc)
        {
            RewardType = rewardType;
            RewardId   = rewardId;
            Quantity   = quantity;
            BannerId   = bannerId;
            TicketType = ticketType;
            PullCount  = pullCount;
            PulledUtc  = pulledUtc;
        }
    }
}
