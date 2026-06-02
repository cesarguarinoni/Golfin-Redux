#if UNITY_EDITOR
// GreenSlopeGridOrbit.cs — editor-only helper for the green_orbit_videos task.
// Renders a full-green slope grid (same shader/material as PutterGreenReader) in the
// currently-open Hole_NN_Geo scene, covering the WHOLE green surface (not ball-centered).
// The grid GO is transient: it is created in memory and displayed for recording, but the
// scene is NEVER saved with it baked in (Generated scenes are build artifacts).
//
// Usage:
//   Menu: GOLFIN/Recording/Green Slope Grid (full green) — toggles grid on/off
//   Static: GreenSlopeGridOrbit.RenderFullGreenSlopeGrid() — creates and shows the grid
//   Static: GreenSlopeGridOrbit.RemoveFullGreenSlopeGrid() — destroys the transient grid GO
//
// NON-DESTRUCTIVE rules enforced:
//   - No calls to EditorSceneManager.SaveScene or SceneManager.SaveScene.
//   - The grid GO has hideFlags = DontSave so it will NOT be persisted if Unity auto-saves.
//   - PhysicsLab / PutterGreenReader is untouched (different scene path).

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Golfin.CourseImport.Recording
{
    public static class GreenSlopeGridOrbit
    {
        // ── Constants ──────────────────────────────────────────────────────────────

        private const string GridGoName    = "_GreenSlopeGridOrbit_Transient";
        private const string GridMeshName  = "_GreenSlopeGridOrbit_Mesh";

        private const float CellSize       = 0.5f;
        private const float SurfaceYOffset = 0.02f;   // 2 cm above surface (z-fight prevention)
        private const float VisibleRadius  = 9999f;   // effectively infinite — whole green visible
        private const float LineWidth      = 0.04f;
        private const float LineGlow       = 1.5f;

        // Material path (same material PutterGreenReader uses)
        private const string MaterialPath  = "Assets/Materials/PutterGreenGrid.mat";

        // Color ramp (same as PutterGreenReader)
        private static readonly Color ColorGreen  = new Color(0.30f, 1.00f, 0.15f, 1.0f);
        private static readonly Color ColorYellow = new Color(1.00f, 0.90f, 0.05f, 1.0f);
        private static readonly Color ColorRed    = new Color(1.00f, 0.20f, 0.05f, 1.0f);

        // Slope thresholds (match GreenSlopeConfig.csv defaults)
        private const float GreenThreshold  = 0.02f;
        private const float YellowThreshold = 0.05f;

        // ── Menu Items ────────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Recording/Green Slope Grid (full green)/Show Grid")]
        private static void MenuShowGrid()
        {
            bool ok = RenderFullGreenSlopeGrid();
            if (ok)
                Debug.Log("[GreenSlopeGridOrbit] Grid created. Call 'Remove Grid' or reopen scene to dispose.");
            else
                Debug.LogError("[GreenSlopeGridOrbit] Failed to create grid — see Console for details.");
        }

        [MenuItem("GOLFIN/Recording/Green Slope Grid (full green)/Remove Grid")]
        private static void MenuRemoveGrid() => RemoveFullGreenSlopeGrid();

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Build the full-green slope grid on the currently-open scene.
        /// The grid GO has DontSave flags — it will NOT be serialised to disk even if Unity
        /// auto-saves the scene. Returns true on success.
        /// </summary>
        public static bool RenderFullGreenSlopeGrid()
        {
            // Remove any existing transient grid first.
            RemoveFullGreenSlopeGrid();

            // Load the grid material.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                Debug.LogError($"[GreenSlopeGridOrbit] Material not found at '{MaterialPath}'.");
                return false;
            }

            // Find the green surface mesh in the open scene.
            (Mesh greenMesh, Transform greenTransform) = FindGreenMesh();
            if (greenMesh == null)
            {
                Debug.LogError("[GreenSlopeGridOrbit] No green mesh found in the open scene. " +
                    "Expected a GameObject named 'Green_*' (or containing 'green') under a 'Greens' parent " +
                    "with a MeshFilter and MeshRenderer.");
                return false;
            }

            Debug.Log($"[GreenSlopeGridOrbit] Found green mesh '{greenMesh.name}' " +
                      $"vertices={greenMesh.vertexCount} transform={greenTransform.name}");

            // Add a temporary MeshCollider for raycasting (removed after bake).
            bool addedCollider = false;
            MeshCollider tempCollider = greenTransform.GetComponent<MeshCollider>();
            if (tempCollider == null)
            {
                tempCollider = greenTransform.gameObject.AddComponent<MeshCollider>();
                tempCollider.sharedMesh = greenMesh;
                addedCollider = true;
            }

            // Bake slope cells by raycasting onto the green mesh surface.
            // Pass the temp collider so ONLY the green surface is ever tested — tree colliders
            // or any other scene geometry cannot corrupt the slope bake.
            List<SlopeCell> cells = BakeSlopeCells(greenTransform, tempCollider);

            // Remove temp collider if we added it.
            if (addedCollider && tempCollider != null)
            {
                Object.DestroyImmediate(tempCollider);
            }

            if (cells.Count == 0)
            {
                Debug.LogError("[GreenSlopeGridOrbit] Slope cell bake returned 0 cells — " +
                    "raycasts may have missed the green surface. Check the green mesh orientation.");
                return false;
            }

            Debug.Log($"[GreenSlopeGridOrbit] Baked {cells.Count} slope cells (cellSize={CellSize}m).");

            // Build the grid mesh.
            Mesh gridMesh = BuildGridMesh(cells);

            // Compute green center (XZ average of cell centers, Y from mesh surface).
            Vector3 greenCenter = ComputeGreenCenter(cells);

            // Create the transient GO with DontSave.
            var go = new GameObject(GridGoName);
            go.hideFlags = HideFlags.DontSave;  // NOT serialised to scene on disk

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = gridMesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Override material properties via MaterialPropertyBlock to force whole-green visibility.
            var mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            mpb.SetVector("_BallPosition", new Vector4(greenCenter.x, greenCenter.y, greenCenter.z, 0f));
            mpb.SetFloat("_VisibleRadius",  VisibleRadius);
            mpb.SetFloat("_CellSize",       CellSize);
            mpb.SetFloat("_LineWidth",      LineWidth);
            mpb.SetFloat("_LineGlow",       LineGlow);
            mr.SetPropertyBlock(mpb);

            Debug.Log($"[GreenSlopeGridOrbit] Grid GO '{GridGoName}' created with {gridMesh.vertexCount} vertices, " +
                      $"greenCenter={greenCenter}, _VisibleRadius={VisibleRadius}. " +
                      "Grid is transient (DontSave) — scene will NOT be mutated on disk.");
            return true;
        }

        /// <summary>Destroy the transient grid GO if it exists in the current scene.</summary>
        public static void RemoveFullGreenSlopeGrid()
        {
            var existing = Object.FindObjectsOfType<GameObject>()
                .FirstOrDefault(g => g.name == GridGoName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                Debug.Log("[GreenSlopeGridOrbit] Transient grid GO removed.");
            }
        }

        // ── Internal helpers ──────────────────────────────────────────────────────

        private struct SlopeCell
        {
            public float cx, cz;
            public float meshY;
            public float slopeX, slopeZ;
            public float magnitude;
            public int   colIdx, rowIdx;
        }

        private static (Mesh mesh, Transform t) FindGreenMesh()
        {
            // Strategy 1: find GreenSurfaceInfo component (most reliable).
            var gsi = Object.FindObjectsOfType<Golfin.Course.GreenSurfaceInfo>();
            if (gsi.Length > 0)
            {
                // Pick the first green with a valid MeshFilter.
                foreach (var g in gsi)
                {
                    var mf = g.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        return (mf.sharedMesh, g.transform);
                }
            }

            // Strategy 2: fallback to name-based search (same as HoleFlyoverRecorder).
            var renderers = Object.FindObjectsOfType<MeshRenderer>();
            foreach (var r in renderers)
            {
                string n = r.gameObject.name.ToLowerInvariant();
                if (!n.Contains("green") || n.Contains("greenside")) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    return (mf.sharedMesh, r.transform);
            }
            return (null, null);
        }

        private static List<SlopeCell> BakeSlopeCells(Transform greenTransform, MeshCollider greenCollider)
        {
            var cells = new List<SlopeCell>(2048);

            // Get world-space bounds of the green mesh.
            var mr = greenTransform.GetComponent<MeshRenderer>();
            if (mr == null) return cells;
            Bounds bounds = mr.bounds;

            float half     = CellSize * 0.5f;
            float originX  = Mathf.Floor(bounds.min.x / CellSize) * CellSize;
            float originZ  = Mathf.Floor(bounds.min.z / CellSize) * CellSize;
            float endX     = Mathf.Ceil(bounds.max.x / CellSize) * CellSize;
            float endZ     = Mathf.Ceil(bounds.max.z / CellSize) * CellSize;

            int cols = Mathf.RoundToInt((endX - originX) / CellSize) + 1;
            int rows = Mathf.RoundToInt((endZ - originZ) / CellSize) + 1;

            // Build a cell-index lookup for quad triangulation.
            var cellByGrid = new Dictionary<(int r, int c), int>(2048);

            float rayStartY = bounds.max.y + 10f;  // start ray above the green

            for (int ci = 0; ci < cols; ci++)
            {
                for (int ri = 0; ri < rows; ri++)
                {
                    float cx = originX + ci * CellSize;
                    float cz = originZ + ri * CellSize;

                    // Sample center Y.
                    float meshY;
                    if (!RaycastGreenY(greenCollider, cx, cz, rayStartY, out meshY)) continue;

                    // Sample 4 neighbours for finite-difference gradient.
                    float hL, hR, hF, hB;
                    bool okL = RaycastGreenY(greenCollider, cx - half, cz,        rayStartY, out hL);
                    bool okR = RaycastGreenY(greenCollider, cx + half, cz,        rayStartY, out hR);
                    bool okF = RaycastGreenY(greenCollider, cx,        cz - half, rayStartY, out hF);
                    bool okB = RaycastGreenY(greenCollider, cx,        cz + half, rayStartY, out hB);

                    if (!okL || !okR || !okF || !okB) continue;

                    float slopeX = (hR - hL) / CellSize;
                    float slopeZ = (hB - hF) / CellSize;
                    float mag    = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ);

                    int idx = cells.Count;
                    cellByGrid[(ri, ci)] = idx;
                    cells.Add(new SlopeCell
                    {
                        cx = cx, cz = cz, meshY = meshY,
                        slopeX = slopeX, slopeZ = slopeZ,
                        magnitude = mag,
                        colIdx = ci, rowIdx = ri
                    });
                }
            }

            // Store grid dimensions for mesh build.
            _lastGridCols = cols;
            _lastGridRows = rows;
            _lastCellByGrid = cellByGrid;
            return cells;
        }

        // Temporary grid dimension storage (used by BuildGridMesh).
        private static int _lastGridCols;
        private static int _lastGridRows;
        private static Dictionary<(int r, int c), int> _lastCellByGrid;

        /// <summary>
        /// Raycast against the green's own MeshCollider ONLY — not the global Physics scene.
        /// This prevents tree colliders (or any other scene collider) from corrupting the slope bake
        /// by intercepting a downward ray that was aimed at the green surface.
        /// </summary>
        private static bool RaycastGreenY(MeshCollider greenCollider, float worldX, float worldZ,
            float fromY, out float hitY)
        {
            hitY = 0f;
            var ray = new Ray(new Vector3(worldX, fromY, worldZ), Vector3.down);
            // Raycast the specific green MeshCollider — trees/anything else are NEVER tested.
            if (!greenCollider.Raycast(ray, out RaycastHit hit, fromY + 20f))
                return false;
            hitY = hit.point.y;
            return true;
        }

        private static Mesh BuildGridMesh(List<SlopeCell> cells)
        {
            var vertices = new Vector3[cells.Count];
            var colors32 = new Color32[cells.Count];

            for (int i = 0; i < cells.Count; i++)
            {
                SlopeCell c = cells[i];
                vertices[i] = new Vector3(c.cx, c.meshY + SurfaceYOffset, c.cz);
                Color col = CellColor(c.magnitude);
                colors32[i] = new Color32(
                    (byte)(col.r * 255), (byte)(col.g * 255),
                    (byte)(col.b * 255), (byte)(col.a * 255));
            }

            var tris = new List<int>(cells.Count * 6);
            for (int row = 0; row < _lastGridRows - 1; row++)
            {
                for (int col = 0; col < _lastGridCols - 1; col++)
                {
                    if (!_lastCellByGrid.TryGetValue((row,     col),     out int idxA)) continue;
                    if (!_lastCellByGrid.TryGetValue((row,     col + 1), out int idxB)) continue;
                    if (!_lastCellByGrid.TryGetValue((row + 1, col),     out int idxC)) continue;
                    if (!_lastCellByGrid.TryGetValue((row + 1, col + 1), out int idxD)) continue;

                    tris.Add(idxA); tris.Add(idxB); tris.Add(idxC);
                    tris.Add(idxB); tris.Add(idxD); tris.Add(idxC);
                }
            }

            var mesh = new Mesh { name = GridMeshName };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices    = vertices;
            mesh.colors32    = colors32;
            mesh.triangles   = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ComputeGreenCenter(List<SlopeCell> cells)
        {
            if (cells.Count == 0) return Vector3.zero;
            float sx = 0, sy = 0, sz = 0;
            foreach (var c in cells) { sx += c.cx; sy += c.meshY; sz += c.cz; }
            return new Vector3(sx / cells.Count, sy / cells.Count, sz / cells.Count);
        }

        private static Color CellColor(float mag)
        {
            if (mag < GreenThreshold)  return ColorGreen;
            if (mag < YellowThreshold) return ColorYellow;
            return ColorRed;
        }
    }
}
#endif
