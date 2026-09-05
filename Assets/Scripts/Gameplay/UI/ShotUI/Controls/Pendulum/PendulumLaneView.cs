using UnityEngine;
using TMPro;
using Golfin.Gameplay.Config;

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

        /// <summary>
        /// Lay the lane out for this swing. Called at Activate and whenever putt mode flips, not
        /// per frame — none of it changes while a finger is down.
        /// </summary>
        public void ApplyGeometry(in ControlsConfig cfg, bool isPutt)
        {
            // A TICK MARKS WHERE THE CLUB HEAD LANDS, not the raw pull distance — the two differ
            // by the club's rest offset, and drawing the raw distance put the 100%/120% lines
            // ~70px above the club that was supposed to be sitting on them.
            float tick100 = _handleRestBelowBall + cfg.PendulumPull100Px;
            float tick120 = _handleRestBelowBall + cfg.PendulumPull120Px;

            // And the LANE IS DERIVED from the deepest tick, so it always contains the club at
            // full pull and the ticks always sit at the same proportion down the pill. Authoring
            // the height by hand is what let the pill and its lines drift apart in the first place.
            float deepest = isPutt ? tick100 : tick120;      // a putt has no 120% tick to reach
            LaneHeight = deepest + _clubHalfHeight + _laneTailPx;

            if (_lane != null)
                _lane.sizeDelta = new Vector2(_lane.sizeDelta.x, LaneHeight);

            PlaceTick(_tick100, _label100, tick100, true);
            PlaceTick(_tick120, _label120, tick120, !isPutt);
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
