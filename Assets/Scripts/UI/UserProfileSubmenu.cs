using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Auth;
using Golfin.UI.Account;

namespace Golfin.UI
{
    /// <summary>
    /// User Profile submenu with username editing and account linking.
    ///
    /// The username here is the REAL account display name: it is read from
    /// <see cref="AuthService"/>.Session.DisplayName and written through
    /// <c>AuthService.UpdateDisplayName</c>, which PUTs Supabase Auth
    /// <c>user_metadata.display_name</c> and persists the session. It previously read and wrote a
    /// local <c>Settings_Username</c> PlayerPrefs key that nothing else in the game ever consulted,
    /// so a name changed here looked changed but was not — sign out and it was gone.
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
        
        private string _originalUsername;
        private bool _busy;

        private void Awake()
        {
            // Wire up events
            if (saveUsernameButton != null)
            {
                saveUsernameButton.onClick.AddListener(OnSaveUsernameClicked);
            }
            
            if (usernameInputField != null)
            {
                usernameInputField.characterLimit = UsernameRules.MaxLength;
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

        private void OnEnable()
        {
            // The accordion deactivates this object while collapsed, so re-read the session on every
            // open — the name may have changed elsewhere (Create Username, a different sign-in).
            LoadUsername();
        }

        /// <summary>
        /// Load the current display name from the auth session (the source of truth).
        /// </summary>
        private void LoadUsername()
        {
            var session = AuthService.Instance != null ? AuthService.Instance.Session : null;
            string currentUsername = session != null && session.HasDisplayName ? session.DisplayName : "";
            _originalUsername = currentUsername;
            
            if (usernameInputField != null)
            {
                usernameInputField.SetTextWithoutNotify(currentUsername);
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
            
            Debug.Log($"[UserProfile] Loaded username from session: '{currentUsername}'");
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
            if (usernameInputField == null || _busy) return;
            
            string newUsername = usernameInputField.text.Trim();
            
            if (!IsUsernameValid(newUsername))
            {
                ShowFeedback(GetValidationMessage(newUsername), Color.red);
                return;
            }
            
            // Two live round-trips — lock the button until both land. The backend uniqueness
            // claim (profiles.display_name, unique index) runs FIRST; only a name the server
            // granted is written into Supabase Auth user_metadata below. A taken name shows its
            // own message and changes nothing anywhere.
            SetBusy(true);
            UsernameClaim.Claim(newUsername, claim =>
            {
                if (!claim.MayProceed)
                {
                    SetBusy(false);
                    ShowFeedback(claim.Message, Color.red);
                    Debug.LogWarning($"[UserProfile] Username claim refused ({claim.Status}): {claim.Message}");
                    return;
                }

                SaveUsernameToAccount(newUsername);
            });
        }

        /// <summary>The pre-existing Supabase metadata write, unchanged — it just runs after the
        /// uniqueness claim now instead of being the whole save.</summary>
        private void SaveUsernameToAccount(string newUsername)
        {
            AuthService.Instance.UpdateDisplayName(newUsername, result =>
            {
                SetBusy(false);
                
                if (result == null || !result.Success)
                {
                    string message = result != null && !string.IsNullOrEmpty(result.Message)
                        ? result.Message
                        : "Could not save username.";
                    ShowFeedback(message, Color.red);
                    Debug.LogWarning($"[UserProfile] Username change rejected: {message}");
                    return;
                }
                
                // AuthService.Wrap already applied + saved the session; push it into the shell UI.
                _originalUsername = newUsername;
                AccountUiBridge.SyncUsername();
                
                ShowFeedback("Username saved!", new Color(0.2f, 0.8f, 0.2f));
                
                if (saveUsernameButton != null)
                {
                    saveUsernameButton.interactable = false;
                }
                
                Debug.Log($"[UserProfile] Username saved to account: {newUsername}");
            });
        }

        /// <summary>
        /// Lock the field and button while the account update is in flight.
        /// </summary>
        private void SetBusy(bool busy)
        {
            _busy = busy;
            
            if (saveUsernameButton != null) saveUsernameButton.interactable = !busy;
            if (usernameInputField != null) usernameInputField.interactable = !busy;
        }

        /// <summary>
        /// Validate username meets requirements.
        /// </summary>
        private bool IsUsernameValid(string username)
        {
            return UsernameRules.IsValid(username);
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
            
            return UsernameRules.Requirement;
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
