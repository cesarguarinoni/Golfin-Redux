using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GolfinRedux.AndroidEditor
{
    /// <summary>
    /// Android bring-up entry points (2026-09-15). Android had never been built in this
    /// project - the package name was still the Unity URP template default and no texture
    /// compression had been chosen. Split into two batchmode passes ON PURPOSE:
    ///
    ///   Setup()       - writes Player Settings, picks ASTC, then switches the active build
    ///                   target to Android. The switch reimports every texture in the
    ///                   project. ASTC is selected BEFORE the switch so that reimport
    ///                   happens once, not twice.
    ///   BuildDevApk() - activates the Dev-Android build profile and emits a development
    ///                   APK. Separate invocation so a build failure does not discard the
    ///                   import.
    ///
    /// Package name is com.nextinnovation.golfingame to match iOS: Redux ships as an update
    /// to the existing Play listing rather than as a new app.
    /// </summary>
    public static class AndroidSetup
    {
        const string PackageName = "com.nextinnovation.golfingame";
        const string ProfileDir  = "Assets/Settings/Build Profiles";
        const string ApkPath     = "Builds/Android-Dev/Golfin.apk";

        [MenuItem("GOLFIN/Android/1 - Setup + Switch Platform")]
        public static void Setup()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion    = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Assets/Plugins/Android/AndroidManifest.xml hardcodes
            // com.unity3d.player.UnityPlayerActivity (it carries the golfin: OAuth deep
            // link). Unity 6 defaults the entry point to GameActivity, which emits
            // UnityPlayerGameActivity instead - the merged manifest then points at a class
            // that is not in any dex and the APK installs but cannot be launched.
            // Pin the classic Activity entry point so manifest and dex agree.
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;

            // APK for sideloading / emulator. The AAB switch happens at Play upload time.
            EditorUserBuildSettings.buildAppBundle = false;

            // ASTC before the switch - see class summary.
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            AssetDatabase.SaveAssets();

            Debug.Log("[AndroidSetup] identifier=" + PlayerSettings.GetApplicationIdentifier(android)
                    + " backend=" + PlayerSettings.GetScriptingBackend(android)
                    + " arch=" + PlayerSettings.Android.targetArchitectures
                    + " minSdk=" + PlayerSettings.Android.minSdkVersion
                    + " targetSdk=" + PlayerSettings.Android.targetSdkVersion
                    + " texture=" + EditorUserBuildSettings.androidBuildSubtarget);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[AndroidSetup] switching active build target to Android - this reimports all textures.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Fail("SwitchActiveBuildTarget to Android returned false.");
                    return;
                }
            }

            Debug.Log("[AndroidSetup] DONE. activeBuildTarget=" + EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("GOLFIN/Android/2 - Build Dev APK")]
        public static void BuildDevApk()
        {
            // Unity 6: the ACTIVE BUILD PROFILE wins at editor launch, so a bare
            // SwitchActiveBuildTarget from a previous batchmode run does NOT survive a
            // relaunch while an iOS profile is still active. Activate the Android profile
            // first, then re-apply settings (which switches the target if needed).
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ApkPath)));

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfileDir + "/Dev-Android.asset");
            if (profile != null)
                BuildProfile.SetActiveBuildProfile(profile);

            Setup();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Fail("Still not on Android after profile activation + switch.");
                return;
            }

            BuildReport report;

            if (profile != null)
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
                {
                    buildProfile     = profile,
                    locationPathName = ApkPath,
                    options          = BuildOptions.Development | BuildOptions.AllowDebugging,
                });
            }
            else
            {
                Debug.LogWarning("[AndroidSetup] Dev-Android profile missing; using global scene list.");
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                report = BuildPipeline.BuildPlayer(scenes, ApkPath, BuildTarget.Android,
                    BuildOptions.Development | BuildOptions.AllowDebugging);
            }

            WriteReport(report);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail("Build " + report.summary.result + " - " + report.summary.totalErrors + " error(s).");
                return;
            }
            Debug.Log("[AndroidSetup] APK SUCCEEDED -> " + report.summary.outputPath
                    + " (" + (report.summary.totalSize / (1024f * 1024f)).ToString("F1") + " MB)");
        }

        static void WriteReport(BuildReport report)
        {
            var s  = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine("=== GOLFIN Android dev APK build report ===");
            sb.AppendLine("result     : " + s.result);
            sb.AppendLine("output     : " + s.outputPath);
            sb.AppendLine("total size : " + (s.totalSize / (1024f * 1024f)).ToString("F2") + " MB");
            sb.AppendLine("errors/warn: " + s.totalErrors + " / " + s.totalWarnings);
            sb.AppendLine("duration   : " + (s.buildEndedAt - s.buildStartedAt).TotalSeconds.ToString("F1") + "s");
            sb.AppendLine();
            sb.AppendLine("=== top 50 packed assets by size ===");
            foreach (var c in report.packedAssets.SelectMany(pa => pa.contents)
                                                 .OrderByDescending(c => c.packedSize)
                                                 .Take(50))
                sb.AppendLine((c.packedSize / 1024f).ToString("F1").PadLeft(10) + " KB  " + c.sourceAssetPath);

            Directory.CreateDirectory("Builds");
            File.WriteAllText(Path.Combine("Builds", "build-report-Android-Dev.txt"), sb.ToString());
        }

        static void Fail(string msg)
        {
            Debug.LogError("[AndroidSetup] " + msg);
            if (Environment.GetCommandLineArgs().Contains("-batchmode"))
                EditorApplication.Exit(1);
        }
    }
}
