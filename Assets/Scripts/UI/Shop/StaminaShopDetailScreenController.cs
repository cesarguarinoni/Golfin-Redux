// Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs
// Order 517 — stamina_boost_shop
// Detail screen for one shop: hero, info card, menu items + BUY, CANCEL.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.EconomyRuntime;
using Golfin.Roster;
using Golfin.UI.Polish;
using Golfin.UI.Toast;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Controller for StaminaShopDetailScreen prefab.
    /// Reads <see cref="StaminaShopSession.SelectedShopId"/> on OnEnable, binds
    /// the hero + info card + 3 menu item rows.  BUY calls
    /// <see cref="ShopTransaction.TryPurchase"/>.
    /// </summary>
    public class StaminaShopDetailScreenController : MonoBehaviour
    {
        // ── Hero ─────────────────────────────────────────────────────────────
        [Header("Hero")]
        [SerializeField] private Image            _heroImage;
        [SerializeField] private TextMeshProUGUI  _titleLabel;          // "BAR&LOUNGE 影牢"
        [SerializeField] private TextMeshProUGUI  _categoryLabel;       // gradient eyebrow
        [SerializeField] private TextMeshProUGUI  _shopNameHeroLabel;   // Noto Sans JP Bold 58px
        [SerializeField] private TextMeshProUGUI  _addressLabel;
        [SerializeField] private GameObject       _openNowBadge;
        [SerializeField] private GameObject       _featuredBadge;

        // ── Info Card (3 columns) ─────────────────────────────────────────────
        [Header("Info Card")]
        [SerializeField] private TextMeshProUGUI _locationCityLabel;    // e.g. "Kameyama"
        [SerializeField] private TextMeshProUGUI _locationWalkLabel;    // "📍 12 min walk"
        [SerializeField] private TextMeshProUGUI _hoursValueLabel;      // "18:00 – 02:00"
        [SerializeField] private TextMeshProUGUI _hoursNoteLabel;       // "Open daily"
        [SerializeField] private TextMeshProUGUI _signatureNameLabel;   // "影牢 Cocktail"
        [SerializeField] private TextMeshProUGUI _signatureNoteLabel;   // "House special"

        // ── Menu Panel ────────────────────────────────────────────────────────
        [Header("Menu Panel")]
        [SerializeField] private TextMeshProUGUI  _dailyBonusChipLabel; // "+15% RECOVERY"
        [SerializeField] private RectTransform    _menuItemsContainer;
        [SerializeField] private StaminaMenuRow   _menuRowPrefab;

        // ── RP Counter (Top Bar) ──────────────────────────────────────────────
        [Header("RP Counter")]
        [SerializeField] private TextMeshProUGUI _rpCounterLabel;

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private Button   _cancelButton;
        [SerializeField] private ScreenId _backTarget = ScreenId.StaminaShopSelection;

        // ── Runtime ───────────────────────────────────────────────────────────
        private ShopModel _currentShop;
        private readonly List<StaminaMenuRow> _menuRows = new List<StaminaMenuRow>();

        /// <summary>One purchase at a time — the RP debit is a server round-trip since
        /// points_cutover_followups item 2. See HandleBuyClicked.</summary>
        private bool _purchaseInFlight;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private void OnEnable()
        {
            RefreshRpCounter();
            StopAllCoroutines();
            StartCoroutine(BindNextFrame());

            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged += OnRpChanged;

            // The Settings overlay leaves the screen underneath enabled, so this panel never
            // re-enables on a language switch and its imperatively-bound labels kept the old
            // language until the screen was re-entered.
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        /// <summary>The info card carries this screen's only localized string (the signature note).</summary>
        private void RefreshLocalizedText()
        {
            if (_currentShop != null) BindInfoCard(_currentShop);
        }

        private void OnDisable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= OnRpChanged;

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
            ClearRows();
        }

        private IEnumerator BindNextFrame()
        {
            yield return null;
            BindShop(StaminaShopSession.SelectedShopId);
        }

        // ── RP counter ────────────────────────────────────────────────────────

        private void RefreshRpCounter()
        {
            if (_rpCounterLabel == null) return;
            var rpm = RewardPointsManager.Instance;
            _rpCounterLabel.text = rpm != null ? rpm.GetPoints().ToString("N0") : "0";
        }

        private void OnRpChanged(int _)
        {
            RefreshRpCounter();
            // Refresh BUY button states (RP may have dropped below cost)
            UpdateBuyButtonStates();
        }

        // ── Bind ──────────────────────────────────────────────────────────────

        private void BindShop(string shopId)
        {
            _currentShop = null;
            foreach (var shop in ShopCatalog.Shops)
            {
                if (string.Equals(shop.Id, shopId, System.StringComparison.Ordinal))
                {
                    _currentShop = shop;
                    break;
                }
            }

            if (_currentShop == null)
            {
                Debug.LogError(string.Format("[StaminaShopDetail] Shop '{0}' not found in catalog.", shopId));
                return;
            }

            BindHero(_currentShop);
            BindInfoCard(_currentShop);
            BindMenu(_currentShop);

            // Drive PersistentUI TopBar center text to shop name (per Figma 13330:1139)
            var pui = Golfin.UI.PersistentUIManager.Instance
                      ?? UnityEngine.Object.FindObjectOfType<Golfin.UI.PersistentUIManager>();
            if (pui != null)
                pui.SetUsername(_currentShop.Name.ToUpperInvariant());
        }

        private void BindHero(ShopModel shop)
        {
            if (_titleLabel        != null) _titleLabel.text        = shop.Name.ToUpperInvariant();
            if (_categoryLabel     != null) _categoryLabel.text     = shop.Category;
            if (_shopNameHeroLabel != null) _shopNameHeroLabel.text = shop.Name;
            if (_addressLabel      != null)
                _addressLabel.text = string.Format("📍  {0}", shop.Address);

            // Hero image: load from Resources/Sprites/Shops/ by hero_img key
            if (_heroImage != null)
            {
                var sprite = Resources.Load<Sprite>(string.Format("Sprites/Shops/{0}", shop.HeroImg));
                if (sprite != null) _heroImage.sprite = sprite;
            }

            if (_openNowBadge  != null) _openNowBadge.SetActive(IsOpenNow(shop));
            if (_featuredBadge != null) _featuredBadge.SetActive(shop.Featured);
        }

        private void BindInfoCard(ShopModel shop)
        {
            if (_locationCityLabel != null) _locationCityLabel.text = shop.City;
            if (_locationWalkLabel != null)
                _locationWalkLabel.text = string.Format("📍 {0} min walk", shop.WalkMinutes);
            if (_hoursValueLabel   != null)
                _hoursValueLabel.text = string.Format("{0} – {1}", shop.HoursOpen, shop.HoursClose);
            if (_hoursNoteLabel    != null) _hoursNoteLabel.text    = shop.HoursNote;
            if (_signatureNameLabel!= null) _signatureNameLabel.text= shop.SignatureName;
            if (_signatureNoteLabel!= null) _signatureNoteLabel.text= LocalizationManager.Get("STAMINA_HOUSE_SPECIAL");
        }

        private void BindMenu(ShopModel shop)
        {
            ClearRows();

            if (_dailyBonusChipLabel != null)
                _dailyBonusChipLabel.text = string.Format("DAILY BONUS  {0}", shop.DailyBonusChipText);

            if (_menuRowPrefab == null)
            {
                Debug.LogError("[StaminaShopDetail] _menuRowPrefab not wired.");
                return;
            }
            if (_menuItemsContainer == null)
            {
                Debug.LogError("[StaminaShopDetail] _menuItemsContainer not wired.");
                return;
            }

            var items = ShopCatalog.GetItemsForShop(shop.Id);
            foreach (var item in items)
            {
                var row = Object.Instantiate(_menuRowPrefab, _menuItemsContainer);
                row.Bind(item);
                row.OnBuyClicked += HandleBuyClicked;
                _menuRows.Add(row);
            }

            UpdateBuyButtonStates();
        }

        private void ClearRows()
        {
            foreach (var row in _menuRows)
            {
                if (row == null) continue;
                row.OnBuyClicked -= HandleBuyClicked;
                Object.Destroy(row.gameObject);
            }
            _menuRows.Clear();
        }

        // ── Stamina-full guard + BUY button enable/disable ────────────────────

        private void UpdateBuyButtonStates()
        {
            var pcd = GetSelectedPcd();
            bool isFull = pcd != null && pcd.currentStaminaEnergy >= pcd.maxStaminaEnergy;

            foreach (var row in _menuRows)
            {
                if (row == null) continue;
                row.SetBuyInteractable(!isFull);
            }
        }

        // ── Purchase handling ─────────────────────────────────────────────────

        private void HandleBuyClicked(StaminaMenuRow row)
        {
            if (row == null) return;

            // The spend is a server round-trip now (points_cutover_followups item 2), so BUY stays
            // live for as long as it takes to answer. Latch the screen for the duration: without it
            // a double-tap fires a second debit, and PointsSpendGate's process-wide in-flight guard
            // would silently swallow the second purchase after charging for the first tap only.
            if (_purchaseInFlight) return;
            _purchaseInFlight = true;

            // …and SHOW it on the row that was tapped (transaction_feedback §3.1). The latch is the
            // guard; this is the affordance — without it BUY looked untouched for the whole trip.
            //
            // NOT begun when the process-wide gate is already busy: TryPurchase routes through
            // PointsSpendGate, whose own latch would DROP this spend without calling either callback,
            // and a pending scope nothing disposes is a BUY button that never comes back.
            var pending = PointsSpendGate.IsSpendInFlight
                ? null
                : PendingSpend.Begin(row.BuyButton, row.BuyLabel);

            var item = row.ItemData;
            var pcd  = GetSelectedPcd();

            ShopTransaction.TryPurchase(pcd, item.RpCost, item.Stamina,
                onGranted: () =>
                {
                    // Refresh buy buttons after successful purchase
                    UpdateBuyButtonStates();
                },
                onResult: result =>
                {
                    // Restore first, then re-assert the truth. UpdateBuyButtonStates ran INSIDE the
                    // pending scope (from onGranted above, and from OnRpChanged when the debit fired),
                    // so the restore would otherwise hand back "enabled" over a stamina-full disable.
                    pending?.Dispose();
                    _purchaseInFlight = false;
                    UpdateBuyButtonStates();

                    ShowResultToast(result, item);
                });
        }

        private void ShowResultToast(ShopTransaction.PurchaseResult result, ShopItemModel item)
        {
            // PointsSpendGate already toasted "Connection required" / "Not enough Reward Points" —
            // a second toast here would stack two messages for one refusal.
            if (result == ShopTransaction.PurchaseResult.SpendDenied) return;

            var tc = ToastController.Instance;
            if (tc == null) return;

            switch (result)
            {
                case ShopTransaction.PurchaseResult.Success:
                    tc.Show(string.Format("Stamina boosted +{0} STA!", item.Stamina));
                    break;
                case ShopTransaction.PurchaseResult.InsufficientRp:
                    tc.Show(string.Format("Not enough RP. Need {0} RP.", item.RpCost));
                    break;
                case ShopTransaction.PurchaseResult.StaminaFull:
                    tc.Show("Stamina is already full!");
                    break;
                case ShopTransaction.PurchaseResult.NullCharacter:
                    tc.Show("No character selected.");
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static PlayerCharacterData GetSelectedPcd()
        {
            var cm     = CharacterManager.Instance;
            var charId = cm?.GetSelectedCharacterId();
            if (string.IsNullOrEmpty(charId)) return null;
            return cm.GetCharacterData(charId);
        }

        /// <summary>
        /// Basic open-now check: compare current local hour against shop's HoursOpen / HoursClose.
        /// Handles overnight spans (e.g. 18:00 – 02:00).
        /// </summary>
        private static bool IsOpenNow(ShopModel shop)
        {
            if (!System.TimeSpan.TryParse(shop.HoursOpen,  out var open))  return false;
            if (!System.TimeSpan.TryParse(shop.HoursClose, out var close)) return false;
            var now = System.DateTime.Now.TimeOfDay;
            if (open <= close)
                return now >= open && now < close;
            else
                return now >= open || now < close; // overnight
        }

        private void OnCancelClicked()
        {
            ScreenManager.Instance?.ShowScreen(_backTarget);
        }
    }
}
