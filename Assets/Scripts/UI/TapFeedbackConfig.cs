using UnityEngine;

namespace Golfin.UI
{
    /// <summary>
    /// Inspector-tweakable tuning for the global tap-feedback effect.
    ///
    /// The TapFeedbackController is runtime-bootstrapped (RuntimeInitializeOnLoadMethod), so it has
    /// no scene/prefab instance whose [SerializeField]s you could edit. This ScriptableObject is the
    /// tuning surface instead: edit the asset at
    ///   Assets/Resources/UI/TapFeedbackConfig.asset
    /// and the controller picks the values up. The controller reads this every spawn, so you can tweak
    /// the asset DURING play mode and each subsequent tap reflects the new values (no recompile needed).
    ///
    /// Defaults here match the shipped values, so a missing/blank asset behaves identically to before.
    /// </summary>
    [CreateAssetMenu(fileName = "TapFeedbackConfig", menuName = "Golfin/Tap Feedback Config", order = 0)]
    public class TapFeedbackConfig : ScriptableObject
    {
        [Header("Master")]
        [Tooltip("Global on/off for the whole tap-feedback effect.")]
        public bool enabled = true;

        [Tooltip("Number of TapFeedbackFX instances pre-instantiated in the pool (one per concurrent tap).")]
        [Min(1)] public int poolSize = 8;

        [Header("Ring (touch circle)")]
        [Tooltip("Starting diameter of the ring in canvas pixels.")]
        public float ringStartPx = 30f;
        [Tooltip("Ending diameter of the ring in canvas pixels.")]
        public float ringEndPx = 90f;
        [Tooltip("Ring expand + fade duration in seconds.")]
        [Min(0.01f)] public float ringDuration = 0.30f;
        [Tooltip("Peak opacity of the ring. Lower = more translucent / subtler.")]
        [Range(0f, 1f)] public float ringPeakAlpha = 0.4f;
        [Tooltip("Tint of the ring (RGB only — alpha is driven by ringPeakAlpha).")]
        public Color ringColor = Color.white;

        [Header("Sparkles")]
        [Tooltip("Number of sparkle particles per burst.")]
        [Min(0)] public int sparkleCount = 6;
        [Tooltip("Outward speed of sparkles in canvas pixels/second.")]
        public float sparkleSpeed = 120f;
        [Tooltip("Lifetime of each sparkle in seconds.")]
        [Min(0.01f)] public float sparkleLifetime = 0.45f;
        [Tooltip("On-screen diameter of each sparkle in canvas pixels (spec target 6-10).")]
        public float sparkleSizePx = 8f;
        [Tooltip("Initial spread radius of the burst in canvas pixels (0 = all sparkles start at the exact tap point; higher = wider scatter).")]
        [Min(0f)] public float sparkleSpreadPx = 0f;
        [Tooltip("Peak opacity of the sparkles (additive). Keep low — subtle is the target.")]
        [Range(0f, 1f)] public float sparklePeakAlpha = 0.5f;
        [Tooltip("Tint of the sparkle particles (RGB; alpha is driven by sparklePeakAlpha).")]
        public Color sparkleTint = new Color(1f, 0.95f, 0.85f, 1f); // soft gold-white

        [Header("Audio (off by default)")]
        [Tooltip("Play a soft tick on each tap. Off by default — per-tap audio reads as noise.")]
        public bool playAudio = false;
        [Tooltip("Optional click/tick clip used when Play Audio is on.")]
        public AudioClip audioClip = null;

        /// <summary>Build an in-memory config with the shipped defaults (used when no asset is found).</summary>
        public static TapFeedbackConfig CreateDefault()
        {
            return CreateInstance<TapFeedbackConfig>();
        }
    }
}
