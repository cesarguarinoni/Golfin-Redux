using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics;
using Golfin.Physics.Runtime;

/// <summary>
/// One-time migration: reads Golfin.Course.SurfaceMarker.surfaceType on every zone
/// mesh GO in each Hole_XX_Geo scene and writes the matching value to the co-located
/// Golfin.Physics.Runtime.SurfaceMarker.Type field.
///
/// The Physics.Runtime markers were added by HoleGeoImporter but never given non-default
/// Type values (they all defaulted to Fairway=0). This script fixes that so
/// SceneSurfaceProvider and SceneGroundProvider can distinguish green / bunker / rough
/// from fairway at runtime.
///
/// Mapping: Course.SurfaceType  →  Physics.SurfaceType
///   Fairway  (0)  → Fairway   (0)
///   Green    (1)  → Green     (1)
///   SemiRough(2)  → Semirough (3)
///   Rough    (3)  → Rough     (4)
///   Bunker   (4)  → Sand      (6)
///   Water    (5)  → Water     (9)
///   Tee      (6)  → Tee       (5)
///   CartPath (7)  → CartPath  (8)
///   Fringe   (8)  → GreenCollar(2)
/// </summary>
public static class SyncPhysicsSurfaceMarkers
{
    [MenuItem("GOLFIN/Tools/Sync Physics Surface Markers (All Holes)")]
    public static void SyncAllHoles()
    {
        int totalFixed = 0;
        int totalScenes = 0;

        for (int hole = 1; hole <= 18; hole++)
        {
            string sceneName = $"Hole_{hole:D2}_Geo";
            string[] guids = AssetDatabase.FindAssets($"t:Scene {sceneName}");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[SyncMarkers] Scene not found: {sceneName}");
                continue;
            }

            string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            // Prefer the non-Video path if multiple results.
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.Contains("Video")) { scenePath = p; break; }
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            int fixedInScene = SyncMarkersInScene(scene);
            totalFixed += fixedInScene;
            totalScenes++;

            if (fixedInScene > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[SyncMarkers] {sceneName}: fixed {fixedInScene} markers, saved.");
            }
            else
            {
                Debug.Log($"[SyncMarkers] {sceneName}: nothing to fix.");
            }

            EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log($"[SyncMarkers] Done. Processed {totalScenes} scenes, fixed {totalFixed} markers total.");
        EditorUtility.DisplayDialog("Sync Physics Surface Markers",
            $"Processed {totalScenes} scenes.\nFixed {totalFixed} Physics.Runtime.SurfaceMarker.Type values.",
            "OK");
    }

    static int SyncMarkersInScene(Scene scene)
    {
        // Resolve Course.SurfaceMarker type via reflection (asmdef wall).
        System.Type courseSmType = System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp");
        System.Reflection.FieldInfo stField = courseSmType?.GetField("surfaceType");
        if (courseSmType == null || stField == null)
        {
            Debug.LogWarning("[SyncMarkers] Golfin.Course.SurfaceMarker not found - skipping scene.");
            return 0;
        }

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var physMarker in root.GetComponentsInChildren<SurfaceMarker>(true))
            {
                var courseMaker = physMarker.GetComponent(courseSmType);
                if (courseMaker == null) continue;

                int courseVal = (int)stField.GetValue(courseMaker);
                SurfaceType physType = MapCourseToPhysics(courseVal);

                if (physMarker.Type != physType)
                {
                    physMarker.Type = physType;
                    EditorUtility.SetDirty(physMarker);
                    count++;
                }
            }
        }
        return count;
    }

    static SurfaceType MapCourseToPhysics(int courseTypeInt)
    {
        // Golfin.Course.SurfaceType ordinal → Golfin.Physics.SurfaceType
        switch (courseTypeInt)
        {
            case 0: return SurfaceType.Fairway;
            case 1: return SurfaceType.Green;
            case 2: return SurfaceType.Semirough;
            case 3: return SurfaceType.Rough;
            case 4: return SurfaceType.Sand;
            case 5: return SurfaceType.Water;
            case 6: return SurfaceType.Tee;
            case 7: return SurfaceType.CartPath;
            case 8: return SurfaceType.GreenCollar; // Fringe → GreenCollar
            default:
                Debug.LogWarning($"[SyncMarkers] Unknown Course.SurfaceType={courseTypeInt}, defaulting to Fairway.");
                return SurfaceType.Fairway;
        }
    }
}
