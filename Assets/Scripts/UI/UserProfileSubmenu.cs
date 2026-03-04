using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.UI
{
    /// <summary>
    /// User Profile submenu with username editing and account linking.
    /// </summary>
    public class UserProfileSubmenu : MonoBehaviour
    {
        [Header("Username Editing")]
        [SerializeField] private TMP_InputField usernameInputField;
        [SerializeField] private Button saveUsernameButton;
        [SerializeField] private TextMeshProUGUI feedbackText;
        
        [Header("Account Linking (Phase 3)")]
        [SerializeField] private Button linkGoogleButton;
        [SerializeField] private Button linkAppleButton;
        [SerializeField] private Button linkTwitterButton;
        [SerializeField] private GameObject linkedIndicatorGoogle;
        [SerializeField] private GameObject linkedIndicatorApple;
        [SerializeField] private GameObject linkedIndicatorTwitter;
        
        [Header("Settings")]
        [SerializeField] private int maxUsernameLength = 16;
        [SerializeField] private int minUsernameLength = 3;
        
        private const string USERNAME_KEY = "Settings_Username";
        private string _originalUsername;

        private void Awake()
        {
            // Wire up events
            if (saveUsernameButton != null)
            {
                saveUsernameButton.onClick.AddListener(OnSaveUsernameClicked);
            }
            
            if (usernameInputField != null)
            {
                usernameInputField.characterLimit = maxUsernameLength;
                usernameInputField.onValueChanged.AddListener(OnUsernameChanged);
            }
            
            // Account linking buttons (Phase 3)
            if (linkGoogleButton != null)
            {
                linkGoogleButton.onClick.AddListener(() => OnAccountLinkClicked("Google"));
            }
            
            if (linkAppleButton != null)
            {
                linkAppleButton.onClick.AddListener(() => OnAccountLinkClicked("Apple"));
            }
            
            if (linkTwitterButton != null)
            {
                linkTwitterButton.onClick.AddListener(() => OnAccountLinkClicked("Twitter"));
            }
        }

        private void Start()
        {
            LoadUsername();
            UpdateAccountLinkingUI();
        }

        /// <summary>
        /// Load the saved username from PlayerPrefs.
        /// </summary>
        private void LoadUsername()
        {
            string savedUsername = PlayerPrefs.GetString(USERNAME_KEY, "Player");
            _originalUsername = savedUsername;
            
            if (usernameInputField != null)
            {
                usernameInputField.text = savedUsername;
            }
            
            // Hide feedback on load
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
            
            // Save button starts disabled
            if (saveUsernameButton != null)
            {
                saveUsernameButton.interactable = false;
            }
            
            Debug.Log($"[UserProfile] Loaded username: {savedUsername}");
        }

        /// <summary>
        /// Called when username input changes.
        /// </summary>
        private void OnUsernameChanged(string newUsername)
        {
            // Enable save button only if username changed and is valid
            bool hasChanged = newUsername != _originalUsername;
            bool isValid = IsUsernameValid(newUsername);
            
            if (saveUsernameButton != null)
            {
                saveUsernameButton.interactable = hasChanged && isValid;
            }
            
            // Show validation feedback
            if (hasChanged && feedbackText != null)
            {
                if (!isValid)
                {
                    feedbackText.gameObject.SetActive(true);
                    feedbackText.text = GetValidationMessage(newUsername);
                    feedbackText.color = Color.red;
                }
                else
                {
                    feedbackText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Called when Save Username button is clicked.
        /// </summary>
        private void OnSaveUsernameClicked()
        {
            if (usernameInputField == null) return;
            
            string newUsername = usernameInputField.text.Trim();
            
            if (!IsUsernameValid(newUsername))
            {
                ShowFeedback("Invalid username", Color.red);
                return;
            }
            
            // Save to PlayerPrefs
            PlayerPrefs.SetString(USERNAME_KEY, newUsername);
            PlayerPrefs.Save();
            
            _originalUsername = newUsername;
            
            // Update PersistentUI if available
            if (PersistentUIManager.Instance != null)
            {
                PersistentUIManager.Instance.UpdateUsername(newUsername);
            }
            
            // Show success feedback
            ShowFeedback("Username saved!", new Color(0.2f, 0.8f, 0.2f));
            
            // Disable save button
            if (saveUsernameButton != null)
            {
                saveUsernameButton.interactable = false;
            }
            
            Debug.Log($"[UserProfile] Username saved: {newUsername}");
        }

        /// <summary>
        /// Validate username meets requirements.
        /// </summary>
        private bool IsUsernameValid(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (username.Length < minUsernameLength) return false;
            if (username.Length > maxUsernameLength) return false;
            
            // Check for invalid characters (optional)
            // For now, allow alphanumeric + underscore + spaces
            foreach (char c in username)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != ' ')
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Get validation error message.
        /// </summary>
        private string GetValidationMessage(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return "Username cannot be empty";
            }
            
            if (username.Length < minUsernameLength)
            {
                return $"Username must be at least {minUsernameLength} characters";
            }
            
            if (username.Length > maxUsernameLength)
            {
                return $"Username must be {maxUsernameLength} characters or less";
            }
            
            return "Username contains invalid characters";
        }

        /// <summary>
        /// Show feedback message.
        /// </summary>
        private void ShowFeedback(string message, Color color)
        {
            if (feedbackText == null) return;
            
            feedbackText.text = message;
            feedbackText.color = color;
            feedbackText.gameObject.SetActive(true);
            
            // Auto-hide after 3 seconds
            CancelInvoke(nameof(HideFeedback));
            Invoke(nameof(HideFeedback), 3f);
        }

        /// <summary>
        /// Hide feedback message.
        /// </summary>
        private void HideFeedback()
        {
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Called when an account linking button is clicked (Phase 3).
        /// </summary>
        private void OnAccountLinkClicked(string provider)
        {
            Debug.Log($"[UserProfile] TODO Phase 3: Link {provider} account");
            ShowFeedback($"{provider} linking coming in Phase 3", Color.yellow);
        }

        /// <summary>
        /// Update account linking UI (Phase 3).
        /// </summary>
        private void UpdateAccountLinkingUI()
        {
            // TODO Phase 3: Check which accounts are linked and update indicators
            // For now, hide all indicators
            if (linkedIndicatorGoogle != null) linkedIndicatorGoogle.SetActive(false);
            if (linkedIndicatorApple != null) linkedIndicatorApple.SetActive(false);
            if (linkedIndicatorTwitter != null) linkedIndicatorTwitter.SetActive(false);
        }

        /// <summary>
        /// Get the current username.
        /// </summary>
        public string GetUsername()
        {
            return _originalUsername;
        }
    }
}
