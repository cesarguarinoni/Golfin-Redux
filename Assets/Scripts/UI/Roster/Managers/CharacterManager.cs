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
        public static CharacterManager Instance { get; private set; }
        
        [SerializeField] private CharacterDatabase characterDatabase;
        [SerializeField] private CharacterLevelUpDatabase levelUpDatabase;
        
        private Dictionary<string, PlayerCharacterData> ownedCharacters = new Dictionary<string, PlayerCharacterData>();
        
        private string selectedCharacterId = "";
        private StatAllocationStrategy allocationStrategy;
        
        // Events
        public event System.Action<string> OnCharacterLeveledUp;
        public event System.Action<string> OnCharacterSelected;
        public event System.Action OnRosterChanged;
        
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
            // Logic for loading or initializing characters here
        }
        
        public CharacterData? GetCharacterData(string characterId)
        {
            // Logic to retrieve character data
        }
        
        public List<PlayerCharacterData> GetAllOwnedCharacters()
        {
            return ownedCharacters.Values.ToList();
        }

        // ... continue implementation
    }
}