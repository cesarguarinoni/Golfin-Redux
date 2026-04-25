using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics;
using Golfin.Physics.Runtime;
using Golfin.Physics.Runtime.Baked;

namespace Golfin.Editor.CourseImporter
{
    /// <summary>
    /// Walks each Hole_XX_Geo scene's zone-mesh hierarchy, extracts the boundary
    /// contour of every zone mesh, projects to XZ, and writes a polygon-based
    /// <see cref="ZoneData"/> JSON at <c>Assets/Resources/HoleData/Hole_XX/zones.json</c>.
    ///
    /// Spec: Docs/Specs/Active/SIM_BAKED_DATA_PATH.md, Milestone 1, step 4.
    ///
    /// Y offsets per zone come from <see cref="HoleGeoImporter"/>'s overlay-mesh
    /// constants (documented in <c>Docs/DIAG/baked-pivot/M0-zone-offsets-inventory.md</c>).
    /// </summary>
    public static class BakeZoneJsonTool
    {
        // Per-zone Y offset above the BAKED HEIGHTMAP terrain. Mirrors mesh-build
        // offsets in HoleGeoImporter (M0-zone-offsets-inventory.md).
        //
        // KNOWN M2 ISSUE — depression band: HoleGeoImporter creates overlay meshes
        // (Fairway, CartPath, Tee) BEFORE depressing terrain under them
        // (HoleGeoImporter.cs ordering: meshes at lines 230/233/242; depression at
        // line 307). Effective visible mesh top is `un-depressed_terrain + meshOffset`
        // EVERYWHERE within the dilated mesh boundary.
        // The heightmap.bytes captures the POST-depression terrain, which is:
        //   - inside the depression mask (the original contour): un-depressed - 0.40
        //   - inside the dilated "ring" (between original and dilated boundary):
        //     un-depressed (depression mask doesn't extend here)
        //
        // With these mesh offsets, sim is correct in the ring (heightmap matches
        // un-depressed) but ~40 cm under the visible surface inside the depression.
        // Inflating the offset to 0.415 inverts the problem (correct inside, ring
        // off by +40 cm). The proper fix (M2-followup or M3 spec'd by Architect):
        // bake mesh-Y-per-polygon instead of relying on heightmap + scalar offset,
        // or have PhysicsHeightmapBaker bake from un-depressed terrain.
        // See Docs/DIAG/baked-pivot/M2-height-agreement.md for the divergence
        // histogram and proposed remediation paths.
        private static readonly Dictionary<SurfaceType, float> YOffsets = new Dictionary<SurfaceType, float>
        {
            { SurfaceType.Green,       0.11f  },  // 0.03 + GreenRaiseMeters(0.08); no separate terrain depression
            { SurfaceType.GreenCollar, 0.04f  },  // mean of smoothstep 0..0.08
            { SurfaceType.Sand,        0.02f  },  // bunker bowl integral to mesh build
            { SurfaceType.BunkerLip,   0.02f  },  // submesh of bunker; same offset
            { SurfaceType.Fairway,     0.015f },  // mesh offset only; depression-band
                                                   // divergence flagged for M2-followup
            { SurfaceType.Tee,         0.005f },  // mesh offset only; depression-band
                                                   // divergence (TeeDepression=0.05) flagged
            { SurfaceType.CartPath,    0.01f  },  // mesh offset only; depression-band
                                                   // divergence flagged for M2-followup
            { SurfaceType.Semirough,   0.00f  },
            { SurfaceType.Rough,       0.00f  },
            { SurfaceType.Water,       0.00f  },
            { SurfaceType.OOB,         0.00f  },
        };

        // Output path constants
        private const string ResourcesRoot = "Assets/Resources/HoleData";

        // ── Menu entries ──────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Tools/Bake Zone JSON (Active Hole)")]
        public static void BakeActive()
        {
            string holePath = FindActiveHoleScenePath();
            if (string.IsNullOrEmpty(holePath))
            {
                EditorUtility.DisplayDialog("Bake Zone JSON",
                    "No Hole_XX_Geo scene currently loaded. Open one additively first.", "OK");
                return;
            }
            BakeOne(holePath);
        }

        [MenuItem("GOLFIN/Tools/Bake Zone JSON (All Holes)")]
        public static void BakeAll()
        {
            var guids = AssetDatabase.FindAssets("t:Scene Hole_");
            int processed = 0, totalPolygons = 0;
            try
            {
                foreach (var g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    if (path.Contains("Video/")) continue;
                    if (!path.EndsWith("_Geo.unity")) continue;

                    EditorUtility.DisplayProgressBar("Bake Zone JSON",
                        $"Processing {Path.GetFileNameWithoutExtension(path)}...",
                        (float)processed / guids.Length);
                    int polys = BakeOne(path);
                    totalPolygons += polys;
                    processed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log($"[BakeZoneJsonTool] Processed {processed} hole(s); wrote {totalPolygons} polygons total.");
        }

        // ── Core ──────────────────────────────────────────────────────────────

        /// <summary>Returns the number of polygons written for this hole.</summary>
        public static int BakeOne(string holeScenePath)
        {
            string holeName = Path.GetFileNameWithoutExtension(holeScenePath); // "Hole_01_Geo"
            string holeId   = holeName.Replace("_Geo", "");                    // "Hole_01"

            // Open additively if not already loaded.
            Scene loaded = SceneManager.GetSceneByPath(holeScenePath);
            bool weOpened = false;
            if (!loaded.IsValid() || !loaded.isLoaded)
            {
                loaded = EditorSceneManager.OpenScene(holeScenePath, OpenSceneMode.Additive);
                if (!loaded.IsValid())
                {
                    Debug.LogError($"[BakeZoneJsonTool] Failed to open {holeScenePath}");
                    return 0;
                }
                weOpened = true;
            }

            // Group polygons by SurfaceType across all GOs in the scene.
            var groups = new Dictionary<SurfaceType, ZonePolygonGroup>();

            foreach (var root in loaded.GetRootGameObjects())
                CollectPolygons(root.transform, groups);

            // Build ZoneData
            var data = new ZoneData { holeId = holeId };
            foreach (var kv in groups) data.zones.Add(kv.Value);

            // Bake the OB mask from the scene's Terrain alphamap (if present).
            data.obMask = BakeObMask(loaded);

            // Sort by enum value for stable JSON.
            data.zones.Sort((a, b) =>
                ((int)a.SurfaceType).CompareTo((int)b.SurfaceType));

            // Write JSON.
            string outDir = Path.Combine(ResourcesRoot, holeId);
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, "zones.json");
            File.WriteAllText(outPath, data.ToJson(pretty: true));
            AssetDatabase.ImportAsset(outPath);

            int totalPolys = 0;
            foreach (var z in data.zones) totalPolys += z.polygons.Count;
            Debug.Log($"[BakeZoneJsonTool] {holeId}: {data.zones.Count} zone groups, "
                    + $"{totalPolys} polygons → {outPath}");

            if (weOpened)
                EditorSceneManager.CloseScene(loaded, removeScene: true);

            return totalPolys;
        }

        // ── Polygon extraction ────────────────────────────────────────────────

        /// <summary>
        /// Recursively walk the transform tree. For every GO carrying a
        /// <see cref="SurfaceMarker"/> (Physics) AND a <see cref="MeshFilter"/>,
        /// extract its mesh-boundary polygon(s) into the per-type group AND
        /// pool all its mesh vertices into the group's meshSamples list (Path A
        /// enrichment for IDW height interpolation).
        /// </summary>
        private static void CollectPolygons(Transform t, Dictionary<SurfaceType, ZonePolygonGroup> groups)
        {
            var marker = t.GetComponent<SurfaceMarker>();
            var mf     = t.GetComponent<MeshFilter>();
            if (marker != null && mf != null && mf.sharedMesh != null)
            {
                SurfaceType type = marker.Type;
                if (!groups.TryGetValue(type, out var grp))
                {
                    grp = new ZonePolygonGroup
                    {
                        type = type.ToString(),
                        yOffsetFromTerrain = YOffsets.TryGetValue(type, out float y) ? y : 0f,
                        polygons = new List<Polygon2D>(),
                        mesh = new ZoneMesh(),
                    };
                    groups[type] = grp;
                }
                ExtractBoundaryPolygons(mf, grp.polygons);
                AddMeshTriangles(mf, grp.mesh);
            }
            for (int i = 0; i < t.childCount; i++)
                CollectPolygons(t.GetChild(i), groups);
        }

        /// <summary>
        /// Path β: append all triangles of <paramref name="mf"/>'s mesh into
        /// the per-zone <see cref="ZoneMesh"/>, rebasing indices so multiple
        /// MeshFilters of the same surface type stay in one flat list.
        /// </summary>
        private static void AddMeshTriangles(MeshFilter mf, ZoneMesh outMesh)
        {
            var verts = mf.sharedMesh.vertices;
            var tris  = mf.sharedMesh.triangles;
            var tx    = mf.transform;

            int baseIdx = outMesh.vertices.Count;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = tx.TransformPoint(verts[i]);
                outMesh.vertices.Add(new Point2D(w.x, w.y, w.z));
            }
            for (int i = 0; i < tris.Length; i++)
                outMesh.indices.Add(baseIdx + tris[i]);
        }

        /// <summary>
        /// Extract the mesh's boundary contour as one or more closed XZ polygons.
        /// Boundary edges = edges used by exactly one triangle. Chained into loops.
        /// Vertices transformed to world space; Y dropped.
        /// </summary>
        private static void ExtractBoundaryPolygons(MeshFilter mf, List<Polygon2D> outPolys)
        {
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var tris  = mesh.triangles;
            if (tris.Length < 3) return;

            // 1. Count usage of each unordered edge.
            var edgeCount  = new Dictionary<long, int>();
            // 2. Track the oriented edge for boundary chaining.
            var nextOf     = new Dictionary<int, int>(); // a -> b for boundary edges

            for (int i = 0; i < tris.Length; i += 3)
            {
                CountEdge(edgeCount, tris[i],     tris[i+1]);
                CountEdge(edgeCount, tris[i+1],   tris[i+2]);
                CountEdge(edgeCount, tris[i+2],   tris[i]);
            }

            // 3. Identify oriented boundary edges: scan triangles again, keep
            //    the (a -> b) where the unordered key has count == 1.
            for (int i = 0; i < tris.Length; i += 3)
            {
                MaybeBoundary(edgeCount, nextOf, tris[i],   tris[i+1]);
                MaybeBoundary(edgeCount, nextOf, tris[i+1], tris[i+2]);
                MaybeBoundary(edgeCount, nextOf, tris[i+2], tris[i]);
            }

            // 4. Chain into closed loops.
            var visited = new HashSet<int>();
            foreach (var seed in nextOf.Keys)
            {
                if (visited.Contains(seed)) continue;

                var loopVerts = new List<int>();
                int current = seed;
                int safety  = nextOf.Count + 4;
                while (safety-- > 0)
                {
                    if (visited.Contains(current)) break;
                    visited.Add(current);
                    loopVerts.Add(current);
                    if (!nextOf.TryGetValue(current, out int next)) break;
                    if (next == seed) { loopVerts.Add(next); break; }
                    current = next;
                }

                if (loopVerts.Count < 3) continue;

                // Drop the duplicated closing vertex if present.
                if (loopVerts.Count >= 2 && loopVerts[0] == loopVerts[loopVerts.Count - 1])
                    loopVerts.RemoveAt(loopVerts.Count - 1);

                if (loopVerts.Count < 3) continue;

                var poly = new Polygon2D();
                for (int i = 0; i < loopVerts.Count; i++)
                {
                    Vector3 w = mf.transform.TransformPoint(verts[loopVerts[i]]);
                    // Path A: store the actual mesh-vertex world Y so
                    // BakedZoneClassifier.TrySampleMeshY can IDW-interpolate
                    // across the polygon boundary without consulting the
                    // (possibly-depressed) heightmap.
                    poly.points.Add(new Point2D(w.x, w.y, w.z));
                }
                outPolys.Add(poly);
            }
        }

        // ── Edge helpers ──────────────────────────────────────────────────────

        private static long EdgeKey(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }

        private static void CountEdge(Dictionary<long, int> dict, int a, int b)
        {
            long k = EdgeKey(a, b);
            dict.TryGetValue(k, out int v);
            dict[k] = v + 1;
        }

        private static void MaybeBoundary(Dictionary<long, int> count,
                                          Dictionary<int, int> nextOf, int a, int b)
        {
            if (count[EdgeKey(a, b)] == 1) nextOf[a] = b;
        }

        // ── OB mask extraction ────────────────────────────────────────────────

        /// <summary>
        /// Walk the loaded scene for a Terrain whose terrainData has at least
        /// one TerrainLayer named *OB*. Read the alphamap, threshold OB-layer
        /// weight at &gt;0.5 (matching <c>SceneSurfaceProvider.ClassifyTerrain</c>),
        /// pack the resulting binary mask into a base64 string, and return an
        /// <see cref="ObMask"/> describing world-space placement. Returns null
        /// if no such Terrain exists.
        /// </summary>
        private static ObMask BakeObMask(Scene scene)
        {
            Terrain terrain = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                terrain = root.GetComponentInChildren<Terrain>();
                if (terrain != null) break;
            }
            if (terrain == null || terrain.terrainData == null) return null;

            var td     = terrain.terrainData;
            var layers = td.terrainLayers;
            int obLayer = -1;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i] != null && layers[i].name.Contains("OB"))
                { obLayer = i; break; }
            if (obLayer < 0)
            {
                Debug.Log("[BakeZoneJsonTool] No OB-named terrain layer; skipping OB mask.");
                return null;
            }

            int aw = td.alphamapWidth;
            int ah = td.alphamapHeight;
            float[,,] maps = td.GetAlphamaps(0, 0, aw, ah);
            int layerCount = maps.GetLength(2);
            if (obLayer >= layerCount)
            {
                Debug.LogWarning("[BakeZoneJsonTool] OB layer index out of range; skipping.");
                return null;
            }

            // Pack bits — bit (z * aw + x) → maps[z, x, obLayer] > 0.5.
            int totalBits  = aw * ah;
            int totalBytes = (totalBits + 7) >> 3;
            byte[] bits = new byte[totalBytes];
            int obCount = 0;
            for (int z = 0; z < ah; z++)
                for (int x = 0; x < aw; x++)
                {
                    if (maps[z, x, obLayer] > 0.5f)
                    {
                        int idx = z * aw + x;
                        bits[idx >> 3] |= (byte)(1 << (idx & 7));
                        obCount++;
                    }
                }

            Vector3 tp = terrain.transform.position;
            var mask = new ObMask
            {
                width        = aw,
                height       = ah,
                worldOriginX = tp.x,
                worldOriginZ = tp.z,
                worldSizeX   = td.size.x,
                worldSizeZ   = td.size.z,
                maskBase64   = System.Convert.ToBase64String(bits),
            };
            Debug.Log($"[BakeZoneJsonTool] OB mask: {aw}×{ah} @ "
                    + $"({tp.x:F1},{tp.z:F1}) size {td.size.x:F1}×{td.size.z:F1}; "
                    + $"{obCount}/{totalBits} = {(100.0 * obCount / totalBits):F1}% OB.");
            return mask;
        }

        // ── Active-hole detector ──────────────────────────────────────────────

        private static string FindActiveHoleScenePath()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (s.name.StartsWith("Hole_") && s.name.EndsWith("_Geo")) return s.path;
            }
            return null;
        }
    }
}
