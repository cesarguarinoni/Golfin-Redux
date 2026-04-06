// JSON data classes for hole export package parsing
// Used by HoleImporter to deserialize hole-manifest.json, aerial-tiles.json, anchors.json

namespace Golfin.CourseImport
{
    [System.Serializable]
    public class HoleManifest
    {
        public string schema_version;
        public string pipeline;           // "uhole-lite" or null (UHole)
        public string course_id;
        public int hole_number;
        public int par;
        public int stroke_index;
        public int championship_yards;
        public ManifestBounds bounds;
        public ManifestOrigin origin;
        public ManifestTerrain terrain;
        public ManifestAerial aerial;
        public LiteTextureInfo texture;   // UHole Lite only — single texture PNG
        public string anchors_file;
        public int anchor_count;
        public ManifestTransform transform;
        public string review_status;
        public string exported_at;
    }

    [System.Serializable]
    public class LiteTextureInfo
    {
        public string file;
        public int width;
        public int height;
    }

    [System.Serializable]
    public class ManifestBounds
    {
        public double north, south, east, west;
    }

    [System.Serializable]
    public class ManifestOrigin
    {
        public double lat, lon;
        public string note;
    }

    [System.Serializable]
    public class ManifestTerrain
    {
        public string heightmap_file;
        public string format;
        public int resolution;
        public float min_elevation_m;
        public float max_elevation_m;
        public float terrain_width_m;
        public float terrain_length_m;
    }

    [System.Serializable]
    public class ManifestAerial
    {
        public string tiles_file;
        public int tile_count;
    }

    [System.Serializable]
    public class ManifestTransform
    {
        public float mean_residual_m;
        public float max_residual_m;
    }

    // aerial-tiles.json
    [System.Serializable]
    public class AerialTilesData
    {
        public TileBounds hole_bounds;
        public AerialTile[] tiles;
    }

    [System.Serializable]
    public class AerialTile
    {
        public string path;
        public int z, x, y;
        public TileBounds bounds;
    }

    [System.Serializable]
    public class TileBounds
    {
        public double north, south, east, west;
    }

    // anchors.json — root is an array, needs wrapper for JsonUtility
    [System.Serializable]
    public class AnchorArrayWrapper
    {
        public AnchorData[] items;
    }

    [System.Serializable]
    public class AnchorData
    {
        public string type;
        public string label;
        public AnchorPixel official_px;
        public AnchorWorld world;
        public AnchorLocal local;
    }

    [System.Serializable]
    public class AnchorPixel
    {
        public float x, y;
    }

    [System.Serializable]
    public class AnchorWorld
    {
        public double lat, lon;
    }

    [System.Serializable]
    public class AnchorLocal
    {
        public float x, z;
    }

    // zones.json — zone grid data for splatmap pipeline
    [System.Serializable]
    public class ZonesData
    {
        public int hole_number;
        public ZoneSourceDimensions source_dimensions;
        public string grid;
    }

    [System.Serializable]
    public class ZoneSourceDimensions
    {
        public int width;
        public int height;
    }
}
