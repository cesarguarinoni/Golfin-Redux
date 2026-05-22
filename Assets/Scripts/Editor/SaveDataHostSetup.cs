#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Golfin.Save;

/// <summary>
/// Adds SaveDataHost component to the Managers GameObject and sets Script Execution Order to -100.
/// Run: GOLFIN/Setup/Save Data Host
/// </summary>
public static class SaveDataHostSetup
{
    [MenuItem("GOLFIN/Setup/Save Data Host")]
    public static void Setup()
    {
        // ── Find or create the Managers GameObject ────────────────────────────

        var managersGO = GameObject.Find("Managers");
        if (managersGO == null)
        {
            managersGO = new GameObject("Managers");
            Debug.Log("[SaveDataHostSetup] Created 'Managers' GameObject.");
        }

        // ── Add SaveDataHost if not already present ───────────────────────────

        var existing = Object.FindObjectOfType<SaveDataHost>(includeInactive: true);
        if (existing != null)
        {
            Debug.Log($"[SaveDataHostSetup] SaveDataHost already exists on '{existing.gameObject.name}'.");
            SetExecutionOrder();
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        managersGO.AddComponent<SaveDataHost>();
        Debug.Log("[SaveDataHostSetup] Added SaveDataHost to 'Managers' GameObject.");

        // ── Set Script Execution Order to -100 ────────────────────────────────

        SetExecutionOrder();
        Selection.activeGameObject = managersGO;
        EditorUtility.SetDirty(managersGO);
    }

    /// <summary>
    /// Sets SaveDataHost Script Execution Order to -100 in Project Settings.
    /// This ensures it Awakes before all managers.
    /// </summary>
    public static void SetExecutionOrder()
    {
        var allScripts = MonoImporter.GetAllRuntimeMonoScripts();
        foreach (var script in allScripts)
        {
            if (script == null) continue;
            if (script.GetClass() == typeof(SaveDataHost))
            {
                int current = MonoImporter.GetExecutionOrder(script);
                if (current != -100)
                {
                    MonoImporter.SetExecutionOrder(script, -100);
                    Debug.Log($"[SaveDataHostSetup] Set SaveDataHost execution order to -100 (was {current}).");
                }
                else
                {
                    Debug.Log("[SaveDataHostSetup] SaveDataHost execution order already -100.");
                }
                return;
            }
        }
        Debug.LogWarning("[SaveDataHostSetup] SaveDataHost MonoScript not found — execution order not set. " +
                         "Recompile and run this setup again.");
    }

    [MenuItem("GOLFIN/Setup/Save Data Host", isValidateFunction: true)]
    static bool Validate() => !EditorApplication.isPlaying;
}
#endif
