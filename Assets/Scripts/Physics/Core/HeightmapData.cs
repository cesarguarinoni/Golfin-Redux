using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// In-memory Q16.16 heightmap. Row-major [z, x]. Metric units (meters).
    /// Indexed by (worldX, worldZ) via SampleHeight; performs bilinear interpolation
    /// between the four nearest grid cells for sub-cell precision.
    ///
    /// Built by HeightmapLoader (Runtime) from heightmap.bytes. Pure math here —
    /// no UnityEngine, no Resources, no file I/O.
    /// </summary>
    public sealed class HeightmapData : IGroundProvider
    {
        public readonly int Resolution;
        public readonly fp SizeX, SizeZ;
        public readonly fp OriginX, OriginY, OriginZ;
        private readonly int[] heights; // Q16.16 raw; length = Resolution * Resolution

        public HeightmapData(int resolution, fp sizeX, fp sizeZ,
                             fp originX, fp originY, fp originZ, int[] heights)
        {
            Resolution = resolution;
            SizeX = sizeX; SizeZ = sizeZ;
            OriginX = originX; OriginY = originY; OriginZ = originZ;
            this.heights = heights;
        }

        public fp SampleHeight(fp worldX, fp worldZ)
        {
            // Convert world to grid coords.
            fp gx = ((worldX - OriginX) / SizeX) * fp.FromInt(Resolution - 1);
            fp gz = ((worldZ - OriginZ) / SizeZ) * fp.FromInt(Resolution - 1);

            // Clamp to valid range.
            fp maxIdx = fp.FromInt(Resolution - 1);
            gx = fpMath.Clamp(gx, fp.Zero, maxIdx);
            gz = fpMath.Clamp(gz, fp.Zero, maxIdx);

            // Integer parts via raw shift (Q16.16: integer = raw >> 16).
            int ix = (int)(gx.raw >> 16);
            int iz = (int)(gz.raw >> 16);
            if (ix >= Resolution - 1) ix = Resolution - 2;
            if (iz >= Resolution - 1) iz = Resolution - 2;

            // Fractional parts.
            fp fx = gx - fp.FromInt(ix);
            fp fz = gz - fp.FromInt(iz);

            // Bilinear sample.
            fp h00 = fp.FromRaw((long)heights[iz * Resolution + ix]);
            fp h10 = fp.FromRaw((long)heights[iz * Resolution + (ix + 1)]);
            fp h01 = fp.FromRaw((long)heights[(iz + 1) * Resolution + ix]);
            fp h11 = fp.FromRaw((long)heights[(iz + 1) * Resolution + (ix + 1)]);

            fp h0 = h00 + (h10 - h00) * fx;
            fp h1 = h01 + (h11 - h01) * fx;
            return OriginY + h0 + (h1 - h0) * fz;
        }

        /// <summary>
        /// Surface normal at (worldX, worldZ), computed from heightmap gradient via central differences.
        /// Unit vector, pointing away from the ground (positive Y component).
        /// </summary>
        public fp3 SampleNormal(fp worldX, fp worldZ)
        {
            fp cellX = SizeX / fp.FromInt(Resolution - 1);
            fp cellZ = SizeZ / fp.FromInt(Resolution - 1);

            // Use one-sided differences at boundaries to avoid clamping bias
            // (central diff with a clamped out-of-bounds sample halves the gradient).
            fp dhdx, dhdz;
            fp minX = OriginX;
            fp maxX = OriginX + SizeX;
            fp minZ = OriginZ;
            fp maxZ = OriginZ + SizeZ;

            if (worldX <= minX)
                dhdx = (SampleHeight(worldX + cellX, worldZ) - SampleHeight(worldX, worldZ)) / cellX;
            else if (worldX >= maxX)
                dhdx = (SampleHeight(worldX, worldZ) - SampleHeight(worldX - cellX, worldZ)) / cellX;
            else
                dhdx = (SampleHeight(worldX + cellX, worldZ) - SampleHeight(worldX - cellX, worldZ)) / (cellX * fp.FromInt(2));

            if (worldZ <= minZ)
                dhdz = (SampleHeight(worldX, worldZ + cellZ) - SampleHeight(worldX, worldZ)) / cellZ;
            else if (worldZ >= maxZ)
                dhdz = (SampleHeight(worldX, worldZ) - SampleHeight(worldX, worldZ - cellZ)) / cellZ;
            else
                dhdz = (SampleHeight(worldX, worldZ + cellZ) - SampleHeight(worldX, worldZ - cellZ)) / (cellZ * fp.FromInt(2));

            fp3 n = new fp3(-dhdx, fp.One, -dhdz);
            return fpMath.Normalize(n);
        }
    }
}
