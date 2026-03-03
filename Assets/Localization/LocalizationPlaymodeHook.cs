// Assets/Scripts/Localization/Editor/LocalizationPlaymodeHook.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LocalizationPlaymodeHook
{
    static LocalizationPlaymodeHook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // When we’re about to enter play mode
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Auto-import CSV (no log spam)
            LocalizationTextImporter.ImportCsv(logResult: true);
        }
    }
}
#endif
