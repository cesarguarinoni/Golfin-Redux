// Assets/Scripts/UI/Shop/Editor/StoreFilterFramesRun.cs
// Shoots the STORE grid under the ITEMS and TICKETS chips, plus Store History under the same two,
// through the real widgets — so "the history should show what the store shows" can be compared on
// frames rather than argued from code. Editor-only.
#nullable enable
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using GolfinRedux.UI;
using GolfinRedux.UI.Gacha;

namespace GolfinRedux.UI.Shop.EditorTools
{
    public static class StoreFilterFramesRun
    {
        private const string ArmedKey   = "GOLFIN.StoreFilterFrames.Armed";
        private const string OutDir     = "Docs/Diagnostics/_capture/store_filter_frames";
        private const string ShellScene = "Assets/Scenes/ShellScene.unity";

        [MenuItem("GOLFIN/Store History/Shoot store vs history filter frames", priority = 263)]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ShellScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Application.runInBackground = true;
            EditorPrefs.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!EditorPrefs.GetBool(ArmedKey, false)) return;
            EditorApplication.update += Pump;
        }

        private static bool _spawned;
        private static void Pump()
        {
            if (!Application.isPlaying) return;
            if (_spawned) { EditorApplication.update -= Pump; return; }
            _spawned = true;
            EditorPrefs.SetBool(ArmedKey, false);
            EditorApplication.update -= Pump;
            var host = new GameObject("[StoreFilterFrames]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Start() => StartCoroutine(Sequence());
            private static ScreenId? Now => ScreenManager.Instance?.CurrentScreen;

            private IEnumerator Sequence()
            {
                Application.runInBackground = true;
                float t = 0f;
                while (t < 25f)
                {
                    var splash = FindFirstObjectByType<SplashScreenController>();
                    var btn = splash == null ? null : splash.transform.Find("StartButton");
                    if (btn != null && btn.gameObject.activeInHierarchy) { btn.GetComponent<Button>()?.onClick.Invoke(); break; }
                    t += Time.unscaledDeltaTime; yield return null;
                }
                yield return WaitFor(ScreenId.Home, 60f);
                yield return Settle(1.5f);

                var pum = FindFirstObjectByType<Golfin.UI.PersistentUIManager>(FindObjectsInactive.Include);
                pum?.shopPlusButton?.onClick.Invoke();
                yield return WaitFor(ScreenId.GeneralShop, 20f);
                yield return Settle(1.5f);
                yield return DismissHints();
                yield return Settle(2f);

                var shop = FindFirstObjectByType<GeneralShopScreenController>(FindObjectsInactive.Include);
                Save("store_ALL");
                foreach (var chip in new[] { "ITEMSChip", "TICKETSChip" })
                {
                    shop?.transform.Find("ContentArea/BarsArea/FilterGroup/CategoryRow/" + chip)?.GetComponent<Button>()?.onClick.Invoke();
                    yield return Settle(2.5f);
                    Save("store_" + chip);
                }
                shop?.transform.Find("ContentArea/BarsArea/FilterGroup/CategoryRow/ALLChip")?.GetComponent<Button>()?.onClick.Invoke();
                yield return Settle(1f);

                var tab = FindFirstObjectByType<GachaTabController>(FindObjectsInactive.Include);
                tab?.transform.Find("HistoryChip")?.GetComponent<Button>()?.onClick.Invoke();
                yield return WaitFor(ScreenId.StoreHistory, 20f);
                yield return Settle(1.5f);
                yield return DismissHints();
                yield return Settle(4f);

                var hist = FindFirstObjectByType<StoreHistoryScreenController>(FindObjectsInactive.Include);
                foreach (var chip in new[] { "ITEMSChip", "TICKETSChip" })
                {
                    hist?.transform.Find("GameScreenContent/ContentContainer/FiltersBlock/CategoryRow/" + chip)?.GetComponent<Button>()?.onClick.Invoke();
                    yield return Settle(2.5f);
                    Save("history_" + chip);
                }

                File.WriteAllText("/tmp/store_filter_frames.txt", "DONE\n");
                EditorApplication.isPlaying = false;
            }

            private static void Save(string label)
            {
                string dst = Path.Combine(OutDir, label + ".png");
                string src = CaptureCore.SnapPlayModeSafe(label);
                if (!string.IsNullOrEmpty(src) && File.Exists(src)) { File.Copy(src, dst, true); return; }
                var tex = CaptureCore.GrabGameViewRT();
                if (tex == null) { Debug.LogError("[StoreFilterFrames] no frame for " + label); return; }
                File.WriteAllBytes(dst, tex.EncodeToPNG());
                Object.Destroy(tex);
                Debug.Log("[StoreFilterFrames] saved " + dst);
            }

            /// <summary>The screen-hint PRO TIP modal fires on the first visit to a screen on this
            /// device and sits over the whole frame. Dismiss it through its own CONTINUE/CLOSE
            /// button — the real one, by reflection on the private field — until it is gone.</summary>
            private static IEnumerator DismissHints()
            {
                for (int i = 0; i < 8; i++)
                {
                    var modal = Golfin.UI.Modals.ScreenHintModalController.Instance;
                    if (modal == null || !modal.gameObject.activeInHierarchy) yield break;
                    var f = typeof(Golfin.UI.Modals.ScreenHintModalController).GetField("nextButton",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var btn = f?.GetValue(modal) as Button;
                    if (btn == null || !btn.gameObject.activeInHierarchy) yield break;
                    btn.onClick.Invoke();
                    yield return Settle(0.8f);
                }
            }

            private static IEnumerator WaitFor(ScreenId id, float s) { float t = 0f; while (t < s && Now != id) { t += Time.unscaledDeltaTime; yield return null; } }
            private static IEnumerator Settle(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }
        }
    }
}
