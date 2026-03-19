#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Duplicates CharacterThumbnailCardGlowUp.prefab as ClubThumbnailCard.prefab,
    /// replaces the CharacterThumbnailCard component with ClubThumbnailCard,
    /// and transfers all shared serialized field references (portrait, name, rarity,
    /// level, background, button, highlight).
    ///
    /// Run: GOLFIN/Setup Club Thumbnail Card Prefab
    /// Safe to re-run — overwrites the existing ClubThumbnailCard.prefab.
    /// </summary>
    public static class ClubThumbnailCardBuilder
    {
        private const string SOURCE_PREFAB = "Assets/Prefabs/UI/Roster/CharacterThumbnailCardGlowUp.prefab";
        private const string DEST_PREFAB   = "Assets/Prefabs/UI/Inventory/ClubThumbnailCard.prefab";
        private const string DEST_FOLDER   = "Assets/Prefabs/UI/Inventory";

        [MenuItem("GOLFIN/Setup Club Thumbnail Card Prefab")]
        public static void Run()
        {
            // ── 1. Ensure destination folder exists ───────────────────────────
            if (!AssetDatabase.IsValidFolder(DEST_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Inventory");
                AssetDatabase.SaveAssets();
                Debug.Log($"[ClubThumbnailCardBuilder] Created folder '{DEST_FOLDER}'.");
            }

            // ── 2. Copy source prefab ──────────────────────────────────────────
            AssetDatabase.CopyAsset(SOURCE_PREFAB, DEST_PREFAB);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ClubThumbnailCardBuilder] Copied '{SOURCE_PREFAB}' → '{DEST_PREFAB}'.");

            // ── 3. Load mutable contents ───────────────────────────────────────
            var root = PrefabUtility.LoadPrefabContents(DEST_PREFAB);
            if (root == null)
            {
                Debug.LogError("[ClubThumbnailCardBuilder] Failed to load prefab contents.");
                return;
            }

            // ── 4. Read existing field references from CharacterThumbnailCard ──
            //      (field names are identical on ClubThumbnailCard — safe to transfer)
            var charCard = root.GetComponent<Golfin.Roster.CharacterThumbnailCard>();
            if (charCard == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogError("[ClubThumbnailCardBuilder] CharacterThumbnailCard component not found on prefab.");
                return;
            }

            var soOld = new SerializedObject(charCard);
            // Cache all shared field references before destroying the component
            var portrait      = soOld.FindProperty("portraitImage")     .objectReferenceValue;
            var nameTMP       = soOld.FindProperty("nameText")           .objectReferenceValue;
            var rarityBadgeImg= soOld.FindProperty("rarityBadgeImage")   .objectReferenceValue;
            var rarityLabelTMP= soOld.FindProperty("rarityLabelText")    .objectReferenceValue;
            var levelTMP      = soOld.FindProperty("levelText")          .objectReferenceValue;
            var highlight     = soOld.FindProperty("selectionHighlight") .objectReferenceValue;
            var background    = soOld.FindProperty("backgroundImage")    .objectReferenceValue;
            var button        = soOld.FindProperty("cardButton")         .objectReferenceValue;

            // ── 5. Remove character-specific component and status icon row ─────
            Object.DestroyImmediate(charCard);

            // Remove the character status icon row — clubs use different icons
            var statusRow = root.transform.Find("StatusIconsRow");
            if (statusRow != null)
            {
                Object.DestroyImmediate(statusRow.gameObject);
                Debug.Log("[ClubThumbnailCardBuilder] Removed character StatusIconsRow.");
            }

            // ── 6. Add ClubThumbnailCard and wire fields ───────────────────────
            var clubCard = root.AddComponent<ClubThumbnailCard>();

            var soNew = new SerializedObject(clubCard);
            soNew.FindProperty("portraitImage")     .objectReferenceValue = portrait;
            soNew.FindProperty("nameText")           .objectReferenceValue = nameTMP;
            soNew.FindProperty("rarityBadgeImage")   .objectReferenceValue = rarityBadgeImg;
            soNew.FindProperty("rarityLabelText")    .objectReferenceValue = rarityLabelTMP;
            soNew.FindProperty("levelText")          .objectReferenceValue = levelTMP;
            soNew.FindProperty("selectionHighlight") .objectReferenceValue = highlight;
            soNew.FindProperty("backgroundImage")    .objectReferenceValue = background;
            soNew.FindProperty("cardButton")         .objectReferenceValue = button;
            // equippedIcon / durabilityLowIcon left null — wire later via StatusIconBuilder equivalent
            soNew.ApplyModifiedProperties();

            // ── 7. Save and unload ─────────────────────────────────────────────
            PrefabUtility.SaveAsPrefabAsset(root, DEST_PREFAB);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Club Thumbnail Card",
                "Done!\n\n" +
                "• ClubThumbnailCard.prefab created at Assets/Prefabs/UI/Inventory/\n" +
                "• CharacterThumbnailCard → ClubThumbnailCard component swapped\n" +
                "• All shared UI fields transferred (portrait, name, rarity, level, bg, button)\n" +
                "• StatusIconsRow removed (club icons wired separately)\n\n" +
                "equippedIcon and durabilityLowIcon are unset — add icon GameObjects\n" +
                "and wire them via the prefab Inspector when ready.",
                "OK");

            Debug.Log("[ClubThumbnailCardBuilder] ✓ ClubThumbnailCard.prefab ready.");
        }
    }
}
#endif
