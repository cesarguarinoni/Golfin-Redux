using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls.Pendulum
{
    /// <summary>
    /// The alpha half of both Pendulum overlays: visible while the player is swinging, gone
    /// otherwise, lerped at the same rate the cone fades at.
    ///
    /// <para>REUSES <c>ConeFadeInSeconds</c> / <c>ConeFadeOutSeconds</c> rather than adding a
    /// Pendulum pair (scheme_pendulum §3.3). Those two numbers are not "the cone's timing", they
    /// are how fast shot-control chrome appears and disappears in this game; a second pair would
    /// be two values to keep equal by hand. Deliberately NOT a subclass of
    /// <c>ConeAlphaController</c> — that component's target table has an idle alpha (the cone is
    /// faintly visible at rest) and these overlays must be fully invisible at rest.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class PendulumFadingView : MonoBehaviour
    {
        private CanvasGroup _group;
        private float       _target;
        private readonly ControlsConfig _cfg = ControlsConfig.Default;

        protected CanvasGroup Group
        {
            get
            {
                if (_group == null) _group = GetComponent<CanvasGroup>();
                return _group;
            }
        }

        /// <summary>Snap to invisible. Called when the scheme deactivates or a swing resets, where
        /// a lerp would leave the overlay hanging over the next shot's first frames.</summary>
        public virtual void HideImmediate()
        {
            _target = 0f;
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
        }

        /// <summary>Drive visibility from the shot state. Fades OUT at Resolving as well as Idle:
        /// once the ball is in the air the bar is stale information.</summary>
        public void ApplyState(ShotState state)
        {
            _target = state switch
            {
                ShotState.Pulling  => 1f,
                ShotState.Timing   => 1f,
                ShotState.Flicking => 1f,
                _                  => 0f,
            };
        }

        protected virtual void Update()
        {
            var g = Group;
            if (Mathf.Approximately(g.alpha, _target)) return;

            float rate = _target > g.alpha
                ? 1f / Mathf.Max(_cfg.ConeFadeInSeconds,  0.001f)
                : 1f / Mathf.Max(_cfg.ConeFadeOutSeconds, 0.001f);

            g.alpha = Mathf.MoveTowards(g.alpha, _target, rate * Time.deltaTime);
        }
    }
}
