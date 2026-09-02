// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D8 — where a ShimmerBlock group lives, and how a controller finds it.
//
// The blocks themselves are authored by GpsPolishBuilder into the prefab, under
// a host that is INACTIVE at rest. That is what keeps A2 at 0 px: a screen that
// has never fetched draws exactly what it drew at HEAD, and the placeholder is
// a SetActive away rather than an Instantiate away (the prefab does not live
// under Resources/, so a runtime load would silently return null and the panel
// would stay blank — the very defect this closes).
//
// FOUND BY SITE NAME, NOT BY PATH. The controllers ask for "hub.rounds", not for
// "ContentContainer/RecentRoundsPanel/RoundRows/Shimmer" — a path lookup breaks
// silently the next time a panel is re-parented, which is exactly what the
// nav-bar safe-area wrapper did to every Find("GpsNavBar") in this task.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using UnityEngine;

namespace Golfin.Gps.UI
{
    /// <summary>A group of <see cref="ShimmerBlock"/> placeholders for one fetch site.</summary>
    [DisallowMultipleComponent]
    public sealed class ShimmerHost : MonoBehaviour
    {
        /// <summary>The five sites, as the controllers name them.</summary>
        public const string HubRounds   = "hub.rounds";
        public const string Badges      = "badges.grid";
        public const string Supporters  = "gift.supporters";
        public const string Golfers     = "gift.golfers";
        public const string VoteList    = "vote.list";

        [Tooltip("Which cold-fetch site this group stands in for. One of the constants on this " +
                 "class; set by GpsPolishBuilder.")]
        [SerializeField] private string _site = string.Empty;

        public string Site => _site;

        /// <summary>Find the host for one site anywhere under a screen root, active or not.</summary>
        public static ShimmerHost? Find(GameObject? root, string site)
        {
            if (root == null) return null;
            foreach (ShimmerHost h in root.GetComponentsInChildren<ShimmerHost>(true))
                if (h != null && h._site == site) return h;
            return null;
        }

        /// <summary>Show or hide the whole group.</summary>
        public void Set(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }
    }
}
