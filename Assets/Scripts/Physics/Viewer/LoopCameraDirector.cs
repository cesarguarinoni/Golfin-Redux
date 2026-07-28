using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Physics;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Test seam for controller-side accessors needed by LoopCameraDirector.
    /// In production this is PhysicsLabController; in tests use StubControllerAccessor.
    /// </summary>
    public interface IControllerAccessor
    {
        Golfin.Gameplay.Loop.BallStateMachine BallSM           { get; }
        Trajectory                            LastTrajectory   { get; }
        Vector3                               LastShotOrigin   { get; }
        Vector3                               LastShotLaunchDir { get; }
        Transform                             CurrentBall      { get; }
        bool                                  CurrentShotIsPutt { get; }
    }

    /// <summary>
    /// Central camera lifecycle state machine. Subscribes to BallStateMachine.OnStateChanged
    /// and dispatches camera modes to ChaseCamera (or any IModeSetter for tests).
    ///
    /// §2b: replaces the nine scattered chaseCamera mutation sites in PhysicsLabController
    /// with a single subscriber. PhysicsLabController keeps:
    ///   - AdjustCameraForDepression (depth offset, orthogonal)
    ///   - HandleCameraOrbit (yaw drag, orthogonal)
    ///   - Awake-time wiring (camera component, depth/color enable, widget setters)
    ///   - SetClub putter→GroundLevel (club-driven, L5)
    ///   - FireInternal (preset path, scaffold-driven per §2a _prevBallPlaying acceptance)
    /// </summary>
    public class LoopCameraDirector : MonoBehaviour
    {
        [SerializeField] ChaseCamera          chaseCamera;
        [SerializeField] PhysicsLabController controller;

        [Header("Cinematic")]
        [SerializeField] float cinematicCutAtCarryFraction = 0.65f;    // L8: cut at 65% of carry
        [SerializeField] float minCarryForCinematicMeters  = 30f;      // skip cut on chips/short shots

        [Header("Downrange framing")]
        [SerializeField] float downrangePastLandingMeters = 12f;       // Q1'b
        [SerializeField] float downrangeHeightMeters      = 4f;

        [Header("CupZoom framing")]
        [SerializeField] float cupZoomHoverHeightMeters   = 2.5f;      // L11: hover above flat circle
        [SerializeField] float cupZoomTweenSeconds        = 1.0f;

        [Header("OBFreeze framing")]
        [SerializeField] float obFreezeHeightAboveTerrain = 5f;        // L9 / Q3'a

        // ── Observable event (§controls_g_smoke_followup) ─────────────────────

        /// <summary>
        /// Raised whenever the Director changes the camera mode (whether at SM state transitions
        /// or at the mid-flight cinematic cut). Subscribers receive the new mode value.
        /// Not load-bearing for production logic — used by smoke runners and debug tools.
        /// Removing subscribers leaves Director behavior identical.
        /// </summary>
        public event System.Action<ChaseCamera.Mode> OnModeChanged;

        // ── Test seams ─────────────────────────────────────────────────────────

        // Allows tests to inject a RecordingModeSetter without a Camera GO.
        IModeSetter _modeSetter;
        // Allows tests to inject a stub controller without a real MonoBehaviour.
        IControllerAccessor _controllerAccessor;

        public void SetModeSetter(IModeSetter ms) => _modeSetter = ms;

        /// <summary>
        /// Inject a stub controller accessor for unit tests (bypasses PhysicsLabController).
        /// Also subscribes to the stub's BallSM.OnStateChanged if it differs from the current SM.
        /// </summary>
        public void SetControllerAccessor(IControllerAccessor accessor)
        {
            // Unsubscribe from old SM if any.
            var oldSM = _controllerAccessor?.BallSM ?? controller?.BallSM;
            if (oldSM != null) oldSM.OnStateChanged -= HandleStateChanged;

            _controllerAccessor = accessor;

            // Subscribe to new SM.
            var newSM = accessor?.BallSM;
            if (newSM != null) newSM.OnStateChanged += HandleStateChanged;
        }

        IModeSetter ActiveSetter => _modeSetter ?? chaseCamera;
        IControllerAccessor ActiveController => _controllerAccessor
            ?? (controller != null ? new PhysicsLabControllerAdapter(controller) : null);

        /// <summary>
        /// Routes ALL mode changes through this helper so OnModeChanged is always raised.
        /// Replaces direct <c>setter.SetMode(mode)</c> calls inside the Director.
        /// </summary>
        void ApplyMode(ChaseCamera.Mode mode)
        {
            ActiveSetter?.SetMode(mode);
            OnModeChanged?.Invoke(mode);
        }

        // ── State→Mode dispatch table ──────────────────────────────────────────

        static readonly Dictionary<BallState, ChaseCamera.Mode?> ModeMap =
            new Dictionary<BallState, ChaseCamera.Mode?>
            {
                { BallState.Aiming,  null                    },  // leave whatever was set
                { BallState.Flying,  ChaseCamera.Mode.Chase  },  // initial; cinematic cut may promote
                { BallState.Rolling, ChaseCamera.Mode.Chase  },  // back to chase on touchdown
                { BallState.AtRest,  ChaseCamera.Mode.Chase  },
                { BallState.InCup,   ChaseCamera.Mode.CupZoom  },
                { BallState.OB,      ChaseCamera.Mode.OBFreeze },
            };

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PhysicsLabController>();
            var sm = ActiveController?.BallSM;
            if (sm != null) sm.OnStateChanged += HandleStateChanged;
        }

        void OnDestroy()
        {
            // Unsubscribe via active controller (covers both real and stub paths).
            var sm = (_controllerAccessor != null ? _controllerAccessor.BallSM : controller?.BallSM);
            if (sm != null) sm.OnStateChanged -= HandleStateChanged;
        }

        void Update() => TickCinematicCut();

        /// <summary>
        /// Extracted from Update() so tests can call it without triggering the MonoBehaviour
        /// ShouldRunBehaviour() assertion that fires when SendMessage("Update") is called
        /// in EditMode tests. Also useful for external triggers (e.g. debug tools).
        /// </summary>
        public void TickCinematicCut()
        {
            // §controls_h iter-6: cinematic cut deleted. Chase runs the entire shot.
            // Method preserved as a no-op for tests that may call it; can be removed
            // entirely after the iter-6 test suite lands.
        }

        // ── SM subscription handler ────────────────────────────────────────────

        void HandleStateChanged(BallStateChange change)
        {
            var setter = ActiveSetter;
            if (setter == null) return;

            var ctrl = ActiveController;

            // Aiming → Flying: arm chase target + reset origin + pre-arm OB clamp.
            if (change.Next == BallState.Flying && change.Previous == BallState.Aiming)
            {
                if (ctrl != null)
                {
                    ArmChaseForShot(ctrl.LastShotOrigin, ctrl.LastShotLaunchDir, ctrl.CurrentBall);

                    // D3: arm the OB clamp NOW using the already-computed trajectory.
                    // Non-OB shots get active=false → camera behaves byte-identically to HEAD.
                    // ExitedWorldBounds has no terrain hit but the ball is still OB — arm at finalPosition.
                    var traj = ctrl.LastTrajectory;
                    Vector3 clampPoint;
                    bool hasOBHit = TryFindFirstOBHit(traj, ctrl.LastShotOrigin, out clampPoint);
                    bool shouldClamp = hasOBHit
                        || (traj != null && traj.termination == TerminationReason.ExitedWorldBounds);
                    setter?.SetChaseClamp(clampPoint, shouldClamp);
                }
            }

            // § controls_h R3: Flying → Rolling (or Rolling → Rolling on bounce):
            // re-arm the target with the current ball so Chase continues tracking
            // through the rolling/settling phase. The ball Transform is still valid
            // at this point (BallAnimator has not destroyed it yet).
            if (change.Next == BallState.Rolling)
            {
                if (ctrl != null && ctrl.CurrentBall != null)
                    setter.SetTarget(ctrl.CurrentBall);
            }

            // InCup: set cup zoom focus before mode switch.
            if (change.Next == BallState.InCup)
            {
                Vector3 pos = new Vector3(
                    change.Position.x.ToFloat(),
                    change.Position.y.ToFloat(),
                    change.Position.z.ToFloat());
                setter.SetCupZoomFocus(pos);
            }

            // OB: set freeze pivot before mode switch.
            if (change.Next == BallState.OB)
            {
                Vector3 fallback = new Vector3(
                    change.Position.x.ToFloat(),
                    change.Position.y.ToFloat(),
                    change.Position.z.ToFloat());
                var pivot = ComputeOBFreezePivot(fallback, ctrl?.LastTrajectory);
                setter.SetOBFreezePivot(pivot);
            }

            // Apply mode mapping (null = leave unchanged).
            if (ModeMap.TryGetValue(change.Next, out var mode) && mode.HasValue)
            {
                ApplyMode(mode.Value);
            }

            // Pre-iter-3 behavior: clear target on ALL terminal states. Aiming-camera owner
            // (ApplyCameraYaw) takes over via ChaseCamera.LateUpdate's null-target early-return.
            if (change.Next == BallState.AtRest
             || change.Next == BallState.InCup
             || change.Next == BallState.OB)
            {
                setter.SetTarget(null);
            }
        }

        // ── Internal helpers ───────────────────────────────────────────────────

        void ArmChaseForShot(Vector3 origin, Vector3 launchDir, Transform ball)
        {
            var setter = ActiveSetter;
            if (setter == null) return;
            setter.SetTarget(ball);
            setter.ResetToOrigin(origin, launchDir);
        }

        /// <summary>
        /// Shared scan: find the first terrain hit with Surface == Water or OOB in traj.terrainHits.
        /// Returns true and writes the XZ world position (Y from hit) into <paramref name="pos"/>
        /// if found; returns false and writes <paramref name="fallbackPos"/> into pos otherwise.
        ///
        /// Both ComputeOBFreezePivot and the Aiming→Flying clamp arm use this to guarantee
        /// they derive from the same first-OB-hit — no copy-paste duplication (Order-731/762 scar).
        /// </summary>
        static bool TryFindFirstOBHit(Trajectory traj, Vector3 fallbackPos, out Vector3 pos)
        {
            if (traj?.terrainHits != null)
            {
                foreach (var hit in traj.terrainHits)
                {
                    if (hit.Surface == SurfaceType.Water || hit.Surface == SurfaceType.OOB)
                    {
                        pos = new Vector3(
                            hit.Position.x.ToFloat(),
                            hit.Position.y.ToFloat(),
                            hit.Position.z.ToFloat());
                        return true;
                    }
                }
            }

            // ExitedWorldBounds or no OB terrain hit — fall back.
            // Use finalPosition if available, otherwise the supplied fallback.
            if (traj != null)
            {
                pos = new Vector3(
                    traj.finalPosition.x.ToFloat(),
                    traj.finalPosition.y.ToFloat(),
                    traj.finalPosition.z.ToFloat());
            }
            else
            {
                pos = fallbackPos;
            }
            return false;
        }

        Vector3 ComputeOBFreezePivot(Vector3 fallback, Trajectory traj)
        {
            TryFindFirstOBHit(traj, fallback, out var hitPos);
            // Pivot is at OB-hit XZ + height offset Y (regardless of whether it was a hit or fallback).
            // INTENTIONAL CHANGE (not equivalent to pre-refactor behaviour): when TryFindFirstOBHit
            // returns false (no OB terrain hit found), hitPos is set to traj.finalPosition rather
            // than the old 'fallback' value. This is deliberate — the freeze pivot now tracks the
            // ball's actual computed final position on ExitedWorldBounds or terrain-unmapped OB
            // terminations, giving a more accurate freeze anchor than the prior fallback did.
            return hitPos + Vector3.up * obFreezeHeightAboveTerrain;
        }

        // ── Carry / progress helpers (for cinematic cut) ──────────────────────

        float ComputePredictedCarry(Trajectory traj, Vector3 origin)
        {
            if (traj.terrainHits != null && traj.terrainHits.Count > 0)
            {
                // First non-stop terrain hit = carry landing.
                foreach (var hit in traj.terrainHits)
                {
                    if (!hit.IsStop)
                    {
                        float dx = hit.Position.x.ToFloat() - origin.x;
                        float dz = hit.Position.z.ToFloat() - origin.z;
                        return Mathf.Sqrt(dx * dx + dz * dz);
                    }
                }
            }

            // Fallback: XZ distance to final position.
            float fx = traj.finalPosition.x.ToFloat() - origin.x;
            float fz = traj.finalPosition.z.ToFloat() - origin.z;
            return Mathf.Sqrt(fx * fx + fz * fz);
        }

        float ComputeCurrentXZProgress(Vector3 ballPos, Vector3 origin, Vector3 launchDir)
        {
            // Project ball displacement onto launch direction to get "along-shot" progress.
            Vector3 displacement = ballPos - origin;
            displacement.y = 0f;
            Vector3 flatDir = new Vector3(launchDir.x, 0f, launchDir.z);
            if (flatDir.sqrMagnitude < 0.0001f) return 0f;
            flatDir.Normalize();
            return Mathf.Max(0f, Vector3.Dot(displacement, flatDir));
        }

        Vector3 ComputeLandingPos(Trajectory traj)
        {
            if (traj.terrainHits != null)
            {
                foreach (var hit in traj.terrainHits)
                {
                    if (!hit.IsStop)
                    {
                        return new Vector3(
                            hit.Position.x.ToFloat(),
                            hit.Position.y.ToFloat(),
                            hit.Position.z.ToFloat());
                    }
                }
            }

            return new Vector3(
                traj.finalPosition.x.ToFloat(),
                traj.finalPosition.y.ToFloat(),
                traj.finalPosition.z.ToFloat());
        }

        // ── Test-accessible properties ─────────────────────────────────────────

        public float OBFreezeHeight         => obFreezeHeightAboveTerrain;
        public float CinematicCutFraction   => cinematicCutAtCarryFraction;
        public float MinCarryForCinematic   => minCarryForCinematicMeters;
        public float DownrangePastLanding   => downrangePastLandingMeters;
        public float DownrangeHeight        => downrangeHeightMeters;
    }

    // ── Production adapter (wraps PhysicsLabController as IControllerAccessor) ──

    internal sealed class PhysicsLabControllerAdapter : IControllerAccessor
    {
        readonly PhysicsLabController _ctrl;
        public PhysicsLabControllerAdapter(PhysicsLabController ctrl) => _ctrl = ctrl;

        public Golfin.Gameplay.Loop.BallStateMachine BallSM           => _ctrl.BallSM;
        public Trajectory                            LastTrajectory    => _ctrl.LastTrajectory;
        public Vector3                               LastShotOrigin    => _ctrl.LastShotOrigin;
        public Vector3                               LastShotLaunchDir => _ctrl.LastShotLaunchDir;
        public Transform                             CurrentBall       => _ctrl.CurrentBall;
        public bool                                  CurrentShotIsPutt => _ctrl.CurrentShotIsPutt;
    }
}
