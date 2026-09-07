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
        [Tooltip("Optional second group handed this view's alpha every frame. For children that " +
                 "must fade WITH the view but draw ABOVE something that is not part of it — the " +
                 "tick labels, which sit over the club head yet belong to the lane.")]
        [SerializeField] private CanvasGroup _mirrorGroup;

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
            Mirror();
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
            if (!Mathf.Approximately(g.alpha, _target))
            {
                float rate = _target > g.alpha
                    ? 1f / Mathf.Max(_cfg.ConeFadeInSeconds,  0.001f)
                    : 1f / Mathf.Max(_cfg.ConeFadeOutSeconds, 0.001f);

                g.alpha = Mathf.MoveTowards(g.alpha, _target, rate * Time.deltaTime);
            }

            // Outside the early-out on purpose: a mirror that starts out of sync (a fresh scene,
            // a group someone left at 1) has to converge even on the frames this view is settled.
            Mirror();
        }

        /// <summary>
        /// Hand the mirror this view's alpha.
        ///
        /// <para>Exists because draw order and fade group are the same thing in uGUI and the tick
        /// labels need to be in two places at once: ABOVE the club head, which is a sibling of
        /// this view rather than part of it, and INVISIBLE whenever this view is. Lifting them out
        /// of the lane fixed the first and broke the second — they then showed at rest and through
        /// the whole ball flight (Cesar, 2026-09-07). A second group, driven from the one alpha
        /// that already exists, is cheaper than teaching them to fade on their own.</para>
        /// </summary>
        private void Mirror()
        {
            if (_mirrorGroup == null) return;
            if (Mathf.Approximately(_mirrorGroup.alpha, Group.alpha)) return;
            _mirrorGroup.alpha = Group.alpha;
        }
    }
}
