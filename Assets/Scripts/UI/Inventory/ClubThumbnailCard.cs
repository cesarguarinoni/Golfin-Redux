#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Golfin.Inventory
{
    /// <summary>
    /// Individual club card in the Club Inventory carousel.
    /// Mirrors CharacterThumbnailCard but reads from ClubManager / ClubDatabaseCSV.
    ///
    /// Shows:
    ///  - Rarity background sprite (shared Resources/Rarities/ folder)
    ///  - Club portrait (thumbnail)
    ///  - Club name label
    ///  - Rarity badge letter (C/U/R/M/L/S)
    ///  - Level badge (Lv X/Y)
    ///  - Selection outline
    ///  - Status icons: Equipped, Durability Low
    /// </summary>
    public class ClubThumbnailCard : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image              portraitImage      = null!;
        [SerializeField] private TextMeshProUGUI    nameText           = null!;
        [SerializeField] private Image              rarityBadgeImage   = null!;
        [SerializeField] private TextMeshProUGUI    rarityLabelText    = null!;
        [SerializeField] private TextMeshProUGUI    levelText          = null!;
        [SerializeField] private Image              selectionHighlight = null!;
        [SerializeField] private Image              backgroundImage    = null!;
        [SerializeField] private Button             cardButton         = null!;

        [Header("Status Icons")]
        [SerializeField] private GameObject? equippedIcon;      // IconEquippedSmall  — wire in Inspector
        [SerializeField] private GameObject? durabilityLowIcon; // IconDurabilitySmall — wire in Inspector

        private string clubId    = "";
        private bool   isSelected = false;
        private Coroutine? scaleCoroutine;

        /// <summary>Fired when the card is tapped.</summary>
        public System.Action? OnClicked;

        // ── Initialization ────────────────────────────────────────────────────

        /// <summary>Binds the card to a club ID and populates all visuals.</summary>
        public void Initialize(string id)
        {
            clubId = id;

            var playerClub = ClubManager.Instance?.GetClubData(clubId);
            if (playerClub == null)
            {
                Debug.LogError($"[ClubThumbnailCard] PlayerClubData for '{clubId}' not found.");
                return;
            }

            var template = ClubDatabaseCSV.Instance?.GetClub(clubId);
            if (template == null)
            {
                Debug.LogError($"[ClubThumbnailCard] ClubDataRuntime for '{clubId}' not found in database.");
                return;
            }

            // ── Rarity ───────────────────────────────────────────────────────
            var rarity          = template.rarity;
            var rarityLabel     = Golfin.Roster.RarityHelper.GetRarityLabel(rarity);
            var rarityTextColor = Golfin.Roster.RarityHelper.GetRarityBadgeTextColor(rarity);

            // ── Portrait ─────────────────────────────────────────────────────
            if (portraitImage != null && template.portraitSprite != null)
                portraitImage.sprite = template.portraitSprite;

            // ── Name (type on top, brand below) ──────────────────────────────
            if (nameText != null)
            {
                string fullName = template.name;
                string brand    = template.brand;

                string typePart = fullName;
                if (!string.IsNullOrEmpty(brand))
                {
                    int brandIndex = fullName.IndexOf(brand, System.StringComparison.OrdinalIgnoreCase);
                    if (brandIndex >= 0)
                        typePart = fullName.Substring(0, brandIndex).Trim();
                }

                nameText.text = $"{typePart.ToUpper()}\n{brand.ToUpper()}";
            }

            // ── Rarity badge ─────────────────────────────────────────────────
            if (rarityBadgeImage != null)
                rarityBadgeImage.enabled = false;   // letter only, no bg

            if (rarityLabelText != null)
            {
                rarityLabelText.text  = rarityLabel;
                rarityLabelText.color = rarityTextColor;
            }

            // ── Level ────────────────────────────────────────────────────────
            if (levelText != null)
                levelText.text = $"Lv {playerClub.currentLevel}/{template.maxLevel}";

            // ── Background (rarity sprite — shared with character cards) ─────
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{rarity}");
                if (bgSprite != null)
                {
                    backgroundImage.sprite = bgSprite;
                    backgroundImage.color  = Color.white;
                }
                else
                {
                    Debug.LogWarning($"[ClubThumbnailCard] Rarity sprite 'Rarities/{rarity}' not found.");
                }
            }

            // ── Button ───────────────────────────────────────────────────────
            if (cardButton != null)
                cardButton.onClick.AddListener(() => OnClicked?.Invoke());

            RefreshIcons();

            Debug.Log($"[ClubThumbnailCard] Initialized: {template.name}");
        }

        // ── Status icons ──────────────────────────────────────────────────────

        /// <summary>
        /// Refreshes status icons from live PlayerClubData.
        /// Call whenever equip state or durability may have changed.
        /// </summary>
        public void RefreshIcons()
        {
            if (string.IsNullOrEmpty(clubId)) return;

            var playerClub = ClubManager.Instance?.GetClubData(clubId);
            if (playerClub == null) return;

            if (equippedIcon != null)
                equippedIcon.SetActive(playerClub.IsEquipped);

            if (durabilityLowIcon != null)
                durabilityLowIcon.SetActive(playerClub.IsDurabilityLow);
        }

        // ── Selection ─────────────────────────────────────────────────────────

        /// <summary>Highlights the card and plays a bounce animation.</summary>
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

        // ── Accessors ─────────────────────────────────────────────────────────

        public string GetClubId()  => clubId;
        public bool   IsSelected() => isSelected;
    }
}
