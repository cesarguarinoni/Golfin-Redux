// ─────────────────────────────────────────────────────────────────────────────
// gps_checkin §C4 — "CHECK IN HERE?" (Figma 14080:34292).
//
// WHY THERE IS A CONFIRM STEP AT ALL. A check-in opens a round the player then
// has to close, it is refused while another is open (D2), and it pays +30 — so
// tapping the wrong row costs a check-out and a re-check-in, not a back button.
// The modal is also where the three numbers that make the +30 honest are shown
// BEFORE it is earned: the distance, the accuracy, and what each half pays.
//
// EVERY NUMBER ON IT IS LIVE. The three stat values come from the row and the
// current fix, not from the node's mock — a modal that always says "2.4 km away
// · inside the course radius" over a spot 40 m away would be the most
// convincing wrong thing on the screen.
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
    /// <summary>Confirms a check-in at one venue. Owns no network call — the screen does the
    /// work, so the round's lifecycle lives in ONE place (<see cref="GpsRoundsScreenController"/>).</summary>
    [DisallowMultipleComponent]
    public sealed class CheckInConfirmModalController : ModalController
    {
        private const string Tag = "[CheckIn]";

        [Header("Venue")]
        [SerializeField] private TextMeshProUGUI? _venueName;
        [SerializeField] private TextMeshProUGUI? _venueSub;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI? _statCheckInValue;
        [SerializeField] private TextMeshProUGUI? _statCheckOutValue;
        [SerializeField] private TextMeshProUGUI? _statAccuracyValue;

        [Header("Actions")]
        [SerializeField] private Button? _confirmButton;
        [SerializeField] private TextMeshProUGUI? _confirmLabel;
        [SerializeField] private Button? _cancelButton;

        /// <summary>The venue this modal is currently asking about.</summary>
        public VenueDto? Venue { get; private set; }

        private Action<VenueDto>? _onConfirm;
        private PendingSpend? _pending;
        private bool _wired;

        /// <summary>
        /// Open the modal for one spot.
        ///
        /// <para><paramref name="onConfirm"/> fires on CHECK IN; the modal stays up with the
        /// button in its pending state until the caller calls <see cref="Finish"/>, because the
        /// request can fail and closing first would leave nowhere to say so.</para>
        /// </summary>
        public void Open(VenueDto venue, GpsQuality quality, Action<VenueDto> onConfirm)
        {
            WireOnce();
            Venue = venue;
            _onConfirm = onConfirm;

            _pending?.Dispose();
            _pending = null;

            if (_venueName != null) _venueName.text = venue?.Name ?? string.Empty;
            if (_venueSub != null) _venueSub.text = SubLine(venue);

            // The two payouts are the SERVER's numbers, stated as constants here only because the
            // player is being asked to agree to them before they happen. If the RPC's amounts ever
            // move, these strings are the second place to change — which is why they are localized
            // rows rather than inline text, and why the report's E2E quotes the real +30/+15.
            if (_statCheckInValue != null)
                _statCheckInValue.text = LocalizationManager.Get("GPS_ROUNDS_PTS_ON_CHECKIN_VALUE");
            if (_statCheckOutValue != null)
                _statCheckOutValue.text = LocalizationManager.Get("GPS_ROUNDS_PTS_ON_CHECKOUT_VALUE");
            if (_statAccuracyValue != null)
                _statAccuracyValue.text = QualityLabel(quality);

            Show();
        }

        /// <summary>Put the CHECK IN button into its pending state (§ motion: PendingSpend on
        /// CHECK IN). Called by the screen the moment the request leaves.</summary>
        public void BeginPending()
        {
            _pending?.Dispose();
            _pending = PendingSpend.Begin(_confirmButton, _confirmLabel, _cancelButton!);
        }

        /// <summary>Release the pending state and, on success, close. A failure leaves the modal
        /// up so the toast has something to sit over and the player can retry.</summary>
        public void Finish(bool close)
        {
            _pending?.Dispose();
            _pending = null;
            if (close) Hide();
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(() =>
                {
                    if (Venue == null) return;
                    Debug.Log($"{Tag} CHECK IN confirmed for #{Venue.Id} {Venue.Name}");
                    _onConfirm?.Invoke(Venue);
                });
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(() =>
                {
                    Debug.Log($"{Tag} cancelled");
                    Hide();
                });
        }

        /// <summary>
        /// "2.4 km away · inside the course radius", or the outside-radius variant.
        ///
        /// <para>The modal is only reachable from a row that IS inside the radius (D1), so the
        /// second half is a fact rather than a hope — but it is still derived from the row's own
        /// numbers, so a future entry point that opens it from further away tells the truth
        /// instead of inheriting an assumption.</para>
        /// </summary>
        internal static string SubLine(VenueDto? v)
        {
            if (v == null) return string.Empty;
            string km = RoundSpotRowView.Km(v.DistanceM);
            bool inside = v.DistanceM.HasValue && v.GpsRadiusM.HasValue &&
                          v.DistanceM.Value <= v.GpsRadiusM.Value;
            return string.Format(
                LocalizationManager.Get(inside ? "GPS_ROUNDS_CONFIRM_SUB"
                                               : "GPS_ROUNDS_CONFIRM_SUB_OUTSIDE"), km);
        }

        /// <summary>"● HIGH GPS ACCURACY" and its two siblings, as one localized row each.</summary>
        internal static string QualityLabel(GpsQuality q) => LocalizationManager.Get(
            q == GpsQuality.High ? "GPS_ROUNDS_ACCURACY_HIGH"
            : q == GpsQuality.Medium ? "GPS_ROUNDS_ACCURACY_MED"
            : "GPS_ROUNDS_ACCURACY_LOW");
    }
}
