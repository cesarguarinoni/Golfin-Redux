using UnityEngine;
using TMPro;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.UI.Controls.Needle
{
    /// <summary>
    /// The power circle around the ball (Figma <c>Needle — Pull</c>, node 14091:102630): three
    /// rings the club head is dragged out to, a red overpower crescent in the bottom arc between
    /// the 100% and 120% rings, and the "100%" label on the gold ring.
    ///
    /// <para>THE RINGS ARE THE CONFIG, DRAWN — the contract <c>PendulumLaneView</c> has with the
    /// pull thresholds, in polar form. A ring sits at <c>HandleRestBelowBall + NeedlePull{80,100,
    /// 120}Px</c>: where the club head LANDS at that power, not the raw pull distance, because the
    /// two differ by the club's rest offset and drawing the raw distance would put every ring
    /// ~70px inside the club that is supposed to be sitting on it. Retune the CSV and the rings
    /// move with the shot; hard-coding the node's 240/300/360 would let a retune move the shot
    /// without moving the circle the player is aiming at, which is the class of bug
    /// <c>TimingBandGoldY01</c> was pulled into ControlsConfig to kill.</para>
    ///
    /// <para>Because of that the built radii are NOT the node's. The node samples the formula at a
    /// shorter pull; this scheme seeds the Pendulum's thresholds so the pull feels the same across
    /// both, which puts the 120% ring at 526px rather than 360. The RATIOS the node fixes — 0.8 /
    /// 1.0 / 1.2, the crescent spanning exactly ring100→ring120 over ±34.4° of the bottom, the
    /// label centred on the gold ring's bottom — are all preserved.</para>
    /// </summary>
    public class NeedlePowerCircleView : PendulumFadingView
    {
        [Header("Figma: Scheme — Needle / Pull (14091:102630)")]
        [SerializeField] private NeedleArcGraphic _ring80;
        [SerializeField] private NeedleArcGraphic _ring100;
        [SerializeField] private NeedleArcGraphic _ring120;
        [SerializeField] private NeedleArcGraphic _crescent;
        [SerializeField] private TextMeshProUGUI  _label100;
        [SerializeField] private RectTransform    _ballGhost;

        [Header("Geometry (canvas px)")]
        [Tooltip("Where the club head RESTS below the ball centre, measured to its CENTRE. The " +
                 "driver uses the same number, so the ring a pull is drawn at and the place the " +
                 "club lands at that pull are one value.")]
        [SerializeField] private float _handleRestBelowBall = 70f;
        [Tooltip("Half the club-head sprite's height — the circle has to contain the club's " +
                 "BOTTOM at full pull, not its centre.")]
        [SerializeField] private float _clubHalfHeight = 50f;

        [Header("Dim")]
        [Tooltip("A SECOND CanvasGroup, on a child holding the rings. The base fading view owns " +
                 "the outer one (shown while swinging, gone otherwise); this one carries the " +
                 "release dim, and the two multiply. A second group rather than a scale factor on " +
                 "PendulumFadingView because that file is shared with the shipping Pendulum build " +
                 "and this scheme has no business editing it.")]
        [SerializeField] private CanvasGroup _dimGroup;
        [Tooltip("What the circle fades to once the power is committed and the needle takes over.")]
        [SerializeField] private float _dimmedAlpha = 0.25f;

        [Header("Node constants (stroke px / angles the design fixes)")]
        [SerializeField] private float _stroke80  = 3f;
        [SerializeField] private float _stroke100 = 4f;
        [SerializeField] private float _stroke120 = 3f;
        [Tooltip("Figma OverpowerCrescent spans 34.38 deg each side of the bottom of the 120 ring.")]
        [SerializeField] private float _crescentHalfAngleDeg = 34.38f;
        [Tooltip("Label100's x offset from the ball centre. Figma: 657 vs centre 537.")]
        [SerializeField] private float _label100OffsetX = 120f;

        /// <summary>The radius of the deepest ring plus the club head's lower half — how much room
        /// the circle actually needs. Derived here rather than authored, for the same reason
        /// <c>PendulumLaneView.LaneHeight</c> is: the pill and its lines cannot drift apart if the
        /// container is computed from the lines.</summary>
        public float CircleRadius { get; private set; }

        /// <summary>The three ring radii as DRAWN, in canvas px. Read back by the tests and the
        /// acceptance run so "the ring is where the club lands" is measured off the live graphic
        /// rather than recomputed from the formula that drew it.</summary>
        public float Ring80Radius  => _ring80  != null ? _ring80.RadiusX  : 0f;
        public float Ring100Radius => _ring100 != null ? _ring100.RadiusX : 0f;
        public float Ring120Radius => _ring120 != null ? _ring120.RadiusX : 0f;

        /// <summary>How far below the ball centre the club head rests. The driver reads it so the
        /// two cannot disagree about where a given pull puts the club.</summary>
        public float HandleRestBelowBall => _handleRestBelowBall;

        /// <summary>
        /// Lay the circle out for this swing. Called at Activate and whenever putt mode flips —
        /// none of it changes while a finger is down.
        /// </summary>
        public void ApplyGeometry(in ControlsConfig cfg, bool isPutt)
        {
            float r80  = _handleRestBelowBall + cfg.NeedlePull80Px;
            float r100 = _handleRestBelowBall + cfg.NeedlePull100Px;
            float r120 = _handleRestBelowBall + cfg.NeedlePull120Px;

            // A putt has no overpower, so it draws neither the 120% ring nor the crescent that
            // lives between it and the 100% ring, and no 80% ring either: the Putt frame shows the
            // single gold circle the putt's power actually stops at.
            Place(_ring80,  r80,  _stroke80,  !isPutt, Ring80Color);
            Place(_ring100, r100, _stroke100, true,     Ring100Color);
            Place(_ring120, r120, _stroke120, !isPutt,  Ring120Color);

            if (_crescent != null)
            {
                bool shown = !isPutt;
                if (_crescent.gameObject.activeSelf != shown) _crescent.gameObject.SetActive(shown);
                if (shown)
                {
                    // Exactly ring100 -> ring120, so a retune of either threshold moves the band
                    // that warns about overpower with the ring that defines it.
                    _crescent.SetEllipse(r120, r120, r120 - r100);
                    _crescent.SetSweep(180f, _crescentHalfAngleDeg);      // 180 deg = the bottom
                    _crescent.color = NeedleColors.OverTurf(new Color32(0xFF, 0x5A, 0x5A, 255), 0.45f);
                }
            }

            if (_label100 != null)
                _label100.rectTransform.anchoredPosition = new Vector2(_label100OffsetX, -r100);

            CircleRadius = (isPutt ? r100 : r120) + _clubHalfHeight;

            if (_ballGhost != null) _ballGhost.anchoredPosition = Vector2.zero;
        }

        private void Place(NeedleArcGraphic ring, float radius, float stroke, bool shown, Color color)
        {
            if (ring == null) return;
            if (ring.gameObject.activeSelf != shown) ring.gameObject.SetActive(shown);
            if (!shown) return;
            // The node states a ring by its STROKE CENTRE (Ring80 is r=238.5 with a 3px stroke in
            // a 480 box), and the club head lands on that centre line, so the band straddles it.
            ring.SetEllipse(radius + stroke * 0.5f, radius + stroke * 0.5f, stroke);
            ring.SetSweep(0f, 180f);      // half-sweep 180 deg = a closed circle
            ring.color = color;
        }

        // ── Colours (node token + node alpha, corrected for Unity's linear blend) ─
        // Straight from the ring SVGs on 14091:102630: white @ 25%, #FFD23A @ 35%, #FF5A5A @ 25%.
        // All three are veils over turf, so they keep their alpha and have their RGB corrected —
        // see NeedleColors.OverTurf for why that is the treatment rather than a fitted alpha.
        public static Color Ring80Color  => NeedleColors.OverTurf(new Color32(255, 255, 255, 255), 0.25f);
        public static Color Ring100Color => NeedleColors.OverTurf(new Color32(0xFF, 0xD2, 0x3A, 255), 0.35f);
        public static Color Ring120Color => NeedleColors.OverTurf(new Color32(0xFF, 0x5A, 0x5A, 255), 0.25f);

        // ── Release dim ──────────────────────────────────────────────────────────

        /// <summary>
        /// Fade the circle back once the power is committed (scheme_needle §1.3). It stays on
        /// screen rather than disappearing: the player has just chosen a power and the ring they
        /// chose is the only record of it, but it is no longer the thing being read, so it gets
        /// out of the needle's way.
        /// </summary>
        public void SetDimmed(bool dimmed) => _dimTarget = dimmed ? _dimmedAlpha : 1f;

        private float _dimTarget = 1f;

        /// <summary>The dim group's live alpha. Read back by the acceptance run, so "the rings
        /// faded" is a number off the live CanvasGroup and not a look at a frame.</summary>
        public float DimAlpha => _dimGroup != null ? _dimGroup.alpha : 1f;

        protected override void Update()
        {
            base.Update();
            if (_dimGroup == null) return;
            if (Mathf.Approximately(_dimGroup.alpha, _dimTarget)) return;
            // The same rate the base view fades chrome at, so the dim reads as one motion with it.
            float rate = 1f / Mathf.Max(ControlsConfig.Default.ConeFadeOutSeconds, 0.001f);
            _dimGroup.alpha = Mathf.MoveTowards(_dimGroup.alpha, _dimTarget, rate * Time.deltaTime);
        }

        public override void HideImmediate()
        {
            base.HideImmediate();
            // Undim as well: the next swing starts at full brightness, and a lerp left running
            // across a reset would open the next pull on the previous swing's faded circle.
            _dimTarget = 1f;
            if (_dimGroup != null) _dimGroup.alpha = 1f;
        }

        /// <summary>EditMode wiring seam — the same graphics the scene builder assigns. Without it
        /// a scene-less fixture drives a view whose rings are null and every radius assertion
        /// passes vacuously against 0.</summary>
        public void ConfigureForTests(NeedleArcGraphic ring80, NeedleArcGraphic ring100,
                                      NeedleArcGraphic ring120, NeedleArcGraphic crescent,
                                      TextMeshProUGUI label100, RectTransform ballGhost,
                                      CanvasGroup dimGroup)
        {
            _ring80 = ring80; _ring100 = ring100; _ring120 = ring120;
            _crescent = crescent; _label100 = label100; _ballGhost = ballGhost;
            _dimGroup = dimGroup;
        }
    }
}
