// ─────────────────────────────────────────────────────────────────────────────
// VersusResultScreenController
// Stage 0: Prefab-only visual check.
//
// Central RESULTS panel for 1v1 match end.
// Two states driven by ShowWin() / ShowLose():
//   Win  → left column WINNER (green), right LOSER (red), rewards bright.
//   Lose → left column LOSER  (red),  right WINNER (green), rewards dimmed.
//
// Stage 0 NOTE: ShowWin() / ShowLose() use baked-in sample data only.
// Stage 1 will bind from MatchContext + LeaderboardManager.
//
// Reward dimming (Camera.Render-visible — edit-mode capture):
//   _rewardRowGroup.alpha = 0.5  → CanvasGroup alpha (runtime compositing)
//   _reward[1-3]Icon.color      → Image.color per icon (direct child tint)
//   _reward[1-3]Amount.color    → TMP.color per amount label (direct child tint)
//   All set together so dimming is visible in ALL capture contexts.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Roster;

namespace Golfin.UI.Matchmaking
{
    /// <summary>
    /// Visual-state driver for VersusResultScreen.prefab.
    /// Stage 0 exposes ShowWin() / ShowLose() for sample-data preview.
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

        // ── Sample data ───────────────────────────────────────────────────────
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
            // Color split flips with win/lose: winner number = green, loser number = orange.
            string leftNumColor  = leftWon ? WinnerColorHex : LoserColorHex;
            string rightNumColor = leftWon ? LoserColorHex  : WinnerColorHex;
            if (_leftRankText  != null)
            {
                _leftRankText.color  = Color.white; // base = white, rich text colors the number
                _leftRankText.text   = $"RANK: <color={leftNumColor}>{SampleLeftRankNum}</color>";
            }
            if (_rightRankText != null)
            {
                _rightRankText.color = Color.white;
                _rightRankText.text  = $"RANK: <color={rightNumColor}>{SampleRightRankNum}</color>";
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
