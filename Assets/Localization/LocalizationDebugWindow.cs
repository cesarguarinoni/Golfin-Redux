// Assets/Scripts/Localization/Editor/LocalizationDebugWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class LocalizationDebugWindow : EditorWindow
{
    private static Language _editorDefaultLanguage = Language.English;

    [MenuItem("Tools/Localization/Language Debug")]
    public static void ShowWindow()
    {
        GetWindow<LocalizationDebugWindow>("Localization Language");
    }

    private void OnGUI()
    {
        GUILayout.Label("Default Language (before Play)", EditorStyles.boldLabel);

        _editorDefaultLanguage =
            (Language)EditorGUILayout.EnumPopup("Default Language", _editorDefaultLanguage);

        if (GUILayout.Button("Apply to Bootstrap (Selected Scene)"))
        {
            ApplyToBootstrap();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This sets the default language used by LocalizationBootstrap in the scene.\n" +
            "At runtime you can still switch languages via the debug UI.",
            MessageType.Info
        );
    }

    private void ApplyToBootstrap()
    {
        var bootstrap = FindObjectOfType<LocalizationBootstrap>();
        if (bootstrap == null)
        {
            Debug.LogWarning("LocalizationBootstrap not found in the open scene.");
            return;
        }

        Undo.RecordObject(bootstrap, "Set Default Language");
        bootstrap.SetDefaultLanguageEditor(_editorDefaultLanguage);
        EditorUtility.SetDirty(bootstrap);

        Debug.Log($"Default language set to {_editorDefaultLanguage} on LocalizationBootstrap.");
    }
}
#endif
