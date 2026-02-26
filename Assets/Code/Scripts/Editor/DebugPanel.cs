using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Debug panel for testing features in-editor and at runtime.
/// New features register their debug toggles here.
/// 
/// Usage: Tools → Debug Panel (editor window)
/// Runtime: enabled via triple-tap on version text or secret gesture
/// </summary>
public class DebugPanel : EditorWindow
{
    // ═══ REGISTERED DEBUG OPTIONS ═══
    // Each new feature adds its debug toggles here
    static Dictionary<string, bool> toggles = new Dictionary<string, bool>();
    static Dictionary<string, System.Action> actions = new Dictionary<string, System.Action>();

    Vector2 scrollPos;

    [MenuItem("Tools/Debug Panel")]
    public static void ShowWindow()
    {
        var window = GetWindow<DebugPanel>("GOLFIN Debug");
        window.minSize = new Vector2(300, 400);
    }

    /// <summary>Register a toggle for a feature. Call from any editor script.</summary>
    public static void RegisterToggle(string name, bool defaultValue = false)
    {
        if (!toggles.ContainsKey(name))
            toggles[name] = EditorPrefs.GetBool($"GOLFIN_Debug_{name}", defaultValue);
    }

    /// <summary>Register a button action for testing.</summary>
    public static void RegisterAction(string name, System.Action action)
    {
        actions[name] = action;
    }

    /// <summary>Check if a debug toggle is enabled.</summary>
    public static bool IsEnabled(string name)
    {
        return toggles.ContainsKey(name) && toggles[name];
    }

    void OnEnable()
    {
        // Register built-in debug options
        RegisterToggle("Show Screen Bounds", false);
        RegisterToggle("Show Touch Targets", false);
        RegisterToggle("Show Font Info", false);
        RegisterToggle("Skip Loading Screen", false);
        RegisterToggle("Force English", false);
        RegisterToggle("Show FPS", false);

        RegisterAction("Reload Localization", () => {
            var lm = Object.FindAnyObjectByType<LocalizationManager>();
            if (lm != null)
            {
                // LoadCSV is private — invoke via reflection to avoid modifying LocalizationManager
                var method = lm.GetType().GetMethod("LoadCSV", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null) { method.Invoke(lm, null); Debug.Log("[Debug] Localization reloaded"); }
                else Debug.LogWarning("[Debug] LoadCSV method not found");
            }
        });

        RegisterAction("Export Scene Values", SceneToCodeSync.ExportSceneValues);

        RegisterAction("Capture Screenshots", ScreenshotCapture.CaptureAllScreens);

        RegisterAction("Cycle Loading Tips", () => {
            var tip = Object.FindAnyObjectByType<ProTipCard>();
            if (tip != null) { tip.NextTip(); Debug.Log("[Debug] Next tip"); }
        });

        // Home Screen debug actions
        RegisterAction("Randomize Character", () => {
            var home = Object.FindAnyObjectByType<HomeScreen>();
            if (home != null) { home.RandomizeCharacter(); Debug.Log("[Debug] Character randomized"); }
        });

        RegisterAction("Next Announcement", () => {
            var home = Object.FindAnyObjectByType<HomeScreen>();
            if (home != null) { home.NextAnnouncement(); Debug.Log("[Debug] Next announcement"); }
        });

        RegisterAction("Go To Home Screen", () => {
            var home = Object.FindAnyObjectByType<HomeScreen>();
            if (home != null) { ScreenManager.Instance.TransitionTo(home); Debug.Log("[Debug] Switched to Home"); }
        });
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("GOLFIN Debug Panel", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // ─── Toggles ─────────────────────────────────────────
        GUILayout.Label("Feature Toggles", EditorStyles.boldLabel);
        var keys = new List<string>(toggles.Keys);
        foreach (var key in keys)
        {
            bool prev = toggles[key];
            toggles[key] = EditorGUILayout.Toggle(key, toggles[key]);
            if (toggles[key] != prev)
                EditorPrefs.SetBool($"GOLFIN_Debug_{key}", toggles[key]);
        }

        GUILayout.Space(10);

        // ─── Actions ─────────────────────────────────────────
        GUILayout.Label("Quick Actions", EditorStyles.boldLabel);
        foreach (var kvp in actions)
        {
            if (GUILayout.Button(kvp.Key))
                kvp.Value?.Invoke();
        }

        GUILayout.Space(10);

        // ─── Screen Navigation ───────────────────────────────
        GUILayout.Label("Screen Navigation", EditorStyles.boldLabel);
        var screens = Object.FindObjectsByType<ScreenBase>(FindObjectsSortMode.None);
        foreach (var s in screens)
        {
            if (GUILayout.Button($"Show: {s.gameObject.name}"))
            {
                // Hide all, show this one
                foreach (var other in screens)
                {
                    var cg = other.GetComponent<CanvasGroup>();
                    if (cg != null) { cg.alpha = 0; cg.blocksRaycasts = false; }
                }
                var thisCg = s.GetComponent<CanvasGroup>();
                if (thisCg != null) { thisCg.alpha = 1; thisCg.blocksRaycasts = true; }
                Selection.activeGameObject = s.gameObject;
            }
        }

        GUILayout.Space(10);

        // ─── Info ────────────────────────────────────────────
        GUILayout.Label("Info", EditorStyles.boldLabel);
        GUILayout.Label($"Reference: 1170×2532");
        GUILayout.Label($"Game View: {Screen.width}×{Screen.height}");
        GUILayout.Label($"Screens found: {screens.Length}");

        EditorGUILayout.EndScrollView();
    }
}
