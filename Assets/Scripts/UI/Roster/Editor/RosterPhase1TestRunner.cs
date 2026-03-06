#nullable enable
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneHierarchy;
using Golfin.Roster;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Phase 1 Test Runner - Automated testing
    /// Menu: Tools → GOLFIN → Test Roster Phase 1
    /// 
    /// Runs all Phase 1 tests and reports results:
    /// 1. Reward Points Manager
    /// 2. Character Manager
    /// 3. Level-Up System
    /// 4. CSV Data Validation
    /// </summary>
    public class RosterPhase1TestRunner
    {
        private static int testsPassed = 0;
        private static int testsFailed = 0;
        
        [MenuItem("Tools/GOLFIN/Test Roster Phase 1")]
        public static void RunTests()
        {
            Debug.Log("\n========================================");
            Debug.Log("🧪 ROSTER PHASE 1 - TEST RUNNER");
            Debug.Log("========================================\n");
            
            testsPassed = 0;
            testsFailed = 0;
            
            // Verify managers exist
            if (!VerifyManagers())
            {
                EditorUtility.DisplayDialog(
                    "Test Failed",
                    "Managers not found in scene!\n\n" +
                    "Run: Tools → GOLFIN → Setup Roster System (Phase 1) first",
                    "OK"
                );
                return;
            }
            
            // Run tests
            TestRewardPointsManager();
            TestCharacterManager();
            TestLevelUpSystem();
            TestCSVDataValidation();
            
            // Summary
            PrintSummary();
        }
        
        private static bool VerifyManagers()
        {
            Debug.Log("📋 Verifying managers exist in scene...");
            
            var rpManager = Object.FindObjectOfType<RewardPointsManager>();
            var charManager = Object.FindObjectOfType<CharacterManager>();
            var levelUpDB = Object.FindObjectOfType<CharacterLevelUpDatabase>();
            
            bool allFound = rpManager != null && charManager != null && levelUpDB != null;
            
            if (!allFound)
            {
                Debug.LogError("❌ Missing managers:");
                if (rpManager == null) Debug.LogError("   - RewardPointsManager");
                if (charManager == null) Debug.LogError("   - CharacterManager");
                if (levelUpDB == null) Debug.LogError("   - CharacterLevelUpDatabase");
                return false;
            }
            
            Debug.Log("✅ All managers found");
            return true;
        }
        
        private static void TestRewardPointsManager()
        {
            Debug.Log("\n--- Test 1: Reward Points Manager ---");
            
            var rpManager = Object.FindObjectOfType<RewardPointsManager>();
            if (rpManager == null)
            {
                Fail("RewardPointsManager not found");
                return;
            }
            
            int points = rpManager.GetPoints();
            if (points == 50000)
            {
                Pass($"Current Points: {points}R ✅");
            }
            else
            {
                Fail($"Expected 50000R, got {points}R");
            }
            
            // Test canAfford
            if (rpManager.CanAfford(100))
            {
                Pass("CanAfford(100) = true ✅");
            }
            else
            {
                Fail("CanAfford(100) should be true");
            }
        }
        
        private static void TestCharacterManager()
        {
            Debug.Log("\n--- Test 2: Character Manager ---");
            
            var charManager = Object.FindObjectOfType<CharacterManager>();
            if (charManager == null)
            {
                Fail("CharacterManager not found");
                return;
            }
            
            // Test GetPlayerCharacter
            var playerChar = charManager.GetPlayerCharacter("char_elizabeth");
            if (playerChar == null)
            {
                Fail("Elizabeth not found");
                return;
            }
            
            if (playerChar.currentLevel == 1)
            {
                Pass($"Elizabeth Level: {playerChar.currentLevel} ✅");
            }
            else
            {
                Fail($"Expected level 1, got {playerChar.currentLevel}");
            }
            
            if (playerChar.totalSPEarned == 0)
            {
                Pass($"Elizabeth SP Earned: {playerChar.totalSPEarned} ✅");
            }
            else
            {
                Fail($"Expected 0 SP, got {playerChar.totalSPEarned}");
            }
            
            // Test GetAllOwnedCharacters
            var allChars = charManager.GetAllOwnedCharacters();
            if (allChars.Count == 4)
            {
                Pass($"Owned Characters: {allChars.Count} ✅");
            }
            else
            {
                Fail($"Expected 4 characters, got {allChars.Count}");
            }
        }
        
        private static void TestLevelUpSystem()
        {
            Debug.Log("\n--- Test 3: Level-Up System ---");
            
            var charManager = Object.FindObjectOfType<CharacterManager>();
            var rpManager = Object.FindObjectOfType<RewardPointsManager>();
            
            if (charManager == null || rpManager == null)
            {
                Fail("Managers not found");
                return;
            }
            
            // Get initial state
            var playerChar = charManager.GetPlayerCharacter("char_elizabeth");
            int initialLevel = playerChar.currentLevel;
            int initialPoints = rpManager.GetPoints();
            int levelUpCost = charManager.GetLevelUpCost("char_elizabeth");
            
            // Level up
            int spEarned = charManager.LevelUp("char_elizabeth");
            
            // Verify level increased
            playerChar = charManager.GetPlayerCharacter("char_elizabeth");
            if (playerChar.currentLevel == initialLevel + 1)
            {
                Pass($"Level Up: {initialLevel} → {playerChar.currentLevel} ✅");
            }
            else
            {
                Fail($"Level didn't increase (expected {initialLevel + 1}, got {playerChar.currentLevel})");
            }
            
            // Verify SP earned
            if (spEarned == 1)
            {
                Pass($"SP Reward: {spEarned} SP ✅");
            }
            else
            {
                Fail($"Expected 1 SP reward, got {spEarned}");
            }
            
            // Verify points spent
            int newPoints = rpManager.GetPoints();
            int pointsSpent = initialPoints - newPoints;
            if (pointsSpent == levelUpCost)
            {
                Pass($"Points Spent: {pointsSpent}R (cost: {levelUpCost}R) ✅");
            }
            else
            {
                Fail($"Expected {levelUpCost}R spent, got {pointsSpent}R");
            }
        }
        
        private static void TestCSVDataValidation()
        {
            Debug.Log("\n--- Test 4: CSV Data Validation ---");
            
            var levelUpDB = Object.FindObjectOfType<CharacterLevelUpDatabase>();
            if (levelUpDB == null)
            {
                Fail("CharacterLevelUpDatabase not found");
                return;
            }
            
            // Test Elizabeth Lv10 cost
            int cost = levelUpDB.GetLevelUpCost("char_elizabeth", 10);
            if (cost == 150)
            {
                Pass($"Elizabeth Lv10 Cost: {cost}R ✅");
            }
            else
            {
                Fail($"Expected 150R, got {cost}R");
            }
            
            // Test SP reward
            int spReward = levelUpDB.GetSPReward("char_elizabeth", 10);
            if (spReward == 1)
            {
                Pass($"Elizabeth Lv10 SP Reward: {spReward} ✅");
            }
            else
            {
                Fail($"Expected 1 SP, got {spReward}");
            }
            
            // Test Shae (Legendary) stat cap
            int strayCap = levelUpDB.GetStatCap("char_shae", 1, "Strength");
            if (strayCap == 40)
            {
                Pass($"Shae (Legendary) Strength Cap: {strayCap} ✅");
            }
            else
            {
                Fail($"Expected 40, got {strayCap}");
            }
            
            // Test all character levels exist
            var elizabethLevels = levelUpDB.GetCharacterLevels("char_elizabeth");
            if (elizabethLevels.Count > 0)
            {
                Pass($"Elizabeth Levels in CSV: {elizabethLevels.Count} rows ✅");
            }
            else
            {
                Fail("No Elizabeth levels found in CSV");
            }
        }
        
        private static void Pass(string message)
        {
            testsPassed++;
            Debug.Log($"✅ {message}");
        }
        
        private static void Fail(string message)
        {
            testsFailed++;
            Debug.LogError($"❌ {message}");
        }
        
        private static void PrintSummary()
        {
            Debug.Log("\n========================================");
            Debug.Log("📊 TEST SUMMARY");
            Debug.Log("========================================");
            Debug.Log($"✅ Passed: {testsPassed}");
            Debug.Log($"❌ Failed: {testsFailed}");
            
            if (testsFailed == 0)
            {
                Debug.Log("\n🎉 ALL TESTS PASSED!");
                Debug.Log("Phase 1 is ready for Phase 2 development");
            }
            else
            {
                Debug.Log($"\n⚠️ {testsFailed} test(s) failed - see errors above");
            }
            
            Debug.Log("========================================\n");
            
            EditorUtility.DisplayDialog(
                "Phase 1 Test Results",
                $"✅ Passed: {testsPassed}\n❌ Failed: {testsFailed}\n\nSee Console for details.",
                "OK"
            );
        }
    }
}
#endif
