#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;

namespace Golfin.Inventory
{
    /// <summary>
    /// Thumbnail card for a bag in the carousel. Uses same child names as BagSlotPrefab:
    /// BagImage, BagLabel, RarityBadge, RarityBadge/Text, EquippedIcon.
    /// </summary>
    public class BagThumbnailCard : MonoBehaviour
    {
        [SerializeField] private Image?           bagImage;
        [SerializeField] private TextMeshProUGUI?  bagLabel;
        [SerializeField] private Image?           rarityBadgeImage;
        [SerializeField] private TextMeshProUGUI?  rarityBadgeText;
        [SerializeField] private Image?           backgroundImage;  // rarity-colored bg
        [SerializeField] private GameObject?       equippedIcon;

        public event System.Action? OnClicked;

        private int _bagSlot;
        private bool _selected;

        public void Initialize(int bagSlot, BagDataRuntime data, bool isEquipped)
        {
            _bagSlot = bagSlot;

            if (bagImage != null && data.thumbnailSprite != null)
                bagImage.sprite = data.thumbnailSprite;

            if (bagLabel != null)
                bagLabel.text = data.name.ToUpper();

            // Rarity badge
            if (rarityBadgeImage != null)
            {
                var raritySprite = Resources.Load<Sprite>($"Rarities/{data.rarity}");
                if (raritySprite != null) rarityBadgeImage.sprite = raritySprite;
            }
            if (rarityBadgeText != null)
            {
                rarityBadgeText.text = RarityHelper.GetRarityLabel(data.rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(data.rarity);
            }

            // Rarity background
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{data.rarity}");
                if (bgSprite != null) backgroundImage.sprite = bgSprite;
            }

            SetEquipped(isEquipped);

            // Click handler
            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClicked?.Invoke());
        }

        public void SetEquipped(bool equipped)
        {
            if (equippedIcon != null) equippedIcon.SetActive(equipped);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            // Scale animation: selected = 1.08, unselected = 1.0
            transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
        }

        public int GetBagSlot() => _bagSlot;
    }
}
