using UnityEngine;
using TMPro;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.UI.Controls.Pendulum
{
    /// <summary>
    /// The vertical pull lane under the ball (Figma <c>PowerLane</c>, node 14092:34681): a
    /// 120-wide stadium the club head slides down inside, with a gold 100% tick and a red 120%
    /// tick across it.
    ///
    /// <para>THE LANE IS THE CONFIG, DRAWN. <c>PendulumPull100Px</c> and <c>PendulumPull120Px</c>
    /// are not "numbers that happen to look right next to the ticks" — the tick offsets ARE those
    /// values, applied here at Activate. Pull 300 canvas px and the club head is exactly on the
    /// gold line, because both come from the same field. Hard-coding the tick positions would let
    /// a retune of the CSV move the shot without moving the line the player is aiming at, which
    /// is the class of bug <c>TimingBandGoldY01</c> was pulled into ControlsConfig to kill.</para>
    /// </summary>
    public class PendulumLaneView : PendulumFadingView
    {
        [Header("Figma: Scheme — Pendulum / PowerLane (14092:34681)")]
        [Tooltip("The lane rect. Top edge sits on the ball rest centre; height is set from the mode.")]
        [SerializeField] private RectTransform _lane;
        [SerializeField] private RectTransform _tick100;
        [SerializeField] private RectTransform _tick120;
        [SerializeField] private TextMeshProUGUI _label100;
        [SerializeField] private TextMeshProUGUI _label120;

        [Header("Geometry (canvas px)")]
        [Tooltip("Where the club head RESTS below the ball centre, measured to its CENTRE. The " +
                 "driver uses the same number, so the tick a pull is drawn at and the place the " +
                 "club lands at that pull are one value.")]
        [SerializeField] private float _handleRestBelowBall = 70f;
        [Tooltip("Half the club-head sprite's height. The lane has to end below the club's BOTTOM " +
                 "at full pull, not below its centre.")]
        [SerializeField] private float _clubHalfHeight = 50f;
        [Tooltip("Slack between the club's bottom edge at full pull and the end of the pill.")]
        [SerializeField] private float _laneTailPx = 20f;

        /// <summary>Lane height for the mode, in canvas px — DERIVED in <see cref="ApplyGeometry"/>
        /// from the deepest tick plus the club's lower half. The driver clamps the pull to it.</summary>
        public float LaneHeight { get; private set; }

        /// <summary>How far below the ball centre the club head rests, to its centre. The driver
        /// reads it so the two cannot disagree about where a given pull puts the club.</summary>
        public float HandleRestBelowBall => _handleRestBelowBall;

        /// <summary>Half the club-head sprite's height, and the slack under it — the two terms
        /// that turn the deepest tick into the pill's bottom edge. Exposed for
        /// <c>ShotLayoutController</c>'s D6 clamp, which has to compute the lane's END from the
        /// SAME numbers this view draws it with (shot_view_layout §3.3).</summary>
        public float ClubHalfHeight => _clubHalfHeight;
        public float LaneTailPx     => _laneTailPx;

        /// <summary>Canvas y the drawn pill may not extend past — the shared bottom baseline,
        /// pushed in by <c>ShotLayoutController</c>. Uncapped until someone sets it, so an Editor
        /// scene without the controller draws exactly what it drew before
        /// (shot_view_layout_followup §1).</summary>
        private float _laneEndCapY = float.NegativeInfinity;

        /// <summary>
        /// Set the bottom cap — AND re-derive if the geometry has already been laid out.
        ///
        /// <para>THE ORDER USED TO MATTER, AND IT MUST NOT. This was a bare field write, and
        /// <see cref="ApplyGeometry"/> is the only thing that reads it, so a cap arriving AFTER
        /// the lane had been laid out was silently discarded and the pill kept its uncapped
        /// height for the rest of the session. That is not hypothetical: on a boot where this
        /// scheme is ALREADY the saved one, the host activates its driver before
        /// <c>ShotLayoutController.ApplyLayout</c> runs, so the pill drew ~96px past the row the
        /// action buttons sit on — exactly what shot_view_layout_followup §1 exists to prevent.
        /// Every earlier acceptance run switched scheme MID-SESSION, which happens to produce the
        /// other order, so the gate never saw it.</para>
        ///
        /// <para>Re-deriving here is what makes the lane own its own invariant ("my drawn height
        /// respects the cap I have been given") instead of depending on two other components
        /// calling it in the right sequence. The controller's ordering comment is still true; it
        /// is simply no longer load-bearing. Guarded on an actual CHANGE so the repeated applies
        /// ShotLayoutController does on every scheme switch cost nothing.</para>
        /// </summary>
        public void SetLaneEndCapY(float canvasY)
        {
            if (_laneEndCapY == canvasY) return;          // exact: these are assigned, not accumulated
            _laneEndCapY = canvasY;
            if (_hasGeometry) ApplyGeometry(_lastCfg, _lastIsPutt);
        }

        // The last inputs ApplyGeometry was called with, so a cap that arrives late can re-run it
        // with them. A copy of the struct, not a reference — the caller's `in` parameter is gone
        // by the time this is needed.
        private ControlsConfig _lastCfg;
        private bool           _lastIsPutt;
        private bool           _hasGeometry;

        /// <summary>
        /// Lay the lane out for this swing. Called at Activate and whenever putt mode flips, not
        /// per frame — none of it changes while a finger is down.
        /// </summary>
        public void ApplyGeometry(in ControlsConfig cfg, bool isPutt)
        {
            _lastCfg = cfg; _lastIsPutt = isPutt; _hasGeometry = true;

            // A TICK MARKS WHERE THE CLUB HEAD LANDS, not the raw pull distance — the two differ
            // by the club's rest offset, and drawing the raw distance put the 100%/120% lines
            // ~70px above the club that was supposed to be sitting on them.
            float tick100 = _handleRestBelowBall + cfg.PendulumPull100Px;
            float tick120 = _handleRestBelowBall + cfg.PendulumPull120Px;

            // And the LANE IS DERIVED from the deepest tick, so it always contains the club at
            // full pull and the ticks always sit at the same proportion down the pill. Authoring
            // the height by hand is what let the pill and its lines drift apart in the first place.
            float deepest = isPutt ? tick100 : tick120;      // a putt has no 120% tick to reach
            float derived = deepest + _clubHalfHeight + _laneTailPx;

            // ...and then TRIMMED to the shared bottom baseline. The derivation above is still
            // what the pill wants to be; the cap is what the screen allows. A putt's lane is
            // short enough that the cap never bites.
            LaneHeight = ShotLayoutMath.CappedLaneHeight(derived, deepest, LaneTopCanvasY(), _laneEndCapY);

            if (_lane != null)
                _lane.sizeDelta = new Vector2(_lane.sizeDelta.x, LaneHeight);

            PlaceTick(_tick100, _label100, tick100, true);
            PlaceTick(_tick120, _label120, tick120, !isPutt);
        }

        /// <summary>The lane's top edge in canvas-centre space, read off the live rect rather than
        /// derived from the ball — the view does not know where the ball is, and its top edge is
        /// pivot-anchored so it does not move when the height below changes. NaN when there is no
        /// canvas to measure against, which <see cref="ShotLayoutMath.CappedLaneHeight"/> reads as
        /// "uncapped".</summary>
        private float LaneTopCanvasY()
        {
            if (_lane == null) return float.NaN;
            Canvas canvas = _lane.GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect == null) return float.NaN;

            var corners = new Vector3[4];
            _lane.GetWorldCorners(corners);
            return canvasRect.InverseTransformPoint(corners[1]).y;   // [1] = top-left
        }

        private void PlaceTick(RectTransform tick, TextMeshProUGUI label, float pullPx, bool shown)
        {
            if (tick != null)
            {
                if (tick.gameObject.activeSelf != shown) tick.gameObject.SetActive(shown);
                if (shown) tick.anchoredPosition = new Vector2(tick.anchoredPosition.x, -pullPx);
            }
            if (label != null)
            {
                if (label.gameObject.activeSelf != shown) label.gameObject.SetActive(shown);
                // The label is a sibling of the LANE (it sits outside the lane's clip rect), so it
                // is offset from the lane's own top edge rather than from the tick's parent.
                if (shown && _lane != null)
                    label.rectTransform.anchoredPosition =
                        new Vector2(label.rectTransform.anchoredPosition.x, -pullPx);
            }
        }

        public override void HideImmediate()
        {
            base.HideImmediate();
            // Nothing else to reset: the lane never moves, only its alpha does.
        }
    }
}
