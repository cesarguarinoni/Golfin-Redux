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

        [Tooltip("The 1px #818ea1 rim the node draws around the DARK capsule only. Hidden on the " +
                 "gold state, whose atom carries its own border.")]
        [SerializeField] private Image? _actionRim;

        /// <summary>Name width when the PARTNER tag shares the line (node 14077:34011).</summary>
        private const float NameWidthWithTag = 330f;

        // ── The action pill's two widths ──────────────────────────────────────
        // "CHECK IN" is 177.7px and "DETAILS" 158.7px, so the node's 230 holds them with room.
        // The TOO FAR state does not fit and never did: "{0} KM AWAY" measures 239.5 at 0.0 km,
        // 256.4 at 12.3 and 281.2 at 128.5 — EVERY English distance overflows 230, which is what
        // Cesar saw spilling out of the capsule. (Japanese "{0} KM 先" is 168.6–210.9 and fits,
        // which is why only EN showed it.) The pill therefore takes a second width in that state,
        // grown LEFTWARD so its right edge stays on the node's 926.
        private const float ActionWidthNormal = 230f;
        private const float ActionWidthFar    = 320f;
        private const float ActionRightEdge   = 926f;   // 696 + 230, the node's position
        private const float ActionLabelInset  = 16f;    // keeps glyphs off the capsule's round ends

        /// <summary>Info width when the wider TOO FAR pill is taking the space.
        ///
        /// <para>The Info frame is <c>overflow-clip</c> and its width IS the clip — the builder's
        /// note is "a real OSM address must be CUT at 540px, not run under the button at 696". The
        /// wider pill starts at 606, so 540 stops clipping in time and the address runs under the
        /// capsule. Narrowing the CONTAINER (not just the name) keeps the same 24px gap the normal
        /// state has, and the RectMask2D then cuts the subtitle and distance lines with it.</para>
        ///
        /// <para>The longest real venue name measures 419.9px ("TEST Office (WeWork Harumi)"), so
        /// 450 truncates nothing that exists today.</para>
        /// </summary>
        private const float InfoWidthFar    = 450f;
        private const float InfoWidthNormal = 540f;

        /// <summary>The authored label size, and the floor auto-sizing may fall to.</summary>
        private const float ActionLabelFontSize = 39f / 1.2f;   // SB(39) — the builder's convert
        private const float ActionLabelFontMin  = 26f;

        /// <summary>…and when it does not — the full Info width (node 14077:34029).</summary>
        private const float NameWidthFull = 540f;

        /// <summary>9-slice calibration for the gold Main Buttons atom (border 18) at the node's
        /// r20 corner.</summary>
        private const float GoldPpum = 18f / 20f;

        /// <summary>…and for S_PillStadium (border 88) at that same r20. The two differ by ~5x,
        /// which is why swapping the sprite without swapping this collapses the capsule.</summary>
        private const float PillPpum = 88f / 20f;

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
            bool partner = venue != null && venue.IsPartner;
            if (_partnerTag != null) _partnerTag.SetActive(partner);

            // The node gives Name 330px on a row that shows the PARTNER tag and the full width on
            // one that does not (14077:34011 vs :34029) — that is flex, and this is the same
            // result stated explicitly. Without it a non-partner name is cut short for a tag that
            // is not there.
            // The Info frame and the name resize TOGETHER with the action pill. Resizing only the
            // name left the subtitle and distance at full width, so a long address kept running
            // under the wider capsule — the exact thing the frame's clip exists to prevent.
            float infoWidth = state == ActionState.TooFar ? InfoWidthFar : InfoWidthNormal;

            if (_name != null)
            {
                var infoRt = _name.transform.parent as RectTransform;
                if (infoRt != null && !Mathf.Approximately(infoRt.sizeDelta.x, infoWidth))
                    infoRt.sizeDelta = new Vector2(infoWidth, infoRt.sizeDelta.y);

                var rt = (RectTransform)_name.transform;
                float want = partner ? NameWidthWithTag : infoWidth;
                if (!Mathf.Approximately(rt.sizeDelta.x, want))
                    rt.sizeDelta = new Vector2(want, rt.sizeDelta.y);
            }

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
                // NoWrap + Overflow: "12.4 KM AWAY" is wider than "CHECK IN" and must not push the
                // capsule or wrap onto a second line inside a 54px button.
                _actionLabel.textWrappingMode = TextWrappingModes.NoWrap;
                // Backstop for a distance wider than even the TOO FAR pill — the widths above are
                // sized from measured strings, but a far-flung venue must shrink rather than spill.
                _actionLabel.enableAutoSizing = true;
                _actionLabel.fontSizeMax = ActionLabelFontSize;
                _actionLabel.fontSizeMin = ActionLabelFontMin;
            }

            bool gold = state == ActionState.CheckIn;

            // The pill is SIZED by its state, not authored once. Growing leftward from a fixed
            // right edge keeps it aligned with every other row while the label gets the width it
            // actually needs. NoWrap alone — which is what the first version relied on — stops the
            // text wrapping but happily lets it spill past the capsule.
            if (_actionButton != null)
            {
                var art = (RectTransform)_actionButton.transform;
                float wantW = state == ActionState.TooFar ? ActionWidthFar : ActionWidthNormal;
                if (!Mathf.Approximately(art.sizeDelta.x, wantW))
                {
                    art.sizeDelta        = new Vector2(wantW, art.sizeDelta.y);
                    art.anchoredPosition = new Vector2(ActionRightEdge - wantW, art.anchoredPosition.y);
                }

                if (_actionLabel != null)
                {
                    var lrt = (RectTransform)_actionLabel.transform;
                    float wantL = wantW - ActionLabelInset * 2f;
                    if (!Mathf.Approximately(lrt.sizeDelta.x, wantL))
                    {
                        lrt.sizeDelta        = new Vector2(wantL, lrt.sizeDelta.y);
                        lrt.anchoredPosition = new Vector2(ActionLabelInset, lrt.anchoredPosition.y);
                    }
                }
            }

            if (_actionFill != null)
            {
                // ⚠️ THREE THINGS CHANGE WITH THE SPRITE, NOT ONE. The first version swapped only
                // `sprite` and left `color = white` and the gold atom's `pixelsPerUnitMultiplier`
                // in place, which rendered every TOO FAR / NO GPS / DETAILS button as a WHITE
                // ELLIPSE with an invisible white label:
                //
                //   colour  `Play Button.png` is a finished gold atom and wants white;
                //           `S_PillStadium` is a WHITE capsule meant to be TINTED, so leaving it
                //           white paints a white blob over the row.
                //   ppum    9-slicing scales the corner by border/ppum. The gold atom's border is
                //           18 (18/20 -> the node's r20); the pill's is 88, so the SAME 0.9 gave a
                //           ~97px radius on a 54px-tall rect — a fully round ellipse. It needs
                //           88/20 = 4.4 to land on the same r20.
                //   rim     the node draws a #818ea1 stroke on the dark capsule and none on the
                //           gold one, whose atom already has a border.
                //
                // This is the Rule 21 shape exactly (9-slice collapse + a tint left at white), so
                // all three move together here rather than being set once at author time.
                Sprite? want = gold ? _goldSprite : _darkSprite;
                if (want != null) _actionFill.sprite = want;
                _actionFill.color = gold ? Color.white : GpsUiColor.ADark(Color.black, 0.35f);
                _actionFill.type = Image.Type.Sliced;
                _actionFill.pixelsPerUnitMultiplier = gold ? GoldPpum : PillPpum;
            }

            if (_actionRim != null) _actionRim.gameObject.SetActive(!gold);

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
