// ─────────────────────────────────────────────────────────────────────────────
// SceneSaveChurnGuard — EDITOR ONLY
//
// Warns before a scene save that would rewrite layout-driven RectTransforms that
// you never touched.
//
// ─── THE TRAP ───────────────────────────────────────────────────────────────
// Entering and leaving play mode leaves layout-driven RectTransforms holding the
// values their LayoutGroup computed at runtime, which differ from what is
// serialized on disk. Unity does NOT mark the scene dirty for this — `isDirty`
// reads false — but the next save writes them all out. Measured on ShellScene:
// one play-mode round trip, zero manual edits, then save = 1285 insertions /
// 1284 deletions across 153 RectTransforms. Every one of them is noise that
// buries the real change in review and collides with anyone else editing the scene.
//
// (Diagnosis: all 153 sit under the ScaleWithScreenSize canvas or its nested
// scaler-less panels, driven by Vertical/HorizontalLayoutGroups. The values are
// idempotent — saving twice produces identical bytes — so this is safe noise, not
// corruption. It is still noise.)
//
// ─── WHAT THIS DOES ─────────────────────────────────────────────────────────
// It does not block the save. It logs a loud warning naming the scene and telling
// you the safe recipe, so a churned save is a decision instead of an accident:
//
//     GOLFIN > Dev > Reload Scene From Disk   (discard in-memory drift)
//     …re-apply your edit…                    (on a freshly loaded scene)
//     save                                    (diff is now just your change)
//
// Verify with `git diff --stat` on the scene before committing either way.
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.EditorTools
{
    [InitializeOnLoad]
    public static class SceneSaveChurnGuard
    {
        /// <summary>Set when play mode ends; cleared when a scene is opened fresh from disk.</summary>
        private static bool _playModeRanSinceLoad;

        static SceneSaveChurnGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.sceneSaving         -= OnSceneSaving;
            EditorSceneManager.sceneSaving         += OnSceneSaving;
            EditorSceneManager.sceneOpened         -= OnSceneOpened;
            EditorSceneManager.sceneOpened         += OnSceneOpened;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) _playModeRanSinceLoad = true;
        }

        // A scene loaded from disk is by definition in sync with disk again.
        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => _playModeRanSinceLoad = false;

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (!_playModeRanSinceLoad) return;

            Debug.LogWarning(
                $"[SceneSaveChurnGuard] Saving '{scene.name}' AFTER a play-mode round trip.\n" +
                "Play mode leaves layout-driven RectTransforms holding runtime-computed values that " +
                "differ from disk, and Unity does not flag the scene dirty for it — so this save may " +
                "rewrite a large number of RectTransforms you never touched (measured: 153 on " +
                "ShellScene, ~1285 lines).\n" +
                "SAFE RECIPE: GOLFIN > Dev > Reload Scene From Disk, re-apply your edit, then save.\n" +
                "EITHER WAY: check `git diff --stat` on the scene before committing.");
        }

        [MenuItem("GOLFIN/Dev/Reload Scene From Disk")]
        private static void ReloadFromDisk()
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[SceneSaveChurnGuard] Active scene has never been saved — nothing to reload.");
                return;
            }
            if (scene.isDirty &&
                !EditorUtility.DisplayDialog("Reload Scene From Disk",
                    $"'{scene.name}' has unsaved changes. Reloading DISCARDS them.\n\nContinue?",
                    "Discard and reload", "Cancel"))
                return;

            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            _playModeRanSinceLoad = false;
            Debug.Log($"[SceneSaveChurnGuard] Reloaded '{scene.name}' from disk — in sync, safe to edit and save.");
        }
    }
}
#endif
