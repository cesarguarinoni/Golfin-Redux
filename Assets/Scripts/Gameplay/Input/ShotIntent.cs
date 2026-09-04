namespace Golfin.Gameplay.Input
{
    /// <summary>
    /// One control scheme's finished answer to "what shot did the player just ask for?".
    ///
    /// <para>The control-scheme seam (control_scheme_seam SPEC §3.1). Everything BEFORE this
    /// struct — pull pixels, arrow passes, a pendulum marker, a needle tap — is the scheme.
    /// Everything AFTER it — <c>ShotInputBuilder.Build</c>, the physics sim, telemetry, SFX,
    /// the tournament <c>ShotCommand</c>, stamina — is shared by every scheme and lives in
    /// <see cref="ShotController.CommitExternal"/> → <c>ResolveAndPublish</c>.</para>
    ///
    /// <para>Flick does NOT go through here: <see cref="ShotController.EndExternalDrag"/> →
    /// <c>CommitFlick()</c> keeps its own maths and calls the same tail, so the shipping scheme
    /// is byte-identical to what it was before the seam existed.</para>
    /// </summary>
    public readonly struct ShotIntent
    {
        /// <summary>0..1.2. Putts and <c>DebugFlags.DisableOverpower</c> are clamped to 1.0 by
        /// the seam, not by the driver.</summary>
        public readonly float PowerNormalized;

        /// <summary>-1..+1 of <c>HalfConeAngleRad()</c>; fed to the same <c>AimYawFor()</c> the
        /// live targeting line uses, so a driver cannot make the line lie.</summary>
        public readonly float AimOffset01;

        /// <summary>The scheme's own miss, in radians. Added exactly where the flick's per-pass
        /// degradation yaw is added today. 0 = the player nailed it.</summary>
        public readonly float ErrorYawRad;

        /// <summary>Power multiplier the scheme judged this swing worth. 1.0 = clean.</summary>
        public readonly float TimingMul;

        /// <summary>0..1 accuracy measure for telemetry, so one dashboard card reads every
        /// scheme. NaN = "this driver had no timing to judge" (bots, capture, debug shots).</summary>
        public readonly float Timing01;

        /// <summary>-1..+1 curve request. 0 unless the scheme derives fade/draw itself.</summary>
        public readonly float FadeDraw01;

        public ShotIntent(float powerNormalized, float aimOffset01, float errorYawRad,
                          float timingMul, float timing01, float fadeDraw01)
        {
            PowerNormalized = powerNormalized;
            AimOffset01     = aimOffset01;
            ErrorYawRad     = errorYawRad;
            TimingMul       = timingMul;
            Timing01        = timing01;
            FadeDraw01      = fadeDraw01;
        }
    }
}
