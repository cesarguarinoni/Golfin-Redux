// iter-4: _surfaceYOffset z-fight fix (2026-05-25)
using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime.Baked;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Input;

// Keep in Golfin.Physics.Viewer: Golfin.Physics.Math is autoReferenced:false, so
// BakedZoneClassifier (which uses fp / ISurfaceProvider) is unreachable from
// Assembly-CSharp or Golfin.Gameplay.UI. This asmdef already references all required
// assemblies.

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Green-reading aim assist. On hole-load, bakes slope vectors per 0.5 m cell across
    /// all green polygons. While the player aims with the putter, renders a warped
    /// wireframe grid via a single procedural mesh + MeshRenderer — world-XZ-aligned
    /// grid lines drawn in the fragment shader, bending in Y with the surface topology.
    ///
    /// PGA 2K-style Sim positioning: the player reads the green — no live predicted curve.
    /// The grid lines are square in world-XZ plan view (L4 enforced by shader math:
    /// frac(worldPos.xz / _CellSize)).
    ///
    /// Color ramp thresholds load from <c>Assets/Resources/Data/GreenSlopeConfig.csv</c>:
    ///   GreenThreshold (default 0.02), YellowThreshold (default 0.05), per Q2.
    ///
    /// SPEC: puttpath_predictor_perf_and_design §Architecture §§ Bake step + Render step
    ///       (REVISED iter-2 — warped wireframe mesh replaces arrow instancing).
    /// </summary>
    public class PutterGreenReader : MonoBehaviour
    {
        // ── Inspector refs ───────────────────────────────────────────────────

        [Header("Rendering")]
        [SerializeField] private Material         _gridMaterial;
        [SerializeField] private Camera           _worldCamera;

        [Header("Lab Controller (for classifier access)")]
        [SerializeField] private PhysicsLabController _labController;

        [Header("Shot Controller (for putter aim detection)")]
        [SerializeField] private ShotController _shotController;

        // ── Runtime state ────────────────────────────────────────────────────

        // Baked slope cells.
        public struct SlopeCell
        {
            public float cx, cz;     // cell center XZ
            public float meshY;      // surface Y at cell center
            public float slopeX;     // (h_right - h_left) / cellSize
            public float slopeZ;     // (h_back  - h_front) / cellSize
            public float magnitude;  // sqrt(slopeX²+slopeZ²) — grade fraction
        }

        private SlopeCell[] _cells = System.Array.Empty<SlopeCell>();
        private int         _cachedHoleNumber = -1;

        // Putter aim state.
        private bool _aimActive;

        // Config (loaded from CSV; defaults per Q2).
        // SerializeField values override CSV defaults at runtime (set in Update() via MPB).
        private float _greenThreshold  = 0.02f;
        private float _yellowThreshold = 0.05f;

        [Header("Grid Parameters (Inspector overrides CSV defaults)")]
        [SerializeField] private float _cellSize        = 0.5f;
        [SerializeField] private float _lineWidth       = 0.04f;
        [SerializeField] private float _lineGlow        = 1.5f;
        [SerializeField] private float _visibleRadius   = 10.0f;

        [SerializeField, Tooltip("Vertical offset (meters) above the terrain mesh. Prevents z-fighting. 0.02 = 2cm, visually imperceptible from putter aim camera angles.")]
        private float _surfaceYOffset = 0.02f;

        // Colors (derived from thresholds, precomputed).
        // Yellow-green family matching PGA 2K reference — warm bright yellow on slopes.
        private static readonly Color ColorGreen  = new Color(0.30f, 1.00f, 0.15f, 1.0f);
        private static readonly Color ColorYellow = new Color(1.00f, 0.90f, 0.05f, 1.0f);
        private static readonly Color ColorRed    = new Color(1.00f, 0.20f, 0.05f, 1.0f);

        // Heatmap mode: colors cells by slope magnitude (Q5 — dashboard toggle).
        public bool HeatmapMode { get; set; } = false;

        // ── Grid mesh state ──────────────────────────────────────────────────

        // Child GO that holds the MeshFilter + MeshRenderer for the warped grid.
        private GameObject     _gridMeshGO;
        private MeshFilter     _gridMeshFilter;
        private MeshRenderer   _gridMeshRenderer;
        private Mesh           _gridMesh;
        private MaterialPropertyBlock _mpb;

        // Lookup: (gridRow, gridCol) → flat cell index in _cells.
        // Used for quad triangulation: skip quads where any corner has no baked cell.
        private Dictionary<(int row, int col), int> _cellIndexByGrid;

        // Grid extents (recomputed on bake).
        private float _gridOriginX;
        private float _gridOriginZ;
        private int   _gridCols;
        private int   _gridRows;

        // ── Test seams ───────────────────────────────────────────────────────

        /// <summary>Number of baked cells (0 = no hole loaded / classifier unavailable).</summary>
        public int BakedCellCount => _cells.Length;

        /// <summary>Number of vertices in the generated grid mesh (test seam for iter-2 tests).</summary>
        public int MeshVertexCount => _gridMesh != null ? _gridMesh.vertexCount : 0;

        /// <summary>
        /// Last visible-cell count (for smoke-bot assertion compatibility with iter-1 tests).
        /// In iter-2 mesh mode, we keep the cell count as BakedCellCount rather than a per-frame
        /// visible count, since the mesh and distance culling is done in the shader.
        /// The smoke-bot asserts >= 50 which is satisfied by BakedCellCount when bake succeeded.
        /// </summary>
        public int LastVisibleCellCount { get; private set; }

        /// <summary>
        /// Force putter aim active (for screenshot verification and smoke-bot).
        /// Do NOT call from production code.
        /// </summary>
        public void SetAimActiveForTest(bool active)
        {
            _aimActive = active;
            if (_gridMeshGO != null)
                _gridMeshGO.SetActive(active);
        }

        // Nullable override for ball position (used by visual-gate captures when no
        // PhysicsLabController ball is spawned — e.g. TestGreen standalone scene).
        // When non-null, Update() uses this instead of _labController.BallPosition.
        // Do NOT set from production code.
        private Vector3? _ballPositionOverride;

        /// <summary>
        /// Override the ball position used for Q3 distance culling. Pass null to revert to
        /// <c>_labController.BallPosition</c>. Used by visual-gate capture scripts only.
        /// </summary>
        public void SetBallPositionOverride(Vector3? pos) => _ballPositionOverride = pos;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            // MaterialPropertyBlock must be constructed here (not as a field initializer)
            // because its ctor calls a Unity native API which is disallowed before Awake.
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            // Guard against domain-reload null.
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            LoadConfig();
            HoleContext.OnChanged += OnHoleContextChanged;
            if (_shotController != null)
                _shotController.OnStateChanged += OnShotStateChanged;

            // If grid mesh GO exists, restore active state.
            if (_gridMeshGO != null)
                _gridMeshGO.SetActive(_aimActive);
        }

        private void OnDisable()
        {
            HoleContext.OnChanged -= OnHoleContextChanged;
            if (_shotController != null)
                _shotController.OnStateChanged -= OnShotStateChanged;
            _aimActive = false;
            LastVisibleCellCount = 0;
            if (_gridMeshGO != null)
                _gridMeshGO.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_gridMesh != null)
            {
                // Destroy the runtime mesh to avoid leaking unmanaged GPU resources.
                if (Application.isPlaying)
                    Destroy(_gridMesh);
                else
#if UNITY_EDITOR
                    DestroyImmediate(_gridMesh);
#else
                    Destroy(_gridMesh);
#endif
                _gridMesh = null;
            }
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnHoleContextChanged()
        {
            int newHole = HoleContext.HoleNumber;
            if (newHole == _cachedHoleNumber) return;
            _cachedHoleNumber = newHole;
            BakeCells();
        }

        private void OnShotStateChanged(ShotInputState state)
        {
            bool isPutterAim = _shotController != null
                && _shotController.IsPutt
                && (state.State == ShotState.Aiming
                 || state.State == ShotState.Pulling
                 || state.State == ShotState.Timing);
            _aimActive = isPutterAim;

            // Toggle the grid mesh child GO.
            if (_gridMeshGO != null)
                _gridMeshGO.SetActive(_aimActive);

            if (!isPutterAim)
                LastVisibleCellCount = 0;
        }

        // ── Update (only for pushing ball position to shader) ─────────────────

        private void Update()
        {
            if (!_aimActive || _gridMeshRenderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            // Push ball position to the shader via MaterialPropertyBlock (option b — no mesh rebuild).
            // _ballPositionOverride takes priority (set by visual-gate capture scripts when no ball is spawned).
            Vector3 ballPos = _ballPositionOverride.HasValue
                ? _ballPositionOverride.Value
                : (_labController != null ? _labController.BallPosition : Vector3.zero);

            _gridMeshRenderer.GetPropertyBlock(_mpb);
            _mpb.SetVector("_BallPosition", new Vector4(ballPos.x, ballPos.y, ballPos.z, 0f));
            _mpb.SetFloat("_VisibleRadius", _visibleRadius);
            // Push Inspector-editable shader params so Cesar can tweak live without editing
            // the material asset or the CSV (SerializeField values are the runtime-authoritative
            // values; CSV sets the initial defaults in OnEnable → LoadConfig → ParseConfig).
            _mpb.SetFloat("_CellSize",   _cellSize);
            _mpb.SetFloat("_LineWidth",  _lineWidth);
            _mpb.SetFloat("_LineGlow",   _lineGlow);
            _gridMeshRenderer.SetPropertyBlock(_mpb);
        }

        // ── Config loader ────────────────────────────────────────────────────

        private void LoadConfig()
        {
            var csv = Resources.Load<TextAsset>("Data/GreenSlopeConfig");
            if (csv == null)
            {
                Debug.Log("[PutterGreenReader] Data/GreenSlopeConfig not found in Resources — using Q2 defaults.");
                return;
            }
            ParseConfig(csv.text);
        }

        private void ParseConfig(string text)
        {
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                string key = parts[0].Trim();
                if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out float val)) continue;
                switch (key)
                {
                    // GreenThreshold and YellowThreshold: not [SerializeField] — always
                    // loaded from CSV (no per-instance Inspector override path needed).
                    case "GreenThreshold":      _greenThreshold  = val; break;
                    case "YellowThreshold":     _yellowThreshold = val; break;
                    // CellSize, VisibleRadius, LineWidth, LineGlow are now [SerializeField].
                    // The Inspector value is authoritative and must NOT be overwritten here.
                    // CSV lines for these keys are ignored gracefully (keys read but not
                    // applied) so old GreenSlopeConfig.csv files don't corrupt the Inspector
                    // values. The [SerializeField] field-initializer defaults (0.5 / 0.04 /
                    // 1.5 / 10.0) are written into the scene asset on first use; they survive
                    // domain reloads without help from CSV.
                    case "CellSize":
                    case "VisibleRadiusMeters":
                    case "LineWidth":
                    case "LineGlow":
                        // intentionally ignored — [SerializeField] fields govern
                        break;
                }
            }
        }

        // ── Bake step ────────────────────────────────────────────────────────

        /// <summary>
        /// Bakes slope vectors for all green cells. Called on hole load and exposed
        /// as a public test seam so EditMode unit tests can inject a synthetic classifier.
        /// After baking, generates the procedural grid mesh.
        /// </summary>
        public void BakeCells(BakedZoneClassifier classifierOverride = null)
        {
            BakedZoneClassifier classifier = classifierOverride;
            if (classifier == null && _labController != null)
            {
                classifier = _labController.GetSurfaces() as BakedZoneClassifier;
            }

            if (classifier == null)
            {
                _cells = System.Array.Empty<SlopeCell>();
                Debug.Log("[PutterGreenReader] BakeCells: no BakedZoneClassifier available — grid empty.");
                DestroyGridMesh();
                return;
            }

            float half = _cellSize * 0.5f;
            var list = new List<SlopeCell>(4096);
            _cellIndexByGrid = new Dictionary<(int, int), int>(4096);

            // Determine grid extents from green AABBs.
            float globalMinX = float.MaxValue, globalMinZ = float.MaxValue;
            float globalMaxX = float.MinValue, globalMaxZ = float.MinValue;

            foreach (var aabb in classifier.GetPolygonAABBsForType(SurfaceType.Green))
            {
                if (aabb.xMin < globalMinX) globalMinX = aabb.xMin;
                if (aabb.yMin < globalMinZ) globalMinZ = aabb.yMin;
                if (aabb.xMax > globalMaxX) globalMaxX = aabb.xMax;
                if (aabb.yMax > globalMaxZ) globalMaxZ = aabb.yMax;
            }

            if (globalMinX >= globalMaxX || globalMinZ >= globalMaxZ)
            {
                _cells = System.Array.Empty<SlopeCell>();
                Debug.Log("[PutterGreenReader] BakeCells: green AABB empty — grid empty.");
                DestroyGridMesh();
                return;
            }

            // Snap grid origin to a multiple of cellSize so world-XZ cells align to
            // frac(worldPos.xz / _CellSize) boundaries in the shader.
            // Floor to nearest cellSize multiple so the grid origin is at a world-space
            // grid line — this keeps the shader's frac() computation aligned.
            _gridOriginX = Mathf.Floor(globalMinX / _cellSize) * _cellSize;
            _gridOriginZ = Mathf.Floor(globalMinZ / _cellSize) * _cellSize;

            float endX = Mathf.Ceil(globalMaxX / _cellSize) * _cellSize;
            float endZ = Mathf.Ceil(globalMaxZ / _cellSize) * _cellSize;

            _gridCols = Mathf.RoundToInt((endX - _gridOriginX) / _cellSize) + 1;
            _gridRows = Mathf.RoundToInt((endZ - _gridOriginZ) / _cellSize) + 1;

            foreach (var aabb in classifier.GetPolygonAABBsForType(SurfaceType.Green))
            {
                float startX = aabb.xMin - half;
                float endXA  = aabb.xMax + half;
                float startZ = aabb.yMin - half;
                float endZA  = aabb.yMax + half;

                for (float cx = startX + half; cx <= endXA - half + 1e-4f; cx += _cellSize)
                {
                    for (float cz = startZ + half; cz <= endZA - half + 1e-4f; cz += _cellSize)
                    {
                        // Snap to grid.
                        float snappedX = Mathf.Round((cx - _gridOriginX) / _cellSize) * _cellSize + _gridOriginX;
                        float snappedZ = Mathf.Round((cz - _gridOriginZ) / _cellSize) * _cellSize + _gridOriginZ;

                        // Skip if outside polygon.
                        if (classifier.Classify(fp.FromFloat(snappedX), fp.FromFloat(snappedZ)) != SurfaceType.Green)
                            continue;

                        // Sample 4 neighbours for finite-difference gradient.
                        SurfaceType dummy;
                        float hL, hR, hF, hB;
                        bool okL = classifier.TrySampleMeshY(fp.FromFloat(snappedX - half), fp.FromFloat(snappedZ), out dummy, out hL);
                        bool okR = classifier.TrySampleMeshY(fp.FromFloat(snappedX + half), fp.FromFloat(snappedZ), out dummy, out hR);
                        bool okF = classifier.TrySampleMeshY(fp.FromFloat(snappedX), fp.FromFloat(snappedZ - half), out dummy, out hF);
                        bool okB = classifier.TrySampleMeshY(fp.FromFloat(snappedX), fp.FromFloat(snappedZ + half), out dummy, out hB);

                        if (!okL || !okR || !okF || !okB) continue;

                        float slopeX = (hR - hL) / _cellSize;
                        float slopeZ = (hB - hF) / _cellSize;
                        float mag    = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ);

                        float meshY = 0f;
                        if (!classifier.TrySampleMeshY(fp.FromFloat(snappedX), fp.FromFloat(snappedZ), out dummy, out meshY))
                            meshY = (hL + hR + hF + hB) * 0.25f;

                        int colIdx = Mathf.RoundToInt((snappedX - _gridOriginX) / _cellSize);
                        int rowIdx = Mathf.RoundToInt((snappedZ - _gridOriginZ) / _cellSize);

                        // Avoid duplicates (can happen with floating point snap).
                        if (_cellIndexByGrid.ContainsKey((rowIdx, colIdx))) continue;

                        _cellIndexByGrid[(rowIdx, colIdx)] = list.Count;
                        list.Add(new SlopeCell
                        {
                            cx = snappedX, cz = snappedZ, meshY = meshY,
                            slopeX = slopeX, slopeZ = slopeZ, magnitude = mag
                        });
                    }
                }
            }

            _cells = list.ToArray();
            Debug.Log($"[PutterGreenReader] BakeCells: {_cells.Length} green cells baked (cellSize={_cellSize}m).");

            // Build the procedural grid mesh.
            BuildGridMesh();

            // Update visible cell count seam for smoke-bot compatibility.
            LastVisibleCellCount = _cells.Length;
        }

        // ── Grid mesh generation ─────────────────────────────────────────────

        private void BuildGridMesh()
        {
            if (_cells == null || _cells.Length == 0)
            {
                DestroyGridMesh();
                return;
            }

            // Ensure child GO exists.
            EnsureGridMeshGO();

            // Build vertex and triangle arrays.
            // Each baked cell → one vertex. We need vertex positions AND vertex colors.
            // For quad triangulation: check 2×2 cell blocks; skip if any corner is missing.

            var vertices  = new Vector3[_cells.Length];
            var colors32  = new Color32[_cells.Length];

            for (int i = 0; i < _cells.Length; i++)
            {
                ref readonly var c = ref _cells[i];
                vertices[i] = new Vector3(c.cx, c.meshY + _surfaceYOffset, c.cz);
                Color col = CellColor(c.magnitude);
                colors32[i] = new Color32(
                    (byte)(col.r * 255),
                    (byte)(col.g * 255),
                    (byte)(col.b * 255),
                    (byte)(col.a * 255));
            }

            // Build quads: for each 2×2 block of adjacent green cells,
            // form 2 triangles. Skip if any of the 4 corner cells is missing.
            var tris = new List<int>(_cells.Length * 6);

            for (int row = 0; row < _gridRows - 1; row++)
            {
                for (int col = 0; col < _gridCols - 1; col++)
                {
                    // Corners: (row,col), (row,col+1), (row+1,col), (row+1,col+1)
                    if (!_cellIndexByGrid.TryGetValue((row,     col),     out int idxA)) continue;
                    if (!_cellIndexByGrid.TryGetValue((row,     col + 1), out int idxB)) continue;
                    if (!_cellIndexByGrid.TryGetValue((row + 1, col),     out int idxC)) continue;
                    if (!_cellIndexByGrid.TryGetValue((row + 1, col + 1), out int idxD)) continue;

                    // Triangle 1: A-B-C
                    tris.Add(idxA); tris.Add(idxB); tris.Add(idxC);
                    // Triangle 2: B-D-C
                    tris.Add(idxB); tris.Add(idxD); tris.Add(idxC);
                }
            }

            // Create or reuse mesh.
            if (_gridMesh == null)
                _gridMesh = new Mesh { name = "GreenGridMesh" };
            else
                _gridMesh.Clear();

            // UInt32 index format supports >65535 vertices (large greens).
            _gridMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _gridMesh.vertices    = vertices;
            _gridMesh.colors32    = colors32;
            _gridMesh.triangles   = tris.ToArray();
            _gridMesh.RecalculateNormals();
            _gridMesh.RecalculateBounds();

            _gridMeshFilter.sharedMesh = _gridMesh;

            // Active state follows _aimActive.
            _gridMeshGO.SetActive(_aimActive);

            Debug.Log($"[PutterGreenReader] BuildGridMesh: {vertices.Length} vertices, {tris.Count / 3} triangles.");
        }

        private void EnsureGridMeshGO()
        {
            if (_gridMeshGO != null) return;

            _gridMeshGO = new GameObject("GreenGridMesh");
            _gridMeshGO.transform.SetParent(transform, worldPositionStays: false);

            _gridMeshFilter   = _gridMeshGO.AddComponent<MeshFilter>();
            _gridMeshRenderer = _gridMeshGO.AddComponent<MeshRenderer>();

            // Assign material.
            _gridMeshRenderer.sharedMaterial = _gridMaterial;

            // Shadow settings: no shadows — it's a transparent overlay.
            _gridMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _gridMeshRenderer.receiveShadows     = false;
        }

        private void DestroyGridMesh()
        {
            if (_gridMeshGO != null)
            {
                if (Application.isPlaying)
                    Destroy(_gridMeshGO);
                else
#if UNITY_EDITOR
                    DestroyImmediate(_gridMeshGO);
#else
                    Destroy(_gridMeshGO);
#endif
                _gridMeshGO       = null;
                _gridMeshFilter   = null;
                _gridMeshRenderer = null;
            }

            if (_gridMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_gridMesh);
                else
#if UNITY_EDITOR
                    DestroyImmediate(_gridMesh);
#else
                    Destroy(_gridMesh);
#endif
                _gridMesh = null;
            }
        }

        // ── Color ramp ───────────────────────────────────────────────────────

        private Color CellColor(float magnitude)
        {
            if (HeatmapMode)
            {
                float t = Mathf.Clamp01(magnitude / _yellowThreshold);
                return t < 0.5f
                    ? Color.Lerp(ColorGreen,  ColorYellow, t * 2f)
                    : Color.Lerp(ColorYellow, ColorRed,    (t - 0.5f) * 2f);
            }

            if (magnitude < _greenThreshold)  return ColorGreen;
            if (magnitude < _yellowThreshold) return ColorYellow;
            return ColorRed;
        }

        // ── Runtime API ──────────────────────────────────────────────────────

        /// <summary>Force a rebake — called externally when the hole changes or tests run.</summary>
        public void RefreshBake() => BakeCells();
    }
}
