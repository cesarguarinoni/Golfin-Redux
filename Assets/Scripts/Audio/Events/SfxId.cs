namespace Golfin.Audio.Events
{
    /// <summary>
    /// Semantic identifiers for every sound effect in the game.
    /// Lives in a no-engine-references leaf asmdef so any assembly can reference it
    /// without creating a dependency on AudioManager (Assembly-CSharp).
    /// </summary>
    public enum SfxId
    {
        // ── Swing (club type) ──────────────────────────────────────────────────
        SwingDriver,
        SwingWood,
        SwingIron,
        SwingWedge,    // A_Wedge, P_Wedge, S_Wedge all map here
        SwingDefault,  // Putter + fallback
        SwingPutt,     // Putter-specific swing (same as SwingDefault — kept separate for tuning)

        // ── Hit (impact, power band) ───────────────────────────────────────────
        HitStrong,
        HitDefault,
        HitDefault02,
        HitWeak,
        HitPutt,
        HitBunker,

        // ── Ball in cup ────────────────────────────────────────────────────────
        HitBallIn,

        // ── Landing (surface type) ─────────────────────────────────────────────
        LandFairway,
        LandGreen,
        LandRough,
        LandSand,
        LandWater,
        LandRoad,
        LandBushes,

        // ── UI ─────────────────────────────────────────────────────────────────
        UiTap,
        UiConfirm,
        UiCancel,
        UiBack,

        // ── Economy / progression ──────────────────────────────────────────────
        RpEarn,
        LevelUp,

        // ── Match stingers ─────────────────────────────────────────────────────
        MatchWin,
        MatchLose,
        MatchDraw,
    }
}
