#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Roster
{
    /// <summary>
    /// Central manager for all character operations
    /// Handles level-up, SP allocation, stat updates, roster management
    /// Works with CharacterLevelUpDatabase for economy data
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

            // Assuming ManualSPAllocation inherits from StatAllocationStrategy
            allocationStrategy = new ManualSPAllocation(this);
            LoadRoster();
        }

        private void LoadRoster()
        {
            ownedCharacters.Clear();
            // Logic to load characters or initialize
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

        // Singleton cleanup to prevent Domain Reload bugs
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null!;
            }
        }

        // Additional methods as needed...
    }
}