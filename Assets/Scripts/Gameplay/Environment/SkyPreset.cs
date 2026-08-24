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

        /// <summary>True when this preset can actually be applied.</summary>
        public bool IsUsable => _skyboxMaterial != null;

        /// <summary>True when this preset may be picked by a random draw.</summary>
        public bool IsDrawable => IsUsable && _enabledInRotation && _weight > 0f;
    }
}
