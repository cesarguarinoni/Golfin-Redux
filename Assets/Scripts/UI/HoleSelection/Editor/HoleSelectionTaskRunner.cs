using System.IO;
using UnityEditor;
using UnityEngine;
using Golfin.EditorTools;

namespace GolfinRedux.UI.HoleSelection.Editor
{
    /// <summary>
    /// File-triggered task runner for hole_selection_screen smoke test.
    /// On each editor update, checks for a trigger file at:
    ///   Docs/Diagnostics/_capture/hole_sel_trigger.txt
    /// If found, runs the specified task and deletes the trigger.
    ///
    /// Supported commands in trigger file:
    ///   RUN_PREREQUISITES   — runs localization import + sprite config
    ///   RUN_AUTOWIRE        — runs GOLFIN/Wire/Hole Selection
    ///   SNAP_EDITMODE       — takes a Game View snapshot in EditMode
    ///
    /// This does NOT enter play mode (safe per CLAUDE.md rules).
    /// </summary>
    [InitializeOnLoad]
    public static class HoleSelectionTaskRunner
    {
        private const string TRIGGER_FILE = "Docs/Diagnostics/_capture/hole_sel_trigger.txt";
        private const string RESULT_FILE  = "Docs/Diagnostics/_capture/hole_sel_result.txt";

        static HoleSelectionTaskRunner()
        {
            EditorApplication.update += CheckTrigger;
        }

        private static void CheckTrigger()
        {
            if (!File.Exists(TRIGGER_FILE)) return;

            string cmd = File.ReadAllText(TRIGGER_FILE).Trim();
            File.Delete(TRIGGER_FILE);

            Debug.Log($"[HoleSelectionTaskRunner] Got trigger: {cmd}");
            var result = new System.Text.StringBuilder();
            result.AppendLine($"COMMAND: {cmd}");
            result.AppendLine($"TIME: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            try
            {
                switch (cmd)
                {
                    case "RUN_AUTOWIRE":
                        bool wireOk = EditorApplication.ExecuteMenuItem("GOLFIN/Wire/Hole Selection");
                        result.AppendLine($"GOLFIN/Wire/Hole Selection: {(wireOk ? "executed" : "not found")}");
                        break;

                    case "RUN_PREREQUISITES":
                        bool locOk = EditorApplication.ExecuteMenuItem("Tools/Localization/Import Text CSV");
                        result.AppendLine($"Tools/Localization/Import Text CSV: {(locOk ? "executed" : "not found")}");
                        bool spriteOk = EditorApplication.ExecuteMenuItem("GOLFIN/Setup/Configure Hole Images as Sprites");
                        result.AppendLine($"GOLFIN/Setup/Configure Hole Images as Sprites: {(spriteOk ? "executed" : "not found")}");
                        break;

                    case "SNAP_EDITMODE":
                        string snap = CaptureHelper.SnapGameViewWithLabel("hole-sel-verification");
                        result.AppendLine($"SNAP: {snap}");
                        // Copy to task screenshots dir
                        string dest = "Docs/Specs/Active/hole_selection_screen/screenshots";
                        Directory.CreateDirectory(dest);
                        string destFile = $"{dest}/editmode_verification.png";
                        File.Copy(snap, destFile, overwrite: true);
                        result.AppendLine($"COPIED_TO: {destFile}");
                        break;

                    case "ALL":
                        // Run all prerequisites then snap
                        bool w = EditorApplication.ExecuteMenuItem("GOLFIN/Wire/Hole Selection");
                        result.AppendLine($"GOLFIN/Wire/Hole Selection: {(w ? "executed" : "not found")}");
                        bool l = EditorApplication.ExecuteMenuItem("Tools/Localization/Import Text CSV");
                        result.AppendLine($"Tools/Localization/Import Text CSV: {(l ? "executed" : "not found")}");
                        bool s = EditorApplication.ExecuteMenuItem("GOLFIN/Setup/Configure Hole Images as Sprites");
                        result.AppendLine($"GOLFIN/Setup/Configure Hole Images as Sprites: {(s ? "executed" : "not found")}");
                        string snap2 = CaptureHelper.SnapGameViewWithLabel("hole-sel-after-setup");
                        result.AppendLine($"SNAP: {snap2}");
                        string dest2 = "Docs/Specs/Active/hole_selection_screen/screenshots";
                        Directory.CreateDirectory(dest2);
                        File.Copy(snap2, $"{dest2}/editmode_after_setup.png", overwrite: true);
                        result.AppendLine($"COPIED_TO: {dest2}/editmode_after_setup.png");
                        break;

                    default:
                        result.AppendLine($"UNKNOWN COMMAND: {cmd}");
                        break;
                }
                result.AppendLine("STATUS: OK");
            }
            catch (System.Exception ex)
            {
                result.AppendLine($"ERROR: {ex.Message}");
                result.AppendLine("STATUS: FAIL");
            }

            File.WriteAllText(RESULT_FILE, result.ToString());
            Debug.Log($"[HoleSelectionTaskRunner] Result written to {RESULT_FILE}");
        }
    }
}
