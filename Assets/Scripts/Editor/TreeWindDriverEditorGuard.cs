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
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                TreeWindDriver.RestoreAuthored();
        }
    }
}
#endif
