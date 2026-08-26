// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentBuildNumber
//
// THE min_build QUESTION, RESOLVED (SPEC §4).
//
// The client has to send an integer build number so the server can withhold
// rows this build cannot render (I4). Unity exposes NO cross-platform runtime
// API for it: Application.version is the MARKETING version
// (CFBundleShortVersionString), never the build number, and the device has no
// git to recompute one.
//
// The two candidate sources on disk disagreed:
//
//   ProjectSettings.asset → buildNumber: iPhone   2113
//   Resources/Data/build_stamp.txt                v1.5.7 (2297) 02c1678+da58 · 08-26 06:56
//
// They disagree because BuildStampGenerator deliberately RESTORES the
// PlayerSettings fields after every build (OnPostprocessBuild + a delayCall
// safety net) so ProjectSettings.asset generates no merge noise across
// machines. The 2113 sitting in ProjectSettings is therefore a stale working-
// copy leftover from whenever those fields were last committed — it is NOT the
// number in any shipped binary, and it is not even readable at runtime.
//
// The number the binary actually carries is `git rev-list --count HEAD`,
// computed in OnPreprocessBuild and written to THREE places at once:
// PlayerSettings.iOS.buildNumber (→ CFBundleVersion), Android
// bundleVersionCode, and the baked Resources/Data/build_stamp.txt. The stamp is
// the only one of the three a runtime can read, and it is baked UNGATED on
// every build — the GOLFIN_TESTBUILD gate is on BuildStamp.cs, the on-screen
// overlay, not on the generator that writes the file.
//
// So: build_stamp.txt is authoritative. It is also already trusted for exactly
// this by GolfinRedux.UI.BuildInfo.AppVersion, which renders the parenthesised
// build number in Settings ▸ About on every profile.
//
// WHY NOT A SECOND FILE. SPEC §4 suggested baking a fresh
// Resources/Data/build_number.txt. That would add a THIRD source that can
// disagree with the other two, which is the precise failure this comment
// exists to close. Reading the artifact the pipeline already bakes cannot
// drift from the binary. Flagged as a deviation in the report.
//
// The format contract ("(1234)") is shared with AppVersion and pinned by
// ContentBuildNumberTests, which parses the REAL bundled stamp — so a change to
// the stamp format breaks a test rather than silently downgrading every client
// to build 0.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Golfin.Content
{
    /// <summary>
    /// The running build number, for the <c>build=</c> query parameter.
    /// <b>Every failure resolves to 0</b>, which is the safe end: the server then serves only rows
    /// whose <c>min_build</c> is 0, i.e. rows every build can render. An over-estimate would do the
    /// opposite and hand this build content it cannot draw.
    /// </summary>
    public static class ContentBuildNumber
    {
        private const string Tag = "[Content]";

        /// <summary><c>Resources.Load</c> key — no folder prefix, no extension.</summary>
        public const string ResourcePath = "Data/build_stamp";

        /// <summary>"(1234)" — the first parenthesised integer in the stamp string.</summary>
        private static readonly Regex BuildNumberPattern =
            new Regex(@"\((\d+)\)", RegexOptions.CultureInvariant);

        private static bool _resolved;
        private static int _current;

        /// <summary>
        /// The running build number, or 0 when it cannot be resolved (no stamp, or an editor
        /// fallback stamp like <c>v1.5.7 (editor) · …</c> that carries no number).
        /// Memoised — the value cannot change within a session.
        /// </summary>
        public static int Current
        {
            get
            {
                if (_resolved) return _current;
                _current = Resolve();
                _resolved = true;
                return _current;
            }
        }

        /// <summary>Drop the memoised value so the next read re-parses. Tests only.</summary>
        public static void ResetForTest() => _resolved = false;

        /// <summary>Pin a value without touching Resources (EditMode tests).</summary>
        public static void ConfigureForTest(int build)
        {
            _current = Mathf.Max(0, build);
            _resolved = true;
        }

        private static int Resolve()
        {
            var stamp = Resources.Load<TextAsset>(ResourcePath);
            if (stamp == null || string.IsNullOrWhiteSpace(stamp.text))
            {
                Debug.LogWarning(
                    $"{Tag} No bundled '{ResourcePath}'; sending build=0, so only rows every build " +
                    $"can render will be served.");
                return 0;
            }

            int parsed = Parse(stamp.text);
            if (parsed <= 0)
            {
                Debug.LogWarning(
                    $"{Tag} Build stamp '{stamp.text.Trim()}' carries no build number; sending build=0.");
            }
            return parsed;
        }

        /// <summary>
        /// Pure parser — no Unity, no IO, so it is directly unit-testable.
        /// Returns the first parenthesised integer, or 0 for anything else.
        /// </summary>
        public static int Parse(string? stamp)
        {
            if (string.IsNullOrWhiteSpace(stamp)) return 0;

            var match = BuildNumberPattern.Match(stamp!);
            if (!match.Success) return 0;

            return int.TryParse(match.Groups[1].Value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int n) && n > 0
                ? n
                : 0;
        }
    }
}
