#nullable enable
using UnityEngine;
using Golfin.Roster;   // CharacterRarity

namespace Golfin.Inventory
{
    // ── Enums ──────────────────────────────────────────────────────────────────

    public enum ClubType
    {
        Driver,
        Wood,
        Iron,
        A_Wedge,   // Approach Wedge  (CSV: "A.Wedge")
        P_Wedge,   // Pitching Wedge  (CSV: "P.Wedge")
        S_Wedge,   // Sand Wedge      (CSV: "S.Wedge")
        Putter
    }

    // ── Template data (loaded from Clubs.csv) ──────────────────────────────────

    /// <summary>
    /// Read-only club template loaded from Clubs.csv.
    /// One instance per club definition shared across all players.
    /// </summary>
    public class ClubDataRuntime
    {
        public string   clubId              = "";
        public string   name                = "";
        public ClubType type                = ClubType.Driver;
        public CharacterRarity rarity       = CharacterRarity.Common;  // reuse existing rarity enum
        public string   brand               = "";

        // Base stats (level 1 values)
        public int basePower          = 0;
        public int baseAccuracy       = 0;
        public int baseLieResistance  = 0;
        public int baseLoft           = 0;
        public int maxDurability      = 100;
        public int baseDistance       = 0;   // shown as "X yd", not a bar

        // Physics flight parameters (from CSV: ballSpeedMps, launchAngleDeg, spinRateRpm)
        // Used by LiveStatProviderHost to build ClubStats for the physics resolver.
        public float ballSpeedMps     = 75f;   // base ball speed at launch
        public float launchAngleDeg   = 10.9f; // loft / launch angle in degrees
        public float spinRateRpm      = 2686f; // base backspin rate in RPM

        // Sprites (loaded from Resources/Clubs/)
        public string  portraitSpriteName = "";
        public Sprite? portraitSprite     = null;
        public string  portraitFullName   = "";
        public Sprite? portraitFull       = null;
        public string  controlSpriteName  = "";
        public Sprite? controlSprite      = null;

        public int    maxLevel = 119;
        public string info     = "";

        public string GetTypeLabel() => type switch
        {
            ClubType.Driver  => "DRIVER",
            ClubType.Wood    => "WOOD",
            ClubType.Iron    => "IRON",
            ClubType.A_Wedge => "A. WEDGE",
            ClubType.P_Wedge => "P. WEDGE",
            ClubType.S_Wedge => "S. WEDGE",
            ClubType.Putter  => "PUTTER",
            _                => "CLUB"
        };

        public override string ToString() =>
            $"{name} ({rarity} {GetTypeLabel()}): PWR={basePower} ACC={baseAccuracy}";
    }

    // ── Player instance data (owned club state) ─────────────────────────────────

    /// <summary>
    /// Mutable per-player club state — level, durability, equip slot.
    /// Mirrors the PlayerCharacterData pattern from the Roster system.
    /// </summary>
    public class PlayerClubData
    {
        public string clubId           = "";
        public int    currentLevel     = 1;
        public int    currentDurability;   // set to maxDurability on init
        public int    maxDurability    = 100;
        public int    equippedBagSlot  = 0;  // 0 = not equipped, 1-N = bag number

        // SP allocation (Phase E1)
        public int totalSPEarned      = 0;
        public int spentPower          = 0;
        public int spentAccuracy       = 0;
        public int spentLieResistance  = 0;
        public int spentDurability     = 0;
        public const int MAX_SP_PER_STAT = 20;

        public bool IsEquipped      => equippedBagSlot > 0;
        public bool IsDurabilityLow => maxDurability > 0
                                    && (float)currentDurability / maxDurability < 0.25f;

        // Computed stats — base + spent SP
        public int GetPower(ClubDataRuntime template)         => template.basePower + spentPower;
        public int GetAccuracy(ClubDataRuntime template)      => template.baseAccuracy + spentAccuracy;
        public int GetLieResistance(ClubDataRuntime template) => template.baseLieResistance + spentLieResistance;
        public int GetLoft(ClubDataRuntime template)          => template.baseLoft;          // fixed — no SP
        public int GetDistance(ClubDataRuntime template)      => template.baseDistance;
    }
}
