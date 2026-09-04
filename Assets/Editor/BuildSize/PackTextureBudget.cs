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
    /// build_size_diet Phase 1 — the iPhone texture budget for third-party art packs.
    ///
    /// WHY THIS IS A TRACKED SCRIPT AND NOT A PILE OF .meta EDITS
    ///   <c>Assets/Packs/</c> is gitignored (.gitignore:107): vendor art is per-machine. An
    ///   import override written into <c>Assets/Packs/**/*.png.meta</c> therefore fixes the
    ///   build on ONE Mac and is invisible to every other clone and to a re-download of the
    ///   pack. The rule has to live somewhere tracked, so it lives here — as a table, a menu
    ///   item that applies it, and an <see cref="AssetPostprocessor"/> that re-applies it every
    ///   time one of these textures is imported. Same reasoning as memory
    ///   `project_assets_packs_is_gitignored`: derive project data into a tracked path.
    ///
    /// WHAT IT IS FOR — the number that made this task
    ///   The 1.5.7 Build Report attributes 460.7 MiB of the 1.74 GiB install to 53 PNGs under
    ///   Assets/Packs/PBR Bridge/3D Art/Textures. Every one of them imports at 4096 with NO
    ///   iPhone override, which is 9.5 MiB built (ASTC 6x6 + mips) — for a decorative bridge.
    ///   The brief blamed the tree packs for that bucket; the Build Report says otherwise, and
    ///   TreePackVol.1 / Simple Trees Pack / Mobile_Tree_Bundle / Pine Trees / MicroVerse-Extras
    ///   contribute exactly zero bytes to the player (no scene references them).
    ///
    /// THE BUDGET, AND WHY THESE NUMBERS
    ///   Albedo is what the eye reads, so it keeps the most resolution; normals halve; the
    ///   metallic/smoothness and occlusion masks are low-frequency data that survives a quarter.
    ///   Nothing here changes a texture's TYPE, sRGB flag, alpha handling or wrap mode — only
    ///   the iOS resolution cap and an explicitly pinned ASTC 6x6 (which is already what the
    ///   automatic choice picks: 4096 ASTC 6x6 + mips is exactly the 9.5 MiB the report shows).
    ///   So the ONLY visible variable is resolution, which is what the before/after captures in
    ///   the task folder are there to judge.
    ///
    /// VEGETATION IS DELIBERATELY LEFT ALONE
    ///   The only vegetation that reaches the player is Assets/Packs/BSP Trees Package (16.5 MiB)
    ///   and Assets/Realistic Tree (12.4 MiB) — already 2048, i.e. already at the "bark 2048"
    ///   line the SPEC asks for. Taking the leaf ALPHA cutouts to 1024 would save a further
    ///   ~7.5 MiB and is the one change in this phase that can visibly fray a silhouette, so it
    ///   is offered as <see cref="LeafBudget"/> with its A/B crops rather than taken silently.
    /// </summary>
    public static class PackTextureBudget
    {
        const string Tag = "[PackTextureBudget]";

        /// <summary>One rule: every texture under <see cref="Root"/> whose file name ends with
        /// <see cref="Suffix"/> (before the extension) is capped at <see cref="MaxSize"/> on
        /// iPhone. A null <see cref="Suffix"/> matches anything the earlier rules did not.</summary>
        public readonly struct Rule
        {
            public readonly string Root, Suffix;
            public readonly int MaxSize;
            public Rule(string root, string suffix, int maxSize) { Root = root; Suffix = suffix; MaxSize = maxSize; }
        }

        public const string BridgeRoot = "Assets/Packs/PBR Bridge/";

        /// <summary>
        /// The budget. Order matters — the first matching rule wins.
        /// </summary>
        public static readonly Rule[] Rules =
        {
            new Rule(BridgeRoot, "_d",  2048),  // albedo — the channel the eye reads
            new Rule(BridgeRoot, "_n",  1024),  // normal
            new Rule(BridgeRoot, "_m",   512),  // metallic / smoothness mask
            new Rule(BridgeRoot, "_ao",  512),  // occlusion mask
            new Rule(BridgeRoot, null,  1024),  // anything else in the pack (fence_d etc. fall to _d above)
        };

        /// <summary>
        /// NOT APPLIED. The leaf-alpha option described in the class docs, kept here so the
        /// switch is one line and its cost is written down rather than remembered: taking the
        /// four Spruce/fir leaf textures from 2048 to 1024 saves ~7.5 MiB install and is the
        /// only change in Phase 1 that can fray a cutout silhouette. Needs Cesar's "go" in
        /// STATUS.md and the A/B spruce crops in screenshots/.
        /// </summary>
        public const int LeafBudget = 1024;

        /// <summary>iOS build-target name as the TextureImporter platform API spells it.</summary>
        const string IPhone = "iPhone";

        /// <summary>
        /// Suspends the import-time enforcer. Set ONLY by <see cref="RevertBatch"/>, which exists
        /// to produce the BEFORE half of the A/B captures this phase is judged on: without it the
        /// enforcer would put the budget straight back on the reimport and the "before" frame
        /// would be the "after" frame with a different file name. Never leave it true.
        /// </summary>
        public static bool Suspended;

        /// <summary>The cap for <paramref name="assetPath"/>, or -1 when no rule covers it.</summary>
        public static int BudgetFor(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return -1;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            foreach (var r in Rules)
            {
                if (!assetPath.StartsWith(r.Root, StringComparison.Ordinal)) continue;
                if (r.Suffix == null || name.EndsWith(r.Suffix, StringComparison.OrdinalIgnoreCase))
                    return r.MaxSize;
            }
            return -1;
        }

        /// <summary>
        /// Writes the iPhone override onto <paramref name="importer"/> if it is not already
        /// exactly right. Returns true when something changed (so the caller knows whether a
        /// reimport is needed). Pure function of the budget table — the menu item and the
        /// postprocessor both go through here so they can never drift apart.
        /// </summary>
        public static bool ApplyTo(TextureImporter importer, int maxSize)
        {
            var s = importer.GetPlatformTextureSettings(IPhone);
            if (s.overridden && s.maxTextureSize == maxSize &&
                s.format == TextureImporterFormat.ASTC_6x6 &&
                s.textureCompression == TextureImporterCompression.Compressed)
                return false;

            s.name = IPhone;
            s.overridden = true;
            s.maxTextureSize = maxSize;
            // Pinned, not chosen: 4096 ASTC 6x6 + mips is 9.95 MiB, which is the 9.5 MiB the
            // Build Report already shows for these textures — so Automatic is ALREADY landing
            // here and pinning it changes no pixel, it only stops a future Unity default from
            // silently moving. Resolution is the only variable this tool actually moves.
            s.format = TextureImporterFormat.ASTC_6x6;
            s.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(s);
            return true;
        }

        [MenuItem("Tools/Golfin/Build Size/Apply Pack Texture Budget", false, 100)]
        public static void ApplyMenu() => Apply(logOnly: false);

        [MenuItem("Tools/Golfin/Build Size/Report Pack Texture Budget (no changes)", false, 101)]
        public static void ReportMenu() => Apply(logOnly: true);

        /// <summary>
        /// -executeMethod Golfin.EditorTools.BuildSize.PackTextureBudget.ApplyBatch
        /// Batchmode entry point (the pack folders are big enough that this is worth doing
        /// headlessly next to a build rather than in an interactive Editor).
        /// </summary>
        public static void ApplyBatch()
        {
            var report = Apply(logOnly: false);
            var outPath = "Docs/Specs/Active/build_size_diet/reference/pack_texture_budget.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, report);
            Debug.Log($"{Tag} wrote {outPath}");
        }

        /// <summary>Applies (or, with <paramref name="logOnly"/>, just tabulates) the budget and
        /// returns the table as text so both the menu item and batchmode print the same thing.</summary>
        public static string Apply(bool logOnly)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# reference/pack_texture_budget.txt — build_size_diet Phase 1");
            sb.AppendLine("# Written by Tools > Golfin > Build Size > Apply Pack Texture Budget");
            sb.AppendLine("# (PackTextureBudget.cs). Assets/Packs is gitignored, so THIS file and that");
            sb.AppendLine("# script are the tracked record of what the iPhone overrides are.");
            sb.AppendLine();
            sb.AppendLine($"{"was",6} {"now",6}  {"est. built MiB before -> after",34}  path");

            var guids = AssetDatabase.FindAssets("t:Texture2D", Rules.Select(r => r.Root.TrimEnd('/')).Distinct().ToArray());
            int changed = 0, seen = 0;
            double mbBefore = 0, mbAfter = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                int budget = BudgetFor(path);
                if (budget < 0) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                seen++;
                var cur = importer.GetPlatformTextureSettings(IPhone);
                int wasSize = cur.overridden ? cur.maxTextureSize : importer.maxTextureSize;
                // Effective built size is capped by the SOURCE resolution — a 512-pixel source
                // under a 2048 budget is still 512, and reporting otherwise would invent savings.
                importer.GetSourceTextureWidthAndHeight(out int srcW, out int srcH);
                int effBefore = Mathf.Min(wasSize, Mathf.Max(srcW, srcH));
                int effAfter = Mathf.Min(budget, Mathf.Max(srcW, srcH));
                mbBefore += Astc6x6MiB(effBefore);
                mbAfter += Astc6x6MiB(effAfter);

                sb.AppendLine($"{wasSize,6} {budget,6}  {Astc6x6MiB(effBefore),12:F2} -> {Astc6x6MiB(effAfter),8:F2}          {path}");

                if (logOnly) continue;
                if (ApplyTo(importer, budget)) { changed++; importer.SaveAndReimport(); }
            }

            sb.AppendLine();
            sb.AppendLine($"{seen} texture(s) under budget rules; {(logOnly ? 0 : changed)} reimported.");
            sb.AppendLine($"Estimated built total: {mbBefore:F1} MiB -> {mbAfter:F1} MiB  ({mbBefore - mbAfter:F1} MiB saved)");
            sb.AppendLine("(Estimate is ASTC 6x6 + full mip chain; the authoritative number is the");
            sb.AppendLine(" Build Report / data_*.txt pair, not this line.)");

            var text = sb.ToString();
            Debug.Log($"{Tag}\n{text}");
            return text;
        }

        [MenuItem("Tools/Golfin/Build Size/Revert Pack Texture Budget (A/B only)", false, 102)]
        public static void RevertMenu() => Revert();

        /// <summary>-executeMethod Golfin.EditorTools.BuildSize.PackTextureBudget.RevertBatch</summary>
        public static void RevertBatch() => Revert();

        /// <summary>
        /// Clears the iPhone override from every texture the budget covers, putting the pack back
        /// on its shipped-by-the-vendor settings (4096, whatever Unity picks). FOR EVIDENCE ONLY —
        /// it is how the "before" screenshots get taken after the change is already in. Re-run
        /// Apply Pack Texture Budget straight afterwards; the report says which state you are in.
        /// </summary>
        public static void Revert()
        {
            Suspended = true;
            int n = 0;
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D",
                             Rules.Select(r => r.Root.TrimEnd('/')).Distinct().ToArray()))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (BudgetFor(path) < 0) continue;
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;
                    var s = importer.GetPlatformTextureSettings(IPhone);
                    if (!s.overridden) continue;
                    s.overridden = false;
                    importer.SetPlatformTextureSettings(s);
                    importer.SaveAndReimport();
                    n++;
                }
            }
            finally { Suspended = false; }
            Debug.LogWarning($"{Tag} REVERTED {n} texture(s) to the vendor defaults. This is the A/B " +
                             $"'before' state and it is NOT what should ship — re-run " +
                             $"Tools > Golfin > Build Size > Apply Pack Texture Budget when the capture is taken.");
        }

        /// <summary>Bytes of an NxN ASTC 6x6 texture with a full mip chain, in MiB.
        /// ASTC 6x6 is 128 bits per 6x6 block = 16/36 byte per texel; mips add ~1/3.</summary>
        static double Astc6x6MiB(int size)
        {
            if (size <= 0) return 0;
            double bytes = (double)size * size * 16.0 / 36.0 * 4.0 / 3.0;
            return bytes / (1024.0 * 1024.0);
        }

        /// <summary>
        /// The durable half. A pack re-download, a "Reimport All", or a fresh clone that copies
        /// the vendor folder in would otherwise land back on 4096 with no override and quietly
        /// put 420 MiB back into the install, because the .meta that carried the fix is
        /// gitignored. This puts the rule back on every import of a covered texture.
        ///
        /// Deliberately does NOT override GetVersion(): that would invalidate and reimport EVERY
        /// texture in the project (this class defines OnPreprocessTexture, so Unity would treat
        /// all of them as dependent), which is a 5,000-asset reimport to fix 53 files. The menu
        /// item is how the existing textures get converted; this guards what comes after.
        /// </summary>
        sealed class Enforcer : AssetPostprocessor
        {
            void OnPreprocessTexture()
            {
                if (Suspended) return;
                int budget = BudgetFor(assetPath);
                if (budget < 0) return;
                if (ApplyTo((TextureImporter)assetImporter, budget))
                    Debug.Log($"{Tag} import-time budget applied: {assetPath} -> iPhone max {budget}, ASTC 6x6.");
            }
        }
    }
}
#endif
