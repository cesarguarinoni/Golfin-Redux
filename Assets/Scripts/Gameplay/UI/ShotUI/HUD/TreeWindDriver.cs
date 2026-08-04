using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    /// <summary>
    /// Drives foliage sway on <c>Custom/Vegetation</c> tree materials from the hole's REAL wind
    /// (<see cref="WindContext.SpeedMph"/>), so the trees agree with the wind indicator.
    ///
    /// Mapping: 0 mph -> trees fully static. <see cref="MaxWindMph"/> (and above) -> <see cref="MaxTreeWindSpeed"/>.
    ///
    /// No scene wiring: it hooks <see cref="WindContext.OnChanged"/> via RuntimeInitializeOnLoadMethod and
    /// discovers materials from the active terrain's tree prototypes, so it covers every hole automatically.
    ///
    /// EDITOR SAFETY: writing to a shared material at runtime would otherwise leave the hole-specific value
    /// baked into the .mat asset on disk. The authored value is cached on first write and restored by
    /// TreeWindDriverEditorGuard when play mode exits. In a player build there is no disk write, so this is
    /// a no-op there.
    /// </summary>
    public static class TreeWindDriver
    {
        public const string VegetationShaderName = "Custom/Vegetation";

        /// Wind speed (mph) at which foliage reaches <see cref="MaxTreeWindSpeed"/>. Tune here.
        /// 11.0 = the windiest hole in HoleDatabase.csv (Hole 17, fully open), so that hole hits
        /// exactly MaxTreeWindSpeed and every other hole scales below it. Per-hole winds are modelled
        /// on the real climate at Kameyama, Mie (34.907N 136.432E): 6.4 mph calmest month -> 10.2 windiest.
        public static float MaxWindMph = 11f;

        /// Shader "Wind Speed" value at <see cref="MaxWindMph"/>. 0.4 per design.
        public static float MaxTreeWindSpeed = 0.4f;

        static readonly int WindSpeedId = Shader.PropertyToID("WindSpeedFloat1");
        static readonly Dictionary<Material, float> _authored = new Dictionary<Material, float>();
        static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            // Statics survive nothing across domain reload; re-arm cleanly every run.
            _authored.Clear();
            if (!_subscribed)
            {
                WindContext.OnChanged += Apply;
                _subscribed = true;
            }
            Apply();
        }

        /// Map current WindContext speed onto every Custom/Vegetation material on the active terrain.
        public static void Apply()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) return;

            float t     = MaxWindMph <= 0f ? 0f : Mathf.Clamp01(WindContext.SpeedMph / MaxWindMph);
            float speed = MaxTreeWindSpeed * t;

            foreach (var proto in terrain.terrainData.treePrototypes)
            {
                if (proto.prefab == null) continue;
                foreach (var r in proto.prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null || m.shader == null) continue;
                        if (m.shader.name != VegetationShaderName) continue;
                        if (!m.HasProperty(WindSpeedId)) continue;

                        if (!_authored.ContainsKey(m)) _authored[m] = m.GetFloat(WindSpeedId);
                        m.SetFloat(WindSpeedId, speed);
                    }
                }
            }
        }

        /// Put every touched material back to its authored "Wind Speed". Editor-only concern; see class docs.
        public static void RestoreAuthored()
        {
            foreach (var kv in _authored)
                if (kv.Key != null) kv.Key.SetFloat(WindSpeedId, kv.Value);
            _authored.Clear();
        }
    }
}
