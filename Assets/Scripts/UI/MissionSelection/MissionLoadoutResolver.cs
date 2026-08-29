#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Gameplay.Missions;
using Golfin.Inventory;
using UnityEngine;

namespace GolfinRedux.UI.MissionSelection
{
    /// <summary>
    /// Turns a `mission_loadouts` row into the exact club ids a mission plays with.
    /// Spec: missions_v1 §B3.
    ///
    /// TWO KINDS, AND THEY FAIL DIFFERENTLY, WHICH IS THE WHOLE REASON THIS IS ITS OWN CLASS.
    ///
    ///   `supplied:` HANDS the player a bag. Each club TYPE is resolved against `Clubs.csv` at
    ///   the stated rarity, taking the FIRST matching row in CSV order — deterministic, so the
    ///   same mission hands out the same clubs on every device and the difficulty score means
    ///   something. A type that resolves to nothing is a bag with a hole in it, and the publish
    ///   validator already blocks that; here it is reported so the card can be warned rather
    ///   than played.
    ///
    ///   `own:` uses the player's OWN bag minus the banned types. It can fail in a way supplied
    ///   cannot: a player who owns only wedges, on a mission that bans wedges, has no clubs at
    ///   all. No validator can catch that — it depends on the player — so it is caught here,
    ///   every time the screen is built, and the card is rendered un-playable (§C3).
    /// </summary>
    public static class MissionLoadoutResolver
    {
        private const string Tag = "[MissionLoadout]";

        /// <summary>
        /// Install this as `MissionCatalog.ClubResolver`, before the first screen builds.
        ///
        /// `MissionCatalog` is a LEAF assembly and cannot reference Assembly-CSharp, where the
        /// club catalog and the player's bag live. So the direction is inverted: this registers
        /// itself, exactly the way `MissionSession` subscribes to `GameSession.OnSessionReset`
        /// rather than being called by it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install() => MissionCatalog.ClubResolver = Resolve;

        public static List<string> Resolve(Dictionary<string, string> loadout, out string warning)
        {
            warning = "";
            var outIds = new List<string>();

            string kind  = Get(loadout, "kind").ToLowerInvariant();
            string mask  = Get(loadout, "clubs");
            string rarity = Get(loadout, "rarity");
            string id    = Get(loadout, "id");

            if (kind == "supplied") return ResolveSupplied(id, mask, rarity, out warning);
            if (kind == "own")      return ResolveOwn(id, mask, out warning);

            warning = $"loadout '{id}' has kind '{kind}', which is neither supplied nor own";
            return outIds;
        }

        // ── supplied: the mission's own bag ─────────────────────────────────────

        private static List<string> ResolveSupplied(string id, string mask, string rarity, out string warning)
        {
            warning = "";
            var outIds = new List<string>();

            var db = ClubDatabaseCSV.Instance;
            if (db == null)
            {
                warning = "the club catalog is not loaded yet";
                return outIds;
            }
            var all = db.GetAllClubs();

            foreach (string type in Split(mask))
            {
                ClubDataRuntime? hit = null;
                // FIRST match in CSV order, not "best" — a deterministic pick is what makes the
                // same supplied mission the same mission everywhere.
                foreach (var club in all)
                {
                    if (!string.Equals(ClubTypeName(club), type, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(club.rarity.ToString(), rarity, StringComparison.OrdinalIgnoreCase)) continue;
                    hit = club;
                    break;
                }
                if (hit == null)
                {
                    warning = $"supplied loadout '{id}' names {type} at {rarity}, which no club row provides";
                    return new List<string>();   // an incomplete supplied bag is not a bag
                }
                outIds.Add(hit.clubId);
            }

            if (outIds.Count == 0) warning = $"supplied loadout '{id}' lists no clubs";
            return outIds;
        }

        // ── own: the player's bag, minus the ban mask ───────────────────────────

        private static List<string> ResolveOwn(string id, string mask, out string warning)
        {
            warning = "";
            var outIds = new List<string>();

            var bag = BagManager.Instance;
            var clubs = ClubManager.Instance;
            if (bag == null || clubs == null)
            {
                warning = "the player's bag is not loaded yet";
                return outIds;
            }

            var banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (mask.StartsWith("ban:", StringComparison.OrdinalIgnoreCase))
                foreach (string t in Split(mask.Substring(4))) banned.Add(t);

            foreach (var owned in bag.GetClubsInBag(bag.EquippedBagSlot))
            {
                var template = ClubDatabaseCSV.Instance?.GetClub(owned.clubId);
                if (template == null) continue;
                if (banned.Contains(ClubTypeName(template))) continue;
                outIds.Add(owned.clubId);
            }

            if (outIds.Count == 0)
            {
                // §C3's named case. Not an error — a real thing a real bag can be.
                warning = banned.Count > 0
                    ? $"your bag has no club this mission allows (it bans {string.Join(", ", banned)})"
                    : "your equipped bag is empty";
            }
            return outIds;
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// `ClubType` as the loadout mask spells it. The enum and the design vocabulary differ
        /// on the wedges (`A.Wedge` vs `AW`), so the mapping is explicit rather than a ToString.
        /// </summary>
        private static string ClubTypeName(ClubDataRuntime club)
        {
            switch (club.type)
            {
                case ClubType.Driver:  return "Driver";
                case ClubType.Wood:    return "Wood";
                case ClubType.A_Wedge: return "AW";
                case ClubType.P_Wedge: return "PW";
                case ClubType.S_Wedge: return "SW";
                case ClubType.Putter:  return "Putter";
                case ClubType.Iron:    return IronName(club);
                default:               return club.type.ToString();
            }
        }

        /// <summary>The mask distinguishes Iron7 from Iron9; `ClubType.Iron` does not, so the
        /// number comes from the club's own id/name.</summary>
        private static string IronName(ClubDataRuntime club)
        {
            string probe = ((club.clubId ?? "") + " " + (club.name ?? "")).ToLowerInvariant();
            if (probe.Contains("9")) return "Iron9";
            if (probe.Contains("7")) return "Iron7";
            return "Iron";
        }

        private static IEnumerable<string> Split(string csv)
        {
            foreach (string part in (csv ?? "").Split(','))
            {
                string t = part.Trim();
                if (t.Length > 0 && t != "*") yield return t;
            }
        }

        private static string Get(Dictionary<string, string> r, string col)
            => r.TryGetValue(col, out var v) ? (v ?? "").Trim() : "";
    }
}
