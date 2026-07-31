using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.Demo
{
    /// <summary>
    /// Soft-gating config for the demo build (demo_build_slice §3.4). Ships as data
    /// inside the binary at Assets/Resources/Data/demo_config.csv — NOT remote config,
    /// so it can't be flipped by a user or a server (keeps clear of guideline 2.3.1).
    ///
    /// Static-catalog pattern (mirrors GachaBannerModel): Resources.Load&lt;TextAsset&gt;,
    /// header-skip parse, lazy singleton. Consumers read the parsed fields; wiring the
    /// values into CharacterManager / ClubManager / item systems is the soft-gating step.
    /// The file is present in every build regardless of scene list, defines, or asmdefs
    /// because Assets/Resources always ships.
    /// </summary>
    public sealed class DemoConfig
    {
        const string ResourcePath = "Data/demo_config";

        public string[] PlayableHoles { get; private set; } = Array.Empty<string>();
        public string CharacterId { get; private set; } = string.Empty;
        public string[] ClubIds { get; private set; } = Array.Empty<string>();
        public bool RepairKitsEnabled { get; private set; }
        public bool BallsEnabled { get; private set; }
        public bool PointsEnabled { get; private set; }

        static DemoConfig _instance;

        /// <summary>Lazily loads and caches the parsed demo config. Never null.</summary>
        public static DemoConfig Instance => _instance ??= Load();

        static DemoConfig Load()
        {
            var config = new DemoConfig();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[DemoConfig] Missing Resources/{ResourcePath}.csv — using empty defaults.");
                return config;
            }

            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("key,")) continue; // skip blanks + header

                int comma = line.IndexOf(',');
                if (comma < 0) continue;
                var key = line.Substring(0, comma).Trim();
                var value = line.Substring(comma + 1).Trim();

                switch (key)
                {
                    case "playable_holes": config.PlayableHoles = SplitList(value); break;
                    case "character_id":   config.CharacterId = value; break;
                    case "club_ids":       config.ClubIds = SplitList(value); break;
                    case "repair_kits_enabled": config.RepairKitsEnabled = ParseBool(value); break;
                    case "balls_enabled":       config.BallsEnabled = ParseBool(value); break;
                    case "points_enabled":      config.PointsEnabled = ParseBool(value); break;
                }
            }
            return config;
        }

        static string[] SplitList(string value)
        {
            if (string.IsNullOrEmpty(value)) return Array.Empty<string>();
            var parts = value.Split(';');
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
                if (trimmed.Length > 0) list.Add(trimmed);
            }
            return list.ToArray();
        }

        static bool ParseBool(string value) =>
            value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }
}
