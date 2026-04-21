using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Classifies a world position to a surface type. Runtime implementation
    /// reads zone-marker components on the hole scene; Core tests use a constant stub.
    /// </summary>
    public interface ISurfaceProvider
    {
        SurfaceType Classify(fp worldX, fp worldZ);
    }

    /// <summary>Stub provider used by Phase 1–3 tests and unit tests. Returns one surface everywhere.</summary>
    public sealed class ConstantSurfaceProvider : ISurfaceProvider
    {
        private readonly SurfaceType type;
        public ConstantSurfaceProvider(SurfaceType t) { type = t; }
        public SurfaceType Classify(fp worldX, fp worldZ) => type;
    }
}
