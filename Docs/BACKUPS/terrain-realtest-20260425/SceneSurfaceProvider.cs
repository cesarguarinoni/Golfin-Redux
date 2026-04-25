using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// One-component-per-zone-mesh marker. Attach this to zone mesh GameObjects so
    /// SceneSurfaceProvider can classify them. All zone meshes that need explicit
    /// surface types must have this component; unmarked areas return Fairway.
    ///
    /// Greens and bunkers that carry legacy GreenSurfaceInfo / BunkerSurfaceInfo
    /// breadcrumbs (from Golfin.Course) are NOT automatically bridged here because
    /// Golfin.Course lives in Assembly-CSharp which Physics.Runtime cannot reference.
    /// Add SurfaceMarker(Green) and SurfaceMarker(Sand) to those zone roots when
    /// integrating Phase 4 with the live scene.
    /// </summary>
    public sealed class SurfaceMarker : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Fairway;
    }

    /// <summary>
    /// Surface classifier backed by scene geometry. Raycasts downward to find the top
    /// zone mesh collider at (x, z); reads SurfaceMarker. Returns Fairway if no marker.
    ///
    /// The raycast is non-deterministic but acceptable: surface classification is a
    /// static property of hole geometry — result is identical every call.
    /// </summary>
    public sealed class SceneSurfaceProvider : ISurfaceProvider
    {
        private const float RaycastFromY  = 500f;
        private const float RaycastLength = 1000f;
        private readonly int layerMask;

        public SceneSurfaceProvider(int layerMask = ~0) { this.layerMask = layerMask; }

        public SurfaceType Classify(fp worldX, fp worldZ)
        {
            var origin = new Vector3(worldX.ToFloat(), RaycastFromY, worldZ.ToFloat());
            if (!UnityEngine.Physics.Raycast(origin, Vector3.down, out var hit, RaycastLength,
                                             layerMask, QueryTriggerInteraction.Collide))
                return SurfaceType.Fairway;

            var marker = hit.collider.GetComponentInParent<SurfaceMarker>();
            if (marker != null) return marker.Type;

            // Fallback: if we hit a Terrain with no SurfaceMarker, check its alphamap
            // for an OB layer (identified by the layer asset name containing "OB").
            var terrain = hit.collider.GetComponent<Terrain>();
            if (terrain != null) return ClassifyTerrain(terrain, worldX.ToFloat(), worldZ.ToFloat());

            return SurfaceType.Fairway;
        }

        private static SurfaceType ClassifyTerrain(Terrain terrain, float worldX, float worldZ)
        {
            TerrainData td = terrain.terrainData;
            Vector3 tp = terrain.transform.position;
            int ax = Mathf.Clamp(Mathf.FloorToInt((worldX - tp.x) / td.size.x * td.alphamapWidth),  0, td.alphamapWidth  - 1);
            int az = Mathf.Clamp(Mathf.FloorToInt((worldZ - tp.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
            float[,,] maps = td.GetAlphamaps(ax, az, 1, 1);

            TerrainLayer[] layers = td.terrainLayers;
            for (int i = 0; i < layers.Length && i < maps.GetLength(2); i++)
            {
                if (layers[i] != null && layers[i].name.Contains("OB") && maps[0, 0, i] > 0.5f)
                    return SurfaceType.OOB;
            }
            return SurfaceType.Fairway;
        }
    }
}
