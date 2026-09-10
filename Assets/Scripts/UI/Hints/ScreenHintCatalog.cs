// ─────────────────────────────────────────────────────────────────────────────
// screen_hints §3.1 — which Loading tips open as a modal on the FIRST entry into
// each screen.
//
// A sibling of LoadingTipCatalog with the same shape and the same reasoning:
// bundled, client-only, NOT a content catalog. The tip TEXT is admin-editable
// through `texts`; the screen → key mapping is a client concern (registering
// it as a catalog is Notion 2239, deferred). The tip's sprite and active flag
// are NOT repeated here — they come from the LoadingTips.csv row the key names,
// so a tip that flips active=1 there starts hinting here with no change.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.UI
{
    /// <summary>One row of <c>Assets/Resources/Data/ScreenHints.csv</c>.</summary>
    [Serializable]
    public struct ScreenHint
    {
        /// <summary>A <see cref="ScreenId"/> name, or one of the two
        /// <see cref="ScreenHintCatalog"/> constants for the entry points that are not
        /// ScreenIds (the shot view, the Settings › Controls submenu).</summary>
        public string screen;

        /// <summary>The <c>n/X</c> order within the screen, from 1.</summary>
        public int order;

        /// <summary>A <c>LoadingTips.csv</c> key.</summary>
        public string key;
    }

    /// <summary>Parses <c>ScreenHints.csv</c>. Malformed rows are dropped with one warning
    /// each — a bad row never takes a screen down with it.</summary>
    public static class ScreenHintCatalog
    {
        /// <summary>Same convention as <c>LoadingTipCatalog</c> / <c>Tools/content/catalogs.py</c>.</summary>
        public const string CommentPrefix = "#";

        public const string ResourcePath = "Data/ScreenHints";

        /// <summary>The shot view. Not a <see cref="ScreenId"/>: <c>GameplaySceneLoader</c>
        /// additively loads it and calls <c>ScreenHintPresenter.NotifyScreenEntered</c> at the
        /// reveal (its LoadCoroutine step 7).</summary>
        public const string GameplayScreen = "Gameplay";

        /// <summary>Settings › Controls. Not a <see cref="ScreenId"/>: Settings is an overlay
        /// and the submenu is an accordion row; <c>ControlsSubmenu.OnEnable</c> notifies.</summary>
        public const string SettingsControlsScreen = "SettingsControls";

        /// <summary>The catalog id of a real screen — its enum name, so the CSV reads as the
        /// code does and a renamed ScreenId breaks <c>ScreenHintCatalogTests</c>, not the player.</summary>
        public static string IdFor(ScreenId id) => id.ToString();

        public static IReadOnlyList<ScreenHint> Load(TextAsset? csv)
            => csv == null ? Array.Empty<ScreenHint>() : Parse(csv.text);

        /// <summary>Resources fallback for callers with no serialized TextAsset (tests, and a
        /// presenter whose field was never dragged).</summary>
        public static IReadOnlyList<ScreenHint> LoadFromResources()
            => Parse(Resources.Load<TextAsset>(ResourcePath)?.text);

        public static IReadOnlyList<ScreenHint> Parse(string? text)
        {
            var rows = new List<ScreenHint>();
            if (string.IsNullOrEmpty(text)) return rows;

            string[] lines = text!.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool headerSeen = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith(CommentPrefix, StringComparison.Ordinal)) continue;

                if (!headerSeen)
                {
                    // The header is the first non-comment line; it is not data.
                    headerSeen = true;
                    if (line.StartsWith("screen,", StringComparison.OrdinalIgnoreCase)) continue;
                }

                string[] cell = line.Split(',');
                if (cell.Length < 3)
                {
                    Debug.LogWarning($"[ScreenHintCatalog] line {i + 1}: expected 3 columns, got {cell.Length} — row dropped: {line}");
                    continue;
                }

                string screen = cell[0].Trim();
                if (screen.Length == 0)
                {
                    Debug.LogWarning($"[ScreenHintCatalog] line {i + 1}: empty screen — row dropped");
                    continue;
                }

                if (!int.TryParse(cell[1].Trim(), out int order))
                {
                    Debug.LogWarning($"[ScreenHintCatalog] line {i + 1}: order '{cell[1]}' is not an integer — row dropped");
                    continue;
                }

                string key = cell[2].Trim();
                if (key.Length == 0)
                {
                    Debug.LogWarning($"[ScreenHintCatalog] line {i + 1}: empty key — row dropped");
                    continue;
                }

                rows.Add(new ScreenHint { screen = screen, order = order, key = key });
            }

            return rows;
        }
    }
}
