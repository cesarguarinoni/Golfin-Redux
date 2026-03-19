#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
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
    }
}
#endif
