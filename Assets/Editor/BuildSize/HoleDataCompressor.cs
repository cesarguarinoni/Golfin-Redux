#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Golfin.Physics.Runtime;
using Golfin.Physics.Runtime.Baked;

namespace Golfin.EditorTools.BuildSize
{
    /// <summary>
    /// build_size_diet Phase 2 — the ONE-SHOT converter that rewrites the shipped hole data in
    /// place: <c>heightmap.bytes</c> GHM1 → GHM2, and <c>zones.json</c> → gzip <c>zones.bytes</c>.
    ///
    /// Together those two files are 385 MiB of the 1.74 GiB install and they ship whole, because
    /// everything under <c>Assets/Resources/</c> ships whether the player reaches it or not.
    ///
    /// THE ONLY THING THAT MATTERS HERE IS THAT NOTHING CHANGES BUT THE BYTES.
    ///   The heightmap is int32 Q16.16 fixed point feeding an fp-deterministic simulation, so
    ///   "close enough" is not a category that exists: one changed sample moves where the ball
    ///   comes to rest. Every hole therefore goes decode → encode → decode and is refused unless
    ///   the round-tripped <c>int[]</c> is SequenceEqual to the original AND every header field
    ///   matches; the SHA-256 of the decoded ints before and after goes in the report so the
    ///   claim is checkable later by someone who does not trust this comment.
    ///
    ///   zones is minified by stripping whitespace outside string literals — never by
    ///   parse-and-re-serialize, which would drop fields ZoneData does not model and re-format
    ///   every float — and then compared field by field through <see cref="ZoneDataDiff"/>
    ///   (polygon counts, every vertex, every surface enum NAME, yOffsets, obMask, zone meshes).
    ///
    /// A hole that fails either check is LEFT UNTOUCHED and reported. Half a conversion is worse
    /// than none.
    /// </summary>
    public static class HoleDataCompressor
    {
        const string Tag = "[HoleDataCompressor]";
        const string ResourcesRoot = "Assets/Resources/HoleData";
        const string ReportPath = "Docs/Specs/Active/build_size_diet/reference/holedata_conversion.txt";

        [MenuItem("Tools/Golfin/Build Size/Convert HoleData (dry run)", false, 200)]
        public static void DryRunMenu() => Run(dryRun: true);

        [MenuItem("Tools/Golfin/Build Size/Convert HoleData", false, 201)]
        public static void ConvertMenu() => Run(dryRun: false);

        /// <summary>-executeMethod Golfin.EditorTools.BuildSize.HoleDataCompressor.ConvertBatch</summary>
        public static void ConvertBatch() => Run(dryRun: false);

        /// <summary>-executeMethod Golfin.EditorTools.BuildSize.HoleDataCompressor.DryRunBatch</summary>
        public static void DryRunBatch() => Run(dryRun: true);

        public static string Run(bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# reference/holedata_conversion.txt — build_size_diet Phase 2");
            sb.AppendLine($"# Tools > Golfin > Build Size > Convert HoleData    ({(dryRun ? "DRY RUN" : "APPLIED")})");
            sb.AppendLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            int failures = 0;
            failures += ConvertHeightmaps(sb, dryRun);
            sb.AppendLine();
            failures += ConvertZones(sb, dryRun);

            sb.AppendLine();
            sb.AppendLine(failures == 0
                ? "ALL HOLES VERIFIED — every decoded int[] and every ZoneData field identical before/after."
                : $"*** {failures} HOLE(S) FAILED VERIFICATION AND WERE LEFT UNTOUCHED — see the rows above. ***");

            if (!dryRun) AssetDatabase.Refresh();

            var text = sb.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, text);
            Debug.Log($"{Tag}\n{text}\n{Tag} report → {ReportPath}");
            return text;
        }

        // ------------------------------------------------------------------ //
        // heightmap.bytes : GHM1 -> GHM2
        // ------------------------------------------------------------------ //

        static int ConvertHeightmaps(StringBuilder sb, bool dryRun)
        {
            sb.AppendLine("== heightmap.bytes : GHM1 -> GHM2 (lossless: row deltas + Deflate) ==");
            sb.AppendLine($"{"hole",-34} {"before B",12} {"after B",12} {"x",6}  {"sha256(decoded int[]) before -> after",20}");

            var files = Directory.Exists(ResourcesRoot)
                ? Directory.GetFiles(ResourcesRoot, "heightmap.bytes", SearchOption.AllDirectories).OrderBy(p => p).ToArray()
                : Array.Empty<string>();
            if (files.Length == 0) sb.AppendLine("  (none found)");

            long totBefore = 0, totAfter = 0;
            int fails = 0;

            foreach (var osPath in files)
            {
                string assetPath = ToAssetPath(osPath);
                var before = File.ReadAllBytes(osPath);
                long beforeLen = before.Length;

                if (before.Length >= 4 && before[3] == '2')
                {
                    sb.AppendLine($"{Rel(assetPath),-34} {beforeLen,12} {beforeLen,12} {"-",6}  already GHM2, skipped");
                    totBefore += beforeLen; totAfter += beforeLen;
                    continue;
                }

                if (!HeightmapLoader.TryDecode(before, out var src))
                {
                    sb.AppendLine($"{Rel(assetPath),-34} {beforeLen,12} {"-",12} {"-",6}  *** FAIL: GHM1 would not decode");
                    fails++; continue;
                }

                var encoded = HeightmapLoader.EncodeGhm2(src);
                bool decoded = HeightmapLoader.TryDecode(encoded, out var rt);

                string shaBefore = Sha256(src.heights);
                string shaAfter  = decoded ? Sha256(rt.heights) : "<undecodable>";

                bool ok = decoded
                          && shaBefore == shaAfter
                          && SequenceEqual(rt.heights, src.heights)
                          && rt.res == src.res
                          && rt.sizeX.Equals(src.sizeX) && rt.sizeZ.Equals(src.sizeZ)
                          && rt.posX.Equals(src.posX) && rt.posY.Equals(src.posY) && rt.posZ.Equals(src.posZ);

                if (!ok)
                {
                    sb.AppendLine($"{Rel(assetPath),-34} {beforeLen,12} {encoded.Length,12} {"-",6}  *** FAIL: round trip differs " +
                                  $"({shaBefore.Substring(0, 12)} -> {shaAfter.Substring(0, Math.Min(12, shaAfter.Length))}) — LEFT UNTOUCHED");
                    fails++; continue;
                }

                totBefore += beforeLen; totAfter += encoded.Length;
                sb.AppendLine($"{Rel(assetPath),-34} {beforeLen,12} {encoded.Length,12} {(double)beforeLen / encoded.Length,6:F1}  " +
                              $"{shaBefore.Substring(0, 16)} -> {shaAfter.Substring(0, 16)}  MATCH");

                if (!dryRun)
                {
                    // Overwritten IN PLACE so the .meta — and with it the GUID that
                    // HeightProvider's scene-serialised heightmapAsset reference points at —
                    // survives untouched.
                    File.WriteAllBytes(osPath, encoded);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }

            sb.AppendLine($"{"TOTAL",-34} {totBefore,12} {totAfter,12} {(totAfter > 0 ? (double)totBefore / totAfter : 0),6:F1}  " +
                          $"({totBefore / 1048576.0:F1} MiB -> {totAfter / 1048576.0:F1} MiB)");
            return fails;
        }

        // ------------------------------------------------------------------ //
        // zones.json -> zones.bytes (gzip of the minified JSON)
        // ------------------------------------------------------------------ //

        static int ConvertZones(StringBuilder sb, bool dryRun)
        {
            sb.AppendLine("== zones.json -> zones.bytes (gzip of whitespace-minified JSON) ==");
            sb.AppendLine($"{"hole",-34} {"before B",12} {"after B",12} {"x",6}  ZoneData equality");

            var files = Directory.Exists(ResourcesRoot)
                ? Directory.GetFiles(ResourcesRoot, "zones.json", SearchOption.AllDirectories).OrderBy(p => p).ToArray()
                : Array.Empty<string>();
            if (files.Length == 0) sb.AppendLine("  (none found — already converted?)");

            long totBefore = 0, totAfter = 0;
            int fails = 0;

            foreach (var osPath in files)
            {
                string assetPath = ToAssetPath(osPath);
                string bytesAssetPath = Path.ChangeExtension(assetPath, ".bytes");
                string json = File.ReadAllText(osPath);
                long beforeLen = new FileInfo(osPath).Length;

                string minified = HoleDataIO.MinifyJson(json);
                byte[] gz = HoleDataIO.EncodeZones(minified);
                string back = HoleDataIO.DecodeZonesText(gz);

                var a = ZoneData.FromJson(json);
                var b = back == null ? null : ZoneData.FromJson(back);
                string diff = HoleDataIO.ZoneDataDiff(a, b);

                if (diff != null)
                {
                    sb.AppendLine($"{Rel(assetPath),-34} {beforeLen,12} {gz.Length,12} {"-",6}  *** FAIL: {diff} — LEFT UNTOUCHED");
                    fails++; continue;
                }

                totBefore += beforeLen; totAfter += gz.Length;
                sb.AppendLine($"{Rel(assetPath),-34} {beforeLen,12} {gz.Length,12} {(double)beforeLen / gz.Length,6:F1}  IDENTICAL");

                if (dryRun) continue;

                // MoveAsset first, so the .bytes asset INHERITS the .json's GUID. Nothing
                // currently references zones by GUID (every call site is Resources.Load by
                // path), but a renamed asset that silently takes a new GUID is exactly how a
                // scene reference turns into a null later.
                string moveError = AssetDatabase.MoveAsset(assetPath, bytesAssetPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    sb.AppendLine($"    *** could not rename to .bytes: {moveError} — LEFT UNTOUCHED");
                    fails++; continue;
                }
                File.WriteAllBytes(ToOsPath(bytesAssetPath), gz);
                AssetDatabase.ImportAsset(bytesAssetPath, ImportAssetOptions.ForceUpdate);
            }

            sb.AppendLine($"{"TOTAL",-34} {totBefore,12} {totAfter,12} {(totAfter > 0 ? (double)totBefore / totAfter : 0),6:F1}  " +
                          $"({totBefore / 1048576.0:F1} MiB -> {totAfter / 1048576.0:F1} MiB)");
            return fails;
        }

        // ------------------------------------------------------------------ //
        // Verification helpers
        // ------------------------------------------------------------------ //

        static bool SequenceEqual(int[] x, int[] y)
        {
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        static string Sha256(int[] values)
        {
            var bytes = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        static string ToAssetPath(string osPath)
        {
            string full = Path.GetFullPath(osPath).Replace('\\', '/');
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/') + "/";
            return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length) : osPath.Replace('\\', '/');
        }

        static string ToOsPath(string assetPath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        static string Rel(string assetPath) => assetPath.StartsWith(ResourcesRoot + "/", StringComparison.Ordinal)
            ? assetPath.Substring(ResourcesRoot.Length + 1)
            : assetPath;
    }
}
#endif
