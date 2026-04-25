using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime.Baked
{
    /// <summary>
    /// IGroundProvider that composes a baked heightmap (terrain Y) with a
    /// <see cref="BakedZoneClassifier"/> (zone Y offset) to produce the
    /// authoritative ground surface Y at (worldX, worldZ).
    ///
    /// SampleHeight(x, z) = heightmap.SampleHeight(x, z) + classifier.GetYOffset(typeAt(x,z))
    ///
    /// Spec: Docs/Specs/Active/SIM_BAKED_DATA_PATH.md, Milestone 2.
    /// Replaces <see cref="SceneGroundProvider"/> in the sim path post-M3.
    /// Editor-time helpers (placement dropdown ray-snap) keep using SceneGroundProvider
    /// until Phase F retires it.
    /// </summary>
    public sealed class BakedHeightProvider : IGroundProvider
    {
        private readonly HeightmapData         heightmap;
        private readonly BakedZoneClassifier   classifier;

        /// <summary>
        /// Construct from already-loaded heightmap + classifier. Use
        /// <see cref="HeightmapLoader.LoadFromBytes"/> for the heightmap and
        /// <c>new BakedZoneClassifier(ZoneData.FromJson(...))</c> for the classifier.
        /// </summary>
        public BakedHeightProvider(HeightmapData heightmap, BakedZoneClassifier classifier)
        {
            this.heightmap  = heightmap;
            this.classifier = classifier;
        }

        public fp SampleHeight(fp worldX, fp worldZ)
        {
            fp terrainY = heightmap == null ? fp.Zero : heightmap.SampleHeight(worldX, worldZ);
            if (classifier == null) return terrainY;

            SurfaceType type = classifier.Classify(worldX, worldZ);
            float offset     = classifier.GetYOffset(type);
            return terrainY + fp.FromFloat(offset);
        }

        /// <summary>
        /// Surface-preferred overload. Baked architecture: terrain + classified
        /// offset is already authoritative — the <paramref name="preferred"/>
        /// hint is informational only. We honour it when the classifier reports
        /// a different type at this XZ but the caller insists on a specific
        /// surface (e.g. roll-step on green that briefly samples adjacent collar);
        /// in that case we use the preferred-type's Y offset on top of terrain.
        /// </summary>
        public fp SampleHeight(fp worldX, fp worldZ, SurfaceType preferred)
        {
            fp terrainY = heightmap == null ? fp.Zero : heightmap.SampleHeight(worldX, worldZ);
            if (classifier == null) return terrainY;

            SurfaceType actual = classifier.Classify(worldX, worldZ);
            // Pick the higher of the two offsets — same intent as the legacy
            // SceneGroundProvider 3-arg behaviour ("prefer the surface the caller
            // is on, but never go BELOW the actually-classified surface").
            float oA = classifier.GetYOffset(actual);
            float oP = classifier.GetYOffset(preferred);
            float chosen = oP > oA ? oP : oA;
            return terrainY + fp.FromFloat(chosen);
        }
    }
}
