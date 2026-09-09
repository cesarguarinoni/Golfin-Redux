// Assets/Scripts/UI/Shop/StoreHistoryRow.cs
// store_history §4 — binds one StoreHistoryRecord into StoreHistoryRow.prefab.
//
// The prefab is a duplicate of GachaHistoryRow.prefab, so the hierarchy — Col1's nested
// BagClubCard, Col2's six MetaLines, Col3's ticket chip — is identical. It is NOT rebuilt; only
// bound, and only two things differ from the gacha row:
//
//   * COL1 GOES THROUGH GachaPrizeCardBinder, not through BagClubCard.Initialize directly. A
//     purchase can be a club, a ball, a character, an item or a ticket, and that binder is the one
//     piece of code that already draws all five on one card shell (the reveal modal, the Prizes
//     grid and the gacha log all call it). Hand-binding a second copy here is how the store log
//     and the gacha log end up drawing the same club differently.
//   * COL2 IS FIVE LINES, NOT SIX, and Col3 is hidden. The Figma row (13509:3045) has no ticket
//     chip — the price is a text line — and the sixth line has nothing to say about a purchase.
#nullable enable
using System.Globalization;
using GolfinRedux.UI.Gacha;
using Golfin.Inventory;
using Golfin.Roster;
using TMPro;
using UnityEngine;

namespace GolfinRedux.UI.Shop
{
    /// <summary>Binder for one StoreHistoryRow prefab instance. On the prefab root.</summary>
    public class StoreHistoryRow : MonoBehaviour
    {
        [Header("Col1 — Prize card")]
        [SerializeField] private BagClubCard? _clubCard;

        [Header("Col2 — Metadata")]
        [Tooltip("Line_0: name, Line_1: amount, Line_2: acquired, Line_3: source, Line_4: price, Line_5: unused")]
        [SerializeField] private TMP_Text[] _metaLines = System.Array.Empty<TMP_Text>();

        [Header("Col3 — Currency (hidden; the Figma row has no chip)")]
        [SerializeField] private GameObject? _currencyColumn;

        // ── Bind ─────────────────────────────────────────────────────────────────

        public void Bind(StoreHistoryRecord record)
        {
            if (record == null) return;

            BindCard(record);

            SetLine(0, ResolveName(record).ToUpperInvariant());
            SetLine(1, string.Format(LocalizationManager.Get("SHOP_HISTORY_AMOUNT"), record.Amount));
            SetLine(2, string.Format(LocalizationManager.Get("SHOP_HISTORY_ACQUIRED"),
                                     DateOf(record.PurchasedUtc)));
            SetLine(3, string.Format(LocalizationManager.Get("SHOP_HISTORY_SOURCE"),
                                     LocalizationManager.Get("SHOP_HISTORY_SOURCE_STORE")));
            // The number the player PAID (charged_rp), never the list price — so a sale price
            // stays a sale price in the log forever.
            SetLine(4, string.Format(LocalizationManager.Get("SHOP_HISTORY_PRICE"), record.ChargedRp));

            // Line 6 of the cloned row has nothing to say about a purchase. HIDDEN, not blanked:
            // the lines sit in a vertical layout group, so an empty one still takes its height and
            // the block would float above centre.
            HideLine(5);

            if (_currencyColumn != null) _currencyColumn.SetActive(false);
        }

        // ── Col1 ─────────────────────────────────────────────────────────────────

        private void BindCard(StoreHistoryRecord record)
        {
            if (_clubCard == null) { Debug.LogWarning("[StoreHistoryRow] _clubCard not wired."); return; }

            _clubCard.gameObject.SetActive(true);
            GachaPrizeCardBinder.Bind(_clubCard.gameObject,
                new PrizeRecord(KindOf(record.Category), record.RefId, record.Amount,
                                ResolveRarity(record), isDupe: false, dupeRp: 0));
        }

        /// <summary>The <c>PrizeRecord.Kind*</c> constant for a shop category — the strings the
        /// binder dispatches on.</summary>
        private static string KindOf(ShopCategory category) => category switch
        {
            ShopCategory.Ball      => PrizeRecord.KindBall,
            ShopCategory.Character => PrizeRecord.KindCharacter,
            ShopCategory.Item      => PrizeRecord.KindItem,
            ShopCategory.Ticket    => PrizeRecord.KindTicket,
            _                      => PrizeRecord.KindClub,
        };

        /// <summary>
        /// The rarity the card frame is drawn at.
        ///
        /// <para>NO RARITY IS INVENTED for a kind that has none. `golfin_shop_purchases` records no
        /// rarity, so unlike a gacha prize (where the SERVER's rolled tier is on the record) it has
        /// to come from this build's databases — and only clubs, balls and characters have one.
        /// An item or a ticket reports Common, which is what <c>GachaPrizeCardBinder.BindOtherKind</c>
        /// already draws them at on the reveal card and the Prizes grid; matching it is the point.</para>
        /// </summary>
        private static CharacterRarity ResolveRarity(StoreHistoryRecord record)
        {
            switch (record.Category)
            {
                case ShopCategory.Club:
                    return ClubDatabaseCSV.Instance?.GetClub(record.RefId)?.rarity ?? CharacterRarity.Common;
                case ShopCategory.Ball:
                    return BallDatabaseCSV.Instance?.GetBall(record.RefId)?.rarity ?? CharacterRarity.Common;
                case ShopCategory.Character:
                    return CharacterDatabaseCSV.Instance?.GetCharacter(record.RefId)?.rarity ?? CharacterRarity.Common;
                default:
                    return CharacterRarity.Common;
            }
        }

        // ── Col2 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// The purchase's display name, resolved against whichever database owns its category —
        /// the same ladder <c>GachaHistoryScreenController.ResolveName</c> walks. Falls back to the
        /// raw ref id: a purchase this build cannot name is still a purchase the player made, and
        /// the id is more useful in the log than a blank line.
        /// </summary>
        private static string ResolveName(StoreHistoryRecord record)
        {
            switch (record.Category)
            {
                case ShopCategory.Club:
                {
                    var club = ClubDatabaseCSV.Instance?.GetClub(record.RefId);
                    return club != null ? club.name : record.RefId;
                }
                case ShopCategory.Ball:
                {
                    var ball = BallDatabaseCSV.Instance?.GetBall(record.RefId);
                    return ball != null ? ball.name : record.RefId;
                }
                case ShopCategory.Character:
                {
                    var ch = CharacterDatabaseCSV.Instance?.GetCharacter(record.RefId);
                    return ch != null ? ch.characterName : record.RefId;
                }
                case ShopCategory.Item:
                {
                    var item = ItemDatabaseCSV.Instance?.GetItem(record.RefId);
                    return item != null ? item.name : record.RefId;
                }
                case ShopCategory.Ticket:
                {
                    if (int.TryParse(record.RefId, out int id))
                    {
                        var type = TicketTypeCatalog.Get(id);
                        if (type != null) return type.DisplayName;
                    }
                    return record.RefId;
                }
                default:
                    return record.RefId;
            }
        }

        /// <summary><c>yyyy/MM/dd</c> from an ISO-8601 UTC string, or empty when it will not parse.
        /// <c>RoundtripKind</c> so the timestamp is read as the UTC it is rather than reinterpreted
        /// as local wall-clock — the same style the gacha row uses.</summary>
        private static string DateOf(string purchasedUtc)
        {
            if (string.IsNullOrEmpty(purchasedUtc)) return string.Empty;
            return System.DateTime.TryParse(purchasedUtc, CultureInfo.InvariantCulture,
                                            DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToString("yyyy/MM/dd")
                : string.Empty;
        }

        private void SetLine(int index, string text)
        {
            if (index >= _metaLines.Length || _metaLines[index] == null) return;
            _metaLines[index].gameObject.SetActive(true);
            _metaLines[index].text = text;
        }

        private void HideLine(int index)
        {
            if (index >= _metaLines.Length || _metaLines[index] == null) return;
            _metaLines[index].text = string.Empty;
            _metaLines[index].gameObject.SetActive(false);
        }
    }
}
