using System;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using GolfinRedux.Demo;
using GolfinRedux.UI;

namespace GolfinRedux.DemoEditor
{
    /// <summary>
    /// Build-time (and play-mode) scene stripper for the demo (demo_build_slice §3.3).
    /// Runs on the IN-MEMORY copy of ShellScene during a build — the file on disk is
    /// never touched. Destroys every ScreenManager screen container whose ScreenId is
    /// not on the DemoGate allowlist, so full-game screen GameObjects AND their
    /// sprite/prefab references are genuinely absent from the demo binary (real size
    /// win, unlike an asmdef split which removes IL but leaves the art).
    ///
    /// ScreenManager.ApplyScreen null-guards every container, so a destroyed screen
    /// becomes a logged no-op — no runtime code change is needed to survive stripping.
    ///
    /// NOTE (§3.6): OnProcessScene ALSO fires when entering play mode, and in that case
    /// `report` is NULL (same for Addressables builds). This method never dereferences
    /// `report`, so it is safe in play mode — where it lets you test the stripping — as
    /// well as in a player build. Do NOT add `report.summary.*` reads without a null check.
    /// </summary>
    public sealed class DemoSceneProcessor : IProcessSceneWithReport
    {
        const string ShellSceneName = "ShellScene";
        const string Tag = "[DemoSceneProcessor]";
        const string Suffix = "Screen";

        /// <summary>
        /// Set true by DemoBuild around BuildPlayer so this stripper runs in a batchmode
        /// demo build even when the editor's own assemblies were compiled without
        /// GOLFIN_DEMO (the editor's active build profile can differ from the profile being
        /// built — that gap is why iter-1 shipped the full-game screens unstripped).
        /// </summary>
        internal static bool ForceDemoStrip;

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // report == null => entering play mode (or an Addressables build). NOT an error.
            if (!ForceDemoStrip && !DemoGate.IsDemo) return;
            if (scene.name != ShellSceneName) return;

            ScreenManager sm = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                sm = root.GetComponentInChildren<ScreenManager>(includeInactive: true);
                if (sm != null) break;
            }
            if (sm == null)
            {
                Debug.LogWarning($"{Tag} No ScreenManager found in '{scene.name}' — nothing stripped.");
                return;
            }

            int stripped = 0;
            foreach (var field in typeof(ScreenManager).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(GameObject)) continue;
                if (!field.Name.StartsWith("_") || !field.Name.EndsWith(Suffix)) continue;

                // "_rosterScreen" -> "roster" -> "Roster"
                string mid = field.Name.Substring(1, field.Name.Length - 1 - Suffix.Length);
                if (mid.Length == 0) continue;
                string enumName = char.ToUpperInvariant(mid[0]) + mid.Substring(1);
                if (!Enum.TryParse<ScreenId>(enumName, out var id)) continue;
                if (DemoGate.IsOnAllowlist(id)) continue; // keep allowlisted screens (IsDemo-independent)

                if (field.GetValue(sm) is GameObject go && go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    stripped++;
                    Debug.Log($"{Tag} stripped '{enumName}' screen container.");
                }
            }

            Debug.Log($"{Tag} demo strip complete — {stripped} screen container(s) removed from '{scene.name}'.");
        }
    }
}
