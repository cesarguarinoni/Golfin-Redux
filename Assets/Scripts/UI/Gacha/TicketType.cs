// Assets/Scripts/UI/Gacha/TicketType.cs
// gacha_history Stage 1 — §1 Ticket Type enum
// Enum order frozen: int value is persisted in SaveData.ticketBalances.
// Adding new kinds = APPEND ONLY. Never reorder or remove.

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Identifies which kind of gacha ticket is being read/spent/displayed.
    /// Serialised as int in PersistedTicketBalance — enum ORDER IS FROZEN.
    /// </summary>
    public enum TicketType
    {
        Standard = 0,
        // Future kinds appended here, never re-numbered.
    }
}
