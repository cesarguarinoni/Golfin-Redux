#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot Editor script to set all images in Resources/HoleImages/ to Texture Type = Sprite.
/// Run: GOLFIN/Setup/Configure Hole Images as Sprites
/// </summary>
public static class HoleImagesImporter
{
    [MenuItem("GOLFIN/Setup/Configure Hole Images as Sprites")]
    public static void ConfigureHoleImages()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/HoleImages" });
        int configured = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[HoleImagesImporter] Configured as Sprite: {path}");
                configured++;
            }
        }

        Debug.Log($"[HoleImagesImporter] Done — {configured} images configured as Sprites in Resources/HoleImages/.");
    }
}
#endif
