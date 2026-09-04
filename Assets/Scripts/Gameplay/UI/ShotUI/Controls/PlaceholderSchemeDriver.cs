using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// Stands in for a scheme whose driver has not shipped yet (Pendulum / Needle / FreeSwing).
    ///
    /// <para>It reports <see cref="IsImplemented"/> = false, which is the signal
    /// <see cref="ShotSchemeHost"/> uses to keep the Flick root live: a tester who picks
    /// Pendulum on this build still has a playable game, the preference still persists, and the
    /// telemetry row still says Pendulum — so the choice is not silently thrown away either.
    /// The scheme specs replace this component with a real driver and the fallback stops
    /// applying on its own.</para>
    /// </summary>
    public class PlaceholderSchemeDriver : MonoBehaviour, IShotSchemeDriver
    {
        [Tooltip("Which scheme this placeholder stands in for.")]
        [SerializeField] private ControlScheme _scheme = ControlScheme.Pendulum;

        public ControlScheme Scheme => _scheme;

        /// <summary>Always false — that is the whole point of this component.</summary>
        public bool IsImplemented => false;

        private bool _warned;

        public void Bind(ShotController controller) { }

        public void Activate()
        {
            // Once per activation, not once per frame: this is a note for the tester's log, not a
            // per-shot warning, and a repeated line would bury the shot logs around it.
            if (_warned) return;
            _warned = true;
            Debug.Log($"[ShotSchemeHost] scheme {_scheme} not implemented — Flick input still active.");
        }

        public void Deactivate() => _warned = false;
    }
}
