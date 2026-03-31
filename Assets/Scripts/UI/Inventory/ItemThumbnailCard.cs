#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Golfin.Inventory
{
    /// <summary>
    /// Individual item card in the Item Inventory carousel.
    /// Extends BallThumbnailCard pattern: adds rarity background + rarity badge letter.
    /// Shows thumbnail sprite, item name+rarity, quantity badge ("x99").
    /// </summary>
    public class ItemThumbnailCard : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image           portraitImage      = null!;
        [SerializeField] private TextMeshProUGUI nameText           = null!;
        [SerializeField] private TextMeshProUGUI quantityText       = null!;   // top-right: "x99"
        [SerializeField] private TextMeshProUGUI rarityBadgeText    = null!;   // top-left: "C", "R", "M"
        [SerializeField] private Image           selectionHighlight = null!;
        [SerializeField] private Image           backgroundImage    = null!;
        [SerializeField] private Button          cardButton         = null!;

        private string itemId     = "";
        private bool   isSelected = false;
        private Coroutine? scaleCoroutine;

        public System.Action? OnClicked;

        public void Initialize(string id)
        {
            itemId = id;

            var playerItem = ItemManager.Instance?.GetItemData(itemId);
            if (playerItem == null) { Debug.LogError($"[ItemThumbnailCard] PlayerItemData for '{id}' not found."); return; }

            var template = ItemDatabaseCSV.Instance?.GetItem(itemId);
            if (template == null) { Debug.LogError($"[ItemThumbnailCard] ItemDataRuntime for '{id}' not found."); return; }

            // Portrait
            if (portraitImage != null && template.thumbnailSprite != null)
                portraitImage.sprite = template.thumbnailSprite;

            // Name + rarity on second line
            if (nameText != null)
                nameText.text = $"{template.name.ToUpper()}\n{template.rarity.ToUpper()}";

            // Quantity badge
            if (quantityText != null)
                quantityText.text = ItemManager.Instance?.GetQuantityDisplay(itemId) ?? "x0";

            // Rarity background sprite
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (bgSprite != null)
                {
                    backgroundImage.sprite = bgSprite;
                    backgroundImage.color  = Color.white;
                }
            }

            // Rarity badge letter (top-left)
            if (rarityBadgeText != null)
            {
                rarityBadgeText.text = template.rarity.Length > 0
                    ? template.rarity[0].ToString().ToUpper()
                    : "";
            }

            // Button
            if (cardButton != null)
                cardButton.onClick.AddListener(() => OnClicked?.Invoke());

            Debug.Log($"[ItemThumbnailCard] Initialized: {template.name} ({template.rarity})");
        }

        // ── Selection ─────────────────────────────────────────────────────────

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selectionHighlight != null)
                selectionHighlight.enabled = selected;

            float target = selected ? 1.05f : 1f;
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(AnimateScale(target));
        }

        private IEnumerator AnimateScale(float target)
        {
            float start    = transform.localScale.x;
            float duration = 0.3f;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t    = elapsed / duration;
                float ease = 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * Mathf.PI * 3f);
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(start, target, ease);
                yield return null;
            }
            transform.localScale = Vector3.one * target;
            scaleCoroutine = null;
        }

        public string GetItemId()  => itemId;
        public bool   IsSelected() => isSelected;
    }
}
