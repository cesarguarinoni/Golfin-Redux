#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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
    ///
    /// <para>THE SNAPSHOT IS THE GUARD; <c>TreeWindDriver.RestoreAuthored()</c> is only the fast
    /// path. The driver keeps its authored values in static dictionaries, and a domain reload
    /// throws those away — a script edit or an <c>AssetDatabase.Refresh</c> from a tool mid-play is
    /// enough. Play mode then exits with nothing to restore. That is not theoretical: it shipped
    /// TWICE. `2cd5c4e88` (build 2697) carried WindSpeedFloat1 0.5 -> 0.23272727 across eleven tree
    /// materials into TestFlight, was reverted by hand in `a9e8eb107` ("it was never asked for"),
    /// and then `3dc613362` (build 2758) shipped the identical value again, because the earlier
    /// version of this guard could only WARN about the reload, not survive it.</para>
    ///
    /// <para>So the authored values are now snapshotted to <see cref="SessionState"/> — which does
    /// survive domain reloads — the instant before play mode starts, and re-applied on the way out
    /// whatever happened in between. The materials are found by asset path rather than through the
    /// driver's terrain walk, because at snapshot time there is no active terrain yet.</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class TreeWindDriverEditorGuard
    {
        const string SnapshotKey  = "Golfin.TreeWindDriverEditorGuard.Snapshot";
        const string WindSpeedProp   = "WindSpeedFloat1";
        const string SpruceSpeedProp = "Vector1_b0ddedae341d4c7ba1d429299f3078ea";

        /// <summary>Where the drifting materials live. Narrow on purpose: this guard exists to
        /// protect tracked tree art, not to police every material in the project.</summary>
        static readonly string[] SearchFolders = { "Assets/Art/3D/Trees(2025)" };

        [Serializable] private class Entry
        {
            public string path;
            public bool   hasWind;   public float wind;
            public bool   hasSpruce; public float spruce;
        }

        [Serializable] private class Snapshot { public List<Entry> entries = new List<Entry>(); }

        static TreeWindDriverEditorGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)  Capture();
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                TreeWindDriver.RestoreAuthored();   // fast path; a no-op after a domain reload
                Restore();                          // the guarantee
            }
        }

        static void Capture()
        {
            var snap = new Snapshot();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", SearchFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;

                var e = new Entry { path = path };
                if (m.HasProperty(WindSpeedProp))   { e.hasWind   = true; e.wind   = m.GetFloat(WindSpeedProp); }
                if (m.HasProperty(SpruceSpeedProp)) { e.hasSpruce = true; e.spruce = m.GetFloat(SpruceSpeedProp); }
                if (e.hasWind || e.hasSpruce) snap.entries.Add(e);
            }
            SessionState.SetString(SnapshotKey, JsonUtility.ToJson(snap));
        }

        static void Restore()
        {
            string json = SessionState.GetString(SnapshotKey, "");
            if (string.IsNullOrEmpty(json)) return;

            Snapshot snap;
            try { snap = JsonUtility.FromJson<Snapshot>(json); }
            catch (Exception e) { Debug.LogWarning($"[TreeWindDriverEditorGuard] snapshot unreadable: {e.Message}"); return; }
            if (snap?.entries == null) return;

            int repaired = 0;
            foreach (var e in snap.entries)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(e.path);
                if (m == null) continue;

                bool changed = false;
                if (e.hasWind   && m.HasProperty(WindSpeedProp)   && !Mathf.Approximately(m.GetFloat(WindSpeedProp), e.wind))
                { m.SetFloat(WindSpeedProp, e.wind); changed = true; }
                if (e.hasSpruce && m.HasProperty(SpruceSpeedProp) && !Mathf.Approximately(m.GetFloat(SpruceSpeedProp), e.spruce))
                { m.SetFloat(SpruceSpeedProp, e.spruce); changed = true; }

                if (changed) { EditorUtility.SetDirty(m); repaired++; }
            }

            if (repaired > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[TreeWindDriverEditorGuard] Restored authored wind on {repaired} material(s) " +
                          "that play mode had left baked — the fast path had been lost to a domain reload.");
            }
            SessionState.EraseString(SnapshotKey);
        }
    }
}
#endif
