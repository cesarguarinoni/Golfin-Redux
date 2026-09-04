using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Golfin.Physics.Runtime.Baked;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// The single door onto <c>Resources/HoleData/&lt;courseSlug&gt;/&lt;holeId&gt;/…</c>.
    ///
    /// build_size_diet Phase 2 changed the BYTES of two of those files and NOTHING else — not
    /// the folder layout, not a <c>Resources.Load</c> path, not a hole's data. Everything that
    /// used to do
    ///
    ///     Resources.Load&lt;TextAsset&gt;($"HoleData/{slug}/{hole}/zones").text
    ///
    /// now goes through <see cref="LoadZonesText"/>, because the shipped <c>zones</c> asset is a
    /// gzip of the minified JSON (96.9 MiB of pretty-printed JSON across 19 holes became 7.1 MiB)
    /// and <c>TextAsset.text</c> on gzip is mojibake, not JSON.
    ///
    /// IT SNIFFS RATHER THAN BRANCHING ON A FILE NAME. <see cref="DecodeZonesText"/> looks at the
    /// first two bytes: 1F 8B is gzip, anything else is treated as UTF-8 text. That is the
    /// fallback the spec asks for — an un-migrated working tree with the old uncompressed
    /// <c>zones.json</c> still loads through exactly this code, and so does a hand-written test
    /// fixture. One load, one path, no "try the other extension" round trip.
    ///
    /// The heightmap needs no equivalent: <see cref="HeightmapLoader"/> already dispatches on its
    /// own GHM1/GHM2 magic, so its callers were not touched.
    /// </summary>
    public static class HoleDataIO
    {
        /// <summary>Resources path (extension-less, as Resources.Load wants) of a hole's zones asset.</summary>
        public static string ZonesResourcePath(string courseSlug, string holeId)
            => $"HoleData/{courseSlug}/{holeId}/zones";

        /// <summary>Resources path of a hole's physics heightmap.</summary>
        public static string HeightmapResourcePath(string courseSlug, string holeId)
            => $"HoleData/{courseSlug}/{holeId}/heightmap";

        /// <summary>
        /// The hole's zones JSON as text, or null when the asset is missing. Handles both the
        /// shipped gzip <c>zones.bytes</c> and a legacy plain <c>zones.json</c>.
        /// </summary>
        public static string LoadZonesText(string courseSlug, string holeId)
            => DecodeZonesText(Resources.Load<TextAsset>(ZonesResourcePath(courseSlug, holeId)));

        /// <summary>As <see cref="LoadZonesText(string,string)"/>, for a TextAsset already in hand.</summary>
        public static string DecodeZonesText(TextAsset asset)
            => asset == null ? null : DecodeZonesText(asset.bytes);

        /// <summary>
        /// gzip → JSON text, or the bytes as UTF-8 when they are not gzip. Returns null on a
        /// corrupt stream (logged), never throws at a call site that just wanted a hole to load.
        /// </summary>
        public static string DecodeZonesText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            if (!IsGzip(bytes)) return new System.Text.UTF8Encoding(false).GetString(bytes);

            try
            {
                using (var ms = new MemoryStream(bytes, writable: false))
                using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                using (var sr = new StreamReader(gz, System.Text.Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch (Exception e)
            {
                Debug.LogError($"[HoleDataIO] zones gunzip failed ({e.GetType().Name}: {e.Message}).");
                return null;
            }
        }

        /// <summary>
        /// The zones file for a hole FOLDER on disk — <c>zones.bytes</c> if it is there, else the
        /// legacy <c>zones.json</c>, else null. For edit-time tools and EditMode tests, which read
        /// the project tree directly rather than through Resources (the Resources cache is stale
        /// right after an AssetDatabase.Refresh, which is exactly when a bake runs).
        /// </summary>
        public static string ZonesDiskPath(string holeFolder)
        {
            if (string.IsNullOrEmpty(holeFolder)) return null;
            string bytes = Path.Combine(holeFolder, "zones.bytes");
            if (File.Exists(bytes)) return bytes;
            string json = Path.Combine(holeFolder, "zones.json");
            return File.Exists(json) ? json : null;
        }

        /// <summary>Zones JSON text for a hole folder on disk, or null when the hole has none.</summary>
        public static string LoadZonesTextFromDisk(string holeFolder)
        {
            string path = ZonesDiskPath(holeFolder);
            return path == null ? null : DecodeZonesText(File.ReadAllBytes(path));
        }

        /// <summary>The inverse, for the baker and the one-shot converter.</summary>
        public static byte[] EncodeZones(string json)
        {
            var raw = new System.Text.UTF8Encoding(false).GetBytes(json);
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                    gz.Write(raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Null when the two ZoneData are equal in every field the simulation reads; otherwise
        /// the FIRST difference, named.
        ///
        /// This lives next to the codec, not in the editor tool that first needed it, so that the
        /// one-shot converter's gate and the EditMode parity test are THE SAME COMPARISON. A test
        /// that re-implements the check it is verifying can agree with the bug.
        /// </summary>
        public static string ZoneDataDiff(ZoneData a, ZoneData b)
        {
            if (a == null || b == null) return $"null ZoneData (a={(a == null ? "null" : "ok")}, b={(b == null ? "null" : "ok")})";
            if (a.holeId != b.holeId) return $"holeId '{a.holeId}' != '{b.holeId}'";

            int za = a.zones?.Count ?? -1, zb = b.zones?.Count ?? -1;
            if (za != zb) return $"zone group count {za} != {zb}";

            for (int i = 0; i < za; i++)
            {
                var ga = a.zones[i]; var gb = b.zones[i];
                if (ga.type != gb.type) return $"zone[{i}].type '{ga.type}' != '{gb.type}'";
                if (!ga.yOffsetFromTerrain.Equals(gb.yOffsetFromTerrain))
                    return $"zone[{i}] ({ga.type}).yOffsetFromTerrain {F(ga.yOffsetFromTerrain)} != {F(gb.yOffsetFromTerrain)}";

                int pa = ga.polygons?.Count ?? -1, pb = gb.polygons?.Count ?? -1;
                if (pa != pb) return $"zone[{i}] ({ga.type}) polygon count {pa} != {pb}";
                for (int p = 0; p < pa; p++)
                {
                    var ra = ga.polygons[p].points; var rb = gb.polygons[p].points;
                    int na = ra?.Count ?? -1, nb = rb?.Count ?? -1;
                    if (na != nb) return $"zone[{i}] ({ga.type}) poly[{p}] vertex count {na} != {nb}";
                    for (int v = 0; v < na; v++)
                        if (!ra[v].x.Equals(rb[v].x) || !ra[v].z.Equals(rb[v].z))
                            return $"zone[{i}] ({ga.type}) poly[{p}] vertex {v} ({F(ra[v].x)},{F(ra[v].z)}) != ({F(rb[v].x)},{F(rb[v].z)})";
                }

                int va = ga.mesh?.vertices?.Count ?? -1, vb = gb.mesh?.vertices?.Count ?? -1;
                if (va != vb) return $"zone[{i}] ({ga.type}) mesh vertex count {va} != {vb}";
                for (int v = 0; v < va; v++)
                {
                    var ma = ga.mesh.vertices[v]; var mb = gb.mesh.vertices[v];
                    if (!ma.x.Equals(mb.x) || !ma.y.Equals(mb.y) || !ma.z.Equals(mb.z))
                        return $"zone[{i}] ({ga.type}) mesh vertex {v} differs";
                }
                int ia = ga.mesh?.indices?.Count ?? -1, ib = gb.mesh?.indices?.Count ?? -1;
                if (ia != ib) return $"zone[{i}] ({ga.type}) mesh index count {ia} != {ib}";
                for (int k = 0; k < ia; k++)
                    if (ga.mesh.indices[k] != gb.mesh.indices[k])
                        return $"zone[{i}] ({ga.type}) mesh index {k} differs";
            }

            var oa = a.obMask; var ob = b.obMask;
            if ((oa == null) != (ob == null)) return "obMask present on one side only";
            if (oa != null)
            {
                if (oa.width != ob.width || oa.height != ob.height) return "obMask dimensions differ";
                if (!oa.worldOriginX.Equals(ob.worldOriginX) || !oa.worldOriginZ.Equals(ob.worldOriginZ))
                    return "obMask world origin differs";
                if (!oa.worldSizeX.Equals(ob.worldSizeX) || !oa.worldSizeZ.Equals(ob.worldSizeZ))
                    return "obMask world size differs";
                if (oa.maskBase64 != ob.maskBase64) return "obMask payload differs";
            }
            return null;
        }

        static string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>gzip member header: 1F 8B.</summary>
        public static bool IsGzip(byte[] b) => b != null && b.Length >= 2 && b[0] == 0x1F && b[1] == 0x8B;

        /// <summary>
        /// Whitespace-only JSON minifier: strips every space, tab, CR and LF that is OUTSIDE a
        /// string literal and touches nothing else.
        ///
        /// DELIBERATELY NOT A PARSE-AND-RE-SERIALIZE. Round-tripping through JsonUtility would
        /// silently drop any field <c>ZoneData</c> does not model and re-format every float
        /// through a second decimal conversion — on data the deterministic simulation reads.
        /// Stripping whitespace leaves the token stream byte-identical, so the parsed
        /// <c>ZoneData</c> cannot differ; the equality test over all 19 holes proves it rather
        /// than assuming it, and gzip of the minified text is another 15% smaller than gzip of
        /// the pretty text (7.1 vs 8.4 MiB across the course) and parses faster.
        /// </summary>
        public static string MinifyJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            var sb = new System.Text.StringBuilder(json.Length);
            bool inString = false, escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    sb.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; sb.Append(c); continue; }
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
