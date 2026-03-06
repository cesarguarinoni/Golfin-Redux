#nullable enable
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Golfin.Roster;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// One-click setup for Roster System Phase 1
    /// Creates and wires all necessary GameObjects and managers
    /// Menu: Tools → GOLFIN → Setup Roster System (Phase 1)
    /// </summary>
    public class RosterSystemSetupTool
    {
        private const string SETUP_COMPLETE_MESSAGE = 
            "✅ Roster System Phase 1 Setup Complete!\n\n" +
            "Created:\n" +
            "• CharacterManager (with CharacterDatabase)\n" +
            "• CharacterLevelUpDatabase\n" +
            "• RewardPointsManager\n\n" +
            "All references are wired. You're ready for Phase 2 UI!";
        
        [MenuItem("Tools/GOLFIN/Setup Roster System (Phase 1)")]
        public static void SetupRosterSystem()
        {
            Debug.Log("[RosterSystemSetupTool] Starting Roster System Phase 1 setup...");
            
            try
            {
                // Create managers
                CreateCharacterManager();
                CreateCharacterLevelUpDatabase();
                CreateRewardPointsManager();
                
                // Wire references
                WireManagerReferences();
                
                // Load CSV
                LoadAndAssignCSV();
                
                // Success
                EditorUtility.DisplayDialog(
                    "Roster System Setup",
                    SETUP_COMPLETE_MESSAGE,
                    "OK"
                );
                
                Debug.Log("[RosterSystemSetupTool] ✅ Setup complete!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RosterSystemSetupTool] Setup failed: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog(
                    "Setup Failed",
                    $"Error: {e.Message}",
                    "OK"
                );
            }
        }
        
        private static void CreateCharacterManager()
        {
            Debug.Log("[RosterSystemSetupTool] Creating CharacterManager...");
            
            // Check if already exists
            var existing = Object.FindObjectOfType<CharacterManager>();
            if (existing != null)
            {
                Debug.LogWarning("[RosterSystemSetupTool] CharacterManager already exists in scene");
                return;
            }
            
            // Create GameObject
            var go = new GameObject("CharacterManager");
            var manager = go.AddComponent<CharacterManager>();
            
            // Find or create CharacterDatabase
            var database = FindOrCreateCharacterDatabase();
            
            // Assign CharacterDatabase via SerializedObject
            var so = new SerializedObject(manager);
            var dbField = so.FindProperty("characterDatabase");
            
            if (dbField != null)
                dbField.objectReferenceValue = database;
            
            so.ApplyModifiedProperties();
            
            EditorUtility.SetDirty(manager);
            Debug.Log("[RosterSystemSetupTool] ✓ CharacterManager created");
        }
        
        private static void CreateCharacterLevelUpDatabase()
        {
            Debug.Log("[RosterSystemSetupTool] Creating CharacterLevelUpDatabase...");
            
            // Check if already exists
            var existing = Object.FindObjectOfType<CharacterLevelUpDatabase>();
            if (existing != null)
            {
                Debug.LogWarning("[RosterSystemSetupTool] CharacterLevelUpDatabase already exists in scene");
                return;
            }
            
            // Create GameObject
            var go = new GameObject("CharacterLevelUpDatabase");
            var database = go.AddComponent<CharacterLevelUpDatabase>();
            
            // Find CSV asset
            var csv = FindCSVAsset();
            if (csv != null)
            {
                var so = new SerializedObject(database);
                var csvField = so.FindProperty("levelUpCostsCsv");
                
                if (csvField != null)
                    csvField.objectReferenceValue = csv;
                
                so.ApplyModifiedProperties();
                Debug.Log("[RosterSystemSetupTool] ✓ CSV assigned to CharacterLevelUpDatabase");
            }
            else
            {
                Debug.LogWarning("[RosterSystemSetupTool] CharacterLevelUpCosts.csv not found! You need to assign it manually.");
            }
            
            EditorUtility.SetDirty(database);
            Debug.Log("[RosterSystemSetupTool] ✓ CharacterLevelUpDatabase created");
        }
        
        private static void CreateRewardPointsManager()
        {
            Debug.Log("[RosterSystemSetupTool] Creating RewardPointsManager...");
            
            // Check if already exists
            var existing = Object.FindObjectOfType<RewardPointsManager>();
            if (existing != null)
            {
                Debug.LogWarning("[RosterSystemSetupTool] RewardPointsManager already exists in scene");
                return;
            }
            
            // Create GameObject
            var go = new GameObject("RewardPointsManager");
            var manager = go.AddComponent<RewardPointsManager>();
            
            EditorUtility.SetDirty(manager);
            Debug.Log("[RosterSystemSetupTool] ✓ RewardPointsManager created");
        }
        
        private static CharacterDatabase FindOrCreateCharacterDatabase()
        {
            // Try to find existing asset
            var guids = AssetDatabase.FindAssets("t:CharacterDatabase");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(path);
                if (database != null)
                {
                    Debug.Log($"[RosterSystemSetupTool] Found existing CharacterDatabase at {path}");
                    return database;
                }
            }
            
            // Create new asset
            Debug.Log("[RosterSystemSetupTool] Creating new CharacterDatabase asset...");
            var newDatabase = ScriptableObject.CreateInstance<CharacterDatabase>();
            
            // Ensure directory exists
            if (!System.IO.Directory.Exists("Assets/Data"))
                System.IO.Directory.CreateDirectory("Assets/Data");
            
            var assetPath = "Assets/Data/CharacterDatabase.asset";
            AssetDatabase.CreateAsset(newDatabase, assetPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[RosterSystemSetupTool] Created CharacterDatabase at {assetPath}");
            return newDatabase;
        }
        
        private static TextAsset? FindCSVAsset()
        {
            var guids = AssetDatabase.FindAssets("CharacterLevelUpCosts t:TextAsset");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                Debug.Log($"[RosterSystemSetupTool] Found CSV at {path}");
                return csv;
            }
            
            return null;
        }
        
        private static void WireManagerReferences()
        {
            Debug.Log("[RosterSystemSetupTool] Wiring manager references...");
            
            var charManager = Object.FindObjectOfType<CharacterManager>();
            var levelUpDb = Object.FindObjectOfType<CharacterLevelUpDatabase>();
            
            if (charManager == null || levelUpDb == null)
            {
                Debug.LogWarning("[RosterSystemSetupTool] Could not wire managers - not all found");
                return;
            }
            
            // Assign levelUpDatabase to CharacterManager
            var so = new SerializedObject(charManager);
            var levelUpDbField = so.FindProperty("levelUpDatabase");
            
            if (levelUpDbField != null)
            {
                levelUpDbField.objectReferenceValue = levelUpDb;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(charManager);
                Debug.Log("[RosterSystemSetupTool] ✓ CharacterLevelUpDatabase assigned to CharacterManager");
            }
        }
        
        private static void LoadAndAssignCSV()
        {
            // This is handled by CharacterLevelUpDatabase.Awake() automatically
            // No manual assignment needed
            Debug.Log("[RosterSystemSetupTool] CSV will be loaded automatically by CharacterLevelUpDatabase");
        }
    }
}
#endif
