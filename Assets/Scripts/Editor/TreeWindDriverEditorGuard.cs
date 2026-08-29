#if UNITY_EDITOR
using UnityEditor;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.EditorTools
{
    /// <summary>
    /// TreeWindDriver writes the hole's wind onto shared Custom/Vegetation materials at runtime.
    /// In the editor those are the .mat ASSETS, so without this guard the last hole's wind value
    /// would be left baked into them on disk (exactly the kind of silent asset drift this project
    /// has been bitten by before). Restore the authored values the moment play mode exits.
    ///
    /// Player builds don't need this — there is no disk to write back to.
    /// </summary>
    [InitializeOnLoad]
    internal static class TreeWindDriverEditorGuard
    {
        static TreeWindDriverEditorGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // A DOMAIN RELOAD DEFEATS THE GUARD, SILENTLY. The authored values live in static
            // dictionaries on TreeWindDriver, so anything that reloads the domain mid-play —
            // a script edit, an AssetDatabase.Refresh from a tool — throws them away. Play mode
            // then exits with nothing to restore and the .mat assets keep whatever the last hole
            // wrote, which is how eleven tree materials ended up at WindSpeedFloat1 = 0 on
            // 2026-08-30 and had to be reverted by hand.
            //
            // This constructor re-runs on every domain reload, so if one happens WHILE playing,
            // that is the moment to say so. It cannot repair it — the values are already gone —
            // but a warning turns silent asset drift into something a `git status` gets checked
            // for, which is the whole difference.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                UnityEngine.Debug.LogWarning(
                    "[TreeWindDriverEditorGuard] Domain reload during play mode — the authored " +
                    "tree-wind values were lost, so exiting play cannot restore them. Check " +
                    "`git status` on Assets/Art/3D/Trees(2025)/**/Materials before committing.");
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                TreeWindDriver.RestoreAuthored();
        }
    }
}
#endif
