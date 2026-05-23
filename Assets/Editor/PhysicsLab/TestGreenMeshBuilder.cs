// TestGreenMeshBuilder.cs — Editor utility that generates a sculpted test green mesh.
//
// Menu: Window/Golfin/Build TestGreen Mesh
//
// Output: Assets/Meshes/TestGreen_25x25.asset
//
// The mesh covers 25m × 25m with sinusoidal undulation:
//   y = 0.30 * sin(x / 4.0) + 0.20 * cos(z / 3.0)
//
// Triangulated on a 0.25m XZ grid (100 vertices per axis = 10,000 vertices total,
// 99×99 = 9,801 quads = 19,602 triangles). The 0.25m grid is denser than the 0.5m
// bake cell size so the surface appears smooth and continuous, not faceted.
//
// Origin at world (0, 0, 0); centre of the green is at (12.5, 0, 12.5).
// The green polygon used by BakedZoneClassifier covers the full 25m × 25m footprint.
//
// SPEC: puttpath_predictor_perf_and_design §Architecture §§ Test green.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Golfin.Physics.Editor
{
    public static class TestGreenMeshBuilder
    {
        private const string OutputPath = "Assets/Meshes/TestGreen_25x25.asset";

        // Green dimensions and mesh resolution.
        private const float GreenSize     = 25.0f;   // metres per side
        private const float GridStep      = 0.25f;    // vertex spacing (denser than 0.5m bake)
        private const int   VertsPerSide  = 101;      // (25 / 0.25) + 1

        // Sinusoidal undulation parameters (SPEC §Architecture §§ Test green).
        private const float AmpSin = 0.30f;   // y += 0.30 * sin(x / 4.0)
        private const float AmpCos = 0.20f;   // y += 0.20 * cos(z / 3.0)

        [MenuItem("Window/Golfin/Build TestGreen Mesh")]
        public static void BuildAndSave()
        {
            Mesh mesh = Build();

            // Ensure output directory exists.
            string dir = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Remove existing asset so CreateAsset doesn't merge/append.
            if (File.Exists(OutputPath))
                AssetDatabase.DeleteAsset(OutputPath);

            AssetDatabase.CreateAsset(mesh, OutputPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TestGreenMeshBuilder] Saved {mesh.vertexCount} vertices to {OutputPath}");
        }

        /// <summary>
        /// Builds the test green mesh and returns it. Can be called from tests or
        /// from the scene setup script without touching AssetDatabase.
        /// </summary>
        public static Mesh Build()
        {
            int n = VertsPerSide;
            Vector3[] verts = new Vector3[n * n];
            Color[]   colors = new Color[n * n];
            int[]     tris  = new int[(n - 1) * (n - 1) * 6];

            // Build vertices.
            for (int iz = 0; iz < n; iz++)
            {
                for (int ix = 0; ix < n; ix++)
                {
                    float wx = ix * GridStep;
                    float wz = iz * GridStep;
                    float wy = Height(wx, wz);
                    verts[iz * n + ix] = new Vector3(wx, wy, wz);
                    // Plain white; PutterGreenReader overwrites vertex colors at bake time.
                    colors[iz * n + ix] = Color.white;
                }
            }

            // Build triangles (two triangles per quad).
            int ti = 0;
            for (int iz = 0; iz < n - 1; iz++)
            {
                for (int ix = 0; ix < n - 1; ix++)
                {
                    int a = iz       * n + ix;
                    int b = iz       * n + (ix + 1);
                    int c = (iz + 1) * n + ix;
                    int d = (iz + 1) * n + (ix + 1);

                    // Triangle 1: a-b-c
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    // Triangle 2: b-d-c
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = c;
                }
            }

            var mesh = new Mesh
            {
                name = "TestGreen_25x25"
            };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // > 65535 vertices
            mesh.vertices  = verts;
            mesh.colors    = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Sinusoidal undulation function per SPEC:
        ///   y = 0.30 * sin(x / 4.0) + 0.20 * cos(z / 3.0)
        /// </summary>
        public static float Height(float x, float z)
            => AmpSin * Mathf.Sin(x / 4.0f) + AmpCos * Mathf.Cos(z / 3.0f);

        /// <summary>
        /// Returns the output asset path (for reference by other scripts).
        /// </summary>
        public static string AssetPath => OutputPath;
    }
}
#endif
