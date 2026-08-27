#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Golfin.Content;
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

            // ── DB-BEFORE-MANAGER, ASSERTED (content_overlay_catalogs) ───────
            // CharacterDatabaseCSV is -200 and this is -100, but that comes from a committed
            // `executionOrder:` in each .cs.meta and nothing re-asserts it. A database that has NOT
            // run yet hands back an EMPTY roster, and an empty roster is indistinguishable from
            // "the player owns nothing" at every consumer downstream. Error, not warning: it is a
            // wiring defect, not a designed path.
            if (csvDb != null && !csvDb.IsLoaded)
            {
                Debug.LogError(
                    "[CharacterManager] EXECUTION ORDER BROKEN: CharacterDatabaseCSV exists but has " +
                    "not loaded any rows yet, so this roster is about to be built from an EMPTY " +
                    "catalog. CharacterDatabaseCSV must stay ahead of CharacterManager " +
                    "(-200 vs -100, from their .cs.meta executionOrder fields).");
            }

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
                // ── SAVE-BEFORE-MANAGER, ASSERTED (content_kill_switch_and_order §2) ──
                // CharacterManager sat at -100 alongside SaveDataHost until 2026-08-26, and Unity
                // leaves the relative order of an execution-order TIE undefined. Losing it does not
                // crash — Instance is null, this whole block is skipped, and the clamp below never
                // runs — so a save with out-of-range values simply stays out of range until a launch
                // where the tie falls the other way. That is now -95 vs -100, and this asserts it
                // rather than trusting it: Instance is assigned BEFORE LoadData(), so only IsLoaded
                // proves the save on disk has actually been read.
                if (!SaveDataHost.Instance.IsLoaded)
                {
                    Debug.LogError(
                        "[CharacterManager] EXECUTION ORDER BROKEN: SaveDataHost exists but has not " +
                        "finished loading, so this roster is about to be overlaid from save data that " +
                        "is not the player's — and the clamp below would run against it. SaveDataHost " +
                        "must stay ahead of CharacterManager (-100 vs -95, from their .cs.meta " +
                        "executionOrder fields).");
                }

                var saveData = SaveDataHost.Instance.Data;
                var nowUtc   = DateTime.UtcNow;

                // ── THE CLAMP STEP (content_overlay_catalogs §2) ──────────────
                //
                // ONCE, HERE, and never at a read site. This is the first point in the boot where
                // BOTH halves are available — the overlaid character definitions (CharacterDatabaseCSV,
                // order -200) and the loaded save (SaveDataHost, -100; this manager is -95, so both
                // are guaranteed done) — and it runs BEFORE the hydration loop below, so nothing
                // downstream ever sees an out-of-bounds value.
                //
                // The case that matters: a rarity DOWNGRADE. Legendary → Rare drops the Strength cap
                // 40 → 30, orphaning any SP allocated above the new ceiling. It is clamped and
                // logged; NOTHING IS REFUNDED (SPEC §2, explicitly out of scope — refunding is its
                // own economy decision, and inventing one here would make it impossible to make
                // properly later).
                var clampEvents = ContentClamp.ClampCharacters(
                    saveData.ownedCharacters, BuildCharacterClampDefinitions());
                ContentClamp.LogAll(clampEvents, "characters");
                if (clampEvents.Count > 0) SaveDataHost.Instance.MarkDirty();

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
                        playerData.isOwned          = persisted.isOwned;

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

                // F8 invariant: the named starter character MUST always be owned.
                // A partial/interrupted write can produce a save where starterCharacterId is set
                // but the character is missing or has isOwned=false in ownedCharacters.
                // Self-repair on every hydration so any player in this state is un-stuck.
                if (!string.IsNullOrEmpty(saveData.starterCharacterId))
                {
                    if (ownedCharacters.TryGetValue(saveData.starterCharacterId, out var starterPD))
                    {
                        if (!starterPD.isOwned)
                        {
                            Debug.LogWarning($"[CharacterManager] INVARIANT REPAIR: " +
                                $"starterCharacterId='{saveData.starterCharacterId}' had isOwned=false. " +
                                "Forcing isOwned=true and flushing save.");
                            starterPD.isOwned = true;
                            var persistedRec = saveData.ownedCharacters.Find(c => c.characterId == saveData.starterCharacterId);
                            if (persistedRec != null)
                                persistedRec.isOwned = true;
                            else
                                saveData.ownedCharacters.Add(new PersistedCharacter
                                    { characterId = saveData.starterCharacterId, isOwned = true,
                                      currentLevel = starterPD.currentLevel });
                            if (string.IsNullOrEmpty(saveData.selectedCharacterId))
                                saveData.selectedCharacterId = saveData.starterCharacterId;
                            SaveDataHost.Instance.MarkDirty();
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[CharacterManager] INVARIANT: starterCharacterId='" +
                            saveData.starterCharacterId + "' not found in catalog — cannot auto-repair.");
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
            existing.isOwned         = playerData.isOwned;

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

        /// <summary>
        /// Returns all characters the player has been granted (isOwned=true).
        /// Pre-v10 saves: all characters are owned (migrated). New saves: only the chosen starter + future grants.
        /// </summary>
        public List<PlayerCharacterData> GetAllOwnedCharacters()
        {
            return ownedCharacters.Values.Where(c => c.isOwned).ToList();
        }

        /// <summary>
        /// Returns the full character catalog (owned + locked).
        /// Use this for the Roster carousel, which shows all cards with locked/unlocked state.
        /// </summary>
        public List<PlayerCharacterData> GetAllCatalogCharacters()
        {
            return ownedCharacters.Values.ToList();
        }

        /// <summary>
        /// Returns characters that can be chosen as the starting character (starterCandidate=true in CSV).
        /// </summary>
        public List<PlayerCharacterData> GetStarterCandidates()
        {
            var db = CharacterDatabaseCSV.Instance;
            return ownedCharacters.Values
                // I6: a DEACTIVATED character can still be owned and rendered, but it must never
                // be offered as a NEW starter — this is an "available" list.
                .Where(c => db?.GetCharacter(c.characterId) is { starterCandidate: true, isActive: true })
                .ToList();
        }

        /// <summary>True if the player has never chosen a starting character.</summary>
        public bool NeedsStarter
        {
            get
            {
                if (SaveDataHost.Instance == null) return false;
                return string.IsNullOrEmpty(SaveDataHost.Instance.Data.starterCharacterId);
            }
        }

        /// <summary>
        /// Grants the specified character as the player's starting character.
        /// Sets isOwned=true, records starterCharacterId, selects the character, and persists.
        /// May only be called once (NeedsStarter gates the call site).
        /// </summary>
        public void GrantStarter(string characterId)
        {
            if (!ownedCharacters.TryGetValue(characterId, out var playerData))
            {
                Debug.LogError($"[CharacterManager] GrantStarter: character '{characterId}' not in catalog.");
                return;
            }

            playerData.isOwned = true;
            SyncCharacterToSaveData(characterId);

            if (SaveDataHost.Instance != null)
            {
                SaveDataHost.Instance.Data.starterCharacterId = characterId;
                SaveDataHost.Instance.MarkDirty();
            }

            SelectCharacter(characterId);
            OnRosterChanged?.Invoke();
            Debug.Log($"[CharacterManager] GrantStarter: '{characterId}' granted and selected.");
        }

        /// <summary>
        /// Unlock a character the player has BOUGHT (shop_server_purchase §3.5).
        ///
        /// <para>
        /// <see cref="GrantStarter"/> minus the two things that are specific to being FIRST:
        /// it does not write <c>starterCharacterId</c> and it does not
        /// <see cref="SelectCharacter"/>. A player who buys their fourth character has not changed
        /// who they are playing as, and stomping the selection would be a bug they would have to undo
        /// by hand every purchase.
        /// </para>
        /// <para>
        /// Every catalog character is ALREADY a row in <c>ownedCharacters</c> with
        /// <c>isOwned = false</c> — that is what <see cref="GrantStarter"/> relies on too — so the
        /// unlock is a flag flip plus a save sync, not an insert. The Roster screen re-renders on
        /// <see cref="OnRosterChanged"/>, so a bought character appears unlocked with no Roster change.
        /// </para>
        /// </summary>
        /// <returns>False when the id is unknown or the character is already owned — neither is an
        /// error here: the shop pre-check and the SERVER both refuse an owned character before the
        /// debit, so reaching this with one means a grant arrived for something already applied, and
        /// a no-op is exactly right.</returns>
        public bool UnlockCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                Debug.LogWarning("[CharacterManager] UnlockCharacter: empty characterId.");
                return false;
            }

            if (!ownedCharacters.TryGetValue(characterId, out var playerData))
            {
                Debug.LogWarning($"[CharacterManager] UnlockCharacter: character '{characterId}' not in catalog.");
                return false;
            }

            if (playerData.isOwned)
            {
                Debug.Log($"[CharacterManager] UnlockCharacter: '{characterId}' already owned — no-op.");
                return false;
            }

            playerData.isOwned = true;
            SyncCharacterToSaveData(characterId);
            SaveDataHost.Instance?.MarkDirty();

            OnRosterChanged?.Invoke();
            Debug.Log($"[CharacterManager] UnlockCharacter: '{characterId}' unlocked.");
            return true;
        }

        /// <summary>Returns true if the player owns the specified character.</summary>
        public bool IsOwned(string characterId)
        {
            return ownedCharacters.TryGetValue(characterId, out var c) && c.isOwned;
        }

        /// <summary>Returns true if the character is flagged as starterCandidate in the CSV.</summary>
        public bool IsStarterCandidate(string characterId)
        {
            return CharacterDatabaseCSV.Instance?.GetCharacter(characterId)?.starterCandidate == true;
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

        /// <summary>
        /// The bounds every owned character must fit inside, from the (possibly overlaid) catalog.
        ///
        /// <para>
        /// The SP ceilings are SPENT ceilings, not stat caps: <see cref="RarityStatCaps"/> caps
        /// <c>base + spent</c>, so what the clamp needs is <c>max(0, cap − base)</c> for the row's
        /// CURRENT rarity — which is exactly the number a rarity downgrade moves. Computed here
        /// rather than inside ContentClamp because RarityStatCaps lives in Assembly-CSharp and
        /// Golfin.Content cannot reference it.
        /// </para>
        /// <para>
        /// <c>startLevel</c> comes from the CSV column when the row carries one and falls back to
        /// the rarity table otherwise, so a published <c>startLevel</c> is honoured without
        /// changing how a NEW character is seeded.
        /// </para>
        /// <para>
        /// PUBLIC since content_player_inventory Phase 4: <c>InventoryCatalogAdapter</c> reads
        /// <c>StartLevel</c> from here to build the "freshly-granted character" the inventory blob
        /// deltas against, rather than re-deriving the rarity → starting-level table a second time.
        /// Pure read; safe to call at any point after the roster is built.
        /// </para>
        /// </summary>
        public Dictionary<string, CharacterClampDefinition> BuildCharacterClampDefinitions()
        {
            var map = new Dictionary<string, CharacterClampDefinition>(StringComparer.Ordinal);

            var db = CharacterDatabaseCSV.Instance;
            if (db == null) return map;

            foreach (var t in db.GetAllCharacters())
            {
                if (string.IsNullOrEmpty(t.characterId)) continue;

                var caps = RarityStatCaps.GetStatCaps(t.rarity);
                map[t.characterId] = new CharacterClampDefinition(
                    t.characterId,
                    t.startLevel > 0 ? t.startLevel : GetStartingLevel(t.rarity),
                    t.maxLevel > 0 ? t.maxLevel : GetMaxLevelForRarity(t.rarity),
                    Mathf.Max(0, caps.strengthCap    - t.baseStrength),
                    Mathf.Max(0, caps.clubControlCap - t.baseClubControl),
                    Mathf.Max(0, caps.recoveryCap    - t.baseRecovery),
                    Mathf.Max(0, caps.staminaCap     - t.baseStamina));
            }

            return map;
        }

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

        /// <summary>
        /// The character's level ceiling: the CATALOG's <c>maxLevel</c> when the row carries one,
        /// and the rarity table only as a fallback.
        ///
        /// <para>
        /// ⚠️ This used to read <see cref="GetMaxLevelForRarity"/> unconditionally and ignore the
        /// CSV's <c>maxLevel</c> column entirely. That was invisible while the bundled CSV agreed
        /// with the table (Common 39, Uncommon 79, …) — content_overlay_catalogs is what makes them
        /// able to DISAGREE, and the bug it produced is worth naming: a published maxLevel of 20
        /// clamped the SAVE to 20 on load (ContentClamp) while the roster went on showing "/39" and
        /// LevelUp went on selling levels 21–39. The player would climb back above the published
        /// ceiling and be silently clamped down again on the next launch, forever, having spent the
        /// RP each time.
        /// </para>
        /// <para>
        /// The clamp and the UI must read the SAME ceiling. This is that ceiling.
        /// </para>
        /// </summary>
        public int GetMaxLevel(string characterId)
        {
            var csv = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
            if (csv != null)
                return csv.maxLevel > 0 ? csv.maxLevel : GetMaxLevelForRarity(csv.rarity);

            // The ScriptableObject fallback carries no maxLevel column, so the rarity table is all
            // there is — and it is never the overlaid path anyway.
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
