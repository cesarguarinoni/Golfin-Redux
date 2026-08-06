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

        // (OBFreeze framing field deleted — OB no longer teleports to a pivot; K10 follow-up.)

        [Header("Water entry (water_entry_presentation)")]
        [Tooltip("Seconds the camera stays live after a Water OB before it freezes. Covers the " +
                 "splash (~0.8s) and the ball sink (0.5s). Non-water OB still freezes instantly.")]
        [SerializeField] float _waterHoldSeconds = 1.1f;

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
                // K10 follow-up (Cesar ruling 2026-08-05): on OB the camera just STOPS chasing —
                // no aerial OBFreeze pivot teleport (the old midpoint-25m pivot read as a jarring
                // top-down cut). Chase + the terminal-handler SetTarget(null) = LateUpdate
                // early-return = camera frozen exactly where the chase (with the OB clamp)
                // left it, looking at where the ball went out. Same pattern AtRest uses, and
                // the same fix SmokeRunner2eHost already applied locally after OB→Aiming.
                { BallState.OB,      ChaseCamera.Mode.Chase  },
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

            // K10 ob_recovery_fixes: exit terminal pivot/focus modes when re-entering Aiming.
            // OBFreeze and CupZoom are focus-based modes with NO null-target early-return in
            // ChaseCamera.RunLateUpdateLogic (that guard covers only Chase/GroundLevel). ModeMap
            // leaves Aiming = null ("leave whatever was set"), so a lingering terminal mode keeps
            // running every frame through the entire next aim phase — LateUpdate points the camera
            // back at _shotOrigin (the tee / the cup) and overwrites both the pin-facing re-aim yaw
            // (RepositionBallWithLookDir → ApplyCameraYaw) and the orbit drag (HandleCameraOrbit),
            // which are themselves gated to Chase-only. Switching to Chase + the already-cleared
            // terminal target makes the mode dormant via the null-target early-return, handing the
            // view to the aim owner. CupZoom is the live case (hole-out → next aim phase); the
            // OBFreeze check is retained defensively even though OB now maps to Chase directly
            // (K10 follow-up ruling — OB stops chasing in place, no pivot).
            // Scoped to OBFreeze/CupZoom ONLY — a blanket Aiming→Chase would clobber the null
            // entry that protects putter GroundLevel re-arms (EnterPutterMode sets GroundLevel).
            if (change.Next == BallState.Aiming
                && (setter.CurrentMode == ChaseCamera.Mode.OBFreeze
                 || setter.CurrentMode == ChaseCamera.Mode.CupZoom))
            {
                ApplyMode(ChaseCamera.Mode.Chase);
            }

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

            // OB (K10 follow-up, Cesar ruling 2026-08-05): NO camera work here. The camera
            // simply stops chasing — ModeMap dispatches Chase and the terminal handler below
            // clears the target, so ChaseCamera.LateUpdate early-returns and the transform
            // stays exactly where the clamped chase left it. The former OBFreeze pivot
            // (ComputeOBFreezePivot midpoint-25m aerial) produced the jarring top-down cut.
            // ResetToOrigin is intentionally NOT called: the dormant camera writes nothing,
            // and the next shot's Aiming→Flying ArmChaseForShot resets origin/velocity/clamp.

            // Apply mode mapping (null = leave unchanged).
            if (ModeMap.TryGetValue(change.Next, out var mode) && mode.HasValue)
            {
                ApplyMode(mode.Value);
            }

            // Pre-iter-3 behavior: clear target on ALL terminal states. Aiming-camera owner
            // (ApplyCameraYaw) takes over via ChaseCamera.LateUpdate's null-target early-return.
            //
            // water_entry_presentation (Cesar 2026-08-06): "The call I made yesterday was not
            // for the water. Stop the camera on contact but don't freeze until after the splash
            // plays." The K10 stop-chasing ruling stands for every other OB reason. On a WATER
            // OB the camera has already stopped ADVANCING — the chase clamp pins its position at
            // the water-entry point — so all we defer is the hard freeze: keep the target for
            // _waterHoldSeconds so the camera stays live through the splash and the ball's sink,
            // then clear it and freeze exactly as before.
            if (change.Next == BallState.OB
             && change.OBReason.HasValue
             && change.OBReason.Value == OBReason.Water
             && isActiveAndEnabled)
            {
                StartCoroutine(ClearTargetAfterWaterHold(setter));
                return;
            }

            if (change.Next == BallState.AtRest
             || change.Next == BallState.InCup
             || change.Next == BallState.OB)
            {
                setter.SetTarget(null);
            }
        }

        /// <summary>
        /// Water-OB freeze delay. The camera keeps rendering (position already pinned by the
        /// chase clamp) until the splash has played, then goes dormant via the null target.
        /// Re-arming a new shot calls ResetToOrigin/SetTarget itself, so a late clear here is
        /// harmless only if it doesn't outlive the hold — hence the short, fixed duration.
        /// </summary>
        System.Collections.IEnumerator ClearTargetAfterWaterHold(IModeSetter setter)
        {
            yield return new WaitForSeconds(_waterHoldSeconds);
            setter.SetTarget(null);
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

        // ComputeOBFreezePivot DELETED (K10 follow-up, 2026-08-05): the OB terminal state no
        // longer teleports the camera to an aerial pivot — it stops chasing in place (Cesar
        // ruling). ChaseCamera.Mode.OBFreeze itself is intentionally left in place (unused by
        // the Director) per the no-ChaseCamera-changes constraint.

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
