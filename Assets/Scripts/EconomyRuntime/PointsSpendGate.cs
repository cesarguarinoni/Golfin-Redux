// ─────────────────────────────────────────────────────────────────────────────
// reward_points_backend Slice 2 — the one door every RP spend goes through.
// Lives in Assembly-CSharp (not the headless Golfin.Economy asmdef) because it
// needs ToastController; same split as TournamentsRuntime vs Golfin.Tournaments.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using Golfin.Economy;
using Golfin.UI.Toast;
using UnityEngine;

namespace Golfin.EconomyRuntime
{
    /// <summary>
    /// Server-debit-then-act, for the four RP spend flows (SPEC §4 Slice 2: character level-up, club
    /// level-up, tournament sign-up, mode entry fee).
    ///
    /// THE ORDERING IS THE POINT. The server debit must land BEFORE the thing the player is buying
    /// happens locally, so a refused or unreachable debit cannot leave them holding a level-up the
    /// ledger never paid for. Every call site therefore wraps its existing body in
    /// <paramref name="onApproved"/> rather than calling <c>SpendPoints</c> and hoping.
    ///
    /// FLAG-OFF IS SYNCHRONOUS AND IDENTICAL TO HEAD. With <c>PointsBackendEnabled</c> off this
    /// short-circuits before <see cref="PointsService"/> is ever touched — no HTTP, no coroutine
    /// runner GameObject, and <paramref name="onApproved"/> runs on the caller's own stack frame, so
    /// modal timing does not shift. The same is true for a zero-cost action.
    ///
    /// The LOCAL debit is unchanged and still lives at the call site (<c>RewardPointsManager.SpendPoints</c>,
    /// <c>LocalTournamentBackend.Register</c>). This gate only adds the server half in front of it.
    /// </summary>
    public static class PointsSpendGate
    {
        /// <summary>Shown when the debit could not be attempted or answered. Spends are online-only
        /// by design (decision of record #2) — there is no offline queue to fall back on.</summary>
        public const string OfflineMessage = "Connection required";

        /// <summary>Shown when the server says the balance is short.</summary>
        public const string InsufficientMessage = "Not enough Reward Points";

        // The two below belong to LEVEL-UPS, which since progress_server_side (§4) no longer go
        // through this gate at all — they go through Golfin.Economy.ProgressService. They live HERE
        // anyway, next to the two above, because the four strings are the complete set of "the server
        // refused your spend" copy and splitting them across two files is how two of them end up
        // saying different things. Plain English rather than a localisation key, matching its two
        // neighbours; the whole set moves together in the localisation pass.

        /// <summary>Shown when the published cost of a level-up run is not the one the modal
        /// previewed. The modal re-prices at the server's number and the player answers again — so
        /// this is an INVITATION to tap CONFIRM once more, not a failure.</summary>
        public const string CostUpdatedMessage = "Level-up cost updated — confirm again";

        /// <summary>Shown when the server's recorded level for a character or club is not the one this
        /// client believes in. Unlike the other three the player cannot answer this by trying again;
        /// the modal closes and the next inventory sync reconciles.</summary>
        public const string LevelConflictMessage = "Your progress is out of date — reopening";

        private const float ToastSeconds = 2f;

        /// <summary>
        /// One spend at a time, process-wide.
        ///
        /// With the flag ON a spend is a round-trip, which opens a window the sync API never had: a
        /// double-tapped CONFIRM would fire two debits with two different idempotency keys, and the
        /// server would honour both. The four call sites are all modal flows that cannot legitimately
        /// overlap, so a single in-flight latch is both sufficient and the cheapest place to fix it.
        /// Flag-OFF spends complete inside <see cref="Spend"/>, so the latch is never observed set.
        /// </summary>
        private static bool _inFlight;

        /// <summary>
        /// Debit <paramref name="amount"/> RP server-side, then run <paramref name="onApproved"/>.
        ///
        /// On a refusal the player is toasted and <paramref name="onApproved"/> NEVER runs.
        /// <paramref name="onDenied"/> is for call-site cleanup (clearing a busy state, closing a
        /// modal) — the toast is already handled here so the copy stays consistent.
        /// </summary>
        public static void Spend(int amount, string reason, Action onApproved, Action<SpendOutcome>? onDenied = null)
        {
            if (onApproved == null)
            {
                Debug.LogError("[PointsSpendGate] Spend called with no onApproved action — ignored.");
                return;
            }

            // Flag OFF, or nothing to pay: run inline, synchronously, exactly as before this slice.
            if (!PointsBackendFlag.Enabled || amount <= 0)
            {
                onApproved();
                return;
            }

            if (_inFlight)
            {
                Debug.LogWarning($"[PointsSpendGate] Spend of {amount} RP ({reason}) ignored — " +
                                 "another spend is still awaiting the server.");
                return;
            }

            _inFlight = true;
            PointsService.Instance.SpendAsync(amount, reason, outcome =>
            {
                _inFlight = false;

                if (outcome != null && outcome.MayProceed)
                {
                    onApproved();
                    return;
                }

                string message = outcome != null && outcome.Verdict == SpendVerdict.Insufficient
                    ? InsufficientMessage
                    : OfflineMessage;

                Debug.LogWarning($"[PointsSpendGate] Spend of {amount} RP ({reason}) denied: " +
                                 $"{(outcome != null ? outcome.ToString() : "no outcome")} — action not performed.");

                if (ToastController.Instance != null) ToastController.Instance.Show(message, ToastSeconds);
                onDenied?.Invoke(outcome ?? SpendOutcome.Unavailable(null));
            });
        }
    }
}
