// Phase 2 of Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md
// Serializes authored green data to green.json atomically.
// Internal DTO matches Phase 1's GreenTopology.GreenJsonDto field-for-field (verified by T1 round-trip test).

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Golfin.Course.Runtime;

namespace Golfin.Editor.GreenAuthoring
{
    /// <summary>
    /// Serializes authored green topology state to JSON and writes atomically to
    /// <c>Assets/Resources/HoleData/Hole_NN/green.json</c>.
    ///
    /// <para>
    /// Pipeline: validate → ToJson → temp write → File.Replace → AssetDatabase.ImportAsset
    /// → GreenTopologyCache.Invalidate. Mirrors the <c>save_layer_reactive_foundation</c> pattern.
    /// </para>
    /// </summary>
    public static class GreenJsonWriter
    {
        // ──────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Serializes the green's authored state to JSON and writes it atomically
        /// to <c>Assets/Resources/HoleData/Hole_NN/green.json</c>.
        /// Returns <c>true</c> on success; emits <see cref="Debug.LogError"/> + returns <c>false</c>
        /// on validation failure or I/O error.
        /// </summary>
        public static bool SaveToResources(
            int holeNumber,
            string sourceTag,
            Vector2 boundsMin, Vector2 boundsMax,
            float cellSize, int gridWidth, int gridHeight,
            float[] slopeGrid,
            IReadOnlyList<(Vector3 world, string label)> pinCandidates,
            int defaultPinIndex,
            EditorBackdropMetadata backdrop /* nullable */)
        {
            // ── Validation (Q8) ──────────────────────────────────────────────
            if (gridWidth <= 0 || gridHeight <= 0)
            {
                Debug.LogError($"[GreenJsonWriter] Validation failed: gridWidth={gridWidth}, gridHeight={gridHeight} must both be > 0.");
                return false;
            }

            int expectedFloats = gridWidth * gridHeight * 3;
            if (slopeGrid == null || slopeGrid.Length != expectedFloats)
            {
                Debug.LogError($"[GreenJsonWriter] Validation failed: slopeGrid.Length={slopeGrid?.Length ?? 0}, expected {expectedFloats} (gridWidth={gridWidth} * gridHeight={gridHeight} * 3).");
                return false;
            }

            if (pinCandidates == null || pinCandidates.Count == 0)
            {
                Debug.LogError($"[GreenJsonWriter] Validation failed: pinCandidates.Count=0; at least 1 required.");
                return false;
            }

            if (defaultPinIndex < 0 || defaultPinIndex >= pinCandidates.Count)
            {
                Debug.LogError($"[GreenJsonWriter] Validation failed: defaultPinIndex={defaultPinIndex} out of range [0, {pinCandidates.Count - 1}].");
                return false;
            }

            // Pin candidates inside bounds (0.5m tolerance).
            const float PinTolerance = 0.5f;
            for (int i = 0; i < pinCandidates.Count; i++)
            {
                var pin = pinCandidates[i];
                if (pin.world.x < boundsMin.x - PinTolerance || pin.world.x > boundsMax.x + PinTolerance ||
                    pin.world.z < boundsMin.y - PinTolerance || pin.world.z > boundsMax.y + PinTolerance)
                {
                    Debug.LogError($"[GreenJsonWriter] Validation failed: pin[{i}] '{pin.label}' at ({pin.world.x}, {pin.world.z}) is outside bounds rect ({boundsMin.x}, {boundsMin.y}) – ({boundsMax.x}, {boundsMax.y}) with 0.5m tolerance.");
                    return false;
                }
            }

            // Magnitude clamp check.
            for (int idx = 2; idx < slopeGrid.Length; idx += 3)
            {
                if (slopeGrid[idx] > 12f)
                {
                    Debug.LogError($"[GreenJsonWriter] Validation failed: slopeGrid[{idx}] magnitudePercent={slopeGrid[idx]:F2} exceeds 12% hard limit.");
                    return false;
                }
            }

            if (cellSize <= 0f)
            {
                Debug.LogError($"[GreenJsonWriter] Validation failed: cellSize={cellSize} must be > 0.");
                return false;
            }

            // ── Serialize ───────────────────────────────────────────────────
            string json = ToJson(holeNumber, sourceTag, boundsMin, boundsMax,
                                 cellSize, gridWidth, gridHeight, slopeGrid,
                                 pinCandidates, defaultPinIndex, backdrop);

            // ── Atomic write (Q5) ────────────────────────────────────────────
            string holeFolder  = $"Assets/Resources/HoleData/Hole_{holeNumber:D2}";
            string assetPath   = $"{holeFolder}/green.json";
            string tmpPath     = $"{holeFolder}/green.json.tmp";
            string fullPath    = Path.GetFullPath(assetPath);
            string fullTmpPath = Path.GetFullPath(tmpPath);

            try
            {
                // Ensure directory exists (e.g. for Hole_99 test fixture).
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                File.WriteAllText(fullTmpPath, json);
                File.Replace(fullTmpPath, fullPath, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GreenJsonWriter] I/O error writing {assetPath}: {ex.Message}");
                return false;
            }

            // ── Notify Unity of changed asset + bust the topology cache ──────
            AssetDatabase.ImportAsset(assetPath);
            GreenTopologyCache.Invalidate(holeNumber);

            Debug.Log($"[GreenJsonWriter] Saved Hole_{holeNumber:D2} green.json (sourceTag={sourceTag}, grid={gridWidth}x{gridHeight}, pins={pinCandidates.Count}).");
            return true;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Serialization helpers
        // ──────────────────────────────────────────────────────────────────────

        private static string ToJson(
            int holeNumber,
            string sourceTag,
            Vector2 boundsMin, Vector2 boundsMax,
            float cellSize, int gridWidth, int gridHeight,
            float[] slopeGrid,
            IReadOnlyList<(Vector3 world, string label)> pinCandidates,
            int defaultPinIndex,
            EditorBackdropMetadata backdrop)
        {
            // Encode float[] to base64 bytes (same as Phase 1 reads: Buffer.BlockCopy little-endian).
            int floatCount = slopeGrid.Length;
            byte[] bytes = new byte[floatCount * sizeof(float)];
            Buffer.BlockCopy(slopeGrid, 0, bytes, 0, bytes.Length);
            string base64 = Convert.ToBase64String(bytes);

            // Build DTO.
            var dto = new GreenJsonDto
            {
                schemaVersion  = GreenTopology.CurrentSchemaVersion,
                holeNumber     = holeNumber,
                sourceTag      = sourceTag ?? string.Empty,
                boundsMin      = new BoundsDto { x = boundsMin.x, z = boundsMin.y },
                boundsMax      = new BoundsDto { x = boundsMax.x, z = boundsMax.y },
                cellSize       = cellSize,
                gridWidth      = gridWidth,
                gridHeight     = gridHeight,
                slopeGridBase64 = base64,
                defaultPinIndex = defaultPinIndex,
                pinCandidates  = new PinDto[pinCandidates.Count],
            };

            for (int i = 0; i < pinCandidates.Count; i++)
            {
                var (w, lbl) = pinCandidates[i];
                dto.pinCandidates[i] = new PinDto
                {
                    worldX = w.x,
                    worldY = w.y,
                    worldZ = w.z,
                    label  = lbl ?? string.Empty,
                };
            }

            // JsonUtility doesn't support null fields, so we serialize with
            // the backdrop embedded only when non-null.
            // We hand-build the JSON so we can include the editorBackdrop object
            // only when present (JsonUtility would serialize it as empty {}).
            string baseJson = JsonUtility.ToJson(dto, true);

            if (backdrop != null)
            {
                // Serialize backdrop as a sub-object using JsonUtility.
                var backdropDto = new BackdropDto
                {
                    imageAssetPath = backdrop.imageAssetPath ?? string.Empty,
                    imagePoints    = backdrop.imagePoints != null
                                        ? Array.ConvertAll(backdrop.imagePoints, p => new Vec2Dto { u = p.x, v = p.y })
                                        : Array.Empty<Vec2Dto>(),
                    worldPoints    = backdrop.worldPoints != null
                                        ? Array.ConvertAll(backdrop.worldPoints, p => new Vec2Dto { u = p.x, v = p.y })
                                        : Array.Empty<Vec2Dto>(),
                };
                string backdropJson = JsonUtility.ToJson(backdropDto, false);

                // Inject into the base JSON before the final closing brace.
                int lastBrace = baseJson.LastIndexOf('}');
                if (lastBrace >= 0)
                    baseJson = baseJson.Substring(0, lastBrace) + $",\n  \"editorBackdrop\": {backdropJson}\n}}";
            }

            return baseJson;
        }

        // ──────────────────────────────────────────────────────────────────────
        // DTOs (private — field names match Phase 1's GreenTopology.GreenJsonDto exactly)
        // ──────────────────────────────────────────────────────────────────────

        [Serializable]
        private class GreenJsonDto
        {
            public int    schemaVersion;
            public int    holeNumber;
            public string sourceTag;
            public BoundsDto boundsMin;
            public BoundsDto boundsMax;
            public float  cellSize;
            public int    gridWidth;
            public int    gridHeight;
            public string slopeGridBase64;
            public PinDto[] pinCandidates;
            public int    defaultPinIndex;
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
            public float  worldX;
            public float  worldY;
            public float  worldZ;
            public string label;
        }

        [Serializable]
        private class BackdropDto
        {
            public string    imageAssetPath;
            public Vec2Dto[] imagePoints;
            public Vec2Dto[] worldPoints;
        }

        [Serializable]
        private class Vec2Dto
        {
            public float u;
            public float v;
        }
    }

    /// <summary>
    /// Optional editor backdrop metadata embedded in <c>green.json</c> under the
    /// <c>editorBackdrop</c> key. Ignored at runtime (<see cref="Golfin.Course.Runtime.GreenTopology"/>
    /// uses <c>JsonUtility</c> which tolerates unknown fields).
    /// </summary>
    [Serializable]
    public class EditorBackdropMetadata
    {
        /// <summary>Relative path to the PNG asset used as a backdrop (relative to repo root).</summary>
        public string imageAssetPath;

        /// <summary>Two image-space UV anchor points for the similarity transform.</summary>
        public Vector2[] imagePoints;   // length 2

        /// <summary>Corresponding world XZ anchor points (Vector2.x = world X, Vector2.y = world Z).</summary>
        public Vector2[] worldPoints;   // length 2
    }
}
