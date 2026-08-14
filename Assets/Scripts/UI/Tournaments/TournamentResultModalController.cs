// ─────────────────────────────────────────────────────────────────────────────
// TournamentResultModalController
// Prize / Claim modal for resolved tournaments (Figma 13498:2067).
// Clone of TournamentSignupModal prefab; extends ModalController.
// Call Open(tournamentId) to populate from the live result and show.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Tournaments;
using Golfin.UI.Modals;
using GolfinRedux.UI;
using Golfin.UI.Toast;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Controller for the TournamentResultModal prefab (Figma 13498:2067).
    /// Call Open(tournamentId) to show; CLAIM is the sole exit — no dismiss/close path.
    /// </summary>
    public class TournamentResultModalController : ModalController
    {
        // ── Header text ───────────────────────────────────────────────────────
        [Header("Header Text")]
        [SerializeField] private TextMeshProUGUI _sponsorText   = null!;
        [SerializeField] private TextMeshProUGUI _titleText     = null!;
        [SerializeField] private TextMeshProUGUI _venueText     = null!;
        [SerializeField] private TextMeshProUGUI _dateLineText  = null!;

        // ── Rank ──────────────────────────────────────────────────────────────
        [Header("Rank")]
        [SerializeField] private TextMeshProUGUI _rankText = null!;

        // ── Reward ────────────────────────────────────────────────────────────
        [Header("Reward")]
        [SerializeField] private Image           _rewardCoinIcon = null!;
        [SerializeField] private TextMeshProUGUI _rewardText     = null!;

        // ── Claim button ──────────────────────────────────────────────────────
        [Header("Buttons")]
        [SerializeField] private Button _claimButton = null!;

        // ── Optional panels to hide while open ───────────────────────────────
        [Header("Panels to hide while open (optional)")]
        [SerializeField] private List<GameObject> _panelsToHide = new List<GameObject>();

        // ── Runtime state ─────────────────────────────────────────────────────
        private string _tournamentId = string.Empty;
        private readonly List<bool> _panelWasActive = new List<bool>();

        /// <summary>
        /// Fired after a successful claim so the presenter can update its session guard.
        /// </summary>
        public event Action<string>? OnClaimed;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            if (_claimButton != null)
                _claimButton.onClick.AddListener(OnClaim);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Populate the modal from the resolved tournament result and show it.
        /// </summary>
        public void Open(string tournamentId)
        {
            if (string.IsNullOrEmpty(tournamentId)) return;

            if (TournamentService.Instance == null)
            {
                Debug.LogWarning("[TournamentResultModal] TournamentService not ready; cannot open modal.");
                return;
            }

            var def = TournamentService.Instance.Backend.GetTournament(tournamentId);
            if (def == null)
            {
                Debug.LogWarning($"[TournamentResultModal] Unknown tournament id={tournamentId}");
                return;
            }

            var result = TournamentService.Instance.Backend.GetResults(tournamentId);
            if (result == null)
            {
                Debug.LogWarning($"[TournamentResultModal] No result yet for id={tournamentId}; cannot show Prize modal.");
                return;
            }

            _tournamentId = tournamentId;
            Populate(def, result);
            Show();
        }

        // ── ModalController overrides ─────────────────────────────────────────

        protected override void OnShow()
        {
            // Capture + hide panels (matches TournamentSignupModal pattern)
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

        // ── Button handler ────────────────────────────────────────────────────

        private void OnClaim()
        {
            if (TournamentService.Instance == null)
            {
                ShowToast("Tournament service unavailable.");
                return;
            }

            if (string.IsNullOrEmpty(_tournamentId)) return;

            try
            {
                TournamentService.Instance.Backend.ClaimPrize(_tournamentId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TournamentResultModal] ClaimPrize failed: {ex.Message}");
                ShowToast("Claim failed.");
                return;
            }

            Debug.Log($"[TournamentResultModal] Claimed tournament={_tournamentId}");
            OnClaimed?.Invoke(_tournamentId);
            Hide();
        }

        // ── Data population ───────────────────────────────────────────────────

        private void Populate(TournamentDefinition def, TournamentResult result)
        {
            // Sponsor: "{SPONSOR} PRESENTS"
            string sponsor = string.IsNullOrEmpty(def.SponsorKey)
                ? LocalizationManager.Get("TOURN_GOLFIN_PRESENTS")
                : def.SponsorKey.ToUpperInvariant() + " PRESENTS";
            if (_sponsorText != null) _sponsorText.text = sponsor;

            // Title: localize(NameKey) → Title → Id. Same ladder as the selection card — a
            // dashboard-created tournament has no localization key in this build.
            if (_titleText != null)
                _titleText.text = TournamentDisplayName.Resolve(def);

            // Venue: "{club name}  -  {N} Holes" — mirrors Signup "already-has-Holes" guard
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

            // Date line: "MMM d – MMM d — Finished"  (node shows "Finished", no countdown)
            if (_dateLineText != null)
            {
                string dateRange = def.StartUtc.ToString("MMM d") + " – " + def.EndUtc.ToString("MMM d");
                _dateLineText.text = $"{dateRange} — Finished";
            }

            // Rank: "RANK #N" (O-3 default: plain, ignoring IsTie in v1)
            if (_rankText != null)
                _rankText.text = $"RANK #{result.FinalRank}";

            // Reward: green #73e080 — bound to actual prize
            if (_rewardCoinIcon != null)
                _rewardCoinIcon.enabled = true;
            if (_rewardText != null)
            {
                _rewardText.text = !string.IsNullOrEmpty(result.ItemRewardId)
                    ? $"{result.PrizeRP:N0} + Trophy"
                    : $"{result.PrizeRP:N0}";
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void ShowToast(string message)
        {
            if (ToastController.Instance != null)
                ToastController.Instance.Show(message, 2f);
            else
                Debug.LogWarning($"[TournamentResultModal] Toast: {message}");
        }
    }
}
