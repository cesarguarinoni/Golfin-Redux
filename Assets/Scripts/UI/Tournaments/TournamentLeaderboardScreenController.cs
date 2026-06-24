using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Stage-1 scaffold controller for the Tournament Leaderboard screen.
    /// Navigation + static title only. Podium, ranking rows, sticky "you" row and
    /// empty-state are static Stage-0 prefab instances placed in the scene; live data
    /// (GetLeaderboard, projected bots, live sticky row) binds in Stage 2
    /// (see Docs/Specs/Active/tournament_screens/SPEC.md §3).
    ///
    /// Deliberately does NOT inherit RankingsScreenController — that controller drives
    /// period tabs, a reset countdown and rebuilds rows from LeaderboardManager, none of
    /// which apply to a tournament board.
    /// </summary>
    public class TournamentLeaderboardScreenController : MonoBehaviour
    {
        [Header("Title")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private string _titleText = "TOURNAMENT LEADERBOARD";

        [Header("Navigation")]
        [Tooltip("Silver CLOSE button → back to Tournament Hole Selection.")]
        [SerializeField] private Button _closeButton;

        [SerializeField] private ScreenId _backScreen = ScreenId.TournamentHoleSelection;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (_titleLabel != null && !string.IsNullOrEmpty(_titleText))
                _titleLabel.text = _titleText;
        }

        private void Close()
        {
            ScreenManager.Instance?.ShowScreen(_backScreen);
        }
    }
}
