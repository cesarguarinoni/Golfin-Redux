// Assets/Scripts/UI/Shop/GeneralShopScreenController.cs
// Order 610 — general_shop_ui (Phase B)
// Data-driven Rewards-Center STORE controller: generates cards from shop_catalog.csv,
// filters by the active category chip, and runs BUY through the ShopTransaction seam.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Toast;
using Golfin.Roster;

namespace GolfinRedux.UI.Shop
{
    public class GeneralShopScreenController : MonoBehaviour
    {
        private const string GridPath =
            "ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea/Viewport/GridContent";
        private const string CatRowPath = "ContentArea/BarsArea/FilterGroup/CategoryRow";

        private static readonly Color ChipGold  = new Color32(0xEB, 0xD1, 0x70, 0xFF);
        private static readonly Color ChipWhite = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        private RectTransform _grid;
        private Transform     _banner;
        private GeneralShopCard _clubTemplate;
        private GeneralShopCard _ballTemplate;

        private readonly List<GeneralShopCard> _cards = new List<GeneralShopCard>();

        /// <summary>One purchase at a time — the RP debit is a server round-trip since
        /// points_cutover_followups item 2. See HandleBuy.</summary>
        private bool _purchaseInFlight;
        private ShopCategory? _activeCategory;   // null = ALL

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _grid   = transform.Find(GridPath) as RectTransform;
            _banner = _grid != null ? _grid.Find("WinterSaleBanner") : null;
            _clubTemplate = Resources.Load<GeneralShopCard>("Prefabs/Shop/GeneralShopCard_Club");
            _ballTemplate = Resources.Load<GeneralShopCard>("Prefabs/Shop/GeneralShopCard_Ball");

            WireChip("ALLChip",        null);
            WireChip("CLUBSChip",      ShopCategory.Club);
            WireChip("BALLSChip",      ShopCategory.Ball);
            WireChip("CHARACTERSChip", ShopCategory.Character);
            WireChip("ITEMSChip",      ShopCategory.Item);
        }

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(RebuildNextFrame());
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged += OnRpChanged;
        }

        private void OnDisable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= OnRpChanged;
            ClearCards();
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null; // let layout settle
            Rebuild();
        }

        /// <summary>Immediate synchronous rebuild (test/capture harness — bypasses the one-frame settle).</summary>
        public void RebuildNow() => Rebuild();

        // ── Filter chips ────────────────────────────────────────────────────────

        private void WireChip(string chipName, ShopCategory? category)
        {
            var chip = transform.Find($"{CatRowPath}/{chipName}");
            var btn  = chip != null ? chip.GetComponent<Button>() : null;
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                _activeCategory = category;
                Rebuild();
            });
        }

        private void RestyleChips()
        {
            SetChipActive("ALLChip",        _activeCategory == null);
            SetChipActive("CLUBSChip",      _activeCategory == ShopCategory.Club);
            SetChipActive("BALLSChip",      _activeCategory == ShopCategory.Ball);
            SetChipActive("CHARACTERSChip", _activeCategory == ShopCategory.Character);
            SetChipActive("ITEMSChip",      _activeCategory == ShopCategory.Item);
        }

        private void SetChipActive(string chipName, bool active)
        {
            var lbl = transform.Find($"{CatRowPath}/{chipName}/Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl != null) lbl.color = active ? ChipGold : ChipWhite;
        }

        // ── Card build ────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            ClearCards();
            if (_grid == null) { Debug.LogError("[GeneralShop] GridContent not found."); return; }
            if (_clubTemplate == null || _ballTemplate == null)
            {
                Debug.LogError("[GeneralShop] card templates missing from Resources/Prefabs/Shop/.");
                return;
            }

            foreach (var entry in GeneralShopCatalog.GetByCategory(_activeCategory))
            {
                // Characters and items ride the CLUB template — there is no character/item card
                // design and none is coming (decision of record, Cesar 2026-08-27). Only a ball has
                // its own hierarchy, because only a ball has segmented bars.
                var template = entry.Category == ShopCategory.Ball ? _ballTemplate : _clubTemplate;
                var card = Instantiate(template, _grid);
                card.name = "Card_" + entry.EntryId;
                card.transform.SetAsLastSibling();
                card.Bind(entry);
                card.OnBuyClicked += HandleBuy;
                _cards.Add(card);
            }

            if (_banner != null) _banner.SetAsFirstSibling(); // banner stays atop the list
            RestyleChips();
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.OnBuyClicked -= HandleBuy;
                Destroy(card.gameObject);
            }
            _cards.Clear();
        }

        // ── Purchase ────────────────────────────────────────────────────────────

        private void HandleBuy(GeneralShopCard card)
        {
            if (card == null || card.Entry == null) return;

            // Server round-trip since points_cutover_followups item 2 — latch so a double-tap cannot
            // fire a second debit (see StaminaShopDetailScreenController.HandleBuyClicked).
            if (_purchaseInFlight) return;
            _purchaseInFlight = true;

            ShopTransaction.TryPurchaseCatalogEntry(card.Entry, onGranted: null, onResult: result =>
            {
                _purchaseInFlight = false;
                var toast = ToastController.Instance;

                switch (result)
                {
                    case ShopTransaction.GeneralPurchaseResult.Success:
                        toast?.Show("Purchased!");
                        card.Bind(card.Entry); // reflect OWNED / debited state
                        break;
                    case ShopTransaction.GeneralPurchaseResult.InsufficientRp:
                        toast?.Show("Not enough RP");
                        break;
                    case ShopTransaction.GeneralPurchaseResult.AlreadyOwned:
                        toast?.Show("Already owned");
                        card.Bind(card.Entry);
                        break;
                    case ShopTransaction.GeneralPurchaseResult.SpendDenied:
                        // The seam already toasted the reason (PointsSpendGate's copy, reused so it
                        // stays identical everywhere) — stay silent rather than stack a second one.
                        break;

                    case ShopTransaction.GeneralPurchaseResult.PriceChanged:
                        // The card showed a price the server no longer publishes and NOTHING was
                        // charged. Reload rather than patch the entry in place: the price is not the
                        // only thing that can have moved, and the catalog is the one place that knows
                        // how to merge the overlay and re-evaluate the windows. The player taps again
                        // against the number they can now see.
                        toast?.Show($"Price updated: {ShopTransaction.LastServerPrice} RP");
                        GeneralShopCatalog.Reload();
                        Rebuild();
                        break;

                    case ShopTransaction.GeneralPurchaseResult.NotListed:
                        // Withdrawn, out of window, or its reference went inactive — again, nothing
                        // was charged. A reload is what makes the card go away instead of sitting
                        // there failing on every tap.
                        toast?.Show("No longer available");
                        GeneralShopCatalog.Reload();
                        Rebuild();
                        break;

                    default:
                        toast?.Show("Purchase unavailable");
                        break;
                }
            });
        }

        private void OnRpChanged(int _) { /* RP pill lives on the persistent top bar */ }
    }
}
