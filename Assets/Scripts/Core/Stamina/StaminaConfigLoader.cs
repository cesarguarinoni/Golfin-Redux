#nullable enable
// ─────────────────────────────────────────────────────────────────────────────
// StaminaConfigLoader — thin bootstrap that loads stamina_economy.csv from
// Resources and calls StaminaModel.Configure.
//
// Matches the pattern in Golfin.Gameplay.Config.ControlsConfigLoader:
//   Resources.Load<TextAsset>("Gameplay/controls")  →  "Gameplay/controls.csv"
// This loader uses:
//   Resources.Load<TextAsset>("Gameplay/stamina_economy")  →  "Gameplay/stamina_economy.csv"
//
// Only this class touches UnityEngine.Resources / IO.
// StaminaModel and StaminaConfig.Parse remain pure.
// ─────────────────────────────────────────────────────────────────────────────
using UnityEngine;

namespace Golfin.Core.Stamina
{
    /// <summary>
    /// Call StaminaConfigLoader.Load() once at game startup (e.g. from an Awake
    /// MonoBehaviour or from a RuntimeInitializeOnLoadMethod). It reads the CSV
    /// TextAsset from Resources/Gameplay/ and configures StaminaModel.
    /// </summary>
    public static class StaminaConfigLoader
    {
        private const string ResourcePath = "Gameplay/stamina_economy";

        /// <summary>
        /// Load the stamina_economy.csv TextAsset from Resources and configure StaminaModel.
        /// Safe to call multiple times; re-configures on every call.
        /// </summary>
        public static void Load()
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta == null)
            {
                Debug.LogError($"[StaminaConfigLoader] '{ResourcePath}.csv' not found in Resources — StaminaModel is NOT configured.");
                return;
            }

            var config = StaminaConfig.Parse(ta.text);
            StaminaModel.Configure(config);
            Debug.Log("[StaminaConfigLoader] Stamina economy loaded and configured.");
        }
    }
}
