// ─────────────────────────────────────────────────────────────────────────────
// TournamentSignupModalController
// Signup / registration modal for Open/Ending tournaments (Figma 13480:2479).
// Extends ModalController; mirrors MatchmakingModalController for show/hide +
// prior-panel active-state capture so closing never resurrects a stale panel.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Roster;
using Golfin.Tournaments;
using Golfin.UI.Modals;
using GolfinRedux.UI;
using Golfin.UI.Toast;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Controller for the TournamentSignupModal prefab (Figma 13480:2479).
    /// Call Open(tournamentId) to show; CONFIRM → Register + navigate to HoleSelection.
    /// </summary>
    public class TournamentSignupModalController : ModalController
    {
        // ── Header content ────────────────────────────────────────────────────
        [Header("Header Text")]
        [SerializeField] private TextMeshProUGUI _sponsorText   = null!;
        [SerializeField] private TextMeshProUGUI _titleText     = null!;
        [SerializeField] private TextMeshProUGUI _venueText     = null!;
        /// <summary>
        /// Single combined line: "MMM d – MMM d — Ends in Xd Xh" (Figma 13480:2579+2580+2582)
        /// </summary>
        [SerializeField] private TextMeshProUGUI _dateLineText  = null!;

        // ── Entry pill ────────────────────────────────────────────────────────
        [Header("Entry Pill (13480:2618)")]
        [SerializeField] private TextMeshProUGUI _entryLabelText  = null!;   // "ENTRY" (13480:2620)
        [SerializeField] private Image           _entryCoinIcon   = null!;   // 30×30 RP coin (13480:2621)
        [SerializeField] private TextMeshProUGUI _entryAmountText = null!;   // fee number (13480:2622)

        // ── Reward row ────────────────────────────────────────────────────────
        [Header("Reward (13480:2624+2625)")]
        [SerializeField] private Image           _rewardCoinIcon = null!;   // 40×40 RP coin (13480:2624)
        [SerializeField] private TextMeshProUGUI _rewardText     = null!;   // "{prize:N0} + Trophy" (13480:2625)

        // ── Action Buttons ────────────────────────────────────────────────────
        [Header("Buttons")]
        [SerializeField] private Button _cancelButton  = null!;
        [SerializeField] private Button _confirmButton = null!;

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private ScreenId _holeSelectionTarget = ScreenId.TournamentHoleSelection;

        // ── Prior active-state capture (MatchmakingModal pattern) ─────────────
        [Header("Panels to hide while open (optional)")]
        [SerializeField] private List<GameObject> _panelsToHide = new List<GameObject>();

        // ── Runtime state ─────────────────────────────────────────────────────
        private string _tournamentId = string.Empty;
        private readonly List<bool> _panelWasActive = new List<bool>();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            if (_cancelButton  != null) _cancelButton.onClick.AddListener(OnCancel);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirm);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Populate the modal from the tournament definition and show it.
        /// </summary>
        public void Open(string tournamentId)
        {
            if (string.IsNullOrEmpty(tournamentId)) return;

            _tournamentId = tournamentId;

            if (TournamentService.Instance == null)
            {
                Debug.LogWarning("[TournamentSignupModal] TournamentService not ready; cannot open modal.");
                return;
            }

            var def = TournamentService.Instance.Backend.GetTournament(tournamentId);
            if (def == null)
            {
                Debug.LogWarning($"[TournamentSignupModal] Unknown tournament id={tournamentId}");
                return;
            }

            Populate(def);
            Show();
        }

        // ── ModalController overrides ─────────────────────────────────────────

        protected override void OnShow()
        {
            // Capture + hide panels (MatchmakingModal pattern)
            _panelWasActive.Clear();
            foreach (var panel in _panelsToHide)
            {
                _panelWasActive.Add(panel != null && panel.activeSelf);
                if (panel != null) panel.SetActive(false);
            }
        }

        protected override void OnHide()
        {
            // Restore panels
            for (int i = 0; i < _panelsToHide.Count && i < _panelWasActive.Count; i++)
            {
                if (_panelsToHide[i] != null)
                    _panelsToHide[i].SetActive(_panelWasActive[i]);
            }
        }

        protected override void OnDisable()
        {
            // Call base FIRST so the OpenModalCount leak guard fires (S2)
            base.OnDisable();
            // Safety restore if disabled before Hide() completes
            for (int i = 0; i < _panelsToHide.Count && i < _panelWasActive.Count; i++)
            {
                if (_panelsToHide[i] != null)
                    _panelsToHide[i].SetActive(_panelWasActive[i]);
            }
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void OnCancel()
        {
            Hide();
            // No Register call; no RP change; no navigation.
        }

        private void OnConfirm()
        {
            if (TournamentService.Instance == null)
            {
                ShowToast("Tournament service unavailable.");
                return;
            }

            var def = TournamentService.Instance.Backend.GetTournament(_tournamentId);
            if (def == null)
            {
                ShowToast("Tournament not found.");
                return;
            }

            // Pre-check RP before calling Register (guard matches mode_select_system pattern)
            long fee = def.EntryFeeRP;
            if (fee > 0 && RewardPointsManager.Instance != null)
            {
                if (RewardPointsManager.Instance.GetPoints() < (int)fee)
                {
                    ShowToast("Not enough RP");
                    Debug.Log($"[TournamentSignupModal] Insufficient RP — need {fee}, have {RewardPointsManager.Instance.GetPoints()}");
                    return;
                }
            }

            string charId = CharacterManager.Instance != null
                ? CharacterManager.Instance.GetSelectedCharacterId()
                : string.Empty;

            if (string.IsNullOrEmpty(charId))
            {
                ShowToast("No character selected.");
                return;
            }

            EntryState entry;
            try
            {
                entry = TournamentService.Instance.Backend.Register(_tournamentId, fee, charId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TournamentSignupModal] Register failed: {ex.Message}");
                ShowToast("Registration failed.");
                return;
            }

            if (entry == null)
            {
                ShowToast("Registration failed.");
                return;
            }

            Debug.Log($"[TournamentSignupModal] Registered tournament={_tournamentId} char={charId} entryFee={fee}RP");

            Hide();
            ScreenManager.Instance?.ShowScreen(_holeSelectionTarget);
        }

        // ── Data population ───────────────────────────────────────────────────

        private void Populate(TournamentDefinition def)
        {
            // Sponsor: "{SPONSOR} PRESENTS"
            string sponsor = string.IsNullOrEmpty(def.SponsorKey)
                ? "GOLFIN PRESENTS"
                : def.SponsorKey.ToUpperInvariant() + " PRESENTS";
            if (_sponsorText != null) _sponsorText.text = sponsor;

            // Title: localised tournament name
            if (_titleText != null)
                _titleText.text = LocalizationManager.Get(def.NameKey);

            // Venue: "{club name}  -  {N} Holes" (single occurrence of Holes count)
            // Guard: if the localization string already embeds "Holes" (e.g. "Kasumigaseki CC · 18 Holes"),
            // do NOT append another count to avoid "Kasumigaseki CC · 18 Holes  -  18 Holes".
            string venueLocKey = "tourn.venue." + def.ClubId;
            string venueName   = LocalizationManager.Get(venueLocKey);
            if (string.IsNullOrEmpty(venueName) || venueName == venueLocKey)
                venueName = def.ClubId;
            if (_venueText != null)
            {
                bool alreadyHasHoles = venueName.IndexOf("Holes", StringComparison.OrdinalIgnoreCase) >= 0;
                _venueText.text = alreadyHasHoles
                    ? venueName
                    : $"{venueName}  -  {def.HoleSet.Count} Holes";
            }

            // Date line (combined): "MMM d – MMM d — Ends in Xd Xh" (Figma 13480:2579+2580+2582)
            if (_dateLineText != null)
            {
                string dateRange = def.StartUtc.ToString("MMM d") + " – " + def.EndUtc.ToString("MMM d");
                TimeSpan diff = def.EndUtc - DateTime.UtcNow;
                string countdown = diff.TotalDays >= 1
                    ? $"Ends in {(int)diff.TotalDays}d {diff.Hours:D2}h"
                    : $"Ends in {(int)diff.TotalHours:D2}h {diff.Minutes:D2}m";
                _dateLineText.text = $"{dateRange} — {countdown}";
            }

            // Entry pill
            if (_entryLabelText  != null) _entryLabelText.text  = "ENTRY";
            if (_entryAmountText != null) _entryAmountText.text = def.EntryFeeRP.ToString("N0");
            // Coin icon visibility
            if (_entryCoinIcon   != null) _entryCoinIcon.enabled = true;

            // Reward
            if (_rewardCoinIcon  != null) _rewardCoinIcon.enabled = true;
            if (_rewardText != null && TournamentService.Instance != null)
            {
                long topPrize = TournamentService.Instance.GetTopPrizeRP(def.Id);
                _rewardText.text = $"{topPrize:N0} + Trophy";
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void ShowToast(string message)
        {
            if (ToastController.Instance != null)
                ToastController.Instance.Show(message, 2f);
            else
                Debug.LogWarning($"[TournamentSignupModal] Toast: {message}");
        }
    }
}
