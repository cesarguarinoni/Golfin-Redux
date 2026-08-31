using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Loads bridge_collision_profiles.csv from Resources/Data and the per-hole
    /// bridge_obstacles.csv from Resources/HoleData/&lt;courseSlug&gt;/Hole_NN/.
    /// Beat-for-beat mirror of <see cref="TreeObstacleLoader"/>.
    /// </summary>
    public static class BridgeObstacleLoader
    {
        private static Dictionary<string, BridgeCollisionProfile> _profiles;
        private static HashSet<string> _warnedMissingProfiles;

        public static Dictionary<string, BridgeCollisionProfile> GetProfiles()
        {
            if (_profiles != null) return _profiles;
            _profiles = new Dictionary<string, BridgeCollisionProfile>(System.StringComparer.OrdinalIgnoreCase);
            _warnedMissingProfiles = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            var asset = Resources.Load<TextAsset>("Data/bridge_collision_profiles");
            if (asset == null)
            {
                Debug.LogWarning("[BridgeObstacleLoader] bridge_collision_profiles.csv not found in Resources/Data/. No bridge profiles loaded.");
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
                    if (!headerSkipped) { headerSkipped = true; continue; }

                    var parts = line.Split(',');
                    if (parts.Length < 3) continue;

                    string name = parts[0].Trim();
                    if (!TryF(parts[1], out float restitution)) continue;
                    if (!TryF(parts[2], out float tangentDamping)) continue;

                    _profiles[name] = new BridgeCollisionProfile(
                        name, fp.FromFloat(restitution), fp.FromFloat(tangentDamping));
                }
            }

            if (!_profiles.ContainsKey("default"))
                _profiles["default"] = new BridgeCollisionProfile(
                    "default", fp.FromFloat(0.35f), fp.FromFloat(0.75f));

            return _profiles;
        }

        /// <summary>
        /// Lookup by part name; falls back to "default".
        ///
        /// The fallback WARNS, once per distinct name. Carried over verbatim from the tree
        /// loader's hard-won comment: an unprofiled name there meant hole 6 shipped every fir
        /// colliding as the generic 0.25 m cylinder for months, silently. A bridge whose parts
        /// all fall back to `default` is the same class of silent mis-tuning.
        /// </summary>
        public static BridgeCollisionProfile GetProfile(string partName)
        {
            var profiles = GetProfiles();
            if (profiles.TryGetValue(partName, out var p)) return p;

            if (_warnedMissingProfiles != null && _warnedMissingProfiles.Add(partName))
            {
                Debug.LogWarning(
                    $"[BridgeObstacleLoader] No collision profile for bridge part '{partName}' — " +
                    "falling back to `default` (restitution 0.35, tangentDamping 0.75). " +
                    "Add a measured row to Assets/Resources/Data/bridge_collision_profiles.csv.");
            }

            if (profiles.TryGetValue("default", out var d)) return d;
            return new BridgeCollisionProfile("default", fp.FromFloat(0.35f), fp.FromFloat(0.75f));
        }

        /// <summary>Load bridge boxes from a per-hole bridge_obstacles.csv TextAsset. Null asset → null.</summary>
        public static List<BridgeBox> LoadBoxes(TextAsset asset)
            => asset == null ? null : LoadBoxesFromText(asset.text);

        /// <summary>
        /// CSV: a `# bake_hash=` comment, a header row, then
        /// <c>centerX,centerZ,baseY,topY,halfX,halfZ,yawDeg,profileName</c>.
        /// </summary>
        public static List<BridgeBox> LoadBoxesFromText(string csvText)
        {
            if (string.IsNullOrEmpty(csvText)) return null;

            var list = new List<BridgeBox>();
            using (var reader = new StringReader(csvText))
            {
                string line;
                bool headerSkipped = false;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("#") || line.Length == 0) continue;
                    if (!headerSkipped) { headerSkipped = true; continue; }

                    var parts = line.Split(',');
                    if (parts.Length < 8) continue;

                    if (!TryF(parts[0], out float cx))    continue;
                    if (!TryF(parts[1], out float cz))    continue;
                    if (!TryF(parts[2], out float baseY)) continue;
                    if (!TryF(parts[3], out float topY))  continue;
                    if (!TryF(parts[4], out float hx))    continue;
                    if (!TryF(parts[5], out float hz))    continue;
                    if (!TryF(parts[6], out float yawDeg))continue;

                    var profile = GetProfile(parts[7].Trim());

                    // cos/sin are computed HERE, in float, once at load — not with fpMath.Sin at
                    // runtime. The provider then only ever multiplies by the stored constants, so
                    // two runs of the same CSV give bit-identical trajectories.
                    double rad = yawDeg * System.Math.PI / 180.0;
                    list.Add(new BridgeBox(
                        fp.FromFloat(cx), fp.FromFloat(cz),
                        fp.FromFloat(baseY), fp.FromFloat(topY),
                        fp.FromFloat(hx), fp.FromFloat(hz),
                        fp.FromDouble(System.Math.Cos(rad)), fp.FromDouble(System.Math.Sin(rad)),
                        profile));
                }
            }

            return list.Count > 0 ? list : null;
        }

        private static bool TryF(string s, out float v)
            => float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out v);
    }
}
