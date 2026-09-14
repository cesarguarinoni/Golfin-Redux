// ─────────────────────────────────────────────────────────────────────────────
// TournamentHeaderPills — the identity pills every tournament screen carries
// (finished_tournament_leaderboard_route F3, 2026-09-14)
//
// The Tournament Leaderboard and the Tournament Hole Selection share the same authored
// banner: Pill_SPONSO ("SPONSORED BY …"), Pill_KASUMI (the tournament's name) and
// Pill_ENDSI (the countdown, driven by TournamentCountdown). The board bound the first two
// from the selected tournament since iter-2; the hole selection never did, so every
// tournament's hole list read "SPONSORED BY PUMA / KASUMIGASEKI OPEN" — the Stage-1
// placeholder text — whatever the player had entered. One binder, two screens, no drift
// (the TournamentVenueLine / TournamentDisplayName lesson).
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Golfin.Tournaments;

namespace GolfinRedux.UI.Tournaments
{
    public static class TournamentHeaderPills
    {
        /// <summary>The selected tournament's definition, or null. <c>Backend.GetTournament</c>
        /// throws on an unknown id; a header should degrade to its authored text instead.</summary>
        public static TournamentDefinition TryFindDef(string tournamentId)
        {
            if (string.IsNullOrEmpty(tournamentId)) return null;
            IReadOnlyList<TournamentDefinition> defs = TournamentService.Instance?.Backend?.GetTournaments();
            if (defs == null) return null;
            foreach (var d in defs)
                if (string.Equals(d.Id, tournamentId, System.StringComparison.Ordinal)) return d;
            return null;
        }

        /// <summary>"SPONSORED BY {SPONSOR}" — the sponsor key upper-cased, GOLFIN when unset.</summary>
        public static string SponsorLine(TournamentDefinition def)
        {
            string sponsor = def == null || string.IsNullOrEmpty(def.SponsorKey)
                ? "GOLFIN"
                : def.SponsorKey.ToUpperInvariant();
            return LocalizationManager.Get("TOURN_SPONSORED_BY") + " " + sponsor;
        }

        /// <summary>The tournament's display name, upper-cased for the pill — the one name ladder
        /// (localized NameKey → dashboard Title → id), so a dashboard-created tournament reads its
        /// title rather than a raw key.</summary>
        public static string NameLine(TournamentDefinition def)
            => TournamentDisplayName.Resolve(def).ToUpperInvariant();

        /// <summary>
        /// Write the sponsor and name pills under <paramref name="root"/>. Paths are relative to the
        /// screen root (the two screens author the same pills under different parents). A missing
        /// label is logged, never thrown — the screen still works with its authored text.
        /// </summary>
        public static void Bind(Transform root, string sponsorLabelPath, string nameLabelPath,
                                TournamentDefinition def, string logTag)
        {
            if (root == null || def == null) return;

            var sponsorLabel = root.Find(sponsorLabelPath)?.GetComponent<TextMeshProUGUI>();
            if (sponsorLabel != null)
            {
                sponsorLabel.text = SponsorLine(def);
                Debug.Log($"{logTag} Header sponsor → '{sponsorLabel.text}'");
            }
            else Debug.LogWarning($"{logTag} Sponsor label not found at {sponsorLabelPath}");

            var nameLabel = root.Find(nameLabelPath)?.GetComponent<TextMeshProUGUI>();
            if (nameLabel != null)
            {
                nameLabel.text = NameLine(def);
                Debug.Log($"{logTag} Header name → '{nameLabel.text}'");
            }
            else Debug.LogWarning($"{logTag} Name label not found at {nameLabelPath}");
        }
    }
}
