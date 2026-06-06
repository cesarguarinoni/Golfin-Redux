#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Reflection helpers around UnityEditor's internal GameViewSizes / GameViewSizeGroup so the bot
    /// recorder can record against the REAL iPhone-14 1170×2532 device preset — exactly the size the
    /// game lays out at in normal play — instead of letting Unity Recorder fabricate a custom
    /// "Recording Resolution" GameViewSize entry. That fabricated entry (250×540 in practice) put the
    /// Game View at a size the UI Canvas was never authored for, so the bottom nav bar / menu
    /// mis-laid-out and the Recorder captured an already-broken frame. It also persisted in the Game
    /// View dropdown after play mode, poisoning later captures.
    ///
    /// This is a Game-View-SIZE problem, not a Recorder output-resolution setting — the fix lives here.
    ///
    /// Surface:
    ///   • EnsureIPhone14Selected() — purge fabricated recorder entries, then select the existing
    ///     1170×2532 iPhone-14 preset (creating it once as a FixedResolution entry only if truly absent).
    ///   • PurgeFabricatedEntries() — delete the "Recording Resolution" / "Restored (BotVideoRecorder)"
    ///     custom sizes that earlier recorder code created.
    ///
    /// All reflection is best-effort and guarded: any failure logs a warning and leaves the view as-is
    /// rather than throwing into the recording pipeline.
    /// </summary>
    internal static class GameViewSizeUtil
    {
        public const int    IPhone14Width  = 1170;
        public const int    IPhone14Height = 2532;
        public const string IPhone14Name   = "iPhone 14";

        // Custom-size baseTexts that recorder code has historically fabricated; these get purged.
        // "Recording Resolution" is created by Unity Recorder's GameViewInput.SetCustomSize when the
        // requested output size differs from the current render size. "Restored (BotVideoRecorder)"
        // was created by an earlier BotVideoRecorder restore path (now removed).
        static readonly string[] FabricatedNames = { "Recording Resolution", "Restored (BotVideoRecorder)" };

        static readonly Assembly EdAsm             = typeof(UnityEditor.Editor).Assembly;
        static readonly Type GameViewSizesT        = EdAsm.GetType("UnityEditor.GameViewSizes");
        static readonly Type GameViewSizeT         = EdAsm.GetType("UnityEditor.GameViewSize");
        static readonly Type GameViewSizeTypeT     = EdAsm.GetType("UnityEditor.GameViewSizeType");
        static readonly Type GameViewSizeGroupT    = EdAsm.GetType("UnityEditor.GameViewSizeGroup");
        static readonly Type PlayModeViewT         = EdAsm.GetType("UnityEditor.PlayModeView");

        static bool Available =>
            GameViewSizesT != null && GameViewSizeT != null && GameViewSizeTypeT != null &&
            GameViewSizeGroupT != null && PlayModeViewT != null;

        // ── group / size reflection accessors ────────────────────────────────
        static object SizesInstance() => GameViewSizesT.BaseType
            .GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        static object CurrentGroup() => GameViewSizesT
            .GetProperty("currentGroup", BindingFlags.Public | BindingFlags.Instance)
            .GetValue(SizesInstance());

        // The real backing List&lt;GameViewSize&gt; of custom sizes. GameViewSizeGroup.RemoveCustomSize
        // did NOT mutate this in practice (verified on 6000.3), so we edit the list directly.
        static IList CustomList(object g) => (IList)GameViewSizeGroupT
            .GetField("m_Custom", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(g);

        // Persist + refresh after a direct list edit so the change survives a fresh currentGroup read,
        // the Game View dropdown rebuild, and editor restarts.
        static void PersistSizes()
        {
            var inst = SizesInstance();
            GameViewSizesT.GetMethod("Changed",   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(inst, null);
            GameViewSizesT.GetMethod("SaveToHDD", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(inst, null);
        }

        static int    TotalCount(object g)              => (int)GameViewSizeGroupT.GetMethod("GetTotalCount").Invoke(g, null);
        static object SizeAt(object g, int i)           => GameViewSizeGroupT.GetMethod("GetGameViewSize", new[] { typeof(int) }).Invoke(g, new object[] { i });
        static void   AddCustom(object g, object size)  => GameViewSizeGroupT.GetMethod("AddCustomSize").Invoke(g, new[] { size });

        static int    SizeW(object s)    => (int)GameViewSizeT.GetProperty("width").GetValue(s);
        static int    SizeH(object s)    => (int)GameViewSizeT.GetProperty("height").GetValue(s);
        static string SizeBase(object s) => (string)GameViewSizeT.GetProperty("baseText").GetValue(s);

        static bool IsFabricated(string baseText)
        {
            foreach (var n in FabricatedNames) if (n == baseText) return true;
            return false;
        }

        // ── public API ───────────────────────────────────────────────────────

        /// <summary>Delete every fabricated recorder custom-size entry from the current group.</summary>
        public static void PurgeFabricatedEntries()
        {
            if (!Available) return;
            try
            {
                var list = CustomList(CurrentGroup());
                if (list == null) return;
                int removed = 0;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (IsFabricated(SizeBase(list[i]))) { list.RemoveAt(i); removed++; }
                }
                if (removed > 0)
                {
                    PersistSizes();
                    Debug.Log($"[GameViewSizeUtil] Purged {removed} fabricated Game View size entr{(removed == 1 ? "y" : "ies")}.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameViewSizeUtil] PurgeFabricatedEntries failed: {e.Message}");
            }
        }

        /// <summary>
        /// Select the iPhone-14 1170×2532 preset on the main Game View, creating it once if absent.
        /// Purges fabricated recorder entries first. Returns true on success.
        /// </summary>
        public static bool EnsureIPhone14Selected()
        {
            if (!Available) return false;
            try
            {
                PurgeFabricatedEntries();

                var g = CurrentGroup();
                int idx = FindIPhone14Index(g);
                if (idx < 0)
                {
                    // Not present at all — add the real device size ONCE (FixedResolution, never a
                    // smaller fabricated value) and re-find it.
                    object fixedResType = Enum.Parse(GameViewSizeTypeT, "FixedResolution");
                    var ctor = GameViewSizeT.GetConstructor(new[] { GameViewSizeTypeT, typeof(int), typeof(int), typeof(string) });
                    var size = ctor.Invoke(new object[] { fixedResType, IPhone14Width, IPhone14Height, IPhone14Name });
                    AddCustom(g, size);
                    PersistSizes();
                    idx = FindIPhone14Index(CurrentGroup());
                    Debug.Log($"[GameViewSizeUtil] Added missing '{IPhone14Name}' {IPhone14Width}x{IPhone14Height} FixedResolution preset.");
                }
                if (idx < 0) return false;

                return SelectIndex(idx);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameViewSizeUtil] EnsureIPhone14Selected failed: {e.Message}");
                return false;
            }
        }

        // ── internals ────────────────────────────────────────────────────────

        static int FindIPhone14Index(object g)
        {
            int total = TotalCount(g);
            // Prefer an entry literally named "iPhone 14".
            for (int i = 0; i < total; i++)
            {
                var s = SizeAt(g, i);
                if (SizeW(s) == IPhone14Width && SizeH(s) == IPhone14Height && SizeBase(s) == IPhone14Name)
                    return i;
            }
            // Else any non-fabricated 1170×2532 entry (e.g. a "iPhone14" dup, or the builtin
            // "iPhone 12 Pro … Portrait" which is the same resolution → same Canvas layout).
            for (int i = 0; i < total; i++)
            {
                var s = SizeAt(g, i);
                if (SizeW(s) == IPhone14Width && SizeH(s) == IPhone14Height && !IsFabricated(SizeBase(s)))
                    return i;
            }
            return -1;
        }

        static bool SelectIndex(int totalIndex)
        {
            var getMain = PlayModeViewT.GetMethod("GetMainPlayModeView",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var gv = getMain?.Invoke(null, null) as EditorWindow;
            if (gv == null) return false;

            var size = SizeAt(CurrentGroup(), totalIndex);
            var ssc = gv.GetType().GetMethod("SizeSelectionCallback",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (ssc == null) return false;

            ssc.Invoke(gv, new object[] { totalIndex, size });
            gv.Repaint();
            return true;
        }
    }
}
#endif
