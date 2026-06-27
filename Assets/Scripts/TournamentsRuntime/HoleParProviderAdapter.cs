// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — HoleParProviderAdapter
// Production adapter for IHoleParProvider.
// Reads HoleDatabaseLoader.RuntimeDatabase lazily per-call (never cached in a field).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using GolfinRedux.UI;
using UnityEngine;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Production <see cref="IHoleParProvider"/> that reads par values from
    /// <see cref="HoleDatabaseLoader.RuntimeDatabase"/> (<c>HoleData.par</c>).
    /// <para>
    /// <b>clubId is advisory:</b> the HoleDatabase is not scoped by club/course,
    /// so par is resolved by hole-id alone (the numeric string from the tournament
    /// holeSet, e.g. "1"–"18"). clubId is accepted but ignored.
    /// </para>
    /// <para>
    /// <b>Hole ID format:</b> after <c>TournamentCsvLoader.ExpandHoleSet</c>, holeSet
    /// values are numeric strings ("1", "2", … "18"). These are matched to
    /// <c>HoleData.holeNumber</c> (int). A missing or non-numeric hole id throws
    /// <see cref="InvalidOperationException"/> — a silent default par would corrupt scoring.
    /// </para>
    /// <para>
    /// Resolves <c>HoleDatabaseLoader.RuntimeDatabase</c> lazily per-call — never cached.
    /// </para>
    /// </summary>
    public sealed class HoleParProviderAdapter : IHoleParProvider
    {
        /// <inheritdoc/>
        public IReadOnlyList<int> ParsFor(string clubId, IReadOnlyList<string> holeSet)
        {
            // clubId is advisory — HoleDatabase is not club-scoped; ignored here.

            var db = HoleDatabaseLoader.RuntimeDatabase;
            if (db == null)
                throw new InvalidOperationException(
                    "[HoleParProviderAdapter] HoleDatabaseLoader.RuntimeDatabase is null. " +
                    "HoleDatabaseLoader.LoadFromCSV() must run before any par lookup.");

            var result = new int[holeSet.Count];
            for (int i = 0; i < holeSet.Count; i++)
            {
                string holeId = holeSet[i];

                // Hole IDs from ExpandHoleSet are numeric strings ("1"–"18").
                if (!int.TryParse(holeId, out int holeNumber))
                    throw new InvalidOperationException(
                        $"[HoleParProviderAdapter] holeId '{holeId}' is not a valid integer. " +
                        "Expected numeric hole IDs from TournamentCsvLoader.ExpandHoleSet.");

                HoleData? found = null;
                foreach (var hole in db.holes)
                {
                    if (hole.holeNumber == holeNumber)
                    {
                        found = hole;
                        break;
                    }
                }

                if (found == null)
                    throw new InvalidOperationException(
                        $"[HoleParProviderAdapter] No HoleData found for holeNumber={holeNumber} (holeId='{holeId}'). " +
                        "Ensure HoleDatabase.csv is complete and HoleDatabaseLoader has loaded it.");

                result[i] = found.par;
            }

            return result;
        }
    }
}
