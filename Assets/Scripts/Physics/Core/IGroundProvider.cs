using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Abstraction over the terrain heightmap. Phase 1 uses FlatGround(y=0).
    /// Phase 4 swaps in a provider backed by the Q16.16 heightmap.bytes file
    /// baked in Phase 0.
    /// </summary>
    public interface IGroundProvider
    {
        /// <summary>World Y of the ground surface at (worldX, worldZ), meters.</summary>
        fp SampleHeight(fp worldX, fp worldZ);

        /// <summary>
        /// Surface-aware overload: prefer a collider whose SurfaceMarker.Type matches
        /// <paramref name="preferred"/> when one exists. Falls back to max-Y if no
        /// preferred-type collider is found (same behaviour as 2-arg). Used by sim-time
        /// roll/putt ground sampling so a bunker ball stays in the bunker and a green
        /// ball stays on the green even when a higher collider overlaps at that XZ.
        /// Default implementation ignores preferred and delegates to 2-arg — correct for
        /// FlatGround and HeightmapData providers that have no overlapping colliders.
        /// </summary>
        fp SampleHeight(fp worldX, fp worldZ, SurfaceType preferred)
            => SampleHeight(worldX, worldZ);
    }

    public sealed class FlatGround : IGroundProvider
    {
        private readonly fp y;
        public FlatGround(fp y) { this.y = y; }
        public FlatGround() { this.y = fp.Zero; }
        public fp SampleHeight(fp worldX, fp worldZ) => y;
    }
}
