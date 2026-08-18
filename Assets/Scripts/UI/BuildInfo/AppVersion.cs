using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GolfinRedux.UI.BuildInfo
{
    /// <summary>
    /// Runtime accessor for the FULL app version — marketing version plus the
    /// build number in parentheses, e.g. "0.1.0 (2201)".
    ///
    /// Why this exists: <see cref="Application.version"/> only returns the marketing
    /// version (PlayerSettings.bundleVersion / CFBundleShortVersionString). The build
    /// number (CFBundleVersion / Android bundleVersionCode) is the value TestFlight and
    /// App Store Connect identify a binary by, and Unity exposes no cross-platform
    /// runtime API for it — the device also has no git, so it cannot be recomputed.
    ///
    /// It is therefore read back from the same baked artifact the build stamp uses:
    /// Resources/Data/build_stamp.txt, written by BuildStampGenerator.OnPreprocessBuild
    /// (ungated — every build bakes it) in the documented format
    ///   "v{bundleVersion} ({buildNumber}) {shortSha}[+{diffHash}] · {MM-dd HH:mm}"
    /// Only the parenthesised build number is taken from it; the marketing half always
    /// comes from Application.version so the two can never disagree with the binary.
    ///
    /// If the stamp is missing or carries no numeric build (e.g. the editor fallback
    /// "v0.1.0 (editor) · …" when git is unavailable), <see cref="Full"/> degrades to
    /// the plain marketing version rather than printing a bogus number.
    /// </summary>
    public static class AppVersion
    {
        const string StampResourcePath = "Data/build_stamp"; // Resources.Load key (no extension)

        // "(1234)" — the first parenthesised integer in the stamp string.
        static readonly Regex BuildNumberPattern = new Regex(@"\((\d+)\)", RegexOptions.CultureInvariant);

        static bool _resolved;
        static string _buildNumber;

        /// <summary>Marketing version only, e.g. "0.1.0".</summary>
        public static string Marketing => Application.version;

        /// <summary>Build number as text, e.g. "2201". Null when unavailable.</summary>
        public static string BuildNumber
        {
            get
            {
                if (!_resolved)
                {
                    _buildNumber = ResolveBuildNumber();
                    _resolved = true;
                }
                return _buildNumber;
            }
        }

        /// <summary>
        /// Full version for display, e.g. "0.1.0 (2201)".
        /// Falls back to "0.1.0" when the build number cannot be resolved.
        /// </summary>
        public static string Full
        {
            get
            {
                string build = BuildNumber;
                return string.IsNullOrEmpty(build)
                    ? Marketing
                    : $"{Marketing} ({build})";
            }
        }

        static string ResolveBuildNumber()
        {
            var stamp = Resources.Load<TextAsset>(StampResourcePath);
            if (stamp == null || string.IsNullOrWhiteSpace(stamp.text))
                return null;

            var match = BuildNumberPattern.Match(stamp.text);
            if (!match.Success)
                return null;

            // Normalise (drops leading zeros / stray formatting) and reject nonsense.
            return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0
                ? n.ToString(CultureInfo.InvariantCulture)
                : null;
        }
    }
}
