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
using Golfin.Economy;
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

            // TryGetTournament, not Backend.GetTournament: the latter THROWS KeyNotFoundException for
            // an unknown id, which made this null-check dead code and an unknown id an uncaught
            // exception. See OnConfirm for why an unknown id is now an ordinary occurrence.
            var def = TournamentService.Instance.TryGetTournament(tournamentId);
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

            // The tournament can legitimately disappear BETWEEN Open() and CONFIRM: the schedule is
            // refetched on every entry to the selection screen, and an admin deactivating a
            // tournament simply removes it from the payload. The player is not entered yet, so
            // MergePreservingEntered does not carry it forward — correctly, it is gone.
            //
            // Backend.GetTournament would throw KeyNotFoundException here and dead-end the player in
            // an open modal with no feedback. Toast and close instead; the list behind is already
            // (or about to be) rebuilt without the row.
            var def = TournamentService.Instance.TryGetTournament(_tournamentId);
            if (def == null)
            {
                Debug.LogWarning(
                    $"[TournamentSignupModal] '{_tournamentId}' left the schedule while the modal was " +
                    "open (deactivated, or its window closed). Closing without registering.");
                ShowToast("Tournament no longer available.");
                Hide();
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

            // Slice 2: the entry fee is now paid BEFORE Register instead of inside it, so an
            // unreachable server cannot enter the player into a tournament the ledger never charged
            // for. Register is therefore called with a fee of 0 — the payment already happened.
            //
            // Register's own idempotence (already-registered → return the existing entry, no
            // re-charge) is preserved by this GetMyEntry short-circuit; without it, moving payment in
            // front of Register would charge a second time on a re-entry.
            if (TournamentService.Instance.Backend.GetMyEntry(_tournamentId) != null)
            {
                Debug.Log($"[TournamentSignupModal] Already registered for {_tournamentId} — no charge.");
                CompleteSignup(charId, alreadyPaid: 0L);
                return;
            }

            TournamentService.Instance.RewardPoints.TrySpendAsync(
                fee,
                SpendReasons.TournamentEntry,
                paid =>
                {
                    // The gate has already toasted the reason (insufficient vs connection required).
                    if (!paid)
                    {
                        Debug.Log($"[TournamentSignupModal] Entry fee of {fee}RP not paid — signup aborted.");
                        return;
                    }

                    CompleteSignup(charId, alreadyPaid: fee);
                });
        }

        /// <summary>
        /// Registers and navigates, with the entry fee already settled. Always passes 0 to
        /// <c>Register</c> — the debit is the caller's responsibility now (see <see cref="OnConfirm"/>).
        /// </summary>
        private void CompleteSignup(string charId, long alreadyPaid)
        {
            EntryState entry;
            try
            {
                entry = TournamentService.Instance.Backend.Register(_tournamentId, 0L, charId);
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

            Debug.Log($"[TournamentSignupModal] Registered tournament={_tournamentId} char={charId} entryFee={alreadyPaid}RP");

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

            // Title: localize(NameKey) → Title → Id. Same ladder as the selection card — a
            // dashboard-created tournament has no localization key in this build.
            if (_titleText != null)
                _titleText.text = TournamentDisplayName.Resolve(def);

            // Venue: the shared ladder. Was an inline copy that appended a hole count on top of a
            // localized string which already had one, guarded by sniffing the output for "Holes" —
            // blind to "ホール", so JP rendered the count twice. See TournamentVenueLine.
            if (_venueText != null)
                _venueText.text = TournamentVenueLine.Resolve(def);

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
