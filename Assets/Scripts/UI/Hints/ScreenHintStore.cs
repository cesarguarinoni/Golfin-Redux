// ─────────────────────────────────────────────────────────────────────────────
// screen_hints §3.4 — which screens have been hinted, between runs.
//
// PlayerPrefs, per DEVICE, and that is Cesar's decision (2026-09-10), not an
// Architect default: a reinstall restarts the tutorial, which is the point —
// the player who most needs the hints is the one on a fresh device. Same
// JsonUtility blob shape as LoadingTipStore; the two stores are deliberately
// separate (a hint read here does not advance the loading-screen pools).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using UnityEngine;

namespace GolfinRedux.UI
{
    /// <summary>The persisted blob. Arrays, not lists, because <c>JsonUtility</c> round-trips
    /// arrays and a struct with no reference cycles cleanly.</summary>
    [Serializable]
    public struct ScreenHintState
    {
        /// <summary>Screens whose first hint has OPENED (Architect default a — not "whose last
        /// CLOSE was tapped"). Also includes screens entered with nothing to show.</summary>
        public string[] seenScreens;

        /// <summary>Tip keys that have been shown by any screen (default b — skipped everywhere else).</summary>
        public string[] seenKeys;
    }

    public static class ScreenHintStore
    {
        public const string PrefsKey = "screenhints.state";

        public static ScreenHintState Default => new ScreenHintState
        {
            seenScreens = Array.Empty<string>(),
            seenKeys    = Array.Empty<string>(),
        };

        public static ScreenHintState Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return Default;

            try
            {
                ScreenHintState s = JsonUtility.FromJson<ScreenHintState>(json);
                s.seenScreens ??= Array.Empty<string>();
                s.seenKeys    ??= Array.Empty<string>();
                return s;
            }
            catch (Exception e)
            {
                // A corrupt blob restarts the tutorial; it never throws on a screen change.
                Debug.LogWarning($"[ScreenHintStore] '{PrefsKey}' unreadable ({e.GetType().Name}) — restarting the screen hints");
                return Default;
            }
        }

        public static void Save(ScreenHintState state)
        {
            state.seenScreens ??= Array.Empty<string>();
            state.seenKeys    ??= Array.Empty<string>();
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        /// <summary>Test / QA seam — a cleared store is a fresh install
        /// (<c>GOLFIN ▸ Hints ▸ Reset seen</c>).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>The state with <paramref name="value"/> appended to <paramref name="list"/>
        /// if it is not already there. Pure; the caller saves.</summary>
        public static string[] With(string[]? list, string value)
        {
            list ??= Array.Empty<string>();
            if (Array.IndexOf(list, value) >= 0) return list;
            var next = new string[list.Length + 1];
            Array.Copy(list, next, list.Length);
            next[list.Length] = value;
            return next;
        }
    }
}
