// ─────────────────────────────────────────────────────────────────────────────
// StartingCharacterConfirmModalController
// Confirm modal shown when a new player taps CHOOSE on a starter character.
// Extends ModalController (same show/hide pattern as TournamentSignupModal).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.UI.Modals;

namespace Golfin.Roster
{
    /// <summary>
    /// Controller for the StartingCharacterConfirmModal prefab.
    /// Call Open(characterId) to show; CONFIRM grants the starter and navigates to Home.
    /// </summary>
    public class StartingCharacterConfirmModalController : ModalController
    {
        [Header("Character Preview")]
        [SerializeField] private TextMeshProUGUI? _characterNameText;
        [SerializeField] private Image?           _characterPortrait;

        [Header("Buttons")]
        [SerializeField] private Button? _confirmButton;
        [SerializeField] private Button? _backButton;
        [SerializeField] private TextMeshProUGUI? _confirmButtonText;
        [SerializeField] private TextMeshProUGUI? _backButtonText;

        private string _pendingCharacterId = "";

        protected override void Awake()
        {
            base.Awake();

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            if (_backButton != null)
                _backButton.onClick.AddListener(OnBackClicked);
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshLocalization;
            RefreshLocalization();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            LocalizationManager.OnLanguageChanged -= RefreshLocalization;
        }

        private void RefreshLocalization()
        {
            if (_confirmButtonText != null)
                _confirmButtonText.text = LocalizationManager.Get("MODAL_CONFIRM");
            if (_backButtonText != null)
                _backButtonText.text = LocalizationManager.Get("ROSTER_STARTER_BACK");
            // _instructionText removed: the modal does NOT contain the instruction copy.
            // The instruction text lives exclusively in the bottom band on the starter screen.
        }

        /// <summary>Show the modal for the given character.</summary>
        public void Open(string characterId)
        {
            _pendingCharacterId = characterId;

            // Populate character name (localized two-line FIRSTNAME\nLASTNAME)
            if (_characterNameText != null)
            {
                var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
                _characterNameText.text = (csvData?.GetLocalizedDisplayName(singleLine: false) ?? characterId).Replace("\n", " ");
            }

            // Populate portrait
            if (_characterPortrait != null)
            {
                var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
                if (csvData?.portraitFullSprite != null)
                    _characterPortrait.sprite = csvData.portraitFullSprite;
                else if (csvData?.portraitSprite != null)
                    _characterPortrait.sprite = csvData.portraitSprite;
            }

            RefreshLocalization();
            Show();
        }

        private void OnConfirmClicked()
        {
            if (string.IsNullOrEmpty(_pendingCharacterId)) return;

            Debug.Log($"[StartingCharacterConfirmModal] Confirming starter: {_pendingCharacterId}");

            if (CharacterManager.Instance != null)
                CharacterManager.Instance.GrantStarter(_pendingCharacterId);

            Hide();

            // Navigate to Home — starter selection is complete
            GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.Home);
        }

        private void OnBackClicked()
        {
            Debug.Log("[StartingCharacterConfirmModal] Back pressed — returning to starter selection.");
            Hide();
        }
    }
}
