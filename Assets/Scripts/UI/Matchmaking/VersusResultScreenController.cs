// ─────────────────────────────────────────────────────────────────────────────
// VersusResultScreenController
// Stage 0: visual-state preview via ShowWin() / ShowLose() (sample data).
// Stage 1: live binding via ShowResult() from MatchContext + LeaderboardManager.
//
// Central RESULTS panel for 1v1 match end.
// Two states driven by ShowWin() / ShowLose() (Stage 0) or ShowResult() (Stage 1):
//   Win  → left column WINNER (green), right LOSER (orange), rewards bright.
//   Lose → left column LOSER (orange), right WINNER (green), rewards dimmed.
//
// Reward dimming (Camera.Render-visible — edit-mode capture):
//   _rewardRowGroup.alpha = 0.5  → CanvasGroup alpha (runtime compositing)
//   _reward[1-3]Icon.color      → Image.color per icon (direct child tint)
//   _reward[1-3]Amount.color    → TMP.color per amount label (direct child tint)
//   All set together so dimming is visible in ALL capture contexts.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Roster;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.UI.Rankings;

namespace Golfin.UI.Matchmaking
{
    /// <summary>
    /// Visual-state driver for VersusResultScreen.prefab.
    /// Stage 0 exposes ShowWin() / ShowLose() for sample-data preview.
    /// Stage 1 exposes ShowResult() for live-data binding.
    /// </summary>
    public class VersusResultScreenController : MonoBehaviour
    {
        // ── Column outcome labels ─────────────────────────────────────────────
        [Header("Column Outcome Labels")]
        [SerializeField] private TextMeshProUGUI _leftOutcomeLabel  = null!;  // "WINNER" or "LOSER"
        [SerializeField] private TextMeshProUGUI _rightOutcomeLabel = null!;  // "WINNER" or "LOSER"

        // ── Portrait cards ────────────────────────────────────────────────────
        [Header("Portrait Cards (CharacterThumbnailCard instances)")]
        [SerializeField] private CharacterThumbnailCard _leftCard  = null!;
        [SerializeField] private CharacterThumbnailCard _rightCard = null!;

        // ── Username / Rank lines ─────────────────────────────────────────────
        [Header("Username / Rank Lines")]
        [SerializeField] private TextMeshProUGUI _leftUsernameText  = null!;
        [SerializeField] private TextMeshProUGUI _rightUsernameText = null!;
        [SerializeField] private TextMeshProUGUI _leftRankText      = null!;
        [SerializeField] private TextMeshProUGUI _rightRankText     = null!;

        // ── Hole Info ─────────────────────────────────────────────────────────
        [Header("Hole Info")]
        [SerializeField] private TextMeshProUGUI _holeInfoText = null!;

        // ── Reward row ────────────────────────────────────────────────────────
        [Header("Reward Row (3 icon+amount pairs)")]
        [SerializeField] private CanvasGroup _rewardRowGroup = null!;   // α=1 win / α=0.5 lose (runtime compositing)
        // Reward icon Images — tinted directly so Camera.Render edit-mode sees the dimming
        [SerializeField] private Image _reward1Icon = null!;
        [SerializeField] private Image _reward2Icon = null!;
        [SerializeField] private Image _reward3Icon = null!;
        [SerializeField] private TextMeshProUGUI _reward1Amount = null!;
        [SerializeField] private TextMeshProUGUI _reward2Amount = null!;
        [SerializeField] private TextMeshProUGUI _reward3Amount = null!;

        // ── NEW MATCH button ──────────────────────────────────────────────────
        [Header("Buttons")]
        [SerializeField] private Button _newMatchButton = null!;

        // ── Color constants ───────────────────────────────────────────────────
        private static readonly Color WinnerColor = new Color(0x50 / 255f, 0xC8 / 255f, 0x78 / 255f, 1f); // #50C878 green  (node 13274:877)
        private static readonly Color LoserColor  = new Color(0xC0 / 255f, 0x40 / 255f, 0x00 / 255f, 1f); // #C04000 burnt orange (node 13275:2358)

        private const string WinnerLabel = "WINNER";
        private const string LoserLabel  = "LOSER";

        // Reward dim/normal colors for direct child tinting (Camera.Render visible)
        private static readonly Color RewardChildDim    = new Color(1f, 1f, 1f, 0.5f); // half-alpha white = dim
        private static readonly Color RewardChildNormal = Color.white;                  // full white = normal

        // ── Sample data (Stage 0 preview) ─────────────────────────────────────
        private const string SampleLeftUsername  = "USERNAME";
        private const string SampleRightUsername = "USERNAME";
        private const string SampleLeftRankNum   = "#142";
        private const string SampleRightRankNum  = "#255";
        private const string WinnerColorHex      = "#50C878";
        private const string LoserColorHex       = "#C04000";
        private const string SampleHoleInfo      = "Lomond Country Club  - Hole 5";
        private const string SampleReward1       = "x200";
        private const string SampleReward2       = "x04";
        private const string SampleReward3       = "x02";

        // ── Hole info formatting constants ────────────────────────────────────
        private static readonly string[] HoleNames = {
            "Hole 1","Hole 2","Hole 3","Hole 4","Hole 5","Hole 6","Hole 7","Hole 8","Hole 9",
            "Hole 10","Hole 11","Hole 12","Hole 13","Hole 14","Hole 15","Hole 16","Hole 17","Hole 18"
        };

        // ─────────────────────────────────────────────────────────────────────
        // Stage 1 — live-data binding
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Stage 1: bind live match data from MatchContext + LeaderboardManager, then display.
        /// Left column = local player (P1), right column = opponent (P2).
        /// </summary>
        public void ShowResult(
            GameSession.MatchOutcome outcome,
            MatchContext.Player      localPlayer,
            MatchContext.Player      opponentPlayer,
            int                      holeNumber,
            Action?                  onNewMatch = null)
        {
            bool localWon = outcome == GameSession.MatchOutcome.P1Win;
            // Draw: treat as local lose (left = LOSER, right = LOSER) — placeholder per D2
            // In Stage 3, DRAW gets its own visual variant.

            // ── Outcome labels ────────────────────────────────────────────────
            SetOutcomeLabelsLive(leftWon: localWon);

            // ── Portrait cards ────────────────────────────────────────────────
            // Left = local (P1); right = opponent (P2)
            if (_leftCard != null && !string.IsNullOrEmpty(CharacterManager.Instance?.GetSelectedCharacterId()))
            {
                string localCharId = CharacterManager.Instance!.GetSelectedCharacterId()!;
                _leftCard.InitializeFromTemplate(localCharId, localPlayer.Level);
            }
            // Opponent portrait comes from MatchContext (set at matchmaking time)
            // We don't have the charId directly from MatchContext.Player — use opponentPlayer.Portrait
            // as a Sprite if available, else skip. MatchmakingModalController seeds charId into
            // the card directly; Stage 1 reads back via the card's last-initialized state which
            // is already set from matchmaking. If re-binding is needed, VersusResultHandler can
            // pass charId directly. For now we rebind using DisplayName match against LeaderboardManager.
            BindOpponentCard(opponentPlayer);

            // ── Username ──────────────────────────────────────────────────────
            if (_leftUsernameText  != null) _leftUsernameText.text  = "You";
            if (_rightUsernameText != null) _rightUsernameText.text = string.IsNullOrEmpty(opponentPlayer.DisplayName)
                                                                       ? "OPPONENT"
                                                                       : opponentPlayer.DisplayName;

            // ── Rank ──────────────────────────────────────────────────────────
            BindRankText(localWon, opponentPlayer);

            // ── Hole info ─────────────────────────────────────────────────────
            if (_holeInfoText != null)
            {
                string holeName = holeNumber >= 1 && holeNumber <= 18
                    ? HoleNames[holeNumber - 1]
                    : $"Hole {holeNumber}";
                _holeInfoText.text = $"Lomond Country Club  - {holeName}";
            }

            // ── Reward row: placeholder (Stage 2 binds real data) ─────────────
            // Win = bright, Lose/Draw = dimmed.  Same logic as ShowWin/ShowLose.
            if (_rewardRowGroup != null) _rewardRowGroup.alpha = localWon ? 1f : 0.5f;
            SetRewardChildrenColor(localWon ? RewardChildNormal : RewardChildDim);

            // ── NEW MATCH button ──────────────────────────────────────────────
            if (_newMatchButton != null)
            {
                _newMatchButton.onClick.RemoveAllListeners();
                if (onNewMatch != null)
                    _newMatchButton.onClick.AddListener(() => onNewMatch());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Stage 0 — sample data preview methods (kept for backward compat / editor testing)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Stage-0 preview: local player won (left = WINNER).</summary>
        public void ShowWin()
        {
            SetOutcomeLabels(leftWon: true);
            SetSampleText();
            // Reward row: full brightness
            if (_rewardRowGroup != null) _rewardRowGroup.alpha = 1f;
            SetRewardChildrenColor(RewardChildNormal);
        }

        /// <summary>Stage-0 preview: local player lost (left = LOSER).</summary>
        public void ShowLose()
        {
            SetOutcomeLabels(leftWon: false);
            SetSampleText();
            // Reward row: dimmed — both CanvasGroup.alpha (runtime) AND direct child tint (edit-mode Camera.Render)
            if (_rewardRowGroup != null) _rewardRowGroup.alpha = 0.5f;
            SetRewardChildrenColor(RewardChildDim);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void BindOpponentCard(MatchContext.Player opponentPlayer)
        {
            if (_rightCard == null) return;

            // Try to bind via LeaderboardManager to find the opponent's character ID
            if (LeaderboardManager.Instance != null)
            {
                var entries = LeaderboardManager.Instance.GetRanking(LeaderboardPeriod.Daily);
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        if (!entry.IsPlayer && entry.DisplayName == opponentPlayer.DisplayName
                            && !string.IsNullOrEmpty(entry.CharacterId))
                        {
                            _rightCard.InitializeFromTemplate(entry.CharacterId, entry.Level);
                            return;
                        }
                    }
                }
            }

            // Fallback: the right card was initialized by MatchmakingModalController
            // at opponent-found time; leave it as-is (no re-bind needed).
        }

        private void BindRankText(bool localWon, MatchContext.Player opponentPlayer)
        {
            string localRankStr    = "—";
            string opponentRankStr = "—";

            if (LeaderboardManager.Instance != null)
            {
                var playerEntry = LeaderboardManager.Instance.GetPlayerEntry(LeaderboardPeriod.Daily);
                if (playerEntry.Rank > 0)
                    localRankStr = $"#{playerEntry.Rank}";

                // Opponent rank: resolve THIS opponent by DisplayName (mirrors BindOpponentCard),
                // not just the first non-player entry — otherwise every opponent showed the board
                // leader's rank regardless of who was matched. Leave "—" if the matched opponent
                // isn't on the leaderboard rather than displaying a misleading rank.
                if (!string.IsNullOrEmpty(opponentPlayer.DisplayName))
                {
                    var entries = LeaderboardManager.Instance.GetRanking(LeaderboardPeriod.Daily);
                    if (entries != null)
                    {
                        foreach (var e in entries)
                        {
                            if (!e.IsPlayer && e.Rank > 0 && e.DisplayName == opponentPlayer.DisplayName)
                            {
                                opponentRankStr = $"#{e.Rank}";
                                break;
                            }
                        }
                    }
                }
            }

            // "RANK: " white base, number colored by outcome (green = winner, orange = loser)
            string localNumColor    = localWon ? WinnerColorHex : LoserColorHex;
            string opponentNumColor = localWon ? LoserColorHex  : WinnerColorHex;

            if (_leftRankText != null)
            {
                _leftRankText.color = Color.white;
                _leftRankText.text  = $"RANK: <color={localNumColor}>{localRankStr}</color>";
            }
            if (_rightRankText != null)
            {
                _rightRankText.color = Color.white;
                _rightRankText.text  = $"RANK: <color={opponentNumColor}>{opponentRankStr}</color>";
            }
        }

        /// <summary>Tints all reward child Images and TMP labels — visible in Camera.Render edit-mode capture.</summary>
        private void SetRewardChildrenColor(Color c)
        {
            if (_reward1Icon   != null) _reward1Icon.color   = c;
            if (_reward2Icon   != null) _reward2Icon.color   = c;
            if (_reward3Icon   != null) _reward3Icon.color   = c;
            if (_reward1Amount != null) _reward1Amount.color = c;
            if (_reward2Amount != null) _reward2Amount.color = c;
            if (_reward3Amount != null) _reward3Amount.color = c;
        }

        private void SetOutcomeLabels(bool leftWon)
        {
            if (_leftOutcomeLabel != null)
            {
                _leftOutcomeLabel.text  = leftWon ? WinnerLabel : LoserLabel;
                _leftOutcomeLabel.color = leftWon ? WinnerColor : LoserColor;
            }
            if (_rightOutcomeLabel != null)
            {
                _rightOutcomeLabel.text  = leftWon ? LoserLabel : WinnerLabel;
                _rightOutcomeLabel.color = leftWon ? LoserColor : WinnerColor;
            }

            // Fix #5 (CESAR_REJECTION iter-7): "RANK:" white, only number colored via rich text.
            string leftNumColor  = leftWon ? WinnerColorHex : LoserColorHex;
            string rightNumColor = leftWon ? LoserColorHex  : WinnerColorHex;
            if (_leftRankText  != null)
            {
                _leftRankText.color  = Color.white;
                _leftRankText.text   = $"RANK: <color={leftNumColor}>{SampleLeftRankNum}</color>";
            }
            if (_rightRankText != null)
            {
                _rightRankText.color = Color.white;
                _rightRankText.text  = $"RANK: <color={rightNumColor}>{SampleRightRankNum}</color>";
            }
        }

        private void SetOutcomeLabelsLive(bool leftWon)
        {
            if (_leftOutcomeLabel != null)
            {
                _leftOutcomeLabel.text  = leftWon ? WinnerLabel : LoserLabel;
                _leftOutcomeLabel.color = leftWon ? WinnerColor : LoserColor;
            }
            if (_rightOutcomeLabel != null)
            {
                _rightOutcomeLabel.text  = leftWon ? LoserLabel : WinnerLabel;
                _rightOutcomeLabel.color = leftWon ? LoserColor : WinnerColor;
            }
        }

        private void SetSampleText()
        {
            if (_leftUsernameText  != null) _leftUsernameText.text  = SampleLeftUsername;
            if (_rightUsernameText != null) _rightUsernameText.text = SampleRightUsername;
            // Rank text is set in SetOutcomeLabels() with per-state rich-text color split (Fix #5)
            if (_holeInfoText      != null) _holeInfoText.text      = SampleHoleInfo;
            if (_reward1Amount     != null) _reward1Amount.text     = SampleReward1;
            if (_reward2Amount     != null) _reward2Amount.text     = SampleReward2;
            if (_reward3Amount     != null) _reward3Amount.text     = SampleReward3;
        }

#if UNITY_EDITOR
        [ContextMenu("Preview: WIN state")]
        private void PreviewWin()  => ShowWin();

        [ContextMenu("Preview: LOSE state")]
        private void PreviewLose() => ShowLose();
#endif
    }
}
