#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;

/// <summary>
/// Temporary editor helper to capture Frame Debugger evidence for
/// puttpath_predictor_perf_and_design task review.
/// DO NOT SHIP — remove after review is complete.
/// </summary>
public class FrameDebuggerCapture
{
    [MenuItem("GOLFIN/Diagnostics/Enable Frame Debugger And Log DrawCalls")]
    static void EnableFrameDebuggerAndLog()
    {
        // Find FrameDebuggerUtility via reflection (internal Unity type)
        System.Type fdUtilType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            fdUtilType = asm.GetType("UnityEditorInternal.FrameDebuggerUtility");
            if (fdUtilType != null) break;
        }

        if (fdUtilType == null)
        {
            Debug.LogError("[FDCapture] FrameDebuggerUtility not found in any loaded assembly.");
            return;
        }

        var enabledProp = fdUtilType.GetProperty("enabled",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var countProp   = fdUtilType.GetProperty("count",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var limitProp   = fdUtilType.GetProperty("limit",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Debug.Log($"[FDCapture] Props — enabled={enabledProp != null} count={countProp != null} limit={limitProp != null}");

        // Enable frame capture
        if (enabledProp != null && enabledProp.CanWrite)
        {
            enabledProp.SetValue(null, true);
        }

        // Delay one frame to let Frame Debugger capture
        EditorApplication.delayCall += () =>
        {
            bool isEnabled   = enabledProp != null ? (bool)enabledProp.GetValue(null)  : false;
            int  drawCalls   = countProp   != null ? (int)countProp.GetValue(null)      : -1;
            int  currentLimit = limitProp  != null ? (int)limitProp.GetValue(null)      : -1;

            Debug.Log($"[FDCapture] FrameDebugger enabled={isEnabled} drawCallCount={drawCalls} limit={currentLimit}");

            // Also open the Frame Debugger window
            EditorApplication.ExecuteMenuItem("Window/Analysis/Frame Debugger");

            Debug.Log("[FDCapture] Frame Debugger window opened via menu. Take a screenshot now.");
        };
    }
}
#endif
