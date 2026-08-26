#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.Quality;
using UnityEngine.Rendering.Universal;   // GetUniversalAdditionalCameraData extension

namespace Golfin.EditorTools
{
    /// <summary>
    /// quality_tiers (9a) — the in-Editor half of the acceptance evidence.
    ///
    /// WHAT IT CANNOT DO: the tier tables, the 5-minute endurance curves and the thermal states are
    /// DEVICE numbers — <c>PerfBaselineBot</c> jobs 14-25 on a cooled iPhone produce those and
    /// nothing here substitutes for them. What the Editor CAN settle is everything that is a
    /// question about pixels and state rather than about milliseconds:
    ///
    ///   • Settings ▸ Graphics submenu, EN and JP, and the override round-trip through the REAL row.
    ///   • Home bloom: High only.
    ///   • THE FAIRNESS RULE — Low vs High at the SAME camera pose in ONE session, so the sky, the
    ///     yaw and the tree LOD selection cannot drift between the two frames the way they would
    ///     across two launches. Every tree silhouette must land on the same pixel.
    ///   • Tree wind: the _WIND keyword and wind-speed state, per material, as numbers.
    ///
    /// Both passes drive the REAL widgets (SettingsButton ▸ GraphicsRow ▸ LowButton) — a synthetic
    /// toggle would prove nothing about the row a player actually taps.
    ///
    /// Output: Docs/Diagnostics/_capture/ (CaptureCore names the files).
    /// </summary>
    public static class QualityTierVerificationRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "QualityTierVerification.Armed";
        const string ModeKey        = "QualityTierVerification.Mode";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Quality Tiers/Capture Settings + Home (EN, JP, Low, High)")]
        public static void CaptureSettings() => Launch("settings");

        [MenuItem("GOLFIN/Quality Tiers/Capture Hole 08 Fairness A-B (Low vs High)")]
        public static void CaptureFairness() => Launch("fairness");

        static void Launch(string mode)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[TierVerify] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);

            // Without this the Game View stops emitting frames while the Editor is unfocused and
            // every capture comes back as the splash frame.
            PlayerSettings.runInBackground = true;

            SessionState.SetBool(ArmedKey, true);
            SessionState.SetString(ModeKey, mode);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            var host = new GameObject("[QualityTierVerificationBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<QualityTierVerificationRunner>().Begin(SessionState.GetString(ModeKey, "settings"));
        }
    }

    public class QualityTierVerificationRunner : MonoBehaviour
    {
        string _mode;
        readonly StringBuilder _log = new StringBuilder();

        public void Begin(string mode) { _mode = mode; StartCoroutine(Sequence()); }

        void Mark(string m) { _log.AppendLine(m); Debug.Log("[TierVerify] " + m); }

        static GameObject Find(string path) => GameObject.Find(path);

        static bool Click(string path)
        {
            var go = Find(path);
            if (go == null) { Debug.LogWarning("[TierVerify] not found: " + path); return false; }
            var b = go.GetComponent<Button>();
            if (b == null) { Debug.LogWarning("[TierVerify] no Button on: " + path); return false; }
            b.onClick.Invoke();
            return true;
        }

        static IEnumerator Hold(float s) { yield return new WaitForSecondsRealtime(s); }

        /// The app boots behind a Title/PLAY gate that ScreenManager does NOT manage — ShowScreen
        /// swaps screens BEHIND it and every capture stays on the title frame. Tap it for real.
        IEnumerator PassTheStartGate()
        {
            yield return Hold(6f);
            for (int i = 0; i < 20; i++)
            {
                foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    if (b.name != "StartButton" && b.name != "PlayButton") continue;
                    Mark("start-gate: tapping " + b.name);
                    b.onClick.Invoke();
                    yield return Hold(2f);
                    yield break;
                }
                yield return Hold(0.5f);
            }
            Mark("start-gate: no StartButton found (already past it?)");
        }

        IEnumerator Sequence()
        {
            yield return PassTheStartGate();

            if (_mode == "fairness") yield return Fairness();
            else                     yield return SettingsPass();

            Debug.Log("[TierVerify] ===== SUMMARY =====\n" + _log);
            EditorApplication.isPlaying = false;
        }

        // ── Pass 1: Settings ▸ Graphics + Home bloom ────────────────────────────────────
        IEnumerator SettingsPass()
        {
            ScreenManager.Instance?.ShowScreen(ScreenId.Home, true);
            yield return Hold(2f);

            // Home bloom is a HIGH-ONLY luxury; the two frames below are the A/B.
            QualityTierService.SetOverride((int)QualityTier.High);
            yield return Hold(1.5f);
            yield return Snap("tier_home_high");
            Mark("home HIGH: postProcessing=" + ShellPost() + " hdr=" + AssetHdr() + " renderScale=" + RenderScale());

            QualityTierService.SetOverride((int)QualityTier.Low);
            yield return Hold(1.5f);
            yield return Snap("tier_home_low");
            Mark("home LOW: postProcessing=" + ShellPost() + " hdr=" + AssetHdr() + " renderScale=" + RenderScale()
                 + " targetFrameRate=" + Application.targetFrameRate);

            // Back to Auto, then open Settings through the real gear.
            QualityTierService.SetOverride(QualityTierService.AutoPref);
            yield return Hold(1f);

            Click("PersistentUI/TopBar/SettingsButton");
            yield return Hold(1.2f);
            Click("SettingsScreen/SettingsPanel/SettingsList/GraphicsRow");
            yield return Hold(1.6f);

            LocalizationManager.SetLanguage(Language.English);
            yield return Hold(0.6f);
            yield return Snap("tier_settings_graphics_en");
            Mark("settings EN open — pref=" + QualityTierService.GetOverridePref()
                 + " current=" + QualityTierService.Current + " auto=" + QualityTierService.AutoTier);

            LocalizationManager.SetLanguage(Language.Japanese);
            yield return Hold(0.8f);
            yield return Snap("tier_settings_graphics_jp");

            LocalizationManager.SetLanguage(Language.English);
            yield return Hold(0.8f);

            // THE OVERRIDE ROUND-TRIP, through the row a player taps.
            Click("SettingsScreen/SettingsPanel/SettingsList/GraphicsRow/GraphicsSubmenu/LowButton");
            yield return Hold(1.2f);
            yield return Snap("tier_settings_graphics_low_selected");
            Mark("after real LowButton click: pref=" + QualityTierService.GetOverridePref()
                 + " current=" + QualityTierService.Current
                 + " isOverride=" + QualityTierService.IsOverride
                 + " playerPrefs=" + PlayerPrefs.GetInt(QualityTierService.PrefKey, -99)
                 + " qualityLevel=" + QualitySettings.GetQualityLevel()
                 + " targetFrameRate=" + Application.targetFrameRate
                 + " maxLOD=" + QualitySettings.maximumLODLevel);

            Click("SettingsScreen/SettingsPanel/SettingsList/GraphicsRow/GraphicsSubmenu/AutoButton");
            yield return Hold(1.2f);
            Mark("after real AutoButton click: pref=" + QualityTierService.GetOverridePref()
                 + " current=" + QualityTierService.Current + " isOverride=" + QualityTierService.IsOverride);

            // Leave no pinned tier behind in the Editor's PlayerPrefs.
            PlayerPrefs.DeleteKey(QualityTierService.PrefKey);
            PlayerPrefs.Save();
        }

        // ── Pass 2: the fairness rule, one session, one pose ────────────────────────────
        IEnumerator Fairness()
        {
            ScreenManager.Instance?.ShowScreen(ScreenId.Home, true);
            yield return Hold(2f);

            QualityTierService.SetOverride((int)QualityTier.High);
            yield return Hold(0.5f);

            if (!SeedAndLoad(8)) { Mark("ABORT could not load Hole 08"); yield break; }
            yield return WaitForScene("LabScaffold", 60f);
            yield return WaitForScene("Hole_08_Geo", 60f);
            yield return Hold(8f);   // tee-idle glow settles

            // HIGH first, then LOW, WITHOUT reloading: same sky, same yaw, same tree LOD selection.
            // Two separate launches could not prove this — Phase 0b saw 5,483 vs 4,043 batches on
            // the same hole because the pose drifted between runs.
            yield return Snap("tier_h08_high");
            Mark("H08 HIGH " + Metrics());

            QualityTierService.SetOverride((int)QualityTier.Low);
            yield return Hold(3f);
            yield return Snap("tier_h08_low");
            Mark("H08 LOW  " + Metrics());

            Mark("WIND @ LOW\n" + WindReport());

            QualityTierService.SetOverride((int)QualityTier.Mid);
            yield return Hold(3f);
            yield return Snap("tier_h08_mid");
            Mark("H08 MID  " + Metrics());
            Mark("WIND @ MID\n" + WindReport());

            PlayerPrefs.DeleteKey(QualityTierService.PrefKey);
            PlayerPrefs.Save();
        }

        // ── Reporting helpers ───────────────────────────────────────────────────────────

        static string Metrics()
        {
            var t = Terrain.activeTerrain;
            return "tier=" + QualityTierService.Current
                 + " qualityLevel=" + QualitySettings.GetQualityLevel()
                 + " renderScale=" + RenderScale()
                 + " cascades=" + Cascades()
                 + " shadowDist=" + ShadowDistance()
                 + " hdr=" + AssetHdr()
                 + " maxLOD=" + QualitySettings.maximumLODLevel
                 + " lodBias=" + QualitySettings.lodBias
                 + " | FAIRNESS-INVARIANTS terrain=" + (t != null ? t.name : "<none>")
                 + " treeInstances=" + (t != null && t.terrainData != null ? t.terrainData.treeInstanceCount : -1)
                 + " treeDistance=" + (t != null ? t.treeDistance : -1f)
                 + " treeBillboardDistance=" + (t != null ? t.treeBillboardDistance : -1f)
                 + " treeCrossFadeLength=" + (t != null ? t.treeCrossFadeLength : -1f)
                 + " heightmapRes=" + (t != null && t.terrainData != null ? t.terrainData.heightmapResolution : -1)
                 + " pixelError=" + (t != null ? t.heightmapPixelError : -1f)
                 + " basemapDistance=" + (t != null ? t.basemapDistance : -1f);
        }

        static string WindReport()
        {
            var sb = new StringBuilder();
            var terrain = Terrain.activeTerrain;
            int veg = 0;
            if (terrain != null && terrain.terrainData != null)
            {
                var seen = new HashSet<Material>();
                foreach (var proto in terrain.terrainData.treePrototypes)
                {
                    if (proto.prefab == null) continue;
                    foreach (var r in proto.prefab.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null || m.shader == null) continue;
                            if (m.shader.name != Golfin.Gameplay.UI.HUD.TreeWindDriver.VegetationShaderName) continue;
                            if (!seen.Add(m)) continue;
                            veg++;
                            sb.AppendLine("   veg " + m.name + " _WIND=" + m.IsKeywordEnabled("_WIND")
                                          + " WindSpeedFloat1=" + m.GetFloat("WindSpeedFloat1"));
                        }
                }
            }
            int spruce = 0;
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.shader == null) continue;
                if (m.shader.name != Golfin.Gameplay.UI.HUD.TreeWindDriver.SpruceShaderName) continue;
                spruce++;
                sb.AppendLine("   spruce " + m.name + " WindSpeed=" + m.GetFloat("Vector1_b0ddedae341d4c7ba1d429299f3078ea"));
            }
            sb.AppendLine("   WindEnabled=" + Golfin.Gameplay.UI.HUD.TreeWindDriver.WindEnabled
                          + " vegMaterials=" + veg + " spruceMaterials=" + spruce);
            return sb.ToString();
        }

        static UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset Rp() =>
            UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;

        static string RenderScale()    { var a = Rp(); return a != null ? a.renderScale.ToString("F2") : "?"; }
        static string Cascades()       { var a = Rp(); return a != null ? a.shadowCascadeCount.ToString() : "?"; }
        static string ShadowDistance() { var a = Rp(); return a != null ? a.shadowDistance.ToString("F0") : "?"; }
        static string AssetHdr()       { var a = Rp(); return a != null ? a.supportsHDR.ToString() : "?"; }

        static string ShellPost()
        {
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam == null || cam.gameObject.scene.name != "ShellScene") continue;
                var d = cam.GetUniversalAdditionalCameraData();
                if (d != null) return cam.name + ":" + d.renderPostProcessing;
            }
            return "<no shell camera>";
        }

        /// <summary>
        /// CaptureCore is the ONLY sanctioned capture path (CLAUDE.md). SnapPlayModeSafe does not
        /// pause and does not AssetDatabase.Refresh, so this coroutine survives it.
        ///
        /// THE WaitForEndOfFrame IS LOAD-BEARING. In play mode SnapPlayModeSafe goes through
        /// ScreenCapture.CaptureScreenshotAsTexture (the only path that composites Screen Space
        /// Overlay canvases, i.e. all of this UI), and that API returns NULL unless the caller is
        /// already at end-of-frame — its own doc comment says frame timing is the caller's job.
        /// Without this yield every snap silently produced a path for a file that was never
        /// written. The File.Exists assert below is what caught it; keep both.
        /// </summary>
        static IEnumerator Snap(string label)
        {
            yield return new WaitForEndOfFrame();

            string path = CaptureCore.SnapPlayModeSafe(label);
            bool ok = !string.IsNullOrEmpty(path) && File.Exists(path);
            Debug.Log("[TierVerify] SNAP " + label + " -> " + path + " exists=" + ok
                      + (ok ? " bytes=" + new FileInfo(path).Length : ""));
            if (!ok) Debug.LogWarning("[TierVerify] SNAP FAILED for " + label + " — do not cite this frame.");
        }

        // ── Hole load, same reflection path PerfBaselineBot uses ────────────────────────

        static Type FindType(string full)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }

        static bool SeedAndLoad(int hole)
        {
            try
            {
                var gsType = FindType("Golfin.Gameplay.Session.GameSession");
                if (gsType == null) { Debug.LogWarning("[TierVerify] GameSession not found"); return false; }
                gsType.GetProperty("IsVersus", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, false);

                string charId = "";
                var cmType = FindType("CharacterManager");
                if (cmType != null)
                {
                    var inst = cmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (inst != null) charId = (string)(cmType.GetMethod("GetSelectedCharacterId")?.Invoke(inst, null) ?? "");
                }
                gsType.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                      ?.Invoke(null, new object[] { hole, charId, 0 });

                var loaderType = FindType("Golfin.UI.GameplayTransition.GameplaySceneLoader");
                var loaderInst = loaderType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (loaderInst == null) { Debug.LogWarning("[TierVerify] GameplaySceneLoader.Instance null"); return false; }

                var begin = loaderType.GetMethods().FirstOrDefault(m => m.Name == "BeginGameplayLoad");
                if (begin == null) { Debug.LogWarning("[TierVerify] BeginGameplayLoad not found"); return false; }
                var pars = begin.GetParameters();
                begin.Invoke(loaderInst, pars.Length == 1 ? new object[] { hole } : new object[] { hole, null });
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[TierVerify] seed/load failed: " + e.Message); return false; }
        }

        static IEnumerator WaitForScene(string name, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (s.name == name && s.isLoaded) yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                t += 0.5f;
            }
            Debug.LogWarning("[TierVerify] timed out waiting for scene " + name);
        }
    }
}
#endif
