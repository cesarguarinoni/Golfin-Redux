#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools.BuildSize
{
    /// <summary>
    /// build_size_diet Phase 2.5 — the smoke-bot AtRest parity gate.
    ///
    /// WHAT IT ANSWERS. Phase 2 rewrote every heightmap and every zones file. Two independent
    /// decoders already agree that the BYTES decode to the same numbers, and the whole EditMode
    /// suite passes identically on both datasets. This is the end-to-end version of the same
    /// question, and the only one that runs the production shot path: fire real shots on a real
    /// hole and compare where the ball STOPS, as raw fixed-point, before and after.
    ///
    /// WHY RAW LONGS AND NOT METRES. <c>Trajectory.finalPosition</c> is <c>fp3</c> — Q32.32 fixed
    /// point. Printing it as a float would round away exactly the difference this gate exists to
    /// catch, and "0.0001 m apart" is not a verdict the SPEC accepts. Every row here is
    /// <c>fp.raw</c>, three longs, compared with ==.
    ///
    /// WHY THE SIMULATION IS READ SYNCHRONOUSLY. PhysicsLabController.Fire runs the sim inside
    /// HandleShotResolved and assigns _previousTrajectory before it returns; the ball animating
    /// down the fairway afterwards is presentation. So the resting place is known in the same
    /// frame as the shot, and this needs no coroutine, no settle timer, and no chance of
    /// sampling a ball that has not finished rolling — which is the usual way an at-rest
    /// comparison goes quietly wrong.
    ///
    /// REFLECTION, DELIBERATELY. <c>PhysicsLabController.LastTrajectory</c> is <c>internal</c> to
    /// Golfin.Physics.Viewer. Widening it for a measurement harness would be the tail wagging
    /// the dog, and Assets/Scripts/Physics/ is under a standing no-edit rule, so this reads it
    /// reflectively and fails loudly if the member ever moves.
    ///
    /// HOW TO RUN (the caller drives play mode; see reference/phase2_atrest_parity.txt):
    ///   1. open LabScaffold + Hole_NN_Geo additively, in that order
    ///   2. enter play mode, wait for the lab to report the hole loaded
    ///   3. call <see cref="RunCurrentHole"/> with a label ("before" / "after")
    ///   4. exit play mode, repeat for the next hole
    /// </summary>
    public static class HoleDataParityBot
    {
        const string Tag = "[HoleDataParityBot]";
        const string ReportDir = "Docs/Specs/Active/build_size_diet/reference";

        /// <summary>
        /// Fired on every hole. Full-swing presets exercise the heightmap over hundreds of metres
        /// of carry and bounce; the putts exercise the green mesh and the zone classification at
        /// centimetre scale. A change to either data file that survived both would have to be
        /// invisible to the whole course.
        /// </summary>
        static readonly string[] Presets =
        {
            "driver_calm", "driver_headwind", "driver_tailwind", "driver_crosswind",
            "iron7_calm", "wedge_100_backspin", "wedge_100_zerospin", "rough_landing",
            "putt_flat_3m", "putt_uphill_6m", "putt_downhill_6m", "putt_crossslope_6m",
        };

        /// <summary>
        /// Fires every preset on whatever hole is currently loaded and appends one row per shot
        /// to <c>reference/atrest_&lt;label&gt;.txt</c>. Must be called IN PLAY MODE.
        /// </summary>
        public static void RunCurrentHole(string label)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError($"{Tag} must run in play mode — the lab wires its baked providers on hole load.");
                return;
            }

            var controllerType = FindType("Golfin.Physics.Viewer.PhysicsLabController");
            if (controllerType == null) { Debug.LogError($"{Tag} PhysicsLabController type not found."); return; }
            var controller = UnityEngine.Object.FindFirstObjectByType(controllerType) as MonoBehaviour;
            if (controller == null) { Debug.LogError($"{Tag} no PhysicsLabController in the loaded scenes."); return; }

            var lastTrajectory = controllerType.GetProperty("LastTrajectory",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fire = controllerType.GetMethod("Fire", BindingFlags.Instance | BindingFlags.Public);
            if (lastTrajectory == null || fire == null)
            {
                Debug.LogError($"{Tag} PhysicsLabController.LastTrajectory / Fire not found — the harness " +
                               "reads them reflectively and one of them has moved. Fix this rather than " +
                               "letting the gate silently pass.");
                return;
            }

            var catalogType = FindType("Golfin.Physics.Viewer.ShotPresetCatalog");
            var all = catalogType?.GetProperty("All", BindingFlags.Static | BindingFlags.Public)?.GetValue(null)
                   ?? catalogType?.GetField("All", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            if (all == null) { Debug.LogError($"{Tag} ShotPresetCatalog.All not found."); return; }

            string hole = CurrentHoleLabel();
            var sb = new StringBuilder();

            foreach (var id in Presets)
            {
                object preset = null;
                foreach (var p in (System.Collections.IEnumerable)all)
                {
                    var pid = p.GetType().GetField("Id")?.GetValue(p) as string
                           ?? p.GetType().GetProperty("Id")?.GetValue(p) as string;
                    if (pid == id) { preset = p; break; }
                }
                if (preset == null) { sb.AppendLine($"{hole,-14} {id,-22}  PRESET NOT FOUND"); continue; }

                ResetForNewHole();

                // Fire runs the sim synchronously and assigns the trajectory before returning.
                fire.Invoke(controller, new[] { preset });

                var traj = lastTrajectory.GetValue(controller);
                if (traj == null) { sb.AppendLine($"{hole,-14} {id,-22}  NO TRAJECTORY"); continue; }

                var t = traj.GetType();
                object finalPos = t.GetField("finalPosition")?.GetValue(traj);
                object termination = t.GetField("termination")?.GetValue(traj);
                var samples = t.GetField("samples")?.GetValue(traj) as System.Collections.ICollection;

                sb.AppendLine($"{hole,-14} {id,-22}  {Raw(finalPos)}  term={termination}  samples={samples?.Count ?? -1}");
            }

            Directory.CreateDirectory(ReportDir);
            var path = $"{ReportDir}/atrest_{label}.txt";
            File.AppendAllText(path, sb.ToString());
            Debug.Log($"{Tag} {hole} '{label}' — {Presets.Length} shots appended to {path}\n{sb}");
        }

        /// <summary>fp3 as three raw Q32.32 longs. Never as metres — see the class docs.</summary>
        static string Raw(object fp3)
        {
            if (fp3 == null) return "x=<null> y=<null> z=<null>";
            var t = fp3.GetType();
            string One(string axis)
            {
                var v = t.GetField(axis)?.GetValue(fp3);
                if (v == null) return "<null>";
                var raw = v.GetType().GetField("raw")?.GetValue(v);
                return raw == null ? "<no raw>" : ((long)raw).ToString();
            }
            return $"x={One("x"),22} y={One("y"),22} z={One("z"),22}";
        }

        static void ResetForNewHole()
        {
            var gs = FindType("Golfin.Gameplay.Session.GameSession");
            gs?.GetMethod("ResetForNewHole", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        }

        /// <summary>
        /// The hole under test, from the LOADED SCENE — not from GameSession.CurrentHoleNumber,
        /// which the lab leaves unset (it reported -1) because nothing navigated here through the
        /// game's hole selection. The scene name is what PhysicsLabController itself scanned for,
        /// so it is the same source of truth the providers were wired from.
        /// </summary>
        static string CurrentHoleLabel()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name.StartsWith("Hole_", StringComparison.Ordinal)) return s.name;
            }
            return "<no hole scene loaded>";
        }

        static Type FindType(string fullName)
            => AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => { try { return a.GetType(fullName); } catch { return null; } })
                        .FirstOrDefault(t => t != null);
    }
}
#endif
