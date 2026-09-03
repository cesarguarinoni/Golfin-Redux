// ─────────────────────────────────────────────────────────────────────────────
// gps_checkin §C4 — one row of the NEAR YOU list (Figma 14077:34004 / 34021 /
// 34037).
//
// ONE TEMPLATE, THREE CATEGORIES. The three node rows differ only in the icon
// ring's stroke colour and whether the PARTNER tag is drawn; authoring them as
// three prefabs would have made the fourth (a partner range) impossible without
// a fourth. So the row is one shape and the differences are Bind() arguments.
//
// THE BUTTON IS NEVER DISABLED, AND THAT IS THE POINT (D1, Cesar 2026-09-03:
// "the player must always be TOLD why check-in is unavailable, never left with a
// dead button"). Outside the radius it changes colour and reads "2.4 KM AWAY",
// and TAPPING IT RAISES A TOAST THAT SAYS WHY. A greyed-out control answers the
// question "can I?" and refuses the question "why not?".
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Globalization;
using Golfin.UI.Polish;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>Binds one <see cref="VenueDto"/> to the authored spot row.</summary>
    [DisallowMultipleComponent]
    public sealed class RoundSpotRowView : MonoBehaviour
    {
        /// <summary>Why the row's action is not a check-in. Decides the button's label, its fill,
        /// and which toast a tap raises.</summary>
        public enum ActionState
        {
            /// <summary>Inside the venue radius — gold CHECK IN, opens the confirm modal.</summary>
            CheckIn,
            /// <summary>Outside it — dark "N KM AWAY", tap toasts the distance.</summary>
            TooFar,
            /// <summary>No fix at all — dark CHECK IN, tap toasts "turn on location".</summary>
            NoGps,
            /// <summary>A round is already live: the list is FOOD first and the rows offer
            /// DETAILS instead (§C4, PLAYLIFE order).</summary>
            Details,
        }

        [Header("Identity")]
        [SerializeField] private Image? _iconRing;
        [SerializeField] private TextMeshProUGUI? _name;
        [SerializeField] private TextMeshProUGUI? _subtitle;
        [SerializeField] private TextMeshProUGUI? _distance;

        [Header("Partner tag")]
        [SerializeField] private GameObject? _partnerTag;

        [Header("Action")]
        [SerializeField] private Button? _actionButton;
        [SerializeField] private Image? _actionFill;
        [SerializeField] private TextMeshProUGUI? _actionLabel;

        [Header("Sprites")]
        [Tooltip("Gold Main Buttons - Small, for the CHECK IN state.")]
        [SerializeField] private Sprite? _goldSprite;

        [Tooltip("The dark ADark(black,0.35) capsule for TOO FAR / NO GPS / DETAILS.")]
        [SerializeField] private Sprite? _darkSprite;

        /// <summary>The bound row, so the screen's click handler does not have to keep a parallel
        /// array. Null on an unbound (hidden) row.</summary>
        public VenueDto? Venue { get; private set; }

        public ActionState State { get; private set; } = ActionState.NoGps;

        /// <summary>Raised on a tap, with this row's venue and the state the button was IN — so
        /// the screen shows the confirm modal, the too-far toast or the details modal without
        /// re-deriving which one applies.</summary>
        public event Action<RoundSpotRowView>? OnAction;

        private bool _wired;

        private void Awake() => WireOnce();

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;
            if (_actionButton != null)
                _actionButton.onClick.AddListener(() => OnAction?.Invoke(this));
        }

        /// <summary>
        /// Paint the row.
        ///
        /// <para><paramref name="ringColour"/> is the icon ring's stroke: gold for a plain course,
        /// green for a partner, orange for food (the node's own three).</para>
        /// </summary>
        public void Bind(VenueDto venue, ActionState state, Color ringColour,
                         Color distanceColour)
        {
            WireOnce();
            Venue = venue;
            State = state;

            if (_name != null) _name.text = venue?.Name ?? string.Empty;
            if (_subtitle != null) _subtitle.text = SubtitleOf(venue);
            if (_distance != null)
            {
                _distance.text = DistanceLine(venue);
                _distance.color = distanceColour;
            }
            if (_iconRing != null) _iconRing.color = ringColour;
            if (_partnerTag != null) _partnerTag.SetActive(venue != null && venue.IsPartner);

            ApplyAction(state, venue);
        }

        private void ApplyAction(ActionState state, VenueDto? venue)
        {
            if (_actionLabel != null)
            {
                _actionLabel.text = state switch
                {
                    ActionState.CheckIn => LocalizationManager.Get("GPS_ROUNDS_CHECK_IN"),
                    ActionState.Details => LocalizationManager.Get("GPS_ROUNDS_DETAILS"),
                    ActionState.TooFar  => string.Format(LocalizationManager.Get("GPS_ROUNDS_TOO_FAR"),
                                                         Km(venue?.DistanceM)),
                    _                   => LocalizationManager.Get("GPS_ROUNDS_CHECK_IN"),
                };
                // Gold buttons take the dark ink of the Main Buttons atom; every dark state is
                // white on ADark, per the node.
                _actionLabel.color = state == ActionState.CheckIn
                    ? GpsUiColor.ButtonInk
                    : Color.white;
            }

            if (_actionFill != null)
            {
                Sprite? want = state == ActionState.CheckIn ? _goldSprite : _darkSprite;
                if (want != null) _actionFill.sprite = want;
                _actionFill.color = Color.white;
            }

            // NEVER interactable = false. See the header: a dead button cannot say why.
            if (_actionButton != null) _actionButton.interactable = true;
        }

        /// <summary>
        /// "Kawagoe, Saitama · East 18H · PAR 72" — the server's own subtitle when it has one,
        /// falling back to the address so an OSM-imported course (which has neither) still shows
        /// something rather than an empty line.
        /// </summary>
        internal static string SubtitleOf(VenueDto? v)
        {
            if (v == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(v.Subtitle)) return v.Subtitle;
            if (!string.IsNullOrWhiteSpace(v.Address)) return v.Address;
            return string.Empty;
        }

        /// <summary>
        /// "2.4 km · ¥15,000〜". The distance half is dropped — not shown as "— km" — when the
        /// fetch had no fix, because a row with an unknown distance is exactly the row whose
        /// button says NO GPS, and repeating the unknown twice is noise.
        /// </summary>
        internal static string DistanceLine(VenueDto? v)
        {
            if (v == null) return string.Empty;
            string km = v.DistanceM.HasValue ? Km(v.DistanceM) + " km" : string.Empty;
            string price = string.IsNullOrWhiteSpace(v.PriceLabel) ? string.Empty : v.PriceLabel;
            if (km.Length == 0) return price;
            if (price.Length == 0) return km;
            return km + " · " + price;
        }

        /// <summary>
        /// Metres to a one-decimal kilometre string. Invariant culture on purpose: this is a
        /// NUMBER inside a localized sentence, and a device in a comma-decimal locale would
        /// otherwise render "2,4 km" beside a "¥15,000〜" that uses the comma as a thousands
        /// separator — two different meanings for one glyph in one line.
        /// </summary>
        internal static string Km(double? metres)
            => metres.HasValue
                ? (metres.Value / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)
                : "—";
    }
}
