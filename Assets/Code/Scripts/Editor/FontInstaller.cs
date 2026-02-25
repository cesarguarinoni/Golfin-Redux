using UnityEngine;
using UnityEditor;
using System.IO;
using System.Net;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Auto-downloads and installs fonts from Google Fonts to match Figma design.
/// Creates TMP Font Assets ready for use.
///
/// Usage: Unity menu → Tools → Install Figma Fonts
///
/// Fonts defined by Figma extraction:
///   - Rubik (400, 500, 600, 700, 800) — Primary font
///   - Arapey (400) — Display/branding only
/// </summary>
public class FontInstaller
{
    // ═══ FONTS FROM FIGMA ═══
    // Extracted via Figma API — these are the exact fonts used in the design
    static readonly FontDef[] RequiredFonts = new FontDef[]
    {
        // Primary font — used on 90%+ of all text
        new FontDef("Rubik", "Regular",    400, "Rubik-Regular"),
        new FontDef("Rubik", "Medium",     500, "Rubik-Medium"),
        new FontDef("Rubik", "SemiBold",   600, "Rubik-SemiBold"),
        new FontDef("Rubik", "Bold",       700, "Rubik-Bold"),
        new FontDef("Rubik", "ExtraBold",  800, "Rubik-ExtraBold"),

        // Secondary — branding/display only
        new FontDef("Arapey", "Regular",   400, "Arapey-Regular"),
    };

    struct FontDef
    {
        public string Family;
        public string Style;
        public int Weight;
        public string FileName; // without extension
        public FontDef(string family, string style, int weight, string fileName)
        {
            Family = family; Style = style; Weight = weight; FileName = fileName;
        }
    }

    const string FontDir = "Assets/Fonts";
    const string GoogleFontsBase = "https://raw.githubusercontent.com/google/fonts/main/ofl";

    [MenuItem("Tools/Install Figma Fonts")]
    public static void InstallFonts()
    {
        Directory.CreateDirectory(FontDir);

        int downloaded = 0;
        int skipped = 0;
        int converted = 0;
        var failedFonts = new List<string>();

        foreach (var font in RequiredFonts)
        {
            string ttfPath = $"{FontDir}/{font.FileName}.ttf";
            string tmpPath = $"{FontDir}/{font.FileName} SDF.asset";

            // Skip if TTF already exists
            if (File.Exists(ttfPath))
            {
                Debug.Log($"[Fonts] ✓ {font.FileName}.ttf already exists");
                skipped++;
            }
            else
            {
                // Download from Google Fonts GitHub
                string familyLower = font.Family.ToLower();
                // Google Fonts repo structure: ofl/{family}/{Family}-{Style}.ttf
                // Some fonts use different structures, try common patterns
                string[] urls = new string[]
                {
                    $"{GoogleFontsBase}/{familyLower}/{font.FileName}.ttf",
                    $"{GoogleFontsBase}/{familyLower}/static/{font.FileName}.ttf",
                    $"https://github.com/google/fonts/raw/main/ofl/{familyLower}/{font.FileName}.ttf",
                    $"https://github.com/google/fonts/raw/main/ofl/{familyLower}/static/{font.FileName}.ttf",
                };

                bool success = false;
                foreach (string url in urls)
                {
                    try
                    {
                        Debug.Log($"[Fonts] Downloading {font.FileName} from {url}...");
                        using (var client = new WebClient())
                        {
                            client.DownloadFile(url, ttfPath);
                        }

                        // Verify it's actually a TTF (starts with 0x00010000 or 'true')
                        byte[] header = File.ReadAllBytes(ttfPath);
                        if (header.Length > 4 && (header[0] == 0x00 || header[0] == 0x74))
                        {
                            Debug.Log($"[Fonts] ✅ Downloaded {font.FileName}.ttf");
                            downloaded++;
                            success = true;
                            break;
                        }
                        else
                        {
                            // Not a valid font file (probably HTML error page)
                            File.Delete(ttfPath);
                        }
                    }
                    catch (System.Exception)
                    {
                        if (File.Exists(ttfPath)) File.Delete(ttfPath);
                    }
                }

                if (!success)
                {
                    Debug.LogWarning($"[Fonts] ⚠️ Could not download {font.FileName}. " +
                        $"Please download manually from https://fonts.google.com/specimen/{font.Family}");
                    failedFonts.Add(font.FileName);
                    continue;
                }
            }

            // Import the TTF
            AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceUpdate);
        }

        AssetDatabase.Refresh();

        // Now create TMP Font Assets for each downloaded TTF
        foreach (var font in RequiredFonts)
        {
            string ttfPath = $"{FontDir}/{font.FileName}.ttf";
            string tmpPath = $"{FontDir}/{font.FileName} SDF.asset";

            if (!File.Exists(ttfPath)) continue;

            if (File.Exists(tmpPath))
            {
                Debug.Log($"[Fonts] ✓ {font.FileName} SDF already exists");
                continue;
            }

            // Load the font
            Font ttfFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (ttfFont == null)
            {
                Debug.LogWarning($"[Fonts] Could not load {ttfPath} as Font asset");
                continue;
            }

            // Create TMP Font Asset
            // Using TMP's built-in font asset creator
            TMP_FontAsset tmpFont = TMP_FontAsset.CreateFontAsset(ttfFont);
            if (tmpFont != null)
            {
                AssetDatabase.CreateAsset(tmpFont, tmpPath);
                Debug.Log($"[Fonts] ✅ Created {font.FileName} SDF.asset");
                converted++;
            }
            else
            {
                Debug.LogWarning($"[Fonts] Failed to create TMP asset for {font.FileName}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Summary
        Debug.Log($"[Fonts] ══════════════════════════════════════");
        Debug.Log($"[Fonts] Font Installation Complete!");
        Debug.Log($"[Fonts] Downloaded: {downloaded} | Skipped: {skipped} | TMP Created: {converted}");
        if (failedFonts.Count > 0)
        {
            Debug.LogWarning($"[Fonts] Failed ({failedFonts.Count}): {string.Join(", ", failedFonts)}");
            Debug.LogWarning("[Fonts] Download manually from Google Fonts and place in Assets/Fonts/");
        }
        Debug.Log($"[Fonts] ══════════════════════════════════════");

        // Open the fonts folder
        EditorUtility.RevealInFinder(FontDir);
    }

    [MenuItem("Tools/Check Figma Fonts")]
    public static void CheckFonts()
    {
        Debug.Log("[Fonts] Checking required fonts...");
        int missing = 0;

        foreach (var font in RequiredFonts)
        {
            string ttfPath = $"{FontDir}/{font.FileName}.ttf";
            string tmpPath = $"{FontDir}/{font.FileName} SDF.asset";

            bool hasTTF = File.Exists(ttfPath);
            bool hasTMP = File.Exists(tmpPath);

            // Also check Resources
            var resTMP = Resources.Load<TMP_FontAsset>($"{font.FileName} SDF");

            string status = hasTMP || resTMP != null ? "✅" : hasTTF ? "⚠️ TTF only (need SDF)" : "❌ Missing";
            Debug.Log($"[Fonts] {status} {font.Family} {font.Style} (w{font.Weight}) — {font.FileName}");

            if (!hasTMP && resTMP == null) missing++;
        }

        if (missing == 0)
            Debug.Log("[Fonts] All fonts installed! ✅");
        else
            Debug.LogWarning($"[Fonts] {missing} fonts missing. Run Tools → Install Figma Fonts");
    }
}
