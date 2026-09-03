// gps_standalone_shell round 2, R4 — the game screens still shipped.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Strips the screens the PLAYLIFE shell refuses out of ShellScene's IN-MEMORY copy during a
    /// standalone build. The file on disk is never touched.
    ///
    /// <para>
    /// WHY, given <see cref="StandaloneGate"/> already refuses them. The gate decides
    /// REACHABILITY, not what ships. ShellScene carries every game screen as an inactive child of
    /// <c>ScreensRoot</c>, and an inactive GameObject still drags in every sprite, material, font
    /// and prefab it references — so build 2635 shipped the Roster, the shop, the bags, the
    /// rankings and the gacha banner (~35 MB of art) inside an app that will not open any of them.
    /// Destroying the container is what makes the references go away.
    /// </para>
    /// <para>
    /// SAME MECHANISM AS <see cref="GolfinRedux.DemoEditor.DemoSceneProcessor"/>, deliberately —
    /// including its lesson: <c>OnProcessScene</c> also fires when ENTERING PLAY MODE, with a null
    /// <paramref name="report"/>. This class does nothing in that case unless
    /// <see cref="ForceStandaloneStrip"/> was set on purpose, because a processor that quietly
    /// destroyed ten screens out of the scene Cesar has open would look exactly like scene
    /// corruption (and once did, in <c>loop_v1</c> iter-12).
    /// </para>
    /// <para>
    /// THE LIST IS NOT HARD-CODED. It is derived from <see cref="StandaloneGate.IsShellScreen"/>,
    /// the same predicate the runtime gate uses, so a GPS screen added later is kept without this
    /// file being told — and a golf screen added later is stripped for the same reason.
    /// </para>
    /// </summary>
    public sealed class StandaloneSceneProcessor : IProcessSceneWithReport
    {
        const string Tag = "[StandaloneStrip]";
        const string ShellSceneName = "ShellScene";
        const string Suffix = "Screen";

        /// <summary>
        /// Set by <see cref="CIBuild.BuildIOSStandalone"/> around <c>BuildPipeline.BuildPlayer</c>.
        /// Required because a build profile's scripting defines never reach editor assemblies, so
        /// this class cannot ask <c>#if GOLFIN_STANDALONE</c> whether it is the standalone being
        /// built — the same gap <c>DemoSceneProcessor.ForceDemoStrip</c> exists to cover.
        /// </summary>
        internal static bool ForceStandaloneStrip;

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!ForceStandaloneStrip) return;              // play mode, or any other build
            if (scene.name != ShellSceneName) return;

            ScreenManager sm = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                sm = root.GetComponentInChildren<ScreenManager>(includeInactive: true);
                if (sm != null) break;
            }
            if (sm == null)
            {
                Debug.LogWarning($"{Tag} no ScreenManager in '{scene.name}' — nothing stripped.");
                return;
            }

            var stripped = new List<string>();
            var kept = new List<string>();

            foreach (var field in typeof(ScreenManager).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(GameObject)) continue;
                if (!field.Name.StartsWith("_") || !field.Name.EndsWith(Suffix)) continue;

                // "_rosterScreen" -> "roster" -> "Roster"
                string mid = field.Name.Substring(1, field.Name.Length - 1 - Suffix.Length);
                if (mid.Length == 0) continue;
                string enumName = char.ToUpperInvariant(mid[0]) + mid.Substring(1);
                if (!Enum.TryParse(enumName, out ScreenId id)) continue;

                if (StandaloneGate.IsShellScreen(id)) { kept.Add(enumName); continue; }

                if (!(field.GetValue(sm) is GameObject go) || go == null) continue;

                UnityEngine.Object.DestroyImmediate(go);

                // Null the field as well as destroying the object. ScreenManager null-guards every
                // container, but a DESTROYED reference is Unity's "fake null": it compares equal to
                // null and throws the moment anything touches a member. An explicitly nulled field
                // is the state the null-guards were written for.
                field.SetValue(sm, null);
                stripped.Add(enumName);
            }

            Debug.Log($"{Tag} stripped {stripped.Count} refused screen container(s) from '{scene.name}': " +
                      string.Join(", ", stripped));
            Debug.Log($"{Tag} kept {kept.Count} shell screen(s): " + string.Join(", ", kept));
        }
    }
}
