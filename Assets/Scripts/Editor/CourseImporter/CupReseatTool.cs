#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.CourseImporter.Editor
{
    /// <summary>
    /// Measures and repairs BURIED HOLE CUPS in already-generated hole scenes, in place.
    ///
    /// Why this exists instead of "just re-import the hole"
    /// ---------------------------------------------------
    /// `HoleGeoImporter` builds each hole with `EditorSceneManager.NewScene(EmptyScene, Single)`
    /// and overwrites `Hole_NN_Geo.unity` wholesale (HoleGeoImporter.cs:286 / :481). Everything
    /// authored AFTER the import is therefore destroyed by a re-import — most importantly the
    /// TREES, which the importer deliberately does not place ("Trees are placed separately via
    /// Trees > Import Trees (Current Hole)", HoleGeoImporter.cs:468). A re-import also regenerates
    /// the baked sim data the multiplayer bots read (`zones.json`, `heightmap.bytes`,
    /// `tree_obstacles.csv`), reverting any later re-bake or hand-tuning.
    ///
    /// So a buried cup on a SHIPPED hole must be repaired surgically. That is safe here because
    /// the cup disc is cosmetic only: `HoleGeoImporter` destroys its collider at creation, so the
    /// object carries just Transform + MeshFilter + MeshRenderer. It drives no physics — cup
    /// capture is `CupSpec` / `RealCupDetector`, keyed off the pin position, not this disc.
    ///
    /// The bug being repaired (Docs/Specs/Quick/hole1_cup_buried_under_green.md)
    /// -----------------------------------------------------------------------
    /// The importer seats the cup at `pinSeatY + 1 mm`, where `pinSeatY` is an ANALYTIC datum
    /// (seat plane + relative height at the pin XZ). The green MESH the player actually putts on
    /// can diverge from that datum by more than the 1 mm margin. On Hole 1 it does, by ~23 mm, so
    /// the disc sits inside the turf and never renders. This tool re-seats against the MEASURED
    /// mesh surface instead of the analytic datum, which is the durable datum.
    ///
    /// Usage: open the hole scene(s) you want (single or additive), then run
    ///   GOLFIN > Course > Cups > Measure Cup Seating   (report only, mutates nothing)
    ///   GOLFIN > Course > Cups > Reseat Buried Cups    (fixes + saves ONLY changed scenes)
    /// Re-run the measure pass after any green re-bake — that is exactly the class of change that
    /// can bury a cup again.
    /// </summary>
    public static class CupReseatTool
    {
        /// <summary>
        /// Target clearance of the cup's TOP face above the measured green mesh surface.
        /// Single source of truth is <see cref="Golfin.CourseImport.HoleGeoImporter"/> so a
        /// repaired hole and a freshly imported one can never seat their cups differently.
        /// </summary>
        public const float TargetClearanceM = Golfin.CourseImport.HoleGeoImporter.CupSurfaceClearanceM;

        /// <summary>Only re-seat a cup whose top is below this (i.e. actually buried or flush).</summary>
        const float BuriedThresholdM = 0.0f;

        [MenuItem("GOLFIN/Course/Cups/Measure Cup Seating")]
        public static void MeasureCupSeating() => Run(applyFix: false);

        [MenuItem("GOLFIN/Course/Cups/Reseat Buried Cups")]
        public static void ReseatBuriedCups() => Run(applyFix: true);

        static void Run(bool applyFix)
        {
            var report = new StringBuilder();
            report.AppendLine(applyFix
                ? "[CupReseatTool] RESEAT pass — buried cups will be raised and their scenes saved."
                : "[CupReseatTool] MEASURE pass — read-only, nothing will be modified.");

            var holeScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name != null && s.name.StartsWith("Hole_") && s.name.EndsWith("_Geo"))
                    holeScenes.Add(s);
            }

            if (holeScenes.Count == 0)
            {
                Debug.LogWarning("[CupReseatTool] No Hole_NN_Geo scene is open. Open the hole "
                               + "scene(s) you want to measure (single or additive) and re-run.");
                return;
            }

            // Fully qualified: inside Golfin.CourseImporter.Editor, a bare `Physics` binds to the
            // `Golfin.Physics` namespace, not UnityEngine.Physics.
            UnityEngine.Physics.SyncTransforms();
            int examined = 0, buried = 0, fixedCount = 0;

            foreach (var scene in holeScenes)
            {
                bool sceneChanged = false;

                foreach (var pair in FindCupGreenPairs(scene))
                {
                    examined++;
                    Transform cup = pair.Key;
                    MeshCollider greenCollider = pair.Value;

                    // Unity's Cylinder primitive is 2 units tall (local Y -1..+1), so the world
                    // half-height is exactly localScale.y and the top face is pos.y + localScale.y.
                    float cupTopY = cup.position.y + cup.localScale.y;

                    if (!TrySampleSurfaceY(cup.position, greenCollider, out float greenY))
                    {
                        Debug.LogWarning($"[CupReseatTool] {scene.name}/{cup.name}: could not raycast "
                                       + $"'{greenCollider.gameObject.name}' at the cup XZ — skipped.");
                        continue;
                    }

                    float clearanceMm = (cupTopY - greenY) * 1000f;
                    bool isBuried = (cupTopY - greenY) <= BuriedThresholdM;
                    if (isBuried) buried++;

                    report.AppendLine($"  {scene.name}/{cup.name}: cupTop={cupTopY:F5} "
                                    + $"greenSurface={greenY:F5} clearance={clearanceMm:+0.00;-0.00} mm "
                                    + $"{(isBuried ? "<< BURIED" : "ok")}");

                    if (!applyFix || !isBuried) continue;

                    // Raise so the TOP face lands TargetClearanceM above the measured mesh.
                    // Only Y moves; XZ stays exactly on the authored pin position.
                    float newY = greenY + TargetClearanceM - cup.localScale.y;
                    Undo.RecordObject(cup, "Reseat buried hole cup");
                    cup.position = new Vector3(cup.position.x, newY, cup.position.z);
                    EditorUtility.SetDirty(cup);
                    sceneChanged = true;
                    fixedCount++;

                    report.AppendLine($"      -> raised {(newY - (cupTopY - cup.localScale.y)) * 1000f:F2} mm; "
                                    + $"new cupTop={newY + cup.localScale.y:F5} "
                                    + $"(clearance {TargetClearanceM * 1000f:F1} mm)");
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    bool saved = EditorSceneManager.SaveScene(scene);
                    report.AppendLine($"  {scene.name}: saved={saved}");
                }
            }

            report.AppendLine($"[CupReseatTool] cups examined={examined} buried={buried} "
                            + $"reseated={fixedCount}");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Pairs each cosmetic cup disc (`Hole_&lt;id&gt;`, MeshRenderer, NO collider) with its green's
        /// MeshCollider (`Green_&lt;id&gt;`), matching on the id suffix the importer assigns to both.
        /// </summary>
        static List<KeyValuePair<Transform, MeshCollider>> FindCupGreenPairs(Scene scene)
        {
            var cups = new Dictionary<string, Transform>();
            var greens = new Dictionary<string, MeshCollider>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Hole_") && t.GetComponent<MeshRenderer>() != null
                        && t.GetComponent<Collider>() == null)
                    {
                        cups[t.name.Substring("Hole_".Length)] = t;
                    }
                    else if (t.name.StartsWith("Green_"))
                    {
                        var mc = t.GetComponent<MeshCollider>();
                        if (mc != null) greens[t.name.Substring("Green_".Length)] = mc;
                    }
                }
            }

            var pairs = new List<KeyValuePair<Transform, MeshCollider>>();
            foreach (var kv in cups)
            {
                if (greens.TryGetValue(kv.Key, out var mc))
                    pairs.Add(new KeyValuePair<Transform, MeshCollider>(kv.Value, mc));
                else
                    Debug.LogWarning($"[CupReseatTool] {scene.name}: cup 'Hole_{kv.Key}' has no "
                                   + $"matching 'Green_{kv.Key}' MeshCollider — skipped.");
            }
            return pairs;
        }

        /// <summary>
        /// Ray-casts straight down at the cup XZ and returns the hit Y on <paramref name="target"/>.
        /// Starts well above the cup and ignores every other collider, so terrain, collars and the
        /// skirt cannot shadow the reading.
        /// </summary>
        static bool TrySampleSurfaceY(Vector3 cupPos, MeshCollider target, out float surfaceY)
        {
            surfaceY = 0f;
            var origin = new Vector3(cupPos.x, cupPos.y + 50f, cupPos.z);
            var hits = UnityEngine.Physics.RaycastAll(origin, Vector3.down, 200f, ~0,
                                                      QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider != target) continue;
                surfaceY = h.point.y;
                return true;
            }
            return false;
        }
    }
}
#endif
