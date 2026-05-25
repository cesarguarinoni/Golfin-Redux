#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Bot-infra editor subscriber that captures [LiveStatProvider] log lines to a per-scenario
    /// file during smoke-bot runs. Subscribed at EnteredPlayMode (same hook pattern as
    /// BotVideoRecorder), unsubscribed at ExitingPlayMode. Active only when the
    /// LoopV2SmokeBot.Armed SessionState flag is true at EnteredPlayMode.
    ///
    /// Output (project-root-relative):
    ///   tasks/loop_v2_smoke_bot/&lt;scenario&gt;/live_stat_log.txt
    ///
    /// Does NOT modify LiveStatProviderHost.cs — this tee belongs entirely to bot infra.
    /// </summary>
    public static class LiveStatLogTee
    {
        const string ArmedKey   = "LoopV2SmokeBot.Armed";
        const string LogPrefix  = "[LiveStatProvider]";

        static StreamWriter _writer;
        static string       _logPath;
        static bool         _active;

        /// <summary>Re-register the playModeStateChanged handler after every domain reload.</summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        static void OnScriptsReloaded()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Only activate if the smoke bot is armed for this run.
                if (!SessionState.GetBool(ArmedKey, false)) return;

                string scenario = LoopV2SmokeBot.Scenario;
                if (string.IsNullOrEmpty(scenario)) scenario = "unknown";

                string dir = $"tasks/loop_v2_smoke_bot/{scenario}";
                Directory.CreateDirectory(dir);
                _logPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath) ?? ".", dir, "live_stat_log.txt"));

                try
                {
                    _writer = new StreamWriter(_logPath, append: false) { AutoFlush = true };
                    _writer.WriteLine($"# LiveStatLogTee — scenario={scenario}");
                    _writer.WriteLine($"# Started {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
                    _writer.WriteLine($"# Captures lines beginning with '{LogPrefix}'");
                    _writer.WriteLine();
                    _active = true;

                    Application.logMessageReceived += OnLogMessage;
                    Debug.Log($"[LiveStatLogTee] Active — tee → {_logPath}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LiveStatLogTee] Failed to open log file: {e.Message}");
                    _active = false;
                }
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Stop();
            }
        }

        static void OnLogMessage(string logString, string stackTrace, LogType type)
        {
            if (!_active || _writer == null) return;
            // Filter: only write lines that contain the [LiveStatProvider] prefix.
            if (logString == null || !logString.Contains(LogPrefix)) return;
            _writer.WriteLine($"[t={Time.realtimeSinceStartup:F2}] {logString}");
        }

        static void Stop()
        {
            if (!_active) return;
            Application.logMessageReceived -= OnLogMessage;
            _active = false;
            try
            {
                _writer?.Flush();
                _writer?.Close();
                _writer = null;
                Debug.Log($"[LiveStatLogTee] Stopped — log written to {_logPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LiveStatLogTee] Stop error: {e.Message}");
            }
        }
    }
}
#endif
