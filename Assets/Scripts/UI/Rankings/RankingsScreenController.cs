#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.Utilities;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Controller for the RankingsScreen prefab.
    /// Wires real data to the existing UI hierarchy via LeaderboardManager.
    ///
    /// Confirmed prefab node paths (from MCP prefab inspection):
    ///   ContentArea/BarsArea/TabBar/DailyTab        — Button (tab)
    ///   ContentArea/BarsArea/TabBar/WeeklyTab
    ///   ContentArea/BarsArea/TabBar/MonthlyTab
    ///   ContentArea/BarsArea/TabBar/HistoryTab
    ///   ContentArea/BarsArea/InfoArea/League/Label   — TextMeshProUGUI
    ///   ContentArea/BarsArea/InfoArea/Reset/Label    — TextMeshProUGUI
    ///   ContentArea/BarsArea/RankingsArea/Modal/Top3/Top2Card  — Top3CardWidget
    ///   ContentArea/BarsArea/RankingsArea/Modal/Top3/Top1Card
    ///   ContentArea/BarsArea/RankingsArea/Modal/Top3/Top3Card
    ///   ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea/Viewport/GridContent — parent for row instantiation
    ///   ContentArea/BarsArea/RankingsArea/Modal/RankingsCardUser  — pinned player row
    ///   Banner  — toggleable (Banner child of ContentArea)
    /// </summary>
    public class RankingsScreenController : MonoBehaviour
    {
        // ── Tab buttons ───────────────────────────────────────────────────────
        [Header("Tabs")]
        [SerializeField] private Button? _dailyTab;
        [SerializeField] private Button? _weeklyTab;
        [SerializeField] private Button? _monthlyTab;
        [SerializeField] private Button? _historyTab;

        // Active tab indicator images (child of each tab named "ActiveIndicator")
        private Image? _dailyIndicator;
        private Image? _weeklyIndicator;
        private Image? _monthlyIndicator;
        private Image? _historyIndicator;

        // R2-Fix F: tab label TMP components for gold/silver gradient
        private TextMeshProUGUI? _dailyTabLabel;
        private TextMeshProUGUI? _weeklyTabLabel;
        private TextMeshProUGUI? _monthlyTabLabel;
        private TextMeshProUGUI? _historyTabLabel;

        // ── Info area ─────────────────────────────────────────────────────────
        [Header("Info Area")]
        [SerializeField] private TextMeshProUGUI? _leagueLabel;
        [SerializeField] private TextMeshProUGUI? _resetLabel;

        // ── Top 3 podium ──────────────────────────────────────────────────────
        [Header("Top 3 Podium")]
        [SerializeField] private Transform? _top1Card;
        [SerializeField] private Transform? _top2Card;
        [SerializeField] private Transform? _top3Card;

        // ── Scroll list ───────────────────────────────────────────────────────
        [Header("List")]
        [SerializeField] private Transform? _gridContent;
        [SerializeField] private GameObject? _rankingsCardPrefab;

        // ── Pinned player row ─────────────────────────────────────────────────
        [Header("Pinned Row")]
        [SerializeField] private Transform? _rankingsCardUser;

        // ── Banner ────────────────────────────────────────────────────────────
        [Header("Banner")]
        [SerializeField] private GameObject? _banner;
        [SerializeField] private bool _showBanner = true;

        // ── Back navigation ───────────────────────────────────────────────────
        [Header("Back Navigation")]
        [SerializeField] private Button? _backButton;

        // ── Screen back target ────────────────────────────────────────────────
        private ScreenId _returnScreen = ScreenId.Home;

        // ── State ─────────────────────────────────────────────────────────────
        private LeaderboardPeriod _activePeriod = LeaderboardPeriod.Daily;
        private Coroutine? _countdownCoroutine;
        private readonly List<GameObject> _rowPool = new List<GameObject>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Wire tab buttons
            if (_dailyTab   != null) _dailyTab.onClick.AddListener(()   => OnTabClicked(LeaderboardPeriod.Daily));
            if (_weeklyTab  != null) _weeklyTab.onClick.AddListener(()  => OnTabClicked(LeaderboardPeriod.Weekly));
            if (_monthlyTab != null) _monthlyTab.onClick.AddListener(() => OnTabClicked(LeaderboardPeriod.Monthly));
            if (_historyTab != null) _historyTab.onClick.AddListener(() => OnTabClicked(LeaderboardPeriod.Historic));

            // Cache indicator images
            _dailyIndicator   = _dailyTab?.transform.Find("ActiveIndicator")?.GetComponent<Image>();
            _weeklyIndicator  = _weeklyTab?.transform.Find("ActiveIndicator")?.GetComponent<Image>();
            _monthlyIndicator = _monthlyTab?.transform.Find("ActiveIndicator")?.GetComponent<Image>();
            _historyIndicator = _historyTab?.transform.Find("ActiveIndicator")?.GetComponent<Image>();

            // R2-Fix F: cache tab Label TMP for gold/silver gradient
            _dailyTabLabel   = _dailyTab?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            _weeklyTabLabel  = _weeklyTab?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            _monthlyTabLabel = _monthlyTab?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            _historyTabLabel = _historyTab?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();

            // Wire back button — returns to whichever screen opened the leaderboard
            if (_backButton != null)
                _backButton.onClick.AddListener(() => ScreenManager.Instance?.ShowScreen(_returnScreen));
        }

        private void OnEnable()
        {
            // Invalidate cached rankings so the board reflects current RP
            LeaderboardManager.Instance?.InvalidateAllCache();

            _activePeriod = LeaderboardPeriod.Daily;
            ApplyBanner();
            ApplyLeagueLabel();
            RebuildList();
            StartCountdown();
        }

        private void OnDisable()
        {
            StopCountdown();
        }

        // ── Public API (called from HomeScreenController / HoleSelectionScreenController) ──

        /// <summary>Open the leaderboard and record which screen to return to.</summary>
        public void OpenFrom(ScreenId returnScreen)
        {
            _returnScreen = returnScreen;
            ScreenManager.Instance?.ShowScreen(ScreenId.Leaderboard);
        }

        // ── Tab switching ─────────────────────────────────────────────────────

        private void OnTabClicked(LeaderboardPeriod period)
        {
            if (_activePeriod == period) return;
            _activePeriod = period;
            RebuildList();
            UpdateTabIndicators();
            StartCountdown();
        }

        private void UpdateTabIndicators()
        {
            SetIndicatorActive(_dailyIndicator,   _activePeriod == LeaderboardPeriod.Daily);
            SetIndicatorActive(_weeklyIndicator,  _activePeriod == LeaderboardPeriod.Weekly);
            SetIndicatorActive(_monthlyIndicator, _activePeriod == LeaderboardPeriod.Monthly);
            SetIndicatorActive(_historyIndicator, _activePeriod == LeaderboardPeriod.Historic);

            // R2-Fix F: active tab → gold gradient, inactive tabs → silver gradient
            ApplyTabGradient(_dailyTabLabel,   _activePeriod == LeaderboardPeriod.Daily);
            ApplyTabGradient(_weeklyTabLabel,  _activePeriod == LeaderboardPeriod.Weekly);
            ApplyTabGradient(_monthlyTabLabel, _activePeriod == LeaderboardPeriod.Monthly);
            ApplyTabGradient(_historyTabLabel, _activePeriod == LeaderboardPeriod.Historic);
        }

        private static void SetIndicatorActive(Image? indicator, bool active)
        {
            if (indicator != null) indicator.gameObject.SetActive(active);
        }

        private static void ApplyTabGradient(TextMeshProUGUI? label, bool isActive)
        {
            if (label == null) return;
            if (isActive)
                TextGradients.ApplyGold(label);
            else
                TextGradients.ApplySilver(label);
        }

        // ── List building ─────────────────────────────────────────────────────

        private void RebuildList()
        {
            IReadOnlyList<LeaderboardEntry>? ranking = LeaderboardManager.Instance?.GetRanking(_activePeriod);
            if (ranking == null || ranking.Count == 0) return;

            // ── Top 3 podium ──────────────────────────────────────────────────
            BindPodiumCard(_top1Card, ranking.Count > 0 ? ranking[0] : (LeaderboardEntry?)null);
            BindPodiumCard(_top2Card, ranking.Count > 1 ? ranking[1] : (LeaderboardEntry?)null);
            BindPodiumCard(_top3Card, ranking.Count > 2 ? ranking[2] : (LeaderboardEntry?)null);

            // Podium hierarchy: #1 full size, #2/#3 smaller. The Top cards use a
            // bottom-center pivot (0.5, 0), so scaling shrinks them toward the shared
            // bottom baseline — #2/#3 stay aligned with #1's bottom, no floating.
            if (_top1Card != null) _top1Card.localScale = Vector3.one;
            if (_top2Card != null) _top2Card.localScale = Vector3.one * 0.85f;
            if (_top3Card != null) _top3Card.localScale = Vector3.one * 0.85f;

            // ── Scrolling list ────────────────────────────────────────────────
            if (_gridContent != null && _rankingsCardPrefab != null)
            {
                // Destroy ALL children of GridContent (both pool rows and any pre-baked
                // designer-default rows that exist in the prefab hierarchy).
                foreach (var row in _rowPool)
                    if (row != null) Destroy(row);
                _rowPool.Clear();

                // Destroy any remaining children not tracked by the pool (pre-baked rows)
                for (int c = _gridContent.childCount - 1; c >= 0; c--)
                    Destroy(_gridContent.GetChild(c).gameObject);

                // R5-Fix 2: start at index 3 — ranks 1/2/3 are shown in the podium;
                // skip them here so they don't appear twice in the scroll list.
                // Guard: if fewer than 4 entries exist, the loop body never executes (correct).
                for (int i = 3; i < ranking.Count; i++)
                {
                    LeaderboardEntry entry = ranking[i]; // local copy — avoids any closure capture issues
                    GameObject row = Instantiate(_rankingsCardPrefab, _gridContent);
                    _rowPool.Add(row);

                    var widget = row.GetComponent<RankingsCardWidget>();
                    if (widget == null) widget = row.AddComponent<RankingsCardWidget>();
                    widget.Bind(entry);
                }
            }

            // ── Pinned player row ─────────────────────────────────────────────
            if (_rankingsCardUser != null)
            {
                var playerEntry = LeaderboardManager.Instance?.GetPlayerEntry(_activePeriod);
                if (playerEntry.HasValue)
                {
                    var widget = _rankingsCardUser.GetComponent<RankingsCardWidget>();
                    if (widget == null) widget = _rankingsCardUser.gameObject.AddComponent<RankingsCardWidget>();
                    widget.Bind(playerEntry.Value);
                }
            }

            UpdateTabIndicators();
        }

        private static void BindPodiumCard(Transform? card, LeaderboardEntry? entry)
        {
            if (card == null) return;
            card.gameObject.SetActive(entry.HasValue);
            if (!entry.HasValue) return;

            var widget = card.GetComponent<Top3CardWidget>();
            if (widget == null) widget = card.gameObject.AddComponent<Top3CardWidget>();
            widget.Bind(entry.Value);
        }

        // ── Banner ────────────────────────────────────────────────────────────

        private void ApplyBanner()
        {
            if (_banner != null)
                _banner.SetActive(_showBanner);
        }

        // ── League label ──────────────────────────────────────────────────────

        private void ApplyLeagueLabel()
        {
            if (_leagueLabel != null)
                _leagueLabel.text = "DIAMOND LEAGUE";
        }

        // ── Reset countdown ───────────────────────────────────────────────────

        private void StartCountdown()
        {
            StopCountdown();
            if (_activePeriod == LeaderboardPeriod.Historic)
            {
                if (_resetLabel != null) _resetLabel.text = string.Empty;
                return;
            }
            _countdownCoroutine = StartCoroutine(CountdownRoutine());
        }

        private void StopCountdown()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }
        }

        private IEnumerator CountdownRoutine()
        {
            while (true)
            {
                UpdateCountdownLabel();
                yield return new WaitForSeconds(1f);
            }
        }

        private void UpdateCountdownLabel()
        {
            if (_resetLabel == null) return;
            if (LeaderboardManager.Instance == null) return;

            DateTime endUtc = LeaderboardManager.Instance.Provider.GetPeriodEndUtc(_activePeriod);
            if (endUtc == DateTime.MaxValue)
            {
                _resetLabel.text = string.Empty;
                return;
            }

            TimeSpan remaining = endUtc - NetworkTimeProvider.Instance.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _resetLabel.text = "RESETS IN: 0s";
                return;
            }

            int days    = (int)remaining.TotalDays;
            int hours   = remaining.Hours;
            int minutes = remaining.Minutes;
            int seconds = remaining.Seconds;

            string text;
            if (days > 0)
                text = $"RESETS IN: {days}d {hours}h {minutes}m {seconds}s";
            else if (hours > 0)
                text = $"RESETS IN: {hours}h {minutes}m {seconds}s";
            else
                text = $"RESETS IN: {minutes}m {seconds}s";

            _resetLabel.text = text;
        }
    }
}
