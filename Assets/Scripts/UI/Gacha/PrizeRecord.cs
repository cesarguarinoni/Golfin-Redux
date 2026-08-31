// Assets/Scripts/UI/Gacha/PrizeRecord.cs
// gacha_client_real_pull §4.3 — one prize of one pull, as the SERVER granted it.
//
// This type replaces the `readonly struct { string ClubId }` that came out of GachaMockPrizePool:
// a pull can now pay a ball, a character, an item or a ticket, and it can pay RP instead of a
// duplicate. Every field is the server's word — in particular RARITY, which is NOT re-derived from
// the client's database. A prize whose row was published after this build shipped still reveals at
// the tier it was actually rolled at, and the reveal FX cannot disagree with the pull log.
#nullable enable
using Golfin.Economy;
using Golfin.Roster;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>One prize the player won, in reveal order.</summary>
    public readonly struct PrizeRecord
    {
        /// <summary><c>club</c> | <c>ball</c> | <c>character</c> | <c>item</c> | <c>ticket</c>.</summary>
        public readonly string Kind;

        /// <summary>The id inside that kind's database — a clubId, ballId, itemId, characterId, or
        /// the ticket type as a decimal string.</summary>
        public readonly string RefId;

        public readonly int Quantity;

        /// <summary>The rarity the server rolled. Drives the reveal FX tier and the card frame.</summary>
        public readonly CharacterRarity Rarity;

        /// <summary>True when the player already owned this unique prize and it paid RP instead.</summary>
        public readonly bool IsDupe;

        /// <summary>The RP a duplicate paid out. 0 when it is not a duplicate.</summary>
        public readonly int DupeRp;

        public PrizeRecord(string kind, string refId, int quantity, CharacterRarity rarity,
                           bool isDupe = false, int dupeRp = 0)
        {
            Kind     = kind ?? string.Empty;
            RefId    = refId ?? string.Empty;
            Quantity = quantity;
            Rarity   = rarity;
            IsDupe   = isDupe;
            DupeRp   = dupeRp;
        }

        /// <summary>
        /// Build one from the server's DTO.
        ///
        /// <para>
        /// An unparseable rarity falls back to Common WITH A WARNING rather than throwing: the
        /// prize itself is already granted server-side by the time this runs, so refusing to build
        /// the record would hide a prize the player owns. Common is the safe direction — a tier
        /// nobody can parse must not silently light up the Legendary fanfare.
        /// </para>
        /// </summary>
        public static PrizeRecord FromDto(GachaPrizeDto dto)
        {
            if (dto == null) return new PrizeRecord("club", string.Empty, 1, CharacterRarity.Common);

            if (!System.Enum.TryParse(dto.Rarity ?? string.Empty, ignoreCase: true, out CharacterRarity rarity))
            {
                Debug.LogWarning($"[PrizeRecord] Unknown rarity '{dto.Rarity}' on {dto.Kind} " +
                                 $"'{dto.RefId}' — revealing it as Common. The server rolled a tier " +
                                 "this build does not know.");
                rarity = CharacterRarity.Common;
            }

            return new PrizeRecord(
                (dto.Kind ?? string.Empty).Trim().ToLowerInvariant(),
                dto.RefId,
                Mathf.Max(1, dto.Quantity),
                rarity,
                dto.IsDupe,
                dto.DupeRp);
        }

        public override string ToString()
            => $"{Kind} '{RefId}' x{Quantity} ({Rarity})" + (IsDupe ? $" DUPE +{DupeRp} RP" : "");

        // ── Kind constants ────────────────────────────────────────────────────
        // Spelled the way the server and InventoryGrants both spell them; a mismatch here is a
        // prize that renders as the wrong card, which no compiler would catch.

        public const string KindClub      = "club";
        public const string KindBall      = "ball";
        public const string KindCharacter = "character";
        public const string KindItem      = "item";
        public const string KindTicket    = "ticket";
    }
}
