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

        /// Shader Graph "Wind Speed" on the Spruce leaves shader. It has NO keyword to switch off, so
        /// Low freezes it by zeroing this instead (quality_tiers §4).
        public const string SpruceShaderName = "Shader Graphs/Leaves_URP";
        const string SpruceWindSpeedProperty = "Vector1_b0ddedae341d4c7ba1d429299f3078ea";

        /// The material keyword the Custom/Vegetation `[Toggle(_WIND)]` drives. Since quality_tiers
        /// the shader declares it `multi_compile _ _WIND`, so BOTH variants ship and this can be
        /// toggled at runtime. Note a global Shader.DisableKeyword would NOT work: material-local and
        /// global keyword sets are OR'd, and every vegetation material ships with _WIND enabled.
        const string WindKeyword = "_WIND";

        static readonly int WindSpeedId       = Shader.PropertyToID("WindSpeedFloat1");
        static readonly int SpruceWindSpeedId = Shader.PropertyToID(SpruceWindSpeedProperty);

        static readonly Dictionary<Material, float> _authored = new Dictionary<Material, float>();
        static readonly Dictionary<Material, bool>  _authoredKeyword = new Dictionary<Material, bool>();
        static readonly Dictionary<Material, float> _authoredSpruce  = new Dictionary<Material, float>();
        static bool _subscribed;

        /// <summary>False on the Low tier: foliage is frozen and the wind vertex work stops.</summary>
        public static bool WindEnabled { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            // Statics survive nothing across domain reload; re-arm cleanly every run.
            _authored.Clear();
            _authoredKeyword.Clear();
            _authoredSpruce.Clear();
            WindEnabled = true;
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

            // On Low the tier owns the value: WindContext still changes per hole, but it must never
            // put sway back onto a frozen tree.
            float t     = MaxWindMph <= 0f ? 0f : Mathf.Clamp01(WindContext.SpeedMph / MaxWindMph);
            float speed = WindEnabled ? MaxTreeWindSpeed * t : 0f;

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

        /// <summary>
        /// Tier switch (quality_tiers §4). Low freezes foliage; Mid/High put it back.
        ///
        /// PER MATERIAL, not global: material-local and global shader keyword sets are OR'd, so
        /// <c>Shader.DisableKeyword("_WIND")</c> cannot override a material that enables it — and
        /// every Custom/Vegetation material ships with the `[Toggle(_WIND)]` on. Disabling the
        /// keyword is what actually removes the wind vertex work from the shader; zeroing the speed
        /// alone would still pay for the math.
        ///
        /// Idempotent, and safe to call with no terrain loaded (it simply finds nothing and the
        /// hole-load hook calls it again — see PhysicsLabController.ApplyTierHoleEffects).
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            WindEnabled = enabled;

            int veg = 0, spruce = 0;

            foreach (var m in VegetationMaterials())
            {
                if (!_authoredKeyword.ContainsKey(m)) _authoredKeyword[m] = m.IsKeywordEnabled(WindKeyword);

                // RE-ENABLING RESTORES THE AUTHORED STATE — it does NOT blanket-enable. Only the
                // leaf materials ship with _WIND on; bark and imposter materials author it OFF
                // (Hole 08: 7 of 14 Custom/Vegetation materials are authored off). Turning the
                // keyword on for all of them would make trunks sway and would pay for wind vertex
                // work on geometry that was never meant to have it — a Low→Mid switch would leave
                // the game in a state the authored assets never describe. In the Editor
                // TreeWindDriverEditorGuard hid this on play-mode exit; a player build has no guard.
                if (enabled && _authoredKeyword[m]) m.EnableKeyword(WindKeyword);
                else                                m.DisableKeyword(WindKeyword);

                veg++;
            }

            foreach (var m in SpruceMaterials())
            {
                if (!_authoredSpruce.ContainsKey(m)) _authoredSpruce[m] = m.GetFloat(SpruceWindSpeedId);

                // No keyword on the Shader Graph, so the vertex math still runs; the tree just stops
                // moving. Accepted — Spruce rendering is Phase 3's problem (spec §4).
                m.SetFloat(SpruceWindSpeedId, enabled ? _authoredSpruce[m] : 0f);
                spruce++;
            }

            // Re-drives WindSpeedFloat1 from WindContext (or to 0 while disabled).
            Apply();

            Debug.Log($"[TreeWindDriver] SetEnabled({enabled}) — {veg} Custom/Vegetation material(s) keyword " +
                      $"{(enabled ? "restored to authored" : "DISABLED")}, {spruce} Spruce material(s) wind speed " +
                      $"{(enabled ? "restored to authored" : "zeroed")}.");
        }

        /// Every Custom/Vegetation material on the active terrain's tree prototypes.
        static IEnumerable<Material> VegetationMaterials()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) yield break;

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
                        yield return m;
                    }
                }
            }
        }

        /// Spruce leaves materials. They are NOT reachable through the terrain tree prototypes on
        /// every hole (Hole 08 places them as scene renderers), so this scans loaded materials
        /// instead — cheap, and only ever runs on a tier switch or a hole load.
        static IEnumerable<Material> SpruceMaterials()
        {
            var all = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var m in all)
            {
                if (m == null || m.shader == null) continue;
                if (m.shader.name != SpruceShaderName) continue;
                if (!m.HasProperty(SpruceWindSpeedId)) continue;
                yield return m;
            }
        }

        /// Put every touched material back to its authored "Wind Speed" AND its authored _WIND
        /// keyword state. Editor-only concern; see class docs.
        public static void RestoreAuthored()
        {
            foreach (var kv in _authored)
                if (kv.Key != null) kv.Key.SetFloat(WindSpeedId, kv.Value);
            _authored.Clear();

            foreach (var kv in _authoredKeyword)
            {
                if (kv.Key == null) continue;
                if (kv.Value) kv.Key.EnableKeyword(WindKeyword);
                else          kv.Key.DisableKeyword(WindKeyword);
            }
            _authoredKeyword.Clear();

            foreach (var kv in _authoredSpruce)
                if (kv.Key != null) kv.Key.SetFloat(SpruceWindSpeedId, kv.Value);
            _authoredSpruce.Clear();

            WindEnabled = true;
        }
    }
}
