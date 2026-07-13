#nullable enable
// Assets/Scripts/UI/Gacha/GachaTabController.cs
// gacha_screen Stage 1 — §3b Tab Routing
// Sits alongside GeneralShopScreenController on the GeneralShopScreen GameObject.
// Wires the three tabs (DailyTab=GACHA, WeeklyTab=STORE, MonthlyTab=GIFTS) and
// controls content panel visibility + active-tab gold/white styling.
// Stage 1 stubs: PullX1/PullX10 → ToastController "Coming soon" + log; HistoryChip → log.

using Golfin.UI.Toast;
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
    /// Defaults to GACHA on every OnEnable (first open and every re-open).
    /// </summary>
    public class GachaTabController : MonoBehaviour
    {
        // ── Hierarchy paths (relative to GeneralShopScreen root) ─────────────

        private const string DailyTabPath    = "ContentArea/BarsArea/TabBar/DailyTab";
        private const string WeeklyTabPath   = "ContentArea/BarsArea/TabBar/WeeklyTab";
        private const string MonthlyTabPath  = "ContentArea/BarsArea/TabBar/MonthlyTab";
        private const string GachaContentPath = "ContentArea/GachaTabContent";
        private const string StoreContentPath = "ContentArea/BarsArea/RankingsArea";
        private const string FilterGroupPath  = "ContentArea/BarsArea/FilterGroup";
        private const string HistoryChipPath  = "HistoryChip";
        private const string PullX1Path       = "ContentArea/GachaTabContent/BannerCard_Main/PullRow/PullX1Button";
        private const string PullX10Path      = "ContentArea/GachaTabContent/BannerCard_Main/PullRow/PullX10Button";

        // ── Colors ────────────────────────────────────────────────────────────

        private static readonly Color TabGold  = new Color32(0xEB, 0xD1, 0x70, 0xFF);
        private static readonly Color TabWhite = Color.white;
        private static readonly Color TabGray  = new Color32(0x80, 0x80, 0x80, 0xFF);

        // ── Internal state ────────────────────────────────────────────────────

        private enum TabId { Gacha, Store, Gifts }
        private TabId _activeTab = TabId.Gacha;

        private Transform? _dailyTab;
        private Transform? _weeklyTab;
        private Transform? _monthlyTab;
        private GameObject? _gachaContent;
        private GameObject? _storeContent;
        private GameObject? _filterGroup;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _dailyTab    = transform.Find(DailyTabPath);
            _weeklyTab   = transform.Find(WeeklyTabPath);
            _monthlyTab  = transform.Find(MonthlyTabPath);
            _gachaContent = transform.Find(GachaContentPath)?.gameObject;
            _storeContent = transform.Find(StoreContentPath)?.gameObject;
            _filterGroup  = transform.Find(FilterGroupPath)?.gameObject;

            if (_dailyTab   == null) Debug.LogWarning("[GachaTab] DailyTab not found at " + DailyTabPath);
            if (_weeklyTab  == null) Debug.LogWarning("[GachaTab] WeeklyTab not found at " + WeeklyTabPath);
            if (_monthlyTab == null) Debug.LogWarning("[GachaTab] MonthlyTab not found at " + MonthlyTabPath);

            WireTab(_dailyTab,   TabId.Gacha);
            WireTab(_weeklyTab,  TabId.Store);
            WireTab(_monthlyTab, TabId.Gifts);

            WirePullStub(PullX1Path,  "x1");
            WirePullStub(PullX10Path, "x10");
            WireHistoryChip();
        }

        private void OnEnable()
        {
            // Default to GACHA every time the Rewards Center opens.
            ActivateTab(TabId.Gacha);
        }

        // ── Tab wiring ────────────────────────────────────────────────────────

        private void WireTab(Transform? tab, TabId id)
        {
            if (tab == null) return;
            var btn = tab.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[GachaTab] No Button component on {tab.name}");
                return;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ActivateTab(id));
        }

        private void ActivateTab(TabId id)
        {
            _activeTab = id;
            bool gacha = id == TabId.Gacha;
            bool store = id == TabId.Store;

            if (_gachaContent != null) _gachaContent.SetActive(gacha);
            if (_storeContent != null) _storeContent.SetActive(store);
            if (_filterGroup  != null) _filterGroup .SetActive(store);

            StyleTab(_dailyTab,   id == TabId.Gacha);
            StyleTab(_weeklyTab,  id == TabId.Store);
            StyleTab(_monthlyTab, id == TabId.Gifts, gray: true);

            Debug.Log($"[GachaTab] Switched to {id}");
        }

        private void StyleTab(Transform? tab, bool active, bool gray = false)
        {
            if (tab == null) return;

            var label = tab.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.color = active ? TabGold : (gray ? TabGray : TabWhite);

            var indicator = tab.Find("ActiveIndicator");
            if (indicator != null)
                indicator.gameObject.SetActive(active);
        }

        // ── Pull stubs (Stage 1 — no ticket spend) ────────────────────────────

        private void WirePullStub(string path, string label)
        {
            var t   = transform.Find(path);
            var btn = t?.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[GachaTab] Pull button not found: {path}");
                return;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log($"[GachaTab] Pull {label} tapped — stub (Stage 1)");
                ToastController.Instance?.Show("Coming soon");
            });
        }

        // ── History chip stub ─────────────────────────────────────────────────

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
            btn.onClick.AddListener(() => Debug.Log("[GachaTab] History tapped — stub (Stage 1)"));
        }
    }
}
