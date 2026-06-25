// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — ITournamentClock
// Single time seam for the entire tournament system (GDD §4).
// Wraps ITimeProvider from Golfin.UI.Rankings.Core (SPEC §1 flag resolved:
// option (b) — ITimeProvider already lives in the lean leaf asmdef
// Golfin.UI.Rankings.Core (noEngineReferences:true, zero deps), so referencing
// it does NOT introduce a UI controller dependency. Golfin.Tournaments →
// Golfin.UI.Rankings.Core is a leaf-to-leaf reference, not a UI dep.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using Golfin.UI.Rankings;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Authoritative UTC clock for all tournament window checks, state derivation,
    /// and organic bot-reveal projection (GDD §4, §7).
    /// <para>
    /// v1 implementation wraps <see cref="NetworkTimeProvider"/> (device clock + HTTP
    /// Date header offset). Later: NTP / server time. Swapping the impl hardens every
    /// window check at once without touching any caller.
    /// </para>
    /// <para>
    /// All leaderboard projection, state derivation, and "ends soon" warnings read
    /// <see cref="UtcNow"/> through this seam — never <c>DateTime.UtcNow</c> directly.
    /// </para>
    /// <para>
    /// <b>Asmdef boundary note:</b> <see cref="Golfin.UI.Rankings.ITimeProvider"/> lives
    /// in <c>Golfin.UI.Rankings.Core</c> (<c>noEngineReferences:true</c>, zero external
    /// dependencies). Referencing it from <c>Golfin.Tournaments</c> does NOT create a
    /// UI/screen dependency — the Core asmdef contains only interfaces and pure data.
    /// This resolves the SPEC §1 flag: option (b) "extract if cheap" is already satisfied
    /// by the existing Core split.
    /// </para>
    /// </summary>
    public interface ITournamentClock
    {
        /// <summary>
        /// Current authoritative UTC time.
        /// Callers must use this; never call <c>DateTime.UtcNow</c> in tournament logic.
        /// </summary>
        DateTime UtcNow { get; }
    }

    /// <summary>
    /// Default ITournamentClock implementation: adapts <see cref="ITimeProvider"/>
    /// (the existing Rankings time seam) to the tournament clock interface.
    /// </summary>
    public sealed class TimeProviderClock : ITournamentClock
    {
        private readonly ITimeProvider _provider;

        public TimeProviderClock(ITimeProvider provider)
        {
            _provider = provider;
        }

        /// <inheritdoc/>
        public DateTime UtcNow => _provider.UtcNow;
    }
}
