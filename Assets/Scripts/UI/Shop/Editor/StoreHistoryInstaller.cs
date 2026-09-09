// Assets/Scripts/UI/Shop/Editor/StoreHistoryInstaller.cs
// store_history §5 — ONE-SHOT scene install for StoreHistoryScreen.
//
// The screen's scene instance and the single ScreenManager reference to it are the only things
// this task adds to ShellScene.unity. Doing it from a menu item rather than by hand is what keeps
// `git diff --stat Assets/Scenes/ShellScene.unity` to those two things: a hand edit in the Editor
// drags along whatever else the open scene has drifted into (the stale MatchMakingModal overrides
// the sim-loop notes warn about, layout churn from a play-mode entry — see
// Docs/PIPELINE_HARDENING.md §14).
//
// RE-RUNNABLE. A second run finds the instance, reports "already", and writes nothing.
#nullable enable
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Shop.EditorTools
{
    public static class StoreHistoryInstaller
    {
        private const string PrefabPath  = "Assets/Prefabs/UI/Shop/StoreHistoryScreen.prefab";
        private const string ScreensRoot = "Canvas/ScreensRoot";
        private const string SiblingName = "GachaHistoryScreen";
        private const string InstanceName = "StoreHistoryScreen";

        [MenuItem("GOLFIN/Store History/Install screen", priority = 260)]
        public static void InstallMenu() => Debug.Log(Install());

        /// <summary>Instantiate the screen beside GachaHistoryScreen (inactive, same parent, same
        /// rect) and point <c>ScreenManager._storeHistoryScreen</c> at it.</summary>
        public static string Install()
        {
            var root = GameObject.Find(ScreensRoot);
            if (root == null) return "[StoreHistory] FAIL — " + ScreensRoot + " not found. Open ShellScene first.";

            var sibling = root.transform.Find(SiblingName);
            if (sibling == null) return "[StoreHistory] FAIL — " + SiblingName + " not found under " + ScreensRoot + ".";

            var manager = Object.FindFirstObjectByType<ScreenManager>(FindObjectsInactive.Include);
            if (manager == null) return "[StoreHistory] FAIL — no ScreenManager in the open scene.";

            var existing = root.transform.Find(InstanceName);
            GameObject instance;

            if (existing != null)
            {
                instance = existing.gameObject;
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) return "[StoreHistory] FAIL — prefab missing at " + PrefabPath + ".";

                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = InstanceName;
                // Immediately after the sibling it was cloned from, so the ScreensRoot order reads
                // the way the pillar does.
                instance.transform.SetSiblingIndex(sibling.GetSiblingIndex() + 1);

                // SAME RECT AS THE SIBLING. The prefab carries its own, but a screen root under
                // ScreensRoot is a full-bleed stretch and the sibling is the authority on what the
                // scene's canvas calls full-bleed.
                var src = (RectTransform)sibling;
                var dst = (RectTransform)instance.transform;
                dst.anchorMin        = src.anchorMin;
                dst.anchorMax        = src.anchorMax;
                dst.pivot            = src.pivot;
                dst.anchoredPosition = src.anchoredPosition;
                dst.sizeDelta        = src.sizeDelta;
                dst.localScale       = src.localScale;

                // INACTIVE. ScreenManager.SetActive drives it; an active second screen root would
                // draw on top of Home from the first frame.
                instance.SetActive(false);

                Undo.RegisterCreatedObjectUndo(instance, "Install StoreHistoryScreen");
            }

            var so = new SerializedObject(manager);
            SerializedProperty p = so.FindProperty("_storeHistoryScreen");
            if (p == null) return "[StoreHistory] FAIL — ScreenManager has no _storeHistoryScreen field.";

            bool changed = p.objectReferenceValue != instance;
            if (changed)
            {
                p.objectReferenceValue = instance;
                so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
                EditorUtility.SetDirty(manager);
            }

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

            return $"[StoreHistory] instance={(existing != null ? "already" : "created")} " +
                   $"reference={(changed ? "wired" : "already")} — SAVE THE SCENE, then diff it.";
        }
    }
}
