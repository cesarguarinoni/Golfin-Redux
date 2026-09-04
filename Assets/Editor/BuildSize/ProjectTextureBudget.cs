#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools.BuildSize
{
    /// <summary>
    /// build_size_diet Phase 2.7 + Phase 3 — the iPhone texture budget for the project's OWN art.
    ///
    /// Sibling of <see cref="PackTextureBudget"/>, and separate from it on purpose. That one
    /// governs <c>Assets/Packs/</c>, which is gitignored, so its rule has to be enforced on every
    /// import or it evaporates. This one governs tracked art, where the resulting <c>.meta</c> IS
    /// the record and shows up in the diff — so it applies once, from a menu item, and does NOT
    /// install an import-time enforcer that would silently overrule a future hand-tuned setting.
    ///
    /// TWO THINGS IT DOES
    ///   Phase 2.7 — <c>Assets/Resources/Clubs</c> (292 PNGs, 55.4 MiB in the 1.5.7 Build Report)
    ///     capped at 512 on iPhone. Sources are 1156 px (Controls) and 900 px (Full); Portraits
    ///     are already 261–411 px, so the cap is a no-op there and the report says so rather than
    ///     claiming a saving that is not real. Everything under Resources/ ships whether the
    ///     player opens the shop or not, which is what makes club art worth capping at all.
    ///
    ///   Phase 3 — every texture that still imports UNCOMPRESSED. Found by asking the importer
    ///     (<c>TextureImporter.textureCompression == Uncompressed</c> with no compressed iPhone
    ///     override), not by grepping <c>textureCompression: 0</c> out of the .meta: the grep
    ///     cannot tell a default-platform value that an override already supersedes from one that
    ///     really reaches the device, and it found 96 files where only 45 reach the player.
    ///
    /// EXCLUSIONS ARE NAMED, NOT SILENT. <see cref="Exclusions"/> carries a reason per rule and
    /// the report prints every skipped asset against it, because "we compressed the textures" with
    /// an unexplained gap is exactly the report nobody can check later.
    /// </summary>
    public static class ProjectTextureBudget
    {
        const string Tag = "[ProjectTextureBudget]";
        const string IPhone = "iPhone";
        const string ReportPath = "Docs/Specs/Active/build_size_diet/reference/project_texture_budget.txt";

        /// <summary>Phase 2.7. Sources are 1156 / 900 / 411 px — see the class docs.</summary>
        const string ClubsRoot = "Assets/Resources/Clubs";
        const int ClubsMax = 512;

        /// <summary>Phase 3 default cap for an uncompressed texture that has no special case.</summary>
        const int UncompressedMax = 2048;

        /// <summary>
        /// Per-asset overrides of the Phase 3 cap. One entry today: a 2680x600 source for a PILL,
        /// which the SPEC calls out by name.
        /// </summary>
        static readonly Dictionary<string, int> SpecialCaps = new Dictionary<string, int>
        {
            { "Assets/Art/UI/Account/S_SocialPillBordered.png", 1024 },
        };

        /// <summary>
        /// Path predicate -> why this texture is deliberately left uncompressed. Printed in the
        /// report next to every asset it skips.
        /// </summary>
        static readonly (Func<string, bool> Match, string Reason)[] Exclusions =
        {
            (p => p.EndsWith("/MatteMaskMap.png", StringComparison.Ordinal),
             "terrain matte MASK read per-texel by the hole shader — 0.2 KB each (3.6 KB for all 18 " +
             "shipping holes), and block compression on a mask can shift a matte boundary. Zero bytes to win."),

            (p => p.Contains("/TextMesh Pro/", StringComparison.Ordinal) || p.EndsWith(" SDF Atlas.png", StringComparison.Ordinal),
             "TMP font atlas — SDF distance fields go to mush under block compression."),

            (p => p.Contains("/Heightmaps/", StringComparison.Ordinal) || p.EndsWith("heightmap.png", StringComparison.OrdinalIgnoreCase),
             "heightmap PNG — sample data, not an image."),

            (p => p.Contains("LUT", StringComparison.Ordinal) || p.Contains("/RenderTextures/", StringComparison.Ordinal),
             "colour LUT / render target — exact texel values are the payload."),

            (p => p.StartsWith("Packages/", StringComparison.Ordinal),
             "package asset — not ours to re-import."),

            (p => p.StartsWith("Assets/Packs/", StringComparison.Ordinal),
             "vendor pack — PackTextureBudget owns that root, and reimporting a 4096 source that " +
             "reaches no scene would cost minutes to change nothing shipped."),
        };

        [MenuItem("Tools/Golfin/Build Size/Apply Project Texture Budget (dry run)", false, 110)]
        public static void DryRunMenu() => Apply(dryRun: true);

        [MenuItem("Tools/Golfin/Build Size/Apply Project Texture Budget", false, 111)]
        public static void ApplyMenu() => Apply(dryRun: false);

        /// <summary>-executeMethod Golfin.EditorTools.BuildSize.ProjectTextureBudget.ApplyBatch</summary>
        public static void ApplyBatch() => Apply(dryRun: false);

        /// <summary>-executeMethod Golfin.EditorTools.BuildSize.ProjectTextureBudget.DryRunBatch</summary>
        public static void DryRunBatch() => Apply(dryRun: true);

        public static string Apply(bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# reference/project_texture_budget.txt — build_size_diet Phase 2.7 + Phase 3");
            sb.AppendLine($"# Tools > Golfin > Build Size > Apply Project Texture Budget   ({(dryRun ? "DRY RUN" : "APPLIED")})");
            sb.AppendLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("# The .meta files these settings land in are TRACKED, so the diff is the real record;");
            sb.AppendLine("# this file exists so the reasoning and the exclusions are checkable without reading 200 metas.");
            sb.AppendLine();

            int changed = 0;
            double before = 0, after = 0;

            sb.AppendLine("== Phase 2.7 — Assets/Resources/Clubs, iPhone max 512, ASTC 6x6 ==");
            sb.AppendLine($"{"src px",7} {"was",6} {"now",6} {"est MiB before -> after",26}  path");
            foreach (var path in TexturesUnder(ClubsRoot))
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                Row(sb, ti, path, ClubsMax, dryRun, ref changed, ref before, ref after);
            }

            sb.AppendLine();
            sb.AppendLine("== Phase 3 — textures that still import UNCOMPRESSED on iPhone ==");
            sb.AppendLine($"{"src px",7} {"was",6} {"now",6} {"est MiB before -> after",26}  path");

            var skipped = new List<string>();
            foreach (var path in TexturesUnder("Assets"))
            {
                if (path.StartsWith(ClubsRoot, StringComparison.Ordinal)) continue;  // covered above
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                bool named = SpecialCaps.ContainsKey(path);
                // A named cap applies whether or not the texture is already compressed: the point
                // of the S_SocialPillBordered row is its 2680x600 SOURCE, not its format. Only the
                // sweep half is conditional on still being uncompressed.
                if (!named && !IsUncompressedOnIPhone(ti)) continue;

                var reason = Exclusions.FirstOrDefault(e => e.Match(path)).Reason;
                if (reason != null) { skipped.Add($"    {path}\n        SKIPPED: {reason}"); continue; }

                int cap = named ? SpecialCaps[path] : UncompressedMax;
                Row(sb, ti, path, cap, dryRun, ref changed, ref before, ref after);
            }

            sb.AppendLine();
            sb.AppendLine("== Deliberately left uncompressed, with the reason ==");
            if (skipped.Count == 0) sb.AppendLine("    (none)");
            foreach (var s in skipped) sb.AppendLine(s);

            sb.AppendLine();
            sb.AppendLine($"{(dryRun ? 0 : changed)} texture(s) reimported.");
            sb.AppendLine($"Estimated built total for the rows above: {before:F1} MiB -> {after:F1} MiB ({before - after:F1} MiB saved)");
            sb.AppendLine("(Estimate is ASTC 6x6 + mips vs RGBA32 + mips where the texture was uncompressed;");
            sb.AppendLine(" the authoritative number is the Build Report / data_*.txt pair, not this line.)");

            var text = sb.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, text);
            Debug.Log($"{Tag} {(dryRun ? "DRY RUN" : "APPLIED")} — report → {ReportPath}\n" +
                      $"{Tag} {(dryRun ? 0 : changed)} reimported, est {before:F1} -> {after:F1} MiB");
            if (!dryRun) AssetDatabase.Refresh();
            return text;
        }

        static void Row(StringBuilder sb, TextureImporter ti, string path, int cap, bool dryRun,
                        ref int changed, ref double before, ref double after)
        {
            ti.GetSourceTextureWidthAndHeight(out int w, out int h);
            int src = Mathf.Max(w, h);
            var cur = ti.GetPlatformTextureSettings(IPhone);
            int wasCap = cur.overridden ? cur.maxTextureSize : ti.maxTextureSize;
            bool wasUncompressed = IsUncompressedOnIPhone(ti);

            // maxTextureSize caps the LONG edge and scales both, so the texel count follows the
            // real aspect ratio. Estimating from max-edge squared inflates every non-square sprite
            // — which is most UI art — and this table would then over-claim the saving.
            int effBefore = Mathf.Min(wasCap, src);
            int effAfter = Mathf.Min(cap, src);
            long texBefore = Texels(w, h, effBefore);
            long texAfter = Texels(w, h, effAfter);
            double mb = (wasUncompressed ? Rgba32MiB(texBefore) : Astc6x6MiB(texBefore));
            double ma = Astc6x6MiB(texAfter);
            before += mb; after += ma;

            string note = effAfter == effBefore && !wasUncompressed ? "  (no-op)" : "";
            sb.AppendLine($"{src,7} {wasCap,6} {cap,6} {mb,12:F3} -> {ma,10:F3}  {path}{note}");

            if (dryRun) return;
            if (PackTextureBudget.ApplyTo(ti, cap)) { changed++; ti.SaveAndReimport(); }
        }

        /// <summary>
        /// True when this texture reaches an iPhone build UNCOMPRESSED: either the iPhone override
        /// itself says Uncompressed, or there is no override and the default platform does.
        /// Asking the importer, not the .meta text — see the class docs.
        /// </summary>
        static bool IsUncompressedOnIPhone(TextureImporter ti)
        {
            var s = ti.GetPlatformTextureSettings(IPhone);
            if (s.overridden) return s.textureCompression == TextureImporterCompression.Uncompressed;
            return ti.textureCompression == TextureImporterCompression.Uncompressed;
        }

        static IEnumerable<string> TexturesUnder(string root)
            => AssetDatabase.FindAssets("t:Texture2D", new[] { root })
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .Distinct()
                            .OrderBy(p => p, StringComparer.Ordinal);

        /// <summary>Texel count for a w x h source whose LONG edge is capped at <paramref name="cap"/>.</summary>
        static long Texels(int w, int h, int cap)
        {
            int longEdge = Mathf.Max(w, h);
            if (longEdge <= 0) return 0;
            double s = Mathf.Min(cap, longEdge) / (double)longEdge;
            return (long)System.Math.Round(w * s) * (long)System.Math.Round(h * s);
        }

        /// <summary>ASTC 6x6 with a full mip chain, MiB. 128 bits per 6x6 block, mips add ~1/3.</summary>
        static double Astc6x6MiB(long texels)
            => texels <= 0 ? 0 : texels * 16.0 / 36.0 * 4.0 / 3.0 / (1024.0 * 1024.0);

        /// <summary>RGBA32 with a full mip chain, MiB — what an uncompressed texture costs today.</summary>
        static double Rgba32MiB(long texels)
            => texels <= 0 ? 0 : texels * 4.0 * 4.0 / 3.0 / (1024.0 * 1024.0);
    }
}
#endif
