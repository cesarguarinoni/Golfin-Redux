#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Golfin.CourseImport
{
    public static class ReimportCurrentHole
    {
        [MenuItem("Import/Re-import Current Hole", priority = 0)]
        public static void ReimportCurrent()
        {
            var metadata = Object.FindObjectOfType<HoleMetadata>();
            if (metadata == null)
            {
                EditorUtility.DisplayDialog("Re-import Error",
                    "No HoleMetadata found in the current scene.\n\nOpen a hole scene first.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(metadata.importType))
            {
                EditorUtility.DisplayDialog("Re-import Error",
                    "This hole was imported before re-import tracking was added.\n\n" +
                    "Please re-import it once manually via Import/Lite or Import/Geo.", "OK");
                return;
            }

            string label = $"Hole {metadata.holeNumber:D2} [{metadata.importType}]  —  {metadata.courseId}";
            bool confirm = EditorUtility.DisplayDialog(
                "Re-import Current Hole",
                $"Re-import {label}?\n\nThis will replace the current scene.",
                "Re-import", "Cancel");

            if (!confirm) return;

            switch (metadata.importType)
            {
                case "Lite":
                    HoleLiteImporter.ImportLiteHole(metadata.courseId, metadata.holeNumber);
                    break;
                case "LiteFlat":
                    HoleLiteImporter.ImportLiteHoleFlat(metadata.courseId, metadata.holeNumber);
                    break;
                case "Geo":
                    HoleGeoImporter.ImportGeoHole(metadata.courseId, metadata.holeNumber);
                    break;
                case "GeoFlat":
                    HoleGeoImporter.ImportGeoHoleFlat(metadata.courseId, metadata.holeNumber);
                    break;
                default:
                    EditorUtility.DisplayDialog("Re-import Error",
                        $"Unknown import type: '{metadata.importType}'\n\nPlease re-import manually.", "OK");
                    break;
            }
        }
    }
}
#endif
