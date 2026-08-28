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

        // ── Mode entry (game_modes_admin §4) ─────────────────────────────────────────────
        //
        // The fee copy deliberately RHYMES with CostUpdatedMessage above and with the shop's
        // "Price updated: N RP": three surfaces, one idea — the number moved, here is the real one,
        // tap again. Three different phrasings for the same event is how a player learns to read
        // one of them as an error.

        /// <summary>Shown when the published entry fee is not the one the card displayed. The card
        /// re-renders at the server's number and the SECOND tap pays it — so this is an INVITATION
        /// to tap again, not a failure. Formatted with the fee.</summary>
        public const string FeeUpdatedFormat = "Entry fee updated: {0} RP";

        /// <summary>Shown when the mode itself is gone or shut server-side (unknown_mode /
        /// mode_locked). Unlike the fee case the player cannot answer by tapping again; the card
        /// refreshes and stops offering entry.</summary>
        public const string ModeUnavailableMessage = "This mode is not available";

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
        /// True while a server-side spend is awaiting its answer, i.e. while the latch above would
        /// SWALLOW a further <see cref="Spend"/> — neither callback fires and the caller is never told.
        ///
        /// <para>
        /// Read-only and purely advisory; it changes nothing about <see cref="Spend"/>. It exists so a
        /// call site can decline to put a button into the pending state for a spend that is about to be
        /// dropped on the floor: a pending affordance is restored by the callback, so beginning one for
        /// a swallowed spend would leave that button disabled forever (transaction_feedback §3.1).
        /// Checking it is race-free — the check and <see cref="Spend"/>'s own run back-to-back on the
        /// same stack frame.
        /// </para>
        /// </summary>
        public static bool IsSpendInFlight => _inFlight;

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

                // The refusal copy, by verdict. `default` is OfflineMessage and that is the right
                // default: an outcome this switch does not recognise is one the server produced and
                // this build does not understand, which is indistinguishable from not reaching it.
                string message;
                switch (outcome != null ? outcome.Verdict : SpendVerdict.Unavailable)
                {
                    case SpendVerdict.Insufficient:
                        message = InsufficientMessage;
                        break;
                    case SpendVerdict.FeeChanged:
                        message = string.Format(FeeUpdatedFormat, outcome.ServerFee);
                        break;
                    case SpendVerdict.UnknownMode:
                    case SpendVerdict.ModeLocked:
                        message = ModeUnavailableMessage;
                        break;
                    default:
                        message = OfflineMessage;
                        break;
                }

                Debug.LogWarning($"[PointsSpendGate] Spend of {amount} RP ({reason}) denied: " +
                                 $"{(outcome != null ? outcome.ToString() : "no outcome")} — action not performed.");

                if (ToastController.Instance != null) ToastController.Instance.Show(message, ToastSeconds);
                onDenied?.Invoke(outcome ?? SpendOutcome.Unavailable(null));
            });
        }
    }
}
