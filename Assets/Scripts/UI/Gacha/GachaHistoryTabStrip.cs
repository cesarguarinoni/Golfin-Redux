#nullable enable
// Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs
// gacha_history node 4079:18306 §L2.2.a — the GACHA/STORE/GIFTS strip that sits above the
// sub-filter row on the history screen. The strip is a clone of the Rewards Center strip
// (GeneralShopScreen/ContentArea/BarsArea/TabBar), so it carries the same segments, sprites and
// LocalizedText keys; only the behaviour differs — from here a tab means "leave history and open
// the Rewards Center on that tab", since history has no content panels of its own.
//
// GACHA renders active (gold) because you are already inside the gacha pillar; GIFTS is grayed and
// non-tappable for the same reason it is on the Rewards Center — there is no gifts content yet.

using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Drives the cloned tab strip on GachaHistoryScreen. Sits on the screen root alongside
    /// <see cref="GachaHistoryScreenController"/>.
    /// </summary>
    public class GachaHistoryTabStrip : MonoBehaviour
    {
        private const string StripPath   = "GameScreenContent/ContentContainer/FiltersBlock/TabBar";
        private const string GachaTab    = "DailyTab";
        private const string StoreTab    = "WeeklyTab";
        private const string GiftsTab    = "MonthlyTab";

        // Same tokens as GachaTabController — the two strips must not drift apart.
        private static readonly Color ActiveTabColor   = new Color(1f, 0.816f, 0.137f);
        private static readonly Color InactiveTabColor = Color.white;
        private static readonly Color DisabledTabColor = new Color(1f, 1f, 1f, 0.35f);

        private Button?   _gacha;
        private Button?   _store;
        private Button?   _gifts;
        private TMP_Text? _gachaLabel;
        private TMP_Text? _storeLabel;
        private TMP_Text? _giftsLabel;

        private void Awake()
        {
            var strip = transform.Find(StripPath);
            if (strip == null)
            {
                Debug.LogWarning("[GachaHistoryTabStrip] TabBar not found at " + StripPath);
                return;
            }

            _gacha = strip.Find(GachaTab)?.GetComponent<Button>();
            _store = strip.Find(StoreTab)?.GetComponent<Button>();
            _gifts = strip.Find(GiftsTab)?.GetComponent<Button>();

            _gachaLabel = _gacha?.GetComponentInChildren<TMP_Text>();
            _storeLabel = _store?.GetComponentInChildren<TMP_Text>();
            _giftsLabel = _gifts?.GetComponentInChildren<TMP_Text>();

            if (_gacha != null) _gacha.onClick.AddListener(OpenGachaTab);
            if (_store != null) _store.onClick.AddListener(OpenStoreTab);
            // GIFTS: unwired AND non-interactable, matching GachaTabController's treatment — a tab
            // with a listener behind interactable=false is one refactor away from opening a blank
            // screen.
            if (_gifts != null) _gifts.interactable = false;
        }

        /// <summary>Re-applied on every open: the strip is static here, but the labels are shared
        /// clones and a language refresh repaints them.</summary>
        private void OnEnable()
        {
            SetLabel(_gachaLabel, ActiveTabColor);
            SetLabel(_storeLabel, InactiveTabColor);
            SetLabel(_giftsLabel, DisabledTabColor);
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private void OpenGachaTab() => ReturnToRewardsCenter(storeTab: false);

        private void OpenStoreTab() => ReturnToRewardsCenter(storeTab: true);

        private void ReturnToRewardsCenter(bool storeTab)
        {
            if (ScreenManager.Instance == null)
            {
                Debug.LogWarning("[GachaHistoryTabStrip] ScreenManager not found — cannot leave history.");
                return;
            }

            // Consumed once by GachaTabController.OnEnable. Both directions are explicit now
            // (nav_back_memory §3): the Rewards Center remembers its last tab, so without a
            // RequestGachaTab() the GACHA chip could land the player back on a remembered STORE.
            if (storeTab) GachaTabController.RequestStoreTab();
            else          GachaTabController.RequestGachaTab();
            ScreenManager.Instance.GoBack(ScreenId.GeneralShop);
        }

        private static void SetLabel(TMP_Text? label, Color color)
        {
            if (label != null) label.color = color;
        }
    }
}
