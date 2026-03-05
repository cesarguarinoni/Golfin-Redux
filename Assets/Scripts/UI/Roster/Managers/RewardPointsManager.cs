#nullable enable
using UnityEngine;
using System;

namespace Golfin.Roster
{
    /// <summary>
    /// Manages player's Reward Points (R currency)
    /// Singleton pattern
    /// Handles earning, spending, and persistence via PlayerPrefs
    /// </summary>
    public class RewardPointsManager : MonoBehaviour
    {
        public static RewardPointsManager Instance { get; private set; }
        
        private int currentPoints;
        private const string PREFS_KEY = "GOLFIN_REWARD_POINTS";
        private const int DEFAULT_STARTING_POINTS = 50000;
        
        // Event for UI updates
        public event System.Action<int> OnPointsChanged;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            LoadPoints();
        }
        
        private void LoadPoints()
        {
            if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                currentPoints = PlayerPrefs.GetInt(PREFS_KEY);
            }
            else
            {
                currentPoints = DEFAULT_STARTING_POINTS;
                SavePoints();
            }
            
            Debug.Log($"[RewardPointsManager] Loaded {currentPoints} points");
        }
        
        private void SavePoints()
        {
            PlayerPrefs.SetInt(PREFS_KEY, currentPoints);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Get current reward points
        /// </summary>
        public int GetPoints()
        {
            return currentPoints;
        }
        
        /// <summary>
        /// Check if player can afford an amount
        /// </summary>
        public bool CanAfford(int amount)
        {
            return currentPoints >= amount;
        }
        
        /// <summary>
        /// Spend points (returns true if successful)
        /// </summary>
        public bool SpendPoints(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[RewardPointsManager] Cannot spend negative amount: {amount}");
                return false;
            }
            
            if (!CanAfford(amount))
            {
                Debug.LogWarning($"[RewardPointsManager] Cannot afford {amount}R (have {currentPoints}R)");
                return false;
            }
            
            currentPoints -= amount;
            SavePoints();
            OnPointsChanged?.Invoke(currentPoints);
            
            Debug.Log($"[RewardPointsManager] Spent {amount}R, now have {currentPoints}R");
            return true;
        }
        
        /// <summary>
        /// Earn points
        /// </summary>
        public void EarnPoints(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[RewardPointsManager] Cannot earn negative amount: {amount}");
                return;
            }
            
            currentPoints += amount;
            SavePoints();
            OnPointsChanged?.Invoke(currentPoints);
            
            Debug.Log($"[RewardPointsManager] Earned {amount}R, now have {currentPoints}R");
        }
        
        /// <summary>
        /// Set points directly (for testing or rewards)
        /// </summary>
        public void SetPoints(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[RewardPointsManager] Cannot set negative points: {amount}");
                return;
            }
            
            currentPoints = amount;
            SavePoints();
            OnPointsChanged?.Invoke(currentPoints);
            
            Debug.Log($"[RewardPointsManager] Set points to {currentPoints}R");
        }
        
        /// <summary>
        /// Reset to default (for testing)
        /// </summary>
        public void ResetToDefault()
        {
            currentPoints = DEFAULT_STARTING_POINTS;
            SavePoints();
            OnPointsChanged?.Invoke(currentPoints);
        }
    }
}
