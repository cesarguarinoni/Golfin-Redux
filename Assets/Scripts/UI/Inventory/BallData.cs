#nullable enable
using UnityEngine;

namespace Golfin.Inventory
{
    // ── Template data (loaded from Balls.csv) ──────────────────────────────────

    /// <summary>
    /// Read-only ball template loaded from Balls.csv.
    /// One instance per ball definition shared across all players.
    /// </summary>
    public class BallDataRuntime
    {
        public string ballId            = "";
        public string name              = "";
        public string brand             = "";

        // Stats — range: -10 to +10
        public int power          = 0;
        public int rebound        = 0;
        public int windResistance = 0;
        public int roll           = 0;
        public int spin           = 0;

        // Sprites (loaded from Resources/Balls/)
        public string  thumbnailSpriteName = "";
        public Sprite? thumbnailSprite     = null;
        public string  fullSpriteName      = "";
        public Sprite? fullSprite          = null;

        public string info = "";

        /// <summary>
        /// I6 — deactivated, never deleted. False means: gone from the shop, still fully renderable
        /// and playable in the bag of a player who already owns one.
        /// </summary>
        public bool isActive = true;

        public override string ToString() =>
            $"{name}: PWR={power} REB={rebound} WIND={windResistance} ROLL={roll} SPIN={spin}";
    }

    // ── Player instance data (owned ball state) ─────────────────────────────────

    /// <summary>
    /// Mutable per-player ball state — just a quantity count.
    /// Balls stack up to 99. No level, no durability, no equip state.
    /// </summary>
    public class PlayerBallData
    {
        public string ballId   = "";
        public int    quantity = 0;   // 0 = not owned, max stacking = 99 (∞ for default ball)

        /// <summary>True if this is the default unlimited ball (Golfin ball).</summary>
        public bool IsUnlimited => quantity < 0;  // -1 = unlimited (shown as ∞)
    }
}
