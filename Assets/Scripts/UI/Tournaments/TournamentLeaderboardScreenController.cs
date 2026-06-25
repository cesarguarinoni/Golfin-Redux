using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.UI.Rankings;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Stage-1 scaffold controller for the Tournament Leaderboard screen.
    /// Navigation + title, plus placeholder population of the podium / rows / sticky with
    /// the SAME fake-bot roster the normal Rankings screen uses (LeaderboardManager →
    /// fake_players.csv), bound via the existing Top3CardWidget / RankingsCardWidget so
    /// the character art + rarity colours match. The only tournament-specific twist is the
    /// score pill, which is overridden to "<n> STROKES" instead of an RP total.
    ///
    /// Live tournament data (GetLeaderboard, projected bots, real sticky row) replaces this
    /// placeholder fill in Stage 2 (see Docs/Specs/Active/tournament_screens/SPEC.md §3).
    ///
    /// Deliberately does NOT inherit RankingsScreenController — that controller drives
    /// period tabs, a reset countdown and rebuilds rows, none of which apply here.
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

        // Relative paths from this screen root to the reused Rankings hierarchy.
        private const string ModalPath = "ContentArea/BarsArea/RankingsArea/Modal";

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (_titleLabel != null && !string.IsNullOrEmpty(_titleText))
                _titleLabel.text = _titleText;

            PopulateBots();
        }

        private void Close()
        {
            ScreenManager.Instance?.ShowScreen(_backScreen);
        }

        // ── Placeholder bot fill (mirrors normal Rankings) ────────────────────────

        private void PopulateBots()
        {
            var modal = transform.Find(ModalPath);
            if (modal == null) return;

            // Reuse the normal-rankings fake roster (fake_players.csv via LeaderboardManager).
            IReadOnlyList<LeaderboardEntry> ranking =
                LeaderboardManager.Instance != null
                    ? LeaderboardManager.Instance.GetRanking(LeaderboardPeriod.Daily)
                    : null;
            if (ranking == null || ranking.Count == 0) return;

            var bots = ranking.Where(e => !e.IsPlayer).ToList();
            if (bots.Count == 0) return;

            // ── Podium #1 / #2 / #3 (lowest strokes wins) ─────────────────────────
            BindCard(modal.Find("Top3/Top1Card"), bots, 0, rank: 1, strokes: 68, isPodium: true);
            BindCard(modal.Find("Top3/Top2Card"), bots, 1, rank: 2, strokes: 70, isPodium: true);
            BindCard(modal.Find("Top3/Top3Card"), bots, 2, rank: 3, strokes: 71, isPodium: true);

            // ── Ranking rows (#4+) ────────────────────────────────────────────────
            var grid = modal.Find("Bottom97/ScrollArea/Viewport/GridContent");
            if (grid != null)
            {
                int botIndex = 3;
                int rank = 4;
                foreach (Transform row in grid)
                {
                    if (!row.name.StartsWith("TournamentRankingRow")) continue;
                    BindCard(row, bots, botIndex, rank, 67 + rank, isPodium: false);
                    botIndex++;
                    rank++;
                }
            }

            // ── Sticky "you" row — the real player, rank "--", LIVE ───────────────
            var sticky = modal.Find("TournamentPlayerStickyRow");
            if (sticky != null)
            {
                LeaderboardEntry? player = null;
                foreach (var e in ranking) { if (e.IsPlayer) { player = e; break; } }

                var widget = sticky.GetComponent<RankingsCardWidget>()
                          ?? sticky.gameObject.AddComponent<RankingsCardWidget>();
                if (player.HasValue) widget.Bind(player.Value);
                // Rank is "--" until the player finishes the tournament.
                var rankLabel = sticky.Find("RankingsCard/Rank")?.GetComponent<TextMeshProUGUI>();
                if (rankLabel != null) rankLabel.text = "--";
                SetStrokes(sticky, 82);
            }
        }

        private static void BindCard(Transform card, List<LeaderboardEntry> bots, int botIndex,
                                     int rank, int strokes, bool isPodium)
        {
            if (card == null || bots.Count == 0) return;
            var entry = bots[botIndex % bots.Count];
            entry.Rank = rank;
            entry.IsTie = false;

            if (isPodium)
            {
                var w = card.GetComponent<Top3CardWidget>() ?? card.gameObject.AddComponent<Top3CardWidget>();
                w.Bind(entry);
            }
            else
            {
                var w = card.GetComponent<RankingsCardWidget>() ?? card.gameObject.AddComponent<RankingsCardWidget>();
                w.Bind(entry);
            }
            SetStrokes(card, strokes);
        }

        /// <summary>Override the reused RP score pill with the tournament STROKES total.</summary>
        private static void SetStrokes(Transform card, int strokes)
        {
            var label = card.Find("RewardPoints/Background/NameLabel")?.GetComponent<TextMeshProUGUI>()
                     ?? card.Find("RankingsCard/RewardPoints/Background/NameLabel")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = $"{strokes} STROKES";
        }
    }
}
