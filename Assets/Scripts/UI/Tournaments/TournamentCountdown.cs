using System;
using System.Collections;
using UnityEngine;
using TMPro;
using Golfin.Tournaments;
using Golfin.UI.Rankings;   // NetworkTimeProvider — same clock the Rankings reset countdown uses

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Live "ENDS IN: …" countdown for the tournament banner pill (Pill_ENDSI).
    ///
    /// The pill used to be pure scene decoration: it was authored as the literal string
    /// "ENDS IN: 1d 5h 25m 05 s" and NOTHING wrote it at runtime, so every tournament showed the
    /// same frozen fake time — in English, in every language. Translating that string would only
    /// have localized a lie, so the pill is now driven from the tournament's real EndUtc.
    ///
    /// Format and units mirror the Rankings reset countdown: the prefix comes from
    /// TOURN_ENDS_IN and d/h/m/s from the shared RANK_TIME_* rows, because "17h 35m 10s" reads as
    /// English abbreviations to a Japanese player. Re-formatting every second means a language
    /// switch repaints it on the next tick with no OnLanguageChanged subscription.
    /// </summary>
    public static class TournamentCountdown
    {
        /// <summary>Relative path from either tournament screen root to the pill's label.</summary>
        public const string LeaderboardPillPath    = "ContentArea/Banner/IdentityPillRow/Row2/Pill_ENDSI/Label";
        public const string HoleSelectionPillPath  = "Content/IdentityPillRow/Row2/Pill_ENDSI/Label";

        /// <summary>
        /// Ticks the pill once a second until the host stops the coroutine. Safe to start when the
        /// label or the tournament is missing — it just clears the pill and returns rather than
        /// leaving the authored placeholder on screen.
        /// </summary>
        public static IEnumerator Run(Transform screenRoot, string pillPath, Func<string> tournamentId)
        {
            var label = screenRoot != null
                ? screenRoot.Find(pillPath)?.GetComponent<TextMeshProUGUI>()
                : null;

            if (label == null)
            {
                Debug.LogWarning($"[TournamentCountdown] Pill label not found at {pillPath}.");
                yield break;
            }

            while (true)
            {
                label.text = Format(Remaining(tournamentId != null ? tournamentId() : null));
                yield return new WaitForSeconds(1f);
            }
        }

        /// <summary>Time left on a tournament, or null when it cannot be resolved.</summary>
        static TimeSpan? Remaining(string tournamentId)
        {
            if (string.IsNullOrEmpty(tournamentId)) return null;

            var defs = TournamentService.Instance?.Backend?.GetTournaments();
            if (defs == null) return null;

            foreach (var d in defs)
            {
                if (!string.Equals(d.Id, tournamentId, StringComparison.Ordinal)) continue;
                return d.EndUtc - NetworkTimeProvider.Instance.UtcNow;
            }
            return null;
        }

        /// <summary>"ENDS IN: 1d 5h 25m 05s" / "終了まで: 1日 5時間 25分 05秒". Public for tests.</summary>
        public static string Format(TimeSpan? remaining)
        {
            if (!remaining.HasValue) return string.Empty;

            string d = LocalizationManager.Get("RANK_TIME_D");
            string h = LocalizationManager.Get("RANK_TIME_H");
            string m = LocalizationManager.Get("RANK_TIME_M");
            string s = LocalizationManager.Get("RANK_TIME_S");

            TimeSpan t = remaining.Value;
            if (t <= TimeSpan.Zero)
                return LocalizationManager.Get("TOURN_ENDED_PILL");

            int days = (int)t.TotalDays;
            string span;
            if (days > 0)      span = $"{days}{d} {t.Hours}{h} {t.Minutes}{m} {t.Seconds:D2}{s}";
            else if (t.Hours > 0) span = $"{t.Hours}{h} {t.Minutes}{m} {t.Seconds:D2}{s}";
            else               span = $"{t.Minutes}{m} {t.Seconds:D2}{s}";

            return string.Format(LocalizationManager.Get("TOURN_ENDS_IN"), span);
        }
    }
}
