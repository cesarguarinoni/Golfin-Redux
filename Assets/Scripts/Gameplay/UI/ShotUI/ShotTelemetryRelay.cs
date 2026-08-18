// Order: beta_telemetry — assembly bridge for the two ShotController telemetry signals.
using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI
{
    /// <summary>
    /// A three-line assembly bridge, and nothing else.
    ///
    /// <c>ShotController</c> lives in <c>Golfin.Gameplay.Input</c>, which is
    /// <c>autoReferenced: false</c> — Assembly-CSharp (where the telemetry hooks live)
    /// cannot see its types at all. <c>Golfin.Gameplay.UI</c> already references
    /// <c>Golfin.Gameplay.Input</c> AND is <c>autoReferenced: true</c>, so it is the one
    /// assembly that can see both sides. This class subscribes there and re-raises the
    /// identical signals where Assembly-CSharp can reach them.
    ///
    /// The alternative — flipping Input's <c>autoReferenced</c> flag — would drag the whole
    /// input assembly into every default-assembly compile for the sake of two events, so
    /// it is explicitly NOT the approach (SPEC § Architecture context).
    ///
    /// Bootstrapped at <c>SubsystemRegistration</c>, the earliest runtime hook: static event
    /// subscriptions do not survive a domain reload, and this runs after every one of them,
    /// before any <c>ShotController</c> could exist.
    /// </summary>
    public static class ShotTelemetryRelay
    {
        /// <summary>Mirrors <c>ShotController.FlickRejected</c>. Argument is the measured
        /// flick speed in screen-heights/second.</summary>
        public static event System.Action<float> FlickRejected;

        /// <summary>Mirrors <c>ShotController.ShotCancelled</c>.</summary>
        public static event System.Action ShotCancelled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            // Idempotent: unsubscribe first so a second call (or a stale subscription that
            // somehow survived) cannot double-raise.
            ShotController.FlickRejected -= OnFlickRejected;
            ShotController.FlickRejected += OnFlickRejected;
            ShotController.ShotCancelled -= OnShotCancelled;
            ShotController.ShotCancelled += OnShotCancelled;
        }

        private static void OnFlickRejected(float speed)
        {
            try { FlickRejected?.Invoke(speed); }
            catch (System.Exception ex) { Debug.LogWarning($"[ShotTelemetryRelay] FlickRejected subscriber threw: {ex.Message}"); }
        }

        private static void OnShotCancelled()
        {
            try { ShotCancelled?.Invoke(); }
            catch (System.Exception ex) { Debug.LogWarning($"[ShotTelemetryRelay] ShotCancelled subscriber threw: {ex.Message}"); }
        }
    }
}
