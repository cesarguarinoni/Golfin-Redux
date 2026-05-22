using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
    /// all green polygons. While the player aims with the putter, renders a grid of slope
    /// arrows via a single <c>Graphics.RenderMeshInstanced</c> call (no per-cell
    /// GameObjects, no per-frame physics).
    ///
    /// PGA 2K-style Sim positioning: the player reads the green — no live predicted curve.
    ///
    /// Color ramp thresholds load from <c>Assets/Data/GreenSlopeConfig.csv</c>:
    ///   GreenThreshold (default 0.02), YellowThreshold (default 0.05), per Q2.
    ///
    /// Material must have "Enable GPU Instancing" checked and must opt out of the URP
    /// SRP Batcher (DisableBatching tag or non-SRP-Batcher-compatible material) so that
    /// RenderMeshInstanced produces ONE draw call for all visible cells, not per-cell draws.
    ///
    /// SPEC: puttpath_predictor_perf_and_design §Architecture §§ Bake step + Render step.
    /// </summary>
    public class PutterGreenReader : MonoBehaviour
    {
        // ── Inspector refs ───────────────────────────────────────────────────

        [Header("Rendering")]
        [SerializeField] private Mesh     _arrowMesh;
        [SerializeField] private Material _arrowMaterial;
        [SerializeField] private Camera   _worldCamera;

        [Header("Lab Controller (for classifier access)")]
        [SerializeField] private PhysicsLabController _labController;

        [Header("Shot Controller (for putter aim detection)")]
        [SerializeField] private ShotController _shotController;

        // ── Runtime state ────────────────────────────────────────────────────

        // Baked slope cells.
        private struct SlopeCell
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
        private float _greenThreshold  = 0.02f;
        private float _yellowThreshold = 0.05f;
        private float _cellSize        = 0.5f;
        private float _visibleRadius   = 10.0f;

        // Colors (derived from thresholds, precomputed).
        private static readonly Color ColorGreen  = new Color(0.20f, 0.85f, 0.20f, 0.85f);
        private static readonly Color ColorYellow = new Color(1.00f, 0.85f, 0.10f, 0.85f);
        private static readonly Color ColorRed    = new Color(0.90f, 0.15f, 0.10f, 0.85f);

        // Heatmap mode: colors cells by slope magnitude (Q5 — dashboard toggle).
        public bool HeatmapMode { get; set; } = false;

        // Per-frame render scratch — reused to avoid per-frame allocation.
        // RenderMeshInstanced takes Matrix4x4[] (up to 1023 per batch).
        // We batch in chunks of MaxBatch.
        private const int MaxBatch = 1000;
        private readonly Matrix4x4[] _matBuf   = new Matrix4x4[MaxBatch];
        private MaterialPropertyBlock _mpb;   // Initialized in Awake — MaterialPropertyBlock.ctor calls Unity native API
        private Color[]               _colorBuf = new Color[MaxBatch];

        // Test seam: last visible-cell count for smoke-bot assertion.
        public int LastVisibleCellCount { get; private set; }

        // Test seam: force putter aim active (for screenshot verification and smoke-bot).
        // Do NOT call from production code; only from bot scenarios and editor scripts.
        public void SetAimActiveForTest(bool active) => _aimActive = active;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            // MaterialPropertyBlock must be constructed here (not as a field initializer)
            // because its ctor calls a Unity native API which is disallowed before Awake.
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            LoadConfig();
            HoleContext.OnChanged += OnHoleContextChanged;
            if (_shotController != null)
                _shotController.OnStateChanged += OnShotStateChanged;
        }

        private void OnDisable()
        {
            HoleContext.OnChanged -= OnHoleContextChanged;
            if (_shotController != null)
                _shotController.OnStateChanged -= OnShotStateChanged;
            _aimActive = false;
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
            if (!isPutterAim) LastVisibleCellCount = 0;
        }

        // ── Config loader ────────────────────────────────────────────────────

        private void LoadConfig()
        {
            // Lives at Assets/Resources/Data/GreenSlopeConfig.csv → Resources path "Data/GreenSlopeConfig".
            var csv = Resources.Load<TextAsset>("Data/GreenSlopeConfig");
            if (csv == null)
            {
                Debug.Log("[PutterGreenReader] Data/GreenSlopeConfig not found in Resources — using hardcoded Q2 defaults.");
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
                    case "GreenThreshold":    _greenThreshold  = val; break;
                    case "YellowThreshold":   _yellowThreshold = val; break;
                    case "CellSize":          _cellSize        = val; break;
                    case "VisibleRadiusMeters": _visibleRadius = val; break;
                }
            }
        }

        // ── Bake step ────────────────────────────────────────────────────────

        /// <summary>
        /// Bakes slope vectors for all green cells. Called on hole load and exposed
        /// as a public test seam so EditMode unit tests can inject a synthetic classifier
        /// and trigger the bake directly without a scene.
        /// </summary>
        public void BakeCells(BakedZoneClassifier classifierOverride = null)
        {
            BakedZoneClassifier classifier = classifierOverride;
            if (classifier == null && _labController != null)
            {
                var surf = _labController.GetSurfaces() as BakedZoneClassifier;
                classifier = surf;
            }

            if (classifier == null)
            {
                _cells = System.Array.Empty<SlopeCell>();
                Debug.Log("[PutterGreenReader] BakeCells: no BakedZoneClassifier available — grid empty.");
                return;
            }

            float half = _cellSize * 0.5f;
            var list   = new List<SlopeCell>(4096);

            foreach (var aabb in classifier.GetPolygonAABBsForType(SurfaceType.Green))
            {
                // Expand AABB by half a cell so boundary cells aren't clipped.
                float startX = aabb.xMin - half;
                float endX   = aabb.xMax + half;
                float startZ = aabb.yMin - half;   // Rect.y maps to world Z
                float endZ   = aabb.yMax + half;

                for (float cx = startX + half; cx <= endX - half; cx += _cellSize)
                {
                    for (float cz = startZ + half; cz <= endZ - half; cz += _cellSize)
                    {
                        // Classify center — skip cells outside the actual polygon.
                        if (classifier.Classify(fp.FromFloat(cx), fp.FromFloat(cz)) != SurfaceType.Green)
                            continue;

                        // Sample 4 neighbours for finite-difference gradient.
                        float hL, hR, hF, hB;
                        SurfaceType dummy;
                        bool okL = classifier.TrySampleMeshY(fp.FromFloat(cx - half), fp.FromFloat(cz), out dummy, out hL);
                        bool okR = classifier.TrySampleMeshY(fp.FromFloat(cx + half), fp.FromFloat(cz), out dummy, out hR);
                        bool okF = classifier.TrySampleMeshY(fp.FromFloat(cx), fp.FromFloat(cz - half), out dummy, out hF);
                        bool okB = classifier.TrySampleMeshY(fp.FromFloat(cx), fp.FromFloat(cz + half), out dummy, out hB);

                        if (!okL || !okR || !okF || !okB) continue;

                        float slopeX = (hR - hL) / _cellSize;   // grade in X direction
                        float slopeZ = (hB - hF) / _cellSize;   // grade in Z direction
                        float mag    = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ);

                        // Sample cell-center Y for placement.
                        float meshY = 0f;
                        if (!classifier.TrySampleMeshY(fp.FromFloat(cx), fp.FromFloat(cz), out dummy, out meshY))
                            meshY = (hL + hR + hF + hB) * 0.25f; // fallback: average of neighbours

                        list.Add(new SlopeCell
                        {
                            cx = cx, cz = cz, meshY = meshY,
                            slopeX = slopeX, slopeZ = slopeZ, magnitude = mag
                        });
                    }
                }
            }

            _cells = list.ToArray();
            Debug.Log($"[PutterGreenReader] BakeCells: {_cells.Length} green cells baked (cellSize={_cellSize}m).");
        }

        // ── Render step (per frame) ──────────────────────────────────────────

        private void Update()
        {
            if (!_aimActive || _cells.Length == 0) return;
            if (_arrowMesh == null || _arrowMaterial == null) return;

            Vector3 ballPos = Vector3.zero;
            if (_labController != null) ballPos = _labController.BallPosition;

            Plane[] frustum = _worldCamera != null
                ? GeometryUtility.CalculateFrustumPlanes(_worldCamera)
                : null;

            float radiusSq = _visibleRadius * _visibleRadius;

            int visCount = 0;
            int batchHead = 0;

            for (int i = 0; i < _cells.Length; i++)
            {
                ref readonly var c = ref _cells[i];

                // Distance cull.
                float dx = c.cx - ballPos.x;
                float dz = c.cz - ballPos.z;
                if (dx * dx + dz * dz > radiusSq) continue;

                // Frustum cull (optional — null when no camera assigned).
                Vector3 worldPos = new Vector3(c.cx, c.meshY + 0.02f, c.cz);
                if (frustum != null)
                {
                    var bounds = new Bounds(worldPos, new Vector3(_cellSize, 0.05f, _cellSize));
                    if (!GeometryUtility.TestPlanesAABB(frustum, bounds)) continue;
                }

                // Build TRS matrix: cell center, Y-rotated to point downhill, flat (no Y rotation for now).
                Quaternion rot = c.magnitude > 0.001f
                    ? Quaternion.LookRotation(new Vector3(-c.slopeX, 0f, -c.slopeZ), Vector3.up)
                    : Quaternion.identity;

                _matBuf[batchHead] = Matrix4x4.TRS(worldPos, rot, new Vector3(_cellSize * 0.8f, 1f, _cellSize * 0.8f));
                _colorBuf[batchHead] = CellColor(c.magnitude);
                batchHead++;
                visCount++;

                if (batchHead == MaxBatch)
                {
                    FlushBatch(batchHead);
                    batchHead = 0;
                }
            }

            if (batchHead > 0) FlushBatch(batchHead);
            LastVisibleCellCount = visCount;
        }

        // Per-frame scratch arrays — sized to MaxBatch to avoid per-flush allocation.
        private readonly Vector4[] _colorV4Buf = new Vector4[MaxBatch];

        private void FlushBatch(int count)
        {
            // Convert Color[] to Vector4[] for SetVectorArray (instanced per-instance color).
            // If the material doesn't declare instanced _BaseColor, arrows render with the
            // material's base color — still correct for direction, just not color-coded.
            for (int k = 0; k < count; k++)
            {
                Color c        = _colorBuf[k];
                _colorV4Buf[k] = new Vector4(c.r, c.g, c.b, c.a);
            }

            // SetVectorArray with the exact count avoids Unity printing warnings about
            // excess elements. Use a slice-sized temp array (one alloc per flush, but
            // flush runs at most a few times per frame for typical greens).
            var colorSlice = new Vector4[count];
            System.Array.Copy(_colorV4Buf, colorSlice, count);
            _mpb.SetVectorArray("_BaseColor", colorSlice);
            _mpb.SetVectorArray("_Color",     colorSlice);

            var rp = new RenderParams(_arrowMaterial)
            {
                matProps          = _mpb,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows    = false,
            };

            // Slice the matrix buffer to exactly 'count' elements.
            var matrices = new Matrix4x4[count];
            System.Array.Copy(_matBuf, matrices, count);

            Graphics.RenderMeshInstanced(rp, _arrowMesh, 0, matrices, count);
        }

        // ── Color ramp ───────────────────────────────────────────────────────

        private Color CellColor(float magnitude)
        {
            if (HeatmapMode)
            {
                // Heatmap: green → yellow → red across full range.
                float t = Mathf.Clamp01(magnitude / _yellowThreshold);
                return t < 0.5f
                    ? Color.Lerp(ColorGreen,  ColorYellow, t * 2f)
                    : Color.Lerp(ColorYellow, ColorRed,    (t - 0.5f) * 2f);
            }

            // Standard slope-grade threshold coloring (Q2).
            if (magnitude < _greenThreshold)  return ColorGreen;
            if (magnitude < _yellowThreshold) return ColorYellow;
            return ColorRed;
        }

        // ── Runtime API ──────────────────────────────────────────────────────

        /// <summary>Number of baked cells (0 = no hole loaded / classifier unavailable).</summary>
        public int BakedCellCount => _cells.Length;

        /// <summary>Force a rebake — called externally when the hole changes or tests run.</summary>
        public void RefreshBake() => BakeCells();
    }
}
