#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Golfin.Save;

/// <summary>
/// Automatically ensures SaveDataHost Script Execution Order = -100 whenever scripts reload.
/// This is safe — it only updates project metadata, never enters play mode.
/// </summary>
[InitializeOnLoad]
public static class SaveDataHostExecutionOrder
{
    static SaveDataHostExecutionOrder()
    {
        EnsureExecutionOrder();
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    static void EnsureExecutionOrder()
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
                    Debug.Log("[SaveDataHostExecutionOrder] Set SaveDataHost execution order to -100.");
                }
                return;
            }
        }
    }
}
#endif
