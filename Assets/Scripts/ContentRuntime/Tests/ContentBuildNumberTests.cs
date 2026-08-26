using NUnit.Framework;

namespace Golfin.Content.Tests
{
    /// <summary>
    /// <c>min_build</c> depends on the client sending a build number the server can compare
    /// against, and SPEC §4 flagged that the two on-disk sources DISAGREED
    /// (ProjectSettings iPhone=2113 vs build_stamp.txt "(2297)"). The resolution — build_stamp.txt,
    /// the artifact the pipeline actually bakes into the binary — is pinned here.
    ///
    /// <para>
    /// <see cref="Parse_ReadsTheRealBundledStamp"/> is the important one: it parses the ACTUAL
    /// shipped file, so a change to the stamp format breaks a test instead of silently downgrading
    /// every client to build 0 and withholding all new content from every player.
    /// </para>
    /// </summary>
    public class ContentBuildNumberTests
    {
        [TearDown]
        public void TearDown() => ContentBuildNumber.ResetForTest();

        [Test]
        public void Parse_ExtractsTheParenthesisedBuild_FromACleanStamp()
        {
            Assert.AreEqual(2297, ContentBuildNumber.Parse("v1.5.7 (2297) 02c1678 · 08-26 06:56"));
        }

        [Test]
        public void Parse_ExtractsTheBuild_FromADirtyTreeStamp()
        {
            Assert.AreEqual(2297, ContentBuildNumber.Parse("v1.5.7 (2297) 02c1678+da58 · 08-26 06:56"),
                "The +diffHash suffix is present on every uncommitted build; it must not shadow the number.");
        }

        [Test]
        public void Parse_EditorFallbackStamp_IsZero_TheSafeEnd()
        {
            Assert.AreEqual(0, ContentBuildNumber.Parse("v1.5.7 (editor) · 08-26 06:56"),
                "BuildStampGenerator writes '(editor)' when git is unavailable. 0 makes the server " +
                "send only rows every build can render — the safe end. An over-estimate would hand " +
                "this build content it cannot draw.");
        }

        [Test]
        public void Parse_MissingOrJunk_IsZero_NotAnException()
        {
            Assert.AreEqual(0, ContentBuildNumber.Parse(null));
            Assert.AreEqual(0, ContentBuildNumber.Parse(""));
            Assert.AreEqual(0, ContentBuildNumber.Parse("build stamp unavailable"));
            Assert.AreEqual(0, ContentBuildNumber.Parse("v1.5.7 (0) abc"), "0 is not a real build.");
        }

        [Test]
        public void Parse_ReadsTheRealBundledStamp()
        {
            ContentBuildNumber.ResetForTest();

            int build = ContentBuildNumber.Current;

            Assert.Greater(build, 0,
                "Assets/Resources/Data/build_stamp.txt must carry a parenthesised build number. " +
                "If this fails, every client sends build=0 and the server withholds every row with " +
                "a min_build above 0 — new content would silently never reach anyone. " +
                "BuildStampGenerator bakes this file UNGATED on every build and refreshes it on " +
                "every play-mode enter, so it should always be present.");
        }

        [Test]
        public void Current_IsMemoised_BecauseItCannotChangeWithinASession()
        {
            ContentBuildNumber.ConfigureForTest(1234);
            Assert.AreEqual(1234, ContentBuildNumber.Current);
            Assert.AreEqual(1234, ContentBuildNumber.Current);
        }

        [Test]
        public void ConfigureForTest_ClampsNegativeToZero()
        {
            ContentBuildNumber.ConfigureForTest(-5);
            Assert.AreEqual(0, ContentBuildNumber.Current);
        }
    }
}
