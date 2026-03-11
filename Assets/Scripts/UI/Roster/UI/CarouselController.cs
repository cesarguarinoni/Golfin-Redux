#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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
        private List<Image> paginationDots = new List<Image>();
        private ScrollRect scrollRect;
        private int currentPage = 0;
        private string selectedCharacterId = "";
        
        // Events
        public System.Action<string> OnCharacterSelected;
        
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
            
            // Pagination dots are optional
            if (paginationDotPrefab != null && paginationDotsParent != null)
            {
                UpdatePaginationDots();
            }
            else
            {
                Debug.Log("[CarouselController] Pagination dots disabled (prefab not assigned)");
            }
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
            
            // Clear existing cards
            cards.Clear();
            foreach (Transform child in contentParent)
            {
                Destroy(child.gameObject);
            }
            
            // Get owned characters from CharacterManager
            // TODO (Phase 2b): Filter for both owned AND locked, display with different states
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
            {
                SelectCharacter(cards[0].GetCharacterId());
            }
            
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
        /// Setup left/right arrow buttons
        /// </summary>
        private void SetupArrowButtons()
        {
            if (leftArrowButton != null)
                leftArrowButton.onClick.AddListener(ScrollLeft);
            
            if (rightArrowButton != null)
                rightArrowButton.onClick.AddListener(ScrollRight);
            
            UpdateArrowButtonStates();
        }
        
        /// <summary>
        /// Scroll carousel left
        /// </summary>
        public void ScrollLeft()
        {
            if (currentPage > 0)
            {
                currentPage--;
                ScrollToPage(currentPage);
                UpdatePaginationDots();
                UpdateArrowButtonStates();
            }
        }
        
        /// <summary>
        /// Scroll carousel right
        /// </summary>
        public void ScrollRight()
        {
            int maxPages = Mathf.CeilToInt((float)cards.Count / cardsPerPage) - 1;
            if (currentPage < maxPages)
            {
                currentPage++;
                ScrollToPage(currentPage);
                UpdatePaginationDots();
                UpdateArrowButtonStates();
            }
        }
        
        /// <summary>
        /// Scroll to specific page
        /// </summary>
        private void ScrollToPage(int page)
        {
            if (scrollRect == null) return;
            
            // Calculate target scroll position
            float cardWidth = 1f / cards.Count;
            float targetScrollPos = page * cardWidth * cardsPerPage / (float)cards.Count;
            targetScrollPos = Mathf.Clamp01(targetScrollPos);
            
            // Smooth scroll
            StartCoroutine(SmoothScroll(targetScrollPos));
        }
        
        private System.Collections.IEnumerator SmoothScroll(float targetPos)
        {
            float elapsedTime = 0f;
            float startPos = scrollRect.horizontalNormalizedPosition;
            
            while (elapsedTime < scrollSmoothness)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / scrollSmoothness;
                scrollRect.horizontalNormalizedPosition = Mathf.Lerp(startPos, targetPos, t);
                yield return null;
            }
            
            scrollRect.horizontalNormalizedPosition = targetPos;
        }
        
        /// <summary>
        /// Update pagination dots to show current page
        /// </summary>
        private void UpdatePaginationDots()
        {
            // Safety check
            if (paginationDotPrefab == null || paginationDotsParent == null)
            {
                return;
            }
            
            // Clear existing dots
            paginationDots.Clear();
            foreach (Transform child in paginationDotsParent)
            {
                Destroy(child.gameObject);
            }
            
            // Create new dots
            int totalPages = Mathf.CeilToInt((float)cards.Count / cardsPerPage);
            for (int i = 0; i < totalPages; i++)
            {
                var dotGO = Instantiate(paginationDotPrefab, paginationDotsParent);
                var dotImage = dotGO.GetComponent<Image>();
                
                if (dotImage != null)
                {
                    dotImage.color = (i == currentPage) ? Color.white : new Color(1, 1, 1, 0.5f);
                    paginationDots.Add(dotImage);
                }
            }
        }
        
        /// <summary>
        /// Update arrow button states (disable when at start/end)
        /// </summary>
        private void UpdateArrowButtonStates()
        {
            if (leftArrowButton != null)
                leftArrowButton.interactable = (currentPage > 0);
            
            if (rightArrowButton != null)
            {
                int maxPages = Mathf.CeilToInt((float)cards.Count / cardsPerPage) - 1;
                rightArrowButton.interactable = (currentPage < maxPages);
            }
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
        
        /// <summary>
        /// Get currently selected character ID
        /// </summary>
        public string GetSelectedCharacterId() => selectedCharacterId;
    }
}
