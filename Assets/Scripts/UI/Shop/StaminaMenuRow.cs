// Assets/Scripts/UI/Shop/StaminaMenuRow.cs
// Order 517 — stamina_boost_shop
// One menu item row in the Detail screen: tier badge, name, STA, RP cost, BUY button.

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Polish;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Prefab controller for a single menu item row inside
    /// <c>StaminaShopDetailScreenController</c>'s menu panel.
    ///
    /// Layout (per Figma node 13330:1139 detail):
    ///   Row  (full-width, rounded card)
    ///   ├── TierBadge          (Image + TextMeshProUGUI overlay)
    ///   │   ├── TierBadgeImage   (gradient: HIGH=gold, MEDIUM=silver, LIGHT=muted)
    ///   │   └── TierLabel        (TMP: "HIGH" / "MEDIUM" / "LIGHT")
    ///   ├── ItemNameLabel       (TMP, left-aligned, ~30px Medium)
    ///   ├── ItemDescLabel       (TMP, sub-text ~24px Regular)
    ///   ├── StaLabel            (TMP "+25 STA")
    ///   ├── RpCostLabel         (TMP "350 RP")
    ///   └── BuyButton           (Button + ButtonPressFeedback)
    ///       └── BuyButtonLabel  (TMP "BUY")
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class StaminaMenuRow : MonoBehaviour
    {
        [Header("Tier Badge")]
        [SerializeField] private Image           _tierBadgeImage;
        [SerializeField] private TextMeshProUGUI _tierLabel;

        [Header("Tier Badge Colors")]
        [SerializeField] private Color _colorHigh   = new Color(1f,   0.82f, 0.15f, 1f); // gold
        [SerializeField] private Color _colorMedium = new Color(0.75f, 0.75f, 0.78f, 1f); // silver
        [SerializeField] private Color _colorLight  = new Color(0.48f, 0.55f, 0.65f, 1f); // muted

        [Header("Content")]
        [SerializeField] private Image           _itemImage;    // Thumbnail/Mask/Photo
        [SerializeField] private TextMeshProUGUI _itemNameLabel;
        [SerializeField] private TextMeshProUGUI _itemDescLabel;
        [SerializeField] private TextMeshProUGUI _staLabel;     // "+25 STA"
        [SerializeField] private TextMeshProUGUI _rpCostLabel;  // "350 RP"

        [Header("BUY Button")]
        [SerializeField] private Button          _buyButton;

        // ── Runtime ───────────────────────────────────────────────────────────

        /// <summary>Item data last bound to this row.</summary>
        public ShopItemModel ItemData { get; private set; }

        /// <summary>Fires when the player taps BUY on this row.</summary>
        public event Action<StaminaMenuRow> OnBuyClicked;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_buyButton != null)
                _buyButton.onClick.AddListener(() => OnBuyClicked?.Invoke(this));
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Binds item data to all visual slots.
        /// Called by <see cref="StaminaShopDetailScreenController.BindMenu"/>.
        /// </summary>
        public void Bind(ShopItemModel item)
        {
            if (item == null)
            {
                Debug.LogWarning("[StaminaMenuRow] Bind called with null item.");
                return;
            }
            ItemData = item;

            // Tier badge — recolor rim + text gradient per tier (matches Figma 13330 references)
            if (_tierLabel     != null) _tierLabel.text = item.TierBadgeText;
            // Pill hugs the text with 12px padding on each side (auto per tier length).
            if (_tierLabel != null && _tierBadgeImage != null)
            {
                float w = _tierLabel.GetPreferredValues(item.TierBadgeText).x + 24f;
                var brt = _tierBadgeImage.rectTransform;
                brt.sizeDelta = new Vector2(w, brt.sizeDelta.y);
            }
            switch (item.Tier)
            {
                case ShopItemTier.High:
                    ApplyTier(_colorHigh,   new Color32(0xFF,0xF4,0xD0,0xFF), new Color32(0xFA,0xC7,0x4D,0xFF));
                    break;
                case ShopItemTier.Medium:
                    ApplyTier(_colorMedium, new Color32(0xFF,0xFF,0xFF,0xFF), new Color32(0x82,0x8F,0xA1,0xFF));
                    break;
                default:
                    ApplyTier(_colorLight,  new Color32(0xE0,0xE8,0xF5,0xFF), new Color32(0xC7,0xD6,0xEB,0xFF));
                    break;
            }

            // Content
            if (_itemNameLabel != null) _itemNameLabel.text = item.Name;
            if (_itemDescLabel != null) _itemDescLabel.text = item.Desc;
            if (_staLabel      != null) _staLabel.text      = item.StaDisplay;   // "+25 STA"
            if (_rpCostLabel   != null) _rpCostLabel.text   = item.RpCost.ToString();

            if (_itemImage != null && !string.IsNullOrEmpty(item.ItemImg))
            {
                var s = Resources.Load<Sprite>(string.Format("Sprites/Shops/{0}", item.ItemImg));
                if (s != null) _itemImage.sprite = s;
            }
        }

        /// <summary>
        /// Recolors the tier badge: rim image color + vertex-gradient text (top→bottom).
        /// </summary>
        private void ApplyTier(Color rim, Color textTop, Color textBottom)
        {
            if (_tierBadgeImage != null) _tierBadgeImage.color = rim;
            if (_tierLabel != null)
            {
                _tierLabel.enableVertexGradient = true;
                _tierLabel.colorGradient = new VertexGradient(textTop, textTop, textBottom, textBottom);
            }
        }

        /// <summary>
        /// Enables or disables the BUY button (stamina-full guard / insufficient RP).
        /// Called by <see cref="StaminaShopDetailScreenController.UpdateBuyButtonStates"/>.
        /// </summary>
        public void SetBuyInteractable(bool interactable)
        {
            if (_buyButton != null)
                _buyButton.interactable = interactable;
        }
    }
}
