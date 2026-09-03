// ─────────────────────────────────────────────────────────────────────────────
// gps_checkin §C4 — "ROUND COMPLETE" (Figma 14078:34155).
//
// TWO STATES, ONE MODAL, and the order matters. It opens as a CONFIRMATION
// ("check out of this round?") and becomes a RECEIPT once the server answers,
// showing the elapsed, the points and the fix count the server actually
// recorded — never the client's own guesses. A separate "are you sure" modal
// before this one would be two taps for one decision.
//
// THE RECEIPT'S NUMBERS ARE THE SERVER'S. `duration` and `awarded` come off the
// check-out RPC's return value, which is the same transaction that moved the
// points. The client's elapsed timer and the server's differ by whatever the
// request took, and showing the client's would mean the receipt disagrees with
// the ledger.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Globalization;
using Golfin.UI.Modals;
using Golfin.UI.Polish;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>Confirms a check-out, then shows what it paid.</summary>
    [DisallowMultipleComponent]
    public sealed class RoundCompleteModalController : ModalController
    {
        private const string Tag = "[CheckOut]";

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI? _title;
        [SerializeField] private TextMeshProUGUI? _sub;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI? _statElapsedValue;
        [SerializeField] private TextMeshProUGUI? _statPtsValue;
        [SerializeField] private TextMeshProUGUI? _statFixesValue;

        [Header("Note")]
        [SerializeField] private TextMeshProUGUI? _note;

        [Header("Actions")]
        [Tooltip("CHECK OUT before the call; POST SCORE after it.")]
        [SerializeField] private Button? _primaryButton;
        [SerializeField] private TextMeshProUGUI? _primaryLabel;

        [Tooltip("CANCEL before the call; DONE after it.")]
        [SerializeField] private Button? _secondaryButton;
        [SerializeField] private TextMeshProUGUI? _secondaryLabel;

        private Action? _onConfirm;
        private Action? _onPostScore;
        private PendingSpend? _pending;
        private bool _wired;

        /// <summary>Whether the server has answered yet — which decides what the two buttons do.</summary>
        public bool IsReceipt { get; private set; }

        /// <summary>
        /// Open as a CONFIRMATION for the round currently open.
        ///
        /// <para>The stats show the CLIENT's running numbers at this point, which is correct: they
        /// are what the player has been watching tick on the card, and they are replaced by the
        /// server's the moment it answers.</para>
        /// </summary>
        public void OpenConfirm(string venueSince, TimeSpan elapsed, int fixes, bool expired,
                                Action onConfirm, Action onPostScore)
        {
            WireOnce();
            IsReceipt = false;
            _onConfirm = onConfirm;
            _onPostScore = onPostScore;
            _pending?.Dispose();
            _pending = null;

            if (_title != null)
                _title.text = LocalizationManager.Get(expired ? "GPS_ROUNDS_EXPIRED"
                                                              : "GPS_ROUNDS_COMPLETE_TITLE");
            if (_sub != null) _sub.text = venueSince;

            SetStats(RoundSession.FormatElapsed(elapsed), null, fixes);

            if (_note != null)
                _note.text = LocalizationManager.Get(expired ? "GPS_ROUNDS_EXPIRED_NOTE"
                                                             : "GPS_ROUNDS_COMPLETE_NOTE");

            SetButtons(LocalizationManager.Get("GPS_ROUNDS_CHECK_OUT"),
                       LocalizationManager.Get("GPS_ROUNDS_CANCEL"));
            Show();
        }

        /// <summary>Put CHECK OUT into its pending state (§ motion: PendingSpend on CHECK OUT).</summary>
        public void BeginPending()
        {
            _pending?.Dispose();
            _pending = PendingSpend.Begin(_primaryButton, _primaryLabel, _secondaryButton!);
        }

        /// <summary>
        /// Become the RECEIPT: the server's elapsed, points and fix count, and the two buttons
        /// swap to POST SCORE / DONE.
        /// </summary>
        public void ShowReceipt(CheckOutResult result, TimeSpan clientElapsed, int fixes,
                                string subLine)
        {
            _pending?.Dispose();
            _pending = null;
            IsReceipt = true;

            bool expired = result != null && result.Expired;

            if (_title != null)
                _title.text = LocalizationManager.Get(expired ? "GPS_ROUNDS_EXPIRED"
                                                              : "GPS_ROUNDS_COMPLETE_TITLE");
            if (_sub != null) _sub.text = subLine;

            // The server's own "1h 24m" is reformatted to the frame's "1:24" rather than trusted
            // as a display string: it is a data format ("%dh %dm"), not a designed one.
            string elapsed = result?.ElapsedSeconds != null
                ? RoundSession.FormatElapsed(TimeSpan.FromSeconds(result.ElapsedSeconds.Value))
                : RoundSession.FormatElapsed(clientElapsed);

            int serverFixes = result?.Activity?.GpsCheckCount ?? fixes;
            SetStats(elapsed, result?.Awarded ?? 0, serverFixes);

            if (_note != null)
                _note.text = LocalizationManager.Get(expired ? "GPS_ROUNDS_EXPIRED_NOTE"
                                                             : "GPS_ROUNDS_COMPLETE_NOTE");

            SetButtons(LocalizationManager.Get("GPS_ROUNDS_POST_SCORE"),
                       LocalizationManager.Get("GPS_ROUNDS_DONE"));
        }

        /// <summary>Release the pending state without becoming the receipt — the failure path.</summary>
        public void FailPending()
        {
            _pending?.Dispose();
            _pending = null;
        }

        /// <summary>
        /// <paramref name="awarded"/> null leaves the PTS EARNED value at its placeholder: before
        /// the server answers nobody knows what the round pays, and showing "+15" next to a button
        /// that has not been pressed would promise a number the 8 h rule can take away.
        /// </summary>
        private void SetStats(string elapsed, int? awarded, int fixes)
        {
            if (_statElapsedValue != null) _statElapsedValue.text = elapsed;
            if (_statPtsValue != null)
                _statPtsValue.text = awarded.HasValue
                    ? "+" + awarded.Value.ToString(CultureInfo.InvariantCulture)
                    : "—";
            if (_statFixesValue != null)
                _statFixesValue.text = fixes.ToString(CultureInfo.InvariantCulture);
        }

        private void SetButtons(string primary, string secondary)
        {
            if (_primaryLabel != null) _primaryLabel.text = primary;
            if (_secondaryLabel != null) _secondaryLabel.text = secondary;
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            if (_primaryButton != null)
                _primaryButton.onClick.AddListener(() =>
                {
                    if (IsReceipt)
                    {
                        Debug.Log($"{Tag} POST SCORE from the receipt");
                        Hide();
                        _onPostScore?.Invoke();
                    }
                    else
                    {
                        Debug.Log($"{Tag} CHECK OUT confirmed");
                        _onConfirm?.Invoke();
                    }
                });

            if (_secondaryButton != null)
                _secondaryButton.onClick.AddListener(() =>
                {
                    Debug.Log($"{Tag} {(IsReceipt ? "DONE" : "cancelled")}");
                    Hide();
                });
        }

        /// <summary>"08:12 – 09:36 · GPS verified" — the receipt's sub-line.</summary>
        public static string ReceiptSub(DateTimeOffset? start, DateTimeOffset? end, bool verified)
            => string.Format(
                LocalizationManager.Get(verified ? "GPS_ROUNDS_COMPLETE_SUB"
                                                 : "GPS_ROUNDS_COMPLETE_SUB_UNVERIFIED"),
                RoundSession.FormatClock(start), RoundSession.FormatClock(end));
    }
}
