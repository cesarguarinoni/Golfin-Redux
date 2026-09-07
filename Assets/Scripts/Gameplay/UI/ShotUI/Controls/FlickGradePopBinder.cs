using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// Raises the Flick scheme's grade pop (miss_grade_duff §3.6).
    ///
    /// <para>WHY A BINDER AND NOT A DRIVER. The other three schemes pop from inside their own
    /// driver, at the line where they build the <c>ShotIntent</c> — they own the swing, so they
    /// own the word. Flick has no such line: <see cref="FlickSchemeDriver"/> is deliberately
    /// empty and <c>ShotController.CommitFlick</c> is the grader. So the pop hangs off the one
    /// signal the controller already broadcasts, the same seam <c>PowerGaugeWidget</c> uses:
    /// <c>OnStateChanged</c> reaching <c>Resolving</c>.</para>
    ///
    /// <para>NOTHING POPS FOR A BOT. <c>LastFlickGrade</c> is null whenever the swing had no
    /// timing to judge — a bot, a capture rig, an EditMode swing, <c>ForcePerfectTiming</c> —
    /// which matches the other three schemes exactly: their pops are raised by a driver, and a
    /// bot swing goes through <c>BotSwing</c> and never runs one.</para>
    /// </summary>
    public class FlickGradePopBinder : MonoBehaviour
    {
        [SerializeField] private ShotController _shotController;
        [SerializeField] private SchemeGradePop _gradePop;

        /// <summary>One pop per shot. <c>PublishState</c> fires every Tick, so Resolving is seen
        /// many times for a single shot; this is re-armed the moment the state leaves it.</summary>
        private bool _armed = true;

        private void OnEnable()
        {
            if (_shotController != null) _shotController.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_shotController != null) _shotController.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(ShotInputState state)
        {
            if (state.State != ShotState.Resolving) { _armed = true; return; }
            if (!_armed) return;
            _armed = false;

            // A REJECTED flick never reaches Resolving at all — it routes back to Idle — so
            // "no pop on a rejected flick" needs no test here: a reset is not a shot.
            var grade = _shotController != null ? _shotController.LastFlickGrade : null;
            if (grade.HasValue) _gradePop?.Show(grade.Value);
        }

        /// <summary>EditMode wiring seam — a plain MonoBehaviour gets no Awake in EditMode.</summary>
        public void ConfigureForTests(ShotController controller, SchemeGradePop pop)
        {
            _shotController = controller;
            _gradePop       = pop;
        }
    }
}
