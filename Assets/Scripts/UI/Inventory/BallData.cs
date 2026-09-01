#nullable enable
using Golfin.Roster;
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

        /// <summary>
        /// ball_data_wiring §4.1 — the same <see cref="CharacterRarity"/> the club and character
        /// rows carry, parsed with the same <c>ClubCsvParser.ParseRarity</c>. Template data: read
        /// from the row at load, never persisted per instance. Nothing draws it yet — rarity
        /// framing on the Balls screen is a later task — but the gacha/shop listings and the
        /// admin already speak in these six tiers, so the ball now does too.
        /// </summary>
        public CharacterRarity rarity   = CharacterRarity.Common;

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

        /// <summary>
        /// Remote art URL for the thumbnail (SPEC §3 — <c>thumbnailUrl</c> column).
        /// Empty means "no remote art". Resolution ladder step 1 (SPEC §2).
        /// </summary>
        public string thumbnailUrl = "";

        /// <summary>
        /// Remote art URL for the full image (SPEC §3 — <c>fullUrl</c> column).
        /// Empty means "no remote art". Resolution ladder step 1 (SPEC §2).
        /// </summary>
        public string fullUrl = "";

        public string info = "";

        /// <summary>
        /// I6 — deactivated, never deleted. False means: gone from the shop, still fully renderable
        /// and playable in the bag of a player who already owns one.
        /// </summary>
        public bool isActive = true;

        /// <summary>
        /// content_two_way §4 — <b>can THIS build draw this row?</b> False when the PRIMARY sprite
        /// (<see cref="thumbnailSprite"/>) did not resolve, which is what a row published in the
        /// admin looks like until the build that bundles its art ships. Set once, at load, from the
        /// resolution the loader already performs — never a second <c>Resources.Load</c>.
        /// <para>
        /// It gates the AVAILABLE view only. The <c>GetAll…</c> view still carries the row: a
        /// player granted a ball whose art is late must not LOSE it — the save and
        /// <c>InventoryCodec</c> round-trip it untouched; they just cannot see it yet.
        /// </para>
        /// </summary>
        public bool renderable = true;

        /// <summary>
        /// gacha_ops_polish §4e — <b>every player already owns this one</b>.
        ///
        /// <para>
        /// <c>RewardGranter</c> grants <c>ball_golfin</c> for every reward that says "a ball", and
        /// a fresh save starts with it. So it is the one ball that can never be a PRIZE and can
        /// never be a SHOP LISTING: a gacha slot that pays it pays nothing, and a shop row that
        /// sells it sells something the player is holding. <c>psc1_ball_golfin</c> sat in the
        /// standard pool at 60 weight until the operator noticed and deactivated it by hand — the
        /// column exists so the next one is refused rather than noticed.
        /// </para>
        /// <para>
        /// It is NOT <c>isActive</c>: the row is entirely live, playable and equippable. It is a
        /// statement about what it may be USED FOR, which is why the three guards
        /// (<c>GachaBannerCatalog.IsRollable</c>, the admin validator, and the server) read it and
        /// nothing else does.
        /// </para>
        /// </summary>
        public bool isDefault;

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
