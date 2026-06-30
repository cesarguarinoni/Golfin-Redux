#nullable enable
using System;
using UnityEngine;
using Golfin.Core.Stamina;
using Golfin.Gameplay.Session;
using Golfin.Roster;

/// <summary>
/// Assembly-CSharp static service that boots the Stamina Economy on load and
/// subscribes to per-hole events (Phase 2).
///
/// D4 (LOCKED): Uses [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] — no scene edit needed.
/// D5 (LOCKED): Drains for both solo AND versus.
///   - Solo path:   subscribes to GameSession.OnHoleComplete
///                  (HoleCompletionBridge fires it when IsVersus=false).
///   - Versus path: subscribes to GameSession.OnMatchComplete
///                  (VersusMatchController.MarkMatchComplete fires it at match end;
///                   HoleCompletionBridge.HandleShot short-circuits on IsVersus=true
///                   so OnHoleComplete never fires on the versus path).
///   Both paths share DrainForCompletedHole() — one code path, no duplication.
///
/// Standing ban: this file is in Assembly-CSharp (NOT Assets/Scripts/Physics/),
/// which is the intended layer. ZERO edits to Physics/ .cs files.
/// </summary>
public static class StaminaRuntimeService
{
    static bool _wired;

    // ─── Boot ──────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        StaminaConfigLoader.Load();

        if (!StaminaModel.IsConfigured)
        {
            Debug.LogWarning("[StaminaRuntimeService] StaminaModel not configured after Load() — condition drain disabled this session.");
            return;
        }

        WireHoleComplete();
    }

    /// <summary>
    /// Idempotent subscription to GameSession.OnHoleComplete (solo) and
    /// GameSession.OnMatchComplete (versus).
    /// Called from Boot(); exposed for test injection only.
    /// </summary>
    internal static void WireHoleComplete()
    {
        if (_wired) return;
        _wired = true;
        GameSession.OnHoleComplete   += OnHoleComplete;
        GameSession.OnMatchComplete  += OnMatchComplete;
    }

    // ─── Solo hole-complete handler ────────────────────────────────────────────
    // HoleCompletionBridge fires OnHoleComplete only when IsVersus=false.

    static void OnHoleComplete(HoleCompletionData _)
    {
        if (!StaminaModel.IsConfigured) return;

        string? charId = GameSession.SelectedCharacterId;
        DrainForCompletedHole(charId);
    }

    // ─── Versus match-complete handler ─────────────────────────────────────────
    // VersusMatchController fires OnMatchComplete at match end.
    // A versus match = exactly one hole for the human player → drain once.

    static void OnMatchComplete(GameSession.MatchOutcome outcome, int p1Strokes, int p2Strokes)
    {
        if (!StaminaModel.IsConfigured) return;

        string? charId = GameSession.SelectedCharacterId;
        DrainForCompletedHole(charId);
    }

    // ─── Shared drain body (solo + versus) ────────────────────────────────────

    /// <summary>
    /// Drains the live stamina pool for a single completed hole (solo or versus).
    /// Sequence: AccrueRegen → subtract DrainForHole → clamp ≥0 → stamp timestamp → PersistCondition.
    /// No-op if charId is null/empty or the character is not found.
    /// </summary>
    internal static void DrainForCompletedHole(string? charId)
    {
        if (!StaminaModel.IsConfigured) return;
        if (string.IsNullOrEmpty(charId)) return;

        var pcd = CharacterManager.Instance?.GetCharacterData(charId);
        if (pcd == null) return;

        var nowUtc = DateTime.UtcNow;

        // Accrue any offline / between-hole regen first (D2: bring current before draining).
        AccrueRegen(pcd, nowUtc);

        // Drain for the completed hole.
        pcd.currentStaminaEnergy = Mathf.Max(0f, pcd.currentStaminaEnergy - StaminaModel.DrainForHole());
        pcd.conditionUpdatedUtc  = nowUtc;

        // Flush to SaveData.
        CharacterManager.Instance?.PersistCondition(charId);

        Debug.Log($"[StaminaRuntimeService] Hole/match complete: char={charId} " +
                  $"energy={pcd.currentStaminaEnergy:F1}/{pcd.maxStaminaEnergy:F1} " +
                  $"drain={StaminaModel.DrainForHole():F1}");
    }

    // ─── AccrueRegen (public so CharacterManager.LoadRoster can call it on load) ──

    /// <summary>
    /// Accrue passive regen from <paramref name="pcd"/>'s last authoritative timestamp
    /// to <paramref name="nowUtc"/> and update both fields in-place.
    ///
    /// Rules (D2):
    ///   • conditionUpdatedUtc == default  →  stamp nowUtc, return (no regen delta to calculate).
    ///   • elapsed &lt;= 0                   →  no-op (clocks went backward or same-second call).
    ///   • else                             →  add RegenForElapsed clamped to maxStaminaEnergy.
    /// </summary>
    public static void AccrueRegen(PlayerCharacterData pcd, DateTime nowUtc)
    {
        if (!StaminaModel.IsConfigured) return;

        if (pcd.conditionUpdatedUtc == default)
        {
            pcd.conditionUpdatedUtc = nowUtc;
            return;
        }

        var elapsed = nowUtc - pcd.conditionUpdatedUtc;
        if (elapsed.TotalHours <= 0) return;

        float regen = StaminaModel.RegenForElapsed(pcd.currentRecovery, elapsed);
        pcd.currentStaminaEnergy = Mathf.Min(pcd.maxStaminaEnergy, pcd.currentStaminaEnergy + regen);
        pcd.conditionUpdatedUtc  = nowUtc;
    }

    // ─── Test helpers (UNITY_EDITOR / test assemblies only) ───────────────────

#if UNITY_EDITOR
    /// <summary>Reset wiring state for test isolation (EditMode tests only).</summary>
    internal static void ResetForTests()
    {
        if (_wired)
        {
            GameSession.OnHoleComplete  -= OnHoleComplete;
            GameSession.OnMatchComplete -= OnMatchComplete;
            _wired = false;
        }
        StaminaModel.ResetForTests();
    }
#endif
}
