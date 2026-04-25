using UnityEngine;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// One-component-per-zone-mesh marker. Importers (HoleGeoImporter / HoleLiteImporter)
    /// stamp this on every zone-mesh GameObject; <c>BakeZoneJsonTool</c> reads it to
    /// group meshes by surface type into <c>zones.json</c>. Load-bearing for the
    /// import → bake bridge — do not delete.
    ///
    /// Originally co-located with the legacy scene-raycast surface provider (removed
    /// in Phase F). Extracted to its own file so deleting the provider doesn't take
    /// the marker with it.
    /// </summary>
    public sealed class SurfaceMarker : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Fairway;
    }
}
