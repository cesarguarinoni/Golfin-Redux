// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — ITournamentBackend
// The single backend seam (GDD §8). v1: LocalTournamentBackend (T4).
// Later: RemoteTournamentBackend (REST). UI code never changes.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Single backend seam for the entire tournament system (GDD §8).
    /// <para>
    /// v1 implementation: <c>LocalTournamentBackend</c> (T4) — CSV-loaded definitions,
    /// deterministic bot field, player entry in <c>Golfin.Save</c>.
    /// Future: <c>RemoteTournamentBackend</c> — same interface, REST transport.
    /// UI screens and all callers are unchanged on the swap.
    /// </para>
    /// <para>
    /// All method signatures are verbatim from GDD §8, return types use SPEC §2 DTOs.
    /// </para>
    /// </summary>
    public interface ITournamentBackend
    {
        /// <summary>
        /// Returns the full list of known tournaments, each with its derived state.
        /// State derivation (<see cref="TournamentState"/>) is performed by the backend
        /// using <see cref="ITournamentClock.UtcNow"/> — callers receive ready-to-bind data.
        /// </summary>
        IReadOnlyList<TournamentDefinition> GetTournaments();

        /// <summary>
        /// Returns the definition for a single tournament by <paramref name="id"/>.
        /// Throws <c>KeyNotFoundException</c> (or returns null in stub) if not found.
        /// </summary>
        TournamentDefinition GetTournament(string id);

        /// <summary>
        /// Registers the player for a tournament.
        /// <list type="bullet">
        ///   <item><description>Debits <paramref name="entryPaymentRP"/> from RewardPointsManager if &gt; 0 (T4).</description></item>
        ///   <item><description>Locks <paramref name="characterId"/> for the duration of the entry (GDD Decision S1).</description></item>
        ///   <item><description>Creates and persists an <see cref="EntryState"/> with <see cref="EntryStatus.InProgress"/>.</description></item>
        ///   <item><description>Idempotent: re-registering an existing entry returns the existing state without double-charging.</description></item>
        /// </list>
        /// </summary>
        /// <param name="id">Tournament id.</param>
        /// <param name="entryPaymentRP">RP to debit (0 for free entry).</param>
        /// <param name="characterId">Character to lock for this entry.</param>
        /// <returns>The newly created (or existing) entry state.</returns>
        EntryState Register(string id, long entryPaymentRP, string characterId);

        /// <summary>
        /// Returns the player's current entry state for the tournament, or null if
        /// not registered.
        /// Used for resume: callers check <see cref="EntryState.PerHole"/> to know
        /// which hole to resume from.
        /// </summary>
        EntryState? GetMyEntry(string id);

        /// <summary>
        /// Appends a completed hole result to the player's entry and persists it.
        /// <list type="bullet">
        ///   <item><description>Local v1: writes to <c>Golfin.Save</c> immediately after append.</description></item>
        ///   <item><description>Remote v1+: POSTs the hole result to the server.</description></item>
        ///   <item><description>If all holes are complete, sets status to <see cref="EntryStatus.Finished"/>.</description></item>
        /// </list>
        /// </summary>
        /// <param name="id">Tournament id.</param>
        /// <param name="result">Completed hole result (must include rngSeed + inputLog).</param>
        /// <returns>Updated entry state after appending the result.</returns>
        EntryState SubmitHoleResult(string id, HoleResult result);

        /// <summary>
        /// Returns the current leaderboard for a tournament.
        /// <list type="bullet">
        ///   <item><description>While the window is open: provisional board — bots projected via their seeded schedule at <c>ITournamentClock.UtcNow</c> (GDD §7 organic reveal).</description></item>
        ///   <item><description>After <c>endUtc</c>: final board — all bots fully revealed, player's submitted entry ranked.</description></item>
        ///   <item><description>DNF entries are hidden from ranked rows (GDD §17.4); the player's own DNF is visible in the sticky "you" row.</description></item>
        /// </list>
        /// </summary>
        IReadOnlyList<TournamentLeaderboardEntry> GetLeaderboard(string id);

        /// <summary>
        /// Returns the player's final result for a resolved tournament.
        /// Only meaningful when the tournament is in a Resolved/Ended state
        /// (<c>now &gt;= endUtc + resolveDelay</c>). Returns null if not yet resolved or
        /// if the player did not enter.
        /// </summary>
        TournamentResult? GetResults(string id);

        /// <summary>
        /// Claims the player's prize for a resolved tournament.
        /// <list type="bullet">
        ///   <item><description>Grants RP via RewardPointsManager (T4).</description></item>
        ///   <item><description>Grants item reward via inventory system if applicable (T4).</description></item>
        ///   <item><description>Sets <see cref="TournamentResult.Claimed"/> = true (one-time-grant guard).</description></item>
        ///   <item><description>No-op if already claimed (idempotent).</description></item>
        ///   <item><description>GDD §17.6: in v1, auto-claim is triggered from the result modal; this method is the backend hook.</description></item>
        /// </list>
        /// </summary>
        void ClaimPrize(string id);
    }

    /// <summary>
    /// Optional companion to <see cref="ITournamentBackend"/>: derives the display
    /// <see cref="TournamentState"/> for a definition at a given instant.
    /// <para>
    /// Split out of the backend interface (rather than added to it) so the selection screen can ask
    /// for it without caring WHICH backend it holds. Before this existed the screen cast to
    /// <c>LocalTournamentBackend</c> and silently dropped to a lesser fallback the moment the remote
    /// backend wrapped it — losing the <c>Playing</c> state for every entered tournament.
    /// </para>
    /// </summary>
    public interface ITournamentStateDeriver
    {
        /// <summary>Derive the state badge for <paramref name="def"/> at <paramref name="now"/> (UTC).</summary>
        TournamentState DeriveState(TournamentDefinition def, System.DateTime now);
    }
}
