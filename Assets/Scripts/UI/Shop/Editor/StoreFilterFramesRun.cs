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
                DumpPriceRows();
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

            /// <summary>World-space centre of coin+number vs the box, per live card — the
            /// instrument for "is the price centred", instead of a pixel scan that also sees the
            /// coin's glow.</summary>
            private static void DumpPriceRows()
            {
                var sb = new System.Text.StringBuilder();
                foreach (var card in FindObjectsByType<GeneralShopCard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    var box = card.transform.Find("PriceBox") as RectTransform;
                    if (box == null || !card.gameObject.activeInHierarchy) continue;
                    var bc = new Vector3[4]; box.GetWorldCorners(bc);
                    float boxCx = (bc[0].x + bc[2].x) * 0.5f;
                    foreach (var row in new[] { "PriceBox/Orig", "PriceBox/SaleBG/Sale" })
                    {
                        var r = card.transform.Find(row);
                        if (r == null || !r.gameObject.activeInHierarchy) continue;
                        var icon = r.Find("RpIcon") as RectTransform;
                        var num  = r.Find("Num")?.GetComponent<TMPro.TextMeshProUGUI>();
                        if (icon == null || num == null) continue;
                        var ic = new Vector3[4]; icon.GetWorldCorners(ic);
                        var tb = num.textBounds;                               // rendered glyph bounds, local
                        var numRt = (RectTransform)num.transform;
                        Vector3 tMin = numRt.TransformPoint(tb.min), tMax = numRt.TransformPoint(tb.max);
                        float groupCx = (ic[0].x + tMax.x) * 0.5f;
                        sb.AppendLine($"{card.name,-34} {row,-22} '{num.text}' iconL={ic[0].x:F1} textR={tMax.x:F1} groupC={groupCx:F1} boxC={boxCx:F1} off={groupCx - boxCx:+0.0;-0.0}  | local iconX={icon.anchoredPosition.x:F1} numX={numRt.anchoredPosition.x:F1} numW={numRt.sizeDelta.x:F1} pref={num.preferredWidth:F1} align={num.alignment}");
                    }
                }
                File.WriteAllText("/tmp/price_rows.txt", sb.ToString());
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
