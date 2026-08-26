using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Golfin.Gameplay.UI.Quality
{
    /// <summary>
    /// Owns the live quality tier: resolves it at boot, applies it, persists the player's override,
    /// and tells hole-scoped systems when it changes.
    ///
    /// BOOT ORDER. This runs at <see cref="RuntimeInitializeLoadType.AfterSceneLoad"/>, deliberately
    /// AFTER <c>FramePacingBootstrap</c> (BeforeSceneLoad, pins 60). FramePacingBootstrap stays: it
    /// guarantees a sane 60 even if this service throws, and its own comment always named the tier
    /// work as the thing that would override it. Low then drops the rate to 30 here.
    ///
    /// THE URP ASSET SWAP IS THE MECHANISM. <c>QualitySettings.SetQualityLevel((int)tier, true)</c>
    /// points the pipeline at that level's <c>customRenderPipeline</c>, which carries render scale,
    /// shadow cascades/distance/resolution and HDR. Nothing here writes those individually, so a
    /// tier can never half-apply.
    ///
    /// PERSISTENCE IS PlayerPrefs, NOT SaveData (AudioManager:139 precedent). Tier is a property of
    /// the DEVICE, like volume and language — an iPhone 11 and an iPhone 16 signed into the same
    /// account must not share one setting, and it has to be readable before any account exists.
    /// </summary>
    public static class QualityTierService
    {
        /// <summary>PlayerPrefs key. -1 (or absent) = Auto; 0/1/2 = a pinned <see cref="QualityTier"/>.</summary>
        public const string PrefKey = "golfin.qualityTier";

        /// <summary>Auto sentinel for <see cref="SetOverride"/> and the Settings submenu.</summary>
        public const int AutoPref = -1;

        public static QualityTier Current { get; private set; } = QualityTier.Mid;

        /// <summary>True when <see cref="Current"/> came from the player's Settings choice, not the device table.</summary>
        public static bool IsOverride { get; private set; }

        /// <summary>The tier the DEVICE resolves to, ignoring any override. Drives the "Auto (High)" label.</summary>
        public static QualityTier AutoTier { get; private set; } = QualityTier.Mid;

        /// <summary>Fired whenever the applied tier changes. Hole-scoped effects (tree wind) re-apply from here.</summary>
        public static event Action<QualityTier> OnTierChanged;

        static bool _booted;
        static bool _awaitingShellScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            _booted = false;                       // statics do not survive a domain reload; re-arm cleanly
            _awaitingShellScene = false;
            Apply(ResolveEffective(), fromBoot: true);
            _booted = true;
        }

        /// <summary>The stored override when it is a real tier, otherwise the device resolution.</summary>
        static QualityTier ResolveEffective()
        {
            AutoTier = QualityTierResolver.Resolve(out string autoReason);
            _lastAutoReason = autoReason;

            int pref = PlayerPrefs.GetInt(PrefKey, AutoPref);
            if (pref >= (int)QualityTier.Low && pref <= (int)QualityTier.High)
            {
                IsOverride = true;
                return (QualityTier)pref;
            }

            IsOverride = false;
            return AutoTier;
        }

        static string _lastAutoReason = "unresolved";

        /// <summary>
        /// Settings -> Graphics. <paramref name="prefValue"/> is -1 (Auto) or 0/1/2.
        /// Persists immediately — a tier the player picked must survive a crash, not just a clean quit.
        /// </summary>
        public static void SetOverride(int prefValue)
        {
            if (prefValue < (int)QualityTier.Low || prefValue > (int)QualityTier.High)
                prefValue = AutoPref;

            PlayerPrefs.SetInt(PrefKey, prefValue);
            PlayerPrefs.Save();

            Apply(ResolveEffective(), fromBoot: false);
        }

        /// <summary>The persisted choice as the submenu wants it: -1 Auto, else 0/1/2.</summary>
        public static int GetOverridePref()
        {
            int pref = PlayerPrefs.GetInt(PrefKey, AutoPref);
            return (pref >= (int)QualityTier.Low && pref <= (int)QualityTier.High) ? pref : AutoPref;
        }

        static void Apply(QualityTier tier, bool fromBoot)
        {
            bool changed = !_booted || tier != Current;

            // applyExpensiveChanges:true is what actually re-points the render pipeline asset. It is
            // safe on Home and mid-hole — URP re-reads the asset on the next frame; nothing is
            // reloaded and no content is touched.
            QualitySettings.SetQualityLevel((int)tier, applyExpensiveChanges: true);

            // Low is a 30 fps tier by decision (Cesar, 2026-08-26): a stable 30 beats a 45 that
            // stutters, and it halves the thermal load that Phase 1 could not fix.
            Application.targetFrameRate = tier == QualityTier.Low ? 30 : 60;

            Current = tier;

            ApplyShellCameraPostProcessing(tier);

            Debug.Log($"[QualityTier] resolved={tier} source={(IsOverride ? "override" : "auto")} " +
                      $"device={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName} " +
                      $"mem={SystemInfo.systemMemorySize} reason={(IsOverride ? "player-override(auto=" + AutoTier + ")" : _lastAutoReason)} " +
                      $"qualityLevel={QualitySettings.GetQualityLevel()} targetFrameRate={Application.targetFrameRate} " +
                      $"renderScale={CurrentRenderScale():F2} fromBoot={fromBoot}");

            if (changed) OnTierChanged?.Invoke(tier);
        }

        static float CurrentRenderScale()
        {
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            return asset != null ? asset.renderScale : -1f;
        }

        // ── Shell camera post-processing (spec §3) ──────────────────────────────────────
        //
        // Bloom on the Home screen is a High-only luxury. HDR rides on the pipeline asset, but
        // post-processing is a CAMERA flag, so it has to be poked directly.
        //
        // FindObjectsByType(..., FindObjectsInactive.Include) rather than Camera.allCameras (which
        // DisableShellCamera uses): PhysicsLabController DISABLES the shell camera for the duration
        // of a hole, and allCameras only returns enabled ones. Switching tier mid-hole would
        // otherwise silently miss the camera and leave Home wrong after the player quits out.
        static void ApplyShellCameraPostProcessing(QualityTier tier)
        {
            bool wantPost = tier == QualityTier.High;
            int applied = 0;

            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var cam in cameras)
            {
                if (cam == null || cam.gameObject.scene.name != "ShellScene") continue;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.renderPostProcessing = wantPost;
                applied++;
            }

            if (applied > 0)
            {
                Debug.Log($"[QualityTier] Shell camera post-processing {(wantPost ? "ON" : "OFF")} on {applied} camera(s).");
                return;
            }

            // ShellScene is loaded by the bootstrap flow and may not exist yet at AfterSceneLoad.
            // Arm a one-shot so the flag lands the moment it does, instead of leaving Home with the
            // wrong post stack until the next tier change.
            if (!_awaitingShellScene)
            {
                _awaitingShellScene = true;
                SceneManager.sceneLoaded += OnSceneLoadedRetry;
                Debug.Log("[QualityTier] No ShellScene camera yet — will apply post-processing when ShellScene loads.");
            }
        }

        static void OnSceneLoadedRetry(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "ShellScene") return;
            SceneManager.sceneLoaded -= OnSceneLoadedRetry;
            _awaitingShellScene = false;
            ApplyShellCameraPostProcessing(Current);
        }
    }
}
