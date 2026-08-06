// BotTreeProbe.cs — tree_aware_bot (Order 351)
// Flat-XZ trunk probe + trunk-clear re-aim ladder shared by VersusBot and BotDriver.
// Production-safe: NO #if UNITY_EDITOR — VersusBot ships in player builds.
// Read-side only: queries ITreeObstacleProvider interface in Golfin.Physics.
// TreeObstacleProvider, tree CSVs, collision profiles, and BallSimulation are untouched.
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Shared trunk-avoidance helper for VersusBot and BotDriver.
    ///
    /// Algorithm: windowed flat-XZ probe that marches the ball→pin line in ≤6 m steps,
    /// testing only the near window (first 35 m) and land window (last 35 m) where the ball
    /// is provably low. The apex band is skipped (assumed fly-over — no height model in v1).
    /// When a trunk is detected, a ladder identical in shape to VersusBot.TrySafeLanding tries
    /// same-yaw walk-back then ±10°/±20° retarget, rejecting any line that lands on Water.
    ///
    /// Null provider (treeless hole / lab flat-ground) → early-return false → strict no-op.
    /// </summary>
    public static class BotTreeProbe
    {
        // ── Constants (v1) ──────────────────────────────────────────────────────────────────
        // These mirror VersusBot's layup parameters for consistent bot behaviour.
        // Follow-up option: promote to bot_clubs.csv header like _slopeAimGain.
        private const float NearWindowM   = 35f;  // probe trunks within this many m of ball (rising, low)
        private const float LandWindowM   = 35f;  // probe trunks within this many m of target (descending, low)
        private const float ProbeStepM    = 6f;   // march step (< 10 m cell → consecutive 3×3 scans overlap; no missed trunk)
        private const float LayupStepM    = 8f;   // walk-back step for ladder (matches VersusBot LayupStep)
        private const float LayupMinDistM = 10f;  // min layup target distance (matches VersusBot LayupMinDist)

        // Retarget offsets — match VersusBot.OffsetDegrees { -10, +10, -20, +20 }.
        private static readonly float[] OffsetsDeg = { -10f, 10f, -20f, 20f };

        // ── Public entry ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Try to find a trunk-clear, surface-safe aim for the current shot.
        ///
        /// Returns <c>true</c> only when:
        ///   (a) the current straight line has a trunk in a probe window, AND
        ///   (b) a trunk-clear + surface-playable alternative was found.
        ///   On true, <paramref name="safeYaw"/> / <paramref name="safeDist"/> carry the result.
        ///
        /// Returns <c>false</c> (leave bot's line unchanged) when:
        ///   <paramref name="trees"/> is null, the straight line is already clear,
        ///   or nothing clear + playable was found (bot keeps original line + logs).
        /// </summary>
        /// <param name="trees">ctrl.GetTreeProvider() — null on treeless holes → no-op.</param>
        /// <param name="surfaces">ctrl.GetSurfaces() — prevents re-aim from landing in Water.</param>
        /// <param name="ball">Current ball world position (ball.y used as low-ball Y proxy).</param>
        /// <param name="aimYaw">Current intended aim in radians (Atan2(z,x) convention).</param>
        /// <param name="targetDist">Current intended carry distance (metres).</param>
        /// <param name="safeYaw">Output: trunk-clear yaw, or aimYaw if returning false.</param>
        /// <param name="safeDist">Output: trunk-clear distance, or targetDist if returning false.</param>
        public static bool TryFindTrunkClearAim(
            ITreeObstacleProvider trees,
            ISurfaceProvider      surfaces,
            Vector3 ball,
            float   aimYaw,
            float   targetDist,
            out float safeYaw,
            out float safeDist)
        {
            safeYaw  = aimYaw;
            safeDist = targetDist;

            // ── Guard: no trees on this hole / lab flat-ground. Strict no-op. ─────────────
            if (trees == null)
                return false;

            // ── Already clear: no detour needed. ─────────────────────────────────────────
            if (!LineHasTrunkInWindows(trees, ball, aimYaw, targetDist))
                return false;

            // ── Ladder: blocked — try walk-back on same yaw, then rotate ─────────────────

            // 1. Walk-back on the original yaw: shorten distance until clear + playable.
            for (float d = targetDist; d >= LayupMinDistM; d -= LayupStepM)
            {
                if (!LineHasTrunkInWindows(trees, ball, aimYaw, d) &&
                    IsPlayableLanding(surfaces, ball, aimYaw, d))
                {
                    safeYaw  = aimYaw;
                    safeDist = d;
                    Debug.Log($"[BotTreeProbe] Walk-back same yaw: trunk-clear dist={d:F0}m yaw={aimYaw * Mathf.Rad2Deg:F1}°");
                    return true;
                }
            }

            // 2. Rotate ±10°/±20° and walk-back on each.
            for (int i = 0; i < OffsetsDeg.Length; i++)
            {
                float offsetYaw = aimYaw + OffsetsDeg[i] * Mathf.Deg2Rad;
                for (float d = targetDist; d >= LayupMinDistM; d -= LayupStepM)
                {
                    if (!LineHasTrunkInWindows(trees, ball, offsetYaw, d) &&
                        IsPlayableLanding(surfaces, ball, offsetYaw, d))
                    {
                        safeYaw  = offsetYaw;
                        safeDist = d;
                        Debug.Log($"[BotTreeProbe] Rotate {OffsetsDeg[i]:+0;-0}°: trunk-clear dist={d:F0}m yaw={offsetYaw * Mathf.Rad2Deg:F1}°");
                        return true;
                    }
                }
            }

            // Nothing found — keep original line (may still clip trunk). Mirrors H2's fallback.
            Debug.Log("[BotTreeProbe] No trunk-clear + playable line found — keeping original aim (may clip trunk).");
            return false;
        }

        // ── bot_tree_error_recheck (Order 352) ─────────────────────────────────────────────

        /// <summary>
        /// Sample a 2b aim error whose resulting line is trunk-clear.
        ///
        /// Pure w.r.t. randomness: caller injects the sampler (UnityEngine.Random.Range in
        /// production, seeded System.Random in tests). trees == null → first sample accepted
        /// (treeless no-op, preserves current behaviour exactly). Returns false when all
        /// maxTries samples were trunk-blocked → caller must use deltaAimDeg = 0 (fires the
        /// already-validated pre-2b line).
        ///
        /// NOTE: rejection-sampling truncates the error distribution near tree corridors —
        /// that IS the feature. Power error and club noise are NOT re-checked here (accepted
        /// approximation per spec §2 Out; power changes carry, not aim).
        /// Production-safe: no #if UNITY_EDITOR.
        /// </summary>
        /// <param name="trees">GetTreeProvider() — null on treeless holes → no-op (treeless).</param>
        /// <param name="ball">Current ball world position.</param>
        /// <param name="safeYaw">The tree-aware aim yaw BEFORE 2b perturbation (radians).</param>
        /// <param name="carry">The club's modelled carry (probeCarry, updated by H2/tree re-aim).</param>
        /// <param name="aimErrorDegMax">Half-width of the aim-error bracket (degrees), from bot_difficulty.csv.</param>
        /// <param name="maxTries">Number of rejection-sampling attempts before fallback (VersusBot.MaxAimErrorResamples).</param>
        /// <param name="sampleRange">Sampler delegate: (min, max) → float. Pass UnityEngine.Random.Range in production.</param>
        /// <param name="deltaAimDeg">Output: the accepted aim-error delta in degrees (0 on false return).</param>
        /// <returns>true if a trunk-clear sample was found within maxTries attempts; false if all were blocked.</returns>
        public static bool TrySampleTrunkClearAimError(
            ITreeObstacleProvider trees, Vector3 ball, float safeYaw, float carry,
            float aimErrorDegMax, int maxTries,
            System.Func<float, float, float> sampleRange,
            out float deltaAimDeg)
        {
            for (int i = 0; i < maxTries; i++)
            {
                deltaAimDeg = sampleRange(-aimErrorDegMax, aimErrorDegMax);
                if (trees == null) return true;  // treeless hole: first sample accepted, no probe
                if (!LineHasTrunkInWindows(trees, ball, safeYaw + deltaAimDeg * Mathf.Deg2Rad, carry))
                    return true;
            }
            deltaAimDeg = 0f;
            return false;
        }

        // ── Helpers (public for sweep tooling / script-execute; logic is read-side only) ──────

        /// <summary>
        /// Returns true if the ball→target line has a trunk in either the near window
        /// (first NearWindowM m) or the landing window (last LandWindowM m).
        /// The apex band between the two windows is skipped (assumed fly-over, no height model).
        /// Both endpoints use ball.y as a flat ground-Y proxy.
        /// Public so the all-holes sweep (script-execute / Tests) can call it directly.
        /// </summary>
        public static bool LineHasTrunkInWindows(
            ITreeObstacleProvider trees,
            Vector3 ball, float yaw, float dist)
        {
            float nearEnd   = Mathf.Min(NearWindowM, dist);
            // landStart: start of the landing window. Clamped so nearEnd <= landStart <= dist.
            float landStart = Mathf.Max(dist - LandWindowM, nearEnd);

            float cosYaw = Mathf.Cos(yaw);
            float sinYaw = Mathf.Sin(yaw);
            float ballY  = ball.y;

            for (float d = 0f; d < dist; d += ProbeStepM)
            {
                float dEnd = Mathf.Min(d + ProbeStepM, dist);

                // Skip the apex band: sub-segment lies entirely between nearEnd and landStart.
                // The ball is high there → assume fly-over (no ballistic height model in v1).
                if (d >= nearEnd && dEnd <= landStart)
                    continue;

                float x0 = ball.x + d    * cosYaw;
                float z0 = ball.z + d    * sinYaw;
                float x1 = ball.x + dEnd * cosYaw;
                float z1 = ball.z + dEnd * sinYaw;

                // Both Y coords = ball.y (flat proxy). Accepted v1 limitation for holes with
                // big tee↔green elevation change; good enough on mostly-flat Lomond fairways.
                var p0 = new fp3(fp.FromFloat(x0), fp.FromFloat(ballY), fp.FromFloat(z0));
                var p1 = new fp3(fp.FromFloat(x1), fp.FromFloat(ballY), fp.FromFloat(z1));

                if (trees.TestSegment(p0, p1, out TreeHit hit) && hit.IsTrunk)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the projected landing point (ball + dist·yaw) is surface-playable
        /// and not Water/OB. Falls back to true (treat as playable) when surfaces is null,
        /// matching VersusBot's ProbeSurface null fallback — prevents silent water steering.
        /// </summary>
        public static bool IsPlayableLanding(
            ISurfaceProvider surfaces,
            Vector3 ball, float yaw, float dist)
        {
            if (surfaces == null) return true;

            float lx = ball.x + dist * Mathf.Cos(yaw);
            float lz = ball.z + dist * Mathf.Sin(yaw);
            try
            {
                SurfaceType s = surfaces.Classify(fp.FromFloat(lx), fp.FromFloat(lz));
                return IsPlayable(s) && !IsAvoid(s);
            }
            catch
            {
                return true; // classify error → treat as playable (graceful degradation)
            }
        }

        // Surface predicates — mirror VersusBot.IsAvoidSurface / IsPlayableSurface exactly.
        private static bool IsAvoid(SurfaceType s)    => s == SurfaceType.Water;
        private static bool IsPlayable(SurfaceType s) =>
            s == SurfaceType.Fairway     ||
            s == SurfaceType.Green       ||
            s == SurfaceType.GreenCollar ||
            s == SurfaceType.Semirough   ||
            s == SurfaceType.Rough       ||
            s == SurfaceType.Tee         ||
            s == SurfaceType.Sand;       // sand (bunker) playable but discouraged
    }
}
