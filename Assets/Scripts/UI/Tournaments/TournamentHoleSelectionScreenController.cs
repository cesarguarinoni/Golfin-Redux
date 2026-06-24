using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Stage-1 scaffold controller for the Tournament Hole Selection screen.
    /// Navigation only — the hole cards (Finished / Next / Locked) are static
    /// Stage-0 prefab instances placed in the scene; data binding lands in Stage 2
    /// (see Docs/Specs/Active/tournament_screens/SPEC.md §3).
    ///
    /// Deliberately does NOT inherit HoleSelectionScreenController — that controller
    /// destroys the scroll content on OnEnable and rebuilds normal hole cards from the
    /// hole database, which would wipe the static tournament cards.
    /// </summary>
    public class TournamentHoleSelectionScreenController : MonoBehaviour
    {
        [Header("Navigation")]
        [Tooltip("Podium icon (top-right) → Tournament Leaderboard.")]
        [SerializeField] private Button _podiumLeaderboardButton;

        [Tooltip("Silver CLOSE button → back to the upstream selection screen.")]
        [SerializeField] private Button _closeButton;

        [Tooltip("Where CLOSE returns to. Stage 1 placeholder = ModeSelection; " +
                 "swap to the real Tournament Selection screen (T7) once it exists.")]
        [SerializeField] private ScreenId _backScreen = ScreenId.ModeSelection;

        private void Awake()
        {
            if (_podiumLeaderboardButton != null)
                _podiumLeaderboardButton.onClick.AddListener(OpenLeaderboard);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void OpenLeaderboard()
        {
            ScreenManager.Instance?.ShowScreen(ScreenId.TournamentLeaderboard);
        }

        private void Close()
        {
            ScreenManager.Instance?.ShowScreen(_backScreen);
        }
    }
}
