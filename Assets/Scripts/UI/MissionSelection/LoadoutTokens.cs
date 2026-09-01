#nullable enable
using System;
using System.Text.RegularExpressions;
using Golfin.Inventory;

namespace GolfinRedux.UI.MissionSelection
{
    /// <summary>
    /// The loadout MASK VOCABULARY — the one definition of what `Driver`, `Iron7` or `AW` means,
    /// shared by the runtime resolver and the publish validator.
    ///
    /// WHY IT IS ITS OWN CLASS AND NOT A PRIVATE HELPER ON THE RESOLVER. The same words are read
    /// in two languages: here, by <see cref="MissionLoadoutResolver"/>, to build a bag; and in
    /// TypeScript, by `Tools/admin-dashboard/lib/loadoutTokens.ts`, to refuse a publish that names
    /// a bag no club row can fill. Two implementations of one grammar are only allowed to exist
    /// because `Tools/content/tests/loadout_tokens_fixture.csv` is run through BOTH of them —
    /// vitest reads it, and so does `LoadoutTokensTests` — so the day they disagree is the day a
    /// test goes red rather than the day a mission ships an empty bag.
    ///
    /// THE GRAMMAR (case-insensitive):
    ///   Driver | Wood | Putter   the matching <see cref="ClubType"/>
    ///   AW | PW | SW             A_Wedge / P_Wedge / S_Wedge (CSV spells them "A.Wedge" etc.)
    ///   Iron                     ANY iron, whatever its loft — a FAMILY token
    ///   IronN (one digit)        an iron whose loft parses to N
    ///   anything else            never matches; the validator reports it as unknown
    ///
    /// THE LOFT PARSE IS ANCHORED, AND THAT IS THE POINT. Its predecessor asked whether the id
    /// and name, concatenated, "contains 9" and then whether it "contains 7" — so `Iron 5 X7`
    /// was a 7-iron and any brand with a digit in it would have been one too. Here the loft is
    /// the digit in `Iron <N>` at the START of the name, or in `club_iron<N>` at the start of the
    /// id, or nothing at all. Over the shipped 114 irons the two agree exactly (12 × Iron7,
    /// 6 × Iron9, none flip); the difference is what happens to the roster we have not written yet.
    /// </summary>
    public static class LoadoutTokens
    {
        /// <summary>The list an operator sees when a mask names something this grammar does not know.
        /// Iron0–Iron3 parse but no shipped iron carries those lofts, so the hint names the real range.</summary>
        public const string KnownTokensHint = "Driver, Wood, Iron, Iron4-Iron9, AW, PW, SW, Putter";

        private static readonly Regex NameLoft = new Regex(@"^\s*Iron\s+(\d)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex IdLoft = new Regex(@"^club_iron(\d)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex IronToken = new Regex(@"^iron(\d)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>True when the grammar above can say anything at all about this token.</summary>
        public static bool IsKnown(string token)
        {
            string t = (token ?? "").Trim();
            if (t.Length == 0) return false;
            if (IronToken.IsMatch(t)) return true;
            switch (t.ToLowerInvariant())
            {
                case "driver": case "wood": case "iron":
                case "aw": case "pw": case "sw": case "putter": return true;
                default: return false;
            }
        }

        /// <summary>
        /// The iron number a club carries, or null for a loft-less iron (and for anything that is
        /// not an iron — callers gate on the family first). Name wins over id; both are anchored.
        /// </summary>
        public static int? IronLoft(string clubId, string name)
        {
            var m = NameLoft.Match(name ?? "");
            if (m.Success) return m.Groups[1].Value[0] - '0';
            m = IdLoft.Match(clubId ?? "");
            if (m.Success) return m.Groups[1].Value[0] - '0';
            return null;
        }

        /// <summary>Does <paramref name="token"/> name this club?</summary>
        public static bool Matches(ClubDataRuntime club, string token)
            => club != null && Matches(club.clubId, club.name, FamilyOf(club.type), token);

        /// <summary>
        /// The string-only parity surface — the same decision as the overload above, over the three
        /// columns the shared fixture (and the CSV, and the admin draft row) actually carry.
        /// <paramref name="type"/> accepts the CSV spelling ("A.Wedge") or the family token ("AW").
        /// </summary>
        public static bool Matches(string clubId, string name, string type, string token)
        {
            string family = FamilyOf(type);
            if (family.Length == 0) return false;

            string t = (token ?? "").Trim();
            if (t.Length == 0) return false;

            if (string.Equals(t, family, StringComparison.OrdinalIgnoreCase)) return true;

            var m = IronToken.Match(t);
            if (m.Success && string.Equals(family, "Iron", StringComparison.OrdinalIgnoreCase))
                return IronLoft(clubId, name) == m.Groups[1].Value[0] - '0';

            return false;
        }

        /// <summary>
        /// `ClubType` as the loadout mask spells it. The enum and the design vocabulary differ on
        /// the wedges (`A.Wedge` vs `AW`), so the mapping is explicit rather than a ToString.
        /// </summary>
        public static string FamilyOf(ClubType type)
        {
            switch (type)
            {
                case ClubType.Driver:  return "Driver";
                case ClubType.Wood:    return "Wood";
                case ClubType.Iron:    return "Iron";
                case ClubType.A_Wedge: return "AW";
                case ClubType.P_Wedge: return "PW";
                case ClubType.S_Wedge: return "SW";
                case ClubType.Putter:  return "Putter";
                default:               return type.ToString();
            }
        }

        /// <summary>The same family, from the CSV `type` string (or from a family token already).</summary>
        public static string FamilyOf(string type)
        {
            switch ((type ?? "").Trim().ToLowerInvariant())
            {
                case "driver":                       return "Driver";
                case "wood":                         return "Wood";
                case "iron":                         return "Iron";
                case "a.wedge": case "a_wedge": case "aw": return "AW";
                case "p.wedge": case "p_wedge": case "pw": return "PW";
                case "s.wedge": case "s_wedge": case "sw": return "SW";
                case "putter":                       return "Putter";
                default:                             return "";
            }
        }
    }
}
