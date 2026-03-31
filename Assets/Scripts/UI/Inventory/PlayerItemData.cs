#nullable enable

namespace Golfin.Inventory
{
    /// <summary>
    /// Player-owned instance of an item (quantity-based, no level or equip).
    /// Mirrors PlayerBallData pattern.
    /// </summary>
    public class PlayerItemData
    {
        public string itemId   = "";
        public int    quantity = 0;

        /// <summary>True when quantity is negative (unlimited/infinite stack).</summary>
        public bool IsUnlimited => quantity < 0;
    }
}
