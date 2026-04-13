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
        public string bunkers_file;
        public string greens_file;
        public int anchor_count;
        public ManifestTransform transform;
        public string tree_zones_file;
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

    // bunkers.json
    [System.Serializable]
    public class BunkersFileData
    {
        public string schema_version;
        public int hole_number;
        public int bunker_count;
        public float depth_m;
        public BunkerData[] bunkers;
    }

    [System.Serializable]
    public class BunkerContourVertex
    {
        public float x;
        public float z;
    }

    [System.Serializable]
    public class BunkerData
    {
        public int id;
        public int pixel_count;
        public BunkerContourVertex[] contour;  // V2: ordered polygon vertices
        public AnchorLocal center_local;       // reuse existing class — has x, z
        public BunkerSize size_m;
        public BunkerNormCenter center_normalized;
        public BunkerNormSize size_normalized;
    }

    [System.Serializable]
    public class BunkerSize
    {
        public float x;
        public float z;
    }

    [System.Serializable]
    public class BunkerNormCenter
    {
        public float x;
        public float y;
    }

    [System.Serializable]
    public class BunkerNormSize
    {
        public float w;
        public float h;
    }

    // greens.json — reuses BunkerData since green regions have the same contour format
    [System.Serializable]
    public class GreensFileData
    {
        public string schema_version;
        public int hole_number;
        public int green_count;
        public float height_m;
        public BunkerData[] greens;
    }

    // water.json — contour mesh data (schema v3.0.0)
    [System.Serializable]
    public class WaterFileData
    {
        public string schema_version;
        public int hole_number;
        public int water_count;
        public WaterRegionData[] water;
    }

    [System.Serializable]
    public class WaterRegionData
    {
        public int id;
        public int pixel_count;
        public ContourPoint[] contour;
        public AnchorLocal center_local;
        public BunkerSize size_m;
    }

    // fairway-contours.json
    [System.Serializable]
    public class FairwayContoursFile
    {
        public int fairway_count;
        public ZoneContourRegion[] fairways;
    }

    // zone-contours.json
    [System.Serializable]
    public class ZoneContoursFile
    {
        public ZoneContoursZones zones;
    }

    [System.Serializable]
    public class ZoneContoursZones
    {
        public ZoneContourRegion[] tee;
        public ZoneContourRegion[] semi_rough;
        public ZoneContourRegion[] cart_path;
    }

    [System.Serializable]
    public class ZoneContourRegion
    {
        public int id;
        public int pixel_count;
        public ContourPoint[] contour;
        public AnchorLocal center_local;
    }

    [System.Serializable]
    public class ContourPoint
    {
        public float x;
        public float z;
    }

    // cart-paths.json — cart path contour mesh data
    [System.Serializable]
    public class CartPathsFile
    {
        public string schema_version;
        public int hole_number;
        public int cart_path_count;
        public float min_width_m;
        public CartPathRegionData[] cart_paths;
    }

    [System.Serializable]
    public class CartPathRegionData
    {
        public int id;
        public int pixel_count;
        public ContourPoint[] contour;
        public ContourPoint[] spine;
        public float width_m;
        public AnchorLocal center_local;
        public BunkerSize size_m;
        public bool dilated;
        public ContourPoint[] junctions; // Where branches meet — for fill patches
    }

    // tree-zones.json — tree placement mask + regions
    [System.Serializable]
    public class TreeZonesFile
    {
        public string schema_version;
        public int hole_number;
        public int mask_width;
        public int mask_height;
        public string mask_base64;
        public TreeMPP meters_per_pixel;
        public int tree_region_count;
        public TreeRegionData[] tree_regions;
    }

    [System.Serializable]
    public class TreeMPP
    {
        public float x;
        public float z;
    }

    [System.Serializable]
    public class TreeRegionData
    {
        public int id;
        public int pixel_count;
        public float area_m2;
        public ContourPoint[] contour;
        public AnchorLocal center_local;
        public BunkerSize size_m;
    }

    // zones.json — zone grid data for splatmap pipeline
    [System.Serializable]
    public class ZonesData
    {
        public int hole_number;
        public ZoneSourceDimensions source_dimensions;
        public string grid;            // merged grid (backward compat)
        public string terrain_grid;    // terrain only (no overlays)
        public string trees_mask;      // binary mask: 1 = tree
        public string ob_mask;         // binary mask: 1 = OB
        public string cart_path_mask;  // binary mask: 1 = cart path
    }

    [System.Serializable]
    public class ZoneSourceDimensions
    {
        public int width;
        public int height;
    }
}
