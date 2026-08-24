#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;   // RarityHelper.GetLocalizedRarityName

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

            // The Settings overlay leaves the screen underneath enabled, so this panel never
            // re-enables on a language switch and its imperatively-bound labels kept the old
            // language until the screen was re-entered.
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;

            // Re-bind on enable, not just on selection. The panel binds once in Start() and then
            // only when the carousel fires a selection, so entering the tab after the language
            // changed elsewhere left the previous language's copy on screen — and unlike the
            // repaint bugs, leaving and re-entering did NOT fix it.
            RefreshLocalizedText();
        }


        /// <summary>Get(key), falling back to the raw CSV copy when the row is absent.</summary>
        static string LocalizeBody(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            string v = LocalizationManager.Get(key);
            return string.Equals(v, key, System.StringComparison.Ordinal) ? fallback : v;
        }

        static string Up(string s) => string.IsNullOrEmpty(s) ? "" : s.ToUpperInvariant();

        /// <summary>Re-resolve the panel against the current language. UpdatePanel is a pure re-bind.</summary>
        private void RefreshLocalizedText()
        {
            if (!string.IsNullOrEmpty(currentItemId)) UpdatePanel(currentItemId);
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnItemSelected -= OnItemSelected;

            if (useButton != null)
                useButton.onClick.RemoveListener(OnUseClicked);

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
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
                // template.rarity is the raw CSV word ("Common"), so .ToUpper() rendered COMMON in
                // every language. Route it through the same localized resolver the roster and both
                // leaderboards use; it falls back to the English name when a row is missing.
                rarityText.text  = RarityHelper.GetLocalizedRarityName(ClubCsvParser.ParseRarity(template.rarity));
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
            // The pro tip is a property of the CATEGORY (every repair kit shares one), so it keys
            // off template.category rather than the item id.
            if (proTipText != null)
                proTipText.text = LocalizeBody("ITEM_PROTIP_" + Up(template.category), template.proTip);

            // Info
            if (infoHeader != null)
                infoHeader.text = LocalizationManager.Get("ITEM_INFO");
            // template.info / template.proTip are raw English CSV copy, so both bodies stayed
            // English even though the headers above them localized. A missing row falls back to
            // the CSV string, so English is byte-identical to before.
            if (infoText != null)
                infoText.text = LocalizeBody("ITEM_INFO_" + Up(template.itemId), template.info);

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
