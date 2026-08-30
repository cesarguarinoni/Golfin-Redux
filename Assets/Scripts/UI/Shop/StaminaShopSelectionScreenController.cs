// Assets/Scripts/UI/Shop/StaminaShopSelectionScreenController.cs
// Order 517 — stamina_boost_shop
// Selection screen: lists all 10 MIE shops with cards, region/prefecture filter pills.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Toast;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Controller for the StaminaShopSelectionScreen prefab.
    /// Spawns StaminaShopCard instances from ShopCatalog, wires filter pills,
    /// and navigates to StaminaShopDetailScreen on card tap.
    /// </summary>
    public class StaminaShopSelectionScreenController : MonoBehaviour
    {
        [Header("Cards")]
        [SerializeField] private ScrollRect       _cardsScrollRect;
        [SerializeField] private RectTransform    _cardsContent;
        [SerializeField] private StaminaShopCard  _cardPrefab;

        [Header("Filter Pills — Region / Prefecture")]
        [Tooltip("Region pill container (one button per region token: ALL / KANSAI / etc.)")]
        [SerializeField] private Button _pillRegionAll;
        [Tooltip("Prefecture pill container (one button per prefecture: ALL / MIE / etc.)")]
        [SerializeField] private Button _pillPrefAll;

        [Header("RP Counter (Top Bar)")]
        [SerializeField] private TextMeshProUGUI _rpCounterLabel;

        [Header("Navigation")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private ScreenId _returnTarget = ScreenId.Roster;

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly List<StaminaShopCard> _cards = new List<StaminaShopCard>();

        /// <summary>Published by ShopDetailScreen on CANCEL; listened to by the nav layer.</summary>
        private string _activeRegion     = string.Empty;  // empty = ALL
        private string _activePrefecture = string.Empty;  // empty = ALL

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);
            if (_pillRegionAll != null)
                _pillRegionAll.onClick.AddListener(() => SetRegionFilter(string.Empty));
            if (_pillPrefAll != null)
                _pillPrefAll.onClick.AddListener(() => SetPrefectureFilter(string.Empty));
        }

        private void OnEnable()
        {
            StopAllCoroutines();
            RefreshRpCounter();
            StartCoroutine(RebuildNextFrame());
            // BarsArea anchor repair: some layout group pass resets anchors to (0,1)→(0,1).
            // Force correct anchors after one frame so layout has settled.
            StartCoroutine(RepairBarsAreaAnchors());

            if (Golfin.Roster.RewardPointsManager.Instance != null)
                Golfin.Roster.RewardPointsManager.Instance.OnPointsChanged += OnRpChanged;
        }

        private void OnDisable()
        {
            if (Golfin.Roster.RewardPointsManager.Instance != null)
                Golfin.Roster.RewardPointsManager.Instance.OnPointsChanged -= OnRpChanged;
            ClearCards();
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            RebuildCards();
        }

        /// <summary>
        /// BarsArea is a stretch-fill content zone but a layout group pass incorrectly
        /// resets its anchors to top-left only. Patch anchors after layout has settled.
        /// </summary>
        private IEnumerator RepairBarsAreaAnchors()
        {
            yield return null; // let layout groups run
            var barsArea = transform.Find("ContentArea/BarsArea");
            if (barsArea == null) yield break;
            var rt = barsArea as RectTransform;
            if (rt == null) rt = barsArea.GetComponent<RectTransform>();
            if (rt == null) yield break;
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        // ── RP counter ────────────────────────────────────────────────────────

        private void RefreshRpCounter()
        {
            if (_rpCounterLabel == null) return;
            var rpm = Golfin.Roster.RewardPointsManager.Instance;
            _rpCounterLabel.text = rpm != null ? rpm.GetPoints().ToString("N0") : "0";
        }

        private void OnRpChanged(int _) => RefreshRpCounter();

        // ── Card build ────────────────────────────────────────────────────────

        private void RebuildCards()
        {
            ClearCards();

            if (_cardPrefab == null)
            {
                Debug.LogError("[StaminaShopSelection] _cardPrefab not wired.");
                return;
            }
            if (_cardsContent == null)
            {
                Debug.LogError("[StaminaShopSelection] _cardsContent not wired.");
                return;
            }

            foreach (var shop in ShopCatalog.Shops)
            {
                var card = Object.Instantiate(_cardPrefab, _cardsContent);
                card.Bind(shop);
                card.OnCardTapped += HandleCardTapped;
                _cards.Add(card);
            }

            if (_cardsScrollRect != null)
                _cardsScrollRect.verticalNormalizedPosition = 1f;

            ApplyFilter();
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.OnCardTapped -= HandleCardTapped;
                Object.Destroy(card.gameObject);
            }
            _cards.Clear();
        }

        // ── Filter ────────────────────────────────────────────────────────────

        private void SetRegionFilter(string region)
        {
            _activeRegion = region;
            ApplyFilter();
            if (_cardsScrollRect != null) _cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void SetPrefectureFilter(string prefecture)
        {
            _activePrefecture = prefecture;
            ApplyFilter();
            if (_cardsScrollRect != null) _cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void ApplyFilter()
        {
            foreach (var card in _cards)
            {
                if (card == null) continue;
                bool show = true;
                if (!string.IsNullOrEmpty(_activeRegion) &&
                    !string.Equals(card.ShopData.Region, _activeRegion, System.StringComparison.OrdinalIgnoreCase))
                    show = false;
                if (!string.IsNullOrEmpty(_activePrefecture) &&
                    !string.Equals(card.ShopData.Prefecture, _activePrefecture, System.StringComparison.OrdinalIgnoreCase))
                    show = false;
                card.gameObject.SetActive(show);
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private void HandleCardTapped(StaminaShopCard card)
        {
            if (card == null) return;
            // Publish selected shop id, then open Detail screen
            StaminaShopSession.SelectedShopId = card.ShopData.Id;
            ScreenManager.Instance?.ShowScreen(ScreenId.StaminaShopDetail);
        }

        // nav_back_memory §3 — history first, serialized _returnTarget as the fallback.
        private void OnCancelClicked()
        {
            ScreenManager.Instance?.GoBack(_returnTarget);
        }
    }
}
