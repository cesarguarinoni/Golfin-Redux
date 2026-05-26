// Phase 2 of Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md
// Green Authoring EditorWindow — per-hole slope grid + pin candidate authoring.
// Menu: GOLFIN/Green Authoring/Open Editor

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Golfin.Course.Runtime;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Physics.Runtime.Baked;

namespace Golfin.Editor.GreenAuthoring
{
    /// <summary>
    /// IMGUI EditorWindow for authoring per-hole green slope grids and pin candidates.
    /// Opens via <c>GOLFIN/Green Authoring/Open Editor</c>.
    ///
    /// <para>
    /// <b>Persistence:</b> UI state (zoom, pan, brush settings) is stored in EditorPrefs.
    /// Green data lives only in <c>Assets/Resources/HoleData/Hole_NN/green.json</c>;
    /// loaded on hole switch, saved only when the Save button is pressed.
    /// </para>
    /// </summary>
    public class GreenTopologyEditor : EditorWindow
    {
        // ──────────────────────────────────────────────────────────────────────
        // Constants
        // ──────────────────────────────────────────────────────────────────────

        public const string PrefKeyCurrentHole  = "Golfin.PhysicsLab.CurrentHole"; // shared with PhysicsLabHolePicker
        private const string PrefKeyZoom        = "Golfin.GreenAuthoring.Zoom";
        private const string PrefKeyPanX        = "Golfin.GreenAuthoring.PanX";
        private const string PrefKeyPanY        = "Golfin.GreenAuthoring.PanY";
        private const string PrefKeyBrushRadius = "Golfin.GreenAuthoring.BrushRadius";
        private const string PrefKeyMode        = "Golfin.GreenAuthoring.Mode";

        private const float DefaultCellSize     = 0.5f;
        private const float ArrowScale          = 0.6f; // fraction of cellSize to draw arrow — bumped from 0.35 for visibility
        private const float MinZoom             = 10f;
        private const float MaxZoom             = 200f;

        // ──────────────────────────────────────────────────────────────────────
        // Editor mode
        // ──────────────────────────────────────────────────────────────────────

        public enum EditMode { PaintSlope = 0, AddPin = 1, ClearCells = 2, ProceduralFill = 3 }

        // ──────────────────────────────────────────────────────────────────────
        // State — persistent via EditorPrefs
        // ──────────────────────────────────────────────────────────────────────

        private int      _holeNumber  = 1;
        private float    _zoom        = 60f;   // pixels per world meter
        private Vector2  _pan         = Vector2.zero;
        private int      _brushRadius = 2;     // in cells
        private EditMode _mode        = EditMode.PaintSlope;
        private float    _slopeMag    = 3.0f;  // 0-12%
        private float    _slopeAngle  = 0f;    // degrees from +X

        // ──────────────────────────────────────────────────────────────────────
        // State — green data (in-memory; not auto-saved)
        // ──────────────────────────────────────────────────────────────────────

        private Vector2  _boundsMin;
        private Vector2  _boundsMax;
        private float    _cellSize  = DefaultCellSize;
        private int      _gridWidth;
        private int      _gridHeight;
        private float[]  _slopeGrid; // interleaved (dirX, dirZ, magPct)

        // Pin candidates list
        private readonly List<(Vector3 world, string label)> _pins = new List<(Vector3, string)>();
        private int      _defaultPinIndex;
        private string   _newPinLabel = "pin";

        // ──────────────────────────────────────────────────────────────────────
        // State — zone polygon (read from zones.json)
        // ──────────────────────────────────────────────────────────────────────

        private List<Vector2> _greenPolygon; // XZ vertices of the first green polygon
        private Vector2       _polygonCentroid;

        // ──────────────────────────────────────────────────────────────────────
        // State — heightmap sampler (loaded from heightmap.bytes)
        // ──────────────────────────────────────────────────────────────────────

        private HeightmapData _heightmap;

        // ──────────────────────────────────────────────────────────────────────
        // State — UI / feedback
        // ──────────────────────────────────────────────────────────────────────

        private string   _statusMessage = string.Empty;
        private bool     _statusIsError;
        private double   _statusClearTime;

        // ──────────────────────────────────────────────────────────────────────
        // State — capture-seam (used by GreenAuthoringVisualGate for window shots)
        // ──────────────────────────────────────────────────────────────────────
        // iter-4 Fix 1: capture is now delegated to CaptureEditorWindow helper.
        // RequestCapture / IsCaptureReady remain as the public gate API but internally
        // forward to the shared helper.

        // iter-4 Fix 4: track cells painted by the visual gate (step 5) so they render in
        // orange (vs. yellow for procedurally-filled gradient cells).
        // Cleared by ResetBoundsFromPolygon; populated by PaintCell when _gateMode=true.
        private readonly System.Collections.Generic.HashSet<int> _gatePaintedCells =
            new System.Collections.Generic.HashSet<int>();
        // _gateMode is enabled by GreenAuthoringVisualGate.SetGateMode before step-5 PaintCell
        // calls; disabled afterward.  Regular user paint does not set this flag.
        private bool _gateMode = false;

        // Retained for compatibility — used by ResetBoundsFromPolygon to clear gate paint tracking.

        private bool     _isDragging;
        private Vector2  _lastDragPos;
        private readonly List<Vector2> _dragHistory = new List<Vector2>();

        private bool     _isPanning;
        private Vector2  _panDragStart;
        private Vector2  _panOrigin;

        // Backdrop
        private Texture2D _backdropTex;
        private EditorBackdropMetadata _backdrop;

        // Pin list scroll
        private Vector2  _pinScrollPos;

        // ──────────────────────────────────────────────────────────────────────
        // Layout regions (computed each repaint)
        // ──────────────────────────────────────────────────────────────────────

        private Rect _leftSidebarRect;
        private Rect _rightSidebarRect;
        private Rect _greenViewRect;
        private Rect _statusBarRect;

        private const float SidebarWidth  = 200f;
        private const float StatusBarHeight = 22f;
        private const float TopBarHeight    = 26f;

        // ──────────────────────────────────────────────────────────────────────
        // Entry point
        // ──────────────────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Green Authoring/Open Editor")]
        public static GreenTopologyEditor Open()
        {
            var win = GetWindow<GreenTopologyEditor>("Green Authoring");
            win.minSize = new Vector2(800, 500);
            return win;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _zoom        = EditorPrefs.GetFloat(PrefKeyZoom,        60f);
            _pan         = new Vector2(EditorPrefs.GetFloat(PrefKeyPanX, 0f),
                                       EditorPrefs.GetFloat(PrefKeyPanY, 0f));
            _brushRadius = EditorPrefs.GetInt(PrefKeyBrushRadius, 2);
            _mode        = (EditMode)EditorPrefs.GetInt(PrefKeyMode, 0);
            _holeNumber  = EditorPrefs.GetInt(PrefKeyCurrentHole, 1);
            _holeNumber  = Mathf.Clamp(_holeNumber, 1, 18);

            LoadHole(_holeNumber);
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat(PrefKeyZoom,        _zoom);
            EditorPrefs.SetFloat(PrefKeyPanX,        _pan.x);
            EditorPrefs.SetFloat(PrefKeyPanY,        _pan.y);
            EditorPrefs.SetInt(PrefKeyBrushRadius,   _brushRadius);
            EditorPrefs.SetInt(PrefKeyMode,          (int)_mode);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Hole loading
        // ──────────────────────────────────────────────────────────────────────

        private void LoadHole(int holeNumber)
        {
            _holeNumber = holeNumber;
            EditorPrefs.SetInt(PrefKeyCurrentHole, holeNumber);

            _greenPolygon = null;
            _heightmap    = null;
            _slopeGrid    = null;
            _pins.Clear();
            _defaultPinIndex = 0;

            // Load zones.json to get green polygon.
            LoadZonesJson(holeNumber);

            // Load heightmap.bytes.
            LoadHeightmap(holeNumber);

            // Load green.json if present; otherwise create a blank grid.
            LoadOrInitGreenJson(holeNumber);

            // Reset status.
            SetStatus($"Loaded Hole {holeNumber:D2}", false);
            Repaint();
        }

        private void LoadZonesJson(int holeNumber)
        {
            string path = $"HoleData/Hole_{holeNumber:D2}/zones";
            TextAsset ta = Resources.Load<TextAsset>(path);
            if (ta == null)
            {
                SetStatus($"zones.json not found for Hole {holeNumber:D2}", true);
                return;
            }

            try
            {
                var zoneData = ZoneData.FromJson(ta.text);
                Resources.UnloadAsset(ta);

                if (zoneData == null || zoneData.zones == null) return;

                // Find first Green polygon.
                foreach (var group in zoneData.zones)
                {
                    if (group.SurfaceType != SurfaceType.Green) continue;
                    if (group.polygons == null || group.polygons.Count == 0) continue;

                    var poly = group.polygons[0];
                    if (poly.points == null || poly.points.Count < 3) continue;

                    _greenPolygon = new List<Vector2>(poly.points.Count);
                    foreach (var pt in poly.points)
                        _greenPolygon.Add(new Vector2(pt.x, pt.z));

                    _polygonCentroid = ComputeCentroid(_greenPolygon);
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GreenTopologyEditor] Failed to load zones.json for Hole {holeNumber:D2}: {ex.Message}");
            }
        }

        private void LoadHeightmap(int holeNumber)
        {
            string path = $"HoleData/Hole_{holeNumber:D2}/heightmap";
            TextAsset ta = Resources.Load<TextAsset>(path);
            if (ta == null) return;

            _heightmap = HeightmapLoader.LoadFromBytes(ta.bytes);
            Resources.UnloadAsset(ta);
        }

        private void LoadOrInitGreenJson(int holeNumber)
        {
            // Invalidate cache so we get fresh data.
            GreenTopologyCache.Invalidate(holeNumber);
            var topology = GreenTopology.LoadFromResources(holeNumber);

            if (topology != null)
            {
                // Restore from saved data.
                _boundsMin   = topology.BoundsMin;
                _boundsMax   = topology.BoundsMax;
                _cellSize    = topology.CellSize;
                _gridWidth   = topology.GridWidth;
                _gridHeight  = topology.GridHeight;

                int floatCount = _gridWidth * _gridHeight * 3;
                _slopeGrid = new float[floatCount];
                for (int i = 0; i < _gridWidth; i++)
                {
                    for (int j = 0; j < _gridHeight; j++)
                    {
                        Vector2 center = new Vector2(
                            _boundsMin.x + (i + 0.5f) * _cellSize,
                            _boundsMin.y + (j + 0.5f) * _cellSize);
                        topology.TrySampleSlope(center, out Vector2 dir, out float mag);
                        int idx = (j * _gridWidth + i) * 3;
                        _slopeGrid[idx]     = dir.x;
                        _slopeGrid[idx + 1] = dir.y;
                        _slopeGrid[idx + 2] = mag;
                    }
                }

                _pins.Clear();
                var pinPos   = topology.GetPinCandidates();
                var pinLabels = topology.GetPinLabels();
                for (int i = 0; i < pinPos.Count; i++)
                    _pins.Add((pinPos[i], pinLabels[i]));

                _defaultPinIndex = topology.DefaultPinIndex;
            }
            else
            {
                // Build a blank grid from the polygon AABB (or skeleton fallback).
                InitBlankGrid();
            }
        }

        private void InitBlankGrid()
        {
            if (_greenPolygon != null && _greenPolygon.Count >= 3)
            {
                // Compute AABB from polygon.
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var v in _greenPolygon)
                {
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;
                    if (v.y < minZ) minZ = v.y;
                    if (v.y > maxZ) maxZ = v.y;
                }
                _boundsMin = new Vector2(minX, minZ);
                _boundsMax = new Vector2(maxX, maxZ);
            }
            else
            {
                // Fallback: 2x2 meter area.
                _boundsMin = new Vector2(-10f, 40f);
                _boundsMax = new Vector2(-8f,  42f);
            }

            _cellSize   = DefaultCellSize;
            _gridWidth  = Mathf.Max(1, Mathf.CeilToInt((_boundsMax.x - _boundsMin.x) / _cellSize));
            _gridHeight = Mathf.Max(1, Mathf.CeilToInt((_boundsMax.y - _boundsMin.y) / _cellSize));
            _slopeGrid  = new float[_gridWidth * _gridHeight * 3];

            // Add a default pin at the centroid.
            _pins.Clear();
            Vector3 pinWorld = _greenPolygon != null
                ? new Vector3(_polygonCentroid.x, 0f, _polygonCentroid.y)
                : new Vector3(-9f, 38.5f, 41f);
            _pins.Add((pinWorld, "default-center"));
            _defaultPinIndex = 0;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Public scripting API (used by GreenAuthoringVisualGate)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Current hole number displayed in the picker.</summary>
        public int HoleNumber => _holeNumber;

        /// <summary>The loaded green polygon (may be null if no zones.json found).</summary>
        public List<Vector2> GreenPolygon => _greenPolygon;

        /// <summary>
        /// Returns the current in-memory slope grid (interleaved dirX, dirZ, magPct).
        /// Returns null if no grid has been initialized.
        /// </summary>
        public float[] SlopeGrid => _slopeGrid;

        /// <summary>World-space bounding rect min (world X in .x, world Z in .y).</summary>
        public Vector2 BoundsMin => _boundsMin;

        /// <summary>World-space bounding rect max (world X in .x, world Z in .y).</summary>
        public Vector2 BoundsMax => _boundsMax;

        /// <summary>Centroid of the loaded green polygon (XZ-plane, X in .x, Z in .y).
        /// Returns Vector2.zero if no polygon is loaded.</summary>
        public Vector2 PolygonCentroid => _polygonCentroid;

        /// <summary>Number of pin candidates currently in-memory.</summary>
        public int PinCount => _pins.Count;

        /// <summary>
        /// Switches to the specified hole and reloads data.
        /// </summary>
        public void SelectHole(int holeNumber)
        {
            LoadHole(Mathf.Clamp(holeNumber, 1, 18));
        }

        /// <summary>
        /// Runs procedural fill and replaces the in-memory slope grid.
        /// Does NOT auto-save; user must click Save.
        /// </summary>
        public void RunProceduralFill()
        {
            if (_greenPolygon == null || _greenPolygon.Count < 3)
            {
                SetStatus("Procedural Fill: no green polygon loaded.", true);
                return;
            }

            Func<Vector2, float> sampler = _heightmap != null
                ? (Func<Vector2, float>)(v => _heightmap.SampleHeight(
                      fp.FromFloat(v.x),
                      fp.FromFloat(v.y)).ToFloat())
                : (v => 0f);

            _slopeGrid = GreenAuthoringMath.ComputeProceduralSlopeField(
                _greenPolygon, _boundsMin, _boundsMax,
                _cellSize, _gridWidth, _gridHeight, sampler);

            SetStatus("Procedural Fill complete.", false);
            Repaint();
        }

        /// <summary>
        /// Reinitializes the slope grid using the loaded green polygon's AABB as bounds.
        /// Used by the visual gate (Fix 4) when the saved green.json skeleton has bounds
        /// that don't overlap the polygon (e.g. Hole_01 skeleton uses placeholder bounds
        /// while the actual polygon is far away in world space).
        ///
        /// <para>Calling this before <see cref="RunProceduralFillWithSampler"/> ensures
        /// that grid cell centers fall inside the polygon so <c>IsPointInPolygon</c>
        /// classifies them correctly and non-zero slope values are produced.</para>
        ///
        /// <para>Does NOT save to disk; call <see cref="SaveCurrentState"/> after.</para>
        /// </summary>
        public bool ResetBoundsFromPolygon()
        {
            if (_greenPolygon == null || _greenPolygon.Count < 3) return false;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in _greenPolygon)
            {
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minZ) minZ = v.y;
                if (v.y > maxZ) maxZ = v.y;
            }

            _boundsMin  = new Vector2(minX, minZ);
            _boundsMax  = new Vector2(maxX, maxZ);
            _cellSize   = DefaultCellSize;
            _gridWidth  = Mathf.Max(1, Mathf.CeilToInt((_boundsMax.x - _boundsMin.x) / _cellSize));
            _gridHeight = Mathf.Max(1, Mathf.CeilToInt((_boundsMax.y - _boundsMin.y) / _cellSize));
            _slopeGrid  = new float[_gridWidth * _gridHeight * 3];

            // Zoom to fit the polygon with ~20% margin, then re-centre the pan on the polygon
            // centroid so the green is visible without manual scrolling.
            //
            // The green view panel occupies: width=(winW - 2*SidebarWidth), height=(winH - TopBarHeight - StatusBarHeight).
            // We use position.width/height which is valid once the window is open and has had at
            // least one layout pass. If still zero (e.g. called before first Repaint), fall back
            // to a sensible default zoom.
            float polyW  = maxX - minX;
            float polyH  = maxZ - minZ;
            float viewW  = Mathf.Max(1f, position.width  - SidebarWidth * 2);
            float viewH  = Mathf.Max(1f, position.height - TopBarHeight - StatusBarHeight);
            const float MarginFraction = 0.8f; // use 80% of view for the polygon
            float zoomX  = viewW * MarginFraction / Mathf.Max(0.01f, polyW);
            float zoomY  = viewH * MarginFraction / Mathf.Max(0.01f, polyH);
            _zoom        = Mathf.Clamp(Mathf.Min(zoomX, zoomY), MinZoom, MaxZoom);

            // Centre pan so that the polygon centroid maps to view centre.
            // WorldToView(centroid) = viewCenter + pan + centroid * zoom = (viewW/2, viewH/2)
            // → pan.x = -centroid.x * zoom  (centroid in world X)
            // → pan.y = +centroid.z * zoom  (inverted Z)
            float cx = (minX + maxX) * 0.5f;
            float cz = (minZ + maxZ) * 0.5f;
            _pan = new Vector2(-cx * _zoom, cz * _zoom);

            EditorPrefs.SetFloat(PrefKeyZoom, _zoom);
            EditorPrefs.SetFloat(PrefKeyPanX, _pan.x);
            EditorPrefs.SetFloat(PrefKeyPanY, _pan.y);

            // Clear gate-painted cell tracking (Fix 4).
            _gatePaintedCells.Clear();

            Debug.Log($"[GreenTopologyEditor] ResetBoundsFromPolygon: bounds=({minX:F1},{minZ:F1})-({maxX:F1},{maxZ:F1}), grid={_gridWidth}x{_gridHeight}, zoom={_zoom:F1}, pan=({_pan.x:F1},{_pan.y:F1})");
            Repaint();
            return true;
        }

        /// <summary>
        /// Zooms the view to show a specific sub-region of the world (for visual gate captures).
        /// Sets zoom so the region fits the center panel, then centres the pan on the region.
        /// Used by the visual gate for arrow-visibility frames — showing a 6×6m subregion
        /// at high zoom makes slope arrows clearly distinguishable from cell coloring.
        /// </summary>
        /// <param name="centerWorldXZ">World-space centre of the region to show.</param>
        /// <param name="regionHalfExtentMeters">Half-extent of the region on each axis (meters).</param>
        public void ZoomToWorldRegion(Vector2 centerWorldXZ, float regionHalfExtentMeters)
        {
            float regionSize = regionHalfExtentMeters * 2f;
            float viewW = Mathf.Max(1f, position.width  - SidebarWidth * 2);
            float viewH = Mathf.Max(1f, position.height - TopBarHeight - StatusBarHeight);
            float zoomFit = Mathf.Min(viewW, viewH) * 0.85f / Mathf.Max(0.01f, regionSize);
            _zoom = Mathf.Clamp(zoomFit, MinZoom, MaxZoom);
            // Centre: WorldToView(centerWorldXZ) should map to (viewW/2, viewH/2).
            _pan  = new Vector2(-centerWorldXZ.x * _zoom, centerWorldXZ.y * _zoom);
            EditorPrefs.SetFloat(PrefKeyZoom, _zoom);
            EditorPrefs.SetFloat(PrefKeyPanX, _pan.x);
            EditorPrefs.SetFloat(PrefKeyPanY, _pan.y);
            Debug.Log($"[GreenTopologyEditor] ZoomToWorldRegion: center={centerWorldXZ}, halfExtent={regionHalfExtentMeters}, zoom={_zoom:F1}");
            Repaint();
        }

        /// <summary>
        /// Sets the window position and size explicitly. Used by the visual gate to ensure
        /// a large enough window so UI elements (toolbar, sidebars, center panel) are all
        /// visible in captures without clipping.
        /// </summary>
        public void SetWindowRect(Rect rect)
        {
            this.position = rect;
            Repaint();
        }

        /// <summary>
        /// Overload for <see cref="RunProceduralFill"/> that uses a caller-supplied
        /// height sampler instead of the loaded heightmap. Used by the visual gate
        /// (Fix 4) to inject a synthetic gradient that guarantees ≥10 non-zero cells
        /// regardless of whether Hole_01's heightmap overlaps the polygon bounds.
        ///
        /// <para>Does NOT replace <c>_heightmap</c>; the injected sampler is used only
        /// for this one procedural fill invocation. After the call returns, the window
        /// reverts to the real heightmap on the next user-initiated fill.</para>
        /// </summary>
        public void RunProceduralFillWithSampler(Func<Vector2, float> heightSampler)
        {
            if (_greenPolygon == null || _greenPolygon.Count < 3)
            {
                SetStatus("Procedural Fill (synthetic): no green polygon loaded.", true);
                return;
            }

            if (heightSampler == null) heightSampler = v => 0f;

            _slopeGrid = GreenAuthoringMath.ComputeProceduralSlopeField(
                _greenPolygon, _boundsMin, _boundsMax,
                _cellSize, _gridWidth, _gridHeight, heightSampler);

            SetStatus("Procedural Fill (synthetic gradient) complete.", false);
            Repaint();
        }

        /// <summary>
        /// Paints a slope value into a specific cell (cellX, cellZ).
        /// When <see cref="SetGateMode"/> was called, the cell is additionally tracked in
        /// <c>_gatePaintedCells</c> so the green-view can render it in a distinct orange color
        /// (vs. yellow for procedurally-filled gradient cells).
        /// </summary>
        public void PaintCell(int cellX, int cellZ, float dirX, float dirZ, float magPct)
        {
            if (_slopeGrid == null || cellX < 0 || cellX >= _gridWidth || cellZ < 0 || cellZ >= _gridHeight)
                return;

            int idx = (cellZ * _gridWidth + cellX) * 3;
            _slopeGrid[idx]     = dirX;
            _slopeGrid[idx + 1] = dirZ;
            _slopeGrid[idx + 2] = Mathf.Clamp(magPct, 0f, 12f);

            // Fix 4: track gate-painted cells for visual differentiation.
            if (_gateMode)
                _gatePaintedCells.Add(cellZ * _gridWidth + cellX);

            Repaint();
        }

        /// <summary>
        /// Enables or disables "gate mode" — when true, <see cref="PaintCell"/> calls also
        /// register the painted cell in an internal set so they can be rendered in orange
        /// (vs. yellow for gradient cells) in the green-view.
        /// Called by <see cref="GreenAuthoringVisualGate"/> before/after step-5 paint stroke.
        /// </summary>
        public void SetGateMode(bool enabled)
        {
            _gateMode = enabled;
            if (!enabled) return;  // Don't clear the set when disabling — the painted cells stay visible.
        }

        /// <summary>
        /// Adds a pin candidate.
        /// </summary>
        public void AddPin(Vector3 worldPos, string label)
        {
            _pins.Add((worldPos, label ?? "pin"));
            SetStatus($"Added pin '{label ?? "pin"}'", false);
            Repaint();
        }

        /// <summary>
        /// Clears all pin candidates from the in-memory state.
        /// Used by the visual gate after <see cref="ResetBoundsFromPolygon"/> to remove
        /// skeleton placeholder pins that are outside the polygon's world bounds.
        /// Does NOT save to disk; call <see cref="SaveCurrentState"/> after re-adding pins.
        /// </summary>
        public void ClearPins()
        {
            _pins.Clear();
            _defaultPinIndex = 0;
            SetStatus("Pins cleared.", false);
            Repaint();
        }

        /// <summary>
        /// Saves the current state to green.json.
        /// Returns true on success.
        /// </summary>
        public bool SaveCurrentState()
        {
            bool ok = GreenJsonWriter.SaveToResources(
                _holeNumber, "authored",
                _boundsMin, _boundsMax,
                _cellSize, _gridWidth, _gridHeight,
                _slopeGrid,
                _pins,
                _defaultPinIndex,
                _backdrop);

            if (ok)
                SetStatus($"Saved Hole_{_holeNumber:D2} green.json", false);
            else
                SetStatus("Save FAILED — see Console for details.", true);

            Repaint();
            return ok;
        }

        /// <summary>Returns count of non-zero-magnitude cells (for gate verification).</summary>
        public int NonZeroCellCount()
        {
            if (_slopeGrid == null) return 0;
            int count = 0;
            for (int i = 2; i < _slopeGrid.Length; i += 3)
                if (_slopeGrid[i] > 0.5f) count++;
            return count;
        }

        // ──────────────────────────────────────────────────────────────────────
        // EditorWindow capture seam (for GreenAuthoringVisualGate)
        // iter-4 Fix 1: now delegates to the shared CaptureEditorWindow helper.
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Schedules a Unity-native window capture on the next OnGUI pass.
        /// After calling this, call <see cref="Repaint()"/> then wait for
        /// <see cref="IsCaptureReady"/> to return true (poll via EditorApplication.update).
        ///
        /// <para>Delegates to <see cref="CaptureEditorWindow.Request"/> so the capture path
        /// is shared and not bespoke to GreenTopologyEditor (iter-4 Fix 1).</para>
        ///
        /// <para>Capture rect = <c>Rect(0, 0, position.width, position.height)</c> = the FULL
        /// window logical-pixel area including top toolbar, left/right sidebars, centre panel,
        /// and bottom status bar.</para>
        /// </summary>
        public void RequestCapture(string outputPath)
        {
            CaptureEditorWindow.Request(this, outputPath);
            Repaint();
        }

        /// <summary>
        /// Returns true once the capture requested by <see cref="RequestCapture"/> has been
        /// written to disk. One-shot — resets to false after reading.
        /// </summary>
        public bool IsCaptureReady()
        {
            return CaptureEditorWindow.IsReady(this);
        }

        // ──────────────────────────────────────────────────────────────────────
        // OnGUI
        // ──────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            // Auto-clear status after 5 seconds.
            if (!string.IsNullOrEmpty(_statusMessage) && EditorApplication.timeSinceStartup > _statusClearTime)
                _statusMessage = string.Empty;

            float w = position.width;
            float h = position.height;

            _leftSidebarRect  = new Rect(0, TopBarHeight, SidebarWidth, h - TopBarHeight - StatusBarHeight);
            _rightSidebarRect = new Rect(w - SidebarWidth, TopBarHeight, SidebarWidth, h - TopBarHeight - StatusBarHeight);
            _greenViewRect    = new Rect(SidebarWidth, TopBarHeight, w - SidebarWidth * 2, h - TopBarHeight - StatusBarHeight);
            _statusBarRect    = new Rect(0, h - StatusBarHeight, w, StatusBarHeight);

            DrawTopBar();
            DrawLeftSidebar();
            DrawRightSidebar();
            DrawGreenView();
            DrawStatusBar();

            // Capture seam: after all drawing, complete any pending capture request.
            // Delegates to CaptureEditorWindow.ExecutePendingCapture (iter-4 Fix 1).
            // Must be the LAST thing in OnGUI so all UI elements are already painted.
            CaptureEditorWindow.ExecutePendingCapture(this);
        }

        // ExecutePendingCapture removed in iter-4 Fix 1.
        // Capture is now handled entirely by CaptureEditorWindow.ExecutePendingCapture(this)
        // called at the end of OnGUI.  See CaptureEditorWindow.cs for implementation details.

        // ──────────────────────────────────────────────────────────────────────
        // Top bar
        // ──────────────────────────────────────────────────────────────────────

        private void DrawTopBar()
        {
            GUILayout.BeginArea(new Rect(0, 0, position.width, TopBarHeight));
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Hole:", GUILayout.Width(38));
            int newHole = EditorGUILayout.IntSlider(_holeNumber, 1, 18, GUILayout.Width(180));
            if (newHole != _holeNumber)
                LoadHole(newHole);

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Grid: {_gridWidth}x{_gridHeight}  Cell: {_cellSize}m  Pins: {_pins.Count}", GUILayout.Width(220));

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Left sidebar — mode + brush
        // ──────────────────────────────────────────────────────────────────────

        private void DrawLeftSidebar()
        {
            GUILayout.BeginArea(_leftSidebarRect);
            EditorGUILayout.LabelField("Edit Mode", EditorStyles.boldLabel);

            string[] modeNames = { "Paint Slope", "Add Pin", "Clear Cells", "Procedural Fill" };
            int newMode = GUILayout.SelectionGrid((int)_mode, modeNames, 1);
            if (newMode != (int)_mode)
            {
                _mode = (EditMode)newMode;
                EditorPrefs.SetInt(PrefKeyMode, newMode);
            }

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            _brushRadius = EditorGUILayout.IntSlider("Radius (cells)", _brushRadius, 1, 5);
            EditorPrefs.SetInt(PrefKeyBrushRadius, _brushRadius);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Slope", EditorStyles.boldLabel);
            _slopeMag = EditorGUILayout.Slider("Magnitude %", _slopeMag, 0f, 12f);
            _slopeAngle = EditorGUILayout.Slider("Direction°", _slopeAngle, 0f, 360f);

            // Direction display.
            float rad = _slopeAngle * Mathf.Deg2Rad;
            float dx  = Mathf.Cos(rad);
            float dz  = Mathf.Sin(rad);
            EditorGUILayout.LabelField($"Dir: ({dx:F2}, {dz:F2})");

            GUILayout.Space(8);
            EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
            _zoom = EditorGUILayout.Slider("Zoom (px/m)", _zoom, MinZoom, MaxZoom);

            if (GUILayout.Button("Reset View"))
            {
                _pan = Vector2.zero;
                _zoom = 60f;
                Repaint();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Procedural Fill"))
                RunProceduralFill();

            GUILayout.EndArea();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Right sidebar — pins + save/load
        // ──────────────────────────────────────────────────────────────────────

        private void DrawRightSidebar()
        {
            GUILayout.BeginArea(_rightSidebarRect);
            EditorGUILayout.LabelField("Pin Candidates", EditorStyles.boldLabel);

            // New pin label input.
            _newPinLabel = EditorGUILayout.TextField("Label", _newPinLabel);
            if (GUILayout.Button("Add Pin at Centroid"))
            {
                Vector3 pos = new Vector3(_polygonCentroid.x, 0f, _polygonCentroid.y);
                AddPin(pos, _newPinLabel);
            }

            GUILayout.Space(4);

            // Scrollable pin list.
            float listHeight = Mathf.Min(_rightSidebarRect.height - 200f, _pins.Count * 70f + 10f);
            _pinScrollPos = EditorGUILayout.BeginScrollView(_pinScrollPos,
                                GUILayout.Height(Mathf.Max(60f, listHeight)));

            int removeIdx = -1;
            for (int i = 0; i < _pins.Count; i++)
            {
                var (w, lbl) = _pins[i];
                bool isDefault = (i == _defaultPinIndex);

                GUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                bool setDefault = GUILayout.Toggle(isDefault, "", GUILayout.Width(16));
                if (setDefault && !isDefault) _defaultPinIndex = i;
                EditorGUILayout.LabelField($"[{i}] {lbl}", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("X", GUILayout.Width(20))) removeIdx = i;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"  ({w.x:F2}, {w.y:F2}, {w.z:F2})", EditorStyles.miniLabel);

                // Reorder buttons.
                EditorGUILayout.BeginHorizontal();
                if (i > 0 && GUILayout.Button("↑", GUILayout.Width(24)))
                {
                    var tmp = _pins[i]; _pins[i] = _pins[i - 1]; _pins[i - 1] = tmp;
                    if (_defaultPinIndex == i) _defaultPinIndex = i - 1;
                    else if (_defaultPinIndex == i - 1) _defaultPinIndex = i;
                }
                if (i < _pins.Count - 1 && GUILayout.Button("↓", GUILayout.Width(24)))
                {
                    var tmp = _pins[i]; _pins[i] = _pins[i + 1]; _pins[i + 1] = tmp;
                    if (_defaultPinIndex == i) _defaultPinIndex = i + 1;
                    else if (_defaultPinIndex == i + 1) _defaultPinIndex = i;
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            if (removeIdx >= 0)
            {
                _pins.RemoveAt(removeIdx);
                if (_defaultPinIndex >= _pins.Count) _defaultPinIndex = Mathf.Max(0, _pins.Count - 1);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(8);

            // Save / Load buttons.
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Save"))
                SaveCurrentState();
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Load / Reload"))
                LoadHole(_holeNumber);

            GUILayout.EndArea();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Green view (center canvas)
        // ──────────────────────────────────────────────────────────────────────

        private void DrawGreenView()
        {
            var e = Event.current;
            GUI.BeginClip(_greenViewRect);

            // Background.
            EditorGUI.DrawRect(new Rect(0, 0, _greenViewRect.width, _greenViewRect.height),
                               new Color(0.18f, 0.22f, 0.18f));

            // World origin in view-local coordinates.
            Vector2 viewCenter = new Vector2(_greenViewRect.width * 0.5f + _pan.x,
                                             _greenViewRect.height * 0.5f + _pan.y);

            // Draw backdrop if loaded.
            if (_backdropTex != null && _backdrop?.imagePoints != null && _backdrop?.worldPoints != null
                && _backdrop.imagePoints.Length == 2 && _backdrop.worldPoints.Length == 2)
            {
                // Just draw at reasonable scale for now.
                var texRect = new Rect(viewCenter.x - _backdropTex.width * 0.5f,
                                       viewCenter.y - _backdropTex.height * 0.5f,
                                       _backdropTex.width, _backdropTex.height);
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(texRect, _backdropTex, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }

            // Draw slope grid cells.
            if (_slopeGrid != null && _gridWidth > 0 && _gridHeight > 0)
            {
                float cellPx = _cellSize * _zoom;
                for (int cz = 0; cz < _gridHeight; cz++)
                {
                    for (int cx = 0; cx < _gridWidth; cx++)
                    {
                        int idx = (cz * _gridWidth + cx) * 3;
                        float dirX  = _slopeGrid[idx];
                        float dirZ  = _slopeGrid[idx + 1];
                        float magPct = _slopeGrid[idx + 2];

                        // Cell center in world XZ.
                        float wx = _boundsMin.x + (cx + 0.5f) * _cellSize;
                        float wz = _boundsMin.y + (cz + 0.5f) * _cellSize;
                        Vector2 cellScreen = WorldToView(new Vector2(wx, wz), viewCenter);

                        // Draw cell background.
                        Rect cellRect = new Rect(cellScreen.x - cellPx * 0.5f,
                                                 cellScreen.y - cellPx * 0.5f,
                                                 cellPx, cellPx);

                        if (magPct > 0.01f)
                        {
                            float intensity = Mathf.Clamp01(magPct / 6f);

                            // iter-4 Fix 4: gate-painted cells render in orange; gradient cells in green.
                            // This makes the 3 painted cells visually distinguishable from the
                            // procedurally-filled background field at any capture resolution.
                            bool isGatePainted = _gatePaintedCells.Contains(cz * _gridWidth + cx);
                            Color cellColor;
                            if (isGatePainted)
                            {
                                // Orange tint: painted stroke (gate step 5 cells)
                                cellColor = Color.Lerp(new Color(0.3f, 0.15f, 0.0f, 0.6f),
                                                       new Color(1.0f, 0.55f, 0.0f, 0.85f), intensity);
                            }
                            else
                            {
                                // Green tint: procedural gradient fill
                                cellColor = Color.Lerp(new Color(0.1f, 0.3f, 0.1f, 0.5f),
                                                       new Color(0.2f, 0.8f, 0.2f, 0.7f), intensity);
                            }
                            EditorGUI.DrawRect(cellRect, cellColor);

                            // Draw arrow if cell is big enough (lowered threshold for visibility at small zoom).
                            if (cellPx > 3f)
                            {
                                DrawArrow(cellScreen, new Vector2(dirX, dirZ), magPct, cellPx,
                                          isGatePainted);
                            }
                        }
                        else
                        {
                            EditorGUI.DrawRect(cellRect, new Color(0.12f, 0.15f, 0.12f, 0.3f));
                        }
                    }
                }
            }

            // Draw polygon outline using DrawAAPolyLine (3px thick) so it is distinguishable
            // from the cell shading at any zoom level.
            if (_greenPolygon != null && _greenPolygon.Count >= 2)
            {
                Handles.BeginGUI();
                Handles.color = new Color(0.2f, 1f, 0.2f, 1.0f);
                var pts = new Vector3[_greenPolygon.Count + 1];
                for (int vi = 0; vi < _greenPolygon.Count; vi++)
                {
                    Vector2 s = WorldToView(_greenPolygon[vi], viewCenter);
                    pts[vi] = new Vector3(s.x + _greenViewRect.x, s.y + _greenViewRect.y, 0);
                }
                // Close the polygon.
                pts[_greenPolygon.Count] = pts[0];
                Handles.DrawAAPolyLine(3f, pts);
                Handles.EndGUI();
            }

            // Draw pin candidates.
            // iter-4 Fix 5: pin marker sized to be clearly visible at capture resolution.
            // Cross arm length is max(14, cellPx*0.4) so at zoom=60px/m (30px cells) = 14px
            // and at zoom=120px/m (60px cells) = 24px.  Always ≥14px per arm = ≥28px cross.
            // Arm width is 3px (was 2px).
            {
                float cellPx = _cellSize * _zoom;
                float armLen = Mathf.Max(14f, cellPx * 0.4f);  // half-arm length in pixels
                float armW   = 3f;
                foreach (var (pinWorld, lbl) in _pins)
                {
                    Vector2 s = WorldToView(new Vector2(pinWorld.x, pinWorld.z), viewCenter);
                    // Horizontal arm.
                    EditorGUI.DrawRect(new Rect(s.x - armLen, s.y - armW * 0.5f, armLen * 2f, armW), Color.yellow);
                    // Vertical arm.
                    EditorGUI.DrawRect(new Rect(s.x - armW * 0.5f, s.y - armLen, armW, armLen * 2f), Color.yellow);
                    // Label.
                    GUI.Label(new Rect(s.x + armLen + 2f, s.y - 8, 120, 16), lbl,
                              new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } });
                }
            }

            // Grid lines (thin, only if zoom high enough).
            if (_zoom > 40f && _gridWidth > 0)
            {
                DrawGridLines(viewCenter);
            }

            // Handle mouse events in the view.
            HandleViewInput(e, viewCenter);

            GUI.EndClip();
        }

        private void DrawArrow(Vector2 center, Vector2 dir, float mag, float cellPx,
                               bool isGatePainted = false)
        {
            if (dir.sqrMagnitude < 1e-6f) return;

            float arrowLen = cellPx * ArrowScale * Mathf.Clamp01(mag / 6f);

            // Invert Y since GUI Y increases downward but world Z goes "up" in our view.
            Vector2 screenDir = new Vector2(dir.x, -dir.y);
            Vector2 end = center + screenDir * arrowLen;

            Handles.BeginGUI();
            // iter-4 Fix 4: gate-painted cells use orange arrows; gradient cells use yellow.
            Handles.color = isGatePainted
                ? new Color(1f, 0.5f, 0.05f, 1.0f)   // bright orange
                : new Color(1f, 0.85f, 0.1f, 0.9f);   // yellow
            Handles.DrawLine(
                new Vector3(center.x + _greenViewRect.x, center.y + _greenViewRect.y),
                new Vector3(end.x    + _greenViewRect.x, end.y    + _greenViewRect.y));

            // Arrowhead.
            Vector2 perpScreen = new Vector2(-screenDir.y, screenDir.x) * (arrowLen * 0.25f);
            Vector3 tipV  = new Vector3(end.x    + _greenViewRect.x, end.y    + _greenViewRect.y);
            Vector3 baseL = new Vector3(end.x - screenDir.x * arrowLen * 0.25f + perpScreen.x + _greenViewRect.x,
                                         end.y - screenDir.y * arrowLen * 0.25f + perpScreen.y + _greenViewRect.y);
            Vector3 baseR = new Vector3(end.x - screenDir.x * arrowLen * 0.25f - perpScreen.x + _greenViewRect.x,
                                         end.y - screenDir.y * arrowLen * 0.25f - perpScreen.y + _greenViewRect.y);
            Handles.DrawLine(tipV, baseL);
            Handles.DrawLine(tipV, baseR);
            Handles.EndGUI();
        }

        private void DrawGridLines(Vector2 viewCenter)
        {
            if (_slopeGrid == null) return;
            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.4f, 0.3f, 0.3f);

            for (int cx = 0; cx <= _gridWidth; cx++)
            {
                float wx = _boundsMin.x + cx * _cellSize;
                Vector2 top = WorldToView(new Vector2(wx, _boundsMin.y), viewCenter);
                Vector2 bot = WorldToView(new Vector2(wx, _boundsMax.y), viewCenter);
                Handles.DrawLine(
                    new Vector3(top.x + _greenViewRect.x, top.y + _greenViewRect.y),
                    new Vector3(bot.x + _greenViewRect.x, bot.y + _greenViewRect.y));
            }
            for (int cz = 0; cz <= _gridHeight; cz++)
            {
                float wz = _boundsMin.y + cz * _cellSize;
                Vector2 left  = WorldToView(new Vector2(_boundsMin.x, wz), viewCenter);
                Vector2 right = WorldToView(new Vector2(_boundsMax.x, wz), viewCenter);
                Handles.DrawLine(
                    new Vector3(left.x  + _greenViewRect.x, left.y  + _greenViewRect.y),
                    new Vector3(right.x + _greenViewRect.x, right.y + _greenViewRect.y));
            }
            Handles.EndGUI();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Status bar
        // ──────────────────────────────────────────────────────────────────────

        private void DrawStatusBar()
        {
            GUILayout.BeginArea(_statusBarRect);
            GUIStyle style = new GUIStyle(EditorStyles.helpBox);
            style.normal.textColor = _statusIsError ? Color.red : new Color(0.6f, 1f, 0.6f);
            style.fontSize = 11;
            GUILayout.Label(string.IsNullOrEmpty(_statusMessage) ? " " : _statusMessage, style);
            GUILayout.EndArea();
        }

        private void SetStatus(string msg, bool isError)
        {
            _statusMessage   = msg;
            _statusIsError   = isError;
            _statusClearTime = EditorApplication.timeSinceStartup + 8.0;
            if (isError) Debug.LogWarning($"[GreenTopologyEditor] {msg}");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Input handling
        // ──────────────────────────────────────────────────────────────────────

        private void HandleViewInput(Event e, Vector2 viewCenter)
        {
            if (e.type == EventType.ScrollWheel)
            {
                _zoom = Mathf.Clamp(_zoom * (1f - e.delta.y * 0.05f), MinZoom, MaxZoom);
                EditorPrefs.SetFloat(PrefKeyZoom, _zoom);
                e.Use();
                Repaint();
                return;
            }

            Vector2 mouseInView = e.mousePosition;
            bool inView = _greenViewRect.Contains(e.mousePosition + new Vector2(_greenViewRect.x, _greenViewRect.y));
            if (!inView) return;
            // Adjust to view-local.
            mouseInView = e.mousePosition - new Vector2(_greenViewRect.x, _greenViewRect.y);

            if (e.type == EventType.MouseDown && e.button == 2)
            {
                _isPanning    = true;
                _panDragStart = mouseInView;
                _panOrigin    = _pan;
                e.Use();
                return;
            }
            if (e.type == EventType.MouseUp && e.button == 2)
            {
                _isPanning = false;
                e.Use();
                return;
            }
            if (_isPanning && e.type == EventType.MouseDrag && e.button == 2)
            {
                _pan = _panOrigin + (mouseInView - _panDragStart);
                EditorPrefs.SetFloat(PrefKeyPanX, _pan.x);
                EditorPrefs.SetFloat(PrefKeyPanY, _pan.y);
                e.Use();
                Repaint();
                return;
            }

            // Convert mouse to world.
            Vector2 worldXZ = ViewToWorld(mouseInView, viewCenter);

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                // Right-click: clear cells under brush.
                PaintBrush(worldXZ, 0f, 0f, 0f);
                e.Use();
                Repaint();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (_mode == EditMode.AddPin)
                {
                    float height = _heightmap != null
                        ? _heightmap.SampleHeight(
                              fp.FromFloat(worldXZ.x),
                              fp.FromFloat(worldXZ.y)).ToFloat()
                        : 0f;
                    _pins.Add((new Vector3(worldXZ.x, height, worldXZ.y), _newPinLabel));
                    SetStatus($"Added pin '{_newPinLabel}' at ({worldXZ.x:F2}, {worldXZ.y:F2})", false);
                    e.Use();
                    Repaint();
                    return;
                }
                else if (_mode == EditMode.ClearCells)
                {
                    PaintBrush(worldXZ, 0f, 0f, 0f);
                    e.Use();
                    Repaint();
                    return;
                }
                else if (_mode == EditMode.PaintSlope || _mode == EditMode.ProceduralFill)
                {
                    _isDragging = true;
                    _lastDragPos = mouseInView;
                    _dragHistory.Clear();
                    _dragHistory.Add(mouseInView);
                }
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && _isDragging)
            {
                if (_mode == EditMode.PaintSlope)
                {
                    _dragHistory.Add(mouseInView);
                    if (_dragHistory.Count > 5) _dragHistory.RemoveAt(0);

                    // Compute drag direction from history.
                    Vector2 dragDir = ComputeDragDirection();
                    PaintBrush(worldXZ, dragDir.x, dragDir.y, _slopeMag);
                    e.Use();
                    Repaint();
                }
                _lastDragPos = mouseInView;
                return;
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _isDragging = false;
                _dragHistory.Clear();
            }
        }

        private Vector2 ComputeDragDirection()
        {
            if (_dragHistory.Count < 2)
            {
                float rad = _slopeAngle * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }

            Vector2 delta = _dragHistory[_dragHistory.Count - 1] - _dragHistory[0];
            // Map screen delta to world direction (Y is inverted in screen space vs. Z in world).
            Vector2 worldDir = new Vector2(delta.x, -delta.y);
            if (worldDir.sqrMagnitude < 1e-6f)
            {
                float rad = _slopeAngle * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }
            return worldDir.normalized;
        }

        private void PaintBrush(Vector2 centerWorldXZ, float dirX, float dirZ, float mag)
        {
            if (_slopeGrid == null) return;

            // Find cell under cursor.
            int centerCellX = Mathf.FloorToInt((centerWorldXZ.x - _boundsMin.x) / _cellSize);
            int centerCellZ = Mathf.FloorToInt((centerWorldXZ.y - _boundsMin.y) / _cellSize);

            for (int dz = -_brushRadius; dz <= _brushRadius; dz++)
            {
                for (int dx = -_brushRadius; dx <= _brushRadius; dx++)
                {
                    float distCells = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distCells > _brushRadius) continue;

                    int cx = centerCellX + dx;
                    int cz = centerCellZ + dz;
                    if (cx < 0 || cx >= _gridWidth || cz < 0 || cz >= _gridHeight) continue;

                    int idx = (cz * _gridWidth + cx) * 3;
                    _slopeGrid[idx]     = dirX;
                    _slopeGrid[idx + 1] = dirZ;
                    _slopeGrid[idx + 2] = Mathf.Clamp(mag, 0f, 12f);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Coordinate transforms
        // ──────────────────────────────────────────────────────────────────────

        private Vector2 WorldToView(Vector2 worldXZ, Vector2 viewCenter)
        {
            // World X → screen X; world Z → screen Y (inverted for Y-up display).
            return new Vector2(
                viewCenter.x + (worldXZ.x) * _zoom,
                viewCenter.y - (worldXZ.y) * _zoom  // invert Z so +Z goes up on screen
            );
        }

        private Vector2 ViewToWorld(Vector2 viewPos, Vector2 viewCenter)
        {
            return new Vector2(
                (viewPos.x - viewCenter.x) / _zoom,
                -((viewPos.y - viewCenter.y) / _zoom)
            );
        }

        // ──────────────────────────────────────────────────────────────────────
        // Geometry helpers
        // ──────────────────────────────────────────────────────────────────────

        private static Vector2 ComputeCentroid(List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (var v in polygon) sum += v;
            return sum / polygon.Count;
        }
    }
}
