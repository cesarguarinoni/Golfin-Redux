// Assets/Scripts/UI/Shop/StaminaShopCard.cs
// Order 517 — stamina_boost_shop
// Prefab card on the Selection screen. One card per shop in ShopCatalog.Shops.
// Node-exact rebuild (Figma 13156:1232) reusing shop atoms; bound to ShopModel.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Binds a single <see cref="ShopModel"/> onto the StaminaShopCard prefab
    /// (Figma node 13156:1232). Elements: StorefrontImage/Mask/Photo, FeaturedBadge,
    /// CategoryLabel, ShopNameLabel, TaglineLabel, HoursLabel, DailyBonusChip/Label,
    /// StaRangeLabel, RpRangeLabel, Chevron. The whole card is the tap target.
    /// STA/RP ranges are DERIVED from the shop's items (min/max), not authored.
    /// </summary>
    public class StaminaShopCard : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Image           _storefrontImage;   // StorefrontImage/Mask/Photo
        [SerializeField] private GameObject      _featuredBadge;     // FeaturedBadge (active when Featured)

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI _categoryLabel;     // "COCKTAIL BAR  •  KAMEYAMA, MIE"
        [SerializeField] private TextMeshProUGUI _shopNameLabel;
        [SerializeField] private TextMeshProUGUI _taglineLabel;
        [SerializeField] private TextMeshProUGUI _hoursLabel;        // "18:00 – 02:00"
        [SerializeField] private TextMeshProUGUI _dailyBonusLabel;   // DailyBonusChip/Label
        [SerializeField] private TextMeshProUGUI _staRangeLabel;     // "+20 / +60 STA" (derived)
        [SerializeField] private TextMeshProUGUI _rpRangeLabel;      // "200~800" (derived)

        [Header("Interaction")]
        [SerializeField] private Button _tapButton;                 // whole-card tap (root Button)

        /// <summary>Shop data last bound to this card.</summary>
        public ShopModel ShopData { get; private set; }

        /// <summary>Fires when the player taps this card.</summary>
        public event Action<StaminaShopCard> OnCardTapped;

        private void Awake()
        {
            if (_tapButton != null)
                _tapButton.onClick.AddListener(() => OnCardTapped?.Invoke(this));
        }

        /// <summary>Populates all visual elements from <paramref name="shop"/>.</summary>
        public void Bind(ShopModel shop)
        {
            if (shop == null)
            {
                Debug.LogWarning("[StaminaShopCard] Bind called with null shop.");
                return;
            }
            ShopData = shop;

            if (_categoryLabel != null)
                _categoryLabel.text = string.Format("{0}  •  {1}, {2}",
                    shop.Category.ToUpperInvariant(),
                    shop.City.ToUpperInvariant(),
                    shop.Prefecture.ToUpperInvariant());

            if (_shopNameLabel  != null) _shopNameLabel.text  = shop.Name;
            if (_taglineLabel   != null) _taglineLabel.text   = shop.Tagline;
            if (_hoursLabel     != null) _hoursLabel.text     = string.Format("{0} – {1}", shop.HoursOpen, shop.HoursClose);
            if (_dailyBonusLabel != null) _dailyBonusLabel.text = shop.DailyBonusChipText;

            // Derived STA / RP ranges from the shop's items (min/max).
            var items = ShopCatalog.GetItemsForShop(shop.Id);
            if (items != null && items.Count > 0)
            {
                int minSta = int.MaxValue, maxSta = int.MinValue, minRp = int.MaxValue, maxRp = int.MinValue;
                foreach (var it in items)
                {
                    if (it.Stamina < minSta) minSta = it.Stamina;
                    if (it.Stamina > maxSta) maxSta = it.Stamina;
                    if (it.RpCost  < minRp)  minRp  = it.RpCost;
                    if (it.RpCost  > maxRp)  maxRp  = it.RpCost;
                }
                if (_staRangeLabel != null) _staRangeLabel.text = string.Format("+{0} / +{1} STA", minSta, maxSta);
                if (_rpRangeLabel  != null) _rpRangeLabel.text  = string.Format("{0}~{1}", minRp, maxRp);
            }

            if (_featuredBadge != null)
                _featuredBadge.SetActive(shop.Featured);

            if (_storefrontImage != null && !string.IsNullOrEmpty(shop.StorefrontImg))
            {
                var sprite = Resources.Load<Sprite>(string.Format("Sprites/Shops/{0}", shop.StorefrontImg));
                if (sprite != null) _storefrontImage.sprite = sprite;
                // If null, the placeholder storefront stays showing.
            }
        }
    }
}
