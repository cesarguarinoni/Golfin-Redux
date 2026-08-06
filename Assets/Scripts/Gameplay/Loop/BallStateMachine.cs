using System;
using System.Collections.Generic;
using Golfin.Physics;
using Golfin.Physics.Math;
// iter-4: ForceShotCompleteForBot seam added below

namespace Golfin.Gameplay.Loop
{
    /// <summary>
    /// Deterministic, headless-capable ball lifecycle state machine.
    /// Centralizes at-rest detection so downstream Loop v1 consumers
    /// (camera §2b, turn counter §2c, result screen §2d, next-shot handoff §2e)
    /// all subscribe to one authoritative signal.
    ///
    /// Determinism rules:
    ///   - MUST NOT call Time.deltaTime, Time.unscaledDeltaTime, Random.value,
    ///     DateTime.Now, or any Unity API that varies per platform/per frame.
    ///   - The only Unity-side input is the bool animatorIsPlaying flag passed to Tick().
    ///   - MUST NOT modify the input Trajectory or any of its lists.
    ///   - All payload structs are immutable readonly structs.
    /// </summary>
    public sealed class BallStateMachine
    {
        // ── Public state ──────────────────────────────────────────────────────

        public BallState State { get; private set; } = BallState.Aiming;
        public bool      Headless { get; set; } = false;

        public event Action<BallStateChange> OnStateChanged;
        public event Action<ShotResult>      OnShotComplete;

        // ── Private state ─────────────────────────────────────────────────────

        ISurfaceProvider _surfaces;
        ICupDetector     _cupDetector;

        // Computed during OnTrajectoryComputed; drained during Tick or (headless) immediately.
        List<BallStateChange> _pendingTransitions = new List<BallStateChange>();
        ShotResult            _pendingResult;

        // For Tick() falling-edge detection.
        bool _prevAnimatorPlaying;

        // ── Construction ───────────────────────────────────────────────────────

        public BallStateMachine(ISurfaceProvider surfaces, ICupDetector cupDetector = null)
        {
            _surfaces    = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            _cupDetector = cupDetector ?? new NullCupDetector();
        }

        // ── Provider swaps ─────────────────────────────────────────────────────

        /// <summary>Swap the cup detector at runtime (e.g. when a hole loads).</summary>
        public void SetCupDetector(ICupDetector cupDetector)
            => _cupDetector = cupDetector ?? new NullCupDetector();

        /// <summary>Swap the surface provider at runtime (hole load/unload).</summary>
        public void SetSurfaceProvider(ISurfaceProvider surfaces)
            => _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));

        // ── Core API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called by PhysicsLabController immediately after BallSimulation.Simulate() returns
        /// AND after BallAnimator.Play() has spawned the new ball Transform. Pre-computes the
        /// canonical transition list from the trajectory + cup scan and stores it for live polling.
        /// Caller MUST invoke this synchronously while the animator is still playing the new shot,
        /// so the falling-edge detection in Tick() correctly fires when the animation later completes.
        /// In Headless mode, drains all transitions synchronously before returning.
        /// </summary>
        public void OnTrajectoryComputed(fp3 startPos, Trajectory trajectory, fp ballRadius)
        {
            _pendingTransitions.Clear();

            // ── Classify start surface ────────────────────────────────────────
            SurfaceType startSurface = _surfaces.Classify(startPos.x, startPos.z);
            int bounceCount = 0;

            // ── First transition: Aiming → Flying ─────────────────────────────
            var aimToFlying = new BallStateChange(
                BallState.Aiming, BallState.Flying,
                startPos, startSurface, null, fp.Zero);
            _pendingTransitions.Add(aimToFlying);

            // ── Build intermediate Flying↔Rolling transitions from terrainHits ─
            // Each non-stop TerrainHit represents ONE complete bounce: the ball was
            // Flying, it touches the ground (→ Rolling), then relaunches (→ Flying).
            // For the last non-stop hit before a stop hit, the ball lands (→ Rolling)
            // and rolls to rest (no relaunch). The stop hit then produces →AtRest.
            //
            // So each non-stop hit emits TWO transitions (land + relaunch), except
            // the final one which emits only ONE (land into roll phase before stop).
            BallState current = BallState.Flying;
            int terminalHitIndex = -1;

            if (trajectory.terrainHits != null)
            {
                for (int i = 0; i < trajectory.terrainHits.Count; i++)
                {
                    var hit = trajectory.terrainHits[i];
                    if (hit.IsStop)
                    {
                        terminalHitIndex = i;
                        break;
                    }
                    bounceCount++;

                    // Non-stop hit: ball was Flying, touches ground → Rolling.
                    _pendingTransitions.Add(new BallStateChange(
                        current, BallState.Rolling,
                        hit.Position, hit.Surface, null, hit.Time));
                    current = BallState.Rolling;

                    // Determine if this is the last non-stop hit before a stop.
                    // Look ahead: if next hit is stop or there are no more hits, no relaunch.
                    bool nextIsStop = (i + 1 >= trajectory.terrainHits.Count)
                                   || trajectory.terrainHits[i + 1].IsStop;
                    if (!nextIsStop)
                    {
                        // Ball relaunches — Rolling → Flying.
                        _pendingTransitions.Add(new BallStateChange(
                            BallState.Rolling, BallState.Flying,
                            hit.Position, hit.Surface, null, hit.Time));
                        current = BallState.Flying;
                    }
                    // If nextIsStop: current stays Rolling; stop hit below handles terminal.
                }
            }

            // ── Determine terminal state ───────────────────────────────────────
            BallState   terminalState;
            OBReason?   obReason    = null;
            fp3         terminalPos = trajectory.finalPosition;
            fp          terminalTime = trajectory.finalTime;
            SurfaceType terminalSurface;

            switch (trajectory.termination)
            {
                case TerminationReason.HitWater:
                    terminalState   = BallState.OB;
                    obReason        = Golfin.Gameplay.Loop.OBReason.Water;
                    terminalSurface = SurfaceType.Water;
                    // Use last hit position if available, otherwise finalPosition.
                    if (trajectory.terrainHits != null && trajectory.terrainHits.Count > 0)
                    {
                        var lastHit = trajectory.terrainHits[trajectory.terrainHits.Count - 1];
                        terminalPos  = lastHit.Position;
                        terminalTime = lastHit.Time;
                        terminalSurface = lastHit.Surface;
                    }
                    break;

                case TerminationReason.HitOOB:
                    terminalState   = BallState.OB;
                    obReason        = Golfin.Gameplay.Loop.OBReason.OutOfBounds;
                    terminalSurface = SurfaceType.OOB;
                    if (trajectory.terrainHits != null && trajectory.terrainHits.Count > 0)
                    {
                        var lastHit = trajectory.terrainHits[trajectory.terrainHits.Count - 1];
                        terminalPos  = lastHit.Position;
                        terminalTime = lastHit.Time;
                        terminalSurface = lastHit.Surface;
                    }
                    break;

                // cup_capture_and_lipout (2026-08-05): the sim itself captured the ball and
                // synthesized the fall-in, so the trajectory ENDS at the cup bottom. Trust the
                // termination directly instead of re-deriving it from the sample scan — the
                // scan's height gate is decided by a sub-millimetre baked-height-vs-pin-Y
                // margin (see Docs/Physics/CUP_CAPTURE_STEP0_DIAGNOSIS.md), whereas the sim's
                // check is XZ-only and cannot miss. Because the trajectory now ends at the cup,
                // the existing animator falling-edge in Tick() fires InCup exactly when the
                // drop finishes on screen — no downstream timing changes needed
                // (HoleCompletionBridge / LoopCameraDirector / modal flow all key off InCup).
                case TerminationReason.CupCapture:
                    terminalState   = BallState.InCup;
                    terminalPos     = trajectory.finalPosition;
                    terminalTime    = trajectory.finalTime;
                    terminalSurface = SurfaceType.Green;
                    break;

                case TerminationReason.ExitedWorldBounds:
                    terminalState   = BallState.OB;
                    obReason        = Golfin.Gameplay.Loop.OBReason.ExitedWorldBounds;
                    terminalSurface = SurfaceType.OOB;
                    break;

                default:
                    // BallStopped, MaxDurationReached, MaxBouncesExceeded, HitGround
                    // — scan for cup first.
                    fp3?         cupPos  = null;
                    fp?          cupTime = null;
                    SurfaceType  cupSurface = SurfaceType.Green;

                    if (trajectory.samples != null)
                    {
                        for (int si = 0; si < trajectory.samples.Count; si++)
                        {
                            var sample = trajectory.samples[si];
                            // Use velocity-aware overload so the speed gate (USGA lip-out rule)
                            // is applied: fast putts above CupCaptureSpeed do not register InCup.
                            if (_cupDetector.IsInCup(sample.position, ballRadius, sample.velocity))
                            {
                                cupPos     = sample.position;
                                cupTime    = sample.time;
                                cupSurface = _surfaces.Classify(sample.position.x, sample.position.z);
                                break;
                            }
                        }
                    }

                    if (cupPos.HasValue)
                    {
                        terminalState   = BallState.InCup;
                        terminalPos     = cupPos.Value;
                        terminalTime    = cupTime.Value;
                        terminalSurface = cupSurface;
                    }
                    else
                    {
                        terminalState   = BallState.AtRest;
                        terminalPos     = trajectory.finalPosition;
                        terminalTime    = trajectory.finalTime;
                        terminalSurface = _surfaces.Classify(
                            trajectory.finalPosition.x, trajectory.finalPosition.z);
                    }
                    break;
            }

            // ── Append terminal transition ─────────────────────────────────────
            _pendingTransitions.Add(new BallStateChange(
                current, terminalState,
                terminalPos, terminalSurface, obReason, terminalTime));

            // ── Build ShotResult ──────────────────────────────────────────────
            _pendingResult = new ShotResult(
                terminalState, obReason,
                startPos, terminalPos,
                startSurface, terminalSurface,
                terminalTime, bounceCount);

            // ── Fire first transition (Aiming → Flying) synchronously ─────────
            if (!Headless)
            {
                var first = _pendingTransitions[0];
                _pendingTransitions.RemoveAt(0);
                State = first.Next;
                OnStateChanged?.Invoke(first);
                _prevAnimatorPlaying = true; // animator hasn't started yet; primes the falling-edge
            }
            else
            {
                // Headless: drain everything synchronously right now.
                DrainPendingTransitions();
            }
        }

        /// <summary>
        /// Called once per Update tick by the owner. Inspects the polled animator state and
        /// fires queued transitions when the animator finishes (non-headless path).
        /// In Headless mode, this is a no-op.
        /// </summary>
        public void Tick(bool animatorIsPlaying)
        {
            if (Headless) return;
            if (_pendingTransitions.Count == 0)
            {
                _prevAnimatorPlaying = animatorIsPlaying;
                return;
            }

            // Falling edge: animator was playing, now it's not → drain all remaining transitions.
            bool fallingEdge = _prevAnimatorPlaying && !animatorIsPlaying;
            _prevAnimatorPlaying = animatorIsPlaying;

            if (fallingEdge)
            {
                DrainPendingTransitions();
            }
        }

        /// <summary>
        /// Forces transition back to Aiming. Called by the owner after handling AtRest/OB,
        /// or by §2d after handling InCup.
        /// </summary>
        public void ReArm()
        {
            BallState from = State;
            // Only valid from terminal states or (for safety) Aiming itself.
            if (from == BallState.Aiming) return;

            State = BallState.Aiming;
            _pendingTransitions.Clear();
            _prevAnimatorPlaying = false;

            var change = new BallStateChange(
                from, BallState.Aiming,
                fp3.Zero, SurfaceType.Fairway, null, fp.Zero);
            OnStateChanged?.Invoke(change);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Smoke-bot test seam. Synthesizes a ShotResult and invokes OnShotComplete
        /// to drive subscribers (HoleCompleteWidget etc.) deterministically without
        /// running physics. ONLY for editor-time bot scenarios where the gate under
        /// test is downstream of terminal-state observation (modal wiring, scene
        /// unload, reward grant). Production shot path is unchanged.
        ///
        /// Seam principle compliance (five conditions, all must hold):
        ///   (i)   Isolates one real unit: "modal subscribes to InCup event" — not physics.
        ///   (ii)  Production path (FireShot via PhysicsLabController) remains the default.
        ///   (iii) #if UNITY_EDITOR — compiler-level proof it cannot leak to player builds.
        ///   (iv)  Named _ForBot — grep-visible signal to reviewer and future maintainer.
        ///   (v)   Delegates to OnShotComplete — the SAME event production uses. Modal sees no difference.
        /// </summary>
        public void ForceShotCompleteForBot(BallState terminalState)
        {
            var result = new ShotResult(
                terminalState,
                obReason: null,
                startPosition: fp3.Zero,
                endPosition: fp3.Zero,
                startSurface: SurfaceType.Green,
                endSurface: SurfaceType.Green,
                simDuration: fp.Zero,
                bounceCount: 0);
            OnShotComplete?.Invoke(result);
        }
#endif

        // ── Internal helpers ───────────────────────────────────────────────────

        void DrainPendingTransitions()
        {
            // Fire every pending transition in order.
            for (int i = 0; i < _pendingTransitions.Count; i++)
            {
                var change = _pendingTransitions[i];
                State = change.Next;
                OnStateChanged?.Invoke(change);
            }
            _pendingTransitions.Clear();

            // Fire OnShotComplete exactly once with the terminal payload.
            OnShotComplete?.Invoke(_pendingResult);
        }
    }
}
