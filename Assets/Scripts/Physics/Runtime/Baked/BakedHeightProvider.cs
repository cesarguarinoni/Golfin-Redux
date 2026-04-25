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
            // Path A: prefer the matched polygon's interpolated mesh Y.
            // This bypasses the heightmap-vs-mesh depression-band conflict
            // documented in MILESTONE_2_DONE.md: zone meshes are built on
            // un-depressed terrain, while heightmap.bytes captures the
            // post-depression terrain. The mesh's own vertex Ys ARE the
            // visible surface, so IDW-interpolating them per polygon is
            // exact (and replaces the heightmap+offset approximation).
            if (classifier != null
                && classifier.TrySampleMeshY(worldX, worldZ, out _, out float meshY))
            {
                return fp.FromFloat(meshY);
            }

            // Fallback: outside any baked polygon. Use heightmap directly +
            // the default offset (which is 0 for OOB / Rough — i.e. terrain).
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
            // Path A: prefer mesh Y when the polygon containing (x, z) has it.
            // The 2-arg path already picks the highest-priority polygon's mesh Y,
            // which is the visible surface — no further preference adjustment
            // needed in baked architecture (the heightmap-overlap race that
            // motivated the legacy 3-arg simply doesn't exist here).
            if (classifier != null
                && classifier.TrySampleMeshY(worldX, worldZ, out _, out float meshY))
            {
                return fp.FromFloat(meshY);
            }

            fp terrainY = heightmap == null ? fp.Zero : heightmap.SampleHeight(worldX, worldZ);
            if (classifier == null) return terrainY;

            SurfaceType actual = classifier.Classify(worldX, worldZ);
            // Fallback to scalar-offset path if no mesh Y is available (synthetic
            // tests). Matches legacy 3-arg semantic: never go below the actually-
            // classified surface even when the caller insists on a preferred one.
            float oA = classifier.GetYOffset(actual);
            float oP = classifier.GetYOffset(preferred);
            float chosen = oP > oA ? oP : oA;
            return terrainY + fp.FromFloat(chosen);
        }
    }
}
