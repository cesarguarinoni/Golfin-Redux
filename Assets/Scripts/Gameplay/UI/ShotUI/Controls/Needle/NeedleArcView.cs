using UnityEngine;
using TMPro;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.UI.Controls.Needle
{
    /// <summary>
    /// The accuracy arc above the ball (Figma <c>Needle — Timing</c> 14091:102430 and
    /// <c>Needle — Result</c> 14091:102730): a navy 180° band, the amber GOOD and blue PERFECT
    /// zones on top of it, the sweeping needle on its hub, the "TAP!" hint, and the pip that marks
    /// where the tap landed.
    ///
    /// <para>THE ZONES ARE THE WINDOWS, DRAWN — the contract <c>PendulumBarView</c> has with its
    /// bands, in polar form, and here it is exact rather than merely proportional. The needle sits
    /// at <c>n × 90°</c> and a zone of half-window <c>w</c> is drawn at <c>±w × 90°</c>: the same
    /// number, no conversion, so "inside the blue" and "graded PERFECT" are the same statement
    /// about the same geometry. The node's own widths (37.82° and 12.61°) are one sample of that
    /// formula at Accuracy ≈ 0.5 and full-width power, not authored constants — hard-coding them
    /// would draw every character the same arc while grading them differently.</para>
    ///
    /// <para>THE PUTT ARC IS AN ELLIPSE (460×300 per the Putt frame) and everything above still
    /// holds, because <see cref="NeedleArcGraphic"/> evaluates its radius along the RAY at each
    /// angle. The needle is a straight bar rotated about the ball, so the ray is where the player
    /// reads it; a parametric sweep would drift from the needle tip near the ends of a flattened
    /// arc and the picture would stop agreeing with the verdict.</para>
    /// </summary>
    public class NeedleArcView : PendulumFadingView
    {
        [Header("Figma: Scheme — Needle / Timing (14091:102430)")]
        [SerializeField] private NeedleArcGraphic _arc;
        [SerializeField] private NeedleArcGraphic _arcStrokeOuter;
        [SerializeField] private NeedleArcGraphic _arcStrokeInner;
        [SerializeField] private NeedleArcGraphic _zoneGood;
        [SerializeField] private NeedleArcGraphic _zonePerfect;
        [SerializeField] private RectTransform    _needle;
        [SerializeField] private UnityEngine.UI.Image _needleBar;
        [SerializeField] private RectTransform    _hub;
        [SerializeField] private RectTransform    _tapPip;
        [SerializeField] private TextMeshProUGUI  _tapHint;

        [Header("Geometry — node values (canvas px)")]
        [Tooltip("AccuracyArc is 460x460 on a swing: outer radius 230.")]
        [SerializeField] private float _swingRadius     = 230f;
        [Tooltip("The Putt frame flattens it to 460x300: rx 230, ry 150.")]
        [SerializeField] private float _puttRadiusY     = 150f;
        [Tooltip("Arc band thickness. Figma's path is r 186..230.")]
        [SerializeField] private float _arcThickness    = 44f;
        [Tooltip("Zone band thickness. Figma's zone paths are r 190..230 — flush with the arc's " +
                 "outer edge and 4px short of its inner one, so the navy reads as a lip.")]
        [SerializeField] private float _zoneThickness   = 40f;
        [Tooltip("The arc's white edge stroke. Figma: 4px masked inside the shape = 2px visible.")]
        [SerializeField] private float _arcStrokePx     = 2f;
        [Tooltip("Needle overhang past the arc's outer edge. Figma: a 240px needle on a 230px " +
                 "radius, and a 160px needle on the putt's 150px one.")]
        [SerializeField] private float _needleOverhang  = 10f;
        [SerializeField] private float _needleWidth     = 10f;
        [Tooltip("TapHint's top edge below the ball centre. Figma: 1051 vs ball centre 961.")]
        [SerializeField] private float _tapHintBelowBall = 90f;

        [Header("Needle colour cue (white -> amber -> red by |n|)")]
        [SerializeField] private Color _needleInZone   = Color.white;
        [SerializeField] private Color _needleNearMiss = new Color(1f, 0xEB / 255f, 0xA6 / 255f);
        [SerializeField] private Color _needleMiss     = new Color(1f, 0x5A / 255f, 0x5A / 255f);

        private float _radiusX, _radiusY;
        private float _perfect01, _good01;

        /// <summary>The zones as DRAWN, in degrees of half-sweep. Read back by the tests and the
        /// acceptance run: this is the number that has to equal <c>window × 90°</c>, measured off
        /// the live graphic rather than recomputed from the formula that drew it.</summary>
        public float PerfectHalfAngleDeg => _zonePerfect != null ? _zonePerfect.HalfSweepDeg : 0f;
        public float GoodHalfAngleDeg    => _zoneGood    != null ? _zoneGood.HalfSweepDeg    : 0f;

        /// <summary>The arc's outer radii as drawn — swing 230/230, putt 230/150.</summary>
        public float ArcRadiusX => _radiusX;
        public float ArcRadiusY => _radiusY;

        /// <summary>The pip's radius: the CENTRE of the arc band, so a tap mark sits in the middle
        /// of the colour it landed on. Figma places TapPip 208px above the ball, which is exactly
        /// (186 + 230) / 2 — derived, not copied.</summary>
        public float PipRadius => _radiusY - _arcThickness * 0.5f;

        // ── Layout ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Lay the arc out for this swing. Called at Activate and whenever putt mode flips.
        /// </summary>
        public void ApplyGeometry(in ControlsConfig cfg, bool isPutt)
        {
            _radiusX = _swingRadius;
            _radiusY = isPutt ? _puttRadiusY : _swingRadius;

            if (_arc != null)
            {
                _arc.SetEllipse(_radiusX, _radiusY, _arcThickness);
                _arc.SetSweep(0f, 90f);                       // the top half
                _arc.color = NeedleColors.ArcFill;
            }
            // Two hairlines rather than an Outline component: UI Rule 21 treats an Outline as a
            // fabricated border, and the node's stroke is on the band's two EDGES, which an
            // Outline cannot draw at all on a curve.
            StrokeAt(_arcStrokeOuter, _radiusX, _radiusY);
            StrokeAt(_arcStrokeInner, _radiusX - _arcThickness + _arcStrokePx,
                                      _radiusY - _arcThickness + _arcStrokePx);

            if (_hub != null) _hub.anchoredPosition = Vector2.zero;

            if (_needle != null)
            {
                _needle.pivot    = new Vector2(0.5f, 0f);     // spins about the ball centre
                _needle.sizeDelta = new Vector2(_needleWidth, _radiusY + _needleOverhang);
                _needle.anchoredPosition = Vector2.zero;
            }

            if (_tapHint != null)
                _tapHint.rectTransform.anchoredPosition =
                    new Vector2(0f, -(_tapHintBelowBall + _tapHint.rectTransform.rect.height * 0.5f));

            SetNeedle(-1f);
            ShowTapPip(false, 0f);
            ShowTapHint(false);
        }

        private void StrokeAt(NeedleArcGraphic g, float rx, float ry)
        {
            if (g == null) return;
            g.SetEllipse(rx, ry, _arcStrokePx);
            g.SetSweep(0f, 90f);
            g.color = NeedleColors.ArcStroke;
        }

        /// <summary>
        /// Size the two zones for this swing's accuracy and power. Called every drag frame, not
        /// only at Activate: the whole point of the power shrink is that the player WATCHES the
        /// blue zone close as they pull.
        /// </summary>
        public void ApplyWindows(float perfect01, float good01, bool isPutt)
        {
            _perfect01 = perfect01;
            _good01    = good01;

            // A window is a fraction of the 90 degree half-sweep, so its drawn half-angle is that
            // fraction OF 90 DEGREES. Clamped to the arc so a mis-tuned config draws a full half
            // rather than a segment wrapping round the back of the ball.
            Zone(_zoneGood,    Mathf.Clamp01(good01),    NeedleColors.ZoneGood,    isPutt);
            Zone(_zonePerfect, Mathf.Clamp01(perfect01), NeedleColors.ZonePerfect, isPutt);
        }

        private void Zone(NeedleArcGraphic g, float window01, Color color, bool isPutt)
        {
            if (g == null) return;
            g.SetEllipse(_radiusX, _radiusY, _zoneThickness);
            g.SetSweep(0f, window01 * NeedleMath.ArcHalfSweepDeg);
            g.color = color;
        }

        // ── Needle ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Place the needle. <paramref name="n"/> is −1 (the arc's LEFT end) … +1 (its right).
        ///
        /// <para>The rotation is <c>−n × 90°</c> because Unity's z-rotation is counter-clockwise
        /// and the arc's angles run clockwise from the top: n = −1 has to put the needle at the
        /// LEFT, which is +90° of Unity rotation. Getting this backwards would swap HOOK and SLICE
        /// on screen while the grade stayed correct, which is the worst kind of wrong.</para>
        /// </summary>
        public void SetNeedle(float n)
        {
            n = Mathf.Clamp(n, -1f, 1f);
            if (_needle != null)
                _needle.localEulerAngles = new Vector3(0f, 0f, -n * NeedleMath.ArcHalfSweepDeg);
            if (_needleBar != null) _needleBar.color = NeedleColorFor(Mathf.Abs(n));
        }

        /// <summary>
        /// The needle's own colour cue: white inside the blue, amber across the amber, red past
        /// it. Driven by the LIVE zones rather than by fixed thresholds, so the cue and the grade
        /// narrow together as the player pulls — a needle that stayed white over a zone that had
        /// closed would be actively lying.
        /// </summary>
        private Color NeedleColorFor(float absN)
        {
            if (absN <= _perfect01) return _needleInZone;
            if (absN <= _good01)
                return Color.Lerp(_needleInZone, _needleNearMiss,
                                  Mathf.InverseLerp(_perfect01, _good01, absN));
            return Color.Lerp(_needleNearMiss, _needleMiss, Mathf.InverseLerp(_good01, 1f, absN));
        }

        /// <summary>Drop the pip on the arc at the angle that was tapped, or take it away.</summary>
        public void ShowTapPip(bool shown, float n)
        {
            if (_tapPip == null) return;
            if (_tapPip.gameObject.activeSelf != shown) _tapPip.gameObject.SetActive(shown);
            if (!shown) return;
            float rad = Mathf.Clamp(n, -1f, 1f) * NeedleMath.ArcHalfSweepDeg * Mathf.Deg2Rad;
            // Along the RAY at that angle, the same evaluation the arc itself uses, so the pip
            // lands on the band even when the putt's ellipse is flattened.
            float rx = _radiusX - _arcThickness * 0.5f;
            float ry = _radiusY - _arcThickness * 0.5f;
            float sx = Mathf.Sin(rad), cy = Mathf.Cos(rad);
            float denom = Mathf.Sqrt(sx * sx * ry * ry + cy * cy * rx * rx);
            float r = denom > 1e-4f ? rx * ry / denom : 0f;
            _tapPip.anchoredPosition = new Vector2(sx * r, cy * r);
        }

        /// <summary>
        /// Show or hide the tap prompt. Up for the needle phase only — before the release there is
        /// nothing to tap, and after it the prompt is stale.
        ///
        /// <para>THE WORD COMES FROM <c>SHOT_TAP_HINT</c>, resolved HERE and not cached: the
        /// builder authors a placeholder so the object is visible while it is being laid out, and
        /// without this the placeholder is what ships — which is exactly what the UI fidelity
        /// linter's <c>unlocalized-text</c> warning caught. Read at show time rather than at
        /// Awake for the same reason <see cref="SchemeGradePop"/> does: the language can change
        /// under a live screen.</para>
        /// </summary>
        public void ShowTapHint(bool shown)
        {
            if (_tapHint == null) return;
            if (shown) _tapHint.text = LocalizationManager.Get(NeedleMath.KeyTapHint);
            if (_tapHint.gameObject.activeSelf != shown) _tapHint.gameObject.SetActive(shown);
        }

        /// <summary>The prompt as it currently reads. Read back by the tests and the acceptance run
        /// so "no hardcoded text" is an assertion against the KEY's resolved value, not a claim.</summary>
        public string TapHintText => _tapHint != null ? _tapHint.text : null;

        public override void HideImmediate()
        {
            base.HideImmediate();
            SetNeedle(-1f);
            ShowTapPip(false, 0f);
            ShowTapHint(false);
        }

        /// <summary>EditMode wiring seam — the same objects the scene builder assigns. Without it
        /// a scene-less fixture drives a view whose zones are null and every angle assertion
        /// passes vacuously against 0.</summary>
        public void ConfigureForTests(NeedleArcGraphic arc, NeedleArcGraphic zoneGood,
                                      NeedleArcGraphic zonePerfect, RectTransform needle,
                                      RectTransform hub, RectTransform tapPip,
                                      TextMeshProUGUI tapHint)
        {
            _arc = arc; _zoneGood = zoneGood; _zonePerfect = zonePerfect;
            _needle = needle; _hub = hub; _tapPip = tapPip; _tapHint = tapHint;
        }

        /// <summary>The needle's live rotation about the ball, in degrees. Read back so a test can
        /// assert the ON-SCREEN direction rather than the number that was passed in.</summary>
        public float NeedleRotationDeg => _needle != null ? _needle.localEulerAngles.z : 0f;
    }
}
