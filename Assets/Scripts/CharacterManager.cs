#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Golfin.Core.Stamina;
using Golfin.Save;
using Golfin.Audio.Events;

namespace Golfin.Roster
{
    /// <summary>
    /// Central manager for all character operations.
    /// Handles level-up, SP allocation, stat updates, roster management.
    ///
    /// Read-through facade over SaveData.ownedCharacters.
    /// On Awake: builds ownedCharacters dict from CSV templates, then overlays
    /// player-specific data (level, SP) from SaveData.ownedCharacters by characterId.
    /// After LevelUp: syncs changes back to SaveData entry and calls MarkDirty.
    /// </summary>
    public class CharacterManager : MonoBehaviour
    {
        // Null-forgiving operator for Unity's Awake initialization
        public static CharacterManager Instance { get; private set; } = null!;

        // Null-forgiving operators to silence Inspector initialization warnings
        [SerializeField] private CharacterDatabase characterDatabase = null!;
        [SerializeField] private CharacterLevelUpDatabase levelUpDatabase = null!;

        private Dictionary<string, PlayerCharacterData> ownedCharacters = new Dictionary<string, PlayerCharacterData>();
        private string selectedCharacterId = "";

        // Initialized in Awake
        private StatAllocationStrategy allocationStrategy = null!;

        // Nullable events
        public event System.Action<string>? OnCharacterLeveledUp;
        public event System.Action<string>? OnCharacterSelected;
        public event System.Action? OnRosterChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            allocationStrategy = new ManualSPAllocation(this);
            LoadRoster();
        }

        private void LoadRoster()
        {
            ownedCharacters.Clear();

            // Step 1: Build dict from CSV templates (or ScriptableObject fallback)
            var csvDb = CharacterDatabaseCSV.Instance;
            if (csvDb != null)
            {
                var allChars = csvDb.GetAllCharacters();
                foreach (var charTemplate in allChars)
                {
                    var playerData = new PlayerCharacterData(charTemplate.characterId);
                    playerData.currentLevel      = GetStartingLevel(charTemplate.rarity);
                    playerData.currentStrength   = charTemplate.baseStrength;
                    playerData.currentClubControl = charTemplate.baseClubControl;
                    playerData.currentRecovery   = charTemplate.baseRecovery;
                    playerData.currentStamina    = charTemplate.baseStamina;
                    ownedCharacters[charTemplate.characterId] = playerData;
                }

                Debug.Log($"[CharacterManager] Loaded {ownedCharacters.Count} characters from CSV");
            }
            else if (characterDatabase != null)
            {
                // Fallback: load from ScriptableObject database
                var allChars = characterDatabase.GetAllCharacters();
                foreach (var charTemplate in allChars)
                {
                    var playerData = new PlayerCharacterData(charTemplate.characterId);
                    playerData.currentLevel      = GetStartingLevel(charTemplate.rarity);
                    playerData.currentStrength   = charTemplate.baseStrength;
                    playerData.currentClubControl = charTemplate.baseClubControl;
                    playerData.currentRecovery   = charTemplate.baseRecovery;
                    playerData.currentStamina    = charTemplate.baseStamina;
                    ownedCharacters[charTemplate.characterId] = playerData;
                }

                Debug.Log($"[CharacterManager] Loaded {ownedCharacters.Count} characters from ScriptableObject DB");
            }
            else
            {
                Debug.LogWarning("[CharacterManager] No character data source available!");
            }

            // Step 2: Overlay player-specific data from SaveData
            if (SaveDataHost.Instance != null)
            {
                var saveData = SaveDataHost.Instance.Data;
                var nowUtc   = DateTime.UtcNow;

                foreach (var persisted in saveData.ownedCharacters)
                {
                    if (ownedCharacters.TryGetValue(persisted.characterId, out var playerData))
                    {
                        playerData.currentLevel     = persisted.currentLevel;
                        playerData.spentStrength    = persisted.spentStrength;
                        playerData.spentClubControl = persisted.spentClubControl;
                        playerData.spentRecovery    = persisted.spentRecovery;
                        playerData.spentStamina     = persisted.spentStamina;
                        playerData.totalSPEarned    = persisted.totalSPEarned;
                        playerData.isSelected       = persisted.isSelected;

                        // ── Stamina condition hydration (Phase 2, §4.3) ───────────
                        // Recompute stat values first so currentStamina is current.
                        // (RefreshStatValues syncs to SaveData — guard against double-sync here
                        //  by computing inline instead of calling the full method.)
                        {
                            var csv = CharacterDatabaseCSV.Instance?.GetCharacter(persisted.characterId);
                            if (csv != null)
                            {
                                var caps = RarityStatCaps.GetStatCaps(csv.rarity);
                                playerData.currentStrength    = Mathf.Min(csv.baseStrength    + playerData.spentStrength,    caps.strengthCap);
                                playerData.currentClubControl = Mathf.Min(csv.baseClubControl + playerData.spentClubControl, caps.clubControlCap);
                                playerData.currentRecovery    = Mathf.Min(csv.baseRecovery    + playerData.spentRecovery,    caps.recoveryCap);
                                playerData.currentStamina     = Mathf.Min(csv.baseStamina     + playerData.spentStamina,     caps.staminaCap);
                            }
                        }

                        // Set real tank size from Stamina stat (§4.2)
                        if (StaminaModel.IsConfigured)
                            playerData.maxStaminaEnergy = StaminaModel.MaxCondition(playerData.currentStamina);

                        // Hydrate energy + timestamp (§4.3 hydrate block)
                        if (string.IsNullOrEmpty(persisted.conditionUpdatedUtc))
                        {
                            // Pre-v4 or fresh character: start at full condition
                            playerData.currentStaminaEnergy = playerData.maxStaminaEnergy;
                            playerData.conditionUpdatedUtc  = nowUtc;
                        }
                        else
                        {
                            // Parse persisted timestamp; fall back to fresh on error
                            try
                            {
                                playerData.currentStaminaEnergy = Mathf.Clamp(
                                    persisted.conditionEnergy, 0f, playerData.maxStaminaEnergy);
                                playerData.conditionUpdatedUtc = DateTime.Parse(
                                    persisted.conditionUpdatedUtc,
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                                // Accrue offline regen since last save (D2)
                                if (StaminaModel.IsConfigured)
                                    StaminaRuntimeService.AccrueRegen(playerData, nowUtc);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[CharacterManager] Failed to parse conditionUpdatedUtc for {persisted.characterId}: {ex.Message} — treating as fresh.");
                                playerData.currentStaminaEnergy = playerData.maxStaminaEnergy;
                                playerData.conditionUpdatedUtc  = nowUtc;
                            }
                        }
                    }
                }

                // Restore selected character from SaveData
                if (!string.IsNullOrEmpty(saveData.selectedCharacterId) &&
                    ownedCharacters.ContainsKey(saveData.selectedCharacterId))
                {
                    selectedCharacterId = saveData.selectedCharacterId;
                }
                else if (ownedCharacters.Count > 0)
                {
                    selectedCharacterId = ownedCharacters.Keys.First();
                    ownedCharacters[selectedCharacterId].isSelected = true;
                }

                Debug.Log($"[CharacterManager] Overlaid SaveData — selectedChar={selectedCharacterId}");
            }
            else
            {
                // No SaveDataHost available (e.g. EditMode tests): use first character as default
                if (ownedCharacters.Count > 0)
                {
                    var firstId = ownedCharacters.Keys.First();
                    selectedCharacterId = firstId;
                    ownedCharacters[firstId].isSelected = true;
                }

                Debug.LogWarning("[CharacterManager] SaveDataHost.Instance is null — character progress NOT loaded from save.");
            }

            // demo_build_slice §3.4 soft-gating: force the demo character selected. The Roster
            // screen is locked in the demo, so this is what Home + gameplay use. No-op in the full game.
            if (GolfinRedux.Demo.DemoGate.IsDemo)
            {
                var demoChar = GolfinRedux.Demo.DemoConfig.Instance.CharacterId;
                if (!string.IsNullOrEmpty(demoChar) && ownedCharacters.ContainsKey(demoChar))
                {
                    foreach (var kv in ownedCharacters) kv.Value.isSelected = false;
                    selectedCharacterId = demoChar;
                    ownedCharacters[demoChar].isSelected = true;
                    Debug.Log($"[CharacterManager] Demo: forced selection to '{demoChar}'.");
                }
                else
                {
                    Debug.LogWarning($"[CharacterManager] Demo character '{demoChar}' not found — keeping default selection.");
                }
            }

            OnRosterChanged?.Invoke();
        }

        /// <summary>
        /// Sync a character's runtime state back to SaveData.ownedCharacters.
        /// Call after any mutation to currentLevel, spentStrength, etc.
        /// </summary>
        private void SyncCharacterToSaveData(string characterId)
        {
            if (SaveDataHost.Instance == null) return;

            if (!ownedCharacters.TryGetValue(characterId, out var playerData)) return;

            var saveData = SaveDataHost.Instance.Data;
            var existing = saveData.ownedCharacters.Find(c => c.characterId == characterId);
            if (existing == null)
            {
                existing = new PersistedCharacter { characterId = characterId };
                saveData.ownedCharacters.Add(existing);
            }

            existing.currentLevel     = playerData.currentLevel;
            existing.spentStrength    = playerData.spentStrength;
            existing.spentClubControl = playerData.spentClubControl;
            existing.spentRecovery    = playerData.spentRecovery;
            existing.spentStamina     = playerData.spentStamina;
            existing.totalSPEarned    = playerData.totalSPEarned;
            existing.isSelected       = playerData.isSelected;

            // Persist stamina condition (Phase 2 §4.3 dehydrate)
            existing.conditionEnergy     = playerData.currentStaminaEnergy;
            existing.conditionUpdatedUtc = playerData.conditionUpdatedUtc == default
                ? DateTime.UtcNow.ToString("o")
                : playerData.conditionUpdatedUtc.ToString("o");

            SaveDataHost.Instance.MarkDirty();
        }

        // Return type updated to exactly match the dictionary value type
        public PlayerCharacterData? GetCharacterData(string characterId)
        {
            if (ownedCharacters.TryGetValue(characterId, out var characterData))
            {
                return characterData;
            }
            return null;
        }

        public List<PlayerCharacterData> GetAllOwnedCharacters()
        {
            return ownedCharacters.Values.ToList();
        }

        /// <summary>
        /// Select a character by ID and fire OnCharacterSelected event.
        /// </summary>
        public void SelectCharacter(string characterId)
        {
            if (!ownedCharacters.ContainsKey(characterId))
            {
                Debug.LogWarning($"[CharacterManager] Cannot select unknown character: {characterId}");
                return;
            }

            // Deselect previous
            if (!string.IsNullOrEmpty(selectedCharacterId) && ownedCharacters.TryGetValue(selectedCharacterId, out var prev))
            {
                prev.isSelected = false;
                SyncCharacterToSaveData(selectedCharacterId);
            }

            selectedCharacterId = characterId;
            ownedCharacters[characterId].isSelected = true;
            SyncCharacterToSaveData(characterId);

            // Update SaveData selectedCharacterId
            if (SaveDataHost.Instance != null)
            {
                SaveDataHost.Instance.Data.selectedCharacterId = characterId;
                SaveDataHost.Instance.MarkDirty();
            }

            OnCharacterSelected?.Invoke(characterId);

            Debug.Log($"[CharacterManager] Selected character: {characterId}");
        }

        /// <summary>
        /// Get the base CharacterData template from the database.
        /// </summary>
        public CharacterData? GetCharacterTemplate(string characterId)
        {
            if (characterDatabase == null)
            {
                Debug.LogError("[CharacterManager] characterDatabase not assigned!");
                return null;
            }
            return characterDatabase.GetCharacter(characterId);
        }

        /// <summary>Alias for GetCharacterData (thumbnail card calls it by this name).</summary>
        public PlayerCharacterData? GetPlayerCharacter(string characterId)
            => GetCharacterData(characterId);

        /// <summary>Alias for GetCharacterTemplate (thumbnail card calls it by this name).</summary>
        public CharacterData? GetCharacter(string characterId)
            => GetCharacterTemplate(characterId);

        private int GetStartingLevel(CharacterRarity rarity) => rarity switch
        {
            CharacterRarity.Common    => 10,
            CharacterRarity.Uncommon  => 40,
            CharacterRarity.Rare      => 80,
            CharacterRarity.Mythic    => 120,
            CharacterRarity.Legendary => 160,
            CharacterRarity.Supreme   => 200,
            _                         => 10
        };

        private int GetMaxLevelForRarity(CharacterRarity rarity) => rarity switch
        {
            CharacterRarity.Common    => 39,
            CharacterRarity.Uncommon  => 79,
            CharacterRarity.Rare      => 119,
            CharacterRarity.Mythic    => 159,
            CharacterRarity.Legendary => 199,
            CharacterRarity.Supreme   => 239,
            _                         => 39
        };

        /// <summary>Get max level based on rarity.</summary>
        public int GetMaxLevel(string characterId)
        {
            var csv = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            if (csv != null) return GetMaxLevelForRarity(csv.rarity);

            var so = GetCharacterTemplate(characterId);
            if (so != null) return GetMaxLevelForRarity(so.rarity);

            return 39;
        }

        /// <summary>Get the cost to level up a character to the next level.</summary>
        public int GetLevelUpCost(string characterId)
        {
            var playerChar = GetCharacterData(characterId);
            if (playerChar == null) return 0;

            int nextLevel = playerChar.currentLevel + 1;
            return levelUpDatabase.GetLevelUpCost(nextLevel);
        }

        /// <summary>
        /// Level up a character: deduct RP, increment level, earn SP.
        /// Returns SP earned (0 if failed).
        /// Syncs changes to SaveData and calls MarkDirty.
        /// </summary>
        public int LevelUp(string characterId)
        {
            var playerChar = GetCharacterData(characterId);
            if (playerChar == null)
            {
                Debug.LogError($"[CharacterManager] LevelUp failed: character {characterId} not found");
                return 0;
            }

            int nextLevel = playerChar.currentLevel + 1;
            int maxLevel = GetMaxLevel(characterId);
            if (nextLevel > maxLevel)
            {
                Debug.LogWarning($"[CharacterManager] {characterId} already at max level {maxLevel}");
                return 0;
            }

            int cost = levelUpDatabase.GetLevelUpCost(nextLevel);
            if (!RewardPointsManager.Instance.CanAfford(cost))
            {
                Debug.LogWarning($"[CharacterManager] Cannot afford level-up: need {cost}R");
                return 0;
            }

            RewardPointsManager.Instance.SpendPoints(cost);

            playerChar.currentLevel = nextLevel;
            int spReward = levelUpDatabase.GetSPReward(nextLevel);
            playerChar.totalSPEarned += spReward;

            // Sync to SaveData
            SyncCharacterToSaveData(characterId);

            OnCharacterLeveledUp?.Invoke(characterId);
            SfxBus.Play(SfxId.LevelUp);

            Debug.Log($"[CharacterManager] {characterId} leveled up to {nextLevel}, earned {spReward} SP");
            return spReward;
        }

        /// <summary>
        /// Recalculate current stat values from base stats + SP allocation.
        /// </summary>
        public void RefreshStatValues(string characterId)
        {
            var playerChar = GetCharacterData(characterId);
            if (playerChar == null) return;

            // Resolve base stats and rarity — CSV first, ScriptableObject fallback
            int bStr, bCtrl, bRec, bStam;
            CharacterRarity rarity;

            var csv = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            if (csv != null)
            {
                bStr = csv.baseStrength; bCtrl = csv.baseClubControl;
                bRec = csv.baseRecovery; bStam = csv.baseStamina;
                rarity = csv.rarity;
            }
            else
            {
                var so = GetCharacterTemplate(characterId);
                if (so == null) return;
                bStr = so.baseStrength; bCtrl = so.baseClubControl;
                bRec = so.baseRecovery; bStam = so.baseStamina;
                rarity = so.rarity;
            }

            var caps = RarityStatCaps.GetStatCaps(rarity);
            playerChar.currentStrength    = Mathf.Min(bStr  + playerChar.spentStrength,    caps.strengthCap);
            playerChar.currentClubControl = Mathf.Min(bCtrl + playerChar.spentClubControl, caps.clubControlCap);
            playerChar.currentRecovery    = Mathf.Min(bRec  + playerChar.spentRecovery,    caps.recoveryCap);
            playerChar.currentStamina     = Mathf.Min(bStam + playerChar.spentStamina,     caps.staminaCap);

            // Recompute tank size from updated Stamina stat (§4.2)
            // On stat raise, leave currentStaminaEnergy unchanged (can't exceed new max anyway).
            if (StaminaModel.IsConfigured)
                playerChar.maxStaminaEnergy = StaminaModel.MaxCondition(playerChar.currentStamina);

            // Sync stat changes to SaveData
            SyncCharacterToSaveData(characterId);
        }

        /// <summary>
        /// Accrue offline regen to now, then flush the current condition energy + timestamp
        /// back to SaveData for the given character. Call after a per-hole drain.
        /// </summary>
        public void PersistCondition(string characterId)
        {
            if (!ownedCharacters.TryGetValue(characterId, out var playerData)) return;
            if (StaminaModel.IsConfigured)
                StaminaRuntimeService.AccrueRegen(playerData, DateTime.UtcNow);
            SyncCharacterToSaveData(characterId);
        }

        /// <summary>Get the currently selected character ID.</summary>
        public string GetSelectedCharacterId() => selectedCharacterId;

        // Singleton cleanup to prevent Domain Reload bugs
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null!;
            }
        }
    }
}
