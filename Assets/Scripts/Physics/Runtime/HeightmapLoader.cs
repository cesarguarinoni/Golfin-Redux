using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Loads heightmap.bytes (baked by PhysicsHeightmapBaker) into a HeightmapData.
    ///
    /// TWO FORMATS, ONE MEANING. Both carry the SAME 36-byte header and decode to the SAME
    /// <c>int[]</c>; they differ only in how the samples are stored after byte 36:
    ///
    ///   GHM1  version 1, format 1 — 2049² row-major [y, x] int32 Q16.16 heights, verbatim.
    ///   GHM2  version 2, format 2 — the same ints as ROW DELTAS (first column of each row raw,
    ///         then h[x] - h[x-1]), little-endian, through a raw Deflate stream.
    ///
    /// GHM2 is LOSSLESS and that is the whole point (build_size_diet Phase 2). These are not
    /// float32 heights that could tolerate a lossy codec — they are fixed-point Q16.16 feeding an
    /// fp-deterministic simulation, so a single changed sample moves where the ball comes to
    /// rest. Row deltas + Deflate takes a hole from 16.0 MiB to ~2.0 MiB with the decoded int[]
    /// bit-identical; the converter that rewrote the shipped files refused any hole whose
    /// round trip was not SequenceEqual, and put the SHA-256 of the decoded ints before and
    /// after in the task report.
    ///
    /// GHM1 IS STILL READ AND MUST STAY READABLE: GreenTopologyTests and the physics fixtures
    /// write GHM1 by hand, and an un-migrated working tree would otherwise lose its holes.
    /// </summary>
    public static class HeightmapLoader
    {
        /// <summary>magic(4) + version(4) + res(4) + sizeX/sizeZ/posX/posY/posZ(20) + format(4).</summary>
        public const int HeaderBytes = 36;

        /// <summary>
        /// A decoded heightmap file: the header fields plus the Q16.16 samples, before they are
        /// wrapped in a <see cref="HeightmapData"/>. Exists because the converter and the format
        /// tests need the raw <c>int[]</c> to compare — HeightmapData keeps its heights private,
        /// and it should.
        /// </summary>
        public struct Decoded
        {
            public int res;
            public float sizeX, sizeZ, posX, posY, posZ;
            public int[] heights;
        }

        public static HeightmapData LoadFromBytes(byte[] data)
        {
            if (!TryDecode(data, out var d)) return null;
            return new HeightmapData(
                d.res,
                fp.FromFloat(d.sizeX), fp.FromFloat(d.sizeZ),
                fp.FromFloat(d.posX), fp.FromFloat(d.posY), fp.FromFloat(d.posZ),
                d.heights);
        }

        /// <summary>Convenience loader from a scene-attached TextAsset reference.</summary>
        public static HeightmapData LoadFromTextAsset(TextAsset asset)
            => asset == null ? null : LoadFromBytes(asset.bytes);

        /// <summary>
        /// Reads GHM1 or GHM2. Returns false (and logs) on anything malformed; never throws.
        /// </summary>
        public static bool TryDecode(byte[] data, out Decoded result)
        {
            result = default;
            if (data == null || data.Length < HeaderBytes) return false;

            byte m3 = data[3];
            if (data[0] != 'G' || data[1] != 'H' || data[2] != 'M' || (m3 != '1' && m3 != '2'))
            {
                Debug.LogError("[HeightmapLoader] Bad magic; expected GHM1 or GHM2.");
                return false;
            }
            bool ghm2 = m3 == '2';
            int expected = ghm2 ? 2 : 1;

            using (var ms = new MemoryStream(data, writable: false))
            using (var br = new BinaryReader(ms))
            {
                br.ReadBytes(4);                       // magic, already validated
                int version = br.ReadInt32();
                if (version != expected)
                {
                    Debug.LogError($"[HeightmapLoader] GHM{(char)m3} carries version {version}; expected {expected}.");
                    return false;
                }
                var d = new Decoded
                {
                    res   = br.ReadInt32(),
                    sizeX = br.ReadSingle(),
                    sizeZ = br.ReadSingle(),
                    posX  = br.ReadSingle(),
                    posY  = br.ReadSingle(),
                    posZ  = br.ReadSingle(),
                };
                int format = br.ReadInt32();
                if (format != expected)
                {
                    Debug.LogError($"[HeightmapLoader] Unknown format {format}; expected {expected} " +
                                   (ghm2 ? "(deflated row deltas)." : "(Q16.16)."));
                    return false;
                }
                if (d.res <= 0 || (long)d.res * d.res > int.MaxValue / 4)
                {
                    Debug.LogError($"[HeightmapLoader] Implausible resolution {d.res}.");
                    return false;
                }

                if (ghm2)
                {
                    d.heights = DecodeDeltas(data, d.res);
                    if (d.heights == null) return false;
                }
                else
                {
                    long need = (long)d.res * d.res * 4;
                    if (data.Length - HeaderBytes < need)
                    {
                        Debug.LogError($"[HeightmapLoader] GHM1 truncated: {data.Length - HeaderBytes} B for {need} B of samples.");
                        return false;
                    }
                    d.heights = new int[d.res * d.res];
                    for (int i = 0; i < d.heights.Length; i++) d.heights[i] = br.ReadInt32();
                }

                result = d;
                return true;
            }
        }

        // ------------------------------------------------------------------ //
        // GHM2 codec
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Encodes a GHM2 file: the 36-byte header, then Deflate over the row-delta int32s.
        /// The exact inverse of the GHM2 branch of <see cref="TryDecode"/>; both halves live in
        /// this file so the format cannot drift apart. Used by the one-shot converter and by
        /// PhysicsHeightmapBaker.
        /// </summary>
        public static byte[] EncodeGhm2(in Decoded d)
        {
            if (d.heights == null || d.heights.Length != d.res * d.res)
                throw new ArgumentException($"heights length {d.heights?.Length ?? -1} != res*res ({d.res * d.res}).");

            var deltas = new int[d.heights.Length];
            for (int y = 0; y < d.res; y++)
            {
                int b = y * d.res;
                deltas[b] = d.heights[b];                          // first column of each row: raw
                for (int x = 1; x < d.res; x++)
                    deltas[b + x] = d.heights[b + x] - d.heights[b + x - 1];
            }

            var payload = new byte[deltas.Length * 4];
            IntsToBytes(deltas, payload);

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, new System.Text.UTF8Encoding(false), leaveOpen: true))
                {
                    bw.Write((byte)'G'); bw.Write((byte)'H'); bw.Write((byte)'M'); bw.Write((byte)'2');
                    bw.Write(2);                                    // version
                    bw.Write(d.res);
                    bw.Write(d.sizeX); bw.Write(d.sizeZ);
                    bw.Write(d.posX);  bw.Write(d.posY); bw.Write(d.posZ);
                    bw.Write(2);                                    // format = deflated row deltas
                }
                using (var def = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                    def.Write(payload, 0, payload.Length);
                return ms.ToArray();
            }
        }

        /// <summary>Inflates and un-deltas the GHM2 payload that starts at byte 36.</summary>
        static int[] DecodeDeltas(byte[] data, int res)
        {
            int count = res * res;
            var payload = new byte[count * 4];

            try
            {
                using (var ms = new MemoryStream(data, HeaderBytes, data.Length - HeaderBytes, writable: false))
                using (var inf = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    int read = 0;
                    while (read < payload.Length)
                    {
                        // DeflateStream is free to return short reads; the loop is not optional.
                        int n = inf.Read(payload, read, payload.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    if (read != payload.Length)
                    {
                        Debug.LogError($"[HeightmapLoader] GHM2 payload truncated: {read} of {payload.Length} bytes.");
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HeightmapLoader] GHM2 inflate failed: {e.GetType().Name}: {e.Message}");
                return null;
            }

            var heights = new int[count];
            BytesToInts(payload, heights);
            for (int y = 0; y < res; y++)
            {
                int b = y * res;
                for (int x = 1; x < res; x++)
                    heights[b + x] += heights[b + x - 1];
            }
            return heights;
        }

        // Little-endian is CHECKED, not assumed. Every platform this ships to (arm64 iOS, arm64
        // and x64 macOS, x64 Windows editor) is little-endian, so the block copy is correct
        // there; on anything else the explicit path keeps the bytes right instead of silently
        // producing a byte-swapped heightmap that would look almost plausible.
        static void IntsToBytes(int[] src, byte[] dst)
        {
            if (BitConverter.IsLittleEndian) { Buffer.BlockCopy(src, 0, dst, 0, dst.Length); return; }
            for (int i = 0; i < src.Length; i++)
            {
                int v = src[i], o = i * 4;
                dst[o] = (byte)v; dst[o + 1] = (byte)(v >> 8); dst[o + 2] = (byte)(v >> 16); dst[o + 3] = (byte)(v >> 24);
            }
        }

        static void BytesToInts(byte[] src, int[] dst)
        {
            if (BitConverter.IsLittleEndian) { Buffer.BlockCopy(src, 0, dst, 0, src.Length); return; }
            for (int i = 0; i < dst.Length; i++)
            {
                int o = i * 4;
                dst[i] = src[o] | (src[o + 1] << 8) | (src[o + 2] << 16) | (src[o + 3] << 24);
            }
        }
    }
}
