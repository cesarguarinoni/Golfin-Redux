using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls.FreeSwing
{
    /// <summary>
    /// The alpha half of the finger trace: full while the finger is down, dimmed once the shot is
    /// away, gone at Idle.
    ///
    /// <para>DELIBERATELY NOT A <c>PendulumFadingView</c>, even though it now also fades out once
    /// the ball is away. That base class is driven by <c>ShotState</c>, and <c>CommitExternal</c>
    /// reaches <c>Resolving</c> SYNCHRONOUSLY — the Needle report §10 scar, where a shared fading
    /// view dropped that scheme's arc two frames after the tap. This view is driven by the DRIVER,
    /// at the moment it decides the swing is over, which is a different fact from a state
    /// transition and is timed to the gesture rather than to the pipeline. <c>Idle</c> is still
    /// the only thing that CLEARS the points.</para>
    ///
    /// <para>The stroke's own 0.85 is NOT here — it is the vertex colour, from
    /// <see cref="FreeSwingColors.Trace"/>, exactly as the node's SVG separates
    /// <c>stroke-opacity</c> from the group's <c>opacity</c>. This class only ever drives the
    /// GROUP alpha.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FreeSwingTraceView : MonoBehaviour
    {
        [SerializeField] private FreeSwingTraceGraphic _graphic;
        [SerializeField] private CanvasGroup _group;

        [Header("Group opacity (Figma: the SVG's <g opacity>)")]
        [Tooltip("While the finger is down. The node's Backswing/Downswing frames draw the trace " +
                 "with no group opacity at all.")]
        [SerializeField] private float _swingingAlpha = 1f;
        [Tooltip("Once the ball is away — ZERO. The trace belongs to the swing, not to the shot: " +
                 "see SetResult(). The node's Result frame holds it at 0.6, and Cesar called that " +
                 "on sight; keep it a field so a retune is one Inspector value, not a code edit.")]
        [SerializeField] private float _resultAlpha = 0f;
        [Tooltip("Seconds to fade. Only the Idle path uses it now — the ball-away path SNAPS, see " +
                 "SetResult().")]
        [SerializeField] private float _fadeSeconds = 0.20f;

        private float _target;

        private CanvasGroup Group
        {
            get
            {
                if (_group == null) _group = GetComponent<CanvasGroup>();
                return _group;
            }
        }

        private void Awake()
        {
            if (_graphic == null) _graphic = GetComponentInChildren<FreeSwingTraceGraphic>(true);
            HideImmediate();
        }

        /// <summary>Redraw the whole polyline from the driver's buffer.</summary>
        public void SetPoints(IReadOnlyList<Vector2> points)
        {
            if (_graphic != null) _graphic.SetPoints(points);
        }

        /// <summary>The finger is down and the trace is live.</summary>
        public void SetSwinging()
        {
            _target = _swingingAlpha;
            Group.alpha = _swingingAlpha;      // no fade IN: the first sample must be visible now
            Group.blocksRaycasts = false;
        }

        /// <summary>
        /// The ball is away — take the trace with it.
        ///
        /// <para>THE TRACE BELONGS TO THE SWING, NOT TO THE SHOT. The node's Result frame draws it
        /// at <c>&lt;g opacity="0.6"&gt;</c> and this first shipped that way; over a real fairway
        /// it reads as a stray line hanging under a ball that has already gone, which is what
        /// Cesar called on the first clip. The analyzer chip is the result readout, and it stays
        /// (carry-over 7) — the finger's path is not, once there is no longer a club on it.</para>
        ///
        /// <para>SNAPPED, NOT FADED, and the difference is not cosmetic. A 0.2 s fade leaves the
        /// line under the ball for a dozen frames — which is exactly the frame the canonical
        /// result capture lands on, so the fade both looked wrong and made the evidence look
        /// wrong. The club head already vanishes on this same frame, so nothing pops: the swing
        /// ending is one event and its two pieces leave together.</para>
        /// </summary>
        public void SetResult()
        {
            _target = _resultAlpha;
            Group.alpha = _resultAlpha;
        }

        /// <summary>Idle: the only state that takes the trace away.</summary>
        public void HideImmediate()
        {
            _target = 0f;
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            _graphic?.Clear();
        }

        private void Update()
        {
            var g = Group;
            if (Mathf.Approximately(g.alpha, _target)) return;
            g.alpha = Mathf.MoveTowards(g.alpha, _target,
                                        Time.deltaTime / Mathf.Max(_fadeSeconds, 1e-3f));
        }

        /// <summary>Drive from the shot state — the ONE state this view listens to. Everything
        /// else is driven by the driver directly, because "the finger is down" is not a
        /// <c>ShotState</c>.</summary>
        public void ApplyState(ShotState state)
        {
            if (state == ShotState.Idle) HideImmediate();
        }

        /// <summary>How many samples are on screen, and the group alpha they are drawn at. Read
        /// back by the tests and the acceptance run.</summary>
        public int   PointCount => _graphic != null ? _graphic.PointCount : 0;
        public float Alpha      => Group.alpha;
        public float TargetAlpha => _target;

        /// <summary>EditMode wiring seam — a plain MonoBehaviour gets no Awake in EditMode.</summary>
        public void ConfigureForTests(FreeSwingTraceGraphic graphic, CanvasGroup group)
        {
            _graphic = graphic; _group = group;
        }
    }
}
