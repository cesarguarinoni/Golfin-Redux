// Assets/Scripts/UI/Gacha/GachaRewardType.cs
// gacha_history Stage 1 — §2 Reward Type enum
// Identifies what was pulled from a gacha banner pull.
// Club and Ball rows are built this stage; others scaffold for later stages.

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Categorises the type of reward recorded in a GachaHistoryRecord.
    /// Enum order is frozen — int values are NOT persisted (mock data only this stage).
    /// </summary>
    public enum GachaRewardType
    {
        Club,
        Ball,
        Character,
        Item,
        Ticket,
    }
}
