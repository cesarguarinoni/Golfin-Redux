// gps_standalone_shell §1 / §D6 — the identity of the "punch it standalone" variant.
using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// R1 (round 2) — Cesar's icon: green gradient, white map pin with a golf ball on a tee.
        /// It replaced a generated placeholder after he installed build 2635 and saw it.
        ///
        /// <para>ALREADY OPAQUE, and left that way. App Store Connect rejects an alpha channel in
        /// the 1024 marketing icon (ITMS-90717), and iOS masks the corners itself — the rounded
        /// shape baked into this file is the artwork's own, and re-rounding or re-flattening it
        /// here would fight the mask rather than help it.</para>
        /// </summary>
        const string IconPath = "Assets/Art/Standalone/AppIcon_GolfinGps_1024_opaque.png";

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
            // Resources first, and OUTSIDE the _applied guard: the folders can be stashed by a
            // build that failed before the identity was ever applied, and leaving them stashed is
            // the more damaging of the two states.
            RestoreGolfResources();

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
        /// The app icon. Missing art is a WARNING, not a failure: a shell built with the wrong
        /// icon still installs and still tests, and a build that died over branding would be a gate
        /// nobody wants — but it IS worth shouting about, because the icon is the only thing that
        /// tells the two apps apart on the springboard.
        /// </summary>
        static void ApplyIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning($"{Tag} no icon at {IconPath} — the shell will build with the GAME's icon, " +
                                 $"which on a springboard next to it is indistinguishable from the game.");
                return;
            }

            // NamedBuildTarget.Unknown = the project default icon, from which Unity generates the
            // whole iOS icon set (app_identity/K15 — no per-platform overrides needed).
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            Debug.Log($"{Tag} icon → {IconPath}");
        }

        // ── R2 · The size: Resources/ ships whole ────────────────────────────────────
        //
        // Build 2635 was a 427 MB .ipa, and almost none of it was the shell. Anything under an
        // `Assets/Resources/` folder is included in EVERY build whether or not a scene references
        // it — that is what Resources means — so the standalone, whose scene list is ShellScene
        // alone, still shipped all 18 `HoleData/*/heightmap.bytes` (16.8 MB each) and their
        // `zones.json`. A gate on the scene list cannot fix that; the files have to leave the
        // folder.
        //
        // SO THEY LEAVE IT, FOR THE DURATION OF THE BUILD. AssetDatabase.MoveAsset to a stash
        // folder still inside Assets/ — which preserves GUIDs and import artifacts, so this is a
        // rename, not a 545 MB re-import — and back afterwards. Outside a Resources folder an
        // asset is included only if something references it, and nothing in ShellScene does.
        //
        // WHICH FOLDERS: enumerated against every Resources.Load / LoadAll call site reachable
        // from the GPS surface + auth + the top bar, not guessed. The enumeration is in
        // IMPLEMENTER_REPORT.md; the two that it saved from this list are worth naming here:
        //   • Characters — `GpsAvatarScreenController.BindCharacterFigure` loads
        //     `Characters/Homescreen/{name}` for the avatar figure. A PLAYLIFE screen reaching
        //     into golf art; moving it would have blanked the Avatar screen.
        //   • Portraits  — added back after build 2637 SHIPPED BROKEN. No GPS screen reads it
        //     directly, which is exactly why the first enumeration missed it: the call site builds
        //     its path from a const (`ThumbnailResourcesPath = "Portraits/Thumbnails"`) plus a
        //     variable, so a grep for LITERAL Resources.Load paths cannot see it. And what it
        //     feeds is a GATE, which is what made the failure total and silent:
        //         CharacterDatabaseCSV:348   renderable = portraitSprite != null
        //         CharacterDatabaseCSV:421   GetAvailableCharacters() = Where(isActive && renderable)
        //         CharacterManager:86        ownedCharacters seeded from GetAvailableCharacters()
        //     With Portraits stashed EVERY character is unrenderable, the roster seeds EMPTY, no
        //     selected id resolves, and the GPS Avatar screen falls back to the placeholder —
        //     which is what Cesar saw on build 2637. R2's lesson was "enumerate the call sites";
        //     the correction is that enumerating LITERAL paths is not enumerating call sites.
        //     Pinned by StandaloneResourceStashTests.
        //   • Data / UI  — texts, the content version, the build stamp, sfx, and the app-wide
        //     TapFeedback config+prefab. Shared by everything, and 700 KB between them.

        const string StashRoot = "Assets/_StandaloneResourceStash";

        /// <summary>
        /// Written when the folders are moved, deleted when they are put back. Its CONTENT is the
        /// list of folders that moved, so a repair does not have to assume this constant never
        /// changed between the build that aborted and the editor session that repairs it.
        ///
        /// <para>A dot-file, so Unity's importer ignores it — it can sit inside <c>Resources/</c>
        /// without ever being imported or shipped. That is also why it is not an EditorPrefs key:
        /// the mess is on disk, so the record of it belongs on disk, where a human who deleted
        /// the Library folder can still find it.</para>
        /// </summary>
        const string SentinelPath = "Assets/Resources/.standalone_moved";

        /// <summary>
        /// Golf-only <c>Assets/Resources</c> subfolders — nothing the shell's surface can load.
        /// Ordered biggest-first purely so the log reads usefully.
        /// </summary>
        static readonly string[] GolfOnlyResourceFolders =
        {
            "HoleData",          // 388 MB — 18 heightmaps + zones.json + green.json. The whole story.
            "Clubs",             // 114 MB — club art, ClubDatabaseCSV
            "Balls",             //  16 MB — BallDatabaseCSV, the shot-UI ball widgets
            "Sprites",           // 5.4 MB — Sprites/Shops, the stamina shop rows
            "HoleImages",        // 3.8 MB — hole cards, the result modal
            "Items",             // 2.6 MB — ItemDatabaseCSV
            "Art",               // 1.7 MB — Art/Gacha banners, dots, tickets
            "TournamentImages",  // 1.0 MB — tournament selection + signup
            "Bags",              // 852 KB — BagDatabaseCSV, ClubManager
            "FX",                // 656 KB — the water-splash prefab and its materials
            "Prefabs",           // 592 KB — Prefabs/Gacha + Prefabs/Shop cards
            "Rarities",          // 332 KB — rarity frames: roster, inventory, rankings, shop
        };

        /// <summary>
        /// Move the golf-only Resources folders aside. Returns the folders actually moved.
        ///
        /// <para>Called from <see cref="CIBuild.BuildIOSStandalone"/> BEFORE
        /// <c>BuildPipeline.BuildPlayer</c> rather than from <see cref="OnPreprocessBuild"/>: the
        /// preprocess hook runs INSIDE the build, by which point the set of included assets is
        /// already decided, and an <c>AssetDatabase.Refresh</c> there would be reentrant.</para>
        /// </summary>
        public static string[] MoveGolfResourcesOut()
        {
            var moved = new List<string>();

            if (!AssetDatabase.IsValidFolder(StashRoot))
                AssetDatabase.CreateFolder("Assets", StashRoot.Substring("Assets/".Length));

            foreach (var folder in GolfOnlyResourceFolders)
            {
                string from = "Assets/Resources/" + folder;
                string to   = StashRoot + "/" + folder;

                if (!AssetDatabase.IsValidFolder(from))
                {
                    // Already stashed (a previous aborted build) or genuinely absent. Either way
                    // there is nothing to move, and it is the sentinel — not this loop — that
                    // decides what gets put back.
                    Debug.Log($"{Tag} Resources/{folder} not present — skipping.");
                    continue;
                }

                string error = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(error))
                {
                    // A folder that will not move must abort the build, loudly: continuing would
                    // ship the 427 MB build again while the log said the diet ran.
                    RestoreGolfResources();
                    throw new BuildFailedException(
                        $"{Tag} could not move {from} → {to}: {error}. Resources restored; build aborted.");
                }

                moved.Add(folder);
            }

            WriteSentinel(moved);
            AssetDatabase.Refresh();

            Debug.Log($"{Tag} moved {moved.Count} golf-only Resources folder(s) out for this build: " +
                      string.Join(", ", moved));
            return moved.ToArray();
        }

        /// <summary>
        /// Put back whatever the sentinel says was moved. Idempotent, and safe to call when
        /// nothing was moved — which is why every build entry point can call it unconditionally.
        ///
        /// <para>SENTINEL-DRIVEN, not constant-driven: a build that aborted last week moved the
        /// list as it was last week. Repairing from today's constant would silently leave a
        /// folder stashed — and a stashed <c>HoleData</c> means the next GAME build ships without
        /// its holes, which is the worst outcome this whole mechanism could produce.</para>
        /// </summary>
        public static void RestoreGolfResources()
        {
            string[] moved = ReadSentinel();
            if (moved == null || moved.Length == 0)
            {
                // No sentinel: either nothing was moved, or someone deleted it. Sweep the stash
                // anyway so a folder can never be orphaned by a missing note.
                moved = AssetDatabase.IsValidFolder(StashRoot)
                    ? Directory.GetDirectories(Path.GetFullPath(StashRoot))
                               .Select(Path.GetFileName).ToArray()
                    : Array.Empty<string>();
                if (moved.Length > 0)
                    Debug.LogWarning($"{Tag} no sentinel, but the stash is not empty — restoring by " +
                                     $"sweep: {string.Join(", ", moved)}");
            }

            int restored = 0;
            foreach (var folder in moved)
            {
                string from = StashRoot + "/" + folder;
                string to   = "Assets/Resources/" + folder;
                if (!AssetDatabase.IsValidFolder(from)) continue;

                string error = AssetDatabase.MoveAsset(from, to);
                if (string.IsNullOrEmpty(error)) { restored++; continue; }

                // Loud, and NOT swallowed into a warning: the project is now in a state where a
                // game build would ship without its holes.
                Debug.LogError($"{Tag} COULD NOT RESTORE {from} → {to}: {error}. " +
                               $"Move it back by hand BEFORE the next build.");
            }

            DeleteSentinel();
            if (AssetDatabase.IsValidFolder(StashRoot) &&
                Directory.GetFileSystemEntries(Path.GetFullPath(StashRoot))
                         .All(p => p.EndsWith(".meta", StringComparison.Ordinal)))
                AssetDatabase.DeleteAsset(StashRoot);

            if (restored > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"{Tag} restored {restored} Resources folder(s).");
            }
        }

        /// <summary>
        /// Repair on editor load. An aborted batchmode build (a crash, a kill, an Exit before the
        /// finally) leaves the folders stashed, and the very next GAME build would then ship
        /// without them — silently, because a missing Resources folder is not a build error. The
        /// editor coming up is the last chance to notice, so it notices every time.
        /// </summary>
        [InitializeOnLoadMethod]
        static void RepairStashOnLoad()
        {
            if (!File.Exists(Path.GetFullPath(SentinelPath)) && !AssetDatabase.IsValidFolder(StashRoot))
                return;

            Debug.LogWarning($"{Tag} a previous standalone build left Resources folders stashed — " +
                             $"restoring them now. (A build that aborts between the move and the " +
                             $"finally lands here.)");
            RestoreGolfResources();
        }

        static void WriteSentinel(List<string> moved)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(SentinelPath)) ?? ".");
                File.WriteAllLines(Path.GetFullPath(SentinelPath), moved);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} could not write the stash sentinel ({e.Message}). " +
                               $"RestoreGolfResources will fall back to sweeping {StashRoot}.");
            }
        }

        static string[] ReadSentinel()
        {
            try
            {
                string full = Path.GetFullPath(SentinelPath);
                if (!File.Exists(full)) return null;
                return File.ReadAllLines(full)
                           .Select(l => l.Trim())
                           .Where(l => l.Length > 0)
                           .ToArray();
            }
            catch { return null; }
        }

        static void DeleteSentinel()
        {
            try
            {
                string full = Path.GetFullPath(SentinelPath);
                if (File.Exists(full)) File.Delete(full);
            }
            catch (Exception e) { Debug.LogWarning($"{Tag} could not delete the sentinel: {e.Message}"); }
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
