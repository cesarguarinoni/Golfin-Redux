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

        /// <summary>
        /// The banner's Image and the Button added by the <c>game_banners</c> task. Held here so
        /// the slot is inspectable from the controller, but the artwork and the tap belong to the
        /// <c>BannerSlotBinder</c> on the same GameObject — <see cref="ApplyBanner"/> keeps doing
        /// only what it did before, which is show or hide the whole slot.
        /// </summary>
        [SerializeField] private Image? _bannerImage;
        [SerializeField] private Button? _bannerButton;

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
            // A session can become signed-in after LeaderboardManager.Awake ran (first launch goes
            // through the auth gate). No-op when the provider is already the right one.
            LeaderboardManager.Instance?.EnsureProviderForSession();

            // Invalidate cached rankings so the board reflects current RP
            LeaderboardManager.Instance?.InvalidateAllCache();

            _activePeriod = LeaderboardPeriod.Daily;
            ApplyBanner();
            ApplyLeagueLabel();
            // Renders the disk-cached board instantly on the backend provider; the refresh below
            // replaces it in place once the server answers.
            RebuildList();
            StartCountdown();
            RequestRefresh(_activePeriod);
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
            RequestRefresh(period);
        }

        // ── Backend refresh (SPEC §4) ─────────────────────────────────────────

        /// <summary>
        /// Ask the backend provider for a fresh board for <paramref name="period"/> and rebuild if it
        /// changed. A no-op on the local-fake provider, which has nothing to fetch.
        ///
        /// <para>The in-flight guard lives in <see cref="BackendLeaderboardProvider"/> and is PER
        /// PERIOD, so bouncing tabs cannot stack requests for the same board while still letting a
        /// second tab load while the first is in the air.</para>
        ///
        /// <para>Silent on failure by design (SPEC §3): the cached board stays on screen.</para>
        /// </summary>
        private void RequestRefresh(LeaderboardPeriod period)
        {
            if (LeaderboardManager.Instance?.Provider is not BackendLeaderboardProvider backend) return;

            backend.Refresh(period, ok =>
            {
                if (!ok) return;

                // The screen can have been closed — or destroyed — while the request was in the air.
                if (this == null || !isActiveAndEnabled) return;

                LeaderboardManager.Instance?.InvalidateCache(period);

                // Another tab may have been tapped since; that tab drove its own refresh.
                if (_activePeriod == period) RebuildList();
            });
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
                _leagueLabel.text = LocalizationManager.Get("RANK_LEAGUE_DIAMOND");
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
                _resetLabel.text = string.Format(LocalizationManager.Get("RANK_RESETS_IN"),
                    "0" + LocalizationManager.Get("RANK_TIME_S"));
                return;
            }

            int days    = (int)remaining.TotalDays;
            int hours   = remaining.Hours;
            int minutes = remaining.Minutes;
            int seconds = remaining.Seconds;

            // Units are localized too: "17h 35m 10s" reads as English abbreviations to a
            // Japanese player, so d/h/m/s come from the table alongside the prefix.
            string d = LocalizationManager.Get("RANK_TIME_D");
            string h = LocalizationManager.Get("RANK_TIME_H");
            string m = LocalizationManager.Get("RANK_TIME_M");
            string sUnit = LocalizationManager.Get("RANK_TIME_S");

            string span;
            if (days > 0)
                span = $"{days}{d} {hours}{h} {minutes}{m} {seconds}{sUnit}";
            else if (hours > 0)
                span = $"{hours}{h} {minutes}{m} {seconds}{sUnit}";
            else
                span = $"{minutes}{m} {seconds}{sUnit}";

            string text = string.Format(LocalizationManager.Get("RANK_RESETS_IN"), span);

            _resetLabel.text = text;
        }
    }
}
