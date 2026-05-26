// Phase 2 of Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md
// Pure math helpers for the green authoring editor tool.
// NO UnityEditor references allowed in this file.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Editor.GreenAuthoring
{
    /// <summary>
    /// Pure static math helpers for green authoring. No UnityEditor references.
    /// Used by <see cref="GreenTopologyEditor"/> and testable in EditMode tests.
    /// </summary>
    public static class GreenAuthoringMath
    {
        // ──────────────────────────────────────────────────────────────────────
        // Procedural slope field
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes a procedural slope field over the green's bounding rect.
        /// Heuristic:
        ///   1. 1.5% baseline along drain axis (mean of -∇heightmap at polygon vertices, normalized).
        ///   2. False-front bump: cells in front 20% of polygon (by drain axis) with heightmap
        ///      less than (polygon median - 0.3m) get magnitude clamped up to 2.5% in drain-axis direction.
        ///   3. Tier-break ridge: if (heightmap range across polygon > 0.5m), insert a 4% magnitude
        ///      band perpendicular to drain axis at the elevation midpoint, smoothed across ±2 cells.
        /// Cells outside the polygon are set to (0, 0, 0).
        /// Returns a freshly-allocated float[width*height*3] interleaved (dirX, dirZ, magPct).
        /// </summary>
        public static float[] ComputeProceduralSlopeField(
            IReadOnlyList<Vector2> polygonXZ,
            Vector2 boundsMin,
            Vector2 boundsMax,
            float cellSize,
            int gridWidth,
            int gridHeight,
            Func<Vector2, float> heightmapSampler)
        {
            var result = new float[gridWidth * gridHeight * 3];

            if (polygonXZ == null || polygonXZ.Count < 3 || cellSize <= 0f
                || gridWidth <= 0 || gridHeight <= 0 || heightmapSampler == null)
                return result;

            // Step 1: compute drain axis from polygon vertices.
            Vector2 drainAxis = ComputeDrainAxis(polygonXZ, heightmapSampler);

            // Step 2: collect heightmap values at polygon vertices for statistics.
            float hMin = float.MaxValue, hMax = float.MinValue;
            var vertexHeights = new float[polygonXZ.Count];
            for (int i = 0; i < polygonXZ.Count; i++)
            {
                float h = heightmapSampler(polygonXZ[i]);
                vertexHeights[i] = h;
                if (h < hMin) hMin = h;
                if (h > hMax) hMax = h;
            }
            float hRange = hMax - hMin;

            // Sort for median
            var sortedH = new float[vertexHeights.Length];
            Array.Copy(vertexHeights, sortedH, vertexHeights.Length);
            Array.Sort(sortedH);
            float hMedian = sortedH[sortedH.Length / 2];

            // Compute drain-axis projection range for the polygon vertices.
            float drainProjMin = float.MaxValue, drainProjMax = float.MinValue;
            for (int i = 0; i < polygonXZ.Count; i++)
            {
                float proj = Vector2.Dot(polygonXZ[i], drainAxis);
                if (proj < drainProjMin) drainProjMin = proj;
                if (proj > drainProjMax) drainProjMax = proj;
            }
            float drainProjRange = drainProjMax - drainProjMin;

            // Front 20% threshold (front = low drain projection = downhill "front" per drain axis).
            float frontThreshold = drainProjMin + drainProjRange * 0.20f;

            // Tier-break midpoint elevation
            float hMid = (hMin + hMax) * 0.5f;

            // Tier-break ridge axis = perpendicular to drain axis
            Vector2 ridgePerp = new Vector2(-drainAxis.y, drainAxis.x);

            // Project polygon vertices onto drain axis to find midpoint in drain space.
            float drainMid = (drainProjMin + drainProjMax) * 0.5f;

            // Step 3: fill each cell.
            for (int cellZ = 0; cellZ < gridHeight; cellZ++)
            {
                for (int cellX = 0; cellX < gridWidth; cellX++)
                {
                    // Cell center in world XZ.
                    Vector2 cellCenter = new Vector2(
                        boundsMin.x + (cellX + 0.5f) * cellSize,
                        boundsMin.y + (cellZ + 0.5f) * cellSize
                    );

                    int idx = (cellZ * gridWidth + cellX) * 3;

                    // Skip if outside polygon.
                    if (!IsPointInPolygon(cellCenter, polygonXZ))
                    {
                        result[idx]     = 0f;
                        result[idx + 1] = 0f;
                        result[idx + 2] = 0f;
                        continue;
                    }

                    float h = heightmapSampler(cellCenter);

                    // Start with 1.5% baseline along drain axis.
                    float dirX = drainAxis.x;
                    float dirZ = drainAxis.y;
                    float mag  = 1.5f;

                    // False-front bump: front 20% + heightmap below (median - 0.3m).
                    float drainProj = Vector2.Dot(cellCenter, drainAxis);
                    if (drainProj <= frontThreshold && h < (hMedian - 0.3f))
                    {
                        // Clamp up to 2.5%, keep drain-axis direction.
                        mag = Mathf.Max(mag, 2.5f);
                    }

                    // Tier-break ridge: if height range > 0.5m, insert 4% band at midpoint.
                    if (hRange > 0.5f)
                    {
                        // Determine which "drain band" this cell is in.
                        float bandDist = Mathf.Abs(drainProj - drainMid);
                        float cellsSmoothing = 2f * cellSize;

                        if (bandDist <= cellsSmoothing)
                        {
                            // Blend from 0 (at ±cellsSmoothing) to 1 (at midpoint).
                            float t = 1f - (bandDist / cellsSmoothing);
                            float ridgeMag = t * 4.0f;

                            // Ridge is perpendicular to drain axis.
                            // Determine sign: project cellCenter onto ridge direction,
                            // positive side gets positive direction.
                            float ridgeSign = Vector2.Dot(cellCenter, ridgePerp) >= 0f ? 1f : -1f;

                            // If the ridge contribution exceeds current mag, override direction.
                            if (ridgeMag > mag)
                            {
                                dirX = ridgePerp.x * ridgeSign;
                                dirZ = ridgePerp.y * ridgeSign;
                                mag  = ridgeMag;
                            }
                        }
                    }

                    // Clamp to 12% hard limit (Phase 6 hard rule 3).
                    mag = Mathf.Clamp(mag, 0f, 12f);

                    result[idx]     = dirX;
                    result[idx + 1] = dirZ;
                    result[idx + 2] = mag;
                }
            }

            return result;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Similarity transform
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Solves the similarity transform (uniform scale + rotation + translation) mapping the
        /// two image-space points to the two world-space points. Returns a Matrix4x4 (treating
        /// it as a 2x3 affine: col 0 = [a, c, 0, 0], col 1 = [b, d, 0, 0], col 3 = [tx, tz, 0, 0])
        /// such that worldXZ = M.MultiplyPoint(imageUV).
        /// Throws ArgumentException if the two image points are coincident.
        /// </summary>
        public static Matrix4x4 SolveSimilarityFrom2Points(
            Vector2 imageA, Vector2 imageB,
            Vector2 worldA, Vector2 worldB)
        {
            Vector2 imgDelta   = imageB - imageA;
            float imgLenSq = imgDelta.sqrMagnitude;

            if (imgLenSq < 1e-10f)
                throw new ArgumentException(
                    "The two image points are coincident — cannot compute similarity transform.");

            // Similarity transform: world = s * R * image + t
            // Two-point fit:
            //   scale = |worldDelta| / |imgDelta|
            //   rotation = angle(worldDelta) - angle(imgDelta)
            // In matrix form, given a = (s*cos(r)), b = (-s*sin(r)):
            //   worldA.x = a * imageA.x - b * imageA.y + tx
            //   worldA.y = b * imageA.x + a * imageA.y + ty

            Vector2 wDelta = worldB - worldA;

            // a = (wx*ix + wz*iz) / (ix^2 + iz^2)
            // b = (wz*ix - wx*iz) / (ix^2 + iz^2)
            float ix = imgDelta.x, iz = imgDelta.y;
            float wx = wDelta.x,   wz = wDelta.y;

            float a  = (wx * ix + wz * iz) / imgLenSq;
            float b  = (wz * ix - wx * iz) / imgLenSq;
            float tx = worldA.x - (a * imageA.x - b * imageA.y);
            float tz = worldA.y - (b * imageA.x + a * imageA.y);

            // Pack into Matrix4x4 as a 2x3 affine in XZ.
            // Row 0: world.x = a * img.x + (-b) * img.y + tx
            // Row 1: world.z = b * img.x + a   * img.y + tz
            var m = Matrix4x4.identity;
            m.m00 = a;   m.m01 = -b;  m.m03 = tx;
            m.m10 = b;   m.m11 =  a;  m.m13 = tz;
            m.m20 = 0f;  m.m21 =  0f; m.m23 = 0f;
            m.m30 = 0f;  m.m31 =  0f; m.m33 = 1f;
            return m;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Point-in-polygon
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Point-in-polygon test (XZ-plane, ray casting). Polygon ring need not be closed
        /// (last vertex != first); function handles both.
        /// </summary>
        public static bool IsPointInPolygon(Vector2 pointXZ, IReadOnlyList<Vector2> polygonXZ)
        {
            if (polygonXZ == null || polygonXZ.Count < 3) return false;

            bool inside = false;
            int n = polygonXZ.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = polygonXZ[i].x, zi = polygonXZ[i].y;
                float xj = polygonXZ[j].x, zj = polygonXZ[j].y;

                bool intersect = ((zi > pointXZ.y) != (zj > pointXZ.y)) &&
                                 (pointXZ.x < (xj - xi) * (pointXZ.y - zi) / (zj - zi + 1e-9f) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the drain axis: mean of -∇heightmap at polygon vertices, normalized.
        /// The negative gradient points downhill. If the result is near-zero (flat green),
        /// returns (0, 1) as a safe default (along +Z).
        /// </summary>
        private static Vector2 ComputeDrainAxis(
            IReadOnlyList<Vector2> polygonXZ,
            Func<Vector2, float> heightmapSampler)
        {
            const float Epsilon = 0.01f; // sample offset for finite difference

            Vector2 sumGrad = Vector2.zero;
            for (int i = 0; i < polygonXZ.Count; i++)
            {
                Vector2 p = polygonXZ[i];
                float h0 = heightmapSampler(p);
                float hx = heightmapSampler(new Vector2(p.x + Epsilon, p.y));
                float hz = heightmapSampler(new Vector2(p.x, p.y + Epsilon));

                // ∇h = (dh/dx, dh/dz); drain = -∇h (downhill direction).
                float dhdx = (hx - h0) / Epsilon;
                float dhdz = (hz - h0) / Epsilon;

                sumGrad += new Vector2(-dhdx, -dhdz);
            }

            Vector2 avg = sumGrad / polygonXZ.Count;
            float len = avg.magnitude;
            if (len < 1e-6f)
                return new Vector2(0f, 1f); // flat green fallback

            return avg / len; // normalized unit vector
        }
    }
}
