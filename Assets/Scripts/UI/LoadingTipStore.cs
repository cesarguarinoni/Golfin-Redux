// ─────────────────────────────────────────────────────────────────────────────
// loading_tips §3.4 — where the tip position lives between runs.
//
// PlayerPrefs, NOT SaveData, and that is an Architect default the spec flags: a
// tutorial-order counter is a property of the DEVICE, not of the account. Tying
// it to the save would mean a player who reinstalls — the one player who most
// needs the first pool — resumes at "general, random", and a second device would
// inherit a tutorial it never showed.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using UnityEngine;

namespace GolfinRedux.UI
{
    public static class LoadingTipStore
    {
        public const string PrefsKey = "loadingtips.state";

        public static LoadingTipState Default => new LoadingTipState
        {
            firstPass  = 0,
            firstIndex = 0,
            recentKeys = Array.Empty<string>(),
        };

        public static LoadingTipState Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return Default;

            try
            {
                LoadingTipState s = JsonUtility.FromJson<LoadingTipState>(json);
                s.recentKeys ??= Array.Empty<string>();
                return s;
            }
            catch (Exception e)
            {
                // A corrupt blob restarts the tutorial; it never throws on the loading screen.
                Debug.LogWarning($"[LoadingTipStore] '{PrefsKey}' unreadable ({e.GetType().Name}) — restarting the tip order");
                return Default;
            }
        }

        public static void Save(LoadingTipState state)
        {
            state.recentKeys ??= Array.Empty<string>();
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        /// <summary>Test / QA seam — a cleared store is a fresh install.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
