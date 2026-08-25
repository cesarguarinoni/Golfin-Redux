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

        [Header("Status Icons")]
        [SerializeField] private GameObject? selectedIcon;     // IconSelectedSmall — wire in Inspector
        [SerializeField] private GameObject? levelUpReadyIcon; // IconLevelUpSmall  — wire in Inspector
        [SerializeField] private GameObject? staminaIcon;      // IconStaminaSmall  — wire in Inspector

        [Header("Locked State (Starter Mode)")]
        [SerializeField] private GameObject? _lockedOverlay;   // Dark gradient + LOCKED label; MUST be wired in Inspector (prefab-authored, no runtime fallback)
        [SerializeField] private TextMeshProUGUI? _lockedLabel;  // "LOCKED" text inside the overlay

        private string characterId = "";
        private bool _isLocked = false;
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

            // CSV-first, SO fallback — only query ScriptableObject if CSV has nothing
            // (avoids "[CharacterDatabase] not found" warnings for CSV-only characters)
            var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            var soData = csvData == null ? CharacterManager.Instance.GetCharacterTemplate(characterId) : null;

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

            // Set name — use localized first name on the card (single-line)
            if (nameText != null)
            {
                if (soData != null && !string.IsNullOrEmpty(soData.characterNickname))
                    nameText.text = soData.characterNickname;
                else if (csvData != null)
                    nameText.text = csvData.GetLocalizedDisplayName(singleLine: true);
            }

            // Disable rarity badge background — only the text letter should be visible
            if (rarityBadgeImage != null)
                rarityBadgeImage.enabled = false;

            if (rarityLabelText != null)
            {
                rarityLabelText.text = rarityLabel;
                rarityLabelText.color = rarityBadgeTextColor;
            }

            // Set level (max shown in detail panel, not on card)
            if (levelText != null)
                levelText.text = $"Lv {playerChar.currentLevel}";

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

            // Status icons
            RefreshIcons();

            string displayName = csvData?.characterName ?? soData?.characterName ?? charId;
            Debug.Log($"[CharacterThumbnailCard] Initialized: {displayName}");
        }

        /// <summary>
        /// Refreshes the three status icons. Called on Initialize and whenever
        /// RP balance or selection state may have changed (via CarouselController).
        /// </summary>
        public void RefreshIcons()
        {
            if (string.IsNullOrEmpty(characterId)) return;

            var playerData = CharacterManager.Instance?.GetCharacterData(characterId);
            if (playerData == null) return;

            // Selected icon
            if (selectedIcon != null)
                selectedIcon.SetActive(playerData.isSelected);

            // Level-up-ready icon
            if (levelUpReadyIcon != null)
            {
                int  maxLevel = CharacterManager.Instance!.GetMaxLevel(characterId);
                int  cost     = CharacterManager.Instance.GetLevelUpCost(characterId);
                bool canLevel = RewardPointsManager.Instance != null
                             && RewardPointsManager.Instance.CanAfford(cost)
                             && playerData.currentLevel < maxLevel;
                levelUpReadyIcon.SetActive(canLevel);
            }

            // Stamina icon
            if (staminaIcon != null)
                staminaIcon.SetActive(playerData.IsStaminaLow());
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
        /// Populate the card from CSV template data only — no PlayerCharacterData required.
        /// Used by matchmaking and other UI that displays characters the player doesn't own.
        /// Status icons (selected / level-up-ready / stamina) are forced off in this mode.
        /// </summary>
        public void InitializeFromTemplate(string charId, int displayLevel)
        {
            characterId = charId;

            var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            if (csvData == null)
            {
                Debug.LogError($"[CharacterThumbnailCard] InitializeFromTemplate: character {charId} not in CSV.");
                return;
            }

            CharacterRarity rarity = csvData.rarity;
            var rarityLabel = RarityHelper.GetRarityLabel(rarity);
            var rarityBadgeTextColor = RarityHelper.GetRarityBadgeTextColor(rarity);

            if (portraitImage != null && csvData.portraitSprite != null)
                portraitImage.sprite = csvData.portraitSprite;

            if (nameText != null)
                nameText.text = csvData.GetLocalizedDisplayName(singleLine: true);

            if (rarityBadgeImage != null)
                rarityBadgeImage.enabled = false;

            if (rarityLabelText != null)
            {
                rarityLabelText.text = rarityLabel;
                rarityLabelText.color = rarityBadgeTextColor;
            }

            if (levelText != null)
                levelText.text = $"Lv {displayLevel}";

            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{rarity}");
                if (bgSprite != null)
                {
                    backgroundImage.sprite = bgSprite;
                    backgroundImage.color  = Color.white;
                }
            }

            // No button wiring in template mode — opponents aren't tappable.
            // Force all status icons off — no PlayerCharacterData to query.
            if (selectedIcon != null)      selectedIcon.SetActive(false);
            if (levelUpReadyIcon != null)  levelUpReadyIcon.SetActive(false);
            if (staminaIcon != null)       staminaIcon.SetActive(false);
        }

        /// <summary>
        /// Get character ID
        /// </summary>
        public string GetCharacterId() => characterId;

        /// <summary>
        /// Check if selected
        /// </summary>
        public bool IsSelected() => isSelected;

        /// <summary>
        /// Show/hide the locked overlay (used in starter-selection mode for non-candidate chars).
        /// When locked: dims the card to 50% alpha (opacity-50 per Figma) and shows the "LOCKED"
        /// overlay authored in the prefab. _lockedOverlay MUST be wired in the Inspector; there
        /// is no runtime fallback — a missing wire is an authoring error, not a handled case.
        /// </summary>
        public void SetLocked(bool isLocked)
        {
            _isLocked = isLocked;

            if (_lockedOverlay == null)
            {
                // Authoring error: the prefab must have _lockedOverlay wired.
                // DO NOT create a runtime fallback — fabricated flat-colour overlays break the
                // clone-provenance mandate and produce wrong dim values. Fix the prefab.
                Debug.LogError("[CharacterThumbnailCard] _lockedOverlay is not wired in the prefab. " +
                               "Wire the LockedOverlay child GO in the Inspector.");
                return;
            }

            _lockedOverlay.SetActive(isLocked);

            // Update the localized label text each call so language changes are reflected.
            // (F6) Card label uses UI_LOCKED, not ROSTER_LOCKED_ACQUIRE.
            if (_lockedLabel != null)
                _lockedLabel.text = LocalizationManager.Get("UI_LOCKED");

            // F1 (iter-7): Locked cards remain TAPPABLE so the player can browse the locked
            // detail panel (SPEC state 6 / node 13922:36488). Keeping the card interactive does
            // NOT let the player select the character — ApplyLockedState() in CharacterDetailPanel
            // hides the SELECT button on locked characters, so tapping only browses, never selects.
            // Do NOT set cardButton.interactable = false or blocksRaycasts = false here.

            // Dim: Figma node 13924:42412 shows the locked card div at opacity-50.
            // That maps to CanvasGroup.alpha = 0.5f (sampled: Figma opacity-50 = 50% = 0.50).
            // IMPORTANT: use Unity == null, not C# ?? — Unity-null is not C#-null.
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = isLocked ? 0.50f : 1f;
            // Keep blocksRaycasts=true so the card button can receive taps regardless of lock state.
            canvasGroup.blocksRaycasts = true;
        }
    }
}
