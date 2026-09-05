using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.UI.Controls.FreeSwing
{
    /// <summary>
    /// The swing lane (Figma <c>SwingLane</c> 14092:34686): a 140-wide stadium the club head
    /// slides down and back up through, carrying the gold 100% and red 120% ticks, the white
    /// IMPACT line, and the green impact window on it.
    ///
    /// <para>THE LANE IS THE CONFIG, DRAWN — the contract <c>PendulumLaneView</c> has with its
    /// ticks, extended upward. Every offset here is <c>HandleRestBelowBall + FreeSwingPull*Px</c>,
    /// i.e. WHERE THE CLUB HEAD LANDS at that power, not the raw pull distance: pull 380 canvas
    /// px and the club head is exactly on the gold line, because both come from the same field.
    /// Hard-coding the tick positions would let a retune of the CSV move the shot without moving
    /// the line the player is aiming at.</para>
    ///
    /// <para>AND THE PILL IS DERIVED FROM THE DEEPEST TICK, the fix Cesar asked for on the
    /// Pendulum and asked to see imitated here: authoring the node's 560px height by hand is what
    /// let the pill and its lines drift apart in the first place, and a longer pull needs a longer
    /// pill or the ticks crowd the cap. It is the Pendulum's own derivation plus one term — this
    /// is the only scheme whose gesture continues PAST the impact line, so
    /// <c>FreeSwingFollowThroughPx</c> of lane is carried ABOVE the ball for the club to travel
    /// through on its way out.</para>
    ///
    /// <para>THE IMPACT LINE IS A TICK LIKE THE OTHERS: it marks where the club head lands when
    /// the pull is back to zero AND the finger has returned the club to the ball, which is the
    /// crossing the driver grades. Drawing it anywhere else would be drawing a target the club
    /// never reaches. See <see cref="ImpactCrossOffsetPx"/> — the driver reads that number off
    /// this view rather than keeping its own, so the drawn line and the graded crossing are one
    /// value.</para>
    /// </summary>
    public class FreeSwingLaneView : PendulumFadingView
    {
        [Header("Figma: Scheme — FreeSwing / SwingLane (14092:34686)")]
        [Tooltip("The lane rect. Pivoted at its TOP edge, which sits FollowThroughPx above the " +
                 "ball rest centre; the height is derived in ApplyGeometry.")]
        [SerializeField] private RectTransform _lane;
        [SerializeField] private RectTransform _tick100;
        [SerializeField] private RectTransform _tick120;
        [SerializeField] private RectTransform _impactLine;
        [SerializeField] private RectTransform _impactWindow;
        [SerializeField] private TextMeshProUGUI _label100;
        [SerializeField] private TextMeshProUGUI _label120;
        [SerializeField] private TextMeshProUGUI _impactLabel;

        [Header("Geometry (canvas px)")]
        [Tooltip("Where the club head RESTS below the ball centre, measured to its CENTRE. The " +
                 "driver uses the same number for the tick a pull is drawn at, for the place the " +
                 "club lands at that pull, AND for how far above the touch origin the finger has " +
                 "to travel to put the club head back on the ball.")]
        [SerializeField] private float _handleRestBelowBall = 70f;
        [Tooltip("Half the club-head sprite's height. The lane has to end below the club's BOTTOM " +
                 "at full pull, not below its centre.")]
        [SerializeField] private float _clubHalfHeight = 50f;
        [Tooltip("Slack between the club's bottom edge at full pull and the end of the pill.")]
        [SerializeField] private float _laneTailPx = 20f;
        [Tooltip("Lane ABOVE the ball on a PUTT. The node halves the follow-through as well as " +
                 "the depth (SwingLane top 861 vs 801 against a ball at 961).")]
        [SerializeField] private float _puttFollowThroughPx = 100f;
        [Tooltip("The green window's height and corner radius — node 16px tall, r8.")]
        [SerializeField] private float _impactWindowHeight = 16f;

        /// <summary>Lane height for the mode, in canvas px — DERIVED in <see cref="ApplyGeometry"/>
        /// from the follow-through plus the deepest tick plus the club's lower half. The driver
        /// clamps the club head's travel to it.</summary>
        public float LaneHeight { get; private set; }

        /// <summary>How far below the ball centre the club head rests, to its centre.</summary>
        public float HandleRestBelowBall => _handleRestBelowBall;

        /// <summary>
        /// How far ABOVE its touch origin the finger must travel for the club head to reach the
        /// impact line — which is exactly how far the club head sits below the ball at rest.
        ///
        /// <para>THE ONE NUMBER THE DRIVER AND THIS VIEW MUST AGREE ON, so it is published here
        /// and read there rather than duplicated. SPEC §3.2 writes the crossing test as
        /// <c>pos.y ≥ origin.y</c>, which is the same statement for a scheme whose club head
        /// rests ON the ball; this one reuses the <c>ClubHandle</c> clone at the rest offset
        /// Pendulum and Needle already share (70px, so the ball ghost is not buried under the
        /// club), and at that offset a finger back at its own origin leaves the club head 70px
        /// SHORT of the drawn line. Firing there would fire at a line the club visibly never
        /// reached — the exact class of "the drawn thing is not the graded thing" defect
        /// carry-over 2 exists to prevent.</para>
        /// </summary>
        public float ImpactCrossOffsetPx => _handleRestBelowBall;

        /// <summary>The green window's drawn HALF-width, read back off the live rect. The number
        /// a test and the acceptance run compare against <c>FreeSwingMath.ImpactWindowPx</c> at
        /// the peak power — measured off the graphic, never recomputed from the formula that drew
        /// it, which would be a tautology.</summary>
        public float DrawnImpactHalfWidthPx =>
            _impactWindow != null ? _impactWindow.sizeDelta.x * 0.5f : 0f;

        /// <summary>The ticks as drawn, in canvas px below the ball centre.</summary>
        public float Tick100BelowBall { get; private set; }
        public float Tick120BelowBall { get; private set; }

        /// <summary>
        /// Lay the lane out for this swing. Called at Activate and whenever putt mode flips, not
        /// per frame — none of it changes while a finger is down. The green window is the one
        /// thing that does, and <see cref="ApplyImpactWindow"/> owns that.
        /// </summary>
        public void ApplyGeometry(in ControlsConfig cfg, bool isPutt)
        {
            Tick100BelowBall = _handleRestBelowBall + cfg.FreeSwingPull100Px;
            Tick120BelowBall = _handleRestBelowBall + cfg.FreeSwingPull120Px;

            float deepest = isPutt ? Tick100BelowBall : Tick120BelowBall;   // a putt has no 120% tick
            float above   = isPutt ? _puttFollowThroughPx : cfg.FreeSwingFollowThroughPx;

            LaneHeight = above + deepest + _clubHalfHeight + _laneTailPx;

            if (_lane != null)
            {
                // Pivoted at its top edge, which is parked FollowThroughPx above the ball, so
                // every child below can be placed as a distance from the lane's top and the two
                // ends of the pill move independently as the derivation changes.
                _lane.pivot = new Vector2(0.5f, 1f);
                _lane.anchoredPosition = new Vector2(0f, above);
                _lane.sizeDelta = new Vector2(_lane.sizeDelta.x, LaneHeight);
            }

            // The impact line is the tick at "club head back on the ball" — offset 0 from the
            // ball, i.e. `above` from the lane's top edge.
            PlaceFromLaneTop(_impactLine,   above, true);
            PlaceFromLaneTop(_impactWindow, above, true);
            PlaceTick(_tick100, _label100, above + Tick100BelowBall, Tick100BelowBall, true);
            PlaceTick(_tick120, _label120, above + Tick120BelowBall, Tick120BelowBall, !isPutt);

            if (_impactLabel != null)
                _impactLabel.rectTransform.anchoredPosition =
                    new Vector2(_impactLabel.rectTransform.anchoredPosition.x, 0f);
        }

        /// <summary>
        /// Size the green window for the power the shot WOULD commit at right now.
        ///
        /// <para>Called every drag frame, not just at Activate: the whole point of the power
        /// shrink is that the player watches the window close as they pull. Driven from the PEAK
        /// pull and not the live one, because the shot commits at the peak — and in THIS scheme
        /// the live pull returns to zero on the way to the shot, so a live-driven window would
        /// yawn back open in the instant before impact and show a target the swing is not going
        /// to be judged against.</para>
        /// </summary>
        /// <param name="halfWidthPx"><c>FreeSwingMath.ImpactWindowPx</c> at the peak power — a
        /// HALF-width, doubled here, in the one place that doubling happens.</param>
        public void ApplyImpactWindow(float halfWidthPx)
        {
            if (_impactWindow == null) return;
            float w = Mathf.Max(2f, halfWidthPx * 2f);
            _impactWindow.sizeDelta = new Vector2(w, _impactWindowHeight);
        }

        /// <summary>The IMPACT word beside the line. Resolved at show time and not cached, for the
        /// reason <c>SchemeGradePop</c> resolves its own: the language can change under a live
        /// screen, and the builder authors a placeholder that would otherwise ship.</summary>
        public void RefreshLabels()
        {
            if (_impactLabel != null)
                _impactLabel.text = LocalizationManager.Get(FreeSwingMath.KeyImpactLine);
        }

        /// <summary>What the IMPACT label currently reads. Read back by the tests and the
        /// acceptance run, so "no hardcoded text" is an assertion against a KEY's resolved value.</summary>
        public string ImpactLabelText => _impactLabel != null ? _impactLabel.text : null;

        private void PlaceTick(RectTransform tick, TextMeshProUGUI label,
                               float fromLaneTop, float belowBall, bool shown)
        {
            PlaceFromLaneTop(tick, fromLaneTop, shown);
            if (label == null) return;
            if (label.gameObject.activeSelf != shown) label.gameObject.SetActive(shown);
            // The label is a sibling of the LANE (it sits outside the lane's clip rect), so it is
            // offset from the BALL rather than from the tick's parent.
            if (shown)
                label.rectTransform.anchoredPosition =
                    new Vector2(label.rectTransform.anchoredPosition.x, -belowBall);
        }

        private static void PlaceFromLaneTop(RectTransform rt, float fromTop, bool shown)
        {
            if (rt == null) return;
            if (rt.gameObject.activeSelf != shown) rt.gameObject.SetActive(shown);
            if (shown) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -fromTop);
        }

        public override void HideImmediate()
        {
            base.HideImmediate();
            // Nothing else to reset: the lane never moves, only its alpha does. The window's
            // width is re-driven from the peak at the next pointer-down.
        }

        /// <summary>EditMode wiring seam — the same objects the scene builder assigns. Without it
        /// a scene-less fixture drives a view whose window is null and every width assertion
        /// passes vacuously against 0.</summary>
        public void ConfigureForTests(RectTransform lane, RectTransform tick100, RectTransform tick120,
                                      RectTransform impactLine, RectTransform impactWindow,
                                      TextMeshProUGUI label100, TextMeshProUGUI label120,
                                      TextMeshProUGUI impactLabel)
        {
            _lane = lane; _tick100 = tick100; _tick120 = tick120;
            _impactLine = impactLine; _impactWindow = impactWindow;
            _label100 = label100; _label120 = label120; _impactLabel = impactLabel;
        }

        /// <summary>The tick rects as drawn, so a test can assert the DRAWN offset against the
        /// CONFIG rather than against a recomputation of the same formula.</summary>
        public float DrawnTick100FromLaneTop => _tick100 != null ? -_tick100.anchoredPosition.y : 0f;
        public float DrawnTick120FromLaneTop => _tick120 != null ? -_tick120.anchoredPosition.y : 0f;
        public float DrawnImpactFromLaneTop  => _impactLine != null ? -_impactLine.anchoredPosition.y : 0f;
        public bool  Tick120Shown            => _tick120 != null && _tick120.gameObject.activeSelf;
    }
}
