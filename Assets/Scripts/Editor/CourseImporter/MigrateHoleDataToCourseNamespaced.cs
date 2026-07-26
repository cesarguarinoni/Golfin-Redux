// Multi-course architecture refactor — SPEC §1.5
// One-shot migration that moves:
//   Assets/Resources/HoleData/Hole_NN/  →  Assets/Resources/HoleData/lomond-country-club/Hole_NN/
//   Assets/Resources/HoleData/TestGreen/ →  Assets/Resources/HoleData/_test/TestGreen/
//   Assets/Resources/HoleImages/Hole_NN.png → Assets/Resources/HoleImages/lomond-country-club/Hole_NN.png
//
// Uses AssetDatabase.MoveAsset so GUIDs are preserved (bit-exact gate).
//
// Self-disabling: the menu item validates against the expected post-migration
// path.  Once migration is done it disappears from the menu.
//
// DO NOT call after migration — running it twice is a no-op (source paths
// won't exist) but validates via the `validate` function.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Golfin.Editor.CourseImporter
{
    public static class MigrateHoleDataToCourseNamespaced
    {
        private const string LomondSlug  = "lomond-country-club";
        private const string TestSubfolder = "_test";

        // ── HoleData paths ──────────────────────────────────────────────────
        private const string HoleDataRoot  = "Assets/Resources/HoleData";
        private const string HoleImagesRoot = "Assets/Resources/HoleImages";

        // ── Menu item ───────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Course Migration/Migrate HoleData to Course-Namespaced (lomond)", validate = false)]
        public static void RunMigration()
        {
            // Re-check via validate logic first to give a clear message
            if (IsMigrationAlreadyDone())
            {
                Debug.Log("[MigrateHoleDataToCourseNamespaced] Migration already done — nothing to do.");
                EditorUtility.DisplayDialog("Migration", "Already migrated — nothing to do.", "OK");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "Migrate HoleData to Course-Namespaced",
                "This will move all Hole_NN folders under Assets/Resources/HoleData/ " +
                "into a 'lomond-country-club/' subfolder, and TestGreen into '_test/'. " +
                "This operation cannot be undone automatically.\n\n" +
                "Proceed?",
                "Migrate", "Cancel");
            if (!ok) return;

            // Phase 1: ensure all destination parent folders exist in the AssetDatabase.
            // NOTE: Do NOT wrap in StartAssetEditing/StopAssetEditing — CreateFolder inside
            // a batch block defers import, causing MoveAsset to fail with
            // "Parent directory is not in asset database".
            EnsureFolder(HoleDataRoot, LomondSlug);
            EnsureFolder(HoleDataRoot, TestSubfolder);
            EnsureFolder(HoleImagesRoot, LomondSlug);
            // Refresh to register the new folders in the AssetDatabase before moving.
            AssetDatabase.Refresh();

            // Phase 2: move assets (no batch wrapper needed; each call is fast).
            MigrateHoleDataFolders();
            MigrateTestGreen();
            MigrateHoleImages();
            AssetDatabase.Refresh();

            Debug.Log("[MigrateHoleDataToCourseNamespaced] Migration complete. Run 'GOLFIN > Course Migration > Verify Bit-Exact Hashes' to confirm.");
        }

        [MenuItem("GOLFIN/Course Migration/Migrate HoleData to Course-Namespaced (lomond)", validate = true)]
        private static bool ValidateRunMigration()
        {
            // Show menu item only if migration has NOT been done yet
            return !IsMigrationAlreadyDone();
        }

        // ── Core helpers ────────────────────────────────────────────────────

        private static bool IsMigrationAlreadyDone()
        {
            // The migration is done when Hole_01 lives under lomond-country-club/
            string postMigrationPath = Path.Combine(HoleDataRoot, LomondSlug, "Hole_01", "heightmap.bytes")
                .Replace("\\", "/");
            return File.Exists(postMigrationPath);
        }

        private static void MigrateHoleDataFolders()
        {
            // Ensure lomond-country-club/ subfolder exists
            string lomondDir = Path.Combine(HoleDataRoot, LomondSlug).Replace("\\", "/");
            EnsureFolder(HoleDataRoot, LomondSlug);

            // Move Hole_01 through Hole_18
            for (int i = 1; i <= 18; i++)
            {
                string holeName = $"Hole_{i:D2}";
                string srcPath  = Path.Combine(HoleDataRoot, holeName).Replace("\\", "/");
                string dstPath  = Path.Combine(HoleDataRoot, LomondSlug, holeName).Replace("\\", "/");

                if (!AssetDatabase.IsValidFolder(srcPath))
                {
                    Debug.LogWarning($"[MigrateHoleDataToCourseNamespaced] Source not found, skipping: {srcPath}");
                    continue;
                }

                string error = AssetDatabase.MoveAsset(srcPath, dstPath);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"[MigrateHoleDataToCourseNamespaced] MoveAsset failed: {srcPath} → {dstPath}: {error}");
                else
                    Debug.Log($"[MigrateHoleDataToCourseNamespaced] Moved: {srcPath} → {dstPath}");
            }
        }

        private static void MigrateTestGreen()
        {
            // Ensure _test/ subfolder exists
            EnsureFolder(HoleDataRoot, TestSubfolder);

            string srcPath = Path.Combine(HoleDataRoot, "TestGreen").Replace("\\", "/");
            string dstPath = Path.Combine(HoleDataRoot, TestSubfolder, "TestGreen").Replace("\\", "/");

            if (!AssetDatabase.IsValidFolder(srcPath))
            {
                Debug.Log($"[MigrateHoleDataToCourseNamespaced] TestGreen not found (already moved?): {srcPath}");
                return;
            }

            string error = AssetDatabase.MoveAsset(srcPath, dstPath);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"[MigrateHoleDataToCourseNamespaced] MoveAsset failed: {srcPath} → {dstPath}: {error}");
            else
                Debug.Log($"[MigrateHoleDataToCourseNamespaced] Moved: {srcPath} → {dstPath}");
        }

        private static void MigrateHoleImages()
        {
            // Ensure HoleImages/lomond-country-club/ subfolder exists
            EnsureFolder(HoleImagesRoot, LomondSlug);

            // Move Hole_01.png through Hole_18.png
            for (int i = 1; i <= 18; i++)
            {
                string pngName = $"Hole_{i:D2}.png";
                string srcPath = Path.Combine(HoleImagesRoot, pngName).Replace("\\", "/");
                string dstPath = Path.Combine(HoleImagesRoot, LomondSlug, pngName).Replace("\\", "/");

                if (!File.Exists(srcPath))
                {
                    Debug.LogWarning($"[MigrateHoleDataToCourseNamespaced] Image not found, skipping: {srcPath}");
                    continue;
                }

                string error = AssetDatabase.MoveAsset(srcPath, dstPath);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"[MigrateHoleDataToCourseNamespaced] MoveAsset failed: {srcPath} → {dstPath}: {error}");
                else
                    Debug.Log($"[MigrateHoleDataToCourseNamespaced] Moved image: {srcPath} → {dstPath}");
            }
        }

        private static void EnsureFolder(string parentFolder, string newFolderName)
        {
            string fullPath = Path.Combine(parentFolder, newFolderName).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                string guid = AssetDatabase.CreateFolder(parentFolder, newFolderName);
                if (string.IsNullOrEmpty(guid))
                    Debug.LogError($"[MigrateHoleDataToCourseNamespaced] Failed to create folder: {fullPath}");
                else
                    Debug.Log($"[MigrateHoleDataToCourseNamespaced] Created folder: {fullPath}");
            }
        }
    }
}
