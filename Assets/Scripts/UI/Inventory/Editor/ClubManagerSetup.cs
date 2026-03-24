#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Inventory;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Creates ClubDatabaseCSV and ClubManager GameObjects in the open scene,
    /// wires Clubs.csv, and sets Script Execution Order so CSV runs before Manager.
    ///
    /// Run: GOLFIN/Setup Club Managers
    /// </summary>
    public static class ClubManagerSetup
    {
        private const string CSV_ASSET_PATH = "Assets/Data/Clubs.csv";

        [MenuItem("GOLFIN/Setup/Club Managers")]
        public static void Run()
        {
            bool changed = false;
            changed |= SetupClubDatabaseCSV();
            changed |= SetupClubManager();
            SetScriptExecutionOrder();

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("Club Managers",
                "Done!\n\n" +
                "• ClubDatabaseCSV — root GameObject, Clubs.csv wired\n" +
                "• ClubManager     — root GameObject\n" +
                "• Execution order: ClubDatabaseCSV (-90) → ClubManager (-80)\n\n" +
                "Hit Play to verify both singletons initialise correctly.",
                "OK");
        }

        // ── ClubDatabaseCSV ───────────────────────────────────────────────────

        private static bool SetupClubDatabaseCSV()
        {
            // Reuse existing if present
            var existing = Object.FindObjectOfType<ClubDatabaseCSV>();
            if (existing != null)
            {
                Debug.Log("[ClubManagerSetup] ClubDatabaseCSV already in scene — skipping creation.");
                WireCSV(existing);
                return false;
            }

            var go = new GameObject("ClubDatabaseCSV");
            var comp = go.AddComponent<ClubDatabaseCSV>();
            WireCSV(comp);

            Debug.Log("[ClubManagerSetup] ✓ Created ClubDatabaseCSV.");
            return true;
        }

        private static void WireCSV(ClubDatabaseCSV comp)
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CSV_ASSET_PATH);
            if (csv == null)
            {
                Debug.LogWarning($"[ClubManagerSetup] Clubs.csv not found at '{CSV_ASSET_PATH}'. " +
                                  "Assign it manually in the Inspector.");
                return;
            }

            var so = new SerializedObject(comp);
            var prop = so.FindProperty("clubsCSV");
            if (prop != null)
            {
                prop.objectReferenceValue = csv;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(comp);
                Debug.Log("[ClubManagerSetup] ✓ Clubs.csv wired.");
            }
        }

        // ── ClubManager ───────────────────────────────────────────────────────

        private static bool SetupClubManager()
        {
            var existing = Object.FindObjectOfType<ClubManager>();
            if (existing != null)
            {
                Debug.Log("[ClubManagerSetup] ClubManager already in scene — skipping creation.");
                return false;
            }

            new GameObject("ClubManager").AddComponent<ClubManager>();
            Debug.Log("[ClubManagerSetup] ✓ Created ClubManager.");
            return true;
        }

        // ── Script Execution Order ────────────────────────────────────────────

        private static void SetScriptExecutionOrder()
        {
            SetOrder<ClubDatabaseCSV>(-90);
            SetOrder<ClubManager>(-80);
            Debug.Log("[ClubManagerSetup] ✓ Execution order: ClubDatabaseCSV=-90, ClubManager=-80.");
        }

        private static void SetOrder<T>(int order) where T : MonoBehaviour
        {
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script.GetClass() == typeof(T))
                {
                    if (MonoImporter.GetExecutionOrder(script) != order)
                        MonoImporter.SetExecutionOrder(script, order);
                    return;
                }
            }
            Debug.LogWarning($"[ClubManagerSetup] Could not find MonoScript for {typeof(T).Name}.");
        }
    }
}
#endif
