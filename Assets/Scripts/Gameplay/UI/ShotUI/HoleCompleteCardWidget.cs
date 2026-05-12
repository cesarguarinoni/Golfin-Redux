using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// §2d: one card in the Result Screen. Two binding modes:
    ///   - BindCurrentHole(): renders the SUCCESS/FAILED header, stats block,
    ///     rewards row, and the Replay/Retry button.
    ///   - BindNextHole(): renders the NEXT/LOCKED header, hole-select-style info block,
    ///     rewards row, and the Play button (or hidden if locked).
    ///
    /// Iter-6: added runtime HoleMap sprite binding and hole-select-style Card 2 info fields.
    /// Iter-10: added _dividerBelowRewards — hidden when locked (no button below it).
    /// </summary>
    public class HoleCompleteCardWidget : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] GameObject _successHeaderRoot;  // ✓ + SUCCESS
        [SerializeField] GameObject _failedHeaderRoot;   // ✗ + FAILED
        [SerializeField] GameObject _nextHeaderRoot;     // NEXT (gold)
        [SerializeField] GameObject _lockedHeaderRoot;   // Lock + LOCKED

        [Header("Subhead")]
        [SerializeField] TMP_Text _subheadText;          // "Lomond Country Club  - Hole 6 - Par 5"

        [Header("Body — Current Hole")]
        [SerializeField] GameObject _currentBodyRoot;
        [SerializeField] Image    _holeMapLarge;         // map graphic (real map at runtime)
        [SerializeField] TMP_Text _statsBlockText;       // multi-line stats

        [Header("Body — Next Hole (hole-select style)")]
        [SerializeField] GameObject _nextBodyRoot;
        [SerializeField] Image    _nextHoleMapLarge;     // real map at runtime
        // §2d iter-8: _nextHoleParText removed — Figma does NOT have a separate Par title in Card 2 body.
        // Par is already in the subhead. The rogue "Par 4" gold title was eliminated per CESAR_REJECTION iter-7 item #8.
        [SerializeField] TMP_Text _nextHoleDescText;     // tip / description text (wide column)

        [Header("Rewards row")]
        [SerializeField] CanvasGroup _rewardsCanvasGroup; // dim via .alpha=0.5 when locked
        [SerializeField] TMP_Text    _rewardCoinText;     // "x10"
        [SerializeField] TMP_Text    _rewardRepairText;
        [SerializeField] TMP_Text    _rewardBallText;

        [Header("Buttons (Card 1 only — one is shown at a time)")]
        [SerializeField] Button _replayButton; // silver → current hole, success or failed-with-PB
        [SerializeField] Button _retryButton;  // gold   → current hole, failed-no-PB

        [Header("Buttons (Card 2 only)")]
        [SerializeField] Button _playButton;   // gold   → next hole, when unlocked

        [Header("Locked overlay")]
        [SerializeField] GameObject _darkenOverlay;       // shown only when locked

        [Header("Dividers")]
        [SerializeField] GameObject _dividerBelowRewards; // hidden when locked — no button below it

        [Header("Card layout (for locked height adjustment)")]
        [SerializeField] LayoutElement _cardLayoutElement; // minHeight=855 unlocked, 0 locked

        Action _onButtonTap;

        public void BindCurrentHole(HoleCompleteData data, Action onButtonTap)
        {
            _onButtonTap = onButtonTap;

            // Header
            SetActive(_successHeaderRoot, !data.IsFailed);
            SetActive(_failedHeaderRoot,   data.IsFailed);
            SetActive(_nextHeaderRoot,     false);
            SetActive(_lockedHeaderRoot,   false);

            // Subhead
            if (_subheadText != null)
                _subheadText.text = $"{ToTitleCase(data.CourseName)} Country Club  - Hole {data.HoleNumber} - Par {data.Par}";

            // Body
            SetActive(_currentBodyRoot, true);
            SetActive(_nextBodyRoot,    false);

            // Real hole map sprite (iter-6)
            if (_holeMapLarge != null && data.HoleMap != null)
                _holeMapLarge.sprite = data.HoleMap;

            if (_statsBlockText != null)
                _statsBlockText.text = BuildStatsBlock(data);

            // Rewards
            if (_rewardCoinText   != null) _rewardCoinText.text   = $"x{data.RewardCoinX}";
            if (_rewardRepairText != null) _rewardRepairText.text = $"x{data.RewardRepairX}";
            if (_rewardBallText   != null) _rewardBallText.text   = $"x{data.RewardBallX}";
            if (_rewardsCanvasGroup != null) _rewardsCanvasGroup.alpha = 1f;

            // Buttons — exactly one of REPLAY / RETRY visible.
            // REPLAY: success OR (failed AND has PB)
            // RETRY:  failed AND no PB
            bool useRetry = data.IsFailed && !data.HasPersonalBest;
            SetActive(_replayButton != null ? _replayButton.gameObject : null, !useRetry);
            SetActive(_retryButton  != null ? _retryButton.gameObject  : null,  useRetry);
            SetActive(_playButton   != null ? _playButton.gameObject   : null,  false);

            HookButton(_replayButton);
            HookButton(_retryButton);

            // Card 1 never shows the darken overlay.
            SetActive(_darkenOverlay, false);

            // Card 1 always has a button below rewards — divider stays visible.
            SetActive(_dividerBelowRewards, true);
        }

        public void BindNextHole(HoleCompleteData data, bool locked, Action onButtonTap)
        {
            _onButtonTap = onButtonTap;

            // Header
            SetActive(_successHeaderRoot, false);
            SetActive(_failedHeaderRoot,  false);
            SetActive(_nextHeaderRoot,   !locked);
            SetActive(_lockedHeaderRoot,  locked);

            // Subhead — show par if known
            if (_subheadText != null)
            {
                string parPart = data.NextHolePar > 0 ? $" - Par {data.NextHolePar}" : "";
                _subheadText.text = $"{ToTitleCase(data.CourseName)} Country Club  - Hole {data.NextHoleNumber}{parPart}";
            }

            // Body — locked shows only header + rewards (no map, no info).
            SetActive(_currentBodyRoot, false);
            SetActive(_nextBodyRoot,    !locked);

            if (!locked)
            {
                // Real next-hole map sprite (iter-6)
                if (_nextHoleMapLarge != null && data.NextHoleMap != null)
                    _nextHoleMapLarge.sprite = data.NextHoleMap;

                // §2d iter-8: _nextHoleParText removed — Par label was a rogue element not in Figma.
                // Par is in the subhead. Description / tip text is the sole content of the info column.
                if (_nextHoleDescText != null)
                    _nextHoleDescText.text = string.IsNullOrEmpty(data.NextHoleTipText) ? "—" : data.NextHoleTipText;
            }

            // Rewards row
            if (_rewardCoinText   != null) _rewardCoinText.text   = $"x{data.RewardCoinX}";
            if (_rewardRepairText != null) _rewardRepairText.text = $"x{data.RewardRepairX}";
            if (_rewardBallText   != null) _rewardBallText.text   = $"x{data.RewardBallX}";
            if (_rewardsCanvasGroup != null) _rewardsCanvasGroup.alpha = locked ? 0.5f : 1f;

            // Buttons
            SetActive(_replayButton != null ? _replayButton.gameObject : null, false);
            SetActive(_retryButton  != null ? _retryButton.gameObject  : null, false);
            SetActive(_playButton   != null ? _playButton.gameObject   : null, !locked);
            HookButton(_playButton);

            // Darken overlay shown only when locked.
            SetActive(_darkenOverlay, locked);

            // §2d iter-10 Bug B: Divider below rewards hidden when locked — no button below it.
            // When unlocked, PLAY button is visible so the divider must be shown.
            SetActive(_dividerBelowRewards, !locked);

            // §2d iter-9 F4: LOCKED card skips the 855px minHeight so CSF resolves the shorter
            // header+subhead+divider+rewards height (~280-360px per Figma).
            if (_cardLayoutElement != null)
                _cardLayoutElement.minHeight = locked ? 0f : 855f;
        }

        // ── Expose internal state for unit tests ─────────────────────────────────

        internal bool IsSuccessHeaderActive  => _successHeaderRoot != null && _successHeaderRoot.activeSelf;
        internal bool IsFailedHeaderActive   => _failedHeaderRoot  != null && _failedHeaderRoot.activeSelf;
        internal bool IsNextHeaderActive     => _nextHeaderRoot    != null && _nextHeaderRoot.activeSelf;
        internal bool IsLockedHeaderActive   => _lockedHeaderRoot  != null && _lockedHeaderRoot.activeSelf;
        internal bool IsReplayButtonActive   => _replayButton  != null && _replayButton.gameObject.activeSelf;
        internal bool IsRetryButtonActive    => _retryButton   != null && _retryButton.gameObject.activeSelf;
        internal bool IsPlayButtonActive     => _playButton    != null && _playButton.gameObject.activeSelf;
        internal bool IsDarkenOverlayActive  => _darkenOverlay != null && _darkenOverlay.activeSelf;

        // ── Helpers ───────────────────────────────────────────────────────

        static string BuildStatsBlock(HoleCompleteData d)
        {
            // Match Figma layout:
            //   TEE OFF: REGULAR
            //   STROKES: 5 (PAR)      ← color: green #50C878 if success, orange #D16A47 if failed
            //   BEST: 5 (PAR)
            //   TIME: 00:02:34
            //   BEST: 00:02:34
            //
            // §E Color tokens: success green #50C878, failed orange #D16A47 (single-colour fallback
            // for the 3-stop gradient since TMP rich-text does not support linear gradients).
            string strokesColor = d.IsFailed ? "D16A47" : "50C878";
            string strokesLine  = $"<b>STROKES:</b> <color=#{strokesColor}>{d.Strokes} ({d.ScoreLabel.ToUpperInvariant()})</color>";
            string bestLine     = string.IsNullOrEmpty(d.BestStrokesLabel)
                                  ? $"<b>BEST:</b> {d.BestStrokes}"
                                  : $"<b>BEST:</b> {d.BestStrokes} ({d.BestStrokesLabel.ToUpperInvariant()})";
            return string.Join('\n',
                $"<b>TEE OFF:</b> {d.TeeName}",
                strokesLine,
                bestLine,
                $"<b>TIME:</b> {d.TimeStr}",
                $"<b>BEST:</b> {d.BestTimeStr}");
        }

        static string ToTitleCase(string courseName)
        {
            if (string.IsNullOrEmpty(courseName)) return courseName;
            // "LOMOND" → "Lomond"
            return char.ToUpperInvariant(courseName[0]) + courseName.Substring(1).ToLowerInvariant();
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null) go.SetActive(on);
        }

        void HookButton(Button btn)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _onButtonTap?.Invoke());
        }
    }
}
