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

            // ── Phase 2a additions (additive — do NOT remove Phase-1 fields above) ──
            /// <summary>Current ball resting position in world space. Set to tee at match start.</summary>
            public Vector3 Lie;
            /// <summary>Number of shots taken this hole by this player.</summary>
            public int     Strokes;
            /// <summary>True once this player's ball has reached InCup.</summary>
            public bool    HoledOut;
            /// <summary>The Strokes value at which this player holed out (0 until holed).</summary>
            public int     HoleOutStroke;
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

        // ── Phase 2a additions ────────────────────────────────────────────────

        /// <summary>
        /// Reset per-match state for both players. Sets both lies to <paramref name="tee"/>,
        /// clears Strokes/HoledOut/HoleOutStroke, and sets ActiveIndex to 0 (P1 shoots first).
        /// Preserves DisplayName/Level/Portrait/RarityBackground so the card data set at
        /// matchmaking time is not lost.
        /// </summary>
        public static void ResetMatchState(Vector3 tee)
        {
            for (int i = 0; i < 2; i++)
            {
                Players[i].Lie           = tee;
                Players[i].Strokes       = 0;
                Players[i].HoledOut      = false;
                Players[i].HoleOutStroke = 0;
            }
            ActiveIndex = 0;
            OnChanged?.Invoke();
            OnActiveChanged?.Invoke();
        }

        /// <summary>Returns the other player index: 0 → 1, 1 → 0.</summary>
        public static int Other(int i) => 1 - i;
    }
}
