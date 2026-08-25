using UnityEngine;

namespace Golfin.Gameplay.Environment
{
    /// <summary>
    /// One complete "look" for the sky: the skybox material PLUS the directional-light
    /// setup that belongs with it.
    ///
    /// The two are authored together on purpose. Hole scenes run with
    /// <c>AmbientMode.Skybox</c>, so swapping only the skybox material silently rewrites
    /// the whole scene's ambient light while leaving the sun pointing wherever the scene
    /// left it — a sunset sky lit by a noon sun. Every preset therefore carries the sun
    /// angle, colour and intensity that match its own HDRI.
    ///
    /// Sun angles for the Poly Haven presets were derived from the HDRIs themselves
    /// (luminance centroid of the sun disc, converted through Unity's lat-long mapping),
    /// so the visible sun in the sky and the direction shadows fall actually agree.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Golfin/Environment/Sky Preset",
        fileName = "SkyPreset_New")]
    public class SkyPreset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string _displayName = "Untitled Sky";
        [SerializeField, TextArea(2, 4)] string _notes = "";

        [Header("Sky")]
        [Tooltip("Skybox material. Skybox/Cubemap for the HDRI presets.")]
        [SerializeField] Material _skyboxMaterial;

        [Header("Sun (matched to the HDRI)")]
        [Tooltip("Directional-light euler angles. X = sun elevation, Y = compass bearing.")]
        [SerializeField] Vector3 _sunEuler = new Vector3(45f, 135f, 0f);
        [SerializeField, ColorUsage(false)] Color _sunColor = new Color(1f, 0.96f, 0.88f, 1f);
        [SerializeField, Range(0f, 3f)] float _sunIntensity = 1.2f;

        [Header("Fog (optional)")]
        [Tooltip("Leave off to keep whatever fog the hole scene ships with.")]
        [SerializeField] bool _overrideFog;
        [SerializeField, ColorUsage(false)] Color _fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Play-line guard override")]
        [Tooltip(
            "How far this sky's sun must stay from the hole's play line, in degrees. " +
            "Negative = use the library default.\n\n" +
            "Per preset because glare is a property of the HDRI, not of the guard. " +
            "Measured over the player's portrait frustum, p99 luminance relative to each " +
            "sky's own median falls off at wildly different rates: MorningClear is 41x the " +
            "median looking straight at the sun and still 10x at 32 degrees, while " +
            "MorningCloudy is a flat 1.2x at every offset. One global clearance therefore " +
            "either blows out the low-sun clear skies or pointlessly rotates the overcast " +
            "ones away from a sun that isn't there. 0 disables the guard for this preset.")]
        [SerializeField, Range(-1f, 90f)] float _minSunAngleFromPlayLine = -1f;

        [Header("Selection")]
        [Tooltip("Uncheck to keep the asset around but exclude it from random draws.")]
        [SerializeField] bool _enabledInRotation = true;

        [Tooltip("Relative draw weight. 2 is twice as likely as 1; 0 never draws.")]
        [SerializeField, Min(0f)] float _weight = 1f;

        public string   DisplayName       => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string   Notes             => _notes;
        public Material SkyboxMaterial    => _skyboxMaterial;
        public Vector3  SunEuler          => _sunEuler;
        public Color    SunColor          => _sunColor;
        public float    SunIntensity      => _sunIntensity;
        public bool     OverrideFog       => _overrideFog;
        public Color    FogColor          => _fogColor;
        public bool     EnabledInRotation => _enabledInRotation;
        public float    Weight            => _weight;

        /// <summary>
        /// Per-preset play-line clearance in degrees, or negative to defer to
        /// <see cref="SkyPresetLibrary.MinSunAngleFromPlayLine"/>.
        /// </summary>
        public float MinSunAngleFromPlayLine => _minSunAngleFromPlayLine;

        /// <summary>True when this preset overrides the library's clearance.</summary>
        public bool HasMinSunAngleOverride => _minSunAngleFromPlayLine >= 0f;

        /// <summary>True when this preset can actually be applied.</summary>
        public bool IsUsable => _skyboxMaterial != null;

        /// <summary>True when this preset may be picked by a random draw.</summary>
        public bool IsDrawable => IsUsable && _enabledInRotation && _weight > 0f;
    }
}
