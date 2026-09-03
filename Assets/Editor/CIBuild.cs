using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Batchmode entry point for the unattended TestFlight pipeline
    /// (fastlane_testflight_pipeline §1). Invoked headlessly by Tools/unity-build-ios.sh
    /// via -executeMethod, which is in turn invoked by fastlane's `ios testflight_build` lane.
    ///
    /// EXIT CODE IS THE WHOLE POINT
    ///   Everything downstream of this method — xcodebuild archive, App Store Connect upload —
    ///   is fed by whatever sits in Builds/iOS-Full. If a batchmode build fails and this
    ///   process still exits 0, fastlane happily archives and uploads the PREVIOUS build's
    ///   Xcode project. That is the classic way to ship a stale binary while the log says
    ///   "success". So: every failure path here calls EditorApplication.Exit(1), including
    ///   the ones that arrive as exceptions (BuildStampGenerator's upload-regression guard
    ///   throws BuildFailedException, and Unity does not reliably turn an -executeMethod
    ///   exception into a non-zero process exit code on its own).
    ///
    /// PROFILE ACTIVATION
    ///   Activated through the BuildProfile API, NOT the -activeBuildProfile CLI flag. That
    ///   flag has a Unity 6 batchmode bug — it exits batchmode when the profile is already
    ///   active, and it wants a project-relative path. Same reasoning, and the same call, as
    ///   GolfinRedux.DemoEditor.DemoBuild (DEMO_BUILD_PLAN.md §3.1).
    ///
    /// BuildOptions.None, deliberately
    ///   NOT BuildOptions.Development. BuildStampGenerator.GuardApplies() skips the
    ///   upload-regression refusal for development builds, so a Development build here would
    ///   silently disarm the guard this pipeline depends on.
    /// </summary>
    public static class CIBuild
    {
        const string Tag = "[CIBuild]";
        const string ProfilePath = "Assets/Settings/Build Profiles/iOS-Full.asset";
        const string OutputPath = "Builds/iOS-Full";

        // perf_baseline Phase 0 (PERF_OPTIMIZATION_PLAN §5). Profiling build, never uploaded.
        const string DevProfilePath = "Assets/Settings/Build Profiles/Dev-iOS.asset";
        const string DevOutputPath = "Builds/iOS-Dev";

        // punch_it_gps_variants — "punch it GPS". Identical to iOS-Full except for the
        // GOLFIN_GPS scripting define, and it writes to the SAME OutputPath: the Fastfile's
        // build_app archives Builds/iOS-Full whichever variant Unity just produced.
        const string GpsProfilePath = "Assets/Settings/Build Profiles/iOS-Full-GPS.asset";
        const string GpsDefine = "GOLFIN_GPS";

        // gps_standalone_shell — "punch it standalone". The PLAYLIFE thin shell: the same
        // codebase and the same OutputPath, with a ShellScene-only scene list and the
        // GOLFIN_STANDALONE define on top of GOLFIN_GPS. StandaloneBuildPreprocessor gives it
        // its own bundle id / name / version / icon during the build and takes them back after.
        const string StandaloneProfilePath = "Assets/Settings/Build Profiles/iOS-Standalone.asset";
        const string StandaloneDefine = "GOLFIN_STANDALONE";

        /// <summary>
        /// -executeMethod Golfin.EditorTools.CIBuild.BuildIOS
        /// Produces Builds/iOS-Full/Unity-iPhone.xcodeproj. Exits 1 on any failure.
        /// </summary>
        public static void BuildIOS()
        {
            // R2 safety net. A standalone build that died between the stash and its finally leaves
            // golf Resources moved aside, and THIS build — the game — would then ship without its
            // holes. A missing Resources folder is not a build error, so nothing else would say a
            // word. Free when there is nothing to repair.
            StandaloneBuildPreprocessor.RestoreGolfResources();

            // BuildStampGenerator writes the git-derived build number into PlayerSettings during
            // OnPreprocessBuild and restores the pre-build values in OnPostprocessBuild — which
            // fires on SUCCESS only. Its failure safety net is an EditorApplication.delayCall,
            // and a delayCall never gets a frame in batchmode: Exit(1) ends the process first.
            // So a FAILED batchmode build would leave ProjectSettings.asset dirty, and the very
            // next `fastlane ios testflight_build` would abort at ensure_git_status_clean blaming
            // a file the pipeline itself dirtied. Snapshot here, restore before ANY exit.
            // Idempotent with BuildStampGenerator's own restore (identical captured values), and
            // it touches nothing but those two fields.
            var prevIosBuildNumber = PlayerSettings.iOS.buildNumber;
            var prevAndroidVersionCode = PlayerSettings.Android.bundleVersionCode;

            string error;
            try
            {
                error = BuildIOSCore();
            }
            catch (Exception e)
            {
                // BuildFailedException (the upload guard, any IPreprocessBuildWithReport that
                // throws) lands here, as does anything unexpected. Never let it reach Unity's
                // own handler, which may still quit 0.
                error = $"unhandled exception during build: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
            }

            // MUST run before Fail() — Fail() calls EditorApplication.Exit, which ends the
            // process immediately, so a `finally` around the call would never execute.
            RestoreBuildNumbers(prevIosBuildNumber, prevAndroidVersionCode);

            if (error != null) Fail(error);
        }

        /// <summary>
        /// -executeMethod Golfin.EditorTools.CIBuild.BuildIOSDev
        /// Produces Builds/iOS-Dev/Unity-iPhone.xcodeproj from the Dev-iOS profile:
        /// Development Build ON, Autoconnect Profiler ON, Deep Profiling OFF. Exits 1 on failure.
        ///
        /// FOR PROFILING ONLY — NEVER UPLOAD THE OUTPUT.
        ///   BuildStampGenerator.GuardApplies() skips the upload-regression refusal for
        ///   development builds, so the guard BuildIOS() depends on is disarmed here by design.
        ///   That is safe precisely because nothing downstream archives Builds/iOS-Dev; fastlane
        ///   reads Builds/iOS-Full. Keep it that way.
        ///
        ///   Same PlayerSettings snapshot/restore as BuildIOS() — a failed batchmode build must
        ///   not leave ProjectSettings.asset dirty and strand the next testflight lane at
        ///   ensure_git_status_clean.
        /// </summary>
        public static void BuildIOSDev()
        {
            StandaloneBuildPreprocessor.RestoreGolfResources();   // see BuildIOS

            var prevIosBuildNumber = PlayerSettings.iOS.buildNumber;
            var prevAndroidVersionCode = PlayerSettings.Android.bundleVersionCode;

            string error;
            try
            {
                // Development | ConnectWithProfiler are also carried by the Dev-iOS profile's
                // iOSPlatformSettings; passing them explicitly means the build is a profiling
                // build even if someone flips the profile back.
                error = BuildIOSCore(DevProfilePath, DevOutputPath,
                                     BuildOptions.Development | BuildOptions.ConnectWithProfiler);
            }
            catch (Exception e)
            {
                error = $"unhandled exception during build: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
            }

            RestoreBuildNumbers(prevIosBuildNumber, prevAndroidVersionCode);

            if (error != null) Fail(error);
        }

        /// <summary>
        /// -executeMethod Golfin.EditorTools.CIBuild.BuildIOSGps
        /// The "punch it GPS" variant: same output, same options, same guard — only the profile
        /// differs, and with it the GOLFIN_GPS define that GpsGate reads.
        ///
        /// BuildOptions.None, NOT Development: the upload-regression guard must stay armed for
        /// GPS uploads exactly as it is for ordinary ones (BuildStampGenerator.GuardApplies skips
        /// the refusal for development builds).
        ///
        /// Same PlayerSettings snapshot/restore as BuildIOS() — a failed batchmode build must not
        /// leave ProjectSettings.asset dirty and strand the next lane at ensure_git_status_clean.
        /// </summary>
        public static void BuildIOSGps()
        {
            StandaloneBuildPreprocessor.RestoreGolfResources();   // see BuildIOS

            var prevIosBuildNumber = PlayerSettings.iOS.buildNumber;
            var prevAndroidVersionCode = PlayerSettings.Android.bundleVersionCode;

            string error;
            try
            {
                // Assert the define BEFORE building. A GPS build whose profile silently lost the
                // define compiles, archives and uploads as an ordinary build — indistinguishable
                // from "punch it" output except by opening the app. That is the stale-binary class
                // of failure this pipeline exists to make impossible, so it fails loudly instead.
                error = AssertGpsDefine(GpsProfilePath) ??
                        BuildIOSCore(GpsProfilePath, OutputPath, BuildOptions.None);
            }
            catch (Exception e)
            {
                error = $"unhandled exception during build: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
            }

            RestoreBuildNumbers(prevIosBuildNumber, prevAndroidVersionCode);

            if (error != null) Fail(error);
        }

        /// <summary>
        /// -executeMethod Golfin.EditorTools.CIBuild.BuildIOSStandalone
        /// The "punch it standalone" variant — PLAYLIFE as a thin shell. Same output path, same
        /// options, same guards as the other two; what differs is the profile, and with it the
        /// ShellScene-only scene list and the GOLFIN_STANDALONE define StandaloneGate reads.
        ///
        /// BOTH defines are asserted, for the reason the GPS lane asserts one: a standalone build
        /// whose profile silently lost GOLFIN_STANDALONE compiles, archives and uploads as an
        /// ORDINARY GPS build — under the PLAYLIFE bundle id, to the PLAYLIFE App Store record,
        /// carrying the whole golf game. That is worse than the stale-binary failure this
        /// pipeline exists to prevent, so it fails loudly instead.
        ///
        /// ForceStandaloneIdentity is set around the build because a build profile's scripting
        /// defines never reach the EDITOR's assemblies, so the preprocessor cannot answer
        /// "is this the standalone?" from an #if — see StandaloneBuildPreprocessor.
        /// </summary>
        public static void BuildIOSStandalone()
        {
            // Repair BEFORE the stash below, never after: BuildIOSCore runs inside the stashed
            // window, so a repair placed there would un-stash the folders this build just moved
            // and quietly ship the 427 MB binary again while the log claimed the diet had run.
            StandaloneBuildPreprocessor.RestoreGolfResources();

            var prevIosBuildNumber = PlayerSettings.iOS.buildNumber;
            var prevAndroidVersionCode = PlayerSettings.Android.bundleVersionCode;

            string error;
            try
            {
                StandaloneBuildPreprocessor.ForceStandaloneIdentity = true;

                error = AssertProfileDefine(StandaloneProfilePath, GpsDefine) ??
                        AssertProfileDefine(StandaloneProfilePath, StandaloneDefine);

                if (error == null)
                {
                    // R2 — Resources/ ships whole, so the golf-only subfolders leave the tree for
                    // the duration of this build. INSIDE the try, with the restore in the finally:
                    // every exit from here, including a BuildFailedException from a preprocessor
                    // and an unhandled throw, must put 545 MB of tracked assets back.
                    StandaloneBuildPreprocessor.MoveGolfResourcesOut();

                    // R4 — the refused game screens are still IN ShellScene, dragging their art
                    // in with them. StandaloneSceneProcessor destroys them in the in-memory copy
                    // during this build; the flag is how it knows the build is the standalone,
                    // since profile defines never reach editor assemblies.
                    StandaloneSceneProcessor.ForceStandaloneStrip = true;

                    error = BuildIOSCore(StandaloneProfilePath, OutputPath, BuildOptions.None);
                }
            }
            catch (Exception e)
            {
                error = $"unhandled exception during build: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
            }
            finally
            {
                StandaloneBuildPreprocessor.ForceStandaloneIdentity = false;
                StandaloneSceneProcessor.ForceStandaloneStrip = false;
            }

            // MUST run before Fail(): Fail() exits the process, and the preprocessor's own
            // delayCall safety net never gets a frame in batchmode. Leaving the PLAYLIFE bundle
            // id on disk would point the next "punch it" upload at the wrong App Store record —
            // and leaving the golf Resources stashed would make the next GAME build ship without
            // its holes. RestoreNow puts both back and is idempotent.
            StandaloneBuildPreprocessor.RestoreNow();
            RestoreBuildNumbers(prevIosBuildNumber, prevAndroidVersionCode);

            if (error != null) Fail(error);
        }

        /// <summary>
        /// Null when the profile at <paramref name="profilePath"/> carries GOLFIN_GPS; otherwise
        /// the failure message. Read through SerializedObject rather than a typed property:
        /// BuildProfile.scriptingDefines is not public API in 6000.3, and m_ScriptingDefines is
        /// what the .asset actually stores (see iOS-Demo.asset, which carries GOLFIN_DEMO the
        /// same way).
        /// </summary>
        static string AssertGpsDefine(string profilePath) => AssertProfileDefine(profilePath, GpsDefine);

        /// <summary>Generalised for the standalone lane, which has two defines to assert.</summary>
        static string AssertProfileDefine(string profilePath, string define)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null) return $"build profile not found: {profilePath}";

            var so = new SerializedObject(profile);
            var defines = so.FindProperty("m_ScriptingDefines");
            if (defines == null || !defines.isArray)
                return $"{profilePath} exposes no m_ScriptingDefines array — cannot verify {define}.";

            for (int i = 0; i < defines.arraySize; i++)
            {
                if (defines.GetArrayElementAtIndex(i).stringValue != define) continue;
                Debug.Log($"{Tag} variant define {define} present on {profile.name}.");
                return null;
            }

            return $"{profilePath} does NOT define {define} — refusing to build a variant that " +
                   $"would ship with that surface gated OFF and be indistinguishable from an " +
                   $"ordinary build.";
        }

        static void RestoreBuildNumbers(string iosBuildNumber, int androidVersionCode)
        {
            try
            {
                if (PlayerSettings.iOS.buildNumber != iosBuildNumber ||
                    PlayerSettings.Android.bundleVersionCode != androidVersionCode)
                {
                    PlayerSettings.iOS.buildNumber = iosBuildNumber;
                    PlayerSettings.Android.bundleVersionCode = androidVersionCode;
                    Debug.Log($"{Tag} restored PlayerSettings buildNumber → iOS={iosBuildNumber} " +
                              $"Android={androidVersionCode} (keeps ProjectSettings.asset out of the diff).");
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                // Never let bookkeeping change the outcome of the build.
                Debug.LogWarning($"{Tag} could not restore PlayerSettings buildNumber: {e.Message}");
            }
        }

        /// <summary>Returns null on success, or the failure message. Never exits — the caller
        /// restores PlayerSettings first, then exits.</summary>
        static string BuildIOSCore() => BuildIOSCore(ProfilePath, OutputPath, BuildOptions.None);

        static string BuildIOSCore(string profilePath, string outputPath, BuildOptions buildOptions)
        {
            var treeError = ValidateTreeBake();
            if (treeError != null) return treeError;

            // content_two_way §5 — a REPORT, not a gate. Deliberately has no failure path and no
            // -skip flag: data published ahead of its art is a legitimate state that §4 makes safe,
            // and a build that fails for it is a validator somebody switches off. It writes
            // Docs/Reports/content_art_<build>.txt so the archive carries the list of what it
            // withholds. Wrapped because a REPORT must never be the reason a build dies.
            try
            {
                ContentArtValidator.RunAndReport();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} catalog-art report failed to run ({e.GetType().Name}: " +
                                 $"{e.Message}) — continuing; it is a report, not a gate.");
            }

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null)
                return $"build profile not found: {profilePath}";

            BuildProfile.SetActiveBuildProfile(profile);
            Debug.Log($"{Tag} active build profile → {profile.name}");

            var full = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(full);
            Debug.Log($"{Tag} output → {full}");

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    locationPathName = outputPath,
                    options = buildOptions,
                });
            }
            catch (Exception e)
            {
                return $"BuildPipeline.BuildPlayer threw: {e.GetType().Name}: {e.Message}";
            }

            if (report == null)
                return "BuildPipeline.BuildPlayer returned no BuildReport.";

            var s = report.summary;
            Debug.Log($"{Tag} result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                      $"size={s.totalSize / (1024f * 1024f):F1} MB duration={(s.buildEndedAt - s.buildStartedAt).TotalSeconds:F0}s");

            if (s.result != BuildResult.Succeeded)
                return $"build {s.result} — {s.totalErrors} error(s). See the batchmode log.";

            // A "Succeeded" report with no Xcode project on disk would still hand a stale
            // project to xcodebuild. Confirm the artifact actually exists before claiming 0.
            var xcodeproj = Path.Combine(full, "Unity-iPhone.xcodeproj");
            if (!Directory.Exists(xcodeproj))
                return $"build reported Succeeded but {xcodeproj} does not exist.";

            LogGeneratedPlist(full);
            Debug.Log($"{Tag} SUCCEEDED → {xcodeproj}");
            return null;
        }

        /// <summary>
        /// Echo the values the upload actually depends on into the batchmode log, so a bad
        /// version/build number is visible in the fastlane output rather than in an App Store
        /// Connect rejection email 40 minutes later. Read straight off the generated
        /// Info.plist (plain XML) rather than PlayerSettings — BuildStampGenerator restores
        /// PlayerSettings.iOS.buildNumber in OnPostprocessBuild, so by now it reads the
        /// pre-build value, not what was baked. Diagnostic only: never fails the build.
        /// </summary>
        static void LogGeneratedPlist(string builtProject)
        {
            var plistPath = Path.Combine(builtProject, "Info.plist");
            try
            {
                if (!File.Exists(plistPath))
                {
                    Debug.LogWarning($"{Tag} Info.plist not found at {plistPath} — cannot report version/build number.");
                    return;
                }

                // A plist carries a DOCTYPE, and XDocument's default reader settings prohibit
                // DTDs outright — read through an explicit reader that ignores it instead.
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
                XDocument doc;
                using (var reader = XmlReader.Create(plistPath, settings))
                    doc = XDocument.Load(reader);

                // <dict><key>K</key><string>V</string>…</dict> — walk key/value pairs.
                var dict = doc.Root?.Element("dict");
                if (dict == null)
                {
                    Debug.LogWarning($"{Tag} Info.plist has no root <dict> — cannot report version/build number.");
                    return;
                }

                string Value(string key)
                {
                    var k = dict.Elements("key").FirstOrDefault(e => e.Value == key);
                    var v = k?.ElementsAfterSelf().FirstOrDefault();
                    if (v == null) return "<absent>";
                    // <true/> / <false/> are empty elements — the name IS the value.
                    var n = v.Name.LocalName;
                    return (n == "true" || n == "false") ? n : v.Value;
                }

                Debug.Log($"{Tag} Info.plist: CFBundleShortVersionString={Value("CFBundleShortVersionString")} " +
                          $"CFBundleVersion={Value("CFBundleVersion")} " +
                          $"CFBundleIdentifier={Value("CFBundleIdentifier")} " +
                          $"ITSAppUsesNonExemptEncryption={Value("ITSAppUsesNonExemptEncryption")}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} could not read {plistPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Drift gate: the hole SCENES are gitignored and per-machine, while physics reads the
        /// TRACKED tree bake. A machine whose scene and bake disagree ships invisible tree
        /// colliders — Hole 02 shipped 1,495 of them. Refuse to build on any mismatch, the same
        /// way the build-stamp guard refuses an upload regression.
        ///
        /// Escape hatch: -skipTreeBakeCheck on the Unity command line. Logged loudly, because a
        /// build produced with the gate disarmed is a build nobody verified the holes of.
        ///
        /// Returns null when the build may proceed, or the failure message.
        /// </summary>
        static string ValidateTreeBake()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-skipTreeBakeCheck") >= 0)
            {
                Debug.LogWarning($"{Tag} ############################################################");
                Debug.LogWarning($"{Tag} -skipTreeBakeCheck: TREE BAKE DRIFT GATE DISARMED.");
                Debug.LogWarning($"{Tag} Hole scenes were NOT checked against the committed bake.");
                Debug.LogWarning($"{Tag} This build may collide with trees it does not render.");
                Debug.LogWarning($"{Tag} ############################################################");
                Console.Error.WriteLine($"{Tag} WARNING: -skipTreeBakeCheck — tree bake drift gate disarmed.");
                return null;
            }

            Golfin.CourseImport.TreeBakeValidator.Report report;
            try
            {
                report = Golfin.CourseImport.TreeBakeValidator.ValidateAllHoles();
            }
            catch (Exception e)
            {
                // A gate that cannot run is not a gate that passed.
                return $"tree bake validation threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
            }

            string table = report.ToTable();
            if (report.AllPass)
            {
                Debug.Log($"{Tag} tree bake drift gate: PASS\n{table}");
                return null;
            }

            Debug.LogError($"{Tag} tree bake drift gate: FAIL\n{table}");
            Console.Error.WriteLine($"{Tag} tree bake drift gate: FAIL\n{table}");
            return $"tree bake drift: {report.FailCount} of {report.holes.Count} hole(s) disagree with the " +
                   "committed data. Run Import/Standalone Trees/Rebuild Current Hole on each listed hole " +
                   "(and Import/Bake Tree Obstacles/Validate All Holes to confirm), or re-run with " +
                   "-skipTreeBakeCheck if you accept shipping unverified holes.\n" + table;
        }

        static void Fail(string msg)
        {
            Debug.LogError($"{Tag} FAILED: {msg}");
            // Flush before the process dies — batchmode logs are buffered and an Exit() can
            // otherwise truncate the one line that explains the failure.
            Console.Error.WriteLine($"{Tag} FAILED: {msg}");
            Console.Error.Flush();
            EditorApplication.Exit(1);
        }
    }
}
