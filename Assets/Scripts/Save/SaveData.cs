#nullable enable
using System.Collections.Generic;

namespace Golfin.Save
{
    /// <summary>
    /// Single canonical save record for all game-state data.
    /// Serialized to JSON via Newtonsoft.Json (supports Dictionary natively).
    /// schemaVersion is bumped whenever the shape of this class changes.
    ///
    /// IMPORTANT: Do NOT serialize PlayerCharacterData, PlayerBallData, etc. directly.
    /// Use the flat DTO types (PersistedCharacter, etc.) to decouple storage from runtime.
    /// </summary>
    public class SaveData
    {
        public int schemaVersion = 1;

        public int rewardPoints;
        public string selectedCharacterId = "";

        public List<PersistedCharacter> ownedCharacters = new List<PersistedCharacter>();

        /// <summary>ballId → quantity (-1 = unlimited)</summary>
        public Dictionary<string, int> ballQuantities = new Dictionary<string, int>();

        /// <summary>itemId → quantity</summary>
        public Dictionary<string, int> itemQuantities = new Dictionary<string, int>();

        /// <summary>Serialized as List for JSON compatibility; hydrated to HashSet by SaveDataHost.</summary>
        public List<int> unlockedHoles = new List<int>();

        public List<int> playedHoles = new List<int>();
    }

    /// <summary>
    /// Flat DTO for persisted per-character data.
    /// Mirrors the persisted subset of PlayerCharacterData; adding UI-only fields to
    /// PlayerCharacterData will never cause migration pain here.
    /// </summary>
    public class PersistedCharacter
    {
        public string characterId = "";
        public int currentLevel;
        public int spentStrength;
        public int spentClubControl;
        public int spentRecovery;
        public int spentStamina;
        public int totalSPEarned;
        public bool isSelected;
    }
}
