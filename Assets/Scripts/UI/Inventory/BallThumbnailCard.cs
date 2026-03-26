#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Golfin.Inventory
{
    /// <summary>
    /// Individual ball card in the Ball Inventory carousel.
    /// Simplified from ClubThumbnailCard — no rarity, shows quantity instead of level.
    /// </summary>
    public class BallThumbnailCard : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image           portraitImage      = null!;
        [SerializeField] private TextMeshProUGUI nameText           = null!;
        [SerializeField] private TextMeshProUGUI quantityText       = null!;   // top-right: "x99" or "∞"
        [SerializeField] private Image           selectionHighlight = null!;
        [SerializeField] private Image           backgroundImage    = null!;
        [SerializeField] private Button          cardButton         = null!;

        private string ballId    = "";
        private bool   isSelected = false;
        private Coroutine? scaleCoroutine;

        public System.Action? OnClicked;

        public void Initialize(string id)
        {
            ballId = id;

            var playerBall = BallManager.Instance?.GetBallData(ballId);
            if (playerBall == null) { Debug.LogError($"[BallThumbnailCard] PlayerBallData for '{id}' not found."); return; }

            var template = BallDatabaseCSV.Instance?.GetBall(ballId);
            if (template == null) { Debug.LogError($"[BallThumbnailCard] BallDataRuntime for '{id}' not found."); return; }

            // Portrait
            if (portraitImage != null && template.thumbnailSprite != null)
                portraitImage.sprite = template.thumbnailSprite;

            // Name
            if (nameText != null)
                nameText.text = template.name.ToUpper();

            // Quantity badge (replaces level badge position)
            if (quantityText != null)
                quantityText.text = BallManager.Instance?.GetQuantityDisplay(ballId) ?? "x0";

            // Background — neutral/default (no rarity coloring)
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>("Rarities/Common");
                if (bgSprite != null)
                {
                    backgroundImage.sprite = bgSprite;
                    backgroundImage.color  = Color.white;
                }
            }

            // Button
            if (cardButton != null)
                cardButton.onClick.AddListener(() => OnClicked?.Invoke());

            Debug.Log($"[BallThumbnailCard] Initialized: {template.name}");
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

        public string GetBallId()  => ballId;
        public bool   IsSelected() => isSelected;
    }
}
