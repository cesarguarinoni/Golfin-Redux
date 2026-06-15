using UnityEngine;
using Golfin.Audio.Events;

namespace Golfin.Audio
{
    /// <summary>
    /// Domain-reload-off safety: clears SfxBus.OnPlay at the start of each play session
    /// so stale MonoBehaviour subscriptions from a previous run don't double-fire.
    ///
    /// Lives in Assembly-CSharp (has UnityEngine references) because the leaf
    /// Golfin.Audio.Events asmdef uses noEngineReferences=true and cannot host
    /// [RuntimeInitializeOnLoadMethod] directly.
    /// </summary>
    internal static class SfxBusReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            SfxBus.ClearSubscribers();
        }
    }
}
