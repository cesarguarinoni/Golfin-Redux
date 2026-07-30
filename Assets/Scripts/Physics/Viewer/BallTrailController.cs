using UnityEngine;
using UnityEngine.Rendering;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Input;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Attaches a color-coded TrailRenderer ribbon to the live ball.
    /// Lives on the same GameObject as BallAnimator (persists across ball respawns).
    /// Wire via PhysicsLabController.Awake() → Configure(anim, sm, shot).
    ///
    /// Color states:
    ///   Blue  (#2E9BFF) — normal flight + roll
    ///   Red   (#FF2E2E) — ball ended OB (whole ribbon flips at OB transition)
    ///   Gold  (#FFD24A) — perfect shot (full-swing, zero aim degradation, not a putt)
    ///
    /// OB overrides perfect: a perfect-tempo shot that still lands OB flips red.
    /// </summary>
    public class BallTrailController : MonoBehaviour
    {
        [Header("Trail Colors")]
        [SerializeField] Color _flightColor  = new Color(0x2E / 255f, 0x9B / 255f, 0xFF / 255f, 1f);  // #2E9BFF blue
        [SerializeField] Color _obColor      = new Color(0xFF / 255f, 0x2E / 255f, 0x2E / 255f, 1f);  // #FF2E2E red
        [SerializeField] Color _perfectColor = new Color(0xFF / 255f, 0xD2 / 255f, 0x4A / 255f, 1f);  // #FFD24A gold

        [Header("Trail Material")]
        [SerializeField] Material _trailMaterial;

        [Header("Trail Tuning")]
        [SerializeField] float _time              = 8f;
        [SerializeField] float _minVertexDistance = 0.3f;
        [SerializeField] float _startWidth        = 0.043f;  // fallback if the ball has no Renderer
        [SerializeField, Range(0.15f, 1f)] float _ballWidthFraction = 0.45f;  // ribbon width = ball diameter × this (thinner than the ball)

        // ── Runtime refs ───────────────────────────────────────────────────────
        BallAnimator      _anim;
        BallStateMachine  _sm;
        ShotController    _shot;
        TrailRenderer     _tr;

        // MPB used for whole-ribbon recolor (hits all already-laid segments)
        MaterialPropertyBlock _mpb;
        Color                 _currentRibbonColor;

        // ── Public config API ──────────────────────────────────────────────────

        /// <summary>
        /// Called by PhysicsLabController.Awake() after creating the BallStateMachine.
        /// Idempotent: safe to call on re-wire after domain reload.
        /// </summary>
        public void Configure(BallAnimator anim, BallStateMachine sm, ShotController shot)
        {
            _anim = anim;
            _shot = shot;

            // Idempotent re-wire: unsubscribe first to avoid double-subscribe.
            if (_sm != null) _sm.OnStateChanged -= HandleStateChanged;
            _sm = sm;
            if (_sm != null) _sm.OnStateChanged += HandleStateChanged;

            _mpb = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnStateChanged -= HandleStateChanged;
        }

        // ── State handler ──────────────────────────────────────────────────────

        void HandleStateChanged(BallStateChange c)
        {
            if (c.Next == BallState.Flying && c.Previous == BallState.Aiming)
            {
                // Shot just launched — grab live ball, ensure trail, start emitting.
                Transform ball = _anim != null ? _anim.CurrentBall : null;
                if (ball != null)
                {
                    EnsureTrail(ball);
                    if (_tr != null)
                    {
                        _tr.Clear();
                        _tr.emitting = true;
                        Color startColor = (_shot != null && _shot.LastShotWasClean)
                            ? _perfectColor
                            : _flightColor;
                        SetRibbonColor(startColor);
                    }
                }
            }
            else if (c.Next == BallState.OB)
            {
                // Whole ribbon flips red; stop emitting new segments.
                if (_tr != null)
                {
                    SetRibbonColor(_obColor);
                    _tr.emitting = false;
                }
            }
            else if (c.Next == BallState.AtRest || c.Next == BallState.InCup)
            {
                // Ball at rest — stop emitting. Ribbon is wiped on the Aiming handler
                // (ReArm), so it never bleeds into the next shot's aiming phase.
                if (_tr != null)
                    _tr.emitting = false;
            }
            else if (c.Next == BallState.Aiming)
            {
                // ReArm: wipe the previous shot's ribbon immediately so it does not
                // bleed into the next shot's aiming phase (H3 fix — ball_trail_shot_isolation).
                if (_tr != null)
                {
                    _tr.Clear();
                    _tr.emitting = false;
                }
            }
        }

        // ── Trail lifecycle ────────────────────────────────────────────────────

        /// <summary>
        /// Ensures a TrailRenderer exists as a child of the ball Transform.
        /// Idempotent: does nothing if one is already attached.
        /// Covers both prefab and fallback-sphere paths — no BallAnimator change needed.
        /// </summary>
        void EnsureTrail(Transform ball)
        {
            _tr = ball.GetComponentInChildren<TrailRenderer>();
            if (_tr != null) return;

            // Emit from the ball's VISUAL center: attach the trail to the MeshRenderer's
            // GameObject, not the ball root. If the prefab's mesh sits on a child offset from
            // the root pivot, attaching to the root makes the ribbon emit beside the ball
            // (the ball reads as off-centre / left of the line). The mesh transform is the
            // true visual centre, so the ribbon stays centred on the ball.
            var meshR = ball.GetComponentInChildren<MeshRenderer>();
            Transform host = meshR != null ? meshR.transform : ball;
            _tr = host.gameObject.AddComponent<TrailRenderer>();

            // ── Geometry / quality ──────────────────────────────────────────
            _tr.time              = _time;
            // Uniform-width ribbon: NO corner/cap verts (those round each join into a bulge,
            // which produced the beaded/totem-pole look). Moderate vertex spacing keeps the
            // billboard quads from overlapping (another source of width fluctuation).
            _tr.minVertexDistance = 0.15f;
            _tr.numCapVertices    = 0;
            _tr.numCornerVertices = 0;
            _tr.autodestruct      = false;

            // Width: CONSTANT (flat curve), thinner than the ball so it reads as a streak,
            // not a fat band. Measure the live ball's rendered diameter and take a fraction
            // of it so the ribbon auto-scales to whatever ball prefab/scale is in use.
            // Tail dissolves via the alpha gradient (1→0) below — no width taper.
            // Width from the ball's MESH renderer (already resolved above as meshR).
            var ballRend = meshR;
            float ballDia = (ballRend != null && ballRend.bounds.size.x > 0.0001f)
                ? Mathf.Min(ballRend.bounds.size.x, ballRend.bounds.size.y, ballRend.bounds.size.z)
                : _startWidth;
            _tr.widthMultiplier = Mathf.Max(0.001f, ballDia * _ballWidthFraction);
            _tr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);

            // ── Orientation / texture ────────────────────────────────────────
            _tr.alignment    = LineAlignment.View;
            _tr.textureMode  = LineTextureMode.Stretch;

            // ── Color gradient: white RGB, alpha 1 → 0 (head → tail).
            // Actual color tint applied via MaterialPropertyBlock._BaseColor.
            // Alpha: a tad translucent at the head (~0.65) fading to 0 at the tail — like a
            // real shot tracer (semi-transparent ribbon, not a solid opaque band).
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]  { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]  { new GradientAlphaKey(0.65f, 0f),       new GradientAlphaKey(0f, 1f) }
            );
            _tr.colorGradient = grad;

            // ── Mobile perf: no shadows / lighting ──────────────────────────
            _tr.shadowCastingMode  = ShadowCastingMode.Off;
            _tr.receiveShadows     = false;
            _tr.lightProbeUsage    = LightProbeUsage.Off;
            _tr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // ── Material ─────────────────────────────────────────────────────
            if (_trailMaterial != null)
                _tr.material = _trailMaterial;
            // Render the trail ON TOP of the terrain so it never appears to dip "under" the
            // surface during the bounce/roll (ZTest = Always, drawn after opaque geometry).
            var mat = _tr.material;
            if (mat != null)
            {
                if (mat.HasProperty("_ZTest"))
                    mat.SetFloat("_ZTest", (float)CompareFunction.Always);
                mat.renderQueue = 4000;
            }

            // ── Start with emitting off; HandleStateChanged turns it on at Flying.
            _tr.emitting = false;
        }

        /// <summary>
        /// Recolors the entire existing ribbon at once via MaterialPropertyBlock,
        /// setting _BaseColor so already-laid segments recolor too.
        /// </summary>
        void SetRibbonColor(Color c)
        {
            _currentRibbonColor = c;
            if (_tr == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _tr.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _tr.SetPropertyBlock(_mpb);
        }

        // ── Internal test / bot seams ─────────────────────────────────────────

#if UNITY_EDITOR
        /// <summary>Bot seam: whether the live trail is currently emitting.</summary>
        public bool EmittingForBot => _tr != null && _tr.emitting;

        /// <summary>Bot seam: current _BaseColor in the MPB (the tint applied at draw-time).</summary>
        public Color RibbonColorForBot
        {
            get
            {
                if (_tr == null) return Color.clear;
                if (_mpb == null) return Color.clear;
                var readMpb = new MaterialPropertyBlock();
                _tr.GetPropertyBlock(readMpb);
                return readMpb.GetColor("_BaseColor");
            }
        }

        /// <summary>
        /// Capture seam: force the OB recolor on the currently-laid ribbon without
        /// needing a real OB zone in the scene. Simulates the exact same code path
        /// HandleStateChanged takes on c.Next==OB (SetRibbonColor(_obColor) + emitting=false).
        /// Used ONLY by BallTrailCaptureRunner to demonstrate the whole-ribbon recolor.
        /// </summary>
        public void ForceOBRecolorForCapture()
        {
            if (_tr == null) return;
            SetRibbonColor(_obColor);
            _tr.emitting = false;
            Debug.Log("[BallTrail] ForceOBRecolorForCapture: ribbon flipped to red, emitting=false");
        }
#endif
    }
}
