// gps_standalone_shell §1 / §D6 — the identity of the "punch it standalone" variant.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Stamps the PLAYLIFE shell's identity onto a build of the <c>iOS-Standalone</c> profile,
    /// and puts every field back afterwards.
    ///
    /// <para>
    /// WHY A PREPROCESSOR AND NOT PROFILE OVERRIDES. A Unity 6 build profile can carry
    /// per-profile PlayerSettings, but this project's profiles do not use them
    /// (<c>m_PlayerSettingsYaml.m_Settings: []</c> on all six), and turning them on for one
    /// profile means the six now disagree about where identity lives. This class keeps identity
    /// in ONE readable place, applied by the same mechanism <see cref="GolfinRedux.BuildEditor.BuildStampGenerator"/>
    /// already uses for the build number, and restored the same way — so an ordinary build
    /// leaves <c>ProjectSettings.asset</c> byte-identical and the next fastlane lane does not
    /// abort at <c>ensure_git_status_clean</c> blaming a file the pipeline dirtied.
    /// </para>
    ///
    /// <para>
    /// HOW IT KNOWS. NOT <c>#if GOLFIN_STANDALONE</c>: a build profile's scripting defines reach
    /// the PLAYER assemblies only, never the editor's own compilation, even while that profile is
    /// active — so an editor script asking the preprocessor question with an <c>#if</c> would
    /// always answer "no". It reads the ACTIVE PROFILE's <c>m_ScriptingDefines</c> instead (the
    /// same SerializedObject read <see cref="CIBuild"/> uses to assert GOLFIN_GPS), with an
    /// explicit override flag for batchmode, mirroring <c>DemoSceneProcessor.ForceDemoStrip</c>.
    /// </para>
    ///
    /// <para>
    /// RESTORE IS NOT OPTIONAL. Every field written here is a field the GAME ships with. A failed
    /// batchmode build that left <c>applicationIdentifier</c> on the PLAYLIFE bundle id would
    /// make the next "punch it" upload the wrong app to the wrong App Store record — so the
    /// restore runs from the postprocess hook AND from an <c>EditorApplication.delayCall</c>
    /// safety net AND from <see cref="RestoreNow"/>, which <see cref="CIBuild"/> calls before it
    /// exits the process (a delayCall never gets a frame in batchmode).
    /// </para>
    /// </summary>
    /// <para>
    /// LOCATED IN <c>Assets/Editor/</c>, beside <see cref="CIBuild"/>, and NOT in an
    /// <c>Assets/Editor/Build/</c> subfolder: <c>.gitignore</c> carries a blanket <c>[Bb]uild/</c>
    /// rule, so a file under any folder called Build is silently untracked — it would work on this
    /// machine and be absent from the repo, which for a build hook means the next machine builds
    /// the shell with the GAME's bundle id. Same trap the upload-guard file avoids.
    /// </para>
    public sealed class StandaloneBuildPreprocessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        const string Tag = "[StandaloneIdentity]";
        public const string Define = "GOLFIN_STANDALONE";

        // ── The identity (D1, read from App Store Connect 2026-09-03) ─────────────────
        //
        // The shell uploads to the EXISTING ASC app "GOLFIN GPS" — Apple ID 6737145432, same
        // team as the game (TCUV4A9VTJ), last TestFlight build 0.7.6 (12). It is NOT
        // com.wonderwall.playlife: that is the retired Flutter project's record.
        public const string BundleId    = "com.nextinnovation.golfingps";
        public const string ProductName = "GOLFIN GPS";
        /// <summary>Must exceed the record's last shipped 0.7.6; the game keeps its own 1.5.7.</summary>
        public const string Version     = "1.0.0";
        /// <summary>Claimed BESIDE the game's <c>golfin</c>, because both apps can be installed
        /// on one phone and two apps claiming one scheme is undefined behaviour on iOS.</summary>
        public const string UrlScheme   = "golfingps";

        const string IconPath = "Assets/Art/Standalone/S_StandaloneAppIcon.png";

        /// <summary>
        /// Batchmode override, set by <see cref="CIBuild.BuildIOSStandalone"/> around
        /// BuildPipeline.BuildPlayer. Same reason <c>DemoSceneProcessor.ForceDemoStrip</c> exists:
        /// the editor's active profile can differ from the profile being built.
        /// </summary>
        internal static bool ForceStandaloneIdentity;

        public int callbackOrder => 10;   // after BuildStampGenerator (0), which owns the build number

        // ── Pre-build snapshot ───────────────────────────────────────────────────────
        static bool _applied;
        static string _prevBundleId;
        static string _prevProductName;
        static string _prevVersion;
        static string[] _prevUrlSchemes;
        static Texture2D[] _prevIcons;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsStandaloneBuild(report)) return;

            _prevBundleId    = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            _prevProductName = PlayerSettings.productName;
            _prevVersion     = PlayerSettings.bundleVersion;
            _prevUrlSchemes  = PlayerSettings.iOS.iOSUrlSchemes;
            _prevIcons       = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            _applied         = true;

            EditorApplication.delayCall += RestoreSafetyNet;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.productName  = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.iOS.iOSUrlSchemes = WithScheme(_prevUrlSchemes, UrlScheme);
            ApplyIcon();

            Debug.Log($"{Tag} applied — bundleId={BundleId} productName=\"{ProductName}\" " +
                      $"version={Version} urlSchemes=[{string.Join(", ", PlayerSettings.iOS.iOSUrlSchemes)}]");
        }

        public void OnPostprocessBuild(BuildReport report) => RestoreNow();

        static void RestoreSafetyNet()
        {
            EditorApplication.delayCall -= RestoreSafetyNet;
            RestoreNow();
        }

        /// <summary>
        /// Put every stamped field back to its exact pre-build value. Idempotent, so the
        /// postprocess hook, the delayCall net and <see cref="CIBuild"/>'s explicit call can all
        /// fire and only the first does anything.
        /// </summary>
        public static void RestoreNow()
        {
            if (!_applied) return;
            _applied = false;

            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, _prevBundleId);
                PlayerSettings.productName  = _prevProductName;
                PlayerSettings.bundleVersion = _prevVersion;
                PlayerSettings.iOS.iOSUrlSchemes = _prevUrlSchemes;
                if (_prevIcons != null)
                    PlayerSettings.SetIcons(NamedBuildTarget.Unknown, _prevIcons, IconKind.Any);
                AssetDatabase.SaveAssets();
                Debug.Log($"{Tag} restored — bundleId={_prevBundleId} productName=\"{_prevProductName}\" " +
                          $"version={_prevVersion} (ProjectSettings.asset stays out of the diff).");
            }
            catch (Exception e)
            {
                // Never let bookkeeping change the outcome of a build — but this one is loud,
                // because a failed restore leaves the GAME pointing at the PLAYLIFE record.
                Debug.LogError($"{Tag} COULD NOT RESTORE identity ({e.GetType().Name}: {e.Message}). " +
                               $"Check ProjectSettings.asset before the next store build.");
            }
        }

        /// <summary>The scheme list with <paramref name="scheme"/> present exactly once.</summary>
        internal static string[] WithScheme(string[] existing, string scheme)
        {
            var list = new List<string>(existing ?? Array.Empty<string>());
            if (!list.Any(s => string.Equals(s, scheme, StringComparison.OrdinalIgnoreCase)))
                list.Add(scheme);
            return list.ToArray();
        }

        /// <summary>
        /// Placeholder branding (D6): a baked wordmark from <c>Docs/Scripts/make_standalone_icon.py</c>
        /// until Ken supplies the real PLAYLIFE assets — a backlog row, not this task. Missing art
        /// is a WARNING, not a failure: an icon-less shell still installs and still tests, and a
        /// build that died on a placeholder would be a gate nobody wants.
        /// </summary>
        static void ApplyIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning($"{Tag} no icon at {IconPath} — the shell will build with the GAME's icon. " +
                                 $"Bake one with: python3 Docs/Scripts/make_standalone_icon.py");
                return;
            }

            // NamedBuildTarget.Unknown = the project default icon, from which Unity generates the
            // whole iOS icon set (app_identity/K15 — no per-platform overrides needed).
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            Debug.Log($"{Tag} icon → {IconPath}");
        }

        // ── Detection ────────────────────────────────────────────────────────────────

        /// <summary>
        /// True when THIS build is the standalone variant: the batchmode override, or the active
        /// build profile carrying <see cref="Define"/>.
        /// </summary>
        static bool IsStandaloneBuild(BuildReport report)
        {
            if (report != null && report.summary.platform != BuildTarget.iOS) return false;
            return IsStandaloneIdentityBuild();
        }

        /// <summary>
        /// Platform-agnostic form of the same question, for the other build-time hooks that have
        /// to know which VARIANT is being produced — <c>BuildStampGenerator</c> picks the shell's
        /// own upload-regression guard file with it, since ASC's uniqueness rule is per record.
        /// </summary>
        public static bool IsStandaloneIdentityBuild()
        {
            if (ForceStandaloneIdentity) return true;
            return ProfileDefines(BuildProfile.GetActiveBuildProfile())
                   .Any(d => string.Equals(d, Define, StringComparison.Ordinal));
        }

        /// <summary>
        /// A profile's scripting defines. Read through SerializedObject because
        /// <c>BuildProfile.scriptingDefines</c> is not public API in 6000.3 — the same read
        /// <see cref="CIBuild"/> uses, and <c>m_ScriptingDefines</c> is what the .asset stores.
        /// </summary>
        internal static IEnumerable<string> ProfileDefines(BuildProfile profile)
        {
            if (profile == null) yield break;

            var so = new SerializedObject(profile);
            var defines = so.FindProperty("m_ScriptingDefines");
            if (defines == null || !defines.isArray) yield break;

            for (int i = 0; i < defines.arraySize; i++)
                yield return defines.GetArrayElementAtIndex(i).stringValue;
        }
    }
}
