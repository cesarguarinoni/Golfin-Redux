// gps_hub_entry §5 — one row of MY RECENT ROUNDS (Figma 14012:98947, rebound from Friends' Rounds).
#nullable enable
using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Binds one <see cref="ActivityDto"/> to the authored round row.
    ///
    /// <para>
    /// A separate component rather than four parallel arrays on the screen controller: the row has
    /// six fields and three of them are conditional, and a mis-indexed parallel array is exactly
    /// how one player's trust ends up next to another player's score.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GpsHubRoundRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI? _initial;
        [SerializeField] private TextMeshProUGUI? _venue;
        [SerializeField] private TextMeshProUGUI? _date;
        [SerializeField] private TextMeshProUGUI? _trust;
        [SerializeField] private TextMeshProUGUI? _score;
        [SerializeField] private TextMeshProUGUI? _holes;

        [Tooltip("The gold BEST tag. Shown only on the row whose score equals the profile's best_score.")]
        [SerializeField] private GameObject? _bestTag;

        public void Bind(ActivityDto row, bool isBest)
        {
            if (row == null) return;

            string venue = !string.IsNullOrWhiteSpace(row.VenueName)
                ? row.VenueName
                : (row.CourseName ?? string.Empty);

            if (_venue != null) _venue.text = venue;

            if (_initial != null)
                _initial.text = venue.Length > 0 ? venue.Substring(0, 1).ToUpperInvariant() : "?";

            if (_date != null) _date.text = RelativeDate(row.CheckInAt);

            if (_trust != null)
            {
                bool has = row.TrustLevel.HasValue;
                _trust.gameObject.SetActive(has);
                if (has)
                    _trust.text = string.Format(
                        LocalizationManager.Get("GPS_HUB_TRUST_FORMAT"),
                        row.TrustLevel!.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (_score != null)
                _score.text = row.Score.HasValue
                    ? row.Score.Value.ToString(CultureInfo.InvariantCulture)
                    : "—";

            // The activities row carries NO par, so "(+12)" cannot be computed from it. The hole
            // count is what the row can honestly say instead — see GpsDtos.ActivityDto.Score.
            if (_holes != null)
            {
                bool has = !string.IsNullOrWhiteSpace(row.ScoreType);
                _holes.gameObject.SetActive(has);
                if (has)
                    _holes.text = string.Format(
                        LocalizationManager.Get("GPS_HUB_HOLES_FORMAT"), row.ScoreType);
            }

            if (_bestTag != null) _bestTag.SetActive(isBest);
        }

        /// <summary>
        /// "today" / "yesterday" / "N days ago" / "N weeks ago" from an ISO-8601 timestamp.
        ///
        /// <para>
        /// Parsed as an ABSOLUTE instant (<see cref="DateTimeStyles.AdjustToUniversal"/>) and
        /// compared in UTC, so two players in different zones see the same row age. An
        /// unparseable or missing value renders empty rather than "today", which would be a lie.
        /// </para>
        /// <para>
        /// The four words go through the CSV like every other player-facing string. SPEC §5
        /// sanctioned a local helper and SPEC §6 forbids hardcoded literals; §6 wins, because a
        /// Japanese player reading "3 days ago" under fully translated copy is the exact bug the
        /// rule exists to stop. The cutover to weeks is at FOURTEEN days, not seven, so the plural
        /// is always correct without a fifth singular key.
        /// </para>
        /// </summary>
        private static string RelativeDate(string? iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return string.Empty;
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime utc))
                return string.Empty;

            int days = (int)(DateTime.UtcNow.Date - utc.Date).TotalDays;
            if (days <= 0) return LocalizationManager.Get("GPS_HUB_DATE_TODAY");
            if (days == 1) return LocalizationManager.Get("GPS_HUB_DATE_YESTERDAY");
            if (days < 14)
                return string.Format(LocalizationManager.Get("GPS_HUB_DATE_DAYS_AGO"),
                    days.ToString(CultureInfo.InvariantCulture));
            return string.Format(LocalizationManager.Get("GPS_HUB_DATE_WEEKS_AGO"),
                (days / 7).ToString(CultureInfo.InvariantCulture));
        }
    }
}
