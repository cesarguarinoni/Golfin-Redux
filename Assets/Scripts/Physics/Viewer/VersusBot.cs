using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Runtime bot opponent for 1v1 versus mode (Phase 2a).
    ///
    /// Drives shots via the PRODUCTION ShotController external-drag path:
    ///   BeginExternalDrag() → ramped SetExternalPower() → EndExternalDrag()
    /// The bot reads the cup position from HoleContext.PinWorld and selects club+power
    /// using the same SelectShot() heuristic from BotDriver (cited as behavioral reference).
    ///
    /// Phase 2a: no error injection. coneFinetune = 0, no aim/power noise.
    /// The bot plays a competent, straight line so matches reliably resolve end-to-end.
    /// Phase 2b will layer the difficulty error model (§13 of SPEC).
    ///
    /// MUST NOT call ForceShotCompleteForBot.
    /// MUST NOT be wrapped in #if UNITY_EDITOR.
    /// </summary>
    public class VersusBot : MonoBehaviour
    {
        [SerializeField] PhysicsLabController _controller;
        [SerializeField] ShotController       _shotController;

        void Awake()
        {
            if (_controller   == null) _controller   = FindObjectOfType<PhysicsLabController>();
            if (_shotController == null) _shotController = FindObjectOfType<ShotController>();
        }

        /// <summary>
        /// Drives one complete bot shot toward the cup.
        /// Called by VersusMatchController during the bot's AwaitShot phase.
        /// Fires OnShotComplete on the BallStateMachine when the shot resolves;
        /// VersusMatchController's subscription handles the rest.
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

            // ── 2. Select club + power via distance-aware heuristic (BUG B fix iter-5) ──
            SelectShot(dist, out int club, out float power01, out string label);

            Debug.Log($"[VersusBot] TakeShot: ball={ball:F1} cup={cup:F1} dist={dist:F1}m — {label}");

            // ── 3. Set club; clear any stat-bundle override so bus resolves live stats ──
            _controller.SetClub(club);
            _shotController.ClearStatBundleOverride();

            // ── 4. Orient camera yaw toward cup ─────────────────────────────
            float yaw = Mathf.Atan2(flat.z, flat.x);
            _controller.SetCameraYawRadians(yaw);

            // ── 5. Gate on ShotController.State == Idle ──────────────────────
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

            // ── 6. Gate on BallStateMachine.State == Aiming ─────────────────
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

            // ── 7. Drive the shot via BeginExternalDrag → ramp → EndExternalDrag ──
            // Mirror BotDriver.PlayHoleToCup ramp: 0.85s visible handle pull-down.
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

            // Brief hold at full pull (mirrors BotDriver.PlayHoleToCup: 0.18s).
            yield return new WaitForSecondsRealtime(0.18f);

            _shotController.EndExternalDrag();

            Debug.Log($"[VersusBot] TakeShot: shot fired — club={club} power={power01:F2}");
            // OnShotComplete on BallStateMachine fires when the ball resolves.
            // VersusMatchController is subscribed and handles the result.
        }

        /// <summary>
        /// Select club index and power for a shot of the given horizontal distance.
        /// Distance-aware from stroke 1 (BUG B fix iter-5): the isFirstStroke=Driver override
        /// has been removed so par-3 tee shots (~110m) correctly use Iron7 instead of Driver.
        ///
        /// Tier thresholds (tuned against Lomond real flight data):
        ///   dist > 180m → Driver full power  (par-5/long par-4 tees)
        ///   dist > 110m → Iron7, power = dist/180  (mid par-4, short par-4, par-3 tees ~110-180m)
        ///   dist > 40m  → Wedge approach
        ///   dist > 15m  → Wedge chip
        ///   dist > 6m   → Putter long putt
        ///   else        → Putter short putt
        ///
        /// Club indices: 0=Driver, 1=Iron7, 2=Wedge, 3=Putter (PhysicsLabController.PutterIndex)
        /// </summary>
        void SelectShot(float dist, out int club, out float power01, out string label)
        {
            int putter = PhysicsLabController.PutterIndex;

            if (dist > 180f)
            {
                // Long tee (par-5 / long par-4): Driver full power.
                club    = 0;
                power01 = 1.0f;
                label   = $"Driver full power (dist={dist:F0}m)";
            }
            else if (dist > 110f)
            {
                // Mid-range (par-4 / par-3 tee ~110-180m): Iron7, power scaled by distance.
                club    = 1;
                power01 = Mathf.Clamp01(dist / 180f);
                label   = $"Iron7 mid-range (dist={dist:F0}m) power={power01:F2}";
            }
            else if (dist > 40f)
            {
                // Short approach: Wedge.
                club    = 2;
                power01 = Mathf.Min(Mathf.Clamp01(dist / 130f), 0.75f);
                label   = $"Wedge approach (dist={dist:F0}m) power={power01:F2}";
            }
            else if (dist > 15f)
            {
                // Chip: Wedge low power.
                club    = 2;
                power01 = Mathf.Clamp01(dist / 80f);
                label   = $"Wedge chip (dist={dist:F0}m) power={power01:F2}";
            }
            else if (dist > 6f)
            {
                // Long putt.
                club    = putter;
                power01 = Mathf.Clamp01(dist / 18f);
                label   = $"Putter long putt (dist={dist:F0}m) power={power01:F2}";
            }
            else
            {
                // Short putt.
                club    = putter;
                power01 = Mathf.Clamp01(dist / 8f);
                label   = $"Putter short putt (dist={dist:F0}m) power={power01:F2}";
            }
        }
    }
}
