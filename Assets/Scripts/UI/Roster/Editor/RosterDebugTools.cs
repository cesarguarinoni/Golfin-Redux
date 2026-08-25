#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using Golfin.Roster;
using Golfin.Save;

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

            // Local-only: this grant never reaches the server ledger, so with PointsBackendEnabled ON
            // it would be wiped by the next balance refresh. Refuse rather than mislead — the same
            // rule as RewardPointsManager.SetPoints (Slice 2).
            if (Golfin.Economy.PointsBackendFlag.Enabled)
            {
                Debug.LogWarning("[Roster Debug] Grant refused — PointsBackendEnabled is ON and the server " +
                                 "balance is authoritative. Grant RP admin-side (Supabase / dashboard points " +
                                 "panel), or turn the flag off via GOLFIN > Points Backend > Enabled.");
                return;
            }

            RewardPointsManager.Instance.EarnPointsLocalOnly(100000);
            Debug.Log("[Roster Debug] Granted 100,000 R (local only).");
        }

        /// <summary>
        /// Resets the in-memory starter-choice state without touching PlayerPrefs or auth.
        /// Mutates SaveDataHost.Instance.Data directly: clears starterCharacterId,
        /// selectedCharacterId, and all PersistedCharacter.isOwned flags, then flushes.
        /// Also clears the runtime PlayerCharacterData.isOwned flags in CharacterManager.
        ///
        /// After calling this, CharacterManager.NeedsStarter returns true in-memory.
        /// Exit and re-enter play mode to test the full fresh-save boot flow (StartingCharacterSelection).
        ///
        /// Do NOT use this as a substitute for "Reset Player Progress" — it does NOT touch
        /// PlayerPrefs, does NOT delete auth session data.
        /// </summary>
        [MenuItem("GOLFIN/Debug/Reset Starter Choice", priority = 303)]
        public static void ResetStarterChoice()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[ResetStarterChoice] Must be in play mode. Enter Play Mode first, then call this menu item.");
                return;
            }

            if (SaveDataHost.Instance == null)
            {
                Debug.LogError("[ResetStarterChoice] SaveDataHost.Instance is null — cannot reset.");
                return;
            }

            var data = SaveDataHost.Instance.Data;
            string previousStarter = data.starterCharacterId;

            // 1. Clear starter and selected in the persisted model.
            data.starterCharacterId  = "";
            data.selectedCharacterId = "";

            // 2. Clear isOwned on all PersistedCharacter entries in SaveData.
            foreach (var c in data.ownedCharacters)
                c.isOwned = false;

            // 3. Mirror the change into CharacterManager's live runtime model.
            if (CharacterManager.Instance != null)
            {
                var all = CharacterManager.Instance.GetAllCatalogCharacters();
                foreach (var pd in all)
                    pd.isOwned = false;
            }
            else
            {
                Debug.LogWarning("[ResetStarterChoice] CharacterManager.Instance is null — runtime data not cleared.");
            }

            // 4. Flush via the host's own debounced write (does NOT touch PlayerPrefs or auth).
            SaveDataHost.Instance.MarkDirty();

            Debug.Log($"[ResetStarterChoice] Starter choice reset. Previous starter was '{previousStarter}'. " +
                      "CharacterManager.NeedsStarter is now true in-memory. " +
                      "Exit and re-enter play mode to test the full fresh-save boot path (StartingCharacterSelection). " +
                      "Auth session (PlayerPrefs) was NOT touched.");
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
