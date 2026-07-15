#nullable enable
// Assets/Scripts/UI/Gacha/GachaTabController.cs
// gacha_screen Stage 1 — §3b Tab Routing
// Sits alongside GeneralShopScreenController on the GeneralShopScreen GameObject.
// Wires the three tabs (DailyTab=GACHA, WeeklyTab=STORE, MonthlyTab=GIFTS) and
// controls content panel visibility + active-tab gold/white styling.
// Stage 1 stubs: PullX1/PullX10 → ToastController "Coming soon" + log.
// gacha_history Stage 1: HistoryChip → ScreenId.GachaHistory (no longer a stub).

using Golfin.UI.Toast;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Drives the Rewards Center (GeneralShopScreen) tab bar.
    /// GACHA tab = show GachaTabContent, hide STORE content + FilterGroup chip row.
    /// STORE tab = inverse.
    /// GIFTS tab = grayed / inert (no content panel to show yet).
    /// </summary>
    public class GachaTabController : MonoBehaviour
    {
        // ── Tab button paths (relative to this GO's transform) ────────────────
        // Verified via script-execute against live GeneralShopScreen scene hierarchy (2026-07-15)
        private const string DailyTabPath   = "ContentArea/BarsArea/TabBar/DailyTab";
        private const string WeeklyTabPath  = "ContentArea/BarsArea/TabBar/WeeklyTab";
        private const string MonthlyTabPath = "ContentArea/BarsArea/TabBar/MonthlyTab";

        // ── Content panel paths ───────────────────────────────────────────────
        private const string GachaContentPath  = "ContentArea/GachaTabContent";
        private const string StoreContentPath  = "ContentArea/StoreTabContent";    // not yet in scene
        private const string FilterGroupPath   = "ContentArea/BarsArea/FilterGroup";

        // ── HistoryChip path (direct child of GeneralShopScreen, not inside GachaTabContent) ──
        private const string HistoryChipPath = "HistoryChip";

        // ── Pull button paths — Stage 2 only; PullSection does not exist yet ───
        private const string PullX1Path  = "ContentArea/GachaTabContent/PullSection/PullX1Button";
        private const string PullX10Path = "ContentArea/GachaTabContent/PullSection/PullX10Button";

        // ── Tab colour tokens ─────────────────────────────────────────────────
        private static readonly Color ActiveTabColor   = new Color(1f, 0.816f, 0.137f); // gold
        private static readonly Color InactiveTabColor = Color.white;

        // ── Runtime state ─────────────────────────────────────────────────────
        private Button?   _dailyTab;
        private Button?   _weeklyTab;
        private Button?   _monthlyTab;
        private GameObject? _gachaContent;
        private GameObject? _storeContent;
        private GameObject? _filterGroup;
        private TMP_Text? _dailyTabLabel;
        private TMP_Text? _weeklyTabLabel;
        private TMP_Text? _monthlyTabLabel;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            WireTabs();
            WireHistoryChip();
            WirePullButtons();
        }

        private void Start()
        {
            // Default to GACHA tab active.
            ShowGachaTab();
        }

        // ── Tab wiring ────────────────────────────────────────────────────────

        private void WireTabs()
        {
            _dailyTab    = GetButtonAt(DailyTabPath);
            _weeklyTab   = GetButtonAt(WeeklyTabPath);
            _monthlyTab  = GetButtonAt(MonthlyTabPath);

            _gachaContent  = transform.Find(GachaContentPath)?.gameObject;
            _storeContent  = transform.Find(StoreContentPath)?.gameObject;
            _filterGroup   = transform.Find(FilterGroupPath)?.gameObject;

            _dailyTabLabel   = GetLabelOf(_dailyTab);
            _weeklyTabLabel  = GetLabelOf(_weeklyTab);
            _monthlyTabLabel = GetLabelOf(_monthlyTab);

            if (_dailyTab  != null) _dailyTab.onClick.AddListener(ShowGachaTab);
            if (_weeklyTab != null) _weeklyTab.onClick.AddListener(ShowStoreTab);
            if (_monthlyTab != null) _monthlyTab.onClick.AddListener(ShowGiftsTab);
        }

        // ── HistoryChip → GachaHistory screen ────────────────────────────────

        private void WireHistoryChip()
        {
            var t   = transform.Find(HistoryChipPath);
            var btn = t?.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning("[GachaTab] HistoryChip not found at " + HistoryChipPath);
                return;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnHistoryChipTapped);
        }

        private void OnHistoryChipTapped()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.ShowScreen(ScreenId.GachaHistory);
            else
                Debug.LogWarning("[GachaTab] ScreenManager not found — cannot open GachaHistory.");
        }

        // ── Pull buttons (stub) ───────────────────────────────────────────────

        private void WirePullButtons()
        {
            var pull1  = GetButtonAt(PullX1Path);
            var pull10 = GetButtonAt(PullX10Path);

            if (pull1  != null) pull1.onClick.AddListener(OnPullX1);
            if (pull10 != null) pull10.onClick.AddListener(OnPullX10);
        }

        private void OnPullX1()
        {
            Debug.Log("[GachaTab] PullX1 tapped — stub (Stage 2)");
            ToastController.Instance?.Show("Coming soon!", 2f);
        }

        private void OnPullX10()
        {
            Debug.Log("[GachaTab] PullX10 tapped — stub (Stage 2)");
            ToastController.Instance?.Show("Coming soon!", 2f);
        }

        // ── Tab show/hide ─────────────────────────────────────────────────────

        private void ShowGachaTab()
        {
            SetTabActive(_dailyTabLabel,   true);
            SetTabActive(_weeklyTabLabel,  false);
            SetTabActive(_monthlyTabLabel, false);
            SetActive(_gachaContent,  true);
            SetActive(_storeContent,  false);
            SetActive(_filterGroup,   false);
        }

        private void ShowStoreTab()
        {
            SetTabActive(_dailyTabLabel,   false);
            SetTabActive(_weeklyTabLabel,  true);
            SetTabActive(_monthlyTabLabel, false);
            SetActive(_gachaContent,  false);
            SetActive(_storeContent,  true);
            SetActive(_filterGroup,   true);
        }

        private void ShowGiftsTab()
        {
            SetTabActive(_dailyTabLabel,   false);
            SetTabActive(_weeklyTabLabel,  false);
            SetTabActive(_monthlyTabLabel, true);
            SetActive(_gachaContent,  false);
            SetActive(_storeContent,  false);
            SetActive(_filterGroup,   false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Button? GetButtonAt(string path)
        {
            var t = transform.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[GachaTab] Path not found: {path}");
                return null;
            }
            return t.GetComponent<Button>();
        }

        private static TMP_Text? GetLabelOf(Button? btn)
            => btn?.GetComponentInChildren<TMP_Text>();

        private static void SetTabActive(TMP_Text? label, bool active)
        {
            if (label == null) return;
            label.color = active ? ActiveTabColor : InactiveTabColor;
        }

        private static void SetActive(GameObject? go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
