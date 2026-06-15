namespace Golfin.Audio.Events
{
    /// <summary>
    /// Cross-assembly gate query interface for BallAudioEmitter (Golfin.Physics.Viewer).
    ///
    /// BallAudioEmitter (in Golfin.Physics.Viewer, which cannot reference Assembly-CSharp)
    /// queries per-event thresholds through this interface via SfxBus.Gates.
    ///
    /// SfxPlayer (Assembly-CSharp) sets SfxBus.Gates = this on OnEnable.
    ///
    /// All methods are null-safe to call when Gates == null — BallAudioEmitter
    /// uses default fallbacks (no suppression / cap=4 / interval=0) when the
    /// SfxPlayer hasn't registered yet.
    /// </summary>
    public interface ISfxGates
    {
        /// <summary>
        /// Returns true when a landing SFX should be suppressed because the impact
        /// velocity magnitude is below the configured threshold for the given SfxId.
        /// </summary>
        bool ShouldSuppressLanding(SfxId id, float velocityMagnitude);

        /// <summary>
        /// Maximum BallAnimator.PlayRate at which per-bounce sounds are emitted.
        /// Above this rate (e.g. Instant mode), bounces are curtailed.
        /// </summary>
        float GetPlayRateCap(SfxId id);

        /// <summary>
        /// Minimum seconds that must elapse between repeated emissions of the same SfxId.
        /// </summary>
        float GetMinInterval(SfxId id);
    }
}
