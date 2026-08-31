// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ScheduleRefreshThrottle
// The "is another fetch allowed right now?" decision, extracted from
// TournamentService so it is unit-testable without a MonoBehaviour, a clock, or
// a socket. Pure C#: no UnityEngine dependency, time is passed in.
//
// ⚠️ IT LIVES IN Golfin.Content, AND ITS NAMESPACE IS STILL Golfin.Tournaments.
// gacha_ops_polish §4c gave it a SECOND caller — `ContentService.RefreshNow()`
// — and ContentService is in the `Golfin.Content` asmdef, which cannot see
// Assembly-CSharp, where this file used to sit (Assets/Scripts/TournamentsRuntime
// has no asmdef). Moving the FILE and keeping the namespace is what let both
// callers share ONE implementation instead of growing a second copy of an
// in-flight guard and a cooldown: `Golfin.Content` is autoReferenced, so
// `TournamentService` still compiles untouched, `Golfin.Tournaments.Tests`
// already references Golfin.Content, and `ScheduleRefreshTests` resolves it by
// scanning every loaded assembly for the full type name.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Gates schedule refetches with an in-flight guard plus a cooldown.
    /// <para>
    /// Bouncing Home → Tournaments → Home → Tournaments is ordinary player behaviour, and with a
    /// refetch wired to screen entry it would otherwise be one request per bounce. Two independent
    /// guards, because they cover different shapes of the same problem:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>In flight</b> — re-entry during a slow request must not queue a second one.
    ///   Nothing about the first request having not finished yet makes a second one more useful.</item>
    ///   <item><b>Cooldown</b> — re-entry shortly after one settled is answered from what is already
    ///   in memory. The schedule changes on an admin's timescale, not a player's.</item>
    /// </list>
    /// <para>
    /// <b>The cooldown is measured from when an attempt SETTLES, not from when it succeeds.</b> The
    /// spec phrased it as "the last successful fetch", but a failure that does not arm the cooldown
    /// is exactly the retry storm the spec also forbids: in airplane mode <c>UnityWebRequest</c>
    /// fails almost instantly, so five screen entries in ten seconds would be five requests. Failing
    /// arms it too — the player keeps the cached list either way, which is the designed behaviour.
    /// </para>
    /// </summary>
    public sealed class ScheduleRefreshThrottle
    {
        /// <summary>
        /// Minimum seconds between two schedule fetches. One constant, referenced everywhere —
        /// see <see cref="TournamentService.ScheduleRefreshCooldownSeconds"/> for the caller.
        /// </summary>
        public const double DefaultCooldownSeconds = 60.0;

        private readonly double _cooldownSeconds;

        // NegativeInfinity, not 0: at t=0 (the boot fetch) `now - _settledAt` must read as
        // "long ago", and 0 - 0 = 0 would block the very first fetch of the session.
        private double _settledAt = double.NegativeInfinity;
        private bool   _inFlight;

        public ScheduleRefreshThrottle(double cooldownSeconds = DefaultCooldownSeconds)
        {
            if (cooldownSeconds < 0.0)
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds));
            _cooldownSeconds = cooldownSeconds;
        }

        /// <summary>True while a fetch begun through <see cref="TryBegin"/> has not yet settled.</summary>
        public bool InFlight => _inFlight;

        public double CooldownSeconds => _cooldownSeconds;

        /// <summary>
        /// Claim the right to fetch. Returns false — and changes nothing — when a fetch is already
        /// in flight or the last one settled less than <see cref="CooldownSeconds"/> ago.
        /// </summary>
        /// <param name="now">A monotonic seconds clock (production passes
        /// <c>Time.realtimeSinceStartupAsDouble</c>).</param>
        public bool TryBegin(double now)
        {
            if (_inFlight) return false;
            if (now - _settledAt < _cooldownSeconds) return false;

            _inFlight = true;
            return true;
        }

        /// <summary>
        /// Release the in-flight guard and arm the cooldown, whether the fetch succeeded or failed.
        /// Safe to call without a matching <see cref="TryBegin"/>.
        /// </summary>
        public void Settle(double now)
        {
            _inFlight  = false;
            _settledAt = now;
        }

        /// <summary>Back to the never-fetched state. Tests and hard schedule invalidation only.</summary>
        public void Reset()
        {
            _inFlight  = false;
            _settledAt = double.NegativeInfinity;
        }

        /// <summary>Seconds until <see cref="TryBegin"/> would pass the cooldown. Diagnostics.</summary>
        public double SecondsUntilAllowed(double now)
        {
            double remaining = _cooldownSeconds - (now - _settledAt);
            return remaining > 0.0 ? remaining : 0.0;
        }
    }
}
