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
    }

    public sealed class FlatGround : IGroundProvider
    {
        private readonly fp y;
        public FlatGround(fp y) { this.y = y; }
        public FlatGround() { this.y = fp.Zero; }
        public fp SampleHeight(fp worldX, fp worldZ) => y;
    }
}
