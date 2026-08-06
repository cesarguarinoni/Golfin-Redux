// BotTreeProbe.cs — tree_aware_bot (Order 351) + canopy_avoidance_v2
// Flat-XZ trunk probe + trunk-clear re-aim ladder shared by VersusBot and BotDriver.
// canopy_avoidance_v2: adds ApexForCarry, CountCanopyContacts, TrySampleTreeAwareAimError.
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

        // ── Constants (v2 — canopy_avoidance_v2) ───────────────────────────────────────────
        // Apex per club (metres), measured from BallSimulation.Simulate(..., AeroConfig.Default)
        // at full power. See SPEC §4.1 and the drift-guard test ApexDriftGuard_TableMatchesSim.
        // NOTE: scaled linearly with carry for part-power shots (first-order approximation).
        // Canopy is a soft cost, so a modest height error changes a tie-break, never playability.
        // Club indices: 0=Driver, 1=Wood(iron7), 2=Iron/Wedge, 3=Putter.
        // VersusBot club bands: >200m→driver(0), 80-200m→iron7(1), 20-80m→wedge(2), ≤20m→putter(3).
        internal static readonly float[] ApexAtFullCarry = { 7.92f, 5.29f, 14.42f, 0f };
        // Full carry (m) at full power, matching the SPEC §1 table.
        internal static readonly float[] FullCarryForClub = { 132.9f, 109.1f, 108.6f, 0f };
        // Iron apex is actually 11.45 m (launch 20°), but the bot uses wedge (index 2) for 20-80m
        // and iron7 (index 1) for 80-200m. The SPEC apex table lists iron at 11.45 but VersusBot
        // never uses a dedicated "iron" club slot — index 1 = "iron7" (5.29 m apex). Index 2
        // is used for both A.Wedge (spec apex 14.42) and P.Wedge; use 14.42 (larger → more conservative).

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

        // ── canopy_avoidance_v2: new API ────────────────────────────────────────────────────

        /// <summary>
        /// Modelled apex height (m) for a given club index and actual carry distance.
        /// Scales linearly with carry as a first-order approximation (canopy is a soft
        /// cost so a modest height error only shifts a tie-break, never playability).
        /// club index: 0=Driver, 1=iron7, 2=wedge, 3=Putter (always returns 0).
        /// </summary>
        public static float ApexForCarry(int club, float carry)
        {
            if (club < 0 || club >= ApexAtFullCarry.Length) return 0f;
            float fullApex  = ApexAtFullCarry[club];
            float fullCarry = FullCarryForClub[club];
            if (fullCarry <= 0f || fullApex <= 0f) return 0f;
            // Scale linearly with carry (first-order approximation — see SPEC §4.1 NOTE).
            return fullApex * (carry / fullCarry);
        }

        /// <summary>
        /// March the FULL ball→target line (no apex-band skip) at modelled trajectory height
        /// and count how many 6 m steps enter a canopy. Trunk hits are reported separately
        /// (the count only covers IsTrunk==false hits). Treeless (trees==null) → 0.
        ///
        /// Height model: parabola h(d) = 4 * apex * t * (1-t), t = d/carry.
        /// Both probe endpoints use ball.y as the ground-Y reference.
        ///
        /// Public so the measurement sweep (script-execute / tests) can call it directly.
        /// </summary>
        /// <param name="trees">Tree obstacle provider. null → 0 (treeless).</param>
        /// <param name="ball">Current ball world position (ball.y = ground-Y reference).</param>
        /// <param name="yaw">Aim yaw in radians.</param>
        /// <param name="carry">Modelled carry distance (m).</param>
        /// <param name="apex">Apex height (m) from ApexForCarry.</param>
        /// <returns>Number of probe steps that hit a canopy (IsTrunk==false).</returns>
        public static int CountCanopyContacts(
            ITreeObstacleProvider trees, Vector3 ball, float yaw, float carry, float apex)
        {
            if (trees == null) return 0;

            float cosYaw = Mathf.Cos(yaw);
            float sinYaw = Mathf.Sin(yaw);
            float ballY  = ball.y;
            int   count  = 0;

            for (float d = 0f; d < carry; d += ProbeStepM)
            {
                float dEnd = Mathf.Min(d + ProbeStepM, carry);

                // Heights at mid-distance of this step (use midpoint for representative height).
                float dMid  = (d + dEnd) * 0.5f;
                float t0    = (carry > 0f) ? dMid / carry : 0f;
                float h0    = 4f * apex * t0 * (1f - t0);

                float x0 = ball.x + d    * cosYaw;
                float z0 = ball.z + d    * sinYaw;
                float x1 = ball.x + dEnd * cosYaw;
                float z1 = ball.z + dEnd * sinYaw;
                float y0 = ballY + h0;
                float y1 = ballY + h0;   // use same representative height for both endpoints

                var p0 = new fp3(fp.FromFloat(x0), fp.FromFloat(y0), fp.FromFloat(z0));
                var p1 = new fp3(fp.FromFloat(x1), fp.FromFloat(y1), fp.FromFloat(z1));

                if (trees.TestSegment(p0, p1, out TreeHit hit) && !hit.IsTrunk)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// canopy_avoidance_v2: scored sampler that supersedes TrySampleTrunkClearAimError
        /// at the D2 call site in VersusBot.
        ///
        /// Trunk = hard reject (unchanged from Order 352).
        /// Canopy = soft cost (scored tie-break): among trunk-clear samples, prefer the
        /// fewest canopy contacts, tie-breaking to the smallest |deltaAimDeg| so the miss
        /// model is preserved when canopy cost is equal.
        ///
        /// trees==null → first sample accepted, single draw (treeless no-op, identical to
        /// TrySampleTrunkClearAimError with null provider).
        /// DebugDisableCanopyPreference==true → reproduces Order 352 exactly (first trunk-
        /// clear sample returned regardless of canopy cost).
        ///
        /// Returns false only when ALL samples were TRUNK-blocked.
        /// On false: deltaAimDeg==0, canopyContacts==-1.
        /// </summary>
        /// <param name="trees">GetTreeProvider() — null on treeless holes → no-op.</param>
        /// <param name="ball">Current ball world position.</param>
        /// <param name="safeYaw">Tree-aware aim yaw BEFORE 2b perturbation (radians).</param>
        /// <param name="carry">Modelled carry (probeCarry).</param>
        /// <param name="apex">Apex height (m) from ApexForCarry(club, carry).</param>
        /// <param name="aimErrorDegMax">Half-width of the aim-error bracket (degrees).</param>
        /// <param name="maxTries">Rejection-sampling attempts before fallback.</param>
        /// <param name="sampleRange">Sampler delegate: (min, max) → float.</param>
        /// <param name="disableCanopyPreference">
        /// When true, behaves identically to TrySampleTrunkClearAimError (Order 352).
        /// </param>
        /// <param name="deltaAimDeg">Output: accepted aim-error delta in degrees (0 on false).</param>
        /// <param name="canopyContacts">Output: canopy contacts on the accepted line (-1 on false).</param>
        /// <returns>true if a trunk-clear sample found; false if all were trunk-blocked.</returns>
        public static bool TrySampleTreeAwareAimError(
            ITreeObstacleProvider trees, Vector3 ball, float safeYaw, float carry, float apex,
            float aimErrorDegMax, int maxTries,
            System.Func<float, float, float> sampleRange,
            bool disableCanopyPreference,
            out float deltaAimDeg, out int canopyContacts)
        {
            // Fast path: treeless hole — identical to null-path of TrySampleTrunkClearAimError.
            if (trees == null)
            {
                deltaAimDeg   = sampleRange(-aimErrorDegMax, aimErrorDegMax);
                canopyContacts = 0;
                return true;
            }

            // DebugDisableCanopyPreference: reproduce Order 352 exactly.
            if (disableCanopyPreference)
            {
                bool ok = TrySampleTrunkClearAimError(
                    trees, ball, safeYaw, carry, aimErrorDegMax, maxTries, sampleRange,
                    out deltaAimDeg);
                canopyContacts = ok ? 0 : -1;
                return ok;
            }

            // Scored sampler: collect all trunk-clear survivors, pick lowest canopy cost.
            // Storage: small fixed arrays (maxTries ≤ 5 in production; no heap alloc).
            float bestDelta        = 0f;
            int   bestCanopy       = int.MaxValue;
            bool  foundSurvivor    = false;

            for (int i = 0; i < maxTries; i++)
            {
                float delta = sampleRange(-aimErrorDegMax, aimErrorDegMax);
                float yaw   = safeYaw + delta * Mathf.Deg2Rad;

                // Hard reject: trunk hit.
                if (LineHasTrunkInWindows(trees, ball, yaw, carry))
                    continue;

                // Soft cost: count canopy contacts on the FULL line at modelled height.
                int contacts = CountCanopyContacts(trees, ball, yaw, carry, apex);

                // Accept if: fewer canopy contacts, OR same contacts + smaller |delta|.
                if (!foundSurvivor ||
                    contacts < bestCanopy ||
                    (contacts == bestCanopy && Mathf.Abs(delta) < Mathf.Abs(bestDelta)))
                {
                    bestDelta     = delta;
                    bestCanopy    = contacts;
                    foundSurvivor = true;
                }
            }

            if (foundSurvivor)
            {
                deltaAimDeg   = bestDelta;
                canopyContacts = bestCanopy;
                return true;
            }

            // All samples trunk-blocked.
            deltaAimDeg   = 0f;
            canopyContacts = -1;
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
