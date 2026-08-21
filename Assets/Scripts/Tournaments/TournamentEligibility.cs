// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentEligibility
// The client half of the entry gate (tournament_restrictions §3).
//
// PURE. It takes ranks and levels, not managers — CharacterManager and BagManager
// live in Assembly-CSharp and this assembly must stay dependency-light — so the
// whole eligibility matrix is directly unit-testable with no scene, no singletons
// and no Unity lifecycle.
//
// THE SERVER IS STILL THE AUTHORITY for the character bands and max_players: it
// re-checks them inside POST /golfin/{slug}/enter, BEFORE the fee debit. This gate
// exists so an ineligible player never reaches the payment path at all (and so the
// offline/local backend enforces something), not because the client is trusted.
//
// The rarity/level checks mirror _check_entry_eligibility in
// backend/routers/tournaments_golfin.py CASE FOR CASE, including the deny-when-
// unknown branches: a character whose rarity cannot be resolved cannot PROVE it is
// inside a restricted band, and an unrestricted tournament never reaches that
// branch, so denying is only ever reached where denying is the safe answer.
//
// gear_rule / club_rarity_max are CLIENT-ONLY by contract — the server never sees
// the bag.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>Which rule refused the entry. <see cref="None"/> means eligible.</summary>
    public enum TournamentEligibilityFailure
    {
        None,

        /// <summary>Character rarity outside <c>char_rarity_min..max</c>, or unresolvable while a
        /// rarity band is set. Server parity: <c>reason: "char_rarity"</c>.</summary>
        CharacterRarity,

        /// <summary>Character level outside <c>char_level_min..max</c>, or unknown while a level
        /// band is set. Server parity: <c>reason: "char_level"</c>.</summary>
        CharacterLevel,

        /// <summary>A club in the equipped bag is above <c>club_rarity_max</c>. Client-only —
        /// the server has no view of the bag.</summary>
        ClubRarity,

        /// <summary>The human field is already at <c>max_players</c>. Only the server can decide
        /// this (it counts the entries); the client maps the served denial onto it so both
        /// paths refuse through one vocabulary.</summary>
        Full,
    }

    /// <summary>
    /// ONE unmet requirement, with enough detail to render a line the player can act on
    /// (tournament_entry_denied_modal). <see cref="TournamentEligibilityFailure"/> says WHICH rule
    /// refused; this also says WHICH BOUND of it was missed and WHAT the bound is, because
    /// "MINIMUM REQUIREMENT: UNCOMMON" and "MAXIMUM ALLOWED: RARE" are different sentences and the
    /// enum alone cannot tell them apart.
    /// </summary>
    public readonly struct TournamentRequirement
    {
        public readonly TournamentEligibilityFailure Failure;

        /// <summary>True when the player exceeded a ceiling, false when they fell short of a floor.</summary>
        public readonly bool IsMaximum;

        /// <summary>Canonical rarity name of the bound, when the bound is a rarity. Else null.</summary>
        public readonly string? RarityBound;

        /// <summary>The bound, when it is a level. Else null.</summary>
        public readonly int? LevelBound;

        public TournamentRequirement(
            TournamentEligibilityFailure failure, bool isMaximum,
            string? rarityBound = null, int? levelBound = null)
        {
            Failure     = failure;
            IsMaximum   = isMaximum;
            RarityBound = rarityBound;
            LevelBound  = levelBound;
        }
    }

    public static class TournamentEligibility
    {
        /// <summary>
        /// Evaluate <paramref name="def"/>'s restrictions against one character and one bag.
        /// </summary>
        /// <param name="characterRarityRank">
        /// 1..6 on <see cref="TournamentRestrictions.RarityLadder"/>, or null when the character's
        /// rarity could not be resolved.
        /// </param>
        /// <param name="characterLevel">The player's current level for that character, or null.</param>
        /// <param name="equippedClubRarityRanks">
        /// Rarity ranks of the clubs in the EQUIPPED bag. Null or empty is eligible: a
        /// <c>club_rarity_max</c> is a ceiling, and a bag with nothing in it clears every ceiling.
        /// Entries that cannot be ranked are skipped rather than denied — an unknown club rarity is
        /// a data gap in the shipped Clubs.csv, not evidence the player is cheating.
        /// </param>
        public static TournamentEligibilityFailure Evaluate(
            TournamentDefinition  def,
            int?                  characterRarityRank,
            int?                  characterLevel,
            IReadOnlyList<int>?   equippedClubRarityRanks)
        {
            if (def == null) return TournamentEligibilityFailure.None;

            // ── Character rarity band ────────────────────────────────────────
            int? rarityMin = TournamentRestrictions.RarityRank(def.CharRarityMin);
            int? rarityMax = TournamentRestrictions.RarityRank(def.CharRarityMax);
            if (rarityMin.HasValue || rarityMax.HasValue)
            {
                if (!characterRarityRank.HasValue)
                    return TournamentEligibilityFailure.CharacterRarity;
                if (rarityMin.HasValue && characterRarityRank.Value < rarityMin.Value)
                    return TournamentEligibilityFailure.CharacterRarity;
                if (rarityMax.HasValue && characterRarityRank.Value > rarityMax.Value)
                    return TournamentEligibilityFailure.CharacterRarity;
            }

            // ── Character level band ─────────────────────────────────────────
            if (def.CharLevelMin.HasValue || def.CharLevelMax.HasValue)
            {
                if (!characterLevel.HasValue)
                    return TournamentEligibilityFailure.CharacterLevel;
                if (def.CharLevelMin.HasValue && characterLevel.Value < def.CharLevelMin.Value)
                    return TournamentEligibilityFailure.CharacterLevel;
                if (def.CharLevelMax.HasValue && characterLevel.Value > def.CharLevelMax.Value)
                    return TournamentEligibilityFailure.CharacterLevel;
            }

            // ── Equipped-bag club cap ────────────────────────────────────────
            // Skipped entirely under `supplied`: the player's own clubs are not what gets played,
            // so capping them would refuse an entry the rule does not actually restrict.
            int? clubMax = TournamentRestrictions.RarityRank(def.ClubRarityMax);
            if (clubMax.HasValue && def.EffectiveGearRule == TournamentGearRule.Own &&
                equippedClubRarityRanks != null)
            {
                for (int i = 0; i < equippedClubRarityRanks.Count; i++)
                    if (equippedClubRarityRanks[i] > clubMax.Value)
                        return TournamentEligibilityFailure.ClubRarity;
            }

            return TournamentEligibilityFailure.None;
        }

        /// <summary>
        /// EVERY unmet requirement, not just the first — the refusal modal lists them, so stopping
        /// at the first would send a player to fix one rule only to be refused by the next.
        /// <para>
        /// Ordered rarity → level → club, the same order <see cref="Evaluate"/> and the server
        /// short-circuit in, so the first entry here is always what the server would have named.
        /// An eligible player yields an empty list.
        /// </para>
        /// </summary>
        public static IReadOnlyList<TournamentRequirement> UnmetRequirements(
            TournamentDefinition  def,
            int?                  characterRarityRank,
            int?                  characterLevel,
            IReadOnlyList<int>?   equippedClubRarityRanks)
        {
            var unmet = new List<TournamentRequirement>();
            if (def == null) return unmet;

            // ── Character rarity band ────────────────────────────────────────
            int? rarityMin = TournamentRestrictions.RarityRank(def.CharRarityMin);
            int? rarityMax = TournamentRestrictions.RarityRank(def.CharRarityMax);
            if (rarityMin.HasValue || rarityMax.HasValue)
            {
                // An unresolvable rarity cannot prove it is inside the band. Report it against the
                // floor when there is one, because "you need at least X" is the actionable half.
                if (!characterRarityRank.HasValue)
                    unmet.Add(new TournamentRequirement(
                        TournamentEligibilityFailure.CharacterRarity,
                        isMaximum: !rarityMin.HasValue,
                        rarityBound: def.CharRarityMin ?? def.CharRarityMax));
                else if (rarityMin.HasValue && characterRarityRank.Value < rarityMin.Value)
                    unmet.Add(new TournamentRequirement(
                        TournamentEligibilityFailure.CharacterRarity, false, def.CharRarityMin));
                else if (rarityMax.HasValue && characterRarityRank.Value > rarityMax.Value)
                    unmet.Add(new TournamentRequirement(
                        TournamentEligibilityFailure.CharacterRarity, true, def.CharRarityMax));
            }

            // ── Character level band ─────────────────────────────────────────
            if (def.CharLevelMin.HasValue || def.CharLevelMax.HasValue)
            {
                if (!characterLevel.HasValue)
                    unmet.Add(new TournamentRequirement(
                        TournamentEligibilityFailure.CharacterLevel,
                        isMaximum: !def.CharLevelMin.HasValue,
                        levelBound: def.CharLevelMin ?? def.CharLevelMax));
                else if (def.CharLevelMin.HasValue && characterLevel.Value < def.CharLevelMin.Value)
                    unmet.Add(new TournamentRequirement(
                        TournamentEligibilityFailure.CharacterLevel, false, null, def.CharLevelMin));
                else if (def.CharLevelMax.HasValue && characterLevel.Value > def.CharLevelMax.Value)
                    unmet.Add(new TournamentRequirement(
                        TournamentEligibilityFailure.CharacterLevel, true, null, def.CharLevelMax));
            }

            // ── Equipped-bag club cap ────────────────────────────────────────
            int? clubMax = TournamentRestrictions.RarityRank(def.ClubRarityMax);
            if (clubMax.HasValue && def.EffectiveGearRule == TournamentGearRule.Own &&
                equippedClubRarityRanks != null)
            {
                for (int i = 0; i < equippedClubRarityRanks.Count; i++)
                {
                    if (equippedClubRarityRanks[i] <= clubMax.Value) continue;
                    // One entry however many clubs are over: the requirement is the cap, and
                    // repeating it per offending club would just pad the list.
                    unmet.Add(new TournamentRequirement(
                        TournamentEligibilityFailure.ClubRarity, true, def.ClubRarityMax));
                    break;
                }
            }

            return unmet;
        }
    }
}
