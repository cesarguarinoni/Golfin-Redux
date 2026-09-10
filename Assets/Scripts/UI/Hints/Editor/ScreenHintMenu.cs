#if UNITY_EDITOR
using GolfinRedux.UI;
using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools.Hints
{
    /// <summary>QA seams for the two per-device tutorial stores (screen_hints §3.4).</summary>
    public static class ScreenHintMenu
    {
        [MenuItem("GOLFIN/Hints/Reset seen")]
        public static void ResetSeen()
        {
            ScreenHintStore.Clear();
            Debug.Log("[ScreenHint] PlayerPrefs '" + ScreenHintStore.PrefsKey + "' cleared — every screen hints again.");
        }

        [MenuItem("GOLFIN/Hints/Reset loading tip position")]
        public static void ResetLoadingTips()
        {
            LoadingTipStore.Clear();
            Debug.Log("[LoadingTips] PlayerPrefs '" + LoadingTipStore.PrefsKey + "' cleared — the loading screen restarts at tip 1.");
        }
    }
}
#endif
