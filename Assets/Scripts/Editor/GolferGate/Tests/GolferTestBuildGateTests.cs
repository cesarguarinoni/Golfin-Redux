// golfer_3d_test §9.6 — the gate proof, as an EditMode test instead of three iOS builds.
//
// WHY REFLECTION AND NOT A DIRECT REFERENCE. GolferTestBuildGate lives in
// Assets/Editor/GolferTestBuildGate.cs with no asmdef, so it compiles into the predefined
// Assembly-CSharp-Editor — and an assembly definition cannot reference a predefined assembly at
// all. Moving the gate under an asmdef would ripple through every other file in Assets/Editor,
// which is far past what §9.6 asked for. Reflection across that boundary is the established
// pattern in this repo for exactly this reason (GolferTestVerificationRecorder reaches
// ShotController the same way, because Golfin.Gameplay.Input is autoReferenced:false).
//
// WHAT IT PROVES. §6/§9.6 want evidence that the experiment does not ship by accident:
// _Test/Resources leaves the tree for an ordinary build, comes back afterwards, and stays only
// when something explicitly asks for it. The stash/restore is exercised for real — these tests
// move the actual folder — because a mocked move would prove nothing about the mechanism that
// keeps a 12.5k-triangle CC0 stand-in out of a release.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Golfin.GolferGate.Tests
{
    public sealed class GolferTestBuildGateTests
    {
        const string ResFolder   = "Assets/Art/3D/Characters/_Test/Resources";
        const string StashFolder = "Assets/_GolferTestStash/GolferTestRes";

        static Type GateType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType("Golfin.EditorTools.GolferTestBuildGate"); } catch { return null; } })
            .FirstOrDefault(t => t != null);

        static FieldInfo IncludeField =>
            GateType.GetField("IncludeTestAssets", BindingFlags.Static | BindingFlags.NonPublic);

        static bool IncludeTestAssets
        {
            get => (bool)IncludeField.GetValue(null);
            set => IncludeField.SetValue(null, value);
        }

        static bool IsGolferBuild() =>
            (bool)GateType.GetMethod("IsGolferBuild", BindingFlags.Static | BindingFlags.Public)
                          .Invoke(null, null);

        static void MoveOut() =>
            GateType.GetMethod("MoveTestAssetsOut", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);

        static void RestoreNow() =>
            GateType.GetMethod("RestoreNow", BindingFlags.Static | BindingFlags.Public)
                    .Invoke(null, null);

        bool _savedInclude;

        [SetUp]
        public void SetUp()
        {
            Assert.NotNull(GateType, "GolferTestBuildGate not found in any loaded assembly.");
            _savedInclude = IncludeTestAssets;
            // Never start from a stashed tree: a previous aborted run would make every
            // assertion below meaningless in a way that looks like a pass.
            RestoreNow();
        }

        [TearDown]
        public void TearDown()
        {
            // Restore FIRST, then the flag — a failed assertion must not leave the golfer
            // assets sitting in the stash folder for the next person who opens the project.
            RestoreNow();
            IncludeTestAssets = _savedInclude;
            Assert.IsTrue(AssetDatabase.IsValidFolder(ResFolder),
                          "TearDown failed to restore " + ResFolder + " — restore it by hand.");
        }

        [Test]
        public void IncludeTestAssets_ForcesAGolferBuild()
        {
            IncludeTestAssets = true;
            Assert.IsTrue(IsGolferBuild(),
                "IncludeTestAssets = true must make this a golfer build regardless of the active " +
                "profile — it is the override CIBuild.BuildIOSGolferTest sets in its try/finally.");
        }

        [Test]
        public void WithoutTheOverride_TheDecisionComesFromTheActiveProfile()
        {
            IncludeTestAssets = false;

            var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            var defines = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("Golfin.EditorTools.StandaloneBuildPreprocessor"); } catch { return null; } })
                .FirstOrDefault(t => t != null)
                ?.GetMethod("ProfileDefines", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                ?.Invoke(null, new object[] { profile }) as System.Collections.Generic.IEnumerable<string>;

            bool profileHasDefine = defines != null &&
                                    defines.Any(d => string.Equals(d, "GOLFIN_GOLFER_TEST", StringComparison.Ordinal));

            // Asserted as an equivalence rather than a fixed expectation, so the test is correct
            // on iOS-Full-GPS (define absent -> excluded) AND on iOS-Full-Golfer (present ->
            // included). Pinning it to one profile would just fail on whichever machine is set
            // up for the other.
            Assert.AreEqual(profileHasDefine, IsGolferBuild(),
                "With no override the gate must follow the ACTIVE profile's scripting defines. " +
                "profile=" + (profile == null ? "<classic>" : profile.name) +
                " hasDefine=" + profileHasDefine);
        }

        [Test]
        public void MoveOut_StashesTheResourcesFolder_AndRestorePutsItBack()
        {
            Assert.IsTrue(AssetDatabase.IsValidFolder(ResFolder),
                          "precondition: " + ResFolder + " must exist before the move");
            Assert.IsFalse(AssetDatabase.IsValidFolder(StashFolder),
                           "precondition: the stash must be empty before the move");

            MoveOut();

            // THE POINT OF THE WHOLE GATE. Anything under a /Resources/ segment ships in every
            // build whether or not a scene references it, so compiling GolferPresenter out is
            // necessary but not sufficient — the folder itself has to leave.
            Assert.IsFalse(AssetDatabase.IsValidFolder(ResFolder),
                           ResFolder + " is still in the tree after MoveTestAssetsOut — it would ship.");
            Assert.IsTrue(AssetDatabase.IsValidFolder(StashFolder),
                          "the folder went somewhere other than the stash");
            Assert.IsFalse(StashFolder.Contains("/Resources"),
                           "the stash path must not itself contain a Resources segment, or the " +
                           "move changes nothing and the experiment still ships");

            RestoreNow();

            Assert.IsTrue(AssetDatabase.IsValidFolder(ResFolder), "restore did not put the folder back");
            Assert.IsFalse(AssetDatabase.IsValidFolder(StashFolder), "restore left the stash behind");
        }

        [Test]
        public void RestoreIsIdempotent_AndSafeWhenNothingMoved()
        {
            // Every build entry point calls RestoreNow unconditionally, including CIBuild right
            // before it exits the process, so a no-op call must be harmless.
            Assert.DoesNotThrow(() => { RestoreNow(); RestoreNow(); });
            Assert.IsTrue(AssetDatabase.IsValidFolder(ResFolder));
        }

        [Test]
        public void TheGolferPrefabIsUnderAResourcesFolder()
        {
            // The premise the gate exists for. If this ever stops being true the gate is
            // solving a problem the project no longer has, and this test should be the thing
            // that says so.
            const string prefab = "Assets/Art/3D/Characters/_Test/Resources/GolferTest/PfGolfer_Test.prefab";
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefab),
                             prefab + " not found — the gate's whole reason to exist has moved.");
        }
    }
}
