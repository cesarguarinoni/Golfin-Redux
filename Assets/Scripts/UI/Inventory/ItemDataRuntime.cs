#nullable enable
using UnityEngine;

namespace Golfin.Inventory
{
    /// <summary>
    /// Runtime data for a single item template loaded from Items.csv.
    /// Mirrors BallDataRuntime pattern.
    /// </summary>
    public class ItemDataRuntime
    {
        public string itemId              = "";
        public string name                = "";
        public string category            = "";   // "RepairKit", future: "Buff", etc.
        public string rarity              = "";   // "Common", "Rare", "Mythic"
        public int    restorePercent      = 0;    // 50, 75, 100
        public string thumbnailSpriteName = "";
        public string fullSpriteName      = "";
        public string proTip              = "";
        public string info                = "";

        // Resolved at load time
        public Sprite? thumbnailSprite;
        public Sprite? fullSprite;
    }
}
