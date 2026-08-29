// Order: login_signup_screens — Phase 1 (UI only, no backend)
using UnityEngine;
using Golfin.Roster;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Auth;
using Golfin.InventorySync;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Create Username screen controller — Phase 2 (mockable auth).
    /// CREATE validates the username then calls <see cref="AuthService"/>.UpdateDisplayName
    /// (Supabase user_metadata.display_name); on success advances to Home.
    /// </summary>
    public class CreateUsernameScreenController : MonoBehaviour
    {
        [Header("ScreenManager")]
        [SerializeField] private ScreenManager _screenManager;

        [Header("Input")]
        [SerializeField] private TMP_InputField _usernameInput;

        [Header("Buttons")]
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _cancelButton;

        [Header("Feedback (optional)")]
        [Tooltip("Optional TMP label shown for validation / auth messages. Safe to leave unset.")]
        [SerializeField] private TextMeshProUGUI _errorLabel;


        private bool _busy;

        /// <summary>starter_restore_gate: the last gate answer was ServerUnreachable. The username is
        /// already claimed at that point, so the next CREATE tap only re-runs the gate.</summary>
        private bool _starterRetryPending;

        private void Awake()
        {
            // Mobile: type directly into the field — hide the OS keyboard's own input bar,
            // which otherwise receives the text and only forwards it on commit.
            if (_usernameInput != null) _usernameInput.shouldHideMobileInput = true;
        }

        private void OnEnable()
        {
            ClearError();
            _starterRetryPending = false;
            if (_createButton != null) _createButton.onClick.AddListener(OnCreateClicked);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private void OnDisable()
        {
            _starterRetryPending = false;
            if (_createButton != null) _createButton.onClick.RemoveListener(OnCreateClicked);
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        private void OnCreateClicked()
        {
            if (_busy) return;

            // starter_restore_gate D1: the name is already claimed and the display name already
            // written — only the inventory fetch failed. This tap retries THAT, not the claim.
            if (_starterRetryPending)
            {
                ClearError();
                InventorySyncBehaviour.RetryBoot();
                RouteAfterAuth();
                return;
            }

            string username = _usernameInput != null ? _usernameInput.text.Trim() : "";
            if (!UsernameRules.IsValid(username))
            { SetError(UsernameRules.Requirement); return; }

            SetBusy(true);

            // Uniqueness first (unique_usernames): the backend's profiles row — the name every
            // OTHER player's board shows — is claimed under a unique index BEFORE the auth
            // metadata write. A taken name stops here with its own message; the metadata write
            // below never runs for a name the player does not own.
            UsernameClaim.Claim(username, claim =>
            {
                if (!claim.MayProceed)
                {
                    SetBusy(false);
                    SetError(claim.Message);
                    return;
                }

                AuthService.Instance.UpdateDisplayName(username, result =>
                {
                    if (result.Success)
                    {
                        AccountUiBridge.SyncUsername();
                        RouteAfterAuth();   // stays busy until the gate answers
                        return;
                    }
                    SetBusy(false);
                    SetError(result.Message);
                });
            });
        }

        /// <summary>
        /// Post-auth routing, gated on the SERVER's inventory answer (starter_restore_gate §3).
        /// The NeedsStarter branch is unchanged — only WHEN it may be read is.
        /// </summary>
        private void RouteAfterAuth()
        {
            SetBusy(true);
            Golfin.UI.Account.StarterGate.Resolve(route =>
            {
                SetBusy(false);

                if (route == Golfin.UI.Account.StarterRoute.ServerUnreachable)
                {
                    // D1: never the picker on a failed fetch.
                    _starterRetryPending = true;
                    SetError(LocalizationManager.Get("AUTH_ERR_OFFLINE"));
                    return;
                }

                _starterRetryPending = false;
                if (_screenManager == null) return;
                // starting_character_selection: first-run players pick a starter before Home
                if (CharacterManager.Instance != null && CharacterManager.Instance.NeedsStarter)
                    _screenManager.ShowScreen(GolfinRedux.UI.ScreenId.StartingCharacterSelection);
                else
                    _screenManager.ShowScreen(GolfinRedux.UI.ScreenId.Home);
            });
        }

        private void OnCancelClicked()
        {
            if (_screenManager != null)
                _screenManager.ShowScreen(ScreenId.Login);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_createButton != null) _createButton.interactable = !busy;
        }

        private static readonly Color ErrColor = new Color(0.898f, 0.282f, 0.302f); // #E5484D

        private void SetError(string message)
        {
            if (_errorLabel != null)
            {
                bool has = !string.IsNullOrEmpty(message);
                _errorLabel.gameObject.SetActive(has);
                _errorLabel.text  = message ?? "";
                _errorLabel.color = ErrColor;
            }
            if (!string.IsNullOrEmpty(message)) Debug.Log($"[CreateUsernameScreen] {message}");
        }

        private void ClearError()
        {
            if (_errorLabel != null) _errorLabel.gameObject.SetActive(false);
        }
    }
}
