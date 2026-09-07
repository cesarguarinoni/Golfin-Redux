using UnityEngine;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.Input
{
    /// <summary>
    /// Flick's pull → power mapping, and its exact inverse.
    ///
    /// <para>REST-RELATIVE, NOT BASE-RELATIVE — that is the whole file. Flick used to read
    /// <c>power = 1 - handleY / ConeHeightPx</c>, which measures from the cone BASE, so the club
    /// already read 31.8% the instant it was touched (the rest sits at 0.6818 x 792 for the 540px
    /// pull parity <c>flick_shot_view</c> chose) and there was no 120% at all — the handle path
    /// clamped at 1.0 where the finger ran out of cone. Cesar, 2026-09-07: <i>"way too high"</i>.
    /// Power is now the distance the finger has TRAVELLED from the club's rest, which is what the
    /// other three schemes have always measured, and the base of the cone is 120%.</para>
    ///
    /// <para>SAME SHAPE AS <c>ShotController.ComputePower</c> and <c>PendulumMath.Power</c> — dead
    /// zone, linear to 100%, then a 0.2-wide overpower ramp — against Flick's own thresholds.
    /// It does NOT call <c>ShotController.ComputePower</c>: that one is private and reads the
    /// RAW-TOUCH keys (<c>Max100PercentPullPx</c> / <c>MaxOverpowerPullPx</c>), which belong to the
    /// gesture path, not to the club handle. <see cref="ControlsConfig.MinUsefulPullPx"/> IS
    /// shared, deliberately — the dead zone is the same 40px of thumb slop in either path, and a
    /// fifth copy of "40" would be a key nobody ever retunes.</para>
    ///
    /// <para>IN THE INPUT ASSEMBLY, beside <see cref="FlickMath"/>, for the same reason that one
    /// is: Flick has no driver of its own, and both the UI (<c>ShotConeView</c>,
    /// <c>ClubHandleDragger</c>) and the editor verification bots have to read ONE copy of the
    /// mapping. <c>Golfin.Gameplay.UI</c> already references this assembly; the reverse does not
    /// exist and cannot be added.</para>
    /// </summary>
    public static class FlickPullMath
    {
        /// <summary>
        /// Finger travel from the club's REST, in cone-local px, → power 0..1.2.
        ///
        /// <para>0 at touch and through the <see cref="ControlsConfig.MinUsefulPullPx"/> dead
        /// zone; linear to 1.0 at <see cref="ControlsConfig.FlickPull100Px"/>; linear on to 1.2 at
        /// <see cref="ControlsConfig.FlickPull120Px"/>, which is the cone's base. A putt caps at
        /// 1.0 — the same rule every scheme applies, and the reason the 120% label is hidden on
        /// one.</para>
        /// </summary>
        public static float Power(float pullPx, in ControlsConfig cfg, bool isPutt)
        {
            float minPull = cfg.MinUsefulPullPx;
            float p100    = cfg.FlickPull100Px;
            float p120    = cfg.FlickPull120Px;

            if (pullPx < minPull) return 0f;

            float span = Mathf.Max(p100 - minPull, 1e-3f);
            if (pullPx <= p100) return Mathf.Clamp01((pullPx - minPull) / span);

            if (isPutt) return 1f;

            float overRange = Mathf.Max(p120 - p100, 1e-3f);
            return Mathf.Min(1f + ((pullPx - p100) / overRange) * 0.2f,
                             ShotController.MaxOverpowerNormalized);
        }

        /// <summary>
        /// The exact inverse of <see cref="Power"/>: the pull, in cone-local px from the rest,
        /// that reads <paramref name="power"/>. This is what draws the club, so the drawn club and
        /// the finger coincide at 0%, 100% and 120% rather than merely agreeing in shape.
        ///
        /// <para>THE DEAD ZONE IS A FLAT SEGMENT, so no right-inverse of it can be continuous:
        /// every pull in [0, 40) reads 0, and this returns the one the player is actually looking
        /// at — 0, the rest, where the club is drawn until power starts registering. The club
        /// therefore catches up 40px at the instant the dead zone is cleared. That is the dead
        /// zone made visible rather than a defect; the alternative (returning 40 at power 0) would
        /// hang the club 40px below a finger that has not moved, which fails the "0% at touch"
        /// reading this task exists to deliver.</para>
        /// </summary>
        public static float PullPxForPower(float power, in ControlsConfig cfg, bool isPutt)
        {
            float minPull = cfg.MinUsefulPullPx;
            float p100    = cfg.FlickPull100Px;
            float p120    = cfg.FlickPull120Px;

            if (power <= 0f) return 0f;

            if (power <= 1f) return minPull + power * Mathf.Max(p100 - minPull, 1e-3f);

            if (isPutt) return p100;

            float over = Mathf.Min(power, ShotController.MaxOverpowerNormalized) - 1f;
            return p100 + (over / 0.2f) * Mathf.Max(p120 - p100, 1e-3f);
        }

        /// <summary>
        /// Where the club handle is DRAWN for a given power, in cone-local px (0 = the cone's
        /// base, <c>coneHeightPx</c> = the apex). Clamped into the cone, so a power past 120% —
        /// which <c>SetExternalPower</c> already refuses — cannot draw the club below the base.
        ///
        /// <para>The one conversion <c>ShotConeView</c> and every editor bot that drives a real
        /// drag share, so a bot aiming for 100% puts the finger exactly where the club is drawn at
        /// 100%.</para>
        /// </summary>
        public static float ConeLocalYForPower(float power, float restYPx, float coneHeightPx,
                                               in ControlsConfig cfg, bool isPutt)
            => Mathf.Clamp(restYPx - PullPxForPower(power, cfg, isPutt), 0f, coneHeightPx);

        /// <summary>The club's rest height above the cone base, in cone-local px — the same
        /// <c>FlickHandleStartY01 x FlickConeHeightPx</c> <c>ShotConeView.ApplyConfiguredGeometry</c>
        /// resolves, for the callers that have a cone but no view.</summary>
        public static float RestYPx(float coneHeightPx, in ControlsConfig cfg)
            => cfg.FlickHandleStartY01 > 0f ? cfg.FlickHandleStartY01 * coneHeightPx : coneHeightPx;

        /// <summary>The 100% MARK's height above the cone base, in cone-local px — where the
        /// "100%" label sits, and where the club lands at a 100% pull. The rest is
        /// <c>FlickPull120Px</c> above the base (D2), so a 100% pull ends
        /// <c>Pull120 - Pull100</c> = 108px up. DERIVED, never authored: a retune of either
        /// threshold moves the label and the power it names together.</summary>
        public static float Mark100YPx(in ControlsConfig cfg) => cfg.FlickPull120Px - cfg.FlickPull100Px;

        /// <summary>The 120% MARK's height above the cone base: zero, because the base IS 120%
        /// (D2). Named rather than inlined so the pair reads as one statement at the call site.</summary>
        public static float Mark120YPx(in ControlsConfig cfg) => 0f;
    }
}
