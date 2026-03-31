#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory
{
    /// <summary>
    /// Item detail panel — shows full item image, rarity, quantity, effect, pro tip, and info.
    /// No stat bars. Compare button always disabled. USE button fires OnUseClicked (Phase I2 wires modal).
    /// </summary>
    public class ItemDetailPanel : MonoBehaviour
    {
        [Header("Left Panel")]
        [SerializeField] private Image           itemImage  = null!;
        [SerializeField] private TextMeshProUGUI brandText  = null!;

        [Header("Right Panel")]
        [SerializeField] private TextMeshProUGUI itemNameText   = null!;
        [SerializeField] private TextMeshProUGUI rarityText     = null!;
        [SerializeField] private TextMeshProUGUI quantityText   = null!;
        [SerializeField] private TextMeshProUGUI restoresHeader = null!;
        [SerializeField] private TextMeshProUGUI effectText     = null!;
        [SerializeField] private TextMeshProUGUI proTipHeader   = null!;
        [SerializeField] private TextMeshProUGUI proTipText     = null!;

        [Header("Bottom")]
        [SerializeField] private TextMeshProUGUI infoHeader = null!;
        [SerializeField] private TextMeshProUGUI infoText   = null!;

        [Header("Buttons")]
        [SerializeField] private Button compareButton = null!;
        [SerializeField] private Button useButton     = null!;

        [Header("Carousel")]
        [SerializeField] private ItemCarouselController? carousel;

        [Header("Modals")]
        [SerializeField] private ItemUseModalController? useModal;

        private string currentItemId = "";

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            if (compareButton != null)
                compareButton.interactable = false;

            // Show first item if none selected yet
            if (string.IsNullOrEmpty(currentItemId))
            {
                var firstIds = ItemManager.Instance?.GetAllOwnedItemIds();
                if (firstIds != null && firstIds.Count > 0)
                    UpdatePanel(firstIds[0]);
            }
        }

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnItemSelected += OnItemSelected;

            if (useButton != null)
                useButton.onClick.AddListener(OnUseClicked);
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnItemSelected -= OnItemSelected;

            if (useButton != null)
                useButton.onClick.RemoveListener(OnUseClicked);
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void OnItemSelected(string itemId)
        {
            UpdatePanel(itemId);
        }

        private void OnUseClicked()
        {
            if (useModal != null && !string.IsNullOrEmpty(currentItemId))
                useModal.Open(currentItemId);
            else
                Debug.Log($"[ItemDetailPanel] USE clicked for '{currentItemId}' — wire ItemUseModalController.");
        }

        // ── Panel Update ───────────────────────────────────────────────────────

        private void UpdatePanel(string itemId)
        {
            currentItemId = itemId;

            var template   = ItemDatabaseCSV.Instance?.GetItem(itemId);
            var playerItem = ItemManager.Instance?.GetItemData(itemId);

            if (template == null || playerItem == null) return;

            // Full image
            if (itemImage != null)
            {
                if (template.fullSprite != null)
                    itemImage.sprite = template.fullSprite;
                else if (template.thumbnailSprite != null)
                    itemImage.sprite = template.thumbnailSprite;
            }

            // Brand text
            if (brandText != null)
                brandText.text = "GOLFIN";

            // Name
            if (itemNameText != null)
                itemNameText.text = template.name.ToUpper();

            // Rarity (colored)
            if (rarityText != null)
            {
                rarityText.text  = template.rarity.ToUpper();
                rarityText.color = GetRarityColor(template.rarity);
            }

            // Quantity
            if (quantityText != null)
                quantityText.text = ItemManager.Instance?.GetQuantityDisplay(itemId) ?? "x0";

            // RESTORES header
            if (restoresHeader != null)
                restoresHeader.text = LocalizationManager.Get("ITEM_RESTORES");

            // Effect: "DURABILITY 50%"
            if (effectText != null)
                effectText.text = $"{LocalizationManager.Get("ITEM_DURABILITY")} {template.restorePercent}%";

            // Pro Tip
            if (proTipHeader != null)
                proTipHeader.text = LocalizationManager.Get("ITEM_PRO_TIP");
            if (proTipText != null)
                proTipText.text = template.proTip;

            // Info
            if (infoHeader != null)
                infoHeader.text = LocalizationManager.Get("ITEM_INFO");
            if (infoText != null)
                infoText.text = template.info;

            // USE button — disabled if no quantity
            if (useButton != null)
            {
                bool hasStock = playerItem.IsUnlimited || playerItem.quantity > 0;
                useButton.interactable = hasStock;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Color GetRarityColor(string rarity) => rarity switch
        {
            "Common"    => new Color(0.75f, 0.75f, 0.80f),
            "Uncommon"  => new Color(0.29f, 0.56f, 0.89f),   // blue  — matches RarityHelper
            "Rare"      => new Color(0.314f, 0.784f, 0.471f), // #50C878 green
            "Mythic"    => new Color(1.00f, 0.757f, 0.027f),  // #FFC107 amber
            "Legendary" => new Color(1.00f, 0.65f, 0.10f),
            "Supreme"   => new Color(1.00f, 0.30f, 0.30f),
            _           => Color.white
        };
    }
}
