#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Inventory;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Creates BallDatabaseCSV and BallManager GameObjects in the open scene,
    /// wires Balls.csv, and sets Script Execution Order so CSV runs before Manager.
    ///
    /// Run: GOLFIN/Setup/Ball Managers
    /// </summary>
    public static class BallManagerSetup
    {
        private const string CSV_ASSET_PATH = "Assets/Data/Balls.csv";

        [MenuItem("GOLFIN/Setup/Ball Managers")]
        public static void Run()
        {
            bool changed = false;
            changed |= SetupBallDatabaseCSV();
            changed |= SetupBallManager();
            SetScriptExecutionOrder();

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("Ball Managers",
                "Done!\n\n" +
                "• BallDatabaseCSV — root GameObject, Balls.csv wired\n" +
                "• BallManager     — root GameObject\n" +
                "• Execution order: BallDatabaseCSV (-70) → BallManager (-60)\n\n" +
                "Hit Play to verify both singletons initialise correctly.",
                "OK");
        }

        // ── BallDatabaseCSV ───────────────────────────────────────────────────

        private static bool SetupBallDatabaseCSV()
        {
            var existing = Object.FindObjectOfType<BallDatabaseCSV>();
            if (existing != null)
            {
                Debug.Log("[BallManagerSetup] BallDatabaseCSV already in scene — skipping creation.");
                WireCSV(existing);
                return false;
            }

            var go   = new GameObject("BallDatabaseCSV");
            var comp = go.AddComponent<BallDatabaseCSV>();
            WireCSV(comp);

            Debug.Log("[BallManagerSetup] ✓ Created BallDatabaseCSV.");
            return true;
        }

        private static void WireCSV(BallDatabaseCSV comp)
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CSV_ASSET_PATH);
            if (csv == null)
            {
                Debug.LogWarning($"[BallManagerSetup] Balls.csv not found at '{CSV_ASSET_PATH}'. " +
                                  "Assign it manually in the Inspector.");
                return;
            }

            var so   = new SerializedObject(comp);
            var prop = so.FindProperty("ballsCSV");
            if (prop != null)
            {
                prop.objectReferenceValue = csv;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(comp);
                Debug.Log("[BallManagerSetup] ✓ Balls.csv wired.");
            }
        }

        // ── BallManager ───────────────────────────────────────────────────────

        private static bool SetupBallManager()
        {
            var existing = Object.FindObjectOfType<BallManager>();
            if (existing != null)
            {
                Debug.Log("[BallManagerSetup] BallManager already in scene — skipping creation.");
                return false;
            }

            new GameObject("BallManager").AddComponent<BallManager>();
            Debug.Log("[BallManagerSetup] ✓ Created BallManager.");
            return true;
        }

        // ── Script Execution Order ────────────────────────────────────────────

        private static void SetScriptExecutionOrder()
        {
            SetOrder<BallDatabaseCSV>(-70);
            SetOrder<BallManager>(-60);
            Debug.Log("[BallManagerSetup] ✓ Execution order: BallDatabaseCSV=-70, BallManager=-60.");
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
            Debug.LogWarning($"[BallManagerSetup] Could not find MonoScript for {typeof(T).Name}.");
        }
    }
}
#endif
