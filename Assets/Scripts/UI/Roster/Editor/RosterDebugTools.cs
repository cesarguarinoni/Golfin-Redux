#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using Golfin.Roster;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Debug and data utilities for the Roster system.
    /// Menu: GOLFIN/Debug/
    /// </summary>
    public static class RosterDebugTools
    {
        [MenuItem("GOLFIN/Debug/List All Characters", priority = 201)]
        public static void DebugListCharacters()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Roster Debug] Enter Play Mode first.");
                return;
            }

            var characters = CharacterManager.Instance.GetAllOwnedCharacters();
            Debug.Log($"[Roster Debug] Found {characters.Count} owned characters:");
            foreach (var character in characters)
                Debug.Log($"  - {character.characterId} (Level {character.currentLevel})");
        }

        [MenuItem("GOLFIN/Debug/Validate References", priority = 202)]
        public static void DebugValidateReferences()
        {
            var rosterScreen = GameObject.Find("RosterScreen");
            if (rosterScreen == null)
            {
                Debug.LogError("[Roster Debug] RosterScreen not found in scene!");
                return;
            }

            var controller = rosterScreen.GetComponent<RosterScreenController>();
            if (controller == null)
            {
                Debug.LogError("[Roster Debug] RosterScreenController component missing!");
                return;
            }

            Debug.Log("[Roster Debug] ✓ RosterScreen found and has RosterScreenController.");
        }

        [MenuItem("GOLFIN/Debug/Grant 100000 Reward Points", priority = 301)]
        public static void GrantRewardPoints()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Roster Debug] Enter Play Mode first.");
                return;
            }

            RewardPointsManager.Instance.EarnPoints(100000);
            Debug.Log("[Roster Debug] Granted 100,000 R.");
        }

        [MenuItem("GOLFIN/Debug/Reset Player Progress", priority = 302)]
        public static void ResetPlayerProgress()
        {
            if (EditorUtility.DisplayDialog(
                "Reset Player Progress?",
                "This will delete all PlayerPrefs data (character levels, SP, reward points, etc.).\n\nAre you sure?",
                "Yes, Reset",
                "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Debug.Log("[Roster Debug] Player progress reset.");
            }
        }

        // ── Scene Cleanup ─────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Debug/Remove Missing Scripts From Scene", priority = 401)]
        public static void RemoveMissingScripts()
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            int removed = 0;
            var dirtyObjects = new List<GameObject>();

            foreach (var go in allObjects)
            {
                // Skip prefab assets in the project — only process scene objects
                if (!go.scene.IsValid()) continue;

                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (count > 0)
                {
                    Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    removed += count;
                    dirtyObjects.Add(go);
                    Debug.Log($"[SceneCleanup] Removed {count} missing script(s) from '{go.name}'.");
                }
            }

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log($"[SceneCleanup] Done — removed {removed} missing script(s) total. Save the scene.");
            }
            else
            {
                Debug.Log("[SceneCleanup] No missing scripts found in the active scene.");
            }
        }
    }
}
#endif
