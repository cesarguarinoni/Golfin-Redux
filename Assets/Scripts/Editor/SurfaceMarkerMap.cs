using Golfin.Physics;
using UnityEngine;

/// <summary>
/// Single source of truth for Course.SurfaceType → Physics.SurfaceType mapping.
/// Used by PhysicsMarkerRepairTool and HoleGeoImporter / HoleLiteImporter.
/// </summary>
public static class SurfaceMarkerMap
{
    public static SurfaceType MapCourseToPhysics(int courseTypeInt)
    {
        switch (courseTypeInt)
        {
            case 0: return SurfaceType.Fairway;
            case 1: return SurfaceType.Green;
            case 2: return SurfaceType.Semirough;
            case 3: return SurfaceType.Rough;
            case 4: return SurfaceType.Sand;
            case 5: return SurfaceType.Water;
            case 6: return SurfaceType.Tee;
            case 7: return SurfaceType.CartPath;
            case 8: return SurfaceType.GreenCollar;
            default:
                Debug.LogWarning($"[SurfaceMarkerMap] Unknown Course.SurfaceType={courseTypeInt}, defaulting to Fairway.");
                return SurfaceType.Fairway;
        }
    }
}
