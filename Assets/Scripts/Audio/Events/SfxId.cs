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
        HitBunker,  // Reserved: bunker-specific hit variant. Not emitted by current ShotController
                    // (all shots use power-band routing HitStrong/HitDefault/HitWeak/HitPutt).
                    // Present in sfx.csv + SfxLibrary.asset for fidelity tour + future use.

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

        // ── Gacha reveal (gacha_reveal_animation) ──────────────────────────────
        // Appended at the end: enum ORDER is what sfx.csv rows and SfxLibrary.asset
        // entries serialize against, so inserting anywhere else would silently
        // re-map every id after the insertion point.
        GachaBagDrop,          // A — bag drops in
        GachaBagShake,         // B — bag rocks before each card
        GachaCardPop,          // C — card launches out of the bag mouth
        GachaCardLand,         // D — card arrives (every card, every rarity)
        GachaRevealUncommon,   // D — rarity stingers, escalating
        GachaRevealRare,
        GachaRevealMythic,
        GachaRevealLegendary,
        GachaRevealSupreme,
        GachaCardExit,         // F — card leaves to make room for the next
        GachaSkip,             // SKIP button
        GachaRevealComplete,   // G — end of the whole pull
    }
}
