// Phase 1 of Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md
// Locked 2026-05-26 by Architect ↔ Cesar (this session).
// Read-only runtime view over per-green authored slope + pin data.
// Authoring tool lives in Phase 2 (Golfin.Editor.GreenAuthoring).
// Physics consumer lands in Phase 6 (BallSimulation.RunPuttPhase / RunRollPhase).

using System;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.Loop;

namespace Golfin.Course.Runtime
{
    /// <summary>
    /// In-memory representation of one green's authored topology.
    /// Loaded from <c>Assets/Resources/HoleData/&lt;courseSlug&gt;/Hole_XX/green.json</c> via <see cref="LoadFromResources"/>.
    ///
    /// Schema v1 (locked 2026-05-26, see SPEC §"green.json schema"):
    /// <list type="bullet">
    ///   <item>Dense slope grid at <c>cellSize</c> resolution (default 0.5 m per SPEC L7)
    ///         over the green's axis-aligned bounding rect in world XZ space.</item>
    ///   <item>Each cell stores <c>(slopeDirX, slopeDirZ, magnitudePercent)</c> as three float32s.
    ///         <c>magnitudePercent</c> is a percent grade (e.g. 3.0 = 3% slope).
    ///         Cells outside the green polygon store <c>(0, 0, 0)</c> — see SPEC L2.</item>
    ///   <item>Pin candidates as world-space <see cref="Vector3"/> list with parallel label array;
    ///         <see cref="DefaultPinIndex"/> selects the canonical pin (SPEC L4).</item>
    /// </list>
    ///
    /// Schema v2 (added 2026-05-28, see green_slope_height_bake SPEC):
    /// <list type="bullet">
    ///   <item>All v1 fields preserved — v2 is strictly additive.</item>
    ///   <item><c>heightGridBase64</c>: float32 ×1 per cell, row-major [z][x], zero-mean relative
    ///         meters. Cells outside the green polygon store 0. Decode via
    ///         <see cref="TrySampleHeight"/>.</item>
    ///   <item><c>heightDatumY</c>: reserved for future absolute datum; bake writes 0.</item>
    ///   <item>Holes without a v2 <c>green.json</c> return <c>null</c> from
    ///         <see cref="LoadFromResources"/> and degrade gracefully (flat green); see the
    ///         null-guard in <see cref="GreenTopologyCache.GetForHole"/>.</item>
    /// </list>
    ///
    /// <para>
    /// <b>Coordinate convention.</b> <see cref="Vector2"/> world-XZ pairs use <c>.x</c> for world X
    /// and <c>.y</c> for world Z (the Vector2 component name is unfortunate but matches the rest of
    /// the codebase's XZ-on-Vector2 idiom — see <c>HeightmapData</c>). The same holds for the slope
    /// direction Vector2 emitted by <see cref="TrySampleSlope"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Inactive cells.</b> <see cref="TrySampleSlope"/> returns <c>true</c> for any query inside
    /// the bounding rect — cells outside the green polygon emit <c>dirXZ = (0,0), magPct = 0</c>.
    /// Physics consumers (Phase 6) can therefore add unconditional lateral acceleration; zero-mag
    /// cells contribute zero force without a separate "is active" check. Same convention for
    /// <see cref="TrySampleHeight"/>: outside-polygon cells return 0.
    /// </para>
    ///
    /// <para>
    /// Use <see cref="GreenTopologyCache.GetForHole"/> in hot paths — direct
    /// <see cref="LoadFromResources"/> calls re-decode the base64 grid each time.
    /// </para>
    /// </summary>
    public sealed class GreenTopology
    {
        /// <summary>Current schema version emitted by the bake tool. Bump on breaking changes.</summary>
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion { get; private set; }
        public int HoleNumber    { get; private set; }
        public string SourceTag  { get; private set; }

        /// <summary>Bounding rect min corner (world X in <c>.x</c>, world Z in <c>.y</c>).</summary>
        public Vector2 BoundsMin { get; private set; }
        /// <summary>Bounding rect max corner (world X in <c>.x</c>, world Z in <c>.y</c>).</summary>
        public Vector2 BoundsMax { get; private set; }

        /// <summary>Cell side length in world meters. Default 0.5 per SPEC L7.</summary>
        public float CellSize    { get; private set; }
        public int GridWidth     { get; private set; }
        public int GridHeight    { get; private set; }

        /// <summary>Index into <see cref="GetPinCandidates"/> picked as canonical default. SPEC L4.</summary>
        public int DefaultPinIndex { get; private set; }

        /// <summary>
        /// Absolute Y datum baked alongside the height grid. Reserved for future use; bake writes 0.
        /// <see cref="TrySampleHeight"/> returns relative-metre offsets — add this if you need absolute.
        /// </summary>
        public float HeightDatumY { get; private set; }

        // ── Coordinate mapping (geo-frame grid ↔ Lite-importer world frame) ──
        // The height/slope grids are baked in geo-pipeline world space.
        // The Lite importer operates in a different world space (different terrain).
        // Transform: geoX = GeoCentroidX + (liteX - LiteCentroidX) * LiteToGeoScale
        //            geoZ = GeoCentroidZ + (liteZ - LiteCentroidZ) * LiteToGeoScale
        // All values are 0 / 1.0 for schema v2 files baked before this field was added
        // (which means TrySampleHeightAtLiteWorld falls back to identity → same as TrySampleHeight).

        /// <summary>Centroid of the geo-pipeline green contour in geo world-X.</summary>
        public float GeoCentroidX { get; private set; }
        /// <summary>Centroid of the geo-pipeline green contour in geo world-Z.</summary>
        public float GeoCentroidZ { get; private set; }
        /// <summary>Centroid of the UHoleLite export contour in Lite world-X (after 90°CCW rotation).</summary>
        public float LiteCentroidX { get; private set; }
        /// <summary>Centroid of the UHoleLite export contour in Lite world-Z (after 90°CCW rotation).</summary>
        public float LiteCentroidZ { get; private set; }
        /// <summary>
        /// Uniform scale factor to convert Lite-world metres to geo-world metres.
        /// geoRadius = liteRadius × LiteToGeoScale. Typically ~1.0–1.3, varies per hole.
        /// </summary>
        public float LiteToGeoScale { get; private set; }

        /// <summary>
        /// Resampled green contour (world XZ, {x=worldX, y=worldZ}) produced by
        /// bake-green.mjs iter-8 (D1). Present when <c>contourVersion == "resampled-v1"</c>.
        /// Used by <see cref="HoleGeoImporter"/> as the single source of truth for the green
        /// polygon (CDT build, collar dilation, terrain-hole-carve, fairway-drop).
        /// Null when the baked file predates iter-8 or does not include the field.
        /// </summary>
        public Vector2[] ContourResampled { get; private set; }

        /// <summary>
        /// Height shift mode used by the bake ("min" = min-shifted ≥ 0, or absent for zero-mean).
        /// Checked by the importer to verify the expected D3 convention.
        /// </summary>
        public string HeightShiftMode { get; private set; }

        // Row-major [z][x] float32 triples (dirX, dirZ, magPct).
        // Length == GridWidth * GridHeight * 3. Triple-stride = 3.
        private float[]  _slopeGrid;

        // Row-major [z][x] float32 singles (relative height in metres).
        // Length == GridWidth * GridHeight. Zero-mean; outside-polygon cells store 0.
        // Null when the loaded file is schema v2 but heightGridBase64 was absent or empty.
        private float[]  _heightGrid;

        private Vector3[] _pinCandidates;
        private string[]  _pinLabels;

        private GreenTopology() { }

        /// <summary>
        /// Loads <c>Assets/Resources/HoleData/&lt;courseSlug&gt;/Hole_NN/green.json</c> for the given hole, or returns
        /// <c>null</c> when (a) no file present, (b) JSON parse failed, (c) schemaVersion mismatch,
        /// or (d) slope grid byte length disagrees with the declared <c>gridWidth × gridHeight × 3</c>.
        /// All failure modes log via <see cref="Debug.LogError"/>. The bare TextAsset is unloaded
        /// from Resources immediately after parse; only the decoded float grid is retained.
        /// </summary>
        public static GreenTopology LoadFromResources(int holeNumber)
        {
            string path = $"HoleData/{ActiveCourseContext.CurrentCourseSlug}/Hole_{holeNumber:D2}/green";
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null) return null;

            string json = asset.text;
            Resources.UnloadAsset(asset);

            GreenJsonDto dto;
            try
            {
                dto = JsonUtility.FromJson<GreenJsonDto>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: JSON parse failed at Resources/{path} — {ex.Message}");
                return null;
            }

            if (dto == null)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: JSON parse returned null at Resources/{path}");
                return null;
            }
            if (dto.schemaVersion != CurrentSchemaVersion)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: schemaVersion={dto.schemaVersion}, expected {CurrentSchemaVersion}. Re-bake via Green Authoring tool.");
                return null;
            }

            return FromDto(dto, holeNumber, path);
        }

        /// <summary>
        /// Loads directly from an absolute or project-relative file path (for use during
        /// Editor import, where Resources.Load may return null due to cache invalidation
        /// after AssetDatabase.Refresh()). Editor-only; bypasses the Resources system.
        /// </summary>
        public static GreenTopology LoadFromDisk(string filePath, int holeNumber)
        {
            if (!System.IO.File.Exists(filePath)) return null;

            string json;
            try { json = System.IO.File.ReadAllText(filePath); }
            catch (Exception ex)
            {
                Debug.LogError($"[GreenTopology] LoadFromDisk Hole {holeNumber:D2}: read failed — {ex.Message}");
                return null;
            }

            GreenJsonDto dto;
            try { dto = JsonUtility.FromJson<GreenJsonDto>(json); }
            catch (Exception ex)
            {
                Debug.LogError($"[GreenTopology] LoadFromDisk Hole {holeNumber:D2}: JSON parse failed — {ex.Message}");
                return null;
            }

            if (dto == null) { Debug.LogError($"[GreenTopology] LoadFromDisk Hole {holeNumber:D2}: JSON parse returned null"); return null; }
            if (dto.schemaVersion != CurrentSchemaVersion)
            {
                Debug.LogError($"[GreenTopology] LoadFromDisk Hole {holeNumber:D2}: schemaVersion={dto.schemaVersion}, expected {CurrentSchemaVersion}. Re-bake.");
                return null;
            }

            return FromDto(dto, holeNumber, filePath);
        }

        /// <summary>
        /// Samples the authored slope at a world-XZ point. Returns <c>false</c> only when the query
        /// falls outside the bounding rect; in-bounds-but-outside-polygon cells return <c>true</c>
        /// with <c>dirXZ = (0,0)</c> and <c>magPct = 0</c>. No interpolation across cell boundaries
        /// — Phase 2 authoring grid is dense at 0.5 m, sufficient for putt-scale physics.
        /// </summary>
        public bool TrySampleSlope(Vector2 worldXZ, out Vector2 dirXZ, out float magPct)
        {
            if (_slopeGrid == null || _slopeGrid.Length == 0 ||
                worldXZ.x < BoundsMin.x || worldXZ.x > BoundsMax.x ||
                worldXZ.y < BoundsMin.y || worldXZ.y > BoundsMax.y)
            {
                dirXZ  = Vector2.zero;
                magPct = 0f;
                return false;
            }

            int cellX = Mathf.Clamp(
                Mathf.FloorToInt((worldXZ.x - BoundsMin.x) / CellSize),
                0, GridWidth  - 1);
            int cellZ = Mathf.Clamp(
                Mathf.FloorToInt((worldXZ.y - BoundsMin.y) / CellSize),
                0, GridHeight - 1);

            int idx = (cellZ * GridWidth + cellX) * 3;
            dirXZ  = new Vector2(_slopeGrid[idx], _slopeGrid[idx + 1]);
            magPct = _slopeGrid[idx + 2];
            return true;
        }

        /// <summary>
        /// Samples the authored relative height at a world-XZ point (schema v2 only).
        /// Returns <c>false</c> when (a) the query falls outside the bounding rect, or (b) no
        /// height grid is available (v1 file or missing <c>heightGridBase64</c>).
        /// In-bounds-but-outside-polygon cells return <c>true</c> with <c>relHeightM = 0</c>.
        /// Uses nearest-cell lookup (same convention as <see cref="TrySampleSlope"/>).
        /// </summary>
        /// <param name="worldXZ">World XZ query point.</param>
        /// <param name="relHeightM">Zero-mean relative height in metres above the green datum.</param>
        public bool TrySampleHeight(Vector2 worldXZ, out float relHeightM)
        {
            if (_heightGrid == null || _heightGrid.Length == 0 ||
                worldXZ.x < BoundsMin.x || worldXZ.x > BoundsMax.x ||
                worldXZ.y < BoundsMin.y || worldXZ.y > BoundsMax.y)
            {
                relHeightM = 0f;
                return false;
            }

            int cellX = Mathf.Clamp(
                Mathf.FloorToInt((worldXZ.x - BoundsMin.x) / CellSize),
                0, GridWidth  - 1);
            int cellZ = Mathf.Clamp(
                Mathf.FloorToInt((worldXZ.y - BoundsMin.y) / CellSize),
                0, GridHeight - 1);

            relHeightM = _heightGrid[cellZ * GridWidth + cellX];
            return true;
        }

        /// <summary>
        /// Samples the authored relative height at a world-XZ point using <b>bilinear interpolation</b>
        /// across the four surrounding cells (Fix 2, iter-12).
        ///
        /// Additive sibling to <see cref="TrySampleHeight"/> (nearest-cell). Nearest-cell stays alive
        /// for <c>BakedHeightProvider</c> and runtime callers that do not require interpolation.
        ///
        /// Behaviour:
        /// <list type="bullet">
        ///   <item>Returns <c>false</c> when the height grid is unavailable, or when any corner of
        ///         the 2×2 bilinear stencil lies outside the grid. In those cases <c>relHeightM = 0</c>.</item>
        ///   <item>All four stencil corners are expected to be non-zero after <c>bake-green.mjs</c> Fix 1
        ///         (1-cell mask dilation) — boundary verts no longer land on zero-outside cells.</item>
        /// </list>
        /// </summary>
        /// <param name="worldXZ">World XZ query point.</param>
        /// <param name="relHeightM">Bilinearly-interpolated relative height in metres above the green datum.</param>
        public bool TrySampleHeightBilinear(Vector2 worldXZ, out float relHeightM)
        {
            if (_heightGrid == null || _heightGrid.Length == 0 ||
                worldXZ.x < BoundsMin.x || worldXZ.x > BoundsMax.x ||
                worldXZ.y < BoundsMin.y || worldXZ.y > BoundsMax.y)
            {
                relHeightM = 0f;
                return false;
            }

            // Fractional cell coordinates (0 = left/bottom edge of cell (0,0))
            float fx = (worldXZ.x - BoundsMin.x) / CellSize;
            float fz = (worldXZ.y - BoundsMin.y) / CellSize;

            int ix0 = Mathf.FloorToInt(fx);
            int iz0 = Mathf.FloorToInt(fz);
            int ix1 = ix0 + 1;
            int iz1 = iz0 + 1;

            // All four stencil corners must lie within the grid.
            // If any corner is out of bounds, fall back gracefully to false.
            if (ix0 < 0 || ix1 >= GridWidth || iz0 < 0 || iz1 >= GridHeight)
            {
                relHeightM = 0f;
                return false;
            }

            float tx = fx - ix0;
            float tz = fz - iz0;

            float h00 = _heightGrid[iz0 * GridWidth + ix0];
            float h10 = _heightGrid[iz0 * GridWidth + ix1];
            float h01 = _heightGrid[iz1 * GridWidth + ix0];
            float h11 = _heightGrid[iz1 * GridWidth + ix1];

            // Standard bilinear: weight (1-tx)(1-tz), tx(1-tz), (1-tx)tz, tx*tz
            relHeightM = h00 * (1f - tx) * (1f - tz)
                       + h10 * tx        * (1f - tz)
                       + h01 * (1f - tx) * tz
                       + h11 * tx        * tz;
            return true;
        }

        /// <summary>
        /// Samples the authored relative height from a <b>Lite-importer world-XZ</b> point.
        /// Applies the centroid + scale transform stored in the green.json to convert from the
        /// Lite coordinate frame to the geo-pipeline frame before delegating to
        /// <see cref="TrySampleHeight"/>. Falls back to identity transform when
        /// <see cref="LiteToGeoScale"/> is 0 or the mapping fields were absent in the JSON
        /// (bakes produced before these fields were added).
        /// </summary>
        /// <param name="liteWorldXZ">Vertex position in Lite-importer world XZ (x = world-X, y = world-Z).</param>
        /// <param name="relHeightM">Zero-mean relative height in metres above the green datum.</param>
        public bool TrySampleHeightAtLiteWorld(Vector2 liteWorldXZ, out float relHeightM)
        {
            float scale = (LiteToGeoScale > 0.001f) ? LiteToGeoScale : 1f;
            float geoX = GeoCentroidX + (liteWorldXZ.x - LiteCentroidX) * scale;
            float geoZ = GeoCentroidZ + (liteWorldXZ.y - LiteCentroidZ) * scale;
            return TrySampleHeight(new Vector2(geoX, geoZ), out relHeightM);
        }

        /// <summary>
        /// Returns the canonical default pin in world space. Throws when no pins are authored —
        /// authoring tool (Phase 2) MUST emit at least one candidate; <c>green.json</c> with an
        /// empty <c>pinCandidates</c> is a hard error to surface during Phase 3 tracing.
        /// </summary>
        public Vector3 GetDefaultPin()
        {
            if (_pinCandidates == null || _pinCandidates.Length == 0)
                throw new InvalidOperationException(
                    $"[GreenTopology] Hole {HoleNumber:D2}: no pin candidates authored. Run Green Authoring tool.");
            return _pinCandidates[DefaultPinIndex];
        }

        /// <summary>All authored pin candidates in author-supplied order. Index 0 == Shot Navi flag per SPEC L8.</summary>
        public IReadOnlyList<Vector3> GetPinCandidates() => _pinCandidates;

        /// <summary>Pin labels indexed-aligned with <see cref="GetPinCandidates"/>.</summary>
        public IReadOnlyList<string> GetPinLabels() => _pinLabels;

        // ───────────────────────── private wiring ─────────────────────────

        private static GreenTopology FromDto(GreenJsonDto dto, int holeNumber, string resourcePath)
        {
            int expectedSlopeFloats = dto.gridWidth * dto.gridHeight * 3;
            if (expectedSlopeFloats <= 0)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: invalid grid size {dto.gridWidth}x{dto.gridHeight} at Resources/{resourcePath}.");
                return null;
            }

            // ── Slope grid (required — present in both v1 and v2) ──
            byte[] slopeBytes;
            try
            {
                slopeBytes = Convert.FromBase64String(dto.slopeGridBase64 ?? string.Empty);
            }
            catch (FormatException ex)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: slopeGridBase64 decode failed at Resources/{resourcePath} — {ex.Message}");
                return null;
            }

            int expectedSlopeBytes = expectedSlopeFloats * sizeof(float);
            if (slopeBytes.Length != expectedSlopeBytes)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: slope grid byte length {slopeBytes.Length} != expected {expectedSlopeBytes} (grid {dto.gridWidth}x{dto.gridHeight} * 3 floats) at Resources/{resourcePath}.");
                return null;
            }

            var slopeGrid = new float[expectedSlopeFloats];
            Buffer.BlockCopy(slopeBytes, 0, slopeGrid, 0, expectedSlopeBytes);

            // ── Height grid (v2 additive — absent or empty = graceful null) ──
            float[] heightGrid = null;
            if (!string.IsNullOrEmpty(dto.heightGridBase64))
            {
                byte[] heightBytes;
                try
                {
                    heightBytes = Convert.FromBase64String(dto.heightGridBase64);
                }
                catch (FormatException ex)
                {
                    Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: heightGridBase64 decode failed at Resources/{resourcePath} — {ex.Message}");
                    return null;
                }

                int expectedHeightFloats = dto.gridWidth * dto.gridHeight; // 1 float per cell
                int expectedHeightBytes  = expectedHeightFloats * sizeof(float);
                if (heightBytes.Length != expectedHeightBytes)
                {
                    Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: height grid byte length {heightBytes.Length} != expected {expectedHeightBytes} (grid {dto.gridWidth}x{dto.gridHeight} * 1 float) at Resources/{resourcePath}.");
                    return null;
                }

                heightGrid = new float[expectedHeightFloats];
                Buffer.BlockCopy(heightBytes, 0, heightGrid, 0, expectedHeightBytes);
            }

            int pinCount = dto.pinCandidates != null ? dto.pinCandidates.Length : 0;
            var pins   = new Vector3[pinCount];
            var labels = new string[pinCount];
            for (int i = 0; i < pinCount; i++)
            {
                var p = dto.pinCandidates[i];
                pins[i]   = new Vector3(p.worldX, p.worldY, p.worldZ);
                labels[i] = p.label ?? string.Empty;
            }

            int defaultIdx = pinCount > 0 ? Mathf.Clamp(dto.defaultPinIndex, 0, pinCount - 1) : 0;

            float minX = dto.boundsMin != null ? dto.boundsMin.x : 0f;
            float minZ = dto.boundsMin != null ? dto.boundsMin.z : 0f;
            float maxX = dto.boundsMax != null ? dto.boundsMax.x : 0f;
            float maxZ = dto.boundsMax != null ? dto.boundsMax.z : 0f;

            // ── iter-8: Decode resampled contour (D1) ──
            Vector2[] contourResampled = null;
            if (dto.contourResampled != null && dto.contourResampled.Length >= 3)
            {
                contourResampled = new Vector2[dto.contourResampled.Length];
                for (int i = 0; i < dto.contourResampled.Length; i++)
                    contourResampled[i] = new Vector2(dto.contourResampled[i].x, dto.contourResampled[i].z);
            }

            return new GreenTopology
            {
                SchemaVersion    = dto.schemaVersion,
                HoleNumber       = dto.holeNumber,
                SourceTag        = dto.sourceTag ?? string.Empty,
                BoundsMin        = new Vector2(minX, minZ),
                BoundsMax        = new Vector2(maxX, maxZ),
                CellSize         = dto.cellSize,
                GridWidth        = dto.gridWidth,
                GridHeight       = dto.gridHeight,
                HeightDatumY     = dto.heightDatumY,
                DefaultPinIndex  = defaultIdx,
                GeoCentroidX     = dto.geoCentroidX,
                GeoCentroidZ     = dto.geoCentroidZ,
                LiteCentroidX    = dto.liteCentroidX,
                LiteCentroidZ    = dto.liteCentroidZ,
                LiteToGeoScale   = (dto.liteToGeoScale > 0.001f) ? dto.liteToGeoScale : 1f,
                ContourResampled = contourResampled,
                HeightShiftMode  = dto.heightShiftMode ?? string.Empty,
                _slopeGrid       = slopeGrid,
                _heightGrid      = heightGrid,
                _pinCandidates   = pins,
                _pinLabels       = labels,
            };
        }

        // JsonUtility DTOs. Field names match the locked schema exactly.

        [Serializable]
        private class GreenJsonDto
        {
            // ── v1 fields (unchanged) ──
            public int schemaVersion;
            public int holeNumber;
            public string sourceTag;
            public BoundsDto boundsMin;
            public BoundsDto boundsMax;
            public float cellSize;
            public int gridWidth;
            public int gridHeight;
            public string slopeGridBase64;
            public PinDto[] pinCandidates;
            public int defaultPinIndex;

            // ── v2 additive fields ──
            /// <summary>
            /// Float32 ×1 per cell, row-major [z][x], zero-mean relative metres.
            /// Absent or empty for v1 files — handled gracefully by <see cref="FromDto"/>.
            /// </summary>
            public string heightGridBase64;
            /// <summary>Reserved absolute Y datum (bake writes 0).</summary>
            public float heightDatumY;

            // ── v2 coordinate-mapping fields (added with Lite↔Geo transform support) ──
            // All default to 0 / 1.0 — legacy files get identity transform.
            public float geoCentroidX;
            public float geoCentroidZ;
            public float liteCentroidX;
            public float liteCentroidZ;
            public float liteToGeoScale;

            // ── iter-8 additive fields ──
            /// <summary>
            /// Version tag for the contour: "resampled-v1" when bake-green.mjs has resampled the
            /// contour to uniform arc-length spacing + Laplacian smooth (iter-8 D1).
            /// Absent for pre-iter-8 bakes.
            /// </summary>
            public string contourVersion;

            /// <summary>
            /// Resampled green contour in world XZ, produced by bake-green.mjs D1.
            /// Points have {x, z} fields matching the geo-pipeline world frame.
            /// Null / absent for pre-iter-8 bakes.
            /// </summary>
            public ContourPointDto[] contourResampled;

            /// <summary>
            /// Height shift mode: "min" (min-shifted, range [0, +R], iter-8 D3) or absent (zero-mean, pre-iter-8).
            /// Importer uses this to verify the expected convention before placing verts.
            /// </summary>
            public string heightShiftMode;
        }

        [Serializable]
        private class ContourPointDto
        {
            public float x;
            public float z;
        }

        [Serializable]
        private class BoundsDto
        {
            public float x;
            public float z;
        }

        [Serializable]
        private class PinDto
        {
            public float worldX;
            public float worldY;
            public float worldZ;
            public string label;
        }
    }
}
