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

        /// <summary>
        /// <see cref="ClubDatabaseCSV.Instance"/> with a live-object fallback.
        ///
        /// The static can be null while the COMPONENT is alive, active and enabled in
        /// DontDestroyOnLoad — observed 2026-08-29, and it is what made the daily mission card
        /// disappear: an empty supplied bag drops the card, and the only symptom was the card
        /// not being there. Awake() sets the static and does not re-run for an object that is
        /// already alive, so anything that wipes statics mid-session (a domain reload) leaves the
        /// two disagreeing permanently.
        ///
        /// Reaching for the live object is the cheap, honest repair for THIS consumer. The
        /// singleton itself is a separate problem and a wider one — ClubManager asserts on this
        /// same database.
        /// </summary>
        private static ClubDatabaseCSV? ClubDb()
        {
            var db = ClubDatabaseCSV.Instance;
            if (db != null) return db;
            return UnityEngine.Object.FindFirstObjectByType<ClubDatabaseCSV>(UnityEngine.FindObjectsInactive.Include);
        }

        // ── supplied: the mission's own bag ─────────────────────────────────────

        private static List<string> ResolveSupplied(string id, string mask, string rarity, out string warning)
        {
            warning = "";
            var outIds = new List<string>();

            var db = ClubDb();
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
                    if (!LoadoutTokens.Matches(club, type)) continue;
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

            // A LIST, not a set of names: a ban token is a QUESTION asked of each club
            // (`LoadoutTokens.Matches`), not a name to look up. `ban:Iron` has no single
            // name to compare against — it is every iron, whatever its loft.
            var banned = new List<string>();
            if (mask.StartsWith("ban:", StringComparison.OrdinalIgnoreCase))
                foreach (string t in Split(mask.Substring(4))) banned.Add(t);

            foreach (var owned in bag.GetClubsInBag(bag.EquippedBagSlot))
            {
                var template = ClubDb()?.GetClub(owned.clubId);
                if (template == null) continue;
                if (IsBanned(banned, template)) continue;
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

        /// <summary>A club is out when ANY ban token names it. The vocabulary is
        /// <see cref="LoadoutTokens"/>'s — the validator refuses to publish a token it does not
        /// know, so an unknown one here simply matches nothing.</summary>
        private static bool IsBanned(List<string> banned, ClubDataRuntime club)
        {
            foreach (string t in banned)
                if (LoadoutTokens.Matches(club, t)) return true;
            return false;
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
