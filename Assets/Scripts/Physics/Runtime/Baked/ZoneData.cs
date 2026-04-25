using System;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics;

namespace Golfin.Physics.Runtime.Baked
{
    /// <summary>
    /// Serializable hole-zone data baked at edit-time and consumed at runtime
    /// by <see cref="BakedZoneClassifier"/> + <see cref="BakedHeightProvider"/>.
    ///
    /// Spec: Docs/Specs/Active/SIM_BAKED_DATA_PATH.md, Milestone 1.
    /// JSON written by BakeZoneJsonTool, loaded via JsonUtility.FromJson.
    ///
    /// Schema (matches the spec verbatim, adapted for JsonUtility quirks):
    ///
    ///   {
    ///     "holeId": "Hole_01",
    ///     "zones": [
    ///       {
    ///         "type": "Green",
    ///         "yOffsetFromTerrain": 0.11,
    ///         "polygons": [
    ///           { "points": [ {"x":1,"z":2}, {"x":3,"z":4}, ... ] },
    ///           ...
    ///         ]
    ///       },
    ///       ...
    ///     ]
    ///   }
    ///
    /// JsonUtility cannot serialize List&lt;List&lt;Vector2&gt;&gt; directly, so polygons
    /// are wrapped in a small struct that JsonUtility CAN serialize. Each polygon
    /// is an ordered ring of XZ points; the ring is implicitly closed (no duplicate
    /// last point required, but tolerated).
    /// </summary>
    [Serializable]
    public sealed class ZoneData
    {
        public string holeId;
        public List<ZonePolygonGroup> zones = new List<ZonePolygonGroup>();

        public static ZoneData FromJson(string json) => JsonUtility.FromJson<ZoneData>(json);
        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);
    }

    /// <summary>One zone-type entry: physics surface type + Y offset + N polygons.</summary>
    [Serializable]
    public sealed class ZonePolygonGroup
    {
        /// <summary>Stored as the SurfaceType enum NAME (e.g. "Green") for human-readable JSON.</summary>
        public string type;

        /// <summary>
        /// Constant Y offset added to the heightmap-sampled terrain Y at this zone.
        /// Mirrors the overlay-mesh offsets HoleGeoImporter applies; see
        /// Docs/DIAG/baked-pivot/M0-zone-offsets-inventory.md.
        /// </summary>
        public float yOffsetFromTerrain;

        public List<Polygon2D> polygons = new List<Polygon2D>();

        /// <summary>Convenience accessor: parse <see cref="type"/> as a SurfaceType.</summary>
        public SurfaceType SurfaceType
        {
            get
            {
                if (Enum.TryParse(type, out SurfaceType s)) return s;
                Debug.LogWarning($"[ZoneData] Unknown SurfaceType '{type}', defaulting to Fairway.");
                return SurfaceType.Fairway;
            }
        }
    }

    /// <summary>Single closed polygon as an ordered ring of XZ points.</summary>
    [Serializable]
    public sealed class Polygon2D
    {
        public List<Point2D> points = new List<Point2D>();
    }

    /// <summary>
    /// World-space point. <c>x</c> and <c>z</c> are the XZ-plane coords used
    /// for polygon point-in-polygon classification. <c>y</c> is the mesh
    /// vertex's actual world Y at that XZ — populated by BakeZoneJsonTool from
    /// the source MeshFilter (so Path A height interpolation reads the visible
    /// surface, not heightmap+offset). Synthetic tests can leave <c>y</c>=0.
    ///
    /// Name is "Point2D" for legacy reasons (the original Polygon2D was 2D-only);
    /// renaming would churn callers without semantic gain.
    /// </summary>
    [Serializable]
    public struct Point2D
    {
        public float x;
        public float y;
        public float z;
        public Point2D(float x, float z) { this.x = x; this.y = 0f; this.z = z; }
        public Point2D(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}
