// ─────────────────────────────────────────────────────────────────────────────
// loading_tips §3.1 — the tip catalog.
//
// WHY A CSV AND NOT THE string[] IT REPLACES. ProTipCard used to carry a
// hard-coded `string[] tipKeys` and a `Sprite[]` matched BY INDEX, so adding a
// key without adding a sprite in the same slot silently shifted every image one
// tip down. The catalog names its sprite, so a row and its picture travel
// together and a missing sprite costs exactly that one row its image.
//
// NOT A CONTENT CATALOG (spec §3.1). This file is bundled and client-only: it is
// not in ContentCatalogs.All, has no migration and no admin panel. The tip TEXT
// is already admin-editable through the `texts` catalog; only pool membership,
// order and the active flag live here.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.UI
{
    /// <summary>One row of <c>Assets/Resources/Data/LoadingTips.csv</c>.</summary>
    [Serializable]
    public struct LoadingTip
    {
        /// <summary>Localization key — also the identity of the row everywhere else.</summary>
        public string key;

        /// <summary>Member of the fixed-order first pool (§2.1). First rows are ALSO
        /// members of the general pool.</summary>
        public bool first;

        /// <summary>Sorts the first pool; cosmetic for general-only rows.</summary>
        public int order;

        /// <summary>Lookup NAME into <c>ProTipCard.tipSprites</c> — not a Resources path,
        /// so the art keeps its GUIDs and its folder.</summary>
        public string sprite;

        /// <summary>§2.3 — a row whose system has not shipped is parsed but never shown.</summary>
        public bool active;

        /// <summary>A row that carries no key is the "nothing to show" sentinel
        /// (empty general pool), never a real tip.</summary>
        public bool IsValid => !string.IsNullOrEmpty(key);
    }

    /// <summary>Parses <c>LoadingTips.csv</c>. Malformed rows are dropped with one warning
    /// each; a bad row never takes the loading screen down with it.</summary>
    public static class LoadingTipCatalog
    {
        /// <summary>Same convention as <c>Tools/content/catalogs.py</c>.</summary>
        public const string CommentPrefix = "#";

        public const string ResourcePath = "Data/LoadingTips";

        public static IReadOnlyList<LoadingTip> Load(TextAsset? csv)
            => csv == null ? Array.Empty<LoadingTip>() : Parse(csv.text);

        /// <summary>Resources fallback for callers with no serialized TextAsset (tests,
        /// and a card whose field was never dragged).</summary>
        public static IReadOnlyList<LoadingTip> LoadFromResources()
            => Parse(Resources.Load<TextAsset>(ResourcePath)?.text);

        public static IReadOnlyList<LoadingTip> Parse(string? text)
        {
            var rows = new List<LoadingTip>();
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
                    if (line.StartsWith("key,", StringComparison.OrdinalIgnoreCase)) continue;
                }

                string[] cell = line.Split(',');
                if (cell.Length < 5)
                {
                    Debug.LogWarning($"[LoadingTipCatalog] line {i + 1}: expected 5 columns, got {cell.Length} — row dropped: {line}");
                    continue;
                }

                string key = cell[0].Trim();
                if (key.Length == 0)
                {
                    Debug.LogWarning($"[LoadingTipCatalog] line {i + 1}: empty key — row dropped");
                    continue;
                }

                string pool = cell[1].Trim();
                if (!pool.Equals("first", StringComparison.OrdinalIgnoreCase) &&
                    !pool.Equals("general", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"[LoadingTipCatalog] line {i + 1}: pool must be first|general, got '{pool}' — row dropped");
                    continue;
                }

                if (!int.TryParse(cell[2].Trim(), out int order))
                {
                    Debug.LogWarning($"[LoadingTipCatalog] line {i + 1}: order '{cell[2]}' is not an integer — row dropped");
                    continue;
                }

                rows.Add(new LoadingTip
                {
                    key    = key,
                    first  = pool.Equals("first", StringComparison.OrdinalIgnoreCase),
                    order  = order,
                    sprite = cell[3].Trim(),
                    active = ParseActive(cell[4]),
                });
            }

            return rows;
        }

        static bool ParseActive(string cell)
        {
            string v = cell.Trim();
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
