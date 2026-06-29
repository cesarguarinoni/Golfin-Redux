// ─────────────────────────────────────────────────────────────────────────────
// TournamentRoundContext
// Runtime-only static context for an active tournament round.
// Lives in Golfin.Gameplay.TournamentContext (autoReferenced:true) so it is
// visible to both:
//   • Golfin.Gameplay.Input (ShotController.CommitFlick — stamina depletion)
//   • Assembly-CSharp (LiveStatProviderHost.ResolveLive — stat seam)
// without any circular asmdef dependency.
//
// Stamina pool is runtime-only v1 (not persisted). Disk persistence and a real
// depletion economy are deferred (SPEC §8, D1).
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
    /// Flat stamina pool cap (placeholder v1 — matches charData.maxStaminaEnergy default).
    /// </summary>
    public const float DefaultStaminaMax = 100f;

    /// <summary>Maximum stamina energy for this entry.</summary>
    public static float StaminaEnergyMax { get; private set; } = DefaultStaminaMax;

    /// <summary>
    /// Remaining stamina energy.
    /// Starts at StaminaEnergyMax on BeginRound; depletes per shot; carries hole→hole.
    /// Never persisted; quitting mid-round starts with full pool on resume.
    /// </summary>
    public static float StaminaEnergyRemaining { get; private set; } = DefaultStaminaMax;

    /// <summary>
    /// Per-shot stamina cost (flat placeholder v1).
    /// CSV-tunable via TournamentCsvLoader; default 5f ≈ 20 shots to empty.
    /// Set in BeginRound from the loaded CSV config.
    /// </summary>
    public static float StaminaCostPerShot { get; private set; } = 5f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Begin a tournament round: cache snapshot + seed stamina pool.
    /// Called by TournamentHoleSelectionScreenController.BeginTournamentHole BEFORE
    /// GameplaySceneLoader.BeginGameplayLoad.
    /// </summary>
    public static void BeginRound(string tournamentId, CharacterSnapshot snapshot, float staminaCostPerShot = 5f)
    {
        IsActive                = true;
        TournamentId            = tournamentId ?? string.Empty;
        Snapshot                = snapshot;
        StaminaEnergyMax        = DefaultStaminaMax;
        StaminaEnergyRemaining  = DefaultStaminaMax;
        StaminaCostPerShot      = staminaCostPerShot;
        Debug.Log($"[TournamentRoundContext] BeginRound tournament={TournamentId} char={snapshot?.CharacterId} stamina={StaminaEnergyMax}");
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

    // ── Stamina ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Subtract the per-shot cost from the stamina pool (no hard gate v1).
    /// Called by ShotController.CommitFlick when IsActive == true.
    /// </summary>
    public static void DepleteStamina()
    {
        if (!IsActive) return;
        StaminaEnergyRemaining = Mathf.Max(0f, StaminaEnergyRemaining - StaminaCostPerShot);
        Debug.Log($"[TournamentRoundContext] DepleteStamina remaining={StaminaEnergyRemaining:F1}/{StaminaEnergyMax:F1}");
    }

    /// <summary>
    /// Overload for tests / explicit amount override.
    /// </summary>
    public static void DepleteStamina(float amount)
    {
        if (!IsActive) return;
        StaminaEnergyRemaining = Mathf.Max(0f, StaminaEnergyRemaining - amount);
    }
}
