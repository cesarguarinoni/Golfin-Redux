#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Golfin.Roster
{
    /// <summary>
    /// Individual character card in the carousel
    /// Shows:
    /// - Character portrait (thumbnail)
    /// - Character name
    /// - Rarity badge (C/U/R/M/L/S)
    /// - Level badge (Lv X)
    /// - Selection highlight
    /// 
    /// Phase 2a: Owned characters only
    /// Phase 2b: Will add locked character state (grayed out, lock icon, "LOCKED" label)
    /// </summary>
    public class CharacterThumbnailCard : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image rarityBadgeImage;
        [SerializeField] private TextMeshProUGUI rarityLabelText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image selectionHighlight;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button cardButton;
        
        private string characterId = "";
        private bool isSelected = false;
        
        // Events
        public System.Action OnClicked;
        
        /// <summary>
        /// Initialize card with character data (CSV-first, SO fallback)
        /// </summary>
        public void Initialize(string charId)
        {
            characterId = charId;

            var playerChar = CharacterManager.Instance.GetPlayerCharacter(characterId);
            if (playerChar == null)
            {
                Debug.LogError($"[CharacterThumbnailCard] PlayerCharacterData for {charId} not found!");
                return;
            }

            // CSV-first, SO fallback (matches CharacterDetailPanel pattern)
            var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            var soData = CharacterManager.Instance.GetCharacterTemplate(characterId);

            if (csvData == null && soData == null)
            {
                Debug.LogError($"[CharacterThumbnailCard] No template data for {charId} in CSV or SO!");
                return;
            }

            // Resolve rarity
            CharacterRarity rarity = csvData?.rarity ?? soData?.rarity ?? CharacterRarity.Common;
            var rarityLabel = RarityHelper.GetRarityLabel(rarity);
            var rarityBadgeTextColor = RarityHelper.GetRarityBadgeTextColor(rarity);

            // Set portrait (SO thumbnail preferred, CSV sprite fallback)
            if (portraitImage != null)
            {
                if (soData != null && soData.portraitThumbnail != null)
                    portraitImage.sprite = soData.portraitThumbnail;
                else if (csvData?.portraitSprite != null)
                    portraitImage.sprite = csvData.portraitSprite;
            }

            // Set name
            if (nameText != null)
            {
                if (soData != null && !string.IsNullOrEmpty(soData.characterNickname))
                    nameText.text = soData.characterNickname;
                else if (csvData != null)
                    nameText.text = csvData.characterName;
            }

            // Disable rarity badge background — only the text letter should be visible
            if (rarityBadgeImage != null)
                rarityBadgeImage.enabled = false;

            if (rarityLabelText != null)
            {
                rarityLabelText.text = rarityLabel;
                rarityLabelText.color = rarityBadgeTextColor;
            }

            // Set level
            if (levelText != null)
            {
                int maxLevel = CharacterManager.Instance.GetMaxLevel(characterId);
                levelText.text = $"Lv {playerChar.currentLevel}/{maxLevel}";
            }

            // Load rarity background sprite — enum name matches filename exactly
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{rarity}");
                if (bgSprite != null)
                {
                    backgroundImage.sprite = bgSprite;
                    backgroundImage.color  = Color.white; // no tint — sprite has the correct color
                }
                else
                {
                    Debug.LogWarning($"[CharacterThumbnailCard] Rarity sprite 'Rarities/{rarity}' not found in Resources.");
                }
            }

            // Wire button
            if (cardButton != null)
                cardButton.onClick.AddListener(() => OnClicked?.Invoke());

            string displayName = csvData?.characterName ?? soData?.characterName ?? charId;
            Debug.Log($"[CharacterThumbnailCard] Initialized: {displayName}");
        }
        
        /// <summary>
        /// Set selection state
        /// </summary>
        private Coroutine? scaleCoroutine;

        public void SetSelected(bool selected)
        {
            isSelected = selected;

            if (selectionHighlight != null)
            {
                selectionHighlight.enabled = selected;
            }

            // Animate scale with bounce
            float target = selected ? 1.05f : 1f;
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(AnimateScale(target));

            Debug.Log($"[CharacterThumbnailCard] {characterId} selection: {selected}");
        }

        private IEnumerator AnimateScale(float target)
        {
            float start = transform.localScale.x;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Elastic overshoot ease-out
                float ease = 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * Mathf.PI * 3f);
                float s = Mathf.LerpUnclamped(start, target, ease);
                transform.localScale = Vector3.one * s;
                yield return null;
            }

            transform.localScale = Vector3.one * target;
            scaleCoroutine = null;
        }
        
        /// <summary>
        /// Get character ID
        /// </summary>
        public string GetCharacterId() => characterId;
        
        /// <summary>
        /// Check if selected
        /// </summary>
        public bool IsSelected() => isSelected;
    }
}
