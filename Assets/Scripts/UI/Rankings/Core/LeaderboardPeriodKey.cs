#nullable enable
using System;
using System.Globalization;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Deterministic period key computation for leaderboard accumulators.
    /// All keys are derived from a UTC DateTime.
    ///
    /// Daily   key = floor(utcUnixSeconds / 86400)
    /// Weekly  key = Monday-anchored ISO week: year * 53 + weekOfYear
    /// Monthly key = year * 12 + (month - 1)
    ///
    /// When the key computed from the current time differs from the stored key,
    /// the accumulator resets to 0 and the stored key updates (lazy rollover).
    /// </summary>
    public static class LeaderboardPeriodKey
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long Daily(DateTime utcNow)
        {
            long unixSeconds = (long)(utcNow - UnixEpoch).TotalSeconds;
            return unixSeconds / 86400L;
        }

        public static long Weekly(DateTime utcNow)
        {
            // Monday-anchored ISO week: shift Sunday (DayOfWeek=0) to be day 7
            // so that Monday is consistently the week start.
            int weekOfYear = ISOWeek.GetWeekOfYear(utcNow);
            int year = ISOWeek.GetYear(utcNow);
            return (long)year * 53L + weekOfYear;
        }

        public static long Monthly(DateTime utcNow)
        {
            return (long)utcNow.Year * 12L + (utcNow.Month - 1);
        }

        /// <summary>UTC DateTime of the end of the current daily period (start of next day).</summary>
        public static DateTime DailyPeriodEnd(DateTime utcNow)
        {
            return utcNow.Date.AddDays(1); // start of next UTC day
        }

        /// <summary>UTC DateTime of the end of the current weekly period (next Monday 00:00 UTC).</summary>
        public static DateTime WeeklyPeriodEnd(DateTime utcNow)
        {
            // DayOfWeek: Monday=1 ... Sunday=0
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)utcNow.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7; // already Monday → next Monday
            return utcNow.Date.AddDays(daysUntilMonday);
        }

        /// <summary>UTC DateTime of the end of the current monthly period (1st of next month).</summary>
        public static DateTime MonthlyPeriodEnd(DateTime utcNow)
        {
            return new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        }
    }
}
