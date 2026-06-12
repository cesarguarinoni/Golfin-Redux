using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Loads tree_collision_profiles.csv from Resources/Data and per-hole tree_obstacles.csv
    /// from the hole's Data folder. Mirrors HeightmapLoader patterns.
    /// </summary>
    public static class TreeObstacleLoader
    {
        private static Dictionary<string, TreeCollisionProfile> _profiles;

        /// <summary>
        /// Ensure the profile table is loaded from Resources/Data/tree_collision_profiles.csv.
        /// Thread-safe via a reference check; main-thread only due to Resources.Load.
        /// </summary>
        public static Dictionary<string, TreeCollisionProfile> GetProfiles()
        {
            if (_profiles != null) return _profiles;
            _profiles = new Dictionary<string, TreeCollisionProfile>(System.StringComparer.OrdinalIgnoreCase);

            var asset = Resources.Load<TextAsset>("Data/tree_collision_profiles");
            if (asset == null)
            {
                Debug.LogWarning("[TreeObstacleLoader] tree_collision_profiles.csv not found in Resources/Data/. No tree profiles loaded.");
                return _profiles;
            }

            using (var reader = new StringReader(asset.text))
            {
                string line;
                bool headerSkipped = false;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("#") || line.Length == 0) continue;
                    if (!headerSkipped)
                    {
                        headerSkipped = true; // skip CSV header row
                        continue;
                    }

                    var parts = line.Split(',');
                    if (parts.Length < 7) continue;

                    string name = parts[0].Trim();
                    if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float trunkRadius)) continue;
                    if (!float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float trunkHeight)) continue;
                    if (!float.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float canopyRadius)) continue;
                    if (!float.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float canopyTop)) continue;
                    if (!float.TryParse(parts[5].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float trunkRestitution)) continue;
                    if (!float.TryParse(parts[6].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float canopyDamping)) continue;

                    var profile = new TreeCollisionProfile(
                        name,
                        fp.FromFloat(trunkRadius),
                        fp.FromFloat(trunkHeight),
                        fp.FromFloat(canopyRadius),
                        fp.FromFloat(canopyTop),
                        fp.FromFloat(trunkRestitution),
                        fp.FromFloat(canopyDamping));

                    _profiles[name] = profile;
                }
            }

            if (!_profiles.ContainsKey("default"))
            {
                // Hard-coded fallback if CSV parsing produced no default row.
                _profiles["default"] = new TreeCollisionProfile(
                    "default",
                    fp.FromFloat(0.25f), fp.FromFloat(3.0f),
                    fp.FromFloat(3.0f),  fp.FromFloat(9.0f),
                    fp.FromFloat(0.15f), fp.FromFloat(0.40f));
            }

            return _profiles;
        }

        /// <summary>
        /// Lookup profile by name; falls back to "default" row.
        /// </summary>
        public static TreeCollisionProfile GetProfile(string prefabName)
        {
            var profiles = GetProfiles();
            if (profiles.TryGetValue(prefabName, out var p)) return p;
            if (profiles.TryGetValue("default", out var d)) return d;
            // Ultra-defensive: return a built-in default.
            return new TreeCollisionProfile("default",
                fp.FromFloat(0.25f), fp.FromFloat(3.0f),
                fp.FromFloat(3.0f),  fp.FromFloat(9.0f),
                fp.FromFloat(0.15f), fp.FromFloat(0.40f));
        }

        /// <summary>
        /// Load tree instances from a per-hole tree_obstacles.csv TextAsset.
        /// Returns null (no trees) if the asset is null.
        /// CSV format: comment line with bake hash, then header row, then data rows:
        ///   worldX,worldZ,baseY,scale,profileName
        /// </summary>
        public static List<TreeInstance> LoadInstances(TextAsset asset)
        {
            if (asset == null) return null;
            return LoadInstancesFromText(asset.text);
        }

        /// <summary>
        /// Load tree instances from raw CSV text. Used by the provider and for tests.
        /// </summary>
        public static List<TreeInstance> LoadInstancesFromText(string csvText)
        {
            if (string.IsNullOrEmpty(csvText)) return null;

            var list = new List<TreeInstance>();
            using (var reader = new StringReader(csvText))
            {
                string line;
                bool headerSkipped = false;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("#") || line.Length == 0) continue;
                    if (!headerSkipped)
                    {
                        headerSkipped = true;
                        continue;
                    }

                    var parts = line.Split(',');
                    if (parts.Length < 5) continue;

                    if (!float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float wx)) continue;
                    if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float wz)) continue;
                    if (!float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float by)) continue;
                    if (!float.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float scale)) continue;

                    string profileName = parts[4].Trim();
                    var profile = GetProfile(profileName);

                    list.Add(new TreeInstance(
                        fp.FromFloat(wx), fp.FromFloat(wz),
                        fp.FromFloat(by), fp.FromFloat(scale),
                        profile));
                }
            }

            return list.Count > 0 ? list : null;
        }
    }
}
