using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    /// <summary>
    /// Static context holding both players' data for a 1v1 match.
    /// Players[0] = local player (P1), Players[1] = opponent (P2).
    /// Only populated when GameSession.IsVersus == true.
    /// Mirrors PlayerContext for P1, set by PlayerContextPopulator.
    /// P2 data set by MatchmakingModalController at OPPONENT FOUND.
    /// </summary>
    public static class MatchContext
    {
        /// <summary>Per-player data snapshot used by PlayerCardWidget in versus mode.</summary>
        public struct Player
        {
            public string DisplayName;
            public int    Level;
            public Sprite Portrait;
            public Sprite RarityBackground;
            public int    TurnCount;
        }

        /// <summary>
        /// Two-element array: [0] = P1 (local player), [1] = P2 (opponent).
        /// </summary>
        public static readonly Player[] Players = new Player[2];

        /// <summary>
        /// Index of the player whose turn is currently active (0 or 1).
        /// </summary>
        public static int ActiveIndex = 0;

        /// <summary>
        /// Fired whenever any player data changes (portrait, level, name, etc.).
        /// </summary>
        public static event System.Action OnChanged;

        /// <summary>
        /// Fired when ActiveIndex changes (turn swap).
        /// </summary>
        public static event System.Action OnActiveChanged;

        /// <summary>Sets the active player index and fires OnActiveChanged.</summary>
        public static void SetActive(int i)
        {
            ActiveIndex = i;
            OnActiveChanged?.Invoke();
        }

        /// <summary>Fires OnChanged — call after mutating Players[n] fields.</summary>
        public static void Raise() => OnChanged?.Invoke();

        /// <summary>
        /// Full reset — clears both player slots, resets ActiveIndex, fires both events.
        /// Called in GameSession.ResetSession() indirectly; callers should invoke explicitly
        /// before a new session if they need immediate UI refresh.
        /// </summary>
        public static void Reset()
        {
            Players[0] = default;
            Players[1] = default;
            ActiveIndex = 0;
            OnChanged?.Invoke();
            OnActiveChanged?.Invoke();
        }
    }
}
