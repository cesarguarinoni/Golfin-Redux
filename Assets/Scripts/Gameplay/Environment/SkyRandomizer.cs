using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.Gameplay.Environment
{
    /// <summary>
    /// Picks a <see cref="SkyPreset"/> and applies it to a freshly-loaded hole scene.
    ///
    /// WHY THIS IS A RUNTIME OVERRIDE, NOT AN IMPORT-TIME CHANGE
    /// ---------------------------------------------------------
    /// HoleGeoImporter bakes Assets/Skybox/Sky-2.mat into every Hole_NN_Geo scene's
    /// RenderSettings. Changing the sky by re-importing would mean re-importing shipped
    /// holes, which wipes their tree instances and bot bake data. So the skybox is
    /// overridden at load time instead and the hole scenes are never touched.
    ///
    /// DETERMINISM
    /// -----------
    /// The draw is seeded from <see cref="RoundSeed"/> + the hole number, never from
    /// unseeded Random. That means:
    ///   - re-entering the same hole in the same round shows the SAME sky (no flicker
    ///     between a retry and the shot you just took),
    ///   - a new round rolls a new sky,
    ///   - both players in a 1v1 see the same sky as soon as the match seeds RoundSeed
    ///     from a shared match id.
    /// </summary>
    public static class SkyRandomizer
    {
        /// <summary>
        /// Per-round salt. Set this from a shared match id when multiplayer needs both
        /// clients to agree on the weather; otherwise it self-seeds once per app launch.
        /// </summary>
        public static int RoundSeed
        {
            get
            {
                if (!s_roundSeedSet) SetRoundSeed(Random.Range(int.MinValue, int.MaxValue));
                return s_roundSeed;
            }
        }

        /// <summary>The preset applied by the last successful Apply call. Null if none.</summary>
        public static SkyPreset Current { get; private set; }

        /// <summary>Yaw actually applied to sky + sun by the last Apply call, in degrees.</summary>
        public static float CurrentYawOffset { get; private set; }

        static int  s_roundSeed;
        static bool s_roundSeedSet;

        // One runtime clone per preset material, reused across holes. Cloning is required
        // because Apply writes _Rotation, and writing that on the shared asset material
        // would dirty the .mat on disk in the editor and persist between sessions.
        static readonly Dictionary<Material, Material> s_runtimeClones =
            new Dictionary<Material, Material>();

        /// <summary>Seeds the round. Call once when a match/round starts.</summary>
        public static void SetRoundSeed(int seed)
        {
            s_roundSeed = seed;
            s_roundSeedSet = true;
        }

        /// <summary>Forces the next hole to roll fresh. Call when a round ends.</summary>
        public static void ResetRound()
        {
            s_roundSeedSet = false;
            Current = null;
            CurrentYawOffset = 0f;
        }

        /// <summary>
        /// Rolls a sky for <paramref name="holeNumber"/> and applies it to
        /// <paramref name="holeScene"/>. Safe to call with an invalid scene or a missing
        /// library — it simply no-ops and the hole keeps its imported sky.
        /// </summary>
        /// <returns>The applied preset, or null if nothing was applied.</returns>
        public static SkyPreset ApplyRandomTo(Scene holeScene, int holeNumber)
        {
            var library = SkyPresetLibrary.Load();
            if (library == null || !library.RandomizationEnabled) return null;

            var drawable = library.GetDrawablePresets();
            if (drawable.Count == 0)
            {
                Debug.LogWarning("[SkyRandomizer] Library has no drawable presets; " +
                                 "keeping the hole's imported sky.");
                return null;
            }

            // Two independent draws off one deterministic stream: which sky, and how far
            // to rotate it. Separate offsets so adding jitter later does not change which
            // sky a given round/hole picks.
            uint stream = Hash((uint)RoundSeed, (uint)holeNumber);
            var preset  = PickWeighted(drawable, Fraction(Hash(stream, 0x9E3779B9u)));

            float yaw = 0f;
            if (library.YawJitterDegrees > 0f)
            {
                float t = Fraction(Hash(stream, 0x85EBCA6Bu));
                yaw = Mathf.Lerp(-library.YawJitterDegrees, library.YawJitterDegrees, t);
            }

            return ApplyTo(holeScene, preset, yaw) ? preset : null;
        }

        /// <summary>
        /// Applies a specific preset. Exposed for the editor preview tool and for tests.
        /// </summary>
        /// <returns>True when the sky was actually changed.</returns>
        public static bool ApplyTo(Scene holeScene, SkyPreset preset, float yawOffsetDegrees = 0f)
        {
            if (preset == null || !preset.IsUsable)
            {
                Debug.LogWarning("[SkyRandomizer] Preset is null or has no skybox material.");
                return false;
            }

            // ── Sky ────────────────────────────────────────────────────────────────
            Material material = preset.SkyboxMaterial;
            if (!Mathf.Approximately(yawOffsetDegrees, 0f))
            {
                material = GetRuntimeClone(preset.SkyboxMaterial);
                if (material.HasProperty(RotationId))
                    material.SetFloat(RotationId,
                        Mathf.Repeat(preset.SkyboxMaterial.GetFloat(RotationId) + yawOffsetDegrees, 360f));
            }
            RenderSettings.skybox = material;

            // ── Sun ────────────────────────────────────────────────────────────────
            // Scoped to the hole scene: ShellScene has its own lights and must not be
            // rotated out from under the menu UI.
            var sun = FindDirectionalLight(holeScene);
            if (sun != null)
            {
                var euler = preset.SunEuler;
                euler.y = Mathf.Repeat(euler.y + yawOffsetDegrees, 360f);
                sun.transform.rotation = Quaternion.Euler(euler);
                sun.color     = preset.SunColor;
                sun.intensity = preset.SunIntensity;

                // The hole scenes ship m_Sun: 0 (auto-pick). Binding it explicitly keeps
                // the ambient/GI solve pointed at the light we just moved.
                RenderSettings.sun = sun;
            }
            else
            {
                Debug.LogWarning(
                    $"[SkyRandomizer] No directional light in scene '{holeScene.name}'. " +
                    "Sky applied, but the sun still points wherever the scene left it.");
            }

            // ── Fog ────────────────────────────────────────────────────────────────
            if (preset.OverrideFog) RenderSettings.fogColor = preset.FogColor;

            // Hole scenes run AmbientMode.Skybox, so ambient light and the default
            // reflection probe are DERIVED from the skybox. Without this they keep
            // solving against the previous sky and the course is lit for the wrong
            // weather. This is the step that is easy to miss and expensive to debug.
            DynamicGI.UpdateEnvironment();

            Current = preset;
            CurrentYawOffset = yawOffsetDegrees;

            Debug.Log($"[SkyRandomizer] Applied '{preset.DisplayName}' " +
                      $"(sun {preset.SunEuler.x:0.#}° elev, yaw offset {yawOffsetDegrees:0.#}°).");
            return true;
        }

        // ── internals ──────────────────────────────────────────────────────────────

        static readonly int RotationId = Shader.PropertyToID("_Rotation");

        static Material GetRuntimeClone(Material source)
        {
            if (s_runtimeClones.TryGetValue(source, out var clone) && clone != null)
                return clone;

            clone = new Material(source) { name = source.name + " (runtime)" };
            s_runtimeClones[source] = clone;
            return clone;
        }

        static Light FindDirectionalLight(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;

            Light best = null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var lights = roots[i].GetComponentsInChildren<Light>(includeInactive: true);
                for (int j = 0; j < lights.Length; j++)
                {
                    if (lights[j].type != LightType.Directional) continue;
                    // Prefer the brightest, mirroring Unity's own auto-sun pick.
                    if (best == null || lights[j].intensity > best.intensity) best = lights[j];
                }
            }
            return best;
        }

        static SkyPreset PickWeighted(List<SkyPreset> presets, float t01)
        {
            float total = 0f;
            for (int i = 0; i < presets.Count; i++) total += presets[i].Weight;
            if (total <= 0f) return presets[0];

            float cursor = t01 * total;
            for (int i = 0; i < presets.Count; i++)
            {
                cursor -= presets[i].Weight;
                if (cursor <= 0f) return presets[i];
            }
            return presets[presets.Count - 1];
        }

        // Hand-rolled so the stream is stable across platforms and Unity versions —
        // string/object GetHashCode is explicitly not guaranteed to be.
        static uint Hash(uint a, uint b)
        {
            unchecked
            {
                uint h = a * 0x9E3779B1u ^ (b + 0x85EBCA77u + (a << 6) + (a >> 2));
                h ^= h >> 16; h *= 0x7FEB352Du;
                h ^= h >> 15; h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        static float Fraction(uint h) => (h & 0x00FFFFFFu) / (float)0x01000000u;
    }
}
