// Assets/Tests/EditMode/StandaloneIdentityProfileTests.cs
// standalone_content_art_report — "is this the standalone?" asked about the profile being BUILT.
//
// Build 2873 (punch it standalone) rewrote Docs/Reports/content_art.txt with "2449 missing sprite
// reference(s)": the lane stashes the golf Resources for the duration of the build, and the
// catalog-art report ran inside that window. CIBuild.BuildIOSCore now skips the report for the
// shell, and it has to decide BEFORE BuildProfile.SetActiveBuildProfile — where the active profile
// is whatever the previous batchmode run left in Library/EditorUserBuildSettings.asset. A game
// lane run straight after a standalone one would have skipped its own report had the skip read
// the parameterless IsStandaloneIdentityBuild(). These pin the overload to the profile it is
// handed.

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace GolfinRedux.Tests.EditMode
{
    public class StandaloneIdentityProfileTests
    {
        const string PreprocessorTypeName = "Golfin.EditorTools.StandaloneBuildPreprocessor";
        const string StandaloneProfilePath = "Assets/Settings/Build Profiles/iOS-Standalone.asset";
        const string GameProfilePath       = "Assets/Settings/Build Profiles/iOS-Full.asset";
        const string GpsProfilePath        = "Assets/Settings/Build Profiles/iOS-Full-GPS.asset";

        static Type Preprocessor()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(PreprocessorTypeName);
                if (t != null) return t;
            }
            Assert.Fail($"{PreprocessorTypeName} not found — did Assembly-CSharp-Editor compile?");
            return null;
        }

        static bool IsStandalone(BuildProfile profile)
        {
            var m = Preprocessor().GetMethod("IsStandaloneIdentityBuild",
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BuildProfile) }, null);
            Assert.IsNotNull(m, "IsStandaloneIdentityBuild(BuildProfile) not found — the overload CIBuild judges the report skip by.");
            return (bool)m.Invoke(null, new object[] { profile });
        }

        static BuildProfile Load(string path)
        {
            var p = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
            Assert.IsNotNull(p, $"{path} not found — the lane constants in CIBuild name it.");
            return p;
        }

        [SetUp]
        public void BatchmodeOverrideIsOff()
        {
            // The override short-circuits the profile read; it is only ever set around a
            // BuildIOSStandalone run. If it is on here, some build left it on and every
            // assertion below would be vacuous.
            var f = Preprocessor().GetField("ForceStandaloneIdentity", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, "StandaloneBuildPreprocessor.ForceStandaloneIdentity not found.");
            Assert.IsFalse((bool)f.GetValue(null), "ForceStandaloneIdentity is set outside a standalone build.");
        }

        [Test]
        public void AnswersFromTheProfileHandedIn_NotTheActiveOne()
        {
            // Both cannot hold if the answer came from the active profile — whichever profile is
            // active, it is one profile, and these two disagree.
            Assert.IsTrue(IsStandalone(Load(StandaloneProfilePath)),
                "iOS-Standalone carries GOLFIN_STANDALONE; the shell lane must skip the catalog-art report.");
            Assert.IsFalse(IsStandalone(Load(GameProfilePath)),
                "iOS-Full is the game; its lane must keep writing Docs/Reports/content_art.txt.");
            Assert.IsFalse(IsStandalone(Load(GpsProfilePath)),
                "iOS-Full-GPS is the game with GOLFIN_GPS only; the GPS lane keeps the report too.");
        }

        [Test]
        public void NullProfile_IsNotTheStandalone()
        {
            // A missing profile fails the build a line later in BuildIOSCore; it must not ALSO
            // read as the shell and swallow the report on the way out.
            Assert.IsFalse(IsStandalone(null));
        }
    }
}
