#nullable enable
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Golfin.UI;

namespace Golfin.Roster
{
    /// <summary>
    /// Main controller for the Roster Screen
    /// Manages carousel, detail panel, and modal interactions
    /// Placed on RosterScreen GameObject in ShellScene
    /// </summary>
    public class RosterScreenController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI rewardPointsText;
        [SerializeField] private CarouselController carousel;

        [Header("Starter Mode")]
        [SerializeField] private GameObject? _starterInstructionBar;   // activated in starter mode
        [SerializeField] private GameObject? _starterConfirmModal;      // StartingCharacterConfirmModal root

        private bool _isStarterMode = false;
        private string currentCharacterId = "";

        /// <summary>starter_restore_gate §5: true while this screen is listening for a starter that
        /// arrives from the server AFTER the picker was already shown.</summary>
        private bool _watchingForLateStarter;

        private void Start()
        {
            InitializeScreen();
            UpdateRewardPointsDisplay();
        }

        private void InitializeScreen()
        {
            Debug.Log("[RosterScreenController] Initializing Roster Screen");

            // Get first owned character
            if (CharacterManager.Instance == null)
            {
                Debug.LogWarning("[RosterScreenController] CharacterManager not ready yet");
                return;
            }
            var characters = CharacterManager.Instance.GetAllOwnedCharacters();
            if (characters.Count > 0)
            {
                currentCharacterId = characters[0].characterId;
                CharacterManager.Instance.SelectCharacter(currentCharacterId);
                Debug.Log($"[RosterScreenController] Selected first character: {currentCharacterId}");
            }
            else
            {
                // In starter mode a new player has no owned chars yet — select first catalog char
                var catalog = CharacterManager.Instance.GetAllCatalogCharacters();
                if (catalog.Count > 0)
                {
                    currentCharacterId = catalog[0].characterId;
                    Debug.Log($"[RosterScreenController] No owned chars; defaulting to catalog: {currentCharacterId}");
                }
                else
                {
                    Debug.LogError("[RosterScreenController] No characters found!");
                }
            }
        }

        private void OnEnable()
        {
            // Subscribe to events
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged += UpdateRewardPointsDisplay;
            if (CharacterManager.Instance != null)
                CharacterManager.Instance.OnCharacterSelected += OnCharacterSelected;
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= UpdateRewardPointsDisplay;
            if (CharacterManager.Instance != null)
                CharacterManager.Instance.OnCharacterSelected -= OnCharacterSelected;

            StopWatchingForLateStarter();   // project convention: nothing outlives OnDisable
        }

        // ── starter_restore_gate §5: leave if the answer arrives late ─────────

        /// <summary>
        /// Belt and braces for the one path <c>StarterGate</c> cannot own: a player who reaches the
        /// picker WITHOUT going through one of the three post-auth routers — Reset Starter Choice in
        /// <c>RosterDebugTools</c>, or a restore that lands a beat after the picker opened. If the
        /// roster changes and the save now names a starter, this screen has nothing left to ask.
        /// </summary>
        private void StartWatchingForLateStarter()
        {
            if (_watchingForLateStarter || CharacterManager.Instance == null) return;
            CharacterManager.Instance.OnRosterChanged += OnRosterChangedWhileInStarterMode;
            _watchingForLateStarter = true;
        }

        private void StopWatchingForLateStarter()
        {
            if (!_watchingForLateStarter) return;
            if (CharacterManager.Instance != null)
                CharacterManager.Instance.OnRosterChanged -= OnRosterChangedWhileInStarterMode;
            _watchingForLateStarter = false;
        }

        private void OnRosterChangedWhileInStarterMode()
        {
            if (!_isStarterMode) { StopWatchingForLateStarter(); return; }
            if (CharacterManager.Instance == null || CharacterManager.Instance.NeedsStarter) return;

            Debug.Log("[RosterScreenController] A starter arrived while the picker was open " +
                      "(server restore) — nothing left to ask; leaving for Home.");
            StopWatchingForLateStarter();
            _isStarterMode = false;

            var manager = FindFirstObjectByType<GolfinRedux.UI.ScreenManager>();
            if (manager != null) manager.ShowScreen(GolfinRedux.UI.ScreenId.Home);
            else Debug.LogWarning("[RosterScreenController] ScreenManager not found — cannot leave starter mode.");
        }

        /// <summary>
        /// Called when carousel selects a character
        /// </summary>
        public void OnCarouselCharacterSelected(string characterId)
        {
            currentCharacterId = characterId;
            if (!_isStarterMode)
                CharacterManager.Instance?.SelectCharacter(characterId);

            Debug.Log($"[RosterScreenController] Character selected from carousel: {characterId}");
        }

        /// <summary>
        /// Called when CharacterManager selects a character
        /// </summary>
        private void OnCharacterSelected(string characterId)
        {
            currentCharacterId = characterId;
            Debug.Log($"[RosterScreenController] Character selected (event): {characterId}");
        }

        private void UpdateRewardPointsDisplay(int points = -1)
        {
            if (rewardPointsText == null) return;

            int currentPoints = RewardPointsManager.Instance.GetPoints();
            rewardPointsText.text = $"R {currentPoints}";
        }

        /// <summary>
        /// Get currently selected character ID
        /// </summary>
        public string GetCurrentCharacterId() => currentCharacterId;

        /// <summary>
        /// Called by ScreenManager when the roster is opened in StartingCharacterSelection mode.
        /// Activates the instruction bar, hides bottom nav (recomputed here so ANY entry path — not
        /// just the ScreenManager transition — enforces ShowTopBarOnly), and restricts carousel to
        /// starter candidates only.
        /// </summary>
        public void SetStarterMode(bool isStarterMode)
        {
            // F6-PathB fix: ScreenManager navigates via ScreenId.Roster (not StartingCharacterSelection)
            // when the user taps NavCharactersButton, so it passes isStarterMode=false even when
            // CharacterManager.NeedsStarter is true (e.g. after Reset Starter Choice). Override here so
            // the starter UI is always enforced regardless of which ScreenId triggered the transition.
            if (!isStarterMode && CharacterManager.Instance != null && CharacterManager.Instance.NeedsStarter)
            {
                Debug.Log("[RosterScreenController] F6-PathB: overriding isStarterMode→true (NeedsStarter=true)");
                isStarterMode = true;
            }

            _isStarterMode = isStarterMode;

            // F6 fix: recompute bar visibility on every entry, not only when ScreenManager drives the
            // transition. This covers "Reset Starter Choice while on Roster" and any other path that
            // calls SetStarterMode(true) without going through ApplyScreen.
            if (isStarterMode)
                PersistentUIManager.Instance?.ShowTopBarOnly();

            if (_starterInstructionBar != null)
            {
                _starterInstructionBar.SetActive(isStarterMode);
                // D2 fix (2026-08-24): LocalizedText component on InstructionText handles the text.
                // It subscribes to OnLanguageChanged and refreshes on SetActive→OnEnable, so no
                // imperative Get() call is needed here (and it would race with domain-reload nulling _textMap).
            }

            // Tell carousel to show all catalog chars (so unowned show as locked)
            var carouselCtrl = GetComponentInChildren<CarouselController>(includeInactive: true);
            if (carouselCtrl != null)
                carouselCtrl.SetStarterMode(isStarterMode);

            // Tell detail panel to switch its Select button behavior
            var detailPanel = GetComponentInChildren<CharacterDetailPanel>(includeInactive: true);
            if (detailPanel != null)
                detailPanel.SetStarterMode(isStarterMode, isStarterMode ? this : null);

            // Hide confirm modal when entering starter mode (may be leftover)
            if (_starterConfirmModal != null)
                _starterConfirmModal.SetActive(false);

            // starter_restore_gate §5 — while the picker is up, a late server answer must be able
            // to close it.
            if (isStarterMode) StartWatchingForLateStarter();
            else StopWatchingForLateStarter();
        }

        /// <summary>Called by SelectButton in starter mode — opens the confirm modal.</summary>
        public void OnStarterSelectPressed(string characterId)
        {
            if (!_isStarterMode) return;
            currentCharacterId = characterId;

            if (_starterConfirmModal != null)
            {
                _starterConfirmModal.SetActive(true);
                var ctrl = _starterConfirmModal.GetComponentInChildren<StartingCharacterConfirmModalController>(includeInactive: true);
                if (ctrl != null)
                    ctrl.Open(characterId);
            }
        }
    }
}
