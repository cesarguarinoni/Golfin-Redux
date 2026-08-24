using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Gameplay.Environment
{
    /// <summary>
    /// The pool of skies a hole can roll. Loaded from Resources so no scene wiring is
    /// needed — matching the project's Resources.Load convention for data assets.
    ///
    /// To add a sky: drop the HDRI in Assets/Skybox/, make a Skybox/Cubemap material,
    /// create a SkyPreset asset pointing at it, and add it to the list on this asset.
    /// Nothing else has to change.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Golfin/Environment/Sky Preset Library",
        fileName = "SkyPresetLibrary")]
    public class SkyPresetLibrary : ScriptableObject
    {
        /// <summary>Resources-relative path. Must match the asset's actual location.</summary>
        public const string ResourcePath = "Environment/SkyPresetLibrary";

        [Tooltip("Every sky the game may roll. Order is irrelevant; weight decides odds.")]
        [SerializeField] List<SkyPreset> _presets = new List<SkyPreset>();

        [Header("Global")]
        [Tooltip("Master switch. Off = hole scenes keep the sky they were imported with.")]
        [SerializeField] bool _randomizationEnabled = true;

        [Tooltip(
            "Extra random yaw applied to BOTH the skybox and the sun, so the sun lands " +
            "in a different spot relative to the hole each round. The two rotate together, " +
            "so sky and shadows stay consistent. Left at 0 by default: it changes whether " +
            "the player is hitting into the sun, which is a playability decision, not a " +
            "cosmetic one.")]
        [SerializeField, Range(0f, 180f)] float _yawJitterDegrees;

        public bool  RandomizationEnabled => _randomizationEnabled;
        public float YawJitterDegrees     => _yawJitterDegrees;

        public IReadOnlyList<SkyPreset> Presets => _presets;

        static SkyPresetLibrary s_cached;
        static bool s_loadAttempted;

        /// <summary>
        /// Loads (and caches) the library from Resources. Returns null when the asset is
        /// absent — callers treat that as "leave the scene's own sky alone".
        /// </summary>
        public static SkyPresetLibrary Load()
        {
            if (s_cached != null) return s_cached;
            if (s_loadAttempted) return null;

            s_loadAttempted = true;
            s_cached = Resources.Load<SkyPresetLibrary>(ResourcePath);

            if (s_cached == null)
                Debug.LogWarning(
                    $"[SkyPresetLibrary] No library at Resources/{ResourcePath}. " +
                    "Sky randomization is disabled; holes keep their imported skybox.");

            return s_cached;
        }

        /// <summary>Drops the cache. For tests and for edit-time asset changes.</summary>
        public static void ClearCache()
        {
            s_cached = null;
            s_loadAttempted = false;
        }

        /// <summary>Every preset that is wired up and eligible for a random draw.</summary>
        public List<SkyPreset> GetDrawablePresets()
        {
            var result = new List<SkyPreset>(_presets.Count);
            for (int i = 0; i < _presets.Count; i++)
            {
                var p = _presets[i];
                if (p != null && p.IsDrawable) result.Add(p);
            }
            return result;
        }
    }
}
