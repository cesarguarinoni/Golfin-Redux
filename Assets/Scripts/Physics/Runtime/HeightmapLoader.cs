using System.IO;
using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Loads heightmap.bytes (baked by PhysicsHeightmapBaker) into a HeightmapData.
    /// Format: 36-byte header (GHM1 magic + version + resolution + sizeX/Z + posX/Y/Z + format),
    /// then row-major [y, x] int32 Q16.16 heights in meters.
    /// </summary>
    public static class HeightmapLoader
    {
        public static HeightmapData LoadFromBytes(byte[] data)
        {
            if (data == null || data.Length < 36) return null;
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                if (br.ReadByte() != 'G' || br.ReadByte() != 'H' ||
                    br.ReadByte() != 'M' || br.ReadByte() != '1')
                {
                    Debug.LogError("[HeightmapLoader] Bad magic; expected GHM1.");
                    return null;
                }
                int version = br.ReadInt32();
                if (version != 1)
                {
                    Debug.LogError($"[HeightmapLoader] Unknown version {version}.");
                    return null;
                }
                int   res    = br.ReadInt32();
                float sx     = br.ReadSingle();
                float sz     = br.ReadSingle();
                float px     = br.ReadSingle();
                float py     = br.ReadSingle();
                float pz     = br.ReadSingle();
                int   format = br.ReadInt32();
                if (format != 1)
                {
                    Debug.LogError($"[HeightmapLoader] Unknown format {format}; expected Q16.16.");
                    return null;
                }

                var heights = new int[res * res];
                for (int i = 0; i < heights.Length; i++)
                    heights[i] = br.ReadInt32();

                return new HeightmapData(
                    res,
                    fp.FromFloat(sx), fp.FromFloat(sz),
                    fp.FromFloat(px), fp.FromFloat(py), fp.FromFloat(pz),
                    heights);
            }
        }

        /// <summary>Convenience loader from a scene-attached TextAsset reference.</summary>
        public static HeightmapData LoadFromTextAsset(TextAsset asset)
            => asset == null ? null : LoadFromBytes(asset.bytes);
    }
}
