#nullable enable
using UnityEngine;
using System.Collections.Generic;

namespace Golfin.Roster
{
    public class CharacterManager : MonoBehaviour
    {
        private Dictionary<string, CharacterData> characterDictionary;

        private void Awake()
        {
            characterDictionary = new Dictionary<string, CharacterData>();
            // Initialize your character data here
        }

        public CharacterData GetCharacterData(string characterId)
        {
            if (characterDictionary.TryGetValue(characterId, out CharacterData characterData))
            {
                return characterData;
            }
            return null; // Or handle not found
        }

        public List<PlayerCharacterData> GetAllOwnedCharacters()
        {
            // Logic to return owned characters
            return new List<PlayerCharacterData>(); // Replace with actual logic
        }
    }
}