#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CaptureSceneSetup = Golfin.Physics.Viewer.Editor.CaptureSceneSetup;

namespace Golfin.SceneHygiene
{
    /// <summary>
    /// Always-on safety net against a generated <c>Hole_NN_Geo</c> scene surviving into the editor
    /// hierarchy (spec: <c>Docs/Specs/Active/hole_scene_leftover_v3/SPEC.md</c>, Layer 2).
    ///
    /// Layer 1 — reload-proof cleanup in the EditMode fixtures that stage hole scenes — is the
    /// actual fix. This guard exists because there are 26 files that reference a hole-geo scene
    /// path and only a handful route through <see cref="CaptureSceneSetup"/>: any present or
    /// future launcher, recorder or fixture that dies before its own restore is covered here.
    ///
    /// AUTHORING PROTECTION is the load-bearing part of the design. A scene is closed only when
    /// ALL of the following hold:
    ///
    ///   a. its name is a generated hole scene (<see cref="CaptureSceneSetup.IsHoleGeoScene"/>);
    ///   b. it is NOT the active scene              — authoring a hole makes it active;
    ///   c. it is NOT dirty                         — never destroy unsaved work;
    ///   d. ShellScene is also open                 — the leak signature (see below);
    ///   e. the Editor is not playing / compiling / updating, and no automated run has declared
    ///      a staged-scene window                   — never fire mid-run.
    ///
    /// CONDITION (d) DELIBERATELY DOES NOT INCLUDE LabScaffold (revised after the red-team gate,
    /// 2026-08-11). LabScaffold + a clean non-active hole is the *identical* scene shape to a
    /// deliberate physics-lab session: <c>PhysicsLabAutoRestore</c> loads the developer's saved
    /// hole every time LabScaffold is opened, by design. Treating that shape as a leak meant this
    /// guard closed the hole the Hole Picker had just restored, on the developer's next recompile.
    /// No condition can tell those two apart, so the lab shape is now hands-off and the injection
    /// is fixed at its source instead (PhysicsLabHolePicker.cs — Single-mode-only + post-deferral
    /// re-validation). The shape Cesar actually reported, and the one that reaches
    /// LastSceneManagerSetup.txt, is ShellScene + Hole_NN_Geo — which is what this sweeps.
    ///
    /// Known coverage boundary: a capture launcher that stages LabScaffold and then dies is no
    /// longer swept by this guard. Its own <c>CaptureSceneSetup.Restore</c> covers the normal exit,
    /// and on the next launch LabScaffold + hole is indistinguishable from a lab session.
    ///
    /// The guard NEVER saves a scene. Hole_NN_Geo is generated content with no merge driver.
    ///
    /// Deliberately does NOT reference <c>UnityEditor.TestTools.TestRunner.Api</c> — see the spec.
    /// The <see cref="AssemblyReloadEvents.afterAssemblyReload"/> hook covers the test vector.
    /// </summary>
    [InitializeOnLoad]
    public static class StagedHoleSceneGuard
    {
        const string EnabledPrefKey = "Golfin.SceneHygiene.StagedHoleSceneGuard.Enabled";

        const string MenuSweep  = "GOLFIN/Scene Hygiene/Close Staged Hole Scenes Now";
        const string MenuToggle = "GOLFIN/Scene Hygiene/Guard Enabled";

        // The scenes whose presence marks a hole scene as *leaked* rather than *authored*.
        // LabScaffold is deliberately NOT here — see the condition (d) note in the class summary.
        static readonly string[] HostSceneNames = { "ShellScene" };

        // How long the editor must be idle after a load/reload before the load sweep runs.
        const double LoadSweepSettleSeconds = 3.0;
        static double s_LoadSweepAt;

        static StagedHoleSceneGuard()
        {
            EditorApplication.playModeStateChanged     += OnPlayModeStateChanged;
            AssemblyReloadEvents.afterAssemblyReload   += OnAfterAssemblyReload;

            // Editor-load sweep — deliberately NOT a bare EditorApplication.delayCall.
            // Unity restores the scene setup from LastSceneManagerSetup.txt AFTER
            // [InitializeOnLoad] runs, so a single delayCall RACES that restore: it can fire
            // while the hierarchy is still empty, find nothing, and never retry. Observed
            // losing that race on 2026-08-11 — a leaked ShellScene + Hole_06_Geo survived a
            // full kill/relaunch untouched, which is exactly the symptom this guard exists to
            // stop. Poll instead until the editor is genuinely idle, then sweep once.
            s_LoadSweepAt = EditorApplication.timeSinceStartup + LoadSweepSettleSeconds;
            EditorApplication.update += LoadSweepPump;
        }

        static void LoadSweepPump()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                return;
            if (EditorApplication.timeSinceStartup < s_LoadSweepAt) return;

            EditorApplication.update -= LoadSweepPump;
            Sweep("editor-load");
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        // ── Hooks ──────────────────────────────────────────────────────────────────

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Covers every capture launcher, protected or not — including one that dies before
            // running its own restore.
            if (change == PlayModeStateChange.EnteredEditMode) Sweep("EnteredEditMode");
        }

        // Covers the post-test-run compile. Note this fires BEFORE the scene setup is restored
        // on a fresh editor launch — the editor-load pump above is what covers that case.
        static void OnAfterAssemblyReload() => Sweep("afterAssemblyReload");

        // ── Sweep ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Close every open hole scene that passes all five conditions. Returns how many were
        /// closed. <paramref name="verbose"/> logs the reason when the sweep declines to run,
        /// which is what the manual menu item wants and the automatic hooks do not.
        /// </summary>
        public static int Sweep(string hook, bool verbose = false)
        {
            if (!Enabled)
            {
                if (verbose) Debug.Log("[StagedHoleSceneGuard] Guard is disabled " +
                                       "(GOLFIN > Scene Hygiene > Guard Enabled) — nothing swept.");
                return 0;
            }

            // (e) never fire mid-run.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying
                || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (verbose) Debug.Log("[StagedHoleSceneGuard] Editor is playing/compiling/updating — " +
                                       "skipping sweep.");
                return 0;
            }

            if (CaptureSceneSetup.IsStagedSceneWindowActive(out string owner))
            {
                if (verbose) Debug.Log($"[StagedHoleSceneGuard] '{owner}' has a staged-scene window open — " +
                                       "skipping sweep so a run in progress keeps its scenes.");
                return 0;
            }

            // (d) the leak signature. A hole scene without ShellScene alongside it is either a
            // physics-lab session or deliberate hole authoring — both hands-off.
            if (!HostSceneOpen())
            {
                if (verbose) Debug.Log("[StagedHoleSceneGuard] ShellScene is not open — treating any open " +
                                       "hole scene as a deliberate lab session or hole authoring, leaving it alone.");
                return 0;
            }

            Scene active = SceneManager.GetActiveScene();
            int closed = 0;

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.IsValid() || string.IsNullOrEmpty(s.name)) continue;

                if (!CaptureSceneSetup.IsHoleGeoScene(s.name)) continue;          // (a)

                if (s == active)                                                  // (b)
                {
                    Debug.Log($"[StagedHoleSceneGuard] {s.name} is the ACTIVE scene — " +
                              $"leaving it open (authoring protection, hook={hook}).");
                    continue;
                }

                if (s.isDirty)                                                    // (c)
                {
                    Debug.Log($"[StagedHoleSceneGuard] {s.name} has unsaved changes — " +
                              $"leaving it open and unsaved (authoring protection, hook={hook}).");
                    continue;
                }

                Debug.Log($"[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: " +
                          $"{s.name} (hook={hook}).");
                EditorSceneManager.CloseScene(s, removeScene: true);
                closed++;
            }

            if (closed > 0)
                Debug.Log($"[StagedHoleSceneGuard] Swept {closed} leftover hole scene(s) on {hook}.");
            else if (verbose)
                Debug.Log($"[StagedHoleSceneGuard] Nothing to sweep on {hook} — hierarchy is clean.");

            return closed;
        }

        static bool HostSceneOpen()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.IsValid()) continue;
                foreach (string host in HostSceneNames)
                    if (s.name == host) return true;
            }
            return false;
        }

        // ── Menu ───────────────────────────────────────────────────────────────────

        [MenuItem(MenuSweep, priority = 2200)]
        static void MenuCloseStagedHoleScenesNow() => Sweep("manual menu", verbose: true);

        [MenuItem(MenuToggle, priority = 2201)]
        static void MenuToggleGuard()
        {
            Enabled = !Enabled;
            Debug.Log($"[StagedHoleSceneGuard] Guard {(Enabled ? "ENABLED" : "DISABLED")}.");
        }

        [MenuItem(MenuToggle, validate = true)]
        static bool MenuToggleGuardValidate()
        {
            Menu.SetChecked(MenuToggle, Enabled);
            return true;
        }
    }
}
#endif
