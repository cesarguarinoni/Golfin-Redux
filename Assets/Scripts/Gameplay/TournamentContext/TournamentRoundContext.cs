// ─────────────────────────────────────────────────────────────────────────────
// TournamentRoundContext
// Runtime-only static context for an active tournament round.
// Lives in Golfin.Gameplay.TournamentContext (autoReferenced:true) so it is
// visible to both:
//   • Golfin.Gameplay.Input (ShotController.CommitFlick)
//   • Assembly-CSharp (LiveStatProviderHost.ResolveLive — stat seam)
// without any circular asmdef dependency.
//
// Phase 3 (stamina_tournament_wiring):
//   • BeginRound now takes explicit tankMax + remaining from the persisted entry
//     (seeded by TournamentHoleSelectionScreenController).
//   • Drain is per-hole (handled in LocalTournamentBackend.SubmitHoleResult),
//     NOT per-shot — the pool is constant within a hole.
//   • DepleteStamina() is retained as legacy API but is no longer called by
//     ShotController (D4). It is kept dead to avoid breaking any test stubs
//     that call it directly; it will be removed in a future cleanup pass.
//
// Clock-trust-free anti-cheat model (D3 = NO regen):
//   The per-hole condition is reproducible from the persisted entry alone
//   (drain-count × DrainForHole(), frozen snapshot.Stamina for tank).
//   No client-clock reads in BeginRound → future server re-sim never trusts
//   client timestamps. Document preserved here per SPEC §6.
// ─────────────────────────────────────────────────────────────────────────────
using UnityEngine;
using Golfin.Tournaments;

/// <summary>
/// Static round context for an active tournament hole.
/// Set by TournamentHoleSelectionScreenController.BeginTournamentHole before
/// calling GameplaySceneLoader.BeginGameplayLoad.
/// Cleared by TournamentRoundHandler.EndRound or GameSession.ResetSession.
/// </summary>
public static class TournamentRoundContext
{
    // ── Round state ───────────────────────────────────────────────────────────

    /// <summary>True while the player is playing a tournament hole.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>The tournament id active this round.</summary>
    public static string TournamentId { get; private set; } = string.Empty;

    /// <summary>
    /// Frozen character snapshot captured at sign-up.
    /// Only valid when IsActive == true.
    /// </summary>
    public static CharacterSnapshot Snapshot { get; private set; } = null;

    // ── Stamina pool ──────────────────────────────────────────────────────────

    /// <summary>
    /// Flat stamina pool cap fallback (used when StaminaModel is not configured,
    /// or as the neutral reset value on EndRound).
    /// </summary>
    public const float DefaultStaminaMax = 100f;

    /// <summary>
    /// Maximum stamina energy for this entry.
    /// Seeded from StaminaModel.MaxCondition(snapshot.Stamina) via BeginRound (Phase 3).
    /// </summary>
    public static float StaminaEnergyMax { get; private set; } = DefaultStaminaMax;

    /// <summary>
    /// Remaining stamina energy.
    /// Seeded from the persisted entry's ConditionRemaining via BeginRound (Phase 3).
    /// Constant within a hole (per-hole drain happens in the backend at hole-complete,
    /// not per-shot). Consumed by LiveStatProviderHost.ResolveLive for the penalty seam.
    /// </summary>
    public static float StaminaEnergyRemaining { get; private set; } = DefaultStaminaMax;

    /// <summary>
    /// Per-shot stamina cost — retained for legacy/test API compatibility.
    /// No longer used in production (D4: per-shot drain removed; drain is per-hole in backend).
    /// </summary>
    public static float StaminaCostPerShot { get; private set; } = 5f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Begin a tournament round: cache snapshot + seed stamina pool from the persisted entry.
    /// Called by TournamentHoleSelectionScreenController.BeginTournamentHole BEFORE
    /// GameplaySceneLoader.BeginGameplayLoad.
    /// <para>
    /// Phase 3: <paramref name="tankMax"/> and <paramref name="remaining"/> are computed
    /// by the caller from the persisted entry (not reset to flat defaults here).
    /// D3 = NO regen: remaining is seeded straight from the entry value — no time-based
    /// refill, no LastHoleUtc/Recovery read. This makes condition reproducible from
    /// drain-count alone (clock-trust-free, SPEC §6).
    /// </para>
    /// </summary>
    /// <param name="tournamentId">The active tournament identifier.</param>
    /// <param name="snapshot">Frozen character snapshot captured at sign-up.</param>
    /// <param name="tankMax">Full tank size = StaminaModel.MaxCondition(snapshot.Stamina).</param>
    /// <param name="remaining">Current pool from the persisted entry (ConditionRemaining).</param>
    public static void BeginRound(string tournamentId, CharacterSnapshot snapshot, float tankMax, float remaining)
    {
        IsActive               = true;
        TournamentId           = tournamentId ?? string.Empty;
        Snapshot               = snapshot;
        StaminaEnergyMax       = tankMax;
        StaminaEnergyRemaining = remaining;
        Debug.Log($"[TournamentRoundContext] BeginRound tournament={TournamentId} char={snapshot?.CharacterId} " +
                  $"tank={StaminaEnergyMax:F1} remaining={StaminaEnergyRemaining:F1}");
    }

    /// <summary>
    /// Clear all round state (called by TournamentRoundHandler on Finished, or
    /// GameSession.ResetSession for full session teardown).
    /// </summary>
    public static void EndRound()
    {
        Debug.Log($"[TournamentRoundContext] EndRound — was active={IsActive} tournament={TournamentId}");
        IsActive               = false;
        TournamentId           = string.Empty;
        Snapshot               = null;
        StaminaEnergyRemaining = DefaultStaminaMax;
    }

    // ── Stamina (legacy / dead API — D4) ──────────────────────────────────────

    /// <summary>
    /// Per-shot stamina depletion — no longer called in production (D4: drain is per-hole
    /// in LocalTournamentBackend.SubmitHoleResult). Retained for backward-compatibility
    /// with existing tests; will be removed in a future cleanup pass.
    /// </summary>
    public static void DepleteStamina()
    {
        if (!IsActive) return;
        StaminaEnergyRemaining = Mathf.Max(0f, StaminaEnergyRemaining - StaminaCostPerShot);
        Debug.Log($"[TournamentRoundContext] DepleteStamina (legacy) remaining={StaminaEnergyRemaining:F1}/{StaminaEnergyMax:F1}");
    }

    /// <summary>
    /// Overload for tests / explicit amount override.
    /// No longer called in production (D4).
    /// </summary>
    public static void DepleteStamina(float amount)
    {
        if (!IsActive) return;
        StaminaEnergyRemaining = Mathf.Max(0f, StaminaEnergyRemaining - amount);
    }
}
