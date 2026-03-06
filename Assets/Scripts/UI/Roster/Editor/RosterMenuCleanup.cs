#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Organizes Roster menu items under a clean structure:
    /// Tools → GOLFIN → Roster → [Build, Test, Debug]
    /// 
    /// Hides legacy menu items to reduce clutter
    /// </summary>
    public static class RosterMenuCleanup
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // ROSTER MENU STRUCTURE (ACTIVE)
        // ═══════════════════════════════════════════════════════════════════════════
        
        // Priority 0-99: Main Build Tools
        [MenuItem("Tools/GOLFIN/Roster/Build Complete Roster Screen", priority = 1)]
        public static void BuildCompleteRosterScreen()
        {
            RosterScreenBuilder.BuildFromScratch();
        }
        
        [MenuItem("Tools/GOLFIN/Roster/Build Character Thumbnail Prefab", priority = 2)]
        public static void BuildCharacterThumbnailPrefab()
        {
            RosterPrefabBuilder.BuildCharacterThumbnailCard();
        }
        
        [MenuItem("Tools/GOLFIN/Roster/Build StatBar Prefab", priority = 3)]
        public static void BuildStatBarPrefab()
        {
            RosterPrefabBuilder.BuildStatBar();
        }
        
        // Priority 100-199: Testing Tools
        [MenuItem("Tools/GOLFIN/Roster/Test Phase 1 (Data)", priority = 101)]
        public static void TestPhase1()
        {
            RosterPhase1TestRunner.RunTests();
        }
        
        [MenuItem("Tools/GOLFIN/Roster/Test Carousel", priority = 102)]
        public static void TestCarousel()
        {
            // Future: Test carousel in isolation
            Debug.Log("[Roster] Carousel test not implemented yet");
        }
        
        // Priority 200-299: Debug/Utility Tools
        [MenuItem("Tools/GOLFIN/Roster/Debug: List All Characters", priority = 201)]
        public static void DebugListCharacters()
        {
            var characters = CharacterManager.Instance.GetAllOwnedCharacters();
            Debug.Log($"[Roster Debug] Found {characters.Count} owned characters:");
            foreach (var character in characters)
            {
                Debug.Log($"  - {character.characterId} (Level {character.currentLevel})");
            }
        }
        
        [MenuItem("Tools/GOLFIN/Roster/Debug: Validate References", priority = 202)]
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
            
            Debug.Log("[Roster Debug] ✓ RosterScreen found and has controller");
            // TODO: Add more validation checks
        }
        
        // Priority 300-399: Data Management
        [MenuItem("Tools/GOLFIN/Roster/Data: Reset Player Progress", priority = 301)]
        public static void ResetPlayerProgress()
        {
            if (EditorUtility.DisplayDialog(
                "Reset Player Progress?",
                "This will delete all player character data (levels, SP allocation, etc.)\n\n" +
                "Are you sure?",
                "Yes, Reset",
                "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                Debug.Log("[Roster] Player progress reset");
            }
        }
        
        [MenuItem("Tools/GOLFIN/Roster/Data: Grant 100000 Reward Points", priority = 302)]
        public static void GrantRewardPoints()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Roster] Enter Play Mode first");
                return;
            }
            
            RewardPointsManager.Instance.AddPoints(100000);
            Debug.Log("[Roster] Granted 100000 R");
        }
    }
}
#endif
