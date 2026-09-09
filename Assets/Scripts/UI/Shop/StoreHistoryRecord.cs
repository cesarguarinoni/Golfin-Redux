// Assets/Scripts/UI/Shop/StoreHistoryRecord.cs
// store_history §3 — one shop purchase, as the SERVER recorded it.
//
// ONE RECORD PER PURCHASE, not per granted item. A purchase of a 3-ball bundle is ONE row reading
// "AMOUNT: 3" — unlike the gacha log, which is one row per prize because an x10 pays ten separate
// things. `golfin_shop_purchases` already stores it that way, so no flattening happens anywhere.
//
// Public fields, not properties, deliberately: `GachaHistoryRecord` is shaped the same way and the
// EditMode tests reflect over them.
#nullable enable
using System;

namespace GolfinRedux.UI.Shop
{
    /// <summary>One row of the player's store purchase log.</summary>
    public sealed class StoreHistoryRecord
    {
        /// <summary>Parsed from the server's <c>category</c> string through
        /// <c>GeneralShopModel.ParseCategory</c> — the one parser. An unknown string does not
        /// become a record at all (see <c>StoreHistoryStore.Map</c>): mislabelling a purchase is
        /// worse than omitting it, because a wrong category shows the wrong art and the wrong name.</summary>
        public ShopCategory Category;

        /// <summary>The id inside that category's database — a clubId, ballId, characterId, itemId,
        /// or the ticket type as a decimal string.</summary>
        public string RefId = "";

        /// <summary>The <c>shop_catalog</c> row that was bought.</summary>
        public string EntryId = "";

        /// <summary>How many the bundle contained. 1 for everything unique.</summary>
        public int Amount = 1;

        /// <summary>What the player actually paid. This is the number the PRICE line shows — never
        /// <see cref="ListRp"/>, so a sale price stays a sale price in the log forever.</summary>
        public int ChargedRp;

        /// <summary>The list price at the time of sale. Carried, not rendered — a sale marker is
        /// out of scope (SPEC § Out of scope), and dropping the field would mean re-deriving it
        /// from a catalog that has since changed.</summary>
        public int ListRp;

        /// <summary>True when the purchase was made inside a sale window. Carried, not rendered.</summary>
        public bool OnSale;

        /// <summary>ISO-8601 UTC, VERBATIM off the wire. Parsed for display with
        /// <see cref="System.Globalization.DateTimeStyles.RoundtripKind"/> at the row, never
        /// rewritten in transit.</summary>
        public string PurchasedUtc = "";
    }
}
