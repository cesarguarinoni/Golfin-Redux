#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Clears TMP <b>dynamic</b> font atlases when leaving play mode, so a play session
    /// never leaves a baked atlas serialized into the asset file.
    ///
    /// Why this exists
    /// ---------------
    /// A dynamic TMP font asset rasterises glyphs on demand and writes them into its own
    /// atlas texture. Render Japanese once in the editor and
    /// `NotoSansJP-VariableFont_wght SDF.asset` grows from ~59 KB to ~2.3 MB, showing up
    /// as a fat binary diff that has nothing to do with the change you were making. It
    /// came back twice in one session on `tournaments_mode_card` and had to be reverted
    /// by hand both times.
    ///
    /// The bloat never shipped — `m_ClearDynamicDataOnBuild` is already true, so player
    /// builds strip it. This is purely an editor-hygiene problem, which is exactly what
    /// this guard addresses: it performs the same clear TMP already does at build time,
    /// at play-mode exit instead.
    ///
    /// Cost: the next play session re-rasterises glyphs on demand (milliseconds). Only
    /// assets whose <c>atlasPopulationMode</c> is <see cref="AtlasPopulationMode.Dynamic"/>
    /// are touched — static/pre-baked atlases are authored data and are left alone.
    ///
    /// Escape hatch: uncheck <c>GOLFIN &gt; Fonts &gt; Clear Dynamic Atlases On Play Exit</c>
    /// when you deliberately want glyphs baked into the asset.
    /// </summary>
    [InitializeOnLoad]
    public static class DynamicFontAtlasGuard
    {
        const string PrefKey  = "Golfin.DynamicFontAtlasGuard.Enabled";
        const string MenuPath = "GOLFIN/Fonts/Clear Dynamic Atlases On Play Exit";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static DynamicFontAtlasGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem(MenuPath)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, isValidateFunction: true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // EnteredEditMode, not ExitingPlayMode: the runtime is still alive during
            // ExitingPlayMode and can rasterise more glyphs after we clear.
            if (state != PlayModeStateChange.EnteredEditMode) return;
            if (!Enabled) return;
            ClearDynamicAtlases(logWhenClean: false);
        }

        [MenuItem("GOLFIN/Fonts/Clear Dynamic Atlases Now")]
        static void ClearNow() => ClearDynamicAtlases(logWhenClean: true);

        static void ClearDynamicAtlases(bool logWhenClean)
        {
            var cleared = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;

                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null) continue;
                if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;

                // Nothing baked → nothing to clear. Avoids dirtying every font every exit.
                if (font.glyphTable.Count == 0 && font.characterTable.Count == 0) continue;

                int glyphs = font.glyphTable.Count;
                // setAtlasSizeToZero:true is required, and is safe. It drops the atlas
                // TEXTURE PAYLOAD (the 1 MB of Alpha8 pixels that makes the asset fat)
                // while LEAVING atlasWidth/atlasHeight at their authored values — verified
                // 1024x1024 before and after. TryAddCharacters on a font in this state
                // still rasterises normally, so nothing ships as tofu.
                // Passing false clears only the glyph/character tables and leaves the
                // pixels serialized, which does not shrink the file at all (measured:
                // 2,104,924 bytes either way) and so fails the entire purpose.
                font.ClearFontAssetData(setAtlasSizeToZero: true);
                EditorUtility.SetDirty(font);
                cleared.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)} ({glyphs} glyphs)");
            }

            if (cleared.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[DynamicFontAtlasGuard] Cleared baked glyphs from "
                        + cleared.Count + " dynamic font asset(s): "
                        + string.Join(", ", cleared)
                        + ". They re-rasterise on demand; this keeps the atlas out of your diff.");
            }
            else if (logWhenClean)
            {
                Debug.Log("[DynamicFontAtlasGuard] No dynamic font asset had baked glyphs — nothing to clear.");
            }
        }
    }
}
#endif
