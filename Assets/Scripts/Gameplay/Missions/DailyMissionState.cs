// daily_mission_home_pill §2 — the one place the Home pill and the Mission Selection daily
// card agree about today's daily.
using System;

namespace Golfin.Gameplay.Missions
{
    /// <summary>
    /// Today's daily, as the last successful <c>GET /missions/daily</c> described it.
    ///
    /// ⚠️ IT EXISTS BECAUSE TWO SURFACES READ THE SAME FACT AND WOULD OTHERWISE DRIFT. The Home
    /// pill says "there is a daily waiting" and the Mission Selection card says what it is; before
    /// this, each fetched independently, so claiming on one screen left the other still advertising
    /// an unclaimed daily until its own next fetch. One static, one event, and the two can no
    /// longer disagree.
    ///
    /// It is deliberately NOT a cache with a policy — no expiry, no refresh timer, no persistence.
    /// It is the last answer, and every screen still fetches on entry. The value it adds is that a
    /// claim or a rollover propagates INSTANTLY to whatever is already on screen, which is the only
    /// thing a per-screen fetch cannot do.
    /// </summary>
    public static class DailyMissionState
    {
        /// <summary>The server's UTC date for this recipe, <c>yyyy-MM-dd</c>. Empty until a
        /// fetch answers. This is also the ROLLOVER SEAM: a test sets it to a past date and the
        /// pill's tick treats the next second as midnight.</summary>
        public static string Date = "";

        public static int Streak;
        public static bool Claimed;

        /// <summary>True when the server returned a real recipe. A failed fetch leaves this
        /// false, which is what keeps a stale pill off the screen (SPEC §2 "never a stale
        /// pill").</summary>
        public static bool HasRecipe;

        /// <summary>True once any fetch has answered — success or not. Distinguishes "no daily"
        /// from "we have not asked yet", which the pill needs so it does not animate out of a
        /// state it was never in.</summary>
        public static bool Known;

        /// <summary>Raised whenever any field above actually changed value.</summary>
        public static event Action OnChanged;

        /// <summary>The pill is on screen exactly when there is a daily and it is unclaimed.</summary>
        public static bool ShouldShowPill => HasRecipe && !Claimed;

        /// <summary>Adopt a successful fetch.</summary>
        public static void Set(string date, int streak, bool claimed, bool hasRecipe)
        {
            bool changed = Date != (date ?? "") || Streak != streak
                        || Claimed != claimed || HasRecipe != hasRecipe || !Known;
            Date      = date ?? "";
            Streak    = streak;
            Claimed   = claimed;
            HasRecipe = hasRecipe;
            Known     = true;
            if (changed) Raise();
        }

        /// <summary>
        /// A fetch answered with no recipe (offline, signed out, or a date nothing was generated
        /// for). Keeps <see cref="Known"/> true so the pill knows the answer was "none" rather
        /// than "not asked".
        /// </summary>
        public static void SetNoDaily()
        {
            bool changed = HasRecipe || !Known;
            HasRecipe = false;
            Known     = true;
            if (changed) Raise();
        }

        /// <summary>
        /// The daily was claimed. Called from the claim path so the pill leaves the moment the
        /// server says the round paid, without waiting for Home's next fetch.
        /// </summary>
        public static void MarkClaimed(int streak)
        {
            bool changed = !Claimed || Streak != streak;
            Claimed = true;
            Streak  = streak;
            Known   = true;
            if (changed) Raise();
        }

        /// <summary>UTC midnight passed, or the player signed out. Forget everything; the next
        /// fetch decides what today holds.</summary>
        public static void Clear()
        {
            bool changed = Known || HasRecipe || Claimed || Streak != 0 || Date.Length > 0;
            Date = ""; Streak = 0; Claimed = false; HasRecipe = false; Known = false;
            if (changed) Raise();
        }

        /// <summary>Clear WITHOUT notifying — for test setup, which must not fire listeners
        /// belonging to a previous test's objects.</summary>
        public static void ResetForTest()
        {
            Date = ""; Streak = 0; Claimed = false; HasRecipe = false; Known = false;
            OnChanged = null;
        }

        private static void Raise()
        {
            var handler = OnChanged;
            if (handler == null) return;
            try { handler(); }
            catch (Exception ex) { UnityEngine.Debug.LogWarning($"[DailyMissionState] listener threw: {ex.Message}"); }
        }
    }
}
