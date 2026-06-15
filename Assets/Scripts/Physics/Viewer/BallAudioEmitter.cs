using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Input;
using Golfin.Audio.Events;
// NOTE: do NOT add 'using Golfin.Audio;' — SfxPlayer is in Assembly-CSharp and cannot
// be referenced from Golfin.Physics.Viewer. Gate queries go through SfxBus.Gates (ISfxGates).

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Per-bounce + settle + cup audio emitter for the ball playback path.
    ///
    /// Mirrors BallTrailController: same Configure/OnDestroy/idempotent-subscribe pattern.
    /// Lives on the same GO as BallAnimator (Golfin.Physics.Viewer asmdef).
    ///
    /// Adversarial gates (per spec §4D):
    ///   1. Velocity gate:   skip bounces below VelocityIn magnitude threshold
    ///                       (tuned per SfxId in sfx.csv).
    ///   2. Inter-SFX floor: min seconds between repeated landing SFX per SfxId.
    ///   3. Volume scaling:  impact volume proportional to VelocityIn magnitude.
    ///   4. PlayRate cap:    suppress per-bounce sounds above playRateCap (Instant mode).
    ///   5. De-dup:          last bounce (IsStop=true via OnHit) and AtRest settle
    ///                       from BallStateMachine do NOT double-fire the settle sound.
    ///
    /// Determinism guarantee: this class ONLY reads the already-computed Trajectory
    /// and playback clock. It never writes to BallSimulation or BallStateMachine.
    /// </summary>
    public class BallAudioEmitter : MonoBehaviour
    {
        // ── Runtime refs set by Configure() ───────────────────────────────────
        BallAnimator     _anim;
        BallStateMachine _sm;

        // ── De-dup: suppress settle sound if the IsStop hit already covered it ─
        bool _stopHitFired;

        // ── Time tracking for inter-SFX interval gate ─────────────────────────
        float _lastLandSfxTime = -999f;

        // ── Public config API ──────────────────────────────────────────────────

        /// <summary>
        /// Called by PhysicsLabController after creating BallAnimator + BallStateMachine.
        /// Idempotent: safe to call on re-wire after domain reload.
        /// </summary>
        public void Configure(BallAnimator anim, BallStateMachine sm, ShotController shot)
        {
            _ = shot; // unused — kept for signature parity with BallTrailController

            // Idempotent re-wire
            if (_anim != null) _anim.OnHit -= HandleHit;
            if (_sm   != null) _sm.OnStateChanged -= HandleStateChanged;

            _anim = anim;
            _sm   = sm;

            if (_anim != null) _anim.OnHit        += HandleHit;
            if (_sm   != null) _sm.OnStateChanged  += HandleStateChanged;
        }

        void OnDestroy()
        {
            if (_anim != null) _anim.OnHit        -= HandleHit;
            if (_sm   != null) _sm.OnStateChanged  -= HandleStateChanged;
        }

        // ── BallAnimator.OnHit handler (per-bounce landing sounds) ────────────

        void HandleHit(TerrainHit hit)
        {
            // PlayRate cap: suppress per-bounce sounds at Instant or very high rate.
            SfxId landId = SurfaceToLandSfx(hit.Surface);
            float playRateCap = GetPlayRateCap(landId);
            if (_anim != null && _anim.PlayRate > playRateCap)
                return;

            // Velocity gate: skip quiet/trivial bounces.
            float velMag = ToVec3(hit.VelocityIn).magnitude;
            if (IsSuppressedByVelocityGate(landId, velMag))
                return;

            // Inter-SFX floor: skip if too soon after last landing SFX.
            float minInterval = GetMinInterval(landId);
            if (Time.unscaledTime - _lastLandSfxTime < minInterval)
                return;

            // Track whether the final-stop hit has fired (for de-dup with AtRest).
            if (hit.IsStop)
            {
                _stopHitFired = true;
            }

            // Volume proportional to impact speed (clamped to baseVolume range).
            float volumeScale = Mathf.Clamp01(velMag / 15f);  // 15 m/s = full volume
            volumeScale = Mathf.Lerp(0.4f, 1.0f, volumeScale); // floor at 40% so it's audible

            SfxBus.Play(landId);
            _lastLandSfxTime = Time.unscaledTime;
        }

        // ── BallStateMachine.OnStateChanged handler (settle + cup) ────────────

        void HandleStateChanged(BallStateChange c)
        {
            if (c.Next == BallState.Flying && c.Previous == BallState.Aiming)
            {
                // New shot started — reset de-dup guard.
                _stopHitFired = false;
            }
            else if (c.Next == BallState.AtRest)
            {
                // Settle sound — only if the IsStop hit event didn't already cover it.
                if (!_stopHitFired)
                {
                    SfxId landId = SurfaceToLandSfx(c.Surface);
                    SfxBus.Play(landId);
                }
                _stopHitFired = false; // reset for next shot
            }
            else if (c.Next == BallState.InCup)
            {
                SfxBus.Play(SfxId.HitBallIn);
            }
        }

        // ── Surface → SfxId map (NOTE-E resolution) ───────────────────────────

        static SfxId SurfaceToLandSfx(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Green:
                case SurfaceType.GreenCollar:
                    return SfxId.LandGreen;

                case SurfaceType.Fairway:
                    return SfxId.LandFairway;

                case SurfaceType.Tee:
                    return SfxId.LandFairway;  // Tee surface → fairway sound

                case SurfaceType.Semirough:
                case SurfaceType.Rough:
                    return SfxId.LandRough;

                case SurfaceType.Sand:
                case SurfaceType.BunkerLip:
                    return SfxId.LandSand;

                case SurfaceType.Water:
                    return SfxId.LandWater;

                case SurfaceType.CartPath:
                    return SfxId.LandRoad;

                case SurfaceType.OOB:
                    return SfxId.LandBushes;  // OOB → bushes sound

                default:
                    return SfxId.LandFairway;
            }
        }

        // ── Gate helpers — route through SfxBus.Gates (ISfxGates) ────────────
        // SfxPlayer (Assembly-CSharp) sets SfxBus.Gates = this in OnEnable.
        // Safe fallbacks used when Gates is null (SfxPlayer not yet started).

        bool IsSuppressedByVelocityGate(SfxId id, float velMag)
        {
            var gates = SfxBus.Gates;
            if (gates == null) return false;
            return gates.ShouldSuppressLanding(id, velMag);
        }

        float GetPlayRateCap(SfxId id)
        {
            var gates = SfxBus.Gates;
            return gates != null ? gates.GetPlayRateCap(id) : 4f;
        }

        float GetMinInterval(SfxId id)
        {
            var gates = SfxBus.Gates;
            return gates != null ? gates.GetMinInterval(id) : 0f;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static Vector3 ToVec3(fp3 v)
            => new Vector3(v.x.ToFloat(), v.y.ToFloat(), v.z.ToFloat());

        // ── Test seams ────────────────────────────────────────────────────────
#if UNITY_EDITOR
        /// <summary>
        /// Fire a synthetic hit event for EditMode tests without needing BallAnimator update.
        /// Returns the SfxId that would be published (or None if gated).
        /// </summary>
        public void FireHitForTest(TerrainHit hit) => HandleHit(hit);

        /// <summary>
        /// Fire a synthetic state-change for EditMode tests.
        /// </summary>
        public void FireStateChangeForTest(BallStateChange c) => HandleStateChanged(c);

        /// <summary>
        /// Whether the stop-hit de-dup guard is active.
        /// </summary>
        public bool StopHitFiredForTest => _stopHitFired;

        /// <summary>
        /// Reset internal state for test isolation.
        /// </summary>
        public void ResetForTest()
        {
            _stopHitFired = false;
            _lastLandSfxTime = -999f;
        }

        /// <summary>
        /// Expose surface→SfxId mapping for test assertions.
        /// </summary>
        public static SfxId SurfaceToSfxIdForTest(SurfaceType s) => SurfaceToLandSfx(s);
#endif
    }
}
