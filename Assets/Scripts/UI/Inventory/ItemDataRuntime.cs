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

        /// <summary>
        /// Remote art URL for the thumbnail (SPEC §3 — <c>thumbnailUrl</c> column).
        /// Empty means "no remote art". Resolution ladder step 1 (SPEC §2).
        /// </summary>
        public string thumbnailUrl = "";

        /// <summary>
        /// Remote art URL for the full image (SPEC §3 — <c>fullUrl</c> column).
        /// Empty means "no remote art". Resolution ladder step 1 (SPEC §2).
        /// </summary>
        public string fullUrl = "";

        public string proTip              = "";
        public string info                = "";

        /// <summary>
        /// I6 — deactivated, never deleted. False means: gone from the shop, still fully renderable
        /// and usable in the inventory of a player who already holds one.
        /// </summary>
        public bool   isActive            = true;

        // Resolved at load time
        public Sprite? thumbnailSprite;
        public Sprite? fullSprite;

        /// <summary>
        /// content_two_way §4 — <b>can THIS build draw this row?</b> False when the PRIMARY sprite
        /// (<see cref="thumbnailSprite"/>) did not resolve, which is what a row published in the
        /// admin looks like until the build that bundles its art ships. Set once, at load, from the
        /// resolution the loader already performs — never a second <c>Resources.Load</c>.
        /// <para>
        /// It gates the AVAILABLE view only. The <c>GetAll…</c> view still carries the row: a
        /// player granted an item whose art is late must not LOSE it — the save and
        /// <c>InventoryCodec</c> round-trip it untouched; they just cannot see it yet.
        /// </para>
        /// </summary>
        public bool renderable = true;
    }
}
