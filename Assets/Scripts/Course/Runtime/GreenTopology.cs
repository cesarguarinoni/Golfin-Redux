// Phase 1 of Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md
// Locked 2026-05-26 by Architect ↔ Cesar (this session).
// Read-only runtime view over per-green authored slope + pin data.
// Authoring tool lives in Phase 2 (Golfin.Editor.GreenAuthoring).
// Physics consumer lands in Phase 6 (BallSimulation.RunPuttPhase / RunRollPhase).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Course.Runtime
{
    /// <summary>
    /// In-memory representation of one green's authored topology.
    /// Loaded from <c>Assets/Resources/HoleData/Hole_XX/green.json</c> via <see cref="LoadFromResources"/>.
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
    /// cells contribute zero force without a separate "is active" check.
    /// </para>
    ///
    /// <para>
    /// Use <see cref="GreenTopologyCache.GetForHole"/> in hot paths — direct
    /// <see cref="LoadFromResources"/> calls re-decode the base64 grid each time.
    /// </para>
    /// </summary>
    public sealed class GreenTopology
    {
        /// <summary>Current schema version emitted by the authoring tool. Bump on breaking changes.</summary>
        public const int CurrentSchemaVersion = 1;

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

        // Row-major [z][x] float32 triples (dirX, dirZ, magPct).
        // Length == GridWidth * GridHeight * 3. Triple-stride = 3.
        private float[]  _slopeGrid;
        private Vector3[] _pinCandidates;
        private string[]  _pinLabels;

        private GreenTopology() { }

        /// <summary>
        /// Loads <c>Assets/Resources/HoleData/Hole_NN/green.json</c> for the given hole, or returns
        /// <c>null</c> when (a) no file present, (b) JSON parse failed, (c) schemaVersion mismatch,
        /// or (d) slope grid byte length disagrees with the declared <c>gridWidth × gridHeight × 3</c>.
        /// All failure modes log via <see cref="Debug.LogError"/>. The bare TextAsset is unloaded
        /// from Resources immediately after parse; only the decoded float grid is retained.
        /// </summary>
        public static GreenTopology LoadFromResources(int holeNumber)
        {
            string path = $"HoleData/Hole_{holeNumber:D2}/green";
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
            int expectedFloats = dto.gridWidth * dto.gridHeight * 3;
            if (expectedFloats <= 0)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: invalid grid size {dto.gridWidth}x{dto.gridHeight} at Resources/{resourcePath}.");
                return null;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(dto.slopeGridBase64 ?? string.Empty);
            }
            catch (FormatException ex)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: slopeGridBase64 decode failed at Resources/{resourcePath} — {ex.Message}");
                return null;
            }

            int expectedBytes = expectedFloats * sizeof(float);
            if (bytes.Length != expectedBytes)
            {
                Debug.LogError($"[GreenTopology] Hole {holeNumber:D2}: slope grid byte length {bytes.Length} != expected {expectedBytes} (grid {dto.gridWidth}x{dto.gridHeight} * 3 floats) at Resources/{resourcePath}.");
                return null;
            }

            var slopeGrid = new float[expectedFloats];
            Buffer.BlockCopy(bytes, 0, slopeGrid, 0, expectedBytes);

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
                DefaultPinIndex  = defaultIdx,
                _slopeGrid       = slopeGrid,
                _pinCandidates   = pins,
                _pinLabels       = labels,
            };
        }

        // JsonUtility DTOs. Field names match the locked schema exactly.

        [Serializable]
        private class GreenJsonDto
        {
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
