// ─────────────────────────────────────────────────────────────────────────────
// TournamentResultPresenter
// Auto-present orchestrator for the Prize / Claim modal.
// Sits on the persistent UI object; wires to ScreenManager.ScreenChanged (S1)
// and ModalController.ModalStackEmptied (S2). Also runs a 30-second safety
// tick so tournaments that resolve while the player is idle on Home still
// surface the modal.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Tournaments;
using Golfin.UI.Modals;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Singleton orchestrator.  Place on the persistent UI canvas next to the
    /// TournamentResultModal prefab instance.  Assign _resultModal in the Inspector.
    /// </summary>
    public class TournamentResultPresenter : MonoBehaviour
    {
        public static TournamentResultPresenter? Instance { get; private set; }

        [Header("Modal reference")]
        [SerializeField] private TournamentResultModalController _resultModal = null!;

        [Header("Timing")]
        [Tooltip("Delay after modals clear before presenting, in real seconds. Default 1.0.")]
        [SerializeField] private float _settleDelay = 1.0f;

        [Tooltip("Safety tick interval (seconds). Fires TryPresent while player idles. Default 30.")]
        [SerializeField] private float _safetyTickInterval = 30.0f;

        // Session guard: ids claimed this session (belt-and-suspenders against stale memo).
        private readonly HashSet<string> _claimedThisSession = new HashSet<string>();

        // True while the Prize modal is shown or coroutine is in-flight.
        private bool _presenting = false;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            // Wire infra seams
            ScreenManager.ScreenChanged += OnScreenChanged;
            ModalController.ModalStackEmptied += OnModalsCleared;

            // Subscribe to the modal's claim event so we can update the session guard
            if (_resultModal != null)
                _resultModal.OnClaimed += OnClaimConfirmed;

            // Safety tick: catch tournaments that resolve while player idles
            InvokeRepeating(nameof(SafetyTick), _safetyTickInterval, _safetyTickInterval);
        }

        private void OnDisable()
        {
            ScreenManager.ScreenChanged -= OnScreenChanged;
            ModalController.ModalStackEmptied -= OnModalsCleared;

            if (_resultModal != null)
                _resultModal.OnClaimed -= OnClaimConfirmed;

            CancelInvoke(nameof(SafetyTick));
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnScreenChanged(ScreenId screenId)
        {
            if (IsEligibleScreen(screenId))
                TryPresent();
        }

        private void OnModalsCleared()
        {
            // The Prize modal's own close fires this — lets us chain to the next unclaimed.
            _presenting = false;
            TryPresent();
        }

        private void SafetyTick() => TryPresent();

        private void OnClaimConfirmed(string tournamentId)
        {
            _claimedThisSession.Add(tournamentId);
        }

        // ── Core logic ────────────────────────────────────────────────────────

        /// <summary>
        /// Eligible screen test (O-1 — CONFIRMED by Cesar 2026-06-29).
        /// </summary>
        private static bool IsEligibleScreen(ScreenId s)
            => s == ScreenId.Home
            || s == ScreenId.TournamentSelection
            || s == ScreenId.TournamentHoleSelection
            || s == ScreenId.TournamentLeaderboard;

        /// <summary>
        /// Finds the oldest-EndUtc unclaimed presentable tournament, oldest→newest (O-6).
        /// </summary>
        private bool TryFindPresentable(out string? id)
        {
            id = null;
            if (TournamentService.Instance == null || TournamentService.Instance.Backend == null)
                return false;

            var backend = TournamentService.Instance.Backend;
            string? best = null;
            System.DateTime bestEnd = System.DateTime.MaxValue;

            foreach (var def in backend.GetTournaments())
            {
                if (_claimedThisSession.Contains(def.Id)) continue;
                if (backend.GetMyEntry(def.Id) == null) continue;
                var r = backend.GetResults(def.Id);
                if (r == null || r.Claimed) continue;
                if (def.EndUtc < bestEnd)
                {
                    best = def.Id;
                    bestEnd = def.EndUtc;
                }
            }
            id = best;
            return best != null;
        }

        private void TryPresent()
        {
            if (_presenting) return;
            if (ScreenManager.Instance == null) return;
            if (!IsEligibleScreen(ScreenManager.Instance.CurrentScreen)) return;
            if (!TryFindPresentable(out var id) || id == null) return;
            if (ModalController.OpenModalCount > 0) return; // another modal up — wait for ModalStackEmptied

            StartCoroutine(PresentAfterDelay(id));
        }

        private IEnumerator PresentAfterDelay(string id)
        {
            // Settle delay (unscaled — honors "wait a second after modals close").
            yield return new WaitForSecondsRealtime(_settleDelay);

            // RE-VALIDATE: player may have navigated / a modal popped / claimed elsewhere
            if (ScreenManager.Instance == null) yield break;
            if (!IsEligibleScreen(ScreenManager.Instance.CurrentScreen)) yield break;
            if (ModalController.OpenModalCount > 0) yield break; // modal popped during wait → ModalStackEmptied will retry
            if (!TryFindPresentable(out var stillId) || stillId != id) yield break;

            _presenting = true;

            if (_resultModal != null)
                _resultModal.Open(id);
            else
                Debug.LogError("[TournamentResultPresenter] _resultModal is null — cannot open Prize modal.");
        }

        // ── Public API (used by TournamentResultModalController callbacks) ─────

        /// <summary>
        /// Called by the modal on claim so the presenter won't re-present the same prize.
        /// This mirrors the OnClaimed event subscription but can be called directly too.
        /// </summary>
        public void NotifyClaimed(string tournamentId)
        {
            _claimedThisSession.Add(tournamentId);
        }
    }
}
