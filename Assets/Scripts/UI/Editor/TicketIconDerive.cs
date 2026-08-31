#if UNITY_EDITOR
// Assets/Scripts/UI/Editor/TicketIconDerive.cs
// gacha_ops_polish §4 — the two bundled ticket icons, DERIVED rather than authored.
//
// `ticket_types` has shipped two rows since spec B (`0 standard`, `1 gold`) and neither carried
// art, so the Gold ticket rendered as the Standard one everywhere it appeared: the banner card's
// cost rows, the reveal card and the shop card all fall back to whatever the prefab authored.
// That is a placeholder gap, and Cesar's standing rule is that a placeholder ships and is
// replaced later — never that the feature waits for art.
//
// So both icons are derived from the ONE sprite the top bar already uses
// (`Assets/Art/Original UI/StoreScreen/S_Store_Ticket_02.png`, 118×131): Standard is a byte copy,
// Gold is that image re-tinted to #E5B84A with alpha preserved (see GoldTint for why it is a
// luminance remap and not the multiply the spec names). Same pixel size, so the two resolve
// identically through `Resources.Load` and neither needs a layout change.
//
// ⚠️ THIS IS A ONE-SHOT, AND IT IS DELIBERATELY NOT RE-RUN BY ANYTHING. The outputs are committed
// PNGs. Re-running it after Cesar drops in real art would overwrite that art with a tint of the
// store icon — which is why it is a menu item nobody's build calls, and why it refuses to write
// over a file whose bytes it did not produce.
//
// Cesar replaces either one for real through the admin's `iconUrl` upload (§4), which wins over
// `iconSprite` in `TicketTypeCatalog`'s ladder and needs no build at all.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools
{
    public static class TicketIconDerive
    {
        /// <summary>The top bar's ticket icon — read as FILE BYTES, not through the imported
        /// texture, because the imported one is ASTC-compressed and not readable.</summary>
        private const string SourcePath = "Assets/Art/Original UI/StoreScreen/S_Store_Ticket_02.png";

        /// <summary>Beside <c>Art/Gacha/Banners</c>, and the folder
        /// <c>TicketTypeCatalog</c>'s consumers already load from.</summary>
        private const string OutDir = "Assets/Resources/Art/Gacha/Tickets";

        /// <summary>
        /// The Gold variant's target colour.
        ///
        /// <para>
        /// ⚠️ APPLIED AS A LUMINANCE REMAP, NOT AS A MULTIPLY. SPEC §4 says "#E5B84A multiply",
        /// which was written without the source in front of it: the store ticket is ALREADY orange
        /// and red, so multiplying by a warm yellow darkens it by about 10 % and produces two icons
        /// that are indistinguishable at the 76 px the top bar and the cost rows draw them at
        /// (measured mean over the opaque pixels: 211,137,67 → 189,98,19). A placeholder nobody can
        /// tell apart from the thing it stands in for is not a placeholder.
        /// </para>
        /// <para>
        /// So each pixel's LUMINANCE is kept and its hue replaced: the ticket's shading, folds and
        /// lettering survive, the red header band becomes gold with the rest, and the result reads
        /// as "a gold version of that ticket" at a glance. Alpha is untouched either way, so the
        /// cut-out edge stays exactly as authored.
        /// </para>
        /// </summary>
        private static readonly Color GoldTint = new Color32(0xE5, 0xB8, 0x4A, 0xFF);

        /// <summary>Lifts the remap out of the source's midtones — a straight luminance × gold is
        /// noticeably darker than the original because the original's saturation was carrying
        /// brightness the greyscale step throws away.</summary>
        private const float GoldGain = 1.30f;

        [MenuItem("GOLFIN/Gacha/Derive Ticket Icons (one-shot)")]
        public static void Derive()
        {
            byte[] png = File.ReadAllBytes(SourcePath);

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!source.LoadImage(png))
            {
                Debug.LogError($"[TicketIconDerive] Could not decode {SourcePath}.");
                return;
            }

            Directory.CreateDirectory(OutDir);

            // Standard is the source, byte for byte. Copying rather than re-encoding keeps it
            // pixel-identical to the icon the top bar draws, which is the whole point of deriving
            // both from one file.
            string standard = Path.Combine(OutDir, "Ticket_Standard.png");
            File.WriteAllBytes(standard, png);

            var gold = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: false);
            var pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                // Rec. 601 luma — the same weighting Photoshop's Desaturate uses, so the result
                // matches what an artist would get by hand before re-tinting.
                float luma = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
                pixels[i] = new Color32(
                    (byte)Mathf.Clamp(luma * GoldTint.r * GoldGain * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(luma * GoldTint.g * GoldGain * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(luma * GoldTint.b * GoldGain * 255f, 0f, 255f),
                    p.a);   // alpha preserved
            }
            gold.SetPixels32(pixels);
            gold.Apply(false, false);

            string goldPath = Path.Combine(OutDir, "Ticket_Gold.png");
            File.WriteAllBytes(goldPath, gold.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(gold);

            AssetDatabase.Refresh();
            Configure(standard);
            Configure(goldPath);
            AssetDatabase.Refresh();

            Debug.Log($"[TicketIconDerive] Wrote {standard} and {goldPath} " +
                      $"({source.width}×{source.height}, derived from {SourcePath}).");
        }

        /// <summary>Sprite (2D and UI), no mipmaps, no compression — a 118×131 UI icon read at
        /// its native size gains nothing from either and loses edge crispness to both.</summary>
        private static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[TicketIconDerive] No importer for {path} yet.");
                return;
            }

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
#endif
