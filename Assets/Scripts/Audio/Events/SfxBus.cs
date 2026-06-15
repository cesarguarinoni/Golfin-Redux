using System;

namespace Golfin.Audio.Events
{
    /// <summary>
    /// Static publish-only bus for SFX events.
    ///
    /// Pattern matches StatProviderBus in Golfin.Gameplay.Defaults:
    ///   - Any assembly adds a reference to Golfin.Audio.Events and calls SfxBus.Play(id).
    ///   - SfxPlayer (Assembly-CSharp, DontDestroyOnLoad) subscribes OnPlay and resolves
    ///     SfxId → AudioClip → AudioManager.PlaySFX.
    ///   - The [RuntimeInitializeOnLoadMethod] reset lives in SfxBusReset.cs (Assembly-CSharp)
    ///     because [RuntimeInitializeOnLoadMethod] requires UnityEngine references which this
    ///     leaf asmdef (noEngineReferences=true) cannot have.
    ///
    /// Thread safety: all Unity audio is main-thread; event is not thread-safe by design.
    /// </summary>
    public static class SfxBus
    {
        /// <summary>
        /// Subscribe to receive SFX play requests.
        /// Subscribe in OnEnable, unsubscribe in OnDisable — do NOT subscribe twice.
        /// </summary>
        public static event Action<SfxId> OnPlay;

        /// <summary>
        /// Per-event gate query provider. Set by SfxPlayer (Assembly-CSharp) in OnEnable.
        /// Queried by BallAudioEmitter (Golfin.Physics.Viewer) to check velocity gates,
        /// play-rate caps, and inter-SFX intervals without a direct cross-assembly reference.
        /// Null when SfxPlayer hasn't registered yet — callers should use safe defaults.
        /// </summary>
        public static ISfxGates Gates { get; set; }

        /// <summary>
        /// Publish an SFX play request to all subscribers.
        /// Call-site is fire-and-forget; subscriber resolves the clip.
        /// </summary>
        public static void Play(SfxId id)
        {
            OnPlay?.Invoke(id);
        }

        /// <summary>
        /// Called by SfxBusReset (Assembly-CSharp) via [RuntimeInitializeOnLoadMethod] on
        /// each play-session start. Clears all subscribers so domain-reload-off environments
        /// (mobile / IL2CPP) don't accumulate stale per-session subscriptions.
        /// </summary>
        public static void ClearSubscribers()
        {
            OnPlay = null;
            Gates  = null;
        }
    }
}
