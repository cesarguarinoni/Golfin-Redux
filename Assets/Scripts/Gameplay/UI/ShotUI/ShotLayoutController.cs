using System;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Places the shot view's five movable anchors for the live control scheme
    /// (shot_view_layout §3.3): the ball, each scheme root's <c>BallSpace</c>, the action-button
    /// cluster, both selector overlays and the round power gauge.
    ///
    /// <para>MOVING THE BALL WIDGET IS THE CAMERA CHANGE. The aim camera pins the 3D ball to
    /// wherever this component leaves <c>CentralBall</c>
    /// (<c>PhysicsLabController.GetAimBallViewportY</c> feeding <c>SolveAimCameraPose</c>), so
    /// dropping the widget to viewport 0.38 pitches the camera up and brings the horizon back to
    /// the Figma framing. Nothing in <c>Assets/Scripts/Physics/</c> is touched.</para>
    ///
    /// <para>PER SCHEME, THROUGH THE HOST. <see cref="ShotSchemeHost.Apply"/> calls
    /// <see cref="Apply"/> immediately before it activates a driver, which means this reuses the
    /// host's existing "never mid-swing" deferral rather than adding a second Idle gate that
    /// could disagree with it. Every <c>BallSpace</c> is moved on every apply, not just the live
    /// one, so a later scheme switch finds its root already in place.</para>
    ///
    /// <para>Reads <see cref="ControlsConfig.Default"/> — the struct the drivers actually run on.
    /// <c>controls.csv</c> is the mirror Cesar tunes (F13 two-mirror rule); nothing calls
    /// <c>ControlsConfigLoader.Load()</c> in production today.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ShotLayoutController : MonoBehaviour
    {
        /// <summary>One scheme root's ball-relative space. The rect is a full-stretch child
        /// inserted between <c>SchemeRoot_*</c> and everything that is drawn RELATIVE TO THE
        /// BALL, so one write moves a whole scheme's geometry.</summary>
        [Serializable]
        public class BallSpaceBinding
        {
            public ControlScheme  scheme;
            public RectTransform  rect;
        }

        [Header("Canvas")]
        [Tooltip("The ROOT canvas rect. Its height is the canvas-px height every number here is in.")]
        [SerializeField] private RectTransform _canvasRect;

        [Header("Ball")]
        [Tooltip("CentralBall. The aim camera reads this rect, so this is the camera pitch.")]
        [SerializeField] private RectTransform _centralBall;
        [Tooltip("One BallSpace per scheme root, in any order — matched by the scheme field.")]
        [SerializeField] private BallSpaceBinding[] _ballSpaces = new BallSpaceBinding[0];

        [Header("Bottom baseline")]
        [Tooltip("ActionButtons_Cluster. Full-stretch, so the whole 2x2 moves by the delta between " +
                 "the authored row y and the resolved baseline — no per-button edits.")]
        [SerializeField] private RectTransform _actionButtonsCluster;
        [SerializeField] private SelectorOverlayWidget _clubSelector;
        [SerializeField] private SelectorOverlayWidget _ballSelector;

        [Header("HUD")]
        [Tooltip("PowerHUD (200x200, pivot 1,1). Re-anchored top-right so it clears the aim bar.")]
        [SerializeField] private RectTransform _powerHud;

        [Header("Lanes (D6 clamp — read, never written)")]
        [SerializeField] private PendulumLaneView  _pendulumLane;
        [SerializeField] private FreeSwingLaneView _freeSwingLane;

        /// <summary>The y the <c>ActionButtonsBuilder</c> authors the bottom row at. The builder
        /// keeps writing it and this component applies the delta, so <c>BottomBaselinePx</c> stays
        /// the single source of the number and a builder re-run cannot fight it.</summary>
        public const float AuthoredClusterBaselinePx = 96f;

        /// <summary>The live instance, so <see cref="ShotSchemeHost"/> can reach it without a
        /// serialized reference that a scene revision could leave null. There is exactly one shot
        /// canvas.</summary>
        public static ShotLayoutController Active { get; private set; }

        /// <summary>Last values applied — surfaced for the acceptance run and the reviewers'
        /// bbox checks rather than re-derived from the rects. <see cref="LastHandleYAtFullPull"/>
        /// is the one the baseline guard is actually about (D3); the lane end is reported beside
        /// it because it is the thing that visibly hangs lower.</summary>
        public float LastBallY            { get; private set; }
        public float LastBaseline         { get; private set; }
        /// <summary>The baseline in canvas y — <c>-H/2 + LastBaseline</c>, and the value both
        /// lane views are capped to.</summary>
        public float LastBaselineY        { get; private set; }
        public float LastLaneEndY         { get; private set; }
        public float LastHandleYAtFullPull { get; private set; }

        private ControlScheme _lastScheme = ControlScheme.Flick;
        private bool _hasApplied;
        private bool _applying;

        private void OnEnable()
        {
            Active = this;

            // The SERVICE, not _lastScheme: nothing orders this OnEnable against
            // ShotSchemeHost's, so if the host bound first its Apply found Active still null and
            // this is the only pass the first frame gets. Defaulting to Flick there would frame
            // the shot for the wrong scheme until the player changed it.
            Apply(ControlSchemeService.Current);
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Active, this)) Active = null;
        }

        /// <summary>Rotation, a resolution change, or the Game View aspect being switched in the
        /// Editor. The baseline and the D6 clamp are both functions of canvas height, so both
        /// have to be recomputed — this is the only reason the component needs a second entry
        /// point at all.</summary>
        private void OnRectTransformDimensionsChange()
        {
            if (!_hasApplied || _applying || !isActiveAndEnabled) return;
            Apply(_lastScheme);
        }

        /// <summary>
        /// Place everything for <paramref name="scheme"/>. Idempotent, and safe to call at any
        /// time — but production only reaches it through <see cref="ShotSchemeHost.Apply"/>, which
        /// has already decided the shot is Idle.
        /// </summary>
        public void Apply(ControlScheme scheme)
        {
            if (_canvasRect == null || _applying) return;

            float height = _canvasRect.rect.height;
            if (height <= 0f) return;   // canvas not laid out yet; OnRectTransformDimensionsChange follows

            _applying   = true;
            _lastScheme = scheme;
            ControlsConfig cfg = ControlsConfig.Default;

            float baseline  = ShotLayoutMath.Baseline(cfg.BottomBaselinePx, SafeBottomCanvasPx(height));
            float baselineY = -height * 0.5f + baseline;
            float ballY     = ResolveBallY(scheme, cfg, height, baseline);

            // The pill may not hang past the row the buttons and the overlays sit on
            // (shot_view_layout_followup §1). Set on BOTH lanes every apply, not just the live
            // one, and set BEFORE the driver's Activate reaches ApplyGeometry — which is where
            // the height is actually derived. This caps the DRAWN pill only; the pull clamp is
            // the driver's, off cfg.*Pull120Px, and neither reads LaneHeight.
            if (_pendulumLane  != null) _pendulumLane.SetLaneEndCapY(baselineY);
            if (_freeSwingLane != null) _freeSwingLane.SetLaneEndCapY(baselineY);

            bool ballMoved = _centralBall != null &&
                             !Mathf.Approximately(_centralBall.anchoredPosition.y, ballY);

            SetY(_centralBall, ballY);
            for (int i = 0; i < _ballSpaces.Length; i++)
            {
                BallSpaceBinding b = _ballSpaces[i];
                if (b != null) SetY(b.rect, ballY);
            }

            SetY(_actionButtonsCluster, baseline - AuthoredClusterBaselinePx);
            if (_clubSelector != null) _clubSelector.SetOpenBaselineY(baseline);
            if (_ballSelector != null) _ballSelector.SetOpenBaselineY(baseline);

            ApplyPowerHud(cfg, height);

            LastBallY            = ballY;
            LastBaseline         = baseline;
            LastBaselineY        = baselineY;
            LastLaneEndY         = LaneEndFor(scheme, cfg, ballY);
            LastHandleYAtFullPull = HandleYFor(scheme, cfg, ballY);
            _hasApplied  = true;
            _applying    = false;

            if (ballMoved && Application.isPlaying) NudgeAimCamera();
        }

        // ── Per-scheme numbers ───────────────────────────────────────────────────

        /// <summary>The authored anchor for a scheme. Four keys rather than one so a retune of
        /// one scheme's framing cannot move an A/B partner — all four read 0.38 today, Flick
        /// since flick_shot_view D1 cut its cone to fit.</summary>
        private static float AnchorViewportY(ControlScheme scheme, in ControlsConfig cfg)
        {
            switch (scheme)
            {
                case ControlScheme.Pendulum:  return cfg.BallAnchorViewportY_Pendulum;
                case ControlScheme.Needle:    return cfg.BallAnchorViewportY_Needle;
                case ControlScheme.FreeSwing: return cfg.BallAnchorViewportY_FreeSwing;
                default:                      return cfg.BallAnchorViewportY_Flick;
            }
        }

        private float ResolveBallY(ControlScheme scheme, in ControlsConfig cfg, float height, float baseline)
        {
            float depth = ClampDepthBelowBall(scheme, cfg);
            return ShotLayoutMath.ResolveBallY(AnchorViewportY(scheme, cfg), height, baseline,
                                               !float.IsNaN(depth), depth);
        }

        /// <summary>
        /// How far below the ball the scheme's deepest drawn thing sits — the number the D6 clamp
        /// guards. NaN for a scheme with nothing to guard.
        ///
        /// <para>Flick's is the cone BASE, straight off the two config keys rather than off a view:
        /// <see cref="ShotConeView"/> derives the mesh position from the SAME pair, so the clamp
        /// and the drawn cone cannot disagree. The two lane schemes still read their live view, for
        /// the same reason — a mirrored copy of a lane's own arithmetic would be guarding a
        /// fiction.</para>
        /// </summary>
        private float ClampDepthBelowBall(ControlScheme scheme, in ControlsConfig cfg)
        {
            if (scheme == ControlScheme.Flick)
                return ShotLayoutMath.FlickLaneDepthBelowBall(cfg.FlickConeApexGapPx,
                                                              cfg.FlickConeHeightPx);

            if (TryLaneGeometry(scheme, cfg, out float pull120, out float rest, out float _, out float _))
                return ShotLayoutMath.HandleDepthAtFullPull(pull120, rest);

            return float.NaN;
        }

        private float LaneEndFor(ControlScheme scheme, in ControlsConfig cfg, float ballY)
        {
            if (!TryLaneGeometry(scheme, cfg, out float pull120, out float rest, out float half, out float tail))
                return float.NaN;
            return ShotLayoutMath.LaneEndY(ballY, pull120, rest, half, tail);
        }

        private float HandleYFor(ControlScheme scheme, in ControlsConfig cfg, float ballY)
        {
            if (!TryLaneGeometry(scheme, cfg, out float pull120, out float rest, out float _, out float _))
                return float.NaN;
            return ShotLayoutMath.HandleYAtFullPull(ballY, pull120, rest);
        }

        /// <summary>The four numbers the lane derives its own height from — false for the two
        /// schemes that draw no PILL (Needle's ring is drawn around the ball; Flick's cone reaches
        /// the baseline but has no club-head-at-the-end geometry, so it takes the clamp through
        /// <see cref="ClampDepthBelowBall"/> instead). Read OFF THE LANE VIEW rather than mirrored
        /// here: the clamp that keeps the lane on the baseline and the pill that is drawn have to
        /// be the same arithmetic or the guard is guarding a fiction.</summary>
        private bool TryLaneGeometry(ControlScheme scheme, in ControlsConfig cfg,
                                     out float pull120, out float rest, out float half, out float tail)
        {
            pull120 = rest = half = tail = 0f;

            if (scheme == ControlScheme.Pendulum && _pendulumLane != null)
            {
                pull120 = cfg.PendulumPull120Px;
                rest    = _pendulumLane.HandleRestBelowBall;
                half    = _pendulumLane.ClubHalfHeight;
                tail    = _pendulumLane.LaneTailPx;
                return true;
            }
            if (scheme == ControlScheme.FreeSwing && _freeSwingLane != null)
            {
                pull120 = cfg.FreeSwingPull120Px;
                rest    = _freeSwingLane.HandleRestBelowBall;
                half    = _freeSwingLane.ClubHalfHeight;
                tail    = _freeSwingLane.LaneTailPx;
                return true;
            }
            return false;
        }

        // ── Writers ──────────────────────────────────────────────────────────────

        private static void SetY(RectTransform rt, float y)
        {
            if (rt == null) return;
            Vector2 p = rt.anchoredPosition;
            if (Mathf.Approximately(p.y, y)) return;
            rt.anchoredPosition = new Vector2(p.x, y);
        }

        /// <summary>
        /// The gauge is re-ANCHORED, not just moved: pinned to the canvas top-right it keeps its
        /// distance from the hole card on every aspect, where the old (1, 0.5) anchor tied it to
        /// the vertical centre — which is exactly the aim bar's row.
        /// </summary>
        private void ApplyPowerHud(in ControlsConfig cfg, float height)
        {
            if (_powerHud == null) return;

            _powerHud.anchorMin = _powerHud.anchorMax = new Vector2(1f, 1f);

            // height * (pivot.y - 0.5), not height * pivot.y: the term is the distance from the
            // rect's CENTRE up to whatever point anchoredPosition actually places, and for the
            // (1,1)-pivoted gauge that is half its height, not all of it.
            float pivotOffsetAboveCentre = _powerHud.rect.height * (_powerHud.pivot.y - 0.5f);
            float y = ShotLayoutMath.PowerHudAnchoredY(cfg.PowerGaugeViewportY, height, pivotOffsetAboveCentre);
            _powerHud.anchoredPosition = new Vector2(_powerHud.anchoredPosition.x, y);
        }

        /// <summary>The device's bottom safe-area inset expressed in canvas px. Both terms are in
        /// screen space so the ratio survives <c>Screen.height</c> reporting the Game View window
        /// size in Editor play mode.</summary>
        private static float SafeBottomCanvasPx(float canvasHeight)
        {
            if (Screen.height <= 0) return 0f;
            return Screen.safeArea.y * canvasHeight / Screen.height;
        }

        /// <summary>
        /// Re-pose the aim camera now that the ball widget has moved.
        ///
        /// <para><c>ApplyCameraYaw</c> is the single place the aim pose is solved and it re-reads
        /// the widget every call, but nothing calls it while the player is simply standing at
        /// address — <c>HandleCameraOrbit</c> only reaches it on a drag. So a scheme swapped from
        /// the in-game gear modal would move the 2D ball and leave the 3D one behind until the
        /// next pan. <c>MapViewController.WriteBackAimToPhysicsLab</c> hit the identical problem
        /// (iter-35) and solved it the identical way; this is that idiom, verbatim, and it lives
        /// here rather than in <c>Assets/Scripts/Physics/</c> per the determinism ban.</para>
        /// </summary>
        private static void NudgeAimCamera()
        {
            const System.Reflection.BindingFlags BF =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;

            var mbs = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in mbs)
            {
                if (mb.GetType().Name != "PhysicsLabController") continue;
                try
                {
                    Type t          = mb.GetType();
                    var  chaseField = t.GetField("chaseCamera", BF);
                    var  chaseComp  = chaseField?.GetValue(mb) as Component;
                    Camera cam      = chaseComp != null ? chaseComp.GetComponent<Camera>() : null;
                    var  applyYaw   = t.GetMethod("ApplyCameraYaw", BF);
                    if (cam != null && applyYaw != null) applyYaw.Invoke(mb, new object[] { cam });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ShotLayout] camera re-pose after ball move: {ex.Message}");
                }
                return;
            }
        }
    }
}
