// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentRestrictions
// The category + entry-restriction vocabulary served by GET /tournaments/golfin
// (tournament_restrictions, server half LIVE 2026-08-18).
//
// Every one of these is NULLABLE all the way down, and null means UNRESTRICTED —
// which is also exactly what the shipped tournaments.csv produces, so the offline
// path keeps today's behaviour without gaining a single column.
//
// PARSING NEVER THROWS. A newer dashboard can author a division type or a rarity
// name this build has never heard of; a client that threw on it would take the
// whole schedule down over a string. Unknown → null → unrestricted, logged once
// by the caller if it cares.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;

namespace Golfin.Tournaments
{
    /// <summary>What KIND of tournament this is. Presentation only — it carries no rules of its
    /// own (stat normalization for <c>Competitive</c> is a later phase, deliberately not implied
    /// by this field).</summary>
    public enum TournamentCategory
    {
        /// <summary>Brand-led event. The backfilled value for every pre-existing tournament.</summary>
        Sponsor,

        /// <summary>Ladder/ranked event.</summary>
        Competitive,
    }

    /// <summary>How the field is split into divisions.</summary>
    public enum TournamentDivisionType
    {
        /// <summary>One field, no split.</summary>
        Open,

        /// <summary>Split by character level. The backfilled value.</summary>
        Level,

        /// <summary>Split by character rarity band.</summary>
        RarityBand,
    }

    /// <summary>Whose clubs are played.</summary>
    public enum TournamentGearRule
    {
        /// <summary>The player's own bag, optionally capped by
        /// <see cref="TournamentDefinition.ClubRarityMax"/>. The backfilled value.</summary>
        Own,

        /// <summary>A standard set supplied by the tournament. The bag is not consulted at all.
        /// <para>⚠️ v1 renders and gates on this; actually SWAPPING the clubs in play is the
        /// later standard-spec task and is deliberately out of scope here.</para></summary>
        Supplied,
    }

    /// <summary>
    /// Tolerant parsers for the wire vocabulary. Case-insensitive, whitespace-tolerant, and
    /// <b>null for anything unrecognised</b> — see the file header.
    /// </summary>
    public static class TournamentRestrictions
    {
        /// <summary>
        /// Canonical rarity names in ASCENDING order. Index + 1 is the rank, which is the same
        /// ladder the server's <c>RARITY_RANK</c> uses and the same order
        /// <c>Golfin.Roster.CharacterRarity</c> declares — the three are pinned together by
        /// <c>TournamentEligibilityTests.RarityLadderMatchesCharacterRarityEnum</c>.
        /// </summary>
        public static readonly string[] RarityLadder =
        {
            "Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme",
        };

        /// <summary>Lowest rank on the ladder — what an absent <c>min</c> bound means.</summary>
        public const int MinRarityRank = 1;

        /// <summary>Highest rank on the ladder — what an absent <c>max</c> bound means.</summary>
        public const int MaxRarityRank = 6;

        /// <summary>The canonical spelling of <paramref name="raw"/>, or null when it is blank or
        /// not a rarity this build knows. Callers store the canonical form so a rarity can never
        /// reach the eligibility gate in a shape the rank lookup will not recognise.</summary>
        public static string? CanonicalRarity(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string trimmed = raw!.Trim();
            foreach (string name in RarityLadder)
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                    return name;
            return null;
        }

        /// <summary>1-based position on <see cref="RarityLadder"/>, or null when unrecognised.</summary>
        public static int? RarityRank(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string trimmed = raw!.Trim();
            for (int i = 0; i < RarityLadder.Length; i++)
                if (string.Equals(RarityLadder[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            return null;
        }

        public static TournamentCategory? ParseCategory(string? raw) => Match(raw) switch
        {
            "sponsor"     => TournamentCategory.Sponsor,
            "competitive" => TournamentCategory.Competitive,
            _             => (TournamentCategory?)null,
        };

        public static TournamentDivisionType? ParseDivisionType(string? raw) => Match(raw) switch
        {
            "open"        => TournamentDivisionType.Open,
            "level"       => TournamentDivisionType.Level,
            "rarity_band" => TournamentDivisionType.RarityBand,
            _             => (TournamentDivisionType?)null,
        };

        public static TournamentGearRule? ParseGearRule(string? raw) => Match(raw) switch
        {
            "own"      => TournamentGearRule.Own,
            "supplied" => TournamentGearRule.Supplied,
            _          => (TournamentGearRule?)null,
        };

        /// <summary>A positive count, or null. Zero and negatives are treated as "no cap": the
        /// dashboard clears a field by emptying it, and a 0 that slipped through would otherwise
        /// mean "nobody may enter".</summary>
        public static int? PositiveOrNull(int? raw) => raw.HasValue && raw.Value > 0 ? raw : null;

        private static string Match(string? raw)
            => string.IsNullOrWhiteSpace(raw) ? string.Empty : raw!.Trim().ToLowerInvariant();
    }
}
