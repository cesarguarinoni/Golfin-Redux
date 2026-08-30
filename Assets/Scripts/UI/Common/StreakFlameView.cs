// daily_mission_home_pill §3 — the streak badge, shared by the Home pill and the Mission
// Selection daily card.
using TMPro;
using UnityEngine;

namespace Golfin.UI.Common
{
    /// <summary>
    /// The flame with the streak number in it.
    ///
    /// ⚠️ ONE PREFAB, TWO HOSTS, AND THAT IS THE POINT. The Home pill and the daily card both
    /// show the same number, and before this the card drew it as the sentence "{0} day streak"
    /// while the pill would have drawn a flame — two renderings of one fact, guaranteed to drift
    /// the first time either changed. The card's text streak is now this prefab too.
    ///
    /// The zero rule lives here and nowhere else: <b>a streak of zero is not a streak</b>, so the
    /// whole badge is switched off rather than reading "0". Callers pass the number; they do not
    /// decide visibility, which is what stopped the card's old two-places-disagree bug
    /// (<c>MissionCardController.ApplyDailyChrome</c>).
    /// </summary>
    public sealed class StreakFlameView : MonoBehaviour
    {
        [Tooltip("The number drawn inside the flame. Auto-sizes so two digits still fit.")]
        [SerializeField] private TextMeshProUGUI streakNumber;

        /// <summary>Last number we were told about, so a host can re-apply without re-deciding.</summary>
        public int Streak { get; private set; }

        /// <summary>
        /// Show the badge for <paramref name="streak"/>, or hide it outright at zero or below.
        /// Safe to call on an already-inactive badge — the reference survives deactivation.
        /// </summary>
        public void SetStreak(int streak)
        {
            Streak = streak;
            bool show = streak >= 1;

            // Write BEFORE activating: a frame where the badge is visible carrying the previous
            // player's streak is a real, if brief, wrong number.
            if (show && streakNumber != null)
                streakNumber.SetText("{0}", streak);   // no string alloc — TMP formats in place

            if (gameObject.activeSelf != show) gameObject.SetActive(show);
        }

        /// <summary>Hide the badge without claiming a streak value. Used when the host is not a
        /// daily at all.</summary>
        public void Hide()
        {
            Streak = 0;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
