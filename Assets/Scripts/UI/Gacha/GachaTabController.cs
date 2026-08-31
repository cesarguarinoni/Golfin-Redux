#nullable enable
// Assets/Scripts/UI/Gacha/GachaTabController.cs
// gacha_screen Stage 1 — §3b Tab Routing
// Sits alongside GeneralShopScreenController on the GeneralShopScreen GameObject.
// Wires the three tabs (DailyTab=GACHA, WeeklyTab=STORE, MonthlyTab=GIFTS) and
// controls content panel visibility + active-tab gold/white styling.
// gacha_history Stage 1: HistoryChip → ScreenId.GachaHistory (no longer a stub).
// 2026-08-28: the chip is TAB-AWARE. GachaHistory is the gacha pull log, so it may only open
// from the GACHA tab; on STORE it toasts instead of showing the wrong screen (the Store
// History screen, Figma 13509:2978, is still deferred — general_shop_ui SPEC § Deferred).

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
        // Order 610 built the STORE card grid under RankingsArea (a tournament-clone name kept from
        // the original clone-and-modify). gacha_screen Stage 1 guessed "ContentArea/StoreTabContent",
        // which never existed — so _storeContent resolved to null and the STORE tab rendered blank.
        // Verified against the live GeneralShopScreen hierarchy (2026-08-17); this is also the parent
        // of GeneralShopScreenController.GridPath.
        private const string StoreContentPath  = "ContentArea/BarsArea/RankingsArea";
        private const string FilterGroupPath   = "ContentArea/BarsArea/FilterGroup";

        // ── HistoryChip path (direct child of GeneralShopScreen, not inside GachaTabContent) ──
        private const string HistoryChipPath = "HistoryChip";

        // ── Tab colour tokens ─────────────────────────────────────────────────
        private static readonly Color ActiveTabColor   = new Color(1f, 0.816f, 0.137f); // gold
        private static readonly Color InactiveTabColor = Color.white;
        private static readonly Color DisabledTabColor = new Color(1f, 1f, 1f, 0.35f);  // grayed-out

        /// <summary>
        /// GIFTS has no content panel built yet (gacha_screen SPEC §8 — out of scope), so the tab is
        /// shown grayed and non-tappable rather than blanking the screen. Flip to true in the same
        /// change that lands the gifts content; ShowGiftsTab() below is already the correct handler.
        /// </summary>
        /// (static readonly rather than const so the disabled branches don't trip CS0162
        /// unreachable-code warnings while the flag is false.)
        private static readonly bool GiftsTabEnabled = false;

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

        /// <summary>Which tab's content is currently on screen.</summary>
        private enum RewardsTab { Gacha, Store, Gifts }

        /// <summary>
        /// The History chip is SHARED across the three tabs — it is a root-level child of
        /// GeneralShopScreen, not part of either content panel — so its destination has to key off
        /// the active tab. See <see cref="OnHistoryChipTapped"/>.
        /// </summary>
        private RewardsTab _activeTab = RewardsTab.Gacha;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            WireTabs();
            WireHistoryChip();
        }

        /// <summary>
        /// ShowScreen re-activates this GameObject on every visit, so the tab choice is applied here
        /// rather than in Start() — Start() runs once and would re-apply the default AFTER OnEnable had
        /// already consumed a RequestStoreTab(), sending the top-bar "+" back to GACHA.
        /// Awake() has always run by the time OnEnable() fires, so the tabs are wired.
        /// </summary>
        private void OnEnable()
        {
            ApplyPendingOrDefaultTab();
        }

        /// <summary>
        /// nav_back_memory §5 / F3 — the Rewards Center REMEMBERS its tab for the session instead
        /// of snapping back to GACHA on every entry. A one-shot request (the top-bar "+", the
        /// history strip) still wins for that single open. First entry of the session is GACHA,
        /// the unchanged default (Cesar 2026-07-08; the bottom-nav slot IS the gacha icon).
        /// </summary>
        private void ApplyPendingOrDefaultTab()
        {
            if      (_pendingStoreTab) ShowStoreTab();
            else if (_pendingGachaTab) ShowGachaTab();
            // GIFTS has no content panel, so a remembered Gifts would blank the screen. It is
            // unreachable today (the tab is inert) — guarded so re-enabling it can't strand us.
            else if (_activeTab == RewardsTab.Store) ShowStoreTab();
            else if (_activeTab == RewardsTab.Gifts && GiftsTabEnabled) ShowGiftsTab();
            else                                     ShowGachaTab();

            _pendingStoreTab = false;
            _pendingGachaTab = false;
        }

        /// <summary>
        /// Ask the Rewards Center to open on the STORE tab regardless of the remembered tab.
        /// Consumed once. Call immediately before navigating to <c>ScreenId.GeneralShop</c>.
        /// </summary>
        public static void RequestStoreTab() { _pendingStoreTab = true; _pendingGachaTab = false; }

        /// <summary>
        /// Symmetric counterpart of <see cref="RequestStoreTab"/> — forces GACHA over the
        /// remembered tab. Used by the Gacha History strip's GACHA chip.
        /// </summary>
        public static void RequestGachaTab() { _pendingGachaTab = true; _pendingStoreTab = false; }

        private static bool _pendingStoreTab;
        private static bool _pendingGachaTab;

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

            if (_monthlyTab != null)
            {
                if (GiftsTabEnabled) _monthlyTab.onClick.AddListener(ShowGiftsTab);
                // Left unwired AND non-interactable while disabled: interactable=false alone still
                // leaves a listener that a future refactor could re-trigger, and the tap would blank
                // the screen. ButtonPressFeedback (if present) keys off interactable, so the tab also
                // stops animating on press.
                else _monthlyTab.interactable = false;
            }
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

        /// <summary>
        /// GACHA tab → the gacha pull log. STORE / GIFTS → nothing to open yet, so the chip stays
        /// inert (the icon is present in the Figma store node, so it is toasted rather than hidden)
        /// instead of sending the player to the gacha log, which is not their purchase history.
        /// Point the STORE branch at ScreenId.StoreHistory once that screen ships.
        /// </summary>
        private void OnHistoryChipTapped()
        {
            if (_activeTab != RewardsTab.Gacha)
            {
                Debug.Log($"[GachaTab] HistoryChip tapped on the {_activeTab} tab — no history screen yet.");
                ToastController.Instance?.Show(LocalizationManager.Get("SHOP_HISTORY_COMING_SOON"), 2f);
                return;
            }

            if (ScreenManager.Instance != null)
                ScreenManager.Instance.ShowScreen(ScreenId.GachaHistory);
            else
                Debug.LogWarning("[GachaTab] ScreenManager not found — cannot open GachaHistory.");
        }

        // ── Pull buttons ──────────────────────────────────────────────────────
        //
        // DELETED by gacha_client_real_pull §4.2, with WirePullButtons.
        //
        // OnPullX1/OnPullX10 opened the Prizes screen with a freshly-rolled MOCK result and no
        // banner, no ticket spend and no server. They have been dead since gacha_screen Stage 2 —
        // the paths they wire (ContentArea/GachaTabContent/PullSection/PullX1Button) do not exist
        // in the hierarchy, so GetButtonAt returned null and nothing was ever listening. The real
        // PULL buttons live on GachaBannerCard and route through GachaPullFlow, which is the only
        // way a pull happens now.

        // ── Tab show/hide ─────────────────────────────────────────────────────

        private void ShowGachaTab()
        {
            _activeTab = RewardsTab.Gacha;
            SetTabActive(_dailyTabLabel,   true);
            SetTabActive(_weeklyTabLabel,  false);
            SetGiftsTabLabel(false);
            SetActive(_gachaContent,  true);
            SetActive(_storeContent,  false);
            SetActive(_filterGroup,   false);
        }

        private void ShowStoreTab()
        {
            _activeTab = RewardsTab.Store;
            SetTabActive(_dailyTabLabel,   false);
            SetTabActive(_weeklyTabLabel,  true);
            SetGiftsTabLabel(false);
            SetActive(_gachaContent,  false);
            SetActive(_storeContent,  true);
            SetActive(_filterGroup,   true);
        }

        /// <summary>
        /// GIFTS styling: grayed while <see cref="GiftsTabEnabled"/> is false, otherwise the normal
        /// active/inactive treatment. Kept separate so re-enabling the tab is a one-const change.
        /// </summary>
        private void SetGiftsTabLabel(bool active)
        {
            if (_monthlyTabLabel == null) return;
            if (!GiftsTabEnabled) { _monthlyTabLabel.color = DisabledTabColor; return; }
            SetTabActive(_monthlyTabLabel, active);
        }

        /// <summary>
        /// Unreferenced while <see cref="GiftsTabEnabled"/> is false — this is the handler the tab
        /// gets wired back to once the gifts content panel exists (add the SetActive for it here).
        /// </summary>
        private void ShowGiftsTab()
        {
            _activeTab = RewardsTab.Gifts;
            SetTabActive(_dailyTabLabel,   false);
            SetTabActive(_weeklyTabLabel,  false);
            SetGiftsTabLabel(true);
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
