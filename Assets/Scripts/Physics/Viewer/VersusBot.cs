// VersusBot.cs — versus_bot_hardening (H1 + H2 + H3) + versus_bot_difficulty (2b)
// H1: calibrated club/power from bot_clubs.csv (production stat path carry table).
// H2: proactive landing probe (Water avoid; Sand discouraged) + layup + ±10°/±20° retarget
//     + reactive OBReason bias.
// H3: PutterGreenReader.TryGetSlopeAt slope-break aim offset + power nudge (CSV-gain).
// 2b: post-decision error injection from bot_difficulty.csv bracket (level-mapped bands).
//
// Constraints:
//   - No #if UNITY_EDITOR, no ForceShotCompleteForBot.
//   - Drives production ShotController external-drag path.
//   - Bot lives in Golfin.Physics.Viewer (internal BallSM / SetCameraYawRadians access).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.HUD;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Runtime bot opponent for 1v1 versus mode (Phase 2a hardened in versus_bot_hardening;
    /// Phase 2b difficulty model in versus_bot_difficulty).
    /// Drives shots via the PRODUCTION ShotController external-drag path.
    /// </summary>
    public class VersusBot : MonoBehaviour
    {
        [SerializeField] PhysicsLabController _controller;
        [SerializeField] ShotController       _shotController;
        [SerializeField] PutterGreenReader    _greenReader;

        // ── 2b: debug level override (inspector) ──────────────────────────────
        // -1 = off (use MatchContext.Players[1].Level). Set to 1–240 to force a
        // specific difficulty bracket for capture / testing. No #if UNITY_EDITOR —
        // field is production-safe (simply a floor/override on the level read).
        [SerializeField] public int DebugLevelOverride = -1;

        // ── bot_tree_error_recheck: suppress tree re-check on 2b aim error ──
        // false (default) = aim error is rejection-sampled until trunk-clear.
        // true = restores pre-fix behaviour byte-for-byte (unchecked random sample).
        // No #if UNITY_EDITOR — field ships in player builds (production-safe).
        [SerializeField] public bool DebugDisableTreeRecheck = false;

        // ── H2: reactive OB state ──────────────────────────────────────────
        // Written by VersusMatchController or caller after each shot resolves.
        // Public so VersusMatchController can set it if desired; VersusBot also
        // clears it after applying the bias (one-shot correction).
        public OBReason? LastOBReason { get; set; }

        // ── H3: green-read CSV config ──────────────────────────────────────
        // _slopeAimGain: yaw-offset multiplier: aimOffset = -slopeX * dist * gain (radians).
        // Tuning: target ~1–3° at 5% grade, 8m: 0.05*8*gain = 0.02–0.05 rad → gain ≈ 0.05–0.125.
        // Chosen value 0.125 gives 0.05 rad ≈ 2.9° on 5% slope at 8m (was 1.5 → 0.6 rad ≈ 34°, ~10× too large).
        // CSV-tunable: add "# slope_aim_gain=0.125" header line to bot_clubs.csv to override at runtime.
        private float _slopeAimGain  = 0.125f;  // yaw-offset multiplier: offset = slopeX*dist*gain
        private float _slopePowerGain = 0.08f;  // power nudge: nudge += slopeZ*dist*gain (positive=uphill)

        // ── H1: carry table ────────────────────────────────────────────────
        private struct CarryRow { public string club; public float power01; public float carry; }
        private static List<CarryRow> _carryTable;
        private static bool           _tableLoaded;

        // Club name map matching calibration harness (0=driver, 1=iron7, 2=wedge, 3=putter).
        private static readonly string[] ClubNames = { "driver", "iron7", "wedge", "putter" };

        // ── H2: layup config ───────────────────────────────────────────────
        private const float LayupStep      = 8f;   // m per layup step
        private const float LayupMinDist   = 10f;  // m minimum layup target
        private const int   RetargetAngles = 4;    // ±10°, ±20°
        private static readonly float[] OffsetDegrees = { -10f, 10f, -20f, 20f };

        // ── bot_tree_error_recheck: 2b aim-error rejection-sampling ────────
        // Max samples before falling back to deltaAimDeg=0 (fires the pre-2b validated line).
        private const int MaxAimErrorResamples = 5;

        // ── 2b: difficulty bracket ─────────────────────────────────────────
        private struct DifficultyBracket
        {
            public int   minLevel;
            public float aimErrorDegMax;
            public float powerErrorMax;
            public float clubNoiseChance;
        }
        private static List<DifficultyBracket> _difficultyTable;
        private static bool                    _difficultyLoaded;

        // Resolved once per match (-1 sentinel = not yet resolved; domain-reload-safe).
        // Using int sentinel instead of bool guard avoids the zero-init domain-reload trap.
        private int               _resolvedLevel   = -1;
        private DifficultyBracket _resolvedBracket;

        void Awake()
        {
            if (_controller    == null) _controller    = FindObjectOfType<PhysicsLabController>();
            if (_shotController == null) _shotController = FindObjectOfType<ShotController>();
            if (_greenReader   == null) _greenReader   = FindObjectOfType<PutterGreenReader>();
        }

        // ── H1: carry table loader ─────────────────────────────────────────

        private void EnsureTableLoaded()
        {
            if (_tableLoaded) return;
            _tableLoaded = true;
            _carryTable  = new List<CarryRow>(100);

            var csv = Resources.Load<TextAsset>("Data/bot_clubs");
            if (csv == null)
            {
                Debug.LogWarning("[VersusBot] bot_clubs.csv not found — falling back to legacy SelectShot.");
                return;
            }

            foreach (var rawLine in csv.text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("club,"))
                    continue;
                var p = line.Split(',');
                if (p.Length < 3) continue;
                if (!float.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float pow)) continue;
                if (!float.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float carry)) continue;
                _carryTable.Add(new CarryRow { club = p[0].Trim(), power01 = pow, carry = carry });
            }
            Debug.Log($"[VersusBot] Carry table loaded: {_carryTable.Count} rows.");
        }

        // ── 2b: difficulty table loader ─────────────────────────────────────

        private void EnsureDifficultyLoaded()
        {
            if (_difficultyLoaded) return;
            _difficultyLoaded = true;
            _difficultyTable  = new List<DifficultyBracket>(8);

            var csv = Resources.Load<TextAsset>("Data/bot_difficulty");
            if (csv == null)
            {
                Debug.LogWarning("[VersusBot] bot_difficulty.csv not found — zero-error fallback (hardened baseline).");
                return;
            }

            foreach (var rawLine in csv.text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("minLevel"))
                    continue;
                var p = line.Split(',');
                if (p.Length < 4) continue;
                if (!int.TryParse(p[0].Trim(), out int minLevel)) continue;
                if (!float.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float aimErr)) continue;
                if (!float.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float powErr)) continue;
                if (!float.TryParse(p[3].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float clubNoise)) continue;
                _difficultyTable.Add(new DifficultyBracket
                {
                    minLevel       = minLevel,
                    aimErrorDegMax = aimErr,
                    powerErrorMax  = powErr,
                    clubNoiseChance = clubNoise
                });
            }
            // Sort ascending by minLevel so bracket lookup is correct.
            _difficultyTable.Sort((a, b) => a.minLevel.CompareTo(b.minLevel));
            Debug.Log($"[VersusBot] Difficulty table loaded: {_difficultyTable.Count} brackets.");
        }

        /// <summary>
        /// Resolve the difficulty bracket once per match and cache it.
        /// Uses DebugLevelOverride if set (>=0), otherwise MatchContext.Players[1].Level.
        /// Logs the resolved bracket once.
        /// </summary>
        private DifficultyBracket ResolveBracket()
        {
            EnsureDifficultyLoaded();

            int level = DebugLevelOverride >= 0
                ? DebugLevelOverride
                : MatchContext.Players[1].Level;

            // Already resolved for this level? Return cached.
            if (_resolvedLevel == level)
                return _resolvedBracket;

            _resolvedLevel = level;

            // Zero-error fallback if table is empty.
            if (_difficultyTable == null || _difficultyTable.Count == 0)
            {
                _resolvedBracket = new DifficultyBracket { minLevel = 0, aimErrorDegMax = 0f, powerErrorMax = 0f, clubNoiseChance = 0f };
                Debug.LogWarning("[VersusBot] Difficulty table empty — zero-error fallback.");
                return _resolvedBracket;
            }

            // Find highest minLevel <= level.
            DifficultyBracket bracket = _difficultyTable[0]; // default to lowest
            foreach (var b in _difficultyTable)
            {
                if (b.minLevel <= level)
                    bracket = b;
                else
                    break; // sorted ascending — no need to continue
            }

            _resolvedBracket = bracket;
            Debug.Log($"[VersusBot] Difficulty: level={level} bracket(minLevel={bracket.minLevel}) " +
                      $"aim=±{bracket.aimErrorDegMax:F1}° pow=±{bracket.powerErrorMax:F3} " +
                      $"clubNoise={bracket.clubNoiseChance:F2}");
            return _resolvedBracket;
        }

        /// <summary>
        /// H1: select club+power from carry table for a given target distance (meters).
        /// Uses explicit distance bands to pick the longest realistic club for the distance:
        ///   > 200m  → driver
        ///   80-200m → iron7
        ///   20-80m  → wedge
        ///   ≤ 20m   → putter
        /// Then interpolates power from the carry table.
        /// Falls back to legacy heuristic if table is absent.
        /// NOTE: the CSV max-carry values are calibrated from the production stat path
        /// (driver@1.0=433m, iron7@1.0=418m, wedge@1.0=360m) — all clubs can technically
        /// reach any distance at high power, so distance bands are the practical selector.
        /// </summary>
        // out float carry: the club's effective landing distance (clamped to its max carry when
        // targetDist exceeds the table ceiling). Used by the trunk probe so it receives the real
        // descent zone, not the full cup distance. tree_aware_bot (Order 351) §9.
        private void SelectShotCalibrated(float targetDist, out int club, out float power01, out string label, out float carry)
        {
            EnsureTableLoaded();

            if (_carryTable == null || _carryTable.Count == 0)
            {
                SelectShotLegacy(targetDist, out club, out power01, out label);
                carry = targetDist;
                return;
            }

            int putter = PhysicsLabController.PutterIndex; // = 3

            // Putt range.
            if (targetDist <= 20f)
            {
                club    = putter;
                power01 = InterpolateClubPower("putter", targetDist);
                label   = $"Putter (calibrated) dist={targetDist:F1}m power={power01:F2}";
                carry   = targetDist;
                return;
            }

            // Full shots: select club by distance band (longest realistic club per range).
            // Distance band boundaries tuned to Lomond course geometry:
            //   > 200m: driver (tee shots, long par-4/par-5 approaches)
            //   80-200m: iron7 (mid-range approaches)
            //   20-80m: wedge (short approaches, chips)
            string bestName;
            int    bestClubIndex;
            if (targetDist > 200f)
            {
                bestName       = "driver";
                bestClubIndex  = 0;
            }
            else if (targetDist > 80f)
            {
                bestName       = "iron7";
                bestClubIndex  = 1;
            }
            else
            {
                bestName       = "wedge";
                bestClubIndex  = 2;
            }

            power01 = InterpolateClubPower(bestName, targetDist);
            club    = bestClubIndex;
            label   = $"{bestName} (calibrated, band) dist={targetDist:F1}m power={power01:F2}";
            // §9: when targetDist exceeds the table ceiling, InterpolateClubPower clamps to max power
            // (line 305: foundAbove=false → return below.power01). The real carry is GetMaxCarry, not
            // targetDist. Clamp so the trunk probe checks the real landing zone.
            carry = Mathf.Min(targetDist, GetMaxCarry(bestName));
        }

        private float GetMaxCarry(string clubName)
        {
            float max = 0f;
            foreach (var r in _carryTable)
                if (r.club == clubName && r.carry > max) max = r.carry;
            return max;
        }

        private float InterpolateClubPower(string clubName, float targetDist)
        {
            // Find the two bracketing rows; linearly interpolate power01.
            CarryRow below = default;
            CarryRow above = default;
            bool foundBelow = false, foundAbove = false;

            foreach (var r in _carryTable)
            {
                if (r.club != clubName) continue;
                if (r.carry <= targetDist)
                {
                    if (!foundBelow || r.carry > below.carry)
                    {
                        below = r; foundBelow = true;
                    }
                }
                else
                {
                    if (!foundAbove || r.carry < above.carry)
                    {
                        above = r; foundAbove = true;
                    }
                }
            }

            if (!foundBelow && foundAbove) return above.power01;
            if (foundBelow && !foundAbove) return below.power01; // clamp at max
            if (!foundBelow)               return 0f;

            float span = above.carry - below.carry;
            if (span < 0.01f) return below.power01;
            float t = (targetDist - below.carry) / span;
            return Mathf.Clamp01(Mathf.Lerp(below.power01, above.power01, t));
        }

        // ── Legacy fallback (original Phase 2a heuristic) ──────────────────

        private void SelectShotLegacy(float dist, out int club, out float power01, out string label)
        {
            int putter = PhysicsLabController.PutterIndex;
            if (dist > 180f)
            {
                club = 0; power01 = 1.0f;
                label = $"Driver full power LEGACY (dist={dist:F0}m)";
            }
            else if (dist > 110f)
            {
                club = 1; power01 = Mathf.Clamp01(dist / 180f);
                label = $"Iron7 mid-range LEGACY (dist={dist:F0}m) power={power01:F2}";
            }
            else if (dist > 40f)
            {
                club = 2; power01 = Mathf.Min(Mathf.Clamp01(dist / 130f), 0.75f);
                label = $"Wedge approach LEGACY (dist={dist:F0}m) power={power01:F2}";
            }
            else if (dist > 15f)
            {
                club = 2; power01 = Mathf.Clamp01(dist / 80f);
                label = $"Wedge chip LEGACY (dist={dist:F0}m) power={power01:F2}";
            }
            else if (dist > 6f)
            {
                club = putter; power01 = Mathf.Clamp01(dist / 18f);
                label = $"Putter long putt LEGACY (dist={dist:F0}m) power={power01:F2}";
            }
            else
            {
                club = putter; power01 = Mathf.Clamp01(dist / 8f);
                label = $"Putter short putt LEGACY (dist={dist:F0}m) power={power01:F2}";
            }
        }

        // ── H2: surface probe helpers ─────────────────────────────────────

        private bool IsAvoidSurface(SurfaceType s)  => s == SurfaceType.Water;
        private bool IsPlayableSurface(SurfaceType s) =>
            s == SurfaceType.Fairway || s == SurfaceType.Green || s == SurfaceType.GreenCollar ||
            s == SurfaceType.Semirough || s == SurfaceType.Rough || s == SurfaceType.Tee ||
            s == SurfaceType.Sand; // sand (bunker) playable but discouraged

        /// <summary>
        /// Compute landing XZ position given ball position, aim yaw, and carry distance.
        /// </summary>
        private static Vector2 LandingXZ(Vector3 ball, float aimYaw, float carry)
        {
            // Aim yaw: 0 = +X, project convention.
            return new Vector2(
                ball.x + carry * Mathf.Cos(aimYaw),
                ball.z + carry * Mathf.Sin(aimYaw));
        }

        /// <summary>
        /// Probe the surface at a world-XZ position.
        /// Returns SurfaceType.Fairway as a safe-playable fallback if surface provider is unavailable
        /// (runtime safety net only; in normal play the provider is always present).
        /// BakedZoneClassifier.DefaultSurface is now Rough (surface_classification_ob_rough Stage 2):
        /// positions outside all polygons and inside the terrain grid classify as Rough.
        /// World-bounds OB IS now detectable via Classify (surface_classification_ob_rough Stage 1):
        /// points outside the terrain grid return OOB, arming the penalty path.
        /// </summary>
        private SurfaceType ProbeSurface(float worldX, float worldZ)
        {
            var provider = _controller?.GetSurfaces();
            if (provider == null) return SurfaceType.Fairway;
            try
            {
                return provider.Classify(fp.FromFloat(worldX), fp.FromFloat(worldZ));
            }
            catch
            {
                return SurfaceType.Fairway;
            }
        }

        /// <summary>
        /// H2: try to find a safe aim yaw + carry distance.
        /// Returns true if a safe landing was found; outputs safe yaw and distance.
        /// </summary>
        private bool TrySafeLanding(Vector3 ball, float aimYaw, float carry,
                                     out float safeYaw, out float safeDist)
        {
            safeYaw = aimYaw;
            safeDist = carry;

            // Step 1: probe straight line, walk back from carry until safe.
            for (float d = carry; d >= LayupMinDist; d -= LayupStep)
            {
                var land = LandingXZ(ball, aimYaw, d);
                var surf = ProbeSurface(land.x, land.y);
                if (IsPlayableSurface(surf) && !IsAvoidSurface(surf))
                {
                    safeDist = d;
                    return true;
                }
            }

            // Step 2: try rotated aim lines.
            for (int i = 0; i < OffsetDegrees.Length; i++)
            {
                float offsetYaw = aimYaw + OffsetDegrees[i] * Mathf.Deg2Rad;
                for (float d = carry; d >= LayupMinDist; d -= LayupStep)
                {
                    var land = LandingXZ(ball, offsetYaw, d);
                    var surf = ProbeSurface(land.x, land.y);
                    if (IsPlayableSurface(surf) && !IsAvoidSurface(surf))
                    {
                        safeYaw  = offsetYaw;
                        safeDist = d;
                        return true;
                    }
                }
            }

            // No safe line found; return original (will likely go OB, reactive path will correct).
            return false;
        }

        // ── Main shot coroutine ────────────────────────────────────────────

        /// <summary>
        /// Drives one complete bot shot toward the cup.
        /// Called by VersusMatchController during the bot's AwaitShot phase.
        /// </summary>
        public IEnumerator TakeShot()
        {
            if (_controller == null || _shotController == null)
            {
                Debug.LogError("[VersusBot] TakeShot: PhysicsLabController or ShotController is null.");
                yield break;
            }

            // ── 1. Read cup position and current ball position ──────────────
            Vector3 cup  = HoleContext.PinWorld;
            Vector3 ball = _controller.BallPosition;
            Vector3 flat = new Vector3(cup.x - ball.x, 0f, cup.z - ball.z);
            float   dist = flat.magnitude;

            // ── 2. Compute base aim yaw ────────────────────────────────────
            float baseYaw = Mathf.Atan2(flat.z, flat.x);
            float aimYaw  = baseYaw;

            // ── H2: reactive OB bias ───────────────────────────────────────
            // If the last shot returned OBReason, bias aim away from that direction.
            if (LastOBReason.HasValue)
            {
                // Simple correction: apply a ±15° randomized offset away from previous line.
                // The proactive Classify probe will further refine the landing.
                float bias = (Random.value > 0.5f ? 1f : -1f) * 15f * Mathf.Deg2Rad;
                aimYaw  += bias;
                Debug.Log($"[VersusBot] H2 reactive: OBReason={LastOBReason.Value}, applying bias={bias * Mathf.Rad2Deg:F0}°");
                LastOBReason = null; // consume
            }

            // ── 3. Select club + power (H1 calibrated) ─────────────────────
            // §9: capture probeCarry — the selected club's modelled carry (may be < dist when
            // dist exceeds the table ceiling). Passed to trunk probe below; updated if H2 lays up.
            SelectShotCalibrated(dist, out int club, out float power01, out string label, out float probeCarry);
            bool isPutt = club == PhysicsLabController.PutterIndex;

            // ── H3b: off-green putter override (putter-fall-through guard) ──
            // When the bot selects Putter (dist ≤ 20m) but the ball is NOT on
            // the green or collar, the ShotController fires an aerial shot
            // (isPuttGate.surfaceOk=false). Aerial putter shots can start
            // slightly below the heightmap surface → ball falls through terrain
            // (hits=0, reaches y≈-2685) → bot stuck in recovery loop.
            // Fix: fall back to Wedge for dist > 3m when ball is off-green.
            // Wedge aerial at low power carries correctly and lands without
            // height-map depenetration issues.
            if (isPutt && dist > 3f)
            {
                var ballSurface = ProbeSurface(ball.x, ball.z);
                if (ballSurface != SurfaceType.Green && ballSurface != SurfaceType.GreenCollar)
                {
                    club    = 2; // wedge
                    power01 = InterpolateClubPower("wedge", dist);
                    label   = $"wedge (off-green override, surface={ballSurface}) dist={dist:F1}m power={power01:F2}";
                    isPutt  = false;
                    Debug.Log($"[VersusBot] H3b off-green override: surface={ballSurface} at ({ball.x:F1},{ball.z:F1}), " +
                              $"using wedge for {dist:F1}m instead of putter (prevents aerial fall-through).");
                }
            }

            // ── H2: proactive landing probe (non-putt only) ─────────────────
            // Probe both intermediate flight-path points (every LayupStep) AND the landing point.
            // This catches holes where the ball must fly OVER water (mid-fairway hazard) even
            // if the landing point itself is safe beyond the water.
            // Example: Hole 18 (water 100-188m along tee→pin, pin 223m) — ball flies through
            // water at ~106m even though it lands safely at 223m.
            if (!isPutt && dist > LayupMinDist)
            {
                float estimatedCarry = dist; // calibrated club carries ≈ to target distance
                bool  hazardFound = false;
                float hazardDist  = estimatedCarry;

                // 1. Probe every LayupStep along the flight path to detect mid-flight water.
                for (float d = LayupMinDist; d <= estimatedCarry; d += LayupStep)
                {
                    var midXZ   = LandingXZ(ball, aimYaw, d);
                    var midSurf = ProbeSurface(midXZ.x, midXZ.y);
                    if (IsAvoidSurface(midSurf))
                    {
                        hazardFound = true;
                        hazardDist  = d;
                        Debug.Log($"[VersusBot] H2 proactive: {midSurf} detected at {d:F0}m ({midXZ.x:F1},{midXZ.y:F1}) along flight path — laying up short of water");
                        break;
                    }
                }

                // H2 fly-over check: if mid-flight water detected but the LANDING POINT is safe,
                // the ball will arc over the hazard — no layup needed.
                // Root cause of layup loop: from 83m with water at 18m, the full shot (83m) lands
                // on the green safely, but the probe at 18m set hazardFound. The correct behaviour
                // is to fly over. Only lay up when the landing point itself is in the hazard.
                if (hazardFound)
                {
                    var flyOverLandXZ   = LandingXZ(ball, aimYaw, estimatedCarry);
                    var flyOverLandSurf = ProbeSurface(flyOverLandXZ.x, flyOverLandXZ.y);
                    if (!IsAvoidSurface(flyOverLandSurf))
                    {
                        hazardFound = false; // landing is safe — fly over
                        Debug.Log($"[VersusBot] H2 fly-over: mid-flight water at {hazardDist:F0}m but landing at {estimatedCarry:F0}m is {flyOverLandSurf} — using full shot (fly over)");
                    }
                }

                // 2. Also check the actual landing point (catches pin-in-water edge cases).
                if (!hazardFound)
                {
                    var landXZ   = LandingXZ(ball, aimYaw, estimatedCarry);
                    var landSurf = ProbeSurface(landXZ.x, landXZ.y);
                    if (IsAvoidSurface(landSurf))
                    {
                        hazardFound = true;
                        hazardDist  = estimatedCarry;
                        Debug.Log($"[VersusBot] H2 proactive: landing surface={landSurf} at ({landXZ.x:F1},{landXZ.y:F1}) → laying up to safe distance (was {estimatedCarry:F1}m)");
                    }
                }

                if (hazardFound)
                {
                    // TrySafeLanding walks back from the hazard entry point to find safe ground.
                    if (TrySafeLanding(ball, aimYaw, hazardDist, out float safeYaw, out float safeDist))
                    {
                        aimYaw = safeYaw;
                        // H2 layup putter-floor: SelectShotCalibrated picks putter for dist ≤ 20m.
                        // If safeDist ≤ 20m, SetClub(3) triggers EnterPutterMode() which teleports
                        // the ball to the LabScaffold flat-green origin (0,0,0) — shot fires from
                        // wrong position, falls through terrain, bot stuck.
                        // Fix: floor safeDist to 22m so a layup always targets wedge/iron, never putter.
                        const float LayupPutterFloor = 22f;
                        if (safeDist < LayupPutterFloor)
                        {
                            Debug.Log($"[VersusBot] H2 layup putter-floor: safeDist={safeDist:F1}m clamped to {LayupPutterFloor}m (prevents EnterPutterMode teleport)");
                            safeDist = LayupPutterFloor;
                        }
                        // Re-select club+power for the new (shorter) safe distance.
                        // §9: update probeCarry — H2 changed the landing target; trunk probe must see it.
                        SelectShotCalibrated(safeDist, out club, out power01, out label, out probeCarry);
                        isPutt = club == PhysicsLabController.PutterIndex;
                        label += $" [laid up to {safeDist:F0}m]";
                        float aimDeltaDeg = (safeYaw - baseYaw) * Mathf.Rad2Deg;
                        Debug.Log($"[VersusBot] H2 layup resolved: safeDist={safeDist:F1}m safeYaw={safeYaw*Mathf.Rad2Deg:F1}° (delta={aimDeltaDeg:+0.1;-0.1}° from cup line)");
                    }
                    else
                    {
                        Debug.Log("[VersusBot] H2: no safe landing found — using original line (reactive OBReason will catch).");
                    }
                }
            }

            // ── H3: green-slope read (putts only) ──────────────────────────
            // SAFETY: only apply slope corrections when:
            //   (a) mag is within a realistic green-slope range (< 0.35 = 35% grade cap),
            //   (b) the ball is within _greenReader.BakedCellCount > 0 (green baked).
            // This guards against degenerate cells at the edge of the baked region (or when
            // the ball is off the green) that can have artificially large slopeX/slopeZ values.
            // Root cause of iter-2's 100%-power putt: the ball was on Hole18's green after
            // tree recovery; TryGetSlopeAt found the nearest baked cell which had a large
            // uphillComponent; powerNudge = uphillComponent * 9.8 * 0.08 pushed power to 1.0.
            if (isPutt && _greenReader != null && _greenReader.BakedCellCount > 0)
            {
                if (_greenReader.TryGetSlopeAt(ball.x, ball.z, out float slopeX, out float slopeZ, out float mag))
                {
                    // Only apply if slope is in a realistic green range (0.01–0.35 grade fraction).
                    // Values > 0.35 are outside realistic green design and likely a degenerate cell.
                    const float MagMin = 0.01f;
                    const float MagMax = 0.35f;
                    if (mag > MagMin && mag < MagMax)
                    {
                        // Aim offset: push aim uphill of the fall line proportional to slopeX.
                        // slopeX > 0 means terrain rises in +X → ball will break toward -X.
                        // To play the break, aim toward +X (uphill) by slopeX*dist*gain.
                        float aimOffset = -slopeX * dist * _slopeAimGain;
                        aimYaw += aimOffset;

                        // Power nudge: uphill (slopeZ<0 in forward direction) needs more power.
                        // Compute component of slope along aim direction.
                        float aimCos  = Mathf.Cos(baseYaw);
                        float aimSin  = Mathf.Sin(baseYaw);
                        float uphillComponent = slopeX * aimCos + slopeZ * aimSin;
                        float powerNudge = uphillComponent * dist * _slopePowerGain;
                        // Cap nudge to ±0.15 (15% power swing) — prevents any single cell from
                        // dramatically changing the computed power even if uphillComponent is large.
                        powerNudge = Mathf.Clamp(powerNudge, -0.15f, 0.15f);
                        power01 = Mathf.Clamp01(power01 + powerNudge);

                        Debug.Log($"[VersusBot] H3 slope: slopeX={slopeX:F3} slopeZ={slopeZ:F3} mag={mag:F3} " +
                                  $"aimOffset={aimOffset*Mathf.Rad2Deg:F2}° powerNudge={powerNudge:F3} → power={power01:F2}");
                    }
                    else
                    {
                        Debug.Log($"[VersusBot] H3 slope: mag={mag:F3} outside [{MagMin},{MagMax}] — skipping slope correction (degenerate cell guard).");
                    }
                }
            }

            // ── tree_aware_bot (Order 351): trunk avoidance on H2-resolved line, before 2b ──
            // Runs AFTER H2 (safe landing already chosen) and BEFORE 2b error injection so
            // difficulty perturbation fires on the tree+water-safe aim exactly as it does today.
            // Non-putt only (putter shots don't drive into trunks at playing distance).
            //
            // bot_tree_error_recheck: hoist `trees` so the 2b block below can use it for the
            // aim-error re-check (TrySampleTrunkClearAimError). Null on treeless holes → no-op.
            var trees = _controller.GetTreeProvider();
            if (!isPutt)
            {
                // §9: pass probeCarry (the club's actual landing distance, updated by H2 if it fired)
                // instead of dist (the full cup distance). Puts the landing window at the real descent zone.
                if (trees != null && BotTreeProbe.TryFindTrunkClearAim(
                        trees, _controller.GetSurfaces(), ball, aimYaw, probeCarry,
                        out float treeYaw, out float treeDist))
                {
                    aimYaw = treeYaw;
                    // putter-floor guard: treeDist < 22m triggers EnterPutterMode teleport.
                    const float LayupPutterFloor = 22f;
                    if (treeDist < LayupPutterFloor)
                    {
                        Debug.Log($"[VersusBot] Tree re-aim putter-floor: treeDist={treeDist:F1}m clamped to {LayupPutterFloor}m (prevents EnterPutterMode teleport)");
                        treeDist = LayupPutterFloor;
                    }
                    // bot_tree_error_recheck: out probeCarry (was out _). The 2b block uses probeCarry
                    // for TrySampleTrunkClearAimError — must reflect the tree-re-aimed carry so the
                    // landing window in the re-check matches the line the bot is actually going to fire.
                    SelectShotCalibrated(treeDist, out club, out power01, out label, out probeCarry);
                    isPutt = club == PhysicsLabController.PutterIndex;
                    label += $" [tree re-aim to {treeDist:F0}m]";
                    float aimDeltaDeg = (treeYaw - baseYaw) * Mathf.Rad2Deg;
                    Debug.Log($"[VersusBot] Tree re-aim resolved: treeDist={treeDist:F1}m treeYaw={treeYaw * Mathf.Rad2Deg:F1}° (delta={aimDeltaDeg:+0.1;-0.1}° from cup line)");
                }
            }

            // ── 2b: POST-DECISION ERROR INJECTION (D1: after H1/H2/H3, before commit) ──
            // Inject per-shot execution error based on the opponent's level bracket.
            // No safety re-check runs on the perturbed values — they fire straight to commit.
            {
                var bkt = ResolveBracket();

                // Remember the safe target distance before any club noise changes it.
                // This is the distance already in scope after H1/H2 selection (dist for normal
                // shots, safeDist for laid-up shots — both are captured in power01+club by this
                // point). We need to recover the target distance for power re-inversion (D3).
                // Re-derive via InterpolateClubPower inverse: we know the club and the intent
                // carry was either dist (full shot) or the safeDist (layup). Rather than tracking
                // both paths, we reconstruct it from the current club+power01 by reading the
                // carry table: find the carry that matches current power01 for current clubName.
                // Simpler: cache the distance that was passed to SelectShotCalibrated — that is
                // the "safe target distance" for re-inversion purposes.
                // Since we no longer have a named local (layup paths shadow `dist`), use a helper:
                // invert InterpolateClubPower(clubName, targetDist) → re-derive targetDist from
                // the current power01 reading. The spec says "re-invert to the SAME safe target
                // distance" — meaning we pass the same targetDist to InterpolateClubPower(noisyClub, targetDist).
                // We can recover targetDist because the carry table is monotone: find the carry
                // corresponding to current power01 for current club name.
                string origClubName = ClubNames[Mathf.Clamp(club, 0, ClubNames.Length - 1)];
                float  safeTargetDist = InvertClubPower(origClubName, power01);

                // D3: club noise (suppressed when club is putter — D4).
                string clubNoiseNote = "none";
                if (!isPutt && bkt.clubNoiseChance > 0f && Random.value < bkt.clubNoiseChance)
                {
                    // ±1 band shift: 0=driver, 1=iron7, 2=wedge, 3=putter.
                    // Putter never noise-in (isPutt guard above); driver can only shift down (→ iron7).
                    int dir = (Random.value > 0.5f) ? 1 : -1;
                    int noisyClubIndex = Mathf.Clamp(club + dir, 0, 2); // clamp [0..2]: putter excluded

                    if (noisyClubIndex != club)
                    {
                        string noisyClubName = ClubNames[noisyClubIndex];
                        float  maxCarry = GetMaxCarry(noisyClubName);
                        // Re-invert power for the noisy club at the SAME safe target distance.
                        float  noisyPower = InterpolateClubPower(noisyClubName, Mathf.Min(safeTargetDist, maxCarry));
                        noisyPower = Mathf.Clamp01(noisyPower);

                        clubNoiseNote = $"{origClubName}→{noisyClubName}";
                        club    = noisyClubIndex;
                        power01 = noisyPower;
                        label  += $" [clubNoise:{origClubName}→{noisyClubName}]";
                    }
                }

                // D2: aim/power error (applies to all shots including putts after H3).
                float deltaAimDeg = 0f;
                float deltaPow    = 0f;
                int   treeChecked = 0;
                if (bkt.aimErrorDegMax > 0f || bkt.powerErrorMax > 0f)
                {
                    // bot_tree_error_recheck: aim error must not point the shot back into a trunk
                    // corridor the tree_aware_bot probe just cleared. Route through rejection sampler.
                    // Power error unchanged (re-check uses pre-error carry — accepted approximation,
                    // see spec §2 Out: power changes carry, not aim direction).
                    bool clamped = false;
                    if (!isPutt && trees != null && !DebugDisableTreeRecheck)
                    {
                        treeChecked = 1;
                        if (!BotTreeProbe.TrySampleTrunkClearAimError(
                                trees, ball, aimYaw, probeCarry, bkt.aimErrorDegMax,
                                MaxAimErrorResamples, Random.Range, out deltaAimDeg))
                            clamped = true;   // deltaAimDeg == 0 → fire the validated pre-2b line
                    }
                    else
                    {
                        deltaAimDeg = Random.Range(-bkt.aimErrorDegMax, bkt.aimErrorDegMax);
                    }
                    deltaPow = Random.Range(-bkt.powerErrorMax, bkt.powerErrorMax);
                    aimYaw  += deltaAimDeg * Mathf.Deg2Rad;
                    power01  = Mathf.Clamp01(power01 + deltaPow);
                    if (clamped)
                        Debug.Log("[VersusBot] 2b tree re-check: all aim samples trunk-blocked — clamped to pre-2b line");
                }

                Debug.Log($"[VersusBot] 2b error: Δaim={deltaAimDeg:+0.0;-0.0}° Δpow={deltaPow:+0.000;-0.000} clubNoise={clubNoiseNote} treeChecked={treeChecked}");
            }
            // ── END 2b error injection ──────────────────────────────────────

            Debug.Log($"[VersusBot] TakeShot: ball={ball:F1} cup={cup:F1} dist={dist:F1}m aimYaw={aimYaw*Mathf.Rad2Deg:F1}° — {label}");

            // ── 4. Set club; sync ClubContext; clear override so LIVE path fires intended club ──
            // Order 762: SetClub only updates the LAB index + putter UI. On the LIVE stat path
            // LiveStatProviderHost resolves the swing club from ClubContext.SelectedClubId (line 188),
            // which SetClub never touches — so without the sync the equipped driver fires every stroke.
            // BotClubSync pushes the nearest available bag entry for the selected lab index into
            // ClubContext so the provider fires the club the bot actually chose.
            _controller.SetClub(club);
            {
                int resolvedLab = BotClubSync.SyncToClubContext(club, "VersusBot");
                if (resolvedLab != club)
                {
                    _controller.SetClub(resolvedLab);
                    club = resolvedLab;
                }
            }
            _shotController.ClearStatBundleOverride();

            // ── 5. Orient camera yaw ────────────────────────────────────────
            _controller.SetCameraYawRadians(aimYaw);

            // ── 6. Gate on ShotController.State == Idle ──────────────────────
            {
                float gateElapsed = 0f;
                while (_shotController.State != ShotState.Idle && gateElapsed < 4f)
                {
                    gateElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (_shotController.State != ShotState.Idle)
                {
                    Debug.LogWarning($"[VersusBot] TakeShot: ShotController never reached Idle (state={_shotController.State})");
                    yield break;
                }
            }

            // ── 7. Gate on BallStateMachine.State == Aiming ─────────────────
            var sm = _controller.BallSM;
            if (sm != null)
            {
                float gateElapsed = 0f;
                while (sm.State != BallState.Aiming && gateElapsed < 4f)
                {
                    gateElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (sm.State != BallState.Aiming)
                    Debug.LogWarning($"[VersusBot] TakeShot: BallSM never reached Aiming (state={sm.State})");
            }

            // ── 8. Drive the shot via BeginExternalDrag → ramp → EndExternalDrag ──
            _shotController.BeginExternalDrag();

            const float rampSeconds = 0.85f;
            float rt = 0f;
            while (rt < rampSeconds)
            {
                rt += Time.unscaledDeltaTime;
                _shotController.SetExternalPower(Mathf.Lerp(0f, power01, rt / rampSeconds), 0f);
                yield return null;
            }
            _shotController.SetExternalPower(power01, 0f);

            yield return new WaitForSecondsRealtime(0.18f);

            _shotController.EndExternalDrag();

            Debug.Log($"[VersusBot] TakeShot: shot fired — club={club} power={power01:F2}");
        }

        // ── 2b: helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Inverts InterpolateClubPower: given a clubName and power01, returns the approximate
        /// carry distance that produced that power reading (used to recover safeTargetDist for
        /// club-noise re-inversion per D3). Uses linear search over the carry table.
        /// Falls back to 50m if the table is empty or club is unknown.
        /// </summary>
        private float InvertClubPower(string clubName, float power01)
        {
            if (_carryTable == null || _carryTable.Count == 0) return 50f;

            CarryRow below = default;
            CarryRow above = default;
            bool foundBelow = false, foundAbove = false;

            foreach (var r in _carryTable)
            {
                if (r.club != clubName) continue;
                if (r.power01 <= power01)
                {
                    if (!foundBelow || r.power01 > below.power01)
                    {
                        below = r; foundBelow = true;
                    }
                }
                else
                {
                    if (!foundAbove || r.power01 < above.power01)
                    {
                        above = r; foundAbove = true;
                    }
                }
            }

            if (!foundBelow && foundAbove) return above.carry;
            if (foundBelow && !foundAbove) return below.carry;
            if (!foundBelow) return 50f;

            float span = above.power01 - below.power01;
            if (span < 0.001f) return below.carry;
            float t = (power01 - below.power01) / span;
            return Mathf.Lerp(below.carry, above.carry, t);
        }
    }
}
