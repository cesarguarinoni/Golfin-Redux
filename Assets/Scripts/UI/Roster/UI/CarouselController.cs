#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

using Golfin.UI.Common;

namespace Golfin.Roster
{
    /// <summary>
    /// Manages the character carousel (horizontal scroll)
    /// Handles:
    /// - Populating thumbnail cards from owned characters
    /// - Left/right arrow navigation
    /// - Pagination dots
    /// - Card selection and highlighting
    /// </summary>
    public class CarouselController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform contentParent;           // Horizontal Layout Group container
        [SerializeField] private GameObject characterCardPrefab;
        [SerializeField] private CharacterDetailPanel detailPanel; // Reference to the DetailPanel    // CharacterThumbnailCard prefab
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private Transform paginationDotsParent;
        [SerializeField] private GameObject paginationDotPrefab;
        
        [Header("Settings")]
        [SerializeField] private int cardsPerPage = 6;
        [SerializeField] private float scrollSmoothness = 0.3f;
        
        private List<CharacterThumbnailCard> cards = new List<CharacterThumbnailCard>();
        private PaginationDotStrip _dots;
        private ScrollRect scrollRect;
        private int currentPage = 0;
        private int totalPages = 1;
        private string selectedCharacterId = "";
        private bool viewportExpanded = false;
        private bool _isAnimating = false;  // true during arrow smooth-scroll; suppresses swipe listener
        
        // Events
        public event System.Action<string> OnCharacterSelected; // Change to event
        
        private void Awake()
        {
            // ScrollRect is on the ScrollView child, not on this GameObject
            scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect == null)
            {
                Debug.LogError("[CarouselController] ScrollRect component not found! Make sure ScrollView exists with ScrollRect component.");
            }
            else
            {
                Debug.Log("[CarouselController] Found ScrollRect component");
            }
        }
        
        private void Start()
        {
            PopulateCarousel();
            SetupArrowButtons();
            SetupPagination();
        }
        
        /// <summary>
        /// Populate carousel with owned characters
        /// 
        /// Phase 2a: Shows only owned characters (prefab optional for testing)
        /// Phase 2b: Will extend to show locked characters (separate visual state, non-interactive)
        /// </summary>
        private void PopulateCarousel()
        {
            Debug.Log("[CarouselController] Populating carousel");

            // Ensure Content's HorizontalLayoutGroup doesn't stretch cards
            var layoutGroup = contentParent.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }
            else
            {
                Debug.LogWarning("[CarouselController] contentParent has no HorizontalLayoutGroup — cards may not lay out correctly");
            }

            // Ensure Content expands horizontally to fit all cards (required for ScrollRect to scroll)
            var sizeFitter = contentParent.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
                sizeFitter = contentParent.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Expand Viewport rect once so the 5% scale-up on selected cards isn't clipped
            if (!viewportExpanded && scrollRect != null && scrollRect.viewport != null)
            {
                var vp = scrollRect.viewport;
                float overflow = 9f; // ~5% of 170px card width
                vp.offsetMin -= new Vector2(overflow, overflow);   // extend left + bottom
                vp.offsetMax += new Vector2(overflow, overflow);   // extend right + top
                viewportExpanded = true;
            }

            // Clear existing cards
            cards.Clear();
            foreach (Transform child in contentParent)
            {
                Destroy(child.gameObject);
            }
            
            // Get owned characters from CharacterManager
            // TODO (Phase 2b): Filter for both owned AND locked, display with different states
            if (CharacterManager.Instance == null)
            {
                Debug.LogWarning("[CarouselController] CharacterManager not ready yet");
                return;
            }
            var ownedCharacters = CharacterManager.Instance.GetAllOwnedCharacters();
            
            if (ownedCharacters.Count == 0)
            {
                Debug.LogWarning("[CarouselController] No owned characters found!");
                return;
            }
            
            // Check if prefab is assigned
            if (characterCardPrefab == null)
            {
                Debug.LogWarning("[CarouselController] characterCardPrefab not assigned - skipping card creation (assign in Phase 2b)");
                Debug.Log($"[CarouselController] {ownedCharacters.Count} characters would be created once prefab is assigned");
                
                // For Phase 2a testing: Create placeholder cards without the prefab
                CreatePlaceholderCards(ownedCharacters);
                return;
            }
            
            // Create card for each character using prefab
            foreach (var playerChar in ownedCharacters)
            {
                var cardGO = Instantiate(characterCardPrefab, contentParent);

                // Ensure each card holds its preferred size against the layout group
                var le = cardGO.GetComponent<LayoutElement>() ?? cardGO.AddComponent<LayoutElement>();
                le.preferredWidth = 170f;
                le.preferredHeight = 343f;

                var card = cardGO.GetComponent<CharacterThumbnailCard>();
                if (card != null)
                {
                    card.Initialize(playerChar.characterId);
                    card.OnClicked += () => SelectCharacter(playerChar.characterId);
                    cards.Add(card);

                    Debug.Log($"[CarouselController] Created card for {playerChar.characterId}");
                }
            }
            
            // Select first character by default
            if (cards.Count > 0)
                SelectCharacter(cards[0].GetCharacterId());

            Debug.Log($"[CarouselController] Populated with {cards.Count} cards");
        }
        
        /// <summary>
        /// Create placeholder cards for Phase 2a testing (when prefab not yet created)
        /// </summary>
        private void CreatePlaceholderCards(System.Collections.Generic.List<PlayerCharacterData> characters)
        {
            Debug.Log("[CarouselController] Creating placeholder cards for Phase 2a testing");
            
            foreach (var playerChar in characters)
            {
                var placeholderGO = new GameObject($"Card_{playerChar.characterId}");
                placeholderGO.transform.SetParent(contentParent);
                
                var placeholderButton = placeholderGO.AddComponent<Button>();
                var placeholderImage = placeholderGO.AddComponent<Image>();
                placeholderImage.color = new Color(0.2f, 0.2f, 0.3f, 1f);
                
                var rect = placeholderGO.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(150, 200);
                
                // Add label
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(placeholderGO.transform);
                var labelText = labelGO.AddComponent<TextMeshProUGUI>();
                labelText.text = playerChar.characterId;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.fontSize = 4;
                
                var labelRect = labelGO.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                
                // Make button clickable
                var characterId = playerChar.characterId;
                placeholderButton.onClick.AddListener(() => SelectCharacter(characterId));
                
                Debug.Log($"[CarouselController] Created placeholder card for {playerChar.characterId}");
            }
            
            Debug.Log($"[CarouselController] Populated with {characters.Count} placeholder cards for testing");
        }
        
        /// <summary>
        /// Wire arrow buttons — called once in Start after PopulateCarousel.
        /// </summary>
        private void SetupArrowButtons()
        {
            if (leftArrowButton != null)
                leftArrowButton.onClick.AddListener(() => GoToPage(currentPage - 1));

            if (rightArrowButton != null)
                rightArrowButton.onClick.AddListener(() => GoToPage(currentPage + 1));
        }

        /// <summary>
        /// Navigate to a page, smooth-scroll, and refresh UI state.
        /// </summary>
        private void GoToPage(int page)
        {
            page = Mathf.Clamp(page, 0, totalPages - 1);
            if (page == currentPage) return;
            currentPage = page;

            // Normalized position: page 0 → 0.0, last page → 1.0
            float targetPos = totalPages > 1 ? (float)currentPage / (totalPages - 1) : 0f;
            StartCoroutine(SmoothScroll(targetPos));

            RefreshDotColors();
            UpdateArrowButtonStates();
        }

        private System.Collections.IEnumerator SmoothScroll(float targetPos)
        {
            if (scrollRect == null) yield break;

            _isAnimating = true;

            float elapsed  = 0f;
            float startPos = scrollRect.horizontalNormalizedPosition;

            while (elapsed < scrollSmoothness)
            {
                elapsed += Time.deltaTime;
                scrollRect.horizontalNormalizedPosition =
                    Mathf.Lerp(startPos, targetPos, elapsed / scrollSmoothness);
                yield return null;
            }

            scrollRect.horizontalNormalizedPosition = targetPos;
            _isAnimating = false;
        }
        
        /// <summary>
        /// Build pagination dots once after PopulateCarousel. Creates dots from the
        /// prefab if assigned, otherwise falls back to a minimal runtime dot.
        /// </summary>
        private void SetupPagination()
        {
            totalPages = Mathf.CeilToInt(cards.Count > 0 ? (float)cards.Count / cardsPerPage : 1);
            currentPage = 0;

            if (paginationDotsParent != null)
            {
                // Pooled + windowed: the strip creates at most PaginationDotStrip.MaxDots dots
                // once and reuses them, instead of destroying and rebuilding one dot per page.
                _dots ??= new PaginationDotStrip(paginationDotsParent, paginationDotPrefab);
                _dots.Rebuild(totalPages, currentPage);
            }
            else
            {
                Debug.Log("[CarouselController] paginationDotsParent not assigned — dots skipped.");
            }

            UpdateArrowButtonStates();

            // Swipe/drag listener — fires on every frame the user moves the scroll view
            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        /// <summary>
        /// Called by ScrollRect every frame during a drag. Skipped during arrow animation.
        /// </summary>
        private void OnScrollValueChanged(Vector2 scrollPos)
        {
            if (_isAnimating || totalPages <= 1) return;

            int newPage = Mathf.Clamp(
                Mathf.RoundToInt(scrollPos.x * (totalPages - 1)),
                0, totalPages - 1);

            if (newPage == currentPage) return;

            currentPage = newPage;
            RefreshDotColors();
            UpdateArrowButtonStates();
        }

        /// <summary>
        /// Update dot colours only — no allocation.
        /// </summary>
        private void RefreshDotColors()
        {
            _dots?.Refresh(currentPage);
        }

        /// <summary>
        /// Disable left arrow on first page, right arrow on last page.
        /// </summary>
        private void UpdateArrowButtonStates()
        {
            if (leftArrowButton  != null) leftArrowButton.interactable  = currentPage > 0;
            if (rightArrowButton != null) rightArrowButton.interactable = currentPage < totalPages - 1;
        }
        
        /// <summary>
        /// Select a character by ID
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            // Deselect previous
            if (!string.IsNullOrEmpty(selectedCharacterId))
            {
                var prevCard = cards.Find(c => c.GetCharacterId() == selectedCharacterId);
                if (prevCard != null)
                    prevCard.SetSelected(false);
            }
            
            // Select new
            selectedCharacterId = characterId;
            var card = cards.Find(c => c.GetCharacterId() == characterId);
            if (card != null)
            {
                card.SetSelected(true);
            }
            
            // Notify listeners
            OnCharacterSelected?.Invoke(characterId);
            
            Debug.Log($"[CarouselController] Selected: {characterId}");
        }
        
        private void OnEnable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged += OnPointsChanged;
        }

        private void OnDisable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= OnPointsChanged;
        }

        /// <summary>
        /// Re-evaluate the level-up-ready icon on every card when RP balance changes.
        /// </summary>
        private void OnPointsChanged(int _)
        {
            foreach (var card in cards)
                card.RefreshIcons();
        }

        /// <summary>
        /// Get currently selected character ID
        /// </summary>
        public string GetSelectedCharacterId() => selectedCharacterId;
    }
}
